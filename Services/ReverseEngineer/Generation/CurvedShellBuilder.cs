using System;
using System.Collections.Generic;

using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;

#if V251
using SpaceClaim.Api.V251;
using SpaceClaim.Api.V251.Modeler;
using SpaceClaim.Api.V251.Geometry;
#elif V252
using SpaceClaim.Api.V252;
using SpaceClaim.Api.V252.Modeler;
using SpaceClaim.Api.V252.Geometry;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Generation
{
    /// <summary>
    /// P2 (FROM_SCRATCH_ROADMAP.md): turn the P1 solid slab into a thin-walled phone shell.
    ///
    /// v1 = uniform-wall open TRAY via a single Boolean-inset cavity Subtract + RoundEdges
    /// corners. ZERO Unsupported.* calls — pure RectangleProfile/ExtrudeProfile/Subtract/
    /// RoundEdges, the exact P0/P1-proven idiom, so it inherits the headless ANSYS-Student pass.
    /// Thicken is the WRONG verb (XML: it walls a SHEET into a solid, does not hollow a closed
    /// solid); OffsetBodyFaces is kernel-fragile on thin walls — both dropped per the P2 spec.
    ///
    /// Wall is uniform BY CONSTRUCTION: inner footprint = outer − 2·wall in X/Y, floor plane at
    /// z = wall, so flank AND floor thickness both equal `wall`. All resulting faces stay
    /// axis-aligned PLANAR, so the (P0-fixed) planar mod tools compose with no curved-face work.
    ///
    /// Caller must already be on the SC API thread inside a WriteBlock (like GenerationService).
    /// v2 (genuinely curved envelope via RevolveTrimmedCurves / faceted Z-stack) is deferred.
    /// </summary>
    public static class CurvedShellBuilder
    {
        public class ShellResult
        {
            public bool Success;
            public string Error;
            public int CornersRounded;
            public int CornersFailed;
            public double WallMm;
        }

        /// <summary>
        /// Hollow <paramref name="body"/> (a solid slab from CreateBaseSolid) into a uniform-wall
        /// open tray of wall = max(MinWallMm, fallback). Rounds the vertical outer corners first.
        /// </summary>
        public static ShellResult HollowToTray(DesignBody body, PhoneParameters p, double wallMm)
        {
            var r = new ShellResult { WallMm = wallMm };
            try
            {
                double L = p.LengthMm, W = p.WidthMm, T = p.ThicknessMm;
                if (wallMm <= 0 || 2 * wallMm >= Math.Min(L, W) || wallMm >= T)
                {
                    r.Error = "wall out of range for envelope";
                    return r;
                }

                // 1) Round the vertical (full-thickness) outer corners. Bulk then per-edge.
                RoundVerticalCorners(body, T, p.CornerRadiusMm, r);

                // 2) Boolean-inset cavity = the hollow. Inner footprint inset by wall on each
                //    side; floor plane at z = wall; cutter extrudes +Z from z=wall up through the
                //    open top (+1mm overshoot) so it spans [wall, T+1] and breaches cleanly.
                double innerW = GeometryUtils.MmToMeters(L - 2 * wallMm);
                double innerL = GeometryUtils.MmToMeters(W - 2 * wallMm);
                double floorZm = GeometryUtils.MmToMeters(wallMm);
                double cutH = GeometryUtils.MmToMeters(T - wallMm + 1.0); // overshoot top by 1mm

                var frame = Frame.Create(Point.Create(0, 0, floorZm), Direction.DirX, Direction.DirY);
                var basePlane = Plane.Create(frame);
                Profile prof = new RectangleProfile(basePlane, innerW, innerL, PointUV.Create(0, 0), 0.0);
                Body cutter = Body.ExtrudeProfile(prof, cutH);

                Part part = body.Parent as Part;
                if (part != null)
                {
                    DesignBody cutDb = DesignBody.Create(part, "_shell_cavity", cutter);
                    body.Shape.Subtract(new[] { cutDb.Shape });
                    try { cutDb.Delete(); } catch { /* Subtract may have consumed it */ }
                }
                else
                {
                    body.Shape.Subtract(new[] { cutter });
                }

                r.Success = true;
                return r;
            }
            catch (Exception ex)
            {
                r.Error = "HollowToTray failed: " + ex.Message;
                return r;
            }
        }

        /// <summary>
        /// v2 hollow for a CURVED-back body (FROM_SCRATCH_ROADMAP.md). The v1 straight-box cavity
        /// leaves a NON-uniform wall under a curved back (flat ceiling vs convex outer => wall grows
        /// by ~bulge at the flanks). Fix: the cavity CEILING must FOLLOW the outer curve offset
        /// inward by `wall`. Cavity = a straight lower box (floor at z=wall, inner footprint =
        /// outer-2*wall) UNITED with an inner curved-back Z-stack mirroring S00a's arc but shrunk
        /// inward by `wall` (each inner slab narrower by 2*wall and lowered by `wall`). Subtracting
        /// it leaves a constant `wall` over the curve, flanks, and floor.
        /// </summary>
        public static ShellResult HollowToCurvedTray(DesignBody body, PhoneParameters p, double wallMm)
        {
            var r = new ShellResult { WallMm = wallMm };
            try
            {
                double L = p.LengthMm, W = p.WidthMm, T = p.ThicknessMm, bulge = p.BackBulgeMm;
                if (wallMm <= 0 || 2 * wallMm >= Math.Min(L, W) || wallMm >= T)
                { r.Error = "wall out of range for envelope"; return r; }

                RoundVerticalCorners(body, T, p.CornerRadiusMm, r);

                Part part = body.Parent as Part;
                double innerW = GeometryUtils.MmToMeters(L - 2 * wallMm);
                double innerL = GeometryUtils.MmToMeters(W - 2 * wallMm);
                double floorZm = GeometryUtils.MmToMeters(wallMm);

                // (a) straight lower cavity box: floor (z=wall) up to the slab top (z=T), inner
                //     footprint inset by wall. Hollows the prismatic part below the curved cap.
                double lowH = GeometryUtils.MmToMeters(T - wallMm);
                var fr0 = Frame.Create(Point.Create(0, 0, floorZm), Direction.DirX, Direction.DirY);
                Profile p0 = new RectangleProfile(Plane.Create(fr0), innerW, innerL, PointUV.Create(0, 0), 0.0);
                Body lower = Body.ExtrudeProfile(p0, lowH);
                SubtractBody(body, part, lower, "_curved_cav_lower");

                // (b) inner curved-back cavity Z-stack: mirror S00a's arc slabs inset by wall in width
                //     AND lowered by wall in height, so the cavity ceiling tracks the outer arc offset
                //     inward by `wall` => uniform wall over the curve.
                const int N = 40;
                double hSlab = bulge / N;
                double halfW = W / 2.0;
                for (int i = 0; i < N; i++)
                {
                    double zMid = (i + 0.5) * hSlab;
                    double frac = 1.0 - zMid / bulge;
                    if (frac <= 0) continue;
                    double yiOuter = halfW * Math.Sqrt(frac);
                    double yiInner = yiOuter - wallMm;            // inset by wall
                    if (yiInner <= 0.05) continue;
                    const double ov = 0.02;
                    double baseZ = (T - wallMm) + i * hSlab - ov; // ceiling = outer slab top - wall
                    double slabH = hSlab + ov;
                    var fr = Frame.Create(Point.Create(0, 0, GeometryUtils.MmToMeters(baseZ)),
                                          Direction.DirX, Direction.DirY);
                    Profile pf = new RectangleProfile(Plane.Create(fr),
                        GeometryUtils.MmToMeters(L - 2 * wallMm), GeometryUtils.MmToMeters(2.0 * yiInner),
                        PointUV.Create(0, 0), 0.0);
                    Body islab = Body.ExtrudeProfile(pf, GeometryUtils.MmToMeters(slabH));
                    SubtractBody(body, part, islab, "_curved_cav_arc");
                }

                r.Success = true;
                return r;
            }
            catch (Exception ex)
            {
                r.Error = "HollowToCurvedTray failed: " + ex.Message;
                return r;
            }
        }

        /// <summary>Subtract a cutter body from the target, wrapping it in a DesignBody so both
        /// operands are design-body-owned (the RT4/RT5 idiom), then delete the cutter.</summary>
        private static void SubtractBody(DesignBody body, Part part, Body cutter, string name)
        {
            if (part != null)
            {
                DesignBody cutDb = DesignBody.Create(part, name, cutter);
                body.Shape.Subtract(new[] { cutDb.Shape });
                try { cutDb.Delete(); } catch { }
            }
            else body.Shape.Subtract(new[] { cutter });
        }

        private static void RoundVerticalCorners(DesignBody body, double thicknessMm, double cornerRmm, ShellResult r)
        {
            if (cornerRmm <= 0) return;
            double radiusM = GeometryUtils.MmToMeters(cornerRmm);
            double tM = GeometryUtils.MmToMeters(thicknessMm);

            // Candidate = a vertical edge whose length ≈ the slab thickness (the 4 corner pillars).
            var candidates = new List<Edge>();
            try
            {
                foreach (var e in body.Shape.Edges)
                {
                    try { if (Math.Abs(e.Length - tM) < 5e-4) candidates.Add(e); }
                    catch { }
                }
            }
            catch { }

            if (candidates.Count == 0) return;

            try
            {
                var map = new Dictionary<Edge, EdgeRound>();
                foreach (var e in candidates)
                    if (!map.ContainsKey(e)) map[e] = new FixedRadiusRound(radiusM);
                body.Shape.RoundEdges(map);
                r.CornersRounded = candidates.Count;
            }
            catch
            {
                // per-edge fallback (RoundEdges is a documented landmine on bulk/large-R)
                foreach (var e in candidates)
                {
                    try
                    {
                        var map = new Dictionary<Edge, EdgeRound> { { e, new FixedRadiusRound(radiusM) } };
                        body.Shape.RoundEdges(map);
                        r.CornersRounded++;
                    }
                    catch { r.CornersFailed++; }
                }
            }
        }
    }
}
