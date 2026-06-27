using System;
using System.Collections.Generic;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer
{
    /// <summary>
    /// CylinderRole.ThroughHole / BlindHole 로 분류된 face 를 HoleFeature 로 변환.
    /// Through vs Blind 판정: cylinder face 가 두 평면 사이를 관통하는가.
    /// </summary>
    public class HoleDetector
    {
        /// <summary>
        /// Audit fix (spec 39 thread_cosmetic):
        ///   ring groove (얇은 환형 컷) 의 inner cylinder 가 ThroughHole 로 오분류되어
        ///   spurious hole 로 떠올랐다. cylinder 의 along-axis 높이가 이 값보다 작으면
        ///   hole 이 아니라 groove/slit 으로 보고 스킵한다.
        ///
        ///   계산: h = area_m2 / (2π · radius_m). cylinder 의 단순 lateral 면적 공식.
        ///
        ///   Scale-invariant refactor with legacy floor:
        ///     effectiveMin = max(MinHoleHeightFloorMm, MinHoleHeightRatio × MaxBboxDim).
        ///   The 0.8mm floor preserves legacy behavior at phone/desktop scale
        ///   (where 0.005 × 60mm = 0.3mm would otherwise let h=0.5mm thread grooves
        ///   leak through as holes — spec 39_box_thread_cosmetic regression).
        ///   기본 0.005 × MaxBboxDim, floor 0.8mm:
        ///     - small phone-frame (60mm)   → max(0.8, 0.30) = 0.8mm (legacy)
        ///     - desktop phone-frame (150mm)→ max(0.8, 0.75) = 0.8mm (legacy ~0.8mm)
        ///     - 1m assembly (1000mm)       → max(0.8, 5.0)  = 5.0mm (scales up)
        ///     - MEMS 10mm 부품              → max(0.8, 0.05) = 0.8mm (floor wins; AND-gated
        ///       by MinHeightToDiameterRatio so micro-features pass via h/D check).
        /// </summary>
        public double MinHoleHeightFloorMm { get; set; } = 0.8;
        public double MinHoleHeightRatio { get; set; } = 0.005;

        /// <summary>
        /// Audit fix (spec 39): height/diameter 비율 필터. 정상 hole 은 통상 h/D ≥ 0.1
        /// (D=10 hole 이면 최소 1mm 깊이) 인 반면, ring groove 는 h/D ≈ 0.05 정도다.
        /// 절대 높이 필터와 함께 AND 조건으로 사용 (둘 다 통과해야 hole 로 인정).
        /// 이 값 자체는 ratio 이므로 scale-invariant.
        /// </summary>
        public double MinHeightToDiameterRatio { get; set; } = 0.08;

        /// <summary>
        /// Counterbore detection: 두 hole 의 centerline 의 cross-axis 거리가 이 값 이하이면
        /// 같은 축. machining-precision 절대값 (0.05mm) 가 baseline; bbox 가 매우 크면
        /// BboxColinearRatio × MaxBboxDim 으로 자동 확장.
        ///
        /// WARNING: 0.05mm floor 는 일반 CAD machining precision (50µm) 의 절대 한계로
        /// 유지. 이는 manufacturing absolute 이지 feature threshold 가 아니다.
        /// </summary>
        public double AxesColinearAbsoluteMm { get; set; } = 0.05;
        public double AxesColinearBboxRatio { get; set; } = 5e-5;

        public List<HoleFeature> Detect(
            List<FaceFeature> faces,
            DesignBody body,
            Dictionary<int, FaceAdjacency> adjGraph,
            ModelScale.Scale scale = null)
        {
            // Scale 이 주어지지 않으면 desktop CAD 기본 (100mm bbox) 으로 가정.
            // 모든 ratio 의 default 가 이 기준에서 legacy 절대값을 재현한다.
            if (scale == null)
            {
                scale = ModelScale.ComputeFromBboxSize(null);
            }
            double effectiveMinHoleHeightMm = Math.Max(
                MinHoleHeightFloorMm,
                MinHoleHeightRatio * scale.MaxBboxDimMm);
            double effectiveColinearTolMm = Math.Max(AxesColinearAbsoluteMm,
                AxesColinearBboxRatio * scale.MaxBboxDimMm);
            var holes = new List<HoleFeature>();
            int holeId = 1;

            foreach (var f in faces)
            {
                if (f.CylinderRole != CylinderRole.ThroughHole &&
                    f.CylinderRole != CylinderRole.BlindHole &&
                    f.CylinderRole != CylinderRole.ConicalHole) continue;

                bool isConical = f.CylinderRole == CylinderRole.ConicalHole;

                // Audit fix (spec 39 thread_cosmetic): groove 필터.
                //   ring groove 의 inner cylinder 는 두께가 매우 얇아 (h ≈ 0.5mm)
                //   hole 이 아닌 cosmetic 라인이다. cylinder lateral area 에서
                //   along-axis 높이를 역산해 h < effectiveMinHoleHeightMm AND
                //   h/D < MinHeightToDiameterRatio 이면 hole 으로 등록하지 않는다.
                //   Conical hole 은 면적-반지름 관계가 다르므로 이 필터 적용 안 함.
                //   RadiusM > 1e-9: WARNING — kernel epsilon (nm-scale floor in meters,
                //   numerical-stability guard against division by zero). Not a feature
                //   threshold; do not refactor to ratio.
                if (!isConical && f.RadiusM > 1e-9 && f.AreaM2 > 0)
                {
                    double approxHeightMm = (f.AreaM2 / (2.0 * Math.PI * f.RadiusM)) * 1000.0;
                    double diameterMm = f.RadiusM * 2.0 * 1000.0;
                    double hToD = diameterMm > 1e-9 ? approxHeightMm / diameterMm : 0.0;
                    if (approxHeightMm < effectiveMinHoleHeightMm && hToD < MinHeightToDiameterRatio)
                    {
                        continue;
                    }
                }

                var hole = new HoleFeature
                {
                    Id = "H" + holeId,
                    CylinderFaceIndex = f.FaceIndex,
                    DiameterMm = f.RadiusM * 2.0 * 1000.0,
                    Axis = f.AxisDirection ?? new[] { 0.0, 0.0, 1.0 },
                    PositionMm = new[] { f.Origin[0] * 1000.0, f.Origin[1] * 1000.0, f.Origin[2] * 1000.0 },
                    IsThrough = true,  // 기본값; 아래에서 보정
                    IsConical = isConical,
                    HalfAngleDeg = isConical ? f.HalfAngleDeg : 0.0,
                };

                // Through vs Blind 판정 + 깊이 추정.
                // Audit fix: 기존 코드는 face.Origin Euclidean 거리를 사용해서
                //   평면이 옆으로 떨어져 있는 경우 (counterbore step / offset)에
                //   depth 가 부풀려졌다. cylinder 축 방향으로 projection 하면
                //   진정한 along-axis 깊이가 나오고, opposite-sign projection 으로
                //   "양쪽에 평면이 있는 through" 도 정확히 판정할 수 있다.
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
                        planeCount++;
                        var p = faces[neighborIdx].Origin;
                        // (p - f.Origin) · axis = 축 위 좌표
                        double proj =
                            (p[0] - f.Origin[0]) * axis[0] +
                            (p[1] - f.Origin[1]) * axis[1] +
                            (p[2] - f.Origin[2]) * axis[2];
                        if (proj < minProj) minProj = proj;
                        if (proj > maxProj) maxProj = proj;
                    }
                    if (planeCount >= 1)
                    {
                        double depthM = planeCount >= 2 ? (maxProj - minProj) : 0.0;
                        hole.DepthMm = depthM * 1000.0;
                    }
                    // through: 두 평면이 cylinder 의 양 끝을 막고 있으면 관통.
                    // 부호-bracket 체크 (minProj<0<maxProj) 는 face.Origin 이 두 평면 사이에 있다는
                    // 가정에 의존하는데, SpaceClaim 의 face.Origin 은 face 시작점 (midpoint 가 아님)
                    // 으로 보고되기도 한다. AddHole 후 cylinder origin 이 box 밖 (z=-1mm) 인 케이스
                    // 에서 두 plane proj 가 모두 +면이라 false-negative 가 발생했다.
                    // 단순히 \"2개 이상의 평면 인접 + along-axis 깊이가 의미있게 큼\" 이면 through.
                    // 1e-6: WARNING — kernel epsilon (µm-scale floor in meters, mathematical-zero
                    // guard against degenerate projections). Not a feature threshold.
                    hole.IsThrough = planeCount >= 2 && (maxProj - minProj) > 1e-6;
                }

                holes.Add(hole);
                holeId++;
            }

            // Audit fix: counterbore 검출.
            //   같은 축 + 같은 centerline 위의 hole 쌍이 있으면
            //   큰 D 쪽을 step (머리), 작은 D 쪽을 through 로 표시.
            //   기존엔 두 개의 별도 HoleFeature 로 떠서 BOM 이 흐트러졌다.
            for (int i = 0; i < holes.Count; i++)
            {
                for (int j = i + 1; j < holes.Count; j++)
                {
                    if (!AxesColinear(holes[i], holes[j], 1.0, effectiveColinearTolMm)) continue;
                    HoleFeature big, small;
                    if (holes[i].DiameterMm > holes[j].DiameterMm)
                    {
                        big = holes[i]; small = holes[j];
                    }
                    else
                    {
                        big = holes[j]; small = holes[i];
                    }
                    big.IsCounterboreStep = true;
                    big.ParentHoleId = small.Id;
                    small.HasCounterbore = true;
                }
            }

            return holes;
        }

        /// <summary>
        /// 두 hole 의 축이 평행하고 (cos &gt; cos(angTolDeg)) 그리고 centerline 의
        /// cross-axis 거리가 distTolMm 이하인지.
        /// </summary>
        private static bool AxesColinear(HoleFeature a, HoleFeature b, double angTolDeg, double distTolMm)
        {
            if (a.Axis == null || b.Axis == null) return false;
            // 단위 벡터 가정. dot 의 절대값이 cos(angTol) 이상이면 평행/반평행.
            double dot = a.Axis[0] * b.Axis[0] + a.Axis[1] * b.Axis[1] + a.Axis[2] * b.Axis[2];
            double cosTol = Math.Cos(angTolDeg * Math.PI / 180.0);
            if (Math.Abs(dot) < cosTol) return false;

            // cross-axis 거리: a.PositionMm 에서 b.PositionMm 로 가는 벡터를
            //   a.Axis 에 수직인 성분의 magnitude.
            double dx = b.PositionMm[0] - a.PositionMm[0];
            double dy = b.PositionMm[1] - a.PositionMm[1];
            double dz = b.PositionMm[2] - a.PositionMm[2];
            double along = dx * a.Axis[0] + dy * a.Axis[1] + dz * a.Axis[2];
            double perpX = dx - along * a.Axis[0];
            double perpY = dy - along * a.Axis[1];
            double perpZ = dz - along * a.Axis[2];
            double perpDist = Math.Sqrt(perpX * perpX + perpY * perpY + perpZ * perpZ);
            return perpDist <= distTolMm;
        }
    }
}
