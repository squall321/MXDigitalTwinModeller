# encoding: utf-8
# g18 (licensed-seat CAE tools, 43->46): what CAN run under Student runs for real; what is
# license-gated must fail GRACEFULLY (honest error envelope + full diagnostic log, no throw).
#   T0 tools/list == 46, all 3 new names advertised.
#   T1 surface_laminate FULL runtime verify: S00 slab top face via seed -> 2 plies; layer
#      volumes are EXACT (area x thickness); detect_contacts sees slab~L1 and L1~L2.
#   T2 conformal_mesh on the same 3-body doc: interfaces==2 (kernel truth); mesh_ok is the
#      LIVE Student-capability probe - either verdict accepted, envelope must be honest
#      (mesh_ok:false => success:false + log). Result is RECORDED for the license decision.
#   T3 mesh_with_gmsh: on Student the STEP export is refused -> success:false + non-empty
#      log, and NEVER "Dispatch threw". (On a licensed seat it would return node counts.)
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import WriteBlock, Task
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import LlmToolDispatcher, LlmToolRegistry
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Mcp import SessionContext, McpToolAdapter

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g18_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g18_mark.txt"
def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass
log = []
def emit(s):
    try: log.append(str(s))
    except Exception: log.append("(unprintable)")

NEW = ["mesh_with_gmsh", "conformal_mesh", "surface_laminate"]

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

    # ---- T0 registry --------------------------------------------------
    tl = McpToolAdapter.ToolsListJson()
    n = LlmToolRegistry.GetAllTools().Count
    missing = [t for t in NEW if ('"name": "%s"' % t) not in tl]
    H["t0"] = (n == 46 and len(missing) == 0)
    _mk("T0 count=%d missing=%s" % (n, ",".join(missing) if missing else "-"))

    # ---- T1 surface_laminate (fully runtime-verifiable) ----------------
    e0 = LlmToolDispatcher.Dispatch(None, None, "generate_phone", '{"stop_at_stage": "S00"}')
    body = sc.Body; graph = sc.Graph
    e1 = LlmToolDispatcher.Dispatch(body, graph, "surface_laminate",
        '{"seed_mm": [0, 0, 7.4], "layers": [' +
        '{"name": "Ply_A", "thickness_mm": 0.3}, {"name": "Ply_B", "thickness_mm": 0.2}]}')
    _mk("T1 e1 " + (e1[:300] if e1 else "(null)"))
    # kernel-truth: the plies are new sibling bodies whose contacts with the slab/each other
    # are full top-face-area planar interfaces (146.7 x 71.5 = 10489.05 mm2, sharp S00).
    e2 = LlmToolDispatcher.Dispatch(body, graph, "detect_contacts", '{"tolerance_mm": 0.05}')
    cnt = num_after(e2, '"count": ')
    areas = []
    for pseg in e2.split('"area_mm2": ')[1:]:
        j = 0
        while j < len(pseg) and (pseg[j].isdigit() or pseg[j] in "-.eE+"):
            j += 1
        try: areas.append(float(pseg[:j]))
        except Exception: pass
    area_ok = (len(areas) == 2 and all(abs(a - 10489.05) < 110.0 for a in areas))
    H["t1"] = (('"success": true' in e0) and ('"success": true' in e1)
               and ('"layers_created": ["Ply_A", "Ply_B"]' in e1)
               and ('"total_thickness_mm": 0.5' in e1)
               and cnt == 2 and area_ok)
    _mk("T1 contacts=%s areas=%s" % (cnt, areas))

    # ---- T2 conformal_mesh (interfaces = kernel truth; mesh+EXPORT = live probe) ----
    KOUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g18_conformal.k"
    try:
        if File.Exists(KOUT): File.Delete(KOUT)
    except Exception: pass
    e3 = LlmToolDispatcher.Dispatch(body, graph, "conformal_mesh",
        '{"tolerance_mm": 0.05, "element_size_mm": 5, "split_cylinder_edges": false, '
        '"export_path": "%s"}' % KOUT.replace("\\", "\\\\"))
    _mk("T2 e3 " + (e3[:400] if e3 else "(null)"))
    ifc = num_after(e3, '"interfaces": ')
    mesh_ok = '"mesh_ok": true' in e3
    exp_ok = '"export_ok": true' in e3
    threw = "Dispatch threw" in (e3 or "")
    if mesh_ok:
        # mesh works on this seat (proven live earlier) -> the export signal must be REAL:
        # export_ok true iff the .k file actually exists.
        env_ok = ('"success": true' in e3) == (mesh_ok and exp_ok)
        H["t2"] = (ifc == 2 and env_ok and exp_ok and File.Exists(KOUT) and not threw)
    else:
        env_ok = ('"success": false' in e3 and '"log": [' in e3)
        H["t2"] = (ifc == 2 and env_ok and not threw)
    H["t2_meshprobe"] = mesh_ok
    _mk("T2 interfaces=%s mesh_ok=%s export_ok=%s kfile=%s" % (
        ifc, mesh_ok, exp_ok, File.Exists(KOUT)))

    # ---- T3 mesh_with_gmsh (license-gated; graceful degrade contract) ----
    e4 = LlmToolDispatcher.Dispatch(body, graph, "mesh_with_gmsh", '{"element_size_mm": 8}')
    _mk("T3 e4 " + (e4[:400] if e4 else "(null)"))
    threw4 = "Dispatch threw" in (e4 or "")
    if '"success": true' in e4:
        nodes = num_after(e4, '"nodes": ')
        H["t3"] = (nodes is not None and nodes > 0)
        H["t3_licensed"] = True
    else:
        H["t3"] = ('"log": [' in e4 and not threw4)
        H["t3_licensed"] = False
    _mk("T3 licensed_path=%s" % H.get("t3_licensed"))

try:
    WriteBlock.ExecuteTask("g18", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name + ": " + e.Message)
    emit("WB THREW: %s: %s" % (e.GetType().Name, e.Message))

for k in ["t0", "t1", "t2", "t3"]:
    emit("%s %s" % (k.upper(), H.get(k)))
emit("PROBE conformal_mesh_under_student=%s gmsh_step_export_under_student=%s" % (
    H.get("t2_meshprobe"), H.get("t3_licensed")))
allp = all(bool(H.get(k)) for k in ["t0", "t1", "t2", "t3"])
emit("G18_PASS ALL=%s (%d/4)" % (allp, sum(1 for k in ["t0", "t1", "t2", "t3"] if H.get(k))))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
