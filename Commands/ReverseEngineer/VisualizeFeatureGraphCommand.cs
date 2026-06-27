using System;
using System.Drawing;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Commands;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer;

#if V251
using SpaceClaim.Api.V251;
using SpaceClaim.Api.V251.Extensibility;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252;
using SpaceClaim.Api.V252.Extensibility;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.ReverseEngineer
{
    /// <summary>
    /// Extracts the FeatureGraph for the first DesignBody in the active part
    /// and paints SC faces with color-coded roles (Hole / Boss / Fillet / Wall / Slit).
    /// </summary>
    // @lat: [[reverse-engineer#VisualizeFeatureGraph]]
    public class VisualizeFeatureGraphCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.VisualizeFeatureGraph";

        public VisualizeFeatureGraphCommand()
            : base(CommandName, "Visualize FeatureGraph", IconHelper.MeshIcon,
                   "활성 Part 의 첫 Body 를 분석해 face 별 feature 역할을 색으로 표시")
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

                // Prefer the first DesignBody under the part. (Most RE tests use a single body.)
                // Part.Bodies returns IBody — must cast to DesignBody.
                DesignBody body = null;
                foreach (var ib in part.Bodies)
                {
                    var db = ib as DesignBody;
                    if (db != null) { body = db; break; }
                }
                if (body == null)
                {
                    ValidationHelper.ShowError("활성 Part 에 DesignBody 가 없습니다.", "오류");
                    return;
                }

                var extractor = new FeatureExtractor();
                FeatureGraph graph = extractor.Extract(body);

                FeatureVisualizer.ApplyColors(body, graph);
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    string.Format("Visualization 중 오류:\n\n{0}", ex.Message),
                    "오류");
            }
        }
    }
}
