using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Simplify;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Simplify;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    public class SimplifyDialog : Form
    {
        private readonly Part _part;
        private DataGridView dgvRules;
        private Button btnAddRow;
        private Button btnRemoveRow;
        private Button btnLoadCsv;
        private Button btnSaveCsv;
        private Button btnExecute;
        private Button btnCopy;
        private Button btnClose;
        private TextBox txtResult;

        public SimplifyDialog(Part part)
        {
            _part = part;
            InitializeLayout();
        }

        private void InitializeLayout()
        {
            Text = "Simplify (바디 단순화)";
            Width = 580;
            Height = 520;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;

            // 룰 테이블 라벨
            var lblRules = new Label
            {
                Text = "단순화 룰:",
                Location = new Point(16, 10),
                AutoSize = true
            };

            // DataGridView
            dgvRules = new DataGridView
            {
                Location = new Point(16, 30),
                Width = 430,
                Height = 150,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.FixedSingle
            };

            var colKeyword = new DataGridViewTextBoxColumn
            {
                Name = "colKeyword",
                HeaderText = "키워드",
                Width = 250
            };

            var colMode = new DataGridViewComboBoxColumn
            {
                Name = "colMode",
                HeaderText = "모드",
                Width = 150,
                FlatStyle = FlatStyle.Flat
            };
            colMode.Items.AddRange(new string[] { "BoundingBox", "SolidToShell" });

            dgvRules.Columns.Add(colKeyword);
            dgvRules.Columns.Add(colMode);

            // 기본 행 1개
            AddRuleRow("", "BoundingBox");

            // 테이블 우측 버튼
            btnAddRow = new Button
            {
                Text = "+",
                Location = new Point(456, 30),
                Width = 40,
                Height = 30
            };
            btnAddRow.Click += (s, ev) => AddRuleRow("", "BoundingBox");

            btnRemoveRow = new Button
            {
                Text = "-",
                Location = new Point(500, 30),
                Width = 40,
                Height = 30
            };
            btnRemoveRow.Click += btnRemoveRow_Click;

            btnLoadCsv = new Button
            {
                Text = "CSV 불러오기",
                Location = new Point(456, 70),
                Width = 84,
                Height = 28
            };
            btnLoadCsv.Click += btnLoadCsv_Click;

            btnSaveCsv = new Button
            {
                Text = "CSV 저장",
                Location = new Point(456, 102),
                Width = 84,
                Height = 28
            };
            btnSaveCsv.Click += btnSaveCsv_Click;

            var lblHint = new Label
            {
                Text = "바디 이름에 키워드가 포함된 바디를 각 모드로 단순화합니다. CSV: keyword,mode",
                Location = new Point(16, 185),
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Font = new Font(Font.FontFamily, 8f)
            };

            // 실행 버튼 영역
            btnExecute = new Button
            {
                Text = "전체 실행",
                Location = new Point(16, 205),
                Width = 100,
                Height = 30
            };
            btnExecute.Click += btnExecute_Click;

            btnCopy = new Button
            {
                Text = "복사",
                Location = new Point(376, 205),
                Width = 80,
                Height = 30,
                Enabled = false
            };
            btnCopy.Click += btnCopy_Click;

            btnClose = new Button
            {
                Text = "닫기",
                Location = new Point(460, 205),
                Width = 80,
                Height = 30
            };
            btnClose.Click += (s, ev) => Close();

            // 결과 영역
            var lblResult = new Label
            {
                Text = "결과:",
                Location = new Point(16, 243),
                AutoSize = true
            };

            txtResult = new TextBox
            {
                Location = new Point(16, 260),
                Width = 530,
                Height = 210,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9f),
                BackColor = SystemColors.Window,
                Text = "룰을 설정하고 \"전체 실행\" 버튼을 클릭하세요."
            };

            Controls.Add(lblRules);
            Controls.Add(dgvRules);
            Controls.Add(btnAddRow);
            Controls.Add(btnRemoveRow);
            Controls.Add(btnLoadCsv);
            Controls.Add(btnSaveCsv);
            Controls.Add(lblHint);
            Controls.Add(btnExecute);
            Controls.Add(btnCopy);
            Controls.Add(btnClose);
            Controls.Add(lblResult);
            Controls.Add(txtResult);

            CancelButton = btnClose;
        }

        // ==========================================
        //  행 추가/삭제
        // ==========================================

        private void AddRuleRow(string keyword, string mode)
        {
            int idx = dgvRules.Rows.Add();
            dgvRules.Rows[idx].Cells["colKeyword"].Value = keyword;
            dgvRules.Rows[idx].Cells["colMode"].Value = mode;
        }

        private void btnRemoveRow_Click(object sender, EventArgs e)
        {
            if (dgvRules.CurrentRow != null && dgvRules.Rows.Count > 1)
            {
                dgvRules.Rows.RemoveAt(dgvRules.CurrentRow.Index);
            }
        }

        // ==========================================
        //  CSV 불러오기 / 저장
        // ==========================================

        private void btnLoadCsv_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                ofd.Title = "Simplify 룰 CSV 불러오기";
                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                try
                {
                    var lines = File.ReadAllLines(ofd.FileName);
                    dgvRules.Rows.Clear();
                    int loaded = 0;

                    foreach (var line in lines)
                    {
                        string trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                            continue;

                        var parts = trimmed.Split(',');
                        if (parts.Length < 2) continue;

                        string keyword = parts[0].Trim();
                        string modeStr = parts[1].Trim();

                        string resolvedMode = "BoundingBox";
                        if (modeStr.Equals("SolidToShell", StringComparison.OrdinalIgnoreCase) ||
                            modeStr.Equals("Shell", StringComparison.OrdinalIgnoreCase))
                        {
                            resolvedMode = "SolidToShell";
                        }

                        if (!string.IsNullOrEmpty(keyword))
                        {
                            AddRuleRow(keyword, resolvedMode);
                            loaded++;
                        }
                    }

                    if (loaded == 0)
                        AddRuleRow("", "BoundingBox");

                    txtResult.Text = string.Format("CSV에서 {0}개 룰을 불러왔습니다: {1}", loaded, ofd.FileName);
                }
                catch (Exception ex)
                {
                    ValidationHelper.ShowError(
                        string.Format("CSV 로드 오류:\n{0}", ex.Message), "오류");
                }
            }
        }

        private void btnSaveCsv_Click(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV files (*.csv)|*.csv";
                sfd.Title = "Simplify 룰 CSV 저장";
                sfd.FileName = "simplify_rules.csv";
                if (sfd.ShowDialog() != DialogResult.OK)
                    return;

                try
                {
                    var csvLines = new List<string>();
                    csvLines.Add("# keyword,mode");

                    foreach (DataGridViewRow row in dgvRules.Rows)
                    {
                        string keyword = (row.Cells["colKeyword"].Value ?? "").ToString().Trim();
                        string mode = (row.Cells["colMode"].Value ?? "BoundingBox").ToString();
                        if (!string.IsNullOrEmpty(keyword))
                            csvLines.Add(string.Format("{0},{1}", keyword, mode));
                    }

                    File.WriteAllLines(sfd.FileName, csvLines.ToArray());
                    txtResult.Text = string.Format("CSV 저장 완료: {0}", sfd.FileName);
                }
                catch (Exception ex)
                {
                    ValidationHelper.ShowError(
                        string.Format("CSV 저장 오류:\n{0}", ex.Message), "오류");
                }
            }
        }

        // ==========================================
        //  실행
        // ==========================================

        private void btnExecute_Click(object sender, EventArgs e)
        {
            var rules = new List<SimplifyRule>();
            foreach (DataGridViewRow row in dgvRules.Rows)
            {
                string keyword = (row.Cells["colKeyword"].Value ?? "").ToString().Trim();
                string modeStr = (row.Cells["colMode"].Value ?? "BoundingBox").ToString();

                if (string.IsNullOrEmpty(keyword))
                    continue;

                SimplifyMode mode = modeStr == "SolidToShell"
                    ? SimplifyMode.SolidToShell
                    : SimplifyMode.BoundingBox;

                rules.Add(new SimplifyRule(keyword, mode));
            }

            if (rules.Count == 0)
            {
                ValidationHelper.ShowError("키워드를 1개 이상 입력해주세요.", "입력 오류");
                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;
                btnExecute.Enabled = false;
                txtResult.Text = "단순화 작업 중...";
                txtResult.Refresh();

                SimplifyResult result = null;

                WriteBlock.ExecuteTask("Simplify Bodies", () =>
                {
                    result = SimplifyService.ExecuteBatch(_part, rules);
                });

                var lines = new List<string>();
                lines.Add(string.Format("룰: {0}개", rules.Count));
                lines.Add(string.Format("매칭 바디: {0}개", result.MatchedCount));
                lines.Add(string.Format("처리 완료: {0}개", result.ProcessedCount));
                if (result.FailedCount > 0)
                    lines.Add(string.Format("실패: {0}개", result.FailedCount));
                lines.Add("");
                lines.AddRange(result.Log);

                txtResult.Text = string.Join("\r\n", lines);
                btnCopy.Enabled = true;
            }
            catch (Exception ex)
            {
                txtResult.Text = string.Format("오류 발생:\r\n\r\n{0}\r\n\r\n{1}",
                    ex.Message, ex.GetType().FullName);
                if (ex.InnerException != null)
                    txtResult.Text += string.Format("\r\n\r\nInner: {0}", ex.InnerException.Message);
                btnCopy.Enabled = true;
            }
            finally
            {
                Cursor = Cursors.Default;
                btnExecute.Enabled = true;
            }
        }

        private void btnCopy_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtResult.Text))
            {
                Clipboard.SetText(txtResult.Text);
                btnCopy.Text = "복사됨!";
            }
        }
    }
}
