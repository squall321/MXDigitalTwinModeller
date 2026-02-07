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
    /// DMA 인장시험 시편 생성 대화창 (Modeless with Preview)
    /// </summary>
    public partial class DMATensileSpecimenDialog : Form
    {
        private readonly DMASpecimenFactory factory;
        private readonly DMATensileService modelingService;
        private Part activePart;
        private DesignBody previewBody;
        private List<DesignBody> previewFixtures; // 부자재 추적

        public DMATensileParameters Parameters { get; private set; }

        public DMATensileSpecimenDialog(Part part)
        {
            InitializeComponent();
            factory = new DMASpecimenFactory();
            modelingService = new DMATensileService();
            activePart = part;
            Parameters = new DMATensileParameters();
            previewFixtures = new List<DesignBody>();

            InitializeSpecimenTypes();
            InitializeShapes();
            LoadDefaultParameters();

            // 대화창이 항상 맨 앞에 표시
            this.TopMost = true;

            // 대화창이 닫힐 때 미리보기 정리
            this.FormClosing += DMATensileSpecimenDialog_FormClosing;
        }

        private void DMATensileSpecimenDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 미리보기 Body 삭제
            CleanupPreview();
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
        /// 형상 콤보박스 초기화
        /// </summary>
        private void InitializeShapes()
        {
            // 이벤트 핸들러 일시 제거
            cmbShape.SelectedIndexChanged -= cmbShape_SelectedIndexChanged;

            cmbShape.Items.Clear();
            cmbShape.Items.Add("Rectangle");
            cmbShape.Items.Add("DogBone");
            cmbShape.SelectedIndex = 0;

            // 이벤트 핸들러 다시 연결
            cmbShape.SelectedIndexChanged += cmbShape_SelectedIndexChanged;
        }

        /// <summary>
        /// 기본 파라미터 로드
        /// </summary>
        private void LoadDefaultParameters()
        {
            DMASpecimenType selectedType = GetSelectedSpecimenType();
            Parameters = new DMATensileParameters();
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
            numGaugeLength.Value = (decimal)Parameters.GaugeLength;
            numGripLength.Value = (decimal)Parameters.GripLength;
            cmbShape.SelectedIndex = Parameters.Shape == DMASpecimenShape.Rectangle ? 0 : 1;
            numGripWidth.Value = (decimal)Parameters.GripWidth;
            numGripHeight.Value = (decimal)Parameters.GripHeight;
            numFilletRadius.Value = (decimal)Parameters.FilletRadius;

            // 설명 업데이트
            lblDescription.Text = factory.GetSpecimenTypeDescription(Parameters.SpecimenType);

            // DogBone 전용 컨트롤 활성화/비활성화
            UpdateDogBoneControls();
        }

        /// <summary>
        /// DogBone 전용 컨트롤 활성화/비활성화
        /// </summary>
        private void UpdateDogBoneControls()
        {
            bool isDogBone = cmbShape.SelectedIndex == 1;
            numGripWidth.Enabled = isDogBone;
            numGripHeight.Enabled = isDogBone;
            numFilletRadius.Enabled = isDogBone;
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
            Parameters.GaugeLength = (double)numGaugeLength.Value;
            Parameters.GripLength = (double)numGripLength.Value;
            Parameters.Shape = cmbShape.SelectedIndex == 0 ? DMASpecimenShape.Rectangle : DMASpecimenShape.DogBone;
            Parameters.GripWidth = (double)numGripWidth.Value;
            Parameters.GripHeight = (double)numGripHeight.Value;
            Parameters.FilletRadius = (double)numFilletRadius.Value;
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

        private void cmbShape_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateDogBoneControls();
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
                    previewBody = modelingService.CreateDMATensileSpecimen(activePart, Parameters);

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
                        modelingService.CreateDMATensileSpecimen(activePart, Parameters);
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
    }
}
