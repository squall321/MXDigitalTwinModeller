namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    partial class ApplyBendingFixtureDialog
    {
        private System.ComponentModel.IContainer components = null;

        // 대상 바디 그룹
        private System.Windows.Forms.GroupBox grpBody;
        private System.Windows.Forms.Label lblBody;
        private System.Windows.Forms.ComboBox cmbBody;
        private System.Windows.Forms.Label lblBboxInfo;

        // 방향 설정 그룹
        private System.Windows.Forms.GroupBox grpDirection;
        private System.Windows.Forms.Label lblSpanDir;
        private System.Windows.Forms.ComboBox cmbSpanDir;
        private System.Windows.Forms.Label lblSpanDim;
        private System.Windows.Forms.Label lblWidthDir;
        private System.Windows.Forms.ComboBox cmbWidthDir;
        private System.Windows.Forms.Label lblWidthDim;
        private System.Windows.Forms.Label lblLoadDir;
        private System.Windows.Forms.ComboBox cmbLoadDir;
        private System.Windows.Forms.Label lblLoadDim;
        private System.Windows.Forms.Button btnAutoDetect;

        // 지지구조 치수 그룹
        private System.Windows.Forms.GroupBox grpFixture;
        private System.Windows.Forms.RadioButton radSpanRatio;
        private System.Windows.Forms.RadioButton radSpanAbsolute;
        private System.Windows.Forms.NumericUpDown numSpanRatio;
        private System.Windows.Forms.Label lblSpanRatioResult;
        private System.Windows.Forms.NumericUpDown numSpanAbsolute;
        private System.Windows.Forms.Label lblSupportDia;
        private System.Windows.Forms.NumericUpDown numSupportDia;
        private System.Windows.Forms.Label lblNoseDia;
        private System.Windows.Forms.NumericUpDown numNoseDia;
        private System.Windows.Forms.Label lblSupportHeight;
        private System.Windows.Forms.NumericUpDown numSupportHeight;
        private System.Windows.Forms.Label lblNoseHeight;
        private System.Windows.Forms.NumericUpDown numNoseHeight;

        // 버튼
        private System.Windows.Forms.Button btnPreview;
        private System.Windows.Forms.Button btnCreate;
        private System.Windows.Forms.Button btnCancel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.grpBody = new System.Windows.Forms.GroupBox();
            this.lblBody = new System.Windows.Forms.Label();
            this.cmbBody = new System.Windows.Forms.ComboBox();
            this.lblBboxInfo = new System.Windows.Forms.Label();

            this.grpDirection = new System.Windows.Forms.GroupBox();
            this.lblSpanDir = new System.Windows.Forms.Label();
            this.cmbSpanDir = new System.Windows.Forms.ComboBox();
            this.lblSpanDim = new System.Windows.Forms.Label();
            this.lblWidthDir = new System.Windows.Forms.Label();
            this.cmbWidthDir = new System.Windows.Forms.ComboBox();
            this.lblWidthDim = new System.Windows.Forms.Label();
            this.lblLoadDir = new System.Windows.Forms.Label();
            this.cmbLoadDir = new System.Windows.Forms.ComboBox();
            this.lblLoadDim = new System.Windows.Forms.Label();
            this.btnAutoDetect = new System.Windows.Forms.Button();

            this.grpFixture = new System.Windows.Forms.GroupBox();
            this.radSpanRatio = new System.Windows.Forms.RadioButton();
            this.radSpanAbsolute = new System.Windows.Forms.RadioButton();
            this.numSpanRatio = new System.Windows.Forms.NumericUpDown();
            this.lblSpanRatioResult = new System.Windows.Forms.Label();
            this.numSpanAbsolute = new System.Windows.Forms.NumericUpDown();
            this.lblSupportDia = new System.Windows.Forms.Label();
            this.numSupportDia = new System.Windows.Forms.NumericUpDown();
            this.lblNoseDia = new System.Windows.Forms.Label();
            this.numNoseDia = new System.Windows.Forms.NumericUpDown();
            this.lblSupportHeight = new System.Windows.Forms.Label();
            this.numSupportHeight = new System.Windows.Forms.NumericUpDown();
            this.lblNoseHeight = new System.Windows.Forms.Label();
            this.numNoseHeight = new System.Windows.Forms.NumericUpDown();

            this.btnPreview = new System.Windows.Forms.Button();
            this.btnCreate = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();

            ((System.ComponentModel.ISupportInitialize)(this.numSpanRatio)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSpanAbsolute)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSupportDia)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numNoseDia)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSupportHeight)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numNoseHeight)).BeginInit();
            this.grpBody.SuspendLayout();
            this.grpDirection.SuspendLayout();
            this.grpFixture.SuspendLayout();
            this.SuspendLayout();

            // ==========================================
            //  grpBody - 대상 바디
            // ==========================================
            this.grpBody.Controls.Add(this.lblBody);
            this.grpBody.Controls.Add(this.cmbBody);
            this.grpBody.Controls.Add(this.lblBboxInfo);
            this.grpBody.Location = new System.Drawing.Point(12, 12);
            this.grpBody.Name = "grpBody";
            this.grpBody.Size = new System.Drawing.Size(440, 75);
            this.grpBody.TabIndex = 0;
            this.grpBody.TabStop = false;
            this.grpBody.Text = "대상 바디";

            // lblBody
            this.lblBody.AutoSize = true;
            this.lblBody.Location = new System.Drawing.Point(15, 25);
            this.lblBody.Name = "lblBody";
            this.lblBody.Size = new System.Drawing.Size(60, 12);
            this.lblBody.TabIndex = 0;
            this.lblBody.Text = "바디 선택:";

            // cmbBody
            this.cmbBody.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbBody.FormattingEnabled = true;
            this.cmbBody.Location = new System.Drawing.Point(80, 22);
            this.cmbBody.Name = "cmbBody";
            this.cmbBody.Size = new System.Drawing.Size(340, 20);
            this.cmbBody.TabIndex = 1;

            // lblBboxInfo
            this.lblBboxInfo.AutoSize = true;
            this.lblBboxInfo.ForeColor = System.Drawing.Color.Blue;
            this.lblBboxInfo.Location = new System.Drawing.Point(15, 52);
            this.lblBboxInfo.Name = "lblBboxInfo";
            this.lblBboxInfo.Size = new System.Drawing.Size(200, 12);
            this.lblBboxInfo.TabIndex = 2;
            this.lblBboxInfo.Text = "바디를 선택하세요";

            // ==========================================
            //  grpDirection - 방향 설정
            // ==========================================
            this.grpDirection.Controls.Add(this.lblSpanDir);
            this.grpDirection.Controls.Add(this.cmbSpanDir);
            this.grpDirection.Controls.Add(this.lblSpanDim);
            this.grpDirection.Controls.Add(this.lblWidthDir);
            this.grpDirection.Controls.Add(this.cmbWidthDir);
            this.grpDirection.Controls.Add(this.lblWidthDim);
            this.grpDirection.Controls.Add(this.lblLoadDir);
            this.grpDirection.Controls.Add(this.cmbLoadDir);
            this.grpDirection.Controls.Add(this.lblLoadDim);
            this.grpDirection.Controls.Add(this.btnAutoDetect);
            this.grpDirection.Location = new System.Drawing.Point(12, 93);
            this.grpDirection.Name = "grpDirection";
            this.grpDirection.Size = new System.Drawing.Size(440, 135);
            this.grpDirection.TabIndex = 1;
            this.grpDirection.TabStop = false;
            this.grpDirection.Text = "방향 설정";

            int dy = 25;
            int dyGap = 30;

            // 스팬 방향
            this.lblSpanDir.AutoSize = true;
            this.lblSpanDir.Location = new System.Drawing.Point(15, dy);
            this.lblSpanDir.Name = "lblSpanDir";
            this.lblSpanDir.Text = "스팬 방향:";

            this.cmbSpanDir.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbSpanDir.Items.AddRange(new object[] { "X", "Y", "Z" });
            this.cmbSpanDir.Location = new System.Drawing.Point(90, dy - 3);
            this.cmbSpanDir.Name = "cmbSpanDir";
            this.cmbSpanDir.Size = new System.Drawing.Size(60, 20);
            this.cmbSpanDir.TabIndex = 3;

            this.lblSpanDim.AutoSize = true;
            this.lblSpanDim.Location = new System.Drawing.Point(160, dy);
            this.lblSpanDim.Name = "lblSpanDim";
            this.lblSpanDim.Text = "";
            dy += dyGap;

            // 폭 방향
            this.lblWidthDir.AutoSize = true;
            this.lblWidthDir.Location = new System.Drawing.Point(15, dy);
            this.lblWidthDir.Name = "lblWidthDir";
            this.lblWidthDir.Text = "폭 방향:";

            this.cmbWidthDir.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbWidthDir.Items.AddRange(new object[] { "X", "Y", "Z" });
            this.cmbWidthDir.Location = new System.Drawing.Point(90, dy - 3);
            this.cmbWidthDir.Name = "cmbWidthDir";
            this.cmbWidthDir.Size = new System.Drawing.Size(60, 20);
            this.cmbWidthDir.TabIndex = 4;

            this.lblWidthDim.AutoSize = true;
            this.lblWidthDim.Location = new System.Drawing.Point(160, dy);
            this.lblWidthDim.Name = "lblWidthDim";
            this.lblWidthDim.Text = "";
            dy += dyGap;

            // 하중 방향
            this.lblLoadDir.AutoSize = true;
            this.lblLoadDir.Location = new System.Drawing.Point(15, dy);
            this.lblLoadDir.Name = "lblLoadDir";
            this.lblLoadDir.Text = "하중 방향:";

            this.cmbLoadDir.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbLoadDir.Items.AddRange(new object[] { "X", "Y", "Z" });
            this.cmbLoadDir.Location = new System.Drawing.Point(90, dy - 3);
            this.cmbLoadDir.Name = "cmbLoadDir";
            this.cmbLoadDir.Size = new System.Drawing.Size(60, 20);
            this.cmbLoadDir.TabIndex = 5;

            this.lblLoadDim.AutoSize = true;
            this.lblLoadDim.Location = new System.Drawing.Point(160, dy);
            this.lblLoadDim.Name = "lblLoadDim";
            this.lblLoadDim.Text = "";

            // 자동 감지 버튼
            this.btnAutoDetect.Location = new System.Drawing.Point(330, 22);
            this.btnAutoDetect.Name = "btnAutoDetect";
            this.btnAutoDetect.Size = new System.Drawing.Size(90, 23);
            this.btnAutoDetect.TabIndex = 6;
            this.btnAutoDetect.Text = "자동 감지";
            this.btnAutoDetect.UseVisualStyleBackColor = true;
            this.btnAutoDetect.Click += new System.EventHandler(this.btnAutoDetect_Click);

            // ==========================================
            //  grpFixture - 지지구조 치수
            // ==========================================
            this.grpFixture.Controls.Add(this.radSpanRatio);
            this.grpFixture.Controls.Add(this.numSpanRatio);
            this.grpFixture.Controls.Add(this.lblSpanRatioResult);
            this.grpFixture.Controls.Add(this.radSpanAbsolute);
            this.grpFixture.Controls.Add(this.numSpanAbsolute);
            this.grpFixture.Controls.Add(this.lblSupportDia);
            this.grpFixture.Controls.Add(this.numSupportDia);
            this.grpFixture.Controls.Add(this.lblNoseDia);
            this.grpFixture.Controls.Add(this.numNoseDia);
            this.grpFixture.Controls.Add(this.lblSupportHeight);
            this.grpFixture.Controls.Add(this.numSupportHeight);
            this.grpFixture.Controls.Add(this.lblNoseHeight);
            this.grpFixture.Controls.Add(this.numNoseHeight);
            this.grpFixture.Location = new System.Drawing.Point(12, 234);
            this.grpFixture.Name = "grpFixture";
            this.grpFixture.Size = new System.Drawing.Size(440, 230);
            this.grpFixture.TabIndex = 2;
            this.grpFixture.TabStop = false;
            this.grpFixture.Text = "지지구조 치수 (mm)";

            int fy = 25;
            int fyGap = 32;

            // 스팬 비율
            this.radSpanRatio.AutoSize = true;
            this.radSpanRatio.Checked = true;
            this.radSpanRatio.Location = new System.Drawing.Point(15, fy);
            this.radSpanRatio.Name = "radSpanRatio";
            this.radSpanRatio.Size = new System.Drawing.Size(90, 16);
            this.radSpanRatio.TabIndex = 7;
            this.radSpanRatio.Text = "스팬 비율 (%):";
            this.radSpanRatio.UseVisualStyleBackColor = true;
            this.radSpanRatio.CheckedChanged += new System.EventHandler(this.radSpanMode_CheckedChanged);

            this.numSpanRatio.DecimalPlaces = 0;
            this.numSpanRatio.Location = new System.Drawing.Point(150, fy - 2);
            this.numSpanRatio.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
            this.numSpanRatio.Minimum = new decimal(new int[] { 10, 0, 0, 0 });
            this.numSpanRatio.Name = "numSpanRatio";
            this.numSpanRatio.Size = new System.Drawing.Size(70, 21);
            this.numSpanRatio.TabIndex = 8;
            this.numSpanRatio.Value = new decimal(new int[] { 80, 0, 0, 0 });

            this.lblSpanRatioResult.AutoSize = true;
            this.lblSpanRatioResult.Location = new System.Drawing.Point(230, fy);
            this.lblSpanRatioResult.Name = "lblSpanRatioResult";
            this.lblSpanRatioResult.Text = "= 0.0 mm";
            fy += fyGap;

            // 스팬 절대값
            this.radSpanAbsolute.AutoSize = true;
            this.radSpanAbsolute.Location = new System.Drawing.Point(15, fy);
            this.radSpanAbsolute.Name = "radSpanAbsolute";
            this.radSpanAbsolute.Size = new System.Drawing.Size(110, 16);
            this.radSpanAbsolute.TabIndex = 9;
            this.radSpanAbsolute.Text = "스팬 절대값 (mm):";
            this.radSpanAbsolute.UseVisualStyleBackColor = true;

            this.numSpanAbsolute.DecimalPlaces = 2;
            this.numSpanAbsolute.Location = new System.Drawing.Point(150, fy - 2);
            this.numSpanAbsolute.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            this.numSpanAbsolute.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numSpanAbsolute.Name = "numSpanAbsolute";
            this.numSpanAbsolute.Size = new System.Drawing.Size(100, 21);
            this.numSpanAbsolute.TabIndex = 10;
            this.numSpanAbsolute.Value = new decimal(new int[] { 64, 0, 0, 0 });
            this.numSpanAbsolute.Enabled = false;
            fy += fyGap;

            // 지지점 직경
            this.lblSupportDia.AutoSize = true;
            this.lblSupportDia.Location = new System.Drawing.Point(15, fy);
            this.lblSupportDia.Name = "lblSupportDia";
            this.lblSupportDia.Text = "지지점 직경:";

            this.numSupportDia.DecimalPlaces = 2;
            this.numSupportDia.Location = new System.Drawing.Point(150, fy - 3);
            this.numSupportDia.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
            this.numSupportDia.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numSupportDia.Name = "numSupportDia";
            this.numSupportDia.Size = new System.Drawing.Size(100, 21);
            this.numSupportDia.TabIndex = 11;
            this.numSupportDia.Value = 3.8m;
            fy += fyGap;

            // 로딩 노즈 직경
            this.lblNoseDia.AutoSize = true;
            this.lblNoseDia.Location = new System.Drawing.Point(15, fy);
            this.lblNoseDia.Name = "lblNoseDia";
            this.lblNoseDia.Text = "로딩 노즈 직경:";

            this.numNoseDia.DecimalPlaces = 2;
            this.numNoseDia.Location = new System.Drawing.Point(150, fy - 3);
            this.numNoseDia.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
            this.numNoseDia.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numNoseDia.Name = "numNoseDia";
            this.numNoseDia.Size = new System.Drawing.Size(100, 21);
            this.numNoseDia.TabIndex = 12;
            this.numNoseDia.Value = 3.8m;
            fy += fyGap;

            // 지지점 높이
            this.lblSupportHeight.AutoSize = true;
            this.lblSupportHeight.Location = new System.Drawing.Point(15, fy);
            this.lblSupportHeight.Name = "lblSupportHeight";
            this.lblSupportHeight.Text = "지지점 높이:";

            this.numSupportHeight.DecimalPlaces = 2;
            this.numSupportHeight.Location = new System.Drawing.Point(150, fy - 3);
            this.numSupportHeight.Maximum = new decimal(new int[] { 500, 0, 0, 0 });
            this.numSupportHeight.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numSupportHeight.Name = "numSupportHeight";
            this.numSupportHeight.Size = new System.Drawing.Size(100, 21);
            this.numSupportHeight.TabIndex = 13;
            this.numSupportHeight.Value = new decimal(new int[] { 20, 0, 0, 0 });
            fy += fyGap;

            // 로딩 노즈 높이
            this.lblNoseHeight.AutoSize = true;
            this.lblNoseHeight.Location = new System.Drawing.Point(15, fy);
            this.lblNoseHeight.Name = "lblNoseHeight";
            this.lblNoseHeight.Text = "로딩 노즈 높이:";

            this.numNoseHeight.DecimalPlaces = 2;
            this.numNoseHeight.Location = new System.Drawing.Point(150, fy - 3);
            this.numNoseHeight.Maximum = new decimal(new int[] { 500, 0, 0, 0 });
            this.numNoseHeight.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numNoseHeight.Name = "numNoseHeight";
            this.numNoseHeight.Size = new System.Drawing.Size(100, 21);
            this.numNoseHeight.TabIndex = 14;
            this.numNoseHeight.Value = new decimal(new int[] { 20, 0, 0, 0 });

            // ==========================================
            //  버튼
            // ==========================================
            int btnY = 475;

            this.btnPreview.Location = new System.Drawing.Point(140, btnY);
            this.btnPreview.Name = "btnPreview";
            this.btnPreview.Size = new System.Drawing.Size(100, 30);
            this.btnPreview.TabIndex = 15;
            this.btnPreview.Text = "미리보기";
            this.btnPreview.UseVisualStyleBackColor = true;
            this.btnPreview.Click += new System.EventHandler(this.btnPreview_Click);

            this.btnCreate.Location = new System.Drawing.Point(250, btnY);
            this.btnCreate.Name = "btnCreate";
            this.btnCreate.Size = new System.Drawing.Size(100, 30);
            this.btnCreate.TabIndex = 16;
            this.btnCreate.Text = "생성";
            this.btnCreate.UseVisualStyleBackColor = true;
            this.btnCreate.Click += new System.EventHandler(this.btnCreate_Click);

            this.btnCancel.Location = new System.Drawing.Point(360, btnY);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 30);
            this.btnCancel.TabIndex = 17;
            this.btnCancel.Text = "취소";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);

            // ==========================================
            //  ApplyBendingFixtureDialog
            // ==========================================
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(474, 520);
            this.Controls.Add(this.grpBody);
            this.Controls.Add(this.grpDirection);
            this.Controls.Add(this.grpFixture);
            this.Controls.Add(this.btnPreview);
            this.Controls.Add(this.btnCreate);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ApplyBendingFixtureDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "벤딩 지그 적용";

            ((System.ComponentModel.ISupportInitialize)(this.numSpanRatio)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSpanAbsolute)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSupportDia)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numNoseDia)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSupportHeight)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numNoseHeight)).EndInit();
            this.grpBody.ResumeLayout(false);
            this.grpBody.PerformLayout();
            this.grpDirection.ResumeLayout(false);
            this.grpDirection.PerformLayout();
            this.grpFixture.ResumeLayout(false);
            this.grpFixture.PerformLayout();
            this.ResumeLayout(false);
        }
    }
}
