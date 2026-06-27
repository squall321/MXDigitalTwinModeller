using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer;

#if V251
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    /// <summary>
    /// RE self-test suite UI.
    /// 한 번 클릭으로 9개 test 모델 생성/분석/비교 → 결과 폴더 + 요약 표시.
    /// </summary>
    public class RunRETestSuiteDialog : Form
    {
        private readonly Part _part;
        private TextBox txtOutputDir;
        private Button btnBrowse;
        private Button btnRun;
        private Button btnClose;
        private TextBox txtSummary;
        private Label lblStatus;

        public RunRETestSuiteDialog(Part part)
        {
            _part = part;
            InitializeLayout();
        }

        private void InitializeLayout()
        {
            SuspendLayout();
            Text = "RE Self-Test Suite";
            Width = 800;
            Height = 600;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.Sizable;

            // 출력 폴더
            var lblOut = new Label { Text = "출력 폴더:", Location = new Point(12, 15), AutoSize = true };
            txtOutputDir = new TextBox
            {
                Location = new Point(100, 12),
                Width = 540,
                Text = GetDefaultOutputDir(),
            };
            btnBrowse = new Button
            {
                Text = "찾기",
                Location = new Point(650, 11),
                Width = 60,
                Height = 24,
            };
            btnBrowse.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog { SelectedPath = txtOutputDir.Text })
                {
                    if (fbd.ShowDialog() == DialogResult.OK)
                        txtOutputDir.Text = fbd.SelectedPath;
                }
            };

            // status
            lblStatus = new Label
            {
                Location = new Point(12, 48),
                Width = 760,
                Height = 24,
                Text = "Self-Test 시작 준비됨. 'Run' 클릭.",
            };

            // summary
            var lblSum = new Label { Text = "결과:", Location = new Point(12, 78), AutoSize = true };
            txtSummary = new TextBox
            {
                Location = new Point(12, 100),
                Width = 760,
                Height = 400,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new Font("Consolas", 9.0f),
            };

            // 버튼
            btnRun = new Button { Text = "Run Self-Test", Location = new Point(12, 520), Width = 140, Height = 32 };
            btnRun.Click += BtnRun_Click;

            btnClose = new Button { Text = "닫기", Location = new Point(672, 520), Width = 100, Height = 32 };
            btnClose.Click += (s, e) => Close();

            Controls.Add(lblOut);
            Controls.Add(txtOutputDir);
            Controls.Add(btnBrowse);
            Controls.Add(lblStatus);
            Controls.Add(lblSum);
            Controls.Add(txtSummary);
            Controls.Add(btnRun);
            Controls.Add(btnClose);

            ResumeLayout(false);
        }

        private string GetDefaultOutputDir()
        {
            string baseDir = @"D:\MXDigitalTwinModeller\Test\RE_SelfTest";
            string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return Path.Combine(baseDir, "run_" + ts);
        }

        private void BtnRun_Click(object sender, EventArgs e)
        {
            string outDir = txtOutputDir.Text.Trim();
            if (string.IsNullOrEmpty(outDir))
            {
                ValidationHelper.ShowError("출력 폴더 필요.", "오류");
                return;
            }

            btnRun.Enabled = false;
            lblStatus.Text = "실행 중...";
            txtSummary.Clear();
            System.Windows.Forms.Application.DoEvents();

            try
            {
                var runner = new SelfTestRunner();
                var results = runner.RunAll(_part, outDir);

                // 요약 표시
                var sb = new StringBuilder();
                int passCount = 0, failCount = 0;
                foreach (var r in results)
                {
                    if (r.OverallPass) passCount++; else failCount++;
                }
                sb.AppendFormat("총 {0}개 test | PASS: {1} | FAIL: {2}\n\n", results.Count, passCount, failCount);
                sb.AppendLine(new string('=', 80));

                foreach (var r in results)
                {
                    sb.AppendFormat("\n[{0}] {1}\n", r.OverallPass ? "✓" : "✗", r.SpecName);
                    sb.AppendLine("  " + r.Description);
                    if (r.Graph != null)
                    {
                        sb.AppendFormat("  bbox = {0:F2} × {1:F2} × {2:F2} mm | faces={3} walls={4} fillets={5} holes={6} bosses={7} slits={8}\n",
                            r.Graph.BboxSizeMm[0], r.Graph.BboxSizeMm[1], r.Graph.BboxSizeMm[2],
                            r.Graph.Faces.Count, r.Graph.Walls.Count, r.Graph.Fillets.Count,
                            r.Graph.Holes.Count, r.Graph.Bosses.Count, r.Graph.Slits.Count);
                    }
                    foreach (var c in r.Checks)
                    {
                        sb.AppendFormat("  - {0}: {1}\n", c.Key, c.Value);
                    }
                }
                sb.AppendLine();
                sb.AppendFormat("출력 폴더: {0}\n", outDir);
                sb.AppendLine("요약 파일: 00_summary.md");

                txtSummary.Text = sb.ToString();
                lblStatus.Text = string.Format("완료. PASS {0} / FAIL {1}. 폴더: {2}", passCount, failCount, outDir);
            }
            catch (Exception ex)
            {
                txtSummary.Text = "FATAL: " + ex.ToString();
                lblStatus.Text = "오류 발생.";
            }
            finally
            {
                btnRun.Enabled = true;
            }
        }
    }
}
