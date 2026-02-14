using System;
using System.Collections.Generic;

#if V251
using SpaceClaim.Api.V251.Geometry;
#elif V252
using SpaceClaim.Api.V252.Geometry;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ConformalMesh
{
    /// <summary>
    /// Body-level AABB 기반 Uniform Grid Spatial Index.
    /// 1600+ 바디에서 O(n²)→O(n·k) 이웃 검색 가속.
    /// </summary>
    public static class SpatialIndex
    {
        /// <summary>
        /// 바디의 축 정렬 바운딩 박스 (meters, SpaceClaim 내부 단위)
        /// </summary>
        public struct BodyBounds
        {
            public int Index;
            public double MinX, MaxX;
            public double MinY, MaxY;
            public double MinZ, MaxZ;

            public double ExtentX { get { return MaxX - MinX; } }
            public double ExtentY { get { return MaxY - MinY; } }
            public double ExtentZ { get { return MaxZ - MinZ; } }
            public double MaxExtent
            {
                get
                {
                    double ex = ExtentX, ey = ExtentY, ez = ExtentZ;
                    return ex > ey ? (ex > ez ? ex : ez) : (ey > ez ? ey : ez);
                }
            }
        }

        /// <summary>
        /// DesignBody의 AABB 계산.
        /// padding: 허용 거리 (meters)를 각 방향에 추가.
        /// </summary>
        public static BodyBounds ComputeBounds(DesignBody body, int index, double paddingM)
        {
            var bb = body.Shape.GetBoundingBox(Matrix.Identity);
            var minC = bb.MinCorner;
            var maxC = bb.MaxCorner;

            return new BodyBounds
            {
                Index = index,
                MinX = minC.X - paddingM,
                MaxX = maxC.X + paddingM,
                MinY = minC.Y - paddingM,
                MaxY = maxC.Y + paddingM,
                MinZ = minC.Z - paddingM,
                MaxZ = maxC.Z + paddingM
            };
        }

        /// <summary>
        /// 바운딩 박스 목록에서 최적 셀 크기를 계산 (중앙값 바디 크기 × 2).
        /// </summary>
        public static double ComputeCellSize(List<BodyBounds> bounds)
        {
            if (bounds.Count == 0) return 1.0;

            var extents = new List<double>(bounds.Count);
            for (int i = 0; i < bounds.Count; i++)
            {
                extents.Add(bounds[i].MaxExtent);
            }
            extents.Sort();

            double median = extents[extents.Count / 2];
            double cellSize = median * 2.0;

            // 최소 셀 크기 보장
            if (cellSize < 1e-6) cellSize = 1e-3;

            return cellSize;
        }

        /// <summary>
        /// Uniform Grid를 구축하고 AABB가 겹치는 이웃 바디 쌍을 반환.
        /// 각 쌍은 (indexA, indexB), indexA &lt; indexB 형태로 1회만 반환.
        /// </summary>
        public static List<KeyValuePair<int, int>> GetNeighborPairs(
            List<BodyBounds> bounds, double cellSize)
        {
            // 셀 키 → 바디 인덱스 목록
            var cells = new Dictionary<long, List<int>>();

            for (int i = 0; i < bounds.Count; i++)
            {
                var b = bounds[i];
                int ixMin = Floor(b.MinX, cellSize);
                int ixMax = Floor(b.MaxX, cellSize);
                int iyMin = Floor(b.MinY, cellSize);
                int iyMax = Floor(b.MaxY, cellSize);
                int izMin = Floor(b.MinZ, cellSize);
                int izMax = Floor(b.MaxZ, cellSize);

                for (int ix = ixMin; ix <= ixMax; ix++)
                {
                    for (int iy = iyMin; iy <= iyMax; iy++)
                    {
                        for (int iz = izMin; iz <= izMax; iz++)
                        {
                            long key = CellKey(ix, iy, iz);
                            List<int> list;
                            if (!cells.TryGetValue(key, out list))
                            {
                                list = new List<int>();
                                cells[key] = list;
                            }
                            list.Add(i);
                        }
                    }
                }
            }

            // 이웃 쌍 수집 (중복 제거)
            var pairSet = new HashSet<long>();
            var result = new List<KeyValuePair<int, int>>();

            foreach (var cell in cells.Values)
            {
                for (int a = 0; a < cell.Count; a++)
                {
                    for (int b = a + 1; b < cell.Count; b++)
                    {
                        int idxA = cell[a];
                        int idxB = cell[b];

                        // 정렬된 쌍 키
                        int lo = idxA < idxB ? idxA : idxB;
                        int hi = idxA < idxB ? idxB : idxA;
                        long pairKey = ((long)lo << 32) | (uint)hi;

                        if (pairSet.Add(pairKey))
                        {
                            // AABB 실제 겹침 확인
                            if (AABBOverlap(bounds[lo], bounds[hi]))
                            {
                                result.Add(new KeyValuePair<int, int>(lo, hi));
                            }
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 두 AABB가 겹치는지 확인
        /// </summary>
        public static bool AABBOverlap(BodyBounds a, BodyBounds b)
        {
            if (a.MaxX < b.MinX || b.MaxX < a.MinX) return false;
            if (a.MaxY < b.MinY || b.MaxY < a.MinY) return false;
            if (a.MaxZ < b.MinZ || b.MaxZ < a.MinZ) return false;
            return true;
        }

        private static int Floor(double value, double cellSize)
        {
            return (int)Math.Floor(value / cellSize);
        }

        private static long CellKey(int ix, int iy, int iz)
        {
            // 21비트씩 사용 (±1M 셀 범위)
            unchecked
            {
                long x = (long)(ix + 1048576) & 0x1FFFFF;
                long y = (long)(iy + 1048576) & 0x1FFFFF;
                long z = (long)(iz + 1048576) & 0x1FFFFF;
                return (x << 42) | (y << 21) | z;
            }
        }
    }
}
