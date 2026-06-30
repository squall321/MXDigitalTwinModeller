# encoding: utf-8
# v2 g12 (MCP batch): apply_operations runs several edits in one call via the real dispatch entry.
#   T1  advertised in ToToolsArrayJson().
#   T2  a chain of 3 real ops (add_hole + add_boss + add_hole_on_face on a curved phone) all succeed
#       in ONE dispatch -> Envelope success, "applied": 3, and the body changed by all three.
#   T3  first-fail-stops: a chain whose 2nd op is invalid -> Envelope success=false naming step 2,
#       and the 3rd op did NOT run (volume reflects only step 1).
#   T4  nesting rejected: an operations array containing apply_operations -> success=false.
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import (
    LlmToolDispatcher, LlmToolRegistry)
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Mcp import SessionContext

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g12_apply_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g12_mark.txt"
def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass
log = []
def emit(s):
    try: log.append("".join(ch for ch in str(s) if ord(ch) >= 32 or ch == "\t"))
    except Exception: log.append("(unprintable)")

L, W, T, bulge = 150.0, 72.0, 8.0, 0.7
def crownZ(y): return T + bulge*(1.0 - (y/(W/2.0))**2)
def wrap_spec(spec_str):
    return '{"spec_json": "' + spec_str.replace("\\","\\\\").replace('"','\\"') + '"}'
CURVED = ('{"length_mm":150,"width_mm":72,"thickness_mm":8,"corner_r":3,"min_wall":0.4,'
          '"hollow_wall_mm":0.6,"back_bulge_mm":0.7,"pocket":false}')

H = {}
def _do():
    _mk("do-start")
    sc = SessionContext.Instance
    H["adv"] = "apply_operations" in LlmToolRegistry.ToToolsArrayJson()

    def vol():
        try: return sc.Body.Shape.Volume * 1e9
        except System.Exception: return 0.0

    # T2: valid 3-op chain on a fresh curved phone.
    Document.Create()
    LlmToolDispatcher.Dispatch(None, None, "generate_phone_from_spec", wrap_spec(CURVED))
    v0 = vol()
    cz = crownZ(0.0) + 0.05
    chain_ok = ('{"operations":['
        '{"tool":"add_hole","args":{"position_mm":[40,25,8],"diameter_mm":2,"through":true}},'
        '{"tool":"add_boss","args":{"position_mm":[-40,25,8],"diameter_mm":4,"height_mm":1.5}},'
        '{"tool":"add_hole_on_face","args":{"seed_mm":[20,0,%.3f],"diameter_mm":3,"depth_mm":2}}'
        ']}' % cz)
    envA = LlmToolDispatcher.Dispatch(sc.Body, sc.Graph, "apply_operations", chain_ok)
    H["A_env"] = envA
    H["A_dV"] = vol() - v0
    _mk("A env=%s dV=%.2f" % (envA[:70], H["A_dV"]))

    # T3: first-fail-stops -> 2nd op references a non-existent hole id (invalid) so it fails;
    #     3rd op (add_boss) must NOT run.
    LlmToolDispatcher.Dispatch(None, None, "generate_phone_from_spec", wrap_spec(CURVED))
    vb = vol()
    chain_fail = ('{"operations":['
        '{"tool":"add_hole","args":{"position_mm":[40,25,8],"diameter_mm":2,"through":true}},'
        '{"tool":"change_hole_diameter","args":{"hole_id":"H999","new_diameter_mm":5}},'
        '{"tool":"add_boss","args":{"position_mm":[-40,25,8],"diameter_mm":8,"height_mm":3}}'
        ']}')
    envB = LlmToolDispatcher.Dispatch(sc.Body, sc.Graph, "apply_operations", chain_fail)
    H["B_env"] = envB
    # if the boss (step 3, big +vol) had run, dV would be large +; it must reflect only step 1 (small -).
    H["B_dV"] = vol() - vb
    _mk("B env=%s dV=%.2f" % (envB[:80], H["B_dV"]))

    # T4: nesting rejected.
    nested = '{"operations":[{"tool":"apply_operations","args":{"operations":[]}}]}'
    envC = LlmToolDispatcher.Dispatch(sc.Body, sc.Graph, "apply_operations", nested)
    H["C_env"] = envC
    _mk("C env=%s" % envC[:80])
WriteBlock.ExecuteTask("g12", Task(_do))

emit("advertised: %s" % H.get("adv"))
emit("A(valid 3-chain): dV=%+.2f env=%s" % (H.get("A_dV",0.0), H.get("A_env","")))
emit("B(first-fail):    dV=%+.2f env=%s" % (H.get("B_dV",0.0), H.get("B_env","")))
emit("C(nested):        env=%s" % H.get("C_env",""))

T1 = bool(H.get("adv"))
# A: success, applied 3, and all three changed the body (net change non-trivial)
T2 = ('"success": true' in H.get("A_env","")) and ('"applied": 3' in H.get("A_env","")) and abs(H.get("A_dV",0.0)) > 1.0
# B: failed naming step 2, and step-3 boss did NOT run -> dV stays small/negative (no +big boss)
T3 = ('"success": false' in H.get("B_env","")) and ("step 2" in H.get("B_env","")) and (H.get("B_dV",99.0) < 1.0)
# C: nesting rejected
T4 = ('"success": false' in H.get("C_env","")) and ("nested" in H.get("C_env",""))
allp = T1 and T2 and T3 and T4
emit("G12_PASS advertised=%s chainOk=%s firstFailStops=%s nestRejected=%s ALL=%s" % (
    T1, T2, T3, T4, allp))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
