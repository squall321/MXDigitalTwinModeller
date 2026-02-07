namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Joint
{
    /// <summary>
    /// 접합부 시편 파라미터
    /// 모든 치수는 mm 단위
    /// </summary>
    public class JointSpecimenParameters
    {
        /// <summary>접합부 타입</summary>
        public JointSpecimenType SpecimenType { get; set; }

        // ===== 공통 판재 치수 =====

        /// <summary>판재 폭 [mm] (Y 방향)</summary>
        public double AdherendWidth { get; set; }

        /// <summary>판재 길이 [mm] (X 방향, 겹침 포함)</summary>
        public double AdherendLength { get; set; }

        /// <summary>판재 두께 [mm] (Z 방향)</summary>
        public double AdherendThickness { get; set; }

        // ===== Lap Joint (Single/Double) =====

        /// <summary>겹침(오버랩) 길이 [mm]</summary>
        public double OverlapLength { get; set; }

        // ===== 접착층 =====

        /// <summary>접착층 두께 [mm]</summary>
        public double AdhesiveThickness { get; set; }

        /// <summary>접착층 별도 바디 생성 여부</summary>
        public bool CreateAdhesiveBody { get; set; }

        // ===== Scarf Joint =====

        /// <summary>스카프 각도 [도]</summary>
        public double ScarfAngle { get; set; }

        // ===== T-Joint =====

        /// <summary>플랜지(베이스) 길이 [mm]</summary>
        public double FlangeLength { get; set; }

        /// <summary>웹 높이 [mm]</summary>
        public double WebHeight { get; set; }

        /// <summary>웹 두께 [mm]</summary>
        public double WebThickness { get; set; }

        /// <summary>필렛 본드 크기 [mm]</summary>
        public double FilletBondSize { get; set; }

        // ===== 옵션 =====

        /// <summary>그립/지그 생성 여부</summary>
        public bool CreateGrips { get; set; }

        /// <summary>탭 길이 [mm] (Lap Joint 끝단)</summary>
        public double TabLength { get; set; }

        /// <summary>탭 두께 [mm]</summary>
        public double TabThickness { get; set; }

        public JointSpecimenParameters()
        {
            SpecimenType = JointSpecimenType.ASTM_D1002_SingleLap;
            AdherendWidth = 25.4;
            AdherendLength = 100.0;
            AdherendThickness = 1.6;
            OverlapLength = 25.4;
            AdhesiveThickness = 0.2;
            CreateAdhesiveBody = true;
            ScarfAngle = 5.0;
            FlangeLength = 100.0;
            WebHeight = 50.0;
            WebThickness = 2.0;
            FilletBondSize = 5.0;
            CreateGrips = false;
            TabLength = 25.0;
            TabThickness = 1.6;
        }

        /// <summary>프리셋 적용</summary>
        public static JointSpecimenParameters FromPreset(JointSpecimenType type)
        {
            var p = new JointSpecimenParameters();
            p.SpecimenType = type;

            switch (type)
            {
                case JointSpecimenType.ASTM_D1002_SingleLap:
                    p.AdherendWidth = 25.4;
                    p.AdherendLength = 100.0;
                    p.AdherendThickness = 1.6;
                    p.OverlapLength = 25.4;
                    p.AdhesiveThickness = 0.2;
                    break;

                case JointSpecimenType.ASTM_D3528_DoubleLap:
                    p.AdherendWidth = 25.4;
                    p.AdherendLength = 100.0;
                    p.AdherendThickness = 1.6;
                    p.OverlapLength = 25.4;
                    p.AdhesiveThickness = 0.2;
                    break;

                case JointSpecimenType.Scarf_Joint:
                    p.AdherendWidth = 25.0;
                    p.AdherendLength = 100.0;
                    p.AdherendThickness = 3.0;
                    p.ScarfAngle = 5.0;
                    p.AdhesiveThickness = 0.2;
                    break;

                case JointSpecimenType.Butt_Joint:
                    p.AdherendWidth = 25.0;
                    p.AdherendLength = 50.0;
                    p.AdherendThickness = 10.0;
                    p.AdhesiveThickness = 0.2;
                    break;

                case JointSpecimenType.T_Joint:
                    p.AdherendWidth = 25.0;
                    p.AdherendThickness = 2.0;
                    p.FlangeLength = 100.0;
                    p.WebHeight = 50.0;
                    p.WebThickness = 2.0;
                    p.FilletBondSize = 5.0;
                    p.AdhesiveThickness = 0.2;
                    break;
            }

            return p;
        }

        /// <summary>파라미터 유효성 검증</summary>
        public bool Validate(out string errorMessage)
        {
            if (AdherendWidth <= 0) { errorMessage = "판재 폭은 0보다 커야 합니다."; return false; }
            if (AdherendThickness <= 0) { errorMessage = "판재 두께는 0보다 커야 합니다."; return false; }

            switch (SpecimenType)
            {
                case JointSpecimenType.ASTM_D1002_SingleLap:
                case JointSpecimenType.ASTM_D3528_DoubleLap:
                    if (AdherendLength <= 0) { errorMessage = "판재 길이는 0보다 커야 합니다."; return false; }
                    if (OverlapLength <= 0) { errorMessage = "겹침 길이는 0보다 커야 합니다."; return false; }
                    break;

                case JointSpecimenType.Scarf_Joint:
                    if (AdherendLength <= 0) { errorMessage = "판재 길이는 0보다 커야 합니다."; return false; }
                    if (ScarfAngle <= 0 || ScarfAngle >= 90)
                    { errorMessage = "스카프 각도는 0~90도 사이여야 합니다."; return false; }
                    break;

                case JointSpecimenType.Butt_Joint:
                    if (AdherendLength <= 0) { errorMessage = "판재 길이는 0보다 커야 합니다."; return false; }
                    break;

                case JointSpecimenType.T_Joint:
                    if (FlangeLength <= 0) { errorMessage = "플랜지 길이는 0보다 커야 합니다."; return false; }
                    if (WebHeight <= 0) { errorMessage = "웹 높이는 0보다 커야 합니다."; return false; }
                    if (WebThickness <= 0) { errorMessage = "웹 두께는 0보다 커야 합니다."; return false; }
                    break;
            }

            if (AdhesiveThickness < 0)
            { errorMessage = "접착층 두께는 0 이상이어야 합니다."; return false; }

            errorMessage = null;
            return true;
        }
    }
}
