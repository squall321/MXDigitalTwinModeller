namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.CAI
{
    /// <summary>
    /// CAI (Compression After Impact) 시편 규격 타입
    /// </summary>
    public enum CAISpecimenType
    {
        /// <summary>ASTM D7137/D7136 - CAI 표준 (150×100mm)</summary>
        ASTM_D7137,

        /// <summary>ASTM D6264 - 낙추충격 (Drop Weight Impact, 150×100mm)</summary>
        ASTM_D6264,

        /// <summary>Boeing BSS 7260 - 항공 산업용 CAI (150×100mm)</summary>
        Boeing_BSS7260,

        /// <summary>사용자 정의</summary>
        Custom
    }
}
