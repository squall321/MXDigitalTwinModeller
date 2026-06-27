using System;
using System.Drawing;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Commands;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs;

#if V251
using SpaceClaim.Api.V251.Extensibility;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Extensibility;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.VoidCut
{
    /// <summary>
    /// Void Cut 커맨드 - 커터 볼륨으로 다중 바디 Boolean Subtract
    /// </summary>
    // @lat: [[mesh#Void Cut]]
    public class VoidCutCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.VoidCut";

        public VoidCutCommand()
            : base(CommandName, "Void Cut", IconHelper.VoidCutIcon,
                   "커터 볼륨으로 교차하는 모든 바디를 Boolean Subtract합니다")
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

                var dialog = new VoidCutDialog(part);
                dialog.Show();
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    string.Format("Void Cut 실행 중 오류가 발생했습니다:\n\n{0}", ex.Message),
                    "오류");
                System.Diagnostics.Debug.WriteLine(string.Format("Error: {0}", ex));
            }
        }
    }
}
