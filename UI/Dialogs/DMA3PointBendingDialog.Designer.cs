namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    partial class DMA3PointBendingDialog
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.ComboBox cmbSpecimenType;
        private System.Windows.Forms.NumericUpDown numLength;
        private System.Windows.Forms.NumericUpDown numWidth;
        private System.Windows.Forms.NumericUpDown numThickness;
        private System.Windows.Forms.NumericUpDown numSpan;
        private System.Windows.Forms.NumericUpDown numSupportDiameter;
        private System.Windows.Forms.NumericUpDown numLoadingNoseDiameter;
        private System.Windows.Forms.NumericUpDown numSupportHeight;
        private System.Windows.Forms.NumericUpDown numLoadingNoseHeight;
        private System.Windows.Forms.Button btnCreate;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnLoadDefaults;
        private System.Windows.Forms.Button btnPreview;
        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.GroupBox grpSpecimenType;
        private System.Windows.Forms.GroupBox grpDimensions;
        private System.Windows.Forms.Label lblSpecimenType;
        private System.Windows.Forms.Label lblLength;
        private System.Windows.Forms.Label lblWidth;
        private System.Windows.Forms.Label lblThickness;
        private System.Windows.Forms.Label lblSpan;
        private System.Windows.Forms.Label lblSupportDiameter;
        private System.Windows.Forms.Label lblLoadingNoseDiameter;
        private System.Windows.Forms.Label lblSupportHeight;
        private System.Windows.Forms.Label lblLoadingNoseHeight;
        private System.Windows.Forms.Label lblWarning;

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
            this.cmbSpecimenType = new System.Windows.Forms.ComboBox();
            this.numLength = new System.Windows.Forms.NumericUpDown();
            this.numWidth = new System.Windows.Forms.NumericUpDown();
            this.numThickness = new System.Windows.Forms.NumericUpDown();
            this.numSpan = new System.Windows.Forms.NumericUpDown();
            this.numSupportDiameter = new System.Windows.Forms.NumericUpDown();
            this.numLoadingNoseDiameter = new System.Windows.Forms.NumericUpDown();
            this.numSupportHeight = new System.Windows.Forms.NumericUpDown();
            this.numLoadingNoseHeight = new System.Windows.Forms.NumericUpDown();
            this.btnCreate = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnLoadDefaults = new System.Windows.Forms.Button();
            this.btnPreview = new System.Windows.Forms.Button();
            this.lblDescription = new System.Windows.Forms.Label();
            this.grpSpecimenType = new System.Windows.Forms.GroupBox();
            this.grpDimensions = new System.Windows.Forms.GroupBox();
            this.lblSpecimenType = new System.Windows.Forms.Label();
            this.lblLength = new System.Windows.Forms.Label();
            this.lblWidth = new System.Windows.Forms.Label();
            this.lblThickness = new System.Windows.Forms.Label();
            this.lblSpan = new System.Windows.Forms.Label();
            this.lblSupportDiameter = new System.Windows.Forms.Label();
            this.lblLoadingNoseDiameter = new System.Windows.Forms.Label();
            this.lblSupportHeight = new System.Windows.Forms.Label();
            this.lblLoadingNoseHeight = new System.Windows.Forms.Label();
            this.lblWarning = new System.Windows.Forms.Label();

            ((System.ComponentModel.ISupportInitialize)(this.numLength)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numWidth)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numThickness)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSpan)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSupportDiameter)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLoadingNoseDiameter)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSupportHeight)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLoadingNoseHeight)).BeginInit();
            this.grpSpecimenType.SuspendLayout();
            this.grpDimensions.SuspendLayout();
            this.SuspendLayout();

            // grpSpecimenType
            this.grpSpecimenType.Controls.Add(this.lblSpecimenType);
            this.grpSpecimenType.Controls.Add(this.cmbSpecimenType);
            this.grpSpecimenType.Controls.Add(this.btnLoadDefaults);
            this.grpSpecimenType.Controls.Add(this.lblDescription);
            this.grpSpecimenType.Location = new System.Drawing.Point(12, 12);
            this.grpSpecimenType.Name = "grpSpecimenType";
            this.grpSpecimenType.Size = new System.Drawing.Size(440, 110);
            this.grpSpecimenType.TabIndex = 0;
            this.grpSpecimenType.TabStop = false;
            this.grpSpecimenType.Text = "시편 규격";

            // lblSpecimenType
            this.lblSpecimenType.AutoSize = true;
            this.lblSpecimenType.Location = new System.Drawing.Point(15, 25);
            this.lblSpecimenType.Name = "lblSpecimenType";
            this.lblSpecimenType.Size = new System.Drawing.Size(57, 12);
            this.lblSpecimenType.TabIndex = 0;
            this.lblSpecimenType.Text = "규격 선택:";

            // cmbSpecimenType
            this.cmbSpecimenType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbSpecimenType.FormattingEnabled = true;
            this.cmbSpecimenType.Location = new System.Drawing.Point(15, 45);
            this.cmbSpecimenType.Name = "cmbSpecimenType";
            this.cmbSpecimenType.Size = new System.Drawing.Size(280, 20);
            this.cmbSpecimenType.TabIndex = 1;
            this.cmbSpecimenType.SelectedIndexChanged += new System.EventHandler(this.cmbSpecimenType_SelectedIndexChanged);

            // btnLoadDefaults
            this.btnLoadDefaults.Location = new System.Drawing.Point(310, 43);
            this.btnLoadDefaults.Name = "btnLoadDefaults";
            this.btnLoadDefaults.Size = new System.Drawing.Size(110, 23);
            this.btnLoadDefaults.TabIndex = 2;
            this.btnLoadDefaults.Text = "기본값 로드";
            this.btnLoadDefaults.UseVisualStyleBackColor = true;
            this.btnLoadDefaults.Click += new System.EventHandler(this.btnLoadDefaults_Click);

            // lblDescription
            this.lblDescription.AutoSize = true;
            this.lblDescription.ForeColor = System.Drawing.Color.Blue;
            this.lblDescription.Location = new System.Drawing.Point(15, 75);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new System.Drawing.Size(300, 12);
            this.lblDescription.TabIndex = 3;
            this.lblDescription.Text = "DMA 3점 굽힘 시험 (3-Point Bending)";

            // grpDimensions
            this.grpDimensions.Controls.Add(this.lblLength);
            this.grpDimensions.Controls.Add(this.numLength);
            this.grpDimensions.Controls.Add(this.lblWidth);
            this.grpDimensions.Controls.Add(this.numWidth);
            this.grpDimensions.Controls.Add(this.lblThickness);
            this.grpDimensions.Controls.Add(this.numThickness);
            this.grpDimensions.Controls.Add(this.lblSpan);
            this.grpDimensions.Controls.Add(this.numSpan);
            this.grpDimensions.Controls.Add(this.lblSupportDiameter);
            this.grpDimensions.Controls.Add(this.numSupportDiameter);
            this.grpDimensions.Controls.Add(this.lblLoadingNoseDiameter);
            this.grpDimensions.Controls.Add(this.numLoadingNoseDiameter);
            this.grpDimensions.Controls.Add(this.lblSupportHeight);
            this.grpDimensions.Controls.Add(this.numSupportHeight);
            this.grpDimensions.Controls.Add(this.lblLoadingNoseHeight);
            this.grpDimensions.Controls.Add(this.numLoadingNoseHeight);
            this.grpDimensions.Location = new System.Drawing.Point(12, 128);
            this.grpDimensions.Name = "grpDimensions";
            this.grpDimensions.Size = new System.Drawing.Size(440, 315);
            this.grpDimensions.TabIndex = 1;
            this.grpDimensions.TabStop = false;
            this.grpDimensions.Text = "치수 (mm)";

            int yPos = 30;
            int yGap = 35;

            // Length
            this.lblLength.AutoSize = true;
            this.lblLength.Location = new System.Drawing.Point(15, yPos);
            this.lblLength.Name = "lblLength";
            this.lblLength.Size = new System.Drawing.Size(140, 12);
            this.lblLength.TabIndex = 0;
            this.lblLength.Text = "길이 (Length):";

            this.numLength.DecimalPlaces = 2;
            this.numLength.Location = new System.Drawing.Point(180, yPos - 3);
            this.numLength.Maximum = new decimal(new int[] { 150, 0, 0, 0 });
            this.numLength.Minimum = new decimal(new int[] { 50, 0, 0, 0 });
            this.numLength.Name = "numLength";
            this.numLength.Size = new System.Drawing.Size(120, 21);
            this.numLength.TabIndex = 1;
            this.numLength.Value = new decimal(new int[] { 80, 0, 0, 0 });
            yPos += yGap;

            // Width
            this.lblWidth.AutoSize = true;
            this.lblWidth.Location = new System.Drawing.Point(15, yPos);
            this.lblWidth.Name = "lblWidth";
            this.lblWidth.Size = new System.Drawing.Size(140, 12);
            this.lblWidth.TabIndex = 2;
            this.lblWidth.Text = "폭 (Width):";

            this.numWidth.DecimalPlaces = 2;
            this.numWidth.Location = new System.Drawing.Point(180, yPos - 3);
            this.numWidth.Maximum = new decimal(new int[] { 25, 0, 0, 0 });
            this.numWidth.Minimum = new decimal(new int[] { 5, 0, 0, 0 });
            this.numWidth.Name = "numWidth";
            this.numWidth.Size = new System.Drawing.Size(120, 21);
            this.numWidth.TabIndex = 3;
            this.numWidth.Value = new decimal(new int[] { 10, 0, 0, 0 });
            yPos += yGap;

            // Thickness
            this.lblThickness.AutoSize = true;
            this.lblThickness.Location = new System.Drawing.Point(15, yPos);
            this.lblThickness.Name = "lblThickness";
            this.lblThickness.Size = new System.Drawing.Size(140, 12);
            this.lblThickness.TabIndex = 4;
            this.lblThickness.Text = "두께 (Thickness):";

            this.numThickness.DecimalPlaces = 2;
            this.numThickness.Location = new System.Drawing.Point(180, yPos - 3);
            this.numThickness.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            this.numThickness.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numThickness.Name = "numThickness";
            this.numThickness.Size = new System.Drawing.Size(120, 21);
            this.numThickness.TabIndex = 5;
            this.numThickness.Value = new decimal(new int[] { 4, 0, 0, 0 });
            this.numThickness.ValueChanged += new System.EventHandler(this.numThickness_ValueChanged);
            yPos += yGap;

            // Span
            this.lblSpan.AutoSize = true;
            this.lblSpan.Location = new System.Drawing.Point(15, yPos);
            this.lblSpan.Name = "lblSpan";
            this.lblSpan.Size = new System.Drawing.Size(140, 12);
            this.lblSpan.TabIndex = 6;
            this.lblSpan.Text = "지지점 간격 (Span):";

            this.numSpan.DecimalPlaces = 2;
            this.numSpan.Location = new System.Drawing.Point(180, yPos - 3);
            this.numSpan.Maximum = new decimal(new int[] { 150, 0, 0, 0 });
            this.numSpan.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numSpan.Name = "numSpan";
            this.numSpan.Size = new System.Drawing.Size(120, 21);
            this.numSpan.TabIndex = 7;
            this.numSpan.Value = new decimal(new int[] { 64, 0, 0, 0 });
            this.numSpan.ValueChanged += new System.EventHandler(this.numSpan_ValueChanged);
            yPos += yGap;

            // Support Diameter
            this.lblSupportDiameter.AutoSize = true;
            this.lblSupportDiameter.Location = new System.Drawing.Point(15, yPos);
            this.lblSupportDiameter.Name = "lblSupportDiameter";
            this.lblSupportDiameter.Size = new System.Drawing.Size(140, 12);
            this.lblSupportDiameter.TabIndex = 8;
            this.lblSupportDiameter.Text = "지지점 직경:";

            this.numSupportDiameter.DecimalPlaces = 2;
            this.numSupportDiameter.Location = new System.Drawing.Point(180, yPos - 3);
            this.numSupportDiameter.Maximum = new decimal(new int[] { 15, 0, 0, 0 });
            this.numSupportDiameter.Minimum = new decimal(new int[] { 5, 0, 0, 0 });
            this.numSupportDiameter.Name = "numSupportDiameter";
            this.numSupportDiameter.Size = new System.Drawing.Size(120, 21);
            this.numSupportDiameter.TabIndex = 9;
            this.numSupportDiameter.Value = new decimal(new int[] { 8, 0, 0, 0 });
            yPos += yGap;

            // Loading Nose Diameter
            this.lblLoadingNoseDiameter.AutoSize = true;
            this.lblLoadingNoseDiameter.Location = new System.Drawing.Point(15, yPos);
            this.lblLoadingNoseDiameter.Name = "lblLoadingNoseDiameter";
            this.lblLoadingNoseDiameter.Size = new System.Drawing.Size(140, 12);
            this.lblLoadingNoseDiameter.TabIndex = 10;
            this.lblLoadingNoseDiameter.Text = "로딩 노즈 직경:";

            this.numLoadingNoseDiameter.DecimalPlaces = 2;
            this.numLoadingNoseDiameter.Location = new System.Drawing.Point(180, yPos - 3);
            this.numLoadingNoseDiameter.Maximum = new decimal(new int[] { 15, 0, 0, 0 });
            this.numLoadingNoseDiameter.Minimum = new decimal(new int[] { 5, 0, 0, 0 });
            this.numLoadingNoseDiameter.Name = "numLoadingNoseDiameter";
            this.numLoadingNoseDiameter.Size = new System.Drawing.Size(120, 21);
            this.numLoadingNoseDiameter.TabIndex = 11;
            this.numLoadingNoseDiameter.Value = new decimal(new int[] { 8, 0, 0, 0 });
            yPos += yGap;

            // Support Height
            this.lblSupportHeight.AutoSize = true;
            this.lblSupportHeight.Location = new System.Drawing.Point(15, yPos);
            this.lblSupportHeight.Name = "lblSupportHeight";
            this.lblSupportHeight.Size = new System.Drawing.Size(140, 12);
            this.lblSupportHeight.TabIndex = 12;
            this.lblSupportHeight.Text = "지지점 높이:";

            this.numSupportHeight.DecimalPlaces = 2;
            this.numSupportHeight.Location = new System.Drawing.Point(180, yPos - 3);
            this.numSupportHeight.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
            this.numSupportHeight.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numSupportHeight.Name = "numSupportHeight";
            this.numSupportHeight.Size = new System.Drawing.Size(120, 21);
            this.numSupportHeight.TabIndex = 13;
            this.numSupportHeight.Value = new decimal(new int[] { 20, 0, 0, 0 });
            yPos += yGap;

            // Loading Nose Height
            this.lblLoadingNoseHeight.AutoSize = true;
            this.lblLoadingNoseHeight.Location = new System.Drawing.Point(15, yPos);
            this.lblLoadingNoseHeight.Name = "lblLoadingNoseHeight";
            this.lblLoadingNoseHeight.Size = new System.Drawing.Size(140, 12);
            this.lblLoadingNoseHeight.TabIndex = 14;
            this.lblLoadingNoseHeight.Text = "로딩 노즈 높이:";

            this.numLoadingNoseHeight.DecimalPlaces = 2;
            this.numLoadingNoseHeight.Location = new System.Drawing.Point(180, yPos - 3);
            this.numLoadingNoseHeight.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
            this.numLoadingNoseHeight.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numLoadingNoseHeight.Name = "numLoadingNoseHeight";
            this.numLoadingNoseHeight.Size = new System.Drawing.Size(120, 21);
            this.numLoadingNoseHeight.TabIndex = 15;
            this.numLoadingNoseHeight.Value = new decimal(new int[] { 20, 0, 0, 0 });

            // lblWarning
            this.lblWarning.Location = new System.Drawing.Point(12, 450);
            this.lblWarning.Name = "lblWarning";
            this.lblWarning.Size = new System.Drawing.Size(450, 0);
            this.lblWarning.TabIndex = 16;
            this.lblWarning.Text = "";
            this.lblWarning.ForeColor = System.Drawing.Color.Red;
            this.lblWarning.AutoSize = false;
            this.lblWarning.MaximumSize = new System.Drawing.Size(450, 0);

            // btnPreview
            this.btnPreview.Location = new System.Drawing.Point(140, 510);
            this.btnPreview.Name = "btnPreview";
            this.btnPreview.Size = new System.Drawing.Size(100, 30);
            this.btnPreview.TabIndex = 2;
            this.btnPreview.Text = "미리보기";
            this.btnPreview.UseVisualStyleBackColor = true;
            this.btnPreview.Click += new System.EventHandler(this.btnPreview_Click);

            // btnCreate
            this.btnCreate.Location = new System.Drawing.Point(250, 510);
            this.btnCreate.Name = "btnCreate";
            this.btnCreate.Size = new System.Drawing.Size(100, 30);
            this.btnCreate.TabIndex = 3;
            this.btnCreate.Text = "생성";
            this.btnCreate.UseVisualStyleBackColor = true;
            this.btnCreate.Click += new System.EventHandler(this.btnCreate_Click);

            // btnCancel
            this.btnCancel.Location = new System.Drawing.Point(360, 510);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 30);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "취소";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);

            // DMA3PointBendingDialog
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(474, 555);
            this.Controls.Add(this.lblWarning);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnCreate);
            this.Controls.Add(this.btnPreview);
            this.Controls.Add(this.grpDimensions);
            this.Controls.Add(this.grpSpecimenType);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DMA3PointBendingDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "DMA 3점 굽힘시험 시편 생성";

            ((System.ComponentModel.ISupportInitialize)(this.numLength)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numWidth)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numThickness)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSpan)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSupportDiameter)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLoadingNoseDiameter)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSupportHeight)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLoadingNoseHeight)).EndInit();
            this.grpSpecimenType.ResumeLayout(false);
            this.grpSpecimenType.PerformLayout();
            this.grpDimensions.ResumeLayout(false);
            this.grpDimensions.PerformLayout();
            this.ResumeLayout(false);
        }
    }
}
