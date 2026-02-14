using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ConformalMesh;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ConformalMesh;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    /// <summary>
    /// Conformal Mesh 대화상자.
    /// STEP 로드 → 계면 검출 → Share Topology → 메쉬 생성 → 내보내기.
    /// </summary>
    public class ConformalMeshDialog : Form
    {
        private Part _part;
        private List<InterfacePairInfo> _interfaces;

        // ── 컨트롤 ──
        // STEP 파일
        private TextBox txtStepPath;
        private Button btnBrowse;
        private RadioButton rdoCurrentPart, rdoOpenNew;

        // 검출 설정
        private NumericUpDown nudTolerance;
        private Button btnDetect;
        private Label lblDetectStatus;

        // 계면 그리드
        private DataGridView dgvInterfaces;

        // 메쉬 설정
        private CheckBox chkShareTopology;
        private NumericUpDown nudElementSize;
        private ComboBox cmbStrategy;
        private NumericUpDown nudGrowthRate;
        private CheckBox chkCurvProx;
        private CheckBox chkSplitCylinder;
        private NumericUpDown nudCylDivisions;

        // 액션 버튼
        private Button btnMesh, btnExport, btnClose;

        // 로그
        private TextBox txtLog;
        private Button btnToggleLog;
        private bool _logExpanded;

        public ConformalMeshDialog(Part part)
        {
            _part = part;
            _interfaces = new List<InterfacePairInfo>();
            InitializeLayout();
        }

        private void InitializeLayout()
        {
            Text = "Conformal Mesh from STEP";
            Size = new Size(780, 580);
            MinimumSize = new Size(640, 480);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            Font = new Font("Segoe UI", 9F);

            int y = 10;
            int pad = 10;
            int lw = 740;

            // ─── STEP 파일 섹션 ───
            var grpStep = new GroupBox
            {
                Text = "STEP 파일",
                Location = new Point(pad, y),
                Size = new Size(lw, 60)
            };

            rdoCurrentPart = new RadioButton { Text = "현재 파트", Location = new Point(10, 22), AutoSize = true, Checked = true };
            rdoOpenNew = new RadioButton { Text = "파일 열기", Location = new Point(110, 22), AutoSize = true };
            txtStepPath = new TextBox { Location = new Point(210, 22), Size = new Size(420, 23), Enabled = false };
            btnBrowse = new Button { Text = "...", Location = new Point(635, 21), Size = new Size(35, 25), Enabled = false };

            rdoCurrentPart.CheckedChanged += (s, e) =>
            {
                bool fileMode = !rdoCurrentPart.Checked;
                txtStepPath.Enabled = fileMode;
                btnBrowse.Enabled = fileMode;
            };

            btnBrowse.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Filter = "STEP files (*.stp;*.step)|*.stp;*.step|All files (*.*)|*.*";
                    if (dlg.ShowDialog() == DialogResult.OK)
                        txtStepPath.Text = dlg.FileName;
                }
            };

            grpStep.Controls.AddRange(new Control[] { rdoCurrentPart, rdoOpenNew, txtStepPath, btnBrowse });
            Controls.Add(grpStep);
            y += 70;

            // ─── 검출 설정 ───
            var pnlDetect = new System.Windows.Forms.Panel { Location = new Point(pad, y), Size = new Size(lw, 35) };

            pnlDetect.Controls.Add(new Label { Text = "허용거리:", Location = new Point(0, 6), AutoSize = true });
            nudTolerance = new NumericUpDown
            {
                Location = new Point(65, 3), Size = new Size(65, 23),
                DecimalPlaces = 2, Minimum = 0.01m, Maximum = 10.0m, Value = 1.0m, Increment = 0.1m
            };
            pnlDetect.Controls.Add(nudTolerance);

            pnlDetect.Controls.Add(new Label { Text = "mm", Location = new Point(133, 6), AutoSize = true });

            btnDetect = new Button { Text = "계면 검출", Location = new Point(170, 1), Size = new Size(90, 28) };
            btnDetect.Click += btnDetect_Click;
            pnlDetect.Controls.Add(btnDetect);

            lblDetectStatus = new Label { Text = "", Location = new Point(270, 6), AutoSize = true, ForeColor = Color.DarkBlue };
            pnlDetect.Controls.Add(lblDetectStatus);

            Controls.Add(pnlDetect);
            y += 40;

            // ─── 계면 그리드 ───
            dgvInterfaces = new DataGridView
            {
                Location = new Point(pad, y),
                Size = new Size(lw, 150),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                ReadOnly = false,
                BackgroundColor = SystemColors.Window
            };

            dgvInterfaces.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "colCheck", HeaderText = "", Width = 30
            });
            dgvInterfaces.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colBodyA", HeaderText = "Body A", Width = 160, ReadOnly = true
            });
            dgvInterfaces.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colBodyB", HeaderText = "Body B", Width = 160, ReadOnly = true
            });
            dgvInterfaces.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colType", HeaderText = "타입", Width = 70, ReadOnly = true
            });
            dgvInterfaces.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colArea", HeaderText = "면적 mm²", Width = 90, ReadOnly = true
            });
            dgvInterfaces.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colNS", HeaderText = "NS 이름", Width = 200, ReadOnly = true
            });

            Controls.Add(dgvInterfaces);
            y += 158;

            // ─── 메쉬 설정 ───
            var grpMesh = new GroupBox
            {
                Text = "메쉬 설정",
                Location = new Point(pad, y),
                Size = new Size(lw, 80)
            };

            chkShareTopology = new CheckBox { Text = "Share Topology", Location = new Point(10, 20), AutoSize = true, Checked = true };
            grpMesh.Controls.Add(chkShareTopology);

            grpMesh.Controls.Add(new Label { Text = "요소크기:", Location = new Point(150, 22), AutoSize = true });
            nudElementSize = new NumericUpDown
            {
                Location = new Point(215, 19), Size = new Size(60, 23),
                DecimalPlaces = 1, Minimum = 0.1m, Maximum = 100.0m, Value = 2.0m, Increment = 0.5m
            };
            grpMesh.Controls.Add(nudElementSize);
            grpMesh.Controls.Add(new Label { Text = "mm", Location = new Point(278, 22), AutoSize = true });

            grpMesh.Controls.Add(new Label { Text = "형상:", Location = new Point(310, 22), AutoSize = true });
            cmbStrategy = new ComboBox
            {
                Location = new Point(345, 19), Size = new Size(80, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbStrategy.Items.AddRange(new[] { "Tet", "Hex", "Mixed" });
            cmbStrategy.SelectedIndex = 0;
            grpMesh.Controls.Add(cmbStrategy);

            grpMesh.Controls.Add(new Label { Text = "성장률:", Location = new Point(440, 22), AutoSize = true });
            nudGrowthRate = new NumericUpDown
            {
                Location = new Point(495, 19), Size = new Size(55, 23),
                DecimalPlaces = 1, Minimum = 1.0m, Maximum = 5.0m, Value = 1.8m, Increment = 0.1m
            };
            grpMesh.Controls.Add(nudGrowthRate);

            chkCurvProx = new CheckBox { Text = "곡률+근접", Location = new Point(560, 20), AutoSize = true, Checked = true };
            grpMesh.Controls.Add(chkCurvProx);

            // 실린더 옵션 (2행)
            chkSplitCylinder = new CheckBox { Text = "실린더 엣지 분할", Location = new Point(10, 50), AutoSize = true };
            grpMesh.Controls.Add(chkSplitCylinder);

            grpMesh.Controls.Add(new Label { Text = "분할수:", Location = new Point(150, 52), AutoSize = true });
            nudCylDivisions = new NumericUpDown
            {
                Location = new Point(200, 49), Size = new Size(50, 23),
                Minimum = 3, Maximum = 64, Value = 8
            };
            grpMesh.Controls.Add(nudCylDivisions);

            Controls.Add(grpMesh);
            y += 88;

            // ─── 액션 버튼 ───
            btnMesh = new Button { Text = "메쉬 생성", Location = new Point(pad, y), Size = new Size(100, 30) };
            btnMesh.Click += btnMesh_Click;
            Controls.Add(btnMesh);

            btnExport = new Button { Text = "내보내기", Location = new Point(120, y), Size = new Size(90, 30) };
            btnExport.Click += btnExport_Click;
            Controls.Add(btnExport);

            btnToggleLog = new Button { Text = "로그 ▼", Location = new Point(550, y), Size = new Size(70, 30) };
            btnToggleLog.Click += (s, e) => ToggleLog();
            Controls.Add(btnToggleLog);

            btnClose = new Button { Text = "닫기", Location = new Point(650, y), Size = new Size(80, 30) };
            btnClose.Click += (s, e) => Close();
            Controls.Add(btnClose);
            y += 38;

            // ─── 로그 (기본 숨김) ───
            txtLog = new TextBox
            {
                Location = new Point(pad, y),
                Size = new Size(lw, 120),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(250, 250, 250),
                Font = new Font("Consolas", 8.5F),
                Visible = false
            };
            Controls.Add(txtLog);
            _logExpanded = false;
        }

        private void ToggleLog()
        {
            _logExpanded = !_logExpanded;
            txtLog.Visible = _logExpanded;
            Height = _logExpanded ? 730 : 580;
            btnToggleLog.Text = _logExpanded ? "로그 ▲" : "로그 ▼";
        }

        private void UpdateLog()
        {
            var log = ConformalMeshService.DiagnosticLog;
            if (log != null && log.Count > 0)
            {
                txtLog.Text = string.Join("\r\n", log);
                txtLog.SelectionStart = txtLog.TextLength;
                txtLog.ScrollToCaret();
            }
        }

        // ─── 이벤트 핸들러 ───

        private void btnDetect_Click(object sender, EventArgs e)
        {
            btnDetect.Enabled = false;
            lblDetectStatus.Text = "검출 중...";
            lblDetectStatus.ForeColor = Color.DarkOrange;
            System.Windows.Forms.Application.DoEvents();

            try
            {
                // STEP 임포트 (파일 열기 모드)
                if (rdoOpenNew.Checked)
                {
                    string stepPath = txtStepPath.Text;
                    if (string.IsNullOrEmpty(stepPath) || !System.IO.File.Exists(stepPath))
                    {
                        MessageBox.Show("STEP 파일 경로를 확인하세요.", "오류",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        btnDetect.Enabled = true;
                        return;
                    }

                    WriteBlock.ExecuteTask("Conformal Mesh - STEP 임포트", () =>
                    {
                        var imported = ConformalMeshService.ImportStep(
                            stepPath, StepImportMode.OpenNew);
                        if (imported != null)
                            _part = imported;
                    });

                    if (_part == null)
                    {
                        lblDetectStatus.Text = "STEP 임포트 실패";
                        lblDetectStatus.ForeColor = Color.Red;
                        btnDetect.Enabled = true;
                        return;
                    }
                }

                double tolMm = (double)nudTolerance.Value;

                WriteBlock.ExecuteTask("Conformal Mesh - 계면 검출", () =>
                {
                    var bodies = ConformalMeshService.CollectBodies(_part, "");
                    _interfaces = ConformalMeshService.DetectInterfaces(bodies, tolMm, true, true);
                });

                // 그리드 업데이트
                dgvInterfaces.Rows.Clear();
                foreach (var iface in _interfaces)
                {
                    int idx = dgvInterfaces.Rows.Add();
                    var row = dgvInterfaces.Rows[idx];
                    row.Cells["colCheck"].Value = true;
                    row.Cells["colBodyA"].Value = iface.BodyA != null ? (iface.BodyA.Name ?? "?") : "?";
                    row.Cells["colBodyB"].Value = iface.BodyB != null ? (iface.BodyB.Name ?? "?") : "?";
                    row.Cells["colType"].Value = iface.Type.ToString();
                    row.Cells["colArea"].Value = string.Format("{0:F2}", iface.TotalAreaMm2);
                    row.Cells["colNS"].Value = iface.GroupName ?? "";
                    row.Tag = iface;
                }

                lblDetectStatus.Text = string.Format("{0}개 계면 검출됨", _interfaces.Count);
                lblDetectStatus.ForeColor = Color.DarkGreen;
            }
            catch (Exception ex)
            {
                lblDetectStatus.Text = "검출 오류";
                lblDetectStatus.ForeColor = Color.Red;
                MessageBox.Show(ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnDetect.Enabled = true;
                UpdateLog();
            }
        }

        private void btnMesh_Click(object sender, EventArgs e)
        {
            btnMesh.Enabled = false;
            System.Windows.Forms.Application.DoEvents();

            try
            {
                var p = BuildParameters();

                string err;
                if (!p.Validate(out err))
                {
                    MessageBox.Show(err, "유효성 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                WriteBlock.ExecuteTask("Conformal Mesh - 메쉬 생성", () =>
                {
                    var bodies = ConformalMeshService.CollectBodies(_part, "");

                    // NS 생성
                    if (p.CreateInterfaceNamedSelections && _interfaces.Count > 0)
                    {
                        // 그리드 체크 상태 반영
                        SyncGridSelection();
                        ConformalMeshService.CreateInterfaceNS(_part, _interfaces);
                    }

                    // Share Topology
                    if (p.EnableShareTopology)
                        ConformalMeshService.EnableShareTopology(_part);

                    // 실린더 엣지 분할
                    if (p.SplitCylinderEdges)
                        ConformalMeshService.SplitCylinderEdges(bodies, p.CylinderEdgeDivisions);

                    // 메쉬 생성
                    ConformalMeshService.GenerateConformalMesh(_part, bodies, p);
                });

                MessageBox.Show("메쉬 생성 완료", "Conformal Mesh",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "메쉬 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnMesh.Enabled = true;
                UpdateLog();
            }
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "LS-DYNA (*.k)|*.k|ANSYS (*.cdb)|*.cdb|All (*.*)|*.*";
                dlg.DefaultExt = "k";
                if (dlg.ShowDialog() != DialogResult.OK) return;

                try
                {
                    string format = dlg.FilterIndex == 2 ? "ANSYS" : "LS-DYNA";
                    WriteBlock.ExecuteTask("Conformal Mesh - 내보내기", () =>
                    {
                        ConformalMeshService.ExportMesh(dlg.FileName, format);
                    });

                    MessageBox.Show(string.Format("내보내기 완료: {0}", dlg.FileName),
                        "Conformal Mesh", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "내보내기 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    UpdateLog();
                }
            }
        }

        private ConformalMeshParameters BuildParameters()
        {
            var p = new ConformalMeshParameters();
            p.ImportMode = rdoCurrentPart.Checked ? StepImportMode.UseCurrentPart : StepImportMode.OpenNew;
            p.StepFilePath = txtStepPath.Text;
            p.ToleranceMm = (double)nudTolerance.Value;
            p.EnableShareTopology = chkShareTopology.Checked;
            p.CreateInterfaceNamedSelections = true;
            p.ElementSizeMm = (double)nudElementSize.Value;
            p.GrowthRate = (double)nudGrowthRate.Value;
            p.UseCurvatureProximity = chkCurvProx.Checked;
            p.SplitCylinderEdges = chkSplitCylinder.Checked;
            p.CylinderEdgeDivisions = (int)nudCylDivisions.Value;

            switch (cmbStrategy.SelectedIndex)
            {
                case 0: p.Strategy = MeshStrategy.AutoTet; break;
                case 1: p.Strategy = MeshStrategy.AutoHex; break;
                case 2: p.Strategy = MeshStrategy.Mixed; break;
            }

            return p;
        }

        private void SyncGridSelection()
        {
            for (int i = 0; i < dgvInterfaces.Rows.Count; i++)
            {
                var row = dgvInterfaces.Rows[i];
                var iface = row.Tag as InterfacePairInfo;
                if (iface != null)
                {
                    var val = row.Cells["colCheck"].Value;
                    iface.IsSelected = val != null && (bool)val;
                }
            }
        }
    }
}
