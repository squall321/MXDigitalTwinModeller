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
    /// DMA 인장시편 모델링 서비스
    /// SpaceClaim API를 사용하여 DMA 시편 3D 모델 생성
    /// </summary>
    public class DMATensileService
    {
        /// <summary>
        /// DMA 인장시편 생성 (시편 + 그립 장비)
        /// </summary>
        public DesignBody CreateDMATensileSpecimen(Part part, DMATensileParameters parameters)
        {
            if (part == null)
                throw new ArgumentNullException(nameof(part));

            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            // 파라미터 유효성 검증
            if (!parameters.Validate(out string errorMessage))
                throw new ArgumentException(errorMessage);

            // 형상에 따라 시편 모델링
            DesignBody specimenBody;
            switch (parameters.Shape)
            {
                case DMASpecimenShape.Rectangle:
                    specimenBody = CreateRectangleSpecimen(part, parameters);
                    break;

                case DMASpecimenShape.DogBone:
                    specimenBody = CreateDogBoneSpecimen(part, parameters);
                    break;

                default:
                    throw new NotSupportedException($"시편 형상 {parameters.Shape}은(는) 지원되지 않습니다.");
            }

            // 시편 Face 이름 지정
            NameSpecimenFaces(specimenBody, parameters);

            // 그립 장비 모델링
            CreateDMAGrippingFixtures(part, parameters);

            return specimenBody;
        }

        /// <summary>
        /// 직사각형 DMA 인장시편 생성
        /// </summary>
        private DesignBody CreateRectangleSpecimen(Part part, DMATensileParameters p)
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
            string name = $"DMA Tensile Specimen (Rectangle - {p.Length}x{p.Width}x{p.Thickness}mm)";
            return BodyBuilder.CreateDesignBody(part, name, body);
        }

        /// <summary>
        /// Dog-bone 형상 DMA 인장시편 생성
        /// </summary>
        private DesignBody CreateDogBoneSpecimen(Part part, DMATensileParameters p)
        {
            // mm를 m로 변환
            double length = GeometryUtils.MmToMeters(p.Length);
            double width = GeometryUtils.MmToMeters(p.Width);
            double thickness = GeometryUtils.MmToMeters(p.Thickness);
            double gaugeLength = GeometryUtils.MmToMeters(p.GaugeLength);
            double gripLength = GeometryUtils.MmToMeters(p.GripLength);
            double gripWidth = GeometryUtils.MmToMeters(p.GripWidth);
            double filletRadius = GeometryUtils.MmToMeters(p.FilletRadius);

            // 프로파일 생성
            Profile profile = CreateDogBoneProfile(length, width, gaugeLength, gripLength, gripWidth, filletRadius);

            // 압출
            Body body = Body.ExtrudeProfile(profile, thickness);

            // DesignBody 생성
            string name = $"DMA Tensile Specimen (DogBone - GL:{p.GaugeLength}mm)";
            return BodyBuilder.CreateDesignBody(part, name, body);
        }

        /// <summary>
        /// Dog-bone 프로파일 생성
        /// </summary>
        private Profile CreateDogBoneProfile(double length, double gaugeWidth, double gaugeLength,
            double gripLength, double gripWidth, double filletRadius)
        {
            double halfL = length / 2.0;
            double halfGaugeW = gaugeWidth / 2.0;
            double halfGripW = gripWidth / 2.0;
            double halfGaugeL = gaugeLength / 2.0;

            // 그립 시작 위치
            double gripStartX = halfL - gripLength;

            var curves = new List<ITrimmedCurve>();

            // Dog-bone 형상 좌표 (시계 반대 방향)
            // 왼쪽 끝단 (하단)
            Point p1 = Point.Create(-halfL, -halfGripW, 0);
            // 왼쪽 그립 시작 (하단)
            Point p2 = Point.Create(-gripStartX, -halfGripW, 0);
            // 왼쪽 그립 → 게이지 전환 (하단)
            Point p3 = Point.Create(-halfGaugeL, -halfGaugeW, 0);
            // 게이지 영역 (하단 → 우측)
            Point p4 = Point.Create(halfGaugeL, -halfGaugeW, 0);
            // 게이지 → 오른쪽 그립 전환 (하단)
            Point p5 = Point.Create(gripStartX, -halfGripW, 0);
            // 오른쪽 그립 끝 (하단)
            Point p6 = Point.Create(halfL, -halfGripW, 0);
            // 오른쪽 끝단 (상단)
            Point p7 = Point.Create(halfL, halfGripW, 0);
            // 오른쪽 그립 시작 (상단)
            Point p8 = Point.Create(gripStartX, halfGripW, 0);
            // 오른쪽 그립 → 게이지 전환 (상단)
            Point p9 = Point.Create(halfGaugeL, halfGaugeW, 0);
            // 게이지 영역 (상단 우측 → 좌측)
            Point p10 = Point.Create(-halfGaugeL, halfGaugeW, 0);
            // 게이지 → 왼쪽 그립 전환 (상단)
            Point p11 = Point.Create(-gripStartX, halfGripW, 0);
            // 왼쪽 끝단 (상단)
            Point p12 = Point.Create(-halfL, halfGripW, 0);

            // 곡선 추가 (시계 반대 방향으로 닫힌 루프)
            curves.Add(CurveSegment.Create(p1, p2));   // 왼쪽 그립 (하단)
            curves.Add(CurveSegment.Create(p2, p3));   // 왼쪽 전환 (하단)
            curves.Add(CurveSegment.Create(p3, p4));   // 게이지 영역 (하단)
            curves.Add(CurveSegment.Create(p4, p5));   // 오른쪽 전환 (하단)
            curves.Add(CurveSegment.Create(p5, p6));   // 오른쪽 그립 (하단)
            curves.Add(CurveSegment.Create(p6, p7));   // 오른쪽 끝단 (측면)
            curves.Add(CurveSegment.Create(p7, p8));   // 오른쪽 그립 (상단)
            curves.Add(CurveSegment.Create(p8, p9));   // 오른쪽 전환 (상단)
            curves.Add(CurveSegment.Create(p9, p10));  // 게이지 영역 (상단)
            curves.Add(CurveSegment.Create(p10, p11)); // 왼쪽 전환 (상단)
            curves.Add(CurveSegment.Create(p11, p12)); // 왼쪽 그립 (상단)
            curves.Add(CurveSegment.Create(p12, p1));  // 왼쪽 끝단 (측면)

            return new Profile(Plane.PlaneXY, curves);
        }

        /// <summary>
        /// DMA 그립 장비 모델링
        /// </summary>
        private void CreateDMAGrippingFixtures(Part part, DMATensileParameters p)
        {
            // mm를 m로 변환
            double length = GeometryUtils.MmToMeters(p.Length);
            double gripLength = GeometryUtils.MmToMeters(p.GripLength);
            double gripWidth = GeometryUtils.MmToMeters(p.GripWidth);
            double gripHeight = GeometryUtils.MmToMeters(p.GripHeight);
            double thickness = GeometryUtils.MmToMeters(p.Thickness);

            double halfL = length / 2.0;
            double halfGripW = gripWidth / 2.0;

            // 그립 장비 치수
            double jawLength = gripLength;
            double jawWidth = gripWidth;
            double jawHeight = gripHeight;

            // 상부 그립 (Upper Jaw) - 왼쪽
            double upperJawX = -halfL + gripLength / 2.0;
            double upperJawZ = thickness;  // 시편 위에 위치

            DesignBody upperLeftGrip = CreateJaw(part, "Upper Grip (Left)", upperJawX, 0, upperJawZ, jawLength, jawWidth, jawHeight);
            NameGripFaces(upperLeftGrip, "Left", true);

            // 하부 그립 (Lower Jaw) - 왼쪽
            double lowerJawZ = -jawHeight;  // 시편 아래에 위치
            DesignBody lowerLeftGrip = CreateJaw(part, "Lower Grip (Left)", upperJawX, 0, lowerJawZ, jawLength, jawWidth, jawHeight);
            NameGripFaces(lowerLeftGrip, "Left", false);

            // 상부 그립 (Upper Jaw) - 오른쪽
            double upperJawXRight = halfL - gripLength / 2.0;
            DesignBody upperRightGrip = CreateJaw(part, "Upper Grip (Right)", upperJawXRight, 0, upperJawZ, jawLength, jawWidth, jawHeight);
            NameGripFaces(upperRightGrip, "Right", true);

            // 하부 그립 (Lower Jaw) - 오른쪽
            DesignBody lowerRightGrip = CreateJaw(part, "Lower Grip (Right)", upperJawXRight, 0, lowerJawZ, jawLength, jawWidth, jawHeight);
            NameGripFaces(lowerRightGrip, "Right", false);
        }

        /// <summary>
        /// 단일 그립 장비(죠) 생성
        /// </summary>
        private DesignBody CreateJaw(Part part, string name, double centerX, double centerY, double baseZ,
            double jawLength, double jawWidth, double jawHeight)
        {
            double halfLength = jawLength / 2.0;
            double halfWidth = jawWidth / 2.0;

            // 프로파일 생성 (baseZ 높이의 평면에서)
            Point p1 = Point.Create(centerX - halfLength, centerY - halfWidth, baseZ);
            Point p2 = Point.Create(centerX + halfLength, centerY - halfWidth, baseZ);
            Point p3 = Point.Create(centerX + halfLength, centerY + halfWidth, baseZ);
            Point p4 = Point.Create(centerX - halfLength, centerY + halfWidth, baseZ);

            var curves = new List<ITrimmedCurve>
            {
                CurveSegment.Create(p1, p2),
                CurveSegment.Create(p2, p3),
                CurveSegment.Create(p3, p4),
                CurveSegment.Create(p4, p1)
            };

            Plane jawPlane = Plane.Create(Frame.Create(Point.Create(0, 0, baseZ), Direction.DirX, Direction.DirY));
            Profile profile = new Profile(jawPlane, curves);
            Body jawBody = Body.ExtrudeProfile(profile, jawHeight);
            return BodyBuilder.CreateDesignBody(part, name, jawBody);
        }

        /// <summary>
        /// 시편 Face 이름 지정
        /// </summary>
        private void NameSpecimenFaces(DesignBody specimenBody, DMATensileParameters p)
        {
            if (specimenBody == null)
                return;

            try
            {
                Part part = specimenBody.Parent as Part;
                if (part == null)
                    return;

                double length = GeometryUtils.MmToMeters(p.Length);
                double thick = GeometryUtils.MmToMeters(p.Thickness);
                double halfL = length / 2.0;

                // 1. 시편 양 끝 면 (인장 방향 X축)
                var leftEndFaces = FaceNamingHelper.FindPlanarFaces(
                    specimenBody,
                    Direction.DirX,
                    -halfL,
                    FaceNamingHelper.Axis.X);
                FaceNamingHelper.NameFaces(part, leftEndFaces, "DMA_Specimen_LeftEnd");

                var rightEndFaces = FaceNamingHelper.FindPlanarFaces(
                    specimenBody,
                    Direction.DirX,
                    halfL,
                    FaceNamingHelper.Axis.X);
                FaceNamingHelper.NameFaces(part, rightEndFaces, "DMA_Specimen_RightEnd");

                // 2. 시편 하단 면 (Z=0) - 그립 접촉
                var bottomFaces = FaceNamingHelper.FindPlanarFaces(
                    specimenBody,
                    Direction.DirZ,
                    0.0,
                    FaceNamingHelper.Axis.Z);
                FaceNamingHelper.NameFaces(part, bottomFaces, "DMA_Specimen_Bottom_Contact");

                // 3. 시편 상단 면 (Z=thickness) - 그립 접촉
                var topFaces = FaceNamingHelper.FindPlanarFaces(
                    specimenBody,
                    Direction.DirZ,
                    thick,
                    FaceNamingHelper.Axis.Z);
                FaceNamingHelper.NameFaces(part, topFaces, "DMA_Specimen_Top_Contact");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to name DMA specimen faces: {ex.Message}");
            }
        }

        /// <summary>
        /// 그립 장비 Face 이름 지정
        /// </summary>
        private void NameGripFaces(DesignBody gripBody, string position, bool isUpper)
        {
            if (gripBody == null)
                return;

            try
            {
                Part part = gripBody.Parent as Part;
                if (part == null)
                    return;

                // 1. 그립 상단 면 (최대 Z)
                var topFace = FaceNamingHelper.FindExtremePlanarFace(
                    gripBody,
                    Direction.DirZ,
                    true);
                if (topFace != null)
                {
                    FaceNamingHelper.NameFace(part, topFace, $"DMA_Grip_{position}_{(isUpper ? "Upper" : "Lower")}_Top");
                }

                // 2. 그립 하단 면 (최소 Z)
                var bottomFace = FaceNamingHelper.FindExtremePlanarFace(
                    gripBody,
                    Direction.DirZ,
                    false);
                if (bottomFace != null)
                {
                    string faceName = isUpper
                        ? $"DMA_Grip_{position}_{(isUpper ? "Upper" : "Lower")}_Contact"
                        : $"DMA_Grip_{position}_{(isUpper ? "Upper" : "Lower")}_Bottom";
                    FaceNamingHelper.NameFace(part, bottomFace, faceName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to name DMA grip faces: {ex.Message}");
            }
        }
    }
}
