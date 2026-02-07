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

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.TensileTest
{
    /// <summary>
    /// ASTM 인장시험 시편 생성 커맨드
    /// </summary>
    public class CreateASTMTensileSpecimenCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.CreateASTMTensileSpecimen";

        public CreateASTMTensileSpecimenCommand()
            : base(CommandName, "인장시편", IconHelper.TensileIcon, "인장시험 시편을 생성합니다 (ASTM/ISO/DMA)")
        {
        }

        protected override void OnUpdate(Command command)
        {
            // 활성 윈도우가 있을 때만 활성화
            command.IsEnabled = IsWindowActive();
        }

        protected override void OnExecute(Command command, ExecutionContext context, Rectangle buttonRect)
        {
            try
            {
                // 현재 Part 가져오기
                Part part = GetActivePart();

                if (part == null)
                {
                    ValidationHelper.ShowError("활성 Part가 없습니다.", "오류");
                    return;
                }

                // Modeless 대화창 생성 (using 없이 Show() 사용)
                var dialog = new TensileSpecimenDialog(part);
                dialog.Show();
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    $"ASTM 인장시편 생성 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류"
                );

                System.Diagnostics.Debug.WriteLine($"Error: {ex}");
            }
        }
    }
}
