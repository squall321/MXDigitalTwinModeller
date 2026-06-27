using System;
using System.Collections.Generic;
using System.Linq;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer
{
    /// <summary>
    /// Stage 3 — 개별 Hole/Boss feature 를 패턴 (Linear/Grid/Circular) 으로 그룹화.
    /// 또한 mirror symmetry plane 후보를 검출.
    ///
    /// 그룹화 규칙:
    ///   - 각 hole/boss 는 최대 1개 패턴에만 속한다.
    ///   - Greedy: Grid → Circular → Linear 순서로 시도. 그 뒤 남는 멤버 → Single (생성 안 함).
    ///
    /// 모든 좌표는 mm 단위. (HoleFeature.PositionMm / BossFeature.BasePositionMm 가 이미 mm.)
    /// </summary>
    public static class PatternDetector
    {
        // Scale-invariant pattern tolerance ratios.
        //
        // Effective DefaultTolMm = max(PatternTolFloor, PatternTolRatio × MinBboxDim).
        // For desktop-CAD (~100mm body) this lands near the legacy 0.5mm.
        //   - phone-frame (minBboxDim ≈ 10mm)  → max(0.05, 0.05) = 0.05mm   ⚠ tighter
        //   - desktop part (minBboxDim ≈ 50mm) → max(0.05, 0.25) = 0.25mm
        //   - desktop part (minBboxDim ≈ 100mm)→ max(0.05, 0.5)  = 0.5mm  (legacy)
        //   - 1m assembly (minBboxDim ≈ 200mm) → max(0.05, 1.0)  = 1.0mm
        //   - MEMS 1mm   (minBboxDim ≈ 1mm)    → max(0.05, 0.005)= 0.05mm
        //
        // PartialTolMm always = 3 × DefaultTolMm (preserves the legacy 3x ratio).
        private const double PatternTolRatio = 0.005;     // 0.5% of min bbox
        private const double PatternTolFloor = 0.05;      // mm — kernel/registration floor
        private const double CoincidenceTolRatio = 1e-6;  // for "distinct points" gate
        private const double PcaEigenRatio = 1e-8;        // (mm)^2 / diag^2 — relative variance
        private const double SpacingFloorRatio = 1e-5;    // for grid row/col-step degeneracy
        private const double PatternSimilarRatio = 0.02;  // mirrored-pattern spacing similarity

        /// <summary>
        /// Resolve effective DefaultTolMm from Scale (or fall back to legacy 0.5mm).
        /// </summary>
        private static double ResolveDefaultTol(ModelScale.Scale scale)
        {
            if (scale == null) return 0.5;
            return Math.Max(PatternTolFloor, PatternTolRatio * scale.MinBboxDimMm);
        }

        // ---------- Hole patterns ----------

        public static List<HolePatternFeature> DetectHolePatterns(List<HoleFeature> holes, ModelScale.Scale scale = null)
        {
            double defaultTol = ResolveDefaultTol(scale);
            double partialTol = 3.0 * defaultTol;
            var result = new List<HolePatternFeature>();
            if (holes == null || holes.Count < 3) return result;

            // member id → (3D position, axis)
            var items = holes
                .Where(h => h.PositionMm != null && h.PositionMm.Length == 3)
                .Select(h => new PatternItem
                {
                    Id = h.Id,
                    Position = (double[])h.PositionMm.Clone(),
                    Axis = h.Axis != null ? (double[])h.Axis.Clone() : new[] { 0.0, 0.0, 1.0 },
                })
                .ToList();

            int patternId = 1;
            var used = new HashSet<string>();

            // 1) Circular first — 4 holes on a circle 도 2x2 grid 로 잘못 잡혀서
            //   circular 6-hole pattern 의 부분집합을 도둑맞는 케이스 (spec 22) 가 있다.
            //   원이 grid 보다 더 엄격한 조건 (모든 점이 동일 반경 + 동일 각간격) 이므로
            //   먼저 시도해서 차지하면 grid 가 남은 점에 대해서 자연스럽게 거른다.
            foreach (var circ in DetectCirculars(items, used, defaultTol, partialTol))
            {
                var p = new HolePatternFeature
                {
                    Id = "HP" + patternId++,
                    PatternType = PatternType.Circular,
                    MemberHoleIds = circ.MemberIds,
                    Centroid_mm = circ.Centroid,
                    DirectionAxis_mm = circ.DirectionAxis,
                    Radius_mm = circ.Radius,
                    Count = circ.MemberIds.Count,
                    IsPartialMatch = circ.IsPartial,
                    ScorePercent = circ.ScorePercent,
                };
                result.Add(p);
                foreach (var id in circ.MemberIds) used.Add(id);
            }

            // 2) Grid
            foreach (var grid in DetectGrids(items, used, defaultTol))
            {
                var p = new HolePatternFeature
                {
                    Id = "HP" + patternId++,
                    PatternType = PatternType.Grid,
                    MemberHoleIds = grid.MemberIds,
                    Centroid_mm = grid.Centroid,
                    DirectionAxis_mm = grid.DirectionAxis,
                    RowSpacing_mm = grid.RowSpacing,
                    ColSpacing_mm = grid.ColSpacing,
                    Rows = grid.Rows,
                    Cols = grid.Cols,
                    Count = grid.MemberIds.Count,
                    IsPartialMatch = grid.IsPartial,
                    ScorePercent = grid.ScorePercent,
                };
                result.Add(p);
                foreach (var id in grid.MemberIds) used.Add(id);
            }

            // 3) Linear
            foreach (var lin in DetectLinears(items, used, defaultTol))
            {
                var p = new HolePatternFeature
                {
                    Id = "HP" + patternId++,
                    PatternType = PatternType.Linear,
                    MemberHoleIds = lin.MemberIds,
                    Centroid_mm = lin.Centroid,
                    DirectionAxis_mm = lin.DirectionAxis,
                    Spacing_mm = lin.Spacing,
                    Count = lin.MemberIds.Count,
                    IsPartialMatch = lin.IsPartial,
                    ScorePercent = lin.ScorePercent,
                };
                result.Add(p);
                foreach (var id in lin.MemberIds) used.Add(id);
            }

            return result;
        }

        // ---------- Boss patterns ----------

        public static List<BossPatternFeature> DetectBossPatterns(List<BossFeature> bosses, ModelScale.Scale scale = null)
        {
            double defaultTol = ResolveDefaultTol(scale);
            double partialTol = 3.0 * defaultTol;
            var result = new List<BossPatternFeature>();
            if (bosses == null || bosses.Count < 3) return result;

            var items = bosses
                .Where(b => b.BasePositionMm != null && b.BasePositionMm.Length == 3)
                .Select(b => new PatternItem
                {
                    Id = b.Id,
                    Position = (double[])b.BasePositionMm.Clone(),
                    Axis = b.Axis != null ? (double[])b.Axis.Clone() : new[] { 0.0, 0.0, 1.0 },
                })
                .ToList();

            int patternId = 1;
            var used = new HashSet<string>();

            // Circular before Grid — 같은 이유로 (DetectHolePatterns 주석 참고).
            foreach (var circ in DetectCirculars(items, used, defaultTol, partialTol))
            {
                var p = new BossPatternFeature
                {
                    Id = "BP" + patternId++,
                    PatternType = PatternType.Circular,
                    MemberBossIds = circ.MemberIds,
                    Centroid_mm = circ.Centroid,
                    DirectionAxis_mm = circ.DirectionAxis,
                    Radius_mm = circ.Radius,
                    Count = circ.MemberIds.Count,
                    IsPartialMatch = circ.IsPartial,
                    ScorePercent = circ.ScorePercent,
                };
                result.Add(p);
                foreach (var id in circ.MemberIds) used.Add(id);
            }

            foreach (var grid in DetectGrids(items, used, defaultTol))
            {
                var p = new BossPatternFeature
                {
                    Id = "BP" + patternId++,
                    PatternType = PatternType.Grid,
                    MemberBossIds = grid.MemberIds,
                    Centroid_mm = grid.Centroid,
                    DirectionAxis_mm = grid.DirectionAxis,
                    RowSpacing_mm = grid.RowSpacing,
                    ColSpacing_mm = grid.ColSpacing,
                    Rows = grid.Rows,
                    Cols = grid.Cols,
                    Count = grid.MemberIds.Count,
                    IsPartialMatch = grid.IsPartial,
                    ScorePercent = grid.ScorePercent,
                };
                result.Add(p);
                foreach (var id in grid.MemberIds) used.Add(id);
            }

            foreach (var lin in DetectLinears(items, used, defaultTol))
            {
                var p = new BossPatternFeature
                {
                    Id = "BP" + patternId++,
                    PatternType = PatternType.Linear,
                    MemberBossIds = lin.MemberIds,
                    Centroid_mm = lin.Centroid,
                    DirectionAxis_mm = lin.DirectionAxis,
                    Spacing_mm = lin.Spacing,
                    Count = lin.MemberIds.Count,
                    IsPartialMatch = lin.IsPartial,
                    ScorePercent = lin.ScorePercent,
                };
                result.Add(p);
                foreach (var id in lin.MemberIds) used.Add(id);
            }

            return result;
        }

        // ---------- Composites (pattern + symmetry combination) ----------

        /// <summary>
        /// Pattern + Symmetry 결합 패스. FeatureExtractor 가 Hole/Boss pattern 과 Symmetry 를
        /// 모두 채운 다음 호출.
        ///
        /// 현재 검출하는 composite:
        ///   * MirroredPair (type = "mirrored_<patterntype>") — 동일 유형 + 비슷한 count/spacing 의
        ///     pattern 2개가 score >= 90 인 symmetry plane 의 양쪽에 거의 거울 대칭으로 위치.
        ///
        /// Side effects: 매칭된 pattern 의 MirroredPartnerId 를 채운다.
        /// </summary>
        public static List<CompositeFeature> DetectComposites(FeatureGraph graph, ModelScale.Scale scale = null)
        {
            double defaultTol = ResolveDefaultTol(scale);
            double partialTol = 3.0 * defaultTol;
            var composites = new List<CompositeFeature>();
            if (graph == null) return composites;

            // 1) 점수 >= 90 인 mirror plane 만 사용
            var strongSyms = graph.Symmetries == null
                ? new List<SymmetryFeature>()
                : graph.Symmetries.Where(s => s.ScorePercent >= 90.0).ToList();
            if (strongSyms.Count == 0) return composites;

            int compositeId = 1;

            // Hole patterns: 같은 type 쌍에 대해 mirror 매칭 검사
            var holePatterns = graph.HolePatterns ?? new List<HolePatternFeature>();
            for (int i = 0; i < holePatterns.Count; i++)
            {
                var a = holePatterns[i];
                if (!string.IsNullOrEmpty(a.MirroredPartnerId)) continue;
                for (int j = i + 1; j < holePatterns.Count; j++)
                {
                    var b = holePatterns[j];
                    if (!string.IsNullOrEmpty(b.MirroredPartnerId)) continue;
                    if (a.PatternType != b.PatternType) continue;
                    // count 가 비슷해야 함 (±20% 또는 ±1)
                    int diff = Math.Abs(a.Count - b.Count);
                    if (diff > 1 && diff > Math.Max(a.Count, b.Count) * 0.2) continue;
                    // spacing 검증 (해당 타입에 맞게)
                    if (!PatternsSimilar(a.PatternType, a.Spacing_mm, b.Spacing_mm,
                        a.RowSpacing_mm, b.RowSpacing_mm, a.ColSpacing_mm, b.ColSpacing_mm,
                        a.Radius_mm, b.Radius_mm)) continue;
                    foreach (var sym in strongSyms)
                    {
                        if (CentroidsMirror(a.Centroid_mm, b.Centroid_mm,
                            sym.MirrorPlaneOrigin_mm, sym.MirrorPlaneNormal_mm, partialTol * 2.0))
                        {
                            // Match!
                            a.MirroredPartnerId = b.Id;
                            b.MirroredPartnerId = a.Id;
                            composites.Add(new CompositeFeature
                            {
                                Id = "C" + compositeId++,
                                Type = "mirrored_" + a.PatternType.ToString().ToLowerInvariant(),
                                MemberPatternIds = new List<string> { a.Id, b.Id },
                                SymmetryId = sym.Id,
                            });
                            break;
                        }
                    }
                }
            }

            // Boss patterns: 동일 로직
            var bossPatterns = graph.BossPatterns ?? new List<BossPatternFeature>();
            for (int i = 0; i < bossPatterns.Count; i++)
            {
                var a = bossPatterns[i];
                if (!string.IsNullOrEmpty(a.MirroredPartnerId)) continue;
                for (int j = i + 1; j < bossPatterns.Count; j++)
                {
                    var b = bossPatterns[j];
                    if (!string.IsNullOrEmpty(b.MirroredPartnerId)) continue;
                    if (a.PatternType != b.PatternType) continue;
                    int diff = Math.Abs(a.Count - b.Count);
                    if (diff > 1 && diff > Math.Max(a.Count, b.Count) * 0.2) continue;
                    if (!PatternsSimilar(a.PatternType, a.Spacing_mm, b.Spacing_mm,
                        a.RowSpacing_mm, b.RowSpacing_mm, a.ColSpacing_mm, b.ColSpacing_mm,
                        a.Radius_mm, b.Radius_mm)) continue;
                    foreach (var sym in strongSyms)
                    {
                        if (CentroidsMirror(a.Centroid_mm, b.Centroid_mm,
                            sym.MirrorPlaneOrigin_mm, sym.MirrorPlaneNormal_mm, partialTol * 2.0))
                        {
                            a.MirroredPartnerId = b.Id;
                            b.MirroredPartnerId = a.Id;
                            composites.Add(new CompositeFeature
                            {
                                Id = "C" + compositeId++,
                                Type = "mirrored_" + a.PatternType.ToString().ToLowerInvariant(),
                                MemberPatternIds = new List<string> { a.Id, b.Id },
                                SymmetryId = sym.Id,
                            });
                            break;
                        }
                    }
                }
            }

            // ---- Nested patterns: pattern-of-patterns ----
            //   If 3+ patterns of the SAME inner PatternType have centroids that themselves
            //   form a Linear / Grid / Circular arrangement, emit a CompositeFeature
            //   with Type = "<outer>_of_<inner>" (e.g. "grid_of_grids", "linear_of_circulars").
            //
            //   Conservative: we require inner patterns to share the same PatternType +
            //   similar Count, then re-use the same Linear/Grid/Circular detector on
            //   synthetic PatternItem points built from centroids. Only 100% matches are
            //   emitted (no partial), and each pattern can belong to at most one nested
            //   composite.
            //
            //   ⚠️ Risk: 3 pattern centroids that coincidentally align in space (random
            //   spatial coincidence) will be reported as spurious "linear_of_X". Mitigation:
            //   require >=3 inner patterns, same inner type, default tolerance.
            DetectNestedComposites(holePatterns, bossPatterns, composites, ref compositeId, defaultTol, partialTol);

            return composites;
        }

        /// <summary>
        /// Find pattern-of-patterns composites. Groups patterns by inner PatternType,
        /// builds synthetic PatternItem points from centroids, then runs the standard
        /// Linear / Grid / Circular detectors.
        /// </summary>
        private static void DetectNestedComposites(
            List<HolePatternFeature> holePatterns,
            List<BossPatternFeature> bossPatterns,
            List<CompositeFeature> composites,
            ref int compositeId,
            double defaultTol,
            double partialTol)
        {
            // Combine both kinds into a uniform shape: (Id, Centroid, PatternType)
            var entries = new List<NestedEntry>();
            if (holePatterns != null)
            {
                foreach (var hp in holePatterns)
                {
                    if (hp.Centroid_mm == null || hp.Centroid_mm.Length != 3) continue;
                    entries.Add(new NestedEntry
                    {
                        Id = hp.Id,
                        Centroid = (double[])hp.Centroid_mm.Clone(),
                        InnerType = hp.PatternType,
                    });
                }
            }
            if (bossPatterns != null)
            {
                foreach (var bp in bossPatterns)
                {
                    if (bp.Centroid_mm == null || bp.Centroid_mm.Length != 3) continue;
                    entries.Add(new NestedEntry
                    {
                        Id = bp.Id,
                        Centroid = (double[])bp.Centroid_mm.Clone(),
                        InnerType = bp.PatternType,
                    });
                }
            }
            if (entries.Count < 3) return;

            // Group by inner PatternType.
            var byType = entries.GroupBy(e => e.InnerType).Where(g => g.Count() >= 3);
            foreach (var grp in byType)
            {
                var list = grp.ToList();
                var items = list.Select(e => new PatternItem
                {
                    Id = e.Id,
                    Position = (double[])e.Centroid.Clone(),
                    Axis = new[] { 0.0, 0.0, 1.0 },
                }).ToList();

                var used = new HashSet<string>();

                // Try Circular > Grid > Linear (same priority as DetectHolePatterns).
                string innerName = grp.Key.ToString().ToLowerInvariant() + "s";
                foreach (var circ in DetectCirculars(items, used, defaultTol, partialTol))
                {
                    if (circ.IsPartial) continue;
                    composites.Add(new CompositeFeature
                    {
                        Id = "C" + compositeId++,
                        Type = "circular_of_" + innerName,
                        MemberPatternIds = circ.MemberIds,
                        SymmetryId = null,
                    });
                    foreach (var id in circ.MemberIds) used.Add(id);
                }
                foreach (var g in DetectGrids(items, used, defaultTol))
                {
                    if (g.IsPartial) continue;
                    composites.Add(new CompositeFeature
                    {
                        Id = "C" + compositeId++,
                        Type = "grid_of_" + innerName,
                        MemberPatternIds = g.MemberIds,
                        SymmetryId = null,
                    });
                    foreach (var id in g.MemberIds) used.Add(id);
                }
                foreach (var lin in DetectLinears(items, used, defaultTol))
                {
                    if (lin.IsPartial) continue;
                    composites.Add(new CompositeFeature
                    {
                        Id = "C" + compositeId++,
                        Type = "linear_of_" + innerName,
                        MemberPatternIds = lin.MemberIds,
                        SymmetryId = null,
                    });
                    foreach (var id in lin.MemberIds) used.Add(id);
                }
            }
        }

        private class NestedEntry
        {
            public string Id;
            public double[] Centroid;
            public PatternType InnerType;
        }

        private static bool PatternsSimilar(PatternType type,
            double sA, double sB,
            double rsA, double rsB, double csA, double csB,
            double radA, double radB)
        {
            // Scale-invariant: spacing similarity is a ratio of the larger spacing.
            //   tol = PatternSimilarRatio × max(sA,sB,rsA,rsB,csA,csB,radA,radB)
            //   With PatternSimilarRatio = 0.02, two patterns match when their spacing
            //   differs by less than 2%. For desktop CAD (spacing ~50mm) this is ~1mm
            //   (legacy). For aerospace (spacing ~500mm) it's 10mm. For MEMS (spacing
            //   ~0.05mm) it's 0.001mm. No absolute mm coupling.
            double maxSpacing = Math.Max(
                Math.Max(Math.Max(Math.Abs(sA), Math.Abs(sB)), Math.Max(Math.Abs(rsA), Math.Abs(rsB))),
                Math.Max(Math.Max(Math.Abs(csA), Math.Abs(csB)), Math.Max(Math.Abs(radA), Math.Abs(radB))));
            double tol = PatternSimilarRatio * Math.Max(maxSpacing, 1e-6);
            if (type == PatternType.Linear)
                return Math.Abs(sA - sB) <= tol;
            if (type == PatternType.Grid)
                return Math.Abs(rsA - rsB) <= tol && Math.Abs(csA - csB) <= tol;
            if (type == PatternType.Circular)
                return Math.Abs(radA - radB) <= tol;
            return true;
        }

        private static bool CentroidsMirror(double[] cA, double[] cB,
            double[] planeOrigin, double[] planeNormal, double tol)
        {
            if (cA == null || cB == null || planeOrigin == null || planeNormal == null) return false;
            // mirror(cA) across plane should equal cB within tol
            var d = Vec3.Sub(cA, planeOrigin);
            double dist = Vec3.Dot(d, planeNormal);
            var mirrored = Vec3.Sub(cA, Vec3.Scale(planeNormal, 2.0 * dist));
            return Vec3.Norm(Vec3.Sub(mirrored, cB)) <= tol;
        }

        // ---------- Symmetry ----------

        public static List<SymmetryFeature> DetectSymmetry(FeatureGraph graph, ModelScale.Scale scale = null)
        {
            double defaultTol = ResolveDefaultTol(scale);
            var result = new List<SymmetryFeature>();
            if (graph == null) return result;

            // 검사 대상: hole + boss 의 위치만 사용 (충분히 대표성 있음).
            // ⚠️ Spec 32 (asymmetric_box) 같이 box 자체는 대칭이지만 feature 분포가
            //   비대칭인 경우 — 3 holes left + 2 bosses right — 가 검출되도록
            //   feature 점들을 TYPE 까지 포함해서 추적한다. mirror 매칭은 같은 type
            //   같은 위치여야 hit. 그렇지 않으면 hole 의 mirror 가 boss 자리에
            //   떨어져서 잘못 hit 로 잡힌다.
            var points = new List<double[]>();      // 전체 (face-equivalent) 점 — 기존 동작 유지
            var typedFeatures = new List<TypedPoint>(); // type-aware feature 점
            if (graph.Holes != null)
            {
                foreach (var h in graph.Holes)
                {
                    if (h.PositionMm != null && h.PositionMm.Length == 3)
                    {
                        var p = new[] { h.PositionMm[0], h.PositionMm[1], h.PositionMm[2] };
                        points.Add(p);
                        typedFeatures.Add(new TypedPoint { Pos = p, Kind = 1 /*hole*/ });
                    }
                }
            }
            if (graph.Bosses != null)
            {
                foreach (var b in graph.Bosses)
                {
                    if (b.BasePositionMm != null && b.BasePositionMm.Length == 3)
                    {
                        var p = new[] { b.BasePositionMm[0], b.BasePositionMm[1], b.BasePositionMm[2] };
                        points.Add(p);
                        typedFeatures.Add(new TypedPoint { Pos = p, Kind = 2 /*boss*/ });
                    }
                }
            }
            if (points.Count < 2) return result;

            // bbox 중심을 plane origin 으로 사용
            double[] origin;
            if (graph.BboxMinMm != null && graph.BboxMaxMm != null &&
                graph.BboxMinMm.Length == 3 && graph.BboxMaxMm.Length == 3)
            {
                origin = new[]
                {
                    0.5 * (graph.BboxMinMm[0] + graph.BboxMaxMm[0]),
                    0.5 * (graph.BboxMinMm[1] + graph.BboxMaxMm[1]),
                    0.5 * (graph.BboxMinMm[2] + graph.BboxMaxMm[2]),
                };
            }
            else
            {
                origin = Centroid(points);
            }

            // 세 후보 평면 (YZ-mirror = normal X, ZX-mirror = normal Y, XY-mirror = normal Z)
            var normals = new[]
            {
                new[] { 1.0, 0.0, 0.0 },
                new[] { 0.0, 1.0, 0.0 },
                new[] { 0.0, 0.0, 1.0 },
            };

            int symId = 1;
            foreach (var n in normals)
            {
                double faceScore = MirrorScore(points, origin, n, defaultTol);

                double combined;
                if (typedFeatures.Count >= 1)
                {
                    double featureScore = TypedMirrorScore(typedFeatures, origin, n, defaultTol);
                    // Feature 가 충분히 (>=3) 있을 때는 feature 분포에 더 큰 weight (0.7) 를 줘서
                    // box 외형의 대칭성에 가려지지 않도록 한다. Feature 1~2 개일 때는 0.5/0.5.
                    double wFeat = (typedFeatures.Count >= 3) ? 0.7 : 0.5;
                    double wFace = 1.0 - wFeat;
                    combined = wFace * faceScore + wFeat * featureScore;
                }
                else
                {
                    combined = faceScore;
                }

                if (combined >= 80.0)  // ≥80% 만 strong symmetry 로 인정 (spec 32 가이드)
                {
                    result.Add(new SymmetryFeature
                    {
                        Id = "S" + symId++,
                        MirrorPlaneNormal_mm = new[] { n[0], n[1], n[2] },
                        MirrorPlaneOrigin_mm = new[] { origin[0], origin[1], origin[2] },
                        ScorePercent = combined,
                    });
                }
            }

            return result;
        }

        private class TypedPoint
        {
            public double[] Pos;
            public int Kind;   // 1 = hole, 2 = boss
        }

        /// <summary>
        /// MirrorScore 의 type-aware 변형. mirror 가 같은 Kind 의 다른 feature 와
        /// tol 이내에 매칭될 때만 hit. (Hole 의 mirror 가 Boss 자리에 떨어져도 hit 아님.)
        /// 평면 위에 있는 (dist &lt; tol) feature 는 자기 자신과 매칭되어 자동 hit.
        /// </summary>
        private static double TypedMirrorScore(List<TypedPoint> features, double[] origin, double[] normal, double tol)
        {
            if (features.Count == 0) return 0.0;
            int hits = 0;
            for (int i = 0; i < features.Count; i++)
            {
                var fi = features[i];
                var d = Vec3.Sub(fi.Pos, origin);
                double dist = Vec3.Dot(d, normal);
                if (Math.Abs(dist) <= tol) { hits++; continue; }
                var mirror = Vec3.Sub(fi.Pos, Vec3.Scale(normal, 2.0 * dist));
                bool found = false;
                for (int j = 0; j < features.Count; j++)
                {
                    if (i == j) continue;
                    if (features[j].Kind != fi.Kind) continue;  // 같은 type 만 인정
                    if (Vec3.Norm(Vec3.Sub(features[j].Pos, mirror)) <= tol) { found = true; break; }
                }
                if (found) hits++;
            }
            return 100.0 * hits / features.Count;
        }

        // ============================================================
        // Internals
        // ============================================================

        private class PatternItem
        {
            public string Id;
            public double[] Position;
            public double[] Axis;
        }

        // --- Linear ---

        private class LinearResult
        {
            public List<string> MemberIds;
            public double[] Centroid;
            public double[] DirectionAxis;
            public double Spacing;
            public bool IsPartial;
            public double ScorePercent;
        }

        /// <summary>
        /// 3개 이상의 collinear + 등간격 hole 검출.
        /// Brute-force: 모든 hole 쌍을 시작점으로 잡고 같은 방향에 더 있는지 본다.
        /// </summary>
        private static List<LinearResult> DetectLinears(List<PatternItem> items, HashSet<string> used, double defaultTol)
        {
            var results = new List<LinearResult>();
            var available = items.Where(i => !used.Contains(i.Id)).ToList();
            if (available.Count < 3) return results;

            // Coincidence floor: any two PatternItems with separation < coincidenceTol are
            // considered the same point (degenerate seed). Scale-invariant: ratio of the
            // largest pair-wise spread along ANY single dimension.
            double maxExtent = 0.0;
            foreach (var p in available)
                for (int k = 0; k < 3; k++)
                    maxExtent = Math.Max(maxExtent, Math.Abs(p.Position[k]));
            double coincidenceTol = Math.Max(1e-9, CoincidenceTolRatio * Math.Max(maxExtent, 1.0));

            var consumed = new HashSet<string>();

            for (int i = 0; i < available.Count; i++)
            {
                if (consumed.Contains(available[i].Id)) continue;
                for (int j = i + 1; j < available.Count; j++)
                {
                    if (consumed.Contains(available[j].Id)) continue;
                    var a = available[i];
                    var b = available[j];
                    double[] dir;
                    double step = Vec3.Norm(Vec3.Sub(b.Position, a.Position));
                    if (step < coincidenceTol) continue;
                    dir = Vec3.Scale(Vec3.Sub(b.Position, a.Position), 1.0 / step);

                    // a 부터 dir 방향 (양 방향 모두) 에 step 의 정수배 거리에 있는 멤버 수집
                    var members = new List<PatternItem> { a, b };
                    foreach (var c in available)
                    {
                        if (c.Id == a.Id || c.Id == b.Id) continue;
                        if (consumed.Contains(c.Id)) continue;
                        var d = Vec3.Sub(c.Position, a.Position);
                        double along = Vec3.Dot(d, dir);
                        double perp = Math.Sqrt(Math.Max(0, Vec3.Dot(d, d) - along * along));
                        if (perp > defaultTol) continue;
                        // along 이 step 의 정수배 ±tol?
                        double k = along / step;
                        double kRounded = Math.Round(k);
                        if (Math.Abs(k - kRounded) * step > defaultTol) continue;
                        members.Add(c);
                    }
                    if (members.Count < 3) continue;

                    // step 위치로 정렬 + 등간격 검증
                    members = members
                        .OrderBy(m => Vec3.Dot(Vec3.Sub(m.Position, a.Position), dir))
                        .ToList();
                    bool ok = true;
                    double measuredSpacing = 0;
                    for (int k = 1; k < members.Count; k++)
                    {
                        double s = Vec3.Dot(Vec3.Sub(members[k].Position, members[k - 1].Position), dir);
                        if (k == 1) measuredSpacing = s;
                        else if (Math.Abs(s - measuredSpacing) > defaultTol) { ok = false; break; }
                    }
                    if (!ok) continue;

                    var memberIds = members.Select(m => m.Id).ToList();
                    var positions = members.Select(m => m.Position).ToList();
                    results.Add(new LinearResult
                    {
                        MemberIds = memberIds,
                        Centroid = Centroid(positions),
                        DirectionAxis = (double[])dir.Clone(),
                        Spacing = measuredSpacing,
                        IsPartial = false,
                        ScorePercent = 100.0,
                    });
                    foreach (var id in memberIds) consumed.Add(id);
                    break;  // i 의 다른 j 와 짝짓지 않음 (consumed)
                }
            }
            return results;
        }

        // --- Circular ---

        private class CircularResult
        {
            public List<string> MemberIds;
            public double[] Centroid;
            public double[] DirectionAxis;
            public double Radius;
            public bool IsPartial;
            public double ScorePercent;
        }

        /// <summary>
        /// 3+ 점이 같은 원 위 (등 반경, 등 각도) 에 있으면 Circular.
        /// 평면은 멤버들 공통 axis (대부분 hole 의 cylinder axis) 의 평균.
        /// </summary>
        private static List<CircularResult> DetectCirculars(List<PatternItem> items, HashSet<string> used, double defaultTol, double partialTol)
        {
            var results = new List<CircularResult>();
            var available = items.Where(i => !used.Contains(i.Id)).ToList();
            if (available.Count < 3) return results;

            // 동일 axis 로 그룹핑 (대부분 케이스: 모든 hole 이 +Z 축)
            var groups = GroupByAxis(available);
            var consumed = new HashSet<string>();

            foreach (var grp in groups)
            {
                if (grp.Items.Count < 3) continue;
                var positions = grp.Items.Select(it => it.Position).ToList();
                var centroid = Centroid(positions);

                // 모든 멤버의 centroid 까지 거리 검사
                var radii = positions.Select(p => Vec3.Norm(Vec3.Sub(p, centroid))).ToList();
                double rMean = radii.Average();
                if (rMean < defaultTol) continue;  // 너무 작은 원
                bool allEqualR = radii.All(r => Math.Abs(r - rMean) <= defaultTol);
                if (!allEqualR) continue;

                // 각도 검사 — centroid 평면 (axis 에 수직) 위에서 각도 정렬
                var axis = grp.Axis;
                // 평면 내 기저 (u, v) 만들기
                var u = OrthoTo(axis);
                var v = Vec3.Cross(axis, u);
                var angles = new List<double>();
                for (int k = 0; k < positions.Count; k++)
                {
                    var d = Vec3.Sub(positions[k], centroid);
                    double du = Vec3.Dot(d, u);
                    double dv = Vec3.Dot(d, v);
                    angles.Add(Math.Atan2(dv, du));
                }
                var sorted = angles.OrderBy(a => a).ToList();
                bool equalAngle = true;
                double expectedStep = 2.0 * Math.PI / sorted.Count;
                for (int k = 0; k < sorted.Count; k++)
                {
                    double next = (k + 1 < sorted.Count) ? sorted[k + 1] : sorted[0] + 2.0 * Math.PI;
                    double diff = next - sorted[k];
                    // 각도 허용 오차: rMean 위에서 호 길이 defaultTol 에 해당하는 각도
                    double angTol = defaultTol / Math.Max(rMean, 1e-3);
                    if (Math.Abs(diff - expectedStep) > angTol) { equalAngle = false; break; }
                }
                if (!equalAngle) continue;

                var memberIds = grp.Items.Select(it => it.Id).ToList();
                if (memberIds.Any(id => consumed.Contains(id))) continue;
                results.Add(new CircularResult
                {
                    MemberIds = memberIds,
                    Centroid = centroid,
                    DirectionAxis = (double[])axis.Clone(),
                    Radius = rMean,
                    IsPartial = false,
                    ScorePercent = 100.0,
                });
                foreach (var id in memberIds) consumed.Add(id);
            }

            // ---- Partial-match second pass ----
            // 각 group 의 unconsumed 멤버에서, 한 멤버씩 outlier 후보로 빼고 나머지가
            // strict criterion 을 만족하는지 검사. tolerance 는 PartialTolMm 까지 완화.
            // 즉 6개 중 5개가 원 위 + 1개가 outlier → 5-member partial Circular 인식.
            foreach (var grp in groups)
            {
                var remaining = grp.Items.Where(it => !consumed.Contains(it.Id)).ToList();
                if (remaining.Count < 4) continue;  // N-1 >= 3 이 되려면 N >= 4

                int origN = remaining.Count;
                int bestRemovedIdx = -1;
                List<PatternItem> bestKept = null;
                double bestRefittedR = 0;

                for (int rem = 0; rem < remaining.Count; rem++)
                {
                    var kept = new List<PatternItem>(remaining);
                    kept.RemoveAt(rem);
                    if (kept.Count < 3) continue;

                    var keptPositions = kept.Select(it => it.Position).ToList();
                    var keptCentroid = Centroid(keptPositions);
                    var keptRadii = keptPositions
                        .Select(p => Vec3.Norm(Vec3.Sub(p, keptCentroid)))
                        .ToList();
                    double rMean = keptRadii.Average();
                    if (rMean < defaultTol) continue;
                    bool allEqualR = keptRadii.All(r => Math.Abs(r - rMean) <= partialTol);
                    if (!allEqualR) continue;

                    // 각도 등각 검사
                    var u = OrthoTo(grp.Axis);
                    var v = Vec3.Cross(grp.Axis, u);
                    var angles = new List<double>();
                    foreach (var it in kept)
                    {
                        var d = Vec3.Sub(it.Position, keptCentroid);
                        angles.Add(Math.Atan2(Vec3.Dot(d, v), Vec3.Dot(d, u)));
                    }
                    var sortedAng = angles.OrderBy(a => a).ToList();
                    double expectedStep = 2.0 * Math.PI / sortedAng.Count;
                    bool equalAngle = true;
                    double angTol = partialTol / Math.Max(rMean, 1e-3);
                    for (int k = 0; k < sortedAng.Count; k++)
                    {
                        double next = (k + 1 < sortedAng.Count) ? sortedAng[k + 1] : sortedAng[0] + 2.0 * Math.PI;
                        if (Math.Abs((next - sortedAng[k]) - expectedStep) > angTol)
                        { equalAngle = false; break; }
                    }
                    if (!equalAngle) continue;

                    // coverage = kept / orig >= 50%
                    double coverage = (double)kept.Count / Math.Max(1, origN);
                    if (coverage < 0.5) continue;

                    // 가장 큰 멤버 수의 partial 을 우선 (현재는 N-1 고정)
                    if (bestKept == null || kept.Count > bestKept.Count)
                    {
                        bestKept = kept;
                        bestRemovedIdx = rem;
                        bestRefittedR = rMean;
                    }
                }

                if (bestKept == null) continue;

                var keptIds = bestKept.Select(it => it.Id).ToList();
                if (keptIds.Any(id => consumed.Contains(id))) continue;
                var keptPos = bestKept.Select(it => it.Position).ToList();
                int origNFinal = bestKept.Count + 1; // we removed exactly 1 outlier
                double partialScore = 100.0 * bestKept.Count / Math.Max(1, origNFinal);
                results.Add(new CircularResult
                {
                    MemberIds = keptIds,
                    Centroid = Centroid(keptPos),
                    DirectionAxis = (double[])grp.Axis.Clone(),
                    Radius = bestRefittedR,
                    IsPartial = true,
                    ScorePercent = partialScore,
                });
                foreach (var id in keptIds) consumed.Add(id);
            }

            return results;
        }

        // --- Grid ---

        private class GridResult
        {
            public List<string> MemberIds;
            public double[] Centroid;
            public double[] DirectionAxis;
            public double RowSpacing;
            public double ColSpacing;
            public int Rows;
            public int Cols;
            public bool IsPartial;
            public double ScorePercent;
        }

        /// <summary>
        /// rows × cols 격자 — 4+ 멤버, 두 perpendicular 방향으로 등간격.
        /// 단순 접근: 같은 axis 그룹 안에서 두 점을 잡아 (u,v) 기저 생성 후
        /// 각 멤버를 (i, j) 격자 좌표로 환산. 모든 정수 (i, j) ∈ rows×cols 가 채워지면 Grid.
        /// </summary>
        private static List<GridResult> DetectGrids(List<PatternItem> items, HashSet<string> used, double defaultTol)
        {
            var results = new List<GridResult>();
            var available = items.Where(i => !used.Contains(i.Id)).ToList();
            if (available.Count < 4) return results;
            var groups = GroupByAxis(available);
            var consumed = new HashSet<string>();

            // Scale-invariant coincidence epsilon for "distinct seed pair".
            double maxExtent = 0.0;
            foreach (var p in available)
                for (int kk = 0; kk < 3; kk++)
                    maxExtent = Math.Max(maxExtent, Math.Abs(p.Position[kk]));
            double coincidenceTol = Math.Max(1e-9, CoincidenceTolRatio * Math.Max(maxExtent, 1.0));

            foreach (var grp in groups)
            {
                if (grp.Items.Count < 4) continue;
                var axis = grp.Axis;

              retry_grid:
                var groupItems = grp.Items.Where(it => !consumed.Contains(it.Id)).ToList();
                if (groupItems.Count < 4) continue;

                // 모든 (i,j) 후보 쌍 시도. i 를 origin, j 를 첫 번째 row neighbor 로 가정.
                bool foundGrid = false;
                for (int i = 0; i < groupItems.Count && !foundGrid; i++)
                {
                    for (int j = 0; j < groupItems.Count && !foundGrid; j++)
                    {
                        if (i == j) continue;
                        var origin = groupItems[i].Position;
                        var uVec = Vec3.Sub(groupItems[j].Position, origin);
                        double uLen = Vec3.Norm(uVec);
                        if (uLen < coincidenceTol) continue;
                        uVec = Vec3.Scale(uVec, 1.0 / uLen);

                        // axis 와 평행한 부분 제거 (격자는 axis 에 수직 평면 위에 있어야)
                        double along = Vec3.Dot(uVec, axis);
                        var uPlanar = Vec3.Sub(uVec, Vec3.Scale(axis, along));
                        double uPLen = Vec3.Norm(uPlanar);
                        if (uPLen < 1 - 1e-3) continue;  // 거의 평면에 안 있으면 skip
                        uVec = Vec3.Scale(uPlanar, 1.0 / uPLen);

                        // v = axis × u (axis 에 수직 평면 안에서 perpendicular)
                        var vVec = Vec3.Cross(axis, uVec);

                        // 각 멤버를 (i,j) 정수 격자 좌표로 환산
                        //   - axis 좌표가 origin 과 크게 다르면 (e.g. counterbore inner/outer 가 다른 Z) skip
                        //   - u 방향 정수 슬롯에 안 맞으면 skip
                        //   - 살아남은 멤버만 격자 후보. 최소 origin/neighbor 둘은 살아있다.
                        var coords = new List<Tuple<int, double, PatternItem>>();
                        double minV = double.PositiveInfinity, maxV = double.NegativeInfinity;
                        foreach (var it in groupItems)
                        {
                            var d = Vec3.Sub(it.Position, origin);
                            double duCoord = Vec3.Dot(d, uVec);
                            double dvCoord = Vec3.Dot(d, vVec);
                            double dAxis = Vec3.Dot(d, axis);
                            if (Math.Abs(dAxis) > defaultTol) continue;
                            double iRaw = duCoord / uLen;
                            int iCol = (int)Math.Round(iRaw);
                            if (Math.Abs(iRaw - iCol) * uLen > defaultTol) continue;
                            coords.Add(Tuple.Create(iCol, dvCoord, it));
                            if (dvCoord < minV) minV = dvCoord;
                            if (dvCoord > maxV) maxV = dvCoord;
                        }
                        if (coords.Count < 4) continue;

                        // v 좌표를 정렬해서 unique row 추출
                        var distinctV = coords
                            .Select(t => t.Item2)
                            .OrderBy(v => v)
                            .ToList();
                        var vRows = new List<double> { distinctV[0] };
                        foreach (var vv in distinctV.Skip(1))
                        {
                            if (Math.Abs(vv - vRows[vRows.Count - 1]) > defaultTol)
                                vRows.Add(vv);
                        }
                        if (vRows.Count < 2) continue;

                        // row 사이 간격이 모두 같은가
                        double rowStep = vRows[1] - vRows[0];
                        if (rowStep < coincidenceTol) continue;
                        bool rowsEqual = true;
                        for (int k = 1; k < vRows.Count; k++)
                        {
                            if (Math.Abs((vRows[k] - vRows[k - 1]) - rowStep) > defaultTol)
                            { rowsEqual = false; break; }
                        }
                        if (!rowsEqual) continue;

                        // 각 멤버를 (iCol, jRow) 로 환산
                        var slotted = new List<Tuple<int, int, PatternItem>>();
                        bool slotOk = true;
                        foreach (var t in coords)
                        {
                            int jRow = -1;
                            for (int k = 0; k < vRows.Count; k++)
                            {
                                if (Math.Abs(t.Item2 - vRows[k]) <= defaultTol) { jRow = k; break; }
                            }
                            if (jRow < 0) { slotOk = false; break; }
                            slotted.Add(Tuple.Create(t.Item1, jRow, t.Item3));
                        }
                        if (!slotOk) continue;

                        int minI = slotted.Min(s => s.Item1);
                        int maxI = slotted.Max(s => s.Item1);
                        int cols = maxI - minI + 1;
                        int rows = vRows.Count;
                        if (rows * cols != slotted.Count) continue;
                        if (rows < 2 || cols < 2) continue;
                        // 각 (i,j) 슬롯이 정확히 하나의 멤버를 가져야 함
                        var slotSet = new HashSet<string>();
                        foreach (var s in slotted)
                        {
                            string key = (s.Item1 - minI) + "," + s.Item2;
                            if (slotSet.Contains(key)) { slotOk = false; break; }
                            slotSet.Add(key);
                        }
                        if (!slotOk) continue;
                        if (slotSet.Count != rows * cols) continue;

                        // 채택!
                        var memberIds = slotted.Select(s => s.Item3.Id).ToList();
                        var positions = slotted.Select(s => s.Item3.Position).ToList();
                        results.Add(new GridResult
                        {
                            MemberIds = memberIds,
                            Centroid = Centroid(positions),
                            DirectionAxis = (double[])uVec.Clone(),
                            ColSpacing = uLen,
                            RowSpacing = rowStep,
                            Rows = rows,
                            Cols = cols,
                            IsPartial = false,
                            ScorePercent = 100.0,
                        });
                        foreach (var id in memberIds) consumed.Add(id);
                        foundGrid = true;
                    }
                }
                if (foundGrid) goto retry_grid;

                // ---- PCA fallback for rotated grids ----
                //   Brute-force pair scan above SHOULD already discover rotated grids in
                //   theory (every adjacent (i,j) pair is tried), but in practice the
                //   tolerance gating around uPlanar/uVec normalization rejects some
                //   rotated cases. Here we use 2D PCA on the in-plane projection to
                //   derive principal axes directly, then snap members to a grid.
                //   This runs only AFTER the brute-force pass fails so axis-aligned
                //   cases (specs 21, 43) remain unchanged.
                var pcaRemaining = grp.Items.Where(it => !consumed.Contains(it.Id)).ToList();
                if (pcaRemaining.Count < 4) continue;
                var pcaGrid = TryDetectGridByPca(pcaRemaining, grp.Axis, defaultTol);
                if (pcaGrid != null && !pcaGrid.MemberIds.Any(id => consumed.Contains(id)))
                {
                    results.Add(pcaGrid);
                    foreach (var id in pcaGrid.MemberIds) consumed.Add(id);
                }
            }
            return results;
        }

        /// <summary>
        /// 2D PCA-based rotated grid detection.
        /// Project points onto axis-perpendicular plane, compute centroid + 2x2 covariance,
        /// solve closed-form 2x2 eigenproblem for principal axes (u, v).
        /// Then snap each point to (iCol, jRow) using u/v projections and verify
        /// equal spacing along both directions and complete rows×cols coverage.
        ///
        /// Returns null if the points do not form a rotated grid.
        ///
        /// ⚠️ Risk: PCA on any non-grid point cluster will still yield some principal
        /// axes. To avoid spurious 2x1 / 1xN "grids" we require:
        ///   - both eigenvalues &gt;= small threshold (otherwise the points are collinear)
        ///   - rows &gt;= 2 AND cols &gt;= 2
        ///   - rows*cols == slotted.Count (no missing slots)
        /// </summary>
        private static GridResult TryDetectGridByPca(List<PatternItem> items, double[] axis, double defaultTol)
        {
            if (items == null || items.Count < 4) return null;

            // Scale-invariant degeneracy thresholds.
            //   eigenFloor : variance floor below which both eigenvalues = "collinear".
            //                Scale by squared spread of points (≈ bbox-diag^2 in xy plane).
            //   spacingFloor : minimum row/col step, scaled by bbox diag.
            double maxExtent2D = 0.0;
            foreach (var it in items)
                for (int kk = 0; kk < 3; kk++)
                    maxExtent2D = Math.Max(maxExtent2D, Math.Abs(it.Position[kk]));
            double bboxLike = Math.Max(maxExtent2D, 1.0);
            double eigenFloor = PcaEigenRatio * bboxLike * bboxLike;
            double spacingFloor = SpacingFloorRatio * bboxLike;

            // Build 2D basis (e1, e2) on the plane perpendicular to axis.
            var e1 = OrthoTo(axis);
            var e2 = Vec3.Cross(axis, e1);

            // Project all points to 2D coordinates (x, y) on (e1, e2).
            // Also reject points whose axial coordinate differs too much from the mean
            // (e.g., counterbore inner/outer of differing depth).
            int n = items.Count;
            var xs = new double[n];
            var ys = new double[n];
            var axials = new double[n];
            for (int k = 0; k < n; k++)
            {
                xs[k] = Vec3.Dot(items[k].Position, e1);
                ys[k] = Vec3.Dot(items[k].Position, e2);
                axials[k] = Vec3.Dot(items[k].Position, axis);
            }
            double axMean = 0;
            for (int k = 0; k < n; k++) axMean += axials[k];
            axMean /= n;
            // 모든 점이 같은 평면에 있어야 함
            for (int k = 0; k < n; k++)
            {
                if (Math.Abs(axials[k] - axMean) > defaultTol) return null;
            }

            // Centroid
            double cx = 0, cy = 0;
            for (int k = 0; k < n; k++) { cx += xs[k]; cy += ys[k]; }
            cx /= n; cy /= n;

            // 2x2 covariance matrix:
            //   [ Sxx Sxy ]
            //   [ Sxy Syy ]
            double Sxx = 0, Syy = 0, Sxy = 0;
            for (int k = 0; k < n; k++)
            {
                double dx = xs[k] - cx;
                double dy = ys[k] - cy;
                Sxx += dx * dx;
                Syy += dy * dy;
                Sxy += dx * dy;
            }
            Sxx /= n; Syy /= n; Sxy /= n;

            // Closed-form 2x2 symmetric eigendecomposition.
            //   λ = (Sxx + Syy)/2 ± sqrt(((Sxx - Syy)/2)^2 + Sxy^2)
            double trace = Sxx + Syy;
            double diff = (Sxx - Syy) * 0.5;
            double rad = Math.Sqrt(diff * diff + Sxy * Sxy);
            double lam1 = trace * 0.5 + rad;
            double lam2 = trace * 0.5 - rad;
            // Degenerate: too small variance in one axis = collinear.
            // Scale-invariant: eigenvalue has units of (mm)^2, compare to bbox-like area.
            if (lam1 < eigenFloor || lam2 < eigenFloor) return null;
            // Principal eigenvector (associated with lam1).
            //   Solve (Sxx - λ) * vx + Sxy * vy = 0
            //   If |Sxy| > eps:  v = (Sxy, λ - Sxx)
            //   Else:            v = (1, 0)  (already diagonal)
            double v1x, v1y;
            // 1e-9: WARNING — covariance-element epsilon (kernel-level numerical guard).
            // Not a feature threshold; do not refactor to ratio.
            if (Math.Abs(Sxy) > 1e-9)
            {
                v1x = Sxy;
                v1y = lam1 - Sxx;
            }
            else
            {
                if (Sxx >= Syy) { v1x = 1; v1y = 0; }
                else { v1x = 0; v1y = 1; }
            }
            double vn = Math.Sqrt(v1x * v1x + v1y * v1y);
            if (vn < 1e-12) return null;
            v1x /= vn; v1y /= vn;
            // Perpendicular: (-v1y, v1x)
            double v2x = -v1y;
            double v2y = v1x;

            // Project each point onto (v1, v2) — these are the rotated row/col coords.
            var rowCoords = new double[n];
            var colCoords = new double[n];
            for (int k = 0; k < n; k++)
            {
                double dx = xs[k] - cx;
                double dy = ys[k] - cy;
                colCoords[k] = dx * v1x + dy * v1y;  // along principal axis (cols)
                rowCoords[k] = dx * v2x + dy * v2y;  // along secondary  (rows)
            }

            // Bin into distinct slots along each axis.
            var colBins = BinSorted(colCoords, defaultTol);
            var rowBins = BinSorted(rowCoords, defaultTol);
            if (colBins == null || rowBins == null) return null;
            if (colBins.Count < 2 || rowBins.Count < 2) return null;

            // Check equal spacing in each direction.
            double colStep = colBins[1] - colBins[0];
            double rowStep = rowBins[1] - rowBins[0];
            // Scale-invariant spacing floor (was 1e-3 mm absolute).
            if (colStep < spacingFloor || rowStep < spacingFloor) return null;
            for (int k = 1; k < colBins.Count; k++)
            {
                if (Math.Abs((colBins[k] - colBins[k - 1]) - colStep) > defaultTol) return null;
            }
            for (int k = 1; k < rowBins.Count; k++)
            {
                if (Math.Abs((rowBins[k] - rowBins[k - 1]) - rowStep) > defaultTol) return null;
            }

            int cols = colBins.Count;
            int rows = rowBins.Count;
            // Reject degenerate 1xN / Nx1.
            if (rows < 2 || cols < 2) return null;
            // Must be a fully populated rectangle.
            if (rows * cols != n) return null;

            // Assign each point to (col, row) slot; reject duplicates / missing slots.
            var slotSet = new HashSet<string>();
            for (int k = 0; k < n; k++)
            {
                int ci = NearestBin(colBins, colCoords[k], defaultTol);
                int ri = NearestBin(rowBins, rowCoords[k], defaultTol);
                if (ci < 0 || ri < 0) return null;
                string key = ci + "," + ri;
                if (slotSet.Contains(key)) return null;
                slotSet.Add(key);
            }
            if (slotSet.Count != rows * cols) return null;

            // Convert 2D principal vector back to 3D for DirectionAxis (encodes rotation).
            var dirAxis3D = new[]
            {
                v1x * e1[0] + v1y * e2[0],
                v1x * e1[1] + v1y * e2[1],
                v1x * e1[2] + v1y * e2[2],
            };

            var memberIds = items.Select(it => it.Id).ToList();
            var positions = items.Select(it => it.Position).ToList();
            return new GridResult
            {
                MemberIds = memberIds,
                Centroid = Centroid(positions),
                DirectionAxis = dirAxis3D,
                ColSpacing = Math.Abs(colStep),
                RowSpacing = Math.Abs(rowStep),
                Rows = rows,
                Cols = cols,
                IsPartial = false,
                ScorePercent = 100.0,
            };
        }

        /// <summary>
        /// Sort values, then collapse runs within tol into a single representative
        /// (mean of the run). Returns sorted distinct bin centers.
        /// </summary>
        private static List<double> BinSorted(double[] values, double tol)
        {
            if (values == null || values.Length == 0) return null;
            var sorted = values.OrderBy(v => v).ToList();
            var bins = new List<double>();
            var current = new List<double> { sorted[0] };
            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i] - current[current.Count - 1] <= tol)
                {
                    current.Add(sorted[i]);
                }
                else
                {
                    bins.Add(current.Average());
                    current = new List<double> { sorted[i] };
                }
            }
            bins.Add(current.Average());
            return bins;
        }

        private static int NearestBin(List<double> bins, double v, double tol)
        {
            for (int i = 0; i < bins.Count; i++)
            {
                if (Math.Abs(bins[i] - v) <= tol) return i;
            }
            return -1;
        }

        // --- Group by axis ---

        private class AxisGroup
        {
            public double[] Axis;
            public List<PatternItem> Items;
        }

        private static List<AxisGroup> GroupByAxis(List<PatternItem> items)
        {
            var groups = new List<AxisGroup>();
            foreach (var it in items)
            {
                AxisGroup matched = null;
                foreach (var g in groups)
                {
                    double dot = Math.Abs(Vec3.Dot(g.Axis, it.Axis));
                    if (dot > 0.99) { matched = g; break; }
                }
                if (matched == null)
                {
                    matched = new AxisGroup { Axis = Normalize(it.Axis), Items = new List<PatternItem>() };
                    groups.Add(matched);
                }
                matched.Items.Add(it);
            }
            return groups;
        }

        // ---------- Symmetry helpers ----------

        /// <summary>
        /// origin 을 지나고 normal 을 법선으로 하는 평면에 대해 points 의 mirror match 비율.
        /// 각 point 의 mirror 가 tol 이내 다른 point 와 매칭되면 +1.
        /// 자기 자신이 평면 위에 있으면 (거리 < tol) 자동 +1.
        /// </summary>
        private static double MirrorScore(List<double[]> points, double[] origin, double[] normal, double tol)
        {
            int hits = 0;
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                var d = Vec3.Sub(p, origin);
                double dist = Vec3.Dot(d, normal);
                if (Math.Abs(dist) <= tol) { hits++; continue; }
                var mirror = Vec3.Sub(p, Vec3.Scale(normal, 2.0 * dist));
                bool found = false;
                for (int j = 0; j < points.Count; j++)
                {
                    if (i == j) continue;
                    if (Vec3.Norm(Vec3.Sub(points[j], mirror)) <= tol) { found = true; break; }
                }
                if (found) hits++;
            }
            return 100.0 * hits / Math.Max(1, points.Count);
        }

        // ---------- Vec helpers ----------

        private static double[] Centroid(List<double[]> pts)
        {
            double x = 0, y = 0, z = 0;
            foreach (var p in pts) { x += p[0]; y += p[1]; z += p[2]; }
            int n = Math.Max(1, pts.Count);
            return new[] { x / n, y / n, z / n };
        }

        private static double[] Normalize(double[] v)
        {
            double n = Vec3.Norm(v);
            if (n < 1e-12) return new[] { 0.0, 0.0, 1.0 };
            return new[] { v[0] / n, v[1] / n, v[2] / n };
        }

        private static double[] OrthoTo(double[] axis)
        {
            // axis 와 직교하는 단위 벡터 하나
            double[] candidate = Math.Abs(axis[2]) < 0.9 ? new[] { 0.0, 0.0, 1.0 } : new[] { 1.0, 0.0, 0.0 };
            var u = Vec3.Sub(candidate, Vec3.Scale(axis, Vec3.Dot(candidate, axis)));
            return Normalize(u);
        }

        private static class Vec3
        {
            public static double[] Sub(double[] a, double[] b) =>
                new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };
            public static double[] Scale(double[] a, double s) =>
                new[] { a[0] * s, a[1] * s, a[2] * s };
            public static double Dot(double[] a, double[] b) =>
                a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
            public static double[] Cross(double[] a, double[] b) =>
                new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };
            public static double Norm(double[] a) =>
                Math.Sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2]);
        }
    }
}
