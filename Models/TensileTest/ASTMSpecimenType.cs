using System.ComponentModel;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.TensileTest
{
    /// <summary>
    /// 인장시험 시편 규격 타입
    /// </summary>
    public enum ASTMSpecimenType
    {
        // ===== 금속 인장 =====

        /// <summary>
        /// ASTM E8 - Standard (금속 평판 시편)
        /// 시험: Tension Testing of Metallic Materials
        /// </summary>
        [Description("ASTM E8 - Standard (Metal)")]
        ASTM_E8_Standard = 0,

        /// <summary>
        /// ASTM E8 - SubSize (금속 서브사이즈 시편)
        /// 시험: Tension Testing of Metallic Materials
        /// </summary>
        [Description("ASTM E8 - SubSize (Metal)")]
        ASTM_E8_SubSize = 1,

        /// <summary>
        /// ISO 6892-1 (금속 인장 - 국제규격)
        /// 시험: Metallic Materials - Tensile Testing (ASTM E8 대응)
        /// </summary>
        [Description("ISO 6892-1 (Metal, International)")]
        ISO_6892_1 = 2,

        // ===== 플라스틱 인장 =====

        /// <summary>
        /// ASTM D638 - Type I (플라스틱 덤벨 시편)
        /// 시험: Tensile Properties of Plastics
        /// </summary>
        [Description("ASTM D638 - Type I (Plastic)")]
        ASTM_D638_TypeI = 10,

        /// <summary>
        /// ASTM D638 - Type II (플라스틱 덤벨 시편)
        /// 시험: Tensile Properties of Plastics
        /// </summary>
        [Description("ASTM D638 - Type II (Plastic)")]
        ASTM_D638_TypeII = 11,

        /// <summary>
        /// ASTM D638 - Type III (플라스틱 덤벨 시편)
        /// 시험: Tensile Properties of Plastics
        /// </summary>
        [Description("ASTM D638 - Type III (Plastic)")]
        ASTM_D638_TypeIII = 12,

        /// <summary>
        /// ASTM D638 - Type IV (플라스틱 덤벨 시편 - 소형)
        /// 시험: Tensile Properties of Plastics
        /// </summary>
        [Description("ASTM D638 - Type IV (Plastic)")]
        ASTM_D638_TypeIV = 13,

        /// <summary>
        /// ASTM D638 - Type V (플라스틱 덤벨 시편 - 초소형)
        /// 시험: Tensile Properties of Plastics
        /// </summary>
        [Description("ASTM D638 - Type V (Plastic)")]
        ASTM_D638_TypeV = 14,

        /// <summary>
        /// ISO 527-2 - Type 1A (플라스틱 인장 - 국제규격)
        /// 시험: Plastics - Determination of Tensile Properties (ASTM D638 대응)
        /// </summary>
        [Description("ISO 527-2 - Type 1A (Plastic, International)")]
        ISO_527_2_Type1A = 15,

        /// <summary>
        /// ISO 527-2 - Type 1B (플라스틱 인장 - 국제규격)
        /// 시험: Plastics - Determination of Tensile Properties
        /// </summary>
        [Description("ISO 527-2 - Type 1B (Plastic, International)")]
        ISO_527_2_Type1B = 16,

        // ===== 노치 인장 =====

        /// <summary>
        /// ASTM E602 - V-Notch (노치 인장 시편)
        /// 시험: Sharp-Notch Tension Testing
        /// </summary>
        [Description("ASTM E602 - V-Notch (Metal)")]
        ASTM_E602_VNotch = 20,

        /// <summary>
        /// ASTM E602 - U-Notch (노치 인장 시편)
        /// 시험: Sharp-Notch Tension Testing
        /// </summary>
        [Description("ASTM E602 - U-Notch (Metal)")]
        ASTM_E602_UNotch = 21,

        /// <summary>
        /// ASTM E338 - Sharp-Notch (고강도 판재 노치 인장)
        /// 시험: Sharp-Notch Tension Testing of High-Strength Sheet Materials
        /// </summary>
        [Description("ASTM E338 (High-Strength Sheet)")]
        ASTM_E338 = 22,

        /// <summary>
        /// ASTM E292 (고온 노치 인장)
        /// 시험: Notch Tension Testing at Elevated Temperatures
        /// </summary>
        [Description("ASTM E292 (Elevated Temperature)")]
        ASTM_E292 = 23,

        // ===== 구멍 시편 (OHT/OHC/Bearing) =====

        /// <summary>
        /// ASTM D5766 - OHT (복합재 오픈홀 인장)
        /// 시험: Open-Hole Tensile Strength of Polymer Matrix Composite Laminates
        /// </summary>
        [Description("ASTM D5766 - OHT (Composite)")]
        ASTM_D5766_OHT = 30,

        /// <summary>
        /// ASTM D6484 - OHC (복합재 오픈홀 압축)
        /// 시험: Open-Hole Compressive Strength of Polymer Matrix Composite Laminates
        /// </summary>
        [Description("ASTM D6484 - OHC (Composite)")]
        ASTM_D6484_OHC = 31,

        /// <summary>
        /// ASTM D6742 - FHT (복합재 필드홀 인장)
        /// 시험: Filled-Hole Tension and Compression Testing of Polymer Matrix Composites
        /// </summary>
        [Description("ASTM D6742 - FHT (Composite)")]
        ASTM_D6742_FHT = 32,

        /// <summary>
        /// ASTM D5961 - Bearing (복합재 베어링 강도)
        /// 시험: Bearing Response of Polymer Matrix Composite Laminates
        /// </summary>
        [Description("ASTM D5961 - Bearing (Composite)")]
        ASTM_D5961_Bearing = 33,

        // ===== 복합재 인장 =====

        /// <summary>
        /// ASTM D3039 (복합재 직선 인장)
        /// 시험: Tensile Properties of Polymer Matrix Composite Materials
        /// </summary>
        [Description("ASTM D3039 (Composite Tensile)")]
        ASTM_D3039 = 40,

        // ===== PCB =====

        /// <summary>
        /// IPC-TM-650 2.4.18.3 (PCB 인장)
        /// 시험: Tensile Strength and Elongation of FR-4 Laminates
        /// </summary>
        [Description("IPC-TM-650 2.4.18.3 (PCB Tensile)")]
        IPC_TM650_Tensile = 50,

        /// <summary>
        /// IPC-TM-650 2.4.1 (PCB 도금관통홀 인발)
        /// 시험: Plated Through-Hole Pull Strength
        /// </summary>
        [Description("IPC-TM-650 2.4.1 (PCB PTH Pull)")]
        IPC_TM650_PTHPull = 51,

        // ===== DMA 인장 =====

        /// <summary>
        /// DMA Tensile - Rectangle (DMA 인장 직사각형)
        /// 시험: Dynamic Mechanical Analysis - Tensile Mode
        /// </summary>
        [Description("DMA Tensile - Rectangle")]
        DMA_Tensile_Rectangle = 60,

        /// <summary>
        /// DMA Tensile - DogBone (DMA 인장 덤벨형)
        /// 시험: Dynamic Mechanical Analysis - Tensile Mode
        /// </summary>
        [Description("DMA Tensile - DogBone")]
        DMA_Tensile_DogBone = 61,

        // ===== 전단 시편 =====

        /// <summary>
        /// ASTM D5379 - Iosipescu 전단 시편 (V-Notch Shear)
        /// 시험: Shear Properties of Composite Materials by the V-Notched Beam Method
        /// 76×20×t mm, 양면 90° V-Notch, 노치깊이 4mm
        /// </summary>
        [Description("ASTM D5379 - Iosipescu (V-Notch Shear)")]
        ASTM_D5379_Iosipescu = 70,

        /// <summary>
        /// ASTM D7078 - V-Notch Rail Shear 시편
        /// 시험: Shear Properties of Composite Materials by V-Notched Rail Shear Method
        /// 56×76×t mm, 양면 90° V-Notch, 노치깊이 12.7mm
        /// </summary>
        [Description("ASTM D7078 - V-Notch Rail Shear")]
        ASTM_D7078_VNotchRailShear = 71,

        // ===== 사용자 정의 =====

        /// <summary>
        /// 사용자 정의
        /// </summary>
        [Description("Custom")]
        Custom = 99
    }
}
