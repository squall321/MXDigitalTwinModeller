using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Contact;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Contact;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    public class ContactDetectionDialog : Form
    {
        private readonly Part _part;
        private TextBox txtKeyword;
        private Button btnDetect;
        private Button btnClose;
        private Button btnCopy;
        private TextBox txtResult;

        public ContactDetectionDialog(Part part)
        {
            _part = part;
            InitializeLayout();
        }

        private void InitializeLayout()
        {
            Text = "Contact Detection (접촉면 감지)";
            Width = 540;
            Height = 420;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;

            // 키워드 입력 영역
            var lblKeyword = new Label
            {
                Text = "키워드 (선택):",
                Location = new Point(16, 18),
                AutoSize = true
            };

            txtKeyword = new TextBox
            {
                Location = new Point(120, 15),
                Width = 280,
                Text = ""
            };

            var lblHint = new Label
            {
                Text = "키워드 입력 시, 해당 바디의 접촉면 접두사를 키워드로 대체합니다.",
                Location = new Point(16, 42),
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Font = new Font(Font.FontFamily, 8f)
            };

            // 버튼 영역
            btnDetect = new Button
            {
                Text = "감지 실행",
                Location = new Point(16, 65),
                Width = 100,
                Height = 30
            };
            btnDetect.Click += btnDetect_Click;

            var btnSelfTest = new Button
            {
                Text = "알고리즘 검증",
                Location = new Point(126, 65),
                Width = 100,
                Height = 30
            };
            btnSelfTest.Click += btnSelfTest_Click;

            btnCopy = new Button
            {
                Text = "복사",
                Location = new Point(326, 65),
                Width = 80,
                Height = 30,
                Enabled = false
            };
            btnCopy.Click += btnCopy_Click;

            btnClose = new Button
            {
                Text = "닫기",
                Location = new Point(416, 65),
                Width = 80,
                Height = 30
            };
            btnClose.Click += (s, ev) => Close();

            // 결과 표시 영역
            var lblResult = new Label
            {
                Text = "결과:",
                Location = new Point(16, 105),
                AutoSize = true
            };

            txtResult = new TextBox
            {
                Location = new Point(16, 125),
                Width = 490,
                Height = 245,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9f),
                BackColor = SystemColors.Window,
                Text = "\"감지 실행\" 버튼을 클릭하세요."
            };

            Controls.Add(lblKeyword);
            Controls.Add(txtKeyword);
            Controls.Add(lblHint);
            Controls.Add(btnDetect);
            Controls.Add(btnSelfTest);
            Controls.Add(btnCopy);
            Controls.Add(btnClose);
            Controls.Add(lblResult);
            Controls.Add(txtResult);

            CancelButton = btnClose;
        }

        private void btnDetect_Click(object sender, EventArgs e)
        {
            string keyword = txtKeyword.Text.Trim();

            try
            {
                Cursor = Cursors.WaitCursor;
                btnDetect.Enabled = false;
                txtResult.Text = "접촉면 감지 중...";
                txtResult.Refresh();

                List<ContactPairInfo> pairs = null;

                WriteBlock.ExecuteTask("Detect Contact", () =>
                {
                    pairs = ContactDetectionService.DetectAndCreateSelections(_part, keyword);
                });

                // 진단 로그 가져오기
                var diagLog = ContactDetectionService.DiagnosticLog ?? new List<string>();

                if (pairs == null || pairs.Count == 0)
                {
                    var noResult = new List<string>();
                    noResult.Add("접촉면이 감지되지 않았습니다.\r\n");
                    noResult.Add("확인 사항:");
                    noResult.Add("- 서로 다른 바디의 평면이 맞닿아 있는지 확인");
                    noResult.Add("- 평면 거리 허용: 0.05mm 이내");
                    noResult.Add("- 평면(Plane) face만 감지 (곡면 접촉 미지원)\r\n");
                    noResult.Add("--- 진단 로그 ---");
                    noResult.AddRange(diagLog);
                    txtResult.Text = string.Join("\r\n", noResult);
                    btnCopy.Enabled = true;
                    return;
                }

                // 면접촉 / 에지접촉 분리
                var facePairs = pairs.FindAll(p => p.Type == ContactType.Face);
                var edgePairs = pairs.FindAll(p => p.Type == ContactType.Edge);

                // 결과 텍스트 생성
                var lines = new List<string>();
                lines.Add(string.Format("총 {0}개 감지 (면접촉: {1}, 에지접촉: {2})\r\n",
                    pairs.Count, facePairs.Count, edgePairs.Count));

                if (facePairs.Count > 0)
                {
                    lines.Add("── 면 접촉 (Face Contact) ── tied 적용 대상");
                    lines.Add(string.Format("{0,-22} {1,-22} {2,-18} {3,-18} {4}",
                        "A면 (+ 법선)", "B면 (- 법선)", "바디 A", "바디 B", "면적"));
                    lines.Add(new string('-', 95));

                    foreach (var pair in facePairs)
                    {
                        string bodyNameA = pair.BodyA.Name ?? "Unnamed";
                        string bodyNameB = pair.BodyB.Name ?? "Unnamed";
                        if (bodyNameA.Length > 16) bodyNameA = bodyNameA.Substring(0, 16) + "..";
                        if (bodyNameB.Length > 16) bodyNameB = bodyNameB.Substring(0, 16) + "..";

                        string areaStr = pair.Area < 1e-6
                            ? string.Format("{0:E2} m²", pair.Area)
                            : string.Format("{0:F4} mm²", GeometryUtils.MetersToMm(Math.Sqrt(pair.Area)) *
                                GeometryUtils.MetersToMm(Math.Sqrt(pair.Area)));

                        lines.Add(string.Format("{0,-22} {1,-22} {2,-18} {3,-18} {4}",
                            pair.NameA, pair.NameB, bodyNameA, bodyNameB, areaStr));
                    }
                }

                if (edgePairs.Count > 0)
                {
                    lines.Add("");
                    lines.Add("── 에지 접촉 (Edge Contact) ── tied 미적용");
                    lines.Add(string.Format("{0,-22} {1,-22} {2,-18} {3,-18}",
                        "A면", "B면", "바디 A", "바디 B"));
                    lines.Add(new string('-', 80));

                    foreach (var pair in edgePairs)
                    {
                        string bodyNameA = pair.BodyA.Name ?? "Unnamed";
                        string bodyNameB = pair.BodyB.Name ?? "Unnamed";
                        if (bodyNameA.Length > 16) bodyNameA = bodyNameA.Substring(0, 16) + "..";
                        if (bodyNameB.Length > 16) bodyNameB = bodyNameB.Substring(0, 16) + "..";

                        lines.Add(string.Format("{0,-22} {1,-22} {2,-18} {3,-18}",
                            pair.NameA, pair.NameB, bodyNameA, bodyNameB));
                    }
                }

                lines.Add("");
                lines.Add("Named Selection이 생성되었습니다.");
                if (!string.IsNullOrEmpty(keyword))
                {
                    lines.Add(string.Format("키워드 \"{0}\"가 포함된 바디의 접촉면은 \"{0}_\" 접두사를 사용합니다.", keyword));
                }

                lines.Add("\r\n--- 진단 로그 ---");
                lines.AddRange(diagLog);

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
                btnDetect.Enabled = true;
            }
        }

        private void btnSelfTest_Click(object sender, EventArgs e)
        {
            try
            {
                var results = ContactDetectionService.RunSelfTest();
                txtResult.Text = string.Join("\r\n", results);
                btnCopy.Enabled = true;
            }
            catch (Exception ex)
            {
                txtResult.Text = string.Format("셀프 테스트 오류:\r\n\r\n{0}\r\n\r\n{1}",
                    ex.Message, ex.GetType().FullName);
                btnCopy.Enabled = true;
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
