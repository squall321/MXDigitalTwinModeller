using System;

#if V251
using SpaceClaim.Api.V251.Geometry;
#elif V252
using SpaceClaim.Api.V252.Geometry;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry
{
    /// <summary>
    /// 기하학 관련 유틸리티 메서드
    /// </summary>
    public static class GeometryUtils
    {
        /// <summary>
        /// 각도를 라디안으로 변환
        /// </summary>
        public static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        /// <summary>
        /// 라디안을 각도로 변환
        /// </summary>
        public static double RadiansToDegrees(double radians)
        {
            return radians * 180.0 / Math.PI;
        }

        /// <summary>
        /// mm를 m로 변환 (SpaceClaim은 m 단위 사용)
        /// </summary>
        public static double MmToMeters(double mm)
        {
            return mm / 1000.0;
        }

        /// <summary>
        /// m를 mm로 변환
        /// </summary>
        public static double MetersToMm(double meters)
        {
            return meters * 1000.0;
        }

        /// <summary>
        /// 두 점 사이의 거리 계산
        /// </summary>
        public static double Distance(Point p1, Point p2)
        {
            return (p1 - p2).Magnitude;
        }

        /// <summary>
        /// 대칭 프로파일 생성을 위한 미러링
        /// </summary>
        public static Point MirrorPointX(Point point)
        {
            return Point.Create(-point.X, point.Y, point.Z);
        }

        /// <summary>
        /// Y축 기준 미러링
        /// </summary>
        public static Point MirrorPointY(Point point)
        {
            return Point.Create(point.X, -point.Y, point.Z);
        }
    }
}
