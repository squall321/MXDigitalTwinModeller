namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Compression
{
    /// <summary>
    /// 압축 시험 시편 표준 타입
    /// </summary>
    public enum CompressionSpecimenType
    {
        /// <summary>ASTM D695 직육면체 (12.7×12.7×25.4mm)</summary>
        ASTM_D695_Prism,

        /// <summary>ASTM D695 원기둥 (∅12.7×25.4mm)</summary>
        ASTM_D695_Cylinder,

        /// <summary>ISO 604 탄성계수용 (50×10×4mm)</summary>
        ISO_604_Modulus,

        /// <summary>ISO 604 강도용 (10×10×4mm)</summary>
        ISO_604_Strength,

        /// <summary>ASTM E9 금속 Short (L/D=2, ∅12.7×25.4mm)</summary>
        ASTM_E9_Short,

        /// <summary>ASTM E9 금속 Medium (L/D=3, ∅12.7×38.1mm)</summary>
        ASTM_E9_Medium,

        /// <summary>사용자 정의</summary>
        Custom
    }

    /// <summary>
    /// 시편 형상 타입
    /// </summary>
    public enum CompressionSpecimenShape
    {
        /// <summary>직육면체 (Prism)</summary>
        Prism,

        /// <summary>원기둥 (Cylinder)</summary>
        Cylinder
    }
}
