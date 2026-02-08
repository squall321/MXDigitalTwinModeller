namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    partial class LaminateDialog
    {
        private System.ComponentModel.IContainer components = null;

        // 모드 선택 그룹
        private System.Windows.Forms.GroupBox grpMode;
        private System.Windows.Forms.RadioButton rdoRectangular;
        private System.Windows.Forms.RadioButton rdoSurface;
        private System.Windows.Forms.RadioButton rdoSolid;

        // 기본 치수 그룹 (직사각형 모드)
        private System.Windows.Forms.GroupBox grpDimensions;
        private System.Windows.Forms.Label lblWidth;
        private System.Windows.Forms.NumericUpDown numWidth;
        private System.Windows.Forms.Label lblLength;
        private System.Windows.Forms.NumericUpDown numLength;
        private System.Windows.Forms.Label lblStackDir;
        private System.Windows.Forms.ComboBox cmbStackDir;

        // 면 선택 그룹 (서피스 모드)
        private System.Windows.Forms.GroupBox grpSurface;
        private System.Windows.Forms.Label lblSelectedFace;
        private System.Windows.Forms.Button btnSelectFace;
        private System.Windows.Forms.Label lblFaceInfo;
        private System.Windows.Forms.Label lblOffsetDir;
        private System.Windows.Forms.ComboBox cmbOffsetDir;

        // 솔리드 적층 그룹 (솔리드 모드)
        private System.Windows.Forms.GroupBox grpSolid;
        private System.Windows.Forms.Label lblSelectedBody;
        private System.Windows.Forms.Button btnSelectBody;
        private System.Windows.Forms.Label lblBodyInfo;
        private System.Windows.Forms.Label lblDetectedThickness;
        private System.Windows.Forms.Label lblDetectedNormal;

        // 레이어 정의 그룹
        private System.Windows.Forms.GroupBox grpLayers;
        private System.Windows.Forms.DataGridView dgvLayers;
        private System.Windows.Forms.DataGridViewTextBoxColumn colIndex;
        private System.Windows.Forms.DataGridViewTextBoxColumn colName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colThickness;
        private System.Windows.Forms.Button btnAddLayer;
        private System.Windows.Forms.Button btnRemoveLayer;
        private System.Windows.Forms.Button btnMoveUp;
        private System.Windows.Forms.Button btnMoveDown;
        private System.Windows.Forms.Label lblTotalThickness;
        private System.Windows.Forms.Button btnMatchThickness;

        // 옵션 그룹
        private System.Windows.Forms.GroupBox grpOptions;
        private System.Windows.Forms.CheckBox chkShareTopology;
        private System.Windows.Forms.CheckBox chkInterfaceNS;
        private System.Windows.Forms.CheckBox chkDeleteOriginal;

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
            this.grpMode = new System.Windows.Forms.GroupBox();
            this.rdoRectangular = new System.Windows.Forms.RadioButton();
            this.rdoSurface = new System.Windows.Forms.RadioButton();
            this.rdoSolid = new System.Windows.Forms.RadioButton();

            this.grpDimensions = new System.Windows.Forms.GroupBox();
            this.lblWidth = new System.Windows.Forms.Label();
            this.numWidth = new System.Windows.Forms.NumericUpDown();
            this.lblLength = new System.Windows.Forms.Label();
            this.numLength = new System.Windows.Forms.NumericUpDown();
            this.lblStackDir = new System.Windows.Forms.Label();
            this.cmbStackDir = new System.Windows.Forms.ComboBox();

            this.grpSurface = new System.Windows.Forms.GroupBox();
            this.lblSelectedFace = new System.Windows.Forms.Label();
            this.btnSelectFace = new System.Windows.Forms.Button();
            this.lblFaceInfo = new System.Windows.Forms.Label();
            this.lblOffsetDir = new System.Windows.Forms.Label();
            this.cmbOffsetDir = new System.Windows.Forms.ComboBox();

            this.grpSolid = new System.Windows.Forms.GroupBox();
            this.lblSelectedBody = new System.Windows.Forms.Label();
            this.btnSelectBody = new System.Windows.Forms.Button();
            this.lblBodyInfo = new System.Windows.Forms.Label();
            this.lblDetectedThickness = new System.Windows.Forms.Label();
            this.lblDetectedNormal = new System.Windows.Forms.Label();

            this.grpLayers = new System.Windows.Forms.GroupBox();
            this.dgvLayers = new System.Windows.Forms.DataGridView();
            this.colIndex = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colThickness = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnAddLayer = new System.Windows.Forms.Button();
            this.btnRemoveLayer = new System.Windows.Forms.Button();
            this.btnMoveUp = new System.Windows.Forms.Button();
            this.btnMoveDown = new System.Windows.Forms.Button();
            this.lblTotalThickness = new System.Windows.Forms.Label();
            this.btnMatchThickness = new System.Windows.Forms.Button();

            this.grpOptions = new System.Windows.Forms.GroupBox();
            this.chkShareTopology = new System.Windows.Forms.CheckBox();
            this.chkInterfaceNS = new System.Windows.Forms.CheckBox();
            this.chkDeleteOriginal = new System.Windows.Forms.CheckBox();

            this.btnPreview = new System.Windows.Forms.Button();
            this.btnCreate = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();

            ((System.ComponentModel.ISupportInitialize)(this.numWidth)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLength)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvLayers)).BeginInit();
            this.grpMode.SuspendLayout();
            this.grpDimensions.SuspendLayout();
            this.grpSurface.SuspendLayout();
            this.grpSolid.SuspendLayout();
            this.grpLayers.SuspendLayout();
            this.grpOptions.SuspendLayout();
            this.SuspendLayout();

            // ==========================================
            //  grpMode - 모드 선택
            // ==========================================
            this.grpMode.Controls.Add(this.rdoRectangular);
            this.grpMode.Controls.Add(this.rdoSurface);
            this.grpMode.Controls.Add(this.rdoSolid);
            this.grpMode.Location = new System.Drawing.Point(12, 12);
            this.grpMode.Name = "grpMode";
            this.grpMode.Size = new System.Drawing.Size(460, 50);
            this.grpMode.TabIndex = 0;
            this.grpMode.TabStop = false;
            this.grpMode.Text = "생성 모드";

            this.rdoRectangular.AutoSize = true;
            this.rdoRectangular.Checked = true;
            this.rdoRectangular.Location = new System.Drawing.Point(10, 22);
            this.rdoRectangular.Name = "rdoRectangular";
            this.rdoRectangular.Size = new System.Drawing.Size(150, 16);
            this.rdoRectangular.TabIndex = 0;
            this.rdoRectangular.TabStop = true;
            this.rdoRectangular.Text = "직사각형 (Rectangular)";
            this.rdoRectangular.UseVisualStyleBackColor = true;
            this.rdoRectangular.CheckedChanged += new System.EventHandler(this.rdoMode_CheckedChanged);

            this.rdoSurface.AutoSize = true;
            this.rdoSurface.Location = new System.Drawing.Point(165, 22);
            this.rdoSurface.Name = "rdoSurface";
            this.rdoSurface.Size = new System.Drawing.Size(150, 16);
            this.rdoSurface.TabIndex = 1;
            this.rdoSurface.Text = "면 기반 (Surface)";
            this.rdoSurface.UseVisualStyleBackColor = true;

            this.rdoSolid.AutoSize = true;
            this.rdoSolid.Location = new System.Drawing.Point(320, 22);
            this.rdoSolid.Name = "rdoSolid";
            this.rdoSolid.Size = new System.Drawing.Size(130, 16);
            this.rdoSolid.TabIndex = 2;
            this.rdoSolid.Text = "솔리드 (Solid)";
            this.rdoSolid.UseVisualStyleBackColor = true;

            // ==========================================
            //  grpDimensions - 기본 치수 (직사각형 모드)
            // ==========================================
            this.grpDimensions.Controls.Add(this.lblWidth);
            this.grpDimensions.Controls.Add(this.numWidth);
            this.grpDimensions.Controls.Add(this.lblLength);
            this.grpDimensions.Controls.Add(this.numLength);
            this.grpDimensions.Controls.Add(this.lblStackDir);
            this.grpDimensions.Controls.Add(this.cmbStackDir);
            this.grpDimensions.Location = new System.Drawing.Point(12, 68);
            this.grpDimensions.Name = "grpDimensions";
            this.grpDimensions.Size = new System.Drawing.Size(460, 105);
            this.grpDimensions.TabIndex = 1;
            this.grpDimensions.TabStop = false;
            this.grpDimensions.Text = "기본 치수 (mm)";

            int dy = 25;
            int dyGap = 30;

            // 폭
            this.lblWidth.AutoSize = true;
            this.lblWidth.Location = new System.Drawing.Point(15, dy);
            this.lblWidth.Name = "lblWidth";
            this.lblWidth.Text = "폭 (Width):";

            this.numWidth.DecimalPlaces = 2;
            this.numWidth.Location = new System.Drawing.Point(120, dy - 3);
            this.numWidth.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            this.numWidth.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numWidth.Name = "numWidth";
            this.numWidth.Size = new System.Drawing.Size(120, 21);
            this.numWidth.TabIndex = 2;
            this.numWidth.Value = new decimal(new int[] { 100, 0, 0, 0 });
            dy += dyGap;

            // 길이
            this.lblLength.AutoSize = true;
            this.lblLength.Location = new System.Drawing.Point(15, dy);
            this.lblLength.Name = "lblLength";
            this.lblLength.Text = "길이 (Length):";

            this.numLength.DecimalPlaces = 2;
            this.numLength.Location = new System.Drawing.Point(120, dy - 3);
            this.numLength.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            this.numLength.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numLength.Name = "numLength";
            this.numLength.Size = new System.Drawing.Size(120, 21);
            this.numLength.TabIndex = 3;
            this.numLength.Value = new decimal(new int[] { 100, 0, 0, 0 });

            // 적층 방향
            this.lblStackDir.AutoSize = true;
            this.lblStackDir.Location = new System.Drawing.Point(270, 25);
            this.lblStackDir.Name = "lblStackDir";
            this.lblStackDir.Text = "적층 방향:";

            this.cmbStackDir.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbStackDir.Items.AddRange(new object[] { "X", "Y", "Z" });
            this.cmbStackDir.Location = new System.Drawing.Point(340, 22);
            this.cmbStackDir.Name = "cmbStackDir";
            this.cmbStackDir.Size = new System.Drawing.Size(60, 20);
            this.cmbStackDir.TabIndex = 4;
            this.cmbStackDir.SelectedIndex = 2; // Z

            // ==========================================
            //  grpSurface - 면 선택 (서피스 모드)
            // ==========================================
            this.grpSurface.Controls.Add(this.lblSelectedFace);
            this.grpSurface.Controls.Add(this.btnSelectFace);
            this.grpSurface.Controls.Add(this.lblFaceInfo);
            this.grpSurface.Controls.Add(this.lblOffsetDir);
            this.grpSurface.Controls.Add(this.cmbOffsetDir);
            this.grpSurface.Location = new System.Drawing.Point(12, 68);
            this.grpSurface.Name = "grpSurface";
            this.grpSurface.Size = new System.Drawing.Size(460, 105);
            this.grpSurface.TabIndex = 1;
            this.grpSurface.TabStop = false;
            this.grpSurface.Text = "면 선택 (Surface)";
            this.grpSurface.Visible = false;

            this.lblSelectedFace.AutoSize = true;
            this.lblSelectedFace.Location = new System.Drawing.Point(15, 25);
            this.lblSelectedFace.Name = "lblSelectedFace";
            this.lblSelectedFace.Text = "선택된 면:";

            this.btnSelectFace.Location = new System.Drawing.Point(120, 20);
            this.btnSelectFace.Name = "btnSelectFace";
            this.btnSelectFace.Size = new System.Drawing.Size(120, 25);
            this.btnSelectFace.TabIndex = 2;
            this.btnSelectFace.Text = "면 선택 (Pick)";
            this.btnSelectFace.UseVisualStyleBackColor = true;
            this.btnSelectFace.Click += new System.EventHandler(this.btnSelectFace_Click);

            this.lblFaceInfo.AutoSize = true;
            this.lblFaceInfo.Location = new System.Drawing.Point(250, 25);
            this.lblFaceInfo.Name = "lblFaceInfo";
            this.lblFaceInfo.Text = "면이 선택되지 않았습니다.";
            this.lblFaceInfo.ForeColor = System.Drawing.Color.Gray;

            this.lblOffsetDir.AutoSize = true;
            this.lblOffsetDir.Location = new System.Drawing.Point(15, 60);
            this.lblOffsetDir.Name = "lblOffsetDir";
            this.lblOffsetDir.Text = "오프셋 방향:";

            this.cmbOffsetDir.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbOffsetDir.Items.AddRange(new object[] { "법선 방향 (Normal)", "역방향 (Reverse)" });
            this.cmbOffsetDir.Location = new System.Drawing.Point(120, 57);
            this.cmbOffsetDir.Name = "cmbOffsetDir";
            this.cmbOffsetDir.Size = new System.Drawing.Size(180, 20);
            this.cmbOffsetDir.TabIndex = 3;
            this.cmbOffsetDir.SelectedIndex = 0;

            // ==========================================
            //  grpSolid - 솔리드 선택 (솔리드 모드)
            // ==========================================
            this.grpSolid.Controls.Add(this.lblSelectedBody);
            this.grpSolid.Controls.Add(this.btnSelectBody);
            this.grpSolid.Controls.Add(this.lblBodyInfo);
            this.grpSolid.Controls.Add(this.lblDetectedThickness);
            this.grpSolid.Controls.Add(this.lblDetectedNormal);
            this.grpSolid.Location = new System.Drawing.Point(12, 68);
            this.grpSolid.Name = "grpSolid";
            this.grpSolid.Size = new System.Drawing.Size(460, 105);
            this.grpSolid.TabIndex = 1;
            this.grpSolid.TabStop = false;
            this.grpSolid.Text = "솔리드 선택 (Solid)";
            this.grpSolid.Visible = false;

            this.lblSelectedBody.AutoSize = true;
            this.lblSelectedBody.Location = new System.Drawing.Point(15, 25);
            this.lblSelectedBody.Name = "lblSelectedBody";
            this.lblSelectedBody.Text = "바디:";

            this.btnSelectBody.Location = new System.Drawing.Point(120, 20);
            this.btnSelectBody.Name = "btnSelectBody";
            this.btnSelectBody.Size = new System.Drawing.Size(120, 25);
            this.btnSelectBody.TabIndex = 2;
            this.btnSelectBody.Text = "바디 선택 (Pick)";
            this.btnSelectBody.UseVisualStyleBackColor = true;
            this.btnSelectBody.Click += new System.EventHandler(this.btnSelectBody_Click);

            this.lblBodyInfo.AutoSize = true;
            this.lblBodyInfo.Location = new System.Drawing.Point(250, 25);
            this.lblBodyInfo.Name = "lblBodyInfo";
            this.lblBodyInfo.Text = "바디가 선택되지 않았습니다.";
            this.lblBodyInfo.ForeColor = System.Drawing.Color.Gray;

            this.lblDetectedThickness.AutoSize = true;
            this.lblDetectedThickness.Location = new System.Drawing.Point(15, 55);
            this.lblDetectedThickness.Name = "lblDetectedThickness";
            this.lblDetectedThickness.Text = "감지된 두께: -- mm";

            this.lblDetectedNormal.AutoSize = true;
            this.lblDetectedNormal.Location = new System.Drawing.Point(15, 80);
            this.lblDetectedNormal.Name = "lblDetectedNormal";
            this.lblDetectedNormal.Text = "적층 방향: --";

            // ==========================================
            //  grpLayers - 레이어 정의
            // ==========================================
            this.grpLayers.Controls.Add(this.dgvLayers);
            this.grpLayers.Controls.Add(this.btnAddLayer);
            this.grpLayers.Controls.Add(this.btnRemoveLayer);
            this.grpLayers.Controls.Add(this.btnMoveUp);
            this.grpLayers.Controls.Add(this.btnMoveDown);
            this.grpLayers.Controls.Add(this.lblTotalThickness);
            this.grpLayers.Controls.Add(this.btnMatchThickness);
            this.grpLayers.Location = new System.Drawing.Point(12, 179);
            this.grpLayers.Name = "grpLayers";
            this.grpLayers.Size = new System.Drawing.Size(460, 280);
            this.grpLayers.TabIndex = 2;
            this.grpLayers.TabStop = false;
            this.grpLayers.Text = "레이어 정의";

            // DataGridView
            this.dgvLayers.Location = new System.Drawing.Point(15, 22);
            this.dgvLayers.Name = "dgvLayers";
            this.dgvLayers.Size = new System.Drawing.Size(350, 215);
            this.dgvLayers.TabIndex = 5;
            this.dgvLayers.AllowUserToAddRows = false;
            this.dgvLayers.AllowUserToDeleteRows = false;
            this.dgvLayers.AllowUserToResizeRows = false;
            this.dgvLayers.RowHeadersVisible = false;
            this.dgvLayers.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvLayers.MultiSelect = false;
            this.dgvLayers.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;

            // Column: #
            this.colIndex.HeaderText = "#";
            this.colIndex.Name = "colIndex";
            this.colIndex.ReadOnly = true;
            this.colIndex.FillWeight = 30;
            this.colIndex.DefaultCellStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dgvLayers.Columns.Add(this.colIndex);

            // Column: 이름
            this.colName.HeaderText = "이름 (Name)";
            this.colName.Name = "colName";
            this.colName.FillWeight = 100;
            this.dgvLayers.Columns.Add(this.colName);

            // Column: 두께
            this.colThickness.HeaderText = "두께 (mm)";
            this.colThickness.Name = "colThickness";
            this.colThickness.FillWeight = 60;
            this.colThickness.DefaultCellStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.dgvLayers.Columns.Add(this.colThickness);

            // 버튼 (오른쪽 배치)
            int bx = 375;
            int by = 22;
            int bGap = 32;

            this.btnAddLayer.Location = new System.Drawing.Point(bx, by);
            this.btnAddLayer.Name = "btnAddLayer";
            this.btnAddLayer.Size = new System.Drawing.Size(70, 25);
            this.btnAddLayer.TabIndex = 6;
            this.btnAddLayer.Text = "추가";
            this.btnAddLayer.UseVisualStyleBackColor = true;
            this.btnAddLayer.Click += new System.EventHandler(this.btnAddLayer_Click);
            by += bGap;

            this.btnRemoveLayer.Location = new System.Drawing.Point(bx, by);
            this.btnRemoveLayer.Name = "btnRemoveLayer";
            this.btnRemoveLayer.Size = new System.Drawing.Size(70, 25);
            this.btnRemoveLayer.TabIndex = 7;
            this.btnRemoveLayer.Text = "삭제";
            this.btnRemoveLayer.UseVisualStyleBackColor = true;
            this.btnRemoveLayer.Click += new System.EventHandler(this.btnRemoveLayer_Click);
            by += bGap;

            this.btnMoveUp.Location = new System.Drawing.Point(bx, by);
            this.btnMoveUp.Name = "btnMoveUp";
            this.btnMoveUp.Size = new System.Drawing.Size(70, 25);
            this.btnMoveUp.TabIndex = 8;
            this.btnMoveUp.Text = "위로 \u2191";
            this.btnMoveUp.UseVisualStyleBackColor = true;
            this.btnMoveUp.Click += new System.EventHandler(this.btnMoveUp_Click);
            by += bGap;

            this.btnMoveDown.Location = new System.Drawing.Point(bx, by);
            this.btnMoveDown.Name = "btnMoveDown";
            this.btnMoveDown.Size = new System.Drawing.Size(70, 25);
            this.btnMoveDown.TabIndex = 9;
            this.btnMoveDown.Text = "아래로 \u2193";
            this.btnMoveDown.UseVisualStyleBackColor = true;
            this.btnMoveDown.Click += new System.EventHandler(this.btnMoveDown_Click);
            by += bGap;

            // 두께 맞춤 버튼 (솔리드 모드에서만 표시)
            this.btnMatchThickness.Location = new System.Drawing.Point(bx, by);
            this.btnMatchThickness.Name = "btnMatchThickness";
            this.btnMatchThickness.Size = new System.Drawing.Size(70, 25);
            this.btnMatchThickness.TabIndex = 15;
            this.btnMatchThickness.Text = "두께 맞춤";
            this.btnMatchThickness.UseVisualStyleBackColor = true;
            this.btnMatchThickness.Visible = false;
            this.btnMatchThickness.Click += new System.EventHandler(this.btnMatchThickness_Click);

            // 총 두께 레이블
            this.lblTotalThickness.AutoSize = true;
            this.lblTotalThickness.Location = new System.Drawing.Point(15, 245);
            this.lblTotalThickness.Name = "lblTotalThickness";
            this.lblTotalThickness.Text = "총 두께: 0.75 mm  /  총 3층";

            // ==========================================
            //  grpOptions - 옵션
            // ==========================================
            this.grpOptions.Controls.Add(this.chkShareTopology);
            this.grpOptions.Controls.Add(this.chkInterfaceNS);
            this.grpOptions.Controls.Add(this.chkDeleteOriginal);
            this.grpOptions.Location = new System.Drawing.Point(12, 465);
            this.grpOptions.Name = "grpOptions";
            this.grpOptions.Size = new System.Drawing.Size(460, 55);
            this.grpOptions.TabIndex = 3;
            this.grpOptions.TabStop = false;
            this.grpOptions.Text = "옵션";

            this.chkShareTopology.AutoSize = true;
            this.chkShareTopology.Checked = true;
            this.chkShareTopology.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkShareTopology.Location = new System.Drawing.Point(15, 25);
            this.chkShareTopology.Name = "chkShareTopology";
            this.chkShareTopology.Size = new System.Drawing.Size(150, 16);
            this.chkShareTopology.TabIndex = 10;
            this.chkShareTopology.Text = "Share Topology 활성화";
            this.chkShareTopology.UseVisualStyleBackColor = true;

            this.chkInterfaceNS.AutoSize = true;
            this.chkInterfaceNS.Checked = true;
            this.chkInterfaceNS.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkInterfaceNS.Location = new System.Drawing.Point(230, 25);
            this.chkInterfaceNS.Name = "chkInterfaceNS";
            this.chkInterfaceNS.Size = new System.Drawing.Size(200, 16);
            this.chkInterfaceNS.TabIndex = 11;
            this.chkInterfaceNS.Text = "계면 Named Selection 생성";
            this.chkInterfaceNS.UseVisualStyleBackColor = true;

            this.chkDeleteOriginal.AutoSize = true;
            this.chkDeleteOriginal.Checked = true;
            this.chkDeleteOriginal.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkDeleteOriginal.Location = new System.Drawing.Point(15, 25);
            this.chkDeleteOriginal.Name = "chkDeleteOriginal";
            this.chkDeleteOriginal.Size = new System.Drawing.Size(150, 16);
            this.chkDeleteOriginal.TabIndex = 16;
            this.chkDeleteOriginal.Text = "원본 바디 삭제";
            this.chkDeleteOriginal.UseVisualStyleBackColor = true;
            this.chkDeleteOriginal.Visible = false;

            // ==========================================
            //  버튼
            // ==========================================
            int btnY = 530;

            this.btnPreview.Location = new System.Drawing.Point(150, btnY);
            this.btnPreview.Name = "btnPreview";
            this.btnPreview.Size = new System.Drawing.Size(100, 30);
            this.btnPreview.TabIndex = 12;
            this.btnPreview.Text = "미리보기";
            this.btnPreview.UseVisualStyleBackColor = true;
            this.btnPreview.Click += new System.EventHandler(this.btnPreview_Click);

            this.btnCreate.Location = new System.Drawing.Point(260, btnY);
            this.btnCreate.Name = "btnCreate";
            this.btnCreate.Size = new System.Drawing.Size(100, 30);
            this.btnCreate.TabIndex = 13;
            this.btnCreate.Text = "생성";
            this.btnCreate.UseVisualStyleBackColor = true;
            this.btnCreate.Click += new System.EventHandler(this.btnCreate_Click);

            this.btnCancel.Location = new System.Drawing.Point(370, btnY);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 30);
            this.btnCancel.TabIndex = 14;
            this.btnCancel.Text = "취소";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);

            // ==========================================
            //  LaminateDialog
            // ==========================================
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(484, 575);
            this.Controls.Add(this.grpMode);
            this.Controls.Add(this.grpDimensions);
            this.Controls.Add(this.grpSurface);
            this.Controls.Add(this.grpSolid);
            this.Controls.Add(this.grpLayers);
            this.Controls.Add(this.grpOptions);
            this.Controls.Add(this.btnPreview);
            this.Controls.Add(this.btnCreate);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "LaminateDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "적층 모델 생성 (Laminate Model)";

            ((System.ComponentModel.ISupportInitialize)(this.numWidth)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLength)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvLayers)).EndInit();
            this.grpMode.ResumeLayout(false);
            this.grpMode.PerformLayout();
            this.grpDimensions.ResumeLayout(false);
            this.grpDimensions.PerformLayout();
            this.grpSurface.ResumeLayout(false);
            this.grpSurface.PerformLayout();
            this.grpSolid.ResumeLayout(false);
            this.grpSolid.PerformLayout();
            this.grpLayers.ResumeLayout(false);
            this.grpLayers.PerformLayout();
            this.grpOptions.ResumeLayout(false);
            this.grpOptions.PerformLayout();
            this.ResumeLayout(false);
        }
    }
}
