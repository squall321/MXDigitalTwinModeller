using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.GmshMesher;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.GmshMesher
{
    /// <summary>
    /// Orchestrator service for the Gmsh meshing pipeline.
    /// STEP export → .geo generation → Gmsh execution → .msh parsing
    /// </summary>
    public static class GmshMeshService
    {
        private static readonly List<string> _log = new List<string>();
        private static GmshMeshData _lastMeshData;
        private static IGmshEngine _engine;

        public static List<string> DiagnosticLog { get { return _log; } }
        public static GmshMeshData LastMeshData { get { return _lastMeshData; } }

        public static IGmshEngine Engine
        {
            get
            {
                if (_engine == null)
                    _engine = new GmshCliEngine();
                return _engine;
            }
            set { _engine = value; }
        }

        /// <summary>
        /// Run the full meshing pipeline:
        /// 1. Export STEP from SpaceClaim Part
        /// 2. Generate .geo script
        /// 3. Run Gmsh
        /// 4. Parse .msh output
        /// </summary>
        /// <returns>Parsed mesh data, or null on failure</returns>
        public static GmshMeshData RunMeshPipeline(Part part, List<IDesignBody> bodies, GmshMeshParameters meshParams)
        {
            _log.Clear();
            _lastMeshData = null;

            string errorMsg;
            if (!meshParams.Validate(out errorMsg))
            {
                Log("ERROR: 파라미터 검증 실패 — " + errorMsg);
                return null;
            }

            if (!Engine.IsAvailable)
            {
                Log("ERROR: Gmsh 엔진을 사용할 수 없습니다. gmsh.exe 경로를 확인하세요.");
                Log("검색 경로: AddIn 폴더/Libs/gmsh/gmsh.exe 또는 시스템 PATH");
                return null;
            }
            Log("Gmsh 버전: " + Engine.Version);

            // Create temp directory
            string tempDir = Path.Combine(Path.GetTempPath(), "MXGmsh_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(tempDir);
            Log("임시 폴더: " + tempDir);

            string stepPath = Path.Combine(tempDir, "input.step");
            string geoPath = Path.Combine(tempDir, "mesh.geo");
            string mshPath = Path.Combine(tempDir, "mesh.msh");

            try
            {
                // Step 1: Export STEP
                Log("── Step 1: STEP 내보내기 ──");
                ExportStep(part, stepPath);

                if (!File.Exists(stepPath))
                {
                    Log("ERROR: STEP 파일 생성 실패");
                    return null;
                }
                Log("STEP 파일 크기: " + new FileInfo(stepPath).Length + " bytes");

                // Step 2: Generate .geo
                Log("── Step 2: .geo 스크립트 생성 ──");
                GmshGeoWriter.WriteGeoFile(geoPath, stepPath, meshParams, _log);

                // Step 3: Run Gmsh
                Log("── Step 3: Gmsh 실행 ──");
                bool success = Engine.RunMesh(geoPath, mshPath, _log);
                if (!success)
                {
                    Log("ERROR: Gmsh 메쉬 생성 실패");
                    return null;
                }

                // Step 4: Parse .msh
                Log("── Step 4: .msh 파싱 ──");
                _lastMeshData = GmshMshParser.Parse(mshPath, _log);

                Log(string.Format("완료: 노드 {0}개, 볼륨 요소 {1}개, 표면 요소 {2}개",
                    _lastMeshData.TotalNodeCount,
                    _lastMeshData.VolumeElements.Count,
                    _lastMeshData.SurfaceElements.Count));

                return _lastMeshData;
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Extract surface triangles from volume elements for visualization.
        /// Uses face-based extraction: each tet face is checked for uniqueness.
        /// Shared faces (appearing twice) are internal; unique faces are boundary.
        /// </summary>
        public static List<int[]> ExtractSurfaceTriangles(GmshMeshData data)
        {
            if (data == null) return new List<int[]>();

            // If surface elements already exist, use them directly
            if (data.SurfaceElements.Count > 0)
            {
                var tris = new List<int[]>();
                foreach (var elem in data.SurfaceElements)
                {
                    if (elem.Type == GmshElementType.Tri3)
                    {
                        tris.Add(new[] { elem.NodeIds[0], elem.NodeIds[1], elem.NodeIds[2] });
                    }
                }
                if (tris.Count > 0) return tris;
            }

            // Fallback: extract boundary faces from volume elements
            var faceCount = new Dictionary<string, int[]>();

            foreach (var elem in data.VolumeElements)
            {
                int[][] faces = GetTetFaces(elem);
                if (faces == null) continue;

                foreach (var face in faces)
                {
                    string key = GetFaceKey(face);
                    if (!faceCount.ContainsKey(key))
                        faceCount[key] = face;
                    else
                        faceCount[key] = null; // Mark as internal (shared)
                }
            }

            var result = new List<int[]>();
            foreach (var kvp in faceCount)
            {
                if (kvp.Value != null)
                    result.Add(kvp.Value);
            }

            return result;
        }

        private static int[][] GetTetFaces(MeshElement elem)
        {
            if (elem.Type == GmshElementType.Tet4 || elem.Type == GmshElementType.Tet10)
            {
                int n0 = elem.NodeIds[0], n1 = elem.NodeIds[1], n2 = elem.NodeIds[2], n3 = elem.NodeIds[3];
                return new[]
                {
                    new[] { n0, n2, n1 },
                    new[] { n0, n1, n3 },
                    new[] { n1, n2, n3 },
                    new[] { n0, n3, n2 }
                };
            }
            if (elem.Type == GmshElementType.Hex8)
            {
                int n0 = elem.NodeIds[0], n1 = elem.NodeIds[1], n2 = elem.NodeIds[2], n3 = elem.NodeIds[3];
                int n4 = elem.NodeIds[4], n5 = elem.NodeIds[5], n6 = elem.NodeIds[6], n7 = elem.NodeIds[7];
                return new[]
                {
                    new[] { n0, n3, n2 }, new[] { n0, n2, n1 }, // bottom
                    new[] { n4, n5, n6 }, new[] { n4, n6, n7 }, // top
                    new[] { n0, n1, n5 }, new[] { n0, n5, n4 }, // front
                    new[] { n2, n3, n7 }, new[] { n2, n7, n6 }, // back
                    new[] { n0, n4, n7 }, new[] { n0, n7, n3 }, // left
                    new[] { n1, n2, n6 }, new[] { n1, n6, n5 }  // right
                };
            }
            return null;
        }

        private static string GetFaceKey(int[] face)
        {
            int[] sorted = (int[])face.Clone();
            Array.Sort(sorted);
            return string.Format("{0}_{1}_{2}", sorted[0], sorted[1], sorted[2]);
        }

        private static void ExportStep(Part part, string stepPath)
        {
            part.Export(PartExportFormat.Step, stepPath, true, null);
            _log.Add("[Export] STEP 내보내기 완료: " + stepPath);
        }

        private static void Log(string message)
        {
            _log.Add("[MeshService] " + message);
        }
    }
}
