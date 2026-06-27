using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
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
    /// Stage 5 wiring: Anthropic Claude API client that runs the agentic
    /// tool-use loop against api.anthropic.com using the tools defined in
    /// LlmToolRegistry and dispatched through LlmToolDispatcher.
    ///
    /// Synchronous (HttpWebRequest) — no Task / async needed. Pure hand-rolled
    /// JSON encoding/decoding (reuses the dispatcher's tiny parser via the
    /// internal ParserShim below) so we avoid any NuGet dependency in the
    /// SpaceClaim Add-In.
    ///
    /// Prompt caching (cache_control: ephemeral) is applied to:
    ///   - the tools array (last tool gets the cache breakpoint)
    ///   - the system prompt text block
    /// which lets repeated turns within a single conversation skip re-parsing
    /// the tool schema + system context.
    ///
    /// Constraints:
    ///   - .NET 4.7.2 → ServicePointManager.SecurityProtocol must include Tls12.
    ///   - HttpClient lifetime in 4.7.2 is fragile; we use HttpWebRequest per
    ///     call, which is fine for a low-frequency interactive command.
    /// </summary>
    // @lat: [[reverse-engineer#LlmClient]]
    public class LlmClient
    {
        private const string ApiEndpoint = "https://api.anthropic.com/v1/messages";
        private const string AnthropicVersion = "2023-06-01";
        private const int MaxTurns = 20;     // safety cap on tool_use loop iterations
        private const int MaxTokens = 4096;

        private readonly string _apiKey;
        private readonly string _model;

        public LlmClient(string apiKey, string model = "claude-sonnet-4-6")
        {
            if (string.IsNullOrEmpty(apiKey))
                throw new ArgumentException("apiKey is required (ANTHROPIC_API_KEY)");
            _apiKey = apiKey;
            _model = string.IsNullOrEmpty(model) ? "claude-sonnet-4-6" : model;

            // .NET 4.7.2 default is SSL3/TLS1.0 — force Tls12+ for HTTPS to
            // api.anthropic.com (otherwise we get "could not create SSL/TLS").
            try
            {
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            }
            catch
            {
                // Best-effort; some sandboxed environments lock SecurityProtocol.
            }
        }

        /// <summary>
        /// Run a single user turn against Claude with the full tool-use loop.
        /// Returns the final assistant text (or an error-prefixed string on
        /// failure — never throws).
        /// </summary>
        public string RunConversation(DesignBody body, FeatureGraph graph, string userMessage)
        {
            if (string.IsNullOrEmpty(userMessage))
                return "[ERROR] userMessage is empty";

            // Conversation messages live as a list of raw JSON content-block
            // sequences (per message). We keep them as already-serialized JSON
            // strings to make round-tripping content blocks (text + tool_use +
            // tool_result with original input verbatim) trivial.
            var messages = new List<MessageJson>();
            messages.Add(new MessageJson("user", BuildUserTextContent(userMessage)));

            string systemPrompt = BuildSystemPrompt(graph);
            string toolsJson = BuildToolsJsonWithCache();

            string lastAssistantText = string.Empty;
            for (int turn = 0; turn < MaxTurns; turn++)
            {
                string requestJson = BuildRequestJson(systemPrompt, toolsJson, messages);
                string responseJson;
                try
                {
                    responseJson = PostMessages(requestJson);
                }
                catch (Exception ex)
                {
                    return "[HTTP ERROR] " + ex.Message;
                }

                // Parse top-level response: { stop_reason, content: [...] }
                Dictionary<string, object> resp;
                try
                {
                    resp = ParserShim.ParseObject(responseJson);
                }
                catch (Exception ex)
                {
                    return "[PARSE ERROR] " + ex.Message + "\nRaw: " + Truncate(responseJson, 2000);
                }

                if (resp.ContainsKey("error") && resp["error"] != null)
                {
                    return "[API ERROR] " + ParserShim.Stringify(resp["error"]);
                }
                if (resp.ContainsKey("type") && resp["type"] is string respType && respType == "error")
                {
                    return "[API ERROR] " + Truncate(responseJson, 2000);
                }

                string stopReason = resp.ContainsKey("stop_reason") ? (resp["stop_reason"] as string) ?? "" : "";
                var content = resp.ContainsKey("content") ? resp["content"] as List<object> : null;
                if (content == null) return "[ERROR] response has no 'content' array.\nRaw: " + Truncate(responseJson, 2000);

                // Re-append the assistant message (verbatim content blocks) to
                // the running conversation so the next request includes it.
                string assistantContentJson = ParserShim.SerializeListVerbatim(content);
                messages.Add(new MessageJson("assistant", assistantContentJson));

                // Collect any text blocks (display) + any tool_use blocks
                // (dispatch).
                var toolUses = new List<ToolUseBlock>();
                var assistantText = new StringBuilder();
                foreach (var blockObj in content)
                {
                    var block = blockObj as Dictionary<string, object>;
                    if (block == null) continue;
                    string btype = block.ContainsKey("type") ? block["type"] as string : null;
                    if (btype == "text")
                    {
                        string txt = block.ContainsKey("text") ? block["text"] as string : null;
                        if (!string.IsNullOrEmpty(txt))
                        {
                            if (assistantText.Length > 0) assistantText.Append("\n");
                            assistantText.Append(txt);
                        }
                    }
                    else if (btype == "tool_use")
                    {
                        var tu = new ToolUseBlock
                        {
                            Id = block.ContainsKey("id") ? block["id"] as string : "",
                            Name = block.ContainsKey("name") ? block["name"] as string : "",
                            InputJson = block.ContainsKey("input")
                                ? ParserShim.SerializeValue(block["input"])
                                : "{}"
                        };
                        toolUses.Add(tu);
                    }
                }
                if (assistantText.Length > 0)
                    lastAssistantText = assistantText.ToString();

                if (toolUses.Count == 0 || stopReason == "end_turn" || stopReason == "stop_sequence")
                {
                    // Conversation is complete.
                    return string.IsNullOrEmpty(lastAssistantText)
                        ? "(no assistant text — stop_reason=" + stopReason + ")"
                        : lastAssistantText;
                }

                // Dispatch each tool_use, build a single "user" message
                // containing the tool_result blocks (Anthropic spec).
                var resultBlocks = new StringBuilder();
                resultBlocks.Append("[");
                for (int i = 0; i < toolUses.Count; i++)
                {
                    if (i > 0) resultBlocks.Append(", ");
                    var t = toolUses[i];
                    string envelope = LlmToolDispatcher.Dispatch(body, graph, t.Name, t.InputJson);
                    // tool_result.content must be a string OR a list of content blocks.
                    // We send the JSON envelope as a string (cheaper, simpler).
                    resultBlocks.Append("{");
                    resultBlocks.Append("\"type\": \"tool_result\", ");
                    resultBlocks.Append("\"tool_use_id\": \"").Append(EscapeJson(t.Id)).Append("\", ");
                    resultBlocks.Append("\"content\": \"").Append(EscapeJson(envelope)).Append("\"");
                    resultBlocks.Append("}");
                }
                resultBlocks.Append("]");
                messages.Add(new MessageJson("user", resultBlocks.ToString()));

                // Loop and call the API again with the appended tool_result.
            }

            return string.IsNullOrEmpty(lastAssistantText)
                ? "[ERROR] tool-use loop exceeded MaxTurns=" + MaxTurns
                : lastAssistantText + "\n\n[WARN] truncated at MaxTurns=" + MaxTurns;
        }

        // ------------------------------------------------------------------
        // Request building
        // ------------------------------------------------------------------
        private string BuildSystemPrompt(FeatureGraph graph)
        {
            var sb = new StringBuilder();
            sb.Append("You are a CAD reverse-engineering assistant embedded in ");
            sb.Append("SpaceClaim. The user has a DesignBody whose FeatureGraph ");
            sb.Append("(walls, holes, bosses, fillet chains, slits, patterns, ");
            sb.Append("symmetries) is provided below. You can read it via ");
            sb.Append("get_feature_graph / find_features_by_type and modify the ");
            sb.Append("body via the change_*, add_*, remove_*, move_*, ");
            sb.Append("rotate_*, mirror_* tools. Use millimetres throughout. ");
            sb.Append("When the user asks for a change, plan a minimal sequence ");
            sb.Append("of tool calls; verify dimensions by reading the graph if ");
            sb.Append("uncertain. Report what you did in 1-3 sentences.\n\n");
            sb.Append("# Current FeatureGraph (as of conversation start)\n");
            if (graph == null)
            {
                sb.Append("(no graph — body not yet analyzed)\n");
            }
            else
            {
                try
                {
                    string graphJson = FeatureGraphJsonWriter.ToJson(graph);
                    sb.Append(graphJson);
                }
                catch (Exception ex)
                {
                    sb.Append("(graph serialization failed: ").Append(ex.Message).Append(")");
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Build the tools JSON array; attach cache_control:ephemeral to the
        /// last entry so the entire tools prefix is cached.
        /// </summary>
        private static string BuildToolsJsonWithCache()
        {
            var tools = LlmToolRegistry.GetAllTools();
            var sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < tools.Count; i++)
            {
                var t = tools[i];
                sb.Append("{");
                sb.Append("\"name\": \"").Append(EscapeJson(t.Name)).Append("\", ");
                sb.Append("\"description\": \"").Append(EscapeJson(t.Description)).Append("\", ");
                sb.Append("\"input_schema\": ").Append(t.InputSchemaJson);
                if (i == tools.Count - 1)
                {
                    // Cache breakpoint on the LAST tool — caches the entire
                    // tools array prefix.
                    sb.Append(", \"cache_control\": {\"type\": \"ephemeral\"}");
                }
                sb.Append("}");
                if (i < tools.Count - 1) sb.Append(", ");
            }
            sb.Append("]");
            return sb.ToString();
        }

        private string BuildRequestJson(string systemPrompt, string toolsJson, List<MessageJson> messages)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"model\": \"").Append(EscapeJson(_model)).Append("\", ");
            sb.Append("\"max_tokens\": ").Append(MaxTokens.ToString(CultureInfo.InvariantCulture)).Append(", ");

            // system as an array of content blocks → lets us attach
            // cache_control to the text block.
            sb.Append("\"system\": [");
            sb.Append("{\"type\": \"text\", \"text\": \"").Append(EscapeJson(systemPrompt)).Append("\", ");
            sb.Append("\"cache_control\": {\"type\": \"ephemeral\"}}");
            sb.Append("], ");

            sb.Append("\"tools\": ").Append(toolsJson).Append(", ");

            sb.Append("\"messages\": [");
            for (int i = 0; i < messages.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var m = messages[i];
                sb.Append("{");
                sb.Append("\"role\": \"").Append(EscapeJson(m.Role)).Append("\", ");
                sb.Append("\"content\": ").Append(m.ContentJson);
                sb.Append("}");
            }
            sb.Append("]");
            sb.Append("}");
            return sb.ToString();
        }

        private static string BuildUserTextContent(string userMessage)
        {
            var sb = new StringBuilder();
            sb.Append("[{\"type\": \"text\", \"text\": \"").Append(EscapeJson(userMessage)).Append("\"}]");
            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // HTTP POST (synchronous HttpWebRequest)
        // ------------------------------------------------------------------
        private string PostMessages(string requestJson)
        {
            var req = (HttpWebRequest)WebRequest.Create(ApiEndpoint);
            req.Method = "POST";
            req.ContentType = "application/json";
            req.Headers["x-api-key"] = _apiKey;
            req.Headers["anthropic-version"] = AnthropicVersion;
            req.Timeout = 180000; // 3 min — generous for tool-heavy turns.
            req.ReadWriteTimeout = 180000;

            byte[] body = Encoding.UTF8.GetBytes(requestJson);
            req.ContentLength = body.Length;
            using (var s = req.GetRequestStream())
            {
                s.Write(body, 0, body.Length);
            }

            try
            {
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var rs = resp.GetResponseStream())
                using (var sr = new StreamReader(rs, Encoding.UTF8))
                {
                    return sr.ReadToEnd();
                }
            }
            catch (WebException wex)
            {
                // Anthropic returns a JSON body on 4xx; surface it.
                if (wex.Response != null)
                {
                    using (var rs = wex.Response.GetResponseStream())
                    using (var sr = new StreamReader(rs, Encoding.UTF8))
                    {
                        string err = sr.ReadToEnd();
                        throw new Exception("HTTP " + ((HttpWebResponse)wex.Response).StatusCode + ": " + err, wex);
                    }
                }
                throw;
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private static string Truncate(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= n ? s : s.Substring(0, n) + "…";
        }

        private class MessageJson
        {
            public string Role;
            public string ContentJson; // already-serialized JSON array of blocks
            public MessageJson(string role, string contentJson)
            {
                Role = role;
                ContentJson = contentJson;
            }
        }

        private class ToolUseBlock
        {
            public string Id;
            public string Name;
            public string InputJson;
        }

        /// <summary>
        /// Reflection-free wrapper around LlmToolDispatcher's tiny parser.
        /// We need to parse arbitrary nested JSON (response payloads) and
        /// re-serialize content blocks verbatim, neither of which the
        /// dispatcher's static surface exposes. So we replicate the small
        /// amount of plumbing here — same shape, same conventions.
        /// </summary>
        private static class ParserShim
        {
            private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

            public static Dictionary<string, object> ParseObject(string json)
            {
                int idx = 0;
                SkipWs(json, ref idx);
                if (idx >= json.Length || json[idx] != '{')
                    throw new FormatException("expected '{' at " + idx);
                object o = ParseValue(json, ref idx);
                if (!(o is Dictionary<string, object> dict))
                    throw new FormatException("top-level value is not an object");
                return dict;
            }

            public static string SerializeValue(object v)
            {
                if (v == null) return "null";
                if (v is bool b) return b ? "true" : "false";
                if (v is string s) return "\"" + EscapeJson(s) + "\"";
                if (v is long l) return l.ToString(Inv);
                if (v is int i) return i.ToString(Inv);
                if (v is double d) return d.ToString("R", Inv);
                if (v is float f) return f.ToString("R", Inv);
                if (v is Dictionary<string, object> obj) return SerializeObject(obj);
                if (v is List<object> arr) return SerializeListVerbatim(arr);
                return "\"" + EscapeJson(v.ToString()) + "\"";
            }

            public static string SerializeObject(Dictionary<string, object> obj)
            {
                var sb = new StringBuilder();
                sb.Append("{");
                bool first = true;
                foreach (var kv in obj)
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append("\"").Append(EscapeJson(kv.Key)).Append("\": ").Append(SerializeValue(kv.Value));
                }
                sb.Append("}");
                return sb.ToString();
            }

            public static string SerializeListVerbatim(List<object> arr)
            {
                var sb = new StringBuilder();
                sb.Append("[");
                for (int i = 0; i < arr.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(SerializeValue(arr[i]));
                }
                sb.Append("]");
                return sb.ToString();
            }

            public static string Stringify(object v) { return SerializeValue(v); }

            // ---- parser primitives ----
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
                        if (i + 1 >= s.Length) throw new FormatException("bad escape");
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
                                if (i + 5 >= s.Length) throw new FormatException("bad \\u");
                                string hex = s.Substring(i + 2, 4);
                                int code;
                                if (!int.TryParse(hex, NumberStyles.HexNumber, Inv, out code))
                                    throw new FormatException("bad \\u: " + hex);
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
                string tok = s.Substring(start, i - start);
                if (tok.IndexOfAny(new[] { '.', 'e', 'E' }) < 0)
                {
                    long l;
                    if (long.TryParse(tok, NumberStyles.Integer, Inv, out l)) return l;
                }
                double d;
                if (double.TryParse(tok, NumberStyles.Float, Inv, out d)) return d;
                throw new FormatException("bad number: " + tok);
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
        }
    }
}
