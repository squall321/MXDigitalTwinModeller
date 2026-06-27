namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Mcp
{
    /// <summary>
    /// Adapts LlmToolRegistry's 20 tool defs (Anthropic shape: "input_schema") into the MCP
    /// tools/list shape ("inputSchema", camelCase). Single source of truth — no schema drift,
    /// because it reads LlmToolRegistry.ToToolsArrayJson() live at call time.
    /// </summary>
    public static class McpToolAdapter
    {
        /// <summary>The JSON array for MCP "tools/list" result.tools.</summary>
        public static string ToolsListJson()
        {
            // LlmToolRegistry emits ... "input_schema": {...} ...; MCP wants "inputSchema".
            // The only structural difference is that key name, so a targeted replace is exact
            // and avoids re-serializing the (already valid) schema bodies.
            string anthropic = LlmToolRegistry.ToToolsArrayJson();
            return anthropic.Replace("\"input_schema\":", "\"inputSchema\":");
        }
    }
}
