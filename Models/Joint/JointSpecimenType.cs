namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Joint
{
    /// <summary>
    /// 접합부 시편 타입
    /// </summary>
    public enum JointSpecimenType
    {
        /// <summary>ASTM D1002 - 단일겹치기 전단 (Single Lap Shear)</summary>
        ASTM_D1002_SingleLap,

        /// <summary>ASTM D3528 - 이중겹치기 전단 (Double Lap Shear)</summary>
        ASTM_D3528_DoubleLap,

        /// <summary>스카프 접합 (Scarf Joint)</summary>
        Scarf_Joint,

        /// <summary>맞대기 접합 (Butt Joint)</summary>
        Butt_Joint,

        /// <summary>T형 접합 (T-Joint, Pull-off)</summary>
        T_Joint,

        /// <summary>사용자 정의</summary>
        Custom
    }
}
