using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.GmshMesher;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.GmshMesher;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    public class GmshMesherDialog : Form
    {
        private readonly Part _part;
        private List<DesignBody> _bodies;

        // Body list
        private DataGridView _dgvBodies;

        // Global settings
        private NumericUpDown _nudElementSize;
        private NumericUpDown _nudGrowthRate;
        private ComboBox _cmbShape;
        private CheckBox _chkMidside;
        private ComboBox _cmbAlgorithm;

        // Refinement (future)
        private GroupBox _grpRefinement;

        // Buttons
        private Button _btnMesh;
        private Button _btnPreview;
        private Button _btnExportK;
        private Button _btnClose;

        // Progress & Log
        private ProgressBar _progressBar;
        private TextBox _txtLog;
        private bool _logExpanded;

        public GmshMesherDialog(Part part)
        {
            _part = part;
            InitializeLayout();
            LoadBodies();
        }

        private void InitializeLayout()
        {
            Text = "Gmsh Mesher — MX Digital Twin";
            Size = new Size(780, 640);
            MinimumSize = new Size(600, 500);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            Font = new Font("Segoe UI", 9F);

            int y = 10;
            int leftMargin = 12;
            int rightBound = 740;

            // ── Body List GroupBox ──
            var grpBodies = new GroupBox
            {
                Text = "바디 목록",
                Location = new Point(leftMargin, y),
                Size = new Size(rightBound, 180)
            };
            Controls.Add(grpBodies);

            _dgvBodies = new DataGridView
            {
                Location = new Point(10, 20),
                Size = new Size(grpBodies.Width - 20, 150),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            _dgvBodies.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "Check", HeaderText = "선택", Width = 50, FillWeight = 10
            });
            _dgvBodies.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "BodyName", HeaderText = "바디 이름", ReadOnly = true, FillWeight = 50
            });
            _dgvBodies.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "OverrideSize", HeaderText = "크기 오버라이드 (mm)", FillWeight = 40
            });
            grpBodies.Controls.Add(_dgvBodies);

            y += 190;

            // ── Global Settings GroupBox ──
            var grpSettings = new GroupBox
            {
                Text = "전역 메쉬 설정",
                Location = new Point(leftMargin, y),
                Size = new Size(rightBound, 120)
            };
            Controls.Add(grpSettings);

            int sy = 22;
            int labelW = 130;
            int ctrlW = 120;
            int col1 = 10;
            int col2 = col1 + labelW + ctrlW + 20;

            // Element Size
            grpSettings.Controls.Add(new Label { Text = "Element Size (mm):", Location = new Point(col1, sy + 3), AutoSize = true });
            _nudElementSize = new NumericUpDown
            {
                Location = new Point(col1 + labelW, sy),
                Size = new Size(ctrlW, 23),
                DecimalPlaces = 2,
                Minimum = 0.01m,
                Maximum = 1000,
                Value = 5.0m,
                Increment = 0.5m
            };
            grpSettings.Controls.Add(_nudElementSize);

            // Growth Rate
            grpSettings.Controls.Add(new Label { Text = "Growth Rate:", Location = new Point(col2, sy + 3), AutoSize = true });
            _nudGrowthRate = new NumericUpDown
            {
                Location = new Point(col2 + labelW, sy),
                Size = new Size(ctrlW, 23),
                DecimalPlaces = 2,
                Minimum = 1.0m,
                Maximum = 3.0m,
                Value = 1.3m,
                Increment = 0.1m
            };
            grpSettings.Controls.Add(_nudGrowthRate);

            sy += 32;

            // Shape
            grpSettings.Controls.Add(new Label { Text = "Element Shape:", Location = new Point(col1, sy + 3), AutoSize = true });
            _cmbShape = new ComboBox
            {
                Location = new Point(col1 + labelW, sy),
                Size = new Size(ctrlW, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbShape.Items.AddRange(new object[] { "Tet", "Hex" });
            _cmbShape.SelectedIndex = 0;
            grpSettings.Controls.Add(_cmbShape);

            // Algorithm
            grpSettings.Controls.Add(new Label { Text = "Algorithm:", Location = new Point(col2, sy + 3), AutoSize = true });
            _cmbAlgorithm = new ComboBox
            {
                Location = new Point(col2 + labelW, sy),
                Size = new Size(ctrlW, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbAlgorithm.Items.AddRange(new object[] { "Auto", "Delaunay", "Frontal", "HXT" });
            _cmbAlgorithm.SelectedIndex = 0;
            grpSettings.Controls.Add(_cmbAlgorithm);

            sy += 32;

            // Midside nodes
            _chkMidside = new CheckBox
            {
                Text = "Midside Nodes (2차 요소)",
                Location = new Point(col1, sy),
                AutoSize = true
            };
            grpSettings.Controls.Add(_chkMidside);

            y += 130;

            // ── Refinement GroupBox (future, disabled) ──
            _grpRefinement = new GroupBox
            {
                Text = "Refinement Box (향후 지원)",
                Location = new Point(leftMargin, y),
                Size = new Size(rightBound, 40),
                Enabled = false
            };
            _grpRefinement.Controls.Add(new Label
            {
                Text = "향후 공간별 메쉬 밀도 조정 기능이 추가될 예정입니다.",
                Location = new Point(10, 16),
                AutoSize = true,
                ForeColor = Color.Gray
            });
            Controls.Add(_grpRefinement);

            y += 50;

            // ── Buttons ──
            int btnW = 110;
            int btnH = 32;
            int btnY = y;
            int btnGap = 10;
            int btnX = leftMargin;

            _btnMesh = new Button
            {
                Text = "Mesh",
                Location = new Point(btnX, btnY),
                Size = new Size(btnW, btnH)
            };
            _btnMesh.Click += btnMesh_Click;
            Controls.Add(_btnMesh);
            btnX += btnW + btnGap;

            _btnPreview = new Button
            {
                Text = "Preview",
                Location = new Point(btnX, btnY),
                Size = new Size(btnW, btnH),
                Enabled = false
            };
            _btnPreview.Click += btnPreview_Click;
            Controls.Add(_btnPreview);
            btnX += btnW + btnGap;

            _btnExportK = new Button
            {
                Text = "Export K-file",
                Location = new Point(btnX, btnY),
                Size = new Size(btnW, btnH),
                Enabled = false
            };
            _btnExportK.Click += btnExportK_Click;
            Controls.Add(_btnExportK);
            btnX += btnW + btnGap;

            // Spacer
            btnX = rightBound + leftMargin - btnW;
            _btnClose = new Button
            {
                Text = "Close",
                Location = new Point(btnX, btnY),
                Size = new Size(btnW, btnH)
            };
            _btnClose.Click += (s, e) => Close();
            Controls.Add(_btnClose);

            y += btnH + 10;

            // ── Progress Bar ──
            _progressBar = new ProgressBar
            {
                Location = new Point(leftMargin, y),
                Size = new Size(rightBound, 18),
                Style = ProgressBarStyle.Marquee,
                Visible = false
            };
            Controls.Add(_progressBar);

            y += 24;

            // ── Log ──
            var btnToggleLog = new Button
            {
                Text = "Log ▼",
                Location = new Point(leftMargin, y),
                Size = new Size(80, 24),
                FlatStyle = FlatStyle.Flat
            };
            btnToggleLog.Click += (s, e) => ToggleLog(btnToggleLog);
            Controls.Add(btnToggleLog);

            y += 28;

            _txtLog = new TextBox
            {
                Location = new Point(leftMargin, y),
                Size = new Size(rightBound, 120),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 8.5F),
                Visible = false
            };
            Controls.Add(_txtLog);
        }

        private void LoadBodies()
        {
            _bodies = new List<DesignBody>();
            try
            {
                CollectBodiesRecursive(_part, _bodies);
            }
            catch { }

            _dgvBodies.Rows.Clear();
            foreach (var body in _bodies)
            {
                int idx = _dgvBodies.Rows.Add();
                _dgvBodies.Rows[idx].Cells["Check"].Value = true;
                _dgvBodies.Rows[idx].Cells["BodyName"].Value = body.Name ?? ("Body_" + idx);
                _dgvBodies.Rows[idx].Cells["OverrideSize"].Value = "";
            }
        }

        private void CollectBodiesRecursive(Part part, List<DesignBody> bodies)
        {
            foreach (var body in part.Bodies)
                bodies.Add(body);
            foreach (var comp in part.Components)
            {
                Part child = comp.Content as Part;
                if (child != null)
                    CollectBodiesRecursive(child, bodies);
            }
        }

        private GmshMeshParameters BuildParameters()
        {
            var p = new GmshMeshParameters
            {
                GlobalElementSizeMm = (double)_nudElementSize.Value,
                GrowthRate = (double)_nudGrowthRate.Value,
                Shape = _cmbShape.SelectedIndex == 1 ? ElementShape.Hex : ElementShape.Tet,
                MidsideNodes = _chkMidside.Checked,
                Algorithm = GetSelectedAlgorithm()
            };

            // Body overrides
            for (int i = 0; i < _dgvBodies.Rows.Count && i < _bodies.Count; i++)
            {
                var row = _dgvBodies.Rows[i];
                bool selected = row.Cells["Check"].Value is bool && (bool)row.Cells["Check"].Value;
                if (!selected) continue;

                string sizeStr = row.Cells["OverrideSize"].Value as string;
                double ovrSize;
                if (!string.IsNullOrWhiteSpace(sizeStr) && double.TryParse(sizeStr, out ovrSize) && ovrSize > 0)
                {
                    p.BodyOverrides.Add(new BodyMeshOverride(
                        _bodies[i].Name ?? ("Body_" + i), i, ovrSize));
                }
            }

            return p;
        }

        private MeshAlgorithm GetSelectedAlgorithm()
        {
            switch (_cmbAlgorithm.SelectedIndex)
            {
                case 1: return MeshAlgorithm.Delaunay;
                case 2: return MeshAlgorithm.Frontal;
                case 3: return MeshAlgorithm.HXT;
                default: return MeshAlgorithm.Auto;
            }
        }

        private List<IDesignBody> GetSelectedBodies()
        {
            var selected = new List<IDesignBody>();
            for (int i = 0; i < _dgvBodies.Rows.Count && i < _bodies.Count; i++)
            {
                var row = _dgvBodies.Rows[i];
                bool check = row.Cells["Check"].Value is bool && (bool)row.Cells["Check"].Value;
                if (check) selected.Add(_bodies[i]);
            }
            return selected;
        }

        // ── Mesh Button ──
        private void btnMesh_Click(object sender, EventArgs e)
        {
            var meshParams = BuildParameters();
            string errorMsg;
            if (!meshParams.Validate(out errorMsg))
            {
                MessageBox.Show(errorMsg, "검증 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var bodies = GetSelectedBodies();
            if (bodies.Count == 0)
            {
                MessageBox.Show("메쉬할 바디를 선택하세요.", "경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetBusy(true);
            _txtLog.Clear();

            try
            {
                GmshMeshData data = null;
                WriteBlock.ExecuteTask("Gmsh Mesh", () =>
                {
                    data = GmshMeshService.RunMeshPipeline(_part, bodies, meshParams);
                });

                UpdateLog(GmshMeshService.DiagnosticLog);

                if (data != null)
                {
                    _btnPreview.Enabled = true;
                    _btnExportK.Enabled = true;
                    MessageBox.Show(
                        string.Format("메쉬 생성 완료!\n노드: {0}\n볼륨 요소: {1}\n표면 요소: {2}",
                            data.TotalNodeCount, data.VolumeElements.Count, data.SurfaceElements.Count),
                        "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("메쉬 생성 실패. 로그를 확인하세요.", "실패",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                UpdateLog(GmshMeshService.DiagnosticLog);
                MessageBox.Show("오류: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ── Preview Button ──
        private void btnPreview_Click(object sender, EventArgs e)
        {
            var meshData = GmshMeshService.LastMeshData;
            if (meshData == null)
            {
                MessageBox.Show("먼저 메쉬를 생성하세요.", "경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetBusy(true);
            try
            {
                var log = new List<string>();
                WriteBlock.ExecuteTask("Gmsh Preview", () =>
                {
                    MeshVisualizationService.CreatePreviewMesh(_part, meshData, log);
                    Window.ActiveWindow?.ZoomExtents();
                });
                UpdateLog(log);
            }
            catch (Exception ex)
            {
                MessageBox.Show("프리뷰 오류: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ── Export K-file Button ──
        private void btnExportK_Click(object sender, EventArgs e)
        {
            var meshData = GmshMeshService.LastMeshData;
            if (meshData == null)
            {
                MessageBox.Show("먼저 메쉬를 생성하세요.", "경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "LS-DYNA K-file (*.k)|*.k|All Files (*.*)|*.*";
                sfd.DefaultExt = "k";
                sfd.FileName = "gmsh_mesh.k";
                if (sfd.ShowDialog() != DialogResult.OK) return;

                try
                {
                    var log = new List<string>();
                    GmshKFileWriter.WriteKFile(sfd.FileName, meshData, _part, log);
                    UpdateLog(log);
                    MessageBox.Show("K-file 내보내기 완료:\n" + sfd.FileName, "완료",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("K-file 내보내기 오류: " + ex.Message, "오류",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ── Helpers ──
        private void SetBusy(bool busy)
        {
            _progressBar.Visible = busy;
            _btnMesh.Enabled = !busy;
            _btnPreview.Enabled = !busy && GmshMeshService.LastMeshData != null;
            _btnExportK.Enabled = !busy && GmshMeshService.LastMeshData != null;
        }

        private void ToggleLog(Button btn)
        {
            _logExpanded = !_logExpanded;
            _txtLog.Visible = _logExpanded;
            btn.Text = _logExpanded ? "Log ▲" : "Log ▼";

            if (_logExpanded)
                Height = Math.Max(Height, _txtLog.Bottom + 50);
        }

        private void UpdateLog(List<string> logLines)
        {
            if (logLines == null || logLines.Count == 0) return;
            string text = string.Join(Environment.NewLine, logLines);
            if (_txtLog.TextLength > 0)
                _txtLog.AppendText(Environment.NewLine + "────────────────" + Environment.NewLine);
            _txtLog.AppendText(text);
            _txtLog.SelectionStart = _txtLog.TextLength;
            _txtLog.ScrollToCaret();
        }
    }
}
