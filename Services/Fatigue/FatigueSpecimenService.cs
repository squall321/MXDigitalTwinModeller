using System;
using System.Collections.Generic;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Fatigue;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Fatigue
{
    /// <summary>
    /// 피로 시편 모델링 서비스
    /// E466 (평판 dog-bone/hourglass), E606 (원형), E647 CT/MT, E2207 (박벽 원통)
    /// </summary>
    public class FatigueSpecimenService
    {
        public List<DesignBody> CreateFatigueSpecimen(Part part, FatigueSpecimenParameters p)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));

            string error;
            if (!p.Validate(out error))
                throw new ArgumentException(error);

            var allBodies = new List<DesignBody>();

            switch (p.SpecimenType)
            {
                case FatigueSpecimenType.ASTM_E466_Uniform:
                    allBodies.Add(CreateE466Uniform(part, p));
                    break;

                case FatigueSpecimenType.ASTM_E466_Hourglass:
                    allBodies.Add(CreateE466Hourglass(part, p));
                    break;

                case FatigueSpecimenType.ASTM_E606:
                    allBodies.Add(CreateE606(part, p));
                    break;

                case FatigueSpecimenType.ASTM_E647_CT:
                    allBodies.AddRange(CreateE647CT(part, p));
                    break;

                case FatigueSpecimenType.ASTM_E647_MT:
                    allBodies.Add(CreateE647MT(part, p));
                    break;

                case FatigueSpecimenType.ASTM_E2207:
                    allBodies.Add(CreateE2207Tube(part, p));
                    break;

                default: // Custom - 기본 dog-bone
                    allBodies.Add(CreateE466Uniform(part, p));
                    break;
            }

            return allBodies;
        }

        /// <summary>
        /// E466 균일 게이지 (Dog-bone 평판)
        /// 그립부 - 필렛 - 게이지 - 필렛 - 그립부
        /// X = 길이 방향 (하중), Y = 폭, Z = 두께
        /// </summary>
        private DesignBody CreateE466Uniform(Part part, FatigueSpecimenParameters p)
        {
            double gl = GeometryUtils.MmToMeters(p.GaugeLength);
            double gw = GeometryUtils.MmToMeters(p.GaugeWidth);
            double gripW = GeometryUtils.MmToMeters(p.GripWidth);
            double gripL = GeometryUtils.MmToMeters(p.GripLength);
            double thick = GeometryUtils.MmToMeters(p.Thickness);
            double filletR = GeometryUtils.MmToMeters(p.FilletRadius);

            // 프로파일을 직접 구성: 직선(그립) + 호(필렛) + 직선(게이지) + 호(필렛) + 직선(그립)
            // 간단하게: 직사각형 블록 + 양쪽 필렛 커팅으로 구현
            double totalL = GeometryUtils.MmToMeters(p.TotalLength);
            double halfTotalL = totalL / 2.0;
            double halfGripW = gripW / 2.0;

            // 기본 블록: TotalLength x GripWidth x Thickness
            Point bp1 = Point.Create(-halfTotalL, -halfGripW, 0);
            Point bp2 = Point.Create(halfTotalL, -halfGripW, 0);
            Point bp3 = Point.Create(halfTotalL, halfGripW, 0);
            Point bp4 = Point.Create(-halfTotalL, halfGripW, 0);

            var baseCurves = new List<ITrimmedCurve>
            {
                CurveSegment.Create(bp1, bp2),
                CurveSegment.Create(bp2, bp3),
                CurveSegment.Create(bp3, bp4),
                CurveSegment.Create(bp4, bp1)
            };
            Profile baseProfile = new Profile(Plane.PlaneXY, baseCurves);
            Body body = Body.ExtrudeProfile(baseProfile, thick);

            // 게이지 영역 양쪽에 단차 커팅 (Dog-bone 형상)
            // 커팅 블록: 게이지 양쪽의 좁아지는 부분
            double halfGL = gl / 2.0;
            double halfGW = gw / 2.0;
            double cutDepth = (gripW - gw) / 2.0;

            if (cutDepth > 1e-8)
            {
                // 상부 커팅 (+Y 측)
                double cutStartX = -(halfTotalL - gripL);
                double cutEndX = halfTotalL - gripL;
                Body topCut = CreateRectBlock(cutStartX, halfGW, 0, cutEndX, halfGripW + 0.001, thick);
                body.Subtract(new Body[] { topCut });

                // 하부 커팅 (-Y 측)
                Body bottomCut = CreateRectBlock(cutStartX, -(halfGripW + 0.001), 0, cutEndX, -halfGW, thick);
                body.Subtract(new Body[] { bottomCut });
            }

            string name = string.Format("E466 Uniform ({0}x{1}x{2}mm)", p.TotalLength, p.GaugeWidth, p.Thickness);
            DesignBody designBody = BodyBuilder.CreateDesignBody(part, name, body);

            // Named Selections
            NameFatigueSpecimenFaces(designBody, part, p.TotalLength, thick);

            return designBody;
        }

        /// <summary>
        /// E466 모래시계형 (Hourglass)
        /// 연속 곡률 프로파일: 양쪽 그립에서 중앙으로 좁아지는 원호
        /// </summary>
        private DesignBody CreateE466Hourglass(Part part, FatigueSpecimenParameters p)
        {
            double minW = GeometryUtils.MmToMeters(p.GaugeWidth);
            double gripW = GeometryUtils.MmToMeters(p.GripWidth);
            double gripL = GeometryUtils.MmToMeters(p.GripLength);
            double thick = GeometryUtils.MmToMeters(p.Thickness);
            double totalL = GeometryUtils.MmToMeters(p.TotalLength);
            double halfTotalL = totalL / 2.0;
            double halfGripW = gripW / 2.0;

            // 기본 블록으로 시작 후 양측면을 원호로 커팅
            Point bp1 = Point.Create(-halfTotalL, -halfGripW, 0);
            Point bp2 = Point.Create(halfTotalL, -halfGripW, 0);
            Point bp3 = Point.Create(halfTotalL, halfGripW, 0);
            Point bp4 = Point.Create(-halfTotalL, halfGripW, 0);

            var baseCurves = new List<ITrimmedCurve>
            {
                CurveSegment.Create(bp1, bp2),
                CurveSegment.Create(bp2, bp3),
                CurveSegment.Create(bp3, bp4),
                CurveSegment.Create(bp4, bp1)
            };
            Profile baseProfile = new Profile(Plane.PlaneXY, baseCurves);
            Body body = Body.ExtrudeProfile(baseProfile, thick);

            // 모래시계 커팅: 호 대신 다각형 근사로 좁아지는 형상
            double halfMinW = minW / 2.0;
            double transitionL = halfTotalL - gripL;
            int segments = 32;
            double cutDepth = halfGripW - halfMinW;

            if (cutDepth > 1e-8 && transitionL > 1e-8)
            {
                // +Y 측 커팅 프로파일
                var topCutPoints = new List<Point>();
                topCutPoints.Add(Point.Create(-transitionL, halfGripW + 0.001, 0));
                for (int i = 0; i <= segments; i++)
                {
                    double t = (double)i / segments;
                    double x = -transitionL + 2.0 * transitionL * t;
                    // 코사인 프로파일로 부드러운 곡선
                    double yOffset = cutDepth * (1.0 - Math.Cos(Math.PI * t)) / 2.0;
                    double y = halfGripW - yOffset;
                    topCutPoints.Add(Point.Create(x, y, 0));
                }
                topCutPoints.Add(Point.Create(transitionL, halfGripW + 0.001, 0));

                var topCutCurves = new List<ITrimmedCurve>();
                for (int i = 0; i < topCutPoints.Count - 1; i++)
                    topCutCurves.Add(CurveSegment.Create(topCutPoints[i], topCutPoints[i + 1]));
                topCutCurves.Add(CurveSegment.Create(topCutPoints[topCutPoints.Count - 1], topCutPoints[0]));

                Profile topCutProfile = new Profile(Plane.PlaneXY, topCutCurves);
                Body topCutBody = Body.ExtrudeProfile(topCutProfile, thick);
                body.Subtract(new Body[] { topCutBody });

                // -Y 측 커팅 (미러)
                var bottomCutPoints = new List<Point>();
                bottomCutPoints.Add(Point.Create(-transitionL, -(halfGripW + 0.001), 0));
                for (int i = 0; i <= segments; i++)
                {
                    double t = (double)i / segments;
                    double x = -transitionL + 2.0 * transitionL * t;
                    double yOffset = cutDepth * (1.0 - Math.Cos(Math.PI * t)) / 2.0;
                    double y = -(halfGripW - yOffset);
                    bottomCutPoints.Add(Point.Create(x, y, 0));
                }
                bottomCutPoints.Add(Point.Create(transitionL, -(halfGripW + 0.001), 0));

                var bottomCutCurves = new List<ITrimmedCurve>();
                for (int i = 0; i < bottomCutPoints.Count - 1; i++)
                    bottomCutCurves.Add(CurveSegment.Create(bottomCutPoints[i], bottomCutPoints[i + 1]));
                bottomCutCurves.Add(CurveSegment.Create(bottomCutPoints[bottomCutPoints.Count - 1], bottomCutPoints[0]));

                Profile bottomCutProfile = new Profile(Plane.PlaneXY, bottomCutCurves);
                Body bottomCutBody = Body.ExtrudeProfile(bottomCutProfile, thick);
                body.Subtract(new Body[] { bottomCutBody });
            }

            string name = string.Format("E466 Hourglass ({0}x{1}x{2}mm)", p.TotalLength, p.GaugeWidth, p.Thickness);
            DesignBody designBody = BodyBuilder.CreateDesignBody(part, name, body);
            NameFatigueSpecimenFaces(designBody, part, p.TotalLength, thick);

            return designBody;
        }

        /// <summary>
        /// E606 변형률제어 시편 (원형 단면)
        /// 원기둥 그립 - 필렛 전환 - 좁은 원기둥 게이지 - 필렛 전환 - 원기둥 그립
        /// 간단 구현: 그립 원기둥 + 게이지 원기둥, boolean union
        /// </summary>
        private DesignBody CreateE606(Part part, FatigueSpecimenParameters p)
        {
            double gaugeR = GeometryUtils.MmToMeters(p.GaugeDiameter) / 2.0;
            double gl = GeometryUtils.MmToMeters(p.GaugeLength);
            double gripR = GeometryUtils.MmToMeters(p.GripWidth) / 2.0;
            double gripL = GeometryUtils.MmToMeters(p.GripLength);
            double totalL = GeometryUtils.MmToMeters(p.TotalLength);
            double halfTotalL = totalL / 2.0;

            // 전체를 그립 직경 원기둥으로 시작
            Body body = CreateCylinderAlongX(gripR, totalL, -halfTotalL);

            // 중앙 게이지부를 좁게 만들기 위해 큰 원기둥에서 게이지 영역 외부를 커팅
            // 게이지 영역: X = -gl/2 ~ +gl/2, 반경 = gaugeR
            // 간단 구현: 전체 원기둥을 유지하고 게이지 영역 외부를 annular cut
            double halfGL = gl / 2.0;
            double transitionL = halfTotalL - gripL - halfGL;

            if (gaugeR < gripR && transitionL > 0)
            {
                // 게이지 영역 + 전환부를 포함하는 커팅 영역을 외부 사각 블록으로 만들고
                // 내부 원기둥을 빼서 annular ring 커팅
                double cutStartX = -(halfGL + transitionL);
                double cutEndX = halfGL + transitionL;
                double outerR = gripR + 0.001;

                // 외부 사각 블록
                Body outerBlock = CreateRectBlock(cutStartX, -outerR, -outerR,
                    cutEndX, outerR, outerR);

                // 내부 원기둥 (게이지 반경) - 남길 부분
                Body innerCyl = CreateCylinderAlongX(gaugeR, cutEndX - cutStartX, cutStartX);

                outerBlock.Subtract(new Body[] { innerCyl });
                body.Subtract(new Body[] { outerBlock });
            }

            string name = string.Format("E606 LCF (D{0}x{1}mm)", p.GaugeDiameter, p.TotalLength);
            DesignBody designBody = BodyBuilder.CreateDesignBody(part, name, body);

            // Named Selections
            try
            {
                var gripTop = FaceNamingHelper.FindExtremePlanarFace(designBody, Direction.DirX, true);
                if (gripTop != null) FaceNamingHelper.NameFace(part, gripTop, "Grip_Top");
                var gripBottom = FaceNamingHelper.FindExtremePlanarFace(designBody, Direction.DirX, false);
                if (gripBottom != null) FaceNamingHelper.NameFace(part, gripBottom, "Grip_Bottom");
            }
            catch { }

            return designBody;
        }

        /// <summary>
        /// E647 CT (Compact Tension) 시편
        /// 직사각형 판 + 핀홀 2개 + 수평 노치 슬롯
        /// ASTM E647 표준: H=0.6W, 핀 중심 X=0.275W from load line
        /// </summary>
        private List<DesignBody> CreateE647CT(Part part, FatigueSpecimenParameters p)
        {
            var bodies = new List<DesignBody>();

            double W = GeometryUtils.MmToMeters(p.CTWidth);
            double B = GeometryUtils.MmToMeters(p.CTThickness);
            double a0 = GeometryUtils.MmToMeters(p.InitialCrackLength);
            double pinD = GeometryUtils.MmToMeters(p.PinHoleDiameter);
            double notchW = GeometryUtils.MmToMeters(p.NotchWidth);
            double pinR = pinD / 2.0;

            // CT 판 크기: 폭=1.25W, 높이=1.2W (ASTM E647 표준 비율)
            double plateWidth = 1.25 * W;
            double plateHeight = 1.2 * W;
            double halfH = plateHeight / 2.0;

            // 판재 생성 (XY 평면, Z 방향 두께)
            Point cp1 = Point.Create(0, -halfH, 0);
            Point cp2 = Point.Create(plateWidth, -halfH, 0);
            Point cp3 = Point.Create(plateWidth, halfH, 0);
            Point cp4 = Point.Create(0, halfH, 0);

            var plateCurves = new List<ITrimmedCurve>
            {
                CurveSegment.Create(cp1, cp2),
                CurveSegment.Create(cp2, cp3),
                CurveSegment.Create(cp3, cp4),
                CurveSegment.Create(cp4, cp1)
            };

            Profile plateProfile = new Profile(Plane.PlaneXY, plateCurves);
            Body ctBody = Body.ExtrudeProfile(plateProfile, B);

            // 핀홀 위치: load line에서 0.275W (X = 0.275W)
            double pinX = 0.275 * W;
            double pinYOffset = 0.275 * W; // 상하 대칭

            // 상부 핀홀
            Body topPin = CreateCylinderAlongZ(pinR, B, Point.Create(pinX, pinYOffset, 0));
            ctBody.Subtract(new Body[] { topPin });

            // 하부 핀홀
            Body bottomPin = CreateCylinderAlongZ(pinR, B, Point.Create(pinX, -pinYOffset, 0));
            ctBody.Subtract(new Body[] { bottomPin });

            // 노치 슬롯: X=0 ~ a0, Y=-notchW/2 ~ +notchW/2, 관통
            double halfNotchW = notchW / 2.0;
            Body notchSlot = CreateRectBlock(0 - 0.001, -halfNotchW, 0, a0, halfNotchW, B);
            ctBody.Subtract(new Body[] { notchSlot });

            string name = string.Format("E647 CT (W={0}mm, B={1}mm)", p.CTWidth, p.CTThickness);
            DesignBody ctDesign = BodyBuilder.CreateDesignBody(part, name, ctBody);
            bodies.Add(ctDesign);

            // Named Selections
            try
            {
                // 핀홀 면은 원통면이므로 별도 처리 필요 - 여기서는 상하면만
                var topFace = FaceNamingHelper.FindPlanarFaces(ctDesign, Direction.DirZ, B, FaceNamingHelper.Axis.Z);
                FaceNamingHelper.NameFaces(part, topFace, "CT_Front");
                var bottomFace = FaceNamingHelper.FindPlanarFaces(ctDesign, Direction.DirZ, 0.0, FaceNamingHelper.Axis.Z);
                FaceNamingHelper.NameFaces(part, bottomFace, "CT_Back");

                // 균열면 (X=a0에 가까운 면)
                var crackFaces = FaceNamingHelper.FindPlanarFaces(ctDesign, Direction.DirX, a0, FaceNamingHelper.Axis.X);
                FaceNamingHelper.NameFaces(part, crackFaces, "CT_Crack_Front");
            }
            catch { }

            // 핀 생성 (옵션)
            if (p.CreateGrips)
            {
                double pinLength = B + GeometryUtils.MmToMeters(10.0); // 핀이 약간 돌출
                Body topPinBody = CreateCylinderAlongZ(pinR * 0.95, pinLength,
                    Point.Create(pinX, pinYOffset, -GeometryUtils.MmToMeters(5.0)));
                DesignBody topPinDesign = BodyBuilder.CreateDesignBody(part, "Pin Upper", topPinBody);
                bodies.Add(topPinDesign);

                Body bottomPinBody = CreateCylinderAlongZ(pinR * 0.95, pinLength,
                    Point.Create(pinX, -pinYOffset, -GeometryUtils.MmToMeters(5.0)));
                DesignBody bottomPinDesign = BodyBuilder.CreateDesignBody(part, "Pin Lower", bottomPinBody);
                bodies.Add(bottomPinDesign);
            }

            return bodies;
        }

        /// <summary>
        /// E647 MT (Middle Tension) 시편
        /// 직사각형 판재 + 중앙 양방향 관통 슬롯
        /// </summary>
        private DesignBody CreateE647MT(Part part, FatigueSpecimenParameters p)
        {
            double W = GeometryUtils.MmToMeters(p.MTWidth);
            double L = GeometryUtils.MmToMeters(p.MTLength);
            double B = GeometryUtils.MmToMeters(p.MTThickness);
            double a0 = GeometryUtils.MmToMeters(p.SlotHalfLength);
            double slotW = GeometryUtils.MmToMeters(p.SlotWidth);

            double halfW = W / 2.0;
            double halfL = L / 2.0;
            double halfSlotW = slotW / 2.0;

            // 기본 판재
            Point mp1 = Point.Create(-halfL, -halfW, 0);
            Point mp2 = Point.Create(halfL, -halfW, 0);
            Point mp3 = Point.Create(halfL, halfW, 0);
            Point mp4 = Point.Create(-halfL, halfW, 0);

            var plateCurves = new List<ITrimmedCurve>
            {
                CurveSegment.Create(mp1, mp2),
                CurveSegment.Create(mp2, mp3),
                CurveSegment.Create(mp3, mp4),
                CurveSegment.Create(mp4, mp1)
            };

            Profile plateProfile = new Profile(Plane.PlaneXY, plateCurves);
            Body body = Body.ExtrudeProfile(plateProfile, B);

            // 중앙 슬롯: X=-a0 ~ +a0, Y=-slotW/2 ~ +slotW/2, 관통
            Body slot = CreateRectBlock(-a0, -halfSlotW, 0, a0, halfSlotW, B);
            body.Subtract(new Body[] { slot });

            string name = string.Format("E647 MT (W={0}mm, L={1}mm)", p.MTWidth, p.MTLength);
            DesignBody designBody = BodyBuilder.CreateDesignBody(part, name, body);

            // Named Selections
            try
            {
                var gripTop = FaceNamingHelper.FindPlanarFaces(designBody, Direction.DirX, halfL, FaceNamingHelper.Axis.X);
                FaceNamingHelper.NameFaces(part, gripTop, "Grip_Top");
                var gripBottom = FaceNamingHelper.FindPlanarFaces(designBody, Direction.DirX, -halfL, FaceNamingHelper.Axis.X);
                FaceNamingHelper.NameFaces(part, gripBottom, "Grip_Bottom");
            }
            catch { }

            return designBody;
        }

        /// <summary>
        /// E2207 박벽 원통형 시편
        /// 외부 원통 - 내부 원통 = 튜브
        /// 그립부는 두꺼운 벽 (외경이 큰 영역)
        /// </summary>
        private DesignBody CreateE2207Tube(Part part, FatigueSpecimenParameters p)
        {
            double outerR = GeometryUtils.MmToMeters(p.TubeOuterDiameter) / 2.0;
            double innerR = GeometryUtils.MmToMeters(p.TubeInnerDiameter) / 2.0;
            double gl = GeometryUtils.MmToMeters(p.TubeGaugeLength);
            double totalL = GeometryUtils.MmToMeters(p.TubeTotalLength);
            double gripOuterR = GeometryUtils.MmToMeters(p.TubeGripOuterDiameter) / 2.0;
            double halfTotalL = totalL / 2.0;

            // 외부: 그립 반경으로 전체 원기둥
            Body outerBody = CreateCylinderAlongX(gripOuterR, totalL, -halfTotalL);

            // 내부: 내경으로 전체 관통 원기둥 제거
            Body innerBody = CreateCylinderAlongX(innerR, totalL + 0.002, -halfTotalL - 0.001);
            outerBody.Subtract(new Body[] { innerBody });

            // 게이지 영역에서 외경을 줄이기: 게이지 영역의 외부 annular ring 제거
            double halfGL = gl / 2.0;
            if (outerR < gripOuterR)
            {
                // 게이지 영역 외부 사각 블록 - 내부 원기둥(게이지 외경) = annular 커팅
                Body cutOuter = CreateRectBlock(-halfGL, -(gripOuterR + 0.001), -(gripOuterR + 0.001),
                    halfGL, gripOuterR + 0.001, gripOuterR + 0.001);
                Body keepInner = CreateCylinderAlongX(outerR, gl + 0.002, -halfGL - 0.001);
                cutOuter.Subtract(new Body[] { keepInner });
                outerBody.Subtract(new Body[] { cutOuter });
            }

            string name = string.Format("E2207 Tube (OD{0}xID{1}x{2}mm)",
                p.TubeOuterDiameter, p.TubeInnerDiameter, p.TubeTotalLength);
            DesignBody designBody = BodyBuilder.CreateDesignBody(part, name, outerBody);

            // Named Selections
            try
            {
                var gripTop = FaceNamingHelper.FindExtremePlanarFace(designBody, Direction.DirX, true);
                if (gripTop != null) FaceNamingHelper.NameFace(part, gripTop, "Grip_Top");
                var gripBottom = FaceNamingHelper.FindExtremePlanarFace(designBody, Direction.DirX, false);
                if (gripBottom != null) FaceNamingHelper.NameFace(part, gripBottom, "Grip_Bottom");
            }
            catch { }

            return designBody;
        }

        // ===== 유틸리티 =====

        /// <summary>직사각형 블록 생성</summary>
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

        /// <summary>X축 방향 원기둥 생성</summary>
        private Body CreateCylinderAlongX(double radius, double length, double xStart)
        {
            // YZ 평면에 원 프로파일, X 방향으로 돌출
            Point center = Point.Create(xStart, 0, 0);
            Frame circleFrame = Frame.Create(center, Direction.DirY, Direction.DirZ);
            Circle circle = Circle.Create(circleFrame, radius);
            ITrimmedCurve fullCircle = CurveSegment.Create(circle, Interval.Create(0, 2.0 * Math.PI));

            var curves = new List<ITrimmedCurve> { fullCircle };
            Plane basePlane = Plane.Create(Frame.Create(center, Direction.DirY, Direction.DirZ));
            Profile profile = new Profile(basePlane, curves);
            return Body.ExtrudeProfile(profile, length);
        }

        /// <summary>Z축 방향 원기둥 생성</summary>
        private Body CreateCylinderAlongZ(double radius, double height, Point center)
        {
            Frame circleFrame = Frame.Create(center, Direction.DirX, Direction.DirY);
            Circle circle = Circle.Create(circleFrame, radius);
            ITrimmedCurve fullCircle = CurveSegment.Create(circle, Interval.Create(0, 2.0 * Math.PI));

            var curves = new List<ITrimmedCurve> { fullCircle };
            Plane basePlane = Plane.Create(circleFrame);
            Profile profile = new Profile(basePlane, curves);
            return Body.ExtrudeProfile(profile, height);
        }

        /// <summary>피로 시편 공통 Named Selections (E466 등)</summary>
        private void NameFatigueSpecimenFaces(DesignBody body, Part part, double totalLengthMm, double thickMm)
        {
            try
            {
                double halfL = GeometryUtils.MmToMeters(totalLengthMm) / 2.0;
                double thick = GeometryUtils.MmToMeters(thickMm);

                // 그립 상단면 (X = +halfL)
                var gripTop = FaceNamingHelper.FindPlanarFaces(body, Direction.DirX, halfL, FaceNamingHelper.Axis.X);
                FaceNamingHelper.NameFaces(part, gripTop, "Grip_Top");

                // 그립 하단면 (X = -halfL)
                var gripBottom = FaceNamingHelper.FindPlanarFaces(body, Direction.DirX, -halfL, FaceNamingHelper.Axis.X);
                FaceNamingHelper.NameFaces(part, gripBottom, "Grip_Bottom");

                // 전면 (Z = thick)
                var front = FaceNamingHelper.FindPlanarFaces(body, Direction.DirZ, thick, FaceNamingHelper.Axis.Z);
                FaceNamingHelper.NameFaces(part, front, "Specimen_Front");

                // 후면 (Z = 0)
                var back = FaceNamingHelper.FindPlanarFaces(body, Direction.DirZ, 0.0, FaceNamingHelper.Axis.Z);
                FaceNamingHelper.NameFaces(part, back, "Specimen_Back");
            }
            catch { }
        }
    }
}
