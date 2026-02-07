using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.BendingFixture;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.BendingFixture;

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
    /// 기존 바디에 3점 벤딩 지지구조를 적용하는 다이얼로그
    /// </summary>
    public partial class ApplyBendingFixtureDialog : Form
    {
        private readonly BendingFixtureService service;
        private Part activePart;
        private DesignBody preSelectedBody;
        private List<DesignBody> previewFixtures;

        private BendingFixtureParameters parameters;
        private AxisAlignedBoundingBox currentBbox;

        // 바디 목록 캐시 (콤보 인덱스 매핑)
        private List<DesignBody> bodyList;

        // 방향 콤보 변경 중 재진입 방지
        private bool updatingDirectionCombos;

        public ApplyBendingFixtureDialog(Part part, DesignBody preSelected)
        {
            InitializeComponent();
            service = new BendingFixtureService();
            activePart = part;
            preSelectedBody = preSelected;
            parameters = new BendingFixtureParameters();
            previewFixtures = new List<DesignBody>();
            bodyList = new List<DesignBody>();

            PopulateBodyCombo();
            SetupEventHandlers();

            this.TopMost = true;
            this.FormClosing += ApplyBendingFixtureDialog_FormClosing;
        }

        // =============================================
        //  초기화
        // =============================================

        private void SetupEventHandlers()
        {
            cmbBody.SelectedIndexChanged += cmbBody_SelectedIndexChanged;
            cmbSpanDir.SelectedIndexChanged += cmbDirection_SelectedIndexChanged;
            cmbWidthDir.SelectedIndexChanged += cmbDirection_SelectedIndexChanged;
            cmbLoadDir.SelectedIndexChanged += cmbDirection_SelectedIndexChanged;
            numSpanRatio.ValueChanged += numSpanRatio_ValueChanged;
            numSpanAbsolute.ValueChanged += numSpanAbsolute_ValueChanged;
        }

        /// <summary>
        /// Part.Bodies에서 바디 목록을 콤보박스에 채움
        /// </summary>
        private void PopulateBodyCombo()
        {
            cmbBody.Items.Clear();
            bodyList.Clear();

            int preSelectedIndex = -1;

            foreach (DesignBody body in activePart.Bodies)
            {
                string name = string.IsNullOrEmpty(body.Name) ? body.ToString() : body.Name;
                cmbBody.Items.Add(name);
                bodyList.Add(body);

                if (preSelectedBody != null && body == preSelectedBody)
                    preSelectedIndex = bodyList.Count - 1;
            }

            if (bodyList.Count == 0)
            {
                lblBboxInfo.Text = "Part에 바디가 없습니다.";
                return;
            }

            // 사전 선택된 바디가 있으면 선택, 없으면 첫 번째 선택
            cmbBody.SelectedIndex = preSelectedIndex >= 0 ? preSelectedIndex : 0;
        }

        // =============================================
        //  바디 선택 → bbox 계산 → 방향 감지
        // =============================================

        private void cmbBody_SelectedIndexChanged(object sender, EventArgs e)
        {
            int idx = cmbBody.SelectedIndex;
            if (idx < 0 || idx >= bodyList.Count) return;

            CleanupPreview();

            DesignBody selectedBody = bodyList[idx];
            try
            {
                currentBbox = service.ComputeBoundingBox(selectedBody);
                service.DetectDirections(currentBbox, parameters);
                UpdateDirectionCombos();
                UpdateDimensionLabels();
                UpdateSpanDisplay();

                lblBboxInfo.Text = string.Format(
                    "바운딩 박스: {0:F1} x {1:F1} x {2:F1} mm",
                    GeometryUtils.MetersToMm(currentBbox.ExtentX),
                    GeometryUtils.MetersToMm(currentBbox.ExtentY),
                    GeometryUtils.MetersToMm(currentBbox.ExtentZ));
            }
            catch (Exception ex)
            {
                lblBboxInfo.Text = "바운딩 박스 계산 실패";
                System.Diagnostics.Debug.WriteLine($"BBox error: {ex.Message}");
            }
        }

        // =============================================
        //  방향 콤보박스 관리
        // =============================================

        private void UpdateDirectionCombos()
        {
            updatingDirectionCombos = true;
            cmbSpanDir.SelectedIndex = (int)parameters.SpanDirection;
            cmbWidthDir.SelectedIndex = (int)parameters.WidthDirection;
            cmbLoadDir.SelectedIndex = (int)parameters.LoadingDirection;
            updatingDirectionCombos = false;
        }

        private void cmbDirection_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (updatingDirectionCombos) return;
            if (currentBbox == null) return;

            var cmb = (ComboBox)sender;
            AxisDirection newAxis = (AxisDirection)cmb.SelectedIndex;

            AxisDirection oldSpan = parameters.SpanDirection;
            AxisDirection oldWidth = parameters.WidthDirection;
            AxisDirection oldLoad = parameters.LoadingDirection;

            // 변경된 콤보에 맞춰 swap 처리
            if (cmb == cmbSpanDir)
            {
                if (newAxis == oldWidth)
                    parameters.WidthDirection = oldSpan;
                else if (newAxis == oldLoad)
                    parameters.LoadingDirection = oldSpan;
                parameters.SpanDirection = newAxis;
            }
            else if (cmb == cmbWidthDir)
            {
                if (newAxis == oldSpan)
                    parameters.SpanDirection = oldWidth;
                else if (newAxis == oldLoad)
                    parameters.LoadingDirection = oldWidth;
                parameters.WidthDirection = newAxis;
            }
            else if (cmb == cmbLoadDir)
            {
                if (newAxis == oldSpan)
                    parameters.SpanDirection = oldLoad;
                else if (newAxis == oldWidth)
                    parameters.WidthDirection = oldLoad;
                parameters.LoadingDirection = newAxis;
            }

            service.UpdateBodyDimensions(currentBbox, parameters);
            UpdateDirectionCombos();
            UpdateDimensionLabels();
            UpdateSpanDisplay();
        }

        private void btnAutoDetect_Click(object sender, EventArgs e)
        {
            if (currentBbox == null) return;
            service.DetectDirections(currentBbox, parameters);
            UpdateDirectionCombos();
            UpdateDimensionLabels();
            UpdateSpanDisplay();
        }

        private void UpdateDimensionLabels()
        {
            lblSpanDim.Text = string.Format("({0:F1} mm)", parameters.BodyLengthMm);
            lblWidthDim.Text = string.Format("({0:F1} mm)", parameters.BodyWidthMm);
            lblLoadDim.Text = string.Format("({0:F1} mm)", parameters.BodyThicknessMm);
        }

        // =============================================
        //  스팬 설정
        // =============================================

        private void radSpanMode_CheckedChanged(object sender, EventArgs e)
        {
            bool useRatio = radSpanRatio.Checked;
            numSpanRatio.Enabled = useRatio;
            numSpanAbsolute.Enabled = !useRatio;
            parameters.UseSpanRatio = useRatio;
            UpdateSpanDisplay();
        }

        private void numSpanRatio_ValueChanged(object sender, EventArgs e)
        {
            parameters.SpanRatio = (double)numSpanRatio.Value / 100.0;
            UpdateSpanDisplay();
        }

        private void numSpanAbsolute_ValueChanged(object sender, EventArgs e)
        {
            parameters.SpanMm = (double)numSpanAbsolute.Value;
            UpdateSpanDisplay();
        }

        private void UpdateSpanDisplay()
        {
            service.UpdateComputedSpan(parameters);
            if (radSpanRatio.Checked)
            {
                lblSpanRatioResult.Text = string.Format("= {0:F1} mm", parameters.ComputedSpanMm);
            }
            else
            {
                lblSpanRatioResult.Text = "";
            }
        }

        // =============================================
        //  파라미터 읽기 / 검증
        // =============================================

        private void ReadParametersFromUI()
        {
            parameters.UseSpanRatio = radSpanRatio.Checked;
            parameters.SpanRatio = (double)numSpanRatio.Value / 100.0;
            parameters.SpanMm = (double)numSpanAbsolute.Value;
            parameters.SupportDiameter = (double)numSupportDia.Value;
            parameters.LoadingNoseDiameter = (double)numNoseDia.Value;
            parameters.SupportHeight = (double)numSupportHeight.Value;
            parameters.LoadingNoseHeight = (double)numNoseHeight.Value;
            service.UpdateComputedSpan(parameters);
        }

        private bool ValidateInputs()
        {
            if (cmbBody.SelectedIndex < 0 || currentBbox == null)
            {
                ValidationHelper.ShowError("바디를 선택하세요.", "입력 오류");
                return false;
            }

            ReadParametersFromUI();
            string errorMessage;
            if (!parameters.Validate(out errorMessage))
            {
                ValidationHelper.ShowError(errorMessage, "입력 오류");
                return false;
            }

            return true;
        }

        // =============================================
        //  미리보기 / 생성 / 취소
        // =============================================

        private void btnPreview_Click(object sender, EventArgs e)
        {
            if (!ValidateInputs()) return;

            try
            {
                CleanupPreview();
                DesignBody targetBody = bodyList[cmbBody.SelectedIndex];

                WriteBlock.ExecuteTask("Bending Fixture Preview", () =>
                {
                    var fixtures = service.CreateFixtures(activePart, targetBody, parameters);
                    previewFixtures.AddRange(fixtures);
                });
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    $"미리보기 생성 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "미리보기 오류");
            }
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            if (!ValidateInputs()) return;

            try
            {
                if (previewFixtures.Count > 0)
                {
                    // 미리보기가 있으면 그대로 유지 (확정)
                    previewFixtures.Clear();
                }
                else
                {
                    // 미리보기 없으면 새로 생성
                    DesignBody targetBody = bodyList[cmbBody.SelectedIndex];
                    WriteBlock.ExecuteTask("Create Bending Fixture", () =>
                    {
                        service.CreateFixtures(activePart, targetBody, parameters);
                    });
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    $"벤딩 지그 생성 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "생성 오류");
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        // =============================================
        //  미리보기 정리
        // =============================================

        private void CleanupPreview()
        {
            if (previewFixtures.Count > 0)
            {
                try
                {
                    WriteBlock.ExecuteTask("Cleanup Fixture Preview", () =>
                    {
                        foreach (var fixture in previewFixtures)
                        {
                            if (fixture != null)
                                fixture.Delete();
                        }
                    });
                    previewFixtures.Clear();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Fixture preview cleanup error: {ex.Message}");
                }
            }
        }

        private void ApplyBendingFixtureDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (DialogResult != DialogResult.OK)
            {
                CleanupPreview();
            }
        }
    }
}
