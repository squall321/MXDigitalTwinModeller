namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer
{
    /// <summary>
    /// 얇고 긴 cut (안테나 라인, 슬릿).
    /// Wall pair 중 두께가 매우 작고 종횡비가 큰 것.
    /// </summary>
    public class SlitFeature
    {
        /// <summary>식별자 (예: "SL1")</summary>
        public string Id { get; set; }

        /// <summary>관련 wall feature 의 face indices</summary>
        public int FaceA { get; set; }
        public int FaceB { get; set; }

        /// <summary>slit 폭 (두 평행 면 거리, mm)</summary>
        public double WidthMm { get; set; }

        /// <summary>slit 길이 (긴 방향, mm) — bounding box 의 가장 큰 차원</summary>
        public double LengthMm { get; set; }

        /// <summary>종횡비 (length / width). 보통 > 5</summary>
        public double AspectRatio { get; set; }

        /// <summary>slit 방향 (단위 벡터, 긴 축)</summary>
        public double[] LongAxis { get; set; }
    }
}
