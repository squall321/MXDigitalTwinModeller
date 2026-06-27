using System;
using System.Drawing;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Commands;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer;

#if V251
using SpaceClaim.Api.V251;
using SpaceClaim.Api.V251.Extensibility;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252;
using SpaceClaim.Api.V252.Extensibility;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.ReverseEngineer
{
    /// <summary>
    /// Stage 5 UI: "Ask Claude..." ribbon command. Opens a small dialog with
    /// a multi-line prompt + Submit button + result panel. On submit, extracts
    /// the FeatureGraph from the first DesignBody and runs LlmClient.RunConversation.
    ///
    /// Requires ANTHROPIC_API_KEY in the process environment.
    /// </summary>
    // @lat: [[reverse-engineer#AskClaude]]
    public class AskClaudeCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.AskClaude";

        public AskClaudeCommand()
            : base(CommandName, "Ask Claude...", IconHelper.MeshIcon,
                   "활성 Body 의 FeatureGraph 를 Claude API 에 전달하고 자연어로 수정 요청")
        {
        }

        protected override void OnUpdate(Command command)
        {
            command.IsEnabled = IsWindowActive();
        }

        protected override void OnExecute(Command command, ExecutionContext context, Rectangle buttonRect)
        {
            try
            {
                Part part = GetActivePart();
                if (part == null)
                {
                    ValidationHelper.ShowError("활성 Part 가 없습니다.", "오류");
                    return;
                }

                DesignBody body = null;
                foreach (var ib in part.Bodies)
                {
                    var db = ib as DesignBody;
                    if (db != null) { body = db; break; }
                }
                if (body == null)
                {
                    ValidationHelper.ShowError("활성 Part 에 DesignBody 가 없습니다.", "오류");
                    return;
                }

                string apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
                if (string.IsNullOrEmpty(apiKey))
                {
                    ValidationHelper.ShowError(
                        "ANTHROPIC_API_KEY 환경 변수가 설정되어 있지 않습니다.\n\n" +
                        "PowerShell:\n  $env:ANTHROPIC_API_KEY = 'sk-ant-...'\n\n" +
                        "또는 시스템 환경 변수로 설정한 뒤 SpaceClaim 을 재시작하세요.",
                        "API Key Missing");
                    return;
                }

                using (var dialog = new AskClaudeDialog(body, apiKey))
                {
                    dialog.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    string.Format("Ask Claude 중 오류:\n\n{0}", ex.Message),
                    "오류");
            }
        }
    }

    /// <summary>
    /// Inline dialog (kept in this file to avoid the Forms designer + extra
    /// csproj entries — matches the lightweight pattern used by other RE
    /// commands).
    /// </summary>
    internal class AskClaudeDialog : Form
    {
        private readonly DesignBody _body;
        private readonly string _apiKey;
        private TextBox _promptBox;
        private TextBox _resultBox;
        private Button _submitBtn;
        private Label _statusLabel;

        public AskClaudeDialog(DesignBody body, string apiKey)
        {
            _body = body;
            _apiKey = apiKey;

            Text = "Ask Claude — CAD Reverse Engineer";
            Width = 720;
            Height = 560;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(560, 400);

            var promptLabel = new Label
            {
                Text = "Prompt (자연어로 수정 요청, 예: 'H1 의 지름을 10mm 로 변경'):",
                Location = new Point(10, 10),
                Size = new Size(680, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _promptBox = new TextBox
            {
                Location = new Point(10, 35),
                Size = new Size(680, 100),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                AcceptsReturn = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _submitBtn = new Button
            {
                Text = "Submit",
                Location = new Point(595, 145),
                Size = new Size(95, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _submitBtn.Click += OnSubmit;

            _statusLabel = new Label
            {
                Text = "Ready.",
                Location = new Point(10, 150),
                Size = new Size(580, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var resultLabel = new Label
            {
                Text = "Result:",
                Location = new Point(10, 185),
                Size = new Size(680, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _resultBox = new TextBox
            {
                Location = new Point(10, 210),
                Size = new Size(680, 300),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font(FontFamily.GenericMonospace, 9f)
            };

            Controls.Add(promptLabel);
            Controls.Add(_promptBox);
            Controls.Add(_submitBtn);
            Controls.Add(_statusLabel);
            Controls.Add(resultLabel);
            Controls.Add(_resultBox);

            AcceptButton = null; // Enter in TextBox should not submit (multiline)
        }

        private void OnSubmit(object sender, EventArgs e)
        {
            string userMsg = _promptBox.Text;
            if (string.IsNullOrEmpty(userMsg) || userMsg.Trim().Length == 0)
            {
                _statusLabel.Text = "Prompt is empty.";
                return;
            }

            _submitBtn.Enabled = false;
            _statusLabel.Text = "Extracting FeatureGraph...";
            _resultBox.Text = "";
            System.Windows.Forms.Application.DoEvents();

            try
            {
                var extractor = new FeatureExtractor();
                FeatureGraph graph = extractor.Extract(_body);

                _statusLabel.Text = "Calling Claude API (this may take 10-60 s)...";
                System.Windows.Forms.Application.DoEvents();

                var client = new LlmClient(_apiKey);
                string reply = client.RunConversation(_body, graph, userMsg);

                _resultBox.Text = reply ?? "(null reply)";
                _statusLabel.Text = "Done.";
            }
            catch (Exception ex)
            {
                _resultBox.Text = "[EXCEPTION] " + ex.Message + "\n\n" + ex.StackTrace;
                _statusLabel.Text = "Error.";
                MessageBox.Show(ex.Message, "Ask Claude — Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _submitBtn.Enabled = true;
            }
        }
    }
}
