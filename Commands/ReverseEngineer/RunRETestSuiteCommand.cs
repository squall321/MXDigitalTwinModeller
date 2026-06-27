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

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.ReverseEngineer
{
    /// <summary>
    /// RE self-test suite 실행.
    /// 9개 test 모델 프로그래밍으로 생성 + extraction + 비교 → 결과 JSON / Markdown.
    /// </summary>
    public class RunRETestSuiteCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.RunRETestSuite";

        public RunRETestSuiteCommand()
            : base(CommandName, "RE Self-Test", IconHelper.MeshIcon,
                   "RE 알고리즘 자체 검증: 프로그램으로 9개 test 모델 생성 + extraction + 결과 보고")
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
                    ValidationHelper.ShowError("활성 Part 가 없습니다.", "오류");
                    return;
                }

                var dialog = new RunRETestSuiteDialog(part);
                dialog.Show();
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError("Self-test 오류:\n\n" + ex.Message, "오류");
            }
        }
    }
}
