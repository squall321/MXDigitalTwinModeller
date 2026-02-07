using System.Collections.Generic;
using System.Linq;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Laminate
{
    /// <summary>
    /// 적층 방향
    /// </summary>
    public enum StackingDirection { X, Y, Z }

    /// <summary>
    /// 직사각형 적층 모델 파라미터
    /// </summary>
    public class RectangularLaminateParameters
    {
        /// <summary>적층판 폭 [mm]</summary>
        public double WidthMm { get; set; }

        /// <summary>적층판 길이 [mm]</summary>
        public double LengthMm { get; set; }

        /// <summary>적층 방향</summary>
        public StackingDirection Direction { get; set; }

        /// <summary>레이어 목록 (적층 순서대로)</summary>
        public List<LaminateLayerDefinition> Layers { get; set; }

        /// <summary>Share Topology 활성화 여부</summary>
        public bool EnableShareTopology { get; set; }

        /// <summary>인터페이스 Named Selection 생성 여부</summary>
        public bool CreateInterfaceNamedSelections { get; set; }

        public RectangularLaminateParameters()
        {
            WidthMm = 100.0;
            LengthMm = 100.0;
            Direction = StackingDirection.Z;
            Layers = new List<LaminateLayerDefinition>
            {
                new LaminateLayerDefinition("Layer_1", 0.25),
                new LaminateLayerDefinition("Layer_2", 0.25),
                new LaminateLayerDefinition("Layer_3", 0.25),
            };
            EnableShareTopology = true;
            CreateInterfaceNamedSelections = true;
        }

        /// <summary>
        /// 총 두께 [mm]
        /// </summary>
        public double GetTotalThicknessMm()
        {
            return Layers.Sum(l => l.ThicknessMm);
        }

        /// <summary>
        /// 유효성 검증
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            if (WidthMm <= 0)
            {
                errorMessage = "폭은 0보다 커야 합니다.";
                return false;
            }

            if (LengthMm <= 0)
            {
                errorMessage = "길이는 0보다 커야 합니다.";
                return false;
            }

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

            // 이름 중복 확인
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
