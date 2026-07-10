using System;
using System.Collections.Generic;
using System.Globalization;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Pcb;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Pcb
{
    /// <summary>
    /// Parametric PCB assembly (#3 of the mobile-structure brainstorm): arbitrary polygon
    /// BOARD (non-convex supported; profile auto-oriented CCW) minus a HOLE map and
    /// polygon CUTOUTS, BLOCK / BGA components seated on the board top (BGA = package
    /// block + per-ball cylinder grid spanning the standoff — one DesignBody per ball,
    /// the package-pipeline precedent, so CAE can assign solder material), optional
    /// STIFFENER plate. All Booleans on raw bodies, chunked subtracts, DesignBody at the
    /// end with rollback. The board area is the SHOELACE of the outline minus holes and
    /// cutouts — the gate cross-checks the kernel against it.
    /// </summary>
    public class PcbAssemblyService
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int SubtractChunk = 100;

        public PcbAssemblyResult Create(Part part, PcbAssemblySpec spec)
        {
            var res = new PcbAssemblyResult();
            if (part == null) { res.Error = "part is null"; return res; }
            try { spec.Validate(); }
            catch (Exception ex) { res.Error = ex.Message; return res; }

            double t = spec.ThicknessMm;
            double areaOut = Math.Abs(PcbAssemblySpec.ShoelaceArea(spec.OutlineMm));
            double areaNet = areaOut;
            res.DimsMm["outline_area_mm2"] = areaOut;

            var pending = new List<KeyValuePair<string, Body>>();
            try
            {
                // ---- board: polygon extrude minus holes and cutouts ------------
                Body board = ExtrudePolygon(spec.OutlineMm, 0, t);
                var cutters = new List<Body>();
                foreach (var h in spec.Holes)
                {
                    Body c = BodyBuilder.CreateCylinder(
                        GeometryUtils.MmToMeters(h.DiaMm / 2), GeometryUtils.MmToMeters(t + 2));
                    c.Transform(Matrix.CreateTranslation(Vector.Create(
                        GeometryUtils.MmToMeters(h.XMm), GeometryUtils.MmToMeters(h.YMm),
                        GeometryUtils.MmToMeters(-1))));
                    cutters.Add(c);
                    areaNet -= Math.PI * h.DiaMm * h.DiaMm / 4;
                }
                foreach (var cp in spec.CutoutsMm)
                {
                    cutters.Add(ExtrudePolygon(cp, -1, t + 2));
                    areaNet -= Math.Abs(PcbAssemblySpec.ShoelaceArea(cp));
                }
                for (int i = 0; i < cutters.Count; i += SubtractChunk)
                    board.Subtract(cutters.GetRange(i, Math.Min(SubtractChunk, cutters.Count - i)));
                pending.Add(new KeyValuePair<string, Body>(spec.NamePrefix + "_Board", board));
                res.DimsMm["board_area_mm2"] = areaNet;
                res.DimsMm["board_v_mm3"] = areaNet * t;

                // ---- components -------------------------------------------------
                foreach (var comp in spec.Components)
                {
                    double zBot = t + comp.StandoffMm;
                    Body pkg = BodyBuilder.CreateBlock(
                        GeometryUtils.MmToMeters(comp.WMm), GeometryUtils.MmToMeters(comp.LMm),
                        GeometryUtils.MmToMeters(comp.HMm));
                    if (Math.Abs(comp.RotDeg) > 1e-9)
                        pkg.Transform(Matrix.CreateRotation(
                            Line.Create(Point.Create(0, 0, 0), Direction.DirZ),
                            comp.RotDeg * Math.PI / 180));
                    pkg.Transform(Matrix.CreateTranslation(Vector.Create(
                        GeometryUtils.MmToMeters(comp.XMm), GeometryUtils.MmToMeters(comp.YMm),
                        GeometryUtils.MmToMeters(zBot))));
                    pending.Add(new KeyValuePair<string, Body>("Comp_" + comp.Ref, pkg));

                    if (comp.Type == PcbComponentType.Bga)
                    {
                        // per-ball bodies (package-pipeline precedent): prototype + copies
                        Body proto = BodyBuilder.CreateCylinder(
                            GeometryUtils.MmToMeters(comp.BallDiaMm / 2),
                            GeometryUtils.MmToMeters(comp.StandoffMm));
                        double rot = comp.RotDeg * Math.PI / 180;
                        double ox = -(comp.BallsNx - 1) * comp.BallPitchMm / 2;
                        double oy = -(comp.BallsNy - 1) * comp.BallPitchMm / 2;
                        int idx = 0;
                        for (int iy = 0; iy < comp.BallsNy; iy++)
                        {
                            for (int ix = 0; ix < comp.BallsNx; ix++)
                            {
                                double lx = ox + ix * comp.BallPitchMm;
                                double ly = oy + iy * comp.BallPitchMm;
                                double wx = comp.XMm + lx * Math.Cos(rot) - ly * Math.Sin(rot);
                                double wy = comp.YMm + lx * Math.Sin(rot) + ly * Math.Cos(rot);
                                Body ball = proto.Copy();
                                ball.Transform(Matrix.CreateTranslation(Vector.Create(
                                    GeometryUtils.MmToMeters(wx), GeometryUtils.MmToMeters(wy),
                                    GeometryUtils.MmToMeters(t))));
                                idx++;
                                pending.Add(new KeyValuePair<string, Body>(
                                    string.Format(Inv, "Comp_{0}_Ball_{1:0000}", comp.Ref, idx), ball));
                            }
                        }
                        res.DimsMm["balls_" + comp.Ref] = idx;
                        res.Log.Add(string.Format(Inv, "{0}: {1}x{2} = {3} balls (d {4:0.###}, h {5:0.###})",
                            comp.Ref, comp.BallsNx, comp.BallsNy, idx, comp.BallDiaMm, comp.StandoffMm));
                    }
                }

                // ---- stiffener ---------------------------------------------------
                if (spec.Stiffener != null)
                {
                    double z0 = spec.Stiffener.Side == StiffenerSide.Bottom
                        ? -spec.Stiffener.ThicknessMm : t;
                    Body st = ExtrudePolygon(spec.Stiffener.OutlineMm, z0, spec.Stiffener.ThicknessMm);
                    pending.Add(new KeyValuePair<string, Body>(spec.NamePrefix + "_Stiffener", st));
                }
            }
            catch (Exception ex)
            {
                res.Error = "pcb geometry failed: " + ex.Message;
                return res;
            }

            // ---- materialize with rollback --------------------------------------
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
            res.Success = true;
            return res;
        }

        /// <summary>Closed polygon extruded from z0 upward by height. The point chain is
        /// auto-oriented CCW (positive shoelace) so extrusion direction never flips with
        /// the caller's winding.</summary>
        internal static Body ExtrudePolygon(double[][] polyMm, double z0Mm, double heightMm)
        {
            var pts = polyMm;
            if (PcbAssemblySpec.ShoelaceArea(pts) < 0)
            {
                var rev = new double[pts.Length][];
                for (int i = 0; i < pts.Length; i++) rev[i] = pts[pts.Length - 1 - i];
                pts = rev;
            }
            var pb = new ProfileBuilder(Plane.PlaneXY);
            for (int i = 0; i < pts.Length; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Length];
                if (Math.Abs(a[0] - b[0]) < 1e-12 && Math.Abs(a[1] - b[1]) < 1e-12) continue;
                pb.AddLine(
                    Point.Create(GeometryUtils.MmToMeters(a[0]), GeometryUtils.MmToMeters(a[1]), 0),
                    Point.Create(GeometryUtils.MmToMeters(b[0]), GeometryUtils.MmToMeters(b[1]), 0));
            }
            Body body = Body.ExtrudeProfile(pb.Build(), GeometryUtils.MmToMeters(heightMm));
            if (Math.Abs(z0Mm) > 1e-12)
                body.Transform(Matrix.CreateTranslation(
                    Vector.Create(0, 0, GeometryUtils.MmToMeters(z0Mm))));
            return body;
        }
    }
}
