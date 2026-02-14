using System.IO;
using System.Reflection;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI
{
    /// <summary>
    /// 임베디드 리소스에서 아이콘을 로드하는 헬퍼
    /// 주의: SpaceClaim.Api.V252.Image와 충돌 방지를 위해 System.Drawing.Image를 정규화하여 사용
    ///
    /// Image.FromStream은 스트림을 닫으면 이미지가 손상되므로
    /// MemoryStream에 복사 후 로드하여 스트림 수명 문제를 방지
    /// </summary>
    public static class IconHelper
    {
        private static readonly Assembly CurrentAssembly = Assembly.GetExecutingAssembly();

        private static System.Drawing.Image _tensileIcon;
        private static System.Drawing.Image _bendingIcon;
        private static System.Drawing.Image _bendingFixtureIcon;
        private static System.Drawing.Image _laminateIcon;
        private static System.Drawing.Image _compressionIcon;
        private static System.Drawing.Image _caiIcon;
        private static System.Drawing.Image _fatigueIcon;
        private static System.Drawing.Image _jointIcon;
        private static System.Drawing.Image _meshIcon;
        private static System.Drawing.Image _exportIcon;
        private static System.Drawing.Image _contactIcon;
        private static System.Drawing.Image _loadDefIcon;
        private static System.Drawing.Image _exportStepIcon;
        private static System.Drawing.Image _materialIcon;
        private static System.Drawing.Image _simulationIcon;
        private static System.Drawing.Image _pipelineIcon;
        private static System.Drawing.Image _conformalMeshIcon;

        public static System.Drawing.Image TensileIcon =>
            _tensileIcon ?? (_tensileIcon = LoadIcon("Resources.Icons.Tensile_32.png"));

        public static System.Drawing.Image BendingIcon =>
            _bendingIcon ?? (_bendingIcon = LoadIcon("Resources.Icons.Bending_32.png"));

        public static System.Drawing.Image BendingFixtureIcon =>
            _bendingFixtureIcon ?? (_bendingFixtureIcon = LoadIcon("Resources.Icons.BendingFixture_32.png"));

        public static System.Drawing.Image LaminateIcon =>
            _laminateIcon ?? (_laminateIcon = LoadIcon("Resources.Icons.Laminate_32.png"));

        public static System.Drawing.Image CompressionIcon =>
            _compressionIcon ?? (_compressionIcon = LoadIcon("Resources.Icons.Compression_32.png"));

        public static System.Drawing.Image CAIIcon =>
            _caiIcon ?? (_caiIcon = LoadIcon("Resources.Icons.CAI_32.png"));

        public static System.Drawing.Image FatigueIcon =>
            _fatigueIcon ?? (_fatigueIcon = LoadIcon("Resources.Icons.Fatigue_32.png"));

        public static System.Drawing.Image JointIcon =>
            _jointIcon ?? (_jointIcon = LoadIcon("Resources.Icons.Joint_32.png"));

        public static System.Drawing.Image MeshIcon =>
            _meshIcon ?? (_meshIcon = LoadIcon("Resources.Icons.Mesh_32.png"));

        public static System.Drawing.Image ExportIcon =>
            _exportIcon ?? (_exportIcon = LoadIcon("Resources.Icons.Export_32.png"));

        public static System.Drawing.Image ContactIcon =>
            _contactIcon ?? (_contactIcon = LoadIcon("Resources.Icons.Contact_32.png"));

        public static System.Drawing.Image LoadDefIcon =>
            _loadDefIcon ?? (_loadDefIcon = LoadIcon("Resources.Icons.Load_32.png"));

        public static System.Drawing.Image ExportStepIcon =>
            _exportStepIcon ?? (_exportStepIcon = LoadIcon("Resources.Icons.ExportStep_32.png"));

        public static System.Drawing.Image MaterialIcon =>
            _materialIcon ?? (_materialIcon = LoadIcon("Resources.Icons.Material_32.png"));

        public static System.Drawing.Image SimulationIcon =>
            _simulationIcon ?? (_simulationIcon = LoadIcon("Resources.Icons.Simulation_32.png"));

        public static System.Drawing.Image PipelineIcon =>
            _pipelineIcon ?? (_pipelineIcon = LoadIcon("Resources.Icons.Pipeline_32.png"));

        public static System.Drawing.Image ConformalMeshIcon =>
            _conformalMeshIcon ?? (_conformalMeshIcon = LoadIcon("Resources.Icons.ConformalMesh_32.png"));

        /// <summary>
        /// 임베디드 리소스에서 이미지 로드
        /// MemoryStream에 복사하여 원본 스트림 해제 후에도 이미지가 유효하도록 보장
        /// </summary>
        private static System.Drawing.Image LoadIcon(string resourceSuffix)
        {
            string resourceName = "MXDigitalTwinModeller." + resourceSuffix;

            using (Stream stream = CurrentAssembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    ms.Position = 0;
                    return System.Drawing.Image.FromStream(ms);
                }
            }

            System.Diagnostics.Debug.WriteLine(
                string.Format("Icon resource not found: {0}", resourceName));
            return null;
        }
    }
}
