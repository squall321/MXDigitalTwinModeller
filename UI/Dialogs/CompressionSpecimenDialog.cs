using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Compression;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Compression;

#if V251
using SpaceClaim.Api.V251.Extensibility;
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Extensibility;
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    /// <summary>
    /// 압축 시편 생성 다이얼로그
    /// 프리셋 선택 → 치수 표시/편집 → 미리보기/생성
    /// </summary>
    public partial class CompressionSpecimenDialog : Form
    {
        private readonly CompressionSpecimenService service;
        private Part activePart;
        private List<DesignBody> previewBodies;
        private bool suppressPresetEvent = false;

        /// <summary>
        /// 프리셋 목록 (콤보박스 순서와 일치)
        /// </summary>
        private readonly CompressionSpecimenType[] presetTypes = new[]
        {
            CompressionSpecimenType.ASTM_D695_Prism,
            CompressionSpecimenType.ASTM_D695_Cylinder,
            CompressionSpecimenType.ISO_604_Modulus,
            CompressionSpecimenType.ISO_604_Strength,
            CompressionSpecimenType.ASTM_E9_Short,
            CompressionSpecimenType.ASTM_E9_Medium,
            CompressionSpecimenType.Custom
        };

        private readonly string[] presetLabels = new[]
        {
            "ASTM D695 - 직육면체 (12.7×12.7×25.4mm)",
            "ASTM D695 - 원기둥 (∅12.7×25.4mm)",
            "ISO 604 - 탄성계수 (50×10×4mm)",
            "ISO 604 - 강도 (10×10×4mm)",
            "ASTM E9 - Short L/D=2 (∅12.7×25.4mm)",
            "ASTM E9 - Medium L/D=3 (∅12.7×38.1mm)",
            "사용자 정의 (Custom)"
        };

        public CompressionSpecimenDialog(Part part)
        {
            InitializeComponent();
            service = new CompressionSpecimenService();
            activePart = part;
            previewBodies = new List<DesignBody>();

            // 프리셋 콤보 초기화
            cmbPreset.Items.AddRange(presetLabels);
            cmbPreset.SelectedIndex = 0;
            cmbPreset.SelectedIndexChanged += cmbPreset_SelectedIndexChanged;

            this.TopMost = true;
            this.FormClosing += CompressionSpecimenDialog_FormClosing;
        }

        // =============================================
        //  프리셋 변경
        // =============================================

        private void cmbPreset_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suppressPresetEvent) return;

            int idx = cmbPreset.SelectedIndex;
            if (idx < 0 || idx >= presetTypes.Length) return;

            var p = CompressionSpecimenParameters.FromPreset(presetTypes[idx]);
            ApplyParamsToUI(p);
        }

        /// <summary>
        /// 파라미터를 UI에 반영
        /// </summary>
        private void ApplyParamsToUI(CompressionSpecimenParameters p)
        {
            suppressPresetEvent = true;

            if (p.Shape == CompressionSpecimenShape.Cylinder)
                rdoCylinder.Checked = true;
            else
                rdoPrism.Checked = true;

            numWidth.Value = (decimal)p.WidthMm;
            numDepth.Value = (decimal)p.DepthMm;
            numHeight.Value = (decimal)p.HeightMm;
            numDiameter.Value = (decimal)p.DiameterMm;

            UpdateShapeVisibility();
            suppressPresetEvent = false;
        }

        // =============================================
        //  형상 전환 (Prism ↔ Cylinder)
        // =============================================

        private void rdoShape_CheckedChanged(object sender, EventArgs e)
        {
            UpdateShapeVisibility();
        }

        private void UpdateShapeVisibility()
        {
            bool isPrism = rdoPrism.Checked;

            // Prism 전용 컨트롤
            lblWidth.Visible = isPrism;
            numWidth.Visible = isPrism;
            lblDepth.Visible = isPrism;
            numDepth.Visible = isPrism;

            // Cylinder 전용 컨트롤
            lblDiameter.Visible = !isPrism;
            numDiameter.Visible = !isPrism;
        }

        // =============================================
        //  플래튼 체크박스
        // =============================================

        private void chkCreatePlatens_CheckedChanged(object sender, EventArgs e)
        {
            bool enabled = chkCreatePlatens.Checked;
            numPlatenDia.Enabled = enabled;
            numPlatenHeight.Enabled = enabled;
        }

        // =============================================
        //  파라미터 읽기 / 검증
        // =============================================

        private CompressionSpecimenParameters ReadParams()
        {
            int idx = cmbPreset.SelectedIndex;
            var type = (idx >= 0 && idx < presetTypes.Length)
                ? presetTypes[idx]
                : CompressionSpecimenType.Custom;

            var p = new CompressionSpecimenParameters();
            p.SpecimenType = type;
            p.Shape = rdoPrism.Checked
                ? CompressionSpecimenShape.Prism
                : CompressionSpecimenShape.Cylinder;
            p.WidthMm = (double)numWidth.Value;
            p.DepthMm = (double)numDepth.Value;
            p.HeightMm = (double)numHeight.Value;
            p.DiameterMm = (double)numDiameter.Value;
            p.CreatePlatens = chkCreatePlatens.Checked;
            p.PlatenDiameterMm = (double)numPlatenDia.Value;
            p.PlatenHeightMm = (double)numPlatenHeight.Value;

            return p;
        }

        // =============================================
        //  미리보기 / 생성 / 취소
        // =============================================

        private void btnPreview_Click(object sender, EventArgs e)
        {
            var p = ReadParams();
            string error;
            if (!p.Validate(out error))
            {
                ValidationHelper.ShowError(error, "입력 오류");
                return;
            }

            try
            {
                CleanupPreview();

                WriteBlock.ExecuteTask("Compression Specimen Preview", () =>
                {
                    var bodies = service.CreateCompressionSpecimen(activePart, p);
                    previewBodies.AddRange(bodies);
                });
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    $"미리보기 생성 중 오류:\n\n{ex.Message}", "오류");
            }
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            var p = ReadParams();
            string error;
            if (!p.Validate(out error))
            {
                ValidationHelper.ShowError(error, "입력 오류");
                return;
            }

            try
            {
                if (previewBodies.Count > 0)
                {
                    previewBodies.Clear();
                }
                else
                {
                    WriteBlock.ExecuteTask("Create Compression Specimen", () =>
                    {
                        service.CreateCompressionSpecimen(activePart, p);
                    });
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    $"시편 생성 중 오류:\n\n{ex.Message}", "오류");
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
            if (previewBodies.Count > 0)
            {
                try
                {
                    WriteBlock.ExecuteTask("Cleanup Preview", () =>
                    {
                        foreach (var body in previewBodies)
                        {
                            if (body != null)
                                body.Delete();
                        }
                    });
                    previewBodies.Clear();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Preview cleanup error: {ex.Message}");
                }
            }
        }

        private void CompressionSpecimenDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (DialogResult != DialogResult.OK)
                CleanupPreview();
        }
    }
}
