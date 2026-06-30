# encoding: utf-8
# v2 g10 (MCP/LLM tool wiring): generate_phone_from_spec is exposed + dispatches end-to-end.
# This is the LAST link: an LLM emits a JSON spec -> the tool dispatcher binds+validates+builds a
# phone in the live session. Verifies the SAME path MCP (tools/call) and AskClaude (RunConversation)
# both use: LlmToolDispatcher.Dispatch(null, null, "generate_phone_from_spec", argsJson).
#   T1  tool advertised: ToolsListJson()/GetAllTools() contains generate_phone_from_spec.
#   T2  valid curved spec  -> Envelope success=true, a curved phone is built in SessionContext.
#   T3  invalid spec       -> Envelope success=false, error mentions the design-intent reason.
#   T4  malformed JSON spec -> Envelope success=false (parse error), no crash.
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import (
    LlmToolDispatcher, LlmToolRegistry)
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Mcp import SessionContext

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g10_mcp_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g10_mark.txt"
def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass
log = []
def emit(s):
    try: log.append("".join(ch for ch in str(s) if ord(ch) >= 32 or ch == "\t"))
    except Exception: log.append("(unprintable)")

# The argsJson the dispatcher receives is {"spec_json": "<the spec as a STRING>"}. The inner spec
# JSON must be embedded as a JSON string value, so its quotes are escaped. Build it programmatically.
def wrap(spec_str):
    esc = spec_str.replace("\\", "\\\\").replace('"', '\\"')
    return '{"spec_json": "' + esc + '"}'

SPEC_VALID = ('{"length_mm":150,"width_mm":72,"thickness_mm":8,"corner_r":3,"min_wall":0.4,'
              '"hollow_wall_mm":0.6,"back_bulge_mm":0.7,"lens_on_curved_back":true,"ports_on_flank":true,'
              '"pocket":false,'
              '"holes":[{"x_mm":20,"y_mm":0,"diameter_mm":4,"through":false,"depth_mm":1,"on_curved_back":true}],'
              '"ports":[{"type":"usbc","x_mm":0,"y_mm":-36,"z_mm":4,"width_mm":9,"height_mm":3,"on_face":"flank"}]}')
SPEC_INVALID = '{"length_mm":146.7,"width_mm":71.5,"thickness_mm":7.4,"min_wall":0.4,"back_bulge_mm":7.2}'
SPEC_MALFORMED = '{"length_mm":150,"width_mm":'

H = {}
def _do():
    _mk("do-start")
    # T1: is the tool advertised by the registry (what MCP tools/list + AskClaude tools share)?
    tools_json = LlmToolRegistry.ToToolsArrayJson()
    H["advertised"] = "generate_phone_from_spec" in tools_json
    _mk("advertised=%s" % H["advertised"])

    # T2: valid spec through the REAL dispatch entry (designBody=null, graph=null -> self-binds).
    Document.Create()
    rv = LlmToolDispatcher.Dispatch(None, None, "generate_phone_from_spec", wrap(SPEC_VALID))
    H["valid_env"] = rv
    sc = SessionContext.Instance
    H["valid_built"] = (sc.Body is not None)
    H["valid_vol"] = 0.0
    try:
        if sc.Body is not None: H["valid_vol"] = sc.Body.Shape.Volume * 1e9
    except System.Exception: pass
    _mk("valid env=%s built=%s vol=%.1f" % (rv[:60], H["valid_built"], H["valid_vol"]))

    # T3: invalid spec (design intent) -> Envelope error.
    ri = LlmToolDispatcher.Dispatch(None, None, "generate_phone_from_spec", wrap(SPEC_INVALID))
    H["invalid_env"] = ri
    _mk("invalid env=%s" % ri[:80])

    # T4: malformed JSON -> Envelope error, no crash.
    rm = LlmToolDispatcher.Dispatch(None, None, "generate_phone_from_spec", wrap(SPEC_MALFORMED))
    H["malformed_env"] = rm
    _mk("malformed env=%s" % rm[:80])
try:
    WriteBlock.ExecuteTask("g10", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name)
    emit("WRITEBLOCK THREW: %s: %s" % (e.GetType().Name, e.Message))

def has(env, frag):
    return env is not None and frag in env

adv = H.get("advertised", False)
ve = H.get("valid_env"); ie = H.get("invalid_env"); me = H.get("malformed_env")
emit("T1 advertised: %s" % adv)
emit("T2 valid:     env=%s built=%s vol=%.1f" % (ve, H.get("valid_built"), H.get("valid_vol", 0.0)))
emit("T3 invalid:   env=%s" % ie)
emit("T4 malformed: env=%s" % me)

# verdicts
T1 = adv
# valid: success true AND a body with real volume was built in the session
T2 = has(ve, '"success": true') and H.get("valid_built") and H.get("valid_vol", 0.0) > 1000.0
# invalid: success false AND mentions the wall/bulge design-intent reason
T3 = has(ie, '"success": false') and (has(ie, "min_wall") or has(ie, "bulge") or has(ie, "invalid spec"))
# malformed: success false AND a parse-ish error, no crash (we got an envelope back)
T4 = has(me, '"success": false')
allp = T1 and T2 and T3 and T4
emit("G10_PASS advertised=%s validBuilds=%s invalidRejected=%s malformedRejected=%s ALL=%s" % (
    T1, T2, T3, T4, allp))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
