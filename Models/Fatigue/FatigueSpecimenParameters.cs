namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Fatigue
{
    /// <summary>
    /// 피로 시편 파라미터
    /// 모든 치수는 mm 단위
    /// </summary>
    public class FatigueSpecimenParameters
    {
        /// <summary>시편 규격 타입</summary>
        public FatigueSpecimenType SpecimenType { get; set; }

        /// <summary>단면 형상</summary>
        public FatigueSectionShape SectionShape { get; set; }

        // ===== 공통 치수 (E466/E606) =====

        /// <summary>게이지 길이 [mm]</summary>
        public double GaugeLength { get; set; }

        /// <summary>게이지 폭 [mm] (직사각형 단면)</summary>
        public double GaugeWidth { get; set; }

        /// <summary>두께 [mm] (직사각형 단면)</summary>
        public double Thickness { get; set; }

        /// <summary>게이지 직경 [mm] (원형 단면, E606)</summary>
        public double GaugeDiameter { get; set; }

        /// <summary>그립 폭 [mm]</summary>
        public double GripWidth { get; set; }

        /// <summary>그립 길이 [mm]</summary>
        public double GripLength { get; set; }

        /// <summary>전체 길이 [mm]</summary>
        public double TotalLength { get; set; }

        /// <summary>필렛 반경 [mm] (게이지-그립 전환부)</summary>
        public double FilletRadius { get; set; }

        /// <summary>모래시계 반경 [mm] (E466 Hourglass)</summary>
        public double HourglassRadius { get; set; }

        // ===== CT 시편 (E647) =====

        /// <summary>CT 폭 W [mm]</summary>
        public double CTWidth { get; set; }

        /// <summary>CT 두께 B [mm]</summary>
        public double CTThickness { get; set; }

        /// <summary>초기 균열 길이 a₀ [mm]</summary>
        public double InitialCrackLength { get; set; }

        /// <summary>핀홀 직경 [mm]</summary>
        public double PinHoleDiameter { get; set; }

        /// <summary>노치(슬릿) 폭 [mm]</summary>
        public double NotchWidth { get; set; }

        // ===== MT 시편 (E647) =====

        /// <summary>MT 폭 W [mm]</summary>
        public double MTWidth { get; set; }

        /// <summary>MT 길이 [mm]</summary>
        public double MTLength { get; set; }

        /// <summary>MT 두께 [mm]</summary>
        public double MTThickness { get; set; }

        /// <summary>중앙 슬롯 반 길이 a₀ [mm] (총 슬롯 길이 = 2a₀)</summary>
        public double SlotHalfLength { get; set; }

        /// <summary>슬롯 폭 [mm]</summary>
        public double SlotWidth { get; set; }

        // ===== 원통형 시편 (E2207) =====

        /// <summary>외경 OD [mm]</summary>
        public double TubeOuterDiameter { get; set; }

        /// <summary>내경 ID [mm]</summary>
        public double TubeInnerDiameter { get; set; }

        /// <summary>튜브 게이지 길이 [mm]</summary>
        public double TubeGaugeLength { get; set; }

        /// <summary>튜브 전체 길이 [mm]</summary>
        public double TubeTotalLength { get; set; }

        /// <summary>튜브 그립부 외경 [mm]</summary>
        public double TubeGripOuterDiameter { get; set; }

        // ===== 옵션 =====

        /// <summary>그립/지그 생성 여부</summary>
        public bool CreateGrips { get; set; }

        public FatigueSpecimenParameters()
        {
            SpecimenType = FatigueSpecimenType.ASTM_E466_Uniform;
            SectionShape = FatigueSectionShape.Rectangular;

            GaugeLength = 75.0;
            GaugeWidth = 12.5;
            Thickness = 6.0;
            GaugeDiameter = 6.35;
            GripWidth = 20.0;
            GripLength = 50.0;
            TotalLength = 200.0;
            FilletRadius = 50.0;
            HourglassRadius = 100.0;

            CTWidth = 50.0;
            CTThickness = 12.5;
            InitialCrackLength = 25.0;
            PinHoleDiameter = 12.5;
            NotchWidth = 1.0;

            MTWidth = 75.0;
            MTLength = 300.0;
            MTThickness = 6.0;
            SlotHalfLength = 10.0;
            SlotWidth = 0.5;

            TubeOuterDiameter = 22.0;
            TubeInnerDiameter = 20.0;
            TubeGaugeLength = 20.0;
            TubeTotalLength = 120.0;
            TubeGripOuterDiameter = 28.0;

            CreateGrips = true;
        }

        /// <summary>프리셋 적용</summary>
        public static FatigueSpecimenParameters FromPreset(FatigueSpecimenType type)
        {
            var p = new FatigueSpecimenParameters();
            p.SpecimenType = type;

            switch (type)
            {
                case FatigueSpecimenType.ASTM_E466_Uniform:
                    p.SectionShape = FatigueSectionShape.Rectangular;
                    p.GaugeLength = 75.0;
                    p.GaugeWidth = 12.5;
                    p.Thickness = 6.0;
                    p.GripWidth = 20.0;
                    p.GripLength = 50.0;
                    p.TotalLength = 200.0;
                    p.FilletRadius = 50.0;
                    break;

                case FatigueSpecimenType.ASTM_E466_Hourglass:
                    p.SectionShape = FatigueSectionShape.Rectangular;
                    p.GaugeWidth = 12.5;
                    p.Thickness = 6.0;
                    p.GripWidth = 20.0;
                    p.GripLength = 50.0;
                    p.TotalLength = 200.0;
                    p.HourglassRadius = 100.0;
                    p.GaugeLength = 0; // Hourglass는 연속 곡률
                    break;

                case FatigueSpecimenType.ASTM_E606:
                    p.SectionShape = FatigueSectionShape.Circular;
                    p.GaugeDiameter = 6.35;
                    p.GaugeLength = 15.0;
                    p.GripWidth = 12.7;
                    p.GripLength = 30.0;
                    p.TotalLength = 100.0;
                    p.FilletRadius = 25.0;
                    break;

                case FatigueSpecimenType.ASTM_E647_CT:
                    p.SectionShape = FatigueSectionShape.Rectangular;
                    p.CTWidth = 50.0;
                    p.CTThickness = 12.5;
                    p.InitialCrackLength = 25.0;
                    p.PinHoleDiameter = 12.5;
                    p.NotchWidth = 1.0;
                    break;

                case FatigueSpecimenType.ASTM_E647_MT:
                    p.SectionShape = FatigueSectionShape.Rectangular;
                    p.MTWidth = 75.0;
                    p.MTLength = 300.0;
                    p.MTThickness = 6.0;
                    p.SlotHalfLength = 10.0;
                    p.SlotWidth = 0.5;
                    break;

                case FatigueSpecimenType.ASTM_E2207:
                    p.SectionShape = FatigueSectionShape.Tubular;
                    p.TubeOuterDiameter = 22.0;
                    p.TubeInnerDiameter = 20.0;
                    p.TubeGaugeLength = 20.0;
                    p.TubeTotalLength = 120.0;
                    p.TubeGripOuterDiameter = 28.0;
                    break;
            }

            return p;
        }

        /// <summary>파라미터 유효성 검증</summary>
        public bool Validate(out string errorMessage)
        {
            switch (SpecimenType)
            {
                case FatigueSpecimenType.ASTM_E466_Uniform:
                    if (GaugeLength <= 0) { errorMessage = "게이지 길이는 0보다 커야 합니다."; return false; }
                    if (GaugeWidth <= 0) { errorMessage = "게이지 폭은 0보다 커야 합니다."; return false; }
                    if (Thickness <= 0) { errorMessage = "두께는 0보다 커야 합니다."; return false; }
                    if (TotalLength <= 0) { errorMessage = "전체 길이는 0보다 커야 합니다."; return false; }
                    break;

                case FatigueSpecimenType.ASTM_E466_Hourglass:
                    if (GaugeWidth <= 0) { errorMessage = "최소 폭은 0보다 커야 합니다."; return false; }
                    if (Thickness <= 0) { errorMessage = "두께는 0보다 커야 합니다."; return false; }
                    if (HourglassRadius <= 0) { errorMessage = "모래시계 반경은 0보다 커야 합니다."; return false; }
                    break;

                case FatigueSpecimenType.ASTM_E606:
                    if (GaugeDiameter <= 0) { errorMessage = "게이지 직경은 0보다 커야 합니다."; return false; }
                    if (GaugeLength <= 0) { errorMessage = "게이지 길이는 0보다 커야 합니다."; return false; }
                    break;

                case FatigueSpecimenType.ASTM_E647_CT:
                    if (CTWidth <= 0) { errorMessage = "CT 폭(W)은 0보다 커야 합니다."; return false; }
                    if (CTThickness <= 0) { errorMessage = "CT 두께(B)는 0보다 커야 합니다."; return false; }
                    if (InitialCrackLength <= 0) { errorMessage = "초기 균열 길이는 0보다 커야 합니다."; return false; }
                    if (PinHoleDiameter <= 0) { errorMessage = "핀홀 직경은 0보다 커야 합니다."; return false; }
                    break;

                case FatigueSpecimenType.ASTM_E647_MT:
                    if (MTWidth <= 0) { errorMessage = "MT 폭은 0보다 커야 합니다."; return false; }
                    if (MTLength <= 0) { errorMessage = "MT 길이는 0보다 커야 합니다."; return false; }
                    if (MTThickness <= 0) { errorMessage = "MT 두께는 0보다 커야 합니다."; return false; }
                    if (SlotHalfLength <= 0) { errorMessage = "슬롯 반 길이는 0보다 커야 합니다."; return false; }
                    break;

                case FatigueSpecimenType.ASTM_E2207:
                    if (TubeOuterDiameter <= 0) { errorMessage = "외경은 0보다 커야 합니다."; return false; }
                    if (TubeInnerDiameter <= 0) { errorMessage = "내경은 0보다 커야 합니다."; return false; }
                    if (TubeInnerDiameter >= TubeOuterDiameter)
                    { errorMessage = "내경은 외경보다 작아야 합니다."; return false; }
                    if (TubeGaugeLength <= 0) { errorMessage = "게이지 길이는 0보다 커야 합니다."; return false; }
                    break;
            }

            errorMessage = null;
            return true;
        }
    }
}
