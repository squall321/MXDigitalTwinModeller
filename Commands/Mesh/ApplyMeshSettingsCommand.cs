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

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Mesh
{
    public class ApplyMeshSettingsCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.ApplyMeshSettings";

        public ApplyMeshSettingsCommand()
            : base(CommandName, "Mesh설정", IconHelper.MeshIcon,
                   "바디별 메쉬 크기를 설정하고 격자를 생성합니다")
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

                var dialog = new MeshSettingsDialog(part);
                dialog.Show();
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    $"메쉬 설정 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류");
                System.Diagnostics.Debug.WriteLine($"Error: {ex}");
            }
        }
    }
}
