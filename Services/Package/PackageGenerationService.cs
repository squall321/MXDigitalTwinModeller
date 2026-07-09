using System;
using System.Collections.Generic;
using System.Globalization;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Package;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Package
{
    /// <summary>
    /// Builds the CAD stack for a parsed package description (PackageSpec):
    ///
    ///   * plain layer                -> one slab body (layer name)
    ///   * layer with a ball map      -> one body PER ball ({Layer}_Ball_0001, ...) plus, when
    ///                                   FillMatrix, the "resin" body {Layer}_Matrix =
    ///                                   slab MINUS all inclusions (underfill / molding compound)
    ///   * layer with Box inclusions  -> {Layer}_Die_1, ... + matrix as above
    ///
    /// Balls are built once per (shape, radius) as a PROTOTYPE body and instanced with
    /// Body.Copy()+Transform — with thousands of ball-map entries, re-extruding each ball is
    /// the difference between seconds and minutes. Barrel joints (convex reflowed solder)
    /// approximate the circular-arc profile with a stacked-disc unite, the same idiom the
    /// phone pipeline uses for its curved back (no revolve API in this codebase).
    ///
    /// All Booleans run on raw (unowned) Body objects; DesignBody.Create happens only at the
    /// end — this sidesteps the owned/unowned mixed-Boolean kernel landmine (RT4/RT5).
    /// </summary>
    public class PackageGenerationService
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int SubtractChunk = 200;

        public PackageGenResult BuildStack(Part part, PackageSpec spec, PackageGenOptions opt,
            out DesignBody boundBody)
        {
            boundBody = null;
            var res = new PackageGenResult();
            if (part == null) { res.Error = "part is null"; return res; }
            if (spec == null || spec.Layers.Count == 0) { res.Error = "spec has no layers"; return res; }
            if (opt == null) opt = new PackageGenOptions();

            bool barrel = string.Equals(opt.BallShape, "barrel", StringComparison.OrdinalIgnoreCase);
            if (barrel && opt.BarrelBulgeRatio <= 1.0)
            {
                res.Log.Add("barrel_bulge_ratio <= 1.0 — falling back to cylinder balls");
                barrel = false;
            }
            int slices = Math.Max(3, opt.BarrelSlices);

            DesignBody firstPlain = null, firstMatrix = null, firstAny = null;
            var zBases = spec.ComputeZBasesMm();

            // every DesignBody materialized so far — on a mid-build kernel failure the
            // partial stack is rolled back instead of committing half a package (a retry
            // would otherwise stack duplicate-named bodies on top).
            var createdBodies = new List<DesignBody>();
            try
            {
            for (int li = 0; li < spec.Layers.Count; li++)
            {
                var layer = spec.Layers[li];
                var info = new PackageLayerBuildInfo
                {
                    Name = layer.Name,
                    ZBaseMm = zBases[li],
                    ThicknessMm = layer.ThicknessMm,
                };
                res.Layers.Add(info);

                if (layer.LenXMm <= 0 || layer.LenYMm <= 0 || layer.ThicknessMm <= 0)
                {
                    info.Skipped = true;
                    res.Log.Add(layer.Name + ": invalid Length/Thickness — skipped");
                    continue;
                }
                if (opt.LayerFilter != null && opt.LayerFilter.Count > 0
                    && !opt.LayerFilter.Contains(layer.Name))
                {
                    info.Skipped = true;
                    res.Log.Add(layer.Name + ": filtered out (z cursor still advanced)");
                    continue;
                }

                double tM = GeometryUtils.MmToMeters(layer.ThicknessMm);
                double zM = GeometryUtils.MmToMeters(info.ZBaseMm);
                double cxM = GeometryUtils.MmToMeters(layer.LocXMm);
                double cyM = GeometryUtils.MmToMeters(layer.LocYMm);

                // layer slab at [zBase, zBase+t], centered on (LocX, LocY). Only extruded
                // when consumed: as the plain-layer body, or as the matrix Boolean target —
                // an inclusion layer with FillMatrix=false would orphan the kernel body.
                Body slab = null;
                if (!layer.HasInclusions || opt.FillMatrix)
                {
                    slab = BodyBuilder.CreateBlock(
                        GeometryUtils.MmToMeters(layer.LenXMm),
                        GeometryUtils.MmToMeters(layer.LenYMm), tM);
                    slab.Transform(Matrix.CreateTranslation(Vector.Create(cxM, cyM, zM)));
                }

                if (!layer.HasInclusions)
                {
                    var db = BodyBuilder.CreateDesignBody(part, layer.Name, slab);
                    createdBodies.Add(db);
                    info.PlainCreated = true;
                    res.TotalBodies++;
                    if (firstPlain == null) firstPlain = db;
                    if (firstAny == null) firstAny = db;
                    res.Log.Add(string.Format(Inv, "{0}: slab {1}x{2}x{3}mm @z={4:0.###}",
                        layer.Name, layer.LenXMm, layer.LenYMm, layer.ThicknessMm, info.ZBaseMm));
                    continue;
                }

                // ---- inclusion layer: balls / boxes + optional matrix -------------------
                var inclusionBodies = new List<Body>();

                // one prototype per distinct radius, instanced by Copy+translate
                var protos = new Dictionary<double, Body>();
                foreach (var ball in layer.Balls)
                {
                    Body proto;
                    if (!protos.TryGetValue(ball.RadiusMm, out proto))
                    {
                        proto = BuildBallPrototype(ball.RadiusMm, layer.ThicknessMm,
                            barrel, opt.BarrelBulgeRatio, slices);
                        protos[ball.RadiusMm] = proto;
                    }
                    Body b = proto.Copy();
                    b.Transform(Matrix.CreateTranslation(Vector.Create(
                        cxM + GeometryUtils.MmToMeters(ball.XMm),
                        cyM + GeometryUtils.MmToMeters(ball.YMm), zM)));
                    inclusionBodies.Add(b);
                }
                foreach (var box in layer.Boxes)
                {
                    Body b = BodyBuilder.CreateBlock(
                        GeometryUtils.MmToMeters(box.WidthMm),
                        GeometryUtils.MmToMeters(box.HeightMm), tM);
                    b.Transform(Matrix.CreateTranslation(Vector.Create(
                        cxM + GeometryUtils.MmToMeters(box.XMm),
                        cyM + GeometryUtils.MmToMeters(box.YMm), zM)));
                    inclusionBodies.Add(b);
                }

                // matrix = slab minus every inclusion (chunked: a single 2000-tool Subtract
                // is where the kernel becomes fragile)
                Body matrix = null;
                if (opt.FillMatrix)
                {
                    matrix = slab;
                    var cutters = new List<Body>(inclusionBodies.Count);
                    foreach (var b in inclusionBodies) cutters.Add(b.Copy());
                    for (int i = 0; i < cutters.Count; i += SubtractChunk)
                    {
                        int n = Math.Min(SubtractChunk, cutters.Count - i);
                        matrix.Subtract(cutters.GetRange(i, n));
                    }
                }

                int ballCount = layer.Balls.Count;
                for (int i = 0; i < inclusionBodies.Count; i++)
                {
                    string name = i < ballCount
                        ? string.Format(Inv, "{0}_Ball_{1:0000}", layer.Name, i + 1)
                        : string.Format(Inv, "{0}_Die_{1}", layer.Name, i - ballCount + 1);
                    var db = BodyBuilder.CreateDesignBody(part, name, inclusionBodies[i]);
                    createdBodies.Add(db);
                    res.TotalBodies++;
                    if (firstAny == null) firstAny = db;
                }
                info.BallBodies = ballCount;
                info.BoxBodies = layer.Boxes.Count;

                if (matrix != null)
                {
                    var db = BodyBuilder.CreateDesignBody(part, layer.Name + "_Matrix", matrix);
                    createdBodies.Add(db);
                    info.MatrixCreated = true;
                    res.TotalBodies++;
                    if (firstMatrix == null) firstMatrix = db;
                    if (firstAny == null) firstAny = db;
                }

                res.Log.Add(string.Format(Inv,
                    "{0}: {1} ball(s){2}{3} @z={4:0.###} (shape={5})",
                    layer.Name, ballCount,
                    layer.Boxes.Count > 0 ? " + " + layer.Boxes.Count.ToString(Inv) + " die(s)" : "",
                    matrix != null ? " + matrix" : "", info.ZBaseMm,
                    barrel ? "barrel" : "cylinder"));
            }
            }
            catch (Exception ex)
            {
                int n = createdBodies.Count;
                foreach (var db in createdBodies)
                {
                    try { db.Delete(); } catch { }
                }
                res.Success = false;
                res.TotalBodies = 0;
                res.Error = string.Format(Inv,
                    "kernel failure mid-build (rolled back {0} bodies): {1}", n, ex.Message);
                return res;
            }

            res.TotalThicknessMm = spec.GetTotalThicknessMm();
            // bind the cheapest body for the session: a plain slab extracts a trivial
            // FeatureGraph; a 1600-hole matrix would take minutes.
            boundBody = firstPlain ?? firstMatrix ?? firstAny;
            res.BoundBodyName = boundBody != null ? boundBody.Name : null;
            res.Success = res.TotalBodies > 0;
            if (!res.Success && res.Error == null) res.Error = "no bodies were created";
            return res;
        }

        /// <summary>Ball prototype at the origin, base at z=0, axis +Z, height t.
        /// Cylinder: straight extrude. Barrel: radius follows the circular arc through
        /// (z=0, r0), (z=t/2, r0*bulge), (z=t, r0), approximated by stacked discs.</summary>
        internal static Body BuildBallPrototype(double r0Mm, double tMm,
            bool barrel, double bulgeRatio, int slices)
        {
            if (!barrel)
                return BodyBuilder.CreateCylinder(
                    GeometryUtils.MmToMeters(r0Mm), GeometryUtils.MmToMeters(tMm));

            double r0 = r0Mm, rm = r0Mm * bulgeRatio, t = tMm;
            // circle through (±t/2, r0) and (0, rm) in (z-t/2, r) coordinates:
            // center on the r axis at c, radius R
            double c = (rm * rm - r0 * r0 - t * t / 4.0) / (2.0 * (rm - r0));
            double R = rm - c;

            // sequential unite with a small downward overlap per slice: a one-shot
            // Unite(collection) rejects tools that do not touch the TARGET body
            // ("modeler body is disjoint"), and pure face-contact unions are tolerance-fragile.
            double sliceT = t / slices;
            double eps = sliceT * 0.1;
            Body result = null;
            for (int i = 0; i < slices; i++)
            {
                double z0 = sliceT * i, z1 = z0 + sliceT;
                double zm = (z0 + z1) / 2.0 - t / 2.0;
                double ri = c + Math.Sqrt(Math.Max(R * R - zm * zm, 0.0));
                if (ri < r0 * 0.2) ri = r0 * 0.2;
                double zStart = i == 0 ? z0 : z0 - eps;
                Body slice = BodyBuilder.CreateCylinder(
                    GeometryUtils.MmToMeters(ri), GeometryUtils.MmToMeters(z1 - zStart));
                slice.Transform(Matrix.CreateTranslation(
                    Vector.Create(0, 0, GeometryUtils.MmToMeters(zStart))));
                if (result == null) result = slice;
                else result.Unite(new List<Body> { slice });
            }
            return result;
        }
    }
}
