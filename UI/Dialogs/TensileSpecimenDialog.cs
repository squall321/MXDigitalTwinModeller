using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.TensileTest;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.TensileTest;

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
    /// ASTM 인장시험 시편 생성 대화창 (Modeless with Preview)
    /// </summary>
    public partial class TensileSpecimenDialog : Form
    {
        private readonly ASTMSpecimenFactory factory;
        private readonly SpecimenModelingService modelingService;
        private Part activePart;
        private DesignBody previewBody;
        private List<DesignBody> previewFixtures; // 그립 장비 추적
        private Timer previewTimer;

        public TensileSpecimenParameters Parameters { get; private set; }

        public TensileSpecimenDialog(Part part)
        {
            InitializeComponent();
            factory = new ASTMSpecimenFactory();
            modelingService = new SpecimenModelingService();
            activePart = part;
            Parameters = new TensileSpecimenParameters();
            previewFixtures = new List<DesignBody>();

            previewTimer = new Timer();
            previewTimer.Interval = 300;
            previewTimer.Tick += PreviewTimer_Tick;

            InitializeSpecimenTypes();
            LoadDefaultParameters();

            // 파라미터 변경 시 자동 미리보기
            numGaugeLength.ValueChanged += (s, ev) => SchedulePreview();
            numGaugeWidth.ValueChanged += (s, ev) => SchedulePreview();
            numThickness.ValueChanged += (s, ev) => SchedulePreview();
            numGripWidth.ValueChanged += (s, ev) => SchedulePreview();
            numTotalLength.ValueChanged += (s, ev) => SchedulePreview();
            numFilletRadius.ValueChanged += (s, ev) => SchedulePreview();
            numGripLength.ValueChanged += (s, ev) => SchedulePreview();
            numNotchDepth.ValueChanged += (s, ev) => SchedulePreview();
            numNotchAngle.ValueChanged += (s, ev) => SchedulePreview();
            numNotchRadius.ValueChanged += (s, ev) => SchedulePreview();
            numHoleDiameter.ValueChanged += (s, ev) => SchedulePreview();
            numHoleMajorAxis.ValueChanged += (s, ev) => SchedulePreview();
            numHoleMinorAxis.ValueChanged += (s, ev) => SchedulePreview();
            numTabLength.ValueChanged += (s, ev) => SchedulePreview();
            numTabThickness.ValueChanged += (s, ev) => SchedulePreview();
            chkDoubleNotch.CheckedChanged += (s, ev) => SchedulePreview();
            chkRectangular.CheckedChanged += (s, ev) => SchedulePreview();

            // 대화창이 항상 맨 앞에 표시
            this.TopMost = true;

            // 대화창이 닫힐 때 미리보기 정리
            this.FormClosing += TensileSpecimenDialog_FormClosing;
        }

        // 콤보박스 인덱스와 enum 매핑 배열
        private static readonly ASTMSpecimenType[] specimenTypeMap = new ASTMSpecimenType[]
        {
            // 금속 인장
            ASTMSpecimenType.ASTM_E8_Standard,      // 0
            ASTMSpecimenType.ASTM_E8_SubSize,        // 1
            ASTMSpecimenType.ISO_6892_1,             // 2
            // 플라스틱 인장
            ASTMSpecimenType.ASTM_D638_TypeI,        // 3
            ASTMSpecimenType.ASTM_D638_TypeII,       // 4
            ASTMSpecimenType.ASTM_D638_TypeIII,      // 5
            ASTMSpecimenType.ASTM_D638_TypeIV,       // 6
            ASTMSpecimenType.ASTM_D638_TypeV,        // 7
            ASTMSpecimenType.ISO_527_2_Type1A,       // 8
            ASTMSpecimenType.ISO_527_2_Type1B,       // 9
            // 노치 인장
            ASTMSpecimenType.ASTM_E602_VNotch,       // 10
            ASTMSpecimenType.ASTM_E602_UNotch,       // 11
            ASTMSpecimenType.ASTM_E338,              // 12
            ASTMSpecimenType.ASTM_E292,              // 13
            // 구멍 시편
            ASTMSpecimenType.ASTM_D5766_OHT,         // 14
            ASTMSpecimenType.ASTM_D6484_OHC,         // 15
            ASTMSpecimenType.ASTM_D6742_FHT,         // 16
            ASTMSpecimenType.ASTM_D5961_Bearing,     // 17
            // 복합재 인장
            ASTMSpecimenType.ASTM_D3039,             // 18
            // PCB
            ASTMSpecimenType.IPC_TM650_Tensile,      // 19
            ASTMSpecimenType.IPC_TM650_PTHPull,      // 20
            // DMA 인장
            ASTMSpecimenType.DMA_Tensile_Rectangle,  // 21
            ASTMSpecimenType.DMA_Tensile_DogBone,    // 22
            // 전단 시편
            ASTMSpecimenType.ASTM_D5379_Iosipescu,           // 23
            ASTMSpecimenType.ASTM_D7078_VNotchRailShear,     // 24
            // 사용자 정의
            ASTMSpecimenType.Custom,                 // 25
        };

        /// <summary>
        /// 시편 타입 콤보박스 초기화
        /// </summary>
        private void InitializeSpecimenTypes()
        {
            cmbSpecimenType.Items.Clear();
            // 금속 인장
            cmbSpecimenType.Items.Add("── 금속 인장 ──");  // separator - will be skipped
            cmbSpecimenType.Items.Add("ASTM E8 - Standard (Metal)");
            cmbSpecimenType.Items.Add("ASTM E8 - SubSize (Metal)");
            cmbSpecimenType.Items.Add("ISO 6892-1 (Metal, International)");
            // 플라스틱 인장
            cmbSpecimenType.Items.Add("── 플라스틱 인장 ──");
            cmbSpecimenType.Items.Add("ASTM D638 - Type I (Plastic)");
            cmbSpecimenType.Items.Add("ASTM D638 - Type II (Plastic)");
            cmbSpecimenType.Items.Add("ASTM D638 - Type III (Plastic)");
            cmbSpecimenType.Items.Add("ASTM D638 - Type IV (Plastic)");
            cmbSpecimenType.Items.Add("ASTM D638 - Type V (Plastic)");
            cmbSpecimenType.Items.Add("ISO 527-2 - Type 1A (Plastic, International)");
            cmbSpecimenType.Items.Add("ISO 527-2 - Type 1B (Plastic, International)");
            // 노치 인장
            cmbSpecimenType.Items.Add("── 노치 인장 ──");
            cmbSpecimenType.Items.Add("ASTM E602 - V-Notch (Metal)");
            cmbSpecimenType.Items.Add("ASTM E602 - U-Notch (Metal)");
            cmbSpecimenType.Items.Add("ASTM E338 (High-Strength Sheet)");
            cmbSpecimenType.Items.Add("ASTM E292 (Elevated Temperature)");
            // 구멍 시편
            cmbSpecimenType.Items.Add("── 구멍 시편 (OHT/OHC/Bearing) ──");
            cmbSpecimenType.Items.Add("ASTM D5766 - OHT (Composite)");
            cmbSpecimenType.Items.Add("ASTM D6484 - OHC (Composite)");
            cmbSpecimenType.Items.Add("ASTM D6742 - FHT (Composite)");
            cmbSpecimenType.Items.Add("ASTM D5961 - Bearing (Composite)");
            // 복합재 인장
            cmbSpecimenType.Items.Add("── 복합재 인장 ──");
            cmbSpecimenType.Items.Add("ASTM D3039 (Composite Tensile)");
            // PCB
            cmbSpecimenType.Items.Add("── PCB ──");
            cmbSpecimenType.Items.Add("IPC-TM-650 2.4.18.3 (PCB Tensile)");
            cmbSpecimenType.Items.Add("IPC-TM-650 2.4.1 (PCB PTH Pull)");
            // DMA 인장
            cmbSpecimenType.Items.Add("── DMA 인장 ──");
            cmbSpecimenType.Items.Add("DMA Tensile - Rectangle");
            cmbSpecimenType.Items.Add("DMA Tensile - DogBone");
            // 전단 시편
            cmbSpecimenType.Items.Add("── 전단 시편 ──");
            cmbSpecimenType.Items.Add("ASTM D5379 - Iosipescu (V-Notch Shear)");
            cmbSpecimenType.Items.Add("ASTM D7078 - V-Notch Rail Shear");
            // 사용자 정의
            cmbSpecimenType.Items.Add("── 사용자 정의 ──");
            cmbSpecimenType.Items.Add("Custom");

            cmbSpecimenType.SelectedIndex = 1; // ASTM E8 Standard
        }

        /// <summary>
        /// 구분선(separator) 항목인지 확인
        /// </summary>
        private bool IsSeparatorItem(int index)
        {
            if (index < 0 || index >= cmbSpecimenType.Items.Count)
                return false;
            string text = cmbSpecimenType.Items[index].ToString();
            return text.StartsWith("──");
        }

        // 콤보박스 인덱스 → specimenTypeMap 인덱스 매핑
        // separator 항목을 건너뛴 실제 데이터 인덱스 반환
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
        /// DMA 인장 타입을 초기 선택으로 설정 (DMA 커맨드에서 호출)
        /// </summary>
        public void SelectDMAType()
        {
            // DMA 인장 카테고리의 첫 번째 항목 (Rectangle) 선택
            for (int i = 0; i < cmbSpecimenType.Items.Count; i++)
            {
                string text = cmbSpecimenType.Items[i].ToString();
                if (text == "DMA Tensile - Rectangle")
                {
                    cmbSpecimenType.SelectedIndex = i;
                    break;
                }
            }
        }

        /// <summary>
        /// 기본 파라미터 로드
        /// </summary>
        private void LoadDefaultParameters()
        {
            ASTMSpecimenType selectedType = GetSelectedSpecimenType();
            Parameters = factory.GetDefaultParameters(selectedType);
            BindParametersToUI();
        }

        /// <summary>
        /// 파라미터를 UI에 바인딩
        /// </summary>
        private void BindParametersToUI()
        {
            numGaugeLength.Value = (decimal)Parameters.GaugeLength;
            numGaugeWidth.Value = (decimal)Parameters.GaugeWidth;
            numThickness.Value = (decimal)Parameters.Thickness;
            numGripWidth.Value = (decimal)Parameters.GripWidth;
            numTotalLength.Value = (decimal)Parameters.TotalLength;
            numFilletRadius.Value = Math.Max(numFilletRadius.Minimum, (decimal)Parameters.FilletRadius);
            numGripLength.Value = Math.Max(numGripLength.Minimum, (decimal)Parameters.GripLength);

            // 직사각형 시편은 필렛/그립폭 비활성화
            bool isRect = Parameters.IsRectangular;
            numFilletRadius.Enabled = !isRect;
            numGripWidth.Enabled = !isRect;

            // 카테고리별 패널 표시
            var st = Parameters.SpecimenType;
            bool isNotchType = st == ASTMSpecimenType.ASTM_E602_VNotch ||
                               st == ASTMSpecimenType.ASTM_E602_UNotch ||
                               st == ASTMSpecimenType.ASTM_E338 ||
                               st == ASTMSpecimenType.ASTM_E292 ||
                               st == ASTMSpecimenType.ASTM_D5379_Iosipescu ||
                               st == ASTMSpecimenType.ASTM_D7078_VNotchRailShear;
            bool isHoleType = st == ASTMSpecimenType.ASTM_D5766_OHT ||
                              st == ASTMSpecimenType.ASTM_D6484_OHC ||
                              st == ASTMSpecimenType.ASTM_D6742_FHT ||
                              st == ASTMSpecimenType.ASTM_D5961_Bearing ||
                              st == ASTMSpecimenType.IPC_TM650_PTHPull;
            bool isTabType = st == ASTMSpecimenType.ASTM_D5766_OHT ||
                             st == ASTMSpecimenType.ASTM_D6484_OHC ||
                             st == ASTMSpecimenType.ASTM_D6742_FHT ||
                             st == ASTMSpecimenType.ASTM_D3039 ||
                             st == ASTMSpecimenType.IPC_TM650_Tensile ||
                             st == ASTMSpecimenType.IPC_TM650_PTHPull;

            // 먼저 모든 옵션 패널 숨기기
            grpNotch.Visible = false;
            grpHole.Visible = false;
            grpTab.Visible = false;

            // 동적 Y 위치 계산 및 패널 배치 후 표시
            // DPI 스케일링 대응: 실제 스케일링된 Width/Height 사용
            int nextY = grpDimensions.Location.Y + grpDimensions.Height + 6;
            int panelX = grpDimensions.Location.X;
            int panelW = grpDimensions.Width;

            if (isNotchType)
            {
                grpNotch.Location = new System.Drawing.Point(panelX, nextY);
                grpNotch.Width = panelW;
                grpNotch.Visible = true;
                nextY += grpNotch.Height + 6;
            }
            if (isHoleType)
            {
                grpHole.Location = new System.Drawing.Point(panelX, nextY);
                grpHole.Width = panelW;
                grpHole.Visible = true;
                nextY += grpHole.Height + 6;
            }
            if (isTabType)
            {
                grpTab.Location = new System.Drawing.Point(panelX, nextY);
                grpTab.Width = panelW;
                grpTab.Visible = true;
                nextY += grpTab.Height + 6;
            }

            // 버튼 위치 조정 (DPI 스케일링 대응: 실제 버튼 크기 기준 상대 배치)
            int btnY = nextY + 10;
            int btnGap = 10;
            lblPreviewStatus.Location = new System.Drawing.Point(12, btnY);
            int btnRow = btnY + 18;
            btnCreate.Location = new System.Drawing.Point(250, btnRow);
            btnCancel.Location = new System.Drawing.Point(btnCreate.Right + btnGap, btnRow);

            // 폼 크기 조정 (ClientSize 대신 Size 직접 계산)
            int newClientHeight = btnRow + 46;
            int borderHeight = this.Height - this.ClientSize.Height;
            this.Height = newClientHeight + borderHeight;
            this.Invalidate(true);
            this.Update();

            // 노치 파라미터 바인딩
            if (isNotchType)
            {
                numNotchDepth.Value = (decimal)Parameters.NotchDepth;

                bool isVNotch = st == ASTMSpecimenType.ASTM_E602_VNotch ||
                                st == ASTMSpecimenType.ASTM_E338 ||
                                st == ASTMSpecimenType.ASTM_E292 ||
                                st == ASTMSpecimenType.ASTM_D5379_Iosipescu ||
                                st == ASTMSpecimenType.ASTM_D7078_VNotchRailShear;
                lblNotchAngle.Visible = isVNotch;
                numNotchAngle.Visible = isVNotch;
                lblNotchRadius.Visible = !isVNotch;
                numNotchRadius.Visible = !isVNotch;

                if (isVNotch)
                    numNotchAngle.Value = (decimal)Parameters.NotchAngle;
                else
                    numNotchRadius.Value = (decimal)Parameters.NotchRadius;

                chkDoubleNotch.Checked = Parameters.IsDoubleNotch;
            }

            // 구멍 파라미터 바인딩
            if (isHoleType)
            {
                numHoleDiameter.Value = Math.Max(numHoleDiameter.Minimum, (decimal)Parameters.HoleDiameter);
                chkEllipticalHole.Checked = Parameters.IsEllipticalHole;

                bool isEllip = Parameters.IsEllipticalHole;
                numHoleDiameter.Enabled = !isEllip;
                lblHoleMajorAxis.Visible = isEllip;
                numHoleMajorAxis.Visible = isEllip;
                lblHoleMinorAxis.Visible = isEllip;
                numHoleMinorAxis.Visible = isEllip;

                if (isEllip)
                {
                    numHoleMajorAxis.Value = (decimal)Parameters.HoleMajorAxis;
                    numHoleMinorAxis.Value = (decimal)Parameters.HoleMinorAxis;
                }
            }

            // 탭 파라미터 바인딩
            if (isTabType)
            {
                chkRectangular.Checked = Parameters.IsRectangular;
                numTabLength.Value = (decimal)Parameters.TabLength;
                numTabThickness.Value = (decimal)Parameters.TabThickness;
            }

            // 설명 업데이트
            lblDescription.Text = factory.GetSpecimenDescription(Parameters.SpecimenType);
        }

        /// <summary>
        /// UI에서 파라미터 읽기
        /// </summary>
        private void ReadParametersFromUI()
        {
            Parameters.SpecimenType = GetSelectedSpecimenType();
            Parameters.GaugeLength = (double)numGaugeLength.Value;
            Parameters.GaugeWidth = (double)numGaugeWidth.Value;
            Parameters.Thickness = (double)numThickness.Value;
            Parameters.GripWidth = (double)numGripWidth.Value;
            Parameters.TotalLength = (double)numTotalLength.Value;
            Parameters.FilletRadius = (double)numFilletRadius.Value;
            Parameters.GripLength = (double)numGripLength.Value;

            // 노치 파라미터 읽기
            if (grpNotch.Visible)
            {
                Parameters.NotchDepth = (double)numNotchDepth.Value;
                Parameters.NotchAngle = (double)numNotchAngle.Value;
                Parameters.NotchRadius = (double)numNotchRadius.Value;
                Parameters.IsDoubleNotch = chkDoubleNotch.Checked;
            }

            // 구멍 파라미터 읽기
            if (grpHole.Visible)
            {
                Parameters.IsEllipticalHole = chkEllipticalHole.Checked;
                if (Parameters.IsEllipticalHole)
                {
                    Parameters.HoleMajorAxis = (double)numHoleMajorAxis.Value;
                    Parameters.HoleMinorAxis = (double)numHoleMinorAxis.Value;
                }
                else
                {
                    Parameters.HoleDiameter = (double)numHoleDiameter.Value;
                }
            }

            // 탭 파라미터 읽기
            if (grpTab.Visible)
            {
                Parameters.IsRectangular = chkRectangular.Checked;
                Parameters.TabLength = (double)numTabLength.Value;
                Parameters.TabThickness = (double)numTabThickness.Value;
            }
        }

        /// <summary>
        /// 선택된 시편 타입 반환
        /// </summary>
        private ASTMSpecimenType GetSelectedSpecimenType()
        {
            int comboIndex = cmbSpecimenType.SelectedIndex;
            if (comboIndex < 0 || IsSeparatorItem(comboIndex))
                return ASTMSpecimenType.ASTM_E8_Standard;

            int dataIndex = GetDataIndex(comboIndex);
            if (dataIndex >= 0 && dataIndex < specimenTypeMap.Length)
                return specimenTypeMap[dataIndex];

            return ASTMSpecimenType.ASTM_E8_Standard;
        }

        /// <summary>
        /// 입력 검증
        /// </summary>
        private bool ValidateInputs()
        {
            ReadParametersFromUI();

            if (!Parameters.Validate(out string errorMessage))
            {
                ValidationHelper.ShowError(errorMessage, "입력 오류");
                return false;
            }

            return true;
        }

        // 이벤트 핸들러들
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

            // Custom이 아닌 표준 규격은 기본값 로드
            ASTMSpecimenType selectedType = GetSelectedSpecimenType();
            if (selectedType != ASTMSpecimenType.Custom)
            {
                LoadDefaultParameters();
            }
            else
            {
                BindParametersToUI();
            }
            SchedulePreview();
        }

        private void btnLoadDefaults_Click(object sender, EventArgs e)
        {
            LoadDefaultParameters();
            SchedulePreview();
        }

        private void chkEllipticalHole_CheckedChanged(object sender, EventArgs e)
        {
            bool isEllip = chkEllipticalHole.Checked;
            numHoleDiameter.Enabled = !isEllip;
            lblHoleMajorAxis.Visible = isEllip;
            numHoleMajorAxis.Visible = isEllip;
            lblHoleMinorAxis.Visible = isEllip;
            numHoleMinorAxis.Visible = isEllip;
        }

        private void SchedulePreview()
        {
            previewTimer.Stop();
            previewTimer.Start();
        }

        private void PreviewTimer_Tick(object sender, EventArgs e)
        {
            previewTimer.Stop();
            ExecuteAutoPreview();
        }

        private bool ValidateInputsSilent()
        {
            ReadParametersFromUI();
            return Parameters.Validate(out string _);
        }

        private void ExecuteAutoPreview()
        {
            if (!ValidateInputsSilent())
            {
                CleanupPreview();
                lblPreviewStatus.Text = "";
                return;
            }
            try
            {
                CleanupPreview();
                WriteBlock.ExecuteTask("ASTM Preview", () =>
                {
                    var existingBodies = new HashSet<DesignBody>(activePart.Bodies);
                    previewBody = modelingService.CreateTensileSpecimen(activePart, Parameters);
                    previewFixtures.Clear();
                    foreach (var body in activePart.Bodies)
                    {
                        if (!existingBodies.Contains(body) && body != previewBody)
                            previewFixtures.Add(body);
                    }
                });
                Window.ActiveWindow?.ZoomExtents();
                lblPreviewStatus.ForeColor = Color.Green;
                lblPreviewStatus.Text = "미리보기 적용됨";
            }
            catch (Exception ex)
            {
                lblPreviewStatus.ForeColor = Color.Red;
                lblPreviewStatus.Text = ex.Message.Split('\n')[0];
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
                    // 미리보기가 없으면 새로 생성
                    WriteBlock.ExecuteTask("Create ASTM Specimen", () =>
                    {
                        modelingService.CreateTensileSpecimen(activePart, Parameters);
                    });
                }
                else
                {
                    // 미리보기가 있으면 그것을 최종 시편으로 전환
                    previewBody = null; // 참조 해제 (삭제 방지)
                    previewFixtures.Clear(); // 그립 장비도 참조 해제
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
        /// 미리보기 Body 정리 (시편 + 그립 장비)
        /// </summary>
        private void CleanupPreview()
        {
            if (previewBody != null || previewFixtures.Count > 0)
            {
                try
                {
                    WriteBlock.ExecuteTask("Cleanup Preview", () =>
                    {
                        // 시편 삭제
                        if (previewBody != null)
                        {
                            previewBody.Delete();
                        }

                        // 그립 장비 삭제
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
        private void TensileSpecimenDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            previewTimer.Stop();
            previewTimer.Dispose();
            if (DialogResult != DialogResult.OK)
            {
                CleanupPreview();
            }
        }
    }
}
