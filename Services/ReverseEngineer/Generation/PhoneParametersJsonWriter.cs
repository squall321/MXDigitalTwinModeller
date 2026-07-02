using System.Collections.Generic;
using System.Globalization;
using System.Text;

using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Generation
{
    /// <summary>
    /// Serialize a PhoneParameters back to the SAME snake_case JSON spec SpecParser accepts —
    /// the read half of the P7 param loop (get_parameters). The contract is ROUND-TRIP:
    /// SpecParser.Parse(ToJson(p)) must bind an equivalent PhoneParameters with ZERO unknown-key
    /// warnings, so every key written here must appear in SpecParser.Bind's alias lists (the
    /// primary alias, e.g. "length_mm" not "l"). Hand-rolled JSON per FeatureGraphJsonWriter
    /// convention (no Newtonsoft).
    /// </summary>
    public static class PhoneParametersJsonWriter
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string ToJson(PhoneParameters p)
        {
            if (p == null) return "null";
            var sb = new StringBuilder();
            sb.Append("{");

            // envelope
            Num(sb, "length_mm", p.LengthMm, first: true);
            Num(sb, "width_mm", p.WidthMm);
            Num(sb, "thickness_mm", p.ThicknessMm);
            Num(sb, "corner_r", p.CornerRadiusMm);
            Num(sb, "edge_chamfer_mm", p.EdgeChamferMm);
            Str(sb, "edge_chamfer_filter", p.EdgeChamferFilter);
            Num(sb, "final_fillet_mm", p.FinalFilletMm);
            Str(sb, "final_fillet_filter", p.FinalFilletFilter);
            Num(sb, "min_wall", p.MinWallMm);
            Num(sb, "hollow_wall_mm", p.HollowWallMm);

            // curved back
            Num(sb, "back_curve_radius_mm", p.BackCurveRadiusMm);
            Num(sb, "back_bulge_mm", p.BackBulgeMm);
            Bool(sb, "lens_on_curved_back", p.LensOnCurvedBack);
            Bool(sb, "ports_on_flank", p.PortsOnFlank);

            // pocket (always emitted so enabled=false round-trips)
            sb.Append(", \"pocket\": ");
            if (p.Pocket == null)
            {
                sb.Append("false");
            }
            else
            {
                sb.Append("{");
                Bool(sb, "enabled", p.Pocket.Enabled, first: true);
                Num(sb, "width_mm", p.Pocket.WidthMm);
                Num(sb, "length_mm", p.Pocket.LengthMm);
                Num(sb, "depth_mm", p.Pocket.DepthMm);
                Num(sb, "corner_r", p.Pocket.CornerRadiusMm);
                sb.Append("}");
            }

            // camera (omitted when null — null means "no camera bump")
            if (p.Camera != null)
            {
                sb.Append(", \"camera\": {");
                Num(sb, "x_mm", p.Camera.XMm, first: true);
                Num(sb, "y_mm", p.Camera.YMm);
                Num(sb, "diameter_mm", p.Camera.DiameterMm);
                Num(sb, "height_mm", p.Camera.HeightMm);
                Num(sb, "width_mm", p.Camera.WidthMm);
                Num(sb, "length_mm", p.Camera.LengthMm);
                Num(sb, "corner_r", p.Camera.CornerRadiusMm);
                if (p.Camera.Lenses != null && p.Camera.Lenses.Count > 0)
                {
                    sb.Append(", \"lenses\": [");
                    for (int i = 0; i < p.Camera.Lenses.Count; i++)
                    {
                        var lz = p.Camera.Lenses[i];
                        if (i > 0) sb.Append(", ");
                        sb.Append("{");
                        Num(sb, "x_mm", lz.XMm, first: true);
                        Num(sb, "y_mm", lz.YMm);
                        Num(sb, "diameter_mm", lz.DiameterMm);
                        Num(sb, "depth_mm", lz.DepthMm);
                        sb.Append("}");
                    }
                    sb.Append("]");
                }
                sb.Append("}");
            }

            // front camera punch-hole (omitted when null)
            if (p.FrontPunch != null)
            {
                sb.Append(", \"front_punch\": {");
                Num(sb, "x_mm", p.FrontPunch.XMm, first: true);
                Num(sb, "y_mm", p.FrontPunch.YMm);
                Num(sb, "diameter_mm", p.FrontPunch.DiameterMm);
                sb.Append("}");
            }

            // holes
            if (p.Holes != null && p.Holes.Count > 0)
            {
                sb.Append(", \"holes\": [");
                for (int i = 0; i < p.Holes.Count; i++)
                {
                    var h = p.Holes[i];
                    if (i > 0) sb.Append(", ");
                    sb.Append("{");
                    Num(sb, "x_mm", h.XMm, first: true);
                    Num(sb, "y_mm", h.YMm);
                    Num(sb, "diameter_mm", h.DiameterMm);
                    Bool(sb, "through", h.Through);
                    Num(sb, "depth_mm", h.DepthMm);
                    Bool(sb, "on_curved_back", h.OnCurvedBack);
                    sb.Append("}");
                }
                sb.Append("]");
            }

            // ports
            if (p.Ports != null && p.Ports.Count > 0)
            {
                sb.Append(", \"ports\": [");
                for (int i = 0; i < p.Ports.Count; i++)
                {
                    var pt = p.Ports[i];
                    if (i > 0) sb.Append(", ");
                    sb.Append("{");
                    Str(sb, "type", pt.Type, first: true);
                    Num(sb, "x_mm", pt.XMm);
                    Num(sb, "y_mm", pt.YMm);
                    Num(sb, "z_mm", pt.ZMm);
                    Num(sb, "width_mm", pt.WidthMm);
                    Num(sb, "height_mm", pt.HeightMm);
                    Str(sb, "on_face", pt.OnFace);
                    sb.Append("}");
                }
                sb.Append("]");
            }

            // grille (omitted when null)
            if (p.Grille != null)
            {
                sb.Append(", \"grille\": {");
                Num(sb, "origin_x_mm", p.Grille.OriginXMm, first: true);
                Num(sb, "origin_y_mm", p.Grille.OriginYMm);
                Num(sb, "pitch_mm", p.Grille.PitchMm);
                Num(sb, "rows", p.Grille.Rows);
                Num(sb, "cols", p.Grille.Cols);
                Num(sb, "hole_diameter_mm", p.Grille.HoleDiameterMm);
                Bool(sb, "on_back", p.Grille.OnBack);
                sb.Append("}");
            }

            // buttons
            if (p.Buttons != null && p.Buttons.Count > 0)
            {
                sb.Append(", \"buttons\": [");
                for (int i = 0; i < p.Buttons.Count; i++)
                {
                    var b = p.Buttons[i];
                    if (i > 0) sb.Append(", ");
                    sb.Append("{");
                    Num(sb, "x_mm", b.XMm, first: true);
                    Num(sb, "y_mm", b.YMm);
                    Num(sb, "z_mm", b.ZMm);
                    Num(sb, "width_mm", b.WidthMm);
                    Num(sb, "height_mm", b.HeightMm);
                    Num(sb, "depth_mm", b.DepthMm);
                    sb.Append("}");
                }
                sb.Append("]");
            }

            // antenna slits (S10)
            if (p.AntennaSlits != null && p.AntennaSlits.Count > 0)
            {
                sb.Append(", \"antenna_slits\": [");
                for (int i = 0; i < p.AntennaSlits.Count; i++)
                {
                    var a = p.AntennaSlits[i];
                    if (i > 0) sb.Append(", ");
                    sb.Append("{");
                    Num(sb, "x_mm", a.XMm, first: true);
                    Num(sb, "y_mm", a.YMm);
                    Num(sb, "z_mm", a.ZMm);
                    Num(sb, "length_mm", a.LengthMm);
                    Num(sb, "width_mm", a.WidthMm);
                    Num(sb, "depth_mm", a.DepthMm);
                    Bool(sb, "on_flank", a.OnFlank);
                    sb.Append("}");
                }
                sb.Append("]");
            }

            // mic/sensor pinholes (S11)
            if (p.PinHoles != null && p.PinHoles.Count > 0)
            {
                sb.Append(", \"pin_holes\": [");
                for (int i = 0; i < p.PinHoles.Count; i++)
                {
                    var ph = p.PinHoles[i];
                    if (i > 0) sb.Append(", ");
                    sb.Append("{");
                    Num(sb, "x_mm", ph.XMm, first: true);
                    Num(sb, "y_mm", ph.YMm);
                    Num(sb, "diameter_mm", ph.DiameterMm);
                    Bool(sb, "through", ph.Through);
                    Num(sb, "depth_mm", ph.DepthMm);
                    Bool(sb, "on_curved_back", ph.OnCurvedBack);
                    sb.Append("}");
                }
                sb.Append("]");
            }

            sb.Append("}");
            return sb.ToString();
        }

        // --- key/value emitters (leading ", " unless first) ---
        private static void Num(StringBuilder sb, string key, double v, bool first = false)
        {
            if (!first) sb.Append(", ");
            // NaN/Infinity have no JSON representation — emit 0 rather than corrupt the payload
            // (SpecParser also rejects non-finite input, so this is defense in depth).
            double safe = (double.IsNaN(v) || double.IsInfinity(v)) ? 0.0 : v;
            sb.Append("\"").Append(key).Append("\": ").Append(safe.ToString("R", Inv));
        }

        private static void Bool(StringBuilder sb, string key, bool v, bool first = false)
        {
            if (!first) sb.Append(", ");
            sb.Append("\"").Append(key).Append("\": ").Append(v ? "true" : "false");
        }

        private static void Str(StringBuilder sb, string key, string v, bool first = false)
        {
            if (!first) sb.Append(", ");
            sb.Append("\"").Append(key).Append("\": ");
            if (v == null) { sb.Append("null"); return; }
            sb.Append("\"").Append(Escape(v)).Append("\"");
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }
    }
}
