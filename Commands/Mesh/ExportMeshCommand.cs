using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using SpaceClaim.Api.V252.Analysis;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Commands;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Export;

#if V251
using SpaceClaim.Api.V251.Extensibility;
#elif V252
using SpaceClaim.Api.V252.Extensibility;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Mesh
{
    public class ExportMeshCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.ExportMesh";

        public ExportMeshCommand()
            : base(CommandName, "Mesh Export", IconHelper.ExportIcon,
                   "메쉬를 LS-DYNA, ANSYS, Abaqus 등으로 내보냅니다")
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
                using (var dlg = new SaveFileDialog())
                {
                    dlg.Title = "메쉬 내보내기 (Mesh Export)";
                    dlg.Filter =
                        "LS-DYNA (*.k)|*.k|" +
                        "ANSYS Mechanical (*.cdb)|*.cdb|" +
                        "Abaqus (*.inp)|*.inp|" +
                        "Fluent Mesh (*.msh)|*.msh|" +
                        "CGNS (*.cgns)|*.cgns";
                    dlg.FilterIndex = 1;
                    dlg.DefaultExt = "k";
                    dlg.OverwritePrompt = true;

                    if (dlg.ShowDialog() != DialogResult.OK)
                        return;

                    string path = dlg.FileName;
                    int filterIndex = dlg.FilterIndex;

                    // WriteBlock 밖에서 결과를 받을 변수
                    string formatName = "";
                    var matLog = new List<string>();
                    int matPatched = 0;
                    int controlInserted = 0;

                    // 프로그래스 표시 + 작업 실행
                    ProgressOverlay.RunWithProgress("메쉬 내보내기 중...", () =>
                    {
                        WriteBlock.ExecuteTask("Export Mesh", () =>
                        {
                            int result;

                            switch (filterIndex)
                            {
                                case 1:
                                    formatName = "LS-DYNA (.k)";
                                    result = MeshMethods.SaveDYNA(path);
                                    break;
                                case 2:
                                    formatName = "ANSYS (.cdb)";
                                    result = MeshMethods.SaveANSYS(path);
                                    break;
                                case 3:
                                    formatName = "Abaqus (.inp)";
                                    result = MeshMethods.SaveAbaqus(path);
                                    break;
                                case 4:
                                    formatName = "Fluent (.msh)";
                                    result = MeshMethods.SaveFluentMesh(path);
                                    break;
                                case 5:
                                    formatName = "CGNS (.cgns)";
                                    result = MeshMethods.SaveCGNS(path);
                                    break;
                                default:
                                    formatName = "LS-DYNA (.k)";
                                    result = MeshMethods.SaveDYNA(path);
                                    break;
                            }

                            // LS-DYNA .k 파일 후처리: 실제 물성으로 교체
                            if (filterIndex == 1 && File.Exists(path))
                            {
                                try
                                {
                                    Part activePart = Window.ActiveWindow.Document.MainPart;
                                    matPatched = KFilePostProcessor.PatchMaterials(path, activePart, matLog);
                                }
                                catch (Exception patchEx)
                                {
                                    matLog.Add("[KFile] 물성 후처리 오류: " + patchEx.Message);
                                }

                                // 시뮬레이션 제어 키워드 + 하중 커브 삽입
                                try
                                {
                                    controlInserted = KFilePostProcessor.AppendControlCards(path, matLog);
                                }
                                catch (Exception ctrlEx)
                                {
                                    matLog.Add("[KFile] 제어카드 삽입 오류: " + ctrlEx.Message);
                                }
                            }
                        });
                    });

                    // 프로그래스 닫힌 후 결과 표시
                    bool fileExists = File.Exists(path);
                    long fileSize = fileExists ? new FileInfo(path).Length : 0;

                    if (fileExists && fileSize > 0)
                    {
                        string sizeStr = fileSize < 1024 * 1024
                            ? string.Format("{0:F1} KB", fileSize / 1024.0)
                            : string.Format("{0:F1} MB", fileSize / (1024.0 * 1024.0));

                        string matInfo = "";
                        if (matPatched > 0)
                            matInfo = string.Format("\n물성 교체: {0}개 재료", matPatched);
                        else if (filterIndex == 1 && matLog.Count > 0)
                            matInfo = "\n물성 후처리: " + matLog[matLog.Count - 1];

                        if (controlInserted > 0)
                            matInfo += string.Format("\n시뮬레이션 제어 + 하중 커브 삽입: {0}개 블록", controlInserted);

                        MessageBox.Show(
                            string.Format("메쉬 내보내기 완료!\n\n포맷: {0}\n경로: {1}\n파일 크기: {2}{3}",
                                formatName, path, sizeStr, matInfo),
                            "Export Mesh",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    else
                    {
                        ValidationHelper.ShowError(
                            string.Format("메쉬 내보내기 실패\n\n포맷: {0}\n경로: {1}\n\n메쉬가 생성되어 있는지 확인하세요.",
                                formatName, path),
                            "Export Mesh");
                    }
                }
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    string.Format("메쉬 내보내기 중 오류가 발생했습니다:\n\n{0}", ex.Message),
                    "오류");
            }
        }
    }
}
