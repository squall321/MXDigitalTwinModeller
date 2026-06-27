using System.Collections.Generic;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer
{
    /// <summary>
    /// 두 평행 평면 face 쌍이 만드는 "벽" — 두께가 두 평면 사이 거리.
    /// 예: 박스의 top-bottom 쌍 = 두께 = 거리. side-side 쌍 = 너비. front-back 쌍 = 길이.
    /// </summary>
    // @lat: [[reverse-engineer#WallDetection]]
    public class WallFeature
    {
        /// <summary>식별자 (예: "W1")</summary>
        public string Id { get; set; }

        /// <summary>쌍을 이루는 두 face 인덱스 (작은 인덱스 먼저)</summary>
        public int FaceA { get; set; }
        public int FaceB { get; set; }

        /// <summary>두 평면 사이 거리 (mm) — "두께"</summary>
        public double ThicknessMm { get; set; }

        /// <summary>
        /// 법선 방향 — 항상 OUTWARD (재료 밖, void 방향) 을 가리킨다.
        /// 외벽 (외부 박스 면) → 박스 바깥 방향.
        /// 내벽 (내부 cavity 면) → cavity 내부 (재료에서 멀어지는) 방향.
        /// </summary>
        public double[] Normal { get; set; }

        /// <summary>면 면적 평균 (mm²) — 큰 wall vs 작은 wall 구분용</summary>
        public double AreaMm2 { get; set; }

        /// <summary>
        /// 어떤 sign convention 으로 Normal 이 결정되었는지 디버그 라벨.
        /// 가능한 값:
        ///   - "A_outward_opposite_B"   : face A 의 outward 와 face B 의 outward 가 반대 (정상 outer/cavity wall)
        ///   - "A_outward_agree_B_centroid_far" : 둘 다 같은 방향 → bbox 중심에서 더 먼 쪽 채택
        ///   - "A_outward_agree_B_centroid_near": 둘 다 같은 방향 → 중심에 가까운 쪽 채택 (드물게 발생)
        ///   - "fallback_a_normal"      : 컨텍스트 부족, 단순히 face A 의 outward 사용
        /// </summary>
        public string ConsistencyCheck { get; set; }

        /// <summary>
        /// True if this wall is "inverted" — at least one of the paired faces
        /// had IsReversed=true (raw plane normal pointed INTO material before
        /// the FaceClassifier sign-flip). Inner-cavity walls of a hollow body
        /// carry this flag; outer-shell walls of a simple solid box do not.
        ///
        /// Distinct from ConsistencyCheck: ConsistencyCheck describes how the
        /// wall's Normal was chosen (sign-convention path), whereas IsInverted
        /// records whether the wall is geometrically inside-out relative to a
        /// "material-on-the-A-side" convention.
        /// </summary>
        public bool IsInverted { get; set; }
    }
}
