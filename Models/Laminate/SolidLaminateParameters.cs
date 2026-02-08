using System.Collections.Generic;
using System.Linq;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Laminate
{
    /// <summary>
    /// 솔리드 기반 적층 모델 파라미터
    /// </summary>
    public class SolidLaminateParameters
    {
        /// <summary>레이어 목록 (적층 순서대로)</summary>
        public List<LaminateLayerDefinition> Layers { get; set; }

        /// <summary>인터페이스 Named Selection 생성 여부</summary>
        public bool CreateInterfaceNamedSelections { get; set; }

        /// <summary>원본 바디 삭제 여부</summary>
        public bool DeleteOriginalBody { get; set; }

        public SolidLaminateParameters()
        {
            Layers = new List<LaminateLayerDefinition>
            {
                new LaminateLayerDefinition("Layer_1", 0.25),
                new LaminateLayerDefinition("Layer_2", 0.25),
                new LaminateLayerDefinition("Layer_3", 0.25),
            };
            CreateInterfaceNamedSelections = true;
            DeleteOriginalBody = true;
        }

        public double GetTotalThicknessMm()
        {
            return Layers.Sum(l => l.ThicknessMm);
        }

        public bool Validate(out string errorMessage)
        {
            if (Layers == null || Layers.Count == 0)
            {
                errorMessage = "최소 1개의 레이어가 필요합니다.";
                return false;
            }

            for (int i = 0; i < Layers.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(Layers[i].Name))
                {
                    errorMessage = string.Format("레이어 {0}의 이름이 비어있습니다.", i + 1);
                    return false;
                }
                if (Layers[i].ThicknessMm <= 0)
                {
                    errorMessage = string.Format("레이어 {0} ({1})의 두께는 0보다 커야 합니다.",
                        i + 1, Layers[i].Name);
                    return false;
                }
            }

            var names = new HashSet<string>();
            for (int i = 0; i < Layers.Count; i++)
            {
                if (!names.Add(Layers[i].Name))
                {
                    errorMessage = string.Format("레이어 이름이 중복됩니다: \"{0}\"", Layers[i].Name);
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }
    }
}
