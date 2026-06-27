using System;
using System.Collections.Generic;
using System.Drawing;
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
        private Timer previewTimer;
        private bool suppressPresetEvent = false;

        public FatigueSpecimenDialog(Part part)
        {
            InitializeComponent();
            service = new FatigueSpecimenService();
            activePart = part;
            previewBodies = new List<DesignBody>();

            previewTimer = new Timer();
            previewTimer.Interval = 300;
            previewTimer.Tick += PreviewTimer_Tick;

            cmbPreset.Items.AddRange(FatigueSpecimenFactory.PresetLabels);
            cmbPreset.SelectedIndex = 0;
            cmbPreset.SelectedIndexChanged += cmbPreset_SelectedIndexChanged;

            // 파라미터 변경 시 자동 미리보기
            numGaugeLength.ValueChanged += (s, ev) => SchedulePreview();
            numGaugeWidth.ValueChanged += (s, ev) => SchedulePreview();
            numThickness.ValueChanged += (s, ev) => SchedulePreview();
            numGaugeDiameter.ValueChanged += (s, ev) => SchedulePreview();
            numGripWidth.ValueChanged += (s, ev) => SchedulePreview();
            numGripLength.ValueChanged += (s, ev) => SchedulePreview();
            numTotalLength.ValueChanged += (s, ev) => SchedulePreview();
            numFilletRadius.ValueChanged += (s, ev) => SchedulePreview();
            numHourglassRadius.ValueChanged += (s, ev) => SchedulePreview();
            numCTWidth.ValueChanged += (s, ev) => SchedulePreview();
            numCTThickness.ValueChanged += (s, ev) => SchedulePreview();
            numInitialCrack.ValueChanged += (s, ev) => SchedulePreview();
            numPinHoleDiameter.ValueChanged += (s, ev) => SchedulePreview();
            numNotchWidth.ValueChanged += (s, ev) => SchedulePreview();
            numMTWidth.ValueChanged += (s, ev) => SchedulePreview();
            numMTLength.ValueChanged += (s, ev) => SchedulePreview();
            numMTThickness.ValueChanged += (s, ev) => SchedulePreview();
            numSlotHalfLength.ValueChanged += (s, ev) => SchedulePreview();
            numSlotWidth.ValueChanged += (s, ev) => SchedulePreview();
            numTubeOD.ValueChanged += (s, ev) => SchedulePreview();
            numTubeID.ValueChanged += (s, ev) => SchedulePreview();
            numTubeGaugeLength.ValueChanged += (s, ev) => SchedulePreview();
            numTubeTotalLength.ValueChanged += (s, ev) => SchedulePreview();
            numTubeGripOD.ValueChanged += (s, ev) => SchedulePreview();
            chkCreateGrips.CheckedChanged += (s, ev) => SchedulePreview();

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
            SchedulePreview();
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

            // 동적 레이아웃 (DPI 스케일링 대응: 실제 스케일된 크기 사용)
            int panelX = grpPreset.Location.X;
            int panelW = grpPreset.Width;

            int nextY = grpPreset.Location.Y + grpPreset.Height + 6;

            if (showBasic)
            {
                grpBasic.Location = new System.Drawing.Point(panelX, nextY);
                grpBasic.Width = panelW;
                nextY += grpBasic.Height + 6;
            }
            if (isCT)
            {
                grpCT.Location = new System.Drawing.Point(panelX, nextY);
                grpCT.Width = panelW;
                nextY += grpCT.Height + 6;
            }
            if (isMT)
            {
                grpMT.Location = new System.Drawing.Point(panelX, nextY);
                grpMT.Width = panelW;
                nextY += grpMT.Height + 6;
            }
            if (isTube)
            {
                grpTube.Location = new System.Drawing.Point(panelX, nextY);
                grpTube.Width = panelW;
                nextY += grpTube.Height + 6;
            }

            grpOptions.Location = new System.Drawing.Point(panelX, nextY);
            grpOptions.Width = panelW;
            nextY += grpOptions.Height + 10;

            int btnGap = 10;
            lblPreviewStatus.Location = new System.Drawing.Point(12, nextY);
            int btnRow = nextY + 18;
            btnCreate.Location = new System.Drawing.Point(250, btnRow);
            btnCancel.Location = new System.Drawing.Point(btnCreate.Right + btnGap, btnRow);

            int borderHeight = this.Height - this.ClientSize.Height;
            this.Height = btnRow + 46 + borderHeight;
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

        private void ExecuteAutoPreview()
        {
            var p = ReadParams();
            string error;
            if (!p.Validate(out error))
            {
                CleanupPreview();
                lblPreviewStatus.Text = "";
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
            previewTimer.Stop();
            previewTimer.Dispose();
            if (DialogResult != DialogResult.OK)
                CleanupPreview();
        }
    }
}
