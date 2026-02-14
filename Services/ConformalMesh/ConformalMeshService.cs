using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpaceClaim.Api.V252.Analysis;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ConformalMesh;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Export;
using SpaceClaim.Api.V252.Scripting.Commands;
using SpaceClaim.Api.V252.Scripting.Commands.CommandOptions;
using SpaceClaim.Api.V252.Scripting.Selection;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ConformalMesh
{
    /// <summary>
    /// STEP 파일에서 Conformal Mesh를 생성하는 서비스.
    /// Spatial Index 기반 고속 계면 검출 + Share Topology + SpaceClaim Hex Mesher.
    /// </summary>
    public static class ConformalMeshService
    {
        private const double NormalTolerance = 0.985;   // anti-parallel (≈±10°)
        private const double CylAxisTolerance = 0.99;   // cylinder axis parallel (≈±8°)
        private const double CylRadiusTolM = 5e-5;      // 0.05mm
        private const double CylAxisDistTolM = 5e-5;    // 0.05mm

        /// <summary>진단 로그</summary>
        public static List<string> DiagnosticLog { get; private set; }

        // ================================================================
        // 전체 워크플로우
        // ================================================================

        /// <summary>
        /// Conformal Mesh 전체 워크플로우 실행.
        /// </summary>
        public static List<InterfacePairInfo> ExecuteWorkflow(Part part, ConformalMeshParameters p)
        {
            DiagnosticLog = new List<string>();
            Log("=== Conformal Mesh Workflow 시작 ===");

            // Step 1: STEP 임포트
            if (p.ImportMode != StepImportMode.UseCurrentPart)
            {
                part = ImportStep(p.StepFilePath, p.ImportMode);
                if (part == null)
                {
                    Log("[ERROR] STEP 임포트 실패");
                    return new List<InterfacePairInfo>();
                }
            }

            // Step 2: 바디 수집
            var bodies = CollectBodies(part, p.BodyKeyword);
            Log(string.Format("바디 수: {0}", bodies.Count));
            if (bodies.Count < 2)
            {
                Log("[WARN] 바디가 2개 미만. 계면 검출 불가.");
                return new List<InterfacePairInfo>();
            }

            // Step 3-4: 계면 검출
            var interfaces = DetectInterfaces(bodies, p.ToleranceMm, p.DetectPlanar, p.DetectCylindrical);
            Log(string.Format("검출된 계면: {0}개", interfaces.Count));

            // Step 5: Named Selection 생성
            if (p.CreateInterfaceNamedSelections && interfaces.Count > 0)
            {
                CreateInterfaceNS(part, interfaces);
                Log(string.Format("Named Selection 생성: {0}개 그룹", interfaces.Count));
            }

            // Step 6: Share Topology
            if (p.EnableShareTopology)
            {
                EnableShareTopology(part);
                Log("Share Topology 활성화");
            }

            // Step 7-8: 실린더 엣지 분할
            if (p.SplitCylinderEdges)
            {
                int splitCount = SplitCylinderEdges(bodies, p.CylinderEdgeDivisions);
                Log(string.Format("실린더 엣지 분할: {0}개", splitCount));
            }

            // Step 9: 메쉬 생성
            bool meshOk = GenerateConformalMesh(part, bodies, p);
            Log(meshOk ? "메쉬 생성 완료" : "[ERROR] 메쉬 생성 실패");

            // Step 10: 내보내기
            if (p.AutoExport && meshOk && !string.IsNullOrEmpty(p.ExportPath))
            {
                ExportMesh(p.ExportPath, p.ExportFormat);
                Log(string.Format("내보내기: {0}", p.ExportPath));
            }

            Log("=== Conformal Mesh Workflow 완료 ===");
            return interfaces;
        }

        // ================================================================
        // Step 1: STEP 임포트
        // ================================================================

        /// <summary>
        /// STEP 파일을 로드한다.
        /// </summary>
        public static Part ImportStep(string filePath, StepImportMode mode)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                Log(string.Format("[ERROR] 파일 없음: {0}", filePath ?? "(null)"));
                return null;
            }

            Log(string.Format("STEP 로드: {0}", filePath));

            try
            {
                if (mode == StepImportMode.OpenNew)
                {
                    var doc = Document.Open(filePath, null);
                    if (doc != null)
                    {
                        Log("새 문서로 열기 완료");
                        return doc.MainPart;
                    }
                }
                else if (mode == StepImportMode.InsertIntoCurrent)
                {
                    var activeDoc = Window.ActiveWindow.Document;
                    if (activeDoc != null)
                    {
                        // 컴포넌트로 삽입
                        var importDoc = Document.Open(filePath, null);
                        if (importDoc != null)
                        {
                            Log("컴포넌트로 삽입 완료");
                            return activeDoc.MainPart;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log(string.Format("[ERROR] 임포트 실패: {0}", ex.Message));
            }

            return null;
        }

        // ================================================================
        // Step 2: 바디 수집
        // ================================================================

        /// <summary>
        /// Part에서 모든 DesignBody를 재귀 수집. keyword로 필터 가능.
        /// </summary>
        public static List<DesignBody> CollectBodies(Part part, string keyword)
        {
            var result = new List<DesignBody>();
            CollectBodiesRecursive(part, result);

            if (!string.IsNullOrEmpty(keyword))
            {
                string kw = keyword.ToLowerInvariant();
                result = result.Where(b => (b.Name ?? "").ToLowerInvariant().Contains(kw)).ToList();
            }

            return result;
        }

        private static void CollectBodiesRecursive(IPart part, List<DesignBody> result)
        {
            if (part == null) return;

            foreach (var body in part.Bodies)
            {
                var db = body as DesignBody;
                if (db != null)
                    result.Add(db);
            }

            foreach (var comp in part.Components)
            {
                try
                {
                    if (comp.Content != null)
                        CollectBodiesRecursive(comp.Content, result);
                }
                catch { }
            }
        }

        // ================================================================
        // Step 3-4: 계면 검출 (Spatial Index 가속)
        // ================================================================

        /// <summary>
        /// 모든 바디 간 계면을 검출.
        /// Spatial Index로 O(n²)→O(n·k) 가속.
        /// </summary>
        public static List<InterfacePairInfo> DetectInterfaces(
            List<DesignBody> bodies, double toleranceMm,
            bool detectPlanar, bool detectCylindrical)
        {
            double tolM = toleranceMm * 1e-3;  // mm → m
            Log(string.Format("계면 검출: {0}개 바디, 허용거리={1:F2}mm", bodies.Count, toleranceMm));

            // Body-level AABB 계산
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var bounds = new List<SpatialIndex.BodyBounds>(bodies.Count);
            for (int i = 0; i < bodies.Count; i++)
            {
                bounds.Add(SpatialIndex.ComputeBounds(bodies[i], i, tolM));
            }
            Log(string.Format("  AABB 계산: {0}ms", sw.ElapsedMilliseconds));

            // Spatial Index 구축 + 이웃 쌍 추출
            sw.Restart();
            double cellSize = SpatialIndex.ComputeCellSize(bounds);
            var neighborPairs = SpatialIndex.GetNeighborPairs(bounds, cellSize);
            Log(string.Format("  Spatial Index: 셀크기={0:E3}m, 이웃쌍={1}개, {2}ms",
                cellSize, neighborPairs.Count, sw.ElapsedMilliseconds));

            // Face-level 매칭
            sw.Restart();
            var result = new List<InterfacePairInfo>();
            int totalFacePairsChecked = 0;

            foreach (var pair in neighborPairs)
            {
                var bodyA = bodies[pair.Key];
                var bodyB = bodies[pair.Value];

                var iface = FindInterfaceBetween(bodyA, bodyB, tolM,
                    detectPlanar, detectCylindrical, ref totalFacePairsChecked);

                if (iface != null)
                {
                    iface.GroupName = string.Format("Interface_{0}_{1}",
                        SanitizeName(bodyA.Name), SanitizeName(bodyB.Name));
                    result.Add(iface);
                }
            }

            Log(string.Format("  Face 매칭: {0}개 계면, {1}개 쌍 검사, {2}ms",
                result.Count, totalFacePairsChecked, sw.ElapsedMilliseconds));

            return result;
        }

        /// <summary>
        /// 두 바디 간 계면을 찾는다.
        /// </summary>
        private static InterfacePairInfo FindInterfaceBetween(
            DesignBody bodyA, DesignBody bodyB, double tolM,
            bool checkPlanar, bool checkCylindrical, ref int pairsChecked)
        {
            var facesA = new List<DesignFace>();
            var facesB = new List<DesignFace>();
            double totalArea = 0;
            bool hasPlanar = false;
            bool hasCylindrical = false;

            var aFaces = bodyA.Faces.ToArray();
            var bFaces = bodyB.Faces.ToArray();

            for (int i = 0; i < aFaces.Length; i++)
            {
                var fA = aFaces[i];
                var geomA = fA.Shape.Geometry;

                for (int j = 0; j < bFaces.Length; j++)
                {
                    pairsChecked++;
                    var fB = bFaces[j];
                    var geomB = fB.Shape.Geometry;

                    // 평면 접촉
                    if (checkPlanar && geomA is Plane && geomB is Plane)
                    {
                        if (ArePlanarFacesMatching(fA, (Plane)geomA, fB, (Plane)geomB, tolM))
                        {
                            facesA.Add(fA);
                            facesB.Add(fB);
                            double area = Math.Min(fA.Area, fB.Area) * 1e6; // m² → mm²
                            totalArea += area;
                            hasPlanar = true;
                        }
                    }
                    // 원통 접촉
                    else if (checkCylindrical && geomA is Cylinder && geomB is Cylinder)
                    {
                        if (AreCylinderFacesMatching(fA, (Cylinder)geomA, fB, (Cylinder)geomB, tolM))
                        {
                            facesA.Add(fA);
                            facesB.Add(fB);
                            double area = Math.Min(fA.Area, fB.Area) * 1e6;
                            totalArea += area;
                            hasCylindrical = true;
                        }
                    }
                }
            }

            if (facesA.Count == 0) return null;

            InterfaceType type;
            if (hasPlanar && hasCylindrical) type = InterfaceType.Mixed;
            else if (hasCylindrical) type = InterfaceType.Cylindrical;
            else type = InterfaceType.Planar;

            return new InterfacePairInfo
            {
                BodyA = bodyA,
                BodyB = bodyB,
                FacesA = facesA,
                FacesB = facesB,
                Type = type,
                TotalAreaMm2 = totalArea,
                IsSelected = true
            };
        }

        // ── 평면 면 매칭 ──

        private static bool ArePlanarFacesMatching(
            DesignFace fA, Plane planeA,
            DesignFace fB, Plane planeB,
            double tolM)
        {
            // 법선 추출 (reversed 반영)
            Vector nA = GetFaceNormal(fA, planeA);
            Vector nB = GetFaceNormal(fB, planeB);

            // 1. 법선 반대 방향 체크
            double dot = VecDot(nA, nB);
            if (dot > -NormalTolerance) return false;

            // 2. 평면 거리 체크
            Point oA = planeA.Frame.Origin;
            Point oB = planeB.Frame.Origin;
            Vector diff = oB - oA;
            double planeDist = Math.Abs(VecDot(diff, Normalize(nA)));
            if (planeDist > tolM) return false;

            // 3. AABB 겹침 (간략 체크 - 면 중심 간 거리 기반)
            Point cA = GetFaceCenter(fA);
            Point cB = GetFaceCenter(fB);
            double dx = cA.X - cB.X, dy = cA.Y - cB.Y, dz = cA.Z - cB.Z;
            double centerDist = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            // 면 크기의 합보다 중심 거리가 크면 겹침 불가
            double radiusA = Math.Sqrt(fA.Area / Math.PI);
            double radiusB = Math.Sqrt(fB.Area / Math.PI);
            if (centerDist > radiusA + radiusB + tolM) return false;

            return true;
        }

        // ── 원통 면 매칭 ──

        private static bool AreCylinderFacesMatching(
            DesignFace fA, Cylinder cylA,
            DesignFace fB, Cylinder cylB,
            double tolM)
        {
            Vector axisA = cylA.Frame.DirZ.UnitVector;
            Vector axisB = cylB.Frame.DirZ.UnitVector;

            // 1. 축 평행 체크
            double axisDot = Math.Abs(VecDot(axisA, axisB));
            if (axisDot < CylAxisTolerance) return false;

            // 2. 반지름 매칭
            double radiusDiff = Math.Abs(cylA.Radius - cylB.Radius);
            if (radiusDiff > CylRadiusTolM) return false;

            // 3. 축 동축 체크 (수직 거리)
            Point oA = cylA.Frame.Origin;
            Point oB = cylB.Frame.Origin;
            Vector diff = oB - oA;
            double projOnAxis = VecDot(diff, Normalize(axisA));
            Vector parallelComp = Scale(Normalize(axisA), projOnAxis);
            Vector perpComp = diff - parallelComp;
            double perpDist = VecMag(perpComp);
            if (perpDist > CylAxisDistTolM) return false;

            // 4. 축 방향 겹침 체크 (바운딩 박스 기반 간략)
            var bbA = fA.Shape.GetBoundingBox(Matrix.Identity);
            var bbB = fB.Shape.GetBoundingBox(Matrix.Identity);
            if (!BBoxOverlap(bbA, bbB, tolM)) return false;

            return true;
        }

        // ================================================================
        // Step 5: Named Selection 생성
        // ================================================================

        /// <summary>
        /// 검출된 계면에 대해 Named Selection을 생성한다.
        /// </summary>
        public static void CreateInterfaceNS(Part part, List<InterfacePairInfo> interfaces)
        {
            int created = 0;
            foreach (var iface in interfaces)
            {
                if (!iface.IsSelected) continue;
                if (string.IsNullOrEmpty(iface.GroupName)) continue;

                // A측 면 + B측 면을 하나의 NS 그룹으로 생성
                var allFaces = new List<DesignFace>();
                allFaces.AddRange(iface.FacesA);
                allFaces.AddRange(iface.FacesB);

                if (allFaces.Count > 0)
                {
                    FaceNamingHelper.NameFaces(part, allFaces, iface.GroupName);
                    created++;
                }
            }

            Log(string.Format("NS 생성 완료: {0}개", created));
        }

        // ================================================================
        // Step 6: Share Topology
        // ================================================================

        /// <summary>
        /// Part의 Share Topology를 활성화한다.
        /// </summary>
        public static void EnableShareTopology(Part part)
        {
            try
            {
                // SpaceClaim API: Part.ShareTopology 속성
                // ShareTopologyType 값 설정 시도
                var propInfo = part.GetType().GetProperty("ShareTopology");
                if (propInfo != null)
                {
                    // 리플렉션으로 enum 값 탐색
                    var enumType = propInfo.PropertyType;
                    var values = Enum.GetValues(enumType);
                    // "AutomaticPerPart" 또는 마지막 값 (가장 적극적인 공유) 사용
                    object bestValue = null;
                    foreach (var v in values)
                    {
                        string name = v.ToString();
                        if (name.Contains("Automatic") || name.Contains("Share") || name.Contains("All"))
                        {
                            bestValue = v;
                        }
                    }
                    if (bestValue == null && values.Length > 1)
                        bestValue = values.GetValue(values.Length - 1);

                    if (bestValue != null)
                    {
                        propInfo.SetValue(part, bestValue, null);
                        Log(string.Format("ShareTopology = {0}", bestValue));
                    }
                    else
                    {
                        Log("[WARN] ShareTopology enum 값을 찾을 수 없음");
                    }
                }
                else
                {
                    Log("[WARN] Part.ShareTopology 속성을 찾을 수 없음");
                }
            }
            catch (Exception ex)
            {
                Log(string.Format("[WARN] ShareTopology 설정 실패: {0}", ex.Message));
            }
        }

        // ================================================================
        // Step 7-8: 실린더 엣지 분할
        // ================================================================

        /// <summary>
        /// 원형 엣지를 분할하여 Hex Blocking Decomposition 지원.
        /// </summary>
        public static int SplitCylinderEdges(List<DesignBody> bodies, int divisions)
        {
            if (divisions < 3) divisions = 8;
            int totalSplits = 0;

            // 원형 엣지를 수집하여 에지 사이징으로 분할 수 제어
            var circleEdges = new List<DesignEdge>();
            foreach (var body in bodies)
            {
                foreach (var face in body.Faces)
                {
                    if (!(face.Shape.Geometry is Cylinder)) continue;

                    foreach (var edge in face.Edges)
                    {
                        try
                        {
                            if (edge.Shape.Geometry is Circle)
                                circleEdges.Add(edge);
                        }
                        catch { }
                    }
                }
            }

            if (circleEdges.Count == 0) return 0;

            try
            {
                // 에지 사이즈 컨트롤로 분할 수 제어
                var docObjects = circleEdges.Cast<IDocObject>().ToArray();
                var edgeSel = Selection.Create(docObjects);

                // 원의 둘레를 분할 수로 나눈 크기를 에지 사이징으로 적용
                // 이를 통해 원형 엣지가 지정된 분할 수에 가까운 요소로 분할됨
                double avgRadius = 0;
                int count = 0;
                foreach (var edge in circleEdges)
                {
                    try
                    {
                        var circle = (Circle)edge.Shape.Geometry;
                        avgRadius += circle.Radius;
                        count++;
                    }
                    catch { }
                }
                if (count > 0) avgRadius /= count;

                double circumference = 2.0 * Math.PI * avgRadius;
                double edgeSize = circumference / divisions;

                var options = new CreateEdgeSizeControlOptions();
                options.ElementSize = edgeSize;
                CreateEdgeSizeControl.Execute(edgeSel, options, null);

                totalSplits = circleEdges.Count;
                Log(string.Format("  원형 엣지 사이징: {0}개, 크기={1:E3}m ({2}분할)",
                    circleEdges.Count, edgeSize, divisions));
            }
            catch (Exception ex)
            {
                Log(string.Format("[WARN] 원형 엣지 사이징 실패: {0}", ex.Message));
            }

            return totalSplits;
        }

        // ================================================================
        // Step 9: 메쉬 생성
        // ================================================================

        /// <summary>
        /// Conformal Mesh를 생성한다.
        /// </summary>
        public static bool GenerateConformalMesh(
            Part part, List<DesignBody> bodies, ConformalMeshParameters p)
        {
            if (bodies.Count == 0)
            {
                Log("[ERROR] 메쉬할 바디 없음");
                return false;
            }

            try
            {
                // 메쉬 초기화
                InitMeshSettings.Execute(PhysicsType.Structural, null);
                Log("  InitMeshSettings 완료");

                double elemSizeM = p.ElementSizeMm / 1000.0;

                // 바디별 메쉬 타입 설정
                if (p.Strategy == MeshStrategy.Mixed || p.Strategy == MeshStrategy.AutoHex)
                {
                    SetBodyMeshTypes(bodies, p.Strategy);
                }

                // CreateMesh 옵션
                var options = new CreateMeshOptions();
                options.ElementSize = elemSizeM;
                options.MidsideNodes = p.MidsideNodes
                    ? MidsideNodesType.Kept
                    : MidsideNodesType.Dropped;
                options.GrowthRate = p.GrowthRate;
                options.SizeFunctionType = p.UseCurvatureProximity
                    ? SizeFunctionType.CurvatureAndProximity
                    : SizeFunctionType.Fixed;

                switch (p.Strategy)
                {
                    case MeshStrategy.AutoTet:
                        options.SolidElementShape = ElementShapeType.Tetrahedral;
                        break;
                    case MeshStrategy.AutoHex:
                        options.SolidElementShape = ElementShapeType.Hexahedral;
                        break;
                    default:
                        options.SolidElementShape = ElementShapeType.Tetrahedral;
                        break;
                }

                // 배치 메쉬 시도
                var docObjects = bodies.Cast<IDocObject>().ToArray();
                var bodySel = Selection.Create(docObjects);
                var emptySel = Selection.Empty();

                Log(string.Format("  메쉬 생성: {0}개 바디, 크기={1:F2}mm, 형상={2}",
                    bodies.Count, p.ElementSizeMm, options.SolidElementShape));

                var result = CreateMesh.Execute(bodySel, emptySel, options, null);
                if (result.Success)
                {
                    Log("  배치 메쉬 성공");
                    return true;
                }

                // 배치 실패 시 개별 메쉬 폴백
                Log("  배치 실패 → 개별 메쉬 폴백");
                int success = 0, fail = 0;
                foreach (var body in bodies)
                {
                    try
                    {
                        var singleSel = Selection.Create(new IDocObject[] { body });
                        var r = CreateMesh.Execute(singleSel, emptySel, options, null);
                        if (r.Success)
                            success++;
                        else
                            fail++;
                    }
                    catch
                    {
                        fail++;
                    }
                }

                Log(string.Format("  개별 메쉬: 성공={0}, 실패={1}", success, fail));
                return fail == 0;
            }
            catch (Exception ex)
            {
                Log(string.Format("[ERROR] 메쉬 생성 예외: {0}", ex.Message));
                return false;
            }
        }

        private static void SetBodyMeshTypes(List<DesignBody> bodies, MeshStrategy strategy)
        {
            foreach (var body in bodies)
            {
                try
                {
                    var bodySel = Selection.Create(new IDocObject[] { body });
                    var emptySel = Selection.Empty();

                    if (strategy == MeshStrategy.AutoHex)
                    {
                        SetBodyMeshType.Execute(bodySel, emptySel,
                            BlockingDecompositionType.Automatic,
                            ElementShapeType.Hexahedral, null);
                    }
                    else if (strategy == MeshStrategy.Mixed)
                    {
                        var classification = ClassifyBody(body);
                        if (classification == MeshStrategy.AutoHex)
                        {
                            SetBodyMeshType.Execute(bodySel, emptySel,
                                BlockingDecompositionType.Automatic,
                                ElementShapeType.Hexahedral, null);
                        }
                    }
                }
                catch
                {
                    // 설정 실패 시 기본값 사용
                }
            }
        }

        private static MeshStrategy ClassifyBody(DesignBody body)
        {
            bool hasCyl = false;
            bool hasPlanar = false;
            int faceCount = 0;

            foreach (var face in body.Faces)
            {
                faceCount++;
                if (face.Shape.Geometry is Cylinder) hasCyl = true;
                if (face.Shape.Geometry is Plane) hasPlanar = true;
            }

            // 순수 원통 (볼/핀): sweep/hex 후보
            if (hasCyl && faceCount <= 4) return MeshStrategy.AutoHex;
            // 평면+원통 혼합 (구멍 뚫린 블록): hex 후보
            if (hasPlanar && hasCyl) return MeshStrategy.AutoHex;
            // 기본: tet
            return MeshStrategy.AutoTet;
        }

        // ================================================================
        // Step 10: 내보내기
        // ================================================================

        /// <summary>
        /// 메쉬를 파일로 내보낸다.
        /// </summary>
        public static void ExportMesh(string path, string format)
        {
            try
            {
                bool isDyna = string.IsNullOrEmpty(format) ||
                              format.Equals("LS-DYNA", StringComparison.OrdinalIgnoreCase);

                if (isDyna)
                {
                    MeshMethods.SaveDYNA(path);
                    Log(string.Format("LS-DYNA 내보내기: {0}", path));
                }
                else if (format.Equals("ANSYS", StringComparison.OrdinalIgnoreCase))
                {
                    MeshMethods.SaveANSYS(path);
                    Log(string.Format("ANSYS 내보내기: {0}", path));
                }
                else
                {
                    isDyna = true;
                    MeshMethods.SaveDYNA(path);
                    Log(string.Format("기본(LS-DYNA) 내보내기: {0}", path));
                }

                // LS-DYNA .k 파일 후처리: 물성 교체 + 시뮬레이션 제어카드 삽입
                if (isDyna && File.Exists(path))
                {
                    var matLog = new List<string>();
                    try
                    {
                        Part activePart = Window.ActiveWindow.Document.MainPart;
                        int patched = KFilePostProcessor.PatchMaterials(path, activePart, matLog);
                        if (patched > 0)
                            Log(string.Format("  물성 교체: {0}개 재료", patched));
                    }
                    catch (Exception patchEx)
                    {
                        Log(string.Format("[WARN] 물성 후처리: {0}", patchEx.Message));
                    }

                    try
                    {
                        int cards = KFilePostProcessor.AppendControlCards(path, matLog);
                        if (cards > 0)
                            Log(string.Format("  제어카드 삽입: {0}개 블록", cards));
                    }
                    catch (Exception ctrlEx)
                    {
                        Log(string.Format("[WARN] 제어카드 삽입: {0}", ctrlEx.Message));
                    }
                }
            }
            catch (Exception ex)
            {
                Log(string.Format("[ERROR] 내보내기 실패: {0}", ex.Message));
            }
        }

        // ================================================================
        // 유틸리티
        // ================================================================

        private static Vector GetFaceNormal(DesignFace face, Plane plane)
        {
            Vector n = plane.Frame.DirZ.UnitVector;
            if (face.Shape.IsReversed) n = Scale(n, -1);
            return n;
        }

        private static Point GetFaceCenter(DesignFace face)
        {
            try
            {
                var bb = face.Shape.GetBoundingBox(Matrix.Identity);
                var min = bb.MinCorner;
                var max = bb.MaxCorner;
                return Point.Create(
                    (min.X + max.X) / 2.0,
                    (min.Y + max.Y) / 2.0,
                    (min.Z + max.Z) / 2.0);
            }
            catch
            {
                return Point.Origin;
            }
        }

        private static bool BBoxOverlap(Box bbA, Box bbB, double tol)
        {
            var aMin = bbA.MinCorner;
            var aMax = bbA.MaxCorner;
            var bMin = bbB.MinCorner;
            var bMax = bbB.MaxCorner;

            if (aMax.X + tol < bMin.X || bMax.X + tol < aMin.X) return false;
            if (aMax.Y + tol < bMin.Y || bMax.Y + tol < aMin.Y) return false;
            if (aMax.Z + tol < bMin.Z || bMax.Z + tol < aMin.Z) return false;
            return true;
        }

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Body";
            return name.Replace(" ", "_").Replace("/", "_").Replace("\\", "_");
        }

        // ── 벡터 수학 ──

        private static double VecDot(Vector a, Vector b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        private static double VecMag(Vector v)
        {
            return Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        }

        private static Vector Normalize(Vector v)
        {
            double mag = VecMag(v);
            if (mag < 1e-15) return v;
            return Vector.Create(v.X / mag, v.Y / mag, v.Z / mag);
        }

        private static Vector Scale(Vector v, double s)
        {
            return Vector.Create(v.X * s, v.Y * s, v.Z * s);
        }

        private static void Log(string msg)
        {
            if (DiagnosticLog == null) DiagnosticLog = new List<string>();
            DiagnosticLog.Add(msg);
        }
    }
}
