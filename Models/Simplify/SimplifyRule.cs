namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Simplify
{
    public class SimplifyRule
    {
        public string Keyword { get; set; }
        public SimplifyMode Mode { get; set; }

        public SimplifyRule()
        {
            Keyword = "";
            Mode = SimplifyMode.BoundingBox;
        }

        public SimplifyRule(string keyword, SimplifyMode mode)
        {
            Keyword = keyword ?? "";
            Mode = mode;
        }
    }
}
