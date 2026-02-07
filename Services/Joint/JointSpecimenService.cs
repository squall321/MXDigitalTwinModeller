using System;
using System.Collections.Generic;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Joint;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Joint
{
    /// <summary>
    /// 접합부 시편 모델링 서비스
    /// Single Lap, Double Lap, Scarf, Butt, T-Joint
    /// </summary>
    public class JointSpecimenService
    {
        public List<DesignBody> CreateJointSpecimen(Part part, JointSpecimenParameters p)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));

            string error;
            if (!p.Validate(out error))
                throw new ArgumentException(error);

            switch (p.SpecimenType)
            {
                case JointSpecimenType.ASTM_D1002_SingleLap:
                    return CreateSingleLap(part, p);
                case JointSpecimenType.ASTM_D3528_DoubleLap:
                    return CreateDoubleLap(part, p);
                case JointSpecimenType.Scarf_Joint:
                    return CreateScarf(part, p);
                case JointSpecimenType.Butt_Joint:
                    return CreateButt(part, p);
                case JointSpecimenType.T_Joint:
                    return CreateTJoint(part, p);
                default:
                    return CreateSingleLap(part, p);
            }
        }

        /// <summary>
        /// Single Lap Joint (D1002)
        /// 하부 판재(Z=0) + 접착층 + 상부 판재가 오버랩 영역에서 겹침
        /// X = 길이 방향, Y = 폭, Z = 두께
        /// </summary>
        private List<DesignBody> CreateSingleLap(Part part, JointSpecimenParameters p)
        {
            var bodies = new List<DesignBody>();

            double w = GeometryUtils.MmToMeters(p.AdherendWidth);
            double L = GeometryUtils.MmToMeters(p.AdherendLength);
            double t = GeometryUtils.MmToMeters(p.AdherendThickness);
            double overlap = GeometryUtils.MmToMeters(p.OverlapLength);
            double adhT = GeometryUtils.MmToMeters(p.AdhesiveThickness);
            double halfW = w / 2.0;

            // 하부 판재: X = -L ~ overlap/2, Z = 0 ~ t
            double lowerXStart = -(L - overlap / 2.0);
            double lowerXEnd = overlap / 2.0;
            Body lowerBody = CreateRectBlock(lowerXStart, -halfW, 0, lowerXEnd, halfW, t);
            DesignBody lowerDesign = BodyBuilder.CreateDesignBody(part, "Lower Adherend", lowerBody);
            bodies.Add(lowerDesign);

            // 상부 판재: X = -overlap/2 ~ L, Z = t + adhT ~ t + adhT + t
            double upperXStart = -overlap / 2.0;
            double upperXEnd = L - overlap / 2.0;
            double upperZ = t + adhT;
            Body upperBody = CreateRectBlock(upperXStart, -halfW, upperZ, upperXEnd, halfW, upperZ + t);
            DesignBody upperDesign = BodyBuilder.CreateDesignBody(part, "Upper Adherend", upperBody);
            bodies.Add(upperDesign);

            // 접착층
            if (p.CreateAdhesiveBody && adhT > 0)
            {
                Body adhesiveBody = CreateRectBlock(-overlap / 2.0, -halfW, t,
                    overlap / 2.0, halfW, t + adhT);
                DesignBody adhesiveDesign = BodyBuilder.CreateDesignBody(part, "Adhesive Layer", adhesiveBody);
                bodies.Add(adhesiveDesign);
                NameAdhesiveFaces(adhesiveDesign, part);
            }

            // Named Selections
            NameLapFaces(lowerDesign, part, "Lower_Plate", lowerXStart);
            NameLapFaces(upperDesign, part, "Upper_Plate", upperXEnd);

            return bodies;
        }

        /// <summary>
        /// Double Lap Joint (D3528)
        /// 중앙 판재 + 상하 외부 판재 + 접착층 2개
        /// </summary>
        private List<DesignBody> CreateDoubleLap(Part part, JointSpecimenParameters p)
        {
            var bodies = new List<DesignBody>();

            double w = GeometryUtils.MmToMeters(p.AdherendWidth);
            double L = GeometryUtils.MmToMeters(p.AdherendLength);
            double t = GeometryUtils.MmToMeters(p.AdherendThickness);
            double overlap = GeometryUtils.MmToMeters(p.OverlapLength);
            double adhT = GeometryUtils.MmToMeters(p.AdhesiveThickness);
            double halfW = w / 2.0;
            double centerT = 2.0 * t; // 중앙판 두꺼움

            // 중앙 판재: X = -overlap ~ +overlap, Z = 0 ~ centerT
            double halfOverlap = overlap / 2.0;
            Body centerBody = CreateRectBlock(-L / 2.0, -halfW, 0, L / 2.0, halfW, centerT);
            DesignBody centerDesign = BodyBuilder.CreateDesignBody(part, "Center Adherend", centerBody);
            bodies.Add(centerDesign);

            // 상부 외부 판재
            double upperZ = centerT + adhT;
            Body upperBody = CreateRectBlock(-halfOverlap, -halfW, upperZ,
                L / 2.0 + halfOverlap, halfW, upperZ + t);
            DesignBody upperDesign = BodyBuilder.CreateDesignBody(part, "Outer Adherend Upper", upperBody);
            bodies.Add(upperDesign);

            // 하부 외부 판재
            double lowerZ = -adhT - t;
            Body lowerBody = CreateRectBlock(-halfOverlap, -halfW, lowerZ,
                L / 2.0 + halfOverlap, halfW, -adhT);
            DesignBody lowerDesign = BodyBuilder.CreateDesignBody(part, "Outer Adherend Lower", lowerBody);
            bodies.Add(lowerDesign);

            // 접착층
            if (p.CreateAdhesiveBody && adhT > 0)
            {
                Body adhUpper = CreateRectBlock(-halfOverlap, -halfW, centerT,
                    L / 2.0 + halfOverlap, halfW, centerT + adhT);
                bodies.Add(BodyBuilder.CreateDesignBody(part, "Adhesive Upper", adhUpper));

                Body adhLower = CreateRectBlock(-halfOverlap, -halfW, -adhT,
                    L / 2.0 + halfOverlap, halfW, 0);
                bodies.Add(BodyBuilder.CreateDesignBody(part, "Adhesive Lower", adhLower));
            }

            return bodies;
        }

        /// <summary>
        /// Scarf Joint
        /// 두 판재가 경사면으로 접합. 접착층은 경사면 사이 쐐기형
        /// </summary>
        private List<DesignBody> CreateScarf(Part part, JointSpecimenParameters p)
        {
            var bodies = new List<DesignBody>();

            double w = GeometryUtils.MmToMeters(p.AdherendWidth);
            double L = GeometryUtils.MmToMeters(p.AdherendLength);
            double t = GeometryUtils.MmToMeters(p.AdherendThickness);
            double adhT = GeometryUtils.MmToMeters(p.AdhesiveThickness);
            double halfW = w / 2.0;
            double angleRad = GeometryUtils.DegreesToRadians(p.ScarfAngle);
            double scarfLen = t / Math.Tan(angleRad); // 경사면 수평 투영 길이

            // 좌측 판재: 직사각형에서 우측을 경사 절단
            // 프로파일: (0,0) → (L,0) → (L, t) → (scarfLen, t) → (0, 0)... 아니, 더 간단하게
            // 좌측 판재: (-L, 0) ~ (scarfLen/2, 0) ~ (−scarfLen/2, t) ~ (-L, t)
            double halfScarf = scarfLen / 2.0;

            var leftCurves = MakeProfile(new Point[]
            {
                Point.Create(-L, -halfW, 0),
                Point.Create(halfScarf, -halfW, 0),
                Point.Create(-halfScarf, -halfW, t),
                Point.Create(-L, -halfW, t)
            });
            Plane leftPlane = Plane.Create(Frame.Create(Point.Create(0, -halfW, 0), Direction.DirZ, Direction.DirX));
            Profile leftProfile = new Profile(leftPlane, leftCurves);
            Body leftBody = Body.ExtrudeProfile(leftProfile, w);
            DesignBody leftDesign = BodyBuilder.CreateDesignBody(part, "Left Adherend", leftBody);
            bodies.Add(leftDesign);

            // 우측 판재: (−halfScarf + adhOffset, t) ~ (halfScarf + adhOffset, 0) ~ (L, 0) ~ (L, t)
            double adhOffset = adhT / Math.Sin(angleRad); // 접착층 수평 오프셋 근사
            var rightCurves = MakeProfile(new Point[]
            {
                Point.Create(-halfScarf + adhOffset, -halfW, t),
                Point.Create(halfScarf + adhOffset, -halfW, 0),
                Point.Create(L, -halfW, 0),
                Point.Create(L, -halfW, t)
            });
            Profile rightProfile = new Profile(leftPlane, rightCurves);
            Body rightBody = Body.ExtrudeProfile(rightProfile, w);
            DesignBody rightDesign = BodyBuilder.CreateDesignBody(part, "Right Adherend", rightBody);
            bodies.Add(rightDesign);

            // Named Selections
            try
            {
                var leftGrip = FaceNamingHelper.FindPlanarFaces(leftDesign, Direction.DirX, -L, FaceNamingHelper.Axis.X);
                FaceNamingHelper.NameFaces(part, leftGrip, "Grip_Left");
                var rightGrip = FaceNamingHelper.FindPlanarFaces(rightDesign, Direction.DirX, L, FaceNamingHelper.Axis.X);
                FaceNamingHelper.NameFaces(part, rightGrip, "Grip_Right");
            }
            catch { }

            return bodies;
        }

        /// <summary>
        /// Butt Joint
        /// 두 블록 끝단 맞대기 + 접착층
        /// </summary>
        private List<DesignBody> CreateButt(Part part, JointSpecimenParameters p)
        {
            var bodies = new List<DesignBody>();

            double w = GeometryUtils.MmToMeters(p.AdherendWidth);
            double L = GeometryUtils.MmToMeters(p.AdherendLength);
            double t = GeometryUtils.MmToMeters(p.AdherendThickness);
            double adhT = GeometryUtils.MmToMeters(p.AdhesiveThickness);
            double halfW = w / 2.0;
            double halfT = t / 2.0;
            double halfAdhT = adhT / 2.0;

            // 좌측 블록: X = -L ~ -adhT/2
            Body leftBody = CreateRectBlock(-L - halfAdhT, -halfW, -halfT, -halfAdhT, halfW, halfT);
            DesignBody leftDesign = BodyBuilder.CreateDesignBody(part, "Left Block", leftBody);
            bodies.Add(leftDesign);

            // 우측 블록: X = adhT/2 ~ L
            Body rightBody = CreateRectBlock(halfAdhT, -halfW, -halfT, L + halfAdhT, halfW, halfT);
            DesignBody rightDesign = BodyBuilder.CreateDesignBody(part, "Right Block", rightBody);
            bodies.Add(rightDesign);

            // 접착층
            if (p.CreateAdhesiveBody && adhT > 0)
            {
                Body adhesive = CreateRectBlock(-halfAdhT, -halfW, -halfT, halfAdhT, halfW, halfT);
                DesignBody adhDesign = BodyBuilder.CreateDesignBody(part, "Adhesive Layer", adhesive);
                bodies.Add(adhDesign);
            }

            // Named Selections
            try
            {
                var leftGrip = FaceNamingHelper.FindExtremePlanarFace(leftDesign, Direction.DirX, false);
                if (leftGrip != null) FaceNamingHelper.NameFace(part, leftGrip, "Load_Left");
                var rightGrip = FaceNamingHelper.FindExtremePlanarFace(rightDesign, Direction.DirX, true);
                if (rightGrip != null) FaceNamingHelper.NameFace(part, rightGrip, "Load_Right");
            }
            catch { }

            return bodies;
        }

        /// <summary>
        /// T-Joint
        /// 수평 플랜지 + 수직 웹 + 필렛 본드
        /// </summary>
        private List<DesignBody> CreateTJoint(Part part, JointSpecimenParameters p)
        {
            var bodies = new List<DesignBody>();

            double w = GeometryUtils.MmToMeters(p.AdherendWidth);
            double flangeL = GeometryUtils.MmToMeters(p.FlangeLength);
            double flangeT = GeometryUtils.MmToMeters(p.AdherendThickness);
            double webH = GeometryUtils.MmToMeters(p.WebHeight);
            double webT = GeometryUtils.MmToMeters(p.WebThickness);
            double adhT = GeometryUtils.MmToMeters(p.AdhesiveThickness);
            double filletSize = GeometryUtils.MmToMeters(p.FilletBondSize);
            double halfW = w / 2.0;
            double halfFlangeL = flangeL / 2.0;
            double halfWebT = webT / 2.0;

            // 플랜지 (Base): X-Y 평면, Z = 0 ~ flangeT
            Body flangeBody = CreateRectBlock(-halfFlangeL, -halfW, 0, halfFlangeL, halfW, flangeT);
            DesignBody flangeDesign = BodyBuilder.CreateDesignBody(part, "Flange (Base)", flangeBody);
            bodies.Add(flangeDesign);

            // 웹: 플랜지 위에 수직, X=-webT/2 ~ +webT/2, Z = flangeT + adhT ~ flangeT + adhT + webH
            double webZBase = flangeT + adhT;
            Body webBody = CreateRectBlock(-halfWebT, -halfW, webZBase, halfWebT, halfW, webZBase + webH);
            DesignBody webDesign = BodyBuilder.CreateDesignBody(part, "Web", webBody);
            bodies.Add(webDesign);

            // 접착층 (웹-플랜지 사이)
            if (p.CreateAdhesiveBody && adhT > 0)
            {
                Body adhBody = CreateRectBlock(-halfWebT, -halfW, flangeT, halfWebT, halfW, flangeT + adhT);
                bodies.Add(BodyBuilder.CreateDesignBody(part, "Adhesive Layer", adhBody));
            }

            // 필렛 본드 (삼각형 단면, 웹 양쪽)
            if (filletSize > 0)
            {
                // 좌측 필렛: 직각삼각형 (-webT/2-filletSize ~ -webT/2, flangeT ~ flangeT+filletSize)
                var leftFilletCurves = MakeProfile(new Point[]
                {
                    Point.Create(-halfWebT, -halfW, flangeT),
                    Point.Create(-halfWebT - filletSize, -halfW, flangeT),
                    Point.Create(-halfWebT, -halfW, flangeT + filletSize)
                });
                Plane filletPlane = Plane.Create(Frame.Create(Point.Create(0, -halfW, 0), Direction.DirZ, Direction.DirX));
                Profile leftFilletProfile = new Profile(filletPlane, leftFilletCurves);
                Body leftFillet = Body.ExtrudeProfile(leftFilletProfile, w);
                bodies.Add(BodyBuilder.CreateDesignBody(part, "Fillet Bond Left", leftFillet));

                // 우측 필렛
                var rightFilletCurves = MakeProfile(new Point[]
                {
                    Point.Create(halfWebT, -halfW, flangeT),
                    Point.Create(halfWebT + filletSize, -halfW, flangeT),
                    Point.Create(halfWebT, -halfW, flangeT + filletSize)
                });
                Profile rightFilletProfile = new Profile(filletPlane, rightFilletCurves);
                Body rightFillet = Body.ExtrudeProfile(rightFilletProfile, w);
                bodies.Add(BodyBuilder.CreateDesignBody(part, "Fillet Bond Right", rightFillet));
            }

            // Named Selections
            try
            {
                var baseBotFaces = FaceNamingHelper.FindPlanarFaces(flangeDesign, Direction.DirZ, 0.0, FaceNamingHelper.Axis.Z);
                FaceNamingHelper.NameFaces(part, baseBotFaces, "Base_Bottom");
                var webTopFace = FaceNamingHelper.FindExtremePlanarFace(webDesign, Direction.DirZ, true);
                if (webTopFace != null) FaceNamingHelper.NameFace(part, webTopFace, "Web_Top");
            }
            catch { }

            return bodies;
        }

        // ===== 유틸리티 =====

        private Body CreateRectBlock(double x1, double y1, double z1, double x2, double y2, double z2)
        {
            double minX = Math.Min(x1, x2), maxX = Math.Max(x1, x2);
            double minY = Math.Min(y1, y2), maxY = Math.Max(y1, y2);
            double minZ = Math.Min(z1, z2), maxZ = Math.Max(z1, z2);

            Point p1 = Point.Create(minX, minY, minZ);
            Point p2 = Point.Create(maxX, minY, minZ);
            Point p3 = Point.Create(maxX, maxY, minZ);
            Point p4 = Point.Create(minX, maxY, minZ);

            var curves = new List<ITrimmedCurve>
            {
                CurveSegment.Create(p1, p2),
                CurveSegment.Create(p2, p3),
                CurveSegment.Create(p3, p4),
                CurveSegment.Create(p4, p1)
            };

            Frame baseFrame = Frame.Create(Point.Create(0, 0, minZ), Direction.DirX, Direction.DirY);
            Profile profile = new Profile(Plane.Create(baseFrame), curves);
            return Body.ExtrudeProfile(profile, maxZ - minZ);
        }

        private List<ITrimmedCurve> MakeProfile(Point[] points)
        {
            var curves = new List<ITrimmedCurve>();
            for (int i = 0; i < points.Length; i++)
            {
                int next = (i + 1) % points.Length;
                curves.Add(CurveSegment.Create(points[i], points[next]));
            }
            return curves;
        }

        private void NameLapFaces(DesignBody body, Part part, string prefix, double gripX)
        {
            try
            {
                var gripFace = FaceNamingHelper.FindPlanarFaces(body, Direction.DirX, gripX, FaceNamingHelper.Axis.X);
                FaceNamingHelper.NameFaces(part, gripFace, prefix + "_Grip");
            }
            catch { }
        }

        private void NameAdhesiveFaces(DesignBody body, Part part)
        {
            try
            {
                var allFaces = new List<IDocObject>();
                foreach (var face in body.Faces)
                    allFaces.Add(face);
                Group.Create(part, "Adhesive_Surface", allFaces);
            }
            catch { }
        }
    }
}
