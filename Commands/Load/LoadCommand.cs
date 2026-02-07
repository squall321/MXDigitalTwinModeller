using System.Drawing;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Commands;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs;

#if V251
using SpaceClaim.Api.V251.Extensibility;
#elif V252
using SpaceClaim.Api.V252.Extensibility;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Load
{
    public class LoadCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.Load";

        public LoadCommand()
            : base(CommandName, "Load", IconHelper.LoadDefIcon,
                   "하중 정의 (Named Selection에 시간이력 하중 적용)")
        {
        }

        protected override void OnUpdate(Command command)
        {
            command.IsEnabled = IsWindowActive();
        }

        protected override void OnExecute(Command command, ExecutionContext context, Rectangle buttonRect)
        {
            var dlg = new LoadDefinitionDialog();
            dlg.Show();
        }
    }
}
