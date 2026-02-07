using System;
using System.Collections.Generic;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.TensileTest;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.TensileTest
{
    /// <summary>
    /// 인장시험 시편 모델링 서비스
    /// SpaceClaim API를 사용하여 실제 3D 모델 생성
    /// </summary>
    public class SpecimenModelingService
    {
        /// <summary>
        /// 인장시편 생성 (시편 + 그립 장비)
        /// </summary>
        public DesignBody CreateTensileSpecimen(Part part, TensileSpecimenParameters parameters)
        {
            if (part == null)
                throw new ArgumentNullException(nameof(part));

            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            // 파라미터 유효성 검증
            if (!parameters.Validate(out string errorMessage))
                throw new ArgumentException(errorMessage);

            // 규격에 따라 시편 모델링
            DesignBody specimenBody;
            switch (parameters.SpecimenType)
            {
                // 금속 인장 (dog-bone)
                case ASTMSpecimenType.ASTM_E8_Standard:
                case ASTMSpecimenType.ASTM_E8_SubSize:
                case ASTMSpecimenType.ISO_6892_1:
                    specimenBody = CreateASTM_E8_Specimen(part, parameters);
                    break;

                // 플라스틱 인장 (dog-bone)
                case ASTMSpecimenType.ASTM_D638_TypeI:
                case ASTMSpecimenType.ASTM_D638_TypeII:
                case ASTMSpecimenType.ASTM_D638_TypeIII:
                case ASTMSpecimenType.ASTM_D638_TypeIV:
                case ASTMSpecimenType.ASTM_D638_TypeV:
                case ASTMSpecimenType.ISO_527_2_Type1A:
                case ASTMSpecimenType.ISO_527_2_Type1B:
                    specimenBody = CreateASTM_D638_Specimen(part, parameters);
                    break;

                // 노치 인장
                case ASTMSpecimenType.ASTM_E602_VNotch:
                case ASTMSpecimenType.ASTM_E602_UNotch:
                case ASTMSpecimenType.ASTM_E338:
                case ASTMSpecimenType.ASTM_E292:
                    specimenBody = CreateNotchedSpecimen(part, parameters);
                    break;

                // 구멍 시편 (직사각형 + 중앙 홀)
                case ASTMSpecimenType.ASTM_D5766_OHT:
                case ASTMSpecimenType.ASTM_D6484_OHC:
                case ASTMSpecimenType.ASTM_D6742_FHT:
                case ASTMSpecimenType.ASTM_D5961_Bearing:
                    specimenBody = CreateHoleSpecimen(part, parameters);
                    break;

                // 복합재 직선 인장 (직사각형 + 탭)
                case ASTMSpecimenType.ASTM_D3039:
                    specimenBody = CreateRectangularSpecimen(part, parameters);
                    break;

                // PCB
                case ASTMSpecimenType.IPC_TM650_Tensile:
                    specimenBody = CreateRectangularSpecimen(part, parameters);
                    break;

                case ASTMSpecimenType.IPC_TM650_PTHPull:
                    specimenBody = CreateHoleSpecimen(part, parameters);
                    break;

                // DMA 인장
                case ASTMSpecimenType.DMA_Tensile_Rectangle:
                    specimenBody = CreateRectangularSpecimen(part, parameters);
                    break;

                case ASTMSpecimenType.DMA_Tensile_DogBone:
                    specimenBody = CreateASTM_E8_Specimen(part, parameters);
                    break;

                // 전단 시편 (직사각형 + 양면 V-Notch)
                case ASTMSpecimenType.ASTM_D5379_Iosipescu:
                case ASTMSpecimenType.ASTM_D7078_VNotchRailShear:
                    specimenBody = CreateShearSpecimen(part, parameters);
                    break;

                case ASTMSpecimenType.Custom:
                    specimenBody = CreateCustomSpecimen(part, parameters);
                    break;

                default:
                    throw new NotSupportedException($"시편 타입 {parameters.SpecimenType}은(는) 지원되지 않습니다.");
            }

            // 시편 Face 이름 지정 (경계조건 설정용)
            NameSpecimenFaces(specimenBody, parameters);

            // 그립 장비(죠/클램프) 모델링 - 전단 시편은 별도 지그 사용하므로 제외
            if (parameters.SpecimenType != ASTMSpecimenType.ASTM_D5379_Iosipescu &&
                parameters.SpecimenType != ASTMSpecimenType.ASTM_D7078_VNotchRailShear)
            {
                CreateGrippingFixtures(part, parameters);
            }

            return specimenBody;
        }

        /// <summary>
        /// ASTM E8 시편 생성 (금속 평판 시편)
        /// </summary>
        private DesignBody CreateASTM_E8_Specimen(Part part, TensileSpecimenParameters p)
        {
            // mm를 m로 변환 (SpaceClaim은 m 단위 사용)
            double gl = GeometryUtils.MmToMeters(p.GaugeLength);
            double gw = GeometryUtils.MmToMeters(p.GaugeWidth);
            double gripW = GeometryUtils.MmToMeters(p.GripWidth);
            double gripL = GeometryUtils.MmToMeters(p.GripLength);
            double totalL = GeometryUtils.MmToMeters(p.TotalLength);
            double fillet = GeometryUtils.MmToMeters(p.FilletRadius);
            double thick = GeometryUtils.MmToMeters(p.Thickness);

            // 프로파일 생성 (대칭 형상)
            Profile profile = CreateE8Profile(gl, gw, gripW, gripL, totalL, fillet);

            // 압출
            Body body = Body.ExtrudeProfile(profile, thick);

            // 구멍 추가 (그립 영역에)
            body = AddGripHoles(body, gripL, gripW, totalL, thick);

            // DesignBody 생성
            string name = $"ASTM E8 Specimen (GL:{p.GaugeLength}mm)";
            return BodyBuilder.CreateDesignBody(part, name, body);
        }

        /// <summary>
        /// ASTM E8 프로파일 생성
        /// Dog-bone 형상 (게이지 영역 + 전환 영역 + 그립 영역)
        /// </summary>
        private Profile CreateE8Profile(double gaugeLength, double gaugeWidth, double gripWidth, double gripLength, double totalLength, double filletRadius)
        {
            // 반값 계산
            double halfGaugeL = gaugeLength / 2.0;
            double halfGaugeW = gaugeWidth / 2.0;
            double halfGripW = gripWidth / 2.0;
            double halfTotalL = totalLength / 2.0;

            // 그립 시작 위치 (끝에서 그립 길이만큼)
            double gripStartX = halfTotalL - gripLength;

            var curves = new List<ITrimmedCurve>();

            // 10개의 점으로 dog-bone 형상 생성 (시계 반대 방향)
            // 왼쪽 끝단 (하단)
            Point p1 = Point.Create(-halfTotalL, -halfGripW, 0);

            // 왼쪽 그립 시작 (하단)
            Point p2 = Point.Create(-gripStartX, -halfGripW, 0);

            // 왼쪽 그립 → 게이지 전환 (하단)
            Point p3 = Point.Create(-halfGaugeL, -halfGaugeW, 0);

            // 게이지 영역 (하단 → 우측 하단)
            Point p4 = Point.Create(halfGaugeL, -halfGaugeW, 0);

            // 게이지 → 오른쪽 그립 전환 (하단)
            Point p5 = Point.Create(gripStartX, -halfGripW, 0);

            // 오른쪽 그립 끝 (하단)
            Point p6 = Point.Create(halfTotalL, -halfGripW, 0);

            // 오른쪽 끝단 (상단)
            Point p7 = Point.Create(halfTotalL, halfGripW, 0);

            // 오른쪽 그립 시작 (상단)
            Point p8 = Point.Create(gripStartX, halfGripW, 0);

            // 오른쪽 그립 → 게이지 전환 (상단)
            Point p9 = Point.Create(halfGaugeL, halfGaugeW, 0);

            // 게이지 영역 (상단 우측 → 좌측)
            Point p10 = Point.Create(-halfGaugeL, halfGaugeW, 0);

            // 게이지 → 왼쪽 그립 전환 (상단)
            Point p11 = Point.Create(-gripStartX, halfGripW, 0);

            // 왼쪽 끝단 (상단)
            Point p12 = Point.Create(-halfTotalL, halfGripW, 0);

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
        /// 그립 영역에 구멍 추가
        /// TODO: Boolean 연산을 사용한 구멍 생성은 향후 구현 예정
        /// </summary>
        private Body AddGripHoles(Body specimenBody, double gripLength, double gripWidth, double totalLength, double thickness)
        {
            // TODO: SpaceClaim API의 Boolean 연산을 사용하여 구멍 추가
            // 현재는 기본 형상만 반환
            // 향후 Body.Subtract 또는 Body.Intersect API 확인 필요

            return specimenBody;
        }

        /// <summary>
        /// 그립 장비(죠/클램프) 모델링
        /// 실제 시험기의 그립 부분
        /// 탭이 있는 시편은 탭 두께만큼 그립 Z 오프셋 추가
        /// </summary>
        private void CreateGrippingFixtures(Part part, TensileSpecimenParameters p)
        {
            // mm를 m로 변환
            double gripL = GeometryUtils.MmToMeters(p.GripLength);
            double gripW = GeometryUtils.MmToMeters(p.GripWidth);
            double totalL = GeometryUtils.MmToMeters(p.TotalLength);
            double thick = GeometryUtils.MmToMeters(p.Thickness);
            double tabT = GeometryUtils.MmToMeters(p.TabThickness);

            // 직사각형 시편(탭 있는)은 gripLength 대신 tabLength 사용
            bool hasTab = p.TabLength > 0 && p.TabThickness > 0;
            if (hasTab)
            {
                gripL = GeometryUtils.MmToMeters(p.TabLength);
                gripW = GeometryUtils.MmToMeters(p.GaugeWidth); // 직사각형은 GaugeWidth = GripWidth
            }

            double halfTotalL = totalL / 2.0;

            // 그립 장비 치수
            double jawLength = gripL;  // 죠 길이
            double jawWidth = gripW + GeometryUtils.MmToMeters(10.0);  // 죠 폭 (시편보다 약간 넓게)
            double jawHeight = GeometryUtils.MmToMeters(30.0);  // 죠 높이

            double halfJawW = jawWidth / 2.0;

            // 탭이 있으면 탭 위에 그립이 올라감
            double upperJawX = -halfTotalL + gripL / 2.0;
            double upperJawZ = hasTab ? thick + tabT : thick;  // 탭 두께만큼 위로 오프셋

            Point upP1 = Point.Create(upperJawX - jawLength / 2.0, -halfJawW, upperJawZ);
            Point upP2 = Point.Create(upperJawX + jawLength / 2.0, -halfJawW, upperJawZ);
            Point upP3 = Point.Create(upperJawX + jawLength / 2.0, halfJawW, upperJawZ);
            Point upP4 = Point.Create(upperJawX - jawLength / 2.0, halfJawW, upperJawZ);

            var upperCurves = new List<ITrimmedCurve>
            {
                CurveSegment.Create(upP1, upP2),
                CurveSegment.Create(upP2, upP3),
                CurveSegment.Create(upP3, upP4),
                CurveSegment.Create(upP4, upP1)
            };

            // Z=upperJawZ 평면에서 프로파일 생성
            Plane upperPlane = Plane.Create(Frame.Create(Point.Create(0, 0, upperJawZ), Direction.DirX, Direction.DirY));
            Profile upperProfile = new Profile(upperPlane, upperCurves);
            Body upperJaw = Body.ExtrudeProfile(upperProfile, jawHeight);
            DesignBody upperLeftGrip = BodyBuilder.CreateDesignBody(part, "Upper Grip (Left)", upperJaw);
            NameGripFaces(upperLeftGrip, "Left", true);

            // 하부 그립 (Lower Jaw) - 왼쪽
            double lowerJawZ = hasTab ? -(tabT + jawHeight) : -jawHeight;  // 탭 두께만큼 아래로 오프셋

            Point lowP1 = Point.Create(upperJawX - jawLength / 2.0, -halfJawW, lowerJawZ);
            Point lowP2 = Point.Create(upperJawX + jawLength / 2.0, -halfJawW, lowerJawZ);
            Point lowP3 = Point.Create(upperJawX + jawLength / 2.0, halfJawW, lowerJawZ);
            Point lowP4 = Point.Create(upperJawX - jawLength / 2.0, halfJawW, lowerJawZ);

            var lowerCurves = new List<ITrimmedCurve>
            {
                CurveSegment.Create(lowP1, lowP2),
                CurveSegment.Create(lowP2, lowP3),
                CurveSegment.Create(lowP3, lowP4),
                CurveSegment.Create(lowP4, lowP1)
            };

            Plane lowerPlane = Plane.Create(Frame.Create(Point.Create(0, 0, lowerJawZ), Direction.DirX, Direction.DirY));
            Profile lowerProfile = new Profile(lowerPlane, lowerCurves);
            Body lowerJaw = Body.ExtrudeProfile(lowerProfile, jawHeight);
            DesignBody lowerLeftGrip = BodyBuilder.CreateDesignBody(part, "Lower Grip (Left)", lowerJaw);
            NameGripFaces(lowerLeftGrip, "Left", false);

            // 상부 그립 (Upper Jaw) - 오른쪽
            double upperJawXRight = halfTotalL - gripL / 2.0;

            Point upP1R = Point.Create(upperJawXRight - jawLength / 2.0, -halfJawW, upperJawZ);
            Point upP2R = Point.Create(upperJawXRight + jawLength / 2.0, -halfJawW, upperJawZ);
            Point upP3R = Point.Create(upperJawXRight + jawLength / 2.0, halfJawW, upperJawZ);
            Point upP4R = Point.Create(upperJawXRight - jawLength / 2.0, halfJawW, upperJawZ);

            var upperCurvesR = new List<ITrimmedCurve>
            {
                CurveSegment.Create(upP1R, upP2R),
                CurveSegment.Create(upP2R, upP3R),
                CurveSegment.Create(upP3R, upP4R),
                CurveSegment.Create(upP4R, upP1R)
            };

            Profile upperProfileR = new Profile(upperPlane, upperCurvesR);
            Body upperJawR = Body.ExtrudeProfile(upperProfileR, jawHeight);
            DesignBody upperRightGrip = BodyBuilder.CreateDesignBody(part, "Upper Grip (Right)", upperJawR);
            NameGripFaces(upperRightGrip, "Right", true);

            // 하부 그립 (Lower Jaw) - 오른쪽
            Point lowP1R = Point.Create(upperJawXRight - jawLength / 2.0, -halfJawW, lowerJawZ);
            Point lowP2R = Point.Create(upperJawXRight + jawLength / 2.0, -halfJawW, lowerJawZ);
            Point lowP3R = Point.Create(upperJawXRight + jawLength / 2.0, halfJawW, lowerJawZ);
            Point lowP4R = Point.Create(upperJawXRight - jawLength / 2.0, halfJawW, lowerJawZ);

            var lowerCurvesR = new List<ITrimmedCurve>
            {
                CurveSegment.Create(lowP1R, lowP2R),
                CurveSegment.Create(lowP2R, lowP3R),
                CurveSegment.Create(lowP3R, lowP4R),
                CurveSegment.Create(lowP4R, lowP1R)
            };

            Profile lowerProfileR = new Profile(lowerPlane, lowerCurvesR);
            Body lowerJawR = Body.ExtrudeProfile(lowerProfileR, jawHeight);
            DesignBody lowerRightGrip = BodyBuilder.CreateDesignBody(part, "Lower Grip (Right)", lowerJawR);
            NameGripFaces(lowerRightGrip, "Right", false);
        }

        /// <summary>
        /// ASTM D638 시편 생성 (플라스틱 덤벨 시편)
        /// </summary>
        private DesignBody CreateASTM_D638_Specimen(Part part, TensileSpecimenParameters p)
        {
            // ASTM E8과 유사하지만 필렛 반경이 매우 큼
            return CreateASTM_E8_Specimen(part, p);
        }

        /// <summary>
        /// 사용자 정의 시편 생성
        /// </summary>
        private DesignBody CreateCustomSpecimen(Part part, TensileSpecimenParameters p)
        {
            // 기본적으로 E8 형상 사용
            return CreateASTM_E8_Specimen(part, p);
        }

        /// <summary>
        /// 전단 시편 생성 (직사각형 + 양면 V-Notch)
        /// ASTM D5379 (Iosipescu), ASTM D7078 (V-Notch Rail Shear)
        /// </summary>
        private DesignBody CreateShearSpecimen(Part part, TensileSpecimenParameters p)
        {
            double width = GeometryUtils.MmToMeters(p.GaugeWidth);
            double totalL = GeometryUtils.MmToMeters(p.TotalLength);
            double thick = GeometryUtils.MmToMeters(p.Thickness);
            double notchDepth = GeometryUtils.MmToMeters(p.NotchDepth);

            double halfW = width / 2.0;
            double halfL = totalL / 2.0;

            // 1. 직사각형 프로파일 생성
            Point rp1 = Point.Create(-halfL, -halfW, 0);
            Point rp2 = Point.Create(halfL, -halfW, 0);
            Point rp3 = Point.Create(halfL, halfW, 0);
            Point rp4 = Point.Create(-halfL, halfW, 0);

            var curves = new List<ITrimmedCurve>
            {
                CurveSegment.Create(rp1, rp2),
                CurveSegment.Create(rp2, rp3),
                CurveSegment.Create(rp3, rp4),
                CurveSegment.Create(rp4, rp1)
            };

            Profile profile = new Profile(Plane.PlaneXY, curves);
            Body body = Body.ExtrudeProfile(profile, thick);

            // 2. 양면 V-Notch 커팅
            try
            {
                Body topNotch = CreateNotchCutBody(p, notchDepth, width, thick, true);
                body.Subtract(new Body[] { topNotch });

                Body bottomNotch = CreateNotchCutBody(p, notchDepth, width, thick, false);
                body.Subtract(new Body[] { bottomNotch });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Shear notch boolean operation failed: {ex.Message}");
            }

            string name = $"{p.SpecimenType} Specimen ({p.TotalLength}×{p.GaugeWidth}mm, ND:{p.NotchDepth}mm)";
            return BodyBuilder.CreateDesignBody(part, name, body);
        }

        /// <summary>
        /// ASTM E602 노치 인장 시편 생성 (V-Notch / U-Notch)
        /// 기본 dog-bone 형상에 게이지 중앙에 노치를 추가 (단면/양면 선택 가능)
        /// </summary>
        private DesignBody CreateNotchedSpecimen(Part part, TensileSpecimenParameters p)
        {
            double gl = GeometryUtils.MmToMeters(p.GaugeLength);
            double gw = GeometryUtils.MmToMeters(p.GaugeWidth);
            double gripW = GeometryUtils.MmToMeters(p.GripWidth);
            double gripL = GeometryUtils.MmToMeters(p.GripLength);
            double totalL = GeometryUtils.MmToMeters(p.TotalLength);
            double fillet = GeometryUtils.MmToMeters(p.FilletRadius);
            double thick = GeometryUtils.MmToMeters(p.Thickness);
            double notchDepth = GeometryUtils.MmToMeters(p.NotchDepth);

            // 1. 기본 dog-bone 프로파일 생성
            Profile baseProfile = CreateE8Profile(gl, gw, gripW, gripL, totalL, fillet);
            Body body = Body.ExtrudeProfile(baseProfile, thick);

            // 2. 노치 커팅 바디 생성
            try
            {
                // 한쪽 면 노치 (기본: top side)
                Body topNotch = CreateNotchCutBody(p, notchDepth, gw, thick, true);
                body.Subtract(new Body[] { topNotch });

                // 양면 노치인 경우 반대쪽도 추가
                if (p.IsDoubleNotch)
                {
                    Body bottomNotch = CreateNotchCutBody(p, notchDepth, gw, thick, false);
                    body.Subtract(new Body[] { bottomNotch });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Notch boolean operation failed: {ex.Message}");
            }

            // DesignBody 생성
            string notchType = (p.SpecimenType == ASTMSpecimenType.ASTM_E602_VNotch ||
                               p.SpecimenType == ASTMSpecimenType.ASTM_E338 ||
                               p.SpecimenType == ASTMSpecimenType.ASTM_E292) ? "V-Notch" : "U-Notch";
            string name = $"{p.SpecimenType} {notchType} Specimen (GL:{p.GaugeLength}mm, ND:{p.NotchDepth}mm)";
            return BodyBuilder.CreateDesignBody(part, name, body);
        }

        /// <summary>
        /// 노치 커팅용 바디 생성
        /// </summary>
        private Body CreateNotchCutBody(TensileSpecimenParameters p, double notchDepth, double gaugeWidth, double thickness, bool isTopSide)
        {
            double halfGW = gaugeWidth / 2.0;
            double notchCenterY = isTopSide ? halfGW : -halfGW;
            double cutLength = GeometryUtils.MmToMeters(2.0); // 커팅 바디 X방향 여유

            if (p.SpecimenType == ASTMSpecimenType.ASTM_E602_VNotch ||
                p.SpecimenType == ASTMSpecimenType.ASTM_E338 ||
                p.SpecimenType == ASTMSpecimenType.ASTM_E292 ||
                p.SpecimenType == ASTMSpecimenType.ASTM_D5379_Iosipescu ||
                p.SpecimenType == ASTMSpecimenType.ASTM_D7078_VNotchRailShear)
            {
                // V-Notch: 삼각형 단면
                double halfAngleRad = (p.NotchAngle / 2.0) * Math.PI / 180.0;
                double halfWidth = notchDepth * Math.Tan(halfAngleRad);

                Point vTip, vLeft, vRight;
                if (isTopSide)
                {
                    vTip = Point.Create(0, notchCenterY - notchDepth, 0);
                    vLeft = Point.Create(-halfWidth, notchCenterY, 0);
                    vRight = Point.Create(halfWidth, notchCenterY, 0);
                }
                else
                {
                    vTip = Point.Create(0, notchCenterY + notchDepth, 0);
                    vLeft = Point.Create(-halfWidth, notchCenterY, 0);
                    vRight = Point.Create(halfWidth, notchCenterY, 0);
                }

                var curves = new List<ITrimmedCurve>
                {
                    CurveSegment.Create(vLeft, vTip),
                    CurveSegment.Create(vTip, vRight),
                    CurveSegment.Create(vRight, vLeft)
                };

                Profile profile = new Profile(Plane.PlaneXY, curves);
                return Body.ExtrudeProfile(profile, thickness);
            }
            else
            {
                // U-Notch: 직사각형 + 반원 단면
                double notchRadius = GeometryUtils.MmToMeters(p.NotchRadius);
                double rectDepth = notchDepth - notchRadius;
                if (rectDepth < 0) rectDepth = 0;

                Point p1, p2, p3, p4;
                if (isTopSide)
                {
                    p1 = Point.Create(-notchRadius, notchCenterY, 0);
                    p2 = Point.Create(-notchRadius, notchCenterY - rectDepth, 0);
                    p3 = Point.Create(notchRadius, notchCenterY - rectDepth, 0);
                    p4 = Point.Create(notchRadius, notchCenterY, 0);
                }
                else
                {
                    p1 = Point.Create(-notchRadius, notchCenterY, 0);
                    p2 = Point.Create(-notchRadius, notchCenterY + rectDepth, 0);
                    p3 = Point.Create(notchRadius, notchCenterY + rectDepth, 0);
                    p4 = Point.Create(notchRadius, notchCenterY, 0);
                }

                var curves = new List<ITrimmedCurve>();
                curves.Add(CurveSegment.Create(p1, p2));

                // 반원 (U 부분)
                Point arcCenter = Point.Create(0, isTopSide ? notchCenterY - rectDepth : notchCenterY + rectDepth, 0);
                Frame arcFrame = Frame.Create(arcCenter, Direction.DirX, Direction.DirY);
                Circle arc = Circle.Create(arcFrame, notchRadius);

                // 반원 구간: p2에서 p3까지
                double startAngle, endAngle;
                if (isTopSide)
                {
                    startAngle = Math.PI;       // 180°
                    endAngle = 2.0 * Math.PI;   // 360°
                }
                else
                {
                    startAngle = 0;             // 0°
                    endAngle = Math.PI;         // 180°
                }
                curves.Add(CurveSegment.Create(arc, Interval.Create(startAngle, endAngle)));

                curves.Add(CurveSegment.Create(p3, p4));
                curves.Add(CurveSegment.Create(p4, p1));

                Profile profile = new Profile(Plane.PlaneXY, curves);
                return Body.ExtrudeProfile(profile, thickness);
            }
        }

        /// <summary>
        /// 직사각형 시편 생성 (D3039, IPC 등 - dog-bone 아닌 직선 형태)
        /// 탭이 있는 경우 탭도 함께 생성
        /// </summary>
        private DesignBody CreateRectangularSpecimen(Part part, TensileSpecimenParameters p)
        {
            double width = GeometryUtils.MmToMeters(p.GaugeWidth);
            double totalL = GeometryUtils.MmToMeters(p.TotalLength);
            double thick = GeometryUtils.MmToMeters(p.Thickness);

            double halfW = width / 2.0;
            double halfL = totalL / 2.0;

            // 직사각형 프로파일
            Point rp1 = Point.Create(-halfL, -halfW, 0);
            Point rp2 = Point.Create(halfL, -halfW, 0);
            Point rp3 = Point.Create(halfL, halfW, 0);
            Point rp4 = Point.Create(-halfL, halfW, 0);

            var curves = new List<ITrimmedCurve>
            {
                CurveSegment.Create(rp1, rp2),
                CurveSegment.Create(rp2, rp3),
                CurveSegment.Create(rp3, rp4),
                CurveSegment.Create(rp4, rp1)
            };

            Profile profile = new Profile(Plane.PlaneXY, curves);
            Body body = Body.ExtrudeProfile(profile, thick);

            string name = $"{p.SpecimenType} Specimen (W:{p.GaugeWidth}mm, L:{p.TotalLength}mm)";
            DesignBody designBody = BodyBuilder.CreateDesignBody(part, name, body);

            // 탭 생성
            if (p.TabLength > 0 && p.TabThickness > 0)
            {
                CreateTabs(part, p, width, totalL, thick);
            }

            return designBody;
        }

        /// <summary>
        /// 구멍 시편 생성 (D5766 OHT, D6484 OHC, D5961 Bearing 등)
        /// 직사각형 시편 + 중앙 원형/타원형 구멍
        /// </summary>
        private DesignBody CreateHoleSpecimen(Part part, TensileSpecimenParameters p)
        {
            double width = GeometryUtils.MmToMeters(p.GaugeWidth);
            double totalL = GeometryUtils.MmToMeters(p.TotalLength);
            double thick = GeometryUtils.MmToMeters(p.Thickness);

            double halfW = width / 2.0;
            double halfL = totalL / 2.0;

            // 직사각형 프로파일
            Point rp1 = Point.Create(-halfL, -halfW, 0);
            Point rp2 = Point.Create(halfL, -halfW, 0);
            Point rp3 = Point.Create(halfL, halfW, 0);
            Point rp4 = Point.Create(-halfL, halfW, 0);

            var curves = new List<ITrimmedCurve>
            {
                CurveSegment.Create(rp1, rp2),
                CurveSegment.Create(rp2, rp3),
                CurveSegment.Create(rp3, rp4),
                CurveSegment.Create(rp4, rp1)
            };

            Profile profile = new Profile(Plane.PlaneXY, curves);
            Body body = Body.ExtrudeProfile(profile, thick);

            // 구멍 뚫기 (Boolean Subtract)
            try
            {
                Body holeCut = CreateHoleCutBody(p, thick);
                body.Subtract(new Body[] { holeCut });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hole boolean operation failed: {ex.Message}");
            }

            string holeSuffix = p.IsEllipticalHole
                ? $"Ellipse:{p.HoleMajorAxis}x{p.HoleMinorAxis}mm"
                : $"D:{p.HoleDiameter}mm";
            string name = $"{p.SpecimenType} Specimen ({holeSuffix})";
            DesignBody designBody = BodyBuilder.CreateDesignBody(part, name, body);

            // 탭 생성
            if (p.TabLength > 0 && p.TabThickness > 0)
            {
                CreateTabs(part, p, width, totalL, thick);
            }

            return designBody;
        }

        /// <summary>
        /// 구멍 커팅용 바디 생성 (원형 또는 타원형)
        /// </summary>
        private Body CreateHoleCutBody(TensileSpecimenParameters p, double thickness)
        {
            if (p.IsEllipticalHole)
            {
                // 타원형 구멍
                double majorR = GeometryUtils.MmToMeters(p.HoleMajorAxis) / 2.0;
                double minorR = GeometryUtils.MmToMeters(p.HoleMinorAxis) / 2.0;

                // 타원 프로파일 (점으로 근사 - 32각형)
                int segments = 32;
                var curves = new List<ITrimmedCurve>();
                var points = new Point[segments];

                for (int i = 0; i < segments; i++)
                {
                    double angle = 2.0 * Math.PI * i / segments;
                    double x = minorR * Math.Cos(angle);   // 인장방향(X) = 단축
                    double y = majorR * Math.Sin(angle);   // 수직방향(Y) = 장축
                    points[i] = Point.Create(x, y, 0);
                }

                for (int i = 0; i < segments; i++)
                {
                    curves.Add(CurveSegment.Create(points[i], points[(i + 1) % segments]));
                }

                Profile profile = new Profile(Plane.PlaneXY, curves);
                return Body.ExtrudeProfile(profile, thickness);
            }
            else
            {
                // 원형 구멍
                double radius = GeometryUtils.MmToMeters(p.HoleDiameter) / 2.0;

                Frame circleFrame = Frame.Create(Point.Origin, Direction.DirX, Direction.DirY);
                Circle circle = Circle.Create(circleFrame, radius);
                ITrimmedCurve fullCircle = CurveSegment.Create(circle, Interval.Create(0, 2.0 * Math.PI));

                var curves = new List<ITrimmedCurve> { fullCircle };
                Profile profile = new Profile(Plane.PlaneXY, curves);
                return Body.ExtrudeProfile(profile, thickness);
            }
        }

        /// <summary>
        /// 탭 생성 (복합재/PCB 시편의 그립 영역 보호 탭)
        /// 시편 양 끝에 상하로 탭을 부착
        /// </summary>
        private void CreateTabs(Part part, TensileSpecimenParameters p, double specimenWidth, double totalLength, double specimenThick)
        {
            double tabL = GeometryUtils.MmToMeters(p.TabLength);
            double tabT = GeometryUtils.MmToMeters(p.TabThickness);
            double halfW = specimenWidth / 2.0;
            double halfL = totalLength / 2.0;

            // 왼쪽 상단 탭
            CreateSingleTab(part, -halfL, -halfL + tabL, -halfW, halfW, specimenThick, tabT, "Left Upper Tab");
            // 왼쪽 하단 탭
            CreateSingleTab(part, -halfL, -halfL + tabL, -halfW, halfW, -tabT, tabT, "Left Lower Tab");
            // 오른쪽 상단 탭
            CreateSingleTab(part, halfL - tabL, halfL, -halfW, halfW, specimenThick, tabT, "Right Upper Tab");
            // 오른쪽 하단 탭
            CreateSingleTab(part, halfL - tabL, halfL, -halfW, halfW, -tabT, tabT, "Right Lower Tab");
        }

        /// <summary>
        /// 단일 탭 생성
        /// </summary>
        private void CreateSingleTab(Part part, double x1, double x2, double y1, double y2, double zBase, double zHeight, string name)
        {
            Point tp1 = Point.Create(x1, y1, zBase);
            Point tp2 = Point.Create(x2, y1, zBase);
            Point tp3 = Point.Create(x2, y2, zBase);
            Point tp4 = Point.Create(x1, y2, zBase);

            var curves = new List<ITrimmedCurve>
            {
                CurveSegment.Create(tp1, tp2),
                CurveSegment.Create(tp2, tp3),
                CurveSegment.Create(tp3, tp4),
                CurveSegment.Create(tp4, tp1)
            };

            Plane tabPlane = Plane.Create(Frame.Create(Point.Create(0, 0, zBase), Direction.DirX, Direction.DirY));
            Profile profile = new Profile(tabPlane, curves);
            Body tabBody = Body.ExtrudeProfile(profile, zHeight);
            BodyBuilder.CreateDesignBody(part, name, tabBody);
        }

        /// <summary>
        /// 시편 Face 이름 지정
        /// 시뮬레이션 경계조건 설정을 위한 Face 명명
        /// </summary>
        private void NameSpecimenFaces(DesignBody specimenBody, TensileSpecimenParameters p)
        {
            if (specimenBody == null)
                return;

            try
            {
                Part part = specimenBody.Parent as Part;
                if (part == null)
                    return;

                double totalL = GeometryUtils.MmToMeters(p.TotalLength);
                double thick = GeometryUtils.MmToMeters(p.Thickness);
                double halfTotalL = totalL / 2.0;

                // 1. 시편 양 끝 면 (인장 방향 X축) - 변위 경계조건 적용 위치
                var leftEndFaces = FaceNamingHelper.FindPlanarFaces(
                    specimenBody,
                    Direction.DirX,
                    -halfTotalL,
                    FaceNamingHelper.Axis.X);
                FaceNamingHelper.NameFaces(part, leftEndFaces, "Specimen_LeftEnd");

                var rightEndFaces = FaceNamingHelper.FindPlanarFaces(
                    specimenBody,
                    Direction.DirX,
                    halfTotalL,
                    FaceNamingHelper.Axis.X);
                FaceNamingHelper.NameFaces(part, rightEndFaces, "Specimen_RightEnd");

                // 2. 시편 하단 면 (Z=0) - 그립과 접촉
                var bottomFaces = FaceNamingHelper.FindPlanarFaces(
                    specimenBody,
                    Direction.DirZ,
                    0.0,
                    FaceNamingHelper.Axis.Z);
                FaceNamingHelper.NameFaces(part, bottomFaces, "Specimen_Bottom_Contact");

                // 3. 시편 상단 면 (Z=thickness) - 그립과 접촉
                var topFaces = FaceNamingHelper.FindPlanarFaces(
                    specimenBody,
                    Direction.DirZ,
                    thick,
                    FaceNamingHelper.Axis.Z);
                FaceNamingHelper.NameFaces(part, topFaces, "Specimen_Top_Contact");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to name specimen faces: {ex.Message}");
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

                // 1. 그립 상단 면 (최대 Z) - 로드셀/액추에이터 연결 위치
                var topFace = FaceNamingHelper.FindExtremePlanarFace(
                    gripBody,
                    Direction.DirZ,
                    true);
                if (topFace != null)
                {
                    FaceNamingHelper.NameFace(part, topFace, $"Grip_{position}_{(isUpper ? "Upper" : "Lower")}_Top");
                }

                // 2. 그립 하단 면 (최소 Z) - 시편 접촉 또는 고정점
                var bottomFace = FaceNamingHelper.FindExtremePlanarFace(
                    gripBody,
                    Direction.DirZ,
                    false);
                if (bottomFace != null)
                {
                    string faceName = isUpper
                        ? $"Grip_{position}_{(isUpper ? "Upper" : "Lower")}_Contact"
                        : $"Grip_{position}_{(isUpper ? "Upper" : "Lower")}_Bottom";
                    FaceNamingHelper.NameFace(part, bottomFace, faceName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to name grip faces: {ex.Message}");
            }
        }
    }
}
