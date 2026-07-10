using System;
using System.Collections.Generic;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Battery
{
    public enum FlangeFold { None, Flat, Folded }
    public enum SwellMode { None, Dome }

    /// <summary>
    /// Fully parametric pouch-cell spec. Every dimension defaults to 0 = "derive from the
    /// core envelope via documented typical ratios"; any value can be overridden — no
    /// hardcoded geometry downstream. Canonical build orientation: length along X,
    /// width along Y, thickness along Z (core z in [0, T]); the terrace/tab end is +X.
    /// </summary>
    public class PouchBatterySpec
    {
        // ---- core envelope (required) --------------------------------------
        public double LengthMm;                 // core (jelly-roll zone) length
        public double WidthMm;
        public double ThicknessMm;
        /// <summary>0 = 5% of the smaller in-plane dimension.</summary>
        public double CornerRMm;

        // ---- terrace (seal shelf at the +X end) ----------------------------
        /// <summary>0 = no terrace. Typical 3-5mm.</summary>
        public double TerraceLengthMm = 4.0;
        /// <summary>0 = 40% of core thickness (z-centered on the mid-plane).</summary>
        public double TerraceThicknessMm;

        // ---- tabs (2, exiting the terrace end face) -------------------------
        /// <summary>0 = 15% of the width.</summary>
        public double TabWidthMm;
        public double TabThicknessMm = 0.2;
        public double TabLengthMm = 5.0;
        /// <summary>Center distance between the two tabs; 0 = 40% of the width.</summary>
        public double TabPitchMm;
        /// <summary>Common Y offset of the tab pair.</summary>
        public double TabOffsetMm;

        // ---- side seal flanges (pouch film fold, along both +/-Y edges) -----
        public FlangeFold Flange = FlangeFold.Flat;
        public double FlangeWidthMm = 1.5;
        public double FlangeThicknessMm = 0.15;

        // ---- swell state -----------------------------------------------------
        public SwellMode Swell = SwellMode.None;
        /// <summary>Dome swell as a volume fraction of the FLAT CORE volume
        /// (dV = percent/100 * V_core). Exclusive with SwellHeightMm.</summary>
        public double SwellPercent;
        /// <summary>Explicit dome height above the face instead of a percent.</summary>
        public double SwellHeightMm;
        /// <summary>Bulge both large faces (default) or the top face only.</summary>
        public bool SwellBothSides = true;
        /// <summary>Dome top section scale (0-1) relative to its base.</summary>
        public double SwellTopScale = 0.55;
        /// <summary>Dome base inset from the core outline; 0 = corner radius.</summary>
        public double SwellInsetMm;

        // ---- stacking ---------------------------------------------------------
        public int Count = 1;
        public double GapMm = 0.5;

        public string NamePrefix = "Battery";

        /// <summary>Fill every 0/auto field from the documented ratios and validate.
        /// Throws ArgumentException with an actionable message on any violation.</summary>
        public void ResolveAndValidate()
        {
            if (LengthMm <= 0 || WidthMm <= 0 || ThicknessMm <= 0)
                throw new ArgumentException("length_mm/width_mm/thickness_mm must be > 0");
            if (CornerRMm <= 0) CornerRMm = 0.05 * Math.Min(LengthMm, WidthMm);
            if (CornerRMm >= Math.Min(LengthMm, WidthMm) / 2)
                throw new ArgumentException("corner_r_mm must be < min(length, width)/2");

            if (TerraceLengthMm < 0) throw new ArgumentException("terrace length_mm must be >= 0");
            if (TerraceLengthMm > 0)
            {
                if (TerraceThicknessMm <= 0) TerraceThicknessMm = 0.4 * ThicknessMm;
                if (TerraceThicknessMm >= ThicknessMm)
                    throw new ArgumentException("terrace thickness_mm must be < core thickness_mm");
            }

            if (TabWidthMm <= 0) TabWidthMm = 0.15 * WidthMm;
            if (TabPitchMm <= 0) TabPitchMm = 0.4 * WidthMm;
            if (TabThicknessMm <= 0 || TabLengthMm <= 0)
                throw new ArgumentException("tab thickness_mm/length_mm must be > 0");
            double tabReach = Math.Abs(TabOffsetMm) + TabPitchMm / 2 + TabWidthMm / 2;
            if (tabReach > WidthMm / 2)
                throw new ArgumentException(string.Format(
                    "tabs exceed the cell width (reach {0:0.##}mm > {1:0.##}mm half-width) - " +
                    "reduce tab width/pitch/offset", tabReach, WidthMm / 2));
            if (TerraceLengthMm > 0 && TabThicknessMm > TerraceThicknessMm)
                throw new ArgumentException("tab thickness_mm must be <= terrace thickness_mm");

            if (Flange != FlangeFold.None && (FlangeWidthMm <= 0 || FlangeThicknessMm <= 0))
                throw new ArgumentException("flange width_mm/thickness_mm must be > 0");
            if (Flange == FlangeFold.Flat && FlangeThicknessMm > ThicknessMm)
                throw new ArgumentException("flat flange thickness_mm must be <= core thickness_mm");
            if (Flange == FlangeFold.Folded && FlangeWidthMm > ThicknessMm)
                throw new ArgumentException("folded flange width_mm must be <= core thickness_mm");

            if (Swell != SwellMode.None)
            {
                if (SwellPercent > 0 && SwellHeightMm > 0)
                    throw new ArgumentException("give swell percent OR height_mm, not both");
                if (SwellPercent <= 0 && SwellHeightMm <= 0)
                    throw new ArgumentException("swell needs percent > 0 or height_mm > 0");
                if (SwellTopScale <= 0.05 || SwellTopScale >= 0.95)
                    throw new ArgumentException("swell top_scale must be in (0.05, 0.95)");
                if (SwellInsetMm <= 0) SwellInsetMm = CornerRMm;
                if (SwellInsetMm < CornerRMm)
                    throw new ArgumentException(
                        "swell inset_mm must be >= corner_r_mm (the dome pad must sit inside " +
                        "the rounded core outline)");
                double baseW = LengthMm - 2 * SwellInsetMm, baseH = WidthMm - 2 * SwellInsetMm;
                if (baseW <= 1 || baseH <= 1)
                    throw new ArgumentException("swell inset_mm leaves no dome footprint");
            }

            if (Count < 1 || Count > 8)
                throw new ArgumentException("count must be in [1, 8]");
            if (Count > 1 && GapMm < 0)
                throw new ArgumentException("gap_mm must be >= 0");
        }

        /// <summary>Flat rounded-rect core volume [L*W - (4-pi)*r^2] * T.</summary>
        public double CoreVolumeMm3()
        {
            return (LengthMm * WidthMm - (4 - Math.PI) * CornerRMm * CornerRMm) * ThicknessMm;
        }
    }

    public class PouchBatteryResult
    {
        public bool Success;
        public string Error;
        public List<string> BodiesCreated = new List<string>();
        public Dictionary<string, double> DimsMm = new Dictionary<string, double>();
        public List<string> Log = new List<string>();
    }
}
