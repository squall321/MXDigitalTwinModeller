using System;

namespace MXDigitalTwinModeller.Core.Geometry
{
    /// <summary>
    /// 기하학 유틸리티 (SpaceClaim API에 독립적)
    /// </summary>
    public static class GeometryUtils
    {
        private const double MmPerMeter = 1000.0;

        /// <summary>
        /// 미터 → 밀리미터 변환
        /// </summary>
        public static double MetersToMm(double meters)
        {
            return meters * MmPerMeter;
        }

        /// <summary>
        /// 밀리미터 → 미터 변환
        /// </summary>
        public static double MmToMeters(double mm)
        {
            return mm / MmPerMeter;
        }

        /// <summary>
        /// 값을 범위로 제한
        /// </summary>
        public static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>
        /// 두 값이 허용 오차 내에서 같은지 확인
        /// </summary>
        public static bool NearlyEqual(double a, double b, double tolerance)
        {
            return Math.Abs(a - b) <= tolerance;
        }
    }
}
