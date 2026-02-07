namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    partial class JointSpecimenDialog
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
            this.grpAdherend = new System.Windows.Forms.GroupBox();
            this.numAdherendThickness = new System.Windows.Forms.NumericUpDown();
            this.lblAdherendThickness = new System.Windows.Forms.Label();
            this.numAdherendLength = new System.Windows.Forms.NumericUpDown();
            this.lblAdherendLength = new System.Windows.Forms.Label();
            this.numAdherendWidth = new System.Windows.Forms.NumericUpDown();
            this.lblAdherendWidth = new System.Windows.Forms.Label();
            this.grpJoint = new System.Windows.Forms.GroupBox();
            this.numOverlapLength = new System.Windows.Forms.NumericUpDown();
            this.lblOverlapLength = new System.Windows.Forms.Label();
            this.numAdhesiveThickness = new System.Windows.Forms.NumericUpDown();
            this.lblAdhesiveThickness = new System.Windows.Forms.Label();
            this.numScarfAngle = new System.Windows.Forms.NumericUpDown();
            this.lblScarfAngle = new System.Windows.Forms.Label();
            this.grpTJoint = new System.Windows.Forms.GroupBox();
            this.numFlangeLength = new System.Windows.Forms.NumericUpDown();
            this.lblFlangeLength = new System.Windows.Forms.Label();
            this.numWebHeight = new System.Windows.Forms.NumericUpDown();
            this.lblWebHeight = new System.Windows.Forms.Label();
            this.numWebThickness = new System.Windows.Forms.NumericUpDown();
            this.lblWebThickness = new System.Windows.Forms.Label();
            this.numFilletBondSize = new System.Windows.Forms.NumericUpDown();
            this.lblFilletBondSize = new System.Windows.Forms.Label();
            this.grpOptions = new System.Windows.Forms.GroupBox();
            this.chkCreateAdhesive = new System.Windows.Forms.CheckBox();
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
            this.lblPreset.AutoSize = true;
            this.lblPreset.Location = new System.Drawing.Point(15, 28);
            this.lblPreset.Text = "접합 유형:";
            this.cmbPreset.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbPreset.Location = new System.Drawing.Point(80, 25);
            this.cmbPreset.Size = new System.Drawing.Size(340, 20);
            this.lblDescription.AutoSize = true;
            this.lblDescription.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblDescription.Location = new System.Drawing.Point(15, 58);
            this.lblDescription.Text = "";
            // grpAdherend
            this.grpAdherend.Controls.Add(this.lblAdherendWidth); this.grpAdherend.Controls.Add(this.numAdherendWidth);
            this.grpAdherend.Controls.Add(this.lblAdherendLength); this.grpAdherend.Controls.Add(this.numAdherendLength);
            this.grpAdherend.Controls.Add(this.lblAdherendThickness); this.grpAdherend.Controls.Add(this.numAdherendThickness);
            this.grpAdherend.Location = new System.Drawing.Point(12, 108);
            this.grpAdherend.Size = new System.Drawing.Size(440, 110);
            this.grpAdherend.Text = "판재 치수 (mm)";
            int y = 24;
            SetupLN(this.lblAdherendWidth, this.numAdherendWidth, "판재 폭:", ref y, 25.4m, 0.1m, 500m);
            SetupLN(this.lblAdherendLength, this.numAdherendLength, "판재 길이:", ref y, 100.0m, 1m, 1000m);
            SetupLN(this.lblAdherendThickness, this.numAdherendThickness, "판재 두께:", ref y, 1.6m, 0.01m, 100m);
            // grpJoint
            this.grpJoint.Controls.Add(this.lblOverlapLength); this.grpJoint.Controls.Add(this.numOverlapLength);
            this.grpJoint.Controls.Add(this.lblAdhesiveThickness); this.grpJoint.Controls.Add(this.numAdhesiveThickness);
            this.grpJoint.Controls.Add(this.lblScarfAngle); this.grpJoint.Controls.Add(this.numScarfAngle);
            this.grpJoint.Location = new System.Drawing.Point(12, 224);
            this.grpJoint.Size = new System.Drawing.Size(440, 110);
            this.grpJoint.Text = "접합 파라미터 (mm)";
            y = 24;
            SetupLN(this.lblOverlapLength, this.numOverlapLength, "겹침 길이:", ref y, 25.4m, 0.1m, 500m);
            SetupLN(this.lblAdhesiveThickness, this.numAdhesiveThickness, "접착층 두께:", ref y, 0.2m, 0.01m, 10m);
            SetupLN(this.lblScarfAngle, this.numScarfAngle, "스카프 각도(°):", ref y, 5.0m, 0.5m, 45m);
            this.lblScarfAngle.Visible = false;
            this.numScarfAngle.Visible = false;
            // grpTJoint
            this.grpTJoint.Controls.Add(this.lblFlangeLength); this.grpTJoint.Controls.Add(this.numFlangeLength);
            this.grpTJoint.Controls.Add(this.lblWebHeight); this.grpTJoint.Controls.Add(this.numWebHeight);
            this.grpTJoint.Controls.Add(this.lblWebThickness); this.grpTJoint.Controls.Add(this.numWebThickness);
            this.grpTJoint.Controls.Add(this.lblFilletBondSize); this.grpTJoint.Controls.Add(this.numFilletBondSize);
            this.grpTJoint.Location = new System.Drawing.Point(12, 340);
            this.grpTJoint.Size = new System.Drawing.Size(440, 130);
            this.grpTJoint.Text = "T-Joint 파라미터 (mm)";
            this.grpTJoint.Visible = false;
            y = 24;
            SetupLN(this.lblFlangeLength, this.numFlangeLength, "플랜지 길이:", ref y, 100.0m, 1m, 500m);
            SetupLN(this.lblWebHeight, this.numWebHeight, "웹 높이:", ref y, 50.0m, 1m, 500m);
            SetupLN(this.lblWebThickness, this.numWebThickness, "웹 두께:", ref y, 2.0m, 0.1m, 100m);
            SetupLN(this.lblFilletBondSize, this.numFilletBondSize, "필렛 크기:", ref y, 5.0m, 0m, 50m);
            // grpOptions
            this.grpOptions.Controls.Add(this.chkCreateAdhesive);
            this.grpOptions.Controls.Add(this.chkCreateGrips);
            this.grpOptions.Location = new System.Drawing.Point(12, 476);
            this.grpOptions.Size = new System.Drawing.Size(440, 50);
            this.grpOptions.Text = "옵션";
            this.chkCreateAdhesive.AutoSize = true;
            this.chkCreateAdhesive.Checked = true;
            this.chkCreateAdhesive.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkCreateAdhesive.Location = new System.Drawing.Point(15, 22);
            this.chkCreateAdhesive.Text = "접착층 별도 바디";
            this.chkCreateGrips.AutoSize = true;
            this.chkCreateGrips.Location = new System.Drawing.Point(180, 22);
            this.chkCreateGrips.Text = "그립/지그 생성";
            // Buttons
            this.btnPreview.Location = new System.Drawing.Point(140, 540);
            this.btnPreview.Size = new System.Drawing.Size(90, 32);
            this.btnPreview.Text = "미리보기";
            this.btnPreview.Click += new System.EventHandler(this.btnPreview_Click);
            this.btnCreate.Location = new System.Drawing.Point(250, 540);
            this.btnCreate.Size = new System.Drawing.Size(90, 32);
            this.btnCreate.Text = "생성";
            this.btnCreate.Click += new System.EventHandler(this.btnCreate_Click);
            this.btnCancel.Location = new System.Drawing.Point(360, 540);
            this.btnCancel.Size = new System.Drawing.Size(90, 32);
            this.btnCancel.Text = "취소";
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // Form
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(474, 590);
            this.Controls.Add(this.grpPreset);
            this.Controls.Add(this.grpAdherend);
            this.Controls.Add(this.grpJoint);
            this.Controls.Add(this.grpTJoint);
            this.Controls.Add(this.grpOptions);
            this.Controls.Add(this.btnPreview);
            this.Controls.Add(this.btnCreate);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "JointSpecimenDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "접합부 시편 생성 (Joint Specimen)";
            this.ResumeLayout(false);
        }

        private void SetupLN(System.Windows.Forms.Label lbl, System.Windows.Forms.NumericUpDown num,
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
        private System.Windows.Forms.GroupBox grpAdherend;
        private System.Windows.Forms.NumericUpDown numAdherendWidth;
        private System.Windows.Forms.Label lblAdherendWidth;
        private System.Windows.Forms.NumericUpDown numAdherendLength;
        private System.Windows.Forms.Label lblAdherendLength;
        private System.Windows.Forms.NumericUpDown numAdherendThickness;
        private System.Windows.Forms.Label lblAdherendThickness;
        private System.Windows.Forms.GroupBox grpJoint;
        private System.Windows.Forms.NumericUpDown numOverlapLength;
        private System.Windows.Forms.Label lblOverlapLength;
        private System.Windows.Forms.NumericUpDown numAdhesiveThickness;
        private System.Windows.Forms.Label lblAdhesiveThickness;
        private System.Windows.Forms.NumericUpDown numScarfAngle;
        private System.Windows.Forms.Label lblScarfAngle;
        private System.Windows.Forms.GroupBox grpTJoint;
        private System.Windows.Forms.NumericUpDown numFlangeLength;
        private System.Windows.Forms.Label lblFlangeLength;
        private System.Windows.Forms.NumericUpDown numWebHeight;
        private System.Windows.Forms.Label lblWebHeight;
        private System.Windows.Forms.NumericUpDown numWebThickness;
        private System.Windows.Forms.Label lblWebThickness;
        private System.Windows.Forms.NumericUpDown numFilletBondSize;
        private System.Windows.Forms.Label lblFilletBondSize;
        private System.Windows.Forms.GroupBox grpOptions;
        private System.Windows.Forms.CheckBox chkCreateAdhesive;
        private System.Windows.Forms.CheckBox chkCreateGrips;
        private System.Windows.Forms.Button btnPreview;
        private System.Windows.Forms.Button btnCreate;
        private System.Windows.Forms.Button btnCancel;
    }
}
