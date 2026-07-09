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

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Package
{
    /// <summary>
    /// Import Package 커맨드 — 패키지 파일(*Layer 스택 + Cylinder 볼맵 + Box 다이)을
    /// 골라 CAD 스택(레이어/솔더볼/수지 매트릭스)을 생성한다.
    /// </summary>
    public class ImportPackageCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.ImportPackage";

        public ImportPackageCommand()
            : base(CommandName, "Import Package", IconHelper.LaminateIcon,
                   "패키지 파일(볼맵/층두께)로 솔더볼·수지·레이어 CAD 스택을 생성합니다")
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

                var dialog = new PackageImportDialog(part);
                dialog.Show();
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    string.Format("Import Package 실행 중 오류가 발생했습니다:\n\n{0}", ex.Message),
                    "오류");
                System.Diagnostics.Debug.WriteLine(string.Format("Error: {0}", ex));
            }
        }
    }
}
