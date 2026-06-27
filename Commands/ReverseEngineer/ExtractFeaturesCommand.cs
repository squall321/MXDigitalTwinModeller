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
    /// Stage 1: 활성 Part 의 모든 Body 에 대해 FeatureGraph 추출.
    /// 결과를 JSON 으로 표시 + 저장.
    /// </summary>
    // @lat: [[reverse-engineer#ExtractFeatures]]
    public class ExtractFeaturesCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.ExtractFeatures";

        public ExtractFeaturesCommand()
            : base(CommandName, "Extract Features", IconHelper.MeshIcon,
                   "활성 모델의 모든 Body 를 분석해 FeatureGraph (JSON) 으로 출력합니다 (Stage 1: Plane/Wall/Fillet)")
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

                var dialog = new ExtractFeaturesDialog(part);
                dialog.Show();
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    string.Format("Feature 추출 중 오류:\n\n{0}", ex.Message),
                    "오류");
            }
        }
    }
}
