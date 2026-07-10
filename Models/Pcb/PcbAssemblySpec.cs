using System;
using System.Collections.Generic;
using System.Globalization;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Pcb
{
    public enum PcbComponentType { Block, Bga }
    public enum StiffenerSide { Bottom, Top }

    public class PcbHoleSpec
    {
        public double XMm, YMm, DiaMm;
    }

    public class PcbComponentSpec
    {
        public string Ref;                      // designator, e.g. "U1"
        public PcbComponentType Type = PcbComponentType.Block;
        public double XMm, YMm;                 // footprint center on the board
        public double RotDeg;
        public double WMm, LMm, HMm;            // package body size
        /// <summary>Package bottom above the board top; BGA balls fill this gap.</summary>
        public double StandoffMm;
        // ---- BGA ball grid (Type == Bga) --------------------------------
        /// <summary>Ball pitch; 0 with Type=Bga is an error.</summary>
        public double BallPitchMm;
        /// <summary>0 = 55% of pitch (JEDEC-typical collapsed ball).</summary>
        public double BallDiaMm;
        /// <summary>0 = derive from footprint: nx = floor((W - pitch)/pitch) + 1.</summary>
        public int BallsNx, BallsNy;
    }

    public class PcbStiffenerSpec
    {
        public double[][] OutlineMm;            // closed polygon [x, y]
        public double ThicknessMm = 0.15;
        public StiffenerSide Side = StiffenerSide.Bottom;
    }

    /// <summary>
    /// Parametric PCB assembly: arbitrary (also non-convex) polygon board with a hole
    /// map and polygon cutouts, block / BGA components, optional stiffener. Geometry
    /// checks are LOUD: self-intersecting outlines, holes/components outside the board
    /// outline, degenerate polygons, and runaway ball counts are all rejected with
    /// actionable messages before any kernel work.
    /// </summary>
    public class PcbAssemblySpec
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public double[][] OutlineMm;            // board polygon [x, y], >= 3 points
        public double ThicknessMm = 1.0;
        public List<PcbHoleSpec> Holes = new List<PcbHoleSpec>();
        public List<double[][]> CutoutsMm = new List<double[][]>();
        public List<PcbComponentSpec> Components = new List<PcbComponentSpec>();
        public PcbStiffenerSpec Stiffener;
        public string NamePrefix = "Pcb";

        public const int MaxBallsPerComponent = 400;

        public void Validate()
        {
            ValidatePolygon(OutlineMm, "outline");
            if (ThicknessMm <= 0) throw new ArgumentException("thickness_mm must be > 0");

            foreach (var h in Holes)
            {
                if (h.DiaMm <= 0) throw new ArgumentException("hole dia_mm must be > 0");
                if (!PointInPolygon(OutlineMm, h.XMm, h.YMm))
                    throw new ArgumentException(string.Format(Inv,
                        "hole at ({0:0.##}, {1:0.##}) lies outside the board outline", h.XMm, h.YMm));
            }
            foreach (var c in CutoutsMm)
                ValidatePolygon(c, "cutout");

            var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var comp in Components)
            {
                if (string.IsNullOrEmpty(comp.Ref))
                    throw new ArgumentException("every component needs a ref designator");
                if (!refs.Add(comp.Ref))
                    throw new ArgumentException("duplicate component ref: " + comp.Ref);
                if (comp.WMm <= 0 || comp.LMm <= 0 || comp.HMm <= 0)
                    throw new ArgumentException(comp.Ref + ": w/l/h_mm must be > 0");
                if (comp.StandoffMm < 0)
                    throw new ArgumentException(comp.Ref + ": standoff_mm must be >= 0");
                if (!PointInPolygon(OutlineMm, comp.XMm, comp.YMm))
                    throw new ArgumentException(string.Format(Inv,
                        "{0} center ({1:0.##}, {2:0.##}) lies outside the board outline",
                        comp.Ref, comp.XMm, comp.YMm));
                if (comp.Type == PcbComponentType.Bga)
                {
                    if (comp.BallPitchMm <= 0)
                        throw new ArgumentException(comp.Ref + ": bga needs ball_pitch_mm > 0");
                    if (comp.BallDiaMm <= 0) comp.BallDiaMm = 0.55 * comp.BallPitchMm;
                    if (comp.BallDiaMm >= comp.BallPitchMm)
                        throw new ArgumentException(comp.Ref + ": ball dia must be < pitch");
                    if (comp.StandoffMm <= 0)
                        throw new ArgumentException(comp.Ref + ": bga needs standoff_mm > 0 (ball height)");
                    if (comp.BallsNx <= 0)
                        comp.BallsNx = Math.Max(1, (int)Math.Floor((comp.WMm - comp.BallPitchMm)
                            / comp.BallPitchMm) + 1);
                    if (comp.BallsNy <= 0)
                        comp.BallsNy = Math.Max(1, (int)Math.Floor((comp.LMm - comp.BallPitchMm)
                            / comp.BallPitchMm) + 1);
                    int n = comp.BallsNx * comp.BallsNy;
                    if (n > MaxBallsPerComponent)
                        throw new ArgumentException(string.Format(Inv,
                            "{0}: {1} balls exceeds the {2} per-component limit - " +
                            "reduce the grid or raise the pitch", comp.Ref, n, MaxBallsPerComponent));
                }
            }
            if (Stiffener != null)
            {
                ValidatePolygon(Stiffener.OutlineMm, "stiffener outline");
                if (Stiffener.ThicknessMm <= 0)
                    throw new ArgumentException("stiffener thickness_mm must be > 0");
            }
        }

        // ------------------------------------------------------------------
        // polygon math (shared by validation, the service, and the gates)
        // ------------------------------------------------------------------
        public static void ValidatePolygon(double[][] poly, string what)
        {
            if (poly == null || poly.Length < 3)
                throw new ArgumentException(what + " needs >= 3 [x, y] points");
            foreach (var p in poly)
                if (p == null || p.Length < 2)
                    throw new ArgumentException(what + " points must be [x, y]");
            if (Math.Abs(ShoelaceArea(poly)) < 1e-9)
                throw new ArgumentException(what + " is degenerate (zero area)");
            int n = poly.Length;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    // skip adjacent edges (they share an endpoint by construction)
                    if (j == i || j == (i + 1) % n || (j + 1) % n == i) continue;
                    if (SegmentsIntersect(poly[i], poly[(i + 1) % n], poly[j], poly[(j + 1) % n]))
                        throw new ArgumentException(string.Format(Inv,
                            "{0} is self-intersecting (edge {1} crosses edge {2})", what, i, j));
                }
            }
        }

        /// <summary>Signed shoelace area (mm^2) — positive for CCW.</summary>
        public static double ShoelaceArea(double[][] poly)
        {
            double a = 0;
            for (int i = 0; i < poly.Length; i++)
            {
                var p = poly[i];
                var q = poly[(i + 1) % poly.Length];
                a += p[0] * q[1] - q[0] * p[1];
            }
            return a / 2;
        }

        /// <summary>Ray-casting point-in-polygon (boundary counts as inside).</summary>
        public static bool PointInPolygon(double[][] poly, double x, double y)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                double xi = poly[i][0], yi = poly[i][1], xj = poly[j][0], yj = poly[j][1];
                if ((yi > y) != (yj > y)
                    && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                    inside = !inside;
            }
            return inside;
        }

        private static bool SegmentsIntersect(double[] a, double[] b, double[] c, double[] d)
        {
            Func<double[], double[], double[], double> cross = (p, q, r) =>
                (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0]);
            double d1 = cross(c, d, a), d2 = cross(c, d, b);
            double d3 = cross(a, b, c), d4 = cross(a, b, d);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0))
                && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }
    }

    public class PcbAssemblyResult
    {
        public bool Success;
        public string Error;
        public List<string> BodiesCreated = new List<string>();
        public Dictionary<string, double> DimsMm = new Dictionary<string, double>();
        public List<string> Log = new List<string>();
    }
}
