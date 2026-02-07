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

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Simplify
{
    public class SimplifyCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.Simplify";

        public SimplifyCommand()
            : base(CommandName, "Simplify", IconHelper.MeshIcon,
                   "키워드 매칭 바디를 BoundingBox 또는 Shell로 단순화합니다")
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

                var dialog = new SimplifyDialog(part);
                dialog.Show();
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    string.Format("Simplify 중 오류가 발생했습니다:\n\n{0}", ex.Message),
                    "오류");
            }
        }
    }
}
