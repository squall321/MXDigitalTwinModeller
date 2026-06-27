using System;
using System.Collections.Generic;
using System.Linq;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.GmshMesher;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.GmshMesher
{
    /// <summary>
    /// Creates SpaceClaim DesignMesh previews from Gmsh mesh data.
    /// Coordinates in .msh are meters (from STEP), SpaceClaim uses meters → no conversion needed.
    /// </summary>
    // @lat: [[mesh#Gmsh Mesher#Mesh Visualization]]
    public static class MeshVisualizationService
    {
        private const string PreviewMeshName = "GmshMeshPreview";
        private static DesignMesh _lastPreview;

        /// <summary>
        /// Create a preview mesh in SpaceClaim from Gmsh mesh data surface triangles.
        /// Must be called inside WriteBlock.ExecuteTask().
        /// </summary>
        public static DesignMesh CreatePreviewMesh(Part part, GmshMeshData meshData, List<string> log)
        {
            // Get surface triangles
            List<int[]> triangles = GmshMeshService.ExtractSurfaceTriangles(meshData);

            if (triangles.Count == 0)
            {
                if (log != null) log.Add("[Preview] 표면 삼각형이 없습니다.");
                return null;
            }

            // Clear existing preview
            ClearPreviewMesh(part);

            // Build vertex and facet arrays for DesignMesh.Create(Part, string, float[], int[])
            // We need to remap node IDs to sequential 0-based indices
            var nodeIdToIndex = new Dictionary<int, int>();
            var vertexList = new List<float>();

            foreach (var tri in triangles)
            {
                foreach (int nodeId in tri)
                {
                    if (!nodeIdToIndex.ContainsKey(nodeId))
                    {
                        MeshNode node;
                        if (!meshData.Nodes.TryGetValue(nodeId, out node))
                            continue;

                        int index = nodeIdToIndex.Count;
                        nodeIdToIndex[nodeId] = index;
                        // Coordinates in meters (matching SpaceClaim internal units)
                        vertexList.Add((float)node.X);
                        vertexList.Add((float)node.Y);
                        vertexList.Add((float)node.Z);
                    }
                }
            }

            // Build facet indices (0-based)
            var facetList = new List<int>();
            foreach (var tri in triangles)
            {
                int i0, i1, i2;
                if (!nodeIdToIndex.TryGetValue(tri[0], out i0)) continue;
                if (!nodeIdToIndex.TryGetValue(tri[1], out i1)) continue;
                if (!nodeIdToIndex.TryGetValue(tri[2], out i2)) continue;
                facetList.Add(i0);
                facetList.Add(i1);
                facetList.Add(i2);
            }

            float[] vertices = vertexList.ToArray();
            int[] facets = facetList.ToArray();

            if (log != null)
                log.Add(string.Format("[Preview] 정점: {0}, 삼각형: {1}",
                    vertices.Length / 3, facets.Length / 3));

            DesignMesh dm = DesignMesh.Create(part, PreviewMeshName, vertices, facets);
            _lastPreview = dm;

            if (log != null)
                log.Add("[Preview] DesignMesh 생성 완료");

            return dm;
        }

        /// <summary>
        /// Remove existing preview mesh from the part.
        /// Must be called inside WriteBlock.ExecuteTask().
        /// </summary>
        public static void ClearPreviewMesh(Part part)
        {
            if (_lastPreview != null)
            {
                try { _lastPreview.Delete(); } catch { }
                _lastPreview = null;
            }
        }
    }
}
