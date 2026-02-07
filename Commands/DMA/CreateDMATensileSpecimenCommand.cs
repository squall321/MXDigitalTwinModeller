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
    /// DMA 인장시편 생성 커맨드
    /// TensileSpecimenDialog를 열어 DMA 인장 타입을 선택하도록 함
    /// </summary>
    public class CreateDMATensileSpecimenCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.CreateDMATensileSpecimen";

        public CreateDMATensileSpecimenCommand()
            : base(CommandName, "DMA 인장시편", IconHelper.TensileIcon, "DMA 인장시험 시편을 생성합니다")
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

                // TensileSpecimenDialog를 열어 DMA 인장 타입으로 초기 선택
                var dialog = new TensileSpecimenDialog(part);
                dialog.SelectDMAType();
                dialog.Show();
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    $"DMA 시편 생성 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류"
                );

                System.Diagnostics.Debug.WriteLine($"Error: {ex}");
            }
        }
    }
}
