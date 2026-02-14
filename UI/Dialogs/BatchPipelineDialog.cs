using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SpaceClaim.Api.V252.Analysis;
using SpaceClaim.Api.V252.Scripting.Commands;
using SpaceClaim.Api.V252.Scripting.Commands.CommandOptions;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Contact;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Simplify;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Contact;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Material;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Mesh;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Simplify;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Export;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    public class BatchPipelineDialog : Form
    {
        private readonly Part _part;

        // Step 체크박스
        private CheckBox chkSimplify, chkMaterial, chkContact, chkMesh, chkExport;

        // Step 1: Simplify
        private TextBox txtSimplifyKeyword;
        private ComboBox cmbSimplifyMode;

        // Step 2: Material
        private ComboBox cmbMaterialPreset;
        private TextBox txtMaterialKeyword;

        // Step 3: Contact
        private TextBox txtContactKwA, txtContactKwB;
        private NumericUpDown nudContactTol;

        // Step 4: Mesh
        private NumericUpDown nudMeshSize;
        private ComboBox cmbMeshShape, cmbMeshMidside, cmbMeshSizeFunc;
        private NumericUpDown nudMeshGrowth;

        // Step 5: Export
        private ComboBox cmbExportFormat;
        private TextBox txtExportPath;

        // 진행/로그
        private ProgressBar progressBar;
        private Label lblProgress;
        private TextBox txtLog;
        private Button btnRun;
        private Button btnClose;
        private CheckBox chkContinueOnError;

        // 내보내기 확장자 매핑
        private static readonly string[] ExportExtensions = { ".k", ".cdb", ".inp", ".msh", ".cgns" };

        public BatchPipelineDialog(Part part)
        {
            _part = part;
            InitializeLayout();
            SetDefaultExportPath();
        }

        private void InitializeLayout()
        {
            Text = "Batch Pipeline (\uC77C\uAD04 \uC2E4\uD589)";
            Width = 720;
            Height = 700;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;

            int y = 12;
            int lx = 32;
            int cx = 130;

            // ═══ Step 1: Simplify ═══
            chkSimplify = new CheckBox
            {
                Text = "Step 1: \uB2E8\uC21C\uD654 (Simplify)",
                Location = new Point(12, y),
                AutoSize = true,
                Checked = false,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold)
            };
            y += 24;

            var lblSimpKw = new Label { Text = "\uD0A4\uC6CC\uB4DC:", Location = new Point(lx, y + 3), AutoSize = true };
            txtSimplifyKeyword = new TextBox { Location = new Point(cx, y), Width = 200 };
            var lblSimpMode = new Label { Text = "\uBAA8\uB4DC:", Location = new Point(cx + 215, y + 3), AutoSize = true };
            cmbSimplifyMode = new ComboBox
            {
                Location = new Point(cx + 260, y),
                Width = 130,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbSimplifyMode.Items.AddRange(new object[] { "BoundingBox", "SolidToShell" });
            cmbSimplifyMode.SelectedIndex = 0;
            y += 34;

            // ═══ Step 2: Material ═══
            chkMaterial = new CheckBox
            {
                Text = "Step 2: \uC7AC\uB8CC \uC801\uC6A9 (Material)",
                Location = new Point(12, y),
                AutoSize = true,
                Checked = true,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold)
            };
            y += 24;

            var lblMatPreset = new Label { Text = "\uD504\uB9AC\uC14B:", Location = new Point(lx, y + 3), AutoSize = true };
            cmbMaterialPreset = new ComboBox
            {
                Location = new Point(cx, y),
                Width = 130,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbMaterialPreset.Items.AddRange(new object[] { "Steel", "Aluminum", "CFRP" });
            cmbMaterialPreset.SelectedIndex = 0;
            var lblMatKw = new Label { Text = "\uD0A4\uC6CC\uB4DC:", Location = new Point(cx + 150, y + 3), AutoSize = true };
            txtMaterialKeyword = new TextBox { Location = new Point(cx + 210, y), Width = 180 };
            var lblMatHint = new Label
            {
                Text = "\uBE44\uC6CC\uB450\uBA74 \uC804\uCCB4 \uBC14\uB514\uC5D0 \uC801\uC6A9",
                Location = new Point(cx + 395, y + 3),
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Font = new Font(Font.FontFamily, 8f)
            };
            y += 34;

            // ═══ Step 3: Contact ═══
            chkContact = new CheckBox
            {
                Text = "Step 3: \uC811\uCD09 \uAC10\uC9C0 (Contact Detection)",
                Location = new Point(12, y),
                AutoSize = true,
                Checked = true,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold)
            };
            y += 24;

            var lblCtKwA = new Label { Text = "\uD0A4\uC6CC\uB4DC A:", Location = new Point(lx, y + 3), AutoSize = true };
            txtContactKwA = new TextBox { Location = new Point(cx, y), Width = 130 };
            var lblCtKwB = new Label { Text = "\uD0A4\uC6CC\uB4DC B:", Location = new Point(cx + 150, y + 3), AutoSize = true };
            txtContactKwB = new TextBox { Location = new Point(cx + 220, y), Width = 130 };
            var lblCtTol = new Label { Text = "\uD5C8\uC6A9(mm):", Location = new Point(cx + 370, y + 3), AutoSize = true };
            nudContactTol = new NumericUpDown
            {
                Location = new Point(cx + 440, y),
                Width = 70,
                Minimum = 0.01m,
                Maximum = 10.0m,
                DecimalPlaces = 2,
                Increment = 0.1m,
                Value = 1.00m
            };
            y += 34;

            // ═══ Step 4: Mesh ═══
            chkMesh = new CheckBox
            {
                Text = "Step 4: \uBA54\uC26C \uC0DD\uC131 (Mesh)",
                Location = new Point(12, y),
                AutoSize = true,
                Checked = true,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold)
            };
            y += 24;

            var lblMeshSize = new Label { Text = "\uD06C\uAE30(mm):", Location = new Point(lx, y + 3), AutoSize = true };
            nudMeshSize = new NumericUpDown
            {
                Location = new Point(cx, y),
                Width = 80,
                Minimum = 0.1m,
                Maximum = 100m,
                DecimalPlaces = 2,
                Value = 2.0m
            };
            var lblMeshShape = new Label { Text = "\uD615\uC0C1:", Location = new Point(cx + 95, y + 3), AutoSize = true };
            cmbMeshShape = new ComboBox
            {
                Location = new Point(cx + 140, y),
                Width = 65,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbMeshShape.Items.AddRange(new object[] { "Tet", "Hex", "Quad", "Tri" });
            cmbMeshShape.SelectedIndex = 0;

            var lblMeshMid = new Label { Text = "\uC911\uAC04\uC808\uC810:", Location = new Point(cx + 220, y + 3), AutoSize = true };
            cmbMeshMidside = new ComboBox
            {
                Location = new Point(cx + 290, y),
                Width = 85,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbMeshMidside.Items.AddRange(new object[] { "Dropped", "Kept", "Auto" });
            cmbMeshMidside.SelectedIndex = 0;
            y += 26;

            var lblMeshGrowth = new Label { Text = "\uC131\uC7A5\uB960:", Location = new Point(lx, y + 3), AutoSize = true };
            nudMeshGrowth = new NumericUpDown
            {
                Location = new Point(cx, y),
                Width = 80,
                Minimum = 1.0m,
                Maximum = 5.0m,
                DecimalPlaces = 2,
                Increment = 0.1m,
                Value = 1.80m
            };
            var lblMeshSizeFunc = new Label { Text = "\uD06C\uAE30\uD568\uC218:", Location = new Point(cx + 95, y + 3), AutoSize = true };
            cmbMeshSizeFunc = new ComboBox
            {
                Location = new Point(cx + 165, y),
                Width = 110,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbMeshSizeFunc.Items.AddRange(new object[] { "Curv+Prox", "Curv", "Prox", "Fixed" });
            cmbMeshSizeFunc.SelectedIndex = 3; // Fixed
            y += 34;

            // ═══ Step 5: Export ═══
            chkExport = new CheckBox
            {
                Text = "Step 5: \uBA54\uC26C \uB0B4\uBCF4\uB0B4\uAE30 (Export)",
                Location = new Point(12, y),
                AutoSize = true,
                Checked = true,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold)
            };
            y += 24;

            var lblExpFmt = new Label { Text = "\uD3EC\uB9F7:", Location = new Point(lx, y + 3), AutoSize = true };
            cmbExportFormat = new ComboBox
            {
                Location = new Point(cx, y),
                Width = 140,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbExportFormat.Items.AddRange(new object[]
            {
                "LS-DYNA (.k)", "ANSYS (.cdb)", "Abaqus (.inp)",
                "Fluent (.msh)", "CGNS (.cgns)"
            });
            cmbExportFormat.SelectedIndex = 0;
            cmbExportFormat.SelectedIndexChanged += (s, e) => UpdateExportExtension();

            var lblExpPath = new Label { Text = "\uACBD\uB85C:", Location = new Point(cx + 155, y + 3), AutoSize = true };
            txtExportPath = new TextBox { Location = new Point(cx + 195, y), Width = 280 };
            var btnBrowse = new Button { Text = "...", Location = new Point(cx + 480, y), Width = 30, Height = 23 };
            btnBrowse.Click += btnBrowse_Click;
            y += 38;

            // ═══ 구분선 ═══
            var separator = new Label
            {
                BorderStyle = BorderStyle.Fixed3D,
                Location = new Point(12, y),
                Size = new Size(680, 2)
            };
            y += 10;

            // ═══ 옵션 + 버튼 ═══
            chkContinueOnError = new CheckBox
            {
                Text = "\uC624\uB958 \uC2DC \uACC4\uC18D \uC9C4\uD589",
                Location = new Point(12, y + 3),
                AutoSize = true,
                Checked = true
            };

            var btnSaveCfg = new Button
            {
                Text = "\uC124\uC815 \uC800\uC7A5",
                Location = new Point(260, y),
                Width = 85,
                Height = 28
            };
            btnSaveCfg.Click += btnSaveCfg_Click;

            var btnLoadCfg = new Button
            {
                Text = "\uC124\uC815 \uBD88\uB7EC\uC624\uAE30",
                Location = new Point(350, y),
                Width = 95,
                Height = 28
            };
            btnLoadCfg.Click += btnLoadCfg_Click;

            btnRun = new Button
            {
                Text = "\u25B6 \uD30C\uC774\uD504\uB77C\uC778 \uC2E4\uD589",
                Location = new Point(470, y),
                Width = 140,
                Height = 28,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold)
            };
            btnRun.Click += btnRun_Click;

            btnClose = new Button
            {
                Text = "\uB2EB\uAE30",
                Location = new Point(618, y),
                Width = 70,
                Height = 28
            };
            btnClose.Click += (s, e) => Close();
            y += 36;

            // ═══ 진행 바 ═══
            lblProgress = new Label
            {
                Text = "\uB300\uAE30 \uC911",
                Location = new Point(12, y + 2),
                Width = 180,
                AutoSize = false
            };
            progressBar = new ProgressBar
            {
                Location = new Point(200, y),
                Size = new Size(490, 20),
                Style = ProgressBarStyle.Continuous,
                Maximum = 100
            };
            y += 28;

            // ═══ 로그 ═══
            txtLog = new TextBox
            {
                Location = new Point(12, y),
                Size = new Size(680, 700 - y - 40),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9f),
                BackColor = SystemColors.Window
            };

            var btnCopyLog = new Button
            {
                Text = "\uBCF5\uC0AC",
                Location = new Point(618, 700 - 38),
                Width = 70,
                Height = 26
            };
            btnCopyLog.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(txtLog.Text))
                    Clipboard.SetText(txtLog.Text);
            };

            // ═══ 모든 컨트롤 추가 ═══
            Controls.AddRange(new Control[]
            {
                chkSimplify, lblSimpKw, txtSimplifyKeyword, lblSimpMode, cmbSimplifyMode,
                chkMaterial, lblMatPreset, cmbMaterialPreset, lblMatKw, txtMaterialKeyword, lblMatHint,
                chkContact, lblCtKwA, txtContactKwA, lblCtKwB, txtContactKwB, lblCtTol, nudContactTol,
                chkMesh, lblMeshSize, nudMeshSize, lblMeshShape, cmbMeshShape,
                lblMeshMid, cmbMeshMidside, lblMeshGrowth, nudMeshGrowth,
                lblMeshSizeFunc, cmbMeshSizeFunc,
                chkExport, lblExpFmt, cmbExportFormat, lblExpPath, txtExportPath, btnBrowse,
                separator,
                chkContinueOnError, btnSaveCfg, btnLoadCfg, btnRun, btnClose,
                lblProgress, progressBar,
                txtLog, btnCopyLog
            });

            CancelButton = btnClose;
        }

        // ─── 기본 내보내기 경로 설정 ───

        private void SetDefaultExportPath()
        {
            try
            {
                string docPath = _part.Document.Path;
                if (!string.IsNullOrEmpty(docPath))
                {
                    string dir = Path.GetDirectoryName(docPath);
                    string name = Path.GetFileNameWithoutExtension(docPath);
                    txtExportPath.Text = Path.Combine(dir, name + ".k");
                    return;
                }
            }
            catch { }

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            txtExportPath.Text = Path.Combine(desktop, "export.k");
        }

        private void UpdateExportExtension()
        {
            try
            {
                string current = txtExportPath.Text;
                if (string.IsNullOrEmpty(current)) return;

                string dir = Path.GetDirectoryName(current);
                string name = Path.GetFileNameWithoutExtension(current);
                int idx = cmbExportFormat.SelectedIndex;
                if (idx >= 0 && idx < ExportExtensions.Length)
                    txtExportPath.Text = Path.Combine(dir, name + ExportExtensions[idx]);
            }
            catch { }
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Title = "\uBA54\uC26C \uB0B4\uBCF4\uB0B4\uAE30 \uACBD\uB85C";
                sfd.Filter =
                    "LS-DYNA (*.k)|*.k|ANSYS (*.cdb)|*.cdb|Abaqus (*.inp)|*.inp|" +
                    "Fluent (*.msh)|*.msh|CGNS (*.cgns)|*.cgns";
                sfd.FilterIndex = cmbExportFormat.SelectedIndex + 1;
                sfd.OverwritePrompt = true;

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    txtExportPath.Text = sfd.FileName;
                    cmbExportFormat.SelectedIndex = sfd.FilterIndex - 1;
                }
            }
        }

        // ─── 파이프라인 실행 ───

        private void btnRun_Click(object sender, EventArgs e)
        {
            var enabledSteps = new List<string>();
            if (chkSimplify.Checked) enabledSteps.Add("\uB2E8\uC21C\uD654");
            if (chkMaterial.Checked) enabledSteps.Add("\uC7AC\uB8CC");
            if (chkContact.Checked) enabledSteps.Add("\uC811\uCD09\uAC10\uC9C0");
            if (chkMesh.Checked) enabledSteps.Add("\uBA54\uC26C");
            if (chkExport.Checked) enabledSteps.Add("\uB0B4\uBCF4\uB0B4\uAE30");

            if (enabledSteps.Count == 0)
            {
                MessageBox.Show("\uC2E4\uD589\uD560 \uB2E8\uACC4\uB97C 1\uAC1C \uC774\uC0C1 \uC120\uD0DD\uD558\uC138\uC694.",
                    "\uD30C\uC774\uD504\uB77C\uC778", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 유효성 검사
            if (chkSimplify.Checked && string.IsNullOrEmpty(txtSimplifyKeyword.Text.Trim()))
            {
                MessageBox.Show("\uB2E8\uC21C\uD654 \uD0A4\uC6CC\uB4DC\uB97C \uC785\uB825\uD558\uC138\uC694.",
                    "\uC785\uB825 \uC624\uB958", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (chkExport.Checked && string.IsNullOrEmpty(txtExportPath.Text.Trim()))
            {
                MessageBox.Show("\uB0B4\uBCF4\uB0B4\uAE30 \uACBD\uB85C\uB97C \uC785\uB825\uD558\uC138\uC694.",
                    "\uC785\uB825 \uC624\uB958", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool continueOnError = chkContinueOnError.Checked;
            var log = new List<string>();
            int totalSteps = enabledSteps.Count;
            int currentStep = 0;
            int successSteps = 0;
            int failedSteps = 0;

            log.Add(string.Format("=== \uD30C\uC774\uD504\uB77C\uC778 \uC2DC\uC791 ({0}\uAC1C \uB2E8\uACC4) ===", totalSteps));
            log.Add(string.Format("\uC2DC\uC791: {0}", DateTime.Now.ToString("HH:mm:ss")));
            log.Add("");

            try
            {
                Cursor = Cursors.WaitCursor;
                btnRun.Enabled = false;
                progressBar.Value = 0;

                // ── Step 1: Simplify ──
                if (chkSimplify.Checked)
                {
                    currentStep++;
                    UpdateProgress(currentStep, totalSteps, "\uB2E8\uC21C\uD654 \uC2E4\uD589 \uC911...");
                    log.Add(string.Format("--- Step {0}/{1}: \uB2E8\uC21C\uD654 ---", currentStep, totalSteps));

                    try
                    {
                        string keyword = txtSimplifyKeyword.Text.Trim();
                        SimplifyMode mode = cmbSimplifyMode.SelectedIndex == 1
                            ? SimplifyMode.SolidToShell
                            : SimplifyMode.BoundingBox;

                        SimplifyResult simplifyResult = null;
                        WriteBlock.ExecuteTask("Pipeline: Simplify", () =>
                        {
                            var rules = new List<SimplifyRule>();
                            rules.Add(new SimplifyRule(keyword, mode));
                            simplifyResult = SimplifyService.ExecuteBatch(_part, rules);
                        });

                        if (simplifyResult != null)
                        {
                            log.Add(string.Format("  \uB9E4\uCE6D: {0}\uAC1C, \uCC98\uB9AC: {1}\uAC1C, \uC2E4\uD328: {2}\uAC1C",
                                simplifyResult.MatchedCount,
                                simplifyResult.ProcessedCount,
                                simplifyResult.FailedCount));
                            foreach (var entry in simplifyResult.Log)
                                log.Add("  " + entry);
                        }

                        successSteps++;
                        log.Add("  [OK]");
                    }
                    catch (Exception ex)
                    {
                        failedSteps++;
                        log.Add(string.Format("  [FAIL] {0}", ex.Message));
                        if (!continueOnError) throw;
                    }
                    log.Add("");
                }

                // ── Step 2: Material ──
                if (chkMaterial.Checked)
                {
                    currentStep++;
                    UpdateProgress(currentStep, totalSteps, "\uC7AC\uB8CC \uC801\uC6A9 \uC911...");
                    log.Add(string.Format("--- Step {0}/{1}: \uC7AC\uB8CC \uC801\uC6A9 ---", currentStep, totalSteps));

                    try
                    {
                        string preset = cmbMaterialPreset.SelectedItem.ToString();
                        string matKeyword = txtMaterialKeyword.Text.Trim();

                        double[] defaults;
                        switch (preset)
                        {
                            case "Aluminum":
                                defaults = MaterialService.GetAluminumDefaults();
                                break;
                            case "CFRP":
                                defaults = MaterialService.GetCFRPDefaults();
                                break;
                            default:
                                defaults = MaterialService.GetSteelDefaults();
                                break;
                        }

                        var matLog = new List<string>();

                        WriteBlock.ExecuteTask("Pipeline: Material", () =>
                        {
                            var allBodies = MaterialService.GetAllDesignBodies(_part);

                            List<DesignBody> targetBodies;
                            if (string.IsNullOrEmpty(matKeyword))
                            {
                                targetBodies = allBodies;
                            }
                            else
                            {
                                targetBodies = new List<DesignBody>();
                                foreach (var body in allBodies)
                                {
                                    string name = body.Name ?? "";
                                    if (name.IndexOf(matKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                                        targetBodies.Add(body);
                                }
                            }

                            string matName = string.IsNullOrEmpty(matKeyword)
                                ? preset
                                : string.Format("{0}_{1}", preset, matKeyword);

                            MaterialService.ApplyMaterial(_part, matName,
                                defaults[0], defaults[1], defaults[2], defaults[3],
                                defaults[4], defaults[5], defaults[6], defaults[7],
                                targetBodies, matLog);
                        });

                        foreach (var entry in matLog)
                            log.Add("  " + entry);

                        successSteps++;
                        log.Add("  [OK]");
                    }
                    catch (Exception ex)
                    {
                        failedSteps++;
                        log.Add(string.Format("  [FAIL] {0}", ex.Message));
                        if (!continueOnError) throw;
                    }
                    log.Add("");
                }

                // ── Step 3: Contact Detection ──
                if (chkContact.Checked)
                {
                    currentStep++;
                    UpdateProgress(currentStep, totalSteps, "\uC811\uCD09 \uAC10\uC9C0 \uC911...");
                    log.Add(string.Format("--- Step {0}/{1}: \uC811\uCD09 \uAC10\uC9C0 ---", currentStep, totalSteps));

                    try
                    {
                        string kwA = txtContactKwA.Text.Trim();
                        string kwB = txtContactKwB.Text.Trim();
                        double tolMm = (double)nudContactTol.Value;

                        int pairCount = 0;
                        int nsCreated = 0;

                        WriteBlock.ExecuteTask("Pipeline: Contact", () =>
                        {
                            var pairs = ContactDetectionService.DetectContacts(_part, kwA, kwB, tolMm);
                            pairCount = pairs.Count;

                            if (!string.IsNullOrEmpty(kwA))
                                ContactDetectionService.AssignPrefixes(pairs, kwA);

                            // 기존 NS 확인 후 신규만 생성
                            ContactDetectionService.MarkExistingPairs(_part, pairs);

                            foreach (var p in pairs)
                            {
                                if (!p.IsExisting)
                                    nsCreated++;
                            }

                            ContactDetectionService.CreateNamedSelections(_part, pairs);
                        });

                        log.Add(string.Format("  \uAC10\uC9C0: {0}\uAC1C \uD398\uC5B4, NS \uC0DD\uC131: {1}\uAC1C", pairCount, nsCreated));

                        var diagLog = ContactDetectionService.DiagnosticLog;
                        if (diagLog != null && diagLog.Count > 0)
                        {
                            log.Add(string.Format("  \uC9C4\uB2E8 \uB85C\uADF8: {0}\uD56D\uBAA9", diagLog.Count));
                        }

                        successSteps++;
                        log.Add("  [OK]");
                    }
                    catch (Exception ex)
                    {
                        failedSteps++;
                        log.Add(string.Format("  [FAIL] {0}", ex.Message));
                        if (!continueOnError) throw;
                    }
                    log.Add("");
                }

                // ── Step 4: Mesh ──
                if (chkMesh.Checked)
                {
                    currentStep++;
                    UpdateProgress(currentStep, totalSteps, "\uBA54\uC26C \uC0DD\uC131 \uC911...");
                    log.Add(string.Format("--- Step {0}/{1}: \uBA54\uC26C \uC0DD\uC131 ---", currentStep, totalSteps));

                    try
                    {
                        double elemMm = (double)nudMeshSize.Value;
                        ElementShapeType shape = ParseShape(cmbMeshShape.SelectedIndex);
                        MidsideNodesType midside = ParseMidside(cmbMeshMidside.SelectedIndex);
                        double growthRate = (double)nudMeshGrowth.Value;
                        SizeFunctionType sizeFunc = ParseSizeFunc(cmbMeshSizeFunc.SelectedIndex);

                        int bodyCount = 0;

                        WriteBlock.ExecuteTask("Pipeline: Mesh", () =>
                        {
                            var bodies = MeshSettingsService.GetAllDesignBodies(_part);
                            bodyCount = bodies.Count;
                            MeshSettingsService.GenerateMesh(bodies, elemMm, shape, midside, growthRate, sizeFunc);
                        });

                        log.Add(string.Format("  {0}\uAC1C \uBC14\uB514, Elem={1:F2}mm, {2}, {3}, Growth={4:F2}, {5}",
                            bodyCount, elemMm, cmbMeshShape.SelectedItem,
                            cmbMeshMidside.SelectedItem, growthRate,
                            cmbMeshSizeFunc.SelectedItem));

                        successSteps++;
                        log.Add("  [OK]");
                    }
                    catch (Exception ex)
                    {
                        failedSteps++;
                        log.Add(string.Format("  [FAIL] {0}", ex.Message));
                        if (!continueOnError) throw;
                    }
                    log.Add("");
                }

                // ── Step 5: Export ──
                if (chkExport.Checked)
                {
                    currentStep++;
                    UpdateProgress(currentStep, totalSteps, "\uBA54\uC26C \uB0B4\uBCF4\uB0B4\uAE30 \uC911...");
                    log.Add(string.Format("--- Step {0}/{1}: \uBA54\uC26C \uB0B4\uBCF4\uB0B4\uAE30 ---", currentStep, totalSteps));

                    try
                    {
                        string exportPath = txtExportPath.Text.Trim();
                        int formatIdx = cmbExportFormat.SelectedIndex;
                        string formatName = cmbExportFormat.SelectedItem.ToString();
                        var matLog = new List<string>();
                        int matPatched = 0;
                        int controlInserted = 0;

                        WriteBlock.ExecuteTask("Pipeline: Export", () =>
                        {
                            switch (formatIdx)
                            {
                                case 0: MeshMethods.SaveDYNA(exportPath); break;
                                case 1: MeshMethods.SaveANSYS(exportPath); break;
                                case 2: MeshMethods.SaveAbaqus(exportPath); break;
                                case 3: MeshMethods.SaveFluentMesh(exportPath); break;
                                case 4: MeshMethods.SaveCGNS(exportPath); break;
                                default: MeshMethods.SaveDYNA(exportPath); break;
                            }

                            // LS-DYNA 후처리
                            if (formatIdx == 0 && File.Exists(exportPath))
                            {
                                try
                                {
                                    matPatched = KFilePostProcessor.PatchMaterials(
                                        exportPath, _part, matLog);
                                }
                                catch (Exception patchEx)
                                {
                                    matLog.Add("[KFile] \uBB3C\uC131 \uD6C4\uCC98\uB9AC \uC624\uB958: " + patchEx.Message);
                                }

                                try
                                {
                                    controlInserted = KFilePostProcessor.AppendControlCards(
                                        exportPath, matLog);
                                }
                                catch (Exception ctrlEx)
                                {
                                    matLog.Add("[KFile] \uC81C\uC5B4\uCE74\uB4DC \uC0BD\uC785 \uC624\uB958: " + ctrlEx.Message);
                                }
                            }
                        });

                        bool fileExists = File.Exists(exportPath);
                        if (fileExists)
                        {
                            long fileSize = new FileInfo(exportPath).Length;
                            string sizeStr = fileSize < 1024 * 1024
                                ? string.Format("{0:F1} KB", fileSize / 1024.0)
                                : string.Format("{0:F1} MB", fileSize / (1024.0 * 1024.0));

                            log.Add(string.Format("  \uD3EC\uB9F7: {0}", formatName));
                            log.Add(string.Format("  \uACBD\uB85C: {0}", exportPath));
                            log.Add(string.Format("  \uD06C\uAE30: {0}", sizeStr));

                            if (matPatched > 0)
                                log.Add(string.Format("  \uBB3C\uC131 \uAD50\uCCB4: {0}\uAC1C \uC7AC\uB8CC", matPatched));
                            if (controlInserted > 0)
                                log.Add(string.Format("  \uC81C\uC5B4\uCE74\uB4DC: {0}\uAC1C \uBE14\uB85D", controlInserted));
                        }
                        else
                        {
                            log.Add("  [WARN] \uD30C\uC77C\uC774 \uC0DD\uC131\uB418\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4");
                        }

                        foreach (var entry in matLog)
                            log.Add("  " + entry);

                        successSteps++;
                        log.Add("  [OK]");
                    }
                    catch (Exception ex)
                    {
                        failedSteps++;
                        log.Add(string.Format("  [FAIL] {0}", ex.Message));
                        if (!continueOnError) throw;
                    }
                    log.Add("");
                }

                // ── 결과 요약 ──
                progressBar.Value = 100;
                log.Add("=== \uD30C\uC774\uD504\uB77C\uC778 \uC644\uB8CC ===");
                log.Add(string.Format("\uC885\uB8CC: {0}", DateTime.Now.ToString("HH:mm:ss")));
                log.Add(string.Format("\uC131\uACF5: {0}\uAC1C, \uC2E4\uD328: {1}\uAC1C / \uCD1D {2}\uAC1C",
                    successSteps, failedSteps, totalSteps));

                lblProgress.Text = failedSteps == 0
                    ? string.Format("\uC644\uB8CC ({0}/{1})", successSteps, totalSteps)
                    : string.Format("\uC644\uB8CC ({0}\uC131\uACF5, {1}\uC2E4\uD328)", successSteps, failedSteps);
            }
            catch (Exception ex)
            {
                log.Add("");
                log.Add(string.Format("=== \uD30C\uC774\uD504\uB77C\uC778 \uC911\uB2E8 ==="));
                log.Add(string.Format("\uC624\uB958: {0}", ex.Message));
                log.Add(string.Format("\uD0C0\uC785: {0}", ex.GetType().FullName));
                if (ex.InnerException != null)
                    log.Add(string.Format("Inner: {0}", ex.InnerException.Message));

                lblProgress.Text = "\uC624\uB958 \uBC1C\uC0DD";
            }
            finally
            {
                Cursor = Cursors.Default;
                btnRun.Enabled = true;
                txtLog.Text = string.Join("\r\n", log.ToArray());
                txtLog.SelectionStart = txtLog.Text.Length;
                txtLog.ScrollToCaret();
            }
        }

        private void UpdateProgress(int current, int total, string message)
        {
            int pct = (int)((current - 1) * 100.0 / total);
            progressBar.Value = Math.Min(pct, 100);
            lblProgress.Text = string.Format("[{0}/{1}] {2}", current, total, message);
            lblProgress.Refresh();
            progressBar.Refresh();
        }

        // ─── 설정 저장/불러오기 ───

        private void btnSaveCfg_Click(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Pipeline Config (*.cfg)|*.cfg|All files (*.*)|*.*";
                sfd.Title = "\uD30C\uC774\uD504\uB77C\uC778 \uC124\uC815 \uC800\uC7A5";
                sfd.FileName = "pipeline.cfg";
                if (sfd.ShowDialog() != DialogResult.OK) return;

                var lines = new List<string>();
                lines.Add("# MXDigitalTwinModeller Pipeline Config");
                lines.Add(string.Format("simplify.enabled={0}", chkSimplify.Checked));
                lines.Add(string.Format("simplify.keyword={0}", txtSimplifyKeyword.Text));
                lines.Add(string.Format("simplify.mode={0}", cmbSimplifyMode.SelectedIndex));
                lines.Add(string.Format("material.enabled={0}", chkMaterial.Checked));
                lines.Add(string.Format("material.preset={0}", cmbMaterialPreset.SelectedIndex));
                lines.Add(string.Format("material.keyword={0}", txtMaterialKeyword.Text));
                lines.Add(string.Format("contact.enabled={0}", chkContact.Checked));
                lines.Add(string.Format("contact.kwA={0}", txtContactKwA.Text));
                lines.Add(string.Format("contact.kwB={0}", txtContactKwB.Text));
                lines.Add(string.Format("contact.tolerance={0:F2}", nudContactTol.Value));
                lines.Add(string.Format("mesh.enabled={0}", chkMesh.Checked));
                lines.Add(string.Format("mesh.size={0:F2}", nudMeshSize.Value));
                lines.Add(string.Format("mesh.shape={0}", cmbMeshShape.SelectedIndex));
                lines.Add(string.Format("mesh.midside={0}", cmbMeshMidside.SelectedIndex));
                lines.Add(string.Format("mesh.growth={0:F2}", nudMeshGrowth.Value));
                lines.Add(string.Format("mesh.sizeFunc={0}", cmbMeshSizeFunc.SelectedIndex));
                lines.Add(string.Format("export.enabled={0}", chkExport.Checked));
                lines.Add(string.Format("export.format={0}", cmbExportFormat.SelectedIndex));
                lines.Add(string.Format("export.path={0}", txtExportPath.Text));
                lines.Add(string.Format("continueOnError={0}", chkContinueOnError.Checked));

                File.WriteAllLines(sfd.FileName, lines.ToArray());
                txtLog.Text = string.Format("\uC124\uC815 \uC800\uC7A5 \uC644\uB8CC: {0}", sfd.FileName);
            }
        }

        private void btnLoadCfg_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Pipeline Config (*.cfg)|*.cfg|All files (*.*)|*.*";
                ofd.Title = "\uD30C\uC774\uD504\uB77C\uC778 \uC124\uC815 \uBD88\uB7EC\uC624\uAE30";
                if (ofd.ShowDialog() != DialogResult.OK) return;

                try
                {
                    var fileLines = File.ReadAllLines(ofd.FileName);
                    var dict = new Dictionary<string, string>();

                    foreach (var line in fileLines)
                    {
                        string trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
                        int eqIdx = trimmed.IndexOf('=');
                        if (eqIdx < 0) continue;
                        string key = trimmed.Substring(0, eqIdx).Trim();
                        string val = trimmed.Substring(eqIdx + 1).Trim();
                        dict[key] = val;
                    }

                    ApplyCfgBool(dict, "simplify.enabled", chkSimplify);
                    ApplyCfgText(dict, "simplify.keyword", txtSimplifyKeyword);
                    ApplyCfgCombo(dict, "simplify.mode", cmbSimplifyMode);
                    ApplyCfgBool(dict, "material.enabled", chkMaterial);
                    ApplyCfgCombo(dict, "material.preset", cmbMaterialPreset);
                    ApplyCfgText(dict, "material.keyword", txtMaterialKeyword);
                    ApplyCfgBool(dict, "contact.enabled", chkContact);
                    ApplyCfgText(dict, "contact.kwA", txtContactKwA);
                    ApplyCfgText(dict, "contact.kwB", txtContactKwB);
                    ApplyCfgDecimal(dict, "contact.tolerance", nudContactTol);
                    ApplyCfgBool(dict, "mesh.enabled", chkMesh);
                    ApplyCfgDecimal(dict, "mesh.size", nudMeshSize);
                    ApplyCfgCombo(dict, "mesh.shape", cmbMeshShape);
                    ApplyCfgCombo(dict, "mesh.midside", cmbMeshMidside);
                    ApplyCfgDecimal(dict, "mesh.growth", nudMeshGrowth);
                    ApplyCfgCombo(dict, "mesh.sizeFunc", cmbMeshSizeFunc);
                    ApplyCfgBool(dict, "export.enabled", chkExport);
                    ApplyCfgCombo(dict, "export.format", cmbExportFormat);
                    ApplyCfgText(dict, "export.path", txtExportPath);
                    ApplyCfgBool(dict, "continueOnError", chkContinueOnError);

                    txtLog.Text = string.Format("\uC124\uC815 \uBD88\uB7EC\uC624\uAE30 \uC644\uB8CC: {0}", ofd.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        string.Format("\uC124\uC815 \uBD88\uB7EC\uC624\uAE30 \uC2E4\uD328: {0}", ex.Message),
                        "\uC624\uB958", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static void ApplyCfgBool(Dictionary<string, string> dict, string key, CheckBox chk)
        {
            string val;
            if (dict.TryGetValue(key, out val))
            {
                bool b;
                if (bool.TryParse(val, out b))
                    chk.Checked = b;
            }
        }

        private static void ApplyCfgText(Dictionary<string, string> dict, string key, TextBox txt)
        {
            string val;
            if (dict.TryGetValue(key, out val))
                txt.Text = val;
        }

        private static void ApplyCfgCombo(Dictionary<string, string> dict, string key, ComboBox cmb)
        {
            string val;
            if (dict.TryGetValue(key, out val))
            {
                int idx;
                if (int.TryParse(val, out idx) && idx >= 0 && idx < cmb.Items.Count)
                    cmb.SelectedIndex = idx;
            }
        }

        private static void ApplyCfgDecimal(Dictionary<string, string> dict, string key, NumericUpDown nud)
        {
            string val;
            if (dict.TryGetValue(key, out val))
            {
                decimal d;
                if (decimal.TryParse(val, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out d))
                {
                    if (d >= nud.Minimum && d <= nud.Maximum)
                        nud.Value = d;
                }
            }
        }

        // ─── 파싱 헬퍼 ───

        private static ElementShapeType ParseShape(int index)
        {
            switch (index)
            {
                case 1: return ElementShapeType.Hexahedral;
                case 2: return ElementShapeType.QuadDominant;
                case 3: return ElementShapeType.Triangle;
                default: return ElementShapeType.Tetrahedral;
            }
        }

        private static MidsideNodesType ParseMidside(int index)
        {
            switch (index)
            {
                case 1: return MidsideNodesType.Kept;
                case 2: return MidsideNodesType.BasedOnPhysics;
                default: return MidsideNodesType.Dropped;
            }
        }

        private static SizeFunctionType ParseSizeFunc(int index)
        {
            switch (index)
            {
                case 0: return SizeFunctionType.CurvatureAndProximity;
                case 1: return SizeFunctionType.Curvature;
                case 2: return SizeFunctionType.Proximity;
                default: return SizeFunctionType.Fixed;
            }
        }
    }
}
