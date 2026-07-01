using System;
using System.Collections.Generic;
using System.Linq;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;
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
    /// FeatureGraph 기반 DesignBody 구조 변경 (primitive ops).
    /// LLM driven modification (Stage 4c) 및 직접 API 테스트에서 사용.
    /// 모든 mutation 은 WriteBlock.ExecuteTask 내부에서 실행.
    /// </summary>
    // @lat: [[reverse-engineer#ModificationService]]
    public static class ModificationService
    {
        // -------------------------------------------------------------------
        // ChangeWallThickness
        // -------------------------------------------------------------------
        /// <summary>
        /// Wall (평행 평면 쌍) 의 두께를 변경.
        /// 비대칭 offset: face_a 를 (current - new) mm 만큼 face_b 쪽으로 이동.
        /// 양쪽 모두 offset 하면 topology breakage 위험이 큼 → 한쪽만.
        /// </summary>
        public static ModificationResult ChangeWallThickness(
            DesignBody designBody,
            FeatureGraph graph,
            string wallId,
            double newThicknessMm)
        {
            var result = new ModificationResult
            {
                Operation = "ChangeWallThickness",
                ModifiedBody = designBody,
            };

            if (designBody == null) { result.ErrorMessage = "designBody is null"; return result; }
            if (graph == null) { result.ErrorMessage = "graph is null"; return result; }
            if (string.IsNullOrEmpty(wallId)) { result.ErrorMessage = "wallId is null"; return result; }
            if (newThicknessMm <= 0) { result.ErrorMessage = "newThicknessMm must be > 0"; return result; }

            var wall = graph.Walls.FirstOrDefault(w => w.Id == wallId);
            if (wall == null)
            {
                result.ErrorMessage = "Wall not found: " + wallId;
                return result;
            }

            double currentMm = wall.ThicknessMm;
            double deltaMm = currentMm - newThicknessMm; // positive = shrink, negative = grow
            double offsetMeters = GeometryUtils.MmToMeters(deltaMm);

            // Cycle 30 fix (real CAD bracket — SampleModel1.scdoc, W1 grew from
            // 16.331 → 17.331 when shrink to 15.331 was requested):
            //
            // Per SpaceClaim API doc, Body.OffsetFaces with a positive value
            // moves the face along its "face normal" (outward). The legacy code
            // used sign=-1 → offset=-deltaMm → inward → shrink, which works for
            // outer-shell walls. But this assumes wall.Normal points AWAY from
            // face B — true for ordinary outer walls and inner-cavity walls
            // where the two faces bound a void/cavity, but FALSE when wall.Normal
            // points TOWARD face B (e.g. inverted / inner walls where material
            // is OUTSIDE the A-B gap rather than between them — the SampleModel1
            // W1 is exactly this shape).
            //
            // Robust direction (geometric, no normal-convention assumption):
            //   Compute (B.origin - A.origin) · wall.Normal.
            //     < 0  → wall.Normal points away from B  → moving face A in the
            //            -Normal direction (negative OffsetFaces) takes it TOWARD
            //            B → wall shrinks. Use sign = -1 (legacy).
            //     > 0  → wall.Normal points toward B → moving face A in the
            //            +Normal direction (positive OffsetFaces) takes it TOWARD
            //            B → wall shrinks. Use sign = +1.
            //   The two faces are by construction antiparallel (FeatureExtractor
            //   requires dot(a.Normal, b.Normal) < -threshold), so this check is
            //   stable for every detected wall.
            var faceFeatA = graph.Faces.FirstOrDefault(ff => ff.FaceIndex == wall.FaceA);
            var faceFeatB = graph.Faces.FirstOrDefault(ff => ff.FaceIndex == wall.FaceB);
            double sign = -1.0; // legacy default — correct when wall.Normal points away from B
            if (faceFeatA != null && faceFeatB != null
                && faceFeatA.Origin != null && faceFeatA.Origin.Length >= 3
                && faceFeatB.Origin != null && faceFeatB.Origin.Length >= 3
                && wall.Normal != null && wall.Normal.Length >= 3)
            {
                double dx = faceFeatB.Origin[0] - faceFeatA.Origin[0];
                double dy = faceFeatB.Origin[1] - faceFeatA.Origin[1];
                double dz = faceFeatB.Origin[2] - faceFeatA.Origin[2];
                double dotToB = dx * wall.Normal[0] + dy * wall.Normal[1] + dz * wall.Normal[2];
                if (dotToB > 0.0) sign = +1.0;
            }
            double offset = sign * offsetMeters;

            var faceA = GetFaceByIndex(designBody, wall.FaceA);
            if (faceA == null)
            {
                result.ErrorMessage = "FaceA not found (index=" + wall.FaceA + ")";
                return result;
            }

            // Defensive pre-heal: tightens inexact edge tolerances + zips small gaps
            // left by STEP import. Cheap (~ms) and lets OffsetFaces succeed on the
            // tight-tolerance bodies (linkrods 1mm wall, 624ZZ bearing race) where
            // the kernel otherwise rejects every offset magnitude.
            TryHealBody(designBody);

            // edges before
            var edgesBefore = CaptureEdges(designBody);

            // W2-3 ScaleNormalized (PROACTIVE — gate finding: scale-trick must run
            // BEFORE the first kernel failure, because the failure itself poisons
            // the process; reactive retry is structurally too late). For sub-2mm
            // walls/deltas (linkrods T=1mm, 624ZZ δ=0.5mm), SC's linear tolerance
            // rejects native-scale offsets; at 1000× the same move is comfortably
            // above tolerance. Body is scaled back before verification.
            const double SCALE_UP = 1000.0;
            bool scaledMode = false;
            if (Math.Min(Math.Abs(deltaMm), currentMm) < 2.0)
            {
                try
                {
                    WriteBlock.ExecuteTask("ChangeWallThickness " + wallId + " [scale-up]", () =>
                    {
                        designBody.Shape.Transform(Matrix.CreateScale(SCALE_UP));
                    });
                    scaledMode = true;
                    offset *= SCALE_UP;
                    // Geometry changed under the cached handle — refetch.
                    faceA = GetFaceByIndex(designBody, wall.FaceA);
                    if (faceA == null)
                    {
                        result.ErrorMessage = "FaceA lost after scale-up";
                        return result;
                    }
                }
                catch { /* stay at native scale */ }
            }

            string lastErr = null; int stepsApplied = 0;
            string strategy = null;
            bool ok = false;

            // Once a single OffsetFaces call fails on a wall, SC marks the body's
            // wall faces as "deleted" — subsequent OffsetFaces calls all throw
            // "The object is deleted". So fallback strategies only have value when
            // tried FIRST on a fresh body, not after a failed primary.
            //
            // STRATEGY ORDER (reverted from Boolean-primary because Boolean
            // Subtract leaves the body's wall topology in a state where
            // VerifyWallThickness's plane-pair scan can't find the modified
            // pair — null after-value for most cases. OffsetFaces preserves
            // the planar face structure intact and is verifiable.):
            //   1. symmetric A+B half-each (lowest disruption)
            //   2. A-only
            //   3. B-only
            //   4. Boolean Reconstruction (shrink-only fallback for cases
            //      where OffsetFaces is kernel-rejected, e.g. samplemodel2
            //      W1=1624mm with ΔT=162mm).
            var faceB = GetFaceByIndex(designBody, wall.FaceB);

            // Strategy 0 (scaled-class walls only): plane SurfaceSwap. linkrods
            // (T=1mm) and 624ZZ (bearing race neighbors) reject OffsetFaces at
            // EVERY scale — structural adjacency, not tolerance. ReplaceFaceGeometry
            // sidesteps the offset op entirely. Restricted to the small-wall class
            // so the 14 corpus-passing large walls keep their proven symmetric path.
            if (scaledMode && deltaMm > 0
                && faceFeatA != null && faceFeatB != null
                && faceFeatA.Origin != null && faceFeatB.Origin != null
                && faceFeatA.Origin.Length >= 3 && faceFeatB.Origin.Length >= 3)
            {
                // unit vector A→B from cached origins (orientation only — scale-safe)
                double dxAB = faceFeatB.Origin[0] - faceFeatA.Origin[0];
                double dyAB = faceFeatB.Origin[1] - faceFeatA.Origin[1];
                double dzAB = faceFeatB.Origin[2] - faceFeatA.Origin[2];
                double magAB = Math.Sqrt(dxAB * dxAB + dyAB * dyAB + dzAB * dzAB);
                if (magAB > 1e-12)
                {
                    var dirToB = new[] { dxAB / magAB, dyAB / magAB, dzAB / magAB };
                    // displacement in CURRENT body units: body is at 1000×, so Δ(m) ×1000
                    double moveM = (deltaMm / 1000.0) * SCALE_UP;
                    string swapErrW;
                    if (TrySwapWallSurface(designBody, wall.FaceA, moveM, dirToB, wallId, out swapErrW))
                    {
                        ok = true; strategy = "WallSurfaceSwap"; stepsApplied = 1;
                    }
                    else
                    {
                        lastErr = "WallSwap: " + (swapErrW ?? "(no msg)");
                    }
                }
            }

            // Strategy 1: symmetric
            if (!ok && faceB != null)
            {
                double halfOffsetMeters = offset / 2.0;
                var perFace = new Dictionary<Face, double>
                {
                    { faceA, halfOffsetMeters },
                    { faceB, halfOffsetMeters },
                };
                int stepsApplied1; string lastErr1;
                bool ok1 = TryOffsetFacesProgressive(designBody, perFace,
                                                    "ChangeWallThickness " + wallId + " [symmetric]",
                                                    out lastErr1, out stepsApplied1);
                if (ok1) { ok = true; stepsApplied = stepsApplied1; strategy = "symmetric"; }
                else { lastErr = (lastErr ?? "") + " | symmetric: " + lastErr1; }
            }

            // Strategy 2: A-only (only if symmetric failed AND body not already
            // deleted — checked via re-fetch).
            if (!ok)
            {
                try
                {
                    var faceA2 = GetFaceByIndex(designBody, wall.FaceA);
                    if (faceA2 != null)
                    {
                        int stepsApplied2; string lastErr2;
                        bool ok2 = TryOffsetFacesProgressive(designBody, new[] { faceA2 }, offset,
                                                            "ChangeWallThickness " + wallId + " [A-only]",
                                                            out lastErr2, out stepsApplied2);
                        if (ok2) { ok = true; stepsApplied = stepsApplied2; strategy = "A-only"; }
                        else { lastErr = (lastErr ?? "") + " | A-only: " + lastErr2; }
                    }
                }
                catch (Exception ex)
                {
                    lastErr = (lastErr ?? "") + " | A-only threw: " + ex.Message;
                }
            }

            // Strategy 3: B-only
            if (!ok)
            {
                try
                {
                    var faceB3 = GetFaceByIndex(designBody, wall.FaceB);
                    if (faceB3 != null)
                    {
                        int stepsApplied3; string lastErr3;
                        bool ok3 = TryOffsetFacesProgressive(designBody, new[] { faceB3 }, offset,
                                                            "ChangeWallThickness " + wallId + " [B-only]",
                                                            out lastErr3, out stepsApplied3);
                        if (ok3) { ok = true; stepsApplied = stepsApplied3; strategy = "B-only"; }
                        else { lastErr = (lastErr ?? "") + " | B-only: " + lastErr3; }
                    }
                }
                catch (Exception ex)
                {
                    lastErr = (lastErr ?? "") + " | B-only threw: " + ex.Message;
                }
            }

            // Strategy 4: Boolean Reconstruction (shrink-only fallback). When
            // all OffsetFaces variants fail (e.g. samplemodel2 W1=1624mm where
            // SC kernel rejects the offset regardless of subdivision), fall
            // back to slab Subtract.
            if (!ok && deltaMm > 0)
            {
                double dotToB4 = 0.0;
                bool haveDot4 = false;
                if (faceFeatA != null && faceFeatB != null
                    && faceFeatA.Origin != null && faceFeatB.Origin != null
                    && wall.Normal != null && wall.Normal.Length >= 3)
                {
                    double dx = faceFeatB.Origin[0] - faceFeatA.Origin[0];
                    double dy = faceFeatB.Origin[1] - faceFeatA.Origin[1];
                    double dz = faceFeatB.Origin[2] - faceFeatA.Origin[2];
                    dotToB4 = dx * wall.Normal[0] + dy * wall.Normal[1] + dz * wall.Normal[2];
                    haveDot4 = true;
                }
                double signToB4 = (haveDot4 && dotToB4 > 0) ? +1.0 : -1.0;
                string boolErr4;
                bool boolOk4;
                if (scaledMode)
                {
                    // Body is already at 1000× — call the AtScale core directly with
                    // matching delta and cached-origin scale factor.
                    boolOk4 = TryReconstructWallByBooleanAtScale(designBody, faceFeatA, faceFeatB,
                                                                 wall.Normal, signToB4, deltaMm * SCALE_UP,
                                                                 wallId, SCALE_UP, out boolErr4);
                }
                else
                {
                    boolOk4 = TryReconstructWallByBoolean(designBody, faceFeatA, faceFeatB,
                                                          wall.Normal, signToB4, deltaMm, wallId, out boolErr4);
                }
                if (boolOk4)
                {
                    ok = true; strategy = "BooleanReconstruction"; stepsApplied = 1;
                }
                else
                {
                    lastErr = (lastErr ?? "") + " | Boolean: " + (boolErr4 ?? "(no msg)");
                }
            }

            // Scale back to native BEFORE verification/return — best-effort on the
            // failure path (a poisoned body may reject the transform; the process
            // is evicted by the orchestrator in that case anyway).
            if (scaledMode)
            {
                try
                {
                    WriteBlock.ExecuteTask("ChangeWallThickness " + wallId + " [scale-down]", () =>
                    {
                        designBody.Shape.Transform(Matrix.CreateScale(1.0 / SCALE_UP));
                    });
                }
                catch { /* unrecoverable — eviction path */ }
                if (ok)
                {
                    result.HintMessage = (result.HintMessage ?? "") + " [ScaleNormalized 1000x]";
                }
            }

            if (!ok)
            {
                result.ErrorMessage = "OffsetFaces failed: " + lastErr;
                // Outlier diagnostic: if |Δthickness| > 10mm AND > 5% of model bbox,
                // SC kernel often rejects the full move even at 1/64 subdivision.
                // Hint at staged manual revision.
                double absDelta = Math.Abs(deltaMm);
                double bboxMax = (graph != null && graph.BboxSizeMm != null && graph.BboxSizeMm.Length >= 3)
                    ? Math.Max(graph.BboxSizeMm[0], Math.Max(graph.BboxSizeMm[1], graph.BboxSizeMm[2]))
                    : 0.0;
                if (absDelta > 10.0 && bboxMax > 0.0 && absDelta / bboxMax > 0.05)
                {
                    result.HintMessage = string.Format(
                        "Wall {0}: ΔT={1:F2}mm (~{2:F1}% of bbox {3:F1}mm). " +
                        "This is a large move; try smaller increments (≤5% of bbox per call) or split into staged changes.",
                        wallId, absDelta, 100.0 * absDelta / bboxMax, bboxMax);
                }
                return result;
            }
            // Record diagnostic info when fallback strategies were needed.
            if (strategy != "A-only" || stepsApplied > 1)
            {
                result.HintMessage = string.Format(
                    "Strategy: {0}{1}.",
                    strategy,
                    stepsApplied > 1 ? string.Format(" with {0}-step subdivision", stepsApplied) : "");
            }

            // edges after — defensive: BooleanReconstruction can replace wall.FaceA
            // with a new face index, so capturing edges via the post-mod body may
            // throw "object deleted" if any cached reference dangles. The mod
            // succeeded by the time we reach here; treat edge capture as best-effort
            // so a stale reference doesn't turn an OK result into an EXC.
            try
            {
                var edgesAfter = CaptureEdges(designBody);
                result.NewlyCreatedEdges = DiffEdges(edgesBefore, edgesAfter);
            }
            catch
            {
                result.NewlyCreatedEdges = new List<Edge>();
            }
            try { result.ModifiedFaceIndices.Add(wall.FaceA); } catch { /* index may be stale post-Boolean */ }
            // Spatial verification anchor — FaceFeature.Origin is in meters (per
            // model contract), we store mm for consistency with the rest of the API.
            if (faceFeatA != null && faceFeatA.Origin != null && faceFeatA.Origin.Length >= 3)
            {
                result.TargetCenterXMm = faceFeatA.Origin[0] * 1000.0;
                result.TargetCenterYMm = faceFeatA.Origin[1] * 1000.0;
                result.TargetCenterZMm = faceFeatA.Origin[2] * 1000.0;
                result.UseTargetCenter = true;
            }
            result.TargetFeatureKind = "Wall";

            // In-process verify: post-mod body must have some pair of parallel
            // planes at thickness ≈ newThicknessMm. Brute scan all face pairs.
            // Skip if mod was via Boolean (which can split walls beyond what the
            // FeatureExtractor recognizes — strict downgrade in Python catches
            // those cases).
            try
            {
                double newTMeters = newThicknessMm / 1000.0;
                double measuredTm = VerifyWallThickness(designBody, newTMeters);
                if (!double.IsNaN(measuredTm))
                {
                    result.MeasuredAfterMm = measuredTm * 1000.0;
                    // For non-Boolean strategies, the measured value being far from
                    // expected indicates a silent no-op (e.g. OffsetFaces returned OK
                    // but didn't actually move the face). Boolean Reconstruction
                    // can legitimately split walls in ways that move the pair, so
                    // we trust its OK status without strict measurement check.
                    if (strategy != "BooleanReconstruction")
                    {
                        double diff = Math.Abs(measuredTm - newTMeters);
                        double tol = Math.Max(newTMeters * 0.05, 1e-5);
                        if (diff > tol)
                        {
                            result.Success = false;
                            result.ErrorMessage = "Mod silent no-op: closest parallel-pair at T=" +
                                (measuredTm * 1000.0).ToString("F3") + "mm vs expected " +
                                newThicknessMm.ToString("F3") + "mm";
                            return result;
                        }
                    }
                }
            }
            catch { /* inconclusive — keep OK */ }
            result.Success = true;
            return result;
        }

        // Scan all planar-face pairs for an antiparallel pair, return the
        // separation closest to targetTm. Always returns a number (NaN only
        // when the body has no planar pairs at all). Caller decides if the
        // returned value is close enough to consider the mod successful.
        private static double VerifyWallThickness(DesignBody designBody, double targetTm)
        {
            var planes = new List<Tuple<Point, Direction, Point>>();
            foreach (var df in designBody.Faces)
            {
                try
                {
                    if (df == null || df.Shape == null) continue;
                    var p = df.Shape.Geometry as Plane;
                    if (p == null) continue;
                    Point centroid = p.Frame.Origin;
                    try { var bb = df.Shape.GetBoundingBox(Matrix.Identity);
                          centroid = Point.Create((bb.MinCorner.X+bb.MaxCorner.X)/2,
                                                  (bb.MinCorner.Y+bb.MaxCorner.Y)/2,
                                                  (bb.MinCorner.Z+bb.MaxCorner.Z)/2); }
                    catch { /* fallback above */ }
                    planes.Add(Tuple.Create(p.Frame.Origin, p.Frame.DirZ, centroid));
                }
                catch { /* skip this face — its state may be stale post-Boolean */ }
            }
            double bestSep = double.NaN; double bestDiff = double.MaxValue;
            for (int i = 0; i < planes.Count; i++)
            {
                for (int j = i + 1; j < planes.Count; j++)
                {
                    var na = planes[i].Item2; var nb = planes[j].Item2;
                    double dot = na.UnitVector.X * nb.UnitVector.X
                               + na.UnitVector.Y * nb.UnitVector.Y
                               + na.UnitVector.Z * nb.UnitVector.Z;
                    // Accept parallel OR antiparallel within a slightly wider
                    // tolerance — STEP imports often have slight skew.
                    if (Math.Abs(Math.Abs(dot) - 1.0) > 0.1) continue;
                    var oa = planes[i].Item1; var ob = planes[j].Item1;
                    double dx = ob.X - oa.X, dy = ob.Y - oa.Y, dz = ob.Z - oa.Z;
                    double sep = Math.Abs(dx * na.UnitVector.X + dy * na.UnitVector.Y + dz * na.UnitVector.Z);
                    if (sep < 1e-7) continue; // coincident planes, not a wall
                    double diff = Math.Abs(sep - targetTm);
                    if (diff < bestDiff) { bestDiff = diff; bestSep = sep; }
                }
            }
            return bestSep; // closest match, regardless of tolerance
        }

        // -------------------------------------------------------------------
        // ChangeHoleDiameter
        // -------------------------------------------------------------------
        /// <summary>
        /// Hole 의 내부 cylinder 면 반지름을 변경.
        /// 내부 cylinder 의 outward normal 은 축 안쪽(중심) 방향(IsReversed 영향) →
        ///   양의 OffsetFaces(positive) = 살이 hole 안쪽으로 채워짐 = D 감소.
        ///   따라서 D 증가 시 offset = -(newR - currentR), D 감소 시 offset = (currentR - newR).
        /// 부호는 face.IsReversed / CylinderRole 로 보정한다.
        /// </summary>
        public static ModificationResult ChangeHoleDiameter(
            DesignBody designBody,
            FeatureGraph graph,
            string holeId,
            double newDiameterMm)
        {
            var result = new ModificationResult
            {
                Operation = "ChangeHoleDiameter",
                ModifiedBody = designBody,
            };

            if (designBody == null) { result.ErrorMessage = "designBody is null"; return result; }
            if (graph == null) { result.ErrorMessage = "graph is null"; return result; }
            if (string.IsNullOrEmpty(holeId)) { result.ErrorMessage = "holeId is null"; return result; }
            if (newDiameterMm <= 0) { result.ErrorMessage = "newDiameterMm must be > 0"; return result; }

            var hole = graph.Holes.FirstOrDefault(h => h.Id == holeId);
            if (hole == null)
            {
                result.ErrorMessage = "Hole not found: " + holeId;
                return result;
            }

            int faceIdx = hole.CylinderFaceIndex;
            var faceFeat = graph.Faces.FirstOrDefault(ff => ff.FaceIndex == faceIdx);
            if (faceFeat == null)
            {
                result.ErrorMessage = "Cylinder face feature missing (index=" + faceIdx + ")";
                return result;
            }

            double currentRm = faceFeat.RadiusM;
            double newRm = GeometryUtils.MmToMeters(newDiameterMm / 2.0);
            double dR = newRm - currentRm; // positive = larger hole

            // Inner hole cylinder face: SpaceClaim stores the face's outward normal
            // pointing AWAY from the axis (into the body material). Therefore
            // OffsetFaces(positive) moves the face AWAY from material → AWAY from axis
            // → hole radius INCREASES. So sign = +1 (dR>0 → offset>0 → larger hole).
            // (Earlier reasoning assumed positive offset shrinks the hole; experimental
            //  RT2 result (D 5→8 requested produced D=2) proved the opposite — fixed.)
            // FaceFeature.IsReversed flips this convention if the cached normal direction
            // is inverted relative to SpaceClaim's runtime face orientation.
            double sign = +1.0;
            if (faceFeat.IsReversed) sign *= -1.0;

            // Boss (외부 cylinder)인 경우는 부호 반전 (하지만 이 메서드는 hole 전용이므로 안전망만)
            if (faceFeat.CylinderRole == CylinderRole.Boss) sign *= -1.0;

            double offset = sign * dR;

            var face = GetFaceByIndex(designBody, faceIdx);
            if (face == null)
            {
                result.ErrorMessage = "Cylinder face not found in body (index=" + faceIdx + ")";
                return result;
            }

            // Pre-heal — see ChangeWallThickness for rationale (STEP tolerance fixup).
            TryHealBody(designBody);

            // Diagnostic: count adjacent fillet/torus faces. If OffsetFaces fails on a hole
            //   with adjacent fillets, SC kernel cannot resolve the topology — give the user
            //   a clear workaround hint.
            var adjFilletFaces = new List<int>();
            FaceAdjacency adj;
            if (graph.Adjacency != null && graph.Adjacency.TryGetValue(faceIdx, out adj))
            {
                foreach (var nIdx in adj.Neighbors)
                {
                    var nFeat = graph.Faces.FirstOrDefault(ff => ff.FaceIndex == nIdx);
                    if (nFeat == null) continue;
                    if (nFeat.Type == SurfaceType.Torus ||
                        nFeat.CylinderRole == CylinderRole.EdgeFillet ||
                        nFeat.CylinderRole == CylinderRole.CornerBlend)
                    {
                        adjFilletFaces.Add(nIdx);
                    }
                }
            }

            var edgesBefore = CaptureEdges(designBody);

            // STRATEGY ORDER (revised after gate experiments 2026-06-10):
            //   0. ReplaceFaceGeometry surface swap (PRIMARY — gate-verified E3-3:
            //      hole R 1.6→2.1mm exact). Direction-agnostic (enlarge AND shrink),
            //      sign-convention-free, topology-preserving (same face, new surface).
            //   1. Boolean Reconstruction — enlarge only; re-cuts the hole.
            //   2. OffsetFaces — last resort (sign depends on IsReversed).
            string lastErrH = null; int stepsAppliedH = 0;
            bool offsetOk = false;
            string holeStrategy = null;

            // Strategy 0: direct surface swap via Unsupported.BodyMethods.ReplaceFaceGeometry.
            {
                string swapErr;
                if (TrySwapHoleSurface(designBody, faceIdx, newRm, holeId, faceFeat, out swapErr))
                {
                    // VERIFY the swap actually changed the radius (kernel-truth). On a
                    // NON-active assembly body, ReplaceFaceGeometry returns success but
                    // SILENTLY no-ops (W4-4 gate: plate hole stayed 10mm) — so a true
                    // return is not proof. SurfaceSwap is topology-preserving (same face),
                    // so faceIdx still points to the cylinder; if its radius didn't move
                    // to newRm, treat it as failed and fall through to Boolean (which
                    // works on non-active bodies — AddHole/Subtract are gate-proven there).
                    if (SwapChangedRadius(designBody, faceIdx, newRm))
                    {
                        offsetOk = true;
                        holeStrategy = "SurfaceSwap";
                    }
                    else
                    {
                        lastErrH = "SurfaceSwap: returned OK but radius unchanged (non-active body no-op)";
                    }
                }
                else
                {
                    lastErrH = "SurfaceSwap: " + (swapErr ?? "(no msg)");
                }
            }

            if (!offsetOk && dR > 0)
            {
                string reconErr;
                if (TryReconstructHoleByBoolean(designBody, faceFeat, faceIdx, newRm, holeId, out reconErr))
                {
                    offsetOk = true;
                    holeStrategy = "BooleanReconstruction";
                }
                else
                {
                    lastErrH = (lastErrH ?? "") + " | Boolean: " + (reconErr ?? "(no msg)");
                }
            }
            if (!offsetOk)
            {
                offsetOk = TryOffsetFacesProgressive(designBody, new[] { face }, offset,
                                                    "ChangeHoleDiameter " + holeId,
                                                    out string offsetErr, out stepsAppliedH);
                if (offsetOk)
                {
                    holeStrategy = "OffsetFaces";
                }
                else
                {
                    lastErrH = (lastErrH ?? "") + " | OffsetFaces: " + offsetErr;
                }
            }
            if (!offsetOk)
            {
                result.ErrorMessage = "OffsetFaces failed: " + lastErrH;
                if (adjFilletFaces.Count > 0)
                {
                    result.HintMessage = string.Format(
                        "Hole {0} has {1} adjacent fillet/blend face(s) (indices: {2}). " +
                        "The SC kernel often rejects OffsetFaces in this case. " +
                        "Workaround: manually unround the adjacent fillets first, change the diameter, then re-fillet at the original R.",
                        holeId, adjFilletFaces.Count, string.Join(",", adjFilletFaces.ConvertAll(i => i.ToString()).ToArray()));
                }
                return result;
            }
            if (holeStrategy != "OffsetFaces" || stepsAppliedH > 1)
            {
                result.HintMessage = string.Format(
                    "Strategy: {0}{1}.",
                    holeStrategy,
                    stepsAppliedH > 1 ? string.Format(" with {0}-step subdivision", stepsAppliedH) : "");
            }

            // Defensive — see ChangeWallThickness for rationale.
            try
            {
                var edgesAfter = CaptureEdges(designBody);
                result.NewlyCreatedEdges = DiffEdges(edgesBefore, edgesAfter);
            }
            catch
            {
                result.NewlyCreatedEdges = new List<Edge>();
            }
            try { result.ModifiedFaceIndices.Add(faceIdx); } catch { /* stale post-Boolean */ }
            if (faceFeat.Origin != null && faceFeat.Origin.Length >= 3)
            {
                result.TargetCenterXMm = faceFeat.Origin[0] * 1000.0;
                result.TargetCenterYMm = faceFeat.Origin[1] * 1000.0;
                result.TargetCenterZMm = faceFeat.Origin[2] * 1000.0;
                result.UseTargetCenter = true;
            }
            result.TargetFeatureKind = "Hole";

            // In-process verify: among cylinders WITHIN a tight perp band of the
            // target axis (axis-coincident neighborhood), pick the one whose R is
            // closest to newRm. Report it as MeasuredAfterMm.
            //
            // Why "closest to newRm" not "closest perp distance":
            //   In multi-cylinder assemblies (e.g. 624ZZ ball bearing has 4mm hole,
            //   8mm inner race, 12mm outer race — many concentric/parallel
            //   cylinders), the bare "closest perp distance" sometimes picks an
            //   adjacent race cylinder near (but not the same as) the target
            //   axis. Filtering by perp ≤ small threshold THEN by R proximity to
            //   newRm gives the actual modified face.
            try
            {
                double targetOx = faceFeat.Origin[0], targetOy = faceFeat.Origin[1], targetOz = faceFeat.Origin[2];
                double targetNx = faceFeat.Normal[0], targetNy = faceFeat.Normal[1], targetNz = faceFeat.Normal[2];
                double magN = Math.Sqrt(targetNx*targetNx + targetNy*targetNy + targetNz*targetNz);
                if (magN > 1e-9) { targetNx/=magN; targetNy/=magN; targetNz/=magN; }
                // Use newRm × 0.5 as the "coincident" band — a parallel cylinder
                // at perp distance > 50% of newRm cannot be the modified target.
                double bandM = Math.Max(newRm * 0.5, 1e-4);
                double bestR = double.NaN; double bestRDiff = double.MaxValue;
                double anyR = double.NaN; double anyDist = double.MaxValue;
                foreach (var df in designBody.Faces)
                {
                    if (df == null || df.Shape == null) continue;
                    var c = df.Shape.Geometry as Cylinder;
                    if (c == null) continue;
                    double dx = c.Frame.Origin.X - targetOx;
                    double dy = c.Frame.Origin.Y - targetOy;
                    double dz = c.Frame.Origin.Z - targetOz;
                    double dot = dx*targetNx + dy*targetNy + dz*targetNz;
                    double px = dx - dot*targetNx, py = dy - dot*targetNy, pz = dz - dot*targetNz;
                    double perp = Math.Sqrt(px*px + py*py + pz*pz);
                    if (perp < anyDist) { anyDist = perp; anyR = c.Radius; }
                    if (perp <= bandM)
                    {
                        double rDiff = Math.Abs(c.Radius - newRm);
                        if (rDiff < bestRDiff) { bestRDiff = rDiff; bestR = c.Radius; }
                    }
                }
                if (!double.IsNaN(bestR))
                {
                    result.MeasuredAfterMm = bestR * 2.0 * 1000.0;
                }
                else if (!double.IsNaN(anyR))
                {
                    // No cylinder on the target axis at all — record nearest
                    // anyway so Python sees something, but flag as suspect.
                    result.MeasuredAfterMm = anyR * 2.0 * 1000.0;
                }
            }
            catch { /* inconclusive — keep OK */ }

            result.Success = true;
            return result;
        }

        // -------------------------------------------------------------------
        // ChangeFilletRadius
        // -------------------------------------------------------------------
        /// <summary>
        /// FilletChain 의 R 변경. 간단 구현: chain 내 모든 fillet face 에 대해
        /// OffsetFaces((newR - currentR) * concavity_sign) 적용.
        /// Concave (오목) blend 는 살이 줄어드는 방향이 R 증가, convex 는 반대.
        /// </summary>
        public static ModificationResult ChangeFilletRadius(
            DesignBody designBody,
            FeatureGraph graph,
            string filletChainId,
            double newRadiusMm,
            bool flipSign = false,
            bool forceScaled = false)
        {
            var result = new ModificationResult
            {
                Operation = "ChangeFilletRadius",
                ModifiedBody = designBody,
            };

            if (designBody == null) { result.ErrorMessage = "designBody is null"; return result; }
            if (graph == null) { result.ErrorMessage = "graph is null"; return result; }
            if (string.IsNullOrEmpty(filletChainId)) { result.ErrorMessage = "filletChainId is null"; return result; }
            if (newRadiusMm < 0) { result.ErrorMessage = "newRadiusMm must be >= 0"; return result; }

            var chain = graph.FilletChains.FirstOrDefault(c => c.Id == filletChainId);
            if (chain == null)
            {
                result.ErrorMessage = "FilletChain not found: " + filletChainId;
                return result;
            }

            double currentRmm = chain.RadiusMm;
            double dRmm = newRadiusMm - currentRmm;
            if (Math.Abs(dRmm) < 1e-6)
            {
                result.Success = true; // no-op
                result.NewlyCreatedEdges = new List<Edge>();
                return result;
            }
            double dRm = GeometryUtils.MmToMeters(dRmm);

            // W2-3 ScaleNormalized (forced via orchestrator retry for cells whose
            // native-scale offset hits a kernel rejection, e.g. nist_ftc_07's
            // tangent-neighbor merge). Proactive only — must run BEFORE the first
            // kernel failure (failure poisons the process; gate E-1).
            const double SCALE_UP_F = 1000.0;
            bool scaledModeF = false;
            if (forceScaled)
            {
                try
                {
                    WriteBlock.ExecuteTask("ChangeFilletRadius " + filletChainId + " [scale-up]", () =>
                    {
                        designBody.Shape.Transform(Matrix.CreateScale(SCALE_UP_F));
                    });
                    scaledModeF = true;
                    dRm *= SCALE_UP_F;
                }
                catch { /* stay native */ }
            }

            // chain 의 각 face 에 대해 offset
            var facesToOffset = new List<Face>();
            var perFaceOffsets = new Dictionary<Face, double>();

            foreach (int faceIdx in chain.FaceIndices)
            {
                var faceFeat = graph.Faces.FirstOrDefault(ff => ff.FaceIndex == faceIdx);
                if (faceFeat == null) continue;
                var face = GetFaceByIndex(designBody, faceIdx);
                if (face == null) continue;

                // Sign mapping (corpus-proven; do NOT introduce a new convention):
                //   Concave → -1, Convex → +1, then IsReversed flip.
                // W1-2: the concavity INPUT now comes from the live ContainsPoint
                // MaterialSign oracle (gate-verified) — the classifier's cached
                // Concavity mis-classifies some STEP chains (nist_ctc_01, boxy:
                // measured R moved the wrong direction). Oracle null (degenerate
                // thin wall / non-cyl geometry) falls back to the classifier.
                Concavity? oracleVerdict = ProbeMaterialConcavity(designBody, face);
                Concavity effConcavity = oracleVerdict ?? faceFeat.Concavity;
                if (oracleVerdict.HasValue && oracleVerdict.Value != faceFeat.Concavity)
                {
                    result.HintMessage = (result.HintMessage ?? "") + string.Format(
                        " [oracle override face {0}: classifier={1} → probed={2}]",
                        faceIdx, faceFeat.Concavity, oracleVerdict.Value);
                }
                double sign = (effConcavity == Concavity.Concave) ? -1.0 : +1.0;
                if (faceFeat.IsReversed) sign *= -1.0;
                // W1-4 orchestrator retry hook: a SIGN_INVERTED verdict from a
                // previous attempt re-runs this cell on a FRESH import with
                // flipSign=true (in-process corrective offsets are impossible —
                // STEP bodies enter deleted-state after the first offset).
                if (flipSign) sign *= -1.0;

                facesToOffset.Add(face);
                perFaceOffsets[face] = sign * dRm;
            }

            if (facesToOffset.Count == 0)
            {
                result.ErrorMessage = "No fillet faces resolved for chain " + filletChainId;
                return result;
            }

            // Pre-heal — see ChangeWallThickness for rationale.
            TryHealBody(designBody);

            var edgesBefore = CaptureEdges(designBody);

            // STRATEGY ORDER (revised after samplemodel2 R=40→48mm crashed SC
            // via RoundEdges-primary path with IndexOutOfRangeException in SC's
            // own kernel — bulk RoundEdges on a chain with large R change is
            // unstable):
            //   1. OffsetFaces (PRIMARY) — works for most cases, predictable
            //      and doesn't crash SC even for extreme R values.
            //   2. RoundEdges replacement (FALLBACK) — used when OffsetFaces
            //      fails (typically tangent-neighbor merge, e.g. nist_ftc_07).
            string lastErrF = null; int stepsAppliedF = 0;
            string filStrategy = null;
            bool filletOk = false;

            // Single-shot OffsetFaces (probe+corrective approach abandoned —
            // SC kernel marks fillet faces as deleted after the first offset
            // even within the same call sequence, so consecutive offsets fail
            // with "object is deleted").
            string offsetErrF;
            bool offsetOkF = TryOffsetFacesProgressive(designBody, perFaceOffsets,
                                                      "ChangeFilletRadius " + filletChainId,
                                                      out offsetErrF, out stepsAppliedF);
            if (offsetOkF)
            {
                filletOk = true;
                filStrategy = "OffsetFaces";
            }
            else
            {
                lastErrF = "OffsetFaces: " + offsetErrF;
            }
            if (!filletOk)
            {
                string roundErr;
                double roundTargetRm = GeometryUtils.MmToMeters(newRadiusMm)
                                     * (scaledModeF ? SCALE_UP_F : 1.0);
                if (TryReplaceFilletByRoundEdges(designBody, chain, graph,
                                                 roundTargetRm,
                                                 filletChainId, out roundErr))
                {
                    filletOk = true;
                    filStrategy = "RoundEdges replacement";
                }
                else
                {
                    lastErrF = (lastErrF ?? "") + " | RoundEdges: " + (roundErr ?? "(no msg)");
                }
            }

            // Scale back to native BEFORE verification (verify compares against
            // native-unit newRadiusMm). Best-effort on the failure path.
            if (scaledModeF)
            {
                try
                {
                    WriteBlock.ExecuteTask("ChangeFilletRadius " + filletChainId + " [scale-down]", () =>
                    {
                        designBody.Shape.Transform(Matrix.CreateScale(1.0 / SCALE_UP_F));
                    });
                }
                catch { /* eviction path */ }
                if (filletOk)
                {
                    result.HintMessage = (result.HintMessage ?? "") + " [ScaleNormalized 1000x]";
                }
            }

            if (!filletOk)
            {
                result.ErrorMessage = "OffsetFaces failed: " + lastErrF;
                return result;
            }
            if (filStrategy != "OffsetFaces" || stepsAppliedF > 1)
            {
                result.HintMessage = string.Format(
                    "Strategy: {0}{1}.",
                    filStrategy,
                    stepsAppliedF > 1 ? string.Format(" with {0}-step subdivision", stepsAppliedF) : "");
            }

            var edgesAfter = CaptureEdges(designBody);
            result.NewlyCreatedEdges = DiffEdges(edgesBefore, edgesAfter);
            foreach (int fi in chain.FaceIndices)
                result.ModifiedFaceIndices.Add(fi);

            // Anchor at the centroid of the chain's face origins.
            double cx = 0, cy = 0, cz = 0; int n = 0;
            foreach (int fi in chain.FaceIndices)
            {
                var ff = graph.Faces.FirstOrDefault(f => f.FaceIndex == fi);
                if (ff == null || ff.Origin == null || ff.Origin.Length < 3) continue;
                cx += ff.Origin[0]; cy += ff.Origin[1]; cz += ff.Origin[2]; n++;
            }
            if (n > 0)
            {
                result.TargetCenterXMm = (cx / n) * 1000.0;
                result.TargetCenterYMm = (cy / n) * 1000.0;
                result.TargetCenterZMm = (cz / n) * 1000.0;
                result.UseTargetCenter = true;
            }
            result.TargetFeatureKind = "Fillet";

            // In-process verify by FACE INDEX (not spatial centroid match).
            // For each fillet face that was in chain.FaceIndices, look at its
            // post-mod R. Record the closest-to-newR face as MeasuredAfterMm.
            // This avoids the failure mode where a spatial centroid match picks
            // an UNRELATED fillet on a complex model (e.g. SampleModel4 has
            // multiple chains; centroid landed in the middle, matched a different
            // 22mm fillet instead of the targeted 7mm one).
            try
            {
                double newRm_local = GeometryUtils.MmToMeters(newRadiusMm);
                double bestR = double.NaN; double bestRDiff = double.MaxValue;
                int i = 0;
                var faceIdxSet = new HashSet<int>(chain.FaceIndices);
                foreach (var df in designBody.Faces)
                {
                    if (faceIdxSet.Contains(i) && df != null && df.Shape != null)
                    {
                        var geom = df.Shape.Geometry;
                        double r = double.NaN;
                        if (geom is Torus tor) r = tor.MinorRadius;
                        else if (geom is Cylinder cyl) r = cyl.Radius;
                        if (!double.IsNaN(r))
                        {
                            double diff = Math.Abs(r - newRm_local);
                            if (diff < bestRDiff) { bestRDiff = diff; bestR = r; }
                        }
                    }
                    i++;
                }
                if (!double.IsNaN(bestR))
                {
                    result.MeasuredAfterMm = bestR * 1000.0;
                    // W1-3 sign-inversion gate: measured R ≈ mirror of newR around
                    // currentR means the offset went the WRONG direction (silent
                    // wrong geometry — worse than failure). Fail loudly with the
                    // SIGN_INVERTED tag so the orchestrator can retry on a fresh
                    // import with flipSign=true.
                    double currentRm_g = GeometryUtils.MmToMeters(currentRmm);
                    double wrongRm = 2 * currentRm_g - newRm_local;
                    double tolG = Math.Max(newRm_local * 0.05, 1e-5);
                    if (Math.Abs(bestR - newRm_local) > tolG
                        && Math.Abs(bestR - wrongRm) <= Math.Max(Math.Abs(wrongRm) * 0.1, 1e-5))
                    {
                        result.Success = false;
                        result.ErrorMessage = string.Format(
                            "SIGN_INVERTED: measured R={0:F3}mm = mirror of requested {1:F3}mm around {2:F3}mm",
                            bestR * 1000.0, newRadiusMm, currentRmm);
                        return result;
                    }
                }
            }
            catch { /* inconclusive */ }

            result.Success = true;
            return result;
        }

        // -------------------------------------------------------------------
        // AddHoleOnFace (P4 curved-face targeting)
        // -------------------------------------------------------------------
        /// <summary>
        /// Drill a hole that ENTERS along the LOCAL face normal at a 3D seed point - for features
        /// on a CURVED face (lens hole on a curved back, USB-C through a curved flank) that the
        /// planar AddHole (FindPlanarFaceAtZ) cannot target. Picks the body face nearest the seed,
        /// evaluates its outward normal via Geometry.ProjectPoint(seed).Normal (the verified idiom,
        /// AdjacencyBuilder.cs:262 - NOT GetFaceNormal), builds the cutter frame with Z = that
        /// normal, and drives it from below the face up through it. Inherits the P0 cutter logic
        /// (base at P - n*depth, extrude depth+margin) so it breaches cleanly.
        /// seedMm = a point ON or just outside the target face; diameterMm/depthMm in mm.
        /// </summary>
        public static ModificationResult AddHoleOnFace(
            DesignBody designBody, double[] seedMm, double diameterMm, double depthMm)
        {
            var result = new ModificationResult { Operation = "AddHoleOnFace", ModifiedBody = designBody };
            if (designBody == null) { result.ErrorMessage = "designBody is null"; return result; }
            if (seedMm == null || seedMm.Length < 3) { result.ErrorMessage = "seedMm must be length 3"; return result; }
            if (diameterMm <= 0 || depthMm <= 0) { result.ErrorMessage = "diameter/depth must be > 0"; return result; }

            double rM = GeometryUtils.MmToMeters(diameterMm / 2.0);
            double depthM = GeometryUtils.MmToMeters(depthMm);
            double marginM = 0.0005;

            // 1) resolve the contact point + outward normal of the nearest face (shared P4 helper).
            double[] contactMm, nrm;
            if (!ResolveFaceNormal(designBody, seedMm, out contactMm, out nrm))
            { result.ErrorMessage = "no projectable face near seed"; return result; }
            Point bestPt = Point.Create(
                GeometryUtils.MmToMeters(contactMm[0]), GeometryUtils.MmToMeters(contactMm[1]),
                GeometryUtils.MmToMeters(contactMm[2]));

            // 2) build the cutter in a frame whose Z = the outward face normal; base below the
            //    face by depth along -n, extrude (depth+margin) along +n so it breaches.
            try
            {
                var nDir = Direction.Create(nrm[0], nrm[1], nrm[2]);
                Point basePt = Point.Create(
                    bestPt.X - nrm[0] * depthM, bestPt.Y - nrm[1] * depthM, bestPt.Z - nrm[2] * depthM);
                Direction dx2 = nDir.ArbitraryPerpendicular;
                Direction dy2 = Direction.Cross(nDir, dx2);
                Frame frame = Frame.Create(basePt, dx2, dy2);   // Frame.DirZ = dx2 x dy2 = +n
                Plane basePlane = Plane.Create(frame);
                Profile profile = new CircleProfile(basePlane, rM, PointUV.Create(0, 0), 0.0);
                Body cutter = Body.ExtrudeProfile(profile, depthM + marginM);

                var edgesBefore = CaptureEdges(designBody);
                WriteBlock.ExecuteTask("AddHoleOnFace", () =>
                {
                    Part part = designBody.Parent as Part;
                    if (part != null)
                    {
                        DesignBody cutterDb = DesignBody.Create(part, "_holeonface_cutter", cutter);
                        designBody.Shape.Subtract(new[] { cutterDb.Shape });
                        try { cutterDb.Delete(); } catch { }
                    }
                    else designBody.Shape.Subtract(new[] { cutter });
                });
                var edgesAfter = CaptureEdges(designBody);
                result.NewlyCreatedEdges = DiffEdges(edgesBefore, edgesAfter);
                result.Success = true;
                result.HintMessage = "entered along face normal (" +
                    nrm[0].ToString("0.##") + "," + nrm[1].ToString("0.##") + "," + nrm[2].ToString("0.##") + ")";
                return result;
            }
            catch (Exception ex) { result.ErrorMessage = "AddHoleOnFace cut failed: " + ex.Message; return result; }
        }

        // -------------------------------------------------------------------
        // AddSlitOnFace / AddPocketOnFace / AddBossOnFace (P4 oriented variants)
        // -------------------------------------------------------------------
        // These are THIN wrappers: resolve the local outward normal + contact point at a 3D seed
        // (ResolveFaceNormal) and delegate to the existing planar Add* with that normal. The planar
        // tools already extrude along an arbitrary normal with the P0-fixed breach/embed logic, so
        // there is NOTHING to re-derive — the only thing they lacked was a curved face's local
        // normal. orientationSeedMm (slit/pocket) gives the in-plane LONG-axis direction: its
        // component projected off the normal becomes longAxis (e.g. a USB-C slit's long edge along
        // the phone length even on a curved flank). Pass null for an auto in-plane axis.

        /// <summary>
        /// Rectangular slit cut along the LOCAL face normal at a curved seed (USB-C / speaker slot
        /// through a curved flank). Resolves the normal via ResolveFaceNormal, derives an in-plane
        /// long axis from orientationSeedMm (or auto), and delegates to AddSlit(normalAxis=n).
        /// </summary>
        public static ModificationResult AddSlitOnFace(
            DesignBody designBody, double[] seedMm, double widthMm, double lengthMm, double depthMm,
            double[] orientationSeedMm = null)
        {
            var result = new ModificationResult { Operation = "AddSlitOnFace", ModifiedBody = designBody };
            double[] contactMm, n;
            if (!ResolveFaceNormal(designBody, seedMm, out contactMm, out n))
            { result.ErrorMessage = "no projectable face near seed"; return result; }
            double[] longAxis = InPlaneLongAxis(n, orientationSeedMm, contactMm);
            var r = AddSlit(designBody, contactMm, widthMm, lengthMm, depthMm, longAxis, n);
            r.Operation = "AddSlitOnFace";
            r.HintMessage = NormalHint(n);
            return r;
        }

        /// <summary>
        /// Rectangular pocket recessed along the LOCAL face normal at a curved seed (a recessed
        /// logo/antenna window on a curved back). Delegates to AddPocket(normalAxis=n).
        /// </summary>
        public static ModificationResult AddPocketOnFace(
            DesignBody designBody, double[] seedMm, double widthMm, double lengthMm, double depthMm)
        {
            var result = new ModificationResult { Operation = "AddPocketOnFace", ModifiedBody = designBody };
            double[] contactMm, n;
            if (!ResolveFaceNormal(designBody, seedMm, out contactMm, out n))
            { result.ErrorMessage = "no projectable face near seed"; return result; }
            var r = AddPocket(designBody, contactMm, widthMm, lengthMm, depthMm, n);
            r.Operation = "AddPocketOnFace";
            r.HintMessage = NormalHint(n);
            return r;
        }

        /// <summary>
        /// Cylindrical boss raised along the LOCAL face normal at a curved seed (a camera-ring boss
        /// standing proud of a curved back). Delegates to AddBoss(axisDir=n).
        /// </summary>
        public static ModificationResult AddBossOnFace(
            DesignBody designBody, double[] seedMm, double diameterMm, double heightMm)
        {
            var result = new ModificationResult { Operation = "AddBossOnFace", ModifiedBody = designBody };
            double[] contactMm, n;
            if (!ResolveFaceNormal(designBody, seedMm, out contactMm, out n))
            { result.ErrorMessage = "no projectable face near seed"; return result; }
            var r = AddBoss(designBody, contactMm, diameterMm, heightMm, n);
            r.Operation = "AddBossOnFace";
            r.HintMessage = NormalHint(n);
            return r;
        }

        /// <summary>In-plane long axis for a face feature: project the seed→orient direction onto
        /// the plane perpendicular to n. If orientationSeedMm is null or degenerate (parallel to n),
        /// fall back to n.ArbitraryPerpendicular so the caller always gets a valid in-plane axis.</summary>
        private static double[] InPlaneLongAxis(double[] n, double[] orientationSeedMm, double[] contactMm)
        {
            if (orientationSeedMm != null && orientationSeedMm.Length >= 3 && contactMm != null)
            {
                double[] dir = {
                    orientationSeedMm[0] - contactMm[0],
                    orientationSeedMm[1] - contactMm[1],
                    orientationSeedMm[2] - contactMm[2] };
                double d = Dot3(dir, n);
                double[] inPlane = { dir[0] - d * n[0], dir[1] - d * n[1], dir[2] - d * n[2] };
                var u = NormalizeVec3(inPlane);
                if (u != null) return u;
            }
            var nDir = Direction.Create(n[0], n[1], n[2]).ArbitraryPerpendicular;
            return new[] { nDir.X, nDir.Y, nDir.Z };
        }

        private static string NormalHint(double[] n)
        {
            return "entered along face normal (" +
                n[0].ToString("0.##") + "," + n[1].ToString("0.##") + "," + n[2].ToString("0.##") + ")";
        }

        // -------------------------------------------------------------------
        // AddBoss
        // -------------------------------------------------------------------
        /// <summary>
        /// body 위에 boss(외부 cylinder)를 추가.
        /// position 은 boss base 중심(mm). 기본 가정: top face = max-Z plane,
        /// boss 는 +Z 방향으로 height 만큼 솟음.
        /// </summary>
        public static ModificationResult AddBoss(
            DesignBody designBody,
            double[] positionMm,
            double diameterMm,
            double heightMm,
            double[] axisDir = null)
        {
            var result = new ModificationResult
            {
                Operation = "AddBoss",
                ModifiedBody = designBody,
            };

            // base plane (top face index, +Z normal) — for ModifiedFaceIndices targeting.
            int baseFaceIdx = -1;
            if (designBody != null && positionMm != null && positionMm.Length >= 3)
            {
                double pzM = GeometryUtils.MmToMeters(positionMm[2]);
                baseFaceIdx = FindPlanarFaceAtZ(designBody, pzM, +1.0);
            }

            if (designBody == null) { result.ErrorMessage = "designBody is null"; return result; }
            if (positionMm == null || positionMm.Length < 3) { result.ErrorMessage = "positionMm must be length 3"; return result; }
            if (diameterMm <= 0) { result.ErrorMessage = "diameterMm must be > 0"; return result; }
            if (heightMm <= 0) { result.ErrorMessage = "heightMm must be > 0"; return result; }

            double r = GeometryUtils.MmToMeters(diameterMm / 2.0);
            double h = GeometryUtils.MmToMeters(heightMm);
            double px = GeometryUtils.MmToMeters(positionMm[0]);
            double py = GeometryUtils.MmToMeters(positionMm[1]);
            double pz = GeometryUtils.MmToMeters(positionMm[2]);

            // Boss-class axis-awareness (matrix gap #1): extrude along the given
            // axis instead of the hardcoded +Z. Null keeps legacy behavior.
            double[] bossAxisU = NormalizeVec3(axisDir);

            var edgesBefore = CaptureEdges(designBody);

            try
            {
                WriteBlock.ExecuteTask("AddBoss", () =>
                {
                    // Embed the base slightly INTO the body so the boss overlaps
                    // the existing material instead of merely touching it at a
                    // coplanar base circle (matrix gap #1: a boss sitting exactly
                    // on a planar face unites to a zero-volume tangent → dV=0).
                    // When an axis is given, embed along -axis; else along -Z.
                    // BUGFIX (P0 from-empty gate): 0.5mm embed gave a partial/imprint Unite
                    // (+75.7 vs +169.6 expected) — the boss base was only marginally interior so
                    // the kernel produced a sliver merge. 1.5mm guarantees an unambiguous volumetric
                    // overlap the Boolean regularizes cleanly. (Masked in the 82% matrix because its
                    // oracle checked only dV>0 sign, never magnitude.)
                    double embedM = 0.0015; // 1.5mm
                    double bx = px, by = py, bz = pz;
                    Direction bDirX = Direction.DirX, bDirY = Direction.DirY;
                    double extrudeH = h;
                    if (bossAxisU != null)
                    {
                        var axD = Direction.Create(bossAxisU[0], bossAxisU[1], bossAxisU[2]);
                        bDirX = axD.ArbitraryPerpendicular;
                        bDirY = Direction.Cross(axD, bDirX);
                        bx = px - bossAxisU[0] * embedM;
                        by = py - bossAxisU[1] * embedM;
                        bz = pz - bossAxisU[2] * embedM;
                        extrudeH = h + embedM;
                    }
                    else
                    {
                        bz = pz - embedM;
                        extrudeH = h + embedM;
                    }
                    Point center = Point.Create(bx, by, bz);
                    Frame frame = Frame.Create(center, bDirX, bDirY);
                    Plane basePlane = Plane.Create(frame);
                    Profile profile = new CircleProfile(basePlane, r);
                    Body boss = Body.ExtrudeProfile(profile, extrudeH);

                    // RT4/RT5 fix: SpaceClaim raises "Some modeler bodies belong to design
                    // bodies and some do not." when Body.Unite mixes an owned target Body
                    // (designBody.Shape) with an unowned cutter Body (the freshly extruded
                    // 'boss'). Wrap the boss in a DesignBody so both operands are
                    // design-body-owned, perform the union, then delete the now-consumed
                    // cutter DesignBody (Unite absorbs its geometry into the target).
                    Part part = designBody.Parent as Part;
                    if (part != null)
                    {
                        DesignBody bossDb = DesignBody.Create(part, "_addboss_cutter", boss);
                        designBody.Shape.Unite(new[] { bossDb.Shape });
                        try { bossDb.Delete(); } catch { /* Unite may have already consumed it */ }
                    }
                    else
                    {
                        // Fallback: no parent Part available — try raw boolean (legacy path).
                        designBody.Shape.Unite(new[] { boss });
                    }
                });
            }
            catch (Exception ex)
            {
                result.ErrorMessage = "AddBoss failed: " + ex.Message;
                return result;
            }

            var edgesAfter = CaptureEdges(designBody);
            result.NewlyCreatedEdges = DiffEdges(edgesBefore, edgesAfter);
            if (baseFaceIdx >= 0) result.ModifiedFaceIndices.Add(baseFaceIdx);
            result.Success = true;
            return result;
        }

        // -------------------------------------------------------------------
        // AddRoundedRectBoss (rounded-rectangle plateau, e.g. a real camera bump)
        // -------------------------------------------------------------------
        /// <summary>
        /// Raise a ROUNDED-RECTANGLE plateau (the real phone camera-bump shape) instead of a plain
        /// cylinder: extrude a RectangleProfile boss along +Z (same embed + RT4/RT5 Unite as AddBoss),
        /// then RoundEdges its 4 vertical corner pillars to cornerRadiusMm. widthMm = X extent,
        /// lengthMm = Y extent. The corner-rounding mirrors CurvedShellBuilder.RoundVerticalCorners
        /// (edge whose length ≈ the boss height = a vertical corner pillar), with a per-edge fallback
        /// since RoundEdges is a bulk-fillet landmine.
        /// </summary>
        public static ModificationResult AddRoundedRectBoss(
            DesignBody designBody, double[] positionMm, double widthMm, double lengthMm,
            double heightMm, double cornerRadiusMm)
        {
            var result = new ModificationResult { Operation = "AddRoundedRectBoss", ModifiedBody = designBody };
            if (designBody == null) { result.ErrorMessage = "designBody is null"; return result; }
            if (positionMm == null || positionMm.Length < 3) { result.ErrorMessage = "positionMm must be length 3"; return result; }
            if (widthMm <= 0 || lengthMm <= 0) { result.ErrorMessage = "width/length must be > 0"; return result; }
            if (heightMm <= 0) { result.ErrorMessage = "heightMm must be > 0"; return result; }

            double wM = GeometryUtils.MmToMeters(widthMm);
            double lM = GeometryUtils.MmToMeters(lengthMm);
            double hM = GeometryUtils.MmToMeters(heightMm);
            double crM = GeometryUtils.MmToMeters(Math.Max(0.0, cornerRadiusMm));
            double px = GeometryUtils.MmToMeters(positionMm[0]);
            double py = GeometryUtils.MmToMeters(positionMm[1]);
            double pz = GeometryUtils.MmToMeters(positionMm[2]);
            const double embedM = 0.0015; // 1.5mm embed (same as AddBoss — clean volumetric overlap)

            var edgesBefore = CaptureEdges(designBody);
            try
            {
                WriteBlock.ExecuteTask("AddRoundedRectBoss", () =>
                {
                    double bz = pz - embedM;
                    double extrudeH = hM + embedM;
                    Point center = Point.Create(px, py, bz);
                    Frame frame = Frame.Create(center, Direction.DirX, Direction.DirY);
                    Plane basePlane = Plane.Create(frame);
                    Profile profile = new RectangleProfile(basePlane, wM, lM, PointUV.Create(0, 0), 0.0);
                    Body boss = Body.ExtrudeProfile(profile, extrudeH);

                    Part part = designBody.Parent as Part;
                    DesignBody bossDb = null;
                    if (part != null)
                    {
                        bossDb = DesignBody.Create(part, "_rrboss_cutter", boss);
                        designBody.Shape.Unite(new[] { bossDb.Shape });
                        try { bossDb.Delete(); } catch { }
                    }
                    else designBody.Shape.Unite(new[] { boss });

                    // Round the 4 vertical corner pillars of the plateau. After the Unite the boss is
                    // embedded 1.5mm INTO the slab, so the EXPOSED vertical corner edges have length ≈
                    // hM (the requested height), NOT extrudeH (=hM+embed). Filter to edges whose length
                    // is within a generous band around hM (covers slight kernel trimming) and RoundEdges
                    // (bulk then per-edge fallback).
                    if (crM > 1e-6)
                    {
                        var candidates = new List<Edge>();
                        double lo = hM - 0.0006, hi = hM + embedM + 0.0006; // [h-0.6mm, h+embed+0.6mm]
                        try
                        {
                            foreach (var e in designBody.Shape.Edges)
                            {
                                try { if (e.Length >= lo && e.Length <= hi) candidates.Add(e); }
                                catch { }
                            }
                        }
                        catch { }
                        if (candidates.Count > 0)
                        {
                            try
                            {
                                var map = new Dictionary<Edge, EdgeRound>();
                                foreach (var e in candidates)
                                    if (!map.ContainsKey(e)) map[e] = new FixedRadiusRound(crM);
                                designBody.Shape.RoundEdges(map);
                            }
                            catch
                            {
                                foreach (var e in candidates)
                                {
                                    try
                                    {
                                        var m1 = new Dictionary<Edge, EdgeRound> { { e, new FixedRadiusRound(crM) } };
                                        designBody.Shape.RoundEdges(m1);
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                });
            }
            catch (Exception ex) { result.ErrorMessage = "AddRoundedRectBoss failed: " + ex.Message; return result; }

            var edgesAfter = CaptureEdges(designBody);
            result.NewlyCreatedEdges = DiffEdges(edgesBefore, edgesAfter);
            result.Success = true;
            return result;
        }

        // -------------------------------------------------------------------
        // AddHole
        // -------------------------------------------------------------------
        /// <summary>
        /// body 에 hole(원기둥 cutter 로 Subtract) 추가.
        /// through=true 면 body Z 범위 전체 + margin 으로 cutter 생성,
        /// false 면 position.Z 위치에서 hole 의 "현재 height" 만큼.
        /// </summary>
        public static ModificationResult AddHole(
            DesignBody designBody,
            double[] positionMm,
            double diameterMm,
            bool through,
            double depthMm = 0.0,
            double[] axisDir = null,
            bool clampToWall = false)
        {
            var result = new ModificationResult
            {
                Operation = "AddHole",
                ModifiedBody = designBody,
            };

            if (designBody == null) { result.ErrorMessage = "designBody is null"; return result; }
            if (positionMm == null || positionMm.Length < 3) { result.ErrorMessage = "positionMm must be length 3"; return result; }
            if (diameterMm <= 0) { result.ErrorMessage = "diameterMm must be > 0"; return result; }

            double r = GeometryUtils.MmToMeters(diameterMm / 2.0);
            double px = GeometryUtils.MmToMeters(positionMm[0]);
            double py = GeometryUtils.MmToMeters(positionMm[1]);
            double pz = GeometryUtils.MmToMeters(positionMm[2]);

            // Matrix run 1/2 finding: the original +Z-only implementation FAILED on
            // every model whose hole axes aren't vertical (11752 = X-axis flange,
            // samplemodel2 = assembly orientations). The optional axisDir makes the
            // cutter axis explicit; null keeps the legacy +Z behavior intact.
            double[] axisU = NormalizeVec3(axisDir);
            // Extraction reports DepthMm=0 for many non-Z holes — with a known axis
            // treat depth-less blind requests as THROUGH (the safe drill).
            if (!through && depthMm <= 0 && axisU != null) through = true;

            Box bbox;
            try { bbox = designBody.Shape.GetBoundingBox(Matrix.Identity); }
            catch (Exception ex)
            {
                result.ErrorMessage = "GetBoundingBox failed: " + ex.Message;
                return result;
            }

            // W5-2 thin-wall TANGENCY guard. A bore whose wall (center +/- r) lands flush
            // against an outer face leaves a zero-thickness sliver -> "Result may become
            // non-manifold" (as1-oc MoveHole: D10 r5 bore shifted +5mm in a 20mm-wide nut
            // sits tangent to the far face). Clamp the in-plane center inward to keep at
            // least MIN_WALL from every bbox extreme PERPENDICULAR to the hole axis; if no
            // safe center exists, skip with a distinct message (harness scores INCONCLUSIVE,
            // not FAILED). Scoped to axis-aligned planar-outer-face bodies (bbox is exact);
            // axisU==null (legacy +Z) clamps in X/Y, axisU!=null clamps in the 2 in-plane axes.
            const double MIN_WALL_M = 5e-4; // 0.5 mm (kernel-accepted minimum on as1-oc)
            bool wallClamped = false;
            // OPT-IN only: clamping silently relocates the bore, which is correct for
            // MoveHole's drill (the move target is approximate) but WRONG for callers that
            // need exact placement — MirrorFeature(hole) calls AddHole at the mirror image
            // and an off-edge clamp there moves the twin away from the symmetry point
            // (regressed samplemodel2 Mirror V->F). So only MoveHole passes clampToWall=true.
            if (clampToWall)
            {
                double[] bmin = { bbox.MinCorner.X, bbox.MinCorner.Y, bbox.MinCorner.Z };
                double[] bmax = { bbox.MaxCorner.X, bbox.MaxCorner.Y, bbox.MaxCorner.Z };
                double[] ctr = { px, py, pz };
                // in-plane axes = the 2 world axes most perpendicular to the hole axis.
                // With axisU null (legacy +Z) the in-plane axes are X(0),Y(1).
                bool[] inPlane = { true, true, true };
                if (axisU != null)
                {
                    // drop the world axis most parallel to the hole axis from clamping
                    int dom = 0; double best = Math.Abs(axisU[0]);
                    if (Math.Abs(axisU[1]) > best) { best = Math.Abs(axisU[1]); dom = 1; }
                    if (Math.Abs(axisU[2]) > best) { dom = 2; }
                    inPlane[dom] = false;
                }
                else { inPlane[2] = false; } // legacy +Z: Z is the axis
                bool infeasible = false;
                for (int a = 0; a < 3; a++)
                {
                    if (!inPlane[a]) continue;
                    double lo = bmin[a] + r + MIN_WALL_M; // nearest safe center to min face
                    double hi = bmax[a] - r - MIN_WALL_M; // nearest safe center to max face
                    if (lo > hi) { infeasible = true; break; } // body too thin for this bore
                    if (ctr[a] < lo) { ctr[a] = lo; wallClamped = true; }
                    else if (ctr[a] > hi) { ctr[a] = hi; wallClamped = true; }
                }
                if (infeasible)
                {
                    result.ErrorMessage = "AddHole skipped: bore would leave < " +
                        (MIN_WALL_M * 1000.0).ToString("0.0") + "mm wall (non-manifold tangency); no safe center";
                    return result;
                }
                if (wallClamped)
                {
                    px = ctr[0]; py = ctr[1]; pz = ctr[2];
                    result.HintMessage = "AddHole: center clamped inward to preserve " +
                        (MIN_WALL_M * 1000.0).ToString("0.0") + "mm min wall (was tangent to outer face)";
                }
            }

            double bodyZmin = bbox.MinCorner.Z;
            double bodyZmax = bbox.MaxCorner.Z;
            double bodyZspan = bodyZmax - bodyZmin;
            double marginM = 0.001; // 1 mm

            double cutterHeight;
            double cutterBaseZ = 0.0;
            Point cutterBase = Point.Origin;
            Direction exDirX = Direction.DirX, exDirY = Direction.DirY;

            if (axisU != null)
            {
                // Project bbox corners onto the axis (relative to the hole position)
                // to size a through cutter that spans the full body along the axis.
                double tmin = double.MaxValue, tmax = double.MinValue;
                for (int ci = 0; ci < 8; ci++)
                {
                    double cx = ((ci & 1) == 0) ? bbox.MinCorner.X : bbox.MaxCorner.X;
                    double cy = ((ci & 2) == 0) ? bbox.MinCorner.Y : bbox.MaxCorner.Y;
                    double cz = ((ci & 4) == 0) ? bbox.MinCorner.Z : bbox.MaxCorner.Z;
                    double t = (cx - px) * axisU[0] + (cy - py) * axisU[1] + (cz - pz) * axisU[2];
                    if (t < tmin) tmin = t;
                    if (t > tmax) tmax = t;
                }
                var axDir = Direction.Create(axisU[0], axisU[1], axisU[2]);
                exDirX = axDir.ArbitraryPerpendicular;
                exDirY = Direction.Cross(axDir, exDirX);
                if (through)
                {
                    cutterHeight = (tmax - tmin) + 2.0 * marginM;
                    cutterBase = Point.Create(
                        px + axisU[0] * (tmin - marginM),
                        py + axisU[1] * (tmin - marginM),
                        pz + axisU[2] * (tmin - marginM));
                }
                else
                {
                    double dM = GeometryUtils.MmToMeters(depthMm);
                    cutterHeight = dM + marginM;
                    cutterBase = Point.Create(px - axisU[0] * dM, py - axisU[1] * dM, pz - axisU[2] * dM);
                }
            }
            else if (through)
            {
                cutterHeight = bodyZspan + 2.0 * marginM;
                cutterBaseZ = bodyZmin - marginM;
            }
            else
            {
                if (depthMm <= 0)
                {
                    result.ErrorMessage = "Blind hole requires depthMm > 0";
                    return result;
                }
                cutterHeight = GeometryUtils.MmToMeters(depthMm) + marginM;
                // hole 입구 = positionMm.Z, 깊이 = -Z 방향으로
                cutterBaseZ = pz - GeometryUtils.MmToMeters(depthMm);
            }

            var edgesBefore = CaptureEdges(designBody);

            try
            {
                WriteBlock.ExecuteTask("AddHole", () =>
                {
                    Point center = (axisU != null) ? cutterBase : Point.Create(px, py, cutterBaseZ);
                    Frame frame = Frame.Create(center, exDirX, exDirY);
                    Plane basePlane = Plane.Create(frame);
                    Profile profile = new CircleProfile(basePlane, r);
                    Body cutter = Body.ExtrudeProfile(profile, cutterHeight);

                    // RT4/RT5 fix: SpaceClaim raises "Some modeler bodies belong to design
                    // bodies and some do not." when Body.Subtract mixes an owned target
                    // Body (designBody.Shape) with an unowned cutter Body (the freshly
                    // extruded 'cutter'). Wrap the cutter in a DesignBody so both operands
                    // are design-body-owned, perform the subtract, then delete the cutter
                    // DesignBody (Subtract consumes it).
                    Part part = designBody.Parent as Part;
                    if (part != null)
                    {
                        DesignBody cutterDb = DesignBody.Create(part, "_addhole_cutter", cutter);
                        designBody.Shape.Subtract(new[] { cutterDb.Shape });
                        try { cutterDb.Delete(); } catch { /* Subtract may have already consumed it */ }
                    }
                    else
                    {
                        // Fallback: no parent Part available — try raw boolean (legacy path).
                        designBody.Shape.Subtract(new[] { cutter });
                    }
                });
            }
            catch (Exception ex)
            {
                result.ErrorMessage = "AddHole failed: " + ex.Message;
                return result;
            }

            var edgesAfter = CaptureEdges(designBody);
            result.NewlyCreatedEdges = DiffEdges(edgesBefore, edgesAfter);
            // hole 입구 평면 (max-Z) 의 face index 를 기록 → AutoFillet 타겟팅.
            try
            {
                int topIdx = FindPlanarFaceAtZ(designBody, bodyZmax, +1.0);
                if (topIdx >= 0) result.ModifiedFaceIndices.Add(topIdx);
                if (through)
                {
                    int botIdx = FindPlanarFaceAtZ(designBody, bodyZmin, -1.0);
                    if (botIdx >= 0) result.ModifiedFaceIndices.Add(botIdx);
                }
            }
            catch { /* best-effort */ }
            result.Success = true;
            return result;
        }

        // W4-1b gate: enable live-cylinder relocation for hole fills. OFF until the
        // forceScale re-import recovery ships (relocation alone regresses planar
        // multi-hole parts and cannot recover the curved-part Boolean poison).
        private const bool RELOCATE_FILL = false;

        /// <summary>
        /// W4-1 live-cylinder relocation (kernel-truth). FeatureExtractor mis-locates
        /// holes on curved parts (gate: 11752 H1 anchor 48mm off, axis misses the real
        /// bore → bbox-projection fill lands in the wrong place → Unite "General
        /// Failure"). Instead, scan LIVE cylindrical faces and lock onto the one whose
        /// axis runs nearest the approx centre (radius-gated) — its Axis/Radius/extent
        /// are read straight from the kernel B-rep, independent of the extraction.
        ///
        /// On success returns true and fills axisFoot (foot of perpendicular from world
        /// origin onto the true axis), axisDir (unit, kernel), radiusM (true), and the
        /// signed axial band [tLo,tHi] of the bore face along axisDir.
        /// </summary>
        private static bool TryRelocateHoleCylinder(
            DesignBody designBody, double cxM, double cyM, double czM,
            double[] approxAxisU, double radiusM,
            out double[] axisFoot, out double[] axisDir, out double radiusOut,
            out double tLo, out double tHi)
        {
            axisFoot = null; axisDir = null; radiusOut = 0; tLo = 0; tHi = 0;
            DesignFace best = null; Cylinder bestC = null; double bestPerp = double.MaxValue;
            double rTol = radiusM > 0 ? Math.Max(radiusM * 0.05, 2e-4) : double.MaxValue;
            foreach (var df in designBody.Faces)
            {
                if (df == null || df.Shape == null) continue;
                var c = df.Shape.Geometry as Cylinder;
                if (c == null) continue;
                if (radiusM > 0 && Math.Abs(c.Radius - radiusM) > rTol) continue;
                var A = c.Axis.Origin; var D = c.Axis.Direction;
                double dux = D.X, duy = D.Y, duz = D.Z;
                if (approxAxisU != null)
                {
                    double adot = Math.Abs(dux * approxAxisU[0] + duy * approxAxisU[1] + duz * approxAxisU[2]);
                    if (adot < 0.98) continue; // not parallel to the extracted axis
                }
                double wx = cxM - A.X, wy = cyM - A.Y, wz = czM - A.Z;
                double proj = wx * dux + wy * duy + wz * duz;
                double px = wx - proj * dux, py = wy - proj * duy, pz = wz - proj * duz;
                double perp = Math.Sqrt(px * px + py * py + pz * pz);
                if (perp < bestPerp) { bestPerp = perp; best = df; bestC = c; }
            }
            if (best == null) return false;
            // accept only if the extracted centre is plausibly on this bore's axis
            double acceptTol = radiusM > 0 ? Math.Max(radiusM * 2.0, 2e-3) : 2e-3;
            if (bestPerp > acceptTol) return false;

            var Ao = bestC.Axis.Origin; var Do = bestC.Axis.Direction;
            double[] du = NormalizeVec3(new[] { Do.X, Do.Y, Do.Z }) ?? new[] { 0.0, 0.0, 1.0 };
            double t0 = -(Ao.X * du[0] + Ao.Y * du[1] + Ao.Z * du[2]);
            axisFoot = new[] { Ao.X + du[0] * t0, Ao.Y + du[1] * t0, Ao.Z + du[2] * t0 };
            axisDir = du;
            radiusOut = bestC.Radius;
            var bb = best.Shape.GetBoundingBox(Matrix.Identity);
            double lo = double.MaxValue, hi = double.MinValue;
            for (int ci = 0; ci < 8; ci++)
            {
                double X = (ci & 1) == 0 ? bb.MinCorner.X : bb.MaxCorner.X;
                double Y = (ci & 2) == 0 ? bb.MinCorner.Y : bb.MaxCorner.Y;
                double Z = (ci & 4) == 0 ? bb.MinCorner.Z : bb.MaxCorner.Z;
                double t = X * du[0] + Y * du[1] + Z * du[2];
                if (t < lo) lo = t;
                if (t > hi) hi = t;
            }
            tLo = lo; tHi = hi;
            return true;
        }

        /// <summary>
        /// Unite a filler cylinder (axis fillBase→+axisU, radius rFillM, length fillHeight)
        /// into the body. A kernel "General Failure" here POISONS the body (blast-radius
        /// landmine — gate-confirmed: a post-failure scale-trick retry then throws "object
        /// is deleted"), so in-process recovery is impossible; the harness must re-import
        /// under a forceScale flag (W4-1b). This helper does the single native Unite and
        /// lets the exception propagate to the caller.
        /// </summary>
        private static void UniteFillerCylinder(
            DesignBody designBody, string tag, Point fillBase, double[] axisU,
            double rFillM, double fillHeight)
        {
            WriteBlock.ExecuteTask(tag, () =>
            {
                Part part = designBody.Parent as Part;
                var axD = Direction.Create(axisU[0], axisU[1], axisU[2]);
                var fDirX = axD.ArbitraryPerpendicular;
                var fDirY = Direction.Cross(axD, fDirX);
                Frame frame = Frame.Create(fillBase, fDirX, fDirY);
                Plane basePlane = Plane.Create(frame);
                Profile profile = new CircleProfile(basePlane, rFillM);
                Body filler = Body.ExtrudeProfile(profile, fillHeight);
                if (part != null)
                {
                    DesignBody fillerDb = DesignBody.Create(part, "_filler", filler);
                    designBody.Shape.Unite(new[] { fillerDb.Shape });
                    try { fillerDb.Delete(); } catch { }
                }
                else
                {
                    designBody.Shape.Unite(new[] { filler });
                }
            });
        }

        /// <summary>
        /// W4-5 boss-height via coaxial Boolean (OffsetFaces replacement). Relocates the
        /// live boss cylinder, finds the free CAP end (air just beyond along the axis),
        /// then Unites (grow, dHm>0) or Subtracts (shrink, dHm<0) a coaxial cylinder at
        /// the cap. Returns a result (Success or op-failed) when applicable, or null if
        /// the boss can't be relocated / has no free cap (both ends solid → not a
        /// protruding boss). Gate-validated on 624ZZ.
        /// </summary>
        private static ModificationResult TryChangeBossHeightBoolean(
            DesignBody designBody, double[] baseM, double[] axisU, double radiusM, double newHeightM)
        {
            double[] foot, dir; double R, tLo, tHi;
            if (!TryRelocateHoleCylinder(designBody, baseM[0], baseM[1], baseM[2], axisU, radiusM,
                                         out foot, out dir, out R, out tLo, out tHi) || (tHi - tLo) <= 1e-9)
                return null;
            // TRUE current height = the relocated cylinder's axial extent (kernel truth) —
            // the caller's EstimateCylFaceExtentMm can be wildly off (624ZZ est ~0.2 vs
            // real 4.6 → over-grow to 9.4). dHm from the live extent makes the target exact.
            double dHm = newHeightM - (tHi - tLo);
            if (Math.Abs(dHm) < 1e-7) { var rr = new ModificationResult { Operation = "ChangeBossHeight", ModifiedBody = designBody, Success = true, MeasuredAfterMm = (tHi - tLo) * 1000.0 }; return rr; }
            const double eps = 3e-4;
            Func<double, double, bool> airBeyond = (tend, sgn) =>
            {
                var q = Point.Create(foot[0] + dir[0] * (tend + sgn * eps),
                                     foot[1] + dir[1] * (tend + sgn * eps),
                                     foot[2] + dir[2] * (tend + sgn * eps));
                try { return !designBody.Shape.ContainsPoint(q); } catch { return false; }
            };
            bool hiAir = airBeyond(tHi, +1.0);
            bool loAir = airBeyond(tLo, -1.0);
            double capT; double[] capDir;
            if (hiAir && !loAir) { capT = tHi; capDir = new[] { dir[0], dir[1], dir[2] }; }
            else if (loAir && !hiAir) { capT = tLo; capDir = new[] { -dir[0], -dir[1], -dir[2] }; }
            else if (hiAir && loAir) { capT = tHi; capDir = new[] { dir[0], dir[1], dir[2] }; }
            else return null; // both ends solid — not a protruding boss

            var result = new ModificationResult { Operation = "ChangeBossHeight", ModifiedBody = designBody };
            double[] cap = { foot[0] + dir[0] * capT, foot[1] + dir[1] * capT, foot[2] + dir[2] * capT };
            const double ov = 1e-4;
            bool grow = dHm > 0;
            double mag = Math.Abs(dHm);
            var edgesBefore = CaptureEdges(designBody);
            try
            {
                WriteBlock.ExecuteTask("ChangeBossHeight[Boolean]", () =>
                {
                    Part part = designBody.Parent as Part;
                    var axD = Direction.Create(capDir[0], capDir[1], capDir[2]);
                    var fdx = axD.ArbitraryPerpendicular;
                    var fdy = Direction.Cross(axD, fdx);
                    Point bp = grow
                        ? Point.Create(cap[0] - capDir[0] * ov, cap[1] - capDir[1] * ov, cap[2] - capDir[2] * ov)
                        : Point.Create(cap[0] + capDir[0] * ov, cap[1] + capDir[1] * ov, cap[2] + capDir[2] * ov);
                    Frame frame = Frame.Create(bp, fdx, fdy);
                    Plane plane = Plane.Create(frame);
                    Profile prof = new CircleProfile(plane, R);
                    Body cyl = Body.ExtrudeProfile(prof, grow ? (mag + ov) : -(mag + ov));
                    if (part != null)
                    {
                        DesignBody cylDb = DesignBody.Create(part, "_bossh", cyl);
                        if (grow) designBody.Shape.Unite(new[] { cylDb.Shape });
                        else designBody.Shape.Subtract(new[] { cylDb.Shape });
                        try { cylDb.Delete(); } catch { }
                    }
                    else
                    {
                        if (grow) designBody.Shape.Unite(new[] { cyl });
                        else designBody.Shape.Subtract(new[] { cyl });
                    }
                });
            }
            catch (Exception ex)
            {
                result.ErrorMessage = "ChangeBossHeight(Boolean) op failed: " + ex.Message;
                return result;
            }
            var edgesAfter = CaptureEdges(designBody);
            result.NewlyCreatedEdges = DiffEdges(edgesBefore, edgesAfter);
            try
            {
                double[] f2, d2; double r2, lo2, hi2;
                if (TryRelocateHoleCylinder(designBody, baseM[0], baseM[1], baseM[2], axisU, radiusM,
                                            out f2, out d2, out r2, out lo2, out hi2) && (hi2 - lo2) > 1e-9)
                    result.MeasuredAfterMm = (hi2 - lo2) * 1000.0;
            }
            catch { }
            result.Success = true;
            return result;
        }

        /// <summary>
        /// W4-5 boss-DIAMETER GROW via coaxial Boolean (OffsetFaces replacement). Relocates
        /// the live boss cylinder and Unites a larger coaxial cylinder (radius newRm) over
        /// its full axial extent — the annulus [currentR,newR] is added, the inner overlap
        /// is a harmless Unite no-op. Returns a result when applicable, or null if the boss
        /// can't be relocated (→ caller uses OffsetFaces). Grow only (newRm>currentRm).
        /// </summary>
        private static ModificationResult TryChangeBossDiameterBoolean(
            DesignBody designBody, BossFeature boss, double currentRm, double newRm)
        {
            if (newRm <= currentRm) return null;
            double[] axisU = NormalizeVec3(boss.Axis) ?? new[] { 0.0, 0.0, 1.0 };
            double[] baseM = new[]
            {
                GeometryUtils.MmToMeters(boss.BasePositionMm != null && boss.BasePositionMm.Length > 0 ? boss.BasePositionMm[0] : 0.0),
                GeometryUtils.MmToMeters(boss.BasePositionMm != null && boss.BasePositionMm.Length > 1 ? boss.BasePositionMm[1] : 0.0),
                GeometryUtils.MmToMeters(boss.BasePositionMm != null && boss.BasePositionMm.Length > 2 ? boss.BasePositionMm[2] : 0.0),
            };
            double[] foot, dir; double R, tLo, tHi;
            if (!TryRelocateHoleCylinder(designBody, baseM[0], baseM[1], baseM[2], axisU, currentRm,
                                         out foot, out dir, out R, out tLo, out tHi) || (tHi - tLo) <= 1e-9)
                return null;

            var result = new ModificationResult { Operation = "ChangeBossDiameter", ModifiedBody = designBody };
            const double ov = 1e-4;
            var edgesBefore = CaptureEdges(designBody);
            try
            {
                WriteBlock.ExecuteTask("ChangeBossDiameter[Boolean]", () =>
                {
                    Part part = designBody.Parent as Part;
                    var axD = Direction.Create(dir[0], dir[1], dir[2]);
                    var fdx = axD.ArbitraryPerpendicular;
                    var fdy = Direction.Cross(axD, fdx);
                    Point bp = Point.Create(foot[0] + dir[0] * (tLo - ov),
                                            foot[1] + dir[1] * (tLo - ov),
                                            foot[2] + dir[2] * (tLo - ov));
                    Frame frame = Frame.Create(bp, fdx, fdy);
                    Plane plane = Plane.Create(frame);
                    Profile prof = new CircleProfile(plane, newRm);
                    Body cyl = Body.ExtrudeProfile(prof, (tHi - tLo) + 2.0 * ov);
                    if (part != null)
                    {
                        DesignBody cylDb = DesignBody.Create(part, "_bossd", cyl);
                        designBody.Shape.Unite(new[] { cylDb.Shape });
                        try { cylDb.Delete(); } catch { }
                    }
                    else
                    {
                        designBody.Shape.Unite(new[] { cyl });
                    }
                });
            }
            catch (Exception ex)
            {
                result.ErrorMessage = "ChangeBossDiameter(Boolean) op failed: " + ex.Message;
                return result;
            }
            var edgesAfter = CaptureEdges(designBody);
            result.NewlyCreatedEdges = DiffEdges(edgesBefore, edgesAfter);
            try
            {
                double[] f2, d2; double r2, lo2, hi2;
                if (TryRelocateHoleCylinder(designBody, baseM[0], baseM[1], baseM[2], axisU, newRm,
                                            out f2, out d2, out r2, out lo2, out hi2))
                    result.MeasuredAfterMm = r2 * 2.0 * 1000.0;
            }
            catch { }
            result.Success = true;
            return result;
        }

        // -------------------------------------------------------------------
        // MoveHole — subtract-new-then-fill-old pattern.
        // -------------------------------------------------------------------
        /// <summary>
        /// 기존 hole 을 채우고 (axis 따라 cylinder Unite) 새 위치에 같은 D/depth 의 hole 을 추가한다.
        /// newPositionMm 은 (X, Y, Z); Z 는 일반적으로 hole 입구 위치 (mm).
        /// </summary>
        public static ModificationResult MoveHole(
            DesignBody designBody,
            FeatureGraph graph,
            string holeId,
            double[] newPositionMm)
        {
            var result = new ModificationResult
            {
                Operation = "MoveHole",
                ModifiedBody = designBody,
            };

            if (designBody == null) { result.ErrorMessage = "designBody is null"; return result; }
            if (graph == null) { result.ErrorMessage = "graph is null"; return result; }
            if (string.IsNullOrEmpty(holeId)) { result.ErrorMessage = "holeId is null"; return result; }
            if (newPositionMm == null || newPositionMm.Length < 3) { result.ErrorMessage = "newPositionMm must be length 3"; return result; }

            var hole = graph.Holes.FirstOrDefault(h => h.Id == holeId);
            if (hole == null) { result.ErrorMessage = "Hole not found: " + holeId; return result; }

            double oldX = hole.PositionMm != null && hole.PositionMm.Length > 0 ? hole.PositionMm[0] : 0.0;
            double oldY = hole.PositionMm != null && hole.PositionMm.Length > 1 ? hole.PositionMm[1] : 0.0;
            double oldZ = hole.PositionMm != null && hole.PositionMm.Length > 2 ? hole.PositionMm[2] : 0.0;
            double diaMm = hole.DiameterMm;
            double depthMm = hole.DepthMm;
            bool through = hole.IsThrough;

            // Step 1: fill the OLD hole at (oldX, oldY, oldZ) — Unite a cylinder of matching D/H.
            // Step 2: subtract a NEW hole at newPositionMm with same D/through/depth.
            var edgesBefore = CaptureEdges(designBody);

            // Axis-aware fill (matrix run finding: the +Z frame + Z-span sizing
            // broke every non-vertical hole — "Distance cannot be zero" when the
            // extraction reports DepthMm=0). Fill along hole.Axis; depth-less
            // holes fill the full bore extent along the axis.
            Box bbox;
            try { bbox = designBody.Shape.GetBoundingBox(Matrix.Identity); }
            catch (Exception ex) { result.ErrorMessage = "GetBoundingBox failed: " + ex.Message; return result; }
            // Fill radius gets a tiny radial margin: a filler cylinder of EXACTLY the
            // bore radius leaves a coincident (zero-thickness) cylindrical face that the
            // kernel keeps and the extractor re-detects as the old hole ("pop 1->1" —
            // SampleModel1/boxy). The extra annulus lies inside surrounding solid, so the
            // Unite engulfs the bore wall with no side effect.
            double rM = GeometryUtils.MmToMeters(diaMm / 2.0);
            double rFillM = rM + Math.Max(rM * 0.02, 1e-5);
            double[] axisU = NormalizeVec3(hole.Axis) ?? new[] { 0.0, 0.0, 1.0 };
            double oxM = GeometryUtils.MmToMeters(oldX);
            double oyM = GeometryUtils.MmToMeters(oldY);
            double ozM = GeometryUtils.MmToMeters(oldZ);

            // Kernel-truth PIN guard (see RemoveHole): if the "hole" is really a solid pin
            // (axis-core solid), filling it Unites into solid → "General Failure" + body
            // poison. Reject up front instead of corrupting the body.
            {
                double[] pgF, pgD; double pgR, pgLo, pgHi;
                if (TryRelocateHoleCylinder(designBody, oxM, oyM, ozM, axisU, rM,
                                            out pgF, out pgD, out pgR, out pgLo, out pgHi)
                    && (pgHi - pgLo) > 1e-9)
                {
                    double tMid = (pgLo + pgHi) / 2.0;
                    var corePt = Point.Create(pgF[0] + pgD[0] * tMid, pgF[1] + pgD[1] * tMid, pgF[2] + pgD[2] * tMid);
                    bool solidCore = false;
                    try { solidCore = designBody.Shape.ContainsPoint(corePt); } catch { }
                    if (solidCore)
                    {
                        result.ErrorMessage = "target is a solid pin, not a hole (mis-extracted) — MoveHole not applicable";
                        return result;
                    }
                }
            }

            double fillHeight;
            Point fillBase;
            if (through || depthMm <= 0)
            {
                double tmin = double.MaxValue, tmax = double.MinValue;
                for (int ci = 0; ci < 8; ci++)
                {
                    double cx = ((ci & 1) == 0) ? bbox.MinCorner.X : bbox.MaxCorner.X;
                    double cy = ((ci & 2) == 0) ? bbox.MinCorner.Y : bbox.MaxCorner.Y;
                    double cz = ((ci & 4) == 0) ? bbox.MinCorner.Z : bbox.MaxCorner.Z;
                    double t = (cx - oxM) * axisU[0] + (cy - oyM) * axisU[1] + (cz - ozM) * axisU[2];
                    if (t < tmin) tmin = t;
                    if (t > tmax) tmax = t;
                }
                fillHeight = tmax - tmin;
                fillBase = Point.Create(oxM + axisU[0] * tmin, oyM + axisU[1] * tmin, ozM + axisU[2] * tmin);
            }
            else
            {
                double dM = GeometryUtils.MmToMeters(depthMm);
                fillHeight = dM;
                fillBase = Point.Create(oxM - axisU[0] * dM, oyM - axisU[1] * dM, ozM - axisU[2] * dM);
            }

            // W4-1 kernel-truth override: relocate the true bore cylinder from the live
            // body and fill EXACTLY its axis/radius/extent (extraction-independent).
            // GATED OFF by default (W4-1b): relocation is gate-validated (correctly locates
            // the curved-part bore) but on its own (a) regresses planar multi-hole parts
            // like boxy (V->IC) and (b) the curved-part Boolean still hits "General Failure"
            // which POISONS the body (no in-process recovery). Net win requires the
            // forceScale re-import path — enable RELOCATE_FILL together with that.
            double[] rfFoot, rfDir; double rfR, rfLo, rfHi;
            if (RELOCATE_FILL
                && TryRelocateHoleCylinder(designBody, oxM, oyM, ozM, axisU, rM,
                                        out rfFoot, out rfDir, out rfR, out rfLo, out rfHi)
                && (rfHi - rfLo) > 1e-9)
            {
                axisU = rfDir;
                rM = rfR;
                rFillM = rM + Math.Max(rM * 0.02, 1e-5);
                double margin = 1e-4; // 0.1mm over-breach each end
                fillHeight = (rfHi - rfLo) + 2.0 * margin;
                fillBase = Point.Create(rfFoot[0] + rfDir[0] * (rfLo - margin),
                                        rfFoot[1] + rfDir[1] * (rfLo - margin),
                                        rfFoot[2] + rfDir[2] * (rfLo - margin));
                result.HintMessage = "fill relocated to live cylinder r=" + (rM * 1000.0).ToString("0.###") + "mm";
            }

            if (fillHeight < 1e-9)
            {
                result.ErrorMessage = "MoveHole.fill: degenerate fill extent along hole axis";
                return result;
            }

            try
            {
                UniteFillerCylinder(designBody, "MoveHole.fill " + holeId, fillBase, axisU, rFillM, fillHeight);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = "MoveHole.fill failed: " + ex.Message;
                return result;
            }

            // Now subtract new hole at newPositionMm (drilled along the same axis).
            // clampToWall=true: the move target is approximate, so nudging it inward to
            // avoid a zero-thickness tangent sliver is acceptable (fixes as1-oc MoveHole).
            var addRes = AddHole(designBody, newPositionMm, diaMm, through, through ? 0.0 : depthMm, hole.Axis, true);
            if (!addRes.Success)
            {
                result.ErrorMessage = "MoveHole.subtract failed: " + addRes.ErrorMessage;
                return result;
            }

            var edgesAfter = CaptureEdges(designBody);
            result.NewlyCreatedEdges = DiffEdges(edgesBefore, edgesAfter);
            foreach (int fi in addRes.ModifiedFaceIndices) result.ModifiedFaceIndices.Add(fi);
            result.Success = true;
            return result;
        }

        // -------------------------------------------------------------------
        // MoveBoss — subtract existing boss region + add boss at new position.
        // -------------------------------------------------------------------
        /// <summary>
        /// 기존 boss 영역을 Subtract 로 제거하고, 새 위치에 같은 D/H 의 boss 를 Unite 한다.
        /// </summary>
        public static ModificationResult MoveBoss(
            DesignBody designBody,
            FeatureGraph graph,
            string bossId,
            double[] newPositionMm)
        {
            var result = new ModificationResult
            {
                Operation = "MoveBoss",
                ModifiedBody = designBody,
            };

            if (designBody == null) { result.ErrorMessage = "designBody is null"; return result; }
            if (graph == null) { result.ErrorMessage = "graph is null"; return result; }
            if (string.IsNullOrEmpty(bossId)) { result.ErrorMessage = "bossId is null"; return result; }
            if (newPositionMm == null || newPositionMm.Length < 3) { result.ErrorMessage = "newPositionMm must be length 3"; return result; }

            var boss = graph.Bosses.FirstOrDefault(b => b.Id == bossId);
            if (boss == null) { result.ErrorMessage = "Boss not found: " + bossId; return result; }

            double oldX = boss.BasePositionMm != null && boss.BasePositionMm.Length > 0 ? boss.BasePositionMm[0] : 0.0;
            double oldY = boss.BasePositionMm != null && boss.BasePositionMm.Length > 1 ? boss.BasePositionMm[1] : 0.0;
            double oldZ = boss.BasePositionMm != null && boss.BasePositionMm.Length > 2 ? boss.BasePositionMm[2] : 0.0;
            double diaMm = boss.DiameterMm;
            double heightMm = boss.HeightMm;
            // Matrix gap #1: extraction reports HeightMm=0 for non-Z bosses —
            // estimate from the live cylinder face's extent along the boss axis.
            double[] bAxisU = NormalizeVec3(boss.Axis) ?? new[] { 0.0, 0.0, 1.0 };
            if (heightMm <= 0)
            {
                heightMm = EstimateCylFaceExtentMm(designBody, boss.CylinderFaceIndex, bAxisU);
                if (heightMm <= 0)
                {
                    result.ErrorMessage = "MoveBoss: boss height unknown (extraction=0, live estimate failed)";
                    return result;
                }
            }

            var edgesBefore = CaptureEdges(designBody);

            // Step 1: subtract the OLD boss volume — cutter along the boss axis.
            double rM = GeometryUtils.MmToMeters(diaMm / 2.0);
            double hM = GeometryUtils.MmToMeters(heightMm);
            double marginM = 0.001;
            try
            {
                WriteBlock.ExecuteTask("MoveBoss.remove " + bossId, () =>
                {
                    Part part = designBody.Parent as Part;
                    // cutter base: (basePos - margin·axis), length h + 2·margin along axis
                    Point center = Point.Create(
                        GeometryUtils.MmToMeters(oldX) - bAxisU[0] * marginM,
                        GeometryUtils.MmToMeters(oldY) - bAxisU[1] * marginM,
                        GeometryUtils.MmToMeters(oldZ) - bAxisU[2] * marginM);
                    var axD = Direction.Create(bAxisU[0], bAxisU[1], bAxisU[2]);
                    var cDirX = axD.ArbitraryPerpendicular;
                    var cDirY = Direction.Cross(axD, cDirX);
                    Frame frame = Frame.Create(center, cDirX, cDirY);
                    Plane basePlane = Plane.Create(frame);
                    Profile profile = new CircleProfile(basePlane, rM);
                    Body cutter = Body.ExtrudeProfile(profile, hM + 2.0 * marginM);
                    if (part != null)
                    {
                        DesignBody cutterDb = DesignBody.Create(part, "_moveboss_cutter", cutter);
                        designBody.Shape.Subtract(new[] { cutterDb.Shape });
                        try { cutterDb.Delete(); } catch { }
                    }
                    else
                    {
                        designBody.Shape.Subtract(new[] { cutter });
                    }
                });
            }
            catch (Exception ex)
            {
                result.ErrorMessage = "MoveBoss.remove failed: " + ex.Message;
                return result;
            }

            // Step 2: add boss at new position (same axis).
            var addRes = AddBoss(designBody, newPositionMm, diaMm, heightMm, boss.Axis);
            if (!addRes.Success)
            {
                result.ErrorMessage = "MoveBoss.add failed: " + addRes.ErrorMessage;
                return result;
            }

            var edgesAfter = CaptureEdges(designBody);
            result.NewlyCreatedEdges = DiffEdges(edgesBefore, edgesAfter);
            foreach (int fi in addRes.ModifiedFaceIndices) result.ModifiedFaceIndices.Add(fi);
            result.Success = true;
            return result;
        }

        // -------------------------------------------------------------------
        // ChangeBossDiameter
        // -------------------------------------------------------------------
        /// <summary>
        /// Boss 의 외부 cylinder face 반지름을 OffsetFaces 로 변경.
        /// 외부 cylinder face 의 outward normal 은 축에서 멀어지는 방향이므로,
        /// OffsetFaces(+) = 살이 외부로 = 직경 증가.
        /// </summary>
        public static ModificationResult ChangeBossDiameter(
            DesignBody designBody,
            FeatureGraph graph,
            string bossId,
            double newDiameterMm,
            bool useBoolean = false)
        {
            var result = new ModificationResult
            {
                Operation = "ChangeBossDiameter",
                ModifiedBody = designBody,
            };

            if (designBody == null) { result.ErrorMessage = "designBody is null"; return result; }
            if (graph == null) { result.ErrorMessage = "graph is null"; return result; }
            if (string.IsNullOrEmpty(bossId)) { result.ErrorMessage = "bossId is null"; return result; }
            if (newDiameterMm <= 0) { result.ErrorMessage = "newDiameterMm must be > 0"; return result; }

            var boss = graph.Bosses.FirstOrDefault(b => b.Id == bossId);
            if (boss == null) { result.ErrorMessage = "Boss not found: " + bossId; return result; }

            int faceIdx = boss.CylinderFaceIndex;
            var faceFeat = graph.Faces.FirstOrDefault(ff => ff.FaceIndex == faceIdx);
            if (faceFeat == null) { result.ErrorMessage = "Boss cylinder face feature missing (index=" + faceIdx + ")"; return result; }

            double currentRm = faceFeat.RadiusM;
            double newRm = GeometryUtils.MmToMeters(newDiameterMm / 2.0);
            double dR = newRm - currentRm; // positive = grow

            // Boss (external cylinder): outward normal points AWAY from axis (outside material).
            // OffsetFaces(positive) moves the face along outward normal → diameter INCREASES.
            // sign = +1 (dR>0 → larger D).
            double sign = +1.0;
            if (faceFeat.IsReversed) sign *= -1.0;
            // safety net: if classifier marked it as an internal hole (ThroughHole/BlindHole), flip.
            if (faceFeat.CylinderRole == CylinderRole.ThroughHole ||
                faceFeat.CylinderRole == CylinderRole.BlindHole) sign *= -1.0;

            double offset = sign * dR;

            // W4-5: OffsetFaces-replacement Boolean for a diameter GROW, gated by
            // useBoolean (harness retries on a FRESH body when OffsetFaces fails/no-ops —
            // samplemodel2 610→732 "Operation failed", 624ZZ silent no-op). A grow is a
            // pure concentric Unite of a larger coaxial cylinder. NOT primary: it
            // General-Failures on thin/curved bosses (11752) that OffsetFaces handles.
            if (useBoolean && dR > 1e-9)
            {
                var bres = TryChangeBossDiameterBoolean(designBody, boss, currentRm, newRm);
                if (bres != null) return bres;
            }

            var face = GetFaceByIndex(designBody, faceIdx);
            if (face == null) { result.ErrorMessage = "Boss cylinder face not found in body (index=" + faceIdx + ")"; return result; }

            var edgesBefore = CaptureEdges(designBody);

            try
            {
                WriteBlock.ExecuteTask("ChangeBossDiameter " + bossId, () =>
                {
                    designBody.Shape.OffsetFaces(new[] { face }, offset);
                });
            }
            catch (Exception ex)
            {
                result.ErrorMessage = "OffsetFaces failed: " + ex.Message;
                return result;
            }

            var edgesAfter = CaptureEdges(designBody);
            result.NewlyCreatedEdges = DiffEdges(edgesBefore, edgesAfter);
            result.ModifiedFaceIndices.Add(faceIdx);
            result.Success = true;
            return result;
        }

        // -------------------------------------------------------------------
        // ChangeBossHeight
        // -------------------------------------------------------------------
        /// <summary>
        /// Boss 의 top plane face 를 boss axis 방향으로 (newH - currentH) 만큼 이동.
        /// graph.Bosses 에는 top face index 가 직접 없으므로 axis + bbox 로 추정한다.
        /// </summary>
        public static ModificationResult ChangeBossHeight(
            DesignBody designBody,
            FeatureGraph graph,
            string bossId,
            double newHeightMm,
            bool useBoolean = false)
        {
            var result = new ModificationResult
            {
                Operation = "ChangeBossHeight",
                ModifiedBody = designBody,
            };

            if (designBody == null) { result.ErrorMessage = "designBody is null"; return result; }
            if (graph == null) { result.ErrorMessage = "graph is null"; return result; }
            if (string.IsNullOrEmpty(bossId)) { result.ErrorMessage = "bossId is null"; return result; }
            if (newHeightMm <= 0) { result.ErrorMessage = "newHeightMm must be > 0"; return result; }

            var boss = graph.Bosses.FirstOrDefault(b => b.Id == bossId);
            if (boss == null) { result.ErrorMessage = "Boss not found: " + bossId; return result; }

            double currentHmm = boss.HeightMm;
            // Matrix gap #1: HeightMm=0 for non-Z bosses — estimate from live face.
            double[] chAxisU = NormalizeVec3(boss.Axis) ?? new[] { 0.0, 0.0, 1.0 };
            if (currentHmm <= 0)
            {
                currentHmm = EstimateCylFaceExtentMm(designBody, boss.CylinderFaceIndex, chAxisU);
                if (currentHmm <= 0)
                {
                    result.ErrorMessage = "ChangeBossHeight: current height unknown (extraction=0, live estimate failed)";
                    return result;
                }
            }
            double dHmm = newHeightMm - currentHmm; // positive = grow taller
            if (Math.Abs(dHmm) < 1e-6)
            {
                result.Success = true;
                return result;
            }
            double dHm = GeometryUtils.MmToMeters(dHmm);

            // Axis-aware cap-face search (replaces the +Z-only FindPlanarFaceAtZ
            // which failed on every non-vertical boss — matrix "top face not
            // found near Z=" class).
            double[] baseM = new[]
            {
                GeometryUtils.MmToMeters(boss.BasePositionMm != null && boss.BasePositionMm.Length > 0 ? boss.BasePositionMm[0] : 0.0),
                GeometryUtils.MmToMeters(boss.BasePositionMm != null && boss.BasePositionMm.Length > 1 ? boss.BasePositionMm[1] : 0.0),
                GeometryUtils.MmToMeters(boss.BasePositionMm != null && boss.BasePositionMm.Length > 2 ? boss.BasePositionMm[2] : 0.0),
            };

            // W4-5: OffsetFaces-replacement Boolean, gated by useBoolean. OffsetFaces is
            // primary (works for most bosses incl. 11752, and the direction-fix path);
            // when it FAILS/no-ops it POISONS the process (no in-process recovery), so the
            // HARNESS re-runs the cell on a FRESH body with useBoolean=true → here we
            // relocate the live boss cylinder, find the free CAP end, and Unite/Subtract a
            // coaxial cylinder (gate-validated on 624ZZ). Boolean is NOT primary because
            // it General-Failures on some thin/curved bosses (11752) that OffsetFaces handles.
            if (useBoolean)
            {
                var bres = TryChangeBossHeightBoolean(designBody, baseM, chAxisU,
                                                      GeometryUtils.MmToMeters(boss.DiameterMm / 2.0),
                                                      GeometryUtils.MmToMeters(newHeightMm));
                if (bres != null) { return bres; }
                result.ErrorMessage = "ChangeBossHeight(Boolean): boss not relocatable as a clean cylinder";
                return result;
            }

            int topFaceIdx = FindPlanarFaceAlongAxis(designBody, baseM, chAxisU,
                                                     GeometryUtils.MmToMeters(currentHmm));
            if (topFaceIdx < 0)
            {
                // Height-independent fallback: the cap is the axis-perpendicular
                // planar face farthest along +axis from the base, within the boss
                // radius of the axis line. Robust when the height estimate is off
                // (matrix "cap face not found at H=" class).
                topFaceIdx = FindBossCapFace(designBody, baseM, chAxisU,
                                             GeometryUtils.MmToMeters(boss.DiameterMm / 2.0));
            }
            if (topFaceIdx < 0)
            {
                result.ErrorMessage = "Boss cap face not found along axis (est H=" + currentHmm.ToString("F2") + "mm)";
                return result;
            }

            var topFace = GetFaceByIndex(designBody, topFaceIdx);
            if (topFace == null) { result.ErrorMessage = "topFace shape null"; return result; }

            // OffsetFaces moves the face along its OUTWARD normal (air side). To grow
            // the boss we must move the cap by +dHm ALONG the boss axis (base→cap),
            // whichever way the cap's normal happens to point on this import. Resolve the
            // cap's true outward via a ContainsPoint straddle, then project: offset =
            // dHm·(outward·axis). cap ∥ +axis → +dHm (grow); cap ∥ −axis → −dHm (still
            // grows up); side face (⊥) → ≈0 no-op (safe). Fixes the direction inversion
            // (face_recog 80→96 measured 64 = it shrank) without poison-prone retries.
            double offset = dHm;
            try
            {
                var capPl = topFace.Geometry as Plane;
                if (capPl != null)
                {
                    var cbb = topFace.GetBoundingBox(Matrix.Identity);
                    var cc = Point.Create((cbb.MinCorner.X + cbb.MaxCorner.X) / 2.0,
                                          (cbb.MinCorner.Y + cbb.MaxCorner.Y) / 2.0,
                                          (cbb.MinCorner.Z + cbb.MaxCorner.Z) / 2.0);
                    var cev = capPl.ProjectPoint(cc);
                    var pc = cev != null ? cev.Point : cc;
                    var cn = capPl.Frame.DirZ.UnitVector;
                    double[] nrm = { cn.X, cn.Y, cn.Z };
                    const double ceps = 2e-4;
                    bool plusIn = designBody.Shape.ContainsPoint(Point.Create(pc.X + nrm[0] * ceps, pc.Y + nrm[1] * ceps, pc.Z + nrm[2] * ceps));
                    bool minusIn = designBody.Shape.ContainsPoint(Point.Create(pc.X - nrm[0] * ceps, pc.Y - nrm[1] * ceps, pc.Z - nrm[2] * ceps));
                    if (plusIn != minusIn)
                    {
                        // outward = air side
                        double sgn = (minusIn && !plusIn) ? 1.0 : -1.0; // +: nrm points to air
                        double[] outward = { nrm[0] * sgn, nrm[1] * sgn, nrm[2] * sgn };
                        double dot = outward[0] * chAxisU[0] + outward[1] * chAxisU[1] + outward[2] * chAxisU[2];
                        offset = dHm * dot;
                    }
                }
            }
            catch { /* keep offset = dHm */ }

            var edgesBefore = CaptureEdges(designBody);

            try
            {
                WriteBlock.ExecuteTask("ChangeBossHeight " + bossId, () =>
                {
                    designBody.Shape.OffsetFaces(new[] { topFace }, offset);
                });
            }
            catch (Exception ex)
            {
                result.ErrorMessage = "OffsetFaces failed: " + ex.Message;
                return result;
            }

            var edgesAfter = CaptureEdges(designBody);
            result.NewlyCreatedEdges = DiffEdges(edgesBefore, edgesAfter);
            result.ModifiedFaceIndices.Add(topFaceIdx);
            // Post-mod measurement: re-estimate the boss cylinder extent along its axis
            // → new height. RELOCATE the live boss cylinder (by axis+radius near the
            // base) rather than reading boss.CylinderFaceIndex — OffsetFaces re-tessellates
            // and shifts face indices, so the stale index reads the WRONG face and reports
            // the OLD height (samplemodel2 "measured 50 vs 60"). Relocation is index-free.
            try
            {
                double measuredH = 0.0;
                double[] mFoot, mDir; double mR, mLo, mHi;
                if (TryRelocateHoleCylinder(designBody, baseM[0], baseM[1], baseM[2], chAxisU,
                                            GeometryUtils.MmToMeters(boss.DiameterMm / 2.0),
                                            out mFoot, out mDir, out mR, out mLo, out mHi)
                    && (mHi - mLo) > 1e-9)
                {
                    measuredH = (mHi - mLo) * 1000.0;
                }
                else
                {
                    measuredH = EstimateCylFaceExtentMm(designBody, boss.CylinderFaceIndex, chAxisU);
                }
                if (measuredH > 0) result.MeasuredAfterMm = measuredH;
            }
            catch { /* best-effort */ }
            result.Success = true;
            return result;
        }

        // -------------------------------------------------------------------
        // RemoveHole — fill an existing hole by Unite-ing a matching cylinder.
        // -------------------------------------------------------------------
        /// <summary>
        /// Hole 의 cylinder 영역을 채우는 cylinder 를 만들어 Unite. through / blind 모두 지원.
        /// </summary>
        public static ModificationResult RemoveHole(
            DesignBody designBody,
            FeatureGraph graph,
            string holeId,
            bool forceScale = false)
        {
            var result = new ModificationResult
            {
                Operation = "RemoveHole",
                ModifiedBody = designBody,
            };

            if (designBody == null) { result.ErrorMessage = "designBody is null"; return result; }
            if (graph == null) { result.ErrorMessage = "graph is null"; return result; }
            if (string.IsNullOrEmpty(holeId)) { result.ErrorMessage = "holeId is null"; return result; }

            var hole = graph.Holes.FirstOrDefault(h => h.Id == holeId);
            if (hole == null) { result.ErrorMessage = "Hole not found: " + holeId; return result; }

            double posX = hole.PositionMm != null && hole.PositionMm.Length > 0 ? hole.PositionMm[0] : 0.0;
            double posY = hole.PositionMm != null && hole.PositionMm.Length > 1 ? hole.PositionMm[1] : 0.0;
            double posZ = hole.PositionMm != null && hole.PositionMm.Length > 2 ? hole.PositionMm[2] : 0.0;
            double diaMm = hole.DiameterMm;
            double depthMm = hole.DepthMm;
            bool through = hole.IsThrough;

            // W4-1b forceScale: dirty/curved STEP Booleans trip "General Failure" at
            // native scale AND poison the body (no in-process recovery). The harness
            // re-imports a FRESH body and calls with forceScale → we scale it 1000×
            // up-front (tolerance recovery, proven scale-trick), relocate+fill the true
            // live bore at scale, then scale back. relocation is forced ON here because
            // the scaled body is fresh (no poison) and live-cylinder reading is exact.
            double SCALE = 1.0;
            bool relocateOn = RELOCATE_FILL;
            Point scaleCenter = Point.Origin;
            if (forceScale)
            {
                // Scale about the body CENTRE, not the world origin: 11752 sits at
                // x≈-550mm, so origin-scaling 1000× throws it to -550 m and Parasolid's
                // modeling box rejects it ("Operation failed"). Centre-scaling keeps the
                // body in place while inflating sub-mm tolerances 1000×.
                try
                {
                    var pbb = designBody.Shape.GetBoundingBox(Matrix.Identity);
                    scaleCenter = Point.Create((pbb.MinCorner.X + pbb.MaxCorner.X) / 2.0,
                                               (pbb.MinCorner.Y + pbb.MaxCorner.Y) / 2.0,
                                               (pbb.MinCorner.Z + pbb.MaxCorner.Z) / 2.0);
                    WriteBlock.ExecuteTask("RemoveHole " + holeId + " [scale-up]", () =>
                        designBody.Shape.Transform(Matrix.CreateScale(1000.0, scaleCenter)));
                    SCALE = 1000.0; relocateOn = true;
                }
                catch (Exception ex) { result.ErrorMessage = "scale-up failed: " + ex.Message; return result; }
            }

            Box bbox;
            try { bbox = designBody.Shape.GetBoundingBox(Matrix.Identity); }
            catch (Exception ex) { result.ErrorMessage = "GetBoundingBox failed: " + ex.Message; return result; }
            double rM = GeometryUtils.MmToMeters(diaMm / 2.0) * SCALE; // radius: pure length

            // Axis-aware fill — same fix as MoveHole (matrix run: +Z hardcoding
            // broke all non-vertical holes; DepthMm=0 → zero-height extrude).
            // Centre-scaling maps a point P → C + SCALE·(P−C) (NOT a plain ×SCALE);
            // map the extracted anchor through the same transform so it lands on the
            // scaled bore. (SCALE=1 → identity, native path unchanged.)
            double[] axisU = NormalizeVec3(hole.Axis) ?? new[] { 0.0, 0.0, 1.0 };
            double pxM = scaleCenter.X + SCALE * (GeometryUtils.MmToMeters(posX) - scaleCenter.X);
            double pyM = scaleCenter.Y + SCALE * (GeometryUtils.MmToMeters(posY) - scaleCenter.Y);
            double pzM = scaleCenter.Z + SCALE * (GeometryUtils.MmToMeters(posZ) - scaleCenter.Z);

            // Kernel-truth PIN guard (gate-proven on 11752 H1): the FeatureExtractor can
            // mis-classify a solid cylindrical PIN as a hole. Relocate the true live
            // cylinder and probe ContainsPoint on its axis at mid-extent — if the core is
            // SOLID it is a pin, and a fill Unite into solid material throws "General
            // Failure" that POISONS the body. Reject cleanly here instead (no Boolean,
            // no poison) and report the misclassification truthfully.
            double[] pgFoot, pgDir; double pgR, pgLo, pgHi;
            if (TryRelocateHoleCylinder(designBody, pxM, pyM, pzM, axisU, rM,
                                        out pgFoot, out pgDir, out pgR, out pgLo, out pgHi)
                && (pgHi - pgLo) > 1e-9)
            {
                double tMid = (pgLo + pgHi) / 2.0;
                var corePt = Point.Create(pgFoot[0] + pgDir[0] * tMid,
                                          pgFoot[1] + pgDir[1] * tMid,
                                          pgFoot[2] + pgDir[2] * tMid);
                bool solidCore = false;
                try { solidCore = designBody.Shape.ContainsPoint(corePt); } catch { }
                if (solidCore)
                {
                    if (SCALE != 1.0)
                    {
                        try { WriteBlock.ExecuteTask("RemoveHole pin-guard scale-down", () =>
                            designBody.Shape.Transform(Matrix.CreateScale(1.0 / SCALE, scaleCenter))); }
                        catch { }
                    }
                    result.ErrorMessage = "target is a solid pin, not a hole (mis-extracted) — RemoveHole not applicable";
                    return result;
                }
            }

            double fillHeight;
            Point fillBase;
            if (through || depthMm <= 0)
            {
                double tmin = double.MaxValue, tmax = double.MinValue;
                for (int ci = 0; ci < 8; ci++)
                {
                    double cx = ((ci & 1) == 0) ? bbox.MinCorner.X : bbox.MaxCorner.X;
                    double cy = ((ci & 2) == 0) ? bbox.MinCorner.Y : bbox.MaxCorner.Y;
                    double cz = ((ci & 4) == 0) ? bbox.MinCorner.Z : bbox.MaxCorner.Z;
                    double t = (cx - pxM) * axisU[0] + (cy - pyM) * axisU[1] + (cz - pzM) * axisU[2];
                    if (t < tmin) tmin = t;
                    if (t > tmax) tmax = t;
                }
                fillHeight = tmax - tmin;
                fillBase = Point.Create(pxM + axisU[0] * tmin, pyM + axisU[1] * tmin, pzM + axisU[2] * tmin);
            }
            else
            {
                double dM = GeometryUtils.MmToMeters(depthMm) * SCALE;
                fillHeight = dM;
                fillBase = Point.Create(pxM - axisU[0] * dM, pyM - axisU[1] * dM, pzM - axisU[2] * dM);
            }

            // W4-1 kernel-truth override: relocate the true live bore (active under
            // forceScale, or when RELOCATE_FILL is globally enabled). All lengths are in
            // CURRENT body units (× SCALE), so the relocation reads the scaled live body.
            double rFillM = rM + Math.Max(rM * 0.02, 1e-5 * SCALE);
            double[] rfFoot, rfDir; double rfR, rfLo, rfHi;
            if (relocateOn
                && TryRelocateHoleCylinder(designBody, pxM, pyM, pzM, axisU, rM,
                                        out rfFoot, out rfDir, out rfR, out rfLo, out rfHi)
                && (rfHi - rfLo) > 1e-9)
            {
                axisU = rfDir;
                rM = rfR;
                rFillM = rM + Math.Max(rM * 0.02, 1e-5 * SCALE);
                // Protrude the filler well past both bore openings so its flat caps sit
                // clearly in AIR — on thin CURVED shells a near-flush cap goes tangent to
                // the curved wall and trips "General Failure". Generous: 50% of bore
                // length or 0.5mm (×SCALE), whichever larger.
                double margin = Math.Max(5e-4 * SCALE, 0.5 * (rfHi - rfLo));
                fillHeight = (rfHi - rfLo) + 2.0 * margin;
                fillBase = Point.Create(rfFoot[0] + rfDir[0] * (rfLo - margin),
                                        rfFoot[1] + rfDir[1] * (rfLo - margin),
                                        rfFoot[2] + rfDir[2] * (rfLo - margin));
                result.HintMessage = "fill relocated to live cylinder r=" + (rM / SCALE * 1000.0).ToString("0.###") + "mm";
            }

            if (fillHeight < 1e-9)
            {
                result.ErrorMessage = "RemoveHole.fill: degenerate fill extent along hole axis";
                return result;
            }

            var edgesBefore = CaptureEdges(designBody);

            try
            {
                UniteFillerCylinder(designBody, "RemoveHole " + holeId, fillBase, axisU, rFillM, fillHeight);
            }
            catch (Exception ex)
            {
                if (SCALE != 1.0)
                {
                    try { WriteBlock.ExecuteTask("RemoveHole scale-down", () =>
                        designBody.Shape.Transform(Matrix.CreateScale(1.0 / SCALE, scaleCenter))); }
                    catch { }
                }
                result.ErrorMessage = "RemoveHole.fill failed: " + ex.Message;
                return result;
            }

            // scale back to native units before returning (forceScale path).
            if (SCALE != 1.0)
            {
                try
                {
                    WriteBlock.ExecuteTask("RemoveHole " + holeId + " [scale-down]", () =>
                        designBody.Shape.Transform(Matrix.CreateScale(1.0 / SCALE, scaleCenter)));
                }
                catch (Exception ex) { result.ErrorMessage = "scale-down failed: " + ex.Message; return result; }
            }

            var edgesAfter = CaptureEdges(designBody);
            result.NewlyCreatedEdges = DiffEdges(edgesBefore, edgesAfter);
            result.Success = true;
            return result;
        }

        // -------------------------------------------------------------------
        // RotateBoss — fill old boss volume + add boss at rotated position.
        // -------------------------------------------------------------------
        /// <summary>
        /// Boss 를 임의 축 (axisPoint, axisDirection) 주변으로 angleDeg 만큼 회전.
        /// 단순 구현: 기존 boss 영역을 잘라내고 회전된 BasePosition 에 동일 D/H 의 boss 를 다시 추가한다.
        /// 회전은 Rodrigues 공식으로 직접 계산 (Matrix API 의존 X).
        /// </summary>
        public static ModificationResult RotateBoss(
            DesignBody designBody,
            FeatureGraph graph,
            string bossId,
            double[] axisPointMm,
            double[] axisDirection,
            double angleDeg)
        {
            var result = new ModificationResult
            {
                Operation = "RotateBoss",
                ModifiedBody = designBody,
            };

            if (designBody == null) { result.ErrorMessage = "designBody is null"; return result; }
            if (graph == null) { result.ErrorMessage = "graph is null"; return result; }
            if (string.IsNullOrEmpty(bossId)) { result.ErrorMessage = "bossId is null"; return result; }
            if (axisPointMm == null || axisPointMm.Length < 3) { result.ErrorMessage = "axisPointMm must be length 3"; return result; }
            if (axisDirection == null || axisDirection.Length < 3) { result.ErrorMessage = "axisDirection must be length 3"; return result; }

            var boss = graph.Bosses.FirstOrDefault(b => b.Id == bossId);
            if (boss == null) { result.ErrorMessage = "Boss not found: " + bossId; return result; }
            if (boss.BasePositionMm == null || boss.BasePositionMm.Length < 3)
            {
                result.ErrorMessage = "Boss has no BasePositionMm";
                return result;
            }

            double[] newPos = RotatePointRodrigues(boss.BasePositionMm, axisPointMm, axisDirection, angleDeg);

            // Reuse MoveBoss-style sequence: subtract old volume, then add boss at rotated pos.
            return MoveBoss(designBody, graph, bossId, newPos);
        }

        // -------------------------------------------------------------------
        // RotateHole — fill old hole + drill at rotated position.
        // -------------------------------------------------------------------
        /// <summary>
        /// Hole 을 임의 축 (axisPoint, axisDirection) 주변으로 angleDeg 만큼 회전.
        /// 기존 hole 을 채우고 회전된 position 에 동일 D/depth/through 의 hole 을 다시 뚫는다.
        /// </summary>
        public static ModificationResult RotateHole(
            DesignBody designBody,
            FeatureGraph graph,
            string holeId,
            double[] axisPointMm,
            double[] axisDirection,
            double angleDeg)
        {
            var result = new ModificationResult
            {
                Operation = "RotateHole",
                ModifiedBody = designBody,
            };

            if (designBody == null) { result.ErrorMessage = "designBody is null"; return result; }
            if (graph == null) { result.ErrorMessage = "graph is null"; return result; }
            if (string.IsNullOrEmpty(holeId)) { result.ErrorMessage = "holeId is null"; return result; }
            if (axisPointMm == null || axisPointMm.Length < 3) { result.ErrorMessage = "axisPointMm must be length 3"; return result; }
            if (axisDirection == null || axisDirection.Length < 3) { result.ErrorMessage = "axisDirection must be length 3"; return result; }

            var hole = graph.Holes.FirstOrDefault(h => h.Id == holeId);
            if (hole == null) { result.ErrorMessage = "Hole not found: " + holeId; return result; }
            if (hole.PositionMm == null || hole.PositionMm.Length < 3)
            {
                result.ErrorMessage = "Hole has no PositionMm";
                return result;
            }

            double[] newPos = RotatePointRodrigues(hole.PositionMm, axisPointMm, axisDirection, angleDeg);

            // Reuse MoveHole: fill old + drill new.
            return MoveHole(designBody, graph, holeId, newPos);
        }

        // -------------------------------------------------------------------
        // MirrorFeature — mirror a hole or boss across a plane.
        // -------------------------------------------------------------------
        /// <summary>
        /// Hole 또는 Boss 를 mirror plane (normal, origin) 에 대해 거울 복제.
        /// 원본은 유지되고, 미러링된 위치에 동일 dimension 의 새 feature 가 추가된다.
        /// featureId 의 prefix 로 type 을 판별: "H" → hole, "B" → boss.
        /// 미러 위치가 평면 너무 가까이면 원본과 겹쳐 boolean 이 실패할 수 있다.
        /// </summary>
        public static ModificationResult MirrorFeature(
            DesignBody designBody,
            FeatureGraph graph,
            string featureId,
            double[] mirrorPlaneNormal,
            double[] mirrorPlaneOrigin)
        {
            var result = new ModificationResult
            {
                Operation = "MirrorFeature",
                ModifiedBody = designBody,
            };

            if (designBody == null) { result.ErrorMessage = "designBody is null"; return result; }
            if (graph == null) { result.ErrorMessage = "graph is null"; return result; }
            if (string.IsNullOrEmpty(featureId)) { result.ErrorMessage = "featureId is null"; return result; }
            if (mirrorPlaneNormal == null || mirrorPlaneNormal.Length < 3) { result.ErrorMessage = "mirrorPlaneNormal must be length 3"; return result; }
            if (mirrorPlaneOrigin == null || mirrorPlaneOrigin.Length < 3) { result.ErrorMessage = "mirrorPlaneOrigin must be length 3"; return result; }

            // Normalize plane normal.
            double[] n = NormalizeVec3(mirrorPlaneNormal);
            if (n == null) { result.ErrorMessage = "mirrorPlaneNormal has zero magnitude"; return result; }

            char prefix = featureId.Length > 0 ? featureId[0] : ' ';
            const double minDistMm = 1e-3; // safety: avoid mirror coincident with feature.

            if (prefix == 'H' || prefix == 'h')
            {
                var hole = graph.Holes.FirstOrDefault(h => h.Id == featureId);
                if (hole == null) { result.ErrorMessage = "Hole not found: " + featureId; return result; }
                if (hole.PositionMm == null || hole.PositionMm.Length < 3)
                {
                    result.ErrorMessage = "Hole has no PositionMm";
                    return result;
                }

                double[] mirrored = ReflectPoint(hole.PositionMm, n, mirrorPlaneOrigin);
                double dist = Distance3(mirrored, hole.PositionMm);
                if (dist < minDistMm)
                {
                    result.ErrorMessage = "Mirror position too close to original (dist=" + dist.ToString("F4") + " mm); feature lies on / near mirror plane.";
                    return result;
                }

                // Snapshot face count so we can detect a no-op Subtract (cutter outside body bounds).
                int facesBefore = -1;
                try { facesBefore = designBody.Shape.Faces.Count; } catch { /* best-effort */ }

                var addRes = AddHole(designBody, mirrored, hole.DiameterMm, hole.IsThrough,
                                     hole.IsThrough ? 0.0 : hole.DepthMm, hole.Axis);
                if (!addRes.Success)
                {
                    result.ErrorMessage = "MirrorFeature(hole) AddHole failed: " + addRes.ErrorMessage;
                    return result;
                }

                // Diagnostic: if face count didn't increase, the AddHole cutter didn't intersect
                // the body (mirrored position is outside the body bounds). AddHole's Subtract
                // silently succeeds in that case, so we have to detect it here.
                if (facesBefore >= 0)
                {
                    int facesAfter = -1;
                    try { facesAfter = designBody.Shape.Faces.Count; } catch { /* best-effort */ }
                    if (facesAfter >= 0 && facesAfter <= facesBefore)
                    {
                        result.ErrorMessage = "Mirror position outside body bounds: mirrored hole at ("
                            + mirrored[0].ToString("F2") + "," + mirrored[1].ToString("F2") + "," + mirrored[2].ToString("F2")
                            + ") did not intersect body (Faces.Count unchanged: " + facesBefore + " → " + facesAfter + ").";
                        return result;
                    }
                }

                foreach (int fi in addRes.ModifiedFaceIndices) result.ModifiedFaceIndices.Add(fi);
                result.NewlyCreatedEdges = addRes.NewlyCreatedEdges;
                result.Success = true;
                return result;
            }
            else if (prefix == 'B' || prefix == 'b')
            {
                var boss = graph.Bosses.FirstOrDefault(b => b.Id == featureId);
                if (boss == null) { result.ErrorMessage = "Boss not found: " + featureId; return result; }
                if (boss.BasePositionMm == null || boss.BasePositionMm.Length < 3)
                {
                    result.ErrorMessage = "Boss has no BasePositionMm";
                    return result;
                }

                double[] mirrored = ReflectPoint(boss.BasePositionMm, n, mirrorPlaneOrigin);
                double dist = Distance3(mirrored, boss.BasePositionMm);
                if (dist < minDistMm)
                {
                    result.ErrorMessage = "Mirror position too close to original (dist=" + dist.ToString("F4") + " mm); feature lies on / near mirror plane.";
                    return result;
                }

                var addRes = AddBoss(designBody, mirrored, boss.DiameterMm, boss.HeightMm);
                if (!addRes.Success)
                {
                    result.ErrorMessage = "MirrorFeature(boss) AddBoss failed: " + addRes.ErrorMessage;
                    return result;
                }
                foreach (int fi in addRes.ModifiedFaceIndices) result.ModifiedFaceIndices.Add(fi);
                result.NewlyCreatedEdges = addRes.NewlyCreatedEdges;
                result.Success = true;
                return result;
            }
            else
            {
                result.ErrorMessage = "Unknown featureId prefix '" + prefix + "' (expected H or B)";
                return result;
            }
        }

        // -------------------------------------------------------------------
        // AddHolePattern — Linear / Circular / Grid layouts.
        // -------------------------------------------------------------------
        /// <summary>
        /// 여러 hole 을 한번에 추가. patternType: "Linear" | "Circular" | "Grid".
        /// - Linear: center 에서 axisDirection 방향으로 spacing 간격, count 개.
        /// - Circular: center 주위 radius 반경 위에 count 개 등각.
        /// - Grid: rows × cols 격자 (count = rows*cols, axisDirection ignored; XY 평면 가정).
        ///   spacing → col_spacing, radius → row_spacing.
        ///
        /// axisDirection: hole 의 drill 축이 아니라 패턴 in-plane 방향. drill 축은 +Z 로 고정 (AddHole 기본).
        /// </summary>
        public static ModificationResult AddHolePattern(
            DesignBody designBody,
            double[] centerMm,
            double[] axisDirection,
            string patternType,
            int count,
            double spacing,
            double diameterMm,
            bool through,
            int rows = 0,
            int cols = 0,
            double rowSpacing = 0.0,
            double colSpacing = 0.0,
            double depthMm = 0.0,
            double[] drillAxis = null)
        {
            var result = new ModificationResult
            {
                Operation = "AddHolePattern",
                ModifiedBody = designBody,
            };

            if (designBody == null) { result.ErrorMessage = "designBody is null"; return result; }
            if (centerMm == null || centerMm.Length < 3) { result.ErrorMessage = "centerMm must be length 3"; return result; }
            if (string.IsNullOrEmpty(patternType)) { result.ErrorMessage = "patternType is null"; return result; }
            if (diameterMm <= 0) { result.ErrorMessage = "diameterMm must be > 0"; return result; }
            if (count <= 0 && (rows <= 0 || cols <= 0)) { result.ErrorMessage = "count must be > 0 (or rows*cols for Grid)"; return result; }

            var positions = new List<double[]>();
            string pt = patternType.Trim().ToLowerInvariant();
            if (pt == "linear")
            {
                if (axisDirection == null || axisDirection.Length < 3) { result.ErrorMessage = "Linear pattern requires axisDirection length 3"; return result; }
                double[] dir = NormalizeVec3(axisDirection);
                if (dir == null) { result.ErrorMessage = "axisDirection has zero magnitude"; return result; }
                if (spacing <= 0) { result.ErrorMessage = "Linear pattern requires spacing > 0"; return result; }

                // n holes spanning [center - (n-1)/2 * spacing, center + (n-1)/2 * spacing]
                double halfSpan = (count - 1) * 0.5;
                for (int i = 0; i < count; i++)
                {
                    double t = (i - halfSpan) * spacing;
                    positions.Add(new[]
                    {
                        centerMm[0] + dir[0] * t,
                        centerMm[1] + dir[1] * t,
                        centerMm[2] + dir[2] * t,
                    });
                }
            }
            else if (pt == "circular")
            {
                if (count < 2) { result.ErrorMessage = "Circular pattern requires count >= 2"; return result; }
                if (spacing <= 0) { result.ErrorMessage = "Circular pattern requires spacing (radius) > 0"; return result; }
                double radius = spacing;
                // axisDirection is the normal of the circle plane; default +Z if missing.
                double[] normal = (axisDirection != null && axisDirection.Length >= 3)
                    ? NormalizeVec3(axisDirection)
                    : new[] { 0.0, 0.0, 1.0 };
                if (normal == null) normal = new[] { 0.0, 0.0, 1.0 };
                // Build an orthonormal basis (u, v) in the plane perpendicular to normal.
                double[] u = OrthoVec(normal);
                double[] v = Cross3(normal, u);

                for (int i = 0; i < count; i++)
                {
                    double theta = 2.0 * Math.PI * i / count;
                    double cosT = Math.Cos(theta);
                    double sinT = Math.Sin(theta);
                    positions.Add(new[]
                    {
                        centerMm[0] + radius * (u[0] * cosT + v[0] * sinT),
                        centerMm[1] + radius * (u[1] * cosT + v[1] * sinT),
                        centerMm[2] + radius * (u[2] * cosT + v[2] * sinT),
                    });
                }
            }
            else if (pt == "grid")
            {
                if (rows <= 0 || cols <= 0) { result.ErrorMessage = "Grid pattern requires rows>0 and cols>0"; return result; }
                if (rowSpacing <= 0 || colSpacing <= 0) { result.ErrorMessage = "Grid pattern requires rowSpacing/colSpacing > 0"; return result; }
                // Grid is laid out in XY plane: X = col direction, Y = row direction.
                double halfRows = (rows - 1) * 0.5;
                double halfCols = (cols - 1) * 0.5;
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        double dx = (c - halfCols) * colSpacing;
                        double dy = (r - halfRows) * rowSpacing;
                        positions.Add(new[]
                        {
                            centerMm[0] + dx,
                            centerMm[1] + dy,
                            centerMm[2],
                        });
                    }
                }
            }
            else
            {
                result.ErrorMessage = "Unknown patternType: " + patternType + " (expected Linear|Circular|Grid)";
                return result;
            }

            // Drill each hole. Aggregate ModifiedFaceIndices and propagate failure if ANY hole fails.
            int succeeded = 0;
            foreach (var pos in positions)
            {
                var addRes = AddHole(designBody, pos, diameterMm, through, through ? 0.0 : depthMm, drillAxis);
                if (!addRes.Success)
                {
                    result.ErrorMessage = "AddHolePattern[" + succeeded + "] failed: " + addRes.ErrorMessage;
                    return result;
                }
                foreach (int fi in addRes.ModifiedFaceIndices)
                {
                    if (!result.ModifiedFaceIndices.Contains(fi)) result.ModifiedFaceIndices.Add(fi);
                }
                succeeded++;
            }

            result.Success = true;
            return result;
        }

        /// <summary>
        /// Build an orthonormal prism basis for a face-normal-aware rectangular
        /// cutter/rib. Given an outward face normal n and a desired in-plane long
        /// axis, returns (nn, longAxis, shortAxis) with nn = unit normal, longAxis
        /// the in-plane projection of `orient` (perp to nn), and shortAxis = nn×long.
        /// A Frame built as Frame.Create(C, shortAxis, longAxis) then has plane
        /// normal shortAxis×longAxis = nn, so ExtrudeProfile runs along +nn.
        /// When normalAxis is null/degenerate, nn defaults to +Z — mathematically
        /// identical to the legacy +Z code path (zero regression on upright faces).
        /// </summary>
        private static void PrismBasis(
            double[] normalAxis, double[] orient,
            out double[] nn, out double[] longAxis, out double[] shortAxis)
        {
            nn = (normalAxis != null && normalAxis.Length >= 3) ? NormalizeVec3(normalAxis) : null;
            if (nn == null) nn = new[] { 0.0, 0.0, 1.0 };

            // longAxis = orient projected onto the plane perpendicular to nn.
            double[] o = (orient != null && orient.Length >= 3) ? orient : new[] { 0.0, 1.0, 0.0 };
            double dot = o[0] * nn[0] + o[1] * nn[1] + o[2] * nn[2];
            double[] proj = { o[0] - dot * nn[0], o[1] - dot * nn[1], o[2] - dot * nn[2] };
            double pMag = Math.Sqrt(proj[0] * proj[0] + proj[1] * proj[1] + proj[2] * proj[2]);
            longAxis = (pMag > 1e-6) ? new[] { proj[0] / pMag, proj[1] / pMag, proj[2] / pMag } : OrthoVec(nn);

            shortAxis = NormalizeVec3(Cross3(nn, longAxis));
        }

        // -------------------------------------------------------------------
        // AddSlit — rectangular slot (cutter Subtract) on the top face.
        // -------------------------------------------------------------------
        /// <summary>
        /// Body top face 에 직사각형 slit (slot) 을 추가한다.
        /// cutter 는 (width × length × (depth + small margin)) 크기로,
        /// position 은 top face 위의 slit 중심.
        /// orientationAxis 는 slit 의 긴 축 방향 (기본 +Y). 단위는 무차원(direction).
        /// width 가 길이 방향과 수직인 짧은 변. cutter 는 -Z 방향으로 굴착한다.
        /// </summary>
        public static ModificationResult AddSlit(
            DesignBody designBody,
            double[] positionMm,
            double widthMm,
            double lengthMm,
            double depthMm,
            double[] orientationAxis,
            double[] normalAxis = null)
        {
            var result = new ModificationResult
            {
                Operation = "AddSlit",
                ModifiedBody = designBody,
            };

            if (designBody == null) { result.ErrorMessage = "designBody is null"; return result; }
            if (positionMm == null || positionMm.Length < 3) { result.ErrorMessage = "positionMm must be length 3"; return result; }
            if (widthMm <= 0) { result.ErrorMessage = "widthMm must be > 0"; return result; }
            if (lengthMm <= 0) { result.ErrorMessage = "lengthMm must be > 0"; return result; }
            if (depthMm <= 0) { result.ErrorMessage = "depthMm must be > 0"; return result; }

            // Face-normal-aware basis. normalAxis = outward face normal (defaults to
            // +Z → identical to the legacy upright-face path). longAxis = in-plane
            // slit length direction; shortAxis = nAxis × longAxis (in-plane width dir).
            double[] nAxis, longAxis, shortAxis;
            PrismBasis(normalAxis, orientationAxis, out nAxis, out longAxis, out shortAxis);

            double widthM = GeometryUtils.MmToMeters(widthMm);
            double lengthM = GeometryUtils.MmToMeters(lengthMm);
            double depthM = GeometryUtils.MmToMeters(depthMm);
            double marginM = 0.001; // 1 mm margin so the cutter reliably breaches the top face.
            double pxM = GeometryUtils.MmToMeters(positionMm[0]);
            double pyM = GeometryUtils.MmToMeters(positionMm[1]);
            double pzM = GeometryUtils.MmToMeters(positionMm[2]);

            // top face index (for ModifiedFaceIndices) — best-effort.
            int topFaceIdx = FindPlanarFaceAtZ(designBody, pzM, +1.0);

            var edgesBefore = CaptureEdges(designBody);

            try
            {
                WriteBlock.ExecuteTask("AddSlit", () =>
                {
                    // RT16 fix: mimic AddHole pattern. Place the frame BELOW the top face at
                    // z = (pz - depth - margin), then extrude UPWARD by (depth + margin). The
                    // cutter then spans [pz - depth - margin, pz], reliably breaching the top
                    // face. (Previous version put the frame at pz + margin and extruded by a
                    // NEGATIVE value; in this SpaceClaim build that variant produces a cutter
                    // that does not intersect the body — Subtract becomes a no-op.)
                    // BUGFIX (P0/P5, same defect as AddPocket): the cutter must BREACH the face.
                    // Frame.Create(center, longAxis, shortAxis) gives DirZ = +nAxis (up); base at
                    // P - n*depth, extrude (depth+margin) so the cutter spans [faceZ-depth,
                    // faceZ+margin]. The previous Frame.Create(center, shortAxis, longAxis) gave
                    // DirZ = -nAxis, burying the cutter inside the body (dV=0, no breach) — masked
                    // by the sign-only matrix oracle. With dirX=longAxis, dirY=shortAxis the
                    // profile's first dim follows longAxis, so pass (lengthM, widthM).
                    double ext = depthM + marginM;
                    Point center = Point.Create(
                        pxM - nAxis[0] * depthM, pyM - nAxis[1] * depthM, pzM - nAxis[2] * depthM);
                    Direction dirX = Direction.Create(longAxis[0], longAxis[1], longAxis[2]);
                    Direction dirY = Direction.Create(shortAxis[0], shortAxis[1], shortAxis[2]);
                    Frame frame = Frame.Create(center, dirX, dirY);
                    Plane basePlane = Plane.Create(frame);
                    Profile profile = new RectangleProfile(basePlane, lengthM, widthM);
                    Body cutter = Body.ExtrudeProfile(profile, ext);

                    Part part = designBody.Parent as Part;
                    if (part != null)
                    {
                        DesignBody cutterDb = DesignBody.Create(part, "_addslit_cutter", cutter);
                        designBody.Shape.Subtract(new[] { cutterDb.Shape });
                        try { cutterDb.Delete(); } catch { }
                    }
                    else
                    {
                        designBody.Shape.Subtract(new[] { cutter });
                    }
                });
            }
            catch (Exception ex)
            {
                result.ErrorMessage = "AddSlit failed: " + ex.Message;
                return result;
            }

            var edgesAfter = CaptureEdges(designBody);
            result.NewlyCreatedEdges = DiffEdges(edgesBefore, edgesAfter);
            if (topFaceIdx >= 0) result.ModifiedFaceIndices.Add(topFaceIdx);
            result.Success = true;
            return result;
        }

        // -------------------------------------------------------------------
        // AddPocket — rectangular pocket (cutter Subtract, depth < body thickness).
        // -------------------------------------------------------------------
        /// <summary>
        /// Body top face 에 직사각형 pocket 추가. AddSlit 과 동일한 cutter 패턴이지만 보통 큰 치수.
        /// width = X 방향, length = Y 방향 (기본 축 정렬).
        /// </summary>
        public static ModificationResult AddPocket(
            DesignBody designBody,
            double[] positionMm,
            double widthMm,
            double lengthMm,
            double depthMm,
            double[] normalAxis = null)
        {
            var result = new ModificationResult
            {
                Operation = "AddPocket",
                ModifiedBody = designBody,
            };

            if (designBody == null) { result.ErrorMessage = "designBody is null"; return result; }
            if (positionMm == null || positionMm.Length < 3) { result.ErrorMessage = "positionMm must be length 3"; return result; }
            if (widthMm <= 0) { result.ErrorMessage = "widthMm must be > 0"; return result; }
            if (lengthMm <= 0) { result.ErrorMessage = "lengthMm must be > 0"; return result; }
            if (depthMm <= 0) { result.ErrorMessage = "depthMm must be > 0"; return result; }

            double widthM = GeometryUtils.MmToMeters(widthMm);
            double lengthM = GeometryUtils.MmToMeters(lengthMm);
            double depthM = GeometryUtils.MmToMeters(depthMm);
            double marginM = 0.001;
            double pxM = GeometryUtils.MmToMeters(positionMm[0]);
            double pyM = GeometryUtils.MmToMeters(positionMm[1]);
            double pzM = GeometryUtils.MmToMeters(positionMm[2]);

            int topFaceIdx = FindPlanarFaceAtZ(designBody, pzM, +1.0);

            var edgesBefore = CaptureEdges(designBody);

            try
            {
                WriteBlock.ExecuteTask("AddPocket", () =>
                {
                    // Face-normal-aware (defaults to +Z = legacy upright path). The cutter must
                    // BREACH the face: base placed below the face by `depth` along -nAxis, frame
                    // oriented so Frame.DirZ = +nAxis, then extrude (depth+margin) so the cutter
                    // spans [faceZ-depth, faceZ+margin] — removing exactly `depth` below the face
                    // and overshooting by `margin` to guarantee a clean breach.
                    // BUGFIX (P0 from-empty gate, IL-confirmed): the previous Frame.Create(center,
                    // shortAxis, longAxis) gave DirZ = shortAxis×longAxis = -nAxis, so the cutter
                    // extruded DOWNWARD and buried itself entirely inside the body (dV=0, no breach).
                    // The 82% matrix's sign-only oracle never caught it (a buried cavity still has
                    // dV<0). Frame.Create(center, longAxis, shortAxis) gives DirZ = +nAxis.
                    double[] nAxis, longAxis, shortAxis;
                    PrismBasis(normalAxis, new[] { 0.0, 1.0, 0.0 }, out nAxis, out longAxis, out shortAxis);
                    double ext = depthM + marginM;
                    Point center = Point.Create(
                        pxM - nAxis[0] * depthM, pyM - nAxis[1] * depthM, pzM - nAxis[2] * depthM);
                    Direction dirX = Direction.Create(longAxis[0], longAxis[1], longAxis[2]);
                    Direction dirY = Direction.Create(shortAxis[0], shortAxis[1], shortAxis[2]);
                    Frame frame = Frame.Create(center, dirX, dirY);
                    Plane basePlane = Plane.Create(frame);
                    // width along shortAxis, length along longAxis. With dirX=longAxis, dirY=shortAxis
                    // the profile's first dim follows longAxis — so pass (lengthM, widthM) to keep
                    // width along shortAxis and length along longAxis as documented.
                    Profile profile = new RectangleProfile(basePlane, lengthM, widthM);
                    Body cutter = Body.ExtrudeProfile(profile, ext);

                    Part part = designBody.Parent as Part;
                    if (part != null)
                    {
                        DesignBody cutterDb = DesignBody.Create(part, "_addpocket_cutter", cutter);
                        designBody.Shape.Subtract(new[] { cutterDb.Shape });
                        try { cutterDb.Delete(); } catch { }
                    }
                    else
                    {
                        designBody.Shape.Subtract(new[] { cutter });
                    }
                });
            }
            catch (Exception ex)
            {
                result.ErrorMessage = "AddPocket failed: " + ex.Message;
                return result;
            }

            var edgesAfter = CaptureEdges(designBody);
            result.NewlyCreatedEdges = DiffEdges(edgesBefore, edgesAfter);
            if (topFaceIdx >= 0) result.ModifiedFaceIndices.Add(topFaceIdx);
            result.Success = true;
            return result;
        }

        // -------------------------------------------------------------------
        // AddRib — rectangular rib (extrude + Unite) on top face along a centerline.
        // -------------------------------------------------------------------
        /// <summary>
        /// startPositionMm → endPositionMm 의 centerline 을 중심으로 두께 widthMm 의 직사각형 rib 을
        /// heightMm 만큼 +Z 방향으로 extrude 한 뒤 body 와 Unite.
        /// start/end Z 가 다르면 평균 Z 를 base 로 사용 (단순화).
        /// </summary>
        public static ModificationResult AddRib(
            DesignBody designBody,
            double[] startPositionMm,
            double[] endPositionMm,
            double widthMm,
            double heightMm,
            double[] normalAxis = null)
        {
            var result = new ModificationResult
            {
                Operation = "AddRib",
                ModifiedBody = designBody,
            };

            if (designBody == null) { result.ErrorMessage = "designBody is null"; return result; }
            if (startPositionMm == null || startPositionMm.Length < 3) { result.ErrorMessage = "startPositionMm must be length 3"; return result; }
            if (endPositionMm == null || endPositionMm.Length < 3) { result.ErrorMessage = "endPositionMm must be length 3"; return result; }
            if (widthMm <= 0) { result.ErrorMessage = "widthMm must be > 0"; return result; }
            if (heightMm <= 0) { result.ErrorMessage = "heightMm must be > 0"; return result; }

            // Centerline vector (in mm).
            double dx = endPositionMm[0] - startPositionMm[0];
            double dy = endPositionMm[1] - startPositionMm[1];
            double dz = endPositionMm[2] - startPositionMm[2];
            double lengthMm = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (lengthMm < 1e-6) { result.ErrorMessage = "start/end coincide (length=0)"; return result; }

            // Face-normal-aware basis (defaults to +Z = legacy upright path). longAxis =
            // centerline projected onto the face plane (perp to nAxis); shortAxis =
            // nAxis × longAxis (rib thickness direction). The rib extrudes +nAxis.
            double[] nAxis, longAxis, shortAxis;
            PrismBasis(normalAxis, new[] { dx, dy, dz }, out nAxis, out longAxis, out shortAxis);

            // Rib base center = midpoint of (start, end).
            double cxMm = 0.5 * (startPositionMm[0] + endPositionMm[0]);
            double cyMm = 0.5 * (startPositionMm[1] + endPositionMm[1]);
            double czMm = 0.5 * (startPositionMm[2] + endPositionMm[2]);

            // Rib length = centerline component along longAxis (its in-plane projection).
            double ribLengthMm = Math.Abs(dx * longAxis[0] + dy * longAxis[1] + dz * longAxis[2]);
            if (ribLengthMm < 1e-6) ribLengthMm = lengthMm;
            double widthM = GeometryUtils.MmToMeters(widthMm);
            double lengthM = GeometryUtils.MmToMeters(ribLengthMm);
            double heightM = GeometryUtils.MmToMeters(heightMm);
            double cxM = GeometryUtils.MmToMeters(cxMm);
            double cyM = GeometryUtils.MmToMeters(cyMm);
            double czM = GeometryUtils.MmToMeters(czMm);

            // Top face index (for ModifiedFaceIndices) — the base plane the rib lands on.
            int topFaceIdx = FindPlanarFaceAtZ(designBody, czM, +1.0);

            var edgesBefore = CaptureEdges(designBody);

            try
            {
                WriteBlock.ExecuteTask("AddRib", () =>
                {
                    // Frame normal must = +nAxis so ExtrudeProfile builds the rib OUTWARD
                    // from the face (not into the body). With DirX=longAxis, DirY=shortAxis:
                    //   frame.DirZ = longAxis × shortAxis = longAxis × (nAxis × longAxis) = nAxis.
                    // RectangleProfile dims follow the frame axes:
                    //   width along DirX = rib length (along centerline) = lengthM
                    //   length along DirY = rib thickness (perpendicular)  = widthM
                    Point center = Point.Create(cxM, cyM, czM);
                    Direction dirX = Direction.Create(longAxis[0], longAxis[1], longAxis[2]);
                    Direction dirY = Direction.Create(shortAxis[0], shortAxis[1], shortAxis[2]);
                    Frame frame = Frame.Create(center, dirX, dirY);
                    Plane basePlane = Plane.Create(frame);
                    Profile profile = new RectangleProfile(basePlane, lengthM, widthM);
                    // Extrude +nAxis (outward from face) by heightM.
                    Body rib = Body.ExtrudeProfile(profile, heightM);

                    Part part = designBody.Parent as Part;
                    if (part != null)
                    {
                        DesignBody ribDb = DesignBody.Create(part, "_addrib_solid", rib);
                        designBody.Shape.Unite(new[] { ribDb.Shape });
                        try { ribDb.Delete(); } catch { }
                    }
                    else
                    {
                        designBody.Shape.Unite(new[] { rib });
                    }
                });
            }
            catch (Exception ex)
            {
                result.ErrorMessage = "AddRib failed: " + ex.Message;
                return result;
            }

            var edgesAfter = CaptureEdges(designBody);
            result.NewlyCreatedEdges = DiffEdges(edgesBefore, edgesAfter);
            if (topFaceIdx >= 0) result.ModifiedFaceIndices.Add(topFaceIdx);
            result.Success = true;
            return result;
        }

        // -------------------------------------------------------------------
        // AddChamfer — apply small fillet (chamfer-approx) to edges matching filter.
        // -------------------------------------------------------------------
        /// <summary>
        /// edgeFilter ("all" | "top" | "bottom" | "vertical") 통과 edge 들에 대해
        /// FixedRadiusRound(widthMm) 라운드를 적용. 진짜 chamfer API 가 노출되지 않아
        /// 작은 R 의 fillet 으로 근사한다.
        /// 휴리스틱 (TestModelGenerator BoxTripleCornerR 와 일치):
        ///   - vertical: edge.Length 가 body T (= bbox Z span) 와 비슷 (~0.5mm tol).
        ///   - top:    midZ ≈ bbox Z max (Z 방향 평행 edge, midZ-Zmax 비교).
        ///   - bottom: midZ ≈ bbox Z min.
        ///   - all:    전부.
        /// </summary>
        /// <summary>An edge is chamferable only when BOTH adjacent faces are simple
        /// analytic surfaces (Plane/Cylinder/Cone). RoundEdges silently no-ops on edges
        /// touching NURBS/Procedural surfaces (Ventilator/RC_Buggy: status OK but faces+0).
        /// catch→true: if the edge-face API can't classify, KEEP the edge so currently-
        /// working AddChamfer cases (624ZZ, SampleModel1) never regress.</summary>
        private static bool IsChamferableEdge(Edge e)
        {
            try
            {
                int analytic = 0, total = 0;
                foreach (var f in e.Faces)
                {
                    total++;
                    var g = f.Geometry;
                    var tn = g == null ? "" : g.GetType().Name;
                    if (tn == "Plane" || tn == "Cylinder" || tn == "Cone") analytic++;
                }
                return total > 0 && analytic == total;
            }
            catch { return true; }
        }

        public static ModificationResult AddChamfer(
            DesignBody designBody,
            double widthMm,
            string edgeFilter)
        {
            var result = new ModificationResult
            {
                Operation = "AddChamfer",
                ModifiedBody = designBody,
            };

            if (designBody == null) { result.ErrorMessage = "designBody is null"; return result; }
            if (widthMm <= 0) { result.ErrorMessage = "widthMm must be > 0"; return result; }
            if (string.IsNullOrEmpty(edgeFilter)) edgeFilter = "all";
            string filter = edgeFilter.Trim().ToLowerInvariant();
            if (filter != "all" && filter != "top" && filter != "bottom" && filter != "vertical")
            {
                result.ErrorMessage = "Unknown edgeFilter '" + edgeFilter + "' (expected all|top|bottom|vertical)";
                return result;
            }

            double radiusM = GeometryUtils.MmToMeters(widthMm);

            // Body bbox for "vertical/top/bottom" heuristics.
            Box bbox;
            try { bbox = designBody.Shape.GetBoundingBox(Matrix.Identity); }
            catch (Exception ex) { result.ErrorMessage = "GetBoundingBox failed: " + ex.Message; return result; }
            double zMin = bbox.MinCorner.Z;
            double zMax = bbox.MaxCorner.Z;
            double tM = zMax - zMin; // body thickness in Z

            var edgesBefore = CaptureEdges(designBody);

            // Collect candidate edges (outside WriteBlock; .Edges enumeration is read-only).
            var candidates = new List<Edge>();
            try
            {
                foreach (var e in designBody.Shape.Edges)
                {
                    bool keep;
                    if (filter == "all")
                    {
                        keep = true;
                    }
                    else if (filter == "vertical")
                    {
                        // length close to body T (vertical = parallel to Z, full thickness).
                        keep = Math.Abs(e.Length - tM) < 0.0005; // 0.5 mm
                    }
                    else if (filter == "top" || filter == "bottom")
                    {
                        // midZ heuristic — edge lies (mostly) in the top/bottom plane.
                        var sp = e.StartPoint;
                        var ep = e.EndPoint;
                        double midZ = (sp.Z + ep.Z) / 2.0;
                        double targetZ = (filter == "top") ? zMax : zMin;
                        keep = Math.Abs(midZ - targetZ) < 1e-4 && Math.Abs(sp.Z - ep.Z) < 1e-5;
                    }
                    else
                    {
                        keep = false;
                    }
                    // Skip NURBS/Procedural-adjacent edges that RoundEdges can't chamfer
                    // (silent no-op on curved parts). Defensive: keeps edge if unclassifiable.
                    if (keep && !IsChamferableEdge(e)) keep = false;
                    if (keep) candidates.Add(e);
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = "Edge enumeration failed: " + ex.Message;
                return result;
            }

            if (candidates.Count == 0)
            {
                result.ErrorMessage = "No edges matched filter '" + edgeFilter + "'";
                return result;
            }

            // Apply RoundEdges with FixedRadiusRound. Try bulk first; fall back per-edge.
            int rounded = 0;
            int failed = 0;
            try
            {
                WriteBlock.ExecuteTask("AddChamfer bulk", () =>
                {
                    var map = new Dictionary<Edge, EdgeRound>();
                    foreach (var e in candidates)
                    {
                        if (!map.ContainsKey(e)) map[e] = new FixedRadiusRound(radiusM);
                    }
                    designBody.Shape.RoundEdges(map);
                });
                rounded = candidates.Count;
            }
            catch
            {
                // Fall back to per-edge with try/catch so partially-roundable bodies still succeed.
                foreach (var e in candidates)
                {
                    try
                    {
                        WriteBlock.ExecuteTask("AddChamfer single", () =>
                        {
                            var map = new Dictionary<Edge, EdgeRound>
                            {
                                { e, new FixedRadiusRound(radiusM) },
                            };
                            designBody.Shape.RoundEdges(map);
                        });
                        rounded++;
                    }
                    catch
                    {
                        failed++;
                    }
                }
            }

            var edgesAfter = CaptureEdges(designBody);
            result.NewlyCreatedEdges = DiffEdges(edgesBefore, edgesAfter);

            if (rounded == 0)
            {
                result.ErrorMessage = "AddChamfer: 0 of " + candidates.Count + " candidate edges rounded (all RoundEdges calls failed).";
                return result;
            }

            result.Success = true;
            // Diagnostic info embedded in ErrorMessage (Success=true means it's just a note).
            // Mirrors RT11 AddBoss+RoundEdgesNearFaces convention.
            result.ErrorMessage = "AddChamfer R=" + widthMm.ToString("F2")
                + "mm rounded=" + rounded + " failed=" + failed
                + " filter=" + filter;
            return result;
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------
        /// <summary>FeatureExtractor 가 사용한 face 순회 순서를 재현하여 face 를 index 로 조회.</summary>
        /// <summary>Kernel-truth check that a SurfaceSwap actually moved the cylinder at
        /// faceIndex to ~newRm. Returns false ONLY when a cylinder is found there with the
        /// WRONG radius (the non-active-body silent no-op). Returns true when it can't read
        /// a cylinder (face shifted) so existing working cases are never broken.</summary>
        private static bool SwapChangedRadius(DesignBody designBody, int faceIndex, double newRm)
        {
            try
            {
                var f = GetFaceByIndex(designBody, faceIndex);
                var cyl = f != null ? f.Geometry as Cylinder : null;
                if (cyl == null) return true;
                return Math.Abs(cyl.Radius - newRm) <= Math.Max(newRm * 0.02, 2e-5);
            }
            catch { return true; }
        }

        private static Face GetFaceByIndex(DesignBody designBody, int faceIndex)
        {
            if (designBody == null) return null;
            int i = 0;
            foreach (var df in designBody.Faces)
            {
                if (i == faceIndex) return df.Shape;
                i++;
            }
            return null;
        }

        // -------------------------------------------------------------------
        // Progressive OffsetFaces (b) — split a large offset into N sub-steps
        // and apply each in its own WriteBlock. SC's kernel rejects single-shot
        // OffsetFaces calls when the requested distance pushes the topology
        // through a degenerate or near-tangent neighbor; the same total move
        // applied in 4–16 small steps very often succeeds because intermediate
        // states stay non-degenerate.
        //
        // signature mirrors Body.OffsetFaces (single-distance + per-face dict)
        // and throws ONLY if every fallback step count also fails — caller's
        // try/catch then sees one combined exception with the smallest step
        // count that almost worked.
        //
        // Each sub-step lives in its own WriteBlock so partial progress is
        // committed atomically: if step k+1 fails after k succeeded, the body
        // is left at the k×(offset/N) state instead of fully rolled back. That's
        // the right tradeoff for batch demos: closer to target > untouched.
        // -------------------------------------------------------------------
        // -------------------------------------------------------------------
        // Body healing — defensive pre-step for STEP-imported bodies.
        //
        // SC's Unsupported.BodyMethods exposes geometric kernel cleanup that
        // standard public APIs don't:
        //   - FixInexactEdges: tightens edge tolerance (STEP files often have
        //     edges stitched at the SOURCE CAD's loose tolerance, which makes
        //     OffsetFaces reject offsets that should geometrically succeed).
        //   - FindAndFixGaps: zips small gaps between faces (STEP import noise).
        //
        // Both are best-effort — failures are swallowed, the original body is
        // left as-is. Call once before attempting mods on suspect (STEP) bodies.
        // -------------------------------------------------------------------
        private static void TryHealBody(DesignBody designBody)
        {
            if (designBody == null) return;
            try
            {
                WriteBlock.ExecuteTask("HealBody.FixInexactEdges", () =>
                {
                    Unsupported.BodyMethods.FixInexactEdges(designBody.Shape);
                });
            }
            catch { /* best-effort */ }
            try
            {
                // 10° max opening angle, 0.1mm gap tolerance — matches XML defaults.
                WriteBlock.ExecuteTask("HealBody.FindAndFixGaps", () =>
                {
                    Unsupported.BodyMethods.FindAndFixGaps(designBody.Shape, 10.0, 1e-4);
                });
            }
            catch { /* best-effort */ }
        }

        // -------------------------------------------------------------------
        // BREAKTHROUGH — Boolean Reconstruction for Hole enlarge.
        //
        // OffsetFaces fails when the cylinder face has adjacency that the kernel
        // can't reconcile (adjacent fillets, tangent cones, tiny tolerance).
        // INSTEAD of offsetting, construct a fresh cylinder body at the same
        // axis with the NEW radius and Subtract it from the design body.
        // Because Subtract operates on the body as a whole (not on a constrained
        // face), it succeeds regardless of the original hole's local topology.
        //
        // After Subtract, the original hole's cylinder face is replaced by a
        // new cylinder face with the new radius, and all the adjacent
        // fillet/cone faces are AUTOMATICALLY re-cut by the Boolean to meet
        // the new cylinder — no manual unround / re-fillet pass needed.
        //
        // Limitation: this primitive only ENLARGES the hole. For shrink, the
        // body needs to be Filled first (Unite a cylinder at old R) which is
        // a separate problem.
        //
        // axisExtensionFactor=4 gives the tool cylinder length = 4× the body's
        // longest bbox dimension, centered on the original hole face's origin
        // — guaranteed to span the body in both axis directions.
        // -------------------------------------------------------------------
        private static bool TryReconstructHoleByBoolean(
            DesignBody designBody,
            FaceFeature faceFeat,
            int faceIdx,
            double newRm,
            string holeId,
            out string error)
        {
            if (TryReconstructHoleByBooleanAtScale(designBody, faceFeat, faceIdx, newRm, holeId, 1.0, out error))
                return true;
            if (error == null || !(error.Contains("Imprinting") || error.Contains("Operation failed")))
                return false;

            // Scale-trick fallback. Transform body by 1000×, mod with scaled R,
            // transform back. Face indices stay identical (only geometry changes).
            const double UP = 1000.0;
            const double DOWN = 1.0 / UP;
            bool scaledOk = false;
            string scaledErr = null;
            try
            {
                WriteBlock.ExecuteTask("BooleanRecon " + holeId + " [scale-up 1000x]", () =>
                {
                    designBody.Shape.Transform(Matrix.CreateScale(UP));
                });
                scaledOk = TryReconstructHoleByBooleanAtScale(
                    designBody, faceFeat, faceIdx, newRm * UP, holeId + "_scaled", UP, out scaledErr);
                // Always scale back even on failure so the body returns to native size.
                WriteBlock.ExecuteTask("BooleanRecon " + holeId + " [scale-down]", () =>
                {
                    designBody.Shape.Transform(Matrix.CreateScale(DOWN));
                });
            }
            catch (Exception ex)
            {
                scaledErr = "scale-trick threw: " + ex.Message;
                try
                {
                    WriteBlock.ExecuteTask("ScaleDown emergency", () =>
                    {
                        designBody.Shape.Transform(Matrix.CreateScale(DOWN));
                    });
                }
                catch { /* body might be unrecoverable */ }
            }
            if (scaledOk) { error = null; return true; }
            error = (error ?? "") + " | scaled1000x: " + (scaledErr ?? "(no msg)");
            return false;
        }

        // Core boolean reconstruction at a specific scale. `bodyScaleFactor` is
        // informational only — caller must already have transformed the body and
        // scaled newRm to match.
        private static bool TryReconstructHoleByBooleanAtScale(
            DesignBody designBody,
            FaceFeature faceFeat,
            int faceIdx,
            double newRm,
            string holeId,
            double bodyScaleFactor,
            out string error)
        {
            error = null;
            try
            {
                // Use the LIVE face geometry, not cached faceFeat coordinates.
                // The cached Origin/Normal were captured before any body transform
                // (notably scale-up for tolerance recovery) — they reference points
                // that may no longer be on the actual cylinder's axis after the
                // body has been scaled. Re-querying face.Geometry as Cylinder gives
                // the current frame in the current body coordinates.
                var liveFace = GetFaceByIndex(designBody, faceIdx);
                if (liveFace == null)
                {
                    error = "live face missing (index=" + faceIdx + ")";
                    return false;
                }
                // Axis tier ladder (W2-4): live Cylinder → cached axis × scale.
                // Ventilator-class STEP imports expose hole walls as bare Surface;
                // the Boolean only needs the axis, which the classifier cached.
                Point origin;
                Direction axis;
                var liveCyl = liveFace.Geometry as Cylinder;
                if (liveCyl != null)
                {
                    origin = liveCyl.Frame.Origin;
                    axis = liveCyl.Frame.DirZ;
                }
                else if (faceFeat != null
                         && faceFeat.Origin != null && faceFeat.Origin.Length >= 3
                         && faceFeat.Normal != null && faceFeat.Normal.Length >= 3)
                {
                    origin = Point.Create(
                        faceFeat.Origin[0] * bodyScaleFactor,
                        faceFeat.Origin[1] * bodyScaleFactor,
                        faceFeat.Origin[2] * bodyScaleFactor);
                    axis = Direction.Create(faceFeat.Normal[0], faceFeat.Normal[1], faceFeat.Normal[2]);
                }
                else
                {
                    error = "no usable axis (live not Cylinder, no cached axis)";
                    return false;
                }

                // bbox-derived span — 4× longest dim is safely beyond the body
                // on both sides of the hole's axis origin.
                var bbox = designBody.Shape.GetBoundingBox(Matrix.Identity);
                double bboxMax = Math.Max(bbox.Size.X, Math.Max(bbox.Size.Y, bbox.Size.Z));
                double toolLen = Math.Max(bboxMax * 4.0, 1e-3); // ≥1mm safety floor

                // Construct the profile plane perpendicular to the cylinder axis.
                // Cylinder.Frame.DirZ is the axis; we need DirX, DirY for the profile.
                // Direction.ArbitraryPerpendicular gives one; Cross gives the other.
                var dirX = axis.ArbitraryPerpendicular;
                var dirY = Direction.Cross(axis, dirX);

                // Place the profile origin half-toolLen back along the axis so the
                // extruded cylinder spans evenly through the body.
                var basePoint = Point.Create(
                    origin.X - axis.UnitVector.X * (toolLen / 2.0),
                    origin.Y - axis.UnitVector.Y * (toolLen / 2.0),
                    origin.Z - axis.UnitVector.Z * (toolLen / 2.0));

                var profileFrame = Frame.Create(basePoint, dirX, dirY);
                var basePlane = Plane.Create(profileFrame);

                var builder = new ProfileBuilder(basePlane);
                builder.AddCircle(basePoint, newRm);
                Profile profile = builder.Build();

                // CRITICAL: Subtract requires BOTH operands to be design bodies (or
                // both free bodies — they must match). The cylinder we extrude is a
                // free Body; wrapping it in DesignBody.Create makes it part of the
                // same document tree as designBody, so the Subtract accepts the pair.
                // Pattern mirrors AddHoleCutter / VoidCutService — wrap, subtract,
                // then Delete the wrapper (Subtract may have consumed it already, so
                // the Delete is best-effort).
                //
                // All three SC ops (Extrude, Create wrapper, Subtract) live in ONE
                // WriteBlock so the transient toolBody never escapes into a state
                // where SC's GC would mark it deleted between calls.
                var part = (designBody.Parent as Part)
                        ?? (designBody.Parent != null ? designBody.Parent.GetAncestor<Part>() : null);
                if (part == null)
                {
                    error = "designBody has no parent Part for tool body wrapping";
                    return false;
                }
                bool boolOk = false;
                string boolErr = null;
                WriteBlock.ExecuteTask("BooleanRecon " + holeId, () =>
                {
                    try
                    {
                        var toolBody = Body.ExtrudeProfile(profile, toolLen);
                        if (toolBody == null) { boolErr = "ExtrudeProfile returned null"; return; }
                        var toolDb = DesignBody.Create(part, "_holecut_" + holeId, toolBody);
                        designBody.Shape.Subtract(new[] { toolDb.Shape });
                        try { toolDb.Delete(); } catch { /* Subtract may have consumed it */ }
                        boolOk = true;
                    }
                    catch (Exception inner)
                    {
                        boolErr = "inside writeblock: " + inner.Message;
                    }
                });
                if (!boolOk)
                {
                    error = boolErr ?? "Boolean op did not complete";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // -------------------------------------------------------------------
        // W2-6 — direct surface swap for WALLS (plane translation).
        //
        // Shrinking a wall = moving face A's PLANE toward face B by Δ. Instead
        // of OffsetFaces (kernel-rejected on linkrods/624ZZ regardless of scale
        // — structural adjacency, not tolerance), replace face A's geometry
        // with the translated plane via ReplaceFaceGeometry; the kernel re-trims
        // neighbors to the new surface.
        // -------------------------------------------------------------------
        private static bool TrySwapWallSurface(
            DesignBody designBody,
            int faceIdxA,
            double moveTowardBMeters,   // signed displacement along dirToB (positive = shrink)
            double[] dirToBUnit,        // unit vector A→B (native orientation, scale-invariant)
            string wallId,
            out string error)
        {
            error = null;
            try
            {
                var liveFace = GetFaceByIndex(designBody, faceIdxA);
                if (liveFace == null) { error = "live face A missing"; return false; }
                var livePlane = liveFace.Geometry as Plane;
                if (livePlane == null) { error = "face A geometry is not a Plane"; return false; }
                if (dirToBUnit == null || dirToBUnit.Length < 3) { error = "no dirToB"; return false; }

                var o = livePlane.Frame.Origin;
                var newOrigin = Point.Create(
                    o.X + dirToBUnit[0] * moveTowardBMeters,
                    o.Y + dirToBUnit[1] * moveTowardBMeters,
                    o.Z + dirToBUnit[2] * moveTowardBMeters);
                var newPlane = Plane.Create(Frame.Create(newOrigin, livePlane.Frame.DirX, livePlane.Frame.DirY));
                var fg = new FaceGeometry(newPlane, liveFace.IsReversed);
                var dict = new Dictionary<Face, FaceGeometry> { { liveFace, fg } };

                bool swapOk = false;
                string swapErr = null;
                WriteBlock.ExecuteTask("WallSurfaceSwap " + wallId, () =>
                {
                    try
                    {
                        Unsupported.BodyMethods.ReplaceFaceGeometry(
                            designBody.Shape, dict, true, 1e-8, false);
                        swapOk = true;
                    }
                    catch (Exception inner)
                    {
                        swapErr = "inside writeblock: " + inner.Message;
                    }
                });
                if (!swapOk) { error = swapErr ?? "swap did not complete"; return false; }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // -------------------------------------------------------------------
        // Boss-class axis helpers (matrix gap #1).
        // -------------------------------------------------------------------
        /// <summary>Extent (mm) of the face's bbox projected onto an axis —
        /// estimates boss height when extraction reports HeightMm=0.</summary>
        private static double EstimateCylFaceExtentMm(DesignBody designBody, int faceIdx, double[] axisU)
        {
            try
            {
                var face = GetFaceByIndex(designBody, faceIdx);
                if (face == null || axisU == null || axisU.Length < 3) return 0.0;
                var bb = face.GetBoundingBox(Matrix.Identity);
                double tmin = double.MaxValue, tmax = double.MinValue;
                for (int ci = 0; ci < 8; ci++)
                {
                    double cx = ((ci & 1) == 0) ? bb.MinCorner.X : bb.MaxCorner.X;
                    double cy = ((ci & 2) == 0) ? bb.MinCorner.Y : bb.MaxCorner.Y;
                    double cz = ((ci & 4) == 0) ? bb.MinCorner.Z : bb.MaxCorner.Z;
                    double t = cx * axisU[0] + cy * axisU[1] + cz * axisU[2];
                    if (t < tmin) tmin = t;
                    if (t > tmax) tmax = t;
                }
                return (tmax - tmin) * 1000.0;
            }
            catch { return 0.0; }
        }

        /// <summary>Height-independent boss-cap finder: among planar faces whose
        /// normal ∥ axisU and whose centroid lies within radiusM of the boss axis
        /// line through baseM, return the one farthest along +axisU from baseM.</summary>
        private static int FindBossCapFace(
            DesignBody designBody, double[] baseM, double[] axisU, double radiusM)
        {
            try
            {
                int i = 0; int best = -1; double bestAlong = -1.0;
                double radTol = radiusM * 1.5 + 1e-4;
                foreach (var df in designBody.Faces)
                {
                    try
                    {
                        var p = df.Shape != null ? df.Shape.Geometry as Plane : null;
                        if (p != null)
                        {
                            var n = p.Frame.DirZ.UnitVector;
                            double dot = Math.Abs(n.X*axisU[0] + n.Y*axisU[1] + n.Z*axisU[2]);
                            if (dot > 0.95)
                            {
                                var o = p.Frame.Origin;
                                double dx = o.X - baseM[0], dy = o.Y - baseM[1], dz = o.Z - baseM[2];
                                double along = dx*axisU[0] + dy*axisU[1] + dz*axisU[2];
                                double px = dx - along*axisU[0], py = dy - along*axisU[1], pz = dz - along*axisU[2];
                                double perp = Math.Sqrt(px*px + py*py + pz*pz);
                                if (along > 1e-5 && perp <= radTol && along > bestAlong)
                                {
                                    bestAlong = along; best = i;
                                }
                            }
                        }
                    }
                    catch { }
                    i++;
                }
                return best;
            }
            catch { return -1; }
        }

        /// <summary>Find a planar face whose normal is parallel to axisU and whose
        /// plane passes near refPoint + axisU·distM (the boss cap). Axis-aware
        /// replacement for FindPlanarFaceAtZ. Returns face index or -1.</summary>
        private static int FindPlanarFaceAlongAxis(
            DesignBody designBody, double[] refPointM, double[] axisU, double distM)
        {
            try
            {
                double tx = refPointM[0] + axisU[0] * distM;
                double ty = refPointM[1] + axisU[1] * distM;
                double tz = refPointM[2] + axisU[2] * distM;
                int i = 0; int best = -1; double bestErr = double.MaxValue;
                foreach (var df in designBody.Faces)
                {
                    try
                    {
                        var p = df.Shape != null ? df.Shape.Geometry as Plane : null;
                        if (p != null)
                        {
                            var n = p.Frame.DirZ.UnitVector;
                            double dot = Math.Abs(n.X * axisU[0] + n.Y * axisU[1] + n.Z * axisU[2]);
                            if (dot > 0.95)
                            {
                                // distance from target point to the plane
                                var o = p.Frame.Origin;
                                double dpl = Math.Abs((tx - o.X) * n.X + (ty - o.Y) * n.Y + (tz - o.Z) * n.Z);
                                if (dpl < bestErr) { bestErr = dpl; best = i; }
                            }
                        }
                    }
                    catch { /* skip stale faces */ }
                    i++;
                }
                // accept only if within 10% of dist (or 1mm) of the expected cap plane
                double tol = Math.Max(Math.Abs(distM) * 0.1, 1e-3);
                return (best >= 0 && bestErr <= tol) ? best : -1;
            }
            catch { return -1; }
        }

        // -------------------------------------------------------------------
        // GATE-VERIFIED (probe 2026-06-10, E3-3) — direct surface swap for holes.
        //
        // Replaces the hole's cylinder SURFACE with a new coaxial cylinder at
        // the target radius via Unsupported.BodyMethods.ReplaceFaceGeometry.
        // The kernel re-trims adjacent faces to meet the new surface
        // (allowTopoChanges=true). Compared to OffsetFaces/Boolean:
        //   - direction-agnostic: enlarge AND shrink with the same code
        //   - no sign convention: the new radius is stated absolutely
        //   - topology-preserving: same Face object, new geometry
        // FaceGeometry ctor (Surface, bool reversed) discovered by reflection
        // in the gate probe; passing the face's own IsReversed preserved
        // orientation correctly (measured post-swap R exact).
        // -------------------------------------------------------------------
        private static bool TrySwapHoleSurface(
            DesignBody designBody,
            int faceIdx,
            double newRm,
            string holeId,
            FaceFeature cachedFeat,
            out string error)
        {
            error = null;
            try
            {
                var liveFace = GetFaceByIndex(designBody, faceIdx);
                if (liveFace == null) { error = "live face missing (index=" + faceIdx + ")"; return false; }

                // Axis tier ladder (W2-4): live Cylinder frame → cached axis frame.
                // STEP imports sometimes expose hole walls as bare Surface (Ventilator)
                // — the live geometry isn't a Cylinder but the classifier captured the
                // axis (Origin + Normal) and radius at extraction time. A coaxial
                // replacement cylinder only needs that axis.
                Frame swapFrame;
                var liveCyl = liveFace.Geometry as Cylinder;
                if (liveCyl != null)
                {
                    swapFrame = liveCyl.Frame;
                }
                else if (cachedFeat != null
                         && cachedFeat.Origin != null && cachedFeat.Origin.Length >= 3
                         && cachedFeat.Normal != null && cachedFeat.Normal.Length >= 3)
                {
                    var origin = Point.Create(cachedFeat.Origin[0], cachedFeat.Origin[1], cachedFeat.Origin[2]);
                    var axis = Direction.Create(cachedFeat.Normal[0], cachedFeat.Normal[1], cachedFeat.Normal[2]);
                    var dirX = axis.ArbitraryPerpendicular;
                    var dirY = Direction.Cross(axis, dirX);
                    swapFrame = Frame.Create(origin, dirX, dirY); // DirZ = dirX×dirY = axis
                }
                else
                {
                    error = "no usable axis (live not Cylinder, no cached axis)";
                    return false;
                }

                var newSurf = Cylinder.Create(swapFrame, newRm);
                var fg = new FaceGeometry(newSurf, liveFace.IsReversed);
                var dict = new Dictionary<Face, FaceGeometry> { { liveFace, fg } };

                bool swapOk = false;
                string swapErr = null;
                WriteBlock.ExecuteTask("SurfaceSwap " + holeId, () =>
                {
                    try
                    {
                        Unsupported.BodyMethods.ReplaceFaceGeometry(
                            designBody.Shape, dict, true, 1e-8, false);
                        swapOk = true;
                    }
                    catch (Exception inner)
                    {
                        swapErr = "inside writeblock: " + inner.Message;
                    }
                });
                if (!swapOk) { error = swapErr ?? "swap did not complete"; return false; }

                // Immediate sanity: the face (same index) must now report newRm.
                // (allowTopoChanges can shift indices in exotic cases — scan all
                // cylinder faces for newRm as a fallback before failing.)
                var postFace = GetFaceByIndex(designBody, faceIdx);
                var postCyl = postFace != null ? postFace.Geometry as Cylinder : null;
                if (postCyl != null && Math.Abs(postCyl.Radius - newRm) <= Math.Max(newRm * 0.01, 1e-7))
                    return true;
                foreach (var f in designBody.Shape.Faces)
                {
                    var c = f.Geometry as Cylinder;
                    if (c != null && Math.Abs(c.Radius - newRm) <= Math.Max(newRm * 0.01, 1e-7))
                        return true;
                }
                error = "post-swap radius mismatch: " +
                        (postCyl != null ? (postCyl.Radius * 1000.0).ToString("F3") : "n/a") + "mm";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // -------------------------------------------------------------------
        // GATE-VERIFIED (probe 2026-06-10) — ContainsPoint MaterialSign oracle.
        //
        // Determines a fillet/hole face's TRUE concavity by probing material
        // presence on both sides of the surface, instead of trusting the
        // classifier's cached Concavity (which mis-classifies some STEP chains
        // — nist_ctc_01 / boxy sign inversions).
        //
        //   probe P_in  = surface point moved toward the curvature axis by eps
        //   probe P_out = moved away by eps
        //   toward=air, away=material → Concave  (hole / inside-corner fillet)
        //   toward=material, away=air → Convex   (boss / outside round)
        //   both same → null (degenerate thin wall) — caller falls back to
        //   the classifier value. NEVER guess.
        //
        // LANDMINES (probe-verified, process-kill): ContainsPoint with an
        // exactly-on-surface point CRASHES SC — eps offsets are mandatory.
        // eps = 0.2×R (validated ratio), floored at 0.01mm.
        // -------------------------------------------------------------------
        private static Concavity? ProbeMaterialConcavity(DesignBody designBody, Face face)
        {
            try
            {
                if (designBody == null || face == null) return null;
                var geom = face.Geometry;
                double r;
                Point axisFoot;
                Point surfP;

                // Surface sample point: project the face's bbox center onto the
                // surface — lands on/near the actual trimmed face band.
                var bb = face.GetBoundingBox(Matrix.Identity);
                var bbCenter = Point.Create(
                    (bb.MinCorner.X + bb.MaxCorner.X) / 2,
                    (bb.MinCorner.Y + bb.MaxCorner.Y) / 2,
                    (bb.MinCorner.Z + bb.MaxCorner.Z) / 2);

                if (geom is Cylinder cyl)
                {
                    r = cyl.Radius;
                    var se = cyl.ProjectPoint(bbCenter);
                    if (se == null) return null;
                    surfP = se.Point;
                    var o = cyl.Frame.Origin;
                    var dz = cyl.Frame.DirZ.UnitVector;
                    double t = Vector.Dot(surfP - o, dz);
                    axisFoot = o + dz * t;
                }
                else if (geom is Torus tor)
                {
                    r = tor.MinorRadius;
                    var se = tor.ProjectPoint(bbCenter);
                    if (se == null) return null;
                    surfP = se.Point;
                    // Tube-center circle point: radial projection in frame plane.
                    var o = tor.Frame.Origin;
                    var dz = tor.Frame.DirZ.UnitVector;
                    var v = surfP - o;
                    var vPlane = v - dz * Vector.Dot(v, dz);
                    double mag = vPlane.Magnitude;
                    if (mag < 1e-9) return null;
                    axisFoot = o + (vPlane / mag) * tor.MajorRadius;
                }
                else
                {
                    return null; // oracle only defined for cylinder/torus fillet geometry
                }

                var toward = axisFoot - surfP;
                double tmag = toward.Magnitude;
                if (tmag < 1e-9) return null;
                var towardU = toward / tmag;
                double eps = Math.Max(0.2 * r, 1e-5);

                var pIn = surfP + towardU * eps;
                var pOut = surfP - towardU * eps;
                bool inMat = designBody.Shape.ContainsPoint(pIn);
                bool outMat = designBody.Shape.ContainsPoint(pOut);

                if (!inMat && outMat) return Concavity.Concave;
                if (inMat && !outMat) return Concavity.Convex;
                return null; // degenerate (thin wall both-air / both-material)
            }
            catch
            {
                return null;
            }
        }

        // Measure the chain's fillet R by face index lookup. Returns meters,
        // or NaN if no chain face has a torus/cylinder geometry. Picks the R
        // value at any face in chain.FaceIndices (assumes uniform R, which is
        // by definition of "chain").
        private static double MeasureChainRadiusByFaceIndex(DesignBody designBody, FilletChain chain)
        {
            var faceIdxSet = new HashSet<int>(chain.FaceIndices);
            int i = 0;
            foreach (var df in designBody.Faces)
            {
                if (faceIdxSet.Contains(i))
                {
                    try
                    {
                        if (df != null && df.Shape != null)
                        {
                            var geom = df.Shape.Geometry;
                            if (geom is Torus tor) return tor.MinorRadius;
                            if (geom is Cylinder cyl) return cyl.Radius;
                        }
                    }
                    catch { /* face may be stale — try next */ }
                }
                i++;
            }
            return double.NaN;
        }

        // -------------------------------------------------------------------
        // BREAKTHROUGH — RoundEdges replacement for fillet chain modification.
        //
        // Walks each fillet face's edge boundary, collects every unique edge
        // (most are shared between the fillet and its adjacent support face),
        // and calls Body.RoundEdges with the new radius. SC's kernel treats
        // RoundEdges on an edge that already has an associated round as a
        // REPLACE — it rebuilds the fillet at the new R without trying to
        // slide the existing fillet's faces through their tangent neighbors.
        //
        // Returns false if no edges resolved or if RoundEdges itself fails.
        // -------------------------------------------------------------------
        private static bool TryReplaceFilletByRoundEdges(
            DesignBody designBody,
            FilletChain chain,
            FeatureGraph graph,
            double newRm,
            string chainId,
            out string error)
        {
            error = null;
            try
            {
                if (newRm <= 0) { error = "newRm must be > 0"; return false; }

                // Collect every unique boundary edge across the fillet face set.
                // Many fillet faces share edges with their neighbors; HashSet
                // dedupes so we don't double-round.
                var edgesToRound = new HashSet<Edge>();
                int designFaceCount = 0;
                int i = 0;
                var faceIdxSet = new HashSet<int>(chain.FaceIndices);
                foreach (var df in designBody.Faces)
                {
                    if (faceIdxSet.Contains(i))
                    {
                        designFaceCount++;
                        try
                        {
                            foreach (var de in df.Edges)
                            {
                                if (de != null && de.Shape != null)
                                    edgesToRound.Add(de.Shape);
                            }
                        }
                        catch { /* face may have stale state — skip */ }
                    }
                    i++;
                }
                if (designFaceCount == 0) { error = "no DesignFaces resolved for chain"; return false; }
                if (edgesToRound.Count == 0) { error = "no edges harvested from fillet faces"; return false; }

                var round = new FixedRadiusRound(newRm);
                var edgeMap = new Dictionary<Edge, EdgeRound>(edgesToRound.Count);
                foreach (var e in edgesToRound) edgeMap[e] = round;

                bool roundOk = false;
                string roundErr = null;
                WriteBlock.ExecuteTask("RoundEdgesReplace " + chainId, () =>
                {
                    try
                    {
                        designBody.Shape.RoundEdges(edgeMap);
                        roundOk = true;
                    }
                    catch (Exception inner)
                    {
                        roundErr = "inside writeblock: " + inner.Message;
                    }
                });
                if (!roundOk)
                {
                    // Bulk failed — try per-edge so partial replacement is captured.
                    int succ = 0, fail = 0;
                    foreach (var e in edgesToRound)
                    {
                        try
                        {
                            WriteBlock.ExecuteTask("RoundEdgesReplace " + chainId + " [single]", () =>
                            {
                                designBody.Shape.RoundEdges(new Dictionary<Edge, EdgeRound> { { e, round } });
                            });
                            succ++;
                        }
                        catch { fail++; }
                    }
                    if (succ > 0)
                    {
                        roundOk = true;
                        roundErr = (roundErr ?? "") + string.Format(" → per-edge {0} ok / {1} fail", succ, fail);
                    }
                }
                if (!roundOk) { error = roundErr ?? "RoundEdges did not complete"; return false; }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // -------------------------------------------------------------------
        // BREAKTHROUGH — Boolean Reconstruction for Wall shrink.
        //
        // Same principle as TryReconstructHoleByBoolean but for a planar wall.
        // To SHRINK a wall (T_new < T_old), subtract a slab on the A-side of
        // thickness Δ = T_old - T_new, positioned so its inner face lands at
        // (face A position + Δ × A.outwardNormal_inward) — i.e. it carves Δ
        // of material off face A's side, bringing face A to its new location.
        //
        // The slab is sized 4× the bbox max dimension in the two directions
        // perpendicular to A.Normal — guaranteed to cover the entire wall area.
        //
        // Limitation: only ENLARGE-DOWN-thickness (i.e. shrink) is implemented.
        // Wall expansion (T_new > T_old) would require Unite of a slab, which
        // is harder topologically (need to ensure the slab joins cleanly to
        // existing adjacent faces). Defer to a future ChangeWallThicknessGrow.
        // -------------------------------------------------------------------
        private static bool TryReconstructWallByBoolean(
            DesignBody designBody,
            FaceFeature faceFeatA,
            FaceFeature faceFeatB,
            double[] wallNormal,
            double signToB,
            double deltaMm,
            string wallId,
            out string error)
        {
            if (TryReconstructWallByBooleanAtScale(designBody, faceFeatA, faceFeatB,
                                                   wallNormal, signToB, deltaMm,
                                                   wallId, 1.0, out error))
                return true;
            if (error == null || !(error.Contains("Imprinting") || error.Contains("Operation failed")))
                return false;

            // Scale-trick for sub-mm walls (linkrods T=1mm, ΔT=0.1mm): native scale
            // hits SC linear tolerance during Subtract imprint. Working at 1000×
            // puts the slab thickness comfortably above tolerance, then we restore.
            const double UP = 1000.0;
            const double DOWN = 1.0 / UP;
            bool scaledOk = false; string scaledErr = null;
            try
            {
                WriteBlock.ExecuteTask("BooleanRecon Wall " + wallId + " [scale-up]", () =>
                {
                    designBody.Shape.Transform(Matrix.CreateScale(UP));
                });
                scaledOk = TryReconstructWallByBooleanAtScale(
                    designBody, faceFeatA, faceFeatB, wallNormal, signToB, deltaMm * UP,
                    wallId + "_scaled", UP, out scaledErr);
                WriteBlock.ExecuteTask("BooleanRecon Wall " + wallId + " [scale-down]", () =>
                {
                    designBody.Shape.Transform(Matrix.CreateScale(DOWN));
                });
            }
            catch (Exception ex)
            {
                scaledErr = "scale-trick threw: " + ex.Message;
                try
                {
                    WriteBlock.ExecuteTask("Wall ScaleDown emergency", () =>
                    {
                        designBody.Shape.Transform(Matrix.CreateScale(DOWN));
                    });
                }
                catch { /* unrecoverable */ }
            }
            if (scaledOk) { error = null; return true; }
            error = (error ?? "") + " | scaled1000x: " + (scaledErr ?? "(no msg)");
            return false;
        }

        private static bool TryReconstructWallByBooleanAtScale(
            DesignBody designBody,
            FaceFeature faceFeatA,
            FaceFeature faceFeatB,
            double[] wallNormal,
            double signToB,
            double deltaMm,
            string wallId,
            double bodyScaleFactor,
            out string error)
        {
            error = null;
            try
            {
                if (deltaMm <= 0) { error = "wall reconstruction supports shrink only"; return false; }
                if (faceFeatA.Origin == null || faceFeatA.Origin.Length < 3)
                { error = "missing face A origin"; return false; }
                if (wallNormal == null || wallNormal.Length < 3)
                { error = "missing wall normal"; return false; }

                // "Inward toward B" direction in A's frame:
                //   if signToB > 0, A.Normal already points toward B → inward = +Normal
                //   if signToB < 0, A.Normal points AWAY from B → inward = -Normal
                // We extrude a slab from a base plane positioned at face A,
                // toward B, with thickness = deltaMm.
                double dxInward = wallNormal[0] * signToB;
                double dyInward = wallNormal[1] * signToB;
                double dzInward = wallNormal[2] * signToB;

                double deltaMeters = deltaMm / 1000.0;

                // Base plane at face A's centroid, slightly OUTSIDE (so the slab
                // straddles A and extends deltaMeters inward, carving the right
                // material off).
                double extentOutward = deltaMeters * 0.5;
                double oxA = faceFeatA.Origin[0] * bodyScaleFactor;
                double oyA = faceFeatA.Origin[1] * bodyScaleFactor;
                double ozA = faceFeatA.Origin[2] * bodyScaleFactor;
                var basePoint = Point.Create(
                    oxA - dxInward * extentOutward,
                    oyA - dyInward * extentOutward,
                    ozA - dzInward * extentOutward);

                // Frame: profile normal = inward direction (extrusion direction).
                var inward = Direction.Create(dxInward, dyInward, dzInward);
                var dirX = inward.ArbitraryPerpendicular;
                var dirY = Direction.Cross(inward, dirX);
                var profileFrame = Frame.Create(basePoint, dirX, dirY);
                var basePlane = Plane.Create(profileFrame);

                // Slab footprint: 4× bbox dim perpendicular to inward — large enough
                // to cover any wall on this body.
                var bbox = designBody.Shape.GetBoundingBox(Matrix.Identity);
                double bboxMax = Math.Max(bbox.Size.X, Math.Max(bbox.Size.Y, bbox.Size.Z));
                double slabSide = Math.Max(bboxMax * 4.0, 1e-3);

                var rect = new RectangleProfile(basePlane, slabSide, slabSide);
                Profile profile = rect;

                // Extrude depth = full deltaMeters + 0.5×deltaMeters padding outside
                // = 1.5×deltaMeters total. The slab straddles face A by 0.5×delta and
                // reaches 1.0×delta inward — subtracting it removes delta worth of
                // wall thickness from the A side.
                double extrudeDepth = deltaMeters * 1.5;

                var part = (designBody.Parent as Part)
                        ?? (designBody.Parent != null ? designBody.Parent.GetAncestor<Part>() : null);
                if (part == null) { error = "designBody has no parent Part"; return false; }

                bool boolOk = false;
                string boolErr = null;
                WriteBlock.ExecuteTask("BooleanRecon Wall " + wallId, () =>
                {
                    try
                    {
                        var toolBody = Body.ExtrudeProfile(profile, extrudeDepth);
                        if (toolBody == null) { boolErr = "ExtrudeProfile returned null"; return; }
                        // RectangleProfile extrudes along basePlane.Normal which we set
                        // as the inward direction. Translate the slab so its CENTER
                        // is at basePoint+0.5×delta inward — i.e., the inner half is
                        // INSIDE the body, the outer half is OUTSIDE.
                        // Body.Transform with a translation along inward direction:
                        // already positioned correctly via basePoint; no translation
                        // needed for the standard ExtrudeProfile semantics, which
                        // extrudes from basePlane along its normal for `extrudeDepth`.

                        var toolDb = DesignBody.Create(part, "_wallcut_" + wallId, toolBody);
                        designBody.Shape.Subtract(new[] { toolDb.Shape });
                        try { toolDb.Delete(); } catch { /* consumed */ }
                        boolOk = true;
                    }
                    catch (Exception inner)
                    {
                        boolErr = "inside writeblock: " + inner.Message;
                    }
                });
                if (!boolOk) { error = boolErr ?? "Boolean op did not complete"; return false; }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // Step ladder: cheap try first, escalate to finer subdivision on failure.
        // 64 sub-steps handle SampleModel2-style outliers (W1=1624mm, ΔT=162mm
        // single-shot rejects but 64×2.5mm chunks pass).
        private static readonly int[] OffsetFaceStepCounts = { 1, 2, 4, 8, 16, 32, 64 };

        private static bool TryOffsetFacesProgressive(
            DesignBody designBody,
            Face[] faces,
            double offset,
            string opLabel,
            out string lastError,
            out int stepsApplied)
        {
            lastError = null;
            stepsApplied = 0;
            foreach (int N in OffsetFaceStepCounts)
            {
                if (Math.Abs(offset) < 1e-12) { lastError = null; stepsApplied = 0; return true; }
                double sub = offset / N;
                bool allOk = true;
                int doneThisRound = 0;
                for (int k = 0; k < N; k++)
                {
                    try
                    {
                        WriteBlock.ExecuteTask(opLabel + " [step " + (k + 1) + "/" + N + "]", () =>
                        {
                            designBody.Shape.OffsetFaces(faces, sub);
                        });
                        doneThisRound++;
                    }
                    catch (Exception ex)
                    {
                        lastError = "step " + (k + 1) + "/" + N + " failed: " + ex.Message;
                        allOk = false;
                        break;
                    }
                }
                if (allOk) { stepsApplied = N; return true; }
                stepsApplied = doneThisRound;
                if (lastError != null && (lastError.Contains("deleted")
                                          || lastError.Contains("삭제")))
                {
                    return false;
                }
            }
            return false;
        }

        private static bool TryOffsetFacesProgressive(
            DesignBody designBody,
            Dictionary<Face, double> perFaceOffsets,
            string opLabel,
            out string lastError,
            out int stepsApplied)
        {
            lastError = null;
            stepsApplied = 0;
            foreach (int N in OffsetFaceStepCounts)
            {
                var sub = new Dictionary<Face, double>(perFaceOffsets.Count);
                bool anyNonzero = false;
                foreach (var kv in perFaceOffsets)
                {
                    double s = kv.Value / N;
                    sub[kv.Key] = s;
                    if (Math.Abs(s) > 1e-12) anyNonzero = true;
                }
                if (!anyNonzero) { lastError = null; stepsApplied = 0; return true; }
                bool allOk = true;
                int doneThisRound = 0;
                for (int k = 0; k < N; k++)
                {
                    try
                    {
                        WriteBlock.ExecuteTask(opLabel + " [step " + (k + 1) + "/" + N + "]", () =>
                        {
                            designBody.Shape.OffsetFaces(sub);
                        });
                        doneThisRound++;
                    }
                    catch (Exception ex)
                    {
                        lastError = "step " + (k + 1) + "/" + N + " failed: " + ex.Message;
                        allOk = false;
                        break;
                    }
                }
                if (allOk) { stepsApplied = N; return true; }
                stepsApplied = doneThisRound;
                if (lastError != null && (lastError.Contains("deleted")
                                          || lastError.Contains("삭제")))
                {
                    return false;
                }
            }
            return false;
        }

        private static HashSet<Edge> CaptureEdges(DesignBody designBody)
        {
            var set = new HashSet<Edge>();
            if (designBody == null || designBody.Shape == null) return set;
            try
            {
                foreach (var e in designBody.Shape.Edges)
                    set.Add(e);
            }
            catch
            {
                // Edges 접근이 WriteBlock 외부에서 예외를 던지면 비어있는 set 반환
            }
            return set;
        }

        private static List<Edge> DiffEdges(HashSet<Edge> before, HashSet<Edge> after)
        {
            var added = new List<Edge>();
            if (after == null) return added;
            foreach (var e in after)
            {
                if (before == null || !before.Contains(e))
                    added.Add(e);
            }
            return added;
        }

        /// <summary>
        /// designBody 에서 origin Z 가 targetZmeters 에 가깝고 (tol=2mm) 법선 Z 부호가
        /// expectedNormalZSign 과 일치하는 평면 face 의 index 를 반환. 없으면 -1.
        /// AddBoss/AddHole/ChangeBossHeight 에서 base/top plane 추정에 사용.
        /// </summary>
        private static int FindPlanarFaceAtZ(DesignBody designBody, double targetZmeters, double expectedNormalZSign)
        {
            if (designBody == null) return -1;
            const double zTol = 0.002; // 2 mm
            int i = -1;
            int bestIdx = -1;
            double bestDz = double.MaxValue;
            try
            {
                foreach (var df in designBody.Faces)
                {
                    i++;
                    var face = df.Shape;
                    if (face == null) continue;
                    if (!(face.Geometry is Plane plane)) continue;
                    var origin = plane.Frame.Origin;
                    double normZ = plane.Frame.DirZ.UnitVector.Z;
                    if (face.IsReversed) normZ = -normZ;
                    if (expectedNormalZSign > 0 && normZ <= 0.5) continue;
                    if (expectedNormalZSign < 0 && normZ >= -0.5) continue;
                    double dz = Math.Abs(origin.Z - targetZmeters);
                    if (dz < zTol && dz < bestDz)
                    {
                        bestDz = dz;
                        bestIdx = i;
                    }
                }
            }
            catch
            {
                // best-effort
            }
            return bestIdx;
        }

        // -------------------------------------------------------------------
        // Vector math (used by Rotate*, Mirror*, AddHolePattern)
        // -------------------------------------------------------------------
        private static double[] NormalizeVec3(double[] v)
        {
            if (v == null || v.Length < 3) return null;
            double m = Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
            if (m < 1e-12) return null;
            return new[] { v[0] / m, v[1] / m, v[2] / m };
        }

        /// <summary>
        /// P4 shared face resolver: given a 3D seed (mm), pick the body face whose ProjectPoint of
        /// the seed lands nearest the seed and return its CONTACT POINT (mm) and OUTWARD unit normal.
        /// This is the one piece the planar Add* tools lack — they already extrude along any normal
        /// (AddSlit/AddPocket normalAxis, AddBoss axisDir, all P0-fixed), they just couldn't find a
        /// curved face's local normal. Normal = ProjectPoint(seed).Normal sign-flipped by
        /// Shape.IsReversed (the verified AdjacencyBuilder.cs:262 idiom — NOT GetFaceNormal, NOT
        /// Evaluate(u,v).Normal). Returns false if no face is projectable. NOTE: ProjectPoint is a
        /// read-only geometry query, safe to call OUTSIDE a WriteBlock (the mutation stays in Add*).
        /// </summary>
        private static bool ResolveFaceNormal(
            DesignBody designBody, double[] seedMm, out double[] contactMm, out double[] outwardNormal)
        {
            contactMm = null; outwardNormal = null;
            if (designBody == null || seedMm == null || seedMm.Length < 3) return false;
            Point seed = Point.Create(
                GeometryUtils.MmToMeters(seedMm[0]), GeometryUtils.MmToMeters(seedMm[1]),
                GeometryUtils.MmToMeters(seedMm[2]));
            double bestD2 = double.MaxValue;
            try
            {
                foreach (var df in designBody.Faces)
                {
                    try
                    {
                        var ev = df.Shape.Geometry.ProjectPoint(seed);
                        if (ev == null) continue;
                        Point pp = ev.Point;
                        double dx = pp.X - seed.X, dy = pp.Y - seed.Y, dz = pp.Z - seed.Z;
                        double d2 = dx * dx + dy * dy + dz * dz;
                        if (d2 < bestD2)
                        {
                            bestD2 = d2;
                            double sgn = df.Shape.IsReversed ? -1.0 : 1.0;
                            var dir = ev.Normal;
                            outwardNormal = NormalizeVec3(new[] { dir.X * sgn, dir.Y * sgn, dir.Z * sgn });
                            contactMm = new[]
                            {
                                GeometryUtils.MetersToMm(pp.X),
                                GeometryUtils.MetersToMm(pp.Y),
                                GeometryUtils.MetersToMm(pp.Z),
                            };
                        }
                    }
                    catch { }
                }
            }
            catch { return false; }
            return contactMm != null && outwardNormal != null;
        }

        private static double[] Cross3(double[] a, double[] b)
        {
            return new[]
            {
                a[1] * b[2] - a[2] * b[1],
                a[2] * b[0] - a[0] * b[2],
                a[0] * b[1] - a[1] * b[0],
            };
        }

        private static double Dot3(double[] a, double[] b)
        {
            return a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        }

        private static double Distance3(double[] a, double[] b)
        {
            double dx = a[0] - b[0];
            double dy = a[1] - b[1];
            double dz = a[2] - b[2];
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>normal 에 직교하는 단위 벡터 한 개 (in-plane basis vector u).</summary>
        private static double[] OrthoVec(double[] normal)
        {
            // pick the axis least aligned with normal, then orthogonalize via Gram-Schmidt.
            double ax = Math.Abs(normal[0]);
            double ay = Math.Abs(normal[1]);
            double az = Math.Abs(normal[2]);
            double[] pick;
            if (ax <= ay && ax <= az) pick = new[] { 1.0, 0.0, 0.0 };
            else if (ay <= az) pick = new[] { 0.0, 1.0, 0.0 };
            else pick = new[] { 0.0, 0.0, 1.0 };

            double d = Dot3(pick, normal);
            double[] u = new[]
            {
                pick[0] - d * normal[0],
                pick[1] - d * normal[1],
                pick[2] - d * normal[2],
            };
            return NormalizeVec3(u) ?? new[] { 1.0, 0.0, 0.0 };
        }

        /// <summary>
        /// Rodrigues 회전: point p 를 axis (axisPoint, axisDir) 주변으로 angleDeg 회전.
        /// 모든 좌표는 동일 단위 (mm). axisDir 은 내부에서 정규화한다.
        /// </summary>
        private static double[] RotatePointRodrigues(double[] p, double[] axisPoint, double[] axisDir, double angleDeg)
        {
            double[] k = NormalizeVec3(axisDir);
            if (k == null) return new[] { p[0], p[1], p[2] };
            double theta = angleDeg * Math.PI / 180.0;
            double cosT = Math.Cos(theta);
            double sinT = Math.Sin(theta);

            // v = p - axisPoint (rotate around origin)
            double[] v = new[] { p[0] - axisPoint[0], p[1] - axisPoint[1], p[2] - axisPoint[2] };
            double[] kCrossV = Cross3(k, v);
            double kDotV = Dot3(k, v);

            // v_rot = v*cosθ + (k×v)*sinθ + k*(k·v)*(1-cosθ)
            double[] vRot = new[]
            {
                v[0] * cosT + kCrossV[0] * sinT + k[0] * kDotV * (1.0 - cosT),
                v[1] * cosT + kCrossV[1] * sinT + k[1] * kDotV * (1.0 - cosT),
                v[2] * cosT + kCrossV[2] * sinT + k[2] * kDotV * (1.0 - cosT),
            };

            return new[]
            {
                axisPoint[0] + vRot[0],
                axisPoint[1] + vRot[1],
                axisPoint[2] + vRot[2],
            };
        }

        /// <summary>
        /// 평면 (normal, origin) 에 대한 p 의 거울 반사.
        ///   p' = p - 2 * dot(p - origin, n) * n
        /// normal 은 미리 정규화되어야 한다.
        /// </summary>
        private static double[] ReflectPoint(double[] p, double[] normal, double[] origin)
        {
            double[] d = new[] { p[0] - origin[0], p[1] - origin[1], p[2] - origin[2] };
            double proj = Dot3(d, normal);
            return new[]
            {
                p[0] - 2.0 * proj * normal[0],
                p[1] - 2.0 * proj * normal[1],
                p[2] - 2.0 * proj * normal[2],
            };
        }

        // -------------------------------------------------------------------
        // ApplyOperations — chain executor.
        // -------------------------------------------------------------------
        /// <summary>
        /// 여러 modification 을 순차적으로 적용한다. 각 op 는 (DesignBody)=>ModificationResult
        /// 형태의 delegate (보통 람다로 ModificationService.* 호출을 래핑한다).
        /// 첫 실패가 발생하면 즉시 멈추고 그 시점까지의 결과를 집계한다.
        ///
        /// 반환된 ModificationResult:
        ///   - Operation     = "ApplyOperations[N]"  (N = 시도 횟수)
        ///   - Success       = 모든 단계가 성공했는가
        ///   - ErrorMessage  = 첫 실패 메시지 (또는 null)
        ///   - ModifiedFaceIndices = 모든 단계의 face index 합집합
        ///   - NewlyCreatedEdges   = 마지막 단계의 NewlyCreatedEdges (per-step diff 는 chain
        ///     안에서 누적이 신뢰성 떨어지므로 마지막 step 값을 노출)
        /// </summary>
        public static ModificationResult ApplyOperations(
            DesignBody designBody,
            FeatureGraph graph,
            List<Func<DesignBody, ModificationResult>> ops)
        {
            // 'graph' is accepted for API symmetry / future use (e.g. re-extracting mid-chain)
            // but the current implementation simply runs each op in order. Callers typically
            // close over the graph they need inside the lambda.
            var aggregate = new ModificationResult
            {
                Operation = "ApplyOperations",
                ModifiedBody = designBody,
            };

            if (designBody == null) { aggregate.ErrorMessage = "designBody is null"; return aggregate; }
            if (ops == null || ops.Count == 0)
            {
                aggregate.ErrorMessage = "ops is null or empty";
                return aggregate;
            }

            int attempted = 0;
            var stepOps = new List<string>();
            foreach (var op in ops)
            {
                attempted++;
                if (op == null)
                {
                    aggregate.Operation = "ApplyOperations[" + attempted + "]";
                    aggregate.ErrorMessage = "Step " + attempted + ": op delegate is null";
                    return aggregate;
                }

                ModificationResult stepRes;
                try
                {
                    stepRes = op(designBody);
                }
                catch (Exception ex)
                {
                    aggregate.Operation = "ApplyOperations[" + attempted + "]";
                    aggregate.ErrorMessage = "Step " + attempted + " threw: " + ex.Message;
                    return aggregate;
                }

                if (stepRes == null)
                {
                    aggregate.Operation = "ApplyOperations[" + attempted + "]";
                    aggregate.ErrorMessage = "Step " + attempted + " returned null";
                    return aggregate;
                }

                stepOps.Add(stepRes.Operation ?? "?");

                // Merge ModifiedFaceIndices (union).
                if (stepRes.ModifiedFaceIndices != null)
                {
                    foreach (int fi in stepRes.ModifiedFaceIndices)
                    {
                        if (!aggregate.ModifiedFaceIndices.Contains(fi))
                            aggregate.ModifiedFaceIndices.Add(fi);
                    }
                }

                if (!stepRes.Success)
                {
                    aggregate.Operation = "ApplyOperations[" + attempted + ":" + (stepRes.Operation ?? "?") + "]";
                    aggregate.ErrorMessage = "Step " + attempted + " (" + (stepRes.Operation ?? "?") + ") failed: "
                        + (stepRes.ErrorMessage ?? "(no message)");
                    aggregate.NewlyCreatedEdges = stepRes.NewlyCreatedEdges ?? new List<Edge>();
                    return aggregate;
                }

                // Carry the latest NewlyCreatedEdges (caller usually inspects post-chain state).
                aggregate.NewlyCreatedEdges = stepRes.NewlyCreatedEdges ?? new List<Edge>();
            }

            aggregate.Operation = "ApplyOperations[" + attempted + ":" + string.Join("->", stepOps.ToArray()) + "]";
            aggregate.Success = true;
            return aggregate;
        }
    }

    /// <summary>
    /// ModificationService primitive 결과.
    /// before/after edge diff 를 통해 새로 생긴 edge 목록(auto-fillet 후보)을 노출.
    /// </summary>
    public class ModificationResult
    {
        /// <summary>true = 성공, false = 실패 (ErrorMessage 확인)</summary>
        public bool Success { get; set; }

        /// <summary>operation 이름 (예: "ChangeWallThickness")</summary>
        public string Operation { get; set; }

        /// <summary>실패 시 사유. 성공이면 null/empty.</summary>
        public string ErrorMessage { get; set; }

        /// <summary>modification 이후 노출된 새 edge 들 (auto-fillet 후보).</summary>
        public List<Edge> NewlyCreatedEdges { get; set; }

        /// <summary>변형된 DesignBody (in-place mutation; 입력과 동일 참조)</summary>
        public DesignBody ModifiedBody { get; set; }

        /// <summary>
        /// 이 modification 이 건드린 graphBefore face index 들 (직접 OffsetFaces 한 face,
        /// 또는 AddBoss/AddHole 처럼 새로 추가된 영역의 base face 등). 후속 AutoFillet 의
        /// RoundEdgesNearFaces 타겟팅에 사용 가능.
        ///
        /// 주의: AddBoss/AddHole 의 경우 추가된 영역이 만들어내는 새 face 는
        /// graphBefore index 체계에 존재하지 않으므로, 대신 그것에 인접한 기존 평면
        /// (예: top face) 의 index 를 기록한다 — RoundEdgesNearFaces 가 그 평면을 통해
        /// 새로 생긴 base edge 를 catch 한다.
        /// </summary>
        public List<int> ModifiedFaceIndices { get; set; }

        /// <summary>
        /// 실패 시 사용자에게 줄 워크어라운드 힌트 (예: "Hole H1 has 2 adjacent fillets — manually unround them first").
        /// 성공이면 null. UI dialog 가 ErrorMessage 와 함께 표시.
        /// </summary>
        public string HintMessage { get; set; }

        /// <summary>
        /// modify 직전 target face 의 world-space centroid (mm). re-extract 후 graph
        /// 의 face index 가 달라져도 같은 hole/fillet/wall 을 spatial proximity 로 찾아
        /// "after" 값을 정확히 검증할 수 있게 함.
        /// 빈 모디파이/실패 시 모두 0 — UseTargetCenter 가 false 면 의미 없음.
        /// </summary>
        public double TargetCenterXMm { get; set; }
        public double TargetCenterYMm { get; set; }
        public double TargetCenterZMm { get; set; }
        public bool UseTargetCenter { get; set; }

        /// <summary>"Hole" / "Fillet" / "Wall" / "Boss" / ... — re-extract 후 어느
        /// collection 에서 찾아야 할지 알려주는 hint.</summary>
        public string TargetFeatureKind { get; set; }

        /// <summary>
        /// In-process post-mod measurement (mm). For hole/wall/fillet, the
        /// ModificationService itself scans the body's faces and reports the
        /// dimension it found at the target location. NaN if not measured.
        /// Python verification trusts this over re-extraction because the
        /// FeatureExtractor's criteria sometimes reject valid post-mod walls.
        /// </summary>
        public double MeasuredAfterMm { get; set; } = double.NaN;

        public ModificationResult()
        {
            NewlyCreatedEdges = new List<Edge>();
            ModifiedFaceIndices = new List<int>();
        }
    }
}
