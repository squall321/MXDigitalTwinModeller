namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    partial class CompressionSpecimenDialog
    {
        private System.ComponentModel.IContainer components = null;

        // 프리셋 그룹
        private System.Windows.Forms.GroupBox grpPreset;
        private System.Windows.Forms.ComboBox cmbPreset;
        private System.Windows.Forms.Label lblPreset;

        // 시편 치수 그룹
        private System.Windows.Forms.GroupBox grpSpecimen;
        private System.Windows.Forms.RadioButton rdoPrism;
        private System.Windows.Forms.RadioButton rdoCylinder;
        private System.Windows.Forms.Label lblWidth;
        private System.Windows.Forms.NumericUpDown numWidth;
        private System.Windows.Forms.Label lblDepth;
        private System.Windows.Forms.NumericUpDown numDepth;
        private System.Windows.Forms.Label lblHeight;
        private System.Windows.Forms.NumericUpDown numHeight;
        private System.Windows.Forms.Label lblDiameter;
        private System.Windows.Forms.NumericUpDown numDiameter;

        // 플래튼 그룹
        private System.Windows.Forms.GroupBox grpPlaten;
        private System.Windows.Forms.CheckBox chkCreatePlatens;
        private System.Windows.Forms.Label lblPlatenDia;
        private System.Windows.Forms.NumericUpDown numPlatenDia;
        private System.Windows.Forms.Label lblPlatenHeight;
        private System.Windows.Forms.NumericUpDown numPlatenHeight;

        // 버튼
        private System.Windows.Forms.Button btnPreview;
        private System.Windows.Forms.Button btnCreate;
        private System.Windows.Forms.Button btnCancel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.grpPreset = new System.Windows.Forms.GroupBox();
            this.cmbPreset = new System.Windows.Forms.ComboBox();
            this.lblPreset = new System.Windows.Forms.Label();

            this.grpSpecimen = new System.Windows.Forms.GroupBox();
            this.rdoPrism = new System.Windows.Forms.RadioButton();
            this.rdoCylinder = new System.Windows.Forms.RadioButton();
            this.lblWidth = new System.Windows.Forms.Label();
            this.numWidth = new System.Windows.Forms.NumericUpDown();
            this.lblDepth = new System.Windows.Forms.Label();
            this.numDepth = new System.Windows.Forms.NumericUpDown();
            this.lblHeight = new System.Windows.Forms.Label();
            this.numHeight = new System.Windows.Forms.NumericUpDown();
            this.lblDiameter = new System.Windows.Forms.Label();
            this.numDiameter = new System.Windows.Forms.NumericUpDown();

            this.grpPlaten = new System.Windows.Forms.GroupBox();
            this.chkCreatePlatens = new System.Windows.Forms.CheckBox();
            this.lblPlatenDia = new System.Windows.Forms.Label();
            this.numPlatenDia = new System.Windows.Forms.NumericUpDown();
            this.lblPlatenHeight = new System.Windows.Forms.Label();
            this.numPlatenHeight = new System.Windows.Forms.NumericUpDown();

            this.btnPreview = new System.Windows.Forms.Button();
            this.btnCreate = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();

            ((System.ComponentModel.ISupportInitialize)(this.numWidth)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDepth)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numHeight)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDiameter)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numPlatenDia)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numPlatenHeight)).BeginInit();
            this.grpPreset.SuspendLayout();
            this.grpSpecimen.SuspendLayout();
            this.grpPlaten.SuspendLayout();
            this.SuspendLayout();

            // ==========================================
            //  grpPreset - 프리셋 선택
            // ==========================================
            this.grpPreset.Controls.Add(this.lblPreset);
            this.grpPreset.Controls.Add(this.cmbPreset);
            this.grpPreset.Location = new System.Drawing.Point(12, 12);
            this.grpPreset.Name = "grpPreset";
            this.grpPreset.Size = new System.Drawing.Size(360, 55);
            this.grpPreset.TabIndex = 0;
            this.grpPreset.TabStop = false;
            this.grpPreset.Text = "표준 프리셋";

            this.lblPreset.AutoSize = true;
            this.lblPreset.Location = new System.Drawing.Point(15, 25);
            this.lblPreset.Text = "규격:";

            this.cmbPreset.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbPreset.Location = new System.Drawing.Point(60, 22);
            this.cmbPreset.Size = new System.Drawing.Size(280, 20);
            this.cmbPreset.TabIndex = 1;

            // ==========================================
            //  grpSpecimen - 시편 치수
            // ==========================================
            this.grpSpecimen.Controls.Add(this.rdoPrism);
            this.grpSpecimen.Controls.Add(this.rdoCylinder);
            this.grpSpecimen.Controls.Add(this.lblWidth);
            this.grpSpecimen.Controls.Add(this.numWidth);
            this.grpSpecimen.Controls.Add(this.lblDepth);
            this.grpSpecimen.Controls.Add(this.numDepth);
            this.grpSpecimen.Controls.Add(this.lblHeight);
            this.grpSpecimen.Controls.Add(this.numHeight);
            this.grpSpecimen.Controls.Add(this.lblDiameter);
            this.grpSpecimen.Controls.Add(this.numDiameter);
            this.grpSpecimen.Location = new System.Drawing.Point(12, 73);
            this.grpSpecimen.Name = "grpSpecimen";
            this.grpSpecimen.Size = new System.Drawing.Size(360, 175);
            this.grpSpecimen.TabIndex = 1;
            this.grpSpecimen.TabStop = false;
            this.grpSpecimen.Text = "시편 치수 (mm)";

            // 형상 선택 라디오
            this.rdoPrism.AutoSize = true;
            this.rdoPrism.Checked = true;
            this.rdoPrism.Location = new System.Drawing.Point(15, 22);
            this.rdoPrism.TabIndex = 2;
            this.rdoPrism.TabStop = true;
            this.rdoPrism.Text = "직육면체 (Prism)";
            this.rdoPrism.UseVisualStyleBackColor = true;
            this.rdoPrism.CheckedChanged += new System.EventHandler(this.rdoShape_CheckedChanged);

            this.rdoCylinder.AutoSize = true;
            this.rdoCylinder.Location = new System.Drawing.Point(180, 22);
            this.rdoCylinder.TabIndex = 3;
            this.rdoCylinder.Text = "원기둥 (Cylinder)";
            this.rdoCylinder.UseVisualStyleBackColor = true;

            int dy = 50;
            int gap = 30;
            int lblX = 15;
            int numX = 130;

            // 폭
            this.lblWidth.AutoSize = true;
            this.lblWidth.Location = new System.Drawing.Point(lblX, dy);
            this.lblWidth.Text = "폭 (Width):";

            this.numWidth.DecimalPlaces = 2;
            this.numWidth.Location = new System.Drawing.Point(numX, dy - 3);
            this.numWidth.Maximum = new decimal(1000);
            this.numWidth.Minimum = new decimal(new int[] { 1, 0, 0, 131072 }); // 0.01
            this.numWidth.Size = new System.Drawing.Size(100, 21);
            this.numWidth.TabIndex = 4;
            this.numWidth.Value = new decimal(new int[] { 127, 0, 0, 65536 }); // 12.7
            dy += gap;

            // 깊이
            this.lblDepth.AutoSize = true;
            this.lblDepth.Location = new System.Drawing.Point(lblX, dy);
            this.lblDepth.Text = "깊이 (Depth):";

            this.numDepth.DecimalPlaces = 2;
            this.numDepth.Location = new System.Drawing.Point(numX, dy - 3);
            this.numDepth.Maximum = new decimal(1000);
            this.numDepth.Minimum = new decimal(new int[] { 1, 0, 0, 131072 });
            this.numDepth.Size = new System.Drawing.Size(100, 21);
            this.numDepth.TabIndex = 5;
            this.numDepth.Value = new decimal(new int[] { 127, 0, 0, 65536 }); // 12.7
            dy += gap;

            // 직경 (원기둥 모드)
            this.lblDiameter.AutoSize = true;
            this.lblDiameter.Location = new System.Drawing.Point(lblX, 50);
            this.lblDiameter.Text = "직경 (Diameter):";
            this.lblDiameter.Visible = false;

            this.numDiameter.DecimalPlaces = 2;
            this.numDiameter.Location = new System.Drawing.Point(numX, 47);
            this.numDiameter.Maximum = new decimal(1000);
            this.numDiameter.Minimum = new decimal(new int[] { 1, 0, 0, 131072 });
            this.numDiameter.Size = new System.Drawing.Size(100, 21);
            this.numDiameter.TabIndex = 6;
            this.numDiameter.Value = new decimal(new int[] { 127, 0, 0, 65536 }); // 12.7
            this.numDiameter.Visible = false;

            // 높이 (공통)
            this.lblHeight.AutoSize = true;
            this.lblHeight.Location = new System.Drawing.Point(lblX, dy);
            this.lblHeight.Text = "높이 (Height):";

            this.numHeight.DecimalPlaces = 2;
            this.numHeight.Location = new System.Drawing.Point(numX, dy - 3);
            this.numHeight.Maximum = new decimal(1000);
            this.numHeight.Minimum = new decimal(new int[] { 1, 0, 0, 131072 });
            this.numHeight.Size = new System.Drawing.Size(100, 21);
            this.numHeight.TabIndex = 7;
            this.numHeight.Value = new decimal(new int[] { 254, 0, 0, 65536 }); // 25.4

            // ==========================================
            //  grpPlaten - 압축판
            // ==========================================
            this.grpPlaten.Controls.Add(this.chkCreatePlatens);
            this.grpPlaten.Controls.Add(this.lblPlatenDia);
            this.grpPlaten.Controls.Add(this.numPlatenDia);
            this.grpPlaten.Controls.Add(this.lblPlatenHeight);
            this.grpPlaten.Controls.Add(this.numPlatenHeight);
            this.grpPlaten.Location = new System.Drawing.Point(12, 254);
            this.grpPlaten.Name = "grpPlaten";
            this.grpPlaten.Size = new System.Drawing.Size(360, 100);
            this.grpPlaten.TabIndex = 2;
            this.grpPlaten.TabStop = false;
            this.grpPlaten.Text = "압축판 (Platen)";

            this.chkCreatePlatens.AutoSize = true;
            this.chkCreatePlatens.Checked = true;
            this.chkCreatePlatens.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkCreatePlatens.Location = new System.Drawing.Point(15, 25);
            this.chkCreatePlatens.TabIndex = 8;
            this.chkCreatePlatens.Text = "플래튼 생성";
            this.chkCreatePlatens.UseVisualStyleBackColor = true;
            this.chkCreatePlatens.CheckedChanged += new System.EventHandler(this.chkCreatePlatens_CheckedChanged);

            this.lblPlatenDia.AutoSize = true;
            this.lblPlatenDia.Location = new System.Drawing.Point(15, 55);
            this.lblPlatenDia.Text = "플래튼 직경:";

            this.numPlatenDia.DecimalPlaces = 1;
            this.numPlatenDia.Location = new System.Drawing.Point(130, 52);
            this.numPlatenDia.Maximum = new decimal(500);
            this.numPlatenDia.Minimum = new decimal(1);
            this.numPlatenDia.Size = new System.Drawing.Size(100, 21);
            this.numPlatenDia.TabIndex = 9;
            this.numPlatenDia.Value = new decimal(50);

            this.lblPlatenHeight.AutoSize = true;
            this.lblPlatenHeight.Location = new System.Drawing.Point(15, 80);
            this.lblPlatenHeight.Text = "플래튼 높이:";

            this.numPlatenHeight.DecimalPlaces = 1;
            this.numPlatenHeight.Location = new System.Drawing.Point(130, 77);
            this.numPlatenHeight.Maximum = new decimal(200);
            this.numPlatenHeight.Minimum = new decimal(1);
            this.numPlatenHeight.Size = new System.Drawing.Size(100, 21);
            this.numPlatenHeight.TabIndex = 10;
            this.numPlatenHeight.Value = new decimal(20);

            // ==========================================
            //  버튼
            // ==========================================
            int btnY = 365;

            this.btnPreview.Location = new System.Drawing.Point(50, btnY);
            this.btnPreview.Size = new System.Drawing.Size(90, 30);
            this.btnPreview.TabIndex = 11;
            this.btnPreview.Text = "미리보기";
            this.btnPreview.UseVisualStyleBackColor = true;
            this.btnPreview.Click += new System.EventHandler(this.btnPreview_Click);

            this.btnCreate.Location = new System.Drawing.Point(150, btnY);
            this.btnCreate.Size = new System.Drawing.Size(90, 30);
            this.btnCreate.TabIndex = 12;
            this.btnCreate.Text = "생성";
            this.btnCreate.UseVisualStyleBackColor = true;
            this.btnCreate.Click += new System.EventHandler(this.btnCreate_Click);

            this.btnCancel.Location = new System.Drawing.Point(250, btnY);
            this.btnCancel.Size = new System.Drawing.Size(90, 30);
            this.btnCancel.TabIndex = 13;
            this.btnCancel.Text = "취소";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);

            // ==========================================
            //  CompressionSpecimenDialog
            // ==========================================
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(384, 410);
            this.Controls.Add(this.grpPreset);
            this.Controls.Add(this.grpSpecimen);
            this.Controls.Add(this.grpPlaten);
            this.Controls.Add(this.btnPreview);
            this.Controls.Add(this.btnCreate);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CompressionSpecimenDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "압축 시편 생성 (Compression Specimen)";

            ((System.ComponentModel.ISupportInitialize)(this.numWidth)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDepth)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numHeight)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDiameter)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numPlatenDia)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numPlatenHeight)).EndInit();
            this.grpPreset.ResumeLayout(false);
            this.grpPreset.PerformLayout();
            this.grpSpecimen.ResumeLayout(false);
            this.grpSpecimen.PerformLayout();
            this.grpPlaten.ResumeLayout(false);
            this.grpPlaten.PerformLayout();
            this.ResumeLayout(false);
        }
    }
}
