using System;
using System.Collections.Generic;
using System.Drawing;
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
    /// 수치 변경 시 300ms 디바운스 후 자동 미리보기 갱신
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

        // 자동 미리보기 디바운스 타이머
        private Timer previewTimer;

        public ApplyBendingFixtureDialog(Part part, DesignBody preSelected)
        {
            InitializeComponent();
            service = new BendingFixtureService();
            activePart = part;
            preSelectedBody = preSelected;
            parameters = new BendingFixtureParameters();
            previewFixtures = new List<DesignBody>();
            bodyList = new List<DesignBody>();

            // 디바운스 타이머 (300ms)
            previewTimer = new Timer();
            previewTimer.Interval = 300;
            previewTimer.Tick += PreviewTimer_Tick;

            PopulateBodyCombo();
            SetupEventHandlers();

            // 초기 선택 트리거 (PopulateBodyCombo가 핸들러 등록 전에 SelectedIndex 설정하므로)
            cmbBody_SelectedIndexChanged(cmbBody, EventArgs.Empty);

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

            // 지지구조 치수 변경 → 자동 미리보기
            numSupportDia.ValueChanged += FixtureParam_ValueChanged;
            numNoseDia.ValueChanged += FixtureParam_ValueChanged;
            numSupportHeight.ValueChanged += FixtureParam_ValueChanged;
            numNoseHeight.ValueChanged += FixtureParam_ValueChanged;
        }

        /// <summary>
        /// 모든 바디(Component 포함)를 재귀 수집하여 콤보박스에 채움
        /// 인덱스 0 = "All Bodies (전체)" → 전체 병합 바운딩박스 사용
        /// Component.Content 접근에 WriteBlock 트랜잭션 컨텍스트 필요
        /// </summary>
        private void PopulateBodyCombo()
        {
            cmbBody.Items.Clear();
            bodyList.Clear();

            // Component.Content는 WriteBlock 내에서만 안정적으로 접근 가능
            try
            {
                WriteBlock.ExecuteTask("Collect Bodies", () =>
                {
                    CollectBodiesRecursive(activePart, bodyList);
                });
            }
            catch
            {
                bodyList.Clear();
                try { CollectBodiesRecursive(activePart, bodyList); }
                catch { }
            }

            if (bodyList.Count == 0)
            {
                lblBboxInfo.Text = "Part에 바디가 없습니다.";
                return;
            }

            // 첫 항목: 전체 바디
            cmbBody.Items.Add("All Bodies (전체) [" + bodyList.Count + "개]");

            // 개별 바디 항목
            int preSelectedIndex = -1;
            for (int i = 0; i < bodyList.Count; i++)
            {
                var body = bodyList[i];
                string name = string.IsNullOrEmpty(body.Name) ? body.ToString() : body.Name;
                var bodyPart = body.Parent as Part;
                if (bodyPart != null && !ReferenceEquals(bodyPart, activePart))
                    name = "[C] " + name;
                cmbBody.Items.Add(name);

                if (preSelectedBody != null && body == preSelectedBody)
                    preSelectedIndex = i + 1;
            }

            cmbBody.SelectedIndex = preSelectedIndex >= 0 ? preSelectedIndex : 0;
        }

        /// <summary>
        /// IPart에서 재귀적으로 모든 DesignBody 수집 (nested Component 포함)
        /// </summary>
        private void CollectBodiesRecursive(IPart part, List<DesignBody> result)
        {
            if (part == null) return;

            foreach (var body in part.Bodies)
            {
                var db = body as DesignBody;
                if (db != null) result.Add(db);
            }

            foreach (var comp in part.Components)
            {
                try
                {
                    if (comp.Content != null)
                        CollectBodiesRecursive(comp.Content, result);
                }
                catch { }
            }
        }

        // =============================================
        //  바디 선택 → bbox 계산 → 방향 감지
        // =============================================

        private void cmbBody_SelectedIndexChanged(object sender, EventArgs e)
        {
            int idx = cmbBody.SelectedIndex;
            if (idx < 0) return;

            CleanupPreview();

            try
            {
                AxisAlignedBoundingBox bbox = null;
                var localBodyList = bodyList;
                WriteBlock.ExecuteTask("Compute BBox", () =>
                {
                    if (idx == 0)
                        bbox = service.ComputeBoundingBox(localBodyList);
                    else
                        bbox = service.ComputeBoundingBox(localBodyList[idx - 1]);
                });
                currentBbox = bbox;

                service.DetectDirections(currentBbox, parameters);
                UpdateDirectionCombos();
                UpdateDimensionLabels();
                UpdateSpanDisplay();

                lblBboxInfo.Text = string.Format(
                    "바운딩 박스: {0:F1} x {1:F1} x {2:F1} mm",
                    GeometryUtils.MetersToMm(currentBbox.ExtentX),
                    GeometryUtils.MetersToMm(currentBbox.ExtentY),
                    GeometryUtils.MetersToMm(currentBbox.ExtentZ));

                // 바디 변경 시 자동 미리보기
                SchedulePreview();
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

            // 방향 변경 시 자동 미리보기
            SchedulePreview();
        }

        private void btnAutoDetect_Click(object sender, EventArgs e)
        {
            if (currentBbox == null) return;
            service.DetectDirections(currentBbox, parameters);
            UpdateDirectionCombos();
            UpdateDimensionLabels();
            UpdateSpanDisplay();
            SchedulePreview();
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
            SchedulePreview();
        }

        private void numSpanRatio_ValueChanged(object sender, EventArgs e)
        {
            parameters.SpanRatio = (double)numSpanRatio.Value / 100.0;
            UpdateSpanDisplay();
            SchedulePreview();
        }

        private void numSpanAbsolute_ValueChanged(object sender, EventArgs e)
        {
            parameters.SpanMm = (double)numSpanAbsolute.Value;
            UpdateSpanDisplay();
            SchedulePreview();
        }

        /// <summary>
        /// 지지구조 치수(직경, 높이) 변경 → 자동 미리보기
        /// </summary>
        private void FixtureParam_ValueChanged(object sender, EventArgs e)
        {
            SchedulePreview();
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
            if (currentBbox == null)
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

        /// <summary>
        /// 팝업 없이 파라미터 검증 (자동 미리보기용)
        /// </summary>
        private bool ValidateInputsSilent()
        {
            if (currentBbox == null) return false;

            ReadParametersFromUI();
            string errorMessage;
            return parameters.Validate(out errorMessage);
        }

        // =============================================
        //  선택된 바디 목록 반환
        // =============================================

        private List<DesignBody> GetSelectedBodies()
        {
            int idx = cmbBody.SelectedIndex;
            if (idx <= 0)
                return new List<DesignBody>(bodyList);
            else
                return new List<DesignBody> { bodyList[idx - 1] };
        }

        // =============================================
        //  자동 미리보기 (디바운스)
        // =============================================

        /// <summary>
        /// 디바운스 타이머 (재)시작. 300ms 동안 추가 변경이 없으면 미리보기 실행.
        /// </summary>
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

        /// <summary>
        /// 자동 미리보기 실행. 충돌/에러 시 라벨에 경고 표시 (팝업 없음).
        /// </summary>
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
                var bbox = currentBbox;
                var targetBodies = GetSelectedBodies();

                WriteBlock.ExecuteTask("Bending Fixture Preview", () =>
                {
                    var fixtures = service.CreateFixtures(activePart, bbox, parameters, targetBodies);
                    previewFixtures.AddRange(fixtures);
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

        // =============================================
        //  생성 / 취소
        // =============================================

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
                    var bbox = currentBbox;
                    var targetBodies = GetSelectedBodies();
                    WriteBlock.ExecuteTask("Create Bending Fixture", () =>
                    {
                        service.CreateFixtures(activePart, bbox, parameters, targetBodies);
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
            previewTimer.Stop();
            previewTimer.Dispose();

            if (DialogResult != DialogResult.OK)
            {
                CleanupPreview();
            }
        }
    }
}
