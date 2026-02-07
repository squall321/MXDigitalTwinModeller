using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Commands;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;

#if V251
using SpaceClaim.Api.V251.Extensibility;
#elif V252
using SpaceClaim.Api.V252.Extensibility;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Export
{
    public class ExportStepCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.ExportStep";

        public ExportStepCommand()
            : base(CommandName, "STEP Export", IconHelper.ExportStepIcon,
                   "현재 모델을 STEP (.stp) 파일로 내보냅니다")
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

                using (var dlg = new SaveFileDialog())
                {
                    dlg.Title = "STEP 내보내기 (STEP Export)";
                    dlg.Filter = "STEP files (*.stp)|*.stp|STEP files (*.step)|*.step";
                    dlg.FilterIndex = 1;
                    dlg.DefaultExt = "stp";
                    dlg.OverwritePrompt = true;

                    if (dlg.ShowDialog() != DialogResult.OK)
                        return;

                    string path = dlg.FileName;

                    try
                    {
                        part.Export(PartExportFormat.Step, path, true, null);

                        bool fileExists = File.Exists(path);
                        long fileSize = fileExists ? new FileInfo(path).Length : 0;

                        if (fileExists && fileSize > 0)
                        {
                            string sizeStr = fileSize < 1024 * 1024
                                ? string.Format("{0:F1} KB", fileSize / 1024.0)
                                : string.Format("{0:F1} MB", fileSize / (1024.0 * 1024.0));

                            MessageBox.Show(
                                string.Format("STEP 내보내기 완료!\n\n경로: {0}\n파일 크기: {1}", path, sizeStr),
                                "STEP Export",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                        else
                        {
                            ValidationHelper.ShowError(
                                string.Format("STEP 내보내기 실패\n\n경로: {0}\n\n모델에 바디가 있는지 확인하세요.", path),
                                "STEP Export");
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        ValidationHelper.ShowError(
                            string.Format("STEP 내보내기 실패:\n\n{0}", ex.Message),
                            "STEP Export");
                    }
                }
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    string.Format("STEP 내보내기 중 오류가 발생했습니다:\n\n{0}", ex.Message),
                    "오류");
            }
        }
    }
}
