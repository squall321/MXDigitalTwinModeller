using System.Collections.Generic;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer
{
    /// <summary>
    /// 면의 기하학적 분류.
    /// face.Shape.Geometry 의 런타임 타입에서 매핑.
    /// </summary>
    public enum SurfaceType
    {
        Unknown,
        Plane,
        Cylinder,
        Cone,
        Sphere,
        Torus,
        Nurbs,
        Other,
    }

    /// <summary>
    /// 단일 면의 추출된 특성.
    /// </summary>
    // @lat: [[reverse-engineer#FeatureExtraction]]
    public class FaceFeature
    {
        /// <summary>0-based 인덱스 (body 내 face 순서)</summary>
        public int FaceIndex { get; set; }

        /// <summary>면 기하 타입</summary>
        public SurfaceType Type { get; set; }

        /// <summary>면 면적 (m²)</summary>
        public double AreaM2 { get; set; }

        /// <summary>평면일 경우 법선 (X, Y, Z), 정규화됨. 다른 면은 (0,0,0)</summary>
        public double[] Normal { get; set; }

        /// <summary>평면의 원점 (X, Y, Z) m. 다른 면은 면 중심 근사.</summary>
        public double[] Origin { get; set; }

        /// <summary>원기둥/원뿔/구/토러스의 반지름 (m). 평면은 0.</summary>
        public double RadiusM { get; set; }

        /// <summary>face.Shape.IsReversed (방향 뒤집힘 여부)</summary>
        public bool IsReversed { get; set; }

        /// <summary>Cylinder face 의 의미적 역할 (Stage 1 보강). 비-cylinder 면 Unknown.</summary>
        public CylinderRole CylinderRole { get; set; }

        /// <summary>볼록/오목 분류 (Stage 1 보강)</summary>
        public Concavity Concavity { get; set; }

        /// <summary>Cylinder 의 축 방향 (단위 벡터). 비-cylinder 면 null.</summary>
        public double[] AxisDirection { get; set; }

        /// <summary>
        /// Cone face 의 half-angle (도). 양수=커지는 쪽이 DirZ 방향.
        /// Cone 이 아니면 0. API 에서 추출 실패 시에도 0 (warning 로깅).
        /// </summary>
        public double HalfAngleDeg { get; set; }
    }
}
