using System;
using System.Collections.Generic;
using System.Globalization;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Odb;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Pcb;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Odb
{
    public class OdbImportOptions
    {
        public double BoardThicknessMm = 1.0;   // ODB++ carries no reliable stack thickness
        public bool IncludePads = true;
        public double PadThicknessMm = 0.05;
        /// <summary>0 = 55% of the package pitch (falls back to 0.4mm when pitch unknown).</summary>
        public double PadDiaMm = 0;
        /// <summary>Used when a CMP has no COMP_HEIGHT property.</summary>
        public double DefaultCompHeightMm = 1.0;
        /// <summary>Skip components whose package bbox is smaller than this (declutter
        /// 0201 passives etc.). 0 = keep everything.</summary>
        public double MinFootprintMm = 0;
        public int MaxComponents = 500;
        public int MaxTotalPads = 5000;
        public string NamePrefix = "Pcb";
    }

    public class OdbImportResult
    {
        public bool Success;
        public string Error;
        public List<string> BodiesCreated = new List<string>();
        public Dictionary<string, double> DimsMm = new Dictionary<string, double>();
        public List<string> Log = new List<string>();
        public int ComponentsBuilt, ComponentsSkipped, PadsBuilt;
        /// <summary>The board DesignBody created by THIS call (never re-resolved by
        /// name — duplicate names in the document must not hijack the binding).</summary>
        public DesignBody BoardBody;
    }

    /// <summary>
    /// Build the CAD stack from a parsed ODB++ design: board (profile island minus
    /// contained profile holes), each CMP as its PACKAGE OUTLINE polygon (minus package
    /// hole contours) extruded at the component height (COMP_HEIGHT when present),
    /// seated on pads at the PIN sites — pin-less packages sit flush on the board.
    /// Placement transform: rotate CLOCKWISE by the CMP angle FIRST, then X-mirror for
    /// bottom-side (M) placements, then translate — the ODB++ convention where the
    /// rotation is given in the component's own view. Reported board area/volume are
    /// KERNEL truth (post-Boolean), not shoelace bookkeeping. Counts are guarded
    /// loudly, skips are REPORTED, never silent.
    /// </summary>
    public class OdbImportService
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public OdbImportResult Build(Part part, OdbDesign design, OdbImportOptions opt)
        {
            var res = new OdbImportResult();
            if (part == null || design == null || design.Step == null)
            { res.Error = "part/design is null"; return res; }
            if (opt == null) opt = new OdbImportOptions();
            if (opt.BoardThicknessMm <= 0) { res.Error = "board_thickness_mm must be > 0"; return res; }
            string boardName = opt.NamePrefix + "_Board";
            foreach (var existing in part.Bodies)
                if (string.Equals(existing.Name, boardName, StringComparison.OrdinalIgnoreCase))
                {
                    res.Error = "body '" + boardName + "' already exists in the part - " +
                        "pass a different name_prefix or start a new document (re-importing " +
                        "over a previous import would duplicate the whole stack)";
                    return res;
                }
            var s = design.Step;
            double t = opt.BoardThicknessMm;

            var pending = new List<KeyValuePair<string, Body>>();
            try
            {
                // ---- board ------------------------------------------------------
                var outline = s.OutlineMm.ToArray();
                PcbAssemblySpec.ValidatePolygon(outline, "profile outline");
                Body board = Pcb.PcbAssemblyService.ExtrudePolygon(outline, 0, t);
                double areaNet = Math.Abs(PcbAssemblySpec.ShoelaceArea(outline));
                foreach (var hole in s.CutoutsMm)
                {
                    var hp = hole.ToArray();
                    // a hole belonging to another profile island (multi-island panels)
                    // would be a disjoint Boolean tool - skip it loudly
                    int insideCount = 0;
                    foreach (var v in hp)
                        if (PcbAssemblySpec.PointInPolygon(outline, v[0], v[1])) insideCount++;
                    if (insideCount == 0)
                    {
                        res.Log.Add(string.Format(Inv,
                            "cutout near ({0:0.##}, {1:0.##}) lies outside the board island - " +
                            "skipped (likely belongs to another profile island)",
                            hp[0][0], hp[0][1]));
                        continue;
                    }
                    PcbAssemblySpec.ValidatePolygon(hp, "profile hole");
                    // one subtract per cutter: a failing cutter must not poison the rest
                    board.Subtract(new List<Body>
                        { Pcb.PcbAssemblyService.ExtrudePolygon(hp, -1, t + 2) });
                    areaNet -= Math.Abs(PcbAssemblySpec.ShoelaceArea(hp));
                }
                pending.Add(new KeyValuePair<string, Body>(boardName, board));
                // KERNEL truth, not shoelace bookkeeping: edge-straddling cutouts make
                // the arithmetic diverge from the real solid
                double vKernel = board.Volume * 1e9;
                res.DimsMm["board_v_mm3"] = vKernel;
                res.DimsMm["board_area_mm2"] = vKernel / t;
                if (Math.Abs(vKernel - areaNet * t) > Math.Max(1.0, Math.Abs(areaNet * t)) * 0.005)
                    res.Log.Add(string.Format(Inv,
                        "cutouts cross the board edge: kernel volume {0:0.##} mm3 differs " +
                        "from the shoelace estimate {1:0.##} mm3", vKernel, areaNet * t));
                if (board.PieceCount > 1)
                    res.Log.Add("WARNING: cutouts sever the board into "
                        + board.PieceCount.ToString(Inv) + " pieces");

                // ---- components + pads -------------------------------------------
                int totalPads = 0;
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var comp in s.Components)
                {
                    var pkg = s.Packages[comp.PkgIndex];
                    double fw = pkg.XMaxMm - pkg.XMinMm, fl = pkg.YMaxMm - pkg.YMinMm;
                    if (fw <= 0 || fl <= 0)
                    {
                        // declared bbox absent/degenerate - measure the outline instead
                        double x0 = double.MaxValue, x1 = double.MinValue;
                        double y0 = double.MaxValue, y1 = double.MinValue;
                        foreach (var p in pkg.OutlineMm)
                        {
                            x0 = Math.Min(x0, p[0]); x1 = Math.Max(x1, p[0]);
                            y0 = Math.Min(y0, p[1]); y1 = Math.Max(y1, p[1]);
                        }
                        fw = x1 - x0; fl = y1 - y0;
                    }
                    if (opt.MinFootprintMm > 0 && Math.Max(fw, fl) < opt.MinFootprintMm)
                    {
                        res.ComponentsSkipped++;
                        continue;
                    }
                    if (res.ComponentsBuilt >= opt.MaxComponents)
                    {
                        res.Error = string.Format(Inv,
                            "component count exceeds max_components={0} - raise the limit " +
                            "or filter with min_footprint_mm", opt.MaxComponents);
                        return res;
                    }
                    double h = comp.HeightMm > 0 ? comp.HeightMm : opt.DefaultCompHeightMm;
                    // pin-less packages (shields, mechanical parts) sit flush on the
                    // board - only components with real pads get the pad standoff
                    bool seatOnPads = opt.IncludePads && pkg.Pins.Count > 0;
                    double zBot = comp.Mirrored ? -(seatOnPads ? opt.PadThicknessMm : 0) - h
                                                : t + (seatOnPads ? opt.PadThicknessMm : 0);

                    // package outline -> world polygon (rotate CW, then mirror X, then move)
                    var world = new double[pkg.OutlineMm.Count][];
                    for (int i = 0; i < pkg.OutlineMm.Count; i++)
                        world[i] = PlaceXy(pkg.OutlineMm[i][0], pkg.OutlineMm[i][1], comp);
                    try
                    {
                        PcbAssemblySpec.ValidatePolygon(world,
                            "package '" + pkg.Name + "' outline");
                    }
                    catch (ArgumentException ex)
                    {
                        // one broken footprint must not abort the whole board
                        res.ComponentsSkipped++;
                        res.Log.Add(comp.RefDes + " SKIPPED: " + ex.Message);
                        continue;
                    }
                    Body body = Pcb.PcbAssemblyService.ExtrudePolygon(world, zBot, h);
                    foreach (var holeLocal in pkg.HolesMm)
                    {
                        // ring/frame packages: subtract the hole contours
                        var hw = new double[holeLocal.Count][];
                        for (int i = 0; i < holeLocal.Count; i++)
                            hw[i] = PlaceXy(holeLocal[i][0], holeLocal[i][1], comp);
                        try
                        {
                            PcbAssemblySpec.ValidatePolygon(hw,
                                "package '" + pkg.Name + "' hole");
                        }
                        catch (ArgumentException ex)
                        {
                            res.Log.Add(comp.RefDes + ": package hole skipped - " + ex.Message);
                            continue;
                        }
                        body.Subtract(new List<Body>
                            { Pcb.PcbAssemblyService.ExtrudePolygon(hw, zBot - 1, h + 2) });
                    }
                    string cname = "Comp_" + comp.RefDes;
                    int dup = 2;
                    while (!usedNames.Add(cname))
                        cname = "Comp_" + comp.RefDes + "_" + (dup++).ToString(Inv);
                    if (dup > 2)
                        res.Log.Add("duplicate RefDes '" + comp.RefDes + "' - body named " + cname);
                    pending.Add(new KeyValuePair<string, Body>(cname, body));
                    res.ComponentsBuilt++;

                    if (seatOnPads)
                    {
                        totalPads += pkg.Pins.Count;
                        if (totalPads > opt.MaxTotalPads)
                        {
                            res.Error = string.Format(Inv,
                                "total pad count exceeds max_total_pads={0} - set " +
                                "include_pads=false or filter components", opt.MaxTotalPads);
                            return res;
                        }
                        double padDia = opt.PadDiaMm > 0 ? opt.PadDiaMm
                            : (pkg.PitchMm > 0 ? 0.55 * pkg.PitchMm : 0.4);
                        double padZ = comp.Mirrored ? -opt.PadThicknessMm : t;
                        Body proto = BodyBuilder.CreateCylinder(
                            GeometryUtils.MmToMeters(padDia / 2),
                            GeometryUtils.MmToMeters(opt.PadThicknessMm));
                        int pi = 0;
                        foreach (var pin in pkg.Pins)
                        {
                            var w = PlaceXy(pin.XMm, pin.YMm, comp);
                            Body pad = proto.Copy();
                            pad.Transform(Matrix.CreateTranslation(Vector.Create(
                                GeometryUtils.MmToMeters(w[0]), GeometryUtils.MmToMeters(w[1]),
                                GeometryUtils.MmToMeters(padZ))));
                            pi++;
                            pending.Add(new KeyValuePair<string, Body>(
                                string.Format(Inv, "{0}_Pad_{1:000}", cname, pi), pad));
                            res.PadsBuilt++;
                        }
                    }
                    res.Log.Add(string.Format(Inv, "{0}: pkg={1} h={2:0.##} rot={3:0.#}{4} pads={5}",
                        comp.RefDes, pkg.Name, h, comp.RotDeg,
                        comp.Mirrored ? " (bottom)" : "", seatOnPads ? pkg.Pins.Count : 0));
                }
            }
            catch (Exception ex)
            {
                res.Error = "odb geometry failed: " + ex.Message;
                return res;
            }

            var created = new List<DesignBody>();
            try
            {
                foreach (var kv in pending)
                    created.Add(BodyBuilder.CreateDesignBody(part, kv.Key, kv.Value));
            }
            catch (Exception ex)
            {
                foreach (var db in created) { try { db.Delete(); } catch { } }
                res.Error = "kernel failure (rolled back): " + ex.Message;
                return res;
            }
            foreach (var db in created) res.BodiesCreated.Add(db.Name);
            res.BoardBody = created.Count > 0 ? created[0] : null;   // pending[0] is the board
            res.DimsMm["components"] = res.ComponentsBuilt;
            res.DimsMm["components_skipped"] = res.ComponentsSkipped;
            res.DimsMm["pads"] = res.PadsBuilt;
            res.Success = true;
            return res;
        }

        /// <summary>Package-local (x, y) -> board coordinates. ODB++ order: rotation
        /// CLOCKWISE by the CMP angle in the component's own view FIRST, then the
        /// X flip for bottom-side (M) placements, then translation to the CMP
        /// position. (Mirror-first would silently flip the rotation sense for every
        /// rotated bottom-side part.)</summary>
        internal static double[] PlaceXy(double x, double y, OdbComponent comp)
        {
            double a = -comp.RotDeg * Math.PI / 180;   // CW -> CCW sign flip
            double xr = x * Math.Cos(a) - y * Math.Sin(a);
            double yr = x * Math.Sin(a) + y * Math.Cos(a);
            if (comp.Mirrored) xr = -xr;
            return new[] { comp.XMm + xr, comp.YMm + yr };
        }
    }
}
