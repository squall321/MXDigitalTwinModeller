namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    partial class DMATensileSpecimenDialog
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.ComboBox cmbSpecimenType;
        private System.Windows.Forms.ComboBox cmbShape;
        private System.Windows.Forms.NumericUpDown numLength;
        private System.Windows.Forms.NumericUpDown numWidth;
        private System.Windows.Forms.NumericUpDown numThickness;
        private System.Windows.Forms.NumericUpDown numGaugeLength;
        private System.Windows.Forms.NumericUpDown numGripLength;
        private System.Windows.Forms.NumericUpDown numGripWidth;
        private System.Windows.Forms.NumericUpDown numGripHeight;
        private System.Windows.Forms.NumericUpDown numFilletRadius;
        private System.Windows.Forms.Button btnCreate;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnLoadDefaults;
        private System.Windows.Forms.Button btnPreview;
        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.GroupBox grpSpecimenType;
        private System.Windows.Forms.GroupBox grpDimensions;
        private System.Windows.Forms.Label lblSpecimenType;
        private System.Windows.Forms.Label lblShape;
        private System.Windows.Forms.Label lblLength;
        private System.Windows.Forms.Label lblWidth;
        private System.Windows.Forms.Label lblThickness;
        private System.Windows.Forms.Label lblGaugeLength;
        private System.Windows.Forms.Label lblGripLength;
        private System.Windows.Forms.Label lblGripWidth;
        private System.Windows.Forms.Label lblGripHeight;
        private System.Windows.Forms.Label lblFilletRadius;

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
            this.cmbShape = new System.Windows.Forms.ComboBox();
            this.numLength = new System.Windows.Forms.NumericUpDown();
            this.numWidth = new System.Windows.Forms.NumericUpDown();
            this.numThickness = new System.Windows.Forms.NumericUpDown();
            this.numGaugeLength = new System.Windows.Forms.NumericUpDown();
            this.numGripLength = new System.Windows.Forms.NumericUpDown();
            this.numGripWidth = new System.Windows.Forms.NumericUpDown();
            this.numGripHeight = new System.Windows.Forms.NumericUpDown();
            this.numFilletRadius = new System.Windows.Forms.NumericUpDown();
            this.btnCreate = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnLoadDefaults = new System.Windows.Forms.Button();
            this.btnPreview = new System.Windows.Forms.Button();
            this.lblDescription = new System.Windows.Forms.Label();
            this.grpSpecimenType = new System.Windows.Forms.GroupBox();
            this.grpDimensions = new System.Windows.Forms.GroupBox();
            this.lblSpecimenType = new System.Windows.Forms.Label();
            this.lblShape = new System.Windows.Forms.Label();
            this.lblLength = new System.Windows.Forms.Label();
            this.lblWidth = new System.Windows.Forms.Label();
            this.lblThickness = new System.Windows.Forms.Label();
            this.lblGaugeLength = new System.Windows.Forms.Label();
            this.lblGripLength = new System.Windows.Forms.Label();
            this.lblGripWidth = new System.Windows.Forms.Label();
            this.lblGripHeight = new System.Windows.Forms.Label();
            this.lblFilletRadius = new System.Windows.Forms.Label();

            ((System.ComponentModel.ISupportInitialize)(this.numLength)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numWidth)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numThickness)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numGaugeLength)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numGripLength)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numGripWidth)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numGripHeight)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numFilletRadius)).BeginInit();
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
            this.lblDescription.Size = new System.Drawing.Size(200, 12);
            this.lblDescription.TabIndex = 3;
            this.lblDescription.Text = "DMA 인장 시험 (Tensile Test)";

            // grpDimensions
            this.grpDimensions.Controls.Add(this.lblShape);
            this.grpDimensions.Controls.Add(this.cmbShape);
            this.grpDimensions.Controls.Add(this.lblLength);
            this.grpDimensions.Controls.Add(this.numLength);
            this.grpDimensions.Controls.Add(this.lblWidth);
            this.grpDimensions.Controls.Add(this.numWidth);
            this.grpDimensions.Controls.Add(this.lblThickness);
            this.grpDimensions.Controls.Add(this.numThickness);
            this.grpDimensions.Controls.Add(this.lblGaugeLength);
            this.grpDimensions.Controls.Add(this.numGaugeLength);
            this.grpDimensions.Controls.Add(this.lblGripLength);
            this.grpDimensions.Controls.Add(this.numGripLength);
            this.grpDimensions.Controls.Add(this.lblGripWidth);
            this.grpDimensions.Controls.Add(this.numGripWidth);
            this.grpDimensions.Controls.Add(this.lblGripHeight);
            this.grpDimensions.Controls.Add(this.numGripHeight);
            this.grpDimensions.Controls.Add(this.lblFilletRadius);
            this.grpDimensions.Controls.Add(this.numFilletRadius);
            this.grpDimensions.Location = new System.Drawing.Point(12, 128);
            this.grpDimensions.Name = "grpDimensions";
            this.grpDimensions.Size = new System.Drawing.Size(440, 350);
            this.grpDimensions.TabIndex = 1;
            this.grpDimensions.TabStop = false;
            this.grpDimensions.Text = "치수 (mm)";

            int yPos = 30;
            int yGap = 35;

            // Shape
            this.lblShape.AutoSize = true;
            this.lblShape.Location = new System.Drawing.Point(15, yPos);
            this.lblShape.Name = "lblShape";
            this.lblShape.Size = new System.Drawing.Size(120, 12);
            this.lblShape.TabIndex = 0;
            this.lblShape.Text = "형상 (Shape):";

            this.cmbShape.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbShape.FormattingEnabled = true;
            this.cmbShape.Location = new System.Drawing.Point(160, yPos - 3);
            this.cmbShape.Name = "cmbShape";
            this.cmbShape.Size = new System.Drawing.Size(120, 20);
            this.cmbShape.TabIndex = 1;
            this.cmbShape.SelectedIndexChanged += new System.EventHandler(this.cmbShape_SelectedIndexChanged);
            yPos += yGap;

            // Length
            this.lblLength.AutoSize = true;
            this.lblLength.Location = new System.Drawing.Point(15, yPos);
            this.lblLength.Name = "lblLength";
            this.lblLength.Size = new System.Drawing.Size(120, 12);
            this.lblLength.TabIndex = 2;
            this.lblLength.Text = "길이 (Length):";

            this.numLength.DecimalPlaces = 2;
            this.numLength.Location = new System.Drawing.Point(160, yPos - 3);
            this.numLength.Maximum = new decimal(new int[] { 200, 0, 0, 0 });
            this.numLength.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numLength.Name = "numLength";
            this.numLength.Size = new System.Drawing.Size(120, 21);
            this.numLength.TabIndex = 3;
            this.numLength.Value = new decimal(new int[] { 50, 0, 0, 0 });
            yPos += yGap;

            // Width
            this.lblWidth.AutoSize = true;
            this.lblWidth.Location = new System.Drawing.Point(15, yPos);
            this.lblWidth.Name = "lblWidth";
            this.lblWidth.Size = new System.Drawing.Size(120, 12);
            this.lblWidth.TabIndex = 4;
            this.lblWidth.Text = "폭 (Width):";

            this.numWidth.DecimalPlaces = 2;
            this.numWidth.Location = new System.Drawing.Point(160, yPos - 3);
            this.numWidth.Maximum = new decimal(new int[] { 25, 0, 0, 0 });
            this.numWidth.Minimum = new decimal(new int[] { 5, 0, 0, 0 });
            this.numWidth.Name = "numWidth";
            this.numWidth.Size = new System.Drawing.Size(120, 21);
            this.numWidth.TabIndex = 5;
            this.numWidth.Value = new decimal(new int[] { 10, 0, 0, 0 });
            yPos += yGap;

            // Thickness
            this.lblThickness.AutoSize = true;
            this.lblThickness.Location = new System.Drawing.Point(15, yPos);
            this.lblThickness.Name = "lblThickness";
            this.lblThickness.Size = new System.Drawing.Size(120, 12);
            this.lblThickness.TabIndex = 6;
            this.lblThickness.Text = "두께 (Thickness):";

            this.numThickness.DecimalPlaces = 2;
            this.numThickness.Location = new System.Drawing.Point(160, yPos - 3);
            this.numThickness.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            this.numThickness.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numThickness.Name = "numThickness";
            this.numThickness.Size = new System.Drawing.Size(120, 21);
            this.numThickness.TabIndex = 7;
            this.numThickness.Value = new decimal(new int[] { 3, 0, 0, 0 });
            yPos += yGap;

            // Gauge Length
            this.lblGaugeLength.AutoSize = true;
            this.lblGaugeLength.Location = new System.Drawing.Point(15, yPos);
            this.lblGaugeLength.Name = "lblGaugeLength";
            this.lblGaugeLength.Size = new System.Drawing.Size(120, 12);
            this.lblGaugeLength.TabIndex = 8;
            this.lblGaugeLength.Text = "게이지 길이:";

            this.numGaugeLength.DecimalPlaces = 2;
            this.numGaugeLength.Location = new System.Drawing.Point(160, yPos - 3);
            this.numGaugeLength.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
            this.numGaugeLength.Minimum = new decimal(new int[] { 10, 0, 0, 0 });
            this.numGaugeLength.Name = "numGaugeLength";
            this.numGaugeLength.Size = new System.Drawing.Size(120, 21);
            this.numGaugeLength.TabIndex = 9;
            this.numGaugeLength.Value = new decimal(new int[] { 20, 0, 0, 0 });
            yPos += yGap;

            // Grip Length
            this.lblGripLength.AutoSize = true;
            this.lblGripLength.Location = new System.Drawing.Point(15, yPos);
            this.lblGripLength.Name = "lblGripLength";
            this.lblGripLength.Size = new System.Drawing.Size(120, 12);
            this.lblGripLength.TabIndex = 10;
            this.lblGripLength.Text = "그립 길이:";

            this.numGripLength.DecimalPlaces = 2;
            this.numGripLength.Location = new System.Drawing.Point(160, yPos - 3);
            this.numGripLength.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
            this.numGripLength.Minimum = new decimal(new int[] { 10, 0, 0, 0 });
            this.numGripLength.Name = "numGripLength";
            this.numGripLength.Size = new System.Drawing.Size(120, 21);
            this.numGripLength.TabIndex = 11;
            this.numGripLength.Value = new decimal(new int[] { 15, 0, 0, 0 });
            yPos += yGap;

            // Grip Width
            this.lblGripWidth.AutoSize = true;
            this.lblGripWidth.Location = new System.Drawing.Point(15, yPos);
            this.lblGripWidth.Name = "lblGripWidth";
            this.lblGripWidth.Size = new System.Drawing.Size(120, 12);
            this.lblGripWidth.TabIndex = 12;
            this.lblGripWidth.Text = "그립 폭 (DogBone):";

            this.numGripWidth.DecimalPlaces = 2;
            this.numGripWidth.Location = new System.Drawing.Point(160, yPos - 3);
            this.numGripWidth.Maximum = new decimal(new int[] { 50, 0, 0, 0 });
            this.numGripWidth.Minimum = new decimal(new int[] { 5, 0, 0, 0 });
            this.numGripWidth.Name = "numGripWidth";
            this.numGripWidth.Size = new System.Drawing.Size(120, 21);
            this.numGripWidth.TabIndex = 13;
            this.numGripWidth.Value = new decimal(new int[] { 15, 0, 0, 0 });
            yPos += yGap;

            // Grip Height
            this.lblGripHeight.AutoSize = true;
            this.lblGripHeight.Location = new System.Drawing.Point(15, yPos);
            this.lblGripHeight.Name = "lblGripHeight";
            this.lblGripHeight.Size = new System.Drawing.Size(120, 12);
            this.lblGripHeight.TabIndex = 14;
            this.lblGripHeight.Text = "그립 높이 (DogBone):";

            this.numGripHeight.DecimalPlaces = 2;
            this.numGripHeight.Location = new System.Drawing.Point(160, yPos - 3);
            this.numGripHeight.Maximum = new decimal(new int[] { 50, 0, 0, 0 });
            this.numGripHeight.Minimum = new decimal(new int[] { 5, 0, 0, 0 });
            this.numGripHeight.Name = "numGripHeight";
            this.numGripHeight.Size = new System.Drawing.Size(120, 21);
            this.numGripHeight.TabIndex = 15;
            this.numGripHeight.Value = new decimal(new int[] { 25, 0, 0, 0 });
            yPos += yGap;

            // Fillet Radius
            this.lblFilletRadius.AutoSize = true;
            this.lblFilletRadius.Location = new System.Drawing.Point(15, yPos);
            this.lblFilletRadius.Name = "lblFilletRadius";
            this.lblFilletRadius.Size = new System.Drawing.Size(120, 12);
            this.lblFilletRadius.TabIndex = 16;
            this.lblFilletRadius.Text = "필렛 반경 (DogBone):";

            this.numFilletRadius.DecimalPlaces = 2;
            this.numFilletRadius.Location = new System.Drawing.Point(160, yPos - 3);
            this.numFilletRadius.Maximum = new decimal(new int[] { 20, 0, 0, 0 });
            this.numFilletRadius.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numFilletRadius.Name = "numFilletRadius";
            this.numFilletRadius.Size = new System.Drawing.Size(120, 21);
            this.numFilletRadius.TabIndex = 17;
            this.numFilletRadius.Value = new decimal(new int[] { 5, 0, 0, 0 });

            // btnPreview
            this.btnPreview.Location = new System.Drawing.Point(140, 490);
            this.btnPreview.Name = "btnPreview";
            this.btnPreview.Size = new System.Drawing.Size(100, 30);
            this.btnPreview.TabIndex = 2;
            this.btnPreview.Text = "미리보기";
            this.btnPreview.UseVisualStyleBackColor = true;
            this.btnPreview.Click += new System.EventHandler(this.btnPreview_Click);

            // btnCreate
            this.btnCreate.Location = new System.Drawing.Point(250, 490);
            this.btnCreate.Name = "btnCreate";
            this.btnCreate.Size = new System.Drawing.Size(100, 30);
            this.btnCreate.TabIndex = 3;
            this.btnCreate.Text = "생성";
            this.btnCreate.UseVisualStyleBackColor = true;
            this.btnCreate.Click += new System.EventHandler(this.btnCreate_Click);

            // btnCancel
            this.btnCancel.Location = new System.Drawing.Point(360, 490);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 30);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "취소";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);

            // DMATensileSpecimenDialog
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(474, 532);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnCreate);
            this.Controls.Add(this.btnPreview);
            this.Controls.Add(this.grpDimensions);
            this.Controls.Add(this.grpSpecimenType);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DMATensileSpecimenDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "DMA 인장시험 시편 생성";

            ((System.ComponentModel.ISupportInitialize)(this.numLength)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numWidth)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numThickness)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numGaugeLength)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numGripLength)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numGripWidth)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numGripHeight)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numFilletRadius)).EndInit();
            this.grpSpecimenType.ResumeLayout(false);
            this.grpSpecimenType.PerformLayout();
            this.grpDimensions.ResumeLayout(false);
            this.grpDimensions.PerformLayout();
            this.ResumeLayout(false);
        }
    }
}
