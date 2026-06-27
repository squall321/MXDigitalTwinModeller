using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Commands;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer;

#if V251
using SpaceClaim.Api.V251.Extensibility;
#elif V252
using SpaceClaim.Api.V252.Extensibility;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.ReverseEngineer
{
    /// <summary>
    /// Ribbon command: pick a .stp/.step/.scdocx file, run the end-to-end
    /// RealModelPipeline (import → extract → visualize → report), and report
    /// where the JSON + Markdown ended up on disk.
    /// </summary>
    // @lat: [[reverse-engineer#AnalyzeRealModel]]
    public class AnalyzeRealModelCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.AnalyzeRealModel";

        public AnalyzeRealModelCommand()
            : base(CommandName, "Analyze Real Model...", IconHelper.MeshIcon,
                   "실제 CAD 파일 (.stp/.step/.scdocx) 을 import → FeatureGraph 추출 → 색상 표시 → 보고서 작성")
        {
        }

        protected override void OnUpdate(Command command)
        {
            command.IsEnabled = true; // file-based; no active part required
        }

        protected override void OnExecute(Command command, ExecutionContext context, Rectangle buttonRect)
        {
            string selectedPath = null;
            try
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Title = "Analyze Real Model — Select CAD File";
                    dlg.Filter = "CAD|*.stp;*.step;*.scdocx";
                    dlg.CheckFileExists = true;
                    dlg.Multiselect = false;
                    if (dlg.ShowDialog() != DialogResult.OK)
                        return;
                    selectedPath = dlg.FileName;
                }

                if (string.IsNullOrEmpty(selectedPath))
                    return;

                string outputDir = Path.GetDirectoryName(selectedPath);
                RealModelPipelineResult result;
                try
                {
                    result = RealModelPipeline.Run(selectedPath, outputDir);
                }
                catch (Exception runEx)
                {
                    ValidationHelper.ShowError(
                        "RealModelPipeline.Run threw:\n\n" + runEx.Message,
                        "Analyze Real Model");
                    return;
                }

                if (result == null)
                {
                    ValidationHelper.ShowError(
                        "Pipeline returned null result.",
                        "Analyze Real Model");
                    return;
                }

                if (!result.Success)
                {
                    ValidationHelper.ShowError(
                        "Analysis failed:\n\n" + (result.ErrorMessage ?? "(no message)"),
                        "Analyze Real Model");
                    return;
                }

                string msg = "Done. Report at " + (result.ReportPath ?? "(unknown)");
                if (!string.IsNullOrEmpty(result.JsonPath))
                    msg += "\nFeatureGraph JSON: " + result.JsonPath;
                MessageBox.Show(msg, "Analyze Real Model",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    "Analyze Real Model error:\n\n" + ex.Message,
                    "오류");
            }
        }
    }
}
