using System;
using System.Collections.Generic;
using System.Linq;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Laminate;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Laminate
{
    /// <summary>
    /// 솔리드 분석 결과
    /// </summary>
    public class SolidAnalysisResult
    {
        public DesignFace BaseFace { get; set; }
        public DesignFace OppositeFace { get; set; }
        public Plane BasePlane { get; set; }
        public Vector StackingNormal { get; set; }
        public double ThicknessM { get; set; }
        public double ThicknessMm { get; set; }
        public double BaseFaceAreaMm2 { get; set; }
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// 솔리드 기반 적층 모델 생성 서비스
    /// - 솔리드 바디에서 최대 면적 면과 반대면 자동 감지
    /// - 감지된 두께 방향으로 적층 레이어 생성
    /// - SurfaceLaminateService의 기하 메서드 재사용
    /// </summary>
    public class SolidLaminateService
    {
        private readonly SurfaceLaminateService surfaceService;

        public SolidLaminateService()
        {
            surfaceService = new SurfaceLaminateService();
        }

        public SolidLaminateService(SurfaceLaminateService sharedService)
        {
            surfaceService = sharedService;
        }

        // =============================================
        //  솔리드 분석
        // =============================================

        /// <summary>
        /// 솔리드 바디를 분석하여 적층 방향과 두께를 감지
        /// </summary>
        public SolidAnalysisResult AnalyzeSolid(DesignBody body)
        {
            var result = new SolidAnalysisResult();

            if (body == null)
            {
                result.IsValid = false;
                result.ErrorMessage = "바디가 null입니다.";
                return result;
            }

            // 1. 모든 평면 면 수집
            var planarFaces = new List<PlanarFaceInfo>();
            foreach (var face in body.Faces)
            {
                Plane plane = face.Shape.Geometry as Plane;
                if (plane == null) continue;

                double area;
                try { area = face.Shape.Area; }
                catch { continue; }

                Vector normal = plane.Frame.DirZ.UnitVector;
                if (face.Shape.IsReversed)
                    normal = -normal;

                planarFaces.Add(new PlanarFaceInfo
                {
                    Face = face,
                    Plane = plane,
                    Normal = normal,
                    Area = area,
                    Origin = plane.Frame.Origin
                });
            }

            if (planarFaces.Count < 2)
            {
                result.IsValid = false;
                result.ErrorMessage = "평면 면이 2개 미만입니다. 적층 방향을 감지할 수 없습니다.";
                return result;
            }

            // 2. 면적 기준 내림차순 정렬
            planarFaces.Sort((a, b) => b.Area.CompareTo(a.Area));
            var baseFaceInfo = planarFaces[0];

            // 3. 반대면 탐색
            PlanarFaceInfo bestCandidate = null;
            double bestDistance = double.MaxValue;
            double minAreaThreshold = baseFaceInfo.Area * 0.5;

            for (int i = 1; i < planarFaces.Count; i++)
            {
                var candidate = planarFaces[i];

                // 법선 평행 확인
                double dot = Math.Abs(Vector.Dot(baseFaceInfo.Normal, candidate.Normal));
                if (dot < 0.99) continue;

                // 다른 평면인지 확인
                Vector diff = candidate.Origin - baseFaceInfo.Origin;
                double projectedDist = Vector.Dot(diff, baseFaceInfo.Normal);
                double absDist = Math.Abs(projectedDist);

                if (absDist < 1e-6) continue;  // 같은 평면

                // 면적 기준 필터
                if (candidate.Area < minAreaThreshold) continue;

                // 가장 가까운 후보 선택
                if (absDist < bestDistance)
                {
                    bestDistance = absDist;
                    bestCandidate = candidate;
                }
            }

            if (bestCandidate == null)
            {
                result.IsValid = false;
                result.ErrorMessage = "반대편 평행 면을 찾을 수 없습니다.\n" +
                                     "솔리드가 두 평행 면 사이의 돌출 형상인지 확인해주세요.";
                return result;
            }

            // 4. 적층 방향 결정 (base → opposite)
            Vector diffVec = bestCandidate.Origin - baseFaceInfo.Origin;
            double projected = Vector.Dot(diffVec, baseFaceInfo.Normal);
            Vector stackNormal = projected > 0 ? baseFaceInfo.Normal : -baseFaceInfo.Normal;
            double thickness = Math.Abs(projected);

            // 5. 결과
            result.BaseFace = baseFaceInfo.Face;
            result.OppositeFace = bestCandidate.Face;
            result.BasePlane = baseFaceInfo.Plane;
            result.StackingNormal = stackNormal;
            result.ThicknessM = thickness;
            result.ThicknessMm = GeometryUtils.MetersToMm(thickness);
            result.BaseFaceAreaMm2 = baseFaceInfo.Area * 1e6; // m² → mm²
            result.IsValid = true;

            return result;
        }

        // =============================================
        //  솔리드 적층 모델 생성
        // =============================================

        /// <summary>
        /// 솔리드 바디를 적층 레이어로 교체
        /// </summary>
        public List<DesignBody> CreateSolidLaminate(Part part, DesignBody body,
            SolidAnalysisResult analysis, SolidLaminateParameters p)
        {
            if (part == null) throw new ArgumentNullException("part");
            if (body == null) throw new ArgumentNullException("body");
            if (analysis == null || !analysis.IsValid)
                throw new InvalidOperationException("유효하지 않은 분석 결과입니다.");

            // 경계 커브 추출 (원본 삭제 전에)
            List<ITrimmedCurve> boundaryCurves = surfaceService.ExtractBoundaryCurves(analysis.BaseFace);
            if (boundaryCurves.Count == 0)
                throw new InvalidOperationException("기저면에서 경계 커브를 추출할 수 없습니다.");

            // 원본 바디 삭제
            if (p.DeleteOriginalBody)
            {
                body.Delete();
            }

            // isReversed 판단: stackingNormal과 basePlane의 DirZ가 반대이면 reversed
            Vector baseDirZ = analysis.BasePlane.Frame.DirZ.UnitVector;
            double dotCheck = Vector.Dot(analysis.StackingNormal, baseDirZ);
            bool isReversed = dotCheck < 0;

            // 레이어 생성
            var resultBodies = new List<DesignBody>();
            double currentOffsetM = 0.0;

            for (int i = 0; i < p.Layers.Count; i++)
            {
                var layer = p.Layers[i];
                double thicknessM = GeometryUtils.MmToMeters(layer.ThicknessMm);

                DesignBody layerBody = surfaceService.CreateOffsetLayerBody(
                    part, boundaryCurves, analysis.BasePlane,
                    analysis.StackingNormal, isReversed,
                    currentOffsetM, thicknessM, layer.Name);

                resultBodies.Add(layerBody);
                currentOffsetM += thicknessM;
            }

            // 인터페이스 Named Selection 생성
            if (p.CreateInterfaceNamedSelections && resultBodies.Count > 1)
            {
                CreateInterfaceGroups(part, resultBodies, p, analysis);
            }

            return resultBodies;
        }

        // =============================================
        //  인터페이스 Named Selection
        // =============================================

        private void CreateInterfaceGroups(Part part, List<DesignBody> bodies,
            SolidLaminateParameters p, SolidAnalysisResult analysis)
        {
            Direction normalDir = Direction.Create(
                analysis.StackingNormal.X,
                analysis.StackingNormal.Y,
                analysis.StackingNormal.Z);

            double cumulativeOffsetM = 0.0;

            for (int i = 0; i < p.Layers.Count - 1; i++)
            {
                cumulativeOffsetM += GeometryUtils.MmToMeters(p.Layers[i].ThicknessMm);

                Point interfacePoint = analysis.BasePlane.Frame.Origin
                    + analysis.StackingNormal * cumulativeOffsetM;

                var topFaces = surfaceService.FindFacesAtPosition(bodies[i], normalDir, interfacePoint);
                var bottomFaces = surfaceService.FindFacesAtPosition(bodies[i + 1], normalDir, interfacePoint);

                var interfaceFaces = new List<DesignFace>();
                interfaceFaces.AddRange(topFaces);
                interfaceFaces.AddRange(bottomFaces);

                if (interfaceFaces.Count > 0)
                {
                    string groupName = string.Format("Interface_{0}_{1}",
                        p.Layers[i].Name, p.Layers[i + 1].Name);
                    FaceNamingHelper.NameFaces(part, interfaceFaces, groupName);
                }
            }
        }

        // =============================================
        //  내부 데이터 구조
        // =============================================

        private class PlanarFaceInfo
        {
            public DesignFace Face;
            public Plane Plane;
            public Vector Normal;
            public double Area;
            public Point Origin;
        }
    }
}
