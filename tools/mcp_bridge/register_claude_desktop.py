#!/usr/bin/env python3
"""
Register the MXDigitalTwinModeller MCP bridge with Claude Desktop (one-click).

Safely MERGES an `mxdtm-spaceclaim` server entry into Claude Desktop's
`claude_desktop_config.json` WITHOUT disturbing any existing servers or settings:
  - reads the existing config (or starts a fresh one if absent),
  - backs it up to `claude_desktop_config.json.mxdtm-bak` before writing,
  - sets/updates ONLY the `mcpServers.mxdtm-spaceclaim` key,
  - points it at THIS bridge script (resolved next to this file) and the Python
    interpreter currently running.

Idempotent: running it again just refreshes the entry to the current paths.
Use --remove to delete the entry (the rest of the config is left intact).

No third-party deps (stdlib json only). Run via register_claude_desktop.bat,
or: python register_claude_desktop.py
"""
import os
import sys
import json
import shutil
import argparse

SERVER_KEY = "mxdtm-spaceclaim"


def config_path():
    # Windows: %APPDATA%\Claude\claude_desktop_config.json
    appdata = os.environ.get("APPDATA")
    if appdata:
        return os.path.join(appdata, "Claude", "claude_desktop_config.json")
    # macOS fallback (best-effort; primary target is Windows where SpaceClaim runs)
    return os.path.expanduser("~/Library/Application Support/Claude/claude_desktop_config.json")


def bridge_path():
    return os.path.join(os.path.dirname(os.path.abspath(__file__)), "mxdtm_mcp_bridge.py")


def load_config(path):
    if not os.path.isfile(path):
        return {}
    try:
        with open(path, "r", encoding="utf-8") as f:
            data = json.load(f)
        if not isinstance(data, dict):
            print("[warn] existing config is not a JSON object; starting fresh (a backup is kept).")
            return {}
        return data
    except Exception as e:
        print("[warn] could not parse existing config (%s); starting fresh (a backup is kept)." % e)
        return {}


def backup(path):
    if os.path.isfile(path):
        bak = path + ".mxdtm-bak"
        try:
            shutil.copy2(path, bak)
            print("[ok] backed up existing config -> %s" % bak)
        except Exception as e:
            print("[warn] could not back up config: %s" % e)


def write_config(path, cfg):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(cfg, f, indent=2, ensure_ascii=False)
        f.write("\n")


def main():
    ap = argparse.ArgumentParser(description="Register the MXDTM MCP bridge with Claude Desktop.")
    ap.add_argument("--remove", action="store_true", help="Remove the mxdtm-spaceclaim entry instead of adding it.")
    ap.add_argument("--python", default=sys.executable or "python",
                    help="Python interpreter Claude Desktop should use to launch the bridge.")
    args = ap.parse_args()

    path = config_path()
    bridge = bridge_path()
    print("Claude Desktop config: %s" % path)
    print("Bridge script:         %s" % bridge)

    if not args.remove and not os.path.isfile(bridge):
        print("[error] bridge script not found next to this file: %s" % bridge)
        return 2

    cfg = load_config(path)
    servers = cfg.get("mcpServers")
    if not isinstance(servers, dict):
        servers = {}
    backup(path)

    if args.remove:
        if SERVER_KEY in servers:
            del servers[SERVER_KEY]
            cfg["mcpServers"] = servers
            write_config(path, cfg)
            print("[ok] removed '%s' from Claude Desktop config." % SERVER_KEY)
        else:
            print("[ok] '%s' was not present; nothing to remove." % SERVER_KEY)
        print("Restart Claude Desktop for the change to take effect.")
        return 0

    # Add / refresh ONLY our entry; everything else in the config is preserved.
    servers[SERVER_KEY] = {
        "command": args.python,
        "args": [bridge],
    }
    cfg["mcpServers"] = servers
    write_config(path, cfg)
    print("[ok] registered '%s' (command=%s)." % (SERVER_KEY, args.python))
    print("")
    print("Next steps:")
    print("  1. Open SpaceClaim (the MXDigitalTwinModeller Add-In starts the MCP server).")
    print("  2. FULLY restart Claude Desktop.")
    print("  3. Ask Claude to design a phone in natural language.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
