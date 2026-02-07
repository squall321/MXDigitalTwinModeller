using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.CAI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.CAI;

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
    /// CAI 시편 생성 다이얼로그
    /// </summary>
    public partial class CAISpecimenDialog : Form
    {
        private readonly CAISpecimenService service;
        private Part activePart;
        private List<DesignBody> previewBodies;
        private bool suppressPresetEvent = false;

        private readonly CAISpecimenType[] presetTypes = new[]
        {
            CAISpecimenType.ASTM_D7137,
            CAISpecimenType.ASTM_D6264,
            CAISpecimenType.Boeing_BSS7260,
            CAISpecimenType.Custom
        };

        private readonly string[] presetLabels = new[]
        {
            "ASTM D7137/D7136 - CAI 표준 (150x100mm)",
            "ASTM D6264 - 낙추충격 (150x100mm)",
            "Boeing BSS 7260 - 항공용 (152.4x101.6mm)",
            "사용자 정의 (Custom)"
        };

        private readonly string[] presetDescriptions = new[]
        {
            "ASTM D7137 표준 CAI 시편. 150x100mm 패널, 4mm 두께 기본.",
            "ASTM D6264 낙추충격 시편. 150x100mm, 5mm 두께 기본.",
            "Boeing BSS 7260 항공 산업용 CAI. 6x4 inch 패널.",
            "사용자 정의 치수."
        };

        public CAISpecimenDialog(Part part)
        {
            InitializeComponent();
            service = new CAISpecimenService();
            activePart = part;
            previewBodies = new List<DesignBody>();

            cmbPreset.Items.AddRange(presetLabels);
            cmbPreset.SelectedIndex = 0;
            cmbPreset.SelectedIndexChanged += cmbPreset_SelectedIndexChanged;

            this.TopMost = true;
            this.FormClosing += CAISpecimenDialog_FormClosing;

            UpdateDescription();
        }

        private void cmbPreset_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suppressPresetEvent) return;

            int idx = cmbPreset.SelectedIndex;
            if (idx < 0 || idx >= presetTypes.Length) return;

            var p = CAISpecimenParameters.FromPreset(presetTypes[idx]);
            ApplyParamsToUI(p);
            UpdateDescription();
        }

        private void UpdateDescription()
        {
            int idx = cmbPreset.SelectedIndex;
            if (idx >= 0 && idx < presetDescriptions.Length)
                lblDescription.Text = presetDescriptions[idx];
        }

        private void ApplyParamsToUI(CAISpecimenParameters p)
        {
            suppressPresetEvent = true;

            numPanelLength.Value = (decimal)p.PanelLength;
            numPanelWidth.Value = (decimal)p.PanelWidth;
            numThickness.Value = (decimal)p.Thickness;

            chkCreateJig.Checked = p.CreateJig;
            numJigThickness.Value = (decimal)p.JigThickness;
            numWindowLength.Value = (decimal)p.WindowLength;
            numWindowWidth.Value = (decimal)p.WindowWidth;
            numJigClearance.Value = (decimal)p.JigClearance;

            chkCreateDamage.Checked = p.CreateDamageZone;
            rdoCircular.Checked = !p.IsEllipticalDamage;
            rdoElliptical.Checked = p.IsEllipticalDamage;
            numDamageDiameter.Value = (decimal)p.DamageDiameter;
            numDamageMajorAxis.Value = (decimal)p.DamageMajorAxis;
            numDamageMinorAxis.Value = (decimal)p.DamageMinorAxis;
            numDamageDepth.Value = (decimal)p.DamageDepthPercent;

            UpdateDamageShapeVisibility();
            suppressPresetEvent = false;
        }

        private void chkCreateJig_CheckedChanged(object sender, EventArgs e)
        {
            bool enabled = chkCreateJig.Checked;
            numJigThickness.Enabled = enabled;
            numWindowLength.Enabled = enabled;
            numWindowWidth.Enabled = enabled;
            numJigClearance.Enabled = enabled;
        }

        private void chkCreateDamage_CheckedChanged(object sender, EventArgs e)
        {
            bool enabled = chkCreateDamage.Checked;
            rdoCircular.Enabled = enabled;
            rdoElliptical.Enabled = enabled;
            numDamageDiameter.Enabled = enabled && rdoCircular.Checked;
            numDamageMajorAxis.Enabled = enabled && rdoElliptical.Checked;
            numDamageMinorAxis.Enabled = enabled && rdoElliptical.Checked;
            numDamageDepth.Enabled = enabled;
        }

        private void rdoDamageShape_CheckedChanged(object sender, EventArgs e)
        {
            UpdateDamageShapeVisibility();
        }

        private void UpdateDamageShapeVisibility()
        {
            bool isCircular = rdoCircular.Checked;
            lblDamageDiameter.Visible = isCircular;
            numDamageDiameter.Visible = isCircular;
            lblDamageMajorAxis.Visible = !isCircular;
            numDamageMajorAxis.Visible = !isCircular;
            lblDamageMinorAxis.Visible = !isCircular;
            numDamageMinorAxis.Visible = !isCircular;
        }

        private CAISpecimenParameters ReadParams()
        {
            int idx = cmbPreset.SelectedIndex;
            var type = (idx >= 0 && idx < presetTypes.Length)
                ? presetTypes[idx]
                : CAISpecimenType.Custom;

            var p = new CAISpecimenParameters();
            p.SpecimenType = type;
            p.PanelLength = (double)numPanelLength.Value;
            p.PanelWidth = (double)numPanelWidth.Value;
            p.Thickness = (double)numThickness.Value;
            p.CreateJig = chkCreateJig.Checked;
            p.JigThickness = (double)numJigThickness.Value;
            p.WindowLength = (double)numWindowLength.Value;
            p.WindowWidth = (double)numWindowWidth.Value;
            p.JigClearance = (double)numJigClearance.Value;
            p.CreateDamageZone = chkCreateDamage.Checked;
            p.IsEllipticalDamage = rdoElliptical.Checked;
            p.DamageDiameter = (double)numDamageDiameter.Value;
            p.DamageMajorAxis = (double)numDamageMajorAxis.Value;
            p.DamageMinorAxis = (double)numDamageMinorAxis.Value;
            p.DamageDepthPercent = (double)numDamageDepth.Value;

            return p;
        }

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
                WriteBlock.ExecuteTask("CAI Specimen Preview", () =>
                {
                    var bodies = service.CreateCAISpecimen(activePart, p);
                    previewBodies.AddRange(bodies);
                });
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError($"미리보기 생성 중 오류:\n\n{ex.Message}", "오류");
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
                    WriteBlock.ExecuteTask("Create CAI Specimen", () =>
                    {
                        service.CreateCAISpecimen(activePart, p);
                    });
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError($"시편 생성 중 오류:\n\n{ex.Message}", "오류");
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

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

        private void CAISpecimenDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (DialogResult != DialogResult.OK)
                CleanupPreview();
        }
    }
}
