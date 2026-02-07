using System;
using System.Collections.Generic;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.CAI;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.CAI
{
    /// <summary>
    /// CAI 시편 모델링 서비스
    /// - 직사각형 패널 생성
    /// - Anti-Buckling Guide (프레임형 지그) 상하 생성
    /// - 충격 손상 영역 (Damage Zone) 표현
    /// - Named Selection 자동 생성
    /// </summary>
    public class CAISpecimenService
    {
        /// <summary>
        /// CAI 시편 + 지그 + 손상영역 생성
        /// </summary>
        public List<DesignBody> CreateCAISpecimen(Part part, CAISpecimenParameters p)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));

            string error;
            if (!p.Validate(out error))
                throw new ArgumentException(error);

            var allBodies = new List<DesignBody>();

            // 1. 패널 생성
            DesignBody panelBody = CreatePanel(part, p);
            allBodies.Add(panelBody);

            // 2. 손상 영역 생성 (패널에서 분리)
            if (p.CreateDamageZone)
            {
                DesignBody damageBody = CreateDamageZone(part, p);
                if (damageBody != null)
                    allBodies.Add(damageBody);
            }

            // 3. Named Selection - 패널
            NamePanelFaces(panelBody, p);

            // 4. Anti-Buckling Guide (지그) 생성
            if (p.CreateJig)
            {
                var jigBodies = CreateAntiBacklingGuides(part, p);
                allBodies.AddRange(jigBodies);
            }

            return allBodies;
        }

        /// <summary>
        /// 직사각형 패널 생성
        /// 패널 중심이 원점, X=길이(하중방향), Y=폭, Z=두께
        /// </summary>
        private DesignBody CreatePanel(Part part, CAISpecimenParameters p)
        {
            double length = GeometryUtils.MmToMeters(p.PanelLength);
            double width = GeometryUtils.MmToMeters(p.PanelWidth);
            double thick = GeometryUtils.MmToMeters(p.Thickness);

            double halfL = length / 2.0;
            double halfW = width / 2.0;

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
            Body body = Body.ExtrudeProfile(profile, thick);

            string name = string.Format("{0} Panel ({1}x{2}x{3}mm)",
                p.SpecimenType, p.PanelLength, p.PanelWidth, p.Thickness);
            return BodyBuilder.CreateDesignBody(part, name, body);
        }

        /// <summary>
        /// 충격 손상 영역 생성
        /// 패널 중앙에 원형/타원형 영역을 별도 Body로 생성
        /// </summary>
        private DesignBody CreateDamageZone(Part part, CAISpecimenParameters p)
        {
            double thick = GeometryUtils.MmToMeters(p.Thickness);
            double damageDepth = thick * (p.DamageDepthPercent / 100.0);

            // 손상 영역은 패널 상면(Z=thick)에서 아래로
            double zTop = thick;
            double zBase = zTop - damageDepth;

            Body damageBody;

            if (p.IsEllipticalDamage)
            {
                double majorR = GeometryUtils.MmToMeters(p.DamageMajorAxis) / 2.0;
                double minorR = GeometryUtils.MmToMeters(p.DamageMinorAxis) / 2.0;
                damageBody = CreateEllipticalCylinder(majorR, minorR, zBase, damageDepth);
            }
            else
            {
                double r = GeometryUtils.MmToMeters(p.DamageDiameter) / 2.0;
                Point center = Point.Create(0, 0, zBase);
                Frame circleFrame = Frame.Create(center, Direction.DirX, Direction.DirY);
                Circle circle = Circle.Create(circleFrame, r);
                ITrimmedCurve fullCircle = CurveSegment.Create(circle, Interval.Create(0, 2.0 * Math.PI));

                var curves = new List<ITrimmedCurve> { fullCircle };
                Plane basePlane = Plane.Create(circleFrame);
                Profile profile = new Profile(basePlane, curves);
                damageBody = Body.ExtrudeProfile(profile, damageDepth);
            }

            string name = "Damage Zone";
            DesignBody designBody = BodyBuilder.CreateDesignBody(part, name, damageBody);

            // Named Selection
            try
            {
                var allFaces = new List<IDocObject>();
                foreach (var face in designBody.Faces)
                    allFaces.Add(face);
                Group.Create(part, "Damage_Zone", allFaces);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to name damage zone: {ex.Message}");
            }

            return designBody;
        }

        /// <summary>
        /// 타원형 실린더 생성 (근사: 다각형 프로파일)
        /// </summary>
        private Body CreateEllipticalCylinder(double majorR, double minorR, double zBase, double height)
        {
            int segments = 64;
            var points = new List<Point>();
            for (int i = 0; i < segments; i++)
            {
                double angle = 2.0 * Math.PI * i / segments;
                double x = majorR * Math.Cos(angle);
                double y = minorR * Math.Sin(angle);
                points.Add(Point.Create(x, y, zBase));
            }

            var curves = new List<ITrimmedCurve>();
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                curves.Add(CurveSegment.Create(points[i], points[next]));
            }

            Frame baseFrame = Frame.Create(Point.Create(0, 0, zBase), Direction.DirX, Direction.DirY);
            Plane basePlane = Plane.Create(baseFrame);
            Profile profile = new Profile(basePlane, curves);
            return Body.ExtrudeProfile(profile, height);
        }

        /// <summary>
        /// Anti-Buckling Guide 생성
        /// 상하 2개의 프레임형 지그 (중앙에 윈도우 개구부)
        /// </summary>
        private List<DesignBody> CreateAntiBacklingGuides(Part part, CAISpecimenParameters p)
        {
            var guides = new List<DesignBody>();

            double length = GeometryUtils.MmToMeters(p.PanelLength);
            double width = GeometryUtils.MmToMeters(p.PanelWidth);
            double thick = GeometryUtils.MmToMeters(p.Thickness);
            double jigT = GeometryUtils.MmToMeters(p.JigThickness);
            double winL = GeometryUtils.MmToMeters(p.WindowLength);
            double winW = GeometryUtils.MmToMeters(p.WindowWidth);
            double clearance = GeometryUtils.MmToMeters(p.JigClearance);

            // 상부 지그: Z = thick + clearance
            double zUpper = thick + clearance;
            DesignBody upperGuide = CreateSingleGuide(part, length, width, jigT, winL, winW, zUpper, "Upper Guide");
            guides.Add(upperGuide);
            NameGuideFaces(upperGuide, "Upper_Guide", true, zUpper, jigT);

            // 하부 지그: Z = -clearance - jigT
            double zLower = -clearance - jigT;
            DesignBody lowerGuide = CreateSingleGuide(part, length, width, jigT, winL, winW, zLower, "Lower Guide");
            guides.Add(lowerGuide);
            NameGuideFaces(lowerGuide, "Lower_Guide", false, zLower, jigT);

            return guides;
        }

        /// <summary>
        /// 단일 Anti-Buckling Guide (프레임) 생성
        /// 외부 사각형 - 내부 윈도우(사각형) = 프레임
        /// </summary>
        private DesignBody CreateSingleGuide(Part part, double length, double width,
            double jigThick, double winLength, double winWidth, double zBase, string name)
        {
            double halfL = length / 2.0;
            double halfW = width / 2.0;

            // 외부 프로파일
            Point o1 = Point.Create(-halfL, -halfW, zBase);
            Point o2 = Point.Create(halfL, -halfW, zBase);
            Point o3 = Point.Create(halfL, halfW, zBase);
            Point o4 = Point.Create(-halfL, halfW, zBase);

            var outerCurves = new List<ITrimmedCurve>
            {
                CurveSegment.Create(o1, o2),
                CurveSegment.Create(o2, o3),
                CurveSegment.Create(o3, o4),
                CurveSegment.Create(o4, o1)
            };

            Frame baseFrame = Frame.Create(Point.Create(0, 0, zBase), Direction.DirX, Direction.DirY);
            Plane basePlane = Plane.Create(baseFrame);

            // 외부 사각형 Body
            Profile outerProfile = new Profile(basePlane, outerCurves);
            Body outerBody = Body.ExtrudeProfile(outerProfile, jigThick);

            // 내부 윈도우 (카팅용)
            double halfWinL = winLength / 2.0;
            double halfWinW = winWidth / 2.0;

            Point i1 = Point.Create(-halfWinL, -halfWinW, zBase);
            Point i2 = Point.Create(halfWinL, -halfWinW, zBase);
            Point i3 = Point.Create(halfWinL, halfWinW, zBase);
            Point i4 = Point.Create(-halfWinL, halfWinW, zBase);

            var innerCurves = new List<ITrimmedCurve>
            {
                CurveSegment.Create(i1, i2),
                CurveSegment.Create(i2, i3),
                CurveSegment.Create(i3, i4),
                CurveSegment.Create(i4, i1)
            };

            Profile innerProfile = new Profile(basePlane, innerCurves);
            Body innerBody = Body.ExtrudeProfile(innerProfile, jigThick);

            // Boolean Subtract: 외부 - 내부 = 프레임
            outerBody.Subtract(new Body[] { innerBody });

            return BodyBuilder.CreateDesignBody(part, name, outerBody);
        }

        /// <summary>
        /// 패널 Named Selection
        /// </summary>
        private void NamePanelFaces(DesignBody body, CAISpecimenParameters p)
        {
            if (body == null) return;
            try
            {
                Part part = body.Parent as Part;
                if (part == null) return;

                double thick = GeometryUtils.MmToMeters(p.Thickness);

                // 하단 면 (Z=0)
                var bottomFaces = FaceNamingHelper.FindPlanarFaces(
                    body, Direction.DirZ, 0.0, FaceNamingHelper.Axis.Z);
                FaceNamingHelper.NameFaces(part, bottomFaces, "Panel_Bottom");

                // 상단 면 (Z=thickness)
                var topFaces = FaceNamingHelper.FindPlanarFaces(
                    body, Direction.DirZ, thick, FaceNamingHelper.Axis.Z);
                FaceNamingHelper.NameFaces(part, topFaces, "Panel_Top");

                // 좌측 면 (X = -halfL) - 압축 하중면
                double halfL = GeometryUtils.MmToMeters(p.PanelLength) / 2.0;
                var leftFaces = FaceNamingHelper.FindPlanarFaces(
                    body, Direction.DirX, -halfL, FaceNamingHelper.Axis.X);
                FaceNamingHelper.NameFaces(part, leftFaces, "Panel_Load_Left");

                // 우측 면 (X = +halfL) - 압축 하중면
                var rightFaces = FaceNamingHelper.FindPlanarFaces(
                    body, Direction.DirX, halfL, FaceNamingHelper.Axis.X);
                FaceNamingHelper.NameFaces(part, rightFaces, "Panel_Load_Right");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to name panel faces: {ex.Message}");
            }
        }

        /// <summary>
        /// Guide Named Selection
        /// </summary>
        private void NameGuideFaces(DesignBody body, string prefix, bool isUpper,
            double zBase, double jigThick)
        {
            if (body == null) return;
            try
            {
                Part part = body.Parent as Part;
                if (part == null) return;

                // 패널 접촉면: Upper는 zBase (하면), Lower는 zBase+jigThick (상면)
                double contactZ = isUpper ? zBase : zBase + jigThick;
                var contactFaces = FaceNamingHelper.FindPlanarFaces(
                    body, Direction.DirZ, contactZ, FaceNamingHelper.Axis.Z);
                FaceNamingHelper.NameFaces(part, contactFaces, prefix + "_Contact");

                // 하중면: Upper는 zBase+jigThick (상면), Lower는 zBase (하면)
                double loadZ = isUpper ? zBase + jigThick : zBase;
                var loadFaces = FaceNamingHelper.FindPlanarFaces(
                    body, Direction.DirZ, loadZ, FaceNamingHelper.Axis.Z);
                FaceNamingHelper.NameFaces(part, loadFaces, prefix + "_Load");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to name guide faces: {ex.Message}");
            }
        }
    }
}
