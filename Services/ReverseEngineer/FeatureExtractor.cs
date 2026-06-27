using System;
using System.Collections.Generic;
using System.Linq;
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
    /// DesignBody → FeatureGraph 추출기 v2 (Stage 1 보강).
    /// Pipeline:
    ///   1) Face 분류 (RTTI)
    ///   2) Adjacency graph 빌드
    ///   3) Cylinder role 분류 (Hole / Fillet / Boss / Functional)
    ///   4) Wall (평행 평면 쌍) 검출
    ///   5) Fillet feature 생성 + R snapping
    ///   6) Hole / Boss / Slit feature 생성
    ///   7) Fillet chain 그룹화
    /// </summary>
    // @lat: [[reverse-engineer#FeatureExtractor]]
    public class FeatureExtractor
    {
        private const double ParallelDotThreshold = 0.999;
        private const double WallAreaRatioThreshold = 0.5;

        /// <summary>
        /// Scale-invariant wall thickness floor. Effective floor =
        /// MinWallThicknessRatio × MaxBboxDim.
        ///   - desktop CAD (100mm bbox) → 0.001mm (legacy 1µm floor).
        ///   - 1m assembly (1000mm)    → 0.01mm.
        ///   - MEMS 1mm part           → 0.00001mm (still distinguishes touching faces).
        /// </summary>
        private const double MinWallThicknessRatio = 1e-5;

        /// <summary>
        /// Scale-invariant bbox boundary tolerance with legacy floor.
        ///   effectiveTol = max(BoundaryTolFloorMm, BoundaryTolRatio × MaxBboxDim).
        /// The 0.5mm floor preserves legacy behavior on small bodies (e.g. a
        /// 60mm phone-frame would otherwise get 0.3mm and miss faces resting
        /// on the bbox plane with kernel-precision offsets).
        ///   - phone-frame (60mm)       → max(0.5, 0.30) = 0.5mm (legacy)
        ///   - desktop CAD (100mm)      → max(0.5, 0.50) = 0.5mm (legacy)
        ///   - 1m assembly (1000mm)     → max(0.5, 5.0)  = 5.0mm (scales up)
        ///   - MEMS 1mm part            → max(0.5, 0.005)= 0.5mm (floor wins)
        /// </summary>
        private const double BoundaryTolRatio = 5e-3;
        private const double BoundaryTolFloorMm = 0.5;

        /// <summary>Scale (per-body characteristic length). Set at Extract() entry.</summary>
        private ModelScale.Scale _scale;

        public FeatureGraph Extract(DesignBody designBody)
        {
            if (designBody == null)
                throw new ArgumentNullException(nameof(designBody));

            var graph = new FeatureGraph
            {
                BodyName = designBody.Name ?? "(unnamed)",
            };

            // 1. AABB
            var bbox = designBody.Shape.GetBoundingBox(Matrix.Identity);
            graph.BboxMinMm = new[] { bbox.MinCorner.X * 1000.0, bbox.MinCorner.Y * 1000.0, bbox.MinCorner.Z * 1000.0 };
            graph.BboxMaxMm = new[] { bbox.MaxCorner.X * 1000.0, bbox.MaxCorner.Y * 1000.0, bbox.MaxCorner.Z * 1000.0 };
            graph.BboxSizeMm = new[]
            {
                graph.BboxMaxMm[0] - graph.BboxMinMm[0],
                graph.BboxMaxMm[1] - graph.BboxMinMm[1],
                graph.BboxMaxMm[2] - graph.BboxMinMm[2],
            };

            // 1b. Model-wide scale (computed ONCE, threaded through every detector).
            _scale = ModelScale.Compute(graph);

            // 2. Face 분류
            int idx = 0;
            var designFaces = designBody.Faces.ToList();
            foreach (var df in designFaces)
            {
                string otherSubtype;
                var f = FaceClassifier.Classify(df, idx, out otherSubtype);
                // Axis direction 채우기 (Cylinder 인 경우)
                if (f.Type == SurfaceType.Cylinder)
                {
                    if (df.Shape.Geometry is Cylinder cyl)
                    {
                        var ax = cyl.Frame.DirZ.UnitVector;
                        f.AxisDirection = new[] { ax.X, ax.Y, ax.Z };
                    }
                }
                graph.Faces.Add(f);

                string key = FaceClassifier.ToTypeKey(f.Type);
                if (!graph.FaceTypeCounts.ContainsKey(key)) graph.FaceTypeCounts[key] = 0;
                graph.FaceTypeCounts[key]++;

                // NURBS/free-form face 인덱스 수집. Hole/Boss/Fillet detector 에는
                // 안 들어가지만 (analytic type 만 처리), JSON 에 flag 형태로 노출하여
                // downstream (CAD RE, 사용자) 가 외장/미관 surface 분포를 파악하도록.
                if (f.Type == SurfaceType.Nurbs)
                {
                    graph.NurbsFaceIndices.Add(f.FaceIndex);
                }

                // Diagnostic: when classify returns Other, aggregate the actual
                // runtime type name so we know which subtypes the SDK is emitting.
                if (otherSubtype != null)
                {
                    if (!graph.OtherSubtypeHistogram.ContainsKey(otherSubtype))
                        graph.OtherSubtypeHistogram[otherSubtype] = 0;
                    graph.OtherSubtypeHistogram[otherSubtype]++;
                }
                idx++;
            }

            // 3. Adjacency graph 빌드
            var adjBuilder = new AdjacencyBuilder();
            var adjGraph = adjBuilder.Build(designBody);
            // Cycle 32: expose on graph so downstream (ModificationService) can introspect.
            graph.Adjacency = adjGraph;

            // 4. Cylinder role 분류
            CylinderRoleClassifier.ClassifyAll(graph.Faces, designBody, adjGraph);

            // 5. Wall 검출 (기존 알고리즘 유지)
            DetectWalls(graph);

            // 6. Fillet feature 생성 (cylinder/torus 중 role = EdgeFillet / CornerBlend)
            var snapper = new RadiusSnapper();
            BuildFilletFeatures(graph, adjGraph, snapper);

            // 7. Hole / Boss / Slit
            var holeDetector = new HoleDetector();
            graph.Holes.AddRange(holeDetector.Detect(graph.Faces, designBody, adjGraph, _scale));

            var bossDetector = new BossDetector();
            graph.Bosses.AddRange(bossDetector.Detect(graph.Faces, designBody, adjGraph));

            var slitDetector = new SlitDetector();
            graph.Slits.AddRange(slitDetector.Detect(graph.Walls, graph.Faces, graph.BboxSizeMm, _scale));

            // 8. Fillet chain 그룹화
            var chainGrouper = new FilletChainGrouper();
            graph.FilletChains.AddRange(chainGrouper.Group(graph.Fillets, adjGraph, null));

            // 9. Stage 3 — Pattern detection (Hole/Boss patterns + Mirror symmetry)
            graph.HolePatterns.AddRange(PatternDetector.DetectHolePatterns(graph.Holes, _scale));
            graph.BossPatterns.AddRange(PatternDetector.DetectBossPatterns(graph.Bosses, _scale));
            graph.Symmetries.AddRange(PatternDetector.DetectSymmetry(graph, _scale));

            // 10. Stage 3+ — Composite features (mirrored pair pattern + symmetry).
            //    Must run after patterns AND symmetries are populated.
            graph.Composites.AddRange(PatternDetector.DetectComposites(graph, _scale));

            return graph;
        }

        // ---- Wall pair detection ----
        //
        // Wall.Normal 은 항상 OUTWARD (재료 바깥, void 방향) 을 가리켜야 한다.
        //
        // 핵심 원칙:
        //   - 한 face 의 "outward" normal = raw plane normal IF IsReversed=false ELSE -raw
        //     ※ FaceClassifier 가 이미 이 sign-flip 을 적용해서 f.Normal 에 저장함.
        //   - face A 의 outward 와 face B 의 outward 는 통상 반대 방향이어야 한다
        //     (둘 다 wall 내부 재료에서 바깥쪽을 가리키므로 서로 반대).
        //   - face A 의 outward 를 wall.Normal 로 사용.
        //   - 만약 둘이 같은 방향이라면 (sign convention 깨짐): bbox 중심에서
        //     더 먼 쪽이 진정한 outward → 그쪽을 채택.
        //
        // 외벽 (예: 박스 6면) → 박스 바깥쪽.
        // 내벽 (예: cavity 양쪽 면) → cavity 안쪽 (재료에서 멀어지는 쪽).
        // 두 경우 모두 "재료 바깥" semantic 으로 일관됨.
        private void DetectWalls(FeatureGraph graph)
        {
            var planes = graph.Faces.Where(f => f.Type == SurfaceType.Plane).ToList();
            var paired = new HashSet<int>();
            int wallId = 1;

            // bbox 중심 (m 단위) — agree case 의 tiebreaker 로 사용
            double cx = (graph.BboxMinMm[0] + graph.BboxMaxMm[0]) * 0.5 / 1000.0;
            double cy = (graph.BboxMinMm[1] + graph.BboxMaxMm[1]) * 0.5 / 1000.0;
            double cz = (graph.BboxMinMm[2] + graph.BboxMaxMm[2]) * 0.5 / 1000.0;

            // bbox bounds in meters (FaceFeature.Origin is in meters).
            // Used to decide whether a face's origin lies on the bbox boundary
            // (outer-shell face) or strictly inside (inner-cavity face).
            double[] bboxMinM = new[]
            {
                graph.BboxMinMm[0] / 1000.0,
                graph.BboxMinMm[1] / 1000.0,
                graph.BboxMinMm[2] / 1000.0,
            };
            double[] bboxMaxM = new[]
            {
                graph.BboxMaxMm[0] / 1000.0,
                graph.BboxMaxMm[1] / 1000.0,
                graph.BboxMaxMm[2] / 1000.0,
            };
            // Scale-invariant tolerance for "on bbox plane" test with legacy floor.
            //   = max(BoundaryTolFloorMm, BoundaryTolRatio × MaxBboxDimMm) (legacy 0.5mm at 100mm bbox).
            double bboxBoundaryTolMm = Math.Max(
                BoundaryTolFloorMm,
                BoundaryTolRatio * _scale.MaxBboxDimMm);
            double bboxBoundaryTolM = bboxBoundaryTolMm / 1000.0;
            // Scale-invariant wall thickness floor (mm).
            double effectiveMinWallThicknessMm = MinWallThicknessRatio * _scale.MaxBboxDimMm;

            for (int i = 0; i < planes.Count; i++)
            {
                if (paired.Contains(planes[i].FaceIndex)) continue;
                for (int j = i + 1; j < planes.Count; j++)
                {
                    if (paired.Contains(planes[j].FaceIndex)) continue;
                    var a = planes[i];
                    var b = planes[j];
                    double dot = a.Normal[0] * b.Normal[0] + a.Normal[1] * b.Normal[1] + a.Normal[2] * b.Normal[2];
                    if (dot > -ParallelDotThreshold) continue;
                    double areaRatio = Math.Min(a.AreaM2, b.AreaM2) / Math.Max(a.AreaM2, b.AreaM2);
                    if (areaRatio < WallAreaRatioThreshold) continue;
                    double dx = b.Origin[0] - a.Origin[0];
                    double dy = b.Origin[1] - a.Origin[1];
                    double dz = b.Origin[2] - a.Origin[2];
                    double distM = Math.Abs(dx * a.Normal[0] + dy * a.Normal[1] + dz * a.Normal[2]);
                    double distMm = distM * 1000.0;
                    if (distMm < effectiveMinWallThicknessMm) continue;

                    // --- Outward normal 결정 ---
                    // f.Normal 은 FaceClassifier 가 이미 IsReversed 를 적용해
                    //   raw plane normal IF !IsReversed ELSE -raw
                    // 로 저장한 "per-face outward" 이다. 명시적으로 다시 한 번 계산해서
                    // 의도를 코드로 못 박는다.
                    double[] aOut = ComputeOutwardNormal(a);
                    double[] bOut = ComputeOutwardNormal(b);

                    double aDotB = aOut[0] * bOut[0] + aOut[1] * bOut[1] + aOut[2] * bOut[2];

                    double[] chosen;
                    string consistency;

                    if (aDotB < 0.0)
                    {
                        // 정상 케이스: A_out 과 B_out 이 반대 → A 의 outward 를 채택.
                        chosen = aOut;
                        consistency = "A_outward_opposite_B";
                    }
                    else
                    {
                        // sign convention 깨짐 (예: IsReversed 가 한쪽만 잘못 설정 등).
                        // bbox 중심에서 더 먼 쪽이 진짜 outward.
                        double aFromCx = a.Origin[0] - cx;
                        double aFromCy = a.Origin[1] - cy;
                        double aFromCz = a.Origin[2] - cz;
                        double aDist = aFromCx * aFromCx + aFromCy * aFromCy + aFromCz * aFromCz;

                        double bFromCx = b.Origin[0] - cx;
                        double bFromCy = b.Origin[1] - cy;
                        double bFromCz = b.Origin[2] - cz;
                        double bDist = bFromCx * bFromCx + bFromCy * bFromCy + bFromCz * bFromCz;

                        if (aDist >= bDist)
                        {
                            chosen = aOut;
                            consistency = "A_outward_agree_B_centroid_far";
                        }
                        else
                        {
                            chosen = bOut;
                            consistency = "A_outward_agree_B_centroid_near";
                        }
                    }

                    // Inverted-wall flag: GEOMETRIC definition.
                    //
                    // A wall is "inverted" (i.e. an inner-cavity wall) if BOTH
                    // of its paired faces lie STRICTLY INSIDE the body's AABB
                    // along THE AXIS PERPENDICULAR TO EACH FACE — i.e. the
                    // face origin's coordinate on its own normal axis is not
                    // on the corresponding bbox min/max plane (within 0.5 mm
                    // tolerance). Outer-shell walls always have at least one
                    // face sitting on the bbox plane perpendicular to its
                    // normal.
                    //
                    // Why only the normal-aligned axis? face.Origin =
                    // plane.Frame.Origin, which is just SOME point on the
                    // infinite plane — its coordinates in axes UNRELATED to
                    // the normal are arbitrary (set by however SpaceClaim
                    // built the frame). For a pocket Y-wall produced from a
                    // sketch on the top face (Z=T), the resulting plane's
                    // Frame.Origin.Z = T = bbox max Z purely as an artefact
                    // of the sketch plane choice — that says nothing about
                    // whether the wall is on the boundary. Only the Y
                    // coordinate (the axis aligned with the wall's normal)
                    // is geometrically meaningful for the boundary test.
                    //
                    // Why not face.IsReversed? SpaceClaim's RectangleProfile +
                    // Body.ExtrudeProfile path produces a "bottom" face with
                    // IsReversed=true on EVERY simple box (purely a topology
                    // convention of how the rectangular sketch is wound). That
                    // convention has nothing to do with cavities, so using
                    // IsReversed flags the bottom wall of every plain box as
                    // inverted — which is wrong. Geometric containment along
                    // the normal axis is the honest test: cavities are
                    // interior, exterior walls are on the boundary.
                    bool aOnBoundary = IsOnBboxBoundary(a.Origin, a.Normal, bboxMinM, bboxMaxM, bboxBoundaryTolM);
                    bool bOnBoundary = IsOnBboxBoundary(b.Origin, b.Normal, bboxMinM, bboxMaxM, bboxBoundaryTolM);
                    bool isInverted = !aOnBoundary && !bOnBoundary;

                    var wall = new WallFeature
                    {
                        Id = "W" + wallId,
                        FaceA = Math.Min(a.FaceIndex, b.FaceIndex),
                        FaceB = Math.Max(a.FaceIndex, b.FaceIndex),
                        ThicknessMm = distMm,
                        Normal = chosen,
                        AreaMm2 = ((a.AreaM2 + b.AreaM2) / 2.0) * 1e6,
                        ConsistencyCheck = consistency,
                        IsInverted = isInverted,
                    };
                    graph.Walls.Add(wall);
                    paired.Add(a.FaceIndex);
                    paired.Add(b.FaceIndex);
                    wallId++;
                    break;
                }
            }
        }

        /// <summary>
        /// face origin (meters) 이, 자신의 normal 과 정렬된 축에서 bbox min/max
        /// 면 위에 (tolM 이내로) 닿아있는지 검사.
        ///
        /// 핵심: normal 의 dominant axis i = argmax(|normal[k]|) 만 검사한다.
        /// 다른 축의 origin 좌표는 plane.Frame.Origin 의 기하학적으로 무의미한
        /// 부산물이라 boundary 판단에 사용하면 false-negative 가 난다.
        ///
        /// 예) Pocket Y-wall: normal = ±Y → dominant axis = Y → origin.Y 만
        ///     bbox Y min/max 와 비교. origin.Z 가 sketch 평면 때문에
        ///     우연히 bbox max Z 와 같아도 무시한다 (irrelevant axis).
        /// 예) Outer top: normal = +Z → axis = Z → origin.Z ≈ bbox max Z →
        ///     boundary.
        /// </summary>
        private static bool IsOnBboxBoundary(double[] origin, double[] normal, double[] bboxMinM, double[] bboxMaxM, double tolM)
        {
            if (origin == null || origin.Length < 3) return false;
            if (normal == null || normal.Length < 3) return false;

            // dominant axis = argmax(|normal[k]|)
            int axis = 0;
            double maxAbs = Math.Abs(normal[0]);
            for (int k = 1; k < 3; k++)
            {
                double a = Math.Abs(normal[k]);
                if (a > maxAbs)
                {
                    maxAbs = a;
                    axis = k;
                }
            }

            // only the normal-aligned axis matters
            if (Math.Abs(origin[axis] - bboxMinM[axis]) <= tolM) return true;
            if (Math.Abs(origin[axis] - bboxMaxM[axis]) <= tolM) return true;
            return false;
        }

        /// <summary>
        /// face 의 OUTWARD normal (재료 → void 방향) 을 반환.
        /// 규칙: raw plane normal IF !IsReversed ELSE negated.
        ///
        /// 주의: FaceClassifier.Classify 는 이미 위 규칙으로 f.Normal 을 저장한다.
        /// 따라서 여기서 IsReversed 를 다시 적용하면 sign 이 두 번 뒤집힌다.
        /// 이 함수는 단순히 "이미 outward 인" f.Normal 을 복제해서 반환하지만,
        /// 의도를 명시적으로 드러내기 위한 thin wrapper 다.
        /// </summary>
        private static double[] ComputeOutwardNormal(FaceFeature f)
        {
            // f.Normal 이 이미 IsReversed 적용된 outward.
            // (FaceClassifier.cs 참조: double sign = face.IsReversed ? -1.0 : 1.0; normal *= sign)
            return new double[] { f.Normal[0], f.Normal[1], f.Normal[2] };
        }

        // ---- Fillet feature 생성 + R snapping ----
        private void BuildFilletFeatures(
            FeatureGraph graph,
            Dictionary<int, FaceAdjacency> adjGraph,
            RadiusSnapper snapper)
        {
            // 1) raw fillet list 만들기 (EdgeFillet or CornerBlend role 인 face)
            var filletFaces = new List<FaceFeature>();
            foreach (var f in graph.Faces)
            {
                if (f.CylinderRole == CylinderRole.EdgeFillet ||
                    f.CylinderRole == CylinderRole.CornerBlend)
                {
                    filletFaces.Add(f);
                }
            }
            if (filletFaces.Count == 0) return;

            // 2) R snapping
            var radii = filletFaces.Select(f => f.RadiusM * 1000.0).ToList();
            var clusters = snapper.ClusterRadii(radii, snapToNominal: true);

            // face_index → cluster snapped R
            var faceToSnapped = new Dictionary<int, double>();
            for (int ci = 0; ci < clusters.Count; ci++)
            {
                foreach (var memberOrigIdx in clusters[ci].MemberIndices)
                {
                    int faceIdx = filletFaces[memberOrigIdx].FaceIndex;
                    faceToSnapped[faceIdx] = clusters[ci].SnappedMm;
                }
            }

            // 3) FilletFeature 생성
            int filletId = 1;
            foreach (var f in filletFaces)
            {
                var fillet = new FilletFeature
                {
                    Id = "F" + filletId,
                    FaceIndex = f.FaceIndex,
                    RadiusMm = f.RadiusM * 1000.0,
                    SnappedRadiusMm = faceToSnapped.ContainsKey(f.FaceIndex)
                        ? faceToSnapped[f.FaceIndex]
                        : f.RadiusM * 1000.0,
                    BlendType = (f.Type == SurfaceType.Torus || f.Type == SurfaceType.Sphere)
                        ? "corner"
                        : "edge",
                    Concavity = f.Concavity == Models.ReverseEngineer.Concavity.Concave ? "concave" : "convex",
                    AdjacentFaces = new List<int>(),
                };
                // adjacency 채우기
                if (adjGraph.TryGetValue(f.FaceIndex, out var adj))
                {
                    fillet.AdjacentFaces.AddRange(adj.Neighbors);
                }
                graph.Fillets.Add(fillet);
                filletId++;
            }
        }
    }
}
