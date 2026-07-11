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

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Odb
{
    /// <summary>
    /// Import ODB++ 커맨드 — ECAD(ODB++) 트리/아카이브를 골라 보드·부품·솔더패드
    /// MCAD 솔리드 스택으로 자동 변환한다 (MCP import_odbpp 와 동일 서비스).
    /// </summary>
    public class ImportOdbCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.ImportOdb";

        public ImportOdbCommand()
            : base(CommandName, "Import ODB++", IconHelper.ExportStepIcon,
                   "ODB++ 폴더/.tgz 를 보드·부품·솔더패드 MCAD 솔리드로 변환합니다")
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

                var dialog = new OdbImportDialog(part);
                dialog.Show();
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    string.Format("Import ODB++ 실행 중 오류가 발생했습니다:\n\n{0}", ex.Message),
                    "오류");
                System.Diagnostics.Debug.WriteLine(string.Format("Error: {0}", ex));
            }
        }
    }
}
