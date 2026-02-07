using System;
using System.Collections.Generic;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.BendingFixture;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.BendingFixture
{
    /// <summary>
    /// 기존 바디에 3점 벤딩 지지구조를 적용하는 서비스
    /// - 바운딩 박스 계산
    /// - 방향 자동 감지
    /// - 범용 지지구조 생성 (임의 축 방향 지원)
    /// </summary>
    public class BendingFixtureService
    {
        // =============================================
        //  바운딩 박스 계산
        // =============================================

        /// <summary>
        /// DesignBody의 모든 에지를 순회하여 AABB 계산
        /// </summary>
        public AxisAlignedBoundingBox ComputeBoundingBox(DesignBody body)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            double minZ = double.MaxValue, maxZ = double.MinValue;

            foreach (DesignEdge edge in body.Edges)
            {
                // 에지의 시작/끝점
                UpdateMinMax(edge.Shape.StartPoint, ref minX, ref maxX, ref minY, ref maxY, ref minZ, ref maxZ);
                UpdateMinMax(edge.Shape.EndPoint, ref minX, ref maxX, ref minY, ref maxY, ref minZ, ref maxZ);

                // 곡선 에지의 경우 중간 지점도 샘플링 (정확도 향상)
                try
                {
                    var bounds = edge.Shape.Bounds;
                    double range = bounds.End - bounds.Start;
                    if (range > 0)
                    {
                        // 1/4, 1/2, 3/4 지점 샘플링
                        for (int i = 1; i <= 3; i++)
                        {
                            double t = bounds.Start + range * i / 4.0;
                            Point pt = edge.Shape.Geometry.Evaluate(t).Point;
                            UpdateMinMax(pt, ref minX, ref maxX, ref minY, ref maxY, ref minZ, ref maxZ);
                        }
                    }
                }
                catch
                {
                    // 직선 에지 등에서는 시작/끝점으로 충분
                }
            }

            return new AxisAlignedBoundingBox
            {
                MinX = minX, MaxX = maxX,
                MinY = minY, MaxY = maxY,
                MinZ = minZ, MaxZ = maxZ
            };
        }

        private void UpdateMinMax(Point pt,
            ref double minX, ref double maxX,
            ref double minY, ref double maxY,
            ref double minZ, ref double maxZ)
        {
            if (pt.X < minX) minX = pt.X;
            if (pt.X > maxX) maxX = pt.X;
            if (pt.Y < minY) minY = pt.Y;
            if (pt.Y > maxY) maxY = pt.Y;
            if (pt.Z < minZ) minZ = pt.Z;
            if (pt.Z > maxZ) maxZ = pt.Z;
        }

        // =============================================
        //  방향 자동 감지
        // =============================================

        /// <summary>
        /// 바운딩 박스 크기 기준으로 스팬/폭/하중 방향 자동 감지
        /// 가장 긴 축 → 스팬, 중간 → 폭, 가장 짧은 → 하중
        /// </summary>
        public void DetectDirections(AxisAlignedBoundingBox bbox, BendingFixtureParameters outParams)
        {
            var extents = new[]
            {
                (axis: AxisDirection.X, extent: bbox.ExtentX),
                (axis: AxisDirection.Y, extent: bbox.ExtentY),
                (axis: AxisDirection.Z, extent: bbox.ExtentZ),
            };

            // 크기 내림차순 정렬
            Array.Sort(extents, (a, b) => b.extent.CompareTo(a.extent));

            outParams.SpanDirection = extents[0].axis;
            outParams.WidthDirection = extents[1].axis;
            outParams.LoadingDirection = extents[2].axis;

            // 바디 치수 저장 (mm)
            outParams.BodyLengthMm = GeometryUtils.MetersToMm(extents[0].extent);
            outParams.BodyWidthMm = GeometryUtils.MetersToMm(extents[1].extent);
            outParams.BodyThicknessMm = GeometryUtils.MetersToMm(extents[2].extent);

            // 기본 스팬 계산
            UpdateComputedSpan(outParams);
        }

        /// <summary>
        /// 현재 설정에 따른 계산 스팬 업데이트
        /// </summary>
        public void UpdateComputedSpan(BendingFixtureParameters p)
        {
            if (p.UseSpanRatio)
                p.ComputedSpanMm = p.BodyLengthMm * p.SpanRatio;
            else
                p.ComputedSpanMm = p.SpanMm;
        }

        /// <summary>
        /// 방향 변경 후 바디 치수 재계산
        /// </summary>
        public void UpdateBodyDimensions(AxisAlignedBoundingBox bbox, BendingFixtureParameters p)
        {
            p.BodyLengthMm = GeometryUtils.MetersToMm(bbox.GetExtent(p.SpanDirection));
            p.BodyWidthMm = GeometryUtils.MetersToMm(bbox.GetExtent(p.WidthDirection));
            p.BodyThicknessMm = GeometryUtils.MetersToMm(bbox.GetExtent(p.LoadingDirection));
            UpdateComputedSpan(p);
        }

        // =============================================
        //  지지구조 생성
        // =============================================

        /// <summary>
        /// 기존 바디에 대해 3점 벤딩 지지구조 생성
        /// 반환: 생성된 모든 DesignBody 리스트 (반원봉 + 보강재)
        /// </summary>
        public List<DesignBody> CreateFixtures(Part part, DesignBody targetBody, BendingFixtureParameters p)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            if (targetBody == null) throw new ArgumentNullException(nameof(targetBody));

            var bbox = ComputeBoundingBox(targetBody);
            var result = new List<DesignBody>();

            // mm → m 변환
            double span = GeometryUtils.MmToMeters(p.ComputedSpanMm);
            double supportRadius = GeometryUtils.MmToMeters(p.SupportDiameter) / 2.0;
            double noseRadius = GeometryUtils.MmToMeters(p.LoadingNoseDiameter) / 2.0;
            double supportHeight = GeometryUtils.MmToMeters(p.SupportHeight);
            double noseHeight = GeometryUtils.MmToMeters(p.LoadingNoseHeight);

            // 바디 치수 (meters)
            double bodyWidthExtent = bbox.GetExtent(p.WidthDirection);
            double fixtureWidth = bodyWidthExtent * 1.1;

            // 각 축의 중심/끝값
            double spanCenter = bbox.GetCenter(p.SpanDirection);
            double widthCenter = bbox.GetCenter(p.WidthDirection);
            double bodyBottomLoading = bbox.GetMin(p.LoadingDirection);
            double bodyTopLoading = bbox.GetMax(p.LoadingDirection);

            double halfSpan = span / 2.0;

            // 하부 지지점: 반원 최상단이 바디 하면에 접촉
            double lowerBaseLoading = bodyBottomLoading - supportRadius;

            result.AddRange(CreateGeneralizedCylinder(part, "Lower Support (Left)",
                p.SpanDirection, p.WidthDirection, p.LoadingDirection,
                spanCenter - halfSpan, widthCenter, lowerBaseLoading,
                supportRadius, fixtureWidth, supportHeight, isLowerSupport: true));

            result.AddRange(CreateGeneralizedCylinder(part, "Lower Support (Right)",
                p.SpanDirection, p.WidthDirection, p.LoadingDirection,
                spanCenter + halfSpan, widthCenter, lowerBaseLoading,
                supportRadius, fixtureWidth, supportHeight, isLowerSupport: true));

            // 상부 로딩 노즈: 반원 최하단이 바디 상면에 접촉
            double upperBaseLoading = bodyTopLoading + noseRadius;

            result.AddRange(CreateGeneralizedCylinder(part, "Upper Loading Nose",
                p.SpanDirection, p.WidthDirection, p.LoadingDirection,
                spanCenter, widthCenter, upperBaseLoading,
                noseRadius, fixtureWidth, noseHeight, isLowerSupport: false));

            return result;
        }

        // =============================================
        //  범용 반원봉 + 보강 블록 생성
        // =============================================

        /// <summary>
        /// 축 매핑을 사용한 범용 반원봉 + 보강 블록 생성
        /// 기존 DMA3PointBendingService.CreateCylinder의 일반화 버전
        /// </summary>
        private List<DesignBody> CreateGeneralizedCylinder(Part part, string name,
            AxisDirection spanAxis, AxisDirection widthAxis, AxisDirection loadingAxis,
            double spanPos, double widthPos, double loadingBasePos,
            double radius, double fixtureWidth, double blockHeight,
            bool isLowerSupport)
        {
            var result = new List<DesignBody>();
            double fixtureHalfWidth = fixtureWidth / 2.0;
            double widthStart = widthPos - fixtureHalfWidth;

            // 반원 프로파일 생성
            var curves = new List<ITrimmedCurve>();
            int segments = 16;

            Point leftEnd = MakePoint(spanAxis, widthAxis, loadingAxis,
                spanPos - radius, widthStart, loadingBasePos);
            Point rightEnd = MakePoint(spanAxis, widthAxis, loadingAxis,
                spanPos + radius, widthStart, loadingBasePos);
            Point prevPoint = leftEnd;

            for (int i = 1; i <= segments; i++)
            {
                double angle = Math.PI * i / segments;
                double localSpan = radius * Math.Cos(Math.PI - angle);
                double localLoad = isLowerSupport
                    ? radius * Math.Sin(Math.PI - angle)
                    : -radius * Math.Sin(Math.PI - angle);

                Point nextPoint = MakePoint(spanAxis, widthAxis, loadingAxis,
                    spanPos + localSpan, widthStart, loadingBasePos + localLoad);
                curves.Add(CurveSegment.Create(prevPoint, nextPoint));
                prevPoint = nextPoint;
            }
            curves.Add(CurveSegment.Create(rightEnd, leftEnd));

            // 프로파일 평면: Frame normal(= dirX × dirY)이 +width 방향이 되도록 설정
            // Body.ExtrudeProfile은 plane normal 방향으로 압출하므로,
            // cross(loading, span)이 +width가 아닌 경우 축을 swap
            Direction dirLoading = ToDirection(loadingAxis);
            Direction dirSpan = ToDirection(spanAxis);
            Plane profilePlane;
            if (IsEvenPermutation(loadingAxis, spanAxis, widthAxis))
                profilePlane = Plane.Create(Frame.Create(leftEnd, dirLoading, dirSpan));
            else
                profilePlane = Plane.Create(Frame.Create(leftEnd, dirSpan, dirLoading));

            Profile profile = new Profile(profilePlane, curves);
            Body semiCylinderBody = Body.ExtrudeProfile(profile, fixtureWidth);
            DesignBody semiCylinderDesignBody = BodyBuilder.CreateDesignBody(part, name, semiCylinderBody);
            result.Add(semiCylinderDesignBody);

            // 보강 블록
            Point bp1, bp2, bp3, bp4;
            if (isLowerSupport)
            {
                double blockLoading = loadingBasePos - blockHeight;
                bp1 = MakePoint(spanAxis, widthAxis, loadingAxis, spanPos - radius, widthStart, blockLoading);
                bp2 = MakePoint(spanAxis, widthAxis, loadingAxis, spanPos + radius, widthStart, blockLoading);
                bp3 = MakePoint(spanAxis, widthAxis, loadingAxis, spanPos + radius, widthStart, loadingBasePos);
                bp4 = MakePoint(spanAxis, widthAxis, loadingAxis, spanPos - radius, widthStart, loadingBasePos);
            }
            else
            {
                double blockLoading = loadingBasePos + blockHeight;
                bp1 = MakePoint(spanAxis, widthAxis, loadingAxis, spanPos - radius, widthStart, loadingBasePos);
                bp2 = MakePoint(spanAxis, widthAxis, loadingAxis, spanPos + radius, widthStart, loadingBasePos);
                bp3 = MakePoint(spanAxis, widthAxis, loadingAxis, spanPos + radius, widthStart, blockLoading);
                bp4 = MakePoint(spanAxis, widthAxis, loadingAxis, spanPos - radius, widthStart, blockLoading);
            }

            var blockCurves = new List<ITrimmedCurve>
            {
                CurveSegment.Create(bp1, bp2),
                CurveSegment.Create(bp2, bp3),
                CurveSegment.Create(bp3, bp4),
                CurveSegment.Create(bp4, bp1)
            };

            Profile blockProfile = new Profile(profilePlane, blockCurves);
            Body blockBody = Body.ExtrudeProfile(blockProfile, fixtureWidth);
            result.Add(BodyBuilder.CreateDesignBody(part, name + " (Reinforcement)", blockBody));

            return result;
        }

        // =============================================
        //  축 매핑 헬퍼
        // =============================================

        /// <summary>
        /// 추상 좌표 (span, width, loading) → 구체 좌표 (X, Y, Z) 변환
        /// </summary>
        private Point MakePoint(AxisDirection spanAxis, AxisDirection widthAxis, AxisDirection loadingAxis,
            double spanVal, double widthVal, double loadVal)
        {
            double x = 0, y = 0, z = 0;
            SetAxisValue(spanAxis, spanVal, ref x, ref y, ref z);
            SetAxisValue(widthAxis, widthVal, ref x, ref y, ref z);
            SetAxisValue(loadingAxis, loadVal, ref x, ref y, ref z);
            return Point.Create(x, y, z);
        }

        private void SetAxisValue(AxisDirection axis, double value, ref double x, ref double y, ref double z)
        {
            switch (axis)
            {
                case AxisDirection.X: x = value; break;
                case AxisDirection.Y: y = value; break;
                case AxisDirection.Z: z = value; break;
            }
        }

        private Direction ToDirection(AxisDirection axis)
        {
            switch (axis)
            {
                case AxisDirection.X: return Direction.DirX;
                case AxisDirection.Y: return Direction.DirY;
                case AxisDirection.Z: return Direction.DirZ;
                default: return Direction.DirZ;
            }
        }

        /// <summary>
        /// (a, b, c)가 (X, Y, Z)의 짝수 순열인지 판별
        /// 짝수 순열: (X,Y,Z), (Y,Z,X), (Z,X,Y) → cross(a,b) = +c
        /// 홀수 순열: (Y,X,Z), (X,Z,Y), (Z,Y,X) → cross(a,b) = -c
        /// </summary>
        private bool IsEvenPermutation(AxisDirection a, AxisDirection b, AxisDirection c)
        {
            int ia = (int)a, ib = (int)b, ic = (int)c;
            // 짝수 순열: (0,1,2), (1,2,0), (2,0,1)
            return (ia == 0 && ib == 1 && ic == 2) ||
                   (ia == 1 && ib == 2 && ic == 0) ||
                   (ia == 2 && ib == 0 && ic == 1);
        }
    }
}
