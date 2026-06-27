using System;
using System.IO;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.IO;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.TensileTest;

#if V251
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.TensileTest
{
    /// <summary>
    /// 시편 메타데이터 관리 서비스
    /// SpaceClaim → Mechanical 데이터 전달을 위한 YAML 파일 생성
    ///
    /// Phase 0: Workbench Parameters 대신 YAML 파일 기반 메타데이터 교환
    ///
    /// 동작 방식:
    /// 1. 시편 생성 시: Document가 저장되어 있으면 YAML 즉시 생성
    /// 2. Document가 저장 안되어 있으면: 임시 JSON 파일에 저장 (.scdoc과 같은 위치 예정)
    /// 3. Document 저장 후 다시 호출하면: YAML 생성
    /// </summary>
    // @lat: [[pipeline#Specimen YAML]]
    public class SpecimenMetadataService
    {
        private const string MetadataFileName = "specimen.yaml";
        private const string PendingFileName = ".pending_specimen.json";

        /// <summary>
        /// 시편 메타데이터를 YAML 파일로 저장
        /// Document가 저장되어 있으면 즉시 YAML 생성, 아니면 pending JSON 생성
        /// </summary>
        public static void SaveMetadata(Document document, TensileSpecimenParameters parameters)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            try
            {
                // Document 파일 경로 확인
                string documentPath = document.Path;

                if (!string.IsNullOrEmpty(documentPath) && File.Exists(documentPath))
                {
                    // Document가 이미 저장됨 → YAML 생성
                    GenerateYaml(documentPath, parameters);

                    string yamlPath = Path.Combine(Path.GetDirectoryName(documentPath), MetadataFileName);
                    System.Diagnostics.Debug.WriteLine(
                        $"[SpecimenMetadata] YAML saved: {yamlPath}");
                }
                else
                {
                    // Document 저장 안됨 → temp만 저장 (조용히)
                    SavePendingMetadata(parameters);
                    System.Diagnostics.Debug.WriteLine(
                        "[SpecimenMetadata] Document not saved, temp YAML only");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SpecimenMetadata] Error: {ex.Message}");
                // 조용히 실패 (사용자에게 방해 안함)
            }
        }

        /// <summary>
        /// Pending 메타데이터를 임시 폴더에 JSON으로 저장
        /// </summary>
        private static void SavePendingMetadata(TensileSpecimenParameters parameters)
        {
            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), PendingFileName);
                string json = SerializeParametersToJson(parameters);
                File.WriteAllText(tempPath, json);
                System.Diagnostics.Debug.WriteLine($"[SpecimenMetadata] Pending saved to: {tempPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SpecimenMetadata] Failed to save pending: {ex.Message}");
            }
        }

        /// <summary>
        /// Document 저장 후 호출: pending 메타데이터가 있으면 YAML 생성
        /// </summary>
        public static void CheckAndGeneratePending(Document document)
        {
            if (document == null)
                return;

            try
            {
                string documentPath = document.Path;
                if (string.IsNullOrEmpty(documentPath))
                    return;

                // Pending 파일 확인
                string tempPath = Path.Combine(Path.GetTempPath(), PendingFileName);
                if (!File.Exists(tempPath))
                    return;

                // Pending 파라미터 읽기
                string json = File.ReadAllText(tempPath);
                var parameters = DeserializeParametersFromJson(json);
                if (parameters == null)
                    return;

                // YAML 생성
                GenerateYaml(documentPath, parameters);

                // Pending 파일 삭제
                File.Delete(tempPath);

                System.Diagnostics.Debug.WriteLine(
                    $"[SpecimenMetadata] YAML generated from pending: {documentPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SpecimenMetadata] CheckAndGeneratePending error: {ex.Message}");
            }
        }

        /// <summary>
        /// YAML 파일 생성
        /// </summary>
        private static void GenerateYaml(string documentPath, TensileSpecimenParameters parameters)
        {
            string directory = Path.GetDirectoryName(documentPath);
            string yamlPath = Path.Combine(directory, MetadataFileName);

            var yaml = new YamlWriter();

            // Header
            yaml.WriteComment("MX Material Twin - Specimen Metadata");
            yaml.WriteComment($"Auto-generated from SpaceClaim: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            yaml.WriteLine();

            // Specimen type and geometry
            yaml.WriteKeyValue("specimen_type", parameters.SpecimenType.ToString());
            yaml.WriteKeyValue("gauge_length_mm", parameters.GaugeLength);
            yaml.WriteKeyValue("gauge_width_mm", parameters.GaugeWidth);
            yaml.WriteKeyValue("thickness_mm", parameters.Thickness);

            // Additional dimensions based on specimen type
            if (IsGrippedSpecimen(parameters.SpecimenType))
            {
                yaml.WriteKeyValue("grip_length_mm", parameters.GripLength);
                yaml.WriteKeyValue("grip_width_mm", parameters.GripWidth);
                yaml.WriteKeyValue("total_length_mm", parameters.TotalLength);

                if (parameters.FilletRadius > 0)
                {
                    yaml.WriteKeyValue("fillet_radius_mm", parameters.FilletRadius);
                }
            }

            // Holes (if applicable)
            if (parameters.HoleDiameter > 0)
            {
                yaml.WriteKeyValue("hole_diameter_mm", parameters.HoleDiameter);
            }

            // Notches (if applicable)
            if (parameters.NotchDepth > 0)
            {
                yaml.WriteKeyValue("notch_depth_mm", parameters.NotchDepth);
                yaml.WriteKeyValue("notch_angle_deg", parameters.NotchAngle);
                yaml.WriteKeyValue("notch_radius_mm", parameters.NotchRadius);
            }

            yaml.WriteLine();

            // Named Selections
            yaml.WriteComment("Named Selections for boundary conditions");
            yaml.WriteListStart("named_selections");
            yaml.WriteListItem("Specimen_LeftEnd");
            yaml.WriteListItem("Specimen_RightEnd");

            // Add gauge section if applicable
            if (IsGrippedSpecimen(parameters.SpecimenType))
            {
                yaml.WriteListItem("Specimen_GaugeSection");
            }

            yaml.WriteListEnd();
            yaml.WriteLine();

            // Creation metadata
            yaml.WriteComment("Creation information");
            yaml.WriteKeyValue("created_date", DateTime.UtcNow);
            yaml.WriteKeyValue("spaceclaim_version", "V252");
            yaml.WriteKeyValue("document_path", documentPath);

            // Save to file (primary location: next to .scdoc)
            yaml.SaveToFile(yamlPath);

            // ALSO save to temp location (Workbench fallback)
            // Mechanical will check this if YAML not found next to geometry
            try
            {
                string tempYamlPath = Path.Combine(Path.GetTempPath(), "mx_specimen_last.yaml");
                yaml.SaveToFile(tempYamlPath);
                System.Diagnostics.Debug.WriteLine($"[SpecimenMetadata] YAML also saved to temp: {tempYamlPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SpecimenMetadata] Failed to save temp YAML: {ex.Message}");
            }
        }

        /// <summary>
        /// JSON serialize (간단한 구현)
        /// </summary>
        private static string SerializeParametersToJson(TensileSpecimenParameters p)
        {
            return string.Format(@"{{
  ""SpecimenType"": ""{0}"",
  ""GaugeLength"": {1},
  ""GaugeWidth"": {2},
  ""Thickness"": {3},
  ""GripLength"": {4},
  ""GripWidth"": {5},
  ""TotalLength"": {6},
  ""FilletRadius"": {7},
  ""HoleDiameter"": {8},
  ""NotchDepth"": {9},
  ""NotchAngle"": {10},
  ""NotchRadius"": {11}
}}",
                p.SpecimenType,
                p.GaugeLength, p.GaugeWidth, p.Thickness,
                p.GripLength, p.GripWidth, p.TotalLength,
                p.FilletRadius, p.HoleDiameter,
                p.NotchDepth, p.NotchAngle, p.NotchRadius);
        }

        /// <summary>
        /// JSON deserialize (간단한 파싱)
        /// </summary>
        private static TensileSpecimenParameters DeserializeParametersFromJson(string json)
        {
            try
            {
                var p = new TensileSpecimenParameters();
                p.SpecimenType = ParseEnumValue<ASTMSpecimenType>(json, "SpecimenType");
                p.GaugeLength = ParseDoubleValue(json, "GaugeLength");
                p.GaugeWidth = ParseDoubleValue(json, "GaugeWidth");
                p.Thickness = ParseDoubleValue(json, "Thickness");
                p.GripLength = ParseDoubleValue(json, "GripLength");
                p.GripWidth = ParseDoubleValue(json, "GripWidth");
                p.TotalLength = ParseDoubleValue(json, "TotalLength");
                p.FilletRadius = ParseDoubleValue(json, "FilletRadius");
                p.HoleDiameter = ParseDoubleValue(json, "HoleDiameter");
                p.NotchDepth = ParseDoubleValue(json, "NotchDepth");
                p.NotchAngle = ParseDoubleValue(json, "NotchAngle");
                p.NotchRadius = ParseDoubleValue(json, "NotchRadius");
                return p;
            }
            catch
            {
                return null;
            }
        }

        private static T ParseEnumValue<T>(string json, string key) where T : struct
        {
            string pattern = $"\"{key}\": \"([^\"]+)\"";
            var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
            if (match.Success && Enum.TryParse<T>(match.Groups[1].Value, out T result))
                return result;
            return default(T);
        }

        private static double ParseDoubleValue(string json, string key)
        {
            string pattern = $"\"{key}\": ([0-9.]+)";
            var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
            if (match.Success && double.TryParse(match.Groups[1].Value, out double result))
                return result;
            return 0.0;
        }

        /// <summary>
        /// 시편 메타데이터 YAML 파일 찾기
        /// </summary>
        public static string FindMetadataFile(string geometryPath)
        {
            if (string.IsNullOrEmpty(geometryPath) || !File.Exists(geometryPath))
                return null;

            string directory = Path.GetDirectoryName(geometryPath);
            string yamlPath = Path.Combine(directory, MetadataFileName);

            return File.Exists(yamlPath) ? yamlPath : null;
        }

        /// <summary>
        /// Gripped specimen 여부 확인
        /// </summary>
        private static bool IsGrippedSpecimen(ASTMSpecimenType type)
        {
            switch (type)
            {
                case ASTMSpecimenType.ASTM_E8_Standard:
                case ASTMSpecimenType.ASTM_E8_SubSize:
                case ASTMSpecimenType.ISO_6892_1:
                case ASTMSpecimenType.ASTM_D638_TypeI:
                case ASTMSpecimenType.ASTM_D638_TypeII:
                case ASTMSpecimenType.ASTM_D638_TypeIII:
                case ASTMSpecimenType.ASTM_D638_TypeIV:
                case ASTMSpecimenType.ASTM_D638_TypeV:
                case ASTMSpecimenType.ISO_527_2_Type1A:
                case ASTMSpecimenType.ISO_527_2_Type1B:
                case ASTMSpecimenType.DMA_Tensile_DogBone:
                    return true;

                default:
                    return false;
            }
        }
    }
}
