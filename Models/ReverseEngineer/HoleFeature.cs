namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer
{
    /// <summary>
    /// 검출된 hole. Through-hole / Blind-hole 모두 포함.
    /// </summary>
    public class HoleFeature
    {
        /// <summary>식별자 (예: "H1")</summary>
        public string Id { get; set; }

        /// <summary>cylindrical 내부 face 의 인덱스</summary>
        public int CylinderFaceIndex { get; set; }

        /// <summary>직경 (mm)</summary>
        public double DiameterMm { get; set; }

        /// <summary>깊이 (mm) — through 면 body Z 길이와 일치, blind 면 작음</summary>
        public double DepthMm { get; set; }

        /// <summary>true = through (관통), false = blind (막힌)</summary>
        public bool IsThrough { get; set; }

        /// <summary>축 방향 (단위 벡터)</summary>
        public double[] Axis { get; set; }

        /// <summary>축 위치 (한 점) — 보통 hole 입구의 중심 (mm)</summary>
        public double[] PositionMm { get; set; }

        /// <summary>
        /// Audit fix: 이 hole 이 counterbore 의 "넓은 머리 부분" (큰 직경 step) 인가.
        /// 동축 + 동선 hole 쌍에서 큰 D 쪽에 true.
        /// </summary>
        public bool IsCounterboreStep { get; set; }

        /// <summary>
        /// Audit fix: counterbore step 의 parent (작은 D = through 부분) hole id.
        /// IsCounterboreStep=true 일 때만 의미.
        /// </summary>
        public string ParentHoleId { get; set; }

        /// <summary>
        /// Audit fix: 이 hole 이 counterbore step 을 갖고 있는가 (작은 D 쪽에 true).
        /// </summary>
        public bool HasCounterbore { get; set; }

        /// <summary>
        /// Cone 기반 hole (countersink / chamfered hole) 의 half-angle (도).
        /// Cylinder hole 은 0. 양수면 cone 이 깊어질수록 vẫn 좁아짐 / 넓어짐 -
        /// 부호 의미는 Cone.Frame.DirZ 와 일치한다.
        /// </summary>
        public double HalfAngleDeg { get; set; }

        /// <summary>
        /// 이 hole 의 inner face 가 cone 인지. true 이면 HalfAngleDeg 의미 있음.
        /// </summary>
        public bool IsConical { get; set; }
    }
}
