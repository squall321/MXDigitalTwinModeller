using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.DMA;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.DMA
{
    /// <summary>
    /// DMA 시편의 기본 파라미터를 제공하는 팩토리 클래스
    /// </summary>
    public class DMASpecimenFactory
    {
        /// <summary>
        /// 시험 타입에 따른 설명 반환
        /// </summary>
        public string GetTestTypeDescription(DMATestType testType)
        {
            switch (testType)
            {
                case DMATestType.Tensile:
                    return "DMA 인장 시험 (Tensile Test)";

                case DMATestType.ThreePointBending:
                    return "DMA 3점 굽힘 시험 (3-Point Bending)";

                case DMATestType.FourPointBending:
                    return "DMA 4점 굽힘 시험 (4-Point Bending)";

                default:
                    return "알 수 없는 시험 타입";
            }
        }

        /// <summary>
        /// 시편 타입에 따른 설명 반환
        /// </summary>
        public string GetSpecimenTypeDescription(DMASpecimenType specimenType)
        {
            switch (specimenType)
            {
                case DMASpecimenType.Standard:
                    return "표준 사이즈";

                case DMASpecimenType.Custom:
                    return "사용자 정의";

                default:
                    return "알 수 없는 시편 타입";
            }
        }
    }
}
