using System.Collections.Generic;
using System.Linq;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Laminate
{
    /// <summary>
    /// 오프셋 방향
    /// </summary>
    public enum OffsetDirection
    {
        Normal,
        Reverse
    }

    /// <summary>
    /// 면 기반 적층 모델 파라미터
    /// </summary>
    public class SurfaceLaminateParameters
    {
        /// <summary>오프셋 방향 (법선 / 역방향)</summary>
        public OffsetDirection Direction { get; set; }

        /// <summary>레이어 목록 (적층 순서대로)</summary>
        public List<LaminateLayerDefinition> Layers { get; set; }

        /// <summary>인터페이스 Named Selection 생성 여부</summary>
        public bool CreateInterfaceNamedSelections { get; set; }

        public SurfaceLaminateParameters()
        {
            Direction = OffsetDirection.Normal;
            Layers = new List<LaminateLayerDefinition>
            {
                new LaminateLayerDefinition("Layer_1", 0.25),
                new LaminateLayerDefinition("Layer_2", 0.25),
                new LaminateLayerDefinition("Layer_3", 0.25),
            };
            CreateInterfaceNamedSelections = true;
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
                    errorMessage = $"레이어 {i + 1}의 이름이 비어있습니다.";
                    return false;
                }
                if (Layers[i].ThicknessMm <= 0)
                {
                    errorMessage = $"레이어 {i + 1} ({Layers[i].Name})의 두께는 0보다 커야 합니다.";
                    return false;
                }
            }

            var names = new HashSet<string>();
            for (int i = 0; i < Layers.Count; i++)
            {
                if (!names.Add(Layers[i].Name))
                {
                    errorMessage = $"레이어 이름이 중복됩니다: \"{Layers[i].Name}\"";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }
    }
}
