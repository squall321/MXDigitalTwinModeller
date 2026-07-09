using System;
using System.Collections.Generic;
using System.Globalization;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Fastener;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Fastener
{
    /// <summary>
    /// Parametric fastener design: detect fastening SITES (coaxial cylindrical hole faces
    /// spanning the plate stack — the "two concentric circles" selection, generalized to any
    /// axis) and generate a bolt (hex/socket/pan/countersunk head + ISO 262/724 thread as
    /// simplified core or cosmetic rings + optional nut/washers) or a rivet (dome/flat/
    /// countersunk factory head + hole-filling shank + bucked tail).
    ///
    /// Every dimension derives from the detected hole diameter and grip via ISO-typical
    /// proportional ratios in FastenerSpec — all overridable, nothing hardcoded downstream.
    ///
    /// Geometry idioms follow the verified package pipeline: components are built at the
    /// world origin along +Z, then mapped once onto the site frame (Matrix.CreateMapping);
    /// curved shapes (dome caps, countersunk cones) are stacked-disc approximations with
    /// SEQUENTIAL unite + small overlaps (a one-shot Unite(collection) rejects tools that
    /// do not touch the target); Booleans run on raw bodies, DesignBody.Create at the end
    /// with rollback on kernel failure.
    /// </summary>
    public class FastenerGenerationService
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int CurveSlices = 10;      // dome / cone stacked-disc resolution
        private const double JoinOverlapMm = 0.02; // unite junction overlap

        // ------------------------------------------------------------------
        // Site detection
        // ------------------------------------------------------------------
        private class CylFace
        {
            public DesignBody Body;
            public double[] Origin;   // mm
            public double[] Dir;      // unit
            public double RadiusMm;
            public double T0, T1;     // axial span (mm) along Dir from world origin projection
        }

        /// <summary>Detect all fastening sites in the part tree: groups of coaxial,
        /// equal-radius cylindrical HOLE faces (axis midpoint outside the body material),
        /// merged across bodies when their axial spans are contiguous (gap ≤ 0.5mm).</summary>
        public List<FastenerSite> DetectSites(Part part)
        {
            var faces = new List<CylFace>();
            var bodies = ConformalMesh.ConformalMeshService.CollectBodies(part, null);
            foreach (var db in bodies)
            {
                foreach (var df in db.Faces)
                {
                    Cylinder cyl;
                    try { cyl = df.Shape.Geometry as Cylinder; }
                    catch { continue; }
                    if (cyl == null) continue;

                    double[] o = { cyl.Frame.Origin.X * 1000, cyl.Frame.Origin.Y * 1000, cyl.Frame.Origin.Z * 1000 };
                    double[] d = { cyl.Frame.DirZ.X, cyl.Frame.DirZ.Y, cyl.Frame.DirZ.Z };
                    double rMm = cyl.Radius * 1000;

                    // axial span: circular-edge CENTERS project exactly onto the axis —
                    // a face BBOX is world-axis-aligned, so its corners overestimate the
                    // span for any tilted hole (up to ~2r error at 45°). BBox corners
                    // remain the fallback for faces without two circular rim edges.
                    double tmin = double.MaxValue, tmax = double.MinValue;
                    int rims = 0;
                    try
                    {
                        foreach (var de in df.Edges)
                        {
                            var circ = de.Shape.Geometry as Circle;
                            if (circ == null) continue;
                            double t = Dot(new[]
                            {
                                circ.Frame.Origin.X * 1000 - o[0],
                                circ.Frame.Origin.Y * 1000 - o[1],
                                circ.Frame.Origin.Z * 1000 - o[2],
                            }, d);
                            if (t < tmin) tmin = t;
                            if (t > tmax) tmax = t;
                            rims++;
                        }
                    }
                    catch { rims = 0; }
                    if (rims < 2 || tmax - tmin < 1e-6)
                    {
                        Box bb;
                        try { bb = df.Shape.GetBoundingBox(Matrix.Identity); }
                        catch { continue; }
                        tmin = double.MaxValue; tmax = double.MinValue;
                        foreach (var c in BoxCorners(bb))
                        {
                            double t = Dot(new[] { c[0] - o[0], c[1] - o[1], c[2] - o[2] }, d);
                            if (t < tmin) tmin = t;
                            if (t > tmax) tmax = t;
                        }
                    }
                    if (tmax - tmin < 1e-6) continue;

                    // HOLE (not boss): the axis midpoint must be OUTSIDE the body material.
                    // Far from any face boundary, so the ContainsPoint oracle is decisive here.
                    double tm = (tmin + tmax) / 2;
                    var mid = Point.Create(
                        (o[0] + d[0] * tm) / 1000.0,
                        (o[1] + d[1] * tm) / 1000.0,
                        (o[2] + d[2] * tm) / 1000.0);
                    bool inMaterial;
                    try { inMaterial = db.Shape.ContainsPoint(mid); }
                    catch { continue; }
                    if (inMaterial) continue;

                    faces.Add(new CylFace
                    {
                        Body = db, Origin = o, Dir = d, RadiusMm = rMm, T0 = tmin, T1 = tmax,
                    });
                }
            }

            // group by coaxiality + equal radius
            var sites = new List<FastenerSite>();
            var used = new bool[faces.Count];
            for (int i = 0; i < faces.Count; i++)
            {
                if (used[i]) continue;
                var group = new List<CylFace> { faces[i] };
                used[i] = true;
                for (int j = i + 1; j < faces.Count; j++)
                {
                    if (used[j]) continue;
                    if (Coaxial(faces[i], faces[j]))
                    {
                        group.Add(faces[j]);
                        used[j] = true;
                    }
                }
                var site = SiteFromGroup(group);
                if (site != null) sites.Add(site);
            }
            return sites;
        }

        /// <summary>Nearest site to a seed point (perpendicular distance to the axis,
        /// clamped to the site's axial span). Null when no sites exist.</summary>
        public FastenerSite FindNearestSite(Part part, double[] seedMm, out List<FastenerSite> all)
        {
            all = DetectSites(part);
            FastenerSite best = null;
            double bestD = double.MaxValue;
            foreach (var s in all)
            {
                double dist = SeedDistance(s, seedMm);
                if (dist < bestD) { bestD = dist; best = s; }
            }
            return best;
        }

        public static double SeedDistance(FastenerSite s, double[] seedMm)
        {
            var v = new[] { seedMm[0] - s.AxisPointMm[0], seedMm[1] - s.AxisPointMm[1], seedMm[2] - s.AxisPointMm[2] };
            double t = Dot(v, s.AxisDir);
            double tc = Math.Max(0, Math.Min(s.GripMm, t));
            double dx = v[0] - s.AxisDir[0] * tc, dy = v[1] - s.AxisDir[1] * tc, dz = v[2] - s.AxisDir[2] * tc;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static bool Coaxial(CylFace a, CylFace b)
        {
            double tolR = Math.Max(0.02 * a.RadiusMm, 0.02);
            if (Math.Abs(a.RadiusMm - b.RadiusMm) > tolR) return false;
            if (Math.Abs(Dot(a.Dir, b.Dir)) < 0.999) return false;
            // perpendicular distance of b's origin from a's axis
            var v = new[] { b.Origin[0] - a.Origin[0], b.Origin[1] - a.Origin[1], b.Origin[2] - a.Origin[2] };
            double t = Dot(v, a.Dir);
            double dx = v[0] - a.Dir[0] * t, dy = v[1] - a.Dir[1] * t, dz = v[2] - a.Dir[2] * t;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz) <= Math.Max(0.02 * a.RadiusMm, 0.05);
        }

        private static FastenerSite SiteFromGroup(List<CylFace> group)
        {
            var refF = group[0];
            // re-project every span onto the reference axis, normalize direction signs
            var spans = new List<double[]>();
            foreach (var f in group)
            {
                double shift = Dot(new[]
                {
                    f.Origin[0] - refF.Origin[0], f.Origin[1] - refF.Origin[1], f.Origin[2] - refF.Origin[2],
                }, refF.Dir);
                bool flipped = Dot(f.Dir, refF.Dir) < 0;
                double s0 = flipped ? shift - f.T1 : shift + f.T0;
                double s1 = flipped ? shift - f.T0 : shift + f.T1;
                spans.Add(new[] { Math.Min(s0, s1), Math.Max(s0, s1) });
            }
            spans.Sort((x, y) => x[0].CompareTo(y[0]));
            // merge contiguous spans (plates in contact); a gap > 0.5mm breaks the stack
            double lo = spans[0][0], hi = spans[0][1];
            foreach (var sp in spans)
            {
                if (sp[0] > hi + 0.5) { if (sp[1] - sp[0] > hi - lo) { lo = sp[0]; hi = sp[1]; } continue; }
                if (sp[1] > hi) hi = sp[1];
            }
            if (hi - lo < 0.05) return null;

            // the kernel's Cylinder.Frame.DirZ sign is arbitrary — normalize so the
            // dominant component is positive, making bottom/top (and thus the HEAD side,
            // which generates at the top) deterministic for identical geometry.
            double[] dir = { refF.Dir[0], refF.Dir[1], refF.Dir[2] };
            int dom = 0;
            for (int k = 1; k < 3; k++)
                if (Math.Abs(dir[k]) > Math.Abs(dir[dom])) dom = k;
            if (dir[dom] < 0)
            {
                for (int k = 0; k < 3; k++) dir[k] = -dir[k];
                double nlo = -hi; hi = -lo; lo = nlo;
            }

            var site = new FastenerSite
            {
                HoleDiaMm = 2 * refF.RadiusMm,
                GripMm = hi - lo,
                FaceCount = group.Count,
            };
            for (int k = 0; k < 3; k++)
            {
                site.AxisDir[k] = dir[k];
                site.AxisPointMm[k] = refF.Origin[k] + dir[k] * lo;
            }
            foreach (var f in group)
                if (!site.BodyNames.Contains(f.Body.Name ?? "")) site.BodyNames.Add(f.Body.Name ?? "");
            return site;
        }

        // ------------------------------------------------------------------
        // Generation
        // ------------------------------------------------------------------
        public FastenerResult Generate(Part part, FastenerSite site, FastenerSpec spec,
            string namePrefix)
        {
            var res = new FastenerResult();
            if (part == null || site == null) { res.Error = "part/site is null"; return res; }
            if (spec == null) spec = new FastenerSpec();

            // ---- derive dimensions (all mm) ----------------------------------
            double hole = site.HoleDiaMm, grip = site.GripMm;
            double d = spec.NominalDMm;
            if (d <= 0)
            {
                d = spec.Type == FastenerType.Rivet
                    ? hole * spec.RivetHoleFillFrac
                    : IsoMetricThread.AutoNominalForHole(hole);
                if (d <= 0) { res.Error = string.Format(Inv, "no standard nominal fits hole d={0:0.###}mm", hole); return res; }
            }
            else if (d > hole + 1e-9 && spec.Type == FastenerType.Bolt)
            {
                res.Error = string.Format(Inv, "nominal {0:0.###} exceeds hole {1:0.###}", d, hole);
                return res;
            }
            double pitch = spec.Thread.PitchMm > 0 ? spec.Thread.PitchMm : IsoMetricThread.CoarsePitchFor(d);
            double minor = IsoMetricThread.MinorDia(d, pitch);
            double headDiaRatio, headHRatio;
            spec.ResolveHeadRatios(out headDiaRatio, out headHRatio);
            double headDia = headDiaRatio * d, headH = headHRatio * d;
            double washerT = spec.WithWasher ? spec.WasherThickRatio * d : 0;
            double nutH = (spec.Type == FastenerType.Bolt && spec.WithNut) ? spec.NutHeightRatio * d : 0;
            double length = spec.LengthMm > 0
                ? spec.LengthMm
                : grip + 2 * washerT + nutH + 2 * pitch;    // protrude 2 pitches past the nut
            double bore = spec.BoreClearanceFrac * d;

            res.DimsMm["hole_d"] = hole; res.DimsMm["grip"] = grip;
            res.DimsMm["nominal_d"] = d; res.DimsMm["pitch"] = pitch; res.DimsMm["minor_d"] = minor;
            res.DimsMm["head_dia"] = headDia; res.DimsMm["head_h"] = headH;
            res.DimsMm["length"] = length; res.DimsMm["nut_h"] = nutH; res.DimsMm["washer_t"] = washerT;

            // local frame: z=0 at the stack BOTTOM face, +z toward the head (top)
            var mapping = SiteMapping(site);

            var components = new List<KeyValuePair<string, Body>>();
            try
            {
                if (spec.Type == FastenerType.Bolt)
                    BuildBolt(components, spec, d, minor, pitch, grip, headDia, headH,
                        washerT, nutH, length, bore, namePrefix, res);
                else
                    BuildRivet(components, spec, d, grip, headDia, headH, namePrefix, res);
            }
            catch (Exception ex)
            {
                res.Error = "fastener geometry failed: " + ex.Message;
                return res;
            }

            // map to site + materialize with rollback
            var created = new List<DesignBody>();
            try
            {
                foreach (var kv in components)
                {
                    kv.Value.Transform(mapping);
                    var db = BodyBuilder.CreateDesignBody(part, kv.Key, kv.Value);
                    created.Add(db);
                    res.BodiesCreated.Add(kv.Key);
                }
            }
            catch (Exception ex)
            {
                foreach (var db in created) { try { db.Delete(); } catch { } }
                res.BodiesCreated.Clear();
                res.Error = "kernel failure (rolled back): " + ex.Message;
                return res;
            }
            res.Success = true;
            return res;
        }

        private void BuildBolt(List<KeyValuePair<string, Body>> outp, FastenerSpec spec,
            double d, double minor, double pitch, double grip, double headDia, double headH,
            double washerT, double nutH, double length, double bore, string prefix,
            FastenerResult res)
        {
            double headBase = grip + washerT;           // head sits on the (washered) top face
            double tip = headBase - length;

            double threadLen = spec.Thread.Style == ThreadStyle.None
                ? 0
                : (spec.Thread.ThreadLenMm > 0 ? Math.Min(spec.Thread.ThreadLenMm, length)
                                               : Math.Min(2.5 * d, length));
            res.DimsMm["thread_len"] = threadLen;
            double threadTop = tip + threadLen;

            // shank: plain zone at d, threaded zone at minor (Simplified/Rings)
            Body bolt;
            if (threadLen <= 0)
            {
                bolt = Disc(d / 2, tip, headBase - tip + JoinOverlapMm);
            }
            else
            {
                bolt = Disc(minor / 2, tip, threadLen + JoinOverlapMm);
                if (threadTop < headBase - 1e-9)
                    UniteInto(ref bolt, Disc(d / 2, threadTop, headBase - threadTop + JoinOverlapMm));
                if (spec.Thread.Style == ThreadStyle.CosmeticRings)
                {
                    int nRings = (int)Math.Floor(threadLen / pitch);
                    double ringT = spec.Thread.RingWidthFrac * pitch;
                    for (int i = 0; i < nRings; i++)
                        UniteInto(ref bolt, Disc(d / 2, tip + (i + 0.3) * pitch, ringT));
                    res.DimsMm["thread_rings"] = nRings;
                }
            }
            UniteInto(ref bolt, BuildHead(spec.Head, d, headDia, headH, headBase));
            outp.Add(new KeyValuePair<string, Body>(prefix + "_Bolt", bolt));

            if (washerT > 0)
                outp.Add(new KeyValuePair<string, Body>(prefix + "_WasherTop",
                    Annulus(spec.WasherOdRatio * d / 2, bore / 2, grip, washerT)));
            if (nutH > 0)
            {
                double nutTop = -washerT;    // under the (washered) bottom face
                Body nut = HexPrism(spec.NutWidthRatio * d, nutTop - nutH, nutH);
                nut.Subtract(new List<Body> { Disc(bore / 2, nutTop - nutH - 1, nutH + 2) });
                outp.Add(new KeyValuePair<string, Body>(prefix + "_Nut", nut));
                if (washerT > 0)
                    outp.Add(new KeyValuePair<string, Body>(prefix + "_WasherBottom",
                        Annulus(spec.WasherOdRatio * d / 2, bore / 2, -washerT, washerT)));
            }
        }

        private void BuildRivet(List<KeyValuePair<string, Body>> outp, FastenerSpec spec,
            double d, double grip, double headDia, double headH, string prefix, FastenerResult res)
        {
            double tailDia = spec.RivetTailDiaRatio * d, tailH = spec.RivetTailHeightRatio * d;
            res.DimsMm["tail_dia"] = tailDia; res.DimsMm["tail_h"] = tailH;

            Body rivet = Disc(d / 2, -JoinOverlapMm, grip + 2 * JoinOverlapMm);   // hole-filling shank
            // factory head on top
            if (spec.Head == HeadStyle.Flat)
                UniteInto(ref rivet, Disc(headDia / 2, grip, headH));
            else if (spec.Head == HeadStyle.Countersunk)
                UniteInto(ref rivet, TaperStack(d / 2, headDia / 2, grip, headH, +1));
            else
                UniteInto(ref rivet, DomeCap(headDia / 2, headH, grip, +1));
            // bucked shop head under the bottom face
            UniteInto(ref rivet, DomeCap(tailDia / 2, tailH, 0, -1));
            outp.Add(new KeyValuePair<string, Body>(prefix + "_Rivet", rivet));
        }

        private Body BuildHead(HeadStyle style, double d, double headDia, double headH, double zBase)
        {
            switch (style)
            {
                case HeadStyle.Hex:
                    return HexPrism(headDia, zBase, headH);           // headDia = across flats
                case HeadStyle.Countersunk:
                    // proud-seated cone: nominal at the seat widening up to headDia flat top
                    return TaperStack(d / 2, headDia / 2, zBase, headH, +1);
                case HeadStyle.Dome:
                    return DomeCap(headDia / 2, headH, zBase, +1);
                default:                                              // SocketCap, Pan, Flat
                    return Disc(headDia / 2, zBase, headH);
            }
        }

        // ------------------------------------------------------------------
        // primitive builders (local coords: +Z axis, mm in / meters to kernel)
        // ------------------------------------------------------------------
        private static Body Disc(double rMm, double z0Mm, double hMm)
        {
            Body b = BodyBuilder.CreateCylinder(
                GeometryUtils.MmToMeters(rMm), GeometryUtils.MmToMeters(hMm));
            b.Transform(Matrix.CreateTranslation(Vector.Create(0, 0, GeometryUtils.MmToMeters(z0Mm))));
            return b;
        }

        private static Body Annulus(double roMm, double riMm, double z0Mm, double hMm)
        {
            Body b = Disc(roMm, z0Mm, hMm);
            b.Subtract(new List<Body> { Disc(riMm, z0Mm - 1, hMm + 2) });
            return b;
        }

        private static Body HexPrism(double acrossFlatsMm, double z0Mm, double hMm)
        {
            double rc = GeometryUtils.MmToMeters(acrossFlatsMm) / Math.Sqrt(3.0);
            var pts = new Point[6];
            for (int i = 0; i < 6; i++)
            {
                double a = Math.PI / 6 + i * Math.PI / 3;   // flats aligned to X
                pts[i] = Point.Create(rc * Math.Cos(a), rc * Math.Sin(a), 0);
            }
            var pb = new ProfileBuilder(Plane.PlaneXY);
            for (int i = 0; i < 6; i++) pb.AddLine(pts[i], pts[(i + 1) % 6]);
            Body b = Body.ExtrudeProfile(pb.Build(), GeometryUtils.MmToMeters(hMm));
            b.Transform(Matrix.CreateTranslation(Vector.Create(0, 0, GeometryUtils.MmToMeters(z0Mm))));
            return b;
        }

        /// <summary>Linear taper (cone) via stacked discs: radius r0 at the base plane,
        /// r1 at the far end, growing along dir (+1 = +Z from zBase, -1 = downward).</summary>
        private static Body TaperStack(double r0Mm, double r1Mm, double zBaseMm, double hMm, int dir)
        {
            return Stack(zBaseMm, hMm, dir, s => r0Mm + (r1Mm - r0Mm) * s);
        }

        /// <summary>Spherical cap via stacked discs: base radius a at zBase shrinking to 0
        /// at height h, along dir. R = (a^2 + h^2) / 2h.</summary>
        private static Body DomeCap(double aMm, double hMm, double zBaseMm, int dir)
        {
            double R = (aMm * aMm + hMm * hMm) / (2 * hMm);
            return Stack(zBaseMm, hMm, dir, s =>
            {
                double z = s * hMm;
                double r2 = R * R - (z - (hMm - R)) * (z - (hMm - R));
                return Math.Sqrt(Math.Max(r2, 0));
            });
        }

        /// <summary>Stacked-disc solid: slice radii from radiusAt(s), s = mid-fraction of
        /// each slice. Sequential unite with overlap — the package-pipeline lesson.</summary>
        private static Body Stack(double zBaseMm, double hMm, int dir, Func<double, double> radiusAt)
        {
            double sliceH = hMm / CurveSlices;
            double eps = sliceH * 0.1;
            Body result = null;
            for (int i = 0; i < CurveSlices; i++)
            {
                double s = (i + 0.5) / CurveSlices;
                double r = Math.Max(radiusAt(s), 0.02 * hMm);
                double lo = i * sliceH - (i > 0 ? eps : 0);
                double hi = (i + 1) * sliceH;
                double z0 = dir > 0 ? zBaseMm + lo : zBaseMm - hi;
                Body slice = Disc(r, z0, hi - lo);
                if (result == null) result = slice;
                else result.Unite(new List<Body> { slice });
            }
            return result;
        }

        private static void UniteInto(ref Body target, Body tool)
        {
            target.Unite(new List<Body> { tool });
        }

        /// <summary>Mapping from local build coords (origin, +Z) onto the site frame
        /// (bottom point, axis dir). Basis via cross products — axis-agnostic.</summary>
        private static Matrix SiteMapping(FastenerSite site)
        {
            var a = site.AxisDir;
            double[] refv = Math.Abs(a[2]) < 0.9 ? new double[] { 0, 0, 1 } : new double[] { 1, 0, 0 };
            var u = Norm(Cross(refv, a));
            var v = Cross(a, u);   // u x v = a
            var origin = Point.Create(
                GeometryUtils.MmToMeters(site.AxisPointMm[0]),
                GeometryUtils.MmToMeters(site.AxisPointMm[1]),
                GeometryUtils.MmToMeters(site.AxisPointMm[2]));
            var frame = Frame.Create(origin,
                Direction.Create(u[0], u[1], u[2]),
                Direction.Create(v[0], v[1], v[2]));
            return Matrix.CreateMapping(frame);
        }

        // ---- tiny vector helpers (mm-space doubles) --------------------------
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
            return m < 1e-12 ? new double[] { 1, 0, 0 } : new[] { x[0] / m, x[1] / m, x[2] / m };
        }

        private static IEnumerable<double[]> BoxCorners(Box b)
        {
            var lo = b.MinCorner; var hi = b.MaxCorner;
            double[] xs = { lo.X * 1000, hi.X * 1000 };
            double[] ys = { lo.Y * 1000, hi.Y * 1000 };
            double[] zs = { lo.Z * 1000, hi.Z * 1000 };
            foreach (var x in xs) foreach (var y in ys) foreach (var z in zs)
                yield return new[] { x, y, z };
        }
    }
}
