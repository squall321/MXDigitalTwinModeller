namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.DMA
{
    /// <summary>
    /// DMA 인장시편 파라미터 클래스
    /// 모든 치수는 mm 단위
    /// 표준 규격: ASTM D4065, ISO 6721
    /// </summary>
    public class DMATensileParameters
    {
        /// <summary>
        /// 시편 타입 (표준/사용자정의)
        /// </summary>
        public DMASpecimenType SpecimenType { get; set; }

        /// <summary>
        /// 전체 길이 (Length) [mm]
        /// 표준: 50mm, 범위: 30-100mm
        /// </summary>
        public double Length { get; set; }

        /// <summary>
        /// 폭 (Width) [mm]
        /// 표준: 10mm, 범위: 5-15mm
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// 두께 (Thickness) [mm]
        /// 표준: 3mm, 범위: 1-5mm
        /// </summary>
        public double Thickness { get; set; }

        /// <summary>
        /// 게이지 길이 (Gauge Length) [mm]
        /// 표준: 20mm, 범위: 10-40mm
        /// </summary>
        public double GaugeLength { get; set; }

        /// <summary>
        /// 그립 길이 (Grip Length) [mm]
        /// 표준: 10mm, 범위: 5-20mm
        /// </summary>
        public double GripLength { get; set; }

        /// <summary>
        /// 시편 형상 (직사각형 또는 dog-bone)
        /// </summary>
        public DMASpecimenShape Shape { get; set; }

        /// <summary>
        /// 그립 장비 폭 [mm]
        /// 그립 지그의 폭 (시편을 고정하는 장비)
        /// </summary>
        public double GripWidth { get; set; }

        /// <summary>
        /// 그립 장비 높이 [mm]
        /// 그립 지그의 높이
        /// </summary>
        public double GripHeight { get; set; }

        /// <summary>
        /// 필렛 반경 (Fillet Radius) [mm]
        /// Dog-bone 형상에서 게이지와 그립 영역을 연결하는 원호의 반경
        /// </summary>
        public double FilletRadius { get; set; }

        /// <summary>
        /// 기본 생성자 - 표준 치수로 초기화
        /// </summary>
        public DMATensileParameters()
        {
            SpecimenType = DMASpecimenType.Standard;
            Length = 50.0;
            Width = 10.0;
            Thickness = 3.0;
            GaugeLength = 20.0;
            GripLength = 10.0;
            Shape = DMASpecimenShape.Rectangle;
            GripWidth = 15.0;
            GripHeight = 30.0;
            FilletRadius = 5.0;
        }

        /// <summary>
        /// 파라미터 복사 생성자
        /// </summary>
        public DMATensileParameters(DMATensileParameters other)
        {
            SpecimenType = other.SpecimenType;
            Length = other.Length;
            Width = other.Width;
            Thickness = other.Thickness;
            GaugeLength = other.GaugeLength;
            GripLength = other.GripLength;
            Shape = other.Shape;
            GripWidth = other.GripWidth;
            GripHeight = other.GripHeight;
            FilletRadius = other.FilletRadius;
        }

        /// <summary>
        /// 파라미터 유효성 검증
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            // 길이 검증
            if (Length <= 0)
            {
                errorMessage = "전체 길이는 0보다 커야 합니다.";
                return false;
            }

            if (Length < 30 || Length > 100)
            {
                errorMessage = "전체 길이는 30-100mm 범위여야 합니다.";
                return false;
            }

            // 폭 검증
            if (Width <= 0)
            {
                errorMessage = "폭은 0보다 커야 합니다.";
                return false;
            }

            if (Width < 5 || Width > 15)
            {
                errorMessage = "폭은 5-15mm 범위여야 합니다.";
                return false;
            }

            // 두께 검증
            if (Thickness <= 0)
            {
                errorMessage = "두께는 0보다 커야 합니다.";
                return false;
            }

            if (Thickness < 1 || Thickness > 5)
            {
                errorMessage = "두께는 1-5mm 범위여야 합니다.";
                return false;
            }

            // 게이지 길이 검증
            if (GaugeLength <= 0)
            {
                errorMessage = "게이지 길이는 0보다 커야 합니다.";
                return false;
            }

            if (GaugeLength < 10 || GaugeLength > 40)
            {
                errorMessage = "게이지 길이는 10-40mm 범위여야 합니다.";
                return false;
            }

            // 그립 길이 검증
            if (GripLength <= 0)
            {
                errorMessage = "그립 길이는 0보다 커야 합니다.";
                return false;
            }

            if (GripLength < 5 || GripLength > 20)
            {
                errorMessage = "그립 길이는 5-20mm 범위여야 합니다.";
                return false;
            }

            // 전체 길이와 구성 요소 길이 관계 검증
            double calculatedLength = Shape == DMASpecimenShape.Rectangle
                ? GaugeLength + (2 * GripLength)
                : GaugeLength + (2 * GripLength) + (4 * FilletRadius); // Dog-bone 형상에서는 필렛 영역 추가

            if (Length < calculatedLength)
            {
                errorMessage = $"전체 길이({Length}mm)가 계산된 길이({calculatedLength:F1}mm)보다 작습니다.";
                return false;
            }

            // Dog-bone 형상에서 필렛 반경 검증
            if (Shape == DMASpecimenShape.DogBone)
            {
                if (FilletRadius <= 0)
                {
                    errorMessage = "필렛 반경은 0보다 커야 합니다.";
                    return false;
                }

                if (FilletRadius > GaugeLength / 2)
                {
                    errorMessage = "필렛 반경이 너무 큽니다.";
                    return false;
                }
            }

            // 그립 장비 치수 검증
            if (GripWidth <= 0 || GripHeight <= 0)
            {
                errorMessage = "그립 장비 치수는 0보다 커야 합니다.";
                return false;
            }

            if (GripWidth <= Width)
            {
                errorMessage = "그립 장비 폭은 시편 폭보다 커야 합니다.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 문자열 표현
        /// </summary>
        public override string ToString()
        {
            return $"DMA Tensile - L:{Length}mm, W:{Width}mm, T:{Thickness}mm, GL:{GaugeLength}mm";
        }
    }
}
