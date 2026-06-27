using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Joint;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Joint;

#if V251
using SpaceClaim.Api.V251.Extensibility;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Extensibility;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    public partial class JointSpecimenDialog : Form
    {
        private readonly JointSpecimenService service;
        private Part activePart;
        private List<DesignBody> previewBodies;
        private Timer previewTimer;
        private bool suppressPresetEvent = false;

        public JointSpecimenDialog(Part part)
        {
            InitializeComponent();
            service = new JointSpecimenService();
            activePart = part;
            previewBodies = new List<DesignBody>();

            previewTimer = new Timer();
            previewTimer.Interval = 300;
            previewTimer.Tick += PreviewTimer_Tick;

            cmbPreset.Items.AddRange(JointSpecimenFactory.PresetLabels);
            cmbPreset.SelectedIndex = 0;
            cmbPreset.SelectedIndexChanged += cmbPreset_SelectedIndexChanged;

            // 파라미터 변경 시 자동 미리보기
            numAdherendWidth.ValueChanged += (s, ev) => SchedulePreview();
            numAdherendLength.ValueChanged += (s, ev) => SchedulePreview();
            numAdherendThickness.ValueChanged += (s, ev) => SchedulePreview();
            numOverlapLength.ValueChanged += (s, ev) => SchedulePreview();
            numAdhesiveThickness.ValueChanged += (s, ev) => SchedulePreview();
            numScarfAngle.ValueChanged += (s, ev) => SchedulePreview();
            numFlangeLength.ValueChanged += (s, ev) => SchedulePreview();
            numWebHeight.ValueChanged += (s, ev) => SchedulePreview();
            numWebThickness.ValueChanged += (s, ev) => SchedulePreview();
            numFilletBondSize.ValueChanged += (s, ev) => SchedulePreview();
            chkCreateAdhesive.CheckedChanged += (s, ev) => SchedulePreview();
            chkCreateGrips.CheckedChanged += (s, ev) => SchedulePreview();

            this.TopMost = true;
            this.FormClosing += JointSpecimenDialog_FormClosing;

            UpdateUIVisibility();
            UpdateDescription();
        }

        private void cmbPreset_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suppressPresetEvent) return;
            int idx = cmbPreset.SelectedIndex;
            if (idx < 0 || idx >= JointSpecimenFactory.PresetTypes.Length) return;

            var p = JointSpecimenParameters.FromPreset(JointSpecimenFactory.PresetTypes[idx]);
            ApplyParamsToUI(p);
            UpdateDescription();
            UpdateUIVisibility();
            SchedulePreview();
        }

        private void UpdateDescription()
        {
            int idx = cmbPreset.SelectedIndex;
            if (idx >= 0 && idx < JointSpecimenFactory.PresetDescriptions.Length)
                lblDescription.Text = JointSpecimenFactory.PresetDescriptions[idx];
        }

        private void ApplyParamsToUI(JointSpecimenParameters p)
        {
            suppressPresetEvent = true;

            numAdherendWidth.Value = Math.Max(numAdherendWidth.Minimum, (decimal)p.AdherendWidth);
            numAdherendLength.Value = Math.Max(numAdherendLength.Minimum, (decimal)p.AdherendLength);
            numAdherendThickness.Value = Math.Max(numAdherendThickness.Minimum, (decimal)p.AdherendThickness);

            numOverlapLength.Value = Math.Max(numOverlapLength.Minimum, (decimal)p.OverlapLength);
            numAdhesiveThickness.Value = Math.Max(numAdhesiveThickness.Minimum, (decimal)p.AdhesiveThickness);
            numScarfAngle.Value = Math.Max(numScarfAngle.Minimum, (decimal)p.ScarfAngle);

            numFlangeLength.Value = Math.Max(numFlangeLength.Minimum, (decimal)p.FlangeLength);
            numWebHeight.Value = Math.Max(numWebHeight.Minimum, (decimal)p.WebHeight);
            numWebThickness.Value = Math.Max(numWebThickness.Minimum, (decimal)p.WebThickness);
            numFilletBondSize.Value = Math.Max(numFilletBondSize.Minimum, Math.Min(numFilletBondSize.Maximum, (decimal)p.FilletBondSize));

            chkCreateAdhesive.Checked = p.CreateAdhesiveBody;
            chkCreateGrips.Checked = p.CreateGrips;

            suppressPresetEvent = false;
        }

        private void UpdateUIVisibility()
        {
            int idx = cmbPreset.SelectedIndex;
            var type = (idx >= 0 && idx < JointSpecimenFactory.PresetTypes.Length)
                ? JointSpecimenFactory.PresetTypes[idx]
                : JointSpecimenType.Custom;

            bool isLap = type == JointSpecimenType.ASTM_D1002_SingleLap ||
                         type == JointSpecimenType.ASTM_D3528_DoubleLap;
            bool isScarf = type == JointSpecimenType.Scarf_Joint;
            bool isButt = type == JointSpecimenType.Butt_Joint;
            bool isTJoint = type == JointSpecimenType.T_Joint;
            bool isCustom = type == JointSpecimenType.Custom;

            // 겹침 길이: Lap + Custom
            lblOverlapLength.Visible = isLap || isCustom;
            numOverlapLength.Visible = isLap || isCustom;

            // 접착층 두께: 항상 표시
            lblAdhesiveThickness.Visible = true;
            numAdhesiveThickness.Visible = true;

            // 스카프 각도: Scarf만
            lblScarfAngle.Visible = isScarf;
            numScarfAngle.Visible = isScarf;

            // T-Joint 그룹
            grpTJoint.Visible = isTJoint;

            // 동적 레이아웃 (DPI 스케일링 대응: 실제 스케일된 크기 사용)
            int panelX = grpPreset.Location.X;
            int panelW = grpPreset.Width;

            int nextY = grpPreset.Location.Y + grpPreset.Height + 6;
            grpAdherend.Location = new System.Drawing.Point(panelX, nextY);
            grpAdherend.Width = panelW;
            nextY += grpAdherend.Height + 6;

            // grpJoint 높이 조정
            int visibleJointRows = 0;
            if (isLap || isCustom) visibleJointRows++; // overlap
            visibleJointRows++; // adhesive always
            if (isScarf) visibleJointRows++; // scarf angle
            int actualJointHeight = 24 + visibleJointRows * 27 + 10;
            if (actualJointHeight < 60) actualJointHeight = 60;
            grpJoint.Location = new System.Drawing.Point(panelX, nextY);
            grpJoint.Size = new System.Drawing.Size(panelW, actualJointHeight);
            nextY += actualJointHeight + 6;

            if (isTJoint)
            {
                grpTJoint.Location = new System.Drawing.Point(panelX, nextY);
                grpTJoint.Width = panelW;
                nextY += grpTJoint.Height + 6;
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

        private JointSpecimenParameters ReadParams()
        {
            int idx = cmbPreset.SelectedIndex;
            var type = (idx >= 0 && idx < JointSpecimenFactory.PresetTypes.Length)
                ? JointSpecimenFactory.PresetTypes[idx]
                : JointSpecimenType.Custom;

            var p = new JointSpecimenParameters();
            p.SpecimenType = type;

            p.AdherendWidth = (double)numAdherendWidth.Value;
            p.AdherendLength = (double)numAdherendLength.Value;
            p.AdherendThickness = (double)numAdherendThickness.Value;

            p.OverlapLength = (double)numOverlapLength.Value;
            p.AdhesiveThickness = (double)numAdhesiveThickness.Value;
            p.ScarfAngle = (double)numScarfAngle.Value;

            p.FlangeLength = (double)numFlangeLength.Value;
            p.WebHeight = (double)numWebHeight.Value;
            p.WebThickness = (double)numWebThickness.Value;
            p.FilletBondSize = (double)numFilletBondSize.Value;

            p.CreateAdhesiveBody = chkCreateAdhesive.Checked;
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
                WriteBlock.ExecuteTask("Joint Specimen Preview", () =>
                {
                    var bodies = service.CreateJointSpecimen(activePart, p);
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
                    WriteBlock.ExecuteTask("Create Joint Specimen", () =>
                    { service.CreateJointSpecimen(activePart, p); });

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

        private void JointSpecimenDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            previewTimer.Stop();
            previewTimer.Dispose();
            if (DialogResult != DialogResult.OK)
                CleanupPreview();
        }
    }
}
