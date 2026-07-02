# encoding: utf-8
# g16b (import_step, the one tool g16 could not cover): runs in a GUI SpaceClaim instance driven
# by /RunScript (no /Headless flag - the documented STEP-translator hang is specific to /Headless).
#   T1  import_step on a real assembly STEP (as1-oc-214) -> imported:true, session bound.
#   T2  the session now targets the IMPORT: measure_body on sc.Body returns volume > 0.
#   T3  the stale-body trap is closed: a mod tool (add_hole) dispatched with the session body
#       mutates the IMPORTED body (volume decreases), not the previous session body.
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import WriteBlock, Task
from SpaceClaim.Api.V252.Geometry import Matrix
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import LlmToolDispatcher
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Mcp import SessionContext

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g16b_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g16b_mark.txt"
STEP = r"D:\MXDigitalTwinModeller\Test\RealCAD\stepcode\as1-oc-214.stp"
def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass
log = []
def emit(s):
    try: log.append("".join(ch for ch in str(s) if ord(ch) >= 32 or ch == "\t"))
    except Exception: log.append("(unprintable)")

def num_after(s, key):
    try:
        i = s.index(key) + len(key)
        j = i
        while j < len(s) and (s[j].isdigit() or s[j] in "-.eE+"):
            j += 1
        return float(s[i:j])
    except Exception:
        return None

H = {}
def _do():
    _mk("do-start")
    sc = SessionContext.Instance
    # generate a phone FIRST so a session body exists - proving import_step re-points a
    # NON-null session (the exact stale-body trap the wiring must close).
    e0 = LlmToolDispatcher.Dispatch(None, None, "generate_phone", '{"stop_at_stage": "S00"}')
    H["pre"] = '"success": true' in e0
    preName = sc.Body.Name if sc.Body is not None else "(none)"
    _mk("pre gen=%s body=%s" % (H["pre"], preName))

    e1 = LlmToolDispatcher.Dispatch(None, None, "import_step",
        '{"path": "%s"}' % STEP.replace("\\", "\\\\"))
    H["t1"] = ('"success": true' in e1) and ('"imported": true' in e1)
    _mk("T1 " + e1[:300])

    body = sc.Body; graph = sc.Graph
    impName = body.Name if body is not None else "(none)"
    H["rebound"] = (body is not None and impName != preName)
    _mk("rebound %s -> %s" % (preName, impName))

    e2 = LlmToolDispatcher.Dispatch(body, graph, "measure_body", "{}")
    v0 = num_after(e2, '"volume_mm3": ')
    H["t2"] = ('"success": true' in e2) and (v0 is not None and v0 > 0)
    _mk("T2 vol=%s" % v0)

    # mutate the imported body via cut_void: clip a corner of the nut's hex flat with a
    # small cuboid (a manifold-safe overlap Boolean). AddHole-through was tried first and
    # hit "Result may become non-manifold" on the nut's chamfer/thread geometry - a
    # geometry-class mod failure, not an import_step wiring issue. The nut (bbox
    # 20x15x3mm, bore at center) has solid material at the y=0 flat mid-edge.
    e3 = LlmToolDispatcher.Dispatch(body, graph, "cut_void",
        '{"shape": "Cuboid", "dim1_mm": 4, "dim2_mm": 4, "dim3_mm": 4, '
        '"position_mm": [10, 0, 1.5], "mode": "Subtract"}')
    _mk("e3 " + (e3[:400] if e3 else "(null)"))
    e4 = LlmToolDispatcher.Dispatch(body, graph, "measure_body", "{}")
    _mk("e4 " + (e4[:400] if e4 else "(null)"))
    v1 = num_after(e4, '"volume_mm3": ')
    H["t3"] = ('"success": true' in e3) and (v1 is not None and v0 is not None and v1 < v0 - 1e-6)
    _mk("T3 hole=%s v1=%s dV=%s" % ('"success": true' in e3, v1, (v1 - v0) if (v1 and v0) else None))

try:
    WriteBlock.ExecuteTask("g16b", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name + ": " + e.Message)
    emit("WB THREW: %s: %s" % (e.GetType().Name, e.Message))

emit("T1 import=%s (rebound=%s)" % (H.get("t1"), H.get("rebound")))
emit("T2 measure_on_import=%s" % H.get("t2"))
emit("T3 mod_targets_import=%s" % H.get("t3"))
allp = bool(H.get("t1")) and bool(H.get("rebound")) and bool(H.get("t2")) and bool(H.get("t3"))
emit("G16B_PASS ALL=%s" % allp)
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
