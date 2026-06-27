namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer
{
    /// <summary>
    /// 평면에서 솟은 외부 원기둥 (boss / pin / 페그).
    /// Cylinder face 의 outward normal 이 축에서 멀어지는 방향이면 boss.
    /// </summary>
    public class BossFeature
    {
        /// <summary>식별자 (예: "B1")</summary>
        public string Id { get; set; }

        /// <summary>cylindrical 외부 face 의 인덱스</summary>
        public int CylinderFaceIndex { get; set; }

        /// <summary>외경 (mm)</summary>
        public double DiameterMm { get; set; }

        /// <summary>높이 (mm) — base plane 에서 top plane 까지</summary>
        public double HeightMm { get; set; }

        /// <summary>축 방향 (단위 벡터)</summary>
        public double[] Axis { get; set; }

        /// <summary>축 base 위치 (mm)</summary>
        public double[] BasePositionMm { get; set; }

        /// <summary>
        /// Cone 기반 boss (taper pin / draft pillar) 의 half-angle (도).
        /// Cylinder boss 는 0. 부호 의미는 Cone.Frame.DirZ 방향과 일치한다.
        /// </summary>
        public double HalfAngleDeg { get; set; }

        /// <summary>
        /// 이 boss 의 outer face 가 cone 인지. true 이면 HalfAngleDeg 의미 있음.
        /// </summary>
        public bool IsConical { get; set; }
    }
}
