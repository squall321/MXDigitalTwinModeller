# encoding: utf-8
# v2 g11 (MCP coverage): the 4 curved-face creation tools + stop_at_stage are LLM-reachable.
# Exercises the REAL dispatch entry MCP+AskClaude share, mirroring g10.
#   T1  all 4 *_on_face tools + stop_at_stage advertised in ToToolsArrayJson().
#   T2  generate a curved phone, then each *_on_face tool dispatches on it and removes/adds material
#       along the local face normal (hole/slit/pocket cut <0; boss adds >0). Each = Envelope success.
#   T3  generate_phone_from_spec with stop_at_stage="S00" -> a BARE base slab (vol ~ L*W*T, no hollow).
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import (
    LlmToolDispatcher, LlmToolRegistry)
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Mcp import SessionContext

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g11_onface_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g11_mark.txt"
def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass
log = []
def emit(s):
    try: log.append("".join(ch for ch in str(s) if ord(ch) >= 32 or ch == "\t"))
    except Exception: log.append("(unprintable)")

L, W, T, bulge = 150.0, 72.0, 8.0, 0.7
def crownZ(y):
    return T + bulge*(1.0 - (y/(W/2.0))**2)
def wrap_spec(spec_str):
    esc = spec_str.replace("\\", "\\\\").replace('"', '\\"')
    return '{"spec_json": "' + esc + '"}'
CURVED_SPEC = ('{"length_mm":150,"width_mm":72,"thickness_mm":8,"corner_r":3,"min_wall":0.4,'
               '"hollow_wall_mm":0.6,"back_bulge_mm":0.7,"pocket":false}')

def jarr(vals):
    return "[" + ",".join(str(v) for v in vals) + "]"

H = {}
def _do():
    _mk("do-start")
    sc = SessionContext.Instance

    # T1: advertised
    tools = LlmToolRegistry.ToToolsArrayJson()
    H["adv"] = all(t in tools for t in
        ["add_hole_on_face","add_slit_on_face","add_pocket_on_face","add_boss_on_face","stop_at_stage"])
    _mk("adv=%s" % H["adv"])

    # T2: build a curved phone (full), then run each *_on_face on the live body.
    Document.Create()
    LlmToolDispatcher.Dispatch(None, None, "generate_phone_from_spec", wrap_spec(CURVED_SPEC))
    body = sc.Body
    H["built"] = (body is not None)
    def vol():
        try: return sc.Body.Shape.Volume * 1e9
        except System.Exception: return 0.0
    def run(name, argjson):
        v0 = vol()
        env = LlmToolDispatcher.Dispatch(sc.Body, sc.Graph, name, argjson)
        v1 = vol()
        ok = ('"success": true' in env)
        H[name] = (ok, v1 - v0, env[:70])
        _mk("%s ok=%s dV=%.2f" % (name, ok, v1 - v0))
    # crown seed (y=0) for hole/pocket/boss; flank seed (+Y outside) for slit.
    sCrown = lambda x: jarr([x, 0.0, crownZ(0.0) + 0.05])
    run("add_hole_on_face",   '{"seed_mm":%s,"diameter_mm":4,"depth_mm":3}' % sCrown(20))
    run("add_pocket_on_face", '{"seed_mm":%s,"width_mm":6,"length_mm":6,"depth_mm":1.5}' % sCrown(-20))
    run("add_boss_on_face",   '{"seed_mm":%s,"diameter_mm":5,"height_mm":1}' % jarr([0.0, 20.0, crownZ(20.0)+0.05]))
    # slit through the +Y flank (seed just outside the side wall at mid-height)
    run("add_slit_on_face",   '{"seed_mm":%s,"width_mm":2,"length_mm":8,"depth_mm":2,"orientation_seed_mm":%s}'
        % (jarr([-30.0, W/2.0+0.05, 4.0]), jarr([0.0, W/2.0+0.05, 4.0])))

    # T3: stop_at_stage="S00" -> bare base slab (no hollow). Fresh session.
    env3 = LlmToolDispatcher.Dispatch(None, None, "generate_phone_from_spec",
        '{"spec_json": "%s", "stop_at_stage": "S00"}' % CURVED_SPEC.replace('"', '\\"'))
    H["slab_env"] = env3
    H["slab_vol"] = vol()
    _mk("slab vol=%.1f env=%s" % (H["slab_vol"], env3[:70]))
WriteBlock.ExecuteTask("g11", Task(_do))

emit("advertised(4 onface + stop_at_stage): %s" % H.get("adv"))
emit("curved phone built: %s" % H.get("built"))
for name in ["add_hole_on_face","add_pocket_on_face","add_boss_on_face","add_slit_on_face"]:
    ok, dV, env = H.get(name, (False, 0.0, "(not run)"))
    emit("%-20s success=%s dV=%+.2f env=%s" % (name, ok, dV, env))
emit("slab(S00): vol=%.1f env=%s" % (H.get("slab_vol", 0.0), H.get("slab_env", "")[:90]))

# verdicts
T1 = bool(H.get("adv"))
hole = H.get("add_hole_on_face", (False,0,""));   pocket = H.get("add_pocket_on_face", (False,0,""))
boss = H.get("add_boss_on_face", (False,0,""));   slit = H.get("add_slit_on_face", (False,0,""))
T2 = (hole[0] and hole[1] < -0.3 and pocket[0] and pocket[1] < -0.3
      and boss[0] and boss[1] > 0.3 and slit[0] and slit[1] < -0.1)
# S00 slab = solid box ~ L*W*T = 86400 mm^3 (no hollow, no curve). Allow generous band.
slabVol = H.get("slab_vol", 0.0)
T3 = ('"success": true' in H.get("slab_env","")) and slabVol > 80000 and slabVol < 92000
allp = T1 and T2 and T3
emit("G11_PASS advertised=%s onfaceOps=%s s00Slab(%.0f~86400)=%s ALL=%s" % (
    T1, T2, slabVol, T3, allp))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
