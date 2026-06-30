using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;

#if V251
using SpaceClaim.Api.V251;
#elif V252
using SpaceClaim.Api.V252;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Mcp
{
    /// <summary>
    /// In-process MCP server (Approach C, MCP_SERVER_PLAN). A loopback HttpListener serves
    /// MCP JSON-RPC 2.0 over Streamable-HTTP POST. Tool calls arrive on listener worker
    /// threads and are marshalled to the SC API thread (ApiThreadMarshaller) for WriteBlock
    /// execution against the persistent SessionContext. The LLM + API key live in the MCP
    /// CLIENT; this server only executes the 20 tools that LlmToolDispatcher already routes.
    ///
    /// Single-call serialization: one tool in flight at a time (GATE-0 advisory) via _callLock.
    /// </summary>
    public sealed class McpServer
    {
        private static McpServer _instance;
        public static McpServer Instance { get { return _instance ?? (_instance = new McpServer()); } }

        private HttpListener _listener;
        private Thread _acceptThread;
        private volatile bool _running;
        private readonly object _callLock = new object();
        public int Port { get; private set; }

        private McpServer() { }

        /// <summary>Start the listener on 127.0.0.1 at an OS-assigned free port. Idempotent.</summary>
        public void Start()
        {
            if (_running) return;
            // Bind to a free port: probe by trying an ephemeral range; HttpListener needs an
            // explicit port, so we try a small set and keep the first that binds.
            for (int port = 8765; port < 8865; port++)
            {
                try
                {
                    var l = new HttpListener();
                    l.Prefixes.Add("http://127.0.0.1:" + port + "/mcp/");
                    l.Start();
                    _listener = l;
                    Port = port;
                    break;
                }
                catch { /* port in use, try next */ }
            }
            if (_listener == null) return; // could not bind; server stays off

            _running = true;
            WriteHandshake();
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "McpAccept" };
            _acceptThread.Start();
        }

        public void Stop()
        {
            _running = false;
            try { if (_listener != null) _listener.Stop(); } catch { }
        }

        private void WriteHandshake()
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MXDTM");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "mcp_handshake.json"),
                    "{\"port\": " + Port + ", \"pid\": " + System.Diagnostics.Process.GetCurrentProcess().Id + "}",
                    new UTF8Encoding(false));
            }
            catch { }
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                HttpListenerContext ctx;
                try { ctx = _listener.GetContext(); }
                catch { if (!_running) break; else continue; }
                try { HandleRequest(ctx); }
                catch { try { ctx.Response.StatusCode = 500; ctx.Response.Close(); } catch { } }
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            string body;
            using (var r = new StreamReader(ctx.Request.InputStream, Encoding.UTF8)) body = r.ReadToEnd();

            string responseJson = HandleJsonRpc(body);
            byte[] bytes = Encoding.UTF8.GetBytes(responseJson ?? "");
            ctx.Response.ContentType = "application/json";
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.OutputStream.Close();
        }

        // ---- JSON-RPC 2.0 / MCP method routing ----
        private string HandleJsonRpc(string requestJson)
        {
            string method = JsonLite.GetString(requestJson, "method");
            string id = JsonLite.GetRawValue(requestJson, "id"); // number or string, echo raw

            if (method == "initialize")
                return RpcResult(id,
                    "{\"protocolVersion\": \"2024-11-05\", \"capabilities\": {\"tools\": {}}, " +
                    "\"serverInfo\": {\"name\": \"mxdtm-spaceclaim\", \"version\": \"0.1.0\"}}");

            if (method == "notifications/initialized" || method == "initialized")
                return null; // notification, no response

            if (method == "tools/list")
                return RpcResult(id, "{\"tools\": " + McpToolAdapter.ToolsListJson() + "}");

            if (method == "tools/call")
                return HandleToolCall(id, requestJson);

            return RpcError(id, -32601, "Method not found: " + method);
        }

        private string HandleToolCall(string id, string requestJson)
        {
            // params: { "name": "...", "arguments": { ... } }
            string toolName = JsonLite.GetString(requestJson, "name");
            string argsJson = JsonLite.GetObjectRaw(requestJson, "arguments") ?? "{}";
            if (string.IsNullOrEmpty(toolName))
                return RpcError(id, -32602, "tools/call missing 'name'");

            string toolResultJson;
            try
            {
                lock (_callLock) // one tool in flight
                {
                    toolResultJson = ExecuteTool(toolName, argsJson);
                }
            }
            catch (Exception ex)
            {
                // surface as an MCP tool error (isError), session survives
                return RpcResult(id, ToolContent(LlmToolDispatcherEnvelopeError(ex.Message), true));
            }
            return RpcResult(id, ToolContent(toolResultJson, false));
        }

        /// <summary>Marshal the Dispatch call onto the API thread (read-only: no WriteBlock;
        /// mutating: WriteBlock inside the marshalled work + graph refresh on success).</summary>
        private string ExecuteTool(string toolName, string argsJson)
        {
            bool readOnly = (toolName == "get_feature_graph" || toolName == "find_features_by_type");
            // generate_phone / set_camera_height BUILD the session body themselves
            // (SessionContext.GeneratePhone) — no pre-existing body to bind.
            bool selfBinds = (toolName == "generate_phone" || toolName == "generate_phone_from_spec"
                || toolName == "set_camera_height");

            return ApiThreadMarshaller.Run(delegate
            {
                var session = SessionContext.Instance;
                string result = null;
                string bindErr = null;

                // EVERYTHING that touches the body runs inside ONE WriteBlock. Bind walks
                // Component.Content (REQUIRES WriteBlock). Generation tools skip the bind —
                // they create the body via SessionContext.GeneratePhone.
                WriteBlock.ExecuteTask("MCP " + toolName, new Task(delegate
                {
                    if (!selfBinds && session.Body == null && !session.BindActiveBody(out bindErr))
                        return;
                    result = LlmToolDispatcher.Dispatch(
                        selfBinds ? null : session.Body, session.Graph, toolName, argsJson);
                    if (!readOnly && !selfBinds) session.RefreshGraph();
                }));

                if (!selfBinds && session.Body == null)
                    return LlmToolDispatcherEnvelopeError("no active body: " + (bindErr ?? "bind failed"));
                return result;
            });
        }

        // ---- small JSON-RPC envelope helpers ----
        private static string RpcResult(string id, string resultJson)
        {
            if (id == null) return null;
            return "{\"jsonrpc\": \"2.0\", \"id\": " + id + ", \"result\": " + resultJson + "}";
        }
        private static string RpcError(string id, int code, string message)
        {
            return "{\"jsonrpc\": \"2.0\", \"id\": " + (id ?? "null") +
                ", \"error\": {\"code\": " + code + ", \"message\": \"" + JsonLite.Escape(message) + "\"}}";
        }
        private static string ToolContent(string innerJson, bool isError)
        {
            // MCP tools/call result: { content: [{type:text, text:"..."}], isError }
            return "{\"content\": [{\"type\": \"text\", \"text\": \"" + JsonLite.Escape(innerJson) + "\"}], " +
                   "\"isError\": " + (isError ? "true" : "false") + "}";
        }
        private static string LlmToolDispatcherEnvelopeError(string msg)
        {
            return "{\"success\": false, \"error\": \"" + JsonLite.Escape(msg) + "\"}";
        }
    }
}
