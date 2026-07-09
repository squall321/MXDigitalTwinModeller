using System;
using System.Collections.Generic;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Fastener
{
    public enum FastenerType { Bolt, Rivet }

    /// <summary>Head geometry family. Hex/SocketCap/Pan/Countersunk suit bolts;
    /// Dome/Flat/Countersunk suit rivet factory heads.</summary>
    public enum HeadStyle { Hex, SocketCap, Pan, Countersunk, Dome, Flat }

    /// <summary>Thread representation. None = plain shank at nominal d;
    /// Simplified = threaded zone at ISO 724 minor diameter (FEA-friendly);
    /// CosmeticRings = minor-diameter core + one ring per pitch (visual thread).</summary>
    public enum ThreadStyle { None, Simplified, CosmeticRings }

    /// <summary>ISO 262 coarse-pitch preferred sizes. This is STANDARDS DATA — every
    /// geometric dimension is derived parametrically from the selected nominal.</summary>
    public static class IsoMetricThread
    {
        public static readonly double[] NominalMm =
            { 1.6, 2.0, 2.5, 3.0, 4.0, 5.0, 6.0, 8.0, 10.0, 12.0, 16.0, 20.0, 24.0 };
        public static readonly double[] CoarsePitchMm =
            { 0.35, 0.4, 0.45, 0.5, 0.7, 0.8, 1.0, 1.25, 1.5, 1.75, 2.0, 2.5, 3.0 };

        /// <summary>Largest preferred nominal that fits a clearance hole with a
        /// medium-fit margin (ISO 273-like: max(0.2mm, 4% of hole)). 0 when none fit.</summary>
        public static double AutoNominalForHole(double holeDiaMm)
        {
            double margin = Math.Max(0.2, 0.04 * holeDiaMm);
            double best = 0;
            foreach (var n in NominalMm)
                if (n <= holeDiaMm - margin && n > best) best = n;
            return best;
        }

        public static double CoarsePitchFor(double nominalMm)
        {
            for (int i = 0; i < NominalMm.Length; i++)
                if (Math.Abs(NominalMm[i] - nominalMm) < 1e-9) return CoarsePitchMm[i];
            // non-preferred size: ISO-typical pitch trend ~ 0.12*d^0.75, clamped
            return Math.Max(0.35, Math.Min(3.0, 0.12 * Math.Pow(nominalMm, 0.75)));
        }

        /// <summary>ISO 724 basic minor diameter: d3 = d − 1.2269·P.</summary>
        public static double MinorDia(double nominalMm, double pitchMm)
        {
            return nominalMm - 1.2269 * pitchMm;
        }
    }

    public class ThreadSpec
    {
        public ThreadStyle Style = ThreadStyle.CosmeticRings;
        /// <summary>0 = ISO 262 coarse pitch for the nominal.</summary>
        public double PitchMm = 0;
        /// <summary>Threaded length from the tip; 0 = min(2.5·d, full shank).</summary>
        public double ThreadLenMm = 0;
        /// <summary>Ring disc thickness as a fraction of pitch (cosmetic rings).</summary>
        public double RingWidthFrac = 0.4;
    }

    /// <summary>
    /// Fully parametric fastener spec. Every dimension defaults to 0 = "derive from the
    /// detected site via ISO-typical proportional ratios"; any value can be overridden —
    /// no hardcoded geometry anywhere downstream.
    /// </summary>
    public class FastenerSpec
    {
        public FastenerType Type = FastenerType.Bolt;
        public HeadStyle Head = HeadStyle.Hex;
        /// <summary>Thread nominal (bolt) / shank dia (rivet). 0 = auto from hole.</summary>
        public double NominalDMm = 0;
        /// <summary>Bolt: shank length below the head. 0 = grip + washers + nut + 2·pitch.</summary>
        public double LengthMm = 0;
        public bool WithNut = true;       // bolts only
        public bool WithWasher = false;   // under head and (when nut) under nut
        public ThreadSpec Thread = new ThreadSpec();

        // ---- proportional ratios (× nominal d); 0 = per-style default -----------
        /// <summary>Hex: width across flats; others: head outer diameter.</summary>
        public double HeadDiaRatio = 0;
        public double HeadHeightRatio = 0;
        public double NutWidthRatio = 1.5;    // hex across flats (ISO 4032-like)
        public double NutHeightRatio = 0.8;
        public double WasherOdRatio = 2.0;    // ISO 7089-like
        public double WasherThickRatio = 0.15;
        public double RivetTailDiaRatio = 1.6;   // bucked shop head
        public double RivetTailHeightRatio = 0.6;
        /// <summary>Rivet shank fills the hole with this fraction of hole dia.</summary>
        public double RivetHoleFillFrac = 0.98;
        /// <summary>Nut/washer bore clearance factor over nominal d.</summary>
        public double BoreClearanceFrac = 1.05;

        /// <summary>Per-style default (ratio, heightRatio) when the spec leaves them 0.
        /// ISO-typical proportions: hex 1.5w/0.65h (ISO 4014), socket 1.5/1.0 (ISO 4762),
        /// pan 2.0/0.4, countersunk 2.0/0.5, dome 1.8/0.5, flat 2.0/0.3.</summary>
        public void ResolveHeadRatios(out double diaRatio, out double heightRatio)
        {
            double d, h;
            switch (Head)
            {
                case HeadStyle.Hex:         d = 1.5; h = 0.65; break;
                case HeadStyle.SocketCap:   d = 1.5; h = 1.0;  break;
                case HeadStyle.Pan:         d = 2.0; h = 0.4;  break;
                case HeadStyle.Countersunk: d = 2.0; h = 0.5;  break;
                case HeadStyle.Dome:        d = 1.8; h = 0.5;  break;
                default:                    d = 2.0; h = 0.3;  break; // Flat
            }
            diaRatio = HeadDiaRatio > 0 ? HeadDiaRatio : d;
            heightRatio = HeadHeightRatio > 0 ? HeadHeightRatio : h;
        }
    }

    /// <summary>A detected fastening site: coaxial cylindrical hole faces spanning one
    /// or more bodies (the "two concentric circles" selection, generalized).</summary>
    public class FastenerSite
    {
        /// <summary>Point on the axis at the BOTTOM of the hole stack (mm).</summary>
        public double[] AxisPointMm = new double[3];
        /// <summary>Unit axis direction, bottom → top.</summary>
        public double[] AxisDir = new double[3];
        public double HoleDiaMm;
        /// <summary>Total clamped stack thickness along the axis.</summary>
        public double GripMm;
        public List<string> BodyNames = new List<string>();
        public int FaceCount;
    }

    public class FastenerResult
    {
        public bool Success;
        public string Error;
        public List<string> BodiesCreated = new List<string>();
        /// <summary>Every derived dimension actually used (mm) — the parametric record.</summary>
        public Dictionary<string, double> DimsMm = new Dictionary<string, double>();
        public List<string> Log = new List<string>();
    }
}
