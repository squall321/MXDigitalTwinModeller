using System;
using System.Text;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Mcp
{
    /// <summary>
    /// Tiny, dependency-free JSON value extractor for the MCP request envelope. NOT a full
    /// parser — it pulls top-level/nested-by-key values out of well-formed JSON-RPC requests
    /// (method, id, params.name, params.arguments). Tool ARGUMENT parsing is delegated to
    /// LlmToolDispatcher's own parser, so this only needs to locate spans by key and balance
    /// braces/brackets/strings. Robust to nested objects, escaped quotes, and whitespace.
    /// </summary>
    public static class JsonLite
    {
        /// <summary>Value of a string-valued key, unescaped. Null if absent/not a string.</summary>
        public static string GetString(string json, string key)
        {
            int v = ValueStart(json, key);
            if (v < 0 || json[v] != '"') return null;
            return ReadString(json, v);
        }

        /// <summary>Raw JSON token for a key (number, true/false/null, or a quoted string with
        /// quotes preserved) — suitable for echoing a JSON-RPC id verbatim. Null if absent.</summary>
        public static string GetRawValue(string json, string key)
        {
            int v = ValueStart(json, key);
            if (v < 0) return null;
            int end = TokenEnd(json, v);
            return end > v ? json.Substring(v, end - v) : null;
        }

        /// <summary>Raw substring of an object/array value for a key (braces included).</summary>
        public static string GetObjectRaw(string json, string key)
        {
            int v = ValueStart(json, key);
            if (v < 0 || (json[v] != '{' && json[v] != '[')) return null;
            int end = TokenEnd(json, v);
            return end > v ? json.Substring(v, end - v) : null;
        }

        public static string Escape(string s)
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
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        // ---- internals ----

        // Find the index where the VALUE for "key" begins (first non-ws after the colon).
        private static int ValueStart(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return -1;
            string needle = "\"" + key + "\"";
            int from = 0;
            while (true)
            {
                int k = json.IndexOf(needle, from, StringComparison.Ordinal);
                if (k < 0) return -1;
                // ensure the match is a KEY (followed by optional ws then ':'), not a value.
                int i = k + needle.Length;
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                if (i < json.Length && json[i] == ':')
                {
                    i++;
                    while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                    return i < json.Length ? i : -1;
                }
                from = k + needle.Length; // was a string value equal to the key; keep scanning
            }
        }

        // Given the start of a value, return the index just past its end.
        private static int TokenEnd(string json, int start)
        {
            char c = json[start];
            if (c == '"') { int e = ReadStringEnd(json, start); return e; }
            if (c == '{' || c == '[') return ReadBalancedEnd(json, start);
            // primitive: number / true / false / null — read until delimiter.
            int i = start;
            while (i < json.Length && ",}] \t\r\n".IndexOf(json[i]) < 0) i++;
            return i;
        }

        private static string ReadString(string json, int quoteIndex)
        {
            var sb = new StringBuilder();
            int i = quoteIndex + 1;
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length)
                {
                    char n = json[i + 1];
                    switch (n)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (i + 5 < json.Length)
                            {
                                int code = Convert.ToInt32(json.Substring(i + 2, 4), 16);
                                sb.Append((char)code); i += 4;
                            }
                            break;
                        default: sb.Append(n); break;
                    }
                    i += 2; continue;
                }
                if (c == '"') break;
                sb.Append(c); i++;
            }
            return sb.ToString();
        }

        // Index just past the closing quote of a string starting at quoteIndex.
        private static int ReadStringEnd(string json, int quoteIndex)
        {
            int i = quoteIndex + 1;
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '\\') { i += 2; continue; }
                if (c == '"') return i + 1;
                i++;
            }
            return json.Length;
        }

        // Index just past the matching close of an object/array starting at 'start'.
        private static int ReadBalancedEnd(string json, int start)
        {
            char open = json[start];
            char close = open == '{' ? '}' : ']';
            int depth = 0; int i = start;
            bool inStr = false;
            while (i < json.Length)
            {
                char c = json[i];
                if (inStr)
                {
                    if (c == '\\') { i += 2; continue; }
                    if (c == '"') inStr = false;
                    i++; continue;
                }
                if (c == '"') { inStr = true; i++; continue; }
                if (c == open) depth++;
                else if (c == close) { depth--; if (depth == 0) return i + 1; }
                i++;
            }
            return json.Length;
        }
    }
}
