using System.Collections.Generic;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer
{
    /// <summary>
    /// Edge fillet (모서리 R) — 인접한 두 평면 사이의 cylindrical 또는 toroidal blend face.
    /// </summary>
    // @lat: [[reverse-engineer#FilletDetection]]
    public class FilletFeature
    {
        /// <summary>식별자 (예: "F1")</summary>
        public string Id { get; set; }

        /// <summary>blend face 인덱스</summary>
        public int FaceIndex { get; set; }

        /// <summary>fillet 반지름 (mm) — raw 검출값</summary>
        public double RadiusMm { get; set; }

        /// <summary>snap 된 nominal R (mm) — RadiusSnapper 적용 후</summary>
        public double SnappedRadiusMm { get; set; }

        /// <summary>속한 fillet chain ID (예: "FC1"). 단독이면 null.</summary>
        public string ChainId { get; set; }

        /// <summary>인접한 두 face 인덱스 (이론적으로 2개; 코너 blend 는 3개일 수 있음)</summary>
        public List<int> AdjacentFaces { get; set; }

        /// <summary>cylindrical vs toroidal (corner blend)</summary>
        public string BlendType { get; set; }  // "edge" or "corner"

        /// <summary>볼록/오목</summary>
        public string Concavity { get; set; }  // "convex" or "concave"
    }
}
