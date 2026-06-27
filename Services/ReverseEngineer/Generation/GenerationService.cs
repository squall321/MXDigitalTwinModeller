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
    /// From-scratch phone-metal generator (FROM_SCRATCH_ROADMAP.md P1, walking skeleton).
    /// Builds a DesignBody from EMPTY by deterministic param->stage replay, reusing the
    /// P0-verified cutter idiom + the (now-fixed) ModificationService mod tools on ONE live
    /// body — no STEP round-trip. The result is left in the active document for the MCP
    /// server / FeatureExtractor / FEA to consume.
    ///
    /// Stages are ordered S00..S12; only S00 (bbox), S04 (display pocket), S05 (camera),
    /// S06 (corner holes) are implemented in the skeleton. All geometry mutation runs inside
    /// the caller's WriteBlock (GenerationService methods assume they are already on the API
    /// thread inside a WriteBlock, matching how MCP marshals tool calls).
    /// </summary>
    public class GenerationService
    {
        public class GenResult
        {
            public bool Success;
            public string Error;
            public DesignBody Body;
            public List<string> StageLog = new List<string>();
            public double MeasuredVolumeMm3;
            // P3: stable feature handles minted at generation time (survive renumber).
            public HandleRegistry Handles = new HandleRegistry();
        }

        /// <summary>
        /// Generate the whole phone from params into <paramref name="part"/>. Must be called
        /// on the SC API thread inside a WriteBlock. stopAtStage (e.g. "S06") halts early.
        /// </summary>
        public GenResult Generate(Part part, PhoneParameters p, string stopAtStage = null)
        {
            var r = new GenResult();
            List<string> verr;
            if (!p.Validate(out verr))
            {
                r.Error = "invalid spec: " + string.Join("; ", verr.ToArray());
                return r;
            }

            try
            {
                // S00 — base solid (bbox) from EMPTY.
                DesignBody body = CreateBaseSolid(part, p);
                r.Body = body;
                r.StageLog.Add("S00 bbox vol=" + VolMm3(body).ToString("0.#"));
                if (stopAtStage == "S00") { Finish(r, body); return r; }

                // S00b — HOLLOW into a uniform-wall tray (P2). Runs BEFORE the discrete features
                // so S04-S06 cut into the already-thin walls (the P0 magnitude/min-wall guard then
                // validates breach-through-thin-wall). Disabled when HollowWall<=0 (solid mode).
                if (p.HollowWallMm > 0)
                {
                    var rs = CurvedShellBuilder.HollowToTray(body, p, p.HollowWallMm);
                    r.StageLog.Add("S00b hollow success=" + rs.Success + " wall=" + rs.WallMm +
                        " cornersR=" + rs.CornersRounded + "/" + (rs.CornersRounded + rs.CornersFailed) +
                        " vol=" + VolMm3(body).ToString("0.#"));
                    if (!rs.Success) { r.Error = "S00b hollow failed: " + rs.Error; r.Body = body; return r; }
                }
                if (stopAtStage == "S00b") { Finish(r, body); return r; }

                // S04 — display pocket (top face step-down).
                if (p.Pocket != null && p.Pocket.Enabled)
                {
                    var rp = ModificationService.AddPocket(
                        body, MmPos(0, 0, p.ThicknessMm),
                        p.Pocket.WidthMm, p.Pocket.LengthMm, p.Pocket.DepthMm,
                        new double[] { 0, 0, 1 });
                    r.StageLog.Add("S04 pocket success=" + rp.Success + " vol=" + VolMm3(body).ToString("0.#"));
                    if (!rp.Success) { r.Error = "S04 pocket failed: " + rp.ErrorMessage; r.Body = body; return r; }
                }
                if (stopAtStage == "S04") { Finish(r, body); return r; }

                // S05 — camera plateau (boss on top face).
                if (p.Camera != null)
                {
                    var rb = ModificationService.AddBoss(
                        body, MmPos(p.Camera.XMm, p.Camera.YMm, p.ThicknessMm),
                        p.Camera.DiameterMm, p.Camera.HeightMm, new double[] { 0, 0, 1 });
                    r.StageLog.Add("S05 camera success=" + rb.Success + " vol=" + VolMm3(body).ToString("0.#"));
                    if (!rb.Success) { r.Error = "S05 camera failed: " + rb.ErrorMessage; r.Body = body; return r; }
                    // P3: mint a stable handle keyed by the intended anchor (survives renumber).
                    r.Handles.Add(new FeatureHandle("S05", "boss", 0,
                        new double[] { p.Camera.XMm, p.Camera.YMm, p.ThicknessMm },
                        new double[] { 0, 0, 1 }, p.Camera.DiameterMm));
                }
                if (stopAtStage == "S05") { Finish(r, body); return r; }

                // S06 — corner / mounting holes (through).
                int hOk = 0, hOrd = 0;
                foreach (var h in p.Holes)
                {
                    var rh = ModificationService.AddHole(
                        body, MmPos(h.XMm, h.YMm, p.ThicknessMm),
                        h.DiameterMm, h.Through, h.DepthMm, new double[] { 0, 0, 1 }, false);
                    if (rh.Success) hOk++;
                    // P3: handle per hole, keyed by its intended (x,y,topZ)+diameter.
                    r.Handles.Add(new FeatureHandle("S06", "hole", hOrd++,
                        new double[] { h.XMm, h.YMm, p.ThicknessMm },
                        new double[] { 0, 0, 1 }, h.DiameterMm));
                }
                r.StageLog.Add("S06 holes " + hOk + "/" + p.Holes.Count + " vol=" + VolMm3(body).ToString("0.#"));

                Finish(r, body);
                return r;
            }
            catch (Exception ex)
            {
                r.Error = "generation exception: " + ex.Message;
                return r;
            }
        }

        /// <summary>
        /// S00: emit the rectangular slab from EMPTY via the P0-verified idiom
        /// (RectangleProfile centred on XY, extrude +Z by T). Slab spans
        /// X∈[-L/2,L/2], Y∈[-W/2,W/2], Z∈[0,T]. Caller is inside a WriteBlock.
        /// </summary>
        public DesignBody CreateBaseSolid(Part part, PhoneParameters p)
        {
            var plane = Plane.PlaneXY;
            // RectangleProfile(Plane, width, height, location, angle) — 5-arg; centred at origin.
            var prof = new RectangleProfile(plane,
                GeometryUtils.MmToMeters(p.LengthMm), GeometryUtils.MmToMeters(p.WidthMm),
                PointUV.Create(0, 0), 0.0);
            Body box = Body.ExtrudeProfile(prof, GeometryUtils.MmToMeters(p.ThicknessMm));
            return DesignBody.Create(part, "PhoneBody", box);
        }

        // ModificationService.Add*/Change* take positionMm in MILLIMETERS (they call
        // MmToMeters internally, e.g. AddPocket ModificationService.cs:2813). So pass mm
        // straight through — do NOT pre-convert. (The P0 probe's arr() pre-divided by 1000,
        // which made position ~1000x too small; it only "passed" because FindPlanarFaceAtZ's
        // 2mm tolerance still latched a face and the analytic volume happened to match. The
        // production path must pass true mm.)
        private static double[] MmPos(double x, double y, double z)
        {
            return new double[] { x, y, z };
        }

        private static double VolMm3(DesignBody b)
        {
            try { return b.Shape.Volume * 1e9; } catch { return 0; }
        }

        private static void Finish(GenResult r, DesignBody body)
        {
            r.Body = body;
            r.MeasuredVolumeMm3 = VolMm3(body);
            r.Success = true;
        }
    }
}
