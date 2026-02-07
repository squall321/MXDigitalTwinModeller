namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Compression
{
    /// <summary>
    /// 압축 시험 시편 파라미터
    /// 모든 치수는 mm 단위
    /// </summary>
    public class CompressionSpecimenParameters
    {
        /// <summary>시편 규격 타입</summary>
        public CompressionSpecimenType SpecimenType { get; set; }

        /// <summary>시편 형상 (직육면체/원기둥)</summary>
        public CompressionSpecimenShape Shape { get; set; }

        // ===== 직육면체 (Prism) 치수 =====

        /// <summary>폭 (Width) [mm] - X 방향</summary>
        public double WidthMm { get; set; }

        /// <summary>깊이 (Depth) [mm] - Y 방향</summary>
        public double DepthMm { get; set; }

        /// <summary>높이 (Height/Length) [mm] - Z 방향 (하중 방향)</summary>
        public double HeightMm { get; set; }

        // ===== 원기둥 (Cylinder) 치수 =====

        /// <summary>직경 (Diameter) [mm]</summary>
        public double DiameterMm { get; set; }

        // ===== 압축판 (Platen) 치수 =====

        /// <summary>플래튼 생성 여부</summary>
        public bool CreatePlatens { get; set; }

        /// <summary>플래튼 직경 [mm] (원형 플래튼)</summary>
        public double PlatenDiameterMm { get; set; }

        /// <summary>플래튼 높이/두께 [mm]</summary>
        public double PlatenHeightMm { get; set; }

        public CompressionSpecimenParameters()
        {
            SpecimenType = CompressionSpecimenType.ASTM_D695_Prism;
            Shape = CompressionSpecimenShape.Prism;
            WidthMm = 12.7;
            DepthMm = 12.7;
            HeightMm = 25.4;
            DiameterMm = 12.7;
            CreatePlatens = true;
            PlatenDiameterMm = 50.0;
            PlatenHeightMm = 20.0;
        }

        /// <summary>
        /// 프리셋 적용
        /// </summary>
        public static CompressionSpecimenParameters FromPreset(CompressionSpecimenType type)
        {
            var p = new CompressionSpecimenParameters();
            p.SpecimenType = type;

            switch (type)
            {
                case CompressionSpecimenType.ASTM_D695_Prism:
                    p.Shape = CompressionSpecimenShape.Prism;
                    p.WidthMm = 12.7;
                    p.DepthMm = 12.7;
                    p.HeightMm = 25.4;
                    break;

                case CompressionSpecimenType.ASTM_D695_Cylinder:
                    p.Shape = CompressionSpecimenShape.Cylinder;
                    p.DiameterMm = 12.7;
                    p.HeightMm = 25.4;
                    break;

                case CompressionSpecimenType.ISO_604_Modulus:
                    p.Shape = CompressionSpecimenShape.Prism;
                    p.WidthMm = 10.0;
                    p.DepthMm = 4.0;
                    p.HeightMm = 50.0;
                    break;

                case CompressionSpecimenType.ISO_604_Strength:
                    p.Shape = CompressionSpecimenShape.Prism;
                    p.WidthMm = 10.0;
                    p.DepthMm = 4.0;
                    p.HeightMm = 10.0;
                    break;

                case CompressionSpecimenType.ASTM_E9_Short:
                    p.Shape = CompressionSpecimenShape.Cylinder;
                    p.DiameterMm = 12.7;
                    p.HeightMm = 25.4; // L/D = 2
                    break;

                case CompressionSpecimenType.ASTM_E9_Medium:
                    p.Shape = CompressionSpecimenShape.Cylinder;
                    p.DiameterMm = 12.7;
                    p.HeightMm = 38.1; // L/D = 3
                    break;

                case CompressionSpecimenType.Custom:
                    // 기본값 유지
                    break;
            }

            return p;
        }

        /// <summary>
        /// 파라미터 유효성 검증
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            if (Shape == CompressionSpecimenShape.Prism)
            {
                if (WidthMm <= 0) { errorMessage = "폭(Width)은 0보다 커야 합니다."; return false; }
                if (DepthMm <= 0) { errorMessage = "깊이(Depth)는 0보다 커야 합니다."; return false; }
            }
            else
            {
                if (DiameterMm <= 0) { errorMessage = "직경(Diameter)은 0보다 커야 합니다."; return false; }
            }

            if (HeightMm <= 0) { errorMessage = "높이(Height)는 0보다 커야 합니다."; return false; }

            if (CreatePlatens)
            {
                if (PlatenDiameterMm <= 0) { errorMessage = "플래튼 직경은 0보다 커야 합니다."; return false; }
                if (PlatenHeightMm <= 0) { errorMessage = "플래튼 높이는 0보다 커야 합니다."; return false; }
            }

            errorMessage = null;
            return true;
        }
    }
}
