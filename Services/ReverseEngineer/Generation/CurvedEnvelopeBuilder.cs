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
    /// v2 (FROM_SCRATCH_ROADMAP.md): add a gently CURVED back to the flat slab so the phone has a
    /// real non-planar form (a cylindrical-section back that bulges along the short/width axis).
    ///
    /// PRIMARY path = a faceted Z-STACK: Unite N thin extruded rounded-rect slabs of decreasing
    /// width that approximate the convex arc - ALL-SUPPORTED API (RectangleProfile/ExtrudeProfile/
    /// Unite, the proven stacked-disc-dome idiom, TestModelGenerator BoxWithRevolvedDome). No
    /// Unsupported.* call, no inward-normal Reverse, no degenerate-profile kernel reject - the
    /// adversarial-verified safe vehicle. (A horizontal-axis RevolveTrimmedCurves was considered
    /// but spec-63 only proved it for a VERTICAL axis + cone profile, so revolve is deferred.)
    ///
    /// No-op (Success, Path="flat") when BackBulgeMm<=0 => the slab is untouched and v1 output
    /// stays byte-identical. Caller is on the SC API thread inside a WriteBlock.
    /// </summary>
    public static class CurvedEnvelopeBuilder
    {
        public class CurveResult
        {
            public bool Success;
            public string Error;
            public string Path = "flat";   // "flat" | "zstackB"
            public double AddedVolMm3;
            public int Slabs;
        }

        /// <summary>
        /// S00a: UNITE a convex cylindrical-section back ridge onto the slab top (z = T), bulging
        /// up to BackBulgeMm at the crown (y=0) and tapering to 0 at the flanks (|y|=W/2). Built as
        /// a Z-stack of decreasing-width slabs following the arc z(y) = bulge*(1 - (y/(W/2))^2).
        /// </summary>
        public static CurveResult AddCurvedBack(DesignBody body, PhoneParameters p)
        {
            var r = new CurveResult();
            double bulge = p.BackBulgeMm;
            if (bulge <= 0) { r.Success = true; r.Path = "flat"; return r; }

            try
            {
                double L = p.LengthMm, W = p.WidthMm, T = p.ThicknessMm;
                Part part = body.Parent as Part;
                double vBefore = VolMm3(body);

                // N slabs stacked from z=T upward to z=T+bulge. Slab i covers z in [T+i*h, T+(i+1)*h];
                // its half-width yi is the arc half-width at the MID height so the union envelope
                // follows the convex arc. arc: z(y)=bulge*(1-(y/(W/2))^2) => y(z)=(W/2)*sqrt(1-z/bulge).
                const int N = 40;
                double hSlab = bulge / N;
                double halfW = W / 2.0;
                int built = 0;

                for (int i = 0; i < N; i++)
                {
                    double zMid = (i + 0.5) * hSlab;            // height above T at slab mid (mm)
                    double frac = 1.0 - zMid / bulge;
                    if (frac <= 0) continue;
                    double yi = halfW * Math.Sqrt(frac);        // arc half-width at this height
                    if (yi <= 0.05) continue;                   // skip a sub-0.1mm sliver

                    const double overlap = 0.02;                // 0.02mm overlap so slabs Unite cleanly
                    double baseZ = T + i * hSlab - overlap;
                    double slabH = hSlab + overlap;

                    var plane = MakeXYPlaneAtZ(GeometryUtils.MmToMeters(baseZ));
                    var prof = new RectangleProfile(plane,
                        GeometryUtils.MmToMeters(L), GeometryUtils.MmToMeters(2.0 * yi),
                        PointUV.Create(0, 0), 0.0);
                    Body slab = Body.ExtrudeProfile(prof, GeometryUtils.MmToMeters(slabH));
                    try
                    {
                        if (part != null)
                        {
                            DesignBody sdb = DesignBody.Create(part, "_back_slab", slab);
                            body.Shape.Unite(new[] { sdb.Shape });
                            try { sdb.Delete(); } catch { }
                        }
                        else body.Shape.Unite(new[] { slab });
                        built++;
                    }
                    catch { /* a single slab union failure is non-fatal; arc just slightly coarser */ }
                }

                r.Slabs = built;
                r.AddedVolMm3 = VolMm3(body) - vBefore;
                r.Path = "zstackB";
                r.Success = built > 0;
                if (!r.Success) r.Error = "no back slab united";
                return r;
            }
            catch (Exception ex) { r.Error = "AddCurvedBack failed: " + ex.Message; return r; }
        }

        private static Plane MakeXYPlaneAtZ(double zM)
        {
            var frame = Frame.Create(Point.Create(0, 0, zM), Direction.DirX, Direction.DirY);
            return Plane.Create(frame);
        }

        private static double VolMm3(DesignBody b)
        {
            try { return b.Shape.Volume * 1e9; } catch { return 0; }
        }
    }
}
