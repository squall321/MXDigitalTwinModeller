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

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Laminate
{
    /// <summary>
    /// 적층 모델 생성 커맨드
    /// </summary>
    public class CreateLaminateCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.CreateLaminate";

        public CreateLaminateCommand()
            : base(CommandName, "적층 모델", IconHelper.LaminateIcon,
                   "적층 모델을 생성합니다 (Laminate Model)")
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

                var dialog = new LaminateDialog(part);
                dialog.Show();
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    $"적층 모델 생성 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류");
                System.Diagnostics.Debug.WriteLine($"Error: {ex}");
            }
        }
    }
}
