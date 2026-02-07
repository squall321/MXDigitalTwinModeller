using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Fatigue;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Fatigue;

#if V251
using SpaceClaim.Api.V251.Extensibility;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Extensibility;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    public partial class FatigueSpecimenDialog : Form
    {
        private readonly FatigueSpecimenService service;
        private Part activePart;
        private List<DesignBody> previewBodies;
        private bool suppressPresetEvent = false;

        public FatigueSpecimenDialog(Part part)
        {
            InitializeComponent();
            service = new FatigueSpecimenService();
            activePart = part;
            previewBodies = new List<DesignBody>();

            cmbPreset.Items.AddRange(FatigueSpecimenFactory.PresetLabels);
            cmbPreset.SelectedIndex = 0;
            cmbPreset.SelectedIndexChanged += cmbPreset_SelectedIndexChanged;

            this.TopMost = true;
            this.FormClosing += FatigueSpecimenDialog_FormClosing;

            UpdateUIVisibility();
            UpdateDescription();
        }

        private void cmbPreset_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suppressPresetEvent) return;
            int idx = cmbPreset.SelectedIndex;
            if (idx < 0 || idx >= FatigueSpecimenFactory.PresetTypes.Length) return;

            var p = FatigueSpecimenParameters.FromPreset(FatigueSpecimenFactory.PresetTypes[idx]);
            ApplyParamsToUI(p);
            UpdateDescription();
            UpdateUIVisibility();
        }

        private void UpdateDescription()
        {
            int idx = cmbPreset.SelectedIndex;
            if (idx >= 0 && idx < FatigueSpecimenFactory.PresetDescriptions.Length)
                lblDescription.Text = FatigueSpecimenFactory.PresetDescriptions[idx];
        }

        private void ApplyParamsToUI(FatigueSpecimenParameters p)
        {
            suppressPresetEvent = true;
            numGaugeLength.Value = Math.Max(numGaugeLength.Minimum, (decimal)p.GaugeLength);
            numGaugeWidth.Value = Math.Max(numGaugeWidth.Minimum, (decimal)p.GaugeWidth);
            numThickness.Value = Math.Max(numThickness.Minimum, (decimal)p.Thickness);
            numGaugeDiameter.Value = Math.Max(numGaugeDiameter.Minimum, (decimal)p.GaugeDiameter);
            numGripWidth.Value = Math.Max(numGripWidth.Minimum, (decimal)p.GripWidth);
            numGripLength.Value = Math.Max(numGripLength.Minimum, (decimal)p.GripLength);
            numTotalLength.Value = Math.Max(numTotalLength.Minimum, (decimal)p.TotalLength);
            numFilletRadius.Value = Math.Max(numFilletRadius.Minimum, (decimal)p.FilletRadius);
            numHourglassRadius.Value = Math.Max(numHourglassRadius.Minimum, (decimal)p.HourglassRadius);

            numCTWidth.Value = (decimal)p.CTWidth;
            numCTThickness.Value = (decimal)p.CTThickness;
            numInitialCrack.Value = (decimal)p.InitialCrackLength;
            numPinHoleDiameter.Value = (decimal)p.PinHoleDiameter;
            numNotchWidth.Value = (decimal)p.NotchWidth;

            numMTWidth.Value = (decimal)p.MTWidth;
            numMTLength.Value = (decimal)p.MTLength;
            numMTThickness.Value = (decimal)p.MTThickness;
            numSlotHalfLength.Value = (decimal)p.SlotHalfLength;
            numSlotWidth.Value = (decimal)p.SlotWidth;

            numTubeOD.Value = (decimal)p.TubeOuterDiameter;
            numTubeID.Value = (decimal)p.TubeInnerDiameter;
            numTubeGaugeLength.Value = (decimal)p.TubeGaugeLength;
            numTubeTotalLength.Value = (decimal)p.TubeTotalLength;
            numTubeGripOD.Value = (decimal)p.TubeGripOuterDiameter;

            chkCreateGrips.Checked = p.CreateGrips;
            suppressPresetEvent = false;
        }

        private void UpdateUIVisibility()
        {
            int idx = cmbPreset.SelectedIndex;
            var type = (idx >= 0 && idx < FatigueSpecimenFactory.PresetTypes.Length)
                ? FatigueSpecimenFactory.PresetTypes[idx]
                : FatigueSpecimenType.Custom;

            bool isE466 = type == FatigueSpecimenType.ASTM_E466_Uniform ||
                          type == FatigueSpecimenType.ASTM_E466_Hourglass;
            bool isE606 = type == FatigueSpecimenType.ASTM_E606;
            bool isHourglass = type == FatigueSpecimenType.ASTM_E466_Hourglass;
            bool isCT = type == FatigueSpecimenType.ASTM_E647_CT;
            bool isMT = type == FatigueSpecimenType.ASTM_E647_MT;
            bool isTube = type == FatigueSpecimenType.ASTM_E2207;
            bool isCustom = type == FatigueSpecimenType.Custom;
            bool showBasic = isE466 || isE606 || isCustom;

            // 기본 치수 그룹
            grpBasic.Visible = showBasic;
            lblGaugeLength.Visible = !isHourglass;
            numGaugeLength.Visible = !isHourglass;
            lblGaugeWidth.Visible = !isE606;
            numGaugeWidth.Visible = !isE606;
            lblThickness.Visible = !isE606;
            numThickness.Visible = !isE606;
            lblGaugeDiameter.Visible = isE606;
            numGaugeDiameter.Visible = isE606;
            lblHourglassRadius.Visible = isHourglass;
            numHourglassRadius.Visible = isHourglass;

            // 전문 패널
            grpCT.Visible = isCT;
            grpMT.Visible = isMT;
            grpTube.Visible = isTube;

            // 동적 레이아웃
            const int basicHeight = 280;
            const int specialHeight = 160;
            const int optionHeight = 45;

            int nextY = grpPreset.Location.Y + grpPreset.Height + 6;

            if (showBasic)
            {
                grpBasic.SetBounds(12, nextY, 440, basicHeight);
                nextY += basicHeight + 6;
            }
            if (isCT)
            {
                grpCT.SetBounds(12, nextY, 440, specialHeight);
                nextY += specialHeight + 6;
            }
            if (isMT)
            {
                grpMT.SetBounds(12, nextY, 440, specialHeight);
                nextY += specialHeight + 6;
            }
            if (isTube)
            {
                grpTube.SetBounds(12, nextY, 440, specialHeight);
                nextY += specialHeight + 6;
            }

            grpOptions.SetBounds(12, nextY, 440, optionHeight);
            nextY += optionHeight + 10;

            btnPreview.Location = new System.Drawing.Point(140, nextY);
            btnCreate.Location = new System.Drawing.Point(250, nextY);
            btnCancel.Location = new System.Drawing.Point(360, nextY);

            int borderHeight = this.Height - this.ClientSize.Height;
            this.Height = nextY + 46 + borderHeight;
        }

        private FatigueSpecimenParameters ReadParams()
        {
            int idx = cmbPreset.SelectedIndex;
            var type = (idx >= 0 && idx < FatigueSpecimenFactory.PresetTypes.Length)
                ? FatigueSpecimenFactory.PresetTypes[idx]
                : FatigueSpecimenType.Custom;

            var p = new FatigueSpecimenParameters();
            p.SpecimenType = type;

            p.GaugeLength = (double)numGaugeLength.Value;
            p.GaugeWidth = (double)numGaugeWidth.Value;
            p.Thickness = (double)numThickness.Value;
            p.GaugeDiameter = (double)numGaugeDiameter.Value;
            p.GripWidth = (double)numGripWidth.Value;
            p.GripLength = (double)numGripLength.Value;
            p.TotalLength = (double)numTotalLength.Value;
            p.FilletRadius = (double)numFilletRadius.Value;
            p.HourglassRadius = (double)numHourglassRadius.Value;

            p.CTWidth = (double)numCTWidth.Value;
            p.CTThickness = (double)numCTThickness.Value;
            p.InitialCrackLength = (double)numInitialCrack.Value;
            p.PinHoleDiameter = (double)numPinHoleDiameter.Value;
            p.NotchWidth = (double)numNotchWidth.Value;

            p.MTWidth = (double)numMTWidth.Value;
            p.MTLength = (double)numMTLength.Value;
            p.MTThickness = (double)numMTThickness.Value;
            p.SlotHalfLength = (double)numSlotHalfLength.Value;
            p.SlotWidth = (double)numSlotWidth.Value;

            p.TubeOuterDiameter = (double)numTubeOD.Value;
            p.TubeInnerDiameter = (double)numTubeID.Value;
            p.TubeGaugeLength = (double)numTubeGaugeLength.Value;
            p.TubeTotalLength = (double)numTubeTotalLength.Value;
            p.TubeGripOuterDiameter = (double)numTubeGripOD.Value;

            p.CreateGrips = chkCreateGrips.Checked;

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
                WriteBlock.ExecuteTask("Fatigue Specimen Preview", () =>
                {
                    var bodies = service.CreateFatigueSpecimen(activePart, p);
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
                    previewBodies.Clear();
                else
                    WriteBlock.ExecuteTask("Create Fatigue Specimen", () =>
                    { service.CreateFatigueSpecimen(activePart, p); });

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
                            if (body != null) body.Delete();
                    });
                    previewBodies.Clear();
                }
                catch { }
            }
        }

        private void FatigueSpecimenDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (DialogResult != DialogResult.OK)
                CleanupPreview();
        }
    }
}
