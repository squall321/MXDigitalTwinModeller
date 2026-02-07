namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Fatigue
{
    /// <summary>
    /// 피로 시편 규격 타입
    /// </summary>
    public enum FatigueSpecimenType
    {
        /// <summary>ASTM E466 - 하중제어 균일 게이지 (Dog-bone, 평판)</summary>
        ASTM_E466_Uniform,

        /// <summary>ASTM E466 - 하중제어 모래시계형 (Hourglass, 평판)</summary>
        ASTM_E466_Hourglass,

        /// <summary>ASTM E606 - 변형률제어 균일 게이지 (LCF, 원형단면)</summary>
        ASTM_E606,

        /// <summary>ASTM E647 - Compact Tension (피로균열성장)</summary>
        ASTM_E647_CT,

        /// <summary>ASTM E647 - Middle Tension (피로균열성장)</summary>
        ASTM_E647_MT,

        /// <summary>ASTM E2207 - 다축 피로 (박벽 원통)</summary>
        ASTM_E2207,

        /// <summary>사용자 정의</summary>
        Custom
    }

    /// <summary>
    /// 피로 시편 단면 형상
    /// </summary>
    public enum FatigueSectionShape
    {
        /// <summary>직사각형 (평판)</summary>
        Rectangular,

        /// <summary>원형</summary>
        Circular,

        /// <summary>원통형 (Tubular)</summary>
        Tubular
    }
}
