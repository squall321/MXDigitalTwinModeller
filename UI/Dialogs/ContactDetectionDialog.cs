using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Contact;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Contact;
using SpaceClaim.Api.V252.Scripting.Selection;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    public class ContactDetectionDialog : Form
    {
        private readonly Part _part;

        // 입력
        private TextBox txtKeywordA;
        private TextBox txtKeywordB;
        private NumericUpDown nudTolerance;
        private Label lblMode;

        // 그리드
        private DataGridView dgvPairs;

        // 선택 헬퍼 / 접두사
        private System.Windows.Forms.Panel pnlHelpers;
        private TextBox txtBatchPrefix;

        // 하단 버튼
        private System.Windows.Forms.Panel pnlBottom;
        private Button btnClose;

        // 로그
        private TextBox txtLog;
        private Button btnCopyLog;
        private Button btnToggleLog;
        private bool _logExpanded;

        // 상태
        private Label lblStatus;

        // 데이터
        private List<ContactPairInfo> _pairs = new List<ContactPairInfo>();

        // 수동 추가 상태
        private bool _isManualSelecting;
        private Button _btnManual;

        // 필터 / 3D 동기화
        private ComboBox cboTypeFilter;
        private bool _suppressSelectionChanged;

        public ContactDetectionDialog(Part part)
        {
            _part = part;
            InitializeLayout();
        }

        private void InitializeLayout()
        {
            Text = "Contact Detection Manager (접촉면 관리)";
            Width = 920;
            Height = 560;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(800, 480);

            // ═══ Row 1: 키워드 A/B + 허용거리 ═══
            var lblKwA = new Label { Text = "키워드 A:", Location = new Point(12, 15), AutoSize = true };
            txtKeywordA = new TextBox { Location = new Point(80, 12), Width = 150 };
            txtKeywordA.TextChanged += (s, e) => UpdateModeHint();

            var lblKwB = new Label { Text = "키워드 B:", Location = new Point(245, 15), AutoSize = true };
            txtKeywordB = new TextBox { Location = new Point(313, 12), Width = 150 };
            txtKeywordB.TextChanged += (s, e) => UpdateModeHint();

            var lblTol = new Label { Text = "허용거리:", Location = new Point(480, 15), AutoSize = true };
            nudTolerance = new NumericUpDown
            {
                Location = new Point(548, 12),
                Width = 70,
                Minimum = 0.01m,
                Maximum = 10.0m,
                DecimalPlaces = 2,
                Increment = 0.1m,
                Value = 1.00m
            };
            var lblMm = new Label { Text = "mm", Location = new Point(622, 15), AutoSize = true };

            // ═══ Row 2: 모드 힌트 ═══
            lblMode = new Label
            {
                Text = "모드: 전체 (모든 바디 간 접촉 감지)",
                Location = new Point(12, 38),
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Font = new Font(Font.FontFamily, 8f)
            };

            // ═══ Row 3: 감지/검증 버튼 + 상태 ═══
            var btnDetect = new Button { Text = "감지 실행", Location = new Point(12, 56), Width = 90, Height = 28 };
            btnDetect.Click += btnDetect_Click;

            var btnSelfTest = new Button { Text = "알고리즘 검증", Location = new Point(108, 56), Width = 100, Height = 28 };
            btnSelfTest.Click += btnSelfTest_Click;

            lblStatus = new Label
            {
                Text = "",
                Location = new Point(220, 62),
                AutoSize = true,
                ForeColor = SystemColors.ControlDarkDark
            };

            var lblFilter = new Label { Text = "필터:", Location = new Point(420, 62), AutoSize = true };
            cboTypeFilter = new ComboBox
            {
                Location = new Point(458, 58),
                Width = 75,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboTypeFilter.Items.AddRange(new object[] { "전체", "면", "에지", "원통", "평-원", "수동" });
            cboTypeFilter.SelectedIndex = 0;
            cboTypeFilter.SelectedIndexChanged += (s, e) => ApplyTypeFilter();

            // ═══ DataGridView ═══
            dgvPairs = new DataGridView
            {
                Location = new Point(12, 90),
                Size = new Size(880, 280),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                BackgroundColor = SystemColors.Window,
                Font = new Font("Consolas", 9f)
            };

            dgvPairs.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewCheckBoxColumn { Name = "colCheck", HeaderText = "", Width = 30 },
                new DataGridViewTextBoxColumn { Name = "colType", HeaderText = "타입", Width = 55, ReadOnly = true, SortMode = DataGridViewColumnSortMode.Automatic },
                new DataGridViewTextBoxColumn { Name = "colBodyA", HeaderText = "바디 A", Width = 140, ReadOnly = true, SortMode = DataGridViewColumnSortMode.Automatic },
                new DataGridViewTextBoxColumn { Name = "colBodyB", HeaderText = "바디 B", Width = 140, ReadOnly = true, SortMode = DataGridViewColumnSortMode.Automatic },
                new DataGridViewTextBoxColumn { Name = "colArea", HeaderText = "면적", Width = 100, ReadOnly = true, SortMode = DataGridViewColumnSortMode.Automatic },
                new DataGridViewTextBoxColumn { Name = "colPrefix", HeaderText = "접두사", Width = 110, SortMode = DataGridViewColumnSortMode.Automatic },
                new DataGridViewTextBoxColumn { Name = "colStatus", HeaderText = "상태", Width = 55, ReadOnly = true, SortMode = DataGridViewColumnSortMode.Automatic }
            });

            // CheckBox 즉시 반영
            dgvPairs.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvPairs.IsCurrentCellDirty)
                    dgvPairs.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            dgvPairs.CellValueChanged += dgvPairs_CellValueChanged;
            dgvPairs.SelectionChanged += dgvPairs_SelectionChanged;

            // ═══ 선택 헬퍼 패널 ═══
            pnlHelpers = new System.Windows.Forms.Panel
            {
                Location = new Point(12, 375),
                Size = new Size(880, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var btnSelectAll = new Button { Text = "전체선택", Location = new Point(0, 2), Width = 70, Height = 26 };
            btnSelectAll.Click += (s, e) => SetAllChecked(true);

            var btnDeselectAll = new Button { Text = "전체해제", Location = new Point(75, 2), Width = 70, Height = 26 };
            btnDeselectAll.Click += (s, e) => SetAllChecked(false);

            var btnSelectKw = new Button { Text = "키워드선택", Location = new Point(150, 2), Width = 80, Height = 26 };
            btnSelectKw.Click += btnSelectKeyword_Click;

            var sep1 = new Label { Text = "|", Location = new Point(238, 6), AutoSize = true, ForeColor = SystemColors.GrayText };

            var lblPfx = new Label { Text = "접두사:", Location = new Point(252, 6), AutoSize = true };
            txtBatchPrefix = new TextBox { Location = new Point(300, 3), Width = 100, Height = 22 };

            var btnApplyPfx = new Button { Text = "일괄적용", Location = new Point(405, 2), Width = 70, Height = 26 };
            btnApplyPfx.Click += btnApplyPrefix_Click;

            var sep2 = new Label { Text = "|", Location = new Point(483, 6), AutoSize = true, ForeColor = SystemColors.GrayText };

            _btnManual = new Button { Text = "수동추가", Location = new Point(500, 2), Width = 70, Height = 26 };
            _btnManual.Click += btnManualAdd_Click;

            var btnDelRow = new Button { Text = "행삭제", Location = new Point(575, 2), Width = 60, Height = 26 };
            btnDelRow.Click += btnDeleteRow_Click;

            pnlHelpers.Controls.AddRange(new Control[]
            {
                btnSelectAll, btnDeselectAll, btnSelectKw,
                sep1, lblPfx, txtBatchPrefix, btnApplyPfx,
                sep2, _btnManual, btnDelRow
            });

            // ═══ 하단 버튼 패널 ═══
            pnlBottom = new System.Windows.Forms.Panel
            {
                Location = new Point(12, 410),
                Size = new Size(880, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var btnCreateNS = new Button { Text = "NS 생성", Location = new Point(0, 2), Width = 80, Height = 26 };
            btnCreateNS.Click += btnCreateNS_Click;

            var btnDeleteNS = new Button { Text = "NS 삭제", Location = new Point(85, 2), Width = 80, Height = 26 };
            btnDeleteNS.Click += btnDeleteNS_Click;

            var btnRefresh = new Button { Text = "새로고침", Location = new Point(170, 2), Width = 80, Height = 26 };
            btnRefresh.Click += btnRefresh_Click;

            var sep3 = new Label { Text = "|", Location = new Point(258, 6), AutoSize = true, ForeColor = SystemColors.GrayText };

            var btnCsvSave = new Button { Text = "CSV 저장", Location = new Point(275, 2), Width = 80, Height = 26 };
            btnCsvSave.Click += btnCsvSave_Click;

            var btnCsvLoad = new Button { Text = "CSV 불러오기", Location = new Point(360, 2), Width = 95, Height = 26 };
            btnCsvLoad.Click += btnCsvLoad_Click;

            var sep4 = new Label { Text = "|", Location = new Point(463, 6), AutoSize = true, ForeColor = SystemColors.GrayText };

            btnToggleLog = new Button { Text = "진단 로그 ▼", Location = new Point(480, 2), Width = 100, Height = 26 };
            btnToggleLog.Click += (s, e) => ToggleLog();

            btnClose = new Button { Text = "닫기", Location = new Point(790, 2), Width = 80, Height = 26 };
            btnClose.Click += (s, e) => Close();

            pnlBottom.Controls.AddRange(new Control[]
            {
                btnCreateNS, btnDeleteNS, btnRefresh,
                sep3, btnCsvSave, btnCsvLoad,
                sep4, btnToggleLog, btnClose
            });

            // ═══ 로그 영역 (숨김) ═══
            txtLog = new TextBox
            {
                Location = new Point(12, 445),
                Size = new Size(830, 150),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9f),
                BackColor = SystemColors.Window,
                Visible = false,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            btnCopyLog = new Button
            {
                Text = "복사",
                Location = new Point(848, 445),
                Width = 50,
                Height = 26,
                Visible = false,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnCopyLog.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(txtLog.Text))
                {
                    Clipboard.SetText(txtLog.Text);
                    btnCopyLog.Text = "복사됨!";
                }
            };

            // ═══ 모든 컨트롤 추가 ═══
            Controls.AddRange(new Control[]
            {
                lblKwA, txtKeywordA, lblKwB, txtKeywordB,
                lblTol, nudTolerance, lblMm,
                lblMode,
                btnDetect, btnSelfTest, lblStatus, lblFilter, cboTypeFilter,
                dgvPairs,
                pnlHelpers, pnlBottom,
                txtLog, btnCopyLog
            });

            CancelButton = btnClose;
        }

        // ─── 모드 힌트 업데이트 ───

        private void UpdateModeHint()
        {
            string kwA = txtKeywordA.Text.Trim();
            string kwB = txtKeywordB.Text.Trim();

            if (string.IsNullOrEmpty(kwA) && string.IsNullOrEmpty(kwB))
                lblMode.Text = "모드: 전체 (모든 바디 간 접촉 감지)";
            else if (!string.IsNullOrEmpty(kwA) && string.IsNullOrEmpty(kwB))
                lblMode.Text = string.Format("모드: 단일 ('{0}' 바디 ↔ 나머지)", kwA);
            else if (!string.IsNullOrEmpty(kwA) && !string.IsNullOrEmpty(kwB))
                lblMode.Text = string.Format("모드: 쌍 ('{0}' ↔ '{1}')", kwA, kwB);
            else
                lblMode.Text = "모드: 키워드 A를 먼저 입력하세요";
        }

        // ─── 감지 실행 ───

        private void btnDetect_Click(object sender, EventArgs e)
        {
            string kwA = txtKeywordA.Text.Trim();
            string kwB = txtKeywordB.Text.Trim();
            double toleranceMm = (double)nudTolerance.Value;

            try
            {
                Cursor = Cursors.WaitCursor;
                lblStatus.Text = "감지 중...";
                lblStatus.Refresh();

                WriteBlock.ExecuteTask("Detect Contact", () =>
                {
                    _pairs = ContactDetectionService.DetectContacts(_part, kwA, kwB, toleranceMm);

                    if (!string.IsNullOrEmpty(kwA))
                        ContactDetectionService.AssignPrefixes(_pairs, kwA);

                    ContactDetectionService.MarkExistingPairs(_part, _pairs);
                });

                PopulateGrid();

                var diagLog = ContactDetectionService.DiagnosticLog ?? new List<string>();
                txtLog.Text = string.Join("\r\n", diagLog);

                lblStatus.Text = string.Format("{0}개 페어 감지됨", _pairs != null ? _pairs.Count : 0);
            }
            catch (Exception ex)
            {
                lblStatus.Text = "오류 발생";
                txtLog.Text = string.Format("오류:\r\n{0}\r\n{1}", ex.Message, ex.GetType().FullName);
                if (ex.InnerException != null)
                    txtLog.Text += string.Format("\r\nInner: {0}", ex.InnerException.Message);
                if (!_logExpanded) ToggleLog();
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        // ─── 그리드 채우기 ───

        private void PopulateGrid()
        {
            _suppressSelectionChanged = true;
            try
            {
                dgvPairs.Rows.Clear();
                if (_pairs == null || _pairs.Count == 0) return;

                string filterType = null;
                if (cboTypeFilter != null && cboTypeFilter.SelectedIndex > 0)
                    filterType = cboTypeFilter.SelectedItem.ToString();

                foreach (var pair in _pairs)
                {
                    if (filterType != null && GetTypeString(pair) != filterType)
                        continue;
                    AddPairToGrid(pair);
                }
            }
            finally
            {
                _suppressSelectionChanged = false;
            }
        }

        private void AddPairToGrid(ContactPairInfo pair)
        {
            int idx = dgvPairs.Rows.Add();
            var row = dgvPairs.Rows[idx];
            row.Tag = pair;

            row.Cells["colCheck"].Value = pair.IsSelected;
            row.Cells["colType"].Value = GetTypeString(pair);

            row.Cells["colBodyA"].Value = pair.BodyA != null ? (pair.BodyA.Name ?? "Unnamed") : "?";
            row.Cells["colBodyB"].Value = pair.BodyB != null ? (pair.BodyB.Name ?? "Unnamed") : "?";

            string areaStr = "-";
            if (pair.Area > 0)
            {
                if (pair.Area < 1e-6)
                    areaStr = string.Format("{0:E2} m\u00B2", pair.Area);
                else
                    areaStr = string.Format("{0:F4} mm\u00B2",
                        GeometryUtils.MetersToMm(Math.Sqrt(pair.Area)) *
                        GeometryUtils.MetersToMm(Math.Sqrt(pair.Area)));
            }
            row.Cells["colArea"].Value = areaStr;

            row.Cells["colPrefix"].Value = pair.Prefix;
            row.Cells["colStatus"].Value = pair.IsExisting ? "기존" : "신규";

            if (pair.IsExisting)
                row.DefaultCellStyle.BackColor = Color.FromArgb(240, 240, 240);
            else if (pair.IsManual)
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 255, 230);
        }

        // ─── 셀 값 변경 동기화 ───

        private void dgvPairs_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dgvPairs.Rows[e.RowIndex];
            var pair = row.Tag as ContactPairInfo;
            if (pair == null) return;

            if (e.ColumnIndex == dgvPairs.Columns["colCheck"].Index)
            {
                pair.IsSelected = Convert.ToBoolean(row.Cells["colCheck"].Value);
            }
            else if (e.ColumnIndex == dgvPairs.Columns["colPrefix"].Index)
            {
                string newPrefix = (row.Cells["colPrefix"].Value ?? "").ToString().Trim();
                if (!string.IsNullOrEmpty(newPrefix))
                    pair.Prefix = newPrefix;
            }
        }

        // ─── 선택 헬퍼 ───

        private void SetAllChecked(bool check)
        {
            foreach (DataGridViewRow row in dgvPairs.Rows)
            {
                var pair = row.Tag as ContactPairInfo;
                if (pair != null)
                {
                    pair.IsSelected = check;
                    row.Cells["colCheck"].Value = check;
                }
            }
        }

        private void btnSelectKeyword_Click(object sender, EventArgs e)
        {
            string kw = txtKeywordA.Text.Trim();
            if (string.IsNullOrEmpty(kw))
            {
                MessageBox.Show("키워드 A를 입력하세요.", "키워드선택",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (DataGridViewRow row in dgvPairs.Rows)
            {
                var pair = row.Tag as ContactPairInfo;
                if (pair == null) continue;

                string bna = pair.BodyA != null ? (pair.BodyA.Name ?? "") : "";
                string bnb = pair.BodyB != null ? (pair.BodyB.Name ?? "") : "";
                bool matches = bna.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0 ||
                               bnb.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0;

                pair.IsSelected = matches;
                row.Cells["colCheck"].Value = matches;
            }
        }

        private void btnApplyPrefix_Click(object sender, EventArgs e)
        {
            string prefix = txtBatchPrefix.Text.Trim();
            if (string.IsNullOrEmpty(prefix))
            {
                MessageBox.Show("접두사를 입력하세요.", "일괄적용",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int count = 0;
            foreach (DataGridViewRow row in dgvPairs.SelectedRows)
            {
                var pair = row.Tag as ContactPairInfo;
                if (pair != null)
                {
                    pair.Prefix = prefix;
                    row.Cells["colPrefix"].Value = prefix;
                    count++;
                }
            }

            if (count == 0)
                MessageBox.Show("그리드에서 행을 선택하세요.", "일괄적용",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                lblStatus.Text = string.Format("{0}개 행에 접두사 '{1}' 적용", count, prefix);
        }

        // ─── 수동 페어 추가 (상태 기반) ───

        private void btnManualAdd_Click(object sender, EventArgs e)
        {
            if (!_isManualSelecting)
            {
                // ── 선택 모드 진입 ──
                _isManualSelecting = true;
                _btnManual.Text = "선택완료";
                _btnManual.BackColor = Color.FromArgb(255, 220, 180);
                Opacity = 0.65;
                TopMost = false;
                lblStatus.Text = "3D 뷰에서 서로 다른 바디의 면 2개를 선택 후 [선택완료] 클릭";
                lblStatus.ForeColor = Color.OrangeRed;
            }
            else
            {
                // ── 선택 완료 ──
                _isManualSelecting = false;
                _btnManual.Text = "수동추가";
                _btnManual.BackColor = SystemColors.Control;
                Opacity = 1.0;
                lblStatus.ForeColor = SystemColors.ControlDarkDark;

                try
                {
                    var selectedFaces = new List<DesignFace>();
                    try
                    {
                        var selection = Window.ActiveWindow.ActiveContext.Selection;
                        foreach (var item in selection)
                        {
                            var df = item as DesignFace;
                            if (df != null)
                                selectedFaces.Add(df);
                        }
                    }
                    catch { }

                    if (selectedFaces.Count < 2)
                    {
                        MessageBox.Show(
                            string.Format("면 2개를 선택해야 합니다. (현재: {0}개)", selectedFaces.Count),
                            "수동 추가 실패",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        lblStatus.Text = "";
                        return;
                    }

                    var faceA = selectedFaces[0];
                    var faceB = selectedFaces[1];

                    var allBodies = ContactDetectionService.GetAllDesignBodies(_part);
                    DesignBody bodyA = FindBodyForFace(faceA, allBodies);
                    DesignBody bodyB = FindBodyForFace(faceB, allBodies);

                    if (bodyA != null && bodyB != null && bodyA == bodyB)
                    {
                        MessageBox.Show("서로 다른 바디의 면을 선택하세요.", "수동 추가 실패",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        lblStatus.Text = "";
                        return;
                    }

                    int nextIdx = 1;
                    if (_pairs != null)
                    {
                        foreach (var p in _pairs)
                        {
                            if (p.PairIndex >= nextIdx)
                                nextIdx = p.PairIndex + 1;
                        }
                    }

                    var newPair = new ContactPairInfo
                    {
                        FaceA = faceA,
                        FaceB = faceB,
                        BodyA = bodyA,
                        BodyB = bodyB,
                        Prefix = "Manual",
                        PairIndex = nextIdx,
                        Area = 0,
                        Type = ContactType.Face,
                        IsManual = true
                    };

                    if (_pairs == null) _pairs = new List<ContactPairInfo>();
                    _pairs.Add(newPair);
                    AddPairToGrid(newPair);
                    lblStatus.Text = "수동 페어 추가됨";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        string.Format("수동 추가 실패: {0}", ex.Message),
                        "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    lblStatus.Text = "";
                }
            }
        }

        private static DesignBody FindBodyForFace(DesignFace face, List<DesignBody> bodies)
        {
            foreach (var body in bodies)
            {
                foreach (var f in body.Faces)
                {
                    if (f == face)
                        return body;
                }
            }
            return null;
        }

        // ─── 행 삭제 ───

        private void btnDeleteRow_Click(object sender, EventArgs e)
        {
            if (dgvPairs.SelectedRows.Count == 0)
            {
                MessageBox.Show("삭제할 행을 선택하세요.", "행삭제",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int count = dgvPairs.SelectedRows.Count;
            var rowsToRemove = new List<DataGridViewRow>();
            foreach (DataGridViewRow row in dgvPairs.SelectedRows)
                rowsToRemove.Add(row);

            foreach (var row in rowsToRemove)
            {
                var pair = row.Tag as ContactPairInfo;
                if (pair != null && _pairs != null)
                    _pairs.Remove(pair);
                dgvPairs.Rows.Remove(row);
            }

            lblStatus.Text = string.Format("{0}개 행 삭제됨", count);
        }

        // ─── NS 생성 ───

        private void btnCreateNS_Click(object sender, EventArgs e)
        {
            if (_pairs == null || _pairs.Count == 0)
            {
                MessageBox.Show("먼저 감지를 실행하세요.", "NS 생성",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int selectedCount = 0;
            foreach (var p in _pairs)
            {
                if (p.IsSelected && !p.IsExisting)
                    selectedCount++;
            }

            if (selectedCount == 0)
            {
                MessageBox.Show("생성할 페어를 선택하세요. (신규 페어만 생성 가능)",
                    "NS 생성", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;
                lblStatus.Text = "NS 생성 중...";
                lblStatus.Refresh();

                WriteBlock.ExecuteTask("Create Named Selections", () =>
                {
                    ContactDetectionService.CreateNamedSelections(_part, _pairs);
                });

                PopulateGrid();
                lblStatus.Text = string.Format("{0}개 NS 생성 완료", selectedCount);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("NS 생성 실패: {0}", ex.Message),
                    "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        // ─── NS 삭제 ───

        private void btnDeleteNS_Click(object sender, EventArgs e)
        {
            var namesToDelete = new List<string>();
            var pairsToUpdate = new List<ContactPairInfo>();

            foreach (DataGridViewRow row in dgvPairs.Rows)
            {
                var pair = row.Tag as ContactPairInfo;
                if (pair == null || !pair.IsExisting || !pair.IsSelected) continue;

                namesToDelete.Add(pair.NameA);
                namesToDelete.Add(pair.NameB);
                pairsToUpdate.Add(pair);
            }

            if (namesToDelete.Count == 0)
            {
                MessageBox.Show("삭제할 기존 NS를 선택하세요.", "NS 삭제",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var dr = MessageBox.Show(
                string.Format("{0}개 NS를 삭제하시겠습니까?", namesToDelete.Count),
                "NS 삭제 확인",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr != DialogResult.Yes) return;

            try
            {
                Cursor = Cursors.WaitCursor;

                WriteBlock.ExecuteTask("Delete Named Selections", () =>
                {
                    ContactDetectionService.DeleteNamedSelections(_part, namesToDelete);
                });

                foreach (var pair in pairsToUpdate)
                {
                    pair.IsExisting = false;
                    pair.IsSelected = true;
                }

                PopulateGrid();
                lblStatus.Text = string.Format("{0}개 NS 삭제 완료", namesToDelete.Count);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("NS 삭제 실패: {0}", ex.Message),
                    "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        // ─── 새로고침 ───

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            if (_pairs == null || _pairs.Count == 0) return;

            try
            {
                WriteBlock.ExecuteTask("Refresh NS Status", () =>
                {
                    ContactDetectionService.MarkExistingPairs(_part, _pairs);
                });
                PopulateGrid();
                lblStatus.Text = "상태 새로고침 완료";
            }
            catch (Exception ex)
            {
                lblStatus.Text = string.Format("새로고침 실패: {0}", ex.Message);
            }
        }

        // ─── CSV 저장 ───

        private void btnCsvSave_Click(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV files (*.csv)|*.csv";
                sfd.Title = "접촉 페어 CSV 저장";
                sfd.FileName = "contact_pairs.csv";
                if (sfd.ShowDialog() != DialogResult.OK)
                    return;

                var lines = new List<string>();
                lines.Add("# MXDigitalTwinModeller Contact Pairs");
                lines.Add(string.Format("# keywordA={0}", txtKeywordA.Text.Trim()));
                lines.Add(string.Format("# keywordB={0}", txtKeywordB.Text.Trim()));
                lines.Add(string.Format("# tolerance={0:F2}", nudTolerance.Value));
                lines.Add("# type,bodyA,bodyB,area_mm2,prefix,status,selected");

                foreach (DataGridViewRow row in dgvPairs.Rows)
                {
                    var pair = row.Tag as ContactPairInfo;
                    if (pair == null) continue;

                    string bodyA = pair.BodyA != null ? (pair.BodyA.Name ?? "Unnamed") : "?";
                    string bodyB = pair.BodyB != null ? (pair.BodyB.Name ?? "Unnamed") : "?";
                    double areaMm2 = pair.Area > 0
                        ? GeometryUtils.MetersToMm(Math.Sqrt(pair.Area)) *
                          GeometryUtils.MetersToMm(Math.Sqrt(pair.Area))
                        : 0;

                    lines.Add(string.Format("{0},{1},{2},{3:F4},{4},{5},{6}",
                        pair.Type, bodyA, bodyB, areaMm2,
                        pair.Prefix,
                        pair.IsExisting ? "existing" : "new",
                        pair.IsSelected ? "true" : "false"));
                }

                File.WriteAllLines(sfd.FileName, lines.ToArray());
                lblStatus.Text = string.Format("CSV 저장 완료: {0}", Path.GetFileName(sfd.FileName));
            }
        }

        // ─── CSV 불러오기 (설정 복원) ───

        private void btnCsvLoad_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                ofd.Title = "접촉 설정 CSV 불러오기";
                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                try
                {
                    var fileLines = File.ReadAllLines(ofd.FileName);

                    foreach (var line in fileLines)
                    {
                        string trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed)) continue;

                        if (trimmed.StartsWith("# keywordA="))
                            txtKeywordA.Text = trimmed.Substring("# keywordA=".Length);
                        else if (trimmed.StartsWith("# keywordB="))
                            txtKeywordB.Text = trimmed.Substring("# keywordB=".Length);
                        else if (trimmed.StartsWith("# tolerance="))
                        {
                            double tol;
                            if (double.TryParse(trimmed.Substring("# tolerance=".Length),
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out tol))
                            {
                                if (tol >= (double)nudTolerance.Minimum && tol <= (double)nudTolerance.Maximum)
                                    nudTolerance.Value = (decimal)tol;
                            }
                        }
                    }

                    lblStatus.Text = "설정 복원 완료. '감지 실행'을 클릭하세요.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        string.Format("CSV 불러오기 실패: {0}", ex.Message),
                        "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ─── 알고리즘 검증 ───

        private void btnSelfTest_Click(object sender, EventArgs e)
        {
            try
            {
                var results = ContactDetectionService.RunSelfTest();
                txtLog.Text = string.Join("\r\n", results);
                if (!_logExpanded) ToggleLog();
                lblStatus.Text = "알고리즘 검증 완료 (로그 확인)";
            }
            catch (Exception ex)
            {
                txtLog.Text = string.Format("셀프 테스트 오류:\r\n{0}\r\n{1}",
                    ex.Message, ex.GetType().FullName);
                if (!_logExpanded) ToggleLog();
            }
        }

        // ─── 3D 면 하이라이트 ───

        private void dgvPairs_SelectionChanged(object sender, EventArgs e)
        {
            if (_suppressSelectionChanged) return;
            if (dgvPairs.SelectedRows.Count == 0) return;

            var pair = dgvPairs.SelectedRows[0].Tag as ContactPairInfo;
            if (pair == null) return;

            try
            {
                var faces = new List<DesignFace>();
                if (pair.FaceA != null) faces.Add(pair.FaceA);
                if (pair.FaceB != null) faces.Add(pair.FaceB);

                if (faces.Count > 0)
                {
                    var sel = FaceSelection.Create(faces.ToArray());
                    sel.SetActive();
                }
            }
            catch { }
        }

        // ─── 타입 필터 ───

        private void ApplyTypeFilter()
        {
            PopulateGrid();
        }

        private string GetTypeString(ContactPairInfo pair)
        {
            switch (pair.Type)
            {
                case ContactType.Face: return pair.IsManual ? "수동" : "면";
                case ContactType.Edge: return "에지";
                case ContactType.Cylinder: return "원통";
                case ContactType.PlaneCylinder: return "평-원";
                default: return "";
            }
        }

        // ─── 접이식 로그 ───

        private void ToggleLog()
        {
            _logExpanded = !_logExpanded;
            txtLog.Visible = _logExpanded;
            btnCopyLog.Visible = _logExpanded;
            Height = _logExpanded ? Height + 170 : Height - 170;
            btnToggleLog.Text = _logExpanded ? "진단 로그 \u25B2" : "진단 로그 \u25BC";
        }
    }
}
