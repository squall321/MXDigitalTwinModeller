using System.Collections.Generic;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer
{
    /// <summary>
    /// 같은 (또는 거의 같은) radius 로 tangent 하게 연결된 fillet face 들의 그룹.
    /// 예: 박스의 12개 edge fillet 이 corner 에서 만나는 경우 12개 cylinder + 8개 sphere/torus
    /// 가 한 chain 으로 묶일 수 있음 (모두 같은 R 이라면).
    /// </summary>
    public class FilletChain
    {
        /// <summary>식별자 (예: "FC1")</summary>
        public string Id { get; set; }

        /// <summary>이 chain 에 속한 fillet face 인덱스들</summary>
        public List<int> FaceIndices { get; set; }

        /// <summary>chain 의 대표 R (mm, snap 된 nominal 값)</summary>
        public double RadiusMm { get; set; }

        /// <summary>chain 내 face 들의 raw R 범위 (분포 확인용)</summary>
        public double RadiusMinMm { get; set; }
        public double RadiusMaxMm { get; set; }

        /// <summary>chain 의 edge fillet (cylinder) 개수</summary>
        public int EdgeFilletCount { get; set; }

        /// <summary>chain 의 corner blend (torus/sphere) 개수</summary>
        public int CornerBlendCount { get; set; }

        /// <summary>
        /// chain 안에서 R 이 의미있게 변하는가 (max/min &gt; 1.5).
        /// true 이면 RadiusMm 는 "대표값 (mode)" 으로 보고 RadiusMinMm / RadiusMaxMm
        /// 로 분포를 확인해야 한다. tangent 연속성은 유지되므로 한 chain 으로 묶지만
        /// 단일 R 가정의 다운스트림 로직 (예: AutoFillet) 은 이 플래그를 보고 분기해야 한다.
        /// </summary>
        public bool IsVariableRadius { get; set; }

        public FilletChain()
        {
            FaceIndices = new List<int>();
        }
    }
}
