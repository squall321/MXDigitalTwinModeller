namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    partial class FatigueSpecimenDialog
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.grpPreset = new System.Windows.Forms.GroupBox();
            this.lblDescription = new System.Windows.Forms.Label();
            this.cmbPreset = new System.Windows.Forms.ComboBox();
            this.lblPreset = new System.Windows.Forms.Label();
            this.grpBasic = new System.Windows.Forms.GroupBox();
            this.numFilletRadius = new System.Windows.Forms.NumericUpDown();
            this.lblFilletRadius = new System.Windows.Forms.Label();
            this.numTotalLength = new System.Windows.Forms.NumericUpDown();
            this.lblTotalLength = new System.Windows.Forms.Label();
            this.numGripLength = new System.Windows.Forms.NumericUpDown();
            this.lblGripLength = new System.Windows.Forms.Label();
            this.numGripWidth = new System.Windows.Forms.NumericUpDown();
            this.lblGripWidth = new System.Windows.Forms.Label();
            this.numThickness = new System.Windows.Forms.NumericUpDown();
            this.lblThickness = new System.Windows.Forms.Label();
            this.numGaugeWidth = new System.Windows.Forms.NumericUpDown();
            this.lblGaugeWidth = new System.Windows.Forms.Label();
            this.numGaugeLength = new System.Windows.Forms.NumericUpDown();
            this.lblGaugeLength = new System.Windows.Forms.Label();
            this.numHourglassRadius = new System.Windows.Forms.NumericUpDown();
            this.lblHourglassRadius = new System.Windows.Forms.Label();
            this.numGaugeDiameter = new System.Windows.Forms.NumericUpDown();
            this.lblGaugeDiameter = new System.Windows.Forms.Label();
            this.grpCT = new System.Windows.Forms.GroupBox();
            this.numNotchWidth = new System.Windows.Forms.NumericUpDown();
            this.lblNotchWidth = new System.Windows.Forms.Label();
            this.numPinHoleDiameter = new System.Windows.Forms.NumericUpDown();
            this.lblPinHoleDiameter = new System.Windows.Forms.Label();
            this.numInitialCrack = new System.Windows.Forms.NumericUpDown();
            this.lblInitialCrack = new System.Windows.Forms.Label();
            this.numCTThickness = new System.Windows.Forms.NumericUpDown();
            this.lblCTThickness = new System.Windows.Forms.Label();
            this.numCTWidth = new System.Windows.Forms.NumericUpDown();
            this.lblCTWidth = new System.Windows.Forms.Label();
            this.grpMT = new System.Windows.Forms.GroupBox();
            this.numSlotWidth = new System.Windows.Forms.NumericUpDown();
            this.lblSlotWidth = new System.Windows.Forms.Label();
            this.numSlotHalfLength = new System.Windows.Forms.NumericUpDown();
            this.lblSlotHalfLength = new System.Windows.Forms.Label();
            this.numMTThickness = new System.Windows.Forms.NumericUpDown();
            this.lblMTThickness = new System.Windows.Forms.Label();
            this.numMTLength = new System.Windows.Forms.NumericUpDown();
            this.lblMTLength = new System.Windows.Forms.Label();
            this.numMTWidth = new System.Windows.Forms.NumericUpDown();
            this.lblMTWidth = new System.Windows.Forms.Label();
            this.grpTube = new System.Windows.Forms.GroupBox();
            this.numTubeGripOD = new System.Windows.Forms.NumericUpDown();
            this.lblTubeGripOD = new System.Windows.Forms.Label();
            this.numTubeTotalLength = new System.Windows.Forms.NumericUpDown();
            this.lblTubeTotalLength = new System.Windows.Forms.Label();
            this.numTubeGaugeLength = new System.Windows.Forms.NumericUpDown();
            this.lblTubeGaugeLength = new System.Windows.Forms.Label();
            this.numTubeID = new System.Windows.Forms.NumericUpDown();
            this.lblTubeID = new System.Windows.Forms.Label();
            this.numTubeOD = new System.Windows.Forms.NumericUpDown();
            this.lblTubeOD = new System.Windows.Forms.Label();
            this.grpOptions = new System.Windows.Forms.GroupBox();
            this.chkCreateGrips = new System.Windows.Forms.CheckBox();
            this.btnPreview = new System.Windows.Forms.Button();
            this.btnCreate = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // grpPreset
            this.grpPreset.Controls.Add(this.lblDescription);
            this.grpPreset.Controls.Add(this.cmbPreset);
            this.grpPreset.Controls.Add(this.lblPreset);
            this.grpPreset.Location = new System.Drawing.Point(12, 12);
            this.grpPreset.Size = new System.Drawing.Size(440, 90);
            this.grpPreset.Text = "프리셋";
            // lblPreset
            this.lblPreset.AutoSize = true;
            this.lblPreset.Location = new System.Drawing.Point(15, 28);
            this.lblPreset.Text = "규격:";
            // cmbPreset
            this.cmbPreset.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbPreset.Location = new System.Drawing.Point(80, 25);
            this.cmbPreset.Size = new System.Drawing.Size(340, 20);
            // lblDescription
            this.lblDescription.AutoSize = true;
            this.lblDescription.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblDescription.Location = new System.Drawing.Point(15, 58);
            this.lblDescription.Text = "";
            // grpBasic - E466/E606 기본 치수
            this.grpBasic.Controls.Add(this.lblGaugeLength); this.grpBasic.Controls.Add(this.numGaugeLength);
            this.grpBasic.Controls.Add(this.lblGaugeWidth); this.grpBasic.Controls.Add(this.numGaugeWidth);
            this.grpBasic.Controls.Add(this.lblThickness); this.grpBasic.Controls.Add(this.numThickness);
            this.grpBasic.Controls.Add(this.lblGripWidth); this.grpBasic.Controls.Add(this.numGripWidth);
            this.grpBasic.Controls.Add(this.lblGripLength); this.grpBasic.Controls.Add(this.numGripLength);
            this.grpBasic.Controls.Add(this.lblTotalLength); this.grpBasic.Controls.Add(this.numTotalLength);
            this.grpBasic.Controls.Add(this.lblFilletRadius); this.grpBasic.Controls.Add(this.numFilletRadius);
            this.grpBasic.Controls.Add(this.lblHourglassRadius); this.grpBasic.Controls.Add(this.numHourglassRadius);
            this.grpBasic.Controls.Add(this.lblGaugeDiameter); this.grpBasic.Controls.Add(this.numGaugeDiameter);
            this.grpBasic.Location = new System.Drawing.Point(12, 108);
            this.grpBasic.Size = new System.Drawing.Size(440, 280);
            this.grpBasic.Text = "기본 치수 (mm)";
            // basic controls
            int y = 24;
            SetupLabelAndNum(this.lblGaugeLength, this.numGaugeLength, "게이지 길이:", ref y, 75.0m, 0.01m, 1000m);
            SetupLabelAndNum(this.lblGaugeWidth, this.numGaugeWidth, "게이지 폭:", ref y, 12.5m, 0.01m, 500m);
            SetupLabelAndNum(this.lblThickness, this.numThickness, "두께:", ref y, 6.0m, 0.01m, 200m);
            SetupLabelAndNum(this.lblGaugeDiameter, this.numGaugeDiameter, "게이지 직경:", ref y, 6.35m, 0.01m, 200m);
            SetupLabelAndNum(this.lblGripWidth, this.numGripWidth, "그립 폭:", ref y, 20.0m, 0.01m, 500m);
            SetupLabelAndNum(this.lblGripLength, this.numGripLength, "그립 길이:", ref y, 50.0m, 0.01m, 500m);
            SetupLabelAndNum(this.lblTotalLength, this.numTotalLength, "전체 길이:", ref y, 200.0m, 1m, 2000m);
            SetupLabelAndNum(this.lblFilletRadius, this.numFilletRadius, "필렛 반경:", ref y, 50.0m, 0.01m, 500m);
            SetupLabelAndNum(this.lblHourglassRadius, this.numHourglassRadius, "Hourglass R:", ref y, 100.0m, 1m, 1000m);
            // grpCT
            this.grpCT.Controls.Add(this.lblCTWidth); this.grpCT.Controls.Add(this.numCTWidth);
            this.grpCT.Controls.Add(this.lblCTThickness); this.grpCT.Controls.Add(this.numCTThickness);
            this.grpCT.Controls.Add(this.lblInitialCrack); this.grpCT.Controls.Add(this.numInitialCrack);
            this.grpCT.Controls.Add(this.lblPinHoleDiameter); this.grpCT.Controls.Add(this.numPinHoleDiameter);
            this.grpCT.Controls.Add(this.lblNotchWidth); this.grpCT.Controls.Add(this.numNotchWidth);
            this.grpCT.Location = new System.Drawing.Point(12, 394);
            this.grpCT.Size = new System.Drawing.Size(440, 160);
            this.grpCT.Text = "CT 시편 파라미터 (E647)";
            this.grpCT.Visible = false;
            y = 24;
            SetupLabelAndNum(this.lblCTWidth, this.numCTWidth, "W (폭):", ref y, 50.0m, 1m, 500m);
            SetupLabelAndNum(this.lblCTThickness, this.numCTThickness, "B (두께):", ref y, 12.5m, 0.1m, 200m);
            SetupLabelAndNum(this.lblInitialCrack, this.numInitialCrack, "초기 균열 a₀:", ref y, 25.0m, 0.1m, 200m);
            SetupLabelAndNum(this.lblPinHoleDiameter, this.numPinHoleDiameter, "핀홀 직경:", ref y, 12.5m, 0.1m, 100m);
            SetupLabelAndNum(this.lblNotchWidth, this.numNotchWidth, "노치 폭:", ref y, 1.0m, 0.1m, 20m);
            // grpMT
            this.grpMT.Controls.Add(this.lblMTWidth); this.grpMT.Controls.Add(this.numMTWidth);
            this.grpMT.Controls.Add(this.lblMTLength); this.grpMT.Controls.Add(this.numMTLength);
            this.grpMT.Controls.Add(this.lblMTThickness); this.grpMT.Controls.Add(this.numMTThickness);
            this.grpMT.Controls.Add(this.lblSlotHalfLength); this.grpMT.Controls.Add(this.numSlotHalfLength);
            this.grpMT.Controls.Add(this.lblSlotWidth); this.grpMT.Controls.Add(this.numSlotWidth);
            this.grpMT.Location = new System.Drawing.Point(12, 394);
            this.grpMT.Size = new System.Drawing.Size(440, 160);
            this.grpMT.Text = "M(T) 시편 파라미터 (E647)";
            this.grpMT.Visible = false;
            y = 24;
            SetupLabelAndNum(this.lblMTWidth, this.numMTWidth, "MT 폭 (W):", ref y, 75.0m, 1m, 500m);
            SetupLabelAndNum(this.lblMTLength, this.numMTLength, "MT 길이:", ref y, 300.0m, 10m, 2000m);
            SetupLabelAndNum(this.lblMTThickness, this.numMTThickness, "MT 두께:", ref y, 6.0m, 0.1m, 100m);
            SetupLabelAndNum(this.lblSlotHalfLength, this.numSlotHalfLength, "슬롯 반길이 a₀:", ref y, 10.0m, 0.1m, 200m);
            SetupLabelAndNum(this.lblSlotWidth, this.numSlotWidth, "슬롯 폭:", ref y, 0.5m, 0.1m, 10m);
            // grpTube
            this.grpTube.Controls.Add(this.lblTubeOD); this.grpTube.Controls.Add(this.numTubeOD);
            this.grpTube.Controls.Add(this.lblTubeID); this.grpTube.Controls.Add(this.numTubeID);
            this.grpTube.Controls.Add(this.lblTubeGaugeLength); this.grpTube.Controls.Add(this.numTubeGaugeLength);
            this.grpTube.Controls.Add(this.lblTubeTotalLength); this.grpTube.Controls.Add(this.numTubeTotalLength);
            this.grpTube.Controls.Add(this.lblTubeGripOD); this.grpTube.Controls.Add(this.numTubeGripOD);
            this.grpTube.Location = new System.Drawing.Point(12, 394);
            this.grpTube.Size = new System.Drawing.Size(440, 160);
            this.grpTube.Text = "원통 시편 파라미터 (E2207)";
            this.grpTube.Visible = false;
            y = 24;
            SetupLabelAndNum(this.lblTubeOD, this.numTubeOD, "외경 (OD):", ref y, 22.0m, 0.1m, 200m);
            SetupLabelAndNum(this.lblTubeID, this.numTubeID, "내경 (ID):", ref y, 20.0m, 0.1m, 200m);
            SetupLabelAndNum(this.lblTubeGaugeLength, this.numTubeGaugeLength, "게이지 길이:", ref y, 20.0m, 1m, 500m);
            SetupLabelAndNum(this.lblTubeTotalLength, this.numTubeTotalLength, "전체 길이:", ref y, 120.0m, 10m, 1000m);
            SetupLabelAndNum(this.lblTubeGripOD, this.numTubeGripOD, "그립부 외경:", ref y, 28.0m, 0.1m, 200m);
            // grpOptions
            this.grpOptions.Controls.Add(this.chkCreateGrips);
            this.grpOptions.Location = new System.Drawing.Point(12, 560);
            this.grpOptions.Size = new System.Drawing.Size(440, 45);
            this.grpOptions.Text = "옵션";
            this.chkCreateGrips.AutoSize = true;
            this.chkCreateGrips.Checked = true;
            this.chkCreateGrips.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkCreateGrips.Location = new System.Drawing.Point(15, 20);
            this.chkCreateGrips.Text = "그립/지그 생성";
            // Buttons
            this.btnPreview.Location = new System.Drawing.Point(140, 618);
            this.btnPreview.Size = new System.Drawing.Size(90, 32);
            this.btnPreview.Text = "미리보기";
            this.btnPreview.Click += new System.EventHandler(this.btnPreview_Click);
            this.btnCreate.Location = new System.Drawing.Point(250, 618);
            this.btnCreate.Size = new System.Drawing.Size(90, 32);
            this.btnCreate.Text = "생성";
            this.btnCreate.Click += new System.EventHandler(this.btnCreate_Click);
            this.btnCancel.Location = new System.Drawing.Point(360, 618);
            this.btnCancel.Size = new System.Drawing.Size(90, 32);
            this.btnCancel.Text = "취소";
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // Form
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(474, 665);
            this.Controls.Add(this.grpPreset);
            this.Controls.Add(this.grpBasic);
            this.Controls.Add(this.grpCT);
            this.Controls.Add(this.grpMT);
            this.Controls.Add(this.grpTube);
            this.Controls.Add(this.grpOptions);
            this.Controls.Add(this.btnPreview);
            this.Controls.Add(this.btnCreate);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FatigueSpecimenDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "피로 시편 생성 (Fatigue Specimen)";
            this.ResumeLayout(false);
        }

        private void SetupLabelAndNum(System.Windows.Forms.Label lbl, System.Windows.Forms.NumericUpDown num,
            string text, ref int y, decimal defaultVal, decimal min, decimal max)
        {
            lbl.AutoSize = true;
            lbl.Location = new System.Drawing.Point(15, y + 2);
            lbl.Text = text;
            num.DecimalPlaces = 2;
            num.Location = new System.Drawing.Point(140, y);
            num.Size = new System.Drawing.Size(120, 21);
            num.Minimum = min;
            num.Maximum = max;
            num.Value = defaultVal;
            y += 27;
        }

        #endregion

        private System.Windows.Forms.GroupBox grpPreset;
        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.ComboBox cmbPreset;
        private System.Windows.Forms.Label lblPreset;
        private System.Windows.Forms.GroupBox grpBasic;
        private System.Windows.Forms.NumericUpDown numGaugeLength;
        private System.Windows.Forms.Label lblGaugeLength;
        private System.Windows.Forms.NumericUpDown numGaugeWidth;
        private System.Windows.Forms.Label lblGaugeWidth;
        private System.Windows.Forms.NumericUpDown numThickness;
        private System.Windows.Forms.Label lblThickness;
        private System.Windows.Forms.NumericUpDown numGaugeDiameter;
        private System.Windows.Forms.Label lblGaugeDiameter;
        private System.Windows.Forms.NumericUpDown numGripWidth;
        private System.Windows.Forms.Label lblGripWidth;
        private System.Windows.Forms.NumericUpDown numGripLength;
        private System.Windows.Forms.Label lblGripLength;
        private System.Windows.Forms.NumericUpDown numTotalLength;
        private System.Windows.Forms.Label lblTotalLength;
        private System.Windows.Forms.NumericUpDown numFilletRadius;
        private System.Windows.Forms.Label lblFilletRadius;
        private System.Windows.Forms.NumericUpDown numHourglassRadius;
        private System.Windows.Forms.Label lblHourglassRadius;
        private System.Windows.Forms.GroupBox grpCT;
        private System.Windows.Forms.NumericUpDown numCTWidth;
        private System.Windows.Forms.Label lblCTWidth;
        private System.Windows.Forms.NumericUpDown numCTThickness;
        private System.Windows.Forms.Label lblCTThickness;
        private System.Windows.Forms.NumericUpDown numInitialCrack;
        private System.Windows.Forms.Label lblInitialCrack;
        private System.Windows.Forms.NumericUpDown numPinHoleDiameter;
        private System.Windows.Forms.Label lblPinHoleDiameter;
        private System.Windows.Forms.NumericUpDown numNotchWidth;
        private System.Windows.Forms.Label lblNotchWidth;
        private System.Windows.Forms.GroupBox grpMT;
        private System.Windows.Forms.NumericUpDown numMTWidth;
        private System.Windows.Forms.Label lblMTWidth;
        private System.Windows.Forms.NumericUpDown numMTLength;
        private System.Windows.Forms.Label lblMTLength;
        private System.Windows.Forms.NumericUpDown numMTThickness;
        private System.Windows.Forms.Label lblMTThickness;
        private System.Windows.Forms.NumericUpDown numSlotHalfLength;
        private System.Windows.Forms.Label lblSlotHalfLength;
        private System.Windows.Forms.NumericUpDown numSlotWidth;
        private System.Windows.Forms.Label lblSlotWidth;
        private System.Windows.Forms.GroupBox grpTube;
        private System.Windows.Forms.NumericUpDown numTubeOD;
        private System.Windows.Forms.Label lblTubeOD;
        private System.Windows.Forms.NumericUpDown numTubeID;
        private System.Windows.Forms.Label lblTubeID;
        private System.Windows.Forms.NumericUpDown numTubeGaugeLength;
        private System.Windows.Forms.Label lblTubeGaugeLength;
        private System.Windows.Forms.NumericUpDown numTubeTotalLength;
        private System.Windows.Forms.Label lblTubeTotalLength;
        private System.Windows.Forms.NumericUpDown numTubeGripOD;
        private System.Windows.Forms.Label lblTubeGripOD;
        private System.Windows.Forms.GroupBox grpOptions;
        private System.Windows.Forms.CheckBox chkCreateGrips;
        private System.Windows.Forms.Button btnPreview;
        private System.Windows.Forms.Button btnCreate;
        private System.Windows.Forms.Button btnCancel;
    }
}
