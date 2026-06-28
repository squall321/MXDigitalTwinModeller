using System;
using System.Collections.Generic;
using System.Text;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer
{
    /// <summary>
    /// The single source of truth for a from-scratch phone front-metal design
    /// (FROM_SCRATCH_ROADMAP.md). Held by SessionContext beside the live DesignBody;
    /// generation is deterministic param->stage replay so the body can never silently
    /// diverge from these params. All lengths in MILLIMETERS.
    ///
    /// Design-intent CONSTRAINTS (relationships, not just per-field bounds) are enforced
    /// in Validate(): an LLM-populated spec that violates them is rejected BEFORE any
    /// geometry is built. Walking-skeleton fields (L/W/T, corner_r, pocket, holes, boss)
    /// are exercised first; the richer feature blocks (camera island, ports, grille, ...)
    /// are populated as stages S05-S12 come online.
    /// </summary>
    public class PhoneParameters
    {
        // --- overall envelope (S00 bbox) ---
        public double LengthMm = 146.7;
        public double WidthMm = 71.5;
        public double ThicknessMm = 7.4;
        public double CornerRadiusMm = 3.0;     // S01
        public double EdgeChamferMm = 0.0;      // S02 (0 = disabled)

        // --- minimum manufacturable wall (the thin-shell guard) ---
        public double MinWallMm = 0.4;

        // --- hollow shell (S00b, P2): wall thickness of the tray; <=0 = solid (no hollow) ---
        public double HollowWallMm = 0.6;

        // --- v2 curved back (S00a, P2/P4). DEFAULT 0 => FLAT v1 tray, byte-identical to current output. ---
        public double BackCurveRadiusMm = 0.0;   // cylindrical-section back radius; 0 = flat (v1). Axis || length X.
        public double BackBulgeMm = 0.0;         // convex sag at the back crown over W/2; 0 = flat (v1).
        public bool LensOnCurvedBack = false;    // S06: route lens holes through AddHoleOnFace (curved) vs planar.

        // --- display pocket (S04): a shallow step-down on the top face ---
        public DisplayPocket Pocket = new DisplayPocket();

        // --- corner / mounting holes (S06 in the skeleton; real lens holes later) ---
        public List<HoleSpec> Holes = new List<HoleSpec>();

        // --- camera plateau (S05): boss on the top face (cylinder today, rounded-rect later) ---
        public CameraIsland Camera = null;      // null = no camera bump

        // --- richer feature blocks, populated as later stages come online (P5) ---
        public List<PortSpec> Ports = new List<PortSpec>();
        public GrilleSpec Grille = null;
        public List<ButtonRecess> Buttons = new List<ButtonRecess>();

        public class DisplayPocket
        {
            public double WidthMm = 130.0;
            public double LengthMm = 60.0;
            public double DepthMm = 1.0;
            public double CornerRadiusMm = 2.0;
            public bool Enabled = true;
        }

        public class HoleSpec
        {
            public double XMm, YMm;             // centre on the top face
            public double DiameterMm = 4.0;
            public bool Through = true;
            public double DepthMm = 0.0;        // ignored when Through
        }

        public class CameraIsland
        {
            public double XMm = 0.0, YMm = 0.0;
            public double DiameterMm = 12.0;    // plateau (cylinder for now)
            public double HeightMm = 1.5;
        }

        public class PortSpec
        {
            public string Type = "usbc";        // usbc | lightning
            public double XMm, YMm, ZMm;
            public double WidthMm, HeightMm;
            public string OnFace = "flank";     // which face the slot enters
        }

        public class GrilleSpec
        {
            public double OriginXMm, OriginYMm;
            public double PitchMm = 2.0;
            public int Rows = 1, Cols = 6;
            public double HoleDiameterMm = 1.0;
        }

        public class ButtonRecess
        {
            public double XMm, YMm, ZMm;
            public double WidthMm, HeightMm, DepthMm;
        }

        /// <summary>
        /// Enforce design-intent RELATIONSHIPS before any geometry is built. Returns true
        /// when the spec is buildable; otherwise false with a human-readable reason list.
        /// This is the Tier-1 guardrail an LLM-populated spec must pass.
        /// </summary>
        public bool Validate(out List<string> errors)
        {
            errors = new List<string>();

            // positive envelope
            if (LengthMm <= 0 || WidthMm <= 0 || ThicknessMm <= 0)
                errors.Add("dimensions must be positive (L,W,T)");
            if (MinWallMm <= 0)
                errors.Add("min_wall must be positive");
            if (CornerRadiusMm < 0 || CornerRadiusMm * 2 > Math.Min(LengthMm, WidthMm))
                errors.Add("corner_r must be >=0 and fit within the smaller plan dimension");

            // pocket fits inside the plan and leaves a back wall
            if (Pocket != null && Pocket.Enabled)
            {
                if (Pocket.WidthMm <= 0 || Pocket.LengthMm <= 0 || Pocket.DepthMm <= 0)
                    errors.Add("pocket dims must be positive");
                if (Pocket.WidthMm >= LengthMm || Pocket.LengthMm >= WidthMm)
                    errors.Add("pocket footprint must fit inside the plan with a surrounding bezel");
                if (Pocket.DepthMm > ThicknessMm - MinWallMm)
                    errors.Add(string.Format(
                        "pocket_depth ({0:0.##}) must leave >= min_wall ({1:0.##}) of back wall (T={2:0.##})",
                        Pocket.DepthMm, MinWallMm, ThicknessMm));
            }

            // camera bump must not pierce the part
            if (Camera != null)
            {
                if (Camera.DiameterMm <= 0 || Camera.HeightMm <= 0)
                    errors.Add("camera island dims must be positive");
                // bump rises ABOVE the top face; it must not be taller than the part is thick
                if (Camera.HeightMm >= ThicknessMm)
                    errors.Add("camera bump_height must be < total_thickness");
            }

            // holes fit through and don't collide with the pocket void where they'd be too thin
            foreach (var h in Holes)
            {
                if (h.DiameterMm <= 0) { errors.Add("hole diameter must be positive"); continue; }
                if (Math.Abs(h.XMm) + h.DiameterMm / 2 > LengthMm / 2 ||
                    Math.Abs(h.YMm) + h.DiameterMm / 2 > WidthMm / 2)
                    errors.Add(string.Format("hole at ({0:0.#},{1:0.#}) extends outside the plan", h.XMm, h.YMm));
            }

            // v2 curved back consistency (only when requested; 0/0 = flat v1 adds zero errors)
            if (BackBulgeMm < 0 || BackCurveRadiusMm < 0)
                errors.Add("back curve radius/bulge must be >= 0");
            if (BackBulgeMm > 0)
            {
                if (BackBulgeMm >= ThicknessMm - MinWallMm)
                    errors.Add("back bulge must leave >= min_wall after hollowing");
                // gentle-curvature guard (the straight-cutter validity bound): require R >> feature size
                double rEff = (BackCurveRadiusMm > 0) ? BackCurveRadiusMm
                    : ((WidthMm / 2.0) * (WidthMm / 2.0) / (2.0 * BackBulgeMm) + BackBulgeMm / 2.0);
                if (rEff < WidthMm)   // R smaller than the phone is a deep draw, not a gentle back
                    errors.Add("back curve too tight (R < W); v2 supports GENTLE curvature only");
            }
            if (LensOnCurvedBack && !(BackCurveRadiusMm > 0 || BackBulgeMm > 0))
                errors.Add("lens_on_curved_back requires a curved back");

            return errors.Count == 0;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("Phone L={0} W={1} T={2} cornerR={3} pocket={4} holes={5} camera={6}",
                LengthMm, WidthMm, ThicknessMm, CornerRadiusMm,
                (Pocket != null && Pocket.Enabled) ? "on" : "off",
                Holes.Count, Camera != null ? "on" : "off");
            return sb.ToString();
        }
    }
}
