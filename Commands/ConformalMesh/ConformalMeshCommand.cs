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

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.ConformalMesh
{
    public class ConformalMeshCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.ConformalMesh";

        public ConformalMeshCommand()
            : base(CommandName, "Conformal", IconHelper.ConformalMeshIcon,
                   "STEP 어셈블리에서 공유 위상 기반 적합 메쉬를 생성합니다")
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

                var dialog = new ConformalMeshDialog(part);
                dialog.Show();
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    string.Format("Conformal Mesh 중 오류가 발생했습니다:\n\n{0}", ex.Message),
                    "오류");
            }
        }
    }
}
