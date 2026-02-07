using System.IO;
using System.Reflection;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI
{
    /// <summary>
    /// 임베디드 리소스에서 아이콘을 로드하는 헬퍼
    /// 주의: SpaceClaim.Api.V252.Image와 충돌 방지를 위해 System.Drawing.Image를 정규화하여 사용
    /// </summary>
    public static class IconHelper
    {
        private static readonly Assembly CurrentAssembly = Assembly.GetExecutingAssembly();

        /// <summary>
        /// 인장시험 아이콘 (32x32)
        /// </summary>
        public static System.Drawing.Image TensileIcon => LoadIcon("Resources.Icons.Tensile_32.png");

        /// <summary>
        /// 굽힘시험 아이콘 (32x32)
        /// </summary>
        public static System.Drawing.Image BendingIcon => LoadIcon("Resources.Icons.Bending_32.png");

        /// <summary>
        /// 벤딩 지그 아이콘 (32x32)
        /// </summary>
        public static System.Drawing.Image BendingFixtureIcon => LoadIcon("Resources.Icons.BendingFixture_32.png");

        /// <summary>
        /// 적층 모델 아이콘 (32x32)
        /// </summary>
        public static System.Drawing.Image LaminateIcon => LoadIcon("Resources.Icons.Laminate_32.png");

        /// <summary>
        /// 압축시험 아이콘 (32x32)
        /// </summary>
        public static System.Drawing.Image CompressionIcon => LoadIcon("Resources.Icons.Compression_32.png");

        /// <summary>
        /// CAI 시편 아이콘 (32x32)
        /// </summary>
        public static System.Drawing.Image CAIIcon => LoadIcon("Resources.Icons.CAI_32.png");

        /// <summary>
        /// 피로시편 아이콘 (32x32)
        /// </summary>
        public static System.Drawing.Image FatigueIcon => LoadIcon("Resources.Icons.Fatigue_32.png");

        /// <summary>
        /// 접합부 시편 아이콘 (32x32)
        /// </summary>
        public static System.Drawing.Image JointIcon => LoadIcon("Resources.Icons.Joint_32.png");

        /// <summary>
        /// 메쉬 설정 아이콘 (32x32)
        /// </summary>
        public static System.Drawing.Image MeshIcon => LoadIcon("Resources.Icons.Mesh_32.png");

        /// <summary>
        /// 메쉬 내보내기 아이콘 (32x32)
        /// </summary>
        public static System.Drawing.Image ExportIcon => LoadIcon("Resources.Icons.Export_32.png");

        /// <summary>
        /// 접촉면 감지 아이콘 (32x32)
        /// </summary>
        public static System.Drawing.Image ContactIcon => LoadIcon("Resources.Icons.Contact_32.png");

        /// <summary>
        /// 하중 정의 아이콘 (32x32)
        /// </summary>
        public static System.Drawing.Image LoadDefIcon => LoadIcon("Resources.Icons.Load_32.png");

        /// <summary>
        /// 시뮬레이션 설정 아이콘 (32x32)
        /// </summary>
        public static System.Drawing.Image SimulationIcon => LoadIcon("Resources.Icons.Simulation_32.png");

        /// <summary>
        /// 임베디드 리소스에서 이미지 로드
        /// </summary>
        private static System.Drawing.Image LoadIcon(string resourceSuffix)
        {
            string resourceName = "MXDigitalTwinModeller." + resourceSuffix;

            using (Stream stream = CurrentAssembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    return System.Drawing.Image.FromStream(stream);
                }
            }

            System.Diagnostics.Debug.WriteLine($"Icon resource not found: {resourceName}");
            return null;
        }
    }
}
