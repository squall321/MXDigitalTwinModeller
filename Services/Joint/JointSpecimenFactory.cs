using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Joint;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Joint
{
    public class JointSpecimenFactory
    {
        public static readonly string[] PresetLabels = new[]
        {
            "ASTM D1002 - 단일겹치기 (Single Lap Shear)",
            "ASTM D3528 - 이중겹치기 (Double Lap Shear)",
            "스카프 접합 (Scarf Joint)",
            "맞대기 접합 (Butt Joint)",
            "T형 접합 (T-Joint Pull-off)",
            "사용자 정의 (Custom)"
        };

        public static readonly string[] PresetDescriptions = new[]
        {
            "ASTM D1002 단일겹치기 전단접합. 25.4mm 오버랩, 전단 강도 측정.",
            "ASTM D3528 이중겹치기 전단접합. 양면 접착, 편심 하중 감소.",
            "스카프 접합. 경사면 접착, 균일 응력 분포. 각도 조절 가능.",
            "맞대기 접합. 끝단 맞대기 접착, 인장 강도 측정.",
            "T형 접합. 플랜지+웹 구조, 풀오프(박리) 시험용.",
            "사용자 정의 치수."
        };

        public static readonly JointSpecimenType[] PresetTypes = new[]
        {
            JointSpecimenType.ASTM_D1002_SingleLap,
            JointSpecimenType.ASTM_D3528_DoubleLap,
            JointSpecimenType.Scarf_Joint,
            JointSpecimenType.Butt_Joint,
            JointSpecimenType.T_Joint,
            JointSpecimenType.Custom
        };
    }
}
