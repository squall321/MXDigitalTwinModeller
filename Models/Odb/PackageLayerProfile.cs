using System;
using System.Collections.Generic;
using System.Globalization;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Odb
{
    /// <summary>Package families with distinct internal layer stacks. ODB++ carries no
    /// internal stack, so the family is inferred (name keywords + pin topology) and each
    /// maps to a fractional preset — an explicit modeling ASSUMPTION, not file data.</summary>
    public enum OdbPackageFamily { Passive, Ic, Bga, FlipChip, BareDie }

    /// <summary>One layer of a package stack, as a FRACTION (0..1) of the component
    /// height. The fractions of a family sum to 1.0 so the split always fills exactly
    /// the imported package thickness.</summary>
    public class OdbLayerFraction
    {
        public string Name;
        public double Fraction;
        public OdbLayerFraction(string name, double fraction) { Name = name; Fraction = fraction; }
    }

    /// <summary>Default fractional layer stacks per family + a transparent classifier.
    /// Presets are overridable (the importer accepts a replacement map), so nothing here
    /// is hardcoded into the geometry path.</summary>
    public static class OdbLayerPresets
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Fractional stacks (bottom -> top). Each list sums to 1.0.</summary>
        public static Dictionary<OdbPackageFamily, List<OdbLayerFraction>> Defaults()
        {
            return new Dictionary<OdbPackageFamily, List<OdbLayerFraction>>
            {
                { OdbPackageFamily.Ic, new List<OdbLayerFraction>
                    {
                        new OdbLayerFraction("leadframe", 0.12),
                        new OdbLayerFraction("die_attach", 0.05),
                        new OdbLayerFraction("die", 0.33),
                        new OdbLayerFraction("mold", 0.50),
                    } },
                { OdbPackageFamily.Bga, new List<OdbLayerFraction>
                    {
                        new OdbLayerFraction("substrate", 0.30),
                        new OdbLayerFraction("die_attach", 0.05),
                        new OdbLayerFraction("die", 0.25),
                        new OdbLayerFraction("mold", 0.40),
                    } },
                { OdbPackageFamily.FlipChip, new List<OdbLayerFraction>
                    {
                        new OdbLayerFraction("substrate", 0.28),
                        new OdbLayerFraction("underfill", 0.10),
                        new OdbLayerFraction("die", 0.32),
                        new OdbLayerFraction("lid", 0.30),
                    } },
                { OdbPackageFamily.BareDie, new List<OdbLayerFraction>
                    {
                        new OdbLayerFraction("die_attach", 0.10),
                        new OdbLayerFraction("die", 0.90),
                    } },
                { OdbPackageFamily.Passive, new List<OdbLayerFraction>
                    {
                        new OdbLayerFraction("termination_bottom", 0.15),
                        new OdbLayerFraction("body", 0.70),
                        new OdbLayerFraction("termination_top", 0.15),
                    } },
            };
        }

        public static string FamilyName(OdbPackageFamily f)
        {
            switch (f)
            {
                case OdbPackageFamily.Passive: return "passive";
                case OdbPackageFamily.Bga: return "bga";
                case OdbPackageFamily.FlipChip: return "flip_chip";
                case OdbPackageFamily.BareDie: return "bare_die";
                default: return "ic";
            }
        }

        /// <summary>Infer the package family from part/package name keywords first, then
        /// pin topology (count + area-array test). Transparent and deterministic.</summary>
        public static OdbPackageFamily Classify(OdbPackage pkg, OdbComponent comp)
        {
            string name = ((comp != null ? comp.PartName : "") + " "
                + (pkg != null ? pkg.Name : "")).ToUpperInvariant();

            if (Contains(name, "FLIP", "FCBGA", "FCCSP", "FC-", "FLIPCHIP"))
                return OdbPackageFamily.FlipChip;
            if (Contains(name, "BGA", "CSP", "LGA", "POP", "WLP", "WLCSP"))
                return OdbPackageFamily.Bga;
            if (Contains(name, "BARE", "COB", "KGD"))
                return OdbPackageFamily.BareDie;

            int pins = pkg != null ? pkg.Pins.Count : 0;
            if (pins <= 3) return OdbPackageFamily.Passive;
            if (pins >= 16 && IsAreaArray(pkg)) return OdbPackageFamily.Bga;
            return OdbPackageFamily.Ic;
        }

        private static bool Contains(string haystack, params string[] needles)
        {
            foreach (var n in needles)
                if (haystack.IndexOf(n, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        /// <summary>Area-array (BGA/LGA) vs peripheral (QFN/QFP): true when pins populate
        /// the interior of the pin bounding box, not just its perimeter. Uses the package
        /// pitch as the edge band; falls back to a bbox fraction when pitch is unknown.</summary>
        private static bool IsAreaArray(OdbPackage pkg)
        {
            if (pkg == null || pkg.Pins.Count < 9) return false;
            double x0 = double.MaxValue, x1 = double.MinValue;
            double y0 = double.MaxValue, y1 = double.MinValue;
            foreach (var p in pkg.Pins)
            {
                x0 = Math.Min(x0, p.XMm); x1 = Math.Max(x1, p.XMm);
                y0 = Math.Min(y0, p.YMm); y1 = Math.Max(y1, p.YMm);
            }
            double w = x1 - x0, h = y1 - y0;
            if (w <= 0 || h <= 0) return false;
            double band = pkg.PitchMm > 0 ? pkg.PitchMm * 1.5
                                          : Math.Min(w, h) * 0.25;
            int interior = 0;
            foreach (var p in pkg.Pins)
                if (p.XMm - x0 > band && x1 - p.XMm > band
                    && p.YMm - y0 > band && y1 - p.YMm > band)
                    interior++;
            // a peripheral package has ~0 interior pins; an area array has many
            return interior >= Math.Max(2, pkg.Pins.Count / 5);
        }
    }
}
