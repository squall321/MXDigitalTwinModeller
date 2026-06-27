using System;
using System.Collections.Generic;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;

#if V251
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer
{
    /// <summary>
    /// CylinderRole.Boss 로 분류된 face → BossFeature 변환.
    /// </summary>
    public class BossDetector
    {
        public List<BossFeature> Detect(
            List<FaceFeature> faces,
            DesignBody body,
            Dictionary<int, FaceAdjacency> adjGraph)
        {
            var bosses = new List<BossFeature>();
            int bossId = 1;

            foreach (var f in faces)
            {
                if (f.CylinderRole != CylinderRole.Boss &&
                    f.CylinderRole != CylinderRole.ConicalBoss) continue;

                bool isConical = f.CylinderRole == CylinderRole.ConicalBoss;

                var boss = new BossFeature
                {
                    Id = "B" + bossId,
                    CylinderFaceIndex = f.FaceIndex,
                    DiameterMm = f.RadiusM * 2.0 * 1000.0,
                    Axis = f.AxisDirection ?? new[] { 0.0, 0.0, 1.0 },
                    BasePositionMm = new[] { f.Origin[0] * 1000.0, f.Origin[1] * 1000.0, f.Origin[2] * 1000.0 },
                    IsConical = isConical,
                    HalfAngleDeg = isConical ? f.HalfAngleDeg : 0.0,
                };

                // 높이 추정: 인접 평면들을 cylinder 축에 projection 한 값의 span.
                // Audit fix: Euclidean 거리는 인접 평면이 옆으로 떨어진 경우 (chamfered base
                //   등) 진짜 along-axis 높이보다 부풀려졌다. axis projection 이 정답이며
                //   sign 까지 보면 위/아래 어느 쪽인지도 알 수 있다 (counterbore step 검출).
                if (adjGraph.TryGetValue(f.FaceIndex, out var adj))
                {
                    var axis = f.AxisDirection ?? new[] { 0.0, 0.0, 1.0 };
                    int planeCount = 0;
                    double minProj = double.PositiveInfinity;
                    double maxProj = double.NegativeInfinity;
                    foreach (var neighborIdx in adj.Neighbors)
                    {
                        if (neighborIdx >= faces.Count) continue;
                        if (faces[neighborIdx].Type != SurfaceType.Plane) continue;
                        var p = faces[neighborIdx].Origin;
                        double proj =
                            (p[0] - f.Origin[0]) * axis[0] +
                            (p[1] - f.Origin[1]) * axis[1] +
                            (p[2] - f.Origin[2]) * axis[2];
                        if (proj < minProj) minProj = proj;
                        if (proj > maxProj) maxProj = proj;
                        planeCount++;
                    }
                    if (planeCount >= 2)
                    {
                        boss.HeightMm = (maxProj - minProj) * 1000.0;
                    }
                }

                bosses.Add(boss);
                bossId++;
            }

            return bosses;
        }
    }
}
