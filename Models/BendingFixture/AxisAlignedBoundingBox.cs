namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.BendingFixture
{
    /// <summary>
    /// Axis-Aligned Bounding Box (AABB)
    /// 모든 값은 meters (SpaceClaim 내부 단위)
    /// </summary>
    public class AxisAlignedBoundingBox
    {
        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinY { get; set; }
        public double MaxY { get; set; }
        public double MinZ { get; set; }
        public double MaxZ { get; set; }

        public double ExtentX => MaxX - MinX;
        public double ExtentY => MaxY - MinY;
        public double ExtentZ => MaxZ - MinZ;

        public double CenterX => (MinX + MaxX) / 2.0;
        public double CenterY => (MinY + MaxY) / 2.0;
        public double CenterZ => (MinZ + MaxZ) / 2.0;

        /// <summary>
        /// 지정된 축의 크기 반환
        /// </summary>
        public double GetExtent(AxisDirection axis)
        {
            switch (axis)
            {
                case AxisDirection.X: return ExtentX;
                case AxisDirection.Y: return ExtentY;
                case AxisDirection.Z: return ExtentZ;
                default: return 0;
            }
        }

        /// <summary>
        /// 지정된 축의 최소값 반환
        /// </summary>
        public double GetMin(AxisDirection axis)
        {
            switch (axis)
            {
                case AxisDirection.X: return MinX;
                case AxisDirection.Y: return MinY;
                case AxisDirection.Z: return MinZ;
                default: return 0;
            }
        }

        /// <summary>
        /// 지정된 축의 최대값 반환
        /// </summary>
        public double GetMax(AxisDirection axis)
        {
            switch (axis)
            {
                case AxisDirection.X: return MaxX;
                case AxisDirection.Y: return MaxY;
                case AxisDirection.Z: return MaxZ;
                default: return 0;
            }
        }

        /// <summary>
        /// 지정된 축의 중심값 반환
        /// </summary>
        public double GetCenter(AxisDirection axis)
        {
            switch (axis)
            {
                case AxisDirection.X: return CenterX;
                case AxisDirection.Y: return CenterY;
                case AxisDirection.Z: return CenterZ;
                default: return 0;
            }
        }
    }
}
