using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Fatigue;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Fatigue
{
    /// <summary>
    /// 피로 시편 프리셋 팩토리
    /// </summary>
    public class FatigueSpecimenFactory
    {
        public static readonly string[] PresetLabels = new[]
        {
            "ASTM E466 - 균일 게이지 (Dog-bone, 평판)",
            "ASTM E466 - 모래시계형 (Hourglass, 평판)",
            "ASTM E606 - 변형률제어 (LCF, 원형단면)",
            "ASTM E647 - CT (Compact Tension)",
            "ASTM E647 - M(T) (Middle Tension)",
            "ASTM E2207 - 다축 피로 (박벽 원통)",
            "사용자 정의 (Custom)"
        };

        public static readonly string[] PresetDescriptions = new[]
        {
            "ASTM E466 하중제어 피로시편. 직사각형 단면 dog-bone, 큰 필렛 반경.",
            "ASTM E466 모래시계형. 연속 곡률로 최소 단면이 중앙에 위치.",
            "ASTM E606 변형률제어 저주기피로(LCF). 원형 단면, 짧은 게이지.",
            "ASTM E647 Compact Tension. 피로균열성장(FCG) 시험용. 핀홀+노치.",
            "ASTM E647 Middle Tension. 중앙 양방향 관통 슬롯.",
            "ASTM E2207 다축 피로. 박벽 원통형, 축방향+비틀림 하중.",
            "사용자 정의 치수."
        };

        public static readonly FatigueSpecimenType[] PresetTypes = new[]
        {
            FatigueSpecimenType.ASTM_E466_Uniform,
            FatigueSpecimenType.ASTM_E466_Hourglass,
            FatigueSpecimenType.ASTM_E606,
            FatigueSpecimenType.ASTM_E647_CT,
            FatigueSpecimenType.ASTM_E647_MT,
            FatigueSpecimenType.ASTM_E2207,
            FatigueSpecimenType.Custom
        };
    }
}
