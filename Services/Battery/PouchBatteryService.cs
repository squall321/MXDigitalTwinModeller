using System;
using System.Collections.Generic;
using System.Globalization;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Battery;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Battery
{
    /// <summary>
    /// Parametric pouch battery cell — the #2 pick of the mobile-structure brainstorm and
    /// the standard content of drop / bend / SWELLING models:
    ///
    ///   rounded-rect CORE + TERRACE seal shelf (+X end) + side SEAL FLANGES (flat or
    ///   folded pouch film) + two TABS (separate bodies: different material in CAE) +
    ///   optional SWELL DOMES on the large faces.
    ///
    /// Swell semantics are exact, not cosmetic: in percent mode the dome height H is
    /// solved numerically (monotone bisection on the closed-form ruled-loft volume
    /// integral) so that the added volume equals percent/100 x V_core — the gate then
    /// verifies the KERNEL agrees. Domes are rounded-rect LoftProfiles solids (cap+fuse
    /// via CadPrimitivesService.Loft) united with a 0.05mm sink below the face to avoid
    /// the coincident-face Boolean sliver.
    /// </summary>
    public class PouchBatteryService
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double OvMm = 0.02;     // unite junction overlap
        private const double SinkMm = 0.05;   // dome base sink below the face

        public PouchBatteryResult Create(Part part, PouchBatterySpec spec)
        {
            var res = new PouchBatteryResult();
            if (part == null) { res.Error = "part is null"; return res; }
            try { spec.ResolveAndValidate(); }
            catch (Exception ex) { res.Error = ex.Message; return res; }

            double L = spec.LengthMm, W = spec.WidthMm, T = spec.ThicknessMm, r = spec.CornerRMm;
            double l = L / 2, w = W / 2;
            double vCore = spec.CoreVolumeMm3();
            res.DimsMm["corner_r"] = r;
            res.DimsMm["core_v_mm3"] = vCore;

            // ---- swell dome sizing (exact percent semantics) ------------------
            double domeHTop = 0, domeHBot = 0, addedPerSide = 0;
            if (spec.Swell == SwellMode.Dome)
            {
                double baseWd = L - 2 * spec.SwellInsetMm, baseHt = W - 2 * spec.SwellInsetMm;
                double k = spec.SwellTopScale;
                if (spec.SwellHeightMm > 0)
                {
                    domeHTop = spec.SwellHeightMm + SinkMm;
                    domeHBot = spec.SwellBothSides ? domeHTop : 0;
                    addedPerSide = AddedAbove(baseWd, baseHt, k, domeHTop);
                }
                else
                {
                    double dV = spec.SwellPercent / 100.0 * vCore;
                    addedPerSide = spec.SwellBothSides ? dV / 2 : dV;
                    domeHTop = SolveDomeHeight(baseWd, baseHt, k, addedPerSide);
                    domeHBot = spec.SwellBothSides ? domeHTop : 0;
                }
                if (domeHTop - SinkMm < 0.05)
                {
                    res.Error = string.Format(Inv,
                        "swell resolves to a {0:0.###}mm dome - below the 0.05mm minimum; " +
                        "increase percent/height or reduce inset", domeHTop - SinkMm);
                    return res;
                }
                res.DimsMm["dome_h_mm"] = domeHTop - SinkMm;
                res.DimsMm["dome_added_mm3_per_side"] = addedPerSide;
            }

            // stack pitch: adjacent domes must not interpenetrate
            double pitchZ = T + spec.GapMm;
            if (spec.Count > 1 && spec.Swell == SwellMode.Dome)
            {
                double need = (domeHTop - SinkMm) + (domeHBot > 0 ? domeHBot - SinkMm : 0) + 0.1;
                if (spec.GapMm < need)
                {
                    res.Error = string.Format(Inv,
                        "gap_mm {0:0.##} too small for the swollen stack - domes need >= {1:0.##}mm",
                        spec.GapMm, need);
                    return res;
                }
            }
            res.DimsMm["stack_pitch_mm"] = pitchZ;

            // ---- build cell 1 as raw bodies -----------------------------------
            Body cell;
            var tabs = new List<Body>();
            try
            {
                cell = ExtrudeRoundedRect(L, W, r, T);

                if (spec.TerraceLengthMm > 0)
                {
                    Body terr = BlockAt(spec.TerraceLengthMm + OvMm, W, spec.TerraceThicknessMm,
                        l + spec.TerraceLengthMm / 2 - OvMm / 2, 0, T / 2);
                    cell.Unite(new List<Body> { terr });
                }

                if (spec.Flange != FlangeFold.None)
                {
                    // strip spans y in [w - Ov, w + protrude]: Ov overlap into the core,
                    // exactly `protrude` sticking out (flat: flange width; folded: film t)
                    double flProtrude = spec.Flange == FlangeFold.Flat
                        ? spec.FlangeWidthMm : spec.FlangeThicknessMm;
                    double flHeight = spec.Flange == FlangeFold.Flat
                        ? spec.FlangeThicknessMm : spec.FlangeWidthMm;
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        Body fl = BlockAt(L, flProtrude + OvMm, flHeight,
                            0, sgn * (w + flProtrude / 2 - OvMm / 2), T / 2);
                        cell.Unite(new List<Body> { fl });
                    }
                }

                if (spec.Swell == SwellMode.Dome)
                {
                    cell.Unite(new List<Body> { Dome(spec, T - SinkMm, domeHTop, +1) });
                    if (domeHBot > 0)
                        cell.Unite(new List<Body> { Dome(spec, SinkMm, domeHBot, -1) });
                }

                double tabX = l + spec.TerraceLengthMm + spec.TabLengthMm / 2;
                for (int sgn = -1; sgn <= 1; sgn += 2)
                    tabs.Add(BlockAt(spec.TabLengthMm, spec.TabWidthMm, spec.TabThicknessMm,
                        tabX, spec.TabOffsetMm + sgn * spec.TabPitchMm / 2, T / 2));
            }
            catch (Exception ex)
            {
                res.Error = "cell geometry failed: " + ex.Message;
                return res;
            }

            // ---- materialize the stack with rollback ---------------------------
            var created = new List<DesignBody>();
            try
            {
                for (int i = 1; i <= spec.Count; i++)
                {
                    double dz = (i - 1) * pitchZ;
                    var tr = Matrix.CreateTranslation(Vector.Create(0, 0, GeometryUtils.MmToMeters(dz)));
                    Body c = i == 1 ? cell : cell.Copy();
                    if (i > 1) c.Transform(tr);
                    created.Add(BodyBuilder.CreateDesignBody(part,
                        spec.NamePrefix + "_Cell_" + i.ToString(Inv), c));
                    for (int t2 = 0; t2 < 2; t2++)
                    {
                        Body tb = i == 1 ? tabs[t2] : tabs[t2].Copy();
                        if (i > 1) tb.Transform(tr);
                        created.Add(BodyBuilder.CreateDesignBody(part,
                            spec.NamePrefix + (t2 == 0 ? "_TabNeg_" : "_TabPos_") + i.ToString(Inv), tb));
                    }
                }
            }
            catch (Exception ex)
            {
                foreach (var db in created) { try { db.Delete(); } catch { } }
                res.Error = "kernel failure (rolled back): " + ex.Message;
                return res;
            }
            foreach (var db in created) res.BodiesCreated.Add(db.Name);
            res.Success = true;
            return res;
        }

        // ------------------------------------------------------------------
        // dome volume math. The dome is a rect pad extruded to height H and drafted by
        // TaperFaces (loft solids proved boolean-hostile: probe showed Unite(core, loft)
        // returning dome-minus-core). A uniform taper insets every edge by
        // e(s) = E * s / H, E = min(Wb, Hb) * (1 - topScale) / 2, so
        //   A(s) = (Wb - 2e)(Hb - 2e)  ->  closed-form quadratic integral.
        // AddedAbove = volume ABOVE the face plane (s from SinkMm to H).
        // ------------------------------------------------------------------
        internal static double EdgeInset(double baseWd, double baseHt, double k)
        {
            return Math.Min(baseWd, baseHt) * (1 - k) / 2;
        }

        internal static double AddedAbove(double baseWd, double baseHt, double k, double h)
        {
            double e = EdgeInset(baseWd, baseHt, k);
            double u0 = SinkMm / h;
            return h * (baseWd * baseHt * (1 - u0)
                        - e * (baseWd + baseHt) * (1 - u0 * u0)
                        + 4.0 / 3 * e * e * (1 - u0 * u0 * u0));
        }

        /// <summary>Monotone bisection for the dome height that adds exactly dV above
        /// the face — AddedAbove is strictly increasing in H, so this always converges,
        /// and the gate then verifies the KERNEL volume matches.</summary>
        internal static double SolveDomeHeight(double baseWd, double baseHt, double k, double dV)
        {
            double lo = SinkMm + 1e-4, hi = 1.0;
            while (AddedAbove(baseWd, baseHt, k, hi) < dV && hi < 1e4) hi *= 2;
            for (int i = 0; i < 80; i++)
            {
                double mid = (lo + hi) / 2;
                if (AddedAbove(baseWd, baseHt, k, mid) < dV) lo = mid; else hi = mid;
            }
            return (lo + hi) / 2;
        }

        /// <summary>Swell dome: rect pad extruded from zBase along dir, side walls drafted
        /// so the top insets by E on every edge — extrude + TaperFaces only, both verified
        /// boolean-safe (unlike loft solids).</summary>
        private static Body Dome(PouchBatterySpec spec, double zBaseMm, double hMm, int dir)
        {
            double baseWd = spec.LengthMm - 2 * spec.SwellInsetMm;
            double baseHt = spec.WidthMm - 2 * spec.SwellInsetMm;
            double e = EdgeInset(baseWd, baseHt, spec.SwellTopScale);
            Body pad = BlockAt(baseWd, baseHt, hMm, 0, 0, zBaseMm + dir * hMm / 2);
            double angleDeg = Math.Atan2(e, hMm) * 180 / Math.PI;
            int skipped;
            // neutral plane at the pad BASE (the face-side end); positive angle shrinks
            // moving away from neutral along the pull direction (g23-verified sense)
            int drafted = new CadOps.CadPrimitivesService().DraftSideFaces(pad,
                new[] { 0.0, 0.0, zBaseMm }, new[] { 0.0, 0.0, (double)dir },
                angleDeg, out skipped);
            if (drafted != 4)
                throw new InvalidOperationException(
                    "dome draft tapered " + drafted.ToString(Inv) + " faces (expected 4)");
            return pad;
        }

        private static Body ExtrudeRoundedRect(double lMm, double wMm, double rMm, double tMm)
        {
            double l = lMm / 2, w = wMm / 2, r = rMm;
            var pb = new ProfileBuilder(Plane.PlaneXY);
            Func<double, double, Point> p = (x, y) => Point.Create(
                GeometryUtils.MmToMeters(x), GeometryUtils.MmToMeters(y), 0);
            pb.AddLine(p(l, -(w - r)), p(l, w - r));
            pb.AddArc(p(l - r, w - r), GeometryUtils.MmToMeters(r), 0, Math.PI / 2);
            pb.AddLine(p(l - r, w), p(-(l - r), w));
            pb.AddArc(p(-(l - r), w - r), GeometryUtils.MmToMeters(r), Math.PI / 2, Math.PI);
            pb.AddLine(p(-l, w - r), p(-l, -(w - r)));
            pb.AddArc(p(-(l - r), -(w - r)), GeometryUtils.MmToMeters(r), Math.PI, 3 * Math.PI / 2);
            pb.AddLine(p(-(l - r), -w), p(l - r, -w));
            pb.AddArc(p(l - r, -(w - r)), GeometryUtils.MmToMeters(r), 3 * Math.PI / 2, 2 * Math.PI);
            return Body.ExtrudeProfile(pb.Build(), GeometryUtils.MmToMeters(tMm));
        }

        private static Body BlockAt(double lxMm, double lyMm, double lzMm,
            double cxMm, double cyMm, double czMm)
        {
            Body b = BodyBuilder.CreateBlock(GeometryUtils.MmToMeters(lxMm),
                GeometryUtils.MmToMeters(lyMm), GeometryUtils.MmToMeters(lzMm));
            b.Transform(Matrix.CreateTranslation(Vector.Create(
                GeometryUtils.MmToMeters(cxMm), GeometryUtils.MmToMeters(cyMm),
                GeometryUtils.MmToMeters(czMm - lzMm / 2))));
            return b;
        }
    }
}
