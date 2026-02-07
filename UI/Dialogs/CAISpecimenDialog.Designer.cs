namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    partial class CAISpecimenDialog
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
            this.grpPanel = new System.Windows.Forms.GroupBox();
            this.numThickness = new System.Windows.Forms.NumericUpDown();
            this.lblThickness = new System.Windows.Forms.Label();
            this.numPanelWidth = new System.Windows.Forms.NumericUpDown();
            this.lblPanelWidth = new System.Windows.Forms.Label();
            this.numPanelLength = new System.Windows.Forms.NumericUpDown();
            this.lblPanelLength = new System.Windows.Forms.Label();
            this.grpJig = new System.Windows.Forms.GroupBox();
            this.numJigClearance = new System.Windows.Forms.NumericUpDown();
            this.lblJigClearance = new System.Windows.Forms.Label();
            this.numWindowWidth = new System.Windows.Forms.NumericUpDown();
            this.lblWindowWidth = new System.Windows.Forms.Label();
            this.numWindowLength = new System.Windows.Forms.NumericUpDown();
            this.lblWindowLength = new System.Windows.Forms.Label();
            this.numJigThickness = new System.Windows.Forms.NumericUpDown();
            this.lblJigThickness = new System.Windows.Forms.Label();
            this.chkCreateJig = new System.Windows.Forms.CheckBox();
            this.grpDamage = new System.Windows.Forms.GroupBox();
            this.numDamageDepth = new System.Windows.Forms.NumericUpDown();
            this.lblDamageDepth = new System.Windows.Forms.Label();
            this.numDamageMinorAxis = new System.Windows.Forms.NumericUpDown();
            this.lblDamageMinorAxis = new System.Windows.Forms.Label();
            this.numDamageMajorAxis = new System.Windows.Forms.NumericUpDown();
            this.lblDamageMajorAxis = new System.Windows.Forms.Label();
            this.numDamageDiameter = new System.Windows.Forms.NumericUpDown();
            this.lblDamageDiameter = new System.Windows.Forms.Label();
            this.rdoElliptical = new System.Windows.Forms.RadioButton();
            this.rdoCircular = new System.Windows.Forms.RadioButton();
            this.chkCreateDamage = new System.Windows.Forms.CheckBox();
            this.btnPreview = new System.Windows.Forms.Button();
            this.btnCreate = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.grpPreset.SuspendLayout();
            this.grpPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numThickness)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numPanelWidth)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numPanelLength)).BeginInit();
            this.grpJig.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numJigClearance)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numWindowWidth)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numWindowLength)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numJigThickness)).BeginInit();
            this.grpDamage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numDamageDepth)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDamageMinorAxis)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDamageMajorAxis)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDamageDiameter)).BeginInit();
            this.SuspendLayout();
            //
            // grpPreset
            //
            this.grpPreset.Controls.Add(this.lblDescription);
            this.grpPreset.Controls.Add(this.cmbPreset);
            this.grpPreset.Controls.Add(this.lblPreset);
            this.grpPreset.Location = new System.Drawing.Point(12, 12);
            this.grpPreset.Name = "grpPreset";
            this.grpPreset.Size = new System.Drawing.Size(440, 90);
            this.grpPreset.TabIndex = 0;
            this.grpPreset.TabStop = false;
            this.grpPreset.Text = "프리셋";
            //
            // lblPreset
            //
            this.lblPreset.AutoSize = true;
            this.lblPreset.Location = new System.Drawing.Point(15, 28);
            this.lblPreset.Name = "lblPreset";
            this.lblPreset.Size = new System.Drawing.Size(35, 12);
            this.lblPreset.Text = "규격:";
            //
            // cmbPreset
            //
            this.cmbPreset.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbPreset.Location = new System.Drawing.Point(80, 25);
            this.cmbPreset.Name = "cmbPreset";
            this.cmbPreset.Size = new System.Drawing.Size(340, 20);
            this.cmbPreset.TabIndex = 1;
            //
            // lblDescription
            //
            this.lblDescription.AutoSize = true;
            this.lblDescription.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblDescription.Location = new System.Drawing.Point(15, 58);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new System.Drawing.Size(200, 12);
            this.lblDescription.Text = "";
            //
            // grpPanel
            //
            this.grpPanel.Controls.Add(this.lblPanelLength);
            this.grpPanel.Controls.Add(this.numPanelLength);
            this.grpPanel.Controls.Add(this.lblPanelWidth);
            this.grpPanel.Controls.Add(this.numPanelWidth);
            this.grpPanel.Controls.Add(this.lblThickness);
            this.grpPanel.Controls.Add(this.numThickness);
            this.grpPanel.Location = new System.Drawing.Point(12, 108);
            this.grpPanel.Name = "grpPanel";
            this.grpPanel.Size = new System.Drawing.Size(440, 110);
            this.grpPanel.TabIndex = 1;
            this.grpPanel.TabStop = false;
            this.grpPanel.Text = "패널 치수 (mm)";
            //
            // lblPanelLength
            //
            this.lblPanelLength.AutoSize = true;
            this.lblPanelLength.Location = new System.Drawing.Point(15, 28);
            this.lblPanelLength.Name = "lblPanelLength";
            this.lblPanelLength.Text = "길이 (Length):";
            //
            // numPanelLength
            //
            this.numPanelLength.DecimalPlaces = 1;
            this.numPanelLength.Location = new System.Drawing.Point(140, 26);
            this.numPanelLength.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            this.numPanelLength.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numPanelLength.Name = "numPanelLength";
            this.numPanelLength.Size = new System.Drawing.Size(120, 21);
            this.numPanelLength.Value = new decimal(new int[] { 150, 0, 0, 0 });
            //
            // lblPanelWidth
            //
            this.lblPanelWidth.AutoSize = true;
            this.lblPanelWidth.Location = new System.Drawing.Point(15, 55);
            this.lblPanelWidth.Name = "lblPanelWidth";
            this.lblPanelWidth.Text = "폭 (Width):";
            //
            // numPanelWidth
            //
            this.numPanelWidth.DecimalPlaces = 1;
            this.numPanelWidth.Location = new System.Drawing.Point(140, 53);
            this.numPanelWidth.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            this.numPanelWidth.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numPanelWidth.Name = "numPanelWidth";
            this.numPanelWidth.Size = new System.Drawing.Size(120, 21);
            this.numPanelWidth.Value = new decimal(new int[] { 100, 0, 0, 0 });
            //
            // lblThickness
            //
            this.lblThickness.AutoSize = true;
            this.lblThickness.Location = new System.Drawing.Point(15, 82);
            this.lblThickness.Name = "lblThickness";
            this.lblThickness.Text = "두께 (Thickness):";
            //
            // numThickness
            //
            this.numThickness.DecimalPlaces = 1;
            this.numThickness.Location = new System.Drawing.Point(140, 80);
            this.numThickness.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
            this.numThickness.Minimum = new decimal(new int[] { 1, 0, 0, 131072 });
            this.numThickness.Name = "numThickness";
            this.numThickness.Size = new System.Drawing.Size(120, 21);
            this.numThickness.Value = new decimal(new int[] { 4, 0, 0, 0 });
            //
            // grpJig
            //
            this.grpJig.Controls.Add(this.chkCreateJig);
            this.grpJig.Controls.Add(this.lblJigThickness);
            this.grpJig.Controls.Add(this.numJigThickness);
            this.grpJig.Controls.Add(this.lblWindowLength);
            this.grpJig.Controls.Add(this.numWindowLength);
            this.grpJig.Controls.Add(this.lblWindowWidth);
            this.grpJig.Controls.Add(this.numWindowWidth);
            this.grpJig.Controls.Add(this.lblJigClearance);
            this.grpJig.Controls.Add(this.numJigClearance);
            this.grpJig.Location = new System.Drawing.Point(12, 224);
            this.grpJig.Name = "grpJig";
            this.grpJig.Size = new System.Drawing.Size(440, 155);
            this.grpJig.TabIndex = 2;
            this.grpJig.TabStop = false;
            this.grpJig.Text = "Anti-Buckling Guide (지그)";
            //
            // chkCreateJig
            //
            this.chkCreateJig.AutoSize = true;
            this.chkCreateJig.Checked = true;
            this.chkCreateJig.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkCreateJig.Location = new System.Drawing.Point(15, 24);
            this.chkCreateJig.Name = "chkCreateJig";
            this.chkCreateJig.Size = new System.Drawing.Size(87, 16);
            this.chkCreateJig.Text = "지그 생성";
            this.chkCreateJig.CheckedChanged += new System.EventHandler(this.chkCreateJig_CheckedChanged);
            //
            // lblJigThickness
            //
            this.lblJigThickness.AutoSize = true;
            this.lblJigThickness.Location = new System.Drawing.Point(15, 50);
            this.lblJigThickness.Name = "lblJigThickness";
            this.lblJigThickness.Text = "지그 두께:";
            //
            // numJigThickness
            //
            this.numJigThickness.DecimalPlaces = 1;
            this.numJigThickness.Location = new System.Drawing.Point(140, 48);
            this.numJigThickness.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
            this.numJigThickness.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numJigThickness.Name = "numJigThickness";
            this.numJigThickness.Size = new System.Drawing.Size(120, 21);
            this.numJigThickness.Value = new decimal(new int[] { 10, 0, 0, 0 });
            //
            // lblWindowLength
            //
            this.lblWindowLength.AutoSize = true;
            this.lblWindowLength.Location = new System.Drawing.Point(15, 77);
            this.lblWindowLength.Name = "lblWindowLength";
            this.lblWindowLength.Text = "윈도우 길이:";
            //
            // numWindowLength
            //
            this.numWindowLength.DecimalPlaces = 1;
            this.numWindowLength.Location = new System.Drawing.Point(140, 75);
            this.numWindowLength.Maximum = new decimal(new int[] { 500, 0, 0, 0 });
            this.numWindowLength.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numWindowLength.Name = "numWindowLength";
            this.numWindowLength.Size = new System.Drawing.Size(120, 21);
            this.numWindowLength.Value = new decimal(new int[] { 75, 0, 0, 0 });
            //
            // lblWindowWidth
            //
            this.lblWindowWidth.AutoSize = true;
            this.lblWindowWidth.Location = new System.Drawing.Point(15, 104);
            this.lblWindowWidth.Name = "lblWindowWidth";
            this.lblWindowWidth.Text = "윈도우 폭:";
            //
            // numWindowWidth
            //
            this.numWindowWidth.DecimalPlaces = 1;
            this.numWindowWidth.Location = new System.Drawing.Point(140, 102);
            this.numWindowWidth.Maximum = new decimal(new int[] { 500, 0, 0, 0 });
            this.numWindowWidth.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numWindowWidth.Name = "numWindowWidth";
            this.numWindowWidth.Size = new System.Drawing.Size(120, 21);
            this.numWindowWidth.Value = new decimal(new int[] { 50, 0, 0, 0 });
            //
            // lblJigClearance
            //
            this.lblJigClearance.AutoSize = true;
            this.lblJigClearance.Location = new System.Drawing.Point(15, 131);
            this.lblJigClearance.Name = "lblJigClearance";
            this.lblJigClearance.Text = "지그 간격:";
            //
            // numJigClearance
            //
            this.numJigClearance.DecimalPlaces = 1;
            this.numJigClearance.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            this.numJigClearance.Location = new System.Drawing.Point(140, 129);
            this.numJigClearance.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            this.numJigClearance.Minimum = new decimal(new int[] { 0, 0, 0, 0 });
            this.numJigClearance.Name = "numJigClearance";
            this.numJigClearance.Size = new System.Drawing.Size(120, 21);
            this.numJigClearance.Value = new decimal(new int[] { 5, 0, 0, 65536 });
            //
            // grpDamage
            //
            this.grpDamage.Controls.Add(this.chkCreateDamage);
            this.grpDamage.Controls.Add(this.rdoCircular);
            this.grpDamage.Controls.Add(this.rdoElliptical);
            this.grpDamage.Controls.Add(this.lblDamageDiameter);
            this.grpDamage.Controls.Add(this.numDamageDiameter);
            this.grpDamage.Controls.Add(this.lblDamageMajorAxis);
            this.grpDamage.Controls.Add(this.numDamageMajorAxis);
            this.grpDamage.Controls.Add(this.lblDamageMinorAxis);
            this.grpDamage.Controls.Add(this.numDamageMinorAxis);
            this.grpDamage.Controls.Add(this.lblDamageDepth);
            this.grpDamage.Controls.Add(this.numDamageDepth);
            this.grpDamage.Location = new System.Drawing.Point(12, 385);
            this.grpDamage.Name = "grpDamage";
            this.grpDamage.Size = new System.Drawing.Size(440, 175);
            this.grpDamage.TabIndex = 3;
            this.grpDamage.TabStop = false;
            this.grpDamage.Text = "손상 영역 (Damage Zone)";
            //
            // chkCreateDamage
            //
            this.chkCreateDamage.AutoSize = true;
            this.chkCreateDamage.Checked = true;
            this.chkCreateDamage.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkCreateDamage.Location = new System.Drawing.Point(15, 24);
            this.chkCreateDamage.Name = "chkCreateDamage";
            this.chkCreateDamage.Size = new System.Drawing.Size(119, 16);
            this.chkCreateDamage.Text = "손상 영역 생성";
            this.chkCreateDamage.CheckedChanged += new System.EventHandler(this.chkCreateDamage_CheckedChanged);
            //
            // rdoCircular
            //
            this.rdoCircular.AutoSize = true;
            this.rdoCircular.Checked = true;
            this.rdoCircular.Location = new System.Drawing.Point(15, 48);
            this.rdoCircular.Name = "rdoCircular";
            this.rdoCircular.Size = new System.Drawing.Size(55, 16);
            this.rdoCircular.TabStop = true;
            this.rdoCircular.Text = "원형";
            this.rdoCircular.CheckedChanged += new System.EventHandler(this.rdoDamageShape_CheckedChanged);
            //
            // rdoElliptical
            //
            this.rdoElliptical.AutoSize = true;
            this.rdoElliptical.Location = new System.Drawing.Point(100, 48);
            this.rdoElliptical.Name = "rdoElliptical";
            this.rdoElliptical.Size = new System.Drawing.Size(67, 16);
            this.rdoElliptical.Text = "타원형";
            //
            // lblDamageDiameter
            //
            this.lblDamageDiameter.AutoSize = true;
            this.lblDamageDiameter.Location = new System.Drawing.Point(15, 75);
            this.lblDamageDiameter.Name = "lblDamageDiameter";
            this.lblDamageDiameter.Text = "직경:";
            //
            // numDamageDiameter
            //
            this.numDamageDiameter.DecimalPlaces = 1;
            this.numDamageDiameter.Location = new System.Drawing.Point(140, 73);
            this.numDamageDiameter.Maximum = new decimal(new int[] { 200, 0, 0, 0 });
            this.numDamageDiameter.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numDamageDiameter.Name = "numDamageDiameter";
            this.numDamageDiameter.Size = new System.Drawing.Size(120, 21);
            this.numDamageDiameter.Value = new decimal(new int[] { 25, 0, 0, 0 });
            //
            // lblDamageMajorAxis
            //
            this.lblDamageMajorAxis.AutoSize = true;
            this.lblDamageMajorAxis.Location = new System.Drawing.Point(15, 75);
            this.lblDamageMajorAxis.Name = "lblDamageMajorAxis";
            this.lblDamageMajorAxis.Text = "장축:";
            this.lblDamageMajorAxis.Visible = false;
            //
            // numDamageMajorAxis
            //
            this.numDamageMajorAxis.DecimalPlaces = 1;
            this.numDamageMajorAxis.Location = new System.Drawing.Point(140, 73);
            this.numDamageMajorAxis.Maximum = new decimal(new int[] { 200, 0, 0, 0 });
            this.numDamageMajorAxis.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numDamageMajorAxis.Name = "numDamageMajorAxis";
            this.numDamageMajorAxis.Size = new System.Drawing.Size(120, 21);
            this.numDamageMajorAxis.Value = new decimal(new int[] { 30, 0, 0, 0 });
            this.numDamageMajorAxis.Visible = false;
            //
            // lblDamageMinorAxis
            //
            this.lblDamageMinorAxis.AutoSize = true;
            this.lblDamageMinorAxis.Location = new System.Drawing.Point(15, 102);
            this.lblDamageMinorAxis.Name = "lblDamageMinorAxis";
            this.lblDamageMinorAxis.Text = "단축:";
            this.lblDamageMinorAxis.Visible = false;
            //
            // numDamageMinorAxis
            //
            this.numDamageMinorAxis.DecimalPlaces = 1;
            this.numDamageMinorAxis.Location = new System.Drawing.Point(140, 100);
            this.numDamageMinorAxis.Maximum = new decimal(new int[] { 200, 0, 0, 0 });
            this.numDamageMinorAxis.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numDamageMinorAxis.Name = "numDamageMinorAxis";
            this.numDamageMinorAxis.Size = new System.Drawing.Size(120, 21);
            this.numDamageMinorAxis.Value = new decimal(new int[] { 20, 0, 0, 0 });
            this.numDamageMinorAxis.Visible = false;
            //
            // lblDamageDepth
            //
            this.lblDamageDepth.AutoSize = true;
            this.lblDamageDepth.Location = new System.Drawing.Point(15, 145);
            this.lblDamageDepth.Name = "lblDamageDepth";
            this.lblDamageDepth.Text = "깊이 비율(%):";
            //
            // numDamageDepth
            //
            this.numDamageDepth.Location = new System.Drawing.Point(140, 143);
            this.numDamageDepth.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
            this.numDamageDepth.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numDamageDepth.Name = "numDamageDepth";
            this.numDamageDepth.Size = new System.Drawing.Size(120, 21);
            this.numDamageDepth.Value = new decimal(new int[] { 50, 0, 0, 0 });
            //
            // btnPreview
            //
            this.btnPreview.Location = new System.Drawing.Point(140, 575);
            this.btnPreview.Name = "btnPreview";
            this.btnPreview.Size = new System.Drawing.Size(90, 32);
            this.btnPreview.TabIndex = 10;
            this.btnPreview.Text = "미리보기";
            this.btnPreview.UseVisualStyleBackColor = true;
            this.btnPreview.Click += new System.EventHandler(this.btnPreview_Click);
            //
            // btnCreate
            //
            this.btnCreate.Location = new System.Drawing.Point(250, 575);
            this.btnCreate.Name = "btnCreate";
            this.btnCreate.Size = new System.Drawing.Size(90, 32);
            this.btnCreate.TabIndex = 11;
            this.btnCreate.Text = "생성";
            this.btnCreate.UseVisualStyleBackColor = true;
            this.btnCreate.Click += new System.EventHandler(this.btnCreate_Click);
            //
            // btnCancel
            //
            this.btnCancel.Location = new System.Drawing.Point(360, 575);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 32);
            this.btnCancel.TabIndex = 12;
            this.btnCancel.Text = "취소";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            //
            // CAISpecimenDialog
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(474, 620);
            this.Controls.Add(this.grpPreset);
            this.Controls.Add(this.grpPanel);
            this.Controls.Add(this.grpJig);
            this.Controls.Add(this.grpDamage);
            this.Controls.Add(this.btnPreview);
            this.Controls.Add(this.btnCreate);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CAISpecimenDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "CAI 시편 생성 (Compression After Impact)";
            this.grpPreset.ResumeLayout(false);
            this.grpPreset.PerformLayout();
            this.grpPanel.ResumeLayout(false);
            this.grpPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numThickness)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numPanelWidth)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numPanelLength)).EndInit();
            this.grpJig.ResumeLayout(false);
            this.grpJig.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numJigClearance)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numWindowWidth)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numWindowLength)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numJigThickness)).EndInit();
            this.grpDamage.ResumeLayout(false);
            this.grpDamage.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numDamageDepth)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDamageMinorAxis)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDamageMajorAxis)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDamageDiameter)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.GroupBox grpPreset;
        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.ComboBox cmbPreset;
        private System.Windows.Forms.Label lblPreset;
        private System.Windows.Forms.GroupBox grpPanel;
        private System.Windows.Forms.NumericUpDown numThickness;
        private System.Windows.Forms.Label lblThickness;
        private System.Windows.Forms.NumericUpDown numPanelWidth;
        private System.Windows.Forms.Label lblPanelWidth;
        private System.Windows.Forms.NumericUpDown numPanelLength;
        private System.Windows.Forms.Label lblPanelLength;
        private System.Windows.Forms.GroupBox grpJig;
        private System.Windows.Forms.NumericUpDown numJigClearance;
        private System.Windows.Forms.Label lblJigClearance;
        private System.Windows.Forms.NumericUpDown numWindowWidth;
        private System.Windows.Forms.Label lblWindowWidth;
        private System.Windows.Forms.NumericUpDown numWindowLength;
        private System.Windows.Forms.Label lblWindowLength;
        private System.Windows.Forms.NumericUpDown numJigThickness;
        private System.Windows.Forms.Label lblJigThickness;
        private System.Windows.Forms.CheckBox chkCreateJig;
        private System.Windows.Forms.GroupBox grpDamage;
        private System.Windows.Forms.NumericUpDown numDamageDepth;
        private System.Windows.Forms.Label lblDamageDepth;
        private System.Windows.Forms.NumericUpDown numDamageMinorAxis;
        private System.Windows.Forms.Label lblDamageMinorAxis;
        private System.Windows.Forms.NumericUpDown numDamageMajorAxis;
        private System.Windows.Forms.Label lblDamageMajorAxis;
        private System.Windows.Forms.NumericUpDown numDamageDiameter;
        private System.Windows.Forms.Label lblDamageDiameter;
        private System.Windows.Forms.RadioButton rdoElliptical;
        private System.Windows.Forms.RadioButton rdoCircular;
        private System.Windows.Forms.CheckBox chkCreateDamage;
        private System.Windows.Forms.Button btnPreview;
        private System.Windows.Forms.Button btnCreate;
        private System.Windows.Forms.Button btnCancel;
    }
}
