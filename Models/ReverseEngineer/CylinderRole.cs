namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer
{
    /// <summary>
    /// Cylinder face 의 의미적 분류.
    /// Stage 1 보강: 모든 cylinder 를 fillet 으로 보던 것을 정밀하게 구분.
    /// </summary>
    public enum CylinderRole
    {
        /// <summary>분류 불가</summary>
        Unknown,

        /// <summary>Edge fillet — 두 평면 사이의 cylindrical blend, 두 면에 모두 tangent</summary>
        EdgeFillet,

        /// <summary>Corner blend — 3개 이상 면이 만나는 코너 (보통 toroidal 이지만 cylinder 일 수도)</summary>
        CornerBlend,

        /// <summary>Boss — 평면에서 솟은 외부 원기둥 (외경)</summary>
        Boss,

        /// <summary>Through hole 의 내부 원기둥 (관통)</summary>
        ThroughHole,

        /// <summary>Blind hole 의 내부 원기둥 (막힌 구멍)</summary>
        BlindHole,

        /// <summary>기능적 cylinder — 샤프트, 핀 등 (fillet/hole/boss 아님)</summary>
        FunctionalCylinder,

        /// <summary>
        /// 원뿔형 구멍 — countersink, chamfered hole 의 taper 내부.
        /// IsReversed=true (오목) + 인접 평면 perpendicular → 이 role.
        /// HoleDetector 가 HoleFeature 로 변환.
        /// </summary>
        ConicalHole,

        /// <summary>
        /// 원뿔형 외부 boss — taper 핀, draft 가 강한 pillar.
        /// IsReversed=false (볼록) + 인접 평면 perpendicular → 이 role.
        /// BossDetector 가 BossFeature 로 변환.
        /// </summary>
        ConicalBoss,
    }

    /// <summary>
    /// 면의 볼록/오목 분류.
    /// </summary>
    public enum Concavity
    {
        Unknown,
        /// <summary>볼록 (외부에서 보이는 면, 살 방향)</summary>
        Convex,
        /// <summary>오목 (내부 방향, hole/pocket 안쪽)</summary>
        Concave,
        /// <summary>평평 (Plane)</summary>
        Flat,
    }
}
