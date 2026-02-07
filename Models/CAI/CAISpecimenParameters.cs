namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.CAI
{
    /// <summary>
    /// CAI 시편 파라미터
    /// 모든 치수는 mm 단위
    /// </summary>
    public class CAISpecimenParameters
    {
        /// <summary>시편 규격 타입</summary>
        public CAISpecimenType SpecimenType { get; set; }

        // ===== 패널 치수 =====

        /// <summary>패널 길이 (Length) [mm] - X 방향 (압축 하중 방향)</summary>
        public double PanelLength { get; set; }

        /// <summary>패널 폭 (Width) [mm] - Y 방향</summary>
        public double PanelWidth { get; set; }

        /// <summary>패널 두께 (Thickness) [mm] - Z 방향</summary>
        public double Thickness { get; set; }

        // ===== Anti-Buckling Guide (지그) =====

        /// <summary>지그 생성 여부</summary>
        public bool CreateJig { get; set; }

        /// <summary>지그 두께 [mm]</summary>
        public double JigThickness { get; set; }

        /// <summary>지그 윈도우 길이 [mm] (X 방향 개구부)</summary>
        public double WindowLength { get; set; }

        /// <summary>지그 윈도우 폭 [mm] (Y 방향 개구부)</summary>
        public double WindowWidth { get; set; }

        /// <summary>지그 간격 (패널과의 클리어런스) [mm]</summary>
        public double JigClearance { get; set; }

        // ===== 손상 영역 (Damage Zone) =====

        /// <summary>손상 영역 생성 여부</summary>
        public bool CreateDamageZone { get; set; }

        /// <summary>타원형 손상 여부 (false = 원형)</summary>
        public bool IsEllipticalDamage { get; set; }

        /// <summary>손상 직경 [mm] (원형일 때)</summary>
        public double DamageDiameter { get; set; }

        /// <summary>손상 장축 [mm] (타원형일 때)</summary>
        public double DamageMajorAxis { get; set; }

        /// <summary>손상 단축 [mm] (타원형일 때)</summary>
        public double DamageMinorAxis { get; set; }

        /// <summary>손상 깊이 비율 [0~100%] (100% = 관통)</summary>
        public double DamageDepthPercent { get; set; }

        public CAISpecimenParameters()
        {
            SpecimenType = CAISpecimenType.ASTM_D7137;
            PanelLength = 150.0;
            PanelWidth = 100.0;
            Thickness = 4.0;
            CreateJig = true;
            JigThickness = 10.0;
            WindowLength = 75.0;
            WindowWidth = 50.0;
            JigClearance = 0.5;
            CreateDamageZone = true;
            IsEllipticalDamage = false;
            DamageDiameter = 25.0;
            DamageMajorAxis = 30.0;
            DamageMinorAxis = 20.0;
            DamageDepthPercent = 50.0;
        }

        /// <summary>
        /// 프리셋 적용
        /// </summary>
        public static CAISpecimenParameters FromPreset(CAISpecimenType type)
        {
            var p = new CAISpecimenParameters();
            p.SpecimenType = type;

            switch (type)
            {
                case CAISpecimenType.ASTM_D7137:
                    p.PanelLength = 150.0;
                    p.PanelWidth = 100.0;
                    p.Thickness = 4.0;
                    p.WindowLength = 75.0;
                    p.WindowWidth = 50.0;
                    p.DamageDiameter = 25.0;
                    break;

                case CAISpecimenType.ASTM_D6264:
                    p.PanelLength = 150.0;
                    p.PanelWidth = 100.0;
                    p.Thickness = 5.0;
                    p.WindowLength = 75.0;
                    p.WindowWidth = 50.0;
                    p.DamageDiameter = 30.0;
                    break;

                case CAISpecimenType.Boeing_BSS7260:
                    p.PanelLength = 152.4;  // 6 inches
                    p.PanelWidth = 101.6;   // 4 inches
                    p.Thickness = 4.0;
                    p.WindowLength = 76.2;
                    p.WindowWidth = 50.8;
                    p.DamageDiameter = 25.4;
                    break;

                case CAISpecimenType.Custom:
                    break;
            }

            return p;
        }

        /// <summary>
        /// 파라미터 유효성 검증
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            if (PanelLength <= 0) { errorMessage = "패널 길이는 0보다 커야 합니다."; return false; }
            if (PanelWidth <= 0) { errorMessage = "패널 폭은 0보다 커야 합니다."; return false; }
            if (Thickness <= 0) { errorMessage = "패널 두께는 0보다 커야 합니다."; return false; }

            if (CreateJig)
            {
                if (JigThickness <= 0) { errorMessage = "지그 두께는 0보다 커야 합니다."; return false; }
                if (WindowLength <= 0 || WindowLength >= PanelLength)
                {
                    errorMessage = "윈도우 길이는 0보다 크고 패널 길이보다 작아야 합니다.";
                    return false;
                }
                if (WindowWidth <= 0 || WindowWidth >= PanelWidth)
                {
                    errorMessage = "윈도우 폭은 0보다 크고 패널 폭보다 작아야 합니다.";
                    return false;
                }
            }

            if (CreateDamageZone)
            {
                if (IsEllipticalDamage)
                {
                    if (DamageMajorAxis <= 0) { errorMessage = "손상 장축은 0보다 커야 합니다."; return false; }
                    if (DamageMinorAxis <= 0) { errorMessage = "손상 단축은 0보다 커야 합니다."; return false; }
                }
                else
                {
                    if (DamageDiameter <= 0) { errorMessage = "손상 직경은 0보다 커야 합니다."; return false; }
                }
                if (DamageDepthPercent <= 0 || DamageDepthPercent > 100)
                {
                    errorMessage = "손상 깊이 비율은 0~100% 사이여야 합니다.";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }
    }
}
