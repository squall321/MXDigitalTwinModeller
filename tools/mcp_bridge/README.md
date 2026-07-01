# MXDigitalTwinModeller — Claude Desktop MCP bridge

Connect **Claude Desktop** (or any stdio-based MCP client) to the
MXDigitalTwinModeller SpaceClaim Add-In so you can design phone front-metal
parts in natural language:

> "Make a 150×72×8 mm phone with a gently curved back, a 4 mm lens hole on the
> curved back, and a USB-C port on the bottom flank."

Claude turns that into a structured spec and calls the Add-In's
`generate_phone_from_spec` tool — the geometry is built live in SpaceClaim.

---

## How it works

```
Claude Desktop ──stdio──▶ mxdtm_mcp_bridge.py ──HTTP──▶ 127.0.0.1:<port>/mcp/  (inside SpaceClaim)
       (your LLM)            (this folder)                  (the Add-In's MCP server)
```

- The Add-In runs an **in-process MCP server** on `127.0.0.1` (loopback only —
  never exposed to the network) and writes its port to a handshake file.
- Claude Desktop speaks **stdio**, not loopback HTTP, so this bridge relays
  between them. It is a dumb pipe — it forwards JSON-RPC bytes verbatim, so the
  tool list always matches exactly what the Add-In exposes.

## Prerequisites

1. **SpaceClaim with the MXDigitalTwinModeller Add-In installed and running.**
   The MCP server starts automatically when the Add-In loads; it writes
   `%LOCALAPPDATA%\MXDTM\mcp_handshake.json` = `{"port": …, "pid": …}`.
2. **Claude Desktop** (or another stdio MCP client).

No Python needed on the user's machine — the bridge and registrar ship as
standalone `.exe` files (PyInstaller). Python is only needed on the *build*
machine to produce them (`build_bridge.bat`).

## Setup — automatic (the MSI does it)

The installer places this folder at
`C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\mcp_bridge\` and
runs `register_claude_desktop.exe` during install, which safely merges an
`mxdtm-spaceclaim` server into **your** Claude Desktop config (existing servers
and settings preserved; a `*.mxdtm-bak` backup is made first). The config points
at the bundled `mxdtm_mcp_bridge.exe`.

Just **fully restart Claude Desktop** after installing, open SpaceClaim, and ask
Claude to design a phone.

> If Claude Desktop wasn't installed when you ran the MSI, run the one-click step
> below afterwards.

## Setup — one click (if you need to re-run it)

**Double-click `register_claude_desktop.exe`** (or `register_claude_desktop.bat`).
Same safe merge as above. To undo:
`register_claude_desktop.exe --remove`.

## Setup — manual (alternative)

Edit `%APPDATA%\Claude\claude_desktop_config.json` and add:

```json
{
  "mcpServers": {
    "mxdtm-spaceclaim": {
      "command": "C:\\ProgramData\\SpaceClaim\\AddIns\\MXDigitalTwinModeller\\V252\\mcp_bridge\\mxdtm_mcp_bridge.exe",
      "args": []
    }
  }
}
```

Then **fully restart Claude Desktop**. The `mxdtm-spaceclaim` tools appear once
SpaceClaim is open with the Add-In loaded.

> Running from source instead of the installed exe? Point `command` at your
> Python interpreter and `args` at `mxdtm_mcp_bridge.py`.

## Usage

1. Open SpaceClaim (Add-In loads → MCP server starts → handshake written).
2. In Claude Desktop, ask for a phone in natural language.
3. Claude calls `generate_phone_from_spec` (or `generate_phone` for a quick flat
   shell), and the part is built in the active SpaceClaim document.
4. Follow-up edits ("make the camera bump 2 mm taller") route to the editing
   tools and regenerate.

## Cold-start behaviour

If SpaceClaim isn't running (no handshake, or connection refused), the bridge
does **not** crash the MCP session — it returns a JSON-RPC error telling you to
open SpaceClaim and retry. No need to restart Claude Desktop.

## Tools exposed

The bridge exposes whatever `LlmToolRegistry.GetAllTools()` lists — the full
set, including:

- `generate_phone_from_spec` — full from-scratch spec (curved back, lens-on-
  curved holes, flank ports, grille, buttons). **Use this for anything richer
  than a flat shell.**
- `generate_phone` — quick flat uniform-wall shell from a few scalars.
- `set_camera_height` and the modification tools (`change_*`, `add_*`,
  `move_*`, `mirror_*`, …) for editing an existing body.

## Security

- The server binds to `127.0.0.1` only — not reachable from other machines.
- The bridge sends an optional `X-MXDTM-Token` header if the handshake file
  carries a `token` field (forward-compatible with a future auth guard); today
  the loopback bind is the guard.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| Tools don't appear in Claude Desktop | SpaceClaim/Add-In not running, or wrong script path in config. Open SpaceClaim, restart Claude Desktop. |
| "server is not running" error | No handshake file — confirm the Add-In loaded (check SpaceClaim's add-ins). |
| "Could not reach … Is SpaceClaim still open?" | SpaceClaim was closed after the handshake was written. Reopen it. |
| `python` not found | Use the full path to `python.exe` in the config `command`. |
