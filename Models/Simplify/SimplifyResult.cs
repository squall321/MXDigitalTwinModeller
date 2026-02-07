using System.Collections.Generic;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Simplify
{
    public class SimplifyResult
    {
        public int MatchedCount { get; set; }
        public int ProcessedCount { get; set; }
        public int FailedCount { get; set; }
        public List<string> Log { get; set; }

        public SimplifyResult()
        {
            Log = new List<string>();
        }
    }
}
