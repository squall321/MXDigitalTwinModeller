using System;
using System.Collections.Generic;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Compression;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Compression
{
    /// <summary>
    /// 압축 시험 시편 + 플래튼 모델링 서비스
    /// - 직육면체(Prism) 또는 원기둥(Cylinder) 시편 생성
    /// - 상하 압축판(Platen) 생성
    /// - 경계조건용 Named Selection 자동 생성
    /// </summary>
    public class CompressionSpecimenService
    {
        /// <summary>
        /// 압축 시편 + 플래튼 생성
        /// 생성된 모든 DesignBody를 반환 (미리보기 정리용)
        /// </summary>
        public List<DesignBody> CreateCompressionSpecimen(Part part, CompressionSpecimenParameters p)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));

            string error;
            if (!p.Validate(out error))
                throw new ArgumentException(error);

            var allBodies = new List<DesignBody>();

            // 시편 생성
            DesignBody specimenBody;
            if (p.Shape == CompressionSpecimenShape.Cylinder)
                specimenBody = CreateCylinderSpecimen(part, p);
            else
                specimenBody = CreatePrismSpecimen(part, p);

            allBodies.Add(specimenBody);

            // 시편 Face 명명
            NameSpecimenFaces(specimenBody, p);

            // 플래튼 생성
            if (p.CreatePlatens)
            {
                var platens = CreatePlatens(part, p);
                allBodies.AddRange(platens);
            }

            return allBodies;
        }

        /// <summary>
        /// 직육면체 시편 생성
        /// 시편 중심이 원점, Z 방향이 하중 방향
        /// </summary>
        private DesignBody CreatePrismSpecimen(Part part, CompressionSpecimenParameters p)
        {
            double w = GeometryUtils.MmToMeters(p.WidthMm);
            double d = GeometryUtils.MmToMeters(p.DepthMm);
            double h = GeometryUtils.MmToMeters(p.HeightMm);

            double halfW = w / 2.0;
            double halfD = d / 2.0;

            // XY 평면 (Z=0)에 프로파일, Z 방향으로 높이만큼 돌출
            Point p1 = Point.Create(-halfW, -halfD, 0);
            Point p2 = Point.Create(halfW, -halfD, 0);
            Point p3 = Point.Create(halfW, halfD, 0);
            Point p4 = Point.Create(-halfW, halfD, 0);

            var curves = new List<ITrimmedCurve>
            {
                CurveSegment.Create(p1, p2),
                CurveSegment.Create(p2, p3),
                CurveSegment.Create(p3, p4),
                CurveSegment.Create(p4, p1)
            };

            Profile profile = new Profile(Plane.PlaneXY, curves);
            Body body = Body.ExtrudeProfile(profile, h);

            string name = string.Format("{0} Specimen ({1}x{2}x{3}mm)",
                p.SpecimenType, p.WidthMm, p.DepthMm, p.HeightMm);
            return BodyBuilder.CreateDesignBody(part, name, body);
        }

        /// <summary>
        /// 원기둥 시편 생성
        /// 시편 중심이 원점, Z 방향이 하중 방향
        /// </summary>
        private DesignBody CreateCylinderSpecimen(Part part, CompressionSpecimenParameters p)
        {
            double r = GeometryUtils.MmToMeters(p.DiameterMm) / 2.0;
            double h = GeometryUtils.MmToMeters(p.HeightMm);

            // XY 평면 (Z=0)에 원 프로파일, Z 방향으로 높이만큼 돌출
            Frame circleFrame = Frame.Create(Point.Origin, Direction.DirX, Direction.DirY);
            Circle circle = Circle.Create(circleFrame, r);
            ITrimmedCurve fullCircle = CurveSegment.Create(circle, Interval.Create(0, 2.0 * Math.PI));

            var curves = new List<ITrimmedCurve> { fullCircle };
            Profile profile = new Profile(Plane.PlaneXY, curves);
            Body body = Body.ExtrudeProfile(profile, h);

            string name = string.Format("{0} Specimen (D{1}x{2}mm)",
                p.SpecimenType, p.DiameterMm, p.HeightMm);
            return BodyBuilder.CreateDesignBody(part, name, body);
        }

        /// <summary>
        /// 상하 압축판(Platen) 생성
        /// 원형 디스크 형상, 시편 상하에 배치
        /// </summary>
        private List<DesignBody> CreatePlatens(Part part, CompressionSpecimenParameters p)
        {
            var platens = new List<DesignBody>();
            double platenR = GeometryUtils.MmToMeters(p.PlatenDiameterMm) / 2.0;
            double platenH = GeometryUtils.MmToMeters(p.PlatenHeightMm);
            double specimenH = GeometryUtils.MmToMeters(p.HeightMm);

            // 상부 플래튼: Z = specimenH ~ specimenH + platenH
            platens.Add(CreateSinglePlaten(part, platenR, platenH, specimenH, "Upper Platen"));

            // 하부 플래튼: Z = -platenH ~ 0
            platens.Add(CreateSinglePlaten(part, platenR, platenH, -platenH, "Lower Platen"));

            return platens;
        }

        /// <summary>
        /// 단일 플래튼 생성
        /// </summary>
        private DesignBody CreateSinglePlaten(Part part, double radius, double height,
            double zBase, string name)
        {
            Point center = Point.Create(0, 0, zBase);
            Frame circleFrame = Frame.Create(center, Direction.DirX, Direction.DirY);
            Circle circle = Circle.Create(circleFrame, radius);
            ITrimmedCurve fullCircle = CurveSegment.Create(circle, Interval.Create(0, 2.0 * Math.PI));

            var curves = new List<ITrimmedCurve> { fullCircle };
            Plane platenPlane = Plane.Create(Frame.Create(center, Direction.DirX, Direction.DirY));
            Profile profile = new Profile(platenPlane, curves);
            Body body = Body.ExtrudeProfile(profile, height);

            DesignBody platenBody = BodyBuilder.CreateDesignBody(part, name, body);

            // 플래튼 접촉면 명명
            NamePlatenFaces(platenBody, name);

            return platenBody;
        }

        /// <summary>
        /// 시편 Face 명명 (경계조건용)
        /// </summary>
        private void NameSpecimenFaces(DesignBody body, CompressionSpecimenParameters p)
        {
            if (body == null) return;

            try
            {
                Part part = body.Parent as Part;
                if (part == null) return;

                double h = GeometryUtils.MmToMeters(p.HeightMm);

                // 하단 면 (Z=0) - 하부 플래튼과 접촉
                var bottomFaces = FaceNamingHelper.FindPlanarFaces(
                    body, Direction.DirZ, 0.0, FaceNamingHelper.Axis.Z);
                FaceNamingHelper.NameFaces(part, bottomFaces, "Specimen_Bottom");

                // 상단 면 (Z=height) - 상부 플래튼과 접촉
                var topFaces = FaceNamingHelper.FindPlanarFaces(
                    body, Direction.DirZ, h, FaceNamingHelper.Axis.Z);
                FaceNamingHelper.NameFaces(part, topFaces, "Specimen_Top");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to name specimen faces: {ex.Message}");
            }
        }

        /// <summary>
        /// 플래튼 Face 명명
        /// </summary>
        private void NamePlatenFaces(DesignBody body, string platenName)
        {
            if (body == null) return;

            try
            {
                Part part = body.Parent as Part;
                if (part == null) return;

                bool isUpper = platenName.Contains("Upper");

                // 시편과 접촉하는 면 (Upper: 최소 Z, Lower: 최대 Z)
                var contactFace = FaceNamingHelper.FindExtremePlanarFace(
                    body, Direction.DirZ, !isUpper);
                if (contactFace != null)
                {
                    FaceNamingHelper.NameFace(part, contactFace,
                        platenName.Replace(" ", "_") + "_Contact");
                }

                // 외부 면 (Upper: 최대 Z, Lower: 최소 Z) - 하중 적용 위치
                var outerFace = FaceNamingHelper.FindExtremePlanarFace(
                    body, Direction.DirZ, isUpper);
                if (outerFace != null)
                {
                    FaceNamingHelper.NameFace(part, outerFace,
                        platenName.Replace(" ", "_") + "_Load");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to name platen faces: {ex.Message}");
            }
        }
    }
}
