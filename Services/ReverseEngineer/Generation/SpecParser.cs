using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Generation
{
    /// <summary>
    /// LLM SpecParser (FROM_SCRATCH_ROADMAP.md, the last v2 item). Binds a STRUCTURED JSON spec
    /// into a PhoneParameters and runs its design-intent Validate() — the deterministic, dependency-
    /// free bridge between an LLM and the generator.
    ///
    /// DIVISION OF LABOR: the LLM does the natural-language understanding ("a 150x72x8mm phone,
    /// gently curved back, USB-C on the bottom flank, a 4mm lens hole on the curved back") and emits
    /// a JSON object whose keys are the snake_case design fields (the SAME names PhoneParameters.
    /// Validate() uses in its messages: length_mm, min_wall, back_bulge_mm, lens_on_curved_back, ...).
    /// This parser does NOT do NLP — it binds that JSON to the typed spec and rejects it BEFORE any
    /// geometry if Validate() fails. Unspecified fields keep the PhoneParameters defaults.
    ///
    /// No JSON library is referenced in this project (FeatureGraphJsonWriter hand-rolls output), so
    /// this file carries a small, self-contained recursive-descent JSON reader scoped to the spec
    /// shape (object / array / string / number / bool / null). Keys are matched case-insensitively
    /// and tolerate a few synonyms (length | length_mm | l).
    /// </summary>
    public static class SpecParser
    {
        public class ParseResult
        {
            public bool Success;
            public PhoneParameters Params;
            public List<string> Errors = new List<string>();   // parse + validation errors
            public List<string> Warnings = new List<string>(); // unknown keys, ignored values
        }

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Parse a structured JSON spec into a validated PhoneParameters.</summary>
        public static ParseResult Parse(string specJson)
        {
            return Parse(specJson, new PhoneParameters());
        }

        /// <summary>
        /// Parse ONTO a baseline (PATCH semantics — the set_parameters channel): only the keys
        /// present in the JSON change; everything else keeps the baseline's values, because Bind
        /// already treats current Params values as the defaults. Two rules the caller must know:
        ///   - LIST blocks (holes/ports/buttons/antenna_slits/pin_holes) are REPLACED wholesale
        ///     when their key is supplied (cleared first — Bind appends), never merged per-item.
        ///   - camera/grille are also replaced wholesale (Bind builds a fresh object).
        /// The baseline instance is MUTATED — pass a private copy, not a live session object.
        /// </summary>
        public static ParseResult Parse(string specJson, PhoneParameters baseline)
        {
            var r = new ParseResult { Params = baseline ?? new PhoneParameters() };
            if (string.IsNullOrEmpty(specJson))
            { r.Errors.Add("spec JSON is empty"); return r; }

            object root;
            try
            {
                var rdr = new JsonReader(specJson);
                root = rdr.ParseValue();
                rdr.SkipWs();
                if (!rdr.AtEnd) r.Warnings.Add("trailing characters after top-level JSON value");
            }
            catch (Exception ex) { r.Errors.Add("JSON parse failed: " + ex.Message); return r; }

            var obj = root as Dictionary<string, object>;
            if (obj == null) { r.Errors.Add("top-level spec must be a JSON object"); return r; }

            // Patch semantics: a supplied list block REPLACES the baseline's list (Bind appends,
            // so clear first). On a fresh baseline the lists are empty and this is a no-op.
            ClearSuppliedLists(obj, r.Params);
            Bind(obj, r);

            // design-intent validation (Tier-1 guard) — same gate the MCP path uses.
            List<string> verrs;
            if (!r.Params.Validate(out verrs))
                r.Errors.AddRange(verrs);

            r.Success = r.Errors.Count == 0;
            return r;
        }

        // -------------------------------------------------------------------
        // Binding (JSON object -> PhoneParameters)
        // -------------------------------------------------------------------
        private static void Bind(Dictionary<string, object> o, ParseResult r)
        {
            var p = r.Params;
            var seen = new HashSet<string>();

            // envelope
            p.LengthMm = Num(o, seen, p.LengthMm, "length_mm", "length", "l");
            p.WidthMm = Num(o, seen, p.WidthMm, "width_mm", "width", "w");
            p.ThicknessMm = Num(o, seen, p.ThicknessMm, "thickness_mm", "thickness", "t");
            p.CornerRadiusMm = Num(o, seen, p.CornerRadiusMm, "corner_r", "corner_radius_mm", "corner_radius");
            p.EdgeChamferMm = Num(o, seen, p.EdgeChamferMm, "edge_chamfer_mm", "edge_chamfer");
            p.EdgeChamferFilter = Str(o, seen, p.EdgeChamferFilter, "edge_chamfer_filter");
            p.FinalFilletMm = Num(o, seen, p.FinalFilletMm, "final_fillet_mm", "final_fillet");
            p.FinalFilletFilter = Str(o, seen, p.FinalFilletFilter, "final_fillet_filter");
            p.MinWallMm = Num(o, seen, p.MinWallMm, "min_wall", "min_wall_mm");
            p.HollowWallMm = Num(o, seen, p.HollowWallMm, "hollow_wall_mm", "hollow_wall", "wall_mm", "wall");

            // curved back
            p.BackCurveRadiusMm = Num(o, seen, p.BackCurveRadiusMm, "back_curve_radius_mm", "back_curve_radius");
            p.BackBulgeMm = Num(o, seen, p.BackBulgeMm, "back_bulge_mm", "back_bulge", "bulge_mm");
            p.LensOnCurvedBack = Bool(o, seen, p.LensOnCurvedBack, "lens_on_curved_back");
            p.PortsOnFlank = Bool(o, seen, p.PortsOnFlank, "ports_on_flank");

            // pocket
            object pk;
            if (TryGet(o, seen, out pk, "pocket", "display_pocket"))
            {
                var pkObj = pk as Dictionary<string, object>;
                if (pkObj != null)
                {
                    var dp = p.Pocket;
                    var s2 = new HashSet<string>();
                    dp.Enabled = Bool(pkObj, s2, true, "enabled");
                    dp.WidthMm = Num(pkObj, s2, dp.WidthMm, "width_mm", "width");
                    dp.LengthMm = Num(pkObj, s2, dp.LengthMm, "length_mm", "length");
                    dp.DepthMm = Num(pkObj, s2, dp.DepthMm, "depth_mm", "depth");
                    dp.CornerRadiusMm = Num(pkObj, s2, dp.CornerRadiusMm, "corner_r", "corner_radius_mm");
                }
                else if (pk is bool) p.Pocket.Enabled = (bool)pk;   // "pocket": false disables it
            }

            // camera (null unless present)
            object cam;
            if (TryGet(o, seen, out cam, "camera", "camera_island"))
            {
                var camObj = cam as Dictionary<string, object>;
                if (camObj != null)
                {
                    var ci = new PhoneParameters.CameraIsland();
                    var s2 = new HashSet<string>();
                    ci.XMm = Num(camObj, s2, ci.XMm, "x_mm", "x");
                    ci.YMm = Num(camObj, s2, ci.YMm, "y_mm", "y");
                    ci.DiameterMm = Num(camObj, s2, ci.DiameterMm, "diameter_mm", "diameter", "d");
                    ci.HeightMm = Num(camObj, s2, ci.HeightMm, "height_mm", "height", "h");
                    // rounded-rect plateau (set width_mm + length_mm to use it instead of the cylinder)
                    ci.WidthMm = Num(camObj, s2, ci.WidthMm, "width_mm", "width", "w");
                    ci.LengthMm = Num(camObj, s2, ci.LengthMm, "length_mm", "length", "l");
                    ci.CornerRadiusMm = Num(camObj, s2, ci.CornerRadiusMm, "corner_r", "corner_radius_mm", "corner_radius");
                    p.Camera = ci;
                }
            }

            // holes
            object holes;
            if (TryGet(o, seen, out holes, "holes"))
            {
                var arr = holes as List<object>;
                if (arr != null)
                    foreach (var item in arr)
                    {
                        var ho = item as Dictionary<string, object>;
                        if (ho == null) continue;
                        var h = new PhoneParameters.HoleSpec();
                        var s2 = new HashSet<string>();
                        h.XMm = Num(ho, s2, h.XMm, "x_mm", "x");
                        h.YMm = Num(ho, s2, h.YMm, "y_mm", "y");
                        h.DiameterMm = Num(ho, s2, h.DiameterMm, "diameter_mm", "diameter", "d");
                        h.Through = Bool(ho, s2, h.Through, "through");
                        h.DepthMm = Num(ho, s2, h.DepthMm, "depth_mm", "depth");
                        h.OnCurvedBack = Bool(ho, s2, h.OnCurvedBack, "on_curved_back");
                        p.Holes.Add(h);
                    }
            }

            // ports
            object ports;
            if (TryGet(o, seen, out ports, "ports"))
            {
                var arr = ports as List<object>;
                if (arr != null)
                    foreach (var item in arr)
                    {
                        var po = item as Dictionary<string, object>;
                        if (po == null) continue;
                        var prt = new PhoneParameters.PortSpec();
                        var s2 = new HashSet<string>();
                        prt.Type = Str(po, s2, prt.Type, "type");
                        prt.XMm = Num(po, s2, prt.XMm, "x_mm", "x");
                        prt.YMm = Num(po, s2, prt.YMm, "y_mm", "y");
                        prt.ZMm = Num(po, s2, prt.ZMm, "z_mm", "z");
                        prt.WidthMm = Num(po, s2, prt.WidthMm, "width_mm", "width");
                        prt.HeightMm = Num(po, s2, prt.HeightMm, "height_mm", "height");
                        prt.OnFace = Str(po, s2, prt.OnFace, "on_face");
                        p.Ports.Add(prt);
                    }
            }

            // grille (null unless present)
            object grille;
            if (TryGet(o, seen, out grille, "grille"))
            {
                var go = grille as Dictionary<string, object>;
                if (go != null)
                {
                    var g = new PhoneParameters.GrilleSpec();
                    var s2 = new HashSet<string>();
                    g.OriginXMm = Num(go, s2, g.OriginXMm, "origin_x_mm", "origin_x", "x_mm", "x");
                    g.OriginYMm = Num(go, s2, g.OriginYMm, "origin_y_mm", "origin_y", "y_mm", "y");
                    g.PitchMm = Num(go, s2, g.PitchMm, "pitch_mm", "pitch");
                    g.Rows = (int)Math.Round(Num(go, s2, g.Rows, "rows"));
                    g.Cols = (int)Math.Round(Num(go, s2, g.Cols, "cols"));
                    g.HoleDiameterMm = Num(go, s2, g.HoleDiameterMm, "hole_diameter_mm", "hole_diameter");
                    p.Grille = g;
                }
            }

            // buttons
            object buttons;
            if (TryGet(o, seen, out buttons, "buttons"))
            {
                var arr = buttons as List<object>;
                if (arr != null)
                    foreach (var item in arr)
                    {
                        var bo = item as Dictionary<string, object>;
                        if (bo == null) continue;
                        var b = new PhoneParameters.ButtonRecess();
                        var s2 = new HashSet<string>();
                        b.XMm = Num(bo, s2, b.XMm, "x_mm", "x");
                        b.YMm = Num(bo, s2, b.YMm, "y_mm", "y");
                        b.ZMm = Num(bo, s2, b.ZMm, "z_mm", "z");
                        b.WidthMm = Num(bo, s2, b.WidthMm, "width_mm", "width");
                        b.HeightMm = Num(bo, s2, b.HeightMm, "height_mm", "height");
                        b.DepthMm = Num(bo, s2, b.DepthMm, "depth_mm", "depth");
                        p.Buttons.Add(b);
                    }
            }

            // antenna slits (S10)
            object antennas;
            if (TryGet(o, seen, out antennas, "antenna_slits", "antennas"))
            {
                var arr = antennas as List<object>;
                if (arr != null)
                    foreach (var item in arr)
                    {
                        var ao = item as Dictionary<string, object>;
                        if (ao == null) continue;
                        var a = new PhoneParameters.AntennaSlit();
                        var s2 = new HashSet<string>();
                        a.XMm = Num(ao, s2, a.XMm, "x_mm", "x");
                        a.YMm = Num(ao, s2, a.YMm, "y_mm", "y");
                        a.ZMm = Num(ao, s2, a.ZMm, "z_mm", "z");
                        a.LengthMm = Num(ao, s2, a.LengthMm, "length_mm", "length");
                        a.WidthMm = Num(ao, s2, a.WidthMm, "width_mm", "width");
                        a.DepthMm = Num(ao, s2, a.DepthMm, "depth_mm", "depth");
                        a.OnFlank = Bool(ao, s2, a.OnFlank, "on_flank");
                        p.AntennaSlits.Add(a);
                    }
            }

            // mic/sensor pinholes (S11)
            object pins;
            if (TryGet(o, seen, out pins, "pin_holes", "pinholes"))
            {
                var arr = pins as List<object>;
                if (arr != null)
                    foreach (var item in arr)
                    {
                        var po = item as Dictionary<string, object>;
                        if (po == null) continue;
                        var ph = new PhoneParameters.PinHole();
                        var s2 = new HashSet<string>();
                        ph.XMm = Num(po, s2, ph.XMm, "x_mm", "x");
                        ph.YMm = Num(po, s2, ph.YMm, "y_mm", "y");
                        ph.DiameterMm = Num(po, s2, ph.DiameterMm, "diameter_mm", "diameter", "d");
                        ph.Through = Bool(po, s2, ph.Through, "through");
                        ph.DepthMm = Num(po, s2, ph.DepthMm, "depth_mm", "depth");
                        ph.OnCurvedBack = Bool(po, s2, ph.OnCurvedBack, "on_curved_back");
                        p.PinHoles.Add(ph);
                    }
            }

            // surface unknown top-level keys as warnings (typo guard — not fatal).
            foreach (var k in o.Keys)
                if (!seen.Contains(k.ToLowerInvariant()))
                    r.Warnings.Add("unknown spec key ignored: '" + k + "'");
        }

        /// <summary>Replace-not-append: clear each baseline list whose key (or alias) the patch
        /// supplies, mirroring the alias sets Bind consults for those blocks.</summary>
        private static void ClearSuppliedLists(Dictionary<string, object> o, PhoneParameters p)
        {
            if (HasKey(o, "holes")) p.Holes.Clear();
            if (HasKey(o, "ports")) p.Ports.Clear();
            if (HasKey(o, "buttons")) p.Buttons.Clear();
            if (HasKey(o, "antenna_slits", "antennas")) p.AntennaSlits.Clear();
            if (HasKey(o, "pin_holes", "pinholes")) p.PinHoles.Clear();
        }

        private static bool HasKey(Dictionary<string, object> o, params string[] keys)
        {
            foreach (var k in keys)
                foreach (var kv in o)
                    if (string.Equals(kv.Key, k, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // -------------------------------------------------------------------
        // Typed getters (case-insensitive, synonym-tolerant). Each marks every
        // alias it consults as 'seen' so the unknown-key warning stays accurate.
        // -------------------------------------------------------------------
        private static bool TryGet(Dictionary<string, object> o, HashSet<string> seen, out object val, params string[] keys)
        {
            foreach (var k in keys)
            {
                string lk = k.ToLowerInvariant();
                seen.Add(lk);
                foreach (var kv in o)
                    if (string.Equals(kv.Key, k, StringComparison.OrdinalIgnoreCase))
                    { val = kv.Value; return true; }
            }
            val = null;
            return false;
        }

        private static double Num(Dictionary<string, object> o, HashSet<string> seen, double dflt, params string[] keys)
        {
            object v;
            if (!TryGet(o, seen, out v, keys) || v == null) return dflt;
            if (v is double) return (double)v;
            // tolerate a numeric string — but on net472 TryParse also accepts "NaN"/"Infinity",
            // which sail through Validate's </> range checks and later break the JSON writer.
            double d;
            if (v is string && double.TryParse((string)v, NumberStyles.Any, Inv, out d)
                && !double.IsNaN(d) && !double.IsInfinity(d)) return d;
            return dflt;
        }

        private static bool Bool(Dictionary<string, object> o, HashSet<string> seen, bool dflt, params string[] keys)
        {
            object v;
            if (!TryGet(o, seen, out v, keys) || v == null) return dflt;
            if (v is bool) return (bool)v;
            if (v is string)
            {
                string s = ((string)v).Trim().ToLowerInvariant();
                if (s == "true" || s == "1" || s == "yes") return true;
                if (s == "false" || s == "0" || s == "no") return false;
            }
            if (v is double) return Math.Abs((double)v) > 1e-9;
            return dflt;
        }

        private static string Str(Dictionary<string, object> o, HashSet<string> seen, string dflt, params string[] keys)
        {
            object v;
            if (!TryGet(o, seen, out v, keys) || v == null) return dflt;
            return v as string ?? dflt;
        }

        // -------------------------------------------------------------------
        // Minimal recursive-descent JSON reader. Returns:
        //   object  -> Dictionary<string,object>
        //   array   -> List<object>
        //   string  -> string
        //   number  -> double
        //   bool    -> bool
        //   null    -> null
        // -------------------------------------------------------------------
        private class JsonReader
        {
            private readonly string _s;
            private int _i;

            public JsonReader(string s) { _s = s; _i = 0; }
            public bool AtEnd { get { return _i >= _s.Length; } }

            public void SkipWs()
            {
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (c == ' ' || c == '\t' || c == '\r' || c == '\n') _i++;
                    else break;
                }
            }

            public object ParseValue()
            {
                SkipWs();
                if (_i >= _s.Length) throw new Exception("unexpected end of input");
                char c = _s[_i];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': case 'f': return ParseBool();
                    case 'n': Expect("null"); return null;
                    default: return ParseNumber();
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                var d = new Dictionary<string, object>();
                _i++; // {
                SkipWs();
                if (_i < _s.Length && _s[_i] == '}') { _i++; return d; }
                while (true)
                {
                    SkipWs();
                    if (_i >= _s.Length || _s[_i] != '"') throw new Exception("expected string key at " + _i);
                    string key = ParseString();
                    SkipWs();
                    if (_i >= _s.Length || _s[_i] != ':') throw new Exception("expected ':' at " + _i);
                    _i++;
                    object val = ParseValue();
                    d[key] = val;
                    SkipWs();
                    if (_i >= _s.Length) throw new Exception("unterminated object");
                    char c = _s[_i++];
                    if (c == ',') continue;
                    if (c == '}') break;
                    throw new Exception("expected ',' or '}' at " + (_i - 1));
                }
                return d;
            }

            private List<object> ParseArray()
            {
                var list = new List<object>();
                _i++; // [
                SkipWs();
                if (_i < _s.Length && _s[_i] == ']') { _i++; return list; }
                while (true)
                {
                    object val = ParseValue();
                    list.Add(val);
                    SkipWs();
                    if (_i >= _s.Length) throw new Exception("unterminated array");
                    char c = _s[_i++];
                    if (c == ',') continue;
                    if (c == ']') break;
                    throw new Exception("expected ',' or ']' at " + (_i - 1));
                }
                return list;
            }

            private string ParseString()
            {
                var sb = new StringBuilder();
                _i++; // opening "
                while (_i < _s.Length)
                {
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c == '\\')
                    {
                        if (_i >= _s.Length) break;
                        char e = _s[_i++];
                        switch (e)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                if (_i + 4 <= _s.Length)
                                {
                                    string hex = _s.Substring(_i, 4);
                                    int code;
                                    if (int.TryParse(hex, NumberStyles.HexNumber, Inv, out code))
                                        sb.Append((char)code);
                                    _i += 4;
                                }
                                break;
                            default: sb.Append(e); break;
                        }
                    }
                    else sb.Append(c);
                }
                throw new Exception("unterminated string");
            }

            private bool ParseBool()
            {
                if (_s[_i] == 't') { Expect("true"); return true; }
                Expect("false"); return false;
            }

            private double ParseNumber()
            {
                int start = _i;
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E') _i++;
                    else break;
                }
                string tok = _s.Substring(start, _i - start);
                double d;
                if (!double.TryParse(tok, NumberStyles.Any, Inv, out d))
                    throw new Exception("invalid number '" + tok + "' at " + start);
                return d;
            }

            private void Expect(string lit)
            {
                if (_i + lit.Length > _s.Length || _s.Substring(_i, lit.Length) != lit)
                    throw new Exception("expected '" + lit + "' at " + _i);
                _i += lit.Length;
            }
        }
    }
}
