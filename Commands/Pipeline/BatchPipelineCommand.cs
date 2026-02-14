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

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Pipeline
{
    public class BatchPipelineCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.BatchPipeline";

        public BatchPipelineCommand()
            : base(CommandName, "Pipeline", IconHelper.PipelineIcon,
                   "\uC7AC\uB8CC\u2192\uC811\uCD09\uAC10\uC9C0\u2192\uBA54\uC26C\u2192\uB0B4\uBCF4\uB0B4\uAE30 \uC77C\uAD04 \uC2E4\uD589")
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
                    ValidationHelper.ShowError("\uD65C\uC131 Part\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.", "\uC624\uB958");
                    return;
                }

                var dialog = new BatchPipelineDialog(part);
                dialog.Show();
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    string.Format("Batch Pipeline \uC624\uB958:\n\n{0}", ex.Message),
                    "\uC624\uB958");
            }
        }
    }
}
