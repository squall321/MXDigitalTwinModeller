using System;
using System.Drawing;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Commands;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer;

#if V251
using SpaceClaim.Api.V251.Extensibility;
#elif V252
using SpaceClaim.Api.V252.Extensibility;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.ReverseEngineer
{
    /// <summary>
    /// "Show Modification History..." ribbon command (Cycle 33).
    /// Opens a read-only dialog listing all ModificationService edits applied
    /// in the current SC session. Display-only — not a true undo.
    /// </summary>
    public class ShowModificationHistoryCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.ShowModificationHistory";

        public ShowModificationHistoryCommand()
            : base(CommandName, "Show Modification History...", IconHelper.MeshIcon,
                   "현재 세션의 ModificationService 호출 이력 표시 (디스플레이 전용)")
        {
        }

        protected override void OnUpdate(Command command)
        {
            command.IsEnabled = true;
        }

        protected override void OnExecute(Command command, ExecutionContext context, Rectangle buttonRect)
        {
            try
            {
                using (var dlg = new HistoryDialog())
                {
                    dlg.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    string.Format("History 표시 중 오류:\n\n{0}", ex.Message),
                    "오류");
            }
        }
    }

    internal class HistoryDialog : Form
    {
        public HistoryDialog()
        {
            Text = "Modification History — current session";
            Width = 720;
            Height = 480;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(540, 320);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;

            var textBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font(FontFamily.GenericMonospace, 9f),
                Location = new Point(10, 10),
                Size = new Size(680, 380),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
                         AnchorStyles.Left | AnchorStyles.Right,
                Text = ModificationHistory.FormatSummary(),
            };

            var clearBtn = new Button
            {
                Text = "Clear",
                Location = new Point(490, 405),
                Size = new Size(90, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            };
            clearBtn.Click += (s, e) =>
            {
                if (ValidationHelper.ShowConfirm(
                    "현재 세션 history 를 모두 삭제하시겠습니까?",
                    "Clear History"))
                {
                    ModificationHistory.Clear();
                    textBox.Text = ModificationHistory.FormatSummary();
                }
            };

            var closeBtn = new Button
            {
                Text = "Close",
                Location = new Point(590, 405),
                Size = new Size(90, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.OK,
            };

            Controls.Add(textBox);
            Controls.Add(clearBtn);
            Controls.Add(closeBtn);

            CancelButton = closeBtn;
            AcceptButton = closeBtn;
        }
    }
}
