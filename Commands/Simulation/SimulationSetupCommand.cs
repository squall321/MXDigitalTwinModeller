using System.Drawing;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Commands;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs;

#if V251
using SpaceClaim.Api.V251.Extensibility;
#elif V252
using SpaceClaim.Api.V252.Extensibility;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Simulation
{
    public class SimulationSetupCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.SimulationSetup";

        public SimulationSetupCommand()
            : base(CommandName, "Simulation", IconHelper.SimulationIcon,
                   "\uc2dc\ubbac\ub808\uc774\uc158 \uc870\uac74 \uc124\uc815 (LS-DYNA Modal Analysis Setup)")
        {
        }

        protected override void OnUpdate(Command command)
        {
            command.IsEnabled = IsWindowActive();
        }

        protected override void OnExecute(Command command, ExecutionContext context, Rectangle buttonRect)
        {
            var dlg = new SimulationSetupDialog();
            dlg.Show();
        }
    }
}
