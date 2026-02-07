using System;
using System.Collections.Generic;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.DMA;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.DMA
{
    /// <summary>
    /// DMA 3점 굽힘시편 모델링 서비스
    /// SpaceClaim API를 사용하여 3점 굽힘 시편 3D 모델 생성
    /// </summary>
    public class DMA3PointBendingService
    {
        /// <summary>
        /// DMA 3점 굽힘시편 생성 (시편 + 지지구조)
        /// </summary>
        public DesignBody Create3PointBendingSpecimen(Part part, DMA3PointBendingParameters parameters)
        {
            if (part == null)
                throw new ArgumentNullException(nameof(part));

            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            // 파라미터 유효성 검증
            if (!parameters.Validate(out string errorMessage))
                throw new ArgumentException(errorMessage);

            // 시편 본체 생성 (직사각형 바)
            DesignBody specimenBody = CreateRectangularBar(part, parameters);

            // 시편 Face 이름 지정
            NameSpecimenFaces(specimenBody, parameters);

            // 지지 구조 생성
            CreateSupportStructure(part, parameters);

            return specimenBody;
        }

        /// <summary>
        /// 직사각형 바 시편 생성
        /// </summary>
        private DesignBody CreateRectangularBar(Part part, DMA3PointBendingParameters p)
        {
            // mm를 m로 변환
            double length = GeometryUtils.MmToMeters(p.Length);
            double width = GeometryUtils.MmToMeters(p.Width);
            double thickness = GeometryUtils.MmToMeters(p.Thickness);

            double halfL = length / 2.0;
            double halfW = width / 2.0;

            // 직사각형 프로파일 생성 (XY 평면)
            Point p1 = Point.Create(-halfL, -halfW, 0);
            Point p2 = Point.Create(halfL, -halfW, 0);
            Point p3 = Point.Create(halfL, halfW, 0);
            Point p4 = Point.Create(-halfL, halfW, 0);

            var curves = new List<ITrimmedCurve>
            {
                CurveSegment.Create(p1, p2),
                CurveSegment.Create(p2, p3),
                CurveSegment.Create(p3, p4),
                CurveSegment.Create(p4, p1)
            };

            Profile profile = new Profile(Plane.PlaneXY, curves);

            // 압출
            Body body = Body.ExtrudeProfile(profile, thickness);

            // DesignBody 생성
            string name = $"DMA 3-Point Bending Specimen ({p.Length}x{p.Width}x{p.Thickness}mm)";
            return BodyBuilder.CreateDesignBody(part, name, body);
        }

        /// <summary>
        /// 지지 구조 생성 (하부 지지점 2개 + 상부 로딩 노즈 1개)
        /// </summary>
        private void CreateSupportStructure(Part part, DMA3PointBendingParameters p)
        {
            // mm를 m로 변환
            double span = GeometryUtils.MmToMeters(p.Span);
            double width = GeometryUtils.MmToMeters(p.Width);
            double supportDia = GeometryUtils.MmToMeters(p.SupportDiameter);
            double loadingNoseDia = GeometryUtils.MmToMeters(p.LoadingNoseDiameter);
            double supportHeight = GeometryUtils.MmToMeters(p.SupportHeight);
            double loadingNoseHeight = GeometryUtils.MmToMeters(p.LoadingNoseHeight);
            double thickness = GeometryUtils.MmToMeters(p.Thickness);

            // 하부 지지점 위치 (반원 최상단이 시편 바닥 Z=0에 접촉)
            double halfSpan = span / 2.0;
            double supportRadius = supportDia / 2.0;
            double loadingNoseRadius = loadingNoseDia / 2.0;

            // 하부 지지점 baseZ: 반원 최상단이 Z=0이 되도록 설정
            double lowerSupportBaseZ = -supportRadius;

            // 하부 지지점 좌 (Left Support Point)
            DesignBody lowerLeftSupport = CreateCylinder(part, "Lower Support (Left)",
                -halfSpan, 0, lowerSupportBaseZ,
                supportDia, width, supportHeight);
            NameSupportFace(lowerLeftSupport, "Left_Lower", true, false);

            // 하부 지지점 우 (Right Support Point)
            DesignBody lowerRightSupport = CreateCylinder(part, "Lower Support (Right)",
                halfSpan, 0, lowerSupportBaseZ,
                supportDia, width, supportHeight);
            NameSupportFace(lowerRightSupport, "Right_Lower", true, false);

            // 상부 로딩 노즈: 반원 최하단이 Z=thickness가 되도록 설정
            double upperLoadingBaseZ = thickness + loadingNoseRadius;

            // 상부 로딩 노즈 (Upper Loading Nose)
            DesignBody upperLoadingNose = CreateCylinder(part, "Upper Loading Nose",
                0, 0, upperLoadingBaseZ,
                loadingNoseDia, width, loadingNoseHeight);
            NameSupportFace(upperLoadingNose, "Center_Upper", false, true);
        }

        /// <summary>
        /// 반원봉 + 보강 육면체 지지점/로딩 노즈 생성
        /// 반원의 곡면이 시편을 누르고, 평평한 면에 육면체 보강재 부착
        /// 접촉선은 시편의 폭 방향(Y축)으로 형성
        /// </summary>
        private DesignBody CreateCylinder(Part part, string name,
            double centerX, double centerY, double baseZ,
            double diameter, double width, double height)
        {
            double radius = diameter / 2.0;
            double halfWidth = width / 2.0;

            // 하부 지지점: 반원이 위로 향함
            // 상부 로딩노즈: 반원이 아래로 향함
            bool isLowerSupport = name.Contains("Lower");

            // XZ 평면에서 반원 프로파일 생성
            // 시편이 Y = -halfWidth ~ +halfWidth에 있으므로, 부자재도 같은 범위로 설정
            // 부자재를 시편보다 약간 길게 (1.1배)하여 양쪽으로 튀어나오게 함
            double fixtureWidth = width * 1.1;
            double fixtureHalfWidth = fixtureWidth / 2.0;

            // XZ 평면 (Y 방향으로 압출) - Frame 방향을 바꿔서 +Y 방향으로 압출되도록 설정
            Point planeOrigin = Point.Create(centerX, centerY - fixtureHalfWidth, baseZ);
            Plane profilePlane = Plane.Create(Frame.Create(planeOrigin, Direction.DirZ, Direction.DirX));

            var curves = new List<ITrimmedCurve>();

            if (isLowerSupport)
            {
                // 하부 지지점: 반원이 위쪽(+Z)을 향함
                // XZ 평면: X방향 = ±radius, Z방향 = 0 ~ +radius
                Point leftEnd = Point.Create(centerX - radius, centerY - fixtureHalfWidth, baseZ);
                Point rightEnd = Point.Create(centerX + radius, centerY - fixtureHalfWidth, baseZ);
                Point prevPoint = leftEnd;

                int segments = 16;
                for (int i = 1; i <= segments; i++)
                {
                    double angle = Math.PI * i / segments;
                    double localX = radius * Math.Cos(Math.PI - angle);  // -radius ~ +radius
                    double localZ = radius * Math.Sin(Math.PI - angle);  // 0 ~ radius ~ 0
                    Point nextPoint = Point.Create(centerX + localX, centerY - fixtureHalfWidth, baseZ + localZ);
                    curves.Add(CurveSegment.Create(prevPoint, nextPoint));
                    prevPoint = nextPoint;
                }
                curves.Add(CurveSegment.Create(rightEnd, leftEnd));
            }
            else
            {
                // 상부 로딩노즈: 반원이 아래쪽(-Z)을 향함
                Point leftEnd = Point.Create(centerX - radius, centerY - fixtureHalfWidth, baseZ);
                Point rightEnd = Point.Create(centerX + radius, centerY - fixtureHalfWidth, baseZ);
                Point prevPoint = leftEnd;

                int segments = 16;
                for (int i = 1; i <= segments; i++)
                {
                    double angle = Math.PI * i / segments;
                    double localX = radius * Math.Cos(Math.PI - angle);
                    double localZ = -radius * Math.Sin(Math.PI - angle);  // 0 ~ -radius ~ 0
                    Point nextPoint = Point.Create(centerX + localX, centerY - fixtureHalfWidth, baseZ + localZ);
                    curves.Add(CurveSegment.Create(prevPoint, nextPoint));
                    prevPoint = nextPoint;
                }
                curves.Add(CurveSegment.Create(rightEnd, leftEnd));
            }

            Profile profile = new Profile(profilePlane, curves);

            // Y 방향으로 압출 - 접촉선이 폭 방향으로 형성됨
            Body semiCylinderBody = Body.ExtrudeProfile(profile, fixtureWidth);
            DesignBody semiCylinderDesignBody = BodyBuilder.CreateDesignBody(part, name, semiCylinderBody);

            // 보강 육면체 생성 (height 파라미터 사용)
            double blockHeight = height;

            Point bp1, bp2, bp3, bp4;
            if (isLowerSupport)
            {
                // 하부 지지점 보강재 (반원 아래쪽)
                double blockZ = baseZ - blockHeight;
                bp1 = Point.Create(centerX - radius, centerY - fixtureHalfWidth, blockZ);
                bp2 = Point.Create(centerX + radius, centerY - fixtureHalfWidth, blockZ);
                bp3 = Point.Create(centerX + radius, centerY - fixtureHalfWidth, baseZ);
                bp4 = Point.Create(centerX - radius, centerY - fixtureHalfWidth, baseZ);
            }
            else
            {
                // 상부 로딩노즈 보강재 (반원 위쪽)
                double blockZ = baseZ + blockHeight;
                bp1 = Point.Create(centerX - radius, centerY - fixtureHalfWidth, baseZ);
                bp2 = Point.Create(centerX + radius, centerY - fixtureHalfWidth, baseZ);
                bp3 = Point.Create(centerX + radius, centerY - fixtureHalfWidth, blockZ);
                bp4 = Point.Create(centerX - radius, centerY - fixtureHalfWidth, blockZ);
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
            BodyBuilder.CreateDesignBody(part, name + " (Reinforcement)", blockBody);

            return semiCylinderDesignBody;
        }

        /// <summary>
        /// 시편 Face 이름 지정
        /// </summary>
        private void NameSpecimenFaces(DesignBody specimenBody, DMA3PointBendingParameters p)
        {
            if (specimenBody == null)
                return;

            try
            {
                Part part = specimenBody.Parent as Part;
                if (part == null)
                    return;

                double thick = GeometryUtils.MmToMeters(p.Thickness);

                // 1. 시편 하단 면 (Z=0) - 하부 지지점 접촉
                var bottomFaces = FaceNamingHelper.FindPlanarFaces(
                    specimenBody,
                    Direction.DirZ,
                    0.0,
                    FaceNamingHelper.Axis.Z);
                FaceNamingHelper.NameFaces(part, bottomFaces, "DMA_3PT_Specimen_Bottom_Contact");

                // 2. 시편 상단 면 (Z=thickness) - 상부 로딩 노즈 접촉
                var topFaces = FaceNamingHelper.FindPlanarFaces(
                    specimenBody,
                    Direction.DirZ,
                    thick,
                    FaceNamingHelper.Axis.Z);
                FaceNamingHelper.NameFaces(part, topFaces, "DMA_3PT_Specimen_Top_Contact");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to name 3PT specimen faces: {ex.Message}");
            }
        }

        /// <summary>
        /// 지지점/로딩 노즈 Face 이름 지정
        /// </summary>
        private void NameSupportFace(DesignBody supportBody, string position, bool isLowerSupport, bool isLoadingNose)
        {
            if (supportBody == null)
                return;

            try
            {
                Part part = supportBody.Parent as Part;
                if (part == null)
                    return;

                // 상단 면 (최대 Z)
                var topFace = FaceNamingHelper.FindExtremePlanarFace(
                    supportBody,
                    Direction.DirZ,
                    true);
                if (topFace != null)
                {
                    string prefix = isLoadingNose ? "LoadingNose" : "Support";
                    FaceNamingHelper.NameFace(part, topFace, $"DMA_3PT_{prefix}_{position}_Top");
                }

                // 하단 면 (최소 Z)
                var bottomFace = FaceNamingHelper.FindExtremePlanarFace(
                    supportBody,
                    Direction.DirZ,
                    false);
                if (bottomFace != null)
                {
                    string prefix = isLoadingNose ? "LoadingNose" : "Support";
                    FaceNamingHelper.NameFace(part, bottomFace, $"DMA_3PT_{prefix}_{position}_Bottom");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to name 3PT support faces: {ex.Message}");
            }
        }
    }
}
