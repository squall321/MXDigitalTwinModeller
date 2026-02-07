namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    partial class MeshSettingsDialog
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
            this.grpBodies = new System.Windows.Forms.GroupBox();
            this.dgvBodies = new System.Windows.Forms.DataGridView();
            this.colCheck = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colBodyName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMeshX = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMeshY = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMeshZ = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colElemSize = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colShape = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colMidside = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colGrowth = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSizeFunc = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.grpBatch = new System.Windows.Forms.GroupBox();
            this.lblKeyword = new System.Windows.Forms.Label();
            this.txtKeyword = new System.Windows.Forms.TextBox();
            this.lblBatchSize = new System.Windows.Forms.Label();
            this.numBatchSize = new System.Windows.Forms.NumericUpDown();
            this.btnUpdate = new System.Windows.Forms.Button();
            this.grpOptions = new System.Windows.Forms.GroupBox();
            this.lblElementShape = new System.Windows.Forms.Label();
            this.cmbElementShape = new System.Windows.Forms.ComboBox();
            this.lblMidsideNodes = new System.Windows.Forms.Label();
            this.cmbMidsideNodes = new System.Windows.Forms.ComboBox();
            this.lblGrowthRate = new System.Windows.Forms.Label();
            this.numGrowthRate = new System.Windows.Forms.NumericUpDown();
            this.lblSizeFunction = new System.Windows.Forms.Label();
            this.cmbSizeFunction = new System.Windows.Forms.ComboBox();
            this.btnApplyAll = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.grpBodies.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvBodies)).BeginInit();
            this.grpBatch.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numBatchSize)).BeginInit();
            this.grpOptions.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numGrowthRate)).BeginInit();
            this.SuspendLayout();

            // === colCheck ===
            this.colCheck.HeaderText = "";
            this.colCheck.Name = "colCheck";
            this.colCheck.Width = 30;
            this.colCheck.TrueValue = true;
            this.colCheck.FalseValue = false;

            // === colBodyName ===
            this.colBodyName.HeaderText = "Body Name";
            this.colBodyName.Name = "colBodyName";
            this.colBodyName.ReadOnly = true;
            this.colBodyName.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;

            // === colMeshX ===
            this.colMeshX.HeaderText = "X (mm)";
            this.colMeshX.Name = "colMeshX";
            this.colMeshX.Width = 70;

            // === colMeshY ===
            this.colMeshY.HeaderText = "Y (mm)";
            this.colMeshY.Name = "colMeshY";
            this.colMeshY.Width = 70;

            // === colMeshZ ===
            this.colMeshZ.HeaderText = "Z (mm)";
            this.colMeshZ.Name = "colMeshZ";
            this.colMeshZ.Width = 70;

            // === colElemSize ===
            this.colElemSize.HeaderText = "Elem (mm)";
            this.colElemSize.Name = "colElemSize";
            this.colElemSize.ReadOnly = true;
            this.colElemSize.Width = 75;

            // === colShape ===
            this.colShape.HeaderText = "Shape";
            this.colShape.Name = "colShape";
            this.colShape.Width = 85;
            this.colShape.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.colShape.Items.AddRange(new object[] { "Tet", "Hex", "Quad", "Tri" });

            // === colMidside ===
            this.colMidside.HeaderText = "Midside";
            this.colMidside.Name = "colMidside";
            this.colMidside.Width = 80;
            this.colMidside.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.colMidside.Items.AddRange(new object[] { "Dropped", "Kept", "Auto" });

            // === colGrowth ===
            this.colGrowth.HeaderText = "Growth";
            this.colGrowth.Name = "colGrowth";
            this.colGrowth.Width = 65;

            // === colSizeFunc ===
            this.colSizeFunc.HeaderText = "SizeFunc";
            this.colSizeFunc.Name = "colSizeFunc";
            this.colSizeFunc.Width = 100;
            this.colSizeFunc.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.colSizeFunc.Items.AddRange(new object[] { "Curv+Prox", "Curv", "Prox", "Fixed" });

            // === dgvBodies ===
            this.dgvBodies.AllowUserToAddRows = false;
            this.dgvBodies.AllowUserToDeleteRows = false;
            this.dgvBodies.AllowUserToResizeRows = false;
            this.dgvBodies.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvBodies.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
                this.colCheck, this.colBodyName, this.colMeshX, this.colMeshY, this.colMeshZ, this.colElemSize,
                this.colShape, this.colMidside, this.colGrowth, this.colSizeFunc
            });
            this.dgvBodies.Location = new System.Drawing.Point(3, 16);
            this.dgvBodies.Size = new System.Drawing.Size(1070, 491);
            this.dgvBodies.Name = "dgvBodies";
            this.dgvBodies.RowHeadersVisible = false;
            this.dgvBodies.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvBodies.BackgroundColor = System.Drawing.SystemColors.Window;

            // === grpBodies ===
            this.grpBodies.Controls.Add(this.dgvBodies);
            this.grpBodies.Location = new System.Drawing.Point(12, 12);
            this.grpBodies.Size = new System.Drawing.Size(1076, 510);
            this.grpBodies.Text = "바디 메쉬 크기 설정";

            // === grpBatch ===
            this.lblKeyword.AutoSize = true;
            this.lblKeyword.Location = new System.Drawing.Point(15, 24);
            this.lblKeyword.Text = "키워드:";

            this.txtKeyword.Location = new System.Drawing.Point(70, 21);
            this.txtKeyword.Size = new System.Drawing.Size(200, 21);
            this.txtKeyword.TextChanged += new System.EventHandler(this.txtKeyword_TextChanged);

            this.lblBatchSize.AutoSize = true;
            this.lblBatchSize.Location = new System.Drawing.Point(290, 24);
            this.lblBatchSize.Text = "메쉬크기(mm):";

            this.numBatchSize.DecimalPlaces = 2;
            this.numBatchSize.Location = new System.Drawing.Point(390, 21);
            this.numBatchSize.Size = new System.Drawing.Size(120, 21);
            this.numBatchSize.Minimum = 0.01m;
            this.numBatchSize.Maximum = 100m;
            this.numBatchSize.Value = 2.0m;

            this.btnUpdate.Location = new System.Drawing.Point(530, 19);
            this.btnUpdate.Size = new System.Drawing.Size(120, 26);
            this.btnUpdate.Text = "일괄 업데이트";
            this.btnUpdate.Click += new System.EventHandler(this.btnUpdate_Click);

            this.grpBatch.Controls.Add(this.lblKeyword);
            this.grpBatch.Controls.Add(this.txtKeyword);
            this.grpBatch.Controls.Add(this.lblBatchSize);
            this.grpBatch.Controls.Add(this.numBatchSize);
            this.grpBatch.Controls.Add(this.btnUpdate);
            this.grpBatch.Location = new System.Drawing.Point(12, 528);
            this.grpBatch.Size = new System.Drawing.Size(1076, 55);
            this.grpBatch.Text = "일괄 설정 (키워드 필터 → 크기+옵션 일괄 적용)";

            // === grpOptions (일괄 적용 기본값) ===
            this.lblElementShape.AutoSize = true;
            this.lblElementShape.Location = new System.Drawing.Point(15, 24);
            this.lblElementShape.Text = "요소 형상:";

            this.cmbElementShape.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbElementShape.Location = new System.Drawing.Point(85, 21);
            this.cmbElementShape.Size = new System.Drawing.Size(130, 20);

            this.lblMidsideNodes.AutoSize = true;
            this.lblMidsideNodes.Location = new System.Drawing.Point(240, 24);
            this.lblMidsideNodes.Text = "중간절점:";

            this.cmbMidsideNodes.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbMidsideNodes.Location = new System.Drawing.Point(310, 21);
            this.cmbMidsideNodes.Size = new System.Drawing.Size(130, 20);

            this.lblGrowthRate.AutoSize = true;
            this.lblGrowthRate.Location = new System.Drawing.Point(470, 24);
            this.lblGrowthRate.Text = "성장률:";

            this.numGrowthRate.DecimalPlaces = 2;
            this.numGrowthRate.Location = new System.Drawing.Point(530, 21);
            this.numGrowthRate.Size = new System.Drawing.Size(80, 21);
            this.numGrowthRate.Minimum = 1.0m;
            this.numGrowthRate.Maximum = 5.0m;
            this.numGrowthRate.Increment = 0.1m;
            this.numGrowthRate.Value = 1.2m;

            this.lblSizeFunction.AutoSize = true;
            this.lblSizeFunction.Location = new System.Drawing.Point(640, 24);
            this.lblSizeFunction.Text = "크기함수:";

            this.cmbSizeFunction.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbSizeFunction.Location = new System.Drawing.Point(710, 21);
            this.cmbSizeFunction.Size = new System.Drawing.Size(150, 20);

            this.grpOptions.Controls.Add(this.lblElementShape);
            this.grpOptions.Controls.Add(this.cmbElementShape);
            this.grpOptions.Controls.Add(this.lblMidsideNodes);
            this.grpOptions.Controls.Add(this.cmbMidsideNodes);
            this.grpOptions.Controls.Add(this.lblGrowthRate);
            this.grpOptions.Controls.Add(this.numGrowthRate);
            this.grpOptions.Controls.Add(this.lblSizeFunction);
            this.grpOptions.Controls.Add(this.cmbSizeFunction);
            this.grpOptions.Location = new System.Drawing.Point(12, 589);
            this.grpOptions.Size = new System.Drawing.Size(1076, 52);
            this.grpOptions.Text = "일괄 적용 기본값 (업데이트 버튼 클릭 시 적용)";

            // === Buttons ===
            this.btnApplyAll.Location = new System.Drawing.Point(830, 650);
            this.btnApplyAll.Size = new System.Drawing.Size(140, 34);
            this.btnApplyAll.Text = "전체 적용 (메쉬 생성)";
            this.btnApplyAll.Click += new System.EventHandler(this.btnApplyAll_Click);

            this.btnClose.Location = new System.Drawing.Point(985, 650);
            this.btnClose.Size = new System.Drawing.Size(100, 34);
            this.btnClose.Text = "닫기";
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);

            // === Form ===
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1100, 700);
            this.Controls.Add(this.grpBodies);
            this.Controls.Add(this.grpBatch);
            this.Controls.Add(this.grpOptions);
            this.Controls.Add(this.btnApplyAll);
            this.Controls.Add(this.btnClose);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MeshSettingsDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "격자 설정 (Mesh Settings)";
            this.grpBodies.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvBodies)).EndInit();
            this.grpBatch.ResumeLayout(false);
            this.grpBatch.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numBatchSize)).EndInit();
            this.grpOptions.ResumeLayout(false);
            this.grpOptions.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numGrowthRate)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.GroupBox grpBodies;
        private System.Windows.Forms.DataGridView dgvBodies;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colCheck;
        private System.Windows.Forms.DataGridViewTextBoxColumn colBodyName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMeshX;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMeshY;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMeshZ;
        private System.Windows.Forms.DataGridViewTextBoxColumn colElemSize;
        private System.Windows.Forms.DataGridViewComboBoxColumn colShape;
        private System.Windows.Forms.DataGridViewComboBoxColumn colMidside;
        private System.Windows.Forms.DataGridViewTextBoxColumn colGrowth;
        private System.Windows.Forms.DataGridViewComboBoxColumn colSizeFunc;
        private System.Windows.Forms.GroupBox grpBatch;
        private System.Windows.Forms.Label lblKeyword;
        private System.Windows.Forms.TextBox txtKeyword;
        private System.Windows.Forms.Label lblBatchSize;
        private System.Windows.Forms.NumericUpDown numBatchSize;
        private System.Windows.Forms.Button btnUpdate;
        private System.Windows.Forms.GroupBox grpOptions;
        private System.Windows.Forms.Label lblElementShape;
        private System.Windows.Forms.ComboBox cmbElementShape;
        private System.Windows.Forms.Label lblMidsideNodes;
        private System.Windows.Forms.ComboBox cmbMidsideNodes;
        private System.Windows.Forms.Label lblGrowthRate;
        private System.Windows.Forms.NumericUpDown numGrowthRate;
        private System.Windows.Forms.Label lblSizeFunction;
        private System.Windows.Forms.ComboBox cmbSizeFunction;
        private System.Windows.Forms.Button btnApplyAll;
        private System.Windows.Forms.Button btnClose;
    }
}
