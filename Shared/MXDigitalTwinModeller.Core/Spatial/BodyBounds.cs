using System;

namespace MXDigitalTwinModeller.Core.Spatial
{
    /// <summary>
    /// 바디의 AABB (Axis-Aligned Bounding Box) 정보
    /// SpaceClaim API에 독립적인 순수 데이터 구조
    /// </summary>
    public struct BodyBounds
    {
        public int Index;
        public double MinX, MinY, MinZ;
        public double MaxX, MaxY, MaxZ;
        public double ExtentX, ExtentY, ExtentZ;
        public double MaxExtent;

        public BodyBounds(int index, double minX, double minY, double minZ,
                          double maxX, double maxY, double maxZ)
        {
            Index = index;
            MinX = minX;
            MinY = minY;
            MinZ = minZ;
            MaxX = maxX;
            MaxY = maxY;
            MaxZ = maxZ;
            ExtentX = maxX - minX;
            ExtentY = maxY - minY;
            ExtentZ = maxZ - minZ;
            MaxExtent = Math.Max(ExtentX, Math.Max(ExtentY, ExtentZ));
        }
    }
}
