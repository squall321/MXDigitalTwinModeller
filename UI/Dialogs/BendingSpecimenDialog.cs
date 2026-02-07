using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.DMA;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.DMA;

#if V251
using SpaceClaim.Api.V251.Extensibility;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Extensibility;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    /// <summary>
    /// 굽힘시험 시편 생성 통합 대화창 (3점/4점 굽힘 통합, Modeless with Preview)
    /// ASTM D790, ISO 178, ASTM D7264, ASTM C1161, ASTM D6272, DMA 규격 지원
    /// </summary>
    public partial class BendingSpecimenDialog : Form
    {
        private readonly DMA3PointBendingService service3pt;
        private readonly DMA4PointBendingService service4pt;
        private Part activePart;
        private DesignBody previewBody;
        private List<DesignBody> previewFixtures;

        public DMA3PointBendingParameters Parameters3pt { get; private set; }
        public DMA4PointBendingParameters Parameters4pt { get; private set; }

        /// <summary>
        /// 현재 선택된 시험 타입이 3점 굽힘인지 여부
        /// </summary>
        public bool Is3Point => cmbTestType.SelectedIndex == 0;

        // ===== 3점 굽힘 프리셋 키 =====
        private static readonly string[] presetKeys3pt = new string[]
        {
            "ASTM_D790_16",     // 0
            "ASTM_D790_32",     // 1
            "ISO_178",          // 2
            "ASTM_D7264",       // 3
            "ASTM_C1161_A",     // 4
            "ASTM_C1161_B",     // 5
            "DMA_Standard",     // 6
            "Custom",           // 7
        };

        // ===== 4점 굽힘 프리셋 키 =====
        private static readonly string[] presetKeys4pt = new string[]
        {
            "ASTM_D6272_16",    // 0
            "ASTM_D6272_32",    // 1
            "ASTM_C1161_B",     // 2
            "ASTM_C1161_C",     // 3
            "DMA_Standard",     // 4
            "Custom",           // 5
        };

        public BendingSpecimenDialog(Part part)
        {
            InitializeComponent();
            service3pt = new DMA3PointBendingService();
            service4pt = new DMA4PointBendingService();
            activePart = part;
            Parameters3pt = new DMA3PointBendingParameters();
            Parameters4pt = new DMA4PointBendingParameters();
            previewFixtures = new List<DesignBody>();

            InitializeTestTypes();
            PopulateSpecimenTypes();
            UpdateUIForTestType();
            LoadPresetParameters();

            // 대화창이 항상 맨 앞에 표시
            this.TopMost = true;

            // 대화창이 닫힐 때 미리보기 정리
            this.FormClosing += BendingSpecimenDialog_FormClosing;

            // 파라미터 변경 시 경고 업데이트
            numLength.ValueChanged += (s, e) => UpdateWarnings();
            numWidth.ValueChanged += (s, e) => UpdateWarnings();
            numThickness.ValueChanged += (s, e) => { UpdateDescription(); UpdateWarnings(); };
            numSpan.ValueChanged += (s, e) => { UpdateDescription(); UpdateWarnings(); };
            numOuterSpan.ValueChanged += (s, e) => { UpdateDescription(); UpdateWarnings(); };
            numInnerSpan.ValueChanged += (s, e) => { UpdateDescription(); UpdateWarnings(); };
            numSupportDiameter.ValueChanged += (s, e) => UpdateWarnings();
            numLoadingNoseDiameter.ValueChanged += (s, e) => UpdateWarnings();
            numSupportHeight.ValueChanged += (s, e) => UpdateWarnings();
            numLoadingNoseHeight.ValueChanged += (s, e) => UpdateWarnings();
        }

        /// <summary>
        /// 4점 굽힘을 초기 선택으로 설정 (4점 굽힘 커맨드에서 호출)
        /// </summary>
        public void Select4PointBending()
        {
            cmbTestType.SelectedIndex = 1;
        }

        /// <summary>
        /// 시험 타입 콤보박스 초기화
        /// </summary>
        private void InitializeTestTypes()
        {
            cmbTestType.Items.Clear();
            cmbTestType.Items.Add("3점 굽힘 (3-Point Bending)");
            cmbTestType.Items.Add("4점 굽힘 (4-Point Bending)");
            cmbTestType.SelectedIndex = 0;
            cmbTestType.SelectedIndexChanged += cmbTestType_SelectedIndexChanged;
        }

        /// <summary>
        /// 시편 규격 콤보박스 채우기 (시험 타입에 따라 다른 항목)
        /// </summary>
        private void PopulateSpecimenTypes()
        {
            cmbSpecimenType.SelectedIndexChanged -= cmbSpecimenType_SelectedIndexChanged;
            cmbSpecimenType.Items.Clear();

            if (Is3Point)
            {
                // ===== 3점 굽힘 프리셋 =====
                cmbSpecimenType.Items.Add("── 플라스틱 ──");
                cmbSpecimenType.Items.Add("ASTM D790 (16:1, Plastic)");
                cmbSpecimenType.Items.Add("ASTM D790 (32:1, Plastic)");
                cmbSpecimenType.Items.Add("ISO 178 (Plastic, International)");
                cmbSpecimenType.Items.Add("── 복합재 ──");
                cmbSpecimenType.Items.Add("ASTM D7264 (32:1, Composite)");
                cmbSpecimenType.Items.Add("── 세라믹 ──");
                cmbSpecimenType.Items.Add("ASTM C1161 - Config A (Ceramic)");
                cmbSpecimenType.Items.Add("ASTM C1161 - Config B (Ceramic)");
                cmbSpecimenType.Items.Add("── DMA ──");
                cmbSpecimenType.Items.Add("DMA Standard");
                cmbSpecimenType.Items.Add("── 사용자 정의 ──");
                cmbSpecimenType.Items.Add("Custom");
            }
            else
            {
                // ===== 4점 굽힘 프리셋 =====
                cmbSpecimenType.Items.Add("── 플라스틱 ──");
                cmbSpecimenType.Items.Add("ASTM D6272 (16:1, Plastic)");
                cmbSpecimenType.Items.Add("ASTM D6272 (32:1, Plastic)");
                cmbSpecimenType.Items.Add("── 세라믹 ──");
                cmbSpecimenType.Items.Add("ASTM C1161 - Config B (Ceramic)");
                cmbSpecimenType.Items.Add("ASTM C1161 - Config C (Ceramic)");
                cmbSpecimenType.Items.Add("── DMA ──");
                cmbSpecimenType.Items.Add("DMA Standard");
                cmbSpecimenType.Items.Add("── 사용자 정의 ──");
                cmbSpecimenType.Items.Add("Custom");
            }

            cmbSpecimenType.SelectedIndex = 1; // 첫 번째 데이터 항목 (separator 건너뜀)
            cmbSpecimenType.SelectedIndexChanged += cmbSpecimenType_SelectedIndexChanged;
        }

        /// <summary>
        /// 구분선(separator) 항목인지 확인
        /// </summary>
        private bool IsSeparatorItem(int index)
        {
            if (index < 0 || index >= cmbSpecimenType.Items.Count)
                return false;
            return cmbSpecimenType.Items[index].ToString().StartsWith("──");
        }

        /// <summary>
        /// 콤보박스 인덱스 → 프리셋 키 배열 인덱스 매핑
        /// </summary>
        private int GetDataIndex(int comboIndex)
        {
            int dataIndex = -1;
            for (int i = 0; i <= comboIndex; i++)
            {
                if (!IsSeparatorItem(i))
                    dataIndex++;
            }
            return dataIndex;
        }

        /// <summary>
        /// 현재 선택된 프리셋 키 반환
        /// </summary>
        private string GetSelectedPresetKey()
        {
            int comboIndex = cmbSpecimenType.SelectedIndex;
            if (comboIndex < 0 || IsSeparatorItem(comboIndex))
                return Is3Point ? "ASTM_D790_16" : "ASTM_D6272_16";

            int dataIndex = GetDataIndex(comboIndex);
            string[] keys = Is3Point ? presetKeys3pt : presetKeys4pt;

            if (dataIndex >= 0 && dataIndex < keys.Length)
                return keys[dataIndex];

            return Is3Point ? "ASTM_D790_16" : "ASTM_D6272_16";
        }

        // =============================================
        //  3점 굽힘 프리셋 기본값
        // =============================================
        private DMA3PointBendingParameters Get3ptPreset(string key)
        {
            var p = new DMA3PointBendingParameters();

            switch (key)
            {
                case "ASTM_D790_16":
                    // ASTM D790 (Span/Thickness = 16:1) - 가장 일반적인 플라스틱 굽힘
                    p.Length = 127.0;
                    p.Width = 12.7;
                    p.Thickness = 3.2;
                    p.Span = 51.2;          // 16 × 3.2
                    p.SupportDiameter = 8.0;
                    p.LoadingNoseDiameter = 8.0;
                    p.SupportHeight = 20.0;
                    p.LoadingNoseHeight = 20.0;
                    break;

                case "ASTM_D790_32":
                    // ASTM D790 (Span/Thickness = 32:1) - 대변형 플라스틱 굽힘
                    p.Length = 127.0;
                    p.Width = 12.7;
                    p.Thickness = 3.2;
                    p.Span = 102.4;         // 32 × 3.2
                    p.SupportDiameter = 8.0;
                    p.LoadingNoseDiameter = 8.0;
                    p.SupportHeight = 20.0;
                    p.LoadingNoseHeight = 20.0;
                    break;

                case "ISO_178":
                    // ISO 178 - 플라스틱 국제규격 (ASTM D790 대응)
                    p.Length = 80.0;
                    p.Width = 10.0;
                    p.Thickness = 4.0;
                    p.Span = 64.0;          // 16 × 4.0
                    p.SupportDiameter = 8.0;
                    p.LoadingNoseDiameter = 8.0;
                    p.SupportHeight = 20.0;
                    p.LoadingNoseHeight = 20.0;
                    break;

                case "ASTM_D7264":
                    // ASTM D7264 (32:1) - 복합재 굽힘
                    p.Length = 160.0;
                    p.Width = 13.0;
                    p.Thickness = 4.0;
                    p.Span = 128.0;         // 32 × 4.0
                    p.SupportDiameter = 6.0;
                    p.LoadingNoseDiameter = 6.0;
                    p.SupportHeight = 20.0;
                    p.LoadingNoseHeight = 20.0;
                    break;

                case "ASTM_C1161_A":
                    // ASTM C1161 Config A - 소형 세라믹
                    p.Length = 25.0;
                    p.Width = 2.0;
                    p.Thickness = 1.5;
                    p.Span = 20.0;
                    p.SupportDiameter = 4.5;
                    p.LoadingNoseDiameter = 4.5;
                    p.SupportHeight = 15.0;
                    p.LoadingNoseHeight = 15.0;
                    break;

                case "ASTM_C1161_B":
                    // ASTM C1161 Config B - 중형 세라믹
                    p.Length = 45.0;
                    p.Width = 4.0;
                    p.Thickness = 3.0;
                    p.Span = 40.0;
                    p.SupportDiameter = 4.5;
                    p.LoadingNoseDiameter = 4.5;
                    p.SupportHeight = 15.0;
                    p.LoadingNoseHeight = 15.0;
                    break;

                case "DMA_Standard":
                default:
                    // DMA 표준 (기본 생성자 값 사용)
                    break;

                case "Custom":
                    // 사용자 정의 - 현재 값 유지
                    return null;
            }

            return p;
        }

        // =============================================
        //  4점 굽힘 프리셋 기본값
        // =============================================
        private DMA4PointBendingParameters Get4ptPreset(string key)
        {
            var p = new DMA4PointBendingParameters();

            switch (key)
            {
                case "ASTM_D6272_16":
                    // ASTM D6272 (Span/Thickness = 16:1) - 플라스틱 4점 굽힘
                    p.Length = 127.0;
                    p.Width = 12.7;
                    p.Thickness = 3.2;
                    p.OuterSpan = 51.2;     // 16 × 3.2
                    p.InnerSpan = 17.1;     // Outer / 3
                    p.SupportDiameter = 8.0;
                    p.LoadingNoseDiameter = 8.0;
                    p.SupportHeight = 20.0;
                    p.LoadingNoseHeight = 20.0;
                    break;

                case "ASTM_D6272_32":
                    // ASTM D6272 (Span/Thickness = 32:1) - 플라스틱 4점 굽힘 (대변형)
                    p.Length = 127.0;
                    p.Width = 12.7;
                    p.Thickness = 3.2;
                    p.OuterSpan = 102.4;    // 32 × 3.2
                    p.InnerSpan = 34.1;     // Outer / 3
                    p.SupportDiameter = 8.0;
                    p.LoadingNoseDiameter = 8.0;
                    p.SupportHeight = 20.0;
                    p.LoadingNoseHeight = 20.0;
                    break;

                case "ASTM_C1161_B":
                    // ASTM C1161 Config B - 세라믹 4점 (45mm 시편)
                    p.Length = 45.0;
                    p.Width = 4.0;
                    p.Thickness = 3.0;
                    p.OuterSpan = 40.0;
                    p.InnerSpan = 20.0;
                    p.SupportDiameter = 4.5;
                    p.LoadingNoseDiameter = 4.5;
                    p.SupportHeight = 15.0;
                    p.LoadingNoseHeight = 15.0;
                    break;

                case "ASTM_C1161_C":
                    // ASTM C1161 Config C - 세라믹 4점 (60mm 시편)
                    p.Length = 60.0;
                    p.Width = 8.0;
                    p.Thickness = 6.0;
                    p.OuterSpan = 40.0;
                    p.InnerSpan = 20.0;
                    p.SupportDiameter = 4.5;
                    p.LoadingNoseDiameter = 4.5;
                    p.SupportHeight = 15.0;
                    p.LoadingNoseHeight = 15.0;
                    break;

                case "DMA_Standard":
                default:
                    // DMA 표준 (기본 생성자 값 사용)
                    break;

                case "Custom":
                    // 사용자 정의 - 현재 값 유지
                    return null;
            }

            return p;
        }

        /// <summary>
        /// 프리셋에 해당하는 설명 반환
        /// </summary>
        private string GetPresetDescription(string key)
        {
            switch (key)
            {
                // 3pt
                case "ASTM_D790_16":
                    return "ASTM D790 (플라스틱 굽힘, 16:1, L:127 W:12.7 T:3.2mm)";
                case "ASTM_D790_32":
                    return "ASTM D790 (플라스틱 굽힘, 32:1, L:127 W:12.7 T:3.2mm)";
                case "ISO_178":
                    return "ISO 178 (플라스틱 국제규격, 16:1, L:80 W:10 T:4mm)";
                case "ASTM_D7264":
                    return "ASTM D7264 (복합재 굽힘, 32:1, L:160 W:13 T:4mm)";
                case "ASTM_C1161_A":
                    return "ASTM C1161 Config A (세라믹, L:25 W:2 T:1.5mm, Span:20mm)";

                // 4pt
                case "ASTM_D6272_16":
                    return "ASTM D6272 (플라스틱 4점 굽힘, 16:1, L:127 W:12.7 T:3.2mm)";
                case "ASTM_D6272_32":
                    return "ASTM D6272 (플라스틱 4점 굽힘, 32:1, L:127 W:12.7 T:3.2mm)";
                case "ASTM_C1161_C":
                    return "ASTM C1161 Config C (세라믹, L:60 W:8 T:6mm, Outer:40 Inner:20mm)";

                // 공통
                case "ASTM_C1161_B":
                    return "ASTM C1161 Config B (세라믹, L:45 W:4 T:3mm, Span:40mm)";
                case "DMA_Standard":
                    return Is3Point
                        ? "DMA 3점 굽힘 표준 (L:80 W:10 T:4mm, Span:64mm)"
                        : "DMA 4점 굽힘 표준 (L:100 W:10 T:4mm, Outer:80 Inner:40mm)";
                case "Custom":
                    return "사용자 정의 시편";
                default:
                    return "";
            }
        }

        /// <summary>
        /// 시험 타입에 따라 UI 갱신 (3pt/4pt 전용 컨트롤 표시/숨김)
        /// </summary>
        private void UpdateUIForTestType()
        {
            bool is3pt = Is3Point;

            // 3pt 전용 컨트롤
            lblSpan.Visible = is3pt;
            numSpan.Visible = is3pt;

            // 4pt 전용 컨트롤
            lblOuterSpan.Visible = !is3pt;
            numOuterSpan.Visible = !is3pt;
            lblInnerSpan.Visible = !is3pt;
            numInnerSpan.Visible = !is3pt;

            // 4pt일 때 InnerSpan 행이 추가되므로 아래 컨트롤 위치 조정
            int spanRowY = lblSpan.Location.Y;
            int yGap = 35;

            if (is3pt)
            {
                int nextY = spanRowY + yGap;
                RepositionFixtureControls(nextY, yGap);
            }
            else
            {
                int nextY = spanRowY + yGap * 2;
                RepositionFixtureControls(nextY, yGap);
            }
        }

        /// <summary>
        /// 지지점/노즈 컨트롤 위치를 재배치
        /// </summary>
        private void RepositionFixtureControls(int startY, int yGap)
        {
            int y = startY;

            lblSupportDiameter.Location = new System.Drawing.Point(15, y);
            numSupportDiameter.Location = new System.Drawing.Point(180, y - 3);
            y += yGap;

            lblLoadingNoseDiameter.Location = new System.Drawing.Point(15, y);
            numLoadingNoseDiameter.Location = new System.Drawing.Point(180, y - 3);
            y += yGap;

            lblSupportHeight.Location = new System.Drawing.Point(15, y);
            numSupportHeight.Location = new System.Drawing.Point(180, y - 3);
            y += yGap;

            lblLoadingNoseHeight.Location = new System.Drawing.Point(15, y);
            numLoadingNoseHeight.Location = new System.Drawing.Point(180, y - 3);
            y += yGap;

            // grpDimensions 크기 조정
            grpDimensions.Size = new System.Drawing.Size(440, y + 10);

            // 경고 레이블 위치
            int warningY = grpDimensions.Bottom + 6;
            lblWarning.Location = new System.Drawing.Point(12, warningY);

            // 버튼 위치
            int btnY = warningY + lblWarning.Height + 10;
            btnPreview.Location = new System.Drawing.Point(140, btnY);
            btnCreate.Location = new System.Drawing.Point(250, btnY);
            btnCancel.Location = new System.Drawing.Point(360, btnY);

            // 폼 크기
            this.ClientSize = new System.Drawing.Size(474, btnY + 46);
        }

        /// <summary>
        /// 선택된 프리셋의 기본 파라미터 로드
        /// </summary>
        private void LoadPresetParameters()
        {
            string key = GetSelectedPresetKey();

            if (Is3Point)
            {
                var preset = Get3ptPreset(key);
                if (preset != null)
                {
                    Parameters3pt = preset;
                    // Custom이 아닌 경우 DMASpecimenType은 Standard로 유지
                    Parameters3pt.SpecimenType = (key == "Custom") ? DMASpecimenType.Custom : DMASpecimenType.Standard;
                }
                BindParametersToUI();
            }
            else
            {
                var preset = Get4ptPreset(key);
                if (preset != null)
                {
                    Parameters4pt = preset;
                    Parameters4pt.SpecimenType = (key == "Custom") ? DMASpecimenType.Custom : DMASpecimenType.Standard;
                }
                BindParametersToUI();
            }
        }

        /// <summary>
        /// 파라미터를 UI에 바인딩
        /// </summary>
        private void BindParametersToUI()
        {
            if (Is3Point)
            {
                numLength.Value = (decimal)Parameters3pt.Length;
                numWidth.Value = (decimal)Parameters3pt.Width;
                numThickness.Value = (decimal)Parameters3pt.Thickness;
                numSpan.Value = (decimal)Parameters3pt.Span;
                numSupportDiameter.Value = (decimal)Parameters3pt.SupportDiameter;
                numLoadingNoseDiameter.Value = (decimal)Parameters3pt.LoadingNoseDiameter;
                numSupportHeight.Value = (decimal)Parameters3pt.SupportHeight;
                numLoadingNoseHeight.Value = (decimal)Parameters3pt.LoadingNoseHeight;
            }
            else
            {
                numLength.Value = (decimal)Parameters4pt.Length;
                numWidth.Value = (decimal)Parameters4pt.Width;
                numThickness.Value = (decimal)Parameters4pt.Thickness;
                numOuterSpan.Value = (decimal)Parameters4pt.OuterSpan;
                numInnerSpan.Value = (decimal)Parameters4pt.InnerSpan;
                numSupportDiameter.Value = (decimal)Parameters4pt.SupportDiameter;
                numLoadingNoseDiameter.Value = (decimal)Parameters4pt.LoadingNoseDiameter;
                numSupportHeight.Value = (decimal)Parameters4pt.SupportHeight;
                numLoadingNoseHeight.Value = (decimal)Parameters4pt.LoadingNoseHeight;
            }

            UpdateDescription();
            UpdateWarnings();
        }

        /// <summary>
        /// 설명 업데이트 (규격 설명 + 비율 정보)
        /// </summary>
        private void UpdateDescription()
        {
            string key = GetSelectedPresetKey();
            string presetDesc = GetPresetDescription(key);

            if (Is3Point)
            {
                double ratio = (double)numSpan.Value / (double)numThickness.Value;
                lblDescription.Text = string.IsNullOrEmpty(presetDesc)
                    ? $"3점 굽힘 (Span/T = {ratio:F1}:1)"
                    : $"{presetDesc}\n(Span/T = {ratio:F1}:1)";
            }
            else
            {
                double ratio = (double)numOuterSpan.Value / (double)numInnerSpan.Value;
                lblDescription.Text = string.IsNullOrEmpty(presetDesc)
                    ? $"4점 굽힘 (Outer/Inner = {ratio:F1}:1)"
                    : $"{presetDesc}\n(Outer/Inner = {ratio:F1}:1)";
            }
        }

        /// <summary>
        /// 경고 메시지 업데이트 (규격별 권장 범위 적용)
        /// </summary>
        private void UpdateWarnings()
        {
            ReadParametersFromUI();
            string key = GetSelectedPresetKey();
            string warnings = GetRangeWarningsForPreset(key);

            if (string.IsNullOrEmpty(warnings))
            {
                lblWarning.Text = "";
                lblWarning.Height = 0;
            }
            else
            {
                lblWarning.Text = "⚠ 권장 범위 이탈:\n" + warnings;
                lblWarning.Height = lblWarning.PreferredHeight;
            }

            // 경고 크기 변경 후 버튼 위치 재조정
            int btnY = lblWarning.Bottom + 10;
            btnPreview.Location = new System.Drawing.Point(140, btnY);
            btnCreate.Location = new System.Drawing.Point(250, btnY);
            btnCancel.Location = new System.Drawing.Point(360, btnY);
            this.ClientSize = new System.Drawing.Size(474, btnY + 46);
        }

        // =============================================
        //  규격별 권장 범위 경고
        // =============================================

        /// <summary>
        /// 규격별 권장 범위 구조체
        /// </summary>
        private struct SpecimenRange
        {
            public double LengthMin, LengthMax;
            public double WidthMin, WidthMax;
            public double ThicknessMin, ThicknessMax;
            public double SpanRatioMin, SpanRatioMax;     // Span/T (3pt) or Outer/Inner (4pt)
            public string RatioLabel;                      // 비율 설명 텍스트
            public double SupportDiaMin, SupportDiaMax;
            public double NoseDiaMin, NoseDiaMax;
        }

        /// <summary>
        /// 프리셋 키에 해당하는 권장 범위 반환
        /// </summary>
        private SpecimenRange GetRangeForPreset(string key)
        {
            switch (key)
            {
                // ===== 3점 굽힘 =====
                case "ASTM_D790_16":
                case "ASTM_D790_32":
                    // ASTM D790: L=127, W=12.7, T=3.2, Span/T=16~32
                    return new SpecimenRange
                    {
                        LengthMin = 50, LengthMax = 200,
                        WidthMin = 10, WidthMax = 25.4,
                        ThicknessMin = 1.6, ThicknessMax = 12.7,
                        SpanRatioMin = 16, SpanRatioMax = 32,
                        RatioLabel = "Span/T",
                        SupportDiaMin = 3, SupportDiaMax = 15,
                        NoseDiaMin = 3, NoseDiaMax = 15,
                    };

                case "ISO_178":
                    // ISO 178: L=80, W=10, T=4, Span/T=16
                    return new SpecimenRange
                    {
                        LengthMin = 60, LengthMax = 120,
                        WidthMin = 8, WidthMax = 15,
                        ThicknessMin = 2, ThicknessMax = 6,
                        SpanRatioMin = 14, SpanRatioMax = 18,
                        RatioLabel = "Span/T",
                        SupportDiaMin = 3, SupportDiaMax = 15,
                        NoseDiaMin = 3, NoseDiaMax = 15,
                    };

                case "ASTM_D7264":
                    // ASTM D7264: L=160, W=13, T=4, Span/T=32
                    return new SpecimenRange
                    {
                        LengthMin = 100, LengthMax = 250,
                        WidthMin = 10, WidthMax = 25,
                        ThicknessMin = 2, ThicknessMax = 8,
                        SpanRatioMin = 16, SpanRatioMax = 40,
                        RatioLabel = "Span/T",
                        SupportDiaMin = 3, SupportDiaMax = 12,
                        NoseDiaMin = 3, NoseDiaMax = 12,
                    };

                case "ASTM_C1161_A":
                    // ASTM C1161 Config A: L=25, W=2, T=1.5
                    return new SpecimenRange
                    {
                        LengthMin = 20, LengthMax = 30,
                        WidthMin = 1.5, WidthMax = 2.5,
                        ThicknessMin = 1.0, ThicknessMax = 2.0,
                        SpanRatioMin = 10, SpanRatioMax = 20,
                        RatioLabel = "Span/T",
                        SupportDiaMin = 2, SupportDiaMax = 6,
                        NoseDiaMin = 2, NoseDiaMax = 6,
                    };

                case "ASTM_C1161_B":
                    if (Is3Point)
                    {
                        // 3pt Config B: L=45, W=4, T=3
                        return new SpecimenRange
                        {
                            LengthMin = 40, LengthMax = 55,
                            WidthMin = 3, WidthMax = 5,
                            ThicknessMin = 2.5, ThicknessMax = 4,
                            SpanRatioMin = 10, SpanRatioMax = 20,
                            RatioLabel = "Span/T",
                            SupportDiaMin = 2, SupportDiaMax = 6,
                            NoseDiaMin = 2, NoseDiaMax = 6,
                        };
                    }
                    else
                    {
                        // 4pt Config B: L=45, W=4, T=3, Outer=40, Inner=20
                        return new SpecimenRange
                        {
                            LengthMin = 40, LengthMax = 55,
                            WidthMin = 3, WidthMax = 5,
                            ThicknessMin = 2.5, ThicknessMax = 4,
                            SpanRatioMin = 1.5, SpanRatioMax = 3,
                            RatioLabel = "Outer/Inner",
                            SupportDiaMin = 2, SupportDiaMax = 6,
                            NoseDiaMin = 2, NoseDiaMax = 6,
                        };
                    }

                // ===== 4점 굽힘 =====
                case "ASTM_D6272_16":
                case "ASTM_D6272_32":
                    // ASTM D6272: L=127, W=12.7, T=3.2, Outer/Inner=3:1
                    return new SpecimenRange
                    {
                        LengthMin = 50, LengthMax = 200,
                        WidthMin = 10, WidthMax = 25.4,
                        ThicknessMin = 1.6, ThicknessMax = 12.7,
                        SpanRatioMin = 2, SpanRatioMax = 4,
                        RatioLabel = "Outer/Inner",
                        SupportDiaMin = 3, SupportDiaMax = 15,
                        NoseDiaMin = 3, NoseDiaMax = 15,
                    };

                case "ASTM_C1161_C":
                    // ASTM C1161 Config C: L=60, W=8, T=6, Outer=40, Inner=20
                    return new SpecimenRange
                    {
                        LengthMin = 50, LengthMax = 70,
                        WidthMin = 6, WidthMax = 10,
                        ThicknessMin = 4, ThicknessMax = 8,
                        SpanRatioMin = 1.5, SpanRatioMax = 3,
                        RatioLabel = "Outer/Inner",
                        SupportDiaMin = 2, SupportDiaMax = 6,
                        NoseDiaMin = 2, NoseDiaMax = 6,
                    };

                case "DMA_Standard":
                default:
                    // DMA 표준 범위
                    if (Is3Point)
                    {
                        return new SpecimenRange
                        {
                            LengthMin = 50, LengthMax = 150,
                            WidthMin = 5, WidthMax = 25,
                            ThicknessMin = 1, ThicknessMax = 10,
                            SpanRatioMin = 16, SpanRatioMax = 32,
                            RatioLabel = "Span/T",
                            SupportDiaMin = 5, SupportDiaMax = 15,
                            NoseDiaMin = 5, NoseDiaMax = 15,
                        };
                    }
                    else
                    {
                        return new SpecimenRange
                        {
                            LengthMin = 60, LengthMax = 200,
                            WidthMin = 5, WidthMax = 25,
                            ThicknessMin = 1, ThicknessMax = 10,
                            SpanRatioMin = 2, SpanRatioMax = 3,
                            RatioLabel = "Outer/Inner",
                            SupportDiaMin = 5, SupportDiaMax = 15,
                            NoseDiaMin = 5, NoseDiaMax = 15,
                        };
                    }

                case "Custom":
                    // Custom은 경고 없음
                    return new SpecimenRange
                    {
                        LengthMin = 0, LengthMax = double.MaxValue,
                        WidthMin = 0, WidthMax = double.MaxValue,
                        ThicknessMin = 0, ThicknessMax = double.MaxValue,
                        SpanRatioMin = 0, SpanRatioMax = double.MaxValue,
                        RatioLabel = "",
                        SupportDiaMin = 0, SupportDiaMax = double.MaxValue,
                        NoseDiaMin = 0, NoseDiaMax = double.MaxValue,
                    };
            }
        }

        /// <summary>
        /// 현재 프리셋 기준 경고 메시지 생성
        /// </summary>
        private string GetRangeWarningsForPreset(string key)
        {
            if (key == "Custom")
                return string.Empty;

            var range = GetRangeForPreset(key);
            var warnings = new List<string>();

            double length = (double)numLength.Value;
            double width = (double)numWidth.Value;
            double thickness = (double)numThickness.Value;
            double supportDia = (double)numSupportDiameter.Value;
            double noseDia = (double)numLoadingNoseDiameter.Value;

            if (length < range.LengthMin || length > range.LengthMax)
                warnings.Add($"• 길이 권장: {range.LengthMin}~{range.LengthMax}mm (현재: {length:F1}mm)");

            if (width < range.WidthMin || width > range.WidthMax)
                warnings.Add($"• 폭 권장: {range.WidthMin}~{range.WidthMax}mm (현재: {width:F1}mm)");

            if (thickness < range.ThicknessMin || thickness > range.ThicknessMax)
                warnings.Add($"• 두께 권장: {range.ThicknessMin}~{range.ThicknessMax}mm (현재: {thickness:F1}mm)");

            // 비율 경고
            if (!string.IsNullOrEmpty(range.RatioLabel))
            {
                double ratio;
                if (Is3Point)
                {
                    ratio = (double)numSpan.Value / thickness;
                }
                else
                {
                    double inner = (double)numInnerSpan.Value;
                    ratio = inner > 0 ? (double)numOuterSpan.Value / inner : 0;
                }

                if (ratio < range.SpanRatioMin || ratio > range.SpanRatioMax)
                    warnings.Add($"• {range.RatioLabel} 비율 권장: {range.SpanRatioMin:F0}~{range.SpanRatioMax:F0}:1 (현재: {ratio:F1}:1)");
            }

            if (supportDia < range.SupportDiaMin || supportDia > range.SupportDiaMax)
                warnings.Add($"• 지지점 직경 권장: {range.SupportDiaMin}~{range.SupportDiaMax}mm (현재: {supportDia:F1}mm)");

            if (noseDia < range.NoseDiaMin || noseDia > range.NoseDiaMax)
                warnings.Add($"• 로딩노즈 직경 권장: {range.NoseDiaMin}~{range.NoseDiaMax}mm (현재: {noseDia:F1}mm)");

            return warnings.Count > 0 ? string.Join("\n", warnings) : string.Empty;
        }

        /// <summary>
        /// UI에서 파라미터 읽기
        /// </summary>
        private void ReadParametersFromUI()
        {
            if (Is3Point)
            {
                Parameters3pt.Length = (double)numLength.Value;
                Parameters3pt.Width = (double)numWidth.Value;
                Parameters3pt.Thickness = (double)numThickness.Value;
                Parameters3pt.Span = (double)numSpan.Value;
                Parameters3pt.SupportDiameter = (double)numSupportDiameter.Value;
                Parameters3pt.LoadingNoseDiameter = (double)numLoadingNoseDiameter.Value;
                Parameters3pt.SupportHeight = (double)numSupportHeight.Value;
                Parameters3pt.LoadingNoseHeight = (double)numLoadingNoseHeight.Value;
            }
            else
            {
                Parameters4pt.Length = (double)numLength.Value;
                Parameters4pt.Width = (double)numWidth.Value;
                Parameters4pt.Thickness = (double)numThickness.Value;
                Parameters4pt.OuterSpan = (double)numOuterSpan.Value;
                Parameters4pt.InnerSpan = (double)numInnerSpan.Value;
                Parameters4pt.SupportDiameter = (double)numSupportDiameter.Value;
                Parameters4pt.LoadingNoseDiameter = (double)numLoadingNoseDiameter.Value;
                Parameters4pt.SupportHeight = (double)numSupportHeight.Value;
                Parameters4pt.LoadingNoseHeight = (double)numLoadingNoseHeight.Value;
            }
        }

        /// <summary>
        /// 입력 검증
        /// </summary>
        private bool ValidateInputs()
        {
            ReadParametersFromUI();
            string errorMessage;

            if (Is3Point)
            {
                if (!Parameters3pt.Validate(out errorMessage))
                {
                    ValidationHelper.ShowError(errorMessage, "입력 오류");
                    return false;
                }
            }
            else
            {
                if (!Parameters4pt.Validate(out errorMessage))
                {
                    ValidationHelper.ShowError(errorMessage, "입력 오류");
                    return false;
                }
            }

            return true;
        }

        // ===== 이벤트 핸들러 =====

        private void cmbTestType_SelectedIndexChanged(object sender, EventArgs e)
        {
            CleanupPreview();

            // 시험 타입 변경 시 규격 콤보 재구성
            PopulateSpecimenTypes();
            UpdateUIForTestType();
            LoadPresetParameters();
        }

        private void cmbSpecimenType_SelectedIndexChanged(object sender, EventArgs e)
        {
            int idx = cmbSpecimenType.SelectedIndex;
            if (idx < 0) return;

            // 구분선 선택 시 다음 항목으로 이동
            if (IsSeparatorItem(idx))
            {
                if (idx + 1 < cmbSpecimenType.Items.Count)
                    cmbSpecimenType.SelectedIndex = idx + 1;
                return;
            }

            // Custom이 아닌 표준 규격은 프리셋 로드
            string key = GetSelectedPresetKey();
            if (key != "Custom")
            {
                LoadPresetParameters();
            }
            else
            {
                // Custom 선택 시 설명만 업데이트
                UpdateDescription();
            }
        }

        private void btnLoadDefaults_Click(object sender, EventArgs e)
        {
            LoadPresetParameters();
        }

        private void btnPreview_Click(object sender, EventArgs e)
        {
            if (!ValidateInputs())
                return;

            try
            {
                CleanupPreview();

                WriteBlock.ExecuteTask("Bending Preview", () =>
                {
                    var existingBodies = new HashSet<DesignBody>(activePart.Bodies);

                    if (Is3Point)
                        previewBody = service3pt.Create3PointBendingSpecimen(activePart, Parameters3pt);
                    else
                        previewBody = service4pt.Create4PointBendingSpecimen(activePart, Parameters4pt);

                    previewFixtures.Clear();
                    foreach (var body in activePart.Bodies)
                    {
                        if (!existingBodies.Contains(body) && body != previewBody)
                        {
                            previewFixtures.Add(body);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    $"미리보기 생성 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "미리보기 오류"
                );
            }
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            if (!ValidateInputs())
                return;

            try
            {
                if (previewBody == null)
                {
                    WriteBlock.ExecuteTask("Create Bending Specimen", () =>
                    {
                        if (Is3Point)
                            service3pt.Create3PointBendingSpecimen(activePart, Parameters3pt);
                        else
                            service4pt.Create4PointBendingSpecimen(activePart, Parameters4pt);
                    });
                }
                else
                {
                    previewBody = null;
                    previewFixtures.Clear();
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    $"시편 생성 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "생성 오류"
                );
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        /// <summary>
        /// 미리보기 정리
        /// </summary>
        private void CleanupPreview()
        {
            if (previewBody != null || previewFixtures.Count > 0)
            {
                try
                {
                    WriteBlock.ExecuteTask("Cleanup Preview", () =>
                    {
                        if (previewBody != null)
                        {
                            previewBody.Delete();
                        }

                        foreach (var fixture in previewFixtures)
                        {
                            if (fixture != null)
                            {
                                fixture.Delete();
                            }
                        }
                    });

                    previewBody = null;
                    previewFixtures.Clear();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Preview cleanup error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 폼 닫힐 때 미리보기 정리
        /// </summary>
        private void BendingSpecimenDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (DialogResult != DialogResult.OK)
            {
                CleanupPreview();
            }
        }
    }
}
