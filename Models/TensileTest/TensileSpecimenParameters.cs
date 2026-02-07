namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.TensileTest
{
    /// <summary>
    /// 인장시험 시편 파라미터 클래스
    /// 모든 치수는 mm 단위
    /// </summary>
    public class TensileSpecimenParameters
    {
        /// <summary>
        /// 시편 규격 타입
        /// </summary>
        public ASTMSpecimenType SpecimenType { get; set; }

        /// <summary>
        /// 게이지 길이 (Gauge Length) [mm]
        /// </summary>
        public double GaugeLength { get; set; }

        /// <summary>
        /// 게이지 폭 (Gauge Width) [mm]
        /// </summary>
        public double GaugeWidth { get; set; }

        /// <summary>
        /// 두께 (Thickness) [mm]
        /// </summary>
        public double Thickness { get; set; }

        /// <summary>
        /// 그립 폭 (Grip Width) [mm]
        /// </summary>
        public double GripWidth { get; set; }

        /// <summary>
        /// 전체 길이 (Total Length) [mm]
        /// </summary>
        public double TotalLength { get; set; }

        /// <summary>
        /// 필렛 반경 (Fillet Radius) [mm]
        /// 게이지 영역과 그립 영역을 연결하는 원호의 반경
        /// </summary>
        public double FilletRadius { get; set; }

        /// <summary>
        /// 그립 길이 (Grip Length) [mm]
        /// </summary>
        public double GripLength { get; set; }

        /// <summary>
        /// 노치 깊이 (Notch Depth) [mm]
        /// V-Notch, U-Notch 시편에만 사용
        /// </summary>
        public double NotchDepth { get; set; }

        /// <summary>
        /// 노치 반경 (Notch Radius) [mm]
        /// U-Notch의 곡률 반경
        /// </summary>
        public double NotchRadius { get; set; }

        /// <summary>
        /// 노치 각도 (Notch Angle) [degrees]
        /// V-Notch의 개구 각도 (일반적으로 60°)
        /// </summary>
        public double NotchAngle { get; set; }

        /// <summary>
        /// 양면 노치 여부 (Double Notch)
        /// true: 양면 노치 (상하), false: 단면 노치 (한쪽만)
        /// </summary>
        public bool IsDoubleNotch { get; set; }

        // ===== 구멍 파라미터 (OHT/OHC/FHT/Bearing) =====

        /// <summary>
        /// 구멍 직경 (Hole Diameter) [mm]
        /// 원형 구멍일 때 사용
        /// </summary>
        public double HoleDiameter { get; set; }

        /// <summary>
        /// 타원형 구멍 여부
        /// true: 타원형 (MajorAxis/MinorAxis 사용), false: 원형 (HoleDiameter 사용)
        /// </summary>
        public bool IsEllipticalHole { get; set; }

        /// <summary>
        /// 타원 장축 (Major Axis) [mm]
        /// 인장 방향(X축)에 수직인 방향
        /// </summary>
        public double HoleMajorAxis { get; set; }

        /// <summary>
        /// 타원 단축 (Minor Axis) [mm]
        /// 인장 방향(X축) 방향
        /// </summary>
        public double HoleMinorAxis { get; set; }

        // ===== 직사각형 시편 / 탭 파라미터 (D3039, IPC 등) =====

        /// <summary>
        /// 직사각형 시편 여부 (dog-bone이 아닌 직선 형태)
        /// D3039, IPC 등 복합재/PCB 시편에 사용
        /// </summary>
        public bool IsRectangular { get; set; }

        /// <summary>
        /// 탭 길이 (Tab Length) [mm]
        /// D3039 등 복합재 시편의 접착 탭
        /// </summary>
        public double TabLength { get; set; }

        /// <summary>
        /// 탭 두께 (Tab Thickness) [mm]
        /// </summary>
        public double TabThickness { get; set; }

        /// <summary>
        /// 기본 생성자
        /// </summary>
        public TensileSpecimenParameters()
        {
            SpecimenType = ASTMSpecimenType.ASTM_E8_Standard;
            GaugeLength = 50.0;
            GaugeWidth = 12.5;
            Thickness = 3.0;
            GripWidth = 20.0;
            TotalLength = 200.0;
            FilletRadius = 12.5;
            GripLength = 50.0;
            NotchDepth = 0.0;
            NotchRadius = 0.0;
            NotchAngle = 0.0;
            IsDoubleNotch = false;
            HoleDiameter = 0.0;
            IsEllipticalHole = false;
            HoleMajorAxis = 0.0;
            HoleMinorAxis = 0.0;
            IsRectangular = false;
            TabLength = 0.0;
            TabThickness = 0.0;
        }

        /// <summary>
        /// 파라미터 복사 생성자
        /// </summary>
        public TensileSpecimenParameters(TensileSpecimenParameters other)
        {
            SpecimenType = other.SpecimenType;
            GaugeLength = other.GaugeLength;
            GaugeWidth = other.GaugeWidth;
            Thickness = other.Thickness;
            GripWidth = other.GripWidth;
            TotalLength = other.TotalLength;
            FilletRadius = other.FilletRadius;
            GripLength = other.GripLength;
            NotchDepth = other.NotchDepth;
            NotchRadius = other.NotchRadius;
            NotchAngle = other.NotchAngle;
            IsDoubleNotch = other.IsDoubleNotch;
            HoleDiameter = other.HoleDiameter;
            IsEllipticalHole = other.IsEllipticalHole;
            HoleMajorAxis = other.HoleMajorAxis;
            HoleMinorAxis = other.HoleMinorAxis;
            IsRectangular = other.IsRectangular;
            TabLength = other.TabLength;
            TabThickness = other.TabThickness;
        }

        /// <summary>
        /// 파라미터 유효성 검증
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            if (GaugeLength <= 0)
            {
                errorMessage = "게이지 길이는 0보다 커야 합니다.";
                return false;
            }

            if (GaugeWidth <= 0)
            {
                errorMessage = "게이지 폭은 0보다 커야 합니다.";
                return false;
            }

            if (Thickness <= 0)
            {
                errorMessage = "두께는 0보다 커야 합니다.";
                return false;
            }

            if (!IsRectangular && GripWidth <= GaugeWidth)
            {
                errorMessage = "그립 폭은 게이지 폭보다 커야 합니다.";
                return false;
            }

            if (TotalLength <= GaugeLength)
            {
                errorMessage = "전체 길이는 게이지 길이보다 커야 합니다.";
                return false;
            }

            if (!IsRectangular && FilletRadius <= 0)
            {
                errorMessage = "필렛 반경은 0보다 커야 합니다.";
                return false;
            }

            // 노치 시편 추가 검증 (인장 노치 + 전단 시편)
            if (SpecimenType == ASTMSpecimenType.ASTM_E602_VNotch ||
                SpecimenType == ASTMSpecimenType.ASTM_E602_UNotch ||
                SpecimenType == ASTMSpecimenType.ASTM_E338 ||
                SpecimenType == ASTMSpecimenType.ASTM_E292 ||
                SpecimenType == ASTMSpecimenType.ASTM_D5379_Iosipescu ||
                SpecimenType == ASTMSpecimenType.ASTM_D7078_VNotchRailShear)
            {
                if (NotchDepth <= 0)
                {
                    errorMessage = "노치 깊이는 0보다 커야 합니다.";
                    return false;
                }

                if (NotchDepth >= GaugeWidth / 2.0)
                {
                    errorMessage = "노치 깊이는 게이지 폭의 절반보다 작아야 합니다.";
                    return false;
                }

                if ((SpecimenType == ASTMSpecimenType.ASTM_E602_VNotch ||
                     SpecimenType == ASTMSpecimenType.ASTM_E338 ||
                     SpecimenType == ASTMSpecimenType.ASTM_E292 ||
                     SpecimenType == ASTMSpecimenType.ASTM_D5379_Iosipescu ||
                     SpecimenType == ASTMSpecimenType.ASTM_D7078_VNotchRailShear) && NotchAngle <= 0)
                {
                    errorMessage = "V-Notch 각도는 0보다 커야 합니다.";
                    return false;
                }

                if (SpecimenType == ASTMSpecimenType.ASTM_E602_UNotch && NotchRadius <= 0)
                {
                    errorMessage = "U-Notch 반경은 0보다 커야 합니다.";
                    return false;
                }
            }

            // 구멍 시편 추가 검증
            if (SpecimenType == ASTMSpecimenType.ASTM_D5766_OHT ||
                SpecimenType == ASTMSpecimenType.ASTM_D6484_OHC ||
                SpecimenType == ASTMSpecimenType.ASTM_D6742_FHT ||
                SpecimenType == ASTMSpecimenType.ASTM_D5961_Bearing)
            {
                if (IsEllipticalHole)
                {
                    if (HoleMajorAxis <= 0 || HoleMinorAxis <= 0)
                    {
                        errorMessage = "타원 구멍의 장축과 단축은 0보다 커야 합니다.";
                        return false;
                    }
                }
                else
                {
                    if (HoleDiameter <= 0)
                    {
                        errorMessage = "구멍 직경은 0보다 커야 합니다.";
                        return false;
                    }
                }
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 문자열 표현
        /// </summary>
        public override string ToString()
        {
            if (SpecimenType == ASTMSpecimenType.ASTM_E602_VNotch)
                return $"{SpecimenType} - GL:{GaugeLength}mm, GW:{GaugeWidth}mm, T:{Thickness}mm, ND:{NotchDepth}mm, NA:{NotchAngle}°";
            if (SpecimenType == ASTMSpecimenType.ASTM_E602_UNotch)
                return $"{SpecimenType} - GL:{GaugeLength}mm, GW:{GaugeWidth}mm, T:{Thickness}mm, ND:{NotchDepth}mm, NR:{NotchRadius}mm";
            if (SpecimenType == ASTMSpecimenType.ASTM_D5379_Iosipescu ||
                SpecimenType == ASTMSpecimenType.ASTM_D7078_VNotchRailShear)
                return $"{SpecimenType} - {TotalLength}×{GaugeWidth}mm, T:{Thickness}mm, ND:{NotchDepth}mm, NA:{NotchAngle}°";
            return $"{SpecimenType} - GL:{GaugeLength}mm, GW:{GaugeWidth}mm, T:{Thickness}mm";
        }
    }
}
