using System;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.TensileTest;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.DMA;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.BendingFixture;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Laminate;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Compression;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.CAI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Fatigue;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Joint;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Mesh;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Contact;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Simplify;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Material;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Export;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Load;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Simulation;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Pipeline;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.ConformalMesh;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.VoidCut;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.Package;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.GmshMesher;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.ReverseEngineer;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.TensileTest;

#if V251
using SpaceClaim.Api.V251.Extensibility;
#elif V252
using SpaceClaim.Api.V252.Extensibility;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller
{
    /// <summary>
    /// MX Digital Twin Modeller AddIn 메인 클래스
    /// </summary>
    public class MXAddIn : AddIn, IExtensibility, ICommandExtensibility, IRibbonExtensibility
    {
        // 커맨드 캡슐 등록
        private readonly CommandCapsule[] commandCapsules = new[]
        {
            // Ribbon Tab
            new CommandCapsule("MXDigitalTwinModeller.RibbonTab", "MX Modeller"),

            // 시편 생성 그룹 (인장 + 굽힘 통합)
            new CommandCapsule("MXDigitalTwinModeller.SpecimenGroup", "Specimen"),

            // 인장시편 (ASTM/DMA 통합 다이얼로그)
            new CreateASTMTensileSpecimenCommand(),

            // 굽힘시편 (3pt/4pt 통합 다이얼로그)
            new CreateDMA3PointBendingCommand(),

            // 벤딩 지그 (기존 모델에 지지구조 적용)
            new ApplyBendingFixtureCommand(),

            // 압축시편
            new CreateCompressionSpecimenCommand(),

            // 고급 시험 그룹
            new CommandCapsule("MXDigitalTwinModeller.AdvancedGroup", "Advanced"),

            // CAI 시편
            new CreateCAISpecimenCommand(),

            // 피로시편
            new CreateFatigueSpecimenCommand(),

            // 접합부 시편
            new CreateJointSpecimenCommand(),

            // 파라메트릭 모델러 그룹
            new CommandCapsule("MXDigitalTwinModeller.ParametricGroup", "Parametric"),

            // 적층 모델 생성
            new CreateLaminateCommand(),

            // 보이드 컷
            new VoidCutCommand(),

            // 패키지 임포트 (볼맵 → 솔더볼/수지/레이어 스택)
            new ImportPackageCommand(),

            // ODB++ 임포트 (ECAD → 보드/부품/패드 MCAD 솔리드)
            new Commands.Odb.ImportOdbCommand(),

            // 메쉬 설정 그룹
            new CommandCapsule("MXDigitalTwinModeller.MeshGroup", "Mesh"),

            // 재료 물성
            new MaterialCommand(),

            // 메쉬 설정
            new ApplyMeshSettingsCommand(),

            // 메쉬 내보내기
            new ExportMeshCommand(),

            // 바디 단순화
            new SimplifyCommand(),

            // 접촉면 감지
            new DetectContactCommand(),

            // STEP 내보내기
            new ExportStepCommand(),

            // 하중 정의
            new LoadCommand(),

            // 시뮬레이션 설정
            new SimulationSetupCommand(),

            // 일괄 실행 파이프라인
            new BatchPipelineCommand(),

            // Conformal Mesh
            new ConformalMeshCommand(),

            // Gmsh Mesher 그룹
            new CommandCapsule("MXDigitalTwinModeller.GmshMesherGroup", "DT Mesher"),

            // Gmsh 외부 메셔
            new GmshMesherCommand(),

            // Reverse Engineering 그룹 (Stage 1: feature extraction)
            new CommandCapsule("MXDigitalTwinModeller.ReverseEngineerGroup", "Reverse Eng"),

            // Feature 추출 (Body → FeatureGraph JSON)
            new ExtractFeaturesCommand(),

            // Feature 시각화 (face 별 색상 — Hole/Boss/Fillet/Wall/Slit)
            new VisualizeFeatureGraphCommand(),

            // Ask Claude (LLM tool-use loop on the FeatureGraph)
            new AskClaudeCommand(),

            // GATE-0: MCP thread-marshalling probe (throwaway, MCP_SERVER_PLAN.md)
            new GateMarshalTestCommand(),

            // Change Dimension (pick a feature → edit D/H/R/T via ModificationService)
            new ChangeDimensionCommand(),

            // Show in-session ModificationService history (display-only, Cycle 33)
            new ShowModificationHistoryCommand(),

            // Analyze a real CAD file end-to-end (.stp/.step/.scdocx → JSON + report)
            new AnalyzeRealModelCommand(),

            // Self-test suite
            new RunRETestSuiteCommand()
        };

        #region IExtensibility Members

        /// <summary>
        /// AddIn 연결 시 호출
        /// </summary>
        public bool Connect()
        {
            try
            {
                // 초기화 로직
                System.Diagnostics.Debug.WriteLine("MX Digital Twin Modeller AddIn Connected");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MX AddIn Connect Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// AddIn 연결 해제 시 호출
        /// </summary>
        public void Disconnect()
        {
            // 정리 로직
            System.Diagnostics.Debug.WriteLine("MX Digital Twin Modeller AddIn Disconnected");
        }

        #endregion

        #region ICommandExtensibility Members

        /// <summary>
        /// 커맨드 초기화
        /// </summary>
        public void Initialize()
        {
            try
            {
                // 모든 커맨드 캡슐 초기화
                foreach (var capsule in commandCapsules)
                {
                    capsule.Initialize();
                }

                System.Diagnostics.Debug.WriteLine($"Initialized {commandCapsules.Length} commands");

                // MCP server (Approach C, MCP_SERVER_PLAN): in-process loopback HttpListener
                // exposing the 20 mod tools so any MCP client can drive SC geometry. Best-effort:
                // a server bind failure must NOT break Add-In load.
                try { Services.ReverseEngineer.Mcp.McpServer.Instance.Start(); }
                catch (Exception mex) { System.Diagnostics.Debug.WriteLine("MCP server start failed: " + mex.Message); }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MX AddIn Initialize Error: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region IRibbonExtensibility Members

        /// <summary>
        /// Ribbon UI XML 반환
        /// </summary>
        public string GetCustomUI()
        {
            try
            {
                // Ribbon.xml 내용을 리소스에서 로드하거나 직접 반환
                string ribbonXml = GetRibbonXml();
                return ribbonXml;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MX AddIn GetCustomUI Error: {ex.Message}");
                return string.Empty;
            }
        }

        #endregion

        /// <summary>
        /// Ribbon XML 반환
        /// </summary>
        private string GetRibbonXml()
        {
            return @"<?xml version=""1.0"" encoding=""utf-8""?>
<customUI
  xmlns=""http://schemas.spaceclaim.com/customui""
  xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
  xsi:schemaLocation=""http://schemas.spaceclaim.com/customui http://schemas.spaceclaim.com/customui/SpaceClaimCustomUI.V251.xsd"">
    <ribbon>
        <tabs>
            <tab id=""MXDigitalTwinModeller.RibbonTab"" command=""MXDigitalTwinModeller.RibbonTab"">
                <group id=""MXDigitalTwinModeller.SpecimenGroup"" command=""MXDigitalTwinModeller.SpecimenGroup"">
                    <button id=""MXDigitalTwinModeller.CreateASTMTensileSpecimen""
                            size=""large""
                            command=""MXDigitalTwinModeller.CreateASTMTensileSpecimen""/>
                    <button id=""MXDigitalTwinModeller.CreateDMA3PointBending""
                            size=""large""
                            command=""MXDigitalTwinModeller.CreateDMA3PointBending""/>
                    <button id=""MXDigitalTwinModeller.ApplyBendingFixture""
                            size=""large""
                            command=""MXDigitalTwinModeller.ApplyBendingFixture""/>
                    <button id=""MXDigitalTwinModeller.CreateCompressionSpecimen""
                            size=""large""
                            command=""MXDigitalTwinModeller.CreateCompressionSpecimen""/>
                </group>
                <group id=""MXDigitalTwinModeller.AdvancedGroup"" command=""MXDigitalTwinModeller.AdvancedGroup"">
                    <button id=""MXDigitalTwinModeller.CreateCAISpecimen""
                            size=""large""
                            command=""MXDigitalTwinModeller.CreateCAISpecimen""/>
                    <button id=""MXDigitalTwinModeller.CreateFatigueSpecimen""
                            size=""large""
                            command=""MXDigitalTwinModeller.CreateFatigueSpecimen""/>
                    <button id=""MXDigitalTwinModeller.CreateJointSpecimen""
                            size=""large""
                            command=""MXDigitalTwinModeller.CreateJointSpecimen""/>
                </group>
                <group id=""MXDigitalTwinModeller.ParametricGroup"" command=""MXDigitalTwinModeller.ParametricGroup"">
                    <button id=""MXDigitalTwinModeller.CreateLaminate""
                            size=""large""
                            command=""MXDigitalTwinModeller.CreateLaminate""/>
                    <button id=""MXDigitalTwinModeller.VoidCut""
                            size=""large""
                            command=""MXDigitalTwinModeller.VoidCut""/>
                    <button id=""MXDigitalTwinModeller.ImportPackage""
                            size=""large""
                            command=""MXDigitalTwinModeller.ImportPackage""/>
                    <button id=""MXDigitalTwinModeller.ImportOdb""
                            size=""large""
                            command=""MXDigitalTwinModeller.ImportOdb""/>
                </group>
                <group id=""MXDigitalTwinModeller.MeshGroup"" command=""MXDigitalTwinModeller.MeshGroup"">
                    <button id=""MXDigitalTwinModeller.Material""
                            size=""large""
                            command=""MXDigitalTwinModeller.Material""/>
                    <button id=""MXDigitalTwinModeller.ApplyMeshSettings""
                            size=""large""
                            command=""MXDigitalTwinModeller.ApplyMeshSettings""/>
                    <button id=""MXDigitalTwinModeller.ExportMesh""
                            size=""large""
                            command=""MXDigitalTwinModeller.ExportMesh""/>
                    <button id=""MXDigitalTwinModeller.Simplify""
                            size=""large""
                            command=""MXDigitalTwinModeller.Simplify""/>
                    <button id=""MXDigitalTwinModeller.DetectContact""
                            size=""large""
                            command=""MXDigitalTwinModeller.DetectContact""/>
                    <button id=""MXDigitalTwinModeller.ExportStep""
                            size=""large""
                            command=""MXDigitalTwinModeller.ExportStep""/>
                    <button id=""MXDigitalTwinModeller.Load""
                            size=""large""
                            command=""MXDigitalTwinModeller.Load""/>
                    <button id=""MXDigitalTwinModeller.SimulationSetup""
                            size=""large""
                            command=""MXDigitalTwinModeller.SimulationSetup""/>
                    <button id=""MXDigitalTwinModeller.BatchPipeline""
                            size=""large""
                            command=""MXDigitalTwinModeller.BatchPipeline""/>
                    <button id=""MXDigitalTwinModeller.ConformalMesh""
                            size=""large""
                            command=""MXDigitalTwinModeller.ConformalMesh""/>
                </group>
                <group id=""MXDigitalTwinModeller.GmshMesherGroup"" command=""MXDigitalTwinModeller.GmshMesherGroup"">
                    <button id=""MXDigitalTwinModeller.GmshMesher""
                            size=""large""
                            command=""MXDigitalTwinModeller.GmshMesher""/>
                </group>
                <group id=""MXDigitalTwinModeller.ReverseEngineerGroup"" command=""MXDigitalTwinModeller.ReverseEngineerGroup"">
                    <button id=""MXDigitalTwinModeller.ExtractFeatures""
                            size=""large""
                            command=""MXDigitalTwinModeller.ExtractFeatures""/>
                    <button id=""MXDigitalTwinModeller.VisualizeFeatureGraph""
                            size=""large""
                            command=""MXDigitalTwinModeller.VisualizeFeatureGraph""/>
                    <button id=""MXDigitalTwinModeller.AskClaude""
                            size=""large""
                            command=""MXDigitalTwinModeller.AskClaude""/>
                    <button id=""MXDigitalTwinModeller.GateMarshalTest""
                            size=""large""
                            command=""MXDigitalTwinModeller.GateMarshalTest""/>
                    <button id=""MXDigitalTwinModeller.ChangeDimension""
                            size=""large""
                            command=""MXDigitalTwinModeller.ChangeDimension""/>
                    <button id=""MXDigitalTwinModeller.ShowModificationHistory""
                            size=""large""
                            command=""MXDigitalTwinModeller.ShowModificationHistory""/>
                    <button id=""MXDigitalTwinModeller.AnalyzeRealModel""
                            size=""large""
                            command=""MXDigitalTwinModeller.AnalyzeRealModel""/>
                    <button id=""MXDigitalTwinModeller.RunRETestSuite""
                            size=""large""
                            command=""MXDigitalTwinModeller.RunRETestSuite""/>
                </group>
            </tab>
        </tabs>
    </ribbon>
</customUI>";
        }
    }
}
