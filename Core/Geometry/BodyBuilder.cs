using System;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry
{
    /// <summary>
    /// 3D Body 생성을 위한 헬퍼 클래스
    /// </summary>
    public class BodyBuilder
    {
        /// <summary>
        /// 프로파일을 압출하여 Body 생성
        /// </summary>
        public static Body ExtrudeProfile(Profile profile, double depth)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            if (depth <= 0)
                throw new ArgumentException("Depth must be greater than 0", nameof(depth));

            return Body.ExtrudeProfile(profile, depth);
        }

        /// <summary>
        /// 직사각형 블록 생성
        /// </summary>
        public static Body CreateBlock(double length, double width, double height)
        {
            if (length <= 0 || width <= 0 || height <= 0)
                throw new ArgumentException("All dimensions must be greater than 0");

            Profile profile = new RectangleProfile(Plane.PlaneXY, length, width);
            return Body.ExtrudeProfile(profile, height);
        }

        /// <summary>
        /// 원기둥 생성
        /// </summary>
        public static Body CreateCylinder(double radius, double height)
        {
            if (radius <= 0 || height <= 0)
                throw new ArgumentException("Radius and height must be greater than 0");

            Profile profile = new CircleProfile(Plane.PlaneXY, radius);
            return Body.ExtrudeProfile(profile, height);
        }

        /// <summary>
        /// DesignBody 생성 헬퍼
        /// </summary>
        public static DesignBody CreateDesignBody(Part part, string name, Body body)
        {
            if (part == null)
                throw new ArgumentNullException(nameof(part));

            if (body == null)
                throw new ArgumentNullException(nameof(body));

            if (string.IsNullOrEmpty(name))
                name = "Body";

            return DesignBody.Create(part, name, body);
        }
    }
}
