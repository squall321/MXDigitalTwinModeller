namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Laminate
{
    /// <summary>
    /// 개별 적층 레이어 정의
    /// </summary>
    public class LaminateLayerDefinition
    {
        /// <summary>
        /// 레이어 이름 (예: "CFRP_0", "Adhesive")
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 레이어 두께 [mm]
        /// </summary>
        public double ThicknessMm { get; set; }

        public LaminateLayerDefinition()
        {
            Name = "Layer";
            ThicknessMm = 0.25;
        }

        public LaminateLayerDefinition(string name, double thicknessMm)
        {
            Name = name;
            ThicknessMm = thicknessMm;
        }
    }
}
