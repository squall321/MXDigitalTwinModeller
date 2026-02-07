namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.BendingFixture
{
    /// <summary>
    /// 축 방향 열거형
    /// </summary>
    public enum AxisDirection { X, Y, Z }

    /// <summary>
    /// 기존 바디에 3점 벤딩 지지구조를 적용하기 위한 파라미터
    /// 모든 치수는 mm 단위 (SpaceClaim 내부 변환은 서비스에서 처리)
    /// </summary>
    public class BendingFixtureParameters
    {
        // ===== 방향 설정 (자동 감지 후 사용자 수정 가능) =====

        /// <summary>
        /// 스팬 방향 (가장 긴 축, 지지점 배치 방향)
        /// </summary>
        public AxisDirection SpanDirection { get; set; }

        /// <summary>
        /// 폭 방향 (중간 축, 지지구조 압출 방향)
        /// </summary>
        public AxisDirection WidthDirection { get; set; }

        /// <summary>
        /// 하중 방향 (가장 짧은 축, 누르는 방향)
        /// </summary>
        public AxisDirection LoadingDirection { get; set; }

        // ===== 스팬 설정 =====

        /// <summary>
        /// true: 바디 길이 비율로 스팬 결정, false: 절대값 사용
        /// </summary>
        public bool UseSpanRatio { get; set; }

        /// <summary>
        /// 스팬 비율 (0.0~1.0, 바디 스팬방향 길이 대비)
        /// </summary>
        public double SpanRatio { get; set; }

        /// <summary>
        /// 스팬 절대값 [mm] (UseSpanRatio=false일 때 사용)
        /// </summary>
        public double SpanMm { get; set; }

        // ===== 지지구조 치수 [mm] =====

        public double SupportDiameter { get; set; }
        public double LoadingNoseDiameter { get; set; }
        public double SupportHeight { get; set; }
        public double LoadingNoseHeight { get; set; }

        // ===== 계산값 (서비스에서 채움) =====

        /// <summary>
        /// 실제 적용될 스팬 [mm]
        /// </summary>
        public double ComputedSpanMm { get; set; }

        /// <summary>
        /// 바디의 스팬 방향 크기 [mm]
        /// </summary>
        public double BodyLengthMm { get; set; }

        /// <summary>
        /// 바디의 폭 방향 크기 [mm]
        /// </summary>
        public double BodyWidthMm { get; set; }

        /// <summary>
        /// 바디의 하중 방향 크기 [mm]
        /// </summary>
        public double BodyThicknessMm { get; set; }

        public BendingFixtureParameters()
        {
            SpanDirection = AxisDirection.X;
            WidthDirection = AxisDirection.Y;
            LoadingDirection = AxisDirection.Z;
            UseSpanRatio = true;
            SpanRatio = 0.8;
            SpanMm = 64.0;
            SupportDiameter = 3.8;
            LoadingNoseDiameter = 3.8;
            SupportHeight = 20.0;
            LoadingNoseHeight = 20.0;
        }

        /// <summary>
        /// 유효성 검증
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            double span = UseSpanRatio ? ComputedSpanMm : SpanMm;

            if (span <= 0)
            {
                errorMessage = "스팬은 0보다 커야 합니다.";
                return false;
            }

            if (span >= BodyLengthMm)
            {
                errorMessage = "스팬은 바디 길이보다 작아야 합니다.";
                return false;
            }

            if (SupportDiameter <= 0 || LoadingNoseDiameter <= 0)
            {
                errorMessage = "지지점 및 로딩 노즈 직경은 0보다 커야 합니다.";
                return false;
            }

            if (SupportHeight <= 0 || LoadingNoseHeight <= 0)
            {
                errorMessage = "지지점 및 로딩 노즈 높이는 0보다 커야 합니다.";
                return false;
            }

            // 3축이 모두 다른지 확인
            if (SpanDirection == WidthDirection || SpanDirection == LoadingDirection || WidthDirection == LoadingDirection)
            {
                errorMessage = "스팬, 폭, 하중 방향은 모두 다른 축이어야 합니다.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
