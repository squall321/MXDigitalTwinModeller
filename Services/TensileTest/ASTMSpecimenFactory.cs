using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.TensileTest;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.TensileTest
{
    /// <summary>
    /// 규격 시편의 기본 파라미터를 제공하는 팩토리 클래스
    /// </summary>
    public class ASTMSpecimenFactory
    {
        /// <summary>
        /// 규격에 따른 기본 파라미터 반환
        /// </summary>
        public TensileSpecimenParameters GetDefaultParameters(ASTMSpecimenType specimenType)
        {
            var parameters = new TensileSpecimenParameters
            {
                SpecimenType = specimenType
            };

            switch (specimenType)
            {
                // ===== 금속 인장 =====

                case ASTMSpecimenType.ASTM_E8_Standard:
                    parameters.GaugeLength = 50.0;
                    parameters.GaugeWidth = 12.5;
                    parameters.Thickness = 3.0;
                    parameters.GripWidth = 20.0;
                    parameters.TotalLength = 200.0;
                    parameters.FilletRadius = 12.5;
                    parameters.GripLength = 50.0;
                    break;

                case ASTMSpecimenType.ASTM_E8_SubSize:
                    parameters.GaugeLength = 25.0;
                    parameters.GaugeWidth = 6.0;
                    parameters.Thickness = 1.5;
                    parameters.GripWidth = 10.0;
                    parameters.TotalLength = 100.0;
                    parameters.FilletRadius = 6.0;
                    parameters.GripLength = 25.0;
                    break;

                case ASTMSpecimenType.ISO_6892_1:
                    // ISO 6892-1 비례시편 (ASTM E8 대응, 약간 다른 치수)
                    parameters.GaugeLength = 50.0;
                    parameters.GaugeWidth = 12.5;
                    parameters.Thickness = 3.0;
                    parameters.GripWidth = 20.0;
                    parameters.TotalLength = 200.0;
                    parameters.FilletRadius = 12.5;
                    parameters.GripLength = 50.0;
                    break;

                // ===== 플라스틱 인장 =====

                case ASTMSpecimenType.ASTM_D638_TypeI:
                    parameters.GaugeLength = 50.0;
                    parameters.GaugeWidth = 13.0;
                    parameters.Thickness = 3.2;
                    parameters.GripWidth = 19.0;
                    parameters.TotalLength = 165.0;
                    parameters.FilletRadius = 76.0;
                    parameters.GripLength = 50.0;
                    break;

                case ASTMSpecimenType.ASTM_D638_TypeII:
                    parameters.GaugeLength = 57.0;
                    parameters.GaugeWidth = 6.0;
                    parameters.Thickness = 3.2;
                    parameters.GripWidth = 19.0;
                    parameters.TotalLength = 183.0;
                    parameters.FilletRadius = 12.7;
                    parameters.GripLength = 50.0;
                    break;

                case ASTMSpecimenType.ASTM_D638_TypeIII:
                    parameters.GaugeLength = 50.0;
                    parameters.GaugeWidth = 19.0;
                    parameters.Thickness = 3.2;
                    parameters.GripWidth = 29.0;
                    parameters.TotalLength = 246.0;
                    parameters.FilletRadius = 76.0;
                    parameters.GripLength = 50.0;
                    break;

                case ASTMSpecimenType.ASTM_D638_TypeIV:
                    parameters.GaugeLength = 33.0;
                    parameters.GaugeWidth = 6.0;
                    parameters.Thickness = 3.2;
                    parameters.GripWidth = 19.0;
                    parameters.TotalLength = 115.0;
                    parameters.FilletRadius = 14.0;
                    parameters.GripLength = 25.0;
                    break;

                case ASTMSpecimenType.ASTM_D638_TypeV:
                    parameters.GaugeLength = 7.62;
                    parameters.GaugeWidth = 3.18;
                    parameters.Thickness = 3.2;
                    parameters.GripWidth = 9.53;
                    parameters.TotalLength = 63.5;
                    parameters.FilletRadius = 12.7;
                    parameters.GripLength = 25.0;
                    break;

                case ASTMSpecimenType.ISO_527_2_Type1A:
                    // ISO 527-2 Type 1A (사출 성형용)
                    parameters.GaugeLength = 80.0;
                    parameters.GaugeWidth = 10.0;
                    parameters.Thickness = 4.0;
                    parameters.GripWidth = 20.0;
                    parameters.TotalLength = 170.0;
                    parameters.FilletRadius = 24.0;
                    parameters.GripLength = 30.0;
                    break;

                case ASTMSpecimenType.ISO_527_2_Type1B:
                    // ISO 527-2 Type 1B (기계 가공용)
                    parameters.GaugeLength = 60.0;
                    parameters.GaugeWidth = 10.0;
                    parameters.Thickness = 4.0;
                    parameters.GripWidth = 20.0;
                    parameters.TotalLength = 150.0;
                    parameters.FilletRadius = 20.0;
                    parameters.GripLength = 30.0;
                    break;

                // ===== 노치 인장 =====

                case ASTMSpecimenType.ASTM_E602_VNotch:
                    parameters.GaugeLength = 50.0;
                    parameters.GaugeWidth = 12.5;
                    parameters.Thickness = 3.0;
                    parameters.GripWidth = 20.0;
                    parameters.TotalLength = 200.0;
                    parameters.FilletRadius = 12.5;
                    parameters.GripLength = 50.0;
                    parameters.NotchDepth = 2.0;
                    parameters.NotchAngle = 60.0;
                    parameters.IsDoubleNotch = false;
                    break;

                case ASTMSpecimenType.ASTM_E602_UNotch:
                    parameters.GaugeLength = 50.0;
                    parameters.GaugeWidth = 12.5;
                    parameters.Thickness = 3.0;
                    parameters.GripWidth = 20.0;
                    parameters.TotalLength = 200.0;
                    parameters.FilletRadius = 12.5;
                    parameters.GripLength = 50.0;
                    parameters.NotchDepth = 2.0;
                    parameters.NotchRadius = 1.0;
                    parameters.IsDoubleNotch = false;
                    break;

                case ASTMSpecimenType.ASTM_E338:
                    // 고강도 판재용 - 얇은 시편, 날카로운 노치
                    parameters.GaugeLength = 50.0;
                    parameters.GaugeWidth = 25.4;
                    parameters.Thickness = 1.6;
                    parameters.GripWidth = 38.0;
                    parameters.TotalLength = 200.0;
                    parameters.FilletRadius = 12.5;
                    parameters.GripLength = 50.0;
                    parameters.NotchDepth = 3.18;       // 1/8 inch
                    parameters.NotchAngle = 60.0;
                    parameters.IsDoubleNotch = true;    // E338은 양면 노치
                    break;

                case ASTMSpecimenType.ASTM_E292:
                    // 고온 노치 인장 - E602와 유사
                    parameters.GaugeLength = 50.0;
                    parameters.GaugeWidth = 12.5;
                    parameters.Thickness = 3.0;
                    parameters.GripWidth = 20.0;
                    parameters.TotalLength = 200.0;
                    parameters.FilletRadius = 12.5;
                    parameters.GripLength = 50.0;
                    parameters.NotchDepth = 2.0;
                    parameters.NotchAngle = 60.0;
                    parameters.IsDoubleNotch = false;
                    break;

                // ===== 구멍 시편 =====

                case ASTMSpecimenType.ASTM_D5766_OHT:
                    // 복합재 OHT - 직사각형 시편 + 중앙 원형 구멍
                    parameters.GaugeWidth = 36.0;
                    parameters.Thickness = 4.0;
                    parameters.TotalLength = 300.0;
                    parameters.GripLength = 50.0;
                    parameters.GripWidth = 36.0;
                    parameters.GaugeLength = 200.0;
                    parameters.FilletRadius = 0.0;
                    parameters.IsRectangular = true;
                    parameters.HoleDiameter = 6.35;     // 1/4 inch 표준
                    parameters.TabLength = 56.0;
                    parameters.TabThickness = 1.5;
                    break;

                case ASTMSpecimenType.ASTM_D6484_OHC:
                    // 복합재 OHC - 압축용 짧은 시편 + 중앙 원형 구멍
                    parameters.GaugeWidth = 36.0;
                    parameters.Thickness = 4.0;
                    parameters.TotalLength = 300.0;
                    parameters.GripLength = 50.0;
                    parameters.GripWidth = 36.0;
                    parameters.GaugeLength = 200.0;
                    parameters.FilletRadius = 0.0;
                    parameters.IsRectangular = true;
                    parameters.HoleDiameter = 6.35;
                    parameters.TabLength = 56.0;
                    parameters.TabThickness = 1.5;
                    break;

                case ASTMSpecimenType.ASTM_D6742_FHT:
                    // 복합재 Filled-Hole - D5766과 동일 시편, 볼트 체결
                    parameters.GaugeWidth = 36.0;
                    parameters.Thickness = 4.0;
                    parameters.TotalLength = 300.0;
                    parameters.GripLength = 50.0;
                    parameters.GripWidth = 36.0;
                    parameters.GaugeLength = 200.0;
                    parameters.FilletRadius = 0.0;
                    parameters.IsRectangular = true;
                    parameters.HoleDiameter = 6.35;
                    parameters.TabLength = 56.0;
                    parameters.TabThickness = 1.5;
                    break;

                case ASTMSpecimenType.ASTM_D5961_Bearing:
                    // 복합재 Bearing - 핀 구멍이 가장자리 쪽
                    parameters.GaugeWidth = 36.0;
                    parameters.Thickness = 4.0;
                    parameters.TotalLength = 200.0;
                    parameters.GripLength = 50.0;
                    parameters.GripWidth = 36.0;
                    parameters.GaugeLength = 100.0;
                    parameters.FilletRadius = 0.0;
                    parameters.IsRectangular = true;
                    parameters.HoleDiameter = 6.35;
                    parameters.TabLength = 0.0;
                    parameters.TabThickness = 0.0;
                    break;

                // ===== 복합재 인장 =====

                case ASTMSpecimenType.ASTM_D3039:
                    // 복합재 직선 인장 - 직사각형 + 탭
                    parameters.GaugeWidth = 25.0;
                    parameters.Thickness = 2.5;
                    parameters.TotalLength = 250.0;
                    parameters.GripLength = 50.0;
                    parameters.GripWidth = 25.0;
                    parameters.GaugeLength = 150.0;
                    parameters.FilletRadius = 0.0;
                    parameters.IsRectangular = true;
                    parameters.TabLength = 56.0;
                    parameters.TabThickness = 1.5;
                    break;

                // ===== PCB =====

                case ASTMSpecimenType.IPC_TM650_Tensile:
                    // PCB FR-4 인장 - 직사각형 시편
                    parameters.GaugeWidth = 25.4;       // 1 inch
                    parameters.Thickness = 1.6;         // 일반적인 PCB 두께
                    parameters.TotalLength = 200.0;
                    parameters.GripLength = 50.0;
                    parameters.GripWidth = 25.4;
                    parameters.GaugeLength = 100.0;
                    parameters.FilletRadius = 0.0;
                    parameters.IsRectangular = true;
                    break;

                case ASTMSpecimenType.IPC_TM650_PTHPull:
                    // PCB PTH 인발 - 소형 시편 + 관통홀
                    parameters.GaugeWidth = 25.4;
                    parameters.Thickness = 1.6;
                    parameters.TotalLength = 50.0;
                    parameters.GripLength = 15.0;
                    parameters.GripWidth = 25.4;
                    parameters.GaugeLength = 20.0;
                    parameters.FilletRadius = 0.0;
                    parameters.IsRectangular = true;
                    parameters.HoleDiameter = 1.0;      // 일반적인 PTH 직경
                    break;

                // ===== DMA 인장 =====

                case ASTMSpecimenType.DMA_Tensile_Rectangle:
                    // DMA 직사각형 인장 - 소형 직사각 시편
                    parameters.GaugeLength = 20.0;
                    parameters.GaugeWidth = 10.0;
                    parameters.Thickness = 3.0;
                    parameters.GripWidth = 10.0;
                    parameters.TotalLength = 50.0;
                    parameters.FilletRadius = 0.0;
                    parameters.GripLength = 10.0;
                    parameters.IsRectangular = true;
                    break;

                case ASTMSpecimenType.DMA_Tensile_DogBone:
                    // DMA 덤벨형 인장 - 소형 dog-bone 시편
                    parameters.GaugeLength = 20.0;
                    parameters.GaugeWidth = 10.0;
                    parameters.Thickness = 3.0;
                    parameters.GripWidth = 15.0;
                    parameters.TotalLength = 50.0;
                    parameters.FilletRadius = 5.0;
                    parameters.GripLength = 10.0;
                    break;

                // ===== 전단 시편 =====

                case ASTMSpecimenType.ASTM_D5379_Iosipescu:
                    // Iosipescu V-Notch 전단 시편 - 76×20×t, 양면 90° V-Notch
                    parameters.GaugeWidth = 20.0;
                    parameters.Thickness = 4.0;
                    parameters.TotalLength = 76.0;
                    parameters.GripLength = 0.0;
                    parameters.GripWidth = 20.0;
                    parameters.GaugeLength = 12.0;   // 노치 사이 유효 폭 (20 - 2×4)
                    parameters.FilletRadius = 0.0;
                    parameters.IsRectangular = true;
                    parameters.NotchDepth = 4.0;
                    parameters.NotchAngle = 90.0;
                    parameters.IsDoubleNotch = true;
                    break;

                case ASTMSpecimenType.ASTM_D7078_VNotchRailShear:
                    // V-Notch Rail Shear 시편 - 56×76×t, 양면 90° V-Notch
                    parameters.GaugeWidth = 56.0;
                    parameters.Thickness = 4.0;
                    parameters.TotalLength = 76.0;
                    parameters.GripLength = 0.0;
                    parameters.GripWidth = 56.0;
                    parameters.GaugeLength = 30.6;   // 노치 사이 유효 폭 (56 - 2×12.7)
                    parameters.FilletRadius = 0.0;
                    parameters.IsRectangular = true;
                    parameters.NotchDepth = 12.7;
                    parameters.NotchAngle = 90.0;
                    parameters.IsDoubleNotch = true;
                    break;

                case ASTMSpecimenType.Custom:
                default:
                    break;
            }

            return parameters;
        }

        /// <summary>
        /// 규격 타입 설명 반환
        /// </summary>
        public string GetSpecimenDescription(ASTMSpecimenType specimenType)
        {
            switch (specimenType)
            {
                // 금속 인장
                case ASTMSpecimenType.ASTM_E8_Standard:
                    return "ASTM E8 - Standard (금속 평판 시편, GL:50mm, GW:12.5mm)";
                case ASTMSpecimenType.ASTM_E8_SubSize:
                    return "ASTM E8 - SubSize (금속 서브사이즈, GL:25mm, GW:6mm)";
                case ASTMSpecimenType.ISO_6892_1:
                    return "ISO 6892-1 (금속 인장 국제규격, GL:50mm, GW:12.5mm)";

                // 플라스틱 인장
                case ASTMSpecimenType.ASTM_D638_TypeI:
                    return "ASTM D638 - Type I (플라스틱 덤벨, GL:50mm, GW:13mm)";
                case ASTMSpecimenType.ASTM_D638_TypeII:
                    return "ASTM D638 - Type II (플라스틱 덤벨, GL:57mm, GW:6mm)";
                case ASTMSpecimenType.ASTM_D638_TypeIII:
                    return "ASTM D638 - Type III (플라스틱 덤벨, GL:50mm, GW:19mm)";
                case ASTMSpecimenType.ASTM_D638_TypeIV:
                    return "ASTM D638 - Type IV (플라스틱 소형, GL:33mm, GW:6mm)";
                case ASTMSpecimenType.ASTM_D638_TypeV:
                    return "ASTM D638 - Type V (플라스틱 초소형, GL:7.62mm, GW:3.18mm)";
                case ASTMSpecimenType.ISO_527_2_Type1A:
                    return "ISO 527-2 Type 1A (사출 성형용, GL:80mm, GW:10mm)";
                case ASTMSpecimenType.ISO_527_2_Type1B:
                    return "ISO 527-2 Type 1B (기계 가공용, GL:60mm, GW:10mm)";

                // 노치 인장
                case ASTMSpecimenType.ASTM_E602_VNotch:
                    return "ASTM E602 - V-Notch (노치 인장, ND:2mm, NA:60°)";
                case ASTMSpecimenType.ASTM_E602_UNotch:
                    return "ASTM E602 - U-Notch (노치 인장, ND:2mm, NR:1mm)";
                case ASTMSpecimenType.ASTM_E338:
                    return "ASTM E338 (고강도 판재 노치, W:25.4mm, 양면 V-Notch)";
                case ASTMSpecimenType.ASTM_E292:
                    return "ASTM E292 (고온 노치 인장, ND:2mm, NA:60°)";

                // 구멍 시편
                case ASTMSpecimenType.ASTM_D5766_OHT:
                    return "ASTM D5766 - OHT (복합재 오픈홀 인장, D:6.35mm)";
                case ASTMSpecimenType.ASTM_D6484_OHC:
                    return "ASTM D6484 - OHC (복합재 오픈홀 압축, D:6.35mm)";
                case ASTMSpecimenType.ASTM_D6742_FHT:
                    return "ASTM D6742 - FHT (복합재 필드홀 인장, D:6.35mm)";
                case ASTMSpecimenType.ASTM_D5961_Bearing:
                    return "ASTM D5961 - Bearing (복합재 베어링, D:6.35mm)";

                // 복합재 인장
                case ASTMSpecimenType.ASTM_D3039:
                    return "ASTM D3039 (복합재 직선 인장, W:25mm, Tab:56mm)";

                // PCB
                case ASTMSpecimenType.IPC_TM650_Tensile:
                    return "IPC-TM-650 2.4.18.3 (PCB 인장, W:25.4mm, T:1.6mm)";
                case ASTMSpecimenType.IPC_TM650_PTHPull:
                    return "IPC-TM-650 2.4.1 (PCB PTH 인발, D:1.0mm)";

                // DMA 인장
                case ASTMSpecimenType.DMA_Tensile_Rectangle:
                    return "DMA Tensile - Rectangle (직사각형, L:50mm, W:10mm, T:3mm)";
                case ASTMSpecimenType.DMA_Tensile_DogBone:
                    return "DMA Tensile - DogBone (덤벨형, GL:20mm, GW:10mm, T:3mm)";

                // 전단 시편
                case ASTMSpecimenType.ASTM_D5379_Iosipescu:
                    return "ASTM D5379 - Iosipescu (V-Notch 전단, 76×20mm, ND:4mm, NA:90°)";
                case ASTMSpecimenType.ASTM_D7078_VNotchRailShear:
                    return "ASTM D7078 - V-Notch Rail Shear (56×76mm, ND:12.7mm, NA:90°)";

                case ASTMSpecimenType.Custom:
                    return "사용자 정의 시편";

                default:
                    return "알 수 없는 규격";
            }
        }
    }
}
