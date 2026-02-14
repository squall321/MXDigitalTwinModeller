namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ConformalMesh
{
    /// <summary>
    /// STEP 임포트 모드
    /// </summary>
    public enum StepImportMode
    {
        /// <summary>현재 파트 사용 (이미 열린 문서)</summary>
        UseCurrentPart,

        /// <summary>STEP 파일을 새 문서로 열기</summary>
        OpenNew,

        /// <summary>현재 파트에 컴포넌트로 삽입</summary>
        InsertIntoCurrent
    }

    /// <summary>
    /// 메쉬 전략
    /// </summary>
    public enum MeshStrategy
    {
        /// <summary>자동 사면체 (가장 안정적)</summary>
        AutoTet,

        /// <summary>자동 육면체 (Blocking Decomposition)</summary>
        AutoHex,

        /// <summary>혼합 (바디별 자동 분류)</summary>
        Mixed
    }

    /// <summary>
    /// Conformal Mesh 파라미터
    /// </summary>
    public class ConformalMeshParameters
    {
        // ===== STEP 파일 =====

        /// <summary>STEP 파일 경로</summary>
        public string StepFilePath { get; set; }

        /// <summary>임포트 모드</summary>
        public StepImportMode ImportMode { get; set; }

        // ===== 계면 검출 =====

        /// <summary>면 간격 허용치 [mm]</summary>
        public double ToleranceMm { get; set; }

        /// <summary>평면 접촉 검출</summary>
        public bool DetectPlanar { get; set; }

        /// <summary>원통 접촉 검출</summary>
        public bool DetectCylindrical { get; set; }

        /// <summary>바디 필터 키워드 (빈값=전체)</summary>
        public string BodyKeyword { get; set; }

        // ===== Share Topology =====

        /// <summary>Share Topology 활성화</summary>
        public bool EnableShareTopology { get; set; }

        /// <summary>인터페이스 Named Selection 생성</summary>
        public bool CreateInterfaceNamedSelections { get; set; }

        // ===== 메쉬 =====

        /// <summary>요소 크기 [mm]</summary>
        public double ElementSizeMm { get; set; }

        /// <summary>메쉬 전략</summary>
        public MeshStrategy Strategy { get; set; }

        /// <summary>성장률</summary>
        public double GrowthRate { get; set; }

        /// <summary>중간절점 유지</summary>
        public bool MidsideNodes { get; set; }

        /// <summary>곡률+근접 크기 함수 사용</summary>
        public bool UseCurvatureProximity { get; set; }

        // ===== 실린더 처리 =====

        /// <summary>원형 엣지 분할</summary>
        public bool SplitCylinderEdges { get; set; }

        /// <summary>원 분할 수 (기본 8)</summary>
        public int CylinderEdgeDivisions { get; set; }

        // ===== 내보내기 =====

        /// <summary>자동 내보내기</summary>
        public bool AutoExport { get; set; }

        /// <summary>내보내기 경로</summary>
        public string ExportPath { get; set; }

        /// <summary>내보내기 형식 ("LS-DYNA", "ANSYS")</summary>
        public string ExportFormat { get; set; }

        public ConformalMeshParameters()
        {
            StepFilePath = "";
            ImportMode = StepImportMode.UseCurrentPart;
            ToleranceMm = 1.0;
            DetectPlanar = true;
            DetectCylindrical = true;
            BodyKeyword = "";
            EnableShareTopology = true;
            CreateInterfaceNamedSelections = true;
            ElementSizeMm = 2.0;
            Strategy = MeshStrategy.AutoTet;
            GrowthRate = 1.8;
            MidsideNodes = false;
            UseCurvatureProximity = true;
            SplitCylinderEdges = false;
            CylinderEdgeDivisions = 8;
            AutoExport = false;
            ExportPath = "";
            ExportFormat = "LS-DYNA";
        }

        /// <summary>유효성 검증</summary>
        public bool Validate(out string errorMessage)
        {
            if (ImportMode != StepImportMode.UseCurrentPart &&
                string.IsNullOrWhiteSpace(StepFilePath))
            {
                errorMessage = "STEP 파일 경로를 지정하세요.";
                return false;
            }

            if (ToleranceMm <= 0)
            {
                errorMessage = "허용 거리는 0보다 커야 합니다.";
                return false;
            }

            if (ElementSizeMm <= 0)
            {
                errorMessage = "요소 크기는 0보다 커야 합니다.";
                return false;
            }

            if (GrowthRate < 1.0 || GrowthRate > 5.0)
            {
                errorMessage = "성장률은 1.0~5.0 범위여야 합니다.";
                return false;
            }

            if (SplitCylinderEdges && CylinderEdgeDivisions < 3)
            {
                errorMessage = "원 분할 수는 3 이상이어야 합니다.";
                return false;
            }

            if (AutoExport && string.IsNullOrWhiteSpace(ExportPath))
            {
                errorMessage = "내보내기 경로를 지정하세요.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
