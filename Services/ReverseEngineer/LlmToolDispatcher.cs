using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;

#if V251
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer
{
    /// <summary>
    /// Stage 5: Dispatch LLM "tool_use" requests to ModificationService primitives.
    ///
    /// Pure JSON in / JSON out — does NOT depend on the Anthropic SDK. The caller
    /// supplies (tool_name, input_json) and receives:
    ///   { "success": bool, "error": string|null, "result": {...} }
    ///
    /// Hand-rolled JSON parsing matches the FeatureGraphJsonWriter style (no
    /// NuGet dependencies, IronPython-friendly). Only the small subset we need
    /// for tool inputs is supported: object literals at the top level with
    /// string / number / bool / number[] / null leaves.
    /// </summary>
    // @lat: [[reverse-engineer#LlmToolDispatcher]]
    public static class LlmToolDispatcher
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// Dispatch one tool_use call. designBody/graph are the targets;
        /// toolName matches LlmToolRegistry.GetAllTools()[i].Name; inputJson
        /// is the JSON object the LLM emitted for "input".
        ///
        /// Returns a JSON envelope string. Never throws — all errors are
        /// surfaced via {"success": false, "error": "..."}.
        /// </summary>
        public static string Dispatch(DesignBody designBody, FeatureGraph graph, string toolName, string inputJson)
        {
            if (string.IsNullOrEmpty(toolName))
                return Envelope(false, "toolName is null/empty", null);
            // generate_phone/set_camera_height are session/generation tools that build their own
            // body via SessionContext — they don't require a pre-existing designBody.
            if (designBody == null && toolName != "get_feature_graph" && toolName != "find_features_by_type"
                && toolName != "generate_phone" && toolName != "generate_phone_from_spec"
                && toolName != "set_camera_height")
                return Envelope(false, "designBody is null (required for modification tools)", null);

            Dictionary<string, object> args;
            try
            {
                args = ParseObject(inputJson ?? "{}");
            }
            catch (Exception ex)
            {
                return Envelope(false, "input JSON parse failed: " + ex.Message, null);
            }

            try
            {
                switch (toolName)
                {
                    case "change_wall_thickness":
                    {
                        string wallId = GetString(args, "wall_id");
                        double newT = GetDouble(args, "new_thickness_mm");
                        var r = ModificationService.ChangeWallThickness(designBody, graph, wallId, newT);
                        return EnvelopeFromResult(r);
                    }
                    case "change_hole_diameter":
                    {
                        string holeId = GetString(args, "hole_id");
                        double newD = GetDouble(args, "new_diameter_mm");
                        var r = ModificationService.ChangeHoleDiameter(designBody, graph, holeId, newD);
                        return EnvelopeFromResult(r);
                    }
                    case "change_fillet_radius":
                    {
                        string chainId = GetString(args, "fillet_chain_id");
                        double newR = GetDouble(args, "new_radius_mm");
                        var r = ModificationService.ChangeFilletRadius(designBody, graph, chainId, newR);
                        return EnvelopeFromResult(r);
                    }
                    case "change_boss_diameter":
                    {
                        string bossId = GetString(args, "boss_id");
                        double newD = GetDouble(args, "new_diameter_mm");
                        var r = ModificationService.ChangeBossDiameter(designBody, graph, bossId, newD);
                        return EnvelopeFromResult(r);
                    }
                    case "change_boss_height":
                    {
                        string bossId = GetString(args, "boss_id");
                        double newH = GetDouble(args, "new_height_mm");
                        var r = ModificationService.ChangeBossHeight(designBody, graph, bossId, newH);
                        return EnvelopeFromResult(r);
                    }
                    case "add_boss":
                    {
                        double[] pos = GetDoubleArray(args, "position_mm", 3);
                        double dia = GetDouble(args, "diameter_mm");
                        double h = GetDouble(args, "height_mm");
                        var r = ModificationService.AddBoss(designBody, pos, dia, h);
                        return EnvelopeFromResult(r);
                    }
                    case "add_hole":
                    {
                        double[] pos = GetDoubleArray(args, "position_mm", 3);
                        double dia = GetDouble(args, "diameter_mm");
                        bool through = GetBool(args, "through");
                        double depth = GetDoubleOrDefault(args, "depth_mm", 0.0);
                        var r = ModificationService.AddHole(designBody, pos, dia, through, depth);
                        return EnvelopeFromResult(r);
                    }
                    case "add_slit":
                    {
                        double[] pos = GetDoubleArray(args, "position_mm", 3);
                        double w = GetDouble(args, "width_mm");
                        double L = GetDouble(args, "length_mm");
                        double d = GetDouble(args, "depth_mm");
                        double[] axis = args.ContainsKey("orientation_axis") ? GetDoubleArray(args, "orientation_axis", 3) : null;
                        var r = ModificationService.AddSlit(designBody, pos, w, L, d, axis);
                        return EnvelopeFromResult(r);
                    }
                    case "add_pocket":
                    {
                        double[] pos = GetDoubleArray(args, "position_mm", 3);
                        double w = GetDouble(args, "width_mm");
                        double L = GetDouble(args, "length_mm");
                        double d = GetDouble(args, "depth_mm");
                        var r = ModificationService.AddPocket(designBody, pos, w, L, d);
                        return EnvelopeFromResult(r);
                    }
                    case "add_rib":
                    {
                        double[] s = GetDoubleArray(args, "start_position_mm", 3);
                        double[] e = GetDoubleArray(args, "end_position_mm", 3);
                        double w = GetDouble(args, "width_mm");
                        double h = GetDouble(args, "height_mm");
                        var r = ModificationService.AddRib(designBody, s, e, w, h);
                        return EnvelopeFromResult(r);
                    }
                    case "add_chamfer":
                    {
                        double w = GetDouble(args, "width_mm");
                        string filter = GetString(args, "edge_filter");
                        var r = ModificationService.AddChamfer(designBody, w, filter);
                        return EnvelopeFromResult(r);
                    }
                    case "add_hole_pattern":
                    {
                        double[] center = GetDoubleArray(args, "center_mm", 3);
                        double[] axis = args.ContainsKey("axis_direction") ? GetDoubleArray(args, "axis_direction", 3) : null;
                        string pt = GetString(args, "pattern_type");
                        int count = (int)GetDoubleOrDefault(args, "count", 0);
                        double spacing = GetDoubleOrDefault(args, "spacing", 0.0);
                        double dia = GetDouble(args, "diameter_mm");
                        bool through = GetBool(args, "through");
                        int rows = (int)GetDoubleOrDefault(args, "rows", 0);
                        int cols = (int)GetDoubleOrDefault(args, "cols", 0);
                        double rowSp = GetDoubleOrDefault(args, "row_spacing", 0.0);
                        double colSp = GetDoubleOrDefault(args, "col_spacing", 0.0);
                        double depth = GetDoubleOrDefault(args, "depth_mm", 0.0);
                        var r = ModificationService.AddHolePattern(
                            designBody, center, axis, pt, count, spacing, dia, through,
                            rows, cols, rowSp, colSp, depth);
                        return EnvelopeFromResult(r);
                    }
                    case "remove_hole":
                    {
                        string holeId = GetString(args, "hole_id");
                        var r = ModificationService.RemoveHole(designBody, graph, holeId);
                        return EnvelopeFromResult(r);
                    }
                    case "move_hole":
                    {
                        string holeId = GetString(args, "hole_id");
                        double[] newPos = GetDoubleArray(args, "new_position_mm", 3);
                        var r = ModificationService.MoveHole(designBody, graph, holeId, newPos);
                        return EnvelopeFromResult(r);
                    }
                    case "move_boss":
                    {
                        string bossId = GetString(args, "boss_id");
                        double[] newPos = GetDoubleArray(args, "new_position_mm", 3);
                        var r = ModificationService.MoveBoss(designBody, graph, bossId, newPos);
                        return EnvelopeFromResult(r);
                    }
                    case "rotate_boss":
                    {
                        string bossId = GetString(args, "boss_id");
                        double[] axisPt = GetDoubleArray(args, "axis_point_mm", 3);
                        double[] axisDir = GetDoubleArray(args, "axis_direction", 3);
                        double angle = GetDouble(args, "angle_deg");
                        var r = ModificationService.RotateBoss(designBody, graph, bossId, axisPt, axisDir, angle);
                        return EnvelopeFromResult(r);
                    }
                    case "rotate_hole":
                    {
                        string holeId = GetString(args, "hole_id");
                        double[] axisPt = GetDoubleArray(args, "axis_point_mm", 3);
                        double[] axisDir = GetDoubleArray(args, "axis_direction", 3);
                        double angle = GetDouble(args, "angle_deg");
                        var r = ModificationService.RotateHole(designBody, graph, holeId, axisPt, axisDir, angle);
                        return EnvelopeFromResult(r);
                    }
                    case "mirror_feature":
                    {
                        string featId = GetString(args, "feature_id");
                        double[] mn = GetDoubleArray(args, "mirror_plane_normal", 3);
                        double[] mo = GetDoubleArray(args, "mirror_plane_origin", 3);
                        var r = ModificationService.MirrorFeature(designBody, graph, featId, mn, mo);
                        return EnvelopeFromResult(r);
                    }

                    // ---- read-only -----------------------------------------
                    case "get_feature_graph":
                    {
                        if (graph == null)
                            return Envelope(false, "graph is null", null);
                        string graphJson = FeatureGraphJsonWriter.ToJson(graph);
                        // result is the FeatureGraph object literal verbatim.
                        return Envelope(true, null, graphJson);
                    }
                    case "find_features_by_type":
                    {
                        if (graph == null)
                            return Envelope(false, "graph is null", null);
                        string ftype = GetString(args, "feature_type");
                        double minV = GetDoubleOrDefault(args, "min_value", double.NegativeInfinity);
                        double maxV = GetDoubleOrDefault(args, "max_value", double.PositiveInfinity);
                        var ids = FindFeaturesByType(graph, ftype, minV, maxV);
                        return Envelope(true, null, BuildIdsResultJson(ftype, ids));
                    }

                    // ---- from-scratch generation (P7) ----------------------
                    case "generate_phone":
                    {
                        var p = new Models.ReverseEngineer.PhoneParameters();
                        p.LengthMm = GetDoubleOrDefault(args, "length_mm", p.LengthMm);
                        p.WidthMm = GetDoubleOrDefault(args, "width_mm", p.WidthMm);
                        p.ThicknessMm = GetDoubleOrDefault(args, "thickness_mm", p.ThicknessMm);
                        p.CornerRadiusMm = GetDoubleOrDefault(args, "corner_radius_mm", p.CornerRadiusMm);
                        p.HollowWallMm = GetDoubleOrDefault(args, "wall_mm", p.HollowWallMm);
                        double camH = GetDoubleOrDefault(args, "camera_bump_mm", -1);
                        if (camH > 0)
                        {
                            p.Camera = new Models.ReverseEngineer.PhoneParameters.CameraIsland();
                            p.Camera.HeightMm = camH;
                        }
                        System.Collections.Generic.List<string> verr;
                        if (!p.Validate(out verr))
                            return Envelope(false, "invalid spec: " + string.Join("; ", verr.ToArray()), null);
                        string genErr = Mcp.SessionContext.Instance.GeneratePhone(p);
                        if (genErr != null) return Envelope(false, genErr, null);
                        return Envelope(true, null, "{\"generated\": true, \"params\": \"" +
                            p.ToString().Replace("\"", "'") + "\"}");
                    }
                    case "generate_phone_from_spec":
                    {
                        // The LLM emits a full structured JSON spec; SpecParser binds + validates it
                        // (the same Validate() generate_phone runs, plus the curved/oriented surface).
                        string specJson = GetString(args, "spec_json");
                        var pr = Generation.SpecParser.Parse(specJson);
                        if (!pr.Success)
                        {
                            string why = (pr.Errors != null && pr.Errors.Count > 0)
                                ? string.Join("; ", pr.Errors.ToArray()) : "spec rejected";
                            return Envelope(false, "invalid spec: " + why, null);
                        }
                        string genErr2 = Mcp.SessionContext.Instance.GeneratePhone(pr.Params);
                        if (genErr2 != null) return Envelope(false, genErr2, null);
                        string warnJson = "[]";
                        if (pr.Warnings != null && pr.Warnings.Count > 0)
                        {
                            var wb = new StringBuilder("[");
                            for (int i = 0; i < pr.Warnings.Count; i++)
                            {
                                if (i > 0) wb.Append(", ");
                                wb.Append("\"").Append(EscapeStr(pr.Warnings[i])).Append("\"");
                            }
                            wb.Append("]");
                            warnJson = wb.ToString();
                        }
                        return Envelope(true, null, "{\"generated\": true, \"params\": \"" +
                            pr.Params.ToString().Replace("\"", "'") + "\", \"warnings\": " + warnJson + "}");
                    }
                    case "set_camera_height":
                    {
                        var sc = Mcp.SessionContext.Instance;
                        if (sc.Params == null)
                            return Envelope(false, "no generated phone in session (call generate_phone first)", null);
                        double newH = GetDouble(args, "height_mm");
                        // Edit the param (the source of truth) + regenerate — the reconciliation
                        // channel (P7): geometry never outruns params. Handle-based targeting is
                        // implicit because regenerate rebuilds the camera at its anchor.
                        if (sc.Params.Camera == null)
                            sc.Params.Camera = new Models.ReverseEngineer.PhoneParameters.CameraIsland();
                        sc.Params.Camera.HeightMm = newH;
                        string e2 = sc.GeneratePhone(sc.Params);
                        if (e2 != null) return Envelope(false, e2, null);
                        return Envelope(true, null, "{\"camera_height_mm\": " + newH + ", \"regenerated\": true}");
                    }

                    default:
                        return Envelope(false, "Unknown toolName: " + toolName, null);
                }
            }
            catch (Exception ex)
            {
                return Envelope(false, "Dispatch threw: " + ex.Message, null);
            }
        }

        // ----------------------------------------------------------------
        // Result envelope builders
        // ----------------------------------------------------------------
        private static string Envelope(bool success, string error, string resultJsonOrNull)
        {
            var sb = new StringBuilder();
            sb.Append("{\"success\": ").Append(success ? "true" : "false");
            sb.Append(", \"error\": ");
            if (string.IsNullOrEmpty(error)) sb.Append("null");
            else sb.Append("\"").Append(EscapeStr(error)).Append("\"");
            sb.Append(", \"result\": ");
            if (string.IsNullOrEmpty(resultJsonOrNull)) sb.Append("null");
            else sb.Append(resultJsonOrNull);
            sb.Append("}");
            return sb.ToString();
        }

        private static string EnvelopeFromResult(ModificationResult r)
        {
            if (r == null) return Envelope(false, "ModificationResult is null", null);
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"operation\": \"").Append(EscapeStr(r.Operation ?? "")).Append("\", ");
            sb.Append("\"modified_face_indices\": [");
            if (r.ModifiedFaceIndices != null)
            {
                for (int i = 0; i < r.ModifiedFaceIndices.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(r.ModifiedFaceIndices[i].ToString(Inv));
                }
            }
            sb.Append("], ");
            int newEdges = (r.NewlyCreatedEdges != null) ? r.NewlyCreatedEdges.Count : 0;
            sb.Append("\"new_edge_count\": ").Append(newEdges.ToString(Inv));
            sb.Append("}");
            return Envelope(r.Success, r.Success ? null : (r.ErrorMessage ?? "(no message)"), sb.ToString());
        }

        // ----------------------------------------------------------------
        // find_features_by_type implementation
        // ----------------------------------------------------------------
        private static List<string> FindFeaturesByType(FeatureGraph graph, string ftype, double minV, double maxV)
        {
            var ids = new List<string>();
            if (graph == null || string.IsNullOrEmpty(ftype)) return ids;
            string t = ftype.Trim().ToLowerInvariant();
            if (t == "hole" && graph.Holes != null)
            {
                foreach (var h in graph.Holes)
                {
                    if (h.DiameterMm >= minV && h.DiameterMm <= maxV) ids.Add(h.Id);
                }
            }
            else if (t == "boss" && graph.Bosses != null)
            {
                foreach (var b in graph.Bosses)
                {
                    if (b.DiameterMm >= minV && b.DiameterMm <= maxV) ids.Add(b.Id);
                }
            }
            else if (t == "wall" && graph.Walls != null)
            {
                foreach (var w in graph.Walls)
                {
                    if (w.ThicknessMm >= minV && w.ThicknessMm <= maxV) ids.Add(w.Id);
                }
            }
            else if ((t == "fillet_chain" || t == "filletchain") && graph.FilletChains != null)
            {
                foreach (var fc in graph.FilletChains)
                {
                    if (fc.RadiusMm >= minV && fc.RadiusMm <= maxV) ids.Add(fc.Id);
                }
            }
            else if (t == "slit" && graph.Slits != null)
            {
                foreach (var s in graph.Slits)
                {
                    // Slits have no single primary dim; include all (min/max ignored).
                    ids.Add(s.Id);
                }
            }
            return ids;
        }

        private static string BuildIdsResultJson(string ftype, List<string> ids)
        {
            var sb = new StringBuilder();
            sb.Append("{\"feature_type\": \"").Append(EscapeStr(ftype ?? "")).Append("\", \"ids\": [");
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append("\"").Append(EscapeStr(ids[i])).Append("\"");
            }
            sb.Append("], \"count\": ").Append(ids.Count.ToString(Inv)).Append("}");
            return sb.ToString();
        }

        // ----------------------------------------------------------------
        // Argument extraction
        // ----------------------------------------------------------------
        private static string GetString(Dictionary<string, object> args, string key)
        {
            if (!args.ContainsKey(key) || args[key] == null)
                throw new ArgumentException("missing required string field: " + key);
            return args[key].ToString();
        }

        private static double GetDouble(Dictionary<string, object> args, string key)
        {
            if (!args.ContainsKey(key) || args[key] == null)
                throw new ArgumentException("missing required number field: " + key);
            object v = args[key];
            if (v is double d) return d;
            if (v is long l) return (double)l;
            double parsed;
            if (double.TryParse(v.ToString(), NumberStyles.Float, Inv, out parsed)) return parsed;
            throw new ArgumentException("field " + key + " is not a number: " + v);
        }

        private static double GetDoubleOrDefault(Dictionary<string, object> args, string key, double dflt)
        {
            if (!args.ContainsKey(key) || args[key] == null) return dflt;
            return GetDouble(args, key);
        }

        private static bool GetBool(Dictionary<string, object> args, string key)
        {
            if (!args.ContainsKey(key) || args[key] == null)
                throw new ArgumentException("missing required bool field: " + key);
            object v = args[key];
            if (v is bool b) return b;
            string s = v.ToString().Trim().ToLowerInvariant();
            if (s == "true") return true;
            if (s == "false") return false;
            throw new ArgumentException("field " + key + " is not a bool: " + v);
        }

        private static double[] GetDoubleArray(Dictionary<string, object> args, string key, int expectedLen)
        {
            if (!args.ContainsKey(key) || args[key] == null)
                throw new ArgumentException("missing required array field: " + key);
            object v = args[key];
            if (!(v is List<object> list))
                throw new ArgumentException("field " + key + " is not an array");
            if (list.Count != expectedLen)
                throw new ArgumentException("field " + key + " expected length " + expectedLen + ", got " + list.Count);
            var arr = new double[expectedLen];
            for (int i = 0; i < expectedLen; i++)
            {
                object e = list[i];
                if (e == null) throw new ArgumentException("field " + key + "[" + i + "] is null");
                if (e is double d) { arr[i] = d; continue; }
                if (e is long l) { arr[i] = (double)l; continue; }
                double parsed;
                if (double.TryParse(e.ToString(), NumberStyles.Float, Inv, out parsed)) { arr[i] = parsed; continue; }
                throw new ArgumentException("field " + key + "[" + i + "] is not a number: " + e);
            }
            return arr;
        }

        // ----------------------------------------------------------------
        // Tiny JSON parser (object / array / string / number / bool / null).
        // Sufficient for LLM tool inputs; not a full RFC 8259 implementation.
        // ----------------------------------------------------------------
        private static Dictionary<string, object> ParseObject(string json)
        {
            int idx = 0;
            SkipWs(json, ref idx);
            if (idx >= json.Length || json[idx] != '{')
                throw new FormatException("expected '{' at position " + idx);
            object o = ParseValue(json, ref idx);
            if (!(o is Dictionary<string, object> dict))
                throw new FormatException("top-level value is not an object");
            return dict;
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) throw new FormatException("unexpected EOF");
            char c = s[i];
            if (c == '{') return ParseObjectInner(s, ref i);
            if (c == '[') return ParseArrayInner(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (c == 't' || c == 'f') return ParseBool(s, ref i);
            if (c == 'n') { ParseNull(s, ref i); return null; }
            if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber(s, ref i);
            throw new FormatException("unexpected char '" + c + "' at " + i);
        }

        private static Dictionary<string, object> ParseObjectInner(string s, ref int i)
        {
            var dict = new Dictionary<string, object>();
            if (s[i] != '{') throw new FormatException("expected '{'");
            i++;
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return dict; }
            while (true)
            {
                SkipWs(s, ref i);
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("expected ':' at " + i);
                i++;
                object val = ParseValue(s, ref i);
                dict[key] = val;
                SkipWs(s, ref i);
                if (i >= s.Length) throw new FormatException("unexpected EOF in object");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return dict; }
                throw new FormatException("expected ',' or '}' at " + i);
            }
        }

        private static List<object> ParseArrayInner(string s, ref int i)
        {
            var list = new List<object>();
            if (s[i] != '[') throw new FormatException("expected '['");
            i++;
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return list; }
            while (true)
            {
                object v = ParseValue(s, ref i);
                list.Add(v);
                SkipWs(s, ref i);
                if (i >= s.Length) throw new FormatException("unexpected EOF in array");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return list; }
                throw new FormatException("expected ',' or ']' at " + i);
            }
        }

        private static string ParseString(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException("expected '\"' at " + i);
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '"') { i++; return sb.ToString(); }
                if (c == '\\')
                {
                    if (i + 1 >= s.Length) throw new FormatException("bad escape at EOF");
                    char esc = s[i + 1];
                    switch (esc)
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
                            if (i + 5 >= s.Length) throw new FormatException("bad \\u escape");
                            string hex = s.Substring(i + 2, 4);
                            int code;
                            if (!int.TryParse(hex, NumberStyles.HexNumber, Inv, out code))
                                throw new FormatException("bad \\u escape: " + hex);
                            sb.Append((char)code);
                            i += 4;
                            break;
                        default: throw new FormatException("unknown escape \\" + esc);
                    }
                    i += 2;
                    continue;
                }
                sb.Append(c);
                i++;
            }
            throw new FormatException("unterminated string");
        }

        private static object ParseNumber(string s, ref int i)
        {
            int start = i;
            if (s[i] == '-') i++;
            while (i < s.Length && ((s[i] >= '0' && s[i] <= '9') || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '+' || s[i] == '-'))
                i++;
            string token = s.Substring(start, i - start);
            // Prefer long for integer-looking values; fall back to double.
            if (token.IndexOfAny(new[] { '.', 'e', 'E' }) < 0)
            {
                long l;
                if (long.TryParse(token, NumberStyles.Integer, Inv, out l)) return l;
            }
            double d;
            if (double.TryParse(token, NumberStyles.Float, Inv, out d)) return d;
            throw new FormatException("bad number: " + token);
        }

        private static bool ParseBool(string s, ref int i)
        {
            if (i + 4 <= s.Length && s.Substring(i, 4) == "true") { i += 4; return true; }
            if (i + 5 <= s.Length && s.Substring(i, 5) == "false") { i += 5; return false; }
            throw new FormatException("bad bool at " + i);
        }

        private static void ParseNull(string s, ref int i)
        {
            if (i + 4 <= s.Length && s.Substring(i, 4) == "null") { i += 4; return; }
            throw new FormatException("bad null at " + i);
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') i++;
                else break;
            }
        }

        private static string EscapeStr(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }
    }
}
