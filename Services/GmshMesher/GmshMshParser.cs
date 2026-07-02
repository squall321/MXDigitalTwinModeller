using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.GmshMesher;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.GmshMesher
{
    /// <summary>
    /// Parser for Gmsh MSH v4.x ASCII format.
    /// </summary>
    // @lat: [[mesh#Gmsh Mesher#.msh 파서]]
    public static class GmshMshParser
    {
        public static GmshMeshData Parse(string mshPath, List<string> log)
        {
            if (!File.Exists(mshPath))
                throw new FileNotFoundException("MSH file not found: " + mshPath);

            var data = new GmshMeshData();
            string[] lines = File.ReadAllLines(mshPath);
            int i = 0;

            while (i < lines.Length)
            {
                string line = lines[i].Trim();

                if (line == "$MeshFormat")
                {
                    i = ParseMeshFormat(lines, i + 1, log);
                }
                else if (line == "$PhysicalNames")
                {
                    i = ParsePhysicalNames(lines, i + 1, data, log);
                }
                else if (line == "$Nodes")
                {
                    i = ParseNodes(lines, i + 1, data, log);
                }
                else if (line == "$Elements")
                {
                    i = ParseElements(lines, i + 1, data, log);
                }
                else
                {
                    i++;
                }
            }

            if (log != null)
            {
                log.Add(string.Format("[MshParser] 노드: {0}, 볼륨 요소: {1}, 표면 요소: {2}",
                    data.Nodes.Count, data.VolumeElements.Count, data.SurfaceElements.Count));
            }

            return data;
        }

        private static int ParseMeshFormat(string[] lines, int i, List<string> log)
        {
            // version-number file-type data-size
            if (i < lines.Length)
            {
                string[] parts = lines[i].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 1 && log != null)
                    log.Add("[MshParser] MSH 버전: " + parts[0]);
                i++;
            }
            // Skip until $EndMeshFormat
            while (i < lines.Length && lines[i].Trim() != "$EndMeshFormat")
                i++;
            return i + 1;
        }

        private static int ParsePhysicalNames(string[] lines, int i, GmshMeshData data, List<string> log)
        {
            int count = int.Parse(lines[i].Trim());
            i++;

            for (int j = 0; j < count && i < lines.Length; j++, i++)
            {
                string line = lines[i].Trim();
                if (line == "$EndPhysicalNames") break;

                // dimension tag "name"
                string[] parts = line.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    int dim = int.Parse(parts[0]);
                    int tag = int.Parse(parts[1]);
                    string name = parts[2].Trim('"');
                    data.PhysicalGroups.Add(new PhysicalGroup(dim, tag, name));
                }
            }

            while (i < lines.Length && lines[i].Trim() != "$EndPhysicalNames")
                i++;
            return i + 1;
        }

        private static int ParseNodes(string[] lines, int i, GmshMeshData data, List<string> log)
        {
            // numEntityBlocks numNodes minNodeTag maxNodeTag
            string[] header = lines[i].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int numEntityBlocks = int.Parse(header[0]);
            i++;

            for (int block = 0; block < numEntityBlocks && i < lines.Length; block++)
            {
                string blockLine = lines[i].Trim();
                if (blockLine == "$EndNodes") break;

                // entityDim entityTag parametric numNodesInBlock
                string[] bp = blockLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int numNodesInBlock = int.Parse(bp[3]);
                i++;

                // Read node tags
                int[] tags = new int[numNodesInBlock];
                for (int n = 0; n < numNodesInBlock && i < lines.Length; n++, i++)
                {
                    tags[n] = int.Parse(lines[i].Trim());
                }

                // Read node coordinates
                for (int n = 0; n < numNodesInBlock && i < lines.Length; n++, i++)
                {
                    string[] coords = lines[i].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    double x = double.Parse(coords[0], CultureInfo.InvariantCulture);
                    double y = double.Parse(coords[1], CultureInfo.InvariantCulture);
                    double z = double.Parse(coords[2], CultureInfo.InvariantCulture);
                    data.Nodes[tags[n]] = new MeshNode(tags[n], x, y, z);
                }
            }

            while (i < lines.Length && lines[i].Trim() != "$EndNodes")
                i++;
            return i + 1;
        }

        private static int ParseElements(string[] lines, int i, GmshMeshData data, List<string> log)
        {
            // numEntityBlocks numElements minElementTag maxElementTag
            string[] header = lines[i].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int numEntityBlocks = int.Parse(header[0]);
            i++;

            for (int block = 0; block < numEntityBlocks && i < lines.Length; block++)
            {
                string blockLine = lines[i].Trim();
                if (blockLine == "$EndElements") break;

                // entityDim entityTag elementType numElementsInBlock
                string[] bp = blockLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int entityDim = int.Parse(bp[0]);
                int entityTag = int.Parse(bp[1]);
                int elemType = int.Parse(bp[2]);
                int numElemsInBlock = int.Parse(bp[3]);
                i++;

                // Check if this is a supported element type
                // (MSH codes: 2=Tri3, 4=Tet4, 5=Hex8, 9=Tri6, 11=Tet10 — 9 was previously
                // mistaken for Tet10, silently dropping all quadratic tets.)
                bool isSupported = elemType == 2 || elemType == 4 || elemType == 5
                    || elemType == 9 || elemType == 11;

                for (int e = 0; e < numElemsInBlock && i < lines.Length; e++, i++)
                {
                    if (!isSupported) continue;

                    string[] parts = lines[i].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    int elemId = int.Parse(parts[0]);
                    int[] nodeIds = new int[parts.Length - 1];
                    for (int n = 1; n < parts.Length; n++)
                        nodeIds[n - 1] = int.Parse(parts[n]);

                    var elem = new MeshElement(elemId, (GmshElementType)elemType, entityTag, nodeIds);

                    if (entityDim == 3)
                        data.VolumeElements.Add(elem);
                    else if (entityDim == 2)
                        data.SurfaceElements.Add(elem);
                }
            }

            while (i < lines.Length && lines[i].Trim() != "$EndElements")
                i++;
            return i + 1;
        }
    }
}
