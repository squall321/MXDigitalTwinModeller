using System.Collections.Generic;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer
{
    /// <summary>
    /// 한 face 의 인접 정보 (adjacency graph node).
    /// In-memory only — JSON 출력엔 face_feature.adjacent_faces 에 인덱스만 들어감.
    /// </summary>
    public class FaceAdjacency
    {
        /// <summary>이 face 의 인덱스</summary>
        public int FaceIndex { get; set; }

        /// <summary>인접 face 인덱스 리스트</summary>
        public List<int> Neighbors { get; set; }

        /// <summary>
        /// 인접 face 별 edge 관계: key = 인접 face index, value = EdgeRelation
        /// </summary>
        public Dictionary<int, EdgeRelation> EdgeRelations { get; set; }

        public FaceAdjacency()
        {
            Neighbors = new List<int>();
            EdgeRelations = new Dictionary<int, EdgeRelation>();
        }
    }

    /// <summary>
    /// 두 face 사이의 edge 가 어떤 성격인지.
    /// Fillet 검출의 핵심: tangent 면 fillet, perpendicular 면 boss/hole entry.
    /// </summary>
    public class EdgeRelation
    {
        /// <summary>두 face 의 노멀이 edge 위에서 평행한가 (tangent / G1 continuity)</summary>
        public bool IsTangent { get; set; }

        /// <summary>두 face 의 노멀이 거의 수직인가 (90° 만남)</summary>
        public bool IsPerpendicular { get; set; }

        /// <summary>edge 의 길이 (mm)</summary>
        public double EdgeLengthMm { get; set; }

        /// <summary>두 normal 의 dot product (참고용). 1 = 같은 방향, -1 = 반대, 0 = 수직</summary>
        public double NormalDot { get; set; }
    }
}
