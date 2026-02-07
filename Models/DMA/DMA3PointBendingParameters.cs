namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.DMA
{
    /// <summary>
    /// DMA 3점 굽힘시편 파라미터 클래스
    /// 모든 치수는 mm 단위
    /// 표준 규격: ASTM D790, ISO 178
    /// </summary>
    public class DMA3PointBendingParameters
    {
        /// <summary>
        /// 시편 타입 (표준/사용자정의)
        /// </summary>
        public DMASpecimenType SpecimenType { get; set; }

        /// <summary>
        /// 길이 (Length) [mm]
        /// 표준: 80mm, 범위: 50-150mm
        /// </summary>
        public double Length { get; set; }

        /// <summary>
        /// 폭 (Width) [mm]
        /// 표준: 10mm, 범위: 5-25mm
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// 두께 (Thickness) [mm]
        /// 표준: 4mm, 범위: 1-10mm
        /// </summary>
        public double Thickness { get; set; }

        /// <summary>
        /// 지지점 간격 (Span) [mm]
        /// 표준: 64mm (16×Thickness)
        /// 범위: 16×Thickness ~ 32×Thickness
        /// </summary>
        public double Span { get; set; }

        /// <summary>
        /// 하부 지지점 직경 (Support Point Diameter) [mm]
        /// 표준: 8mm, 범위: 5-15mm
        /// </summary>
        public double SupportDiameter { get; set; }

        /// <summary>
        /// 상부 로딩 노즈 직경 (Loading Nose Diameter) [mm]
        /// 표준: 8mm, 범위: 5-15mm
        /// </summary>
        public double LoadingNoseDiameter { get; set; }

        /// <summary>
        /// 지지점 높이 (Support Point Height) [mm]
        /// 하부 지지점의 원통 높이
        /// 표준: 20mm
        /// </summary>
        public double SupportHeight { get; set; }

        /// <summary>
        /// 로딩 노즈 높이 (Loading Nose Height) [mm]
        /// 상부 로딩 노즈의 원통 높이
        /// 표준: 20mm
        /// </summary>
        public double LoadingNoseHeight { get; set; }

        /// <summary>
        /// 기본 생성자 - 표준 치수로 초기화
        /// </summary>
        public DMA3PointBendingParameters()
        {
            SpecimenType = DMASpecimenType.Standard;
            Length = 80.0;
            Width = 10.0;
            Thickness = 4.0;
            Span = 64.0; // 16 × 4mm
            SupportDiameter = 8.0;  // 두꺼운 반원 형태
            LoadingNoseDiameter = 8.0;  // 두꺼운 반원 형태
            SupportHeight = 20.0;
            LoadingNoseHeight = 20.0;
        }

        /// <summary>
        /// 파라미터 복사 생성자
        /// </summary>
        public DMA3PointBendingParameters(DMA3PointBendingParameters other)
        {
            SpecimenType = other.SpecimenType;
            Length = other.Length;
            Width = other.Width;
            Thickness = other.Thickness;
            Span = other.Span;
            SupportDiameter = other.SupportDiameter;
            LoadingNoseDiameter = other.LoadingNoseDiameter;
            SupportHeight = other.SupportHeight;
            LoadingNoseHeight = other.LoadingNoseHeight;
        }

        /// <summary>
        /// 파라미터 유효성 검증 (필수 조건만 검사, 생성 가능 여부)
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            // 길이 검증
            if (Length <= 0)
            {
                errorMessage = "길이는 0보다 커야 합니다.";
                return false;
            }

            // 폭 검증
            if (Width <= 0)
            {
                errorMessage = "폭은 0보다 커야 합니다.";
                return false;
            }

            // 두께 검증
            if (Thickness <= 0)
            {
                errorMessage = "두께는 0보다 커야 합니다.";
                return false;
            }

            // 스팬 검증
            if (Span <= 0)
            {
                errorMessage = "지지점 간격은 0보다 커야 합니다.";
                return false;
            }

            // 스팬이 길이보다 작아야 함
            if (Span >= Length)
            {
                errorMessage = "지지점 간격은 시편 길이보다 작아야 합니다.";
                return false;
            }

            // 지지점 직경 검증
            if (SupportDiameter <= 0)
            {
                errorMessage = "지지점 직경은 0보다 커야 합니다.";
                return false;
            }

            // 로딩 노즈 직경 검증
            if (LoadingNoseDiameter <= 0)
            {
                errorMessage = "로딩 노즈 직경은 0보다 커야 합니다.";
                return false;
            }

            // 높이 검증
            if (SupportHeight <= 0 || LoadingNoseHeight <= 0)
            {
                errorMessage = "지지점 및 로딩 노즈 높이는 0보다 커야 합니다.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 표준 범위 이탈 경고 메시지 반환 (경고가 없으면 빈 문자열)
        /// </summary>
        public string GetRangeWarnings()
        {
            var warnings = new System.Collections.Generic.List<string>();

            // 길이 범위 경고
            if (Length < 50 || Length > 150)
            {
                warnings.Add($"• 길이 권장 범위: 50-150mm (현재: {Length:F1}mm)");
            }

            // 폭 범위 경고
            if (Width < 5 || Width > 25)
            {
                warnings.Add($"• 폭 권장 범위: 5-25mm (현재: {Width:F1}mm)");
            }

            // 두께 범위 경고
            if (Thickness < 1 || Thickness > 10)
            {
                warnings.Add($"• 두께 권장 범위: 1-10mm (현재: {Thickness:F1}mm)");
            }

            // 스팬/두께 비율 경고 (16:1 ~ 32:1)
            double spanToThicknessRatio = Span / Thickness;
            if (spanToThicknessRatio < 16 || spanToThicknessRatio > 32)
            {
                warnings.Add($"• 스팬/두께 비율 권장: 16:1~32:1 (현재: {spanToThicknessRatio:F1}:1)");
            }

            // 지지점 직경 범위 경고
            if (SupportDiameter < 5 || SupportDiameter > 15)
            {
                warnings.Add($"• 지지점 직경 권장 범위: 5-15mm (현재: {SupportDiameter:F1}mm)");
            }

            // 로딩 노즈 직경 범위 경고
            if (LoadingNoseDiameter < 5 || LoadingNoseDiameter > 15)
            {
                warnings.Add($"• 로딩노즈 직경 권장 범위: 5-15mm (현재: {LoadingNoseDiameter:F1}mm)");
            }

            return warnings.Count > 0 ? string.Join("\n", warnings) : string.Empty;
        }

        /// <summary>
        /// 문자열 표현
        /// </summary>
        public override string ToString()
        {
            return $"DMA 3-Point Bending - L:{Length}mm, W:{Width}mm, T:{Thickness}mm, Span:{Span}mm";
        }
    }
}
