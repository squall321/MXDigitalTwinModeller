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
    /// - 바운딩 박스 계산 (Shape.GetBoundingBox API)
    /// - Boolean Probe로 실제 표면 위치 탐색 (복잡한 곡면 형상 대응)
    /// - 방향 자동 감지
    /// - 범용 지지구조 생성 (임의 축 방향 지원)
    /// - 충돌 감지 (Body.Intersect 기반)
    /// </summary>
    public class BendingFixtureService
    {
        // =============================================
        //  바운딩 박스 계산
        // =============================================

        /// <summary>
        /// 여러 바디의 병합 AABB 계산
        /// </summary>
        public AxisAlignedBoundingBox ComputeBoundingBox(IList<DesignBody> bodies)
        {
            if (bodies == null || bodies.Count == 0)
                throw new ArgumentException("바디가 1개 이상 필요합니다.");

            var merged = ComputeBoundingBox(bodies[0]);
            for (int i = 1; i < bodies.Count; i++)
            {
                var b = ComputeBoundingBox(bodies[i]);
                if (b.MinX < merged.MinX) merged.MinX = b.MinX;
                if (b.MaxX > merged.MaxX) merged.MaxX = b.MaxX;
                if (b.MinY < merged.MinY) merged.MinY = b.MinY;
                if (b.MaxY > merged.MaxY) merged.MaxY = b.MaxY;
                if (b.MinZ < merged.MinZ) merged.MinZ = b.MinZ;
                if (b.MaxZ > merged.MaxZ) merged.MaxZ = b.MaxZ;
            }
            return merged;
        }

        /// <summary>
        /// DesignBody의 정확한 AABB 계산 (SpaceClaim Shape API 사용)
        /// </summary>
        public AxisAlignedBoundingBox ComputeBoundingBox(DesignBody body)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            var bb = body.Shape.GetBoundingBox(Matrix.Identity);
            return new AxisAlignedBoundingBox
            {
                MinX = bb.MinCorner.X, MaxX = bb.MaxCorner.X,
                MinY = bb.MinCorner.Y, MaxY = bb.MaxCorner.Y,
                MinZ = bb.MinCorner.Z, MaxZ = bb.MaxCorner.Z
            };
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

            Array.Sort(extents, (a, b) => b.extent.CompareTo(a.extent));

            outParams.SpanDirection = extents[0].axis;
            outParams.WidthDirection = extents[1].axis;
            outParams.LoadingDirection = extents[2].axis;

            outParams.BodyLengthMm = GeometryUtils.MetersToMm(extents[0].extent);
            outParams.BodyWidthMm = GeometryUtils.MetersToMm(extents[1].extent);
            outParams.BodyThicknessMm = GeometryUtils.MetersToMm(extents[2].extent);

            UpdateComputedSpan(outParams);
        }

        public void UpdateComputedSpan(BendingFixtureParameters p)
        {
            if (p.UseSpanRatio)
                p.ComputedSpanMm = p.BodyLengthMm * p.SpanRatio;
            else
                p.ComputedSpanMm = p.SpanMm;
        }

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

        public List<DesignBody> CreateFixtures(Part part, DesignBody targetBody, BendingFixtureParameters p)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            if (targetBody == null) throw new ArgumentNullException(nameof(targetBody));

            var bbox = ComputeBoundingBox(targetBody);
            return CreateFixtures(part, bbox, p, new List<DesignBody> { targetBody });
        }

        /// <summary>
        /// 3점 벤딩 지지구조 생성.
        ///
        /// 동작 순서:
        /// 1. Boolean Probe로 각 지지점/노즈 위치의 실제 표면 높이 탐색
        /// 2. Body 오브젝트 생성 (모델에 미반영)
        /// 3. 모든 대상 바디와 충돌 확인
        /// 4. 충돌 없으면 DesignBody 최종 생성
        /// </summary>
        public List<DesignBody> CreateFixtures(Part part, AxisAlignedBoundingBox bbox,
            BendingFixtureParameters p, IList<DesignBody> allBodies = null)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));

            // mm → m 변환
            double span = GeometryUtils.MmToMeters(p.ComputedSpanMm);
            double supportRadius = GeometryUtils.MmToMeters(p.SupportDiameter) / 2.0;
            double noseRadius = GeometryUtils.MmToMeters(p.LoadingNoseDiameter) / 2.0;
            double supportHeight = GeometryUtils.MmToMeters(p.SupportHeight);
            double noseHeight = GeometryUtils.MmToMeters(p.LoadingNoseHeight);

            double bodyWidthExtent = bbox.GetExtent(p.WidthDirection);
            double fixtureWidth = bodyWidthExtent * 1.1;

            double spanCenter = bbox.GetCenter(p.SpanDirection);
            double widthCenter = bbox.GetCenter(p.WidthDirection);
            double halfSpan = span / 2.0;

            // === 실제 표면 위치 결정 (Boolean Probe) ===
            double bodyBottomLoading, bodyTopLoading;
            double bboxLoadMin = bbox.GetMin(p.LoadingDirection);
            double bboxLoadMax = bbox.GetMax(p.LoadingDirection);

            if (allBodies != null && allBodies.Count > 0)
            {
                double probeWidth = bodyWidthExtent * 1.2;

                // 좌측 지지점 위치의 실제 하면
                double leftBottom = FindActualSurface(allBodies,
                    p.SpanDirection, p.WidthDirection, p.LoadingDirection,
                    spanCenter - halfSpan, supportRadius,
                    widthCenter, probeWidth,
                    bboxLoadMin - 0.01, bboxLoadMax + 0.01, true);

                // 우측 지지점 위치의 실제 하면
                double rightBottom = FindActualSurface(allBodies,
                    p.SpanDirection, p.WidthDirection, p.LoadingDirection,
                    spanCenter + halfSpan, supportRadius,
                    widthCenter, probeWidth,
                    bboxLoadMin - 0.01, bboxLoadMax + 0.01, true);

                // 로딩 노즈 위치의 실제 상면
                double topCenter = FindActualSurface(allBodies,
                    p.SpanDirection, p.WidthDirection, p.LoadingDirection,
                    spanCenter, noseRadius,
                    widthCenter, probeWidth,
                    bboxLoadMin - 0.01, bboxLoadMax + 0.01, false);

                // 양쪽 서포터는 같은 높이 → 낮은 쪽에 맞춤 (바디 관통 방지)
                bodyBottomLoading = Math.Min(leftBottom, rightBottom);
                bodyTopLoading = topCenter;
            }
            else
            {
                // allBodies 없으면 AABB 기반 (하위 호환)
                bodyBottomLoading = bboxLoadMin;
                bodyTopLoading = bboxLoadMax;
            }

            // 하부 지지점: 반원 최상단이 실제 바디 하면에 접촉
            double lowerBaseLoading = bodyBottomLoading - supportRadius;

            // 상부 로딩 노즈: 반원 최하단이 실제 바디 상면에 접촉
            double upperBaseLoading = bodyTopLoading + noseRadius;

            // === Phase 1: Body 생성 (모델에 미반영) ===
            var namedBodies = new List<KeyValuePair<string, Body>>();

            namedBodies.AddRange(CreateCylinderBodies("Lower Support (Left)",
                p.SpanDirection, p.WidthDirection, p.LoadingDirection,
                spanCenter - halfSpan, widthCenter, lowerBaseLoading,
                supportRadius, fixtureWidth, supportHeight, isLowerSupport: true));

            namedBodies.AddRange(CreateCylinderBodies("Lower Support (Right)",
                p.SpanDirection, p.WidthDirection, p.LoadingDirection,
                spanCenter + halfSpan, widthCenter, lowerBaseLoading,
                supportRadius, fixtureWidth, supportHeight, isLowerSupport: true));

            namedBodies.AddRange(CreateCylinderBodies("Upper Loading Nose",
                p.SpanDirection, p.WidthDirection, p.LoadingDirection,
                spanCenter, widthCenter, upperBaseLoading,
                noseRadius, fixtureWidth, noseHeight, isLowerSupport: false));

            // === Phase 2: 충돌 감지 ===
            if (allBodies != null && allBodies.Count > 0)
            {
                foreach (var nb in namedBodies)
                {
                    foreach (var target in allBodies)
                    {
                        if (HasBodyCollision(nb.Value, target.Shape))
                        {
                            string targetName = string.IsNullOrEmpty(target.Name)
                                ? target.ToString() : target.Name;
                            throw new InvalidOperationException(
                                string.Format(
                                    "'{0}'이(가) 바디 '{1}'과(와) 겹칩니다.\n" +
                                    "지지대 치수를 조정하거나 방향을 변경해 주세요.",
                                    nb.Key, targetName));
                        }
                    }
                }
            }

            // === Phase 3: DesignBody 생성 ===
            var result = new List<DesignBody>();
            foreach (var nb in namedBodies)
            {
                result.Add(BodyBuilder.CreateDesignBody(part, nb.Key, nb.Value));
            }

            return result;
        }

        // =============================================
        //  Boolean Probe - 실제 표면 위치 탐색
        // =============================================

        /// <summary>
        /// 지정된 span 위치에서 실제 바디 표면의 loading 방향 극값을 찾음.
        ///
        /// 동작: 얇은 박스(프로브)를 지지점/노즈 위치에 생성 →
        ///       모든 대상 바디와 Boolean Intersect →
        ///       교집합의 loading 방향 min/max = 실제 표면 위치
        ///
        /// findMin=true: 하면 탐색 (서포터 배치용)
        /// findMin=false: 상면 탐색 (로딩 노즈 배치용)
        /// </summary>
        private double FindActualSurface(IList<DesignBody> allBodies,
            AxisDirection spanAxis, AxisDirection widthAxis, AxisDirection loadingAxis,
            double spanPos, double probeSpanHalfWidth,
            double widthCenter, double probeWidthExtent,
            double loadingSearchMin, double loadingSearchMax,
            bool findMin)
        {
            double fallback = findMin ? loadingSearchMin : loadingSearchMax;
            double widthStart = widthCenter - probeWidthExtent / 2.0;

            // 프로브 프로파일: (span, loading) 평면의 사각형
            Point p1 = MakePoint(spanAxis, widthAxis, loadingAxis,
                spanPos - probeSpanHalfWidth, widthStart, loadingSearchMin);
            Point p2 = MakePoint(spanAxis, widthAxis, loadingAxis,
                spanPos + probeSpanHalfWidth, widthStart, loadingSearchMin);
            Point p3 = MakePoint(spanAxis, widthAxis, loadingAxis,
                spanPos + probeSpanHalfWidth, widthStart, loadingSearchMax);
            Point p4 = MakePoint(spanAxis, widthAxis, loadingAxis,
                spanPos - probeSpanHalfWidth, widthStart, loadingSearchMax);

            var probeCurves = new List<ITrimmedCurve>
            {
                CurveSegment.Create(p1, p2),
                CurveSegment.Create(p2, p3),
                CurveSegment.Create(p3, p4),
                CurveSegment.Create(p4, p1)
            };

            // 프로파일 평면 (기존 CreateCylinderBodies와 동일 축 매핑)
            Direction dirLoading = ToDirection(loadingAxis);
            Direction dirSpan = ToDirection(spanAxis);
            Plane probePlane;
            if (IsEvenPermutation(loadingAxis, spanAxis, widthAxis))
                probePlane = Plane.Create(Frame.Create(p1, dirLoading, dirSpan));
            else
                probePlane = Plane.Create(Frame.Create(p1, dirSpan, dirLoading));

            Body probeBody;
            try
            {
                Profile probeProfile = new Profile(probePlane, probeCurves);
                probeBody = Body.ExtrudeProfile(probeProfile, probeWidthExtent);
            }
            catch
            {
                return fallback;
            }

            // 각 대상 바디와 교집합 → 실제 표면 위치
            double result = findMin ? double.MaxValue : double.MinValue;

            foreach (var target in allBodies)
            {
                try
                {
                    Body probeCopy = probeBody.Copy();
                    Body targetCopy = target.Shape.Copy();
                    probeCopy.Intersect(new Body[] { targetCopy });

                    if (probeCopy.Faces.Count > 0)
                    {
                        var bb = probeCopy.GetBoundingBox(Matrix.Identity);
                        if (findMin)
                        {
                            double val = GetAxisValue(loadingAxis, bb.MinCorner);
                            if (val < result) result = val;
                        }
                        else
                        {
                            double val = GetAxisValue(loadingAxis, bb.MaxCorner);
                            if (val > result) result = val;
                        }
                    }
                }
                catch { }
            }

            // 프로브 위치에 바디가 없으면 AABB 경계값으로 fallback
            if (result == double.MaxValue || result == double.MinValue)
                return fallback;

            return result;
        }

        /// <summary>
        /// Point에서 지정된 축의 값 추출
        /// </summary>
        private double GetAxisValue(AxisDirection axis, Point pt)
        {
            switch (axis)
            {
                case AxisDirection.X: return pt.X;
                case AxisDirection.Y: return pt.Y;
                case AxisDirection.Z: return pt.Z;
                default: return 0;
            }
        }

        // =============================================
        //  충돌 감지
        // =============================================

        /// <summary>
        /// 두 Body가 실제로 겹치는지 Boolean Intersect로 확인.
        /// 복사본으로 테스트하므로 원본은 변경되지 않음.
        /// </summary>
        private bool HasBodyCollision(Body bodyA, Body bodyB)
        {
            try
            {
                Body testA = bodyA.Copy();
                Body testB = bodyB.Copy();
                testA.Intersect(new Body[] { testB });
                return testA.Faces.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        // =============================================
        //  범용 반원봉 + 보강 블록 Body 생성
        // =============================================

        /// <summary>
        /// 축 매핑을 사용한 범용 반원봉 + 보강 블록 Body 생성
        /// DesignBody는 생성하지 않음 (충돌 검사 후 호출자가 생성)
        /// </summary>
        private List<KeyValuePair<string, Body>> CreateCylinderBodies(string name,
            AxisDirection spanAxis, AxisDirection widthAxis, AxisDirection loadingAxis,
            double spanPos, double widthPos, double loadingBasePos,
            double radius, double fixtureWidth, double blockHeight,
            bool isLowerSupport)
        {
            var result = new List<KeyValuePair<string, Body>>();
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

            Direction dirLoading = ToDirection(loadingAxis);
            Direction dirSpan = ToDirection(spanAxis);
            Plane profilePlane;
            if (IsEvenPermutation(loadingAxis, spanAxis, widthAxis))
                profilePlane = Plane.Create(Frame.Create(leftEnd, dirLoading, dirSpan));
            else
                profilePlane = Plane.Create(Frame.Create(leftEnd, dirSpan, dirLoading));

            Profile profile = new Profile(profilePlane, curves);
            Body semiCylinderBody = Body.ExtrudeProfile(profile, fixtureWidth);
            result.Add(new KeyValuePair<string, Body>(name, semiCylinderBody));

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
            result.Add(new KeyValuePair<string, Body>(name + " (Reinforcement)", blockBody));

            return result;
        }

        // =============================================
        //  축 매핑 헬퍼
        // =============================================

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

        private bool IsEvenPermutation(AxisDirection a, AxisDirection b, AxisDirection c)
        {
            int ia = (int)a, ib = (int)b, ic = (int)c;
            return (ia == 0 && ib == 1 && ic == 2) ||
                   (ia == 1 && ib == 2 && ic == 0) ||
                   (ia == 2 && ib == 0 && ic == 1);
        }
    }
}
