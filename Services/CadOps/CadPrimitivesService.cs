using System;
using System.Collections.Generic;
using System.Globalization;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.CadOps
{
    /// <summary>
    /// General-CAD primitive operations the toolchain was missing — thin, verified wrappers
    /// over kernel APIs that existed in SpaceClaim.Api.V252 but were never used here:
    ///
    ///   * Revolve : Body.SweepProfile along a circular arc about an arbitrary axis
    ///               (the API has no RevolveProfile; a rigid sweep on a circular path
    ///               centered on the axis IS the revolve)
    ///   * Sweep   : Body.SweepProfile along a 3D polyline with tangent arc-blended
    ///               corners (sharp corners are kernel-hostile), optional hollow wall
    ///   * Loft    : Body.LoftProfiles between stacked sections (circle/rect/polygon)
    ///   * Split   : copy + half-space Intersect on both sides of a plane — the
    ///               laminate-proven idiom (Body.Split's piece separation is unexposed)
    ///   * Draft   : Body.TaperFaces on the side faces w.r.t. a neutral plane
    ///   * Transform: move/rotate/scale + copy/pattern of whole named bodies
    ///
    /// All inputs mm/deg, all kernel calls in meters/radians. Raw-body Booleans,
    /// DesignBody.Create at the end (ownership landmine), mm-space double[] vector math.
    /// </summary>
    public class CadPrimitivesService
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------
        // Revolve
        // ------------------------------------------------------------------
        /// <summary>Revolve a closed (r, z) polyline profile about an axis.
        /// r = radial distance from the axis (must be >= 0, not all on the axis),
        /// z = position along the axis. angleDeg in (0, 360].</summary>
        public Body Revolve(double[][] rzProfileMm, double[] axisPointMm, double[] axisDir,
            double angleDeg)
        {
            if (rzProfileMm == null || rzProfileMm.Length < 3)
                throw new ArgumentException("profile needs >= 3 (r,z) points");
            if (angleDeg <= 0 || angleDeg > 360) throw new ArgumentException("angle_deg must be in (0, 360]");
            var a = Norm(axisDir);
            double[] u, v;
            OrthoBasis(a, out u, out v);

            // profile in the (u, a) half-plane through the axis
            var pts = new List<Point>();
            double rSum = 0, zSum = 0;
            foreach (var rz in rzProfileMm)
            {
                if (rz == null || rz.Length < 2) throw new ArgumentException("each profile point is [r, z]");
                if (rz[0] < -1e-9) throw new ArgumentException("profile r must be >= 0");
                pts.Add(AxisPoint(axisPointMm, a, u, rz[0], rz[1]));
                rSum += rz[0]; zSum += rz[1];
            }
            if (rSum < 1e-9) throw new ArgumentException("profile lies entirely on the axis");
            var curves = new List<ITrimmedCurve>();
            for (int i = 0; i < pts.Count; i++)
            {
                Point p0 = pts[i], p1 = pts[(i + 1) % pts.Count];
                if ((p1 - p0).Magnitude < 1e-12) continue;    // tolerate duplicate closing point
                curves.Add(CurveSegment.Create(p0, p1));
            }
            var profilePlane = Plane.Create(Frame.Create(
                Point.Create(GeometryUtils.MmToMeters(axisPointMm[0]),
                             GeometryUtils.MmToMeters(axisPointMm[1]),
                             GeometryUtils.MmToMeters(axisPointMm[2])),
                Direction.Create(u[0], u[1], u[2]),
                Direction.Create(a[0], a[1], a[2])));
            var profile = new Profile(profilePlane, curves);

            // sweep path: circular arc about the axis through the profile's reference
            // point (centroid radius/height keeps the path away from degenerate r=0)
            double rRef = rSum / rzProfileMm.Length, zRef = zSum / rzProfileMm.Length;
            var arcCenter = AxisPoint(axisPointMm, a, u, 0, zRef);
            var circle = Circle.Create(Frame.Create(arcCenter,
                Direction.Create(u[0], u[1], u[2]),
                Direction.Create(v[0], v[1], v[2])),
                GeometryUtils.MmToMeters(rRef));
            double rad = angleDeg * Math.PI / 180.0;
            var path = new List<ITrimmedCurve>
            {
                angleDeg >= 360.0 - 1e-9
                    ? CurveSegment.Create(circle)
                    : CurveSegment.Create(circle, Interval.Create(0, rad)),
            };
            return Body.SweepProfile(profile, path);
        }

        // ------------------------------------------------------------------
        // Sweep
        // ------------------------------------------------------------------
        /// <summary>Sweep a circular (or hollow circular) section along a 3D polyline
        /// with tangent arc corners. wallMm > 0 makes a pipe (outer minus bore).</summary>
        public Body SweepTube(double[][] pathMm, double diaMm, double cornerRMm, double wallMm)
        {
            if (pathMm == null || pathMm.Length < 2) throw new ArgumentException("path needs >= 2 points");
            if (diaMm <= 0) throw new ArgumentException("dia_mm must be > 0");
            if (wallMm < 0 || (wallMm > 0 && wallMm * 2 >= diaMm))
                throw new ArgumentException("wall_mm must be in [0, dia/2)");

            var path = BuildBlendedPath(pathMm, cornerRMm);
            Body outer = SweepCircle(path, pathMm, diaMm / 2);
            if (wallMm <= 0) return outer;
            // the path curves are consumed defensively — rebuild for the bore
            var path2 = BuildBlendedPath(pathMm, cornerRMm);
            Body bore = SweepCircle(path2, pathMm, diaMm / 2 - wallMm);
            outer.Subtract(new List<Body> { bore });
            return outer;
        }

        private static Body SweepCircle(List<ITrimmedCurve> path, double[][] pathMm, double rMm)
        {
            // section plane at the path start, normal = first segment direction
            var d0 = Norm(Sub(pathMm[1], pathMm[0]));
            double[] u, v;
            OrthoBasis(d0, out u, out v);
            var start = Point.Create(GeometryUtils.MmToMeters(pathMm[0][0]),
                GeometryUtils.MmToMeters(pathMm[0][1]), GeometryUtils.MmToMeters(pathMm[0][2]));
            var plane = Plane.Create(Frame.Create(start,
                Direction.Create(u[0], u[1], u[2]), Direction.Create(v[0], v[1], v[2])));
            var circle = Circle.Create(Frame.Create(start,
                Direction.Create(u[0], u[1], u[2]), Direction.Create(v[0], v[1], v[2])),
                GeometryUtils.MmToMeters(rMm));
            var profile = new Profile(plane, new List<ITrimmedCurve> { CurveSegment.Create(circle) });
            return Body.SweepProfile(profile, path);
        }

        /// <summary>Polyline path with tangent arc fillets at interior corners —
        /// kernel sweeps want tangent continuity. cornerR = 0 gives sharp corners.
        /// Fails LOUDLY when the radius does not fit the legs (an over-consumed setback
        /// silently produces a self-crossing path and a wrong solid) or when a corner is
        /// a near-reversal hairpin (tan(phi/2) blows up long before the collinear guard).</summary>
        internal static List<ITrimmedCurve> BuildBlendedPath(double[][] p, double cornerRMm)
        {
            var curves = new List<ITrimmedCurve>();
            var cur = p[0];
            for (int i = 1; i < p.Length; i++)
            {
                bool corner = i < p.Length - 1 && cornerRMm > 1e-9;
                if (!corner)
                {
                    curves.Add(Seg(cur, p[i]));
                    cur = p[i];
                    continue;
                }
                var d1 = Norm(Sub(p[i], cur));
                var d2 = Norm(Sub(p[i + 1], p[i]));
                var n = Cross(d1, d2);
                double nMag = Math.Sqrt(Dot(n, n));
                if (nMag < 1e-9)   // collinear — no corner needed
                {
                    curves.Add(Seg(cur, p[i]));
                    cur = p[i];
                    continue;
                }
                for (int k = 0; k < 3; k++) n[k] /= nMag;
                double cosPhi = Math.Max(-1.0, Math.Min(1.0, Dot(d1, d2)));
                double phi = Math.Acos(cosPhi);                 // turn angle
                if (phi > 170.0 * Math.PI / 180.0)
                    throw new ArgumentException(string.Format(Inv,
                        "path corner {0} is a near-reversal ({1:0.#} deg turn) - " +
                        "an arc blend cannot fit; add an intermediate waypoint", i, phi * 180 / Math.PI));
                double setback = cornerRMm * Math.Tan(phi / 2); // tangent-point distance
                // the setback must fit the REMAINING incoming leg and its share of the
                // outgoing leg (halved when the next vertex is also a blended corner)
                double availIn = Math.Sqrt(Dot(Sub(p[i], cur), Sub(p[i], cur)));
                double availOut = Math.Sqrt(Dot(Sub(p[i + 1], p[i]), Sub(p[i + 1], p[i])));
                if (i + 1 < p.Length - 1) availOut /= 2;
                if (setback > availIn + 1e-9 || setback > availOut + 1e-9)
                    throw new ArgumentException(string.Format(Inv,
                        "corner_r_mm {0:0.###} does not fit the path at vertex {1} " +
                        "(needs {2:0.###}mm of leg, has {3:0.###}mm)",
                        cornerRMm, i, setback, Math.Min(availIn, availOut)));
                double[] t1 = Add(p[i], Scale(d1, -setback));   // tangent point on incoming leg
                double[] t2 = Add(p[i], Scale(d2, setback));    // tangent point on outgoing leg
                // arc center: from t1, perpendicular to d1 (in the corner plane), toward inside
                var inward = Norm(Cross(n, d1));
                double[] c = Add(t1, Scale(inward, cornerRMm));
                var ax = Norm(Sub(t1, c));
                var ay = Cross(n, ax);
                var circle = Circle.Create(Frame.Create(
                    Point.Create(GeometryUtils.MmToMeters(c[0]), GeometryUtils.MmToMeters(c[1]), GeometryUtils.MmToMeters(c[2])),
                    Direction.Create(ax[0], ax[1], ax[2]),
                    Direction.Create(ay[0], ay[1], ay[2])),
                    GeometryUtils.MmToMeters(cornerRMm));
                double segLen = Math.Sqrt(Dot(Sub(t1, cur), Sub(t1, cur)));
                if (segLen > 1e-12) curves.Add(Seg(cur, t1));
                curves.Add(CurveSegment.Create(circle, Interval.Create(0, phi)));
                cur = t2;
            }
            return curves;
        }

        // ------------------------------------------------------------------
        // Loft
        // ------------------------------------------------------------------
        public class LoftSection
        {
            public string Shape = "circle";     // circle | rect | polygon
            public double DiaMm;                // circle / polygon circumscribed dia
            public double WMm, HMm;             // rect
            public int Sides = 6;               // polygon
            public double[] CenterMm = new double[3];
            public double RotDeg;               // in-plane rotation (rect/polygon)
        }

        /// <summary>Loft through stacked sections; section planes are perpendicular to
        /// axisDir (default +Z). ruled = straight transitions (frustum-exact).</summary>
        public Body Loft(List<LoftSection> sections, double[] axisDir, bool ruled)
        {
            if (sections == null || sections.Count < 2)
                throw new ArgumentException("loft needs >= 2 sections");
            var a = Norm(axisDir ?? new double[] { 0, 0, 1 });
            double[] u, v;
            OrthoBasis(a, out u, out v);

            var profiles = new List<ICollection<ITrimmedCurve>>();
            foreach (var s in sections)
                profiles.Add(SectionCurves(s, a, u, v));
            Body body = Body.LoftProfiles(profiles, false, ruled);

            // LoftProfiles yields an OPEN sheet (volume 0) — cap the end sections with
            // planar bodies and Fuse: a closed stitched shell becomes a solid.
            if (!body.IsClosed)
            {
                var s0 = sections[0];
                var s1 = sections[sections.Count - 1];
                Body cap0 = Body.CreatePlanarBody(SectionPlane(s0, u, v), SectionCurves(s0, a, u, v));
                Body cap1 = Body.CreatePlanarBody(SectionPlane(s1, u, v), SectionCurves(s1, a, u, v));
                body.Fuse(new List<Body> { cap0, cap1 }, false, null);
            }
            if (!body.IsClosed)
                throw new InvalidOperationException("loft did not close into a solid");
            return body;
        }

        private static Plane SectionPlane(LoftSection s, double[] u, double[] v)
        {
            return Plane.Create(Frame.Create(MmPoint(s.CenterMm),
                Direction.Create(u[0], u[1], u[2]), Direction.Create(v[0], v[1], v[2])));
        }

        private static ICollection<ITrimmedCurve> SectionCurves(LoftSection s,
            double[] a, double[] u, double[] v)
        {
            var c = s.CenterMm;
            double rot = s.RotDeg * Math.PI / 180.0;
            var curves = new List<ITrimmedCurve>();
            switch ((s.Shape ?? "circle").ToLowerInvariant())
            {
                case "circle":
                {
                    if (s.DiaMm <= 0) throw new ArgumentException("circle section needs dia_mm > 0");
                    var circle = Circle.Create(Frame.Create(MmPoint(c),
                        Direction.Create(u[0], u[1], u[2]), Direction.Create(v[0], v[1], v[2])),
                        GeometryUtils.MmToMeters(s.DiaMm / 2));
                    curves.Add(CurveSegment.Create(circle));
                    break;
                }
                case "rect":
                {
                    if (s.WMm <= 0 || s.HMm <= 0) throw new ArgumentException("rect section needs w_mm/h_mm > 0");
                    var pts = new List<Point>();
                    double[][] corners =
                    {
                        new[] { -s.WMm / 2, -s.HMm / 2 }, new[] { s.WMm / 2, -s.HMm / 2 },
                        new[] { s.WMm / 2, s.HMm / 2 }, new[] { -s.WMm / 2, s.HMm / 2 },
                    };
                    foreach (var uv in corners)
                    {
                        double x = uv[0] * Math.Cos(rot) - uv[1] * Math.Sin(rot);
                        double y = uv[0] * Math.Sin(rot) + uv[1] * Math.Cos(rot);
                        pts.Add(InPlanePoint(c, u, v, x, y));
                    }
                    for (int i = 0; i < 4; i++) curves.Add(CurveSegment.Create(pts[i], pts[(i + 1) % 4]));
                    break;
                }
                case "polygon":
                {
                    if (s.DiaMm <= 0 || s.Sides < 3) throw new ArgumentException("polygon needs dia_mm > 0 and sides >= 3");
                    var pts = new List<Point>();
                    for (int i = 0; i < s.Sides; i++)
                    {
                        double ang = rot + 2 * Math.PI * i / s.Sides;
                        pts.Add(InPlanePoint(c, u, v,
                            s.DiaMm / 2 * Math.Cos(ang), s.DiaMm / 2 * Math.Sin(ang)));
                    }
                    for (int i = 0; i < s.Sides; i++)
                        curves.Add(CurveSegment.Create(pts[i], pts[(i + 1) % s.Sides]));
                    break;
                }
                default:
                    throw new ArgumentException("section shape must be circle|rect|polygon");
            }
            return curves;
        }

        // ------------------------------------------------------------------
        // Split (copy + half-space intersect — the laminate-proven idiom)
        // ------------------------------------------------------------------
        /// <summary>Split a body by a plane into below/above pieces (along the normal).
        /// Returns raw bodies; either may be null when the plane misses the body.</summary>
        public void SplitByPlane(Body shape, double[] planePointMm, double[] planeNormal,
            out Body below, out Body above)
        {
            var n = Norm(planeNormal);
            var bb = shape.GetBoundingBox(Matrix.Identity);
            double diag = (bb.MaxCorner - bb.MinCorner).Magnitude * 1000 + 1.0;  // mm
            // re-anchor the slab OVER THE BODY: any point on the plane is a valid plane
            // spec, so a caller point far from the body would otherwise leave the slab
            // (sized from the body bbox but centered at the caller point) partially or
            // fully missing the body — silent clipping. Projecting the bbox center onto
            // the plane keeps the cut identical and the coverage guaranteed.
            double[] cMm =
            {
                (bb.MinCorner.X + bb.MaxCorner.X) / 2 * 1000,
                (bb.MinCorner.Y + bb.MaxCorner.Y) / 2 * 1000,
                (bb.MinCorner.Z + bb.MaxCorner.Z) / 2 * 1000,
            };
            double dist = Dot(Sub(cMm, planePointMm), n);
            double[] anchor = Sub(cMm, Scale(n, dist));
            below = HalfIntersect(shape, anchor, n, diag, false);
            above = HalfIntersect(shape, anchor, n, diag, true);
        }

        private static Body HalfIntersect(Body shape, double[] pMm, double[] n,
            double sizeMm, bool positiveSide)
        {
            double[] u, v;
            OrthoBasis(n, out u, out v);
            // big slab on one side of the plane
            var plane = Plane.Create(Frame.Create(MmPoint(pMm),
                Direction.Create(u[0], u[1], u[2]), Direction.Create(v[0], v[1], v[2])));
            var prof = new RectangleProfile(plane,
                GeometryUtils.MmToMeters(2 * sizeMm), GeometryUtils.MmToMeters(2 * sizeMm));
            Body slab = Body.ExtrudeProfile(prof, GeometryUtils.MmToMeters(sizeMm));
            if (!positiveSide)
                slab.Transform(Matrix.CreateTranslation(Vector.Create(
                    GeometryUtils.MmToMeters(-sizeMm * n[0]),
                    GeometryUtils.MmToMeters(-sizeMm * n[1]),
                    GeometryUtils.MmToMeters(-sizeMm * n[2]))));
            Body piece = shape.Copy();
            try { piece.Intersect(new List<Body> { slab }); }
            catch { return null; }
            try { if (piece.Volume < 1e-15) return null; } catch { return null; }
            return piece;
        }

        // ------------------------------------------------------------------
        // Draft (taper the side faces about a neutral plane)
        // ------------------------------------------------------------------
        /// <summary>Taper every planar face whose normal is perpendicular to pullDir
        /// (the "side walls") by angleDeg about the neutral plane through neutralPointMm.
        /// Mutates the body in place; returns the number of faces tapered.</summary>
        public int DraftSideFaces(Body shape, double[] neutralPointMm, double[] pullDir,
            double angleDeg, out int skippedNonPlanar)
        {
            var pull = Norm(pullDir);
            var faces = new List<Face>();
            skippedNonPlanar = 0;
            foreach (var f in shape.Faces)
            {
                var pl = f.Geometry as Plane;
                if (pl == null) { skippedNonPlanar++; continue; }
                double[] fn = { pl.Frame.DirZ.X, pl.Frame.DirZ.Y, pl.Frame.DirZ.Z };
                if (Math.Abs(Dot(fn, pull)) < 0.05) faces.Add(f);
            }
            if (faces.Count == 0) return 0;
            double[] u, v;
            OrthoBasis(pull, out u, out v);
            var neutral = Plane.Create(Frame.Create(MmPoint(neutralPointMm),
                Direction.Create(u[0], u[1], u[2]), Direction.Create(v[0], v[1], v[2])));
            shape.TaperFaces(faces, neutral, angleDeg * Math.PI / 180.0);
            return faces.Count;
        }

        // ------------------------------------------------------------------
        // shared helpers
        // ------------------------------------------------------------------
        private static Point MmPoint(double[] p)
        {
            return Point.Create(GeometryUtils.MmToMeters(p[0]),
                GeometryUtils.MmToMeters(p[1]), GeometryUtils.MmToMeters(p[2]));
        }

        private static Point AxisPoint(double[] originMm, double[] a, double[] u,
            double rMm, double zMm)
        {
            return Point.Create(
                GeometryUtils.MmToMeters(originMm[0] + u[0] * rMm + a[0] * zMm),
                GeometryUtils.MmToMeters(originMm[1] + u[1] * rMm + a[1] * zMm),
                GeometryUtils.MmToMeters(originMm[2] + u[2] * rMm + a[2] * zMm));
        }

        private static Point InPlanePoint(double[] cMm, double[] u, double[] v,
            double xMm, double yMm)
        {
            return Point.Create(
                GeometryUtils.MmToMeters(cMm[0] + u[0] * xMm + v[0] * yMm),
                GeometryUtils.MmToMeters(cMm[1] + u[1] * xMm + v[1] * yMm),
                GeometryUtils.MmToMeters(cMm[2] + u[2] * xMm + v[2] * yMm));
        }

        private static ITrimmedCurve Seg(double[] a, double[] b)
        {
            return CurveSegment.Create(MmPoint(a), MmPoint(b));
        }

        internal static void OrthoBasis(double[] a, out double[] u, out double[] v)
        {
            double[] refv = Math.Abs(a[2]) < 0.9 ? new double[] { 0, 0, 1 } : new double[] { 1, 0, 0 };
            u = Norm(Cross(refv, a));
            v = Cross(a, u);   // u x v = a
        }

        private static double Dot(double[] x, double[] y)
        {
            return x[0] * y[0] + x[1] * y[1] + x[2] * y[2];
        }

        private static double[] Cross(double[] x, double[] y)
        {
            return new[]
            {
                x[1] * y[2] - x[2] * y[1],
                x[2] * y[0] - x[0] * y[2],
                x[0] * y[1] - x[1] * y[0],
            };
        }

        private static double[] Norm(double[] x)
        {
            double m = Math.Sqrt(Dot(x, x));
            if (m < 1e-12) throw new ArgumentException("zero-length direction");
            return new[] { x[0] / m, x[1] / m, x[2] / m };
        }

        private static double[] Sub(double[] x, double[] y)
        {
            return new[] { x[0] - y[0], x[1] - y[1], x[2] - y[2] };
        }

        private static double[] Add(double[] x, double[] y)
        {
            return new[] { x[0] + y[0], x[1] + y[1], x[2] + y[2] };
        }

        private static double[] Scale(double[] x, double s)
        {
            return new[] { x[0] * s, x[1] * s, x[2] * s };
        }
    }
}
