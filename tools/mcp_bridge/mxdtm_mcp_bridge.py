#!/usr/bin/env python3
"""
MXDigitalTwinModeller — stdio<->loopback-HTTP MCP bridge.

Claude Desktop (and any MCP client that speaks stdio but not Streamable-HTTP)
launches THIS script. It relays JSON-RPC 2.0 messages between the client's
stdin/stdout and the in-Add-In MCP HTTP server running at
http://127.0.0.1:<port>/mcp/ inside a live SpaceClaim session.

It is a DUMB pipe: it forwards request bytes verbatim and returns the server's
response verbatim — NO schema knowledge, NO translation — so there is zero drift
between what the Add-In exposes (LlmToolRegistry) and what the client sees.

Port discovery: the Add-In writes %LOCALAPPDATA%\\MXDTM\\mcp_handshake.json
= {"port": <int>, "pid": <int>[, "token": <str>]} on startup (McpServer.
WriteHandshake). The bridge reads it each request so it survives the Add-In
re-binding to a new ephemeral port between SpaceClaim restarts.

Requires: Python 3.8+ (stdlib only — no pip install). Works on Windows where
SpaceClaim runs; the loopback server is 127.0.0.1 so client+server share the PC.

Cold-start UX: if SpaceClaim is not running (no handshake / connection refused),
the bridge does NOT crash the MCP session — it returns a JSON-RPC error telling
the user to open SpaceClaim, so the client can surface a helpful message and the
user can retry without restarting the client.
"""
import sys
import os
import json
import time
import urllib.request
import urllib.error

HANDSHAKE = os.path.join(
    os.environ.get("LOCALAPPDATA", os.path.expanduser("~")), "MXDTM", "mcp_handshake.json")
CONNECT_TIMEOUT = 10  # seconds per HTTP call


def _force_utf8_streams():
    # A frozen exe launched on a non-UTF-8 Windows codepage can otherwise corrupt the
    # JSON-RPC channel (surrogates in OS error strings -> UnicodeEncodeError). Force UTF-8
    # with replacement on both streams so the pipe stays clean regardless of locale.
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")  # py3.7+
        except Exception:
            pass


def safe(text):
    # Turn any string (incl. lone surrogates from OS error messages, e.g. a non-UTF-8
    # Windows codepage) into pure ASCII so json.dumps (ascii-safe) can always serialize it.
    # 'backslashreplace' never raises on lone surrogates the way 'replace'/utf-8 can.
    try:
        return str(text).encode("ascii", "backslashreplace").decode("ascii")
    except Exception:
        return "(unprintable)"


def log(msg):
    # stderr only — stdout is the JSON-RPC channel and must stay clean.
    sys.stderr.write("[mxdtm-bridge] " + safe(msg) + "\n")
    sys.stderr.flush()


def read_handshake():
    """Return (url, token) or (None, None) if SpaceClaim/Add-In isn't up."""
    try:
        with open(HANDSHAKE, "r", encoding="utf-8") as f:
            h = json.load(f)
        port = int(h["port"])
        token = h.get("token")  # optional; only present if the Add-In adds auth
        return "http://127.0.0.1:%d/mcp/" % port, token
    except Exception:
        return None, None


def jsonrpc_error(req_id, code, message):
    return {"jsonrpc": "2.0", "id": req_id, "error": {"code": code, "message": message}}


def post(url, token, raw_bytes):
    """POST raw JSON-RPC bytes to the loopback server, return response bytes."""
    headers = {"Content-Type": "application/json"}
    if token:
        headers["X-MXDTM-Token"] = token
    req = urllib.request.Request(url, data=raw_bytes, headers=headers, method="POST")
    with urllib.request.urlopen(req, timeout=CONNECT_TIMEOUT) as resp:
        return resp.read()


def handle_line(line):
    """Forward one JSON-RPC request line; return the response string (or None for a notification)."""
    line = line.strip()
    if not line:
        return None

    # Parse only enough to recover the id (for error framing) and detect notifications.
    req_id = None
    is_notification = False
    try:
        obj = json.loads(line)
        req_id = obj.get("id", None)
        is_notification = "id" not in obj
    except Exception:
        # Malformed JSON from the client — still try to relay; the server will error.
        pass

    url, token = read_handshake()
    if url is None:
        if is_notification:
            return None
        return json.dumps(jsonrpc_error(
            req_id, -32001,
            "SpaceClaim MXDigitalTwinModeller MCP server is not running. "
            "Open SpaceClaim (the Add-In starts the server automatically), then retry."))

    try:
        resp_bytes = post(url, token, line.encode("utf-8"))
    except urllib.error.URLError as e:
        if is_notification:
            return None
        return json.dumps(jsonrpc_error(
            req_id, -32002,
            "Could not reach the SpaceClaim MCP server at %s (%s). "
            "Is SpaceClaim still open?" % (url, safe(getattr(e, "reason", e)))))
    except Exception as e:
        if is_notification:
            return None
        return json.dumps(jsonrpc_error(req_id, -32603, "bridge relay error: %s" % safe(e)))

    text = resp_bytes.decode("utf-8", errors="replace").strip()
    # The server returns an empty body for notifications (e.g. notifications/initialized).
    if not text:
        return None
    return text


def main():
    _force_utf8_streams()
    log("started; handshake=%s" % HANDSHAKE)
    # Line-delimited JSON-RPC over stdio (the MCP stdio framing Claude Desktop uses).
    for line in sys.stdin:
        out = handle_line(line)
        if out is not None:
            sys.stdout.write(out + "\n")
            sys.stdout.flush()
    log("stdin closed; exiting")


if __name__ == "__main__":
    main()
