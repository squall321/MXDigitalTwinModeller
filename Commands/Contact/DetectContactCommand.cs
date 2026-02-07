using System;
using System.Drawing;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Commands;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs;

#if V251
using SpaceClaim.Api.V251.Extensibility;
#elif V252
using SpaceClaim.Api.V252.Extensibility;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Contact
{
    public class DetectContactCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.DetectContact";

        public DetectContactCommand()
            : base(CommandName, "Contact", IconHelper.ContactIcon,
                   "바디 간 접촉면을 자동 감지하여 Named Selection을 생성합니다")
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
                    ValidationHelper.ShowError("활성 Part가 없습니다.", "오류");
                    return;
                }

                var dialog = new ContactDetectionDialog(part);
                dialog.Show();
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    string.Format("Contact Detection 중 오류가 발생했습니다:\n\n{0}", ex.Message),
                    "오류");
            }
        }
    }
}
