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
    /// P6 (FROM_SCRATCH_ROADMAP.md) Tier-2 post-generation validation. Tier-1 is
    /// PhoneParameters.Validate() (reject impossible specs BEFORE geometry). Tier-2 reads the
    /// LIVE B-rep of the generated solid (kernel-truth, never the extractor) and checks
    /// manufacturability invariants the per-stage logs alone can't catch:
    ///   - single closed solid (Volume > 0)
    ///   - minimum wall thickness by axis march (the thin-shell killer)
    ///   - feature no-op detection (a subtractive stage with dV ~ 0 = a cut that didn't bite)
    ///
    /// ContainsPoint is used ONLY via a multi-sample MARCH (a consecutive-transition is reliable;
    /// a single isolated probe is not - proven flaky at bore voids). Magnitude, not point trust.
    /// </summary>
    public static class ValidationService
    {
        public class ValidationResult
        {
            public bool Pass;
            public List<string> Issues = new List<string>();
            public double VolumeMm3;
            public double MinWallMm = -1;
        }

        /// <summary>Validate a generated hollow-tray phone body against its params. API-thread only.</summary>
        public static ValidationResult Validate(DesignBody body, PhoneParameters p)
        {
            var r = new ValidationResult();
            if (body == null) { r.Issues.Add("body is null"); return r; }

            double vol = VolMm3(body);
            r.VolumeMm3 = vol;
            if (vol <= 0) r.Issues.Add("not a closed solid (Volume<=0)");

            // min-wall by march (only when hollowed). Sample floor + flanks away from corners.
            if (p.HollowWallMm > 0)
            {
                double wall = p.HollowWallMm, L = p.LengthMm, W = p.WidthMm, T = p.ThicknessMm;
                double zmid = (wall + T) / 2.0;
                double floor = MarchSolidRun(body, L / 4.0, 0.0, -0.2, 0, 0, 1, T + 0.5);
                double xfl = MarchSolidRun(body, L / 2.0 - wall - 2.0, 0.0, zmid, 1, 0, 0, wall + 3.0);
                double yfl = MarchSolidRun(body, 0.0, W / 2.0 - wall - 2.0, zmid, 0, 1, 0, wall + 3.0);
                double minw = Math.Min(floor, Math.Min(xfl, yfl));
                r.MinWallMm = minw;
                if (minw < p.MinWallMm - 0.05)
                    r.Issues.Add(string.Format(
                        "min wall {0:0.###}mm < MinWall {1:0.###}mm (floor={2:0.###} xflank={3:0.###} yflank={4:0.###})",
                        minw, p.MinWallMm, floor, xfl, yfl));
            }

            r.Pass = r.Issues.Count == 0;
            return r;
        }

        /// <summary>
        /// Flag a no-op cut: a subtractive stage whose volume delta is ~0 means the cutter never
        /// bit (e.g. a pocket on an already-hollow region). Returns an issue string or null.
        /// </summary>
        public static string CheckCut(string stage, double vBefore, double vAfter, double expectMagMm3)
        {
            double dV = vAfter - vBefore;
            double tol = Math.Max(1e-3, 0.05 * Math.Abs(expectMagMm3));
            if (Math.Abs(dV) < tol)
                return stage + ": no-op cut (dV=" + dV.ToString("0.###") + ", expected ~" + (-expectMagMm3).ToString("0.###") + ")";
            return null;
        }

        // --- kernel-truth helpers ---
        private static double VolMm3(DesignBody b)
        {
            try { return b.Shape.Volume * 1e9; } catch { return 0; }
        }

        // March along a unit dir; return the FIRST contiguous solid run (mm). A transition between
        // consecutive opposite ContainsPoint results is reliable; a single isolated probe is not.
        private static double MarchSolidRun(DesignBody b, double x0, double y0, double z0,
                                            double dx, double dy, double dz, double spanMm)
        {
            const double step = 0.02;
            int n = (int)(spanMm / step);
            bool inside = false; double run0 = -1;
            for (int i = 0; i <= n; i++)
            {
                double t = i * step;
                bool solid = ContainsMm(b, x0 + dx * t, y0 + dy * t, z0 + dz * t);
                if (solid && !inside) { inside = true; run0 = t; }
                else if (!solid && inside) { return t - run0; }
            }
            return (inside && run0 >= 0) ? (n * step - run0) : 0.0;
        }

        private static bool ContainsMm(DesignBody b, double xmm, double ymm, double zmm)
        {
            try { return b.Shape.ContainsPoint(Point.Create(xmm / 1000.0, ymm / 1000.0, zmm / 1000.0)); }
            catch { return false; }
        }
    }
}
