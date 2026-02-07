using System;
using System.Drawing;
using System.Windows.Forms;
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

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.DMA
{
    /// <summary>
    /// DMA 3점 굽힘시편 생성 커맨드
    /// BendingSpecimenDialog를 열어 3점 굽힘으로 초기 선택
    /// </summary>
    public class CreateDMA3PointBendingCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.CreateDMA3PointBending";

        public CreateDMA3PointBendingCommand()
            : base(CommandName, "굽힘시편", IconHelper.BendingIcon, "굽힘시험 시편을 생성합니다 (3점/4점, ASTM/ISO/DMA)")
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

                // 통합 BendingSpecimenDialog (기본: 3점 굽힘)
                var dialog = new BendingSpecimenDialog(part);
                dialog.Show();
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    $"DMA 3점 굽힘시편 생성 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류"
                );

                System.Diagnostics.Debug.WriteLine($"Error: {ex}");
            }
        }
    }
}
