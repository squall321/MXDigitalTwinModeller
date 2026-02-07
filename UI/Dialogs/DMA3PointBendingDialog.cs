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
    /// DMA 3점 굽힘시험 시편 생성 대화창 (Modeless with Preview)
    /// </summary>
    public partial class DMA3PointBendingDialog : Form
    {
        private readonly DMASpecimenFactory factory;
        private readonly DMA3PointBendingService modelingService;
        private Part activePart;
        private DesignBody previewBody;
        private List<DesignBody> previewFixtures; // 부자재 추적

        public DMA3PointBendingParameters Parameters { get; private set; }

        public DMA3PointBendingDialog(Part part)
        {
            InitializeComponent();
            factory = new DMASpecimenFactory();
            modelingService = new DMA3PointBendingService();
            activePart = part;
            Parameters = new DMA3PointBendingParameters();
            previewFixtures = new List<DesignBody>();

            InitializeSpecimenTypes();
            LoadDefaultParameters();

            // 대화창이 항상 맨 앞에 표시
            this.TopMost = true;

            // 대화창이 닫힐 때 미리보기 정리
            this.FormClosing += DMA3PointBendingDialog_FormClosing;

            // 모든 파라미터 변경 시 경고 업데이트
            numLength.ValueChanged += (s, e) => UpdateWarnings();
            numWidth.ValueChanged += (s, e) => UpdateWarnings();
            numThickness.ValueChanged += (s, e) => UpdateWarnings();
            numSpan.ValueChanged += (s, e) => UpdateWarnings();
            numSupportDiameter.ValueChanged += (s, e) => UpdateWarnings();
            numLoadingNoseDiameter.ValueChanged += (s, e) => UpdateWarnings();
            numSupportHeight.ValueChanged += (s, e) => UpdateWarnings();
            numLoadingNoseHeight.ValueChanged += (s, e) => UpdateWarnings();
        }

        /// <summary>
        /// 시편 타입 콤보박스 초기화
        /// </summary>
        private void InitializeSpecimenTypes()
        {
            // 이벤트 핸들러 일시 제거
            cmbSpecimenType.SelectedIndexChanged -= cmbSpecimenType_SelectedIndexChanged;

            cmbSpecimenType.Items.Clear();
            cmbSpecimenType.Items.Add("Standard");
            cmbSpecimenType.Items.Add("Custom");
            cmbSpecimenType.SelectedIndex = 0;

            // 이벤트 핸들러 다시 연결
            cmbSpecimenType.SelectedIndexChanged += cmbSpecimenType_SelectedIndexChanged;
        }

        /// <summary>
        /// 기본 파라미터 로드
        /// </summary>
        private void LoadDefaultParameters()
        {
            DMASpecimenType selectedType = GetSelectedSpecimenType();
            Parameters = new DMA3PointBendingParameters();
            Parameters.SpecimenType = selectedType;
            BindParametersToUI();
        }

        /// <summary>
        /// 파라미터를 UI에 바인딩
        /// </summary>
        private void BindParametersToUI()
        {
            numLength.Value = (decimal)Parameters.Length;
            numWidth.Value = (decimal)Parameters.Width;
            numThickness.Value = (decimal)Parameters.Thickness;
            numSpan.Value = (decimal)Parameters.Span;
            numSupportDiameter.Value = (decimal)Parameters.SupportDiameter;
            numLoadingNoseDiameter.Value = (decimal)Parameters.LoadingNoseDiameter;
            numSupportHeight.Value = (decimal)Parameters.SupportHeight;
            numLoadingNoseHeight.Value = (decimal)Parameters.LoadingNoseHeight;

            // 설명 업데이트 (Span/Thickness 비율 표시)
            UpdateDescription();

            // 경고 업데이트
            UpdateWarnings();
        }

        /// <summary>
        /// 설명 업데이트 (Span/Thickness 비율 표시)
        /// </summary>
        private void UpdateDescription()
        {
            double ratio = (double)numSpan.Value / (double)numThickness.Value;
            lblDescription.Text = factory.GetSpecimenTypeDescription(Parameters.SpecimenType) +
                $" (Span/Thickness = {ratio:F1}:1, 권장: 16:1 ~ 32:1)";
        }

        /// <summary>
        /// 경고 메시지 업데이트 (범위 이탈 시 빨간색 경고 표시)
        /// </summary>
        private void UpdateWarnings()
        {
            ReadParametersFromUI();
            string warnings = Parameters.GetRangeWarnings();

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
        }

        /// <summary>
        /// UI에서 파라미터 읽기
        /// </summary>
        private void ReadParametersFromUI()
        {
            Parameters.SpecimenType = GetSelectedSpecimenType();
            Parameters.Length = (double)numLength.Value;
            Parameters.Width = (double)numWidth.Value;
            Parameters.Thickness = (double)numThickness.Value;
            Parameters.Span = (double)numSpan.Value;
            Parameters.SupportDiameter = (double)numSupportDiameter.Value;
            Parameters.LoadingNoseDiameter = (double)numLoadingNoseDiameter.Value;
            Parameters.SupportHeight = (double)numSupportHeight.Value;
            Parameters.LoadingNoseHeight = (double)numLoadingNoseHeight.Value;
        }

        /// <summary>
        /// 선택된 시편 타입 반환
        /// </summary>
        private DMASpecimenType GetSelectedSpecimenType()
        {
            switch (cmbSpecimenType.SelectedIndex)
            {
                case 0: return DMASpecimenType.Standard;
                case 1: return DMASpecimenType.Custom;
                default: return DMASpecimenType.Standard;
            }
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
            if (cmbSpecimenType.SelectedIndex == 0)
            {
                // 표준 규격 선택 시 기본값 로드
                LoadDefaultParameters();
            }
        }

        private void numSpan_ValueChanged(object sender, EventArgs e)
        {
            UpdateDescription();
        }

        private void numThickness_ValueChanged(object sender, EventArgs e)
        {
            UpdateDescription();
        }

        private void btnLoadDefaults_Click(object sender, EventArgs e)
        {
            LoadDefaultParameters();
        }

        private void btnPreview_Click(object sender, EventArgs e)
        {
            if (!ValidateInputs())
                return;

            try
            {
                // 기존 미리보기 삭제
                CleanupPreview();

                // 새로운 미리보기 생성 (WriteBlock 내에서 실행)
                WriteBlock.ExecuteTask("DMA Preview", () =>
                {
                    // 생성 전 Body 개수 저장
                    var existingBodies = new HashSet<DesignBody>(activePart.Bodies);

                    // 시편 + 부자재 생성
                    previewBody = modelingService.Create3PointBendingSpecimen(activePart, Parameters);

                    // 새로 생성된 Body들 추적 (시편 제외)
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
                    // 미리보기가 없으면 새로 생성
                    WriteBlock.ExecuteTask("Create DMA Specimen", () =>
                    {
                        modelingService.Create3PointBendingSpecimen(activePart, Parameters);
                    });
                }
                else
                {
                    // 미리보기가 있으면 그것을 최종 시편으로 전환
                    previewBody = null; // 참조 해제 (삭제 방지)
                    previewFixtures.Clear(); // 부자재도 참조 해제
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
        /// 미리보기 Body 정리 (시편 + 부자재)
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

                        // 부자재 삭제
                        foreach (var fixture in previewFixtures)
                        {
                            fixture.Delete();
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
        private void DMA3PointBendingDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (DialogResult != DialogResult.OK)
            {
                // OK가 아닌 경우(취소 등)만 미리보기 삭제
                CleanupPreview();
            }
        }
    }
}
