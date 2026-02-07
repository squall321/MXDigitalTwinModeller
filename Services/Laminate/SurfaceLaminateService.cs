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
    /// 면 기반 적층 모델 생성 서비스
    /// - 선택된 면(DesignFace)의 경계 형상을 기반으로 오프셋 적층
    /// - 법선 방향 또는 역방향으로 레이어 생성
    /// - 인접 레이어 간 계면 Named Selection 자동 생성
    /// - 직선, 원호, 타원 등 다양한 경계 커브 지원
    /// </summary>
    public class SurfaceLaminateService
    {
        /// <summary>
        /// 면 기반 적층 모델 생성
        /// </summary>
        public List<DesignBody> CreateSurfaceLaminate(Part part, DesignFace selectedFace,
            SurfaceLaminateParameters p)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            if (selectedFace == null) throw new ArgumentNullException(nameof(selectedFace));

            // 선택된 면이 Plane인지 확인
            Plane facePlane = selectedFace.Shape.Geometry as Plane;
            if (facePlane == null)
                throw new InvalidOperationException("선택된 면이 평면이 아닙니다. 평면 면만 지원됩니다.");

            // 면의 경계 커브 추출
            List<ITrimmedCurve> boundaryCurves = ExtractBoundaryCurves(selectedFace);
            if (boundaryCurves.Count == 0)
                throw new InvalidOperationException("선택된 면에서 경계 커브를 추출할 수 없습니다.");

            // 면 법선 방향 결정
            Vector faceNormalVec = facePlane.Frame.DirZ.UnitVector;
            bool isReversed = (p.Direction == OffsetDirection.Reverse);

            // 오프셋 벡터 방향 (레이어가 쌓이는 방향)
            Vector stackVector = isReversed ? -faceNormalVec : faceNormalVec;

            var result = new List<DesignBody>();
            double currentOffsetM = 0.0;

            for (int i = 0; i < p.Layers.Count; i++)
            {
                var layer = p.Layers[i];
                double thicknessM = GeometryUtils.MmToMeters(layer.ThicknessMm);

                DesignBody layerBody = CreateOffsetLayerBody(
                    part, boundaryCurves, facePlane,
                    stackVector, isReversed, currentOffsetM, thicknessM, layer.Name);

                result.Add(layerBody);
                currentOffsetM += thicknessM;
            }

            // 계면 Named Selection 생성
            if (p.CreateInterfaceNamedSelections && result.Count > 1)
            {
                CreateInterfaceGroups(part, result, p, facePlane, stackVector);
            }

            return result;
        }

        /// <summary>
        /// 면에서 경계 커브 추출 (외부 루프)
        /// </summary>
        private List<ITrimmedCurve> ExtractBoundaryCurves(DesignFace face)
        {
            var curves = new List<ITrimmedCurve>();

            foreach (var edge in face.Edges)
            {
                if (edge.Shape != null)
                    curves.Add(edge.Shape);
            }

            return curves;
        }

        /// <summary>
        /// 오프셋된 레이어 바디 생성
        /// </summary>
        private DesignBody CreateOffsetLayerBody(Part part,
            List<ITrimmedCurve> originalCurves, Plane originalPlane,
            Vector stackVector, bool isReversed,
            double offsetM, double thicknessM, string name)
        {
            // 오프셋 벡터 계산
            Vector offsetVector = stackVector * offsetM;

            // 오프셋된 프로파일 평면 생성
            // 돌출 방향은 프로파일 평면의 DirZ (= DirX × DirY) 방향
            Point offsetOrigin = originalPlane.Frame.Origin + offsetVector;

            Direction dirX, dirY;
            if (isReversed)
            {
                // Reverse: DirX/DirY 스왑으로 DirZ 방향 반전
                // DirZ = DirY × DirX = -(DirX × DirY) = -원래법선
                dirX = originalPlane.Frame.DirY;
                dirY = originalPlane.Frame.DirX;
            }
            else
            {
                dirX = originalPlane.Frame.DirX;
                dirY = originalPlane.Frame.DirY;
            }

            Frame offsetFrame = Frame.Create(offsetOrigin, dirX, dirY);
            Plane offsetPlane = Plane.Create(offsetFrame);

            // 경계 커브를 오프셋 위치로 이동 (커브 타입별 처리)
            List<ITrimmedCurve> offsetCurves = TranslateCurves(originalCurves, offsetVector);

            // 프로파일 생성 및 돌출
            Profile profile = new Profile(offsetPlane, offsetCurves);
            Body body = Body.ExtrudeProfile(profile, thicknessM);

            return BodyBuilder.CreateDesignBody(part, name, body);
        }

        /// <summary>
        /// 커브 목록을 벡터만큼 평행이동 (커브 타입별 처리)
        /// - Line: 시작점/끝점 이동
        /// - Circle: 중심 이동 + 동일 반경 원호 재생성
        /// - Ellipse: 중심 이동 + 동일 파라미터 타원호 재생성
        /// - 기타: 시작점/끝점 직선 근사
        /// </summary>
        private List<ITrimmedCurve> TranslateCurves(List<ITrimmedCurve> curves, Vector translation)
        {
            var result = new List<ITrimmedCurve>();

            foreach (var curve in curves)
            {
                ITrimmedCurve translated = TranslateSingleCurve(curve, translation);
                if (translated != null)
                    result.Add(translated);
            }

            return result;
        }

        /// <summary>
        /// 단일 커브를 벡터만큼 평행이동
        /// </summary>
        private ITrimmedCurve TranslateSingleCurve(ITrimmedCurve curve, Vector translation)
        {
            Curve geometry = curve.Geometry;

            if (geometry is Line)
            {
                // 직선: 시작점/끝점 이동
                Point startPt = curve.StartPoint + translation;
                Point endPt = curve.EndPoint + translation;
                return CurveSegment.Create(startPt, endPt);
            }
            else if (geometry is Circle circle)
            {
                // 원/원호: 중심 이동, 반경 유지
                Frame origFrame = circle.Frame;
                Point newCenter = origFrame.Origin + translation;
                Frame newFrame = Frame.Create(newCenter, origFrame.DirX, origFrame.DirY);
                Circle newCircle = Circle.Create(newFrame, circle.Radius);
                return CurveSegment.Create(newCircle, curve.Bounds);
            }
            else if (geometry is Ellipse ellipse)
            {
                // 타원/타원호: 중심 이동, 축 유지
                Frame origFrame = ellipse.Frame;
                Point newCenter = origFrame.Origin + translation;
                Frame newFrame = Frame.Create(newCenter, origFrame.DirX, origFrame.DirY);
                Ellipse newEllipse = Ellipse.Create(newFrame, ellipse.MajorRadius, ellipse.MinorRadius);
                return CurveSegment.Create(newEllipse, curve.Bounds);
            }
            else
            {
                // 기타 커브: 시작점/끝점 직선 근사 (Fallback)
                Point startPt = curve.StartPoint + translation;
                Point endPt = curve.EndPoint + translation;
                return CurveSegment.Create(startPt, endPt);
            }
        }

        /// <summary>
        /// 레이어 간 계면 Named Selection 생성
        /// </summary>
        private void CreateInterfaceGroups(Part part, List<DesignBody> bodies,
            SurfaceLaminateParameters p, Plane originalPlane, Vector stackVector)
        {
            Direction normalDir = Direction.Create(stackVector.X, stackVector.Y, stackVector.Z);
            double cumulativeOffsetM = 0.0;

            for (int i = 0; i < p.Layers.Count - 1; i++)
            {
                cumulativeOffsetM += GeometryUtils.MmToMeters(p.Layers[i].ThicknessMm);

                // 계면 위치 = 원래 면 + 누적 오프셋
                Point interfacePoint = originalPlane.Frame.Origin + stackVector * cumulativeOffsetM;

                // 계면에 위치한 면 찾기
                var topFaces = FindFacesAtPosition(bodies[i], normalDir, interfacePoint);
                var bottomFaces = FindFacesAtPosition(bodies[i + 1], normalDir, interfacePoint);

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

        /// <summary>
        /// 특정 위치에서 법선 방향의 평면 Face 찾기
        /// </summary>
        private List<DesignFace> FindFacesAtPosition(DesignBody body,
            Direction normalDir, Point targetPoint)
        {
            const double tolerance = 1e-6;
            var result = new List<DesignFace>();

            foreach (var face in body.Faces)
            {
                if (!(face.Shape.Geometry is Plane plane))
                    continue;

                // 법선 방향 확인
                double dot = Math.Abs(Vector.Dot(plane.Frame.DirZ.UnitVector, normalDir.UnitVector));
                if (dot < 0.99)
                    continue;

                // 위치 확인: 면의 원점과 타겟 점을 법선 방향으로 투영하여 비교
                Vector diff = targetPoint - plane.Frame.Origin;
                double projectedDist = Math.Abs(Vector.Dot(diff, normalDir.UnitVector));
                if (projectedDist < tolerance)
                {
                    result.Add(face);
                }
            }

            return result;
        }
    }
}
