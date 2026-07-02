# encoding: utf-8
# v2 g16 (FULL MCP COVERAGE): the 28->43 tool expansion. Smoke-tests every NEW tool through the
# REAL LlmToolDispatcher.Dispatch switch (the exact path McpServer routes), headless. import_step
# is excluded by design (Document.Open(.stp) hangs behind a translator dialog in /Headless; it has
# its own interactive sub-gate g16b).
#   T0  tools/list: 43 defs, all 15 new names present (UTF-16-safe presence check).
#   T1  parse_spec: valid / typo-warn / invalid, no geometry.
#   T2  generate_phone(camera) -> measure_body V0 / validate_body pass+minwall.
#   T3  add_hole -> measure_body V1 < V0 (dV reasoning end-to-end).
#   T4  fea_freeze scdocx (file + volume parity) & step (downgraded_from_step on Student).
#   T5  get_parameters -> parse_spec ROUND-TRIP: 0 errors 0 warnings, camera height echoes.
#   T6  set_parameters: length patch grows bbox; camera-height patch echoes in get_parameters.
#   T7  generate_tensile_specimen D638-I (thickness override 4) -> >=5 bodies, bbox min ~4.
#   T8  create_bending_fixture on the specimen -> 6 bodies, span>0.
#   T9  generate_laminate 0.5/1.0/0.5 -> 3 bodies; detect_contacts(Layer)=2 planar ~5000mm2.
#   T10 simplify_bodies Layer_2 BoundingBox -> matched=processed=1.
#   T11 slab: cut_void Cuboid 10x10x4 dV~-400; apply_operations([cut_void]) batchable.
#   T12 laminate_body 3.7+3.7 (delete_original) -> 2 plies + stale-session guard survives.
#   T13 rebind_active_body -> bound.
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import WriteBlock, Task
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import LlmToolDispatcher, LlmToolRegistry
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Mcp import SessionContext, McpToolAdapter

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g16_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g16_mark.txt"
FRZ = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g16_freeze"
def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass
log = []
def emit(s):
    try: log.append("".join(ch for ch in str(s) if ord(ch) >= 32 or ch == "\t"))
    except Exception: log.append("(unprintable)")

NEW_TOOLS = ["validate_body", "measure_body", "fea_freeze", "parse_spec", "get_parameters",
             "set_parameters", "rebind_active_body", "import_step", "generate_tensile_specimen",
             "generate_laminate", "laminate_body", "cut_void", "simplify_bodies",
             "create_bending_fixture", "detect_contacts"]

def jesc(s):
    return s.replace("\\", "\\\\").replace('"', '\\"')

def call(tool, argsJson, body=None, graph=None):
    env = LlmToolDispatcher.Dispatch(body, graph, tool, argsJson)
    return ('"success": true' in env), (env or "(null)")

def num_after(s, key):
    try:
        i = s.index(key) + len(key)
        j = i
        while j < len(s) and (s[j].isdigit() or s[j] in "-.eE+"):
            j += 1
        return float(s[i:j])
    except Exception:
        return None

def bbox_of(env):
    try:
        seg = env.split('"bbox_size_mm": [')[1].split("]")[0]
        return [float(x) for x in seg.split(",")]
    except Exception:
        return None

def camera_height(env):
    try:
        seg = env.split('"camera": {')[1].split("}")[0]
        return num_after(seg, '"height_mm": ')
    except Exception:
        return None

H = {}
def _do():
    _mk("do-start")
    sc = SessionContext.Instance

    # ---- T0 registry / tools list --------------------------------------
    tl = McpToolAdapter.ToolsListJson()
    n = LlmToolRegistry.GetAllTools().Count
    missing = [t for t in NEW_TOOLS if ('"name": "%s"' % t) not in tl]
    H["t0"] = (n == 43 and len(missing) == 0)
    _mk("T0 count=%d missing=%s" % (n, ",".join(missing) if missing else "-"))

    # ---- T1 parse_spec (no body, no geometry) ---------------------------
    ok1a, e1a = call("parse_spec", '{"spec_json": "%s"}' % jesc('{"length_mm":150,"width_mm":72,"thickness_mm":8}'))
    ok1b, e1b = call("parse_spec", '{"spec_json": "%s"}' % jesc('{"lenght_mm":150}'))
    ok1c, e1c = call("parse_spec", '{"spec_json": "%s"}' % jesc('{"thickness_mm":-5}'))
    H["t1"] = (ok1a and '"valid": true' in e1a
               and ok1b and '"warnings": ["' in e1b
               and ok1c and '"valid": false' in e1c)
    _mk("T1 %s|%s|%s" % ('"valid": true' in e1a, '"warnings": ["' in e1b, '"valid": false' in e1c))

    # ---- T2 generate_phone + measure/validate ---------------------------
    ok2, e2 = call("generate_phone", '{"camera_bump_mm": 1.5}')
    body = sc.Body; graph = sc.Graph
    okm, em = call("measure_body", "{}", body, graph)
    v0 = num_after(em, '"volume_mm3": ')
    okv, ev = call("validate_body", "{}", body, graph)
    mw = num_after(ev, '"min_wall_mm": ')
    H["t2"] = (ok2 and okm and okv and v0 is not None and v0 > 0
               and '"closed_solid": true' in em and '"pass": true' in ev
               and mw is not None and mw >= 0.55)
    _mk("T2 gen=%s V0=%s minwall=%s" % (ok2, v0, mw))

    # ---- T3 add_hole -> dV ----------------------------------------------
    ok3, e3 = call("add_hole", '{"position_mm": [30, 0, 7.4], "diameter_mm": 2, "through": true}', body, graph)
    okm2, em2 = call("measure_body", "{}", body, graph)
    v1 = num_after(em2, '"volume_mm3": ')
    H["t3"] = (ok3 and v1 is not None and v0 is not None and v1 < v0 - 0.5)
    _mk("T3 hole=%s V1=%s dV=%s" % (ok3, v1, (v1 - v0) if (v1 and v0) else None))

    # ---- T4 fea_freeze scdocx + step-downgrade ---------------------------
    ok4a, e4a = call("fea_freeze", '{"out_path": "%s"}' % jesc(FRZ + ".scdocx"), body, graph)
    fvol = num_after(e4a, '"volume_mm3": ')
    ok4b, e4b = call("fea_freeze", '{"out_path": "%s", "format": "step"}' % jesc(FRZ + "2.stp"), body, graph)
    H["t4"] = (ok4a and File.Exists(FRZ + ".scdocx")
               and fvol is not None and v1 is not None and abs(fvol - v1) < 0.01
               and ok4b and '"downgraded_from_step": true' in e4b)
    _mk("T4 scdocx=%s parity=%s stepdown=%s" % (ok4a, fvol, '"downgraded_from_step": true' in e4b))

    # ---- T5 get_parameters -> parse_spec ROUND-TRIP ----------------------
    ok5, e5 = call("get_parameters", "{}")
    rt = None; rtok = False; ch = None
    if ok5:
        try:
            pj = e5.split('"result": ')[1]
            pj = pj[:pj.rfind("}")]  # strip the envelope's closing brace
            ch = camera_height(pj)
            ok5b, e5b = call("parse_spec", '{"spec_json": "%s"}' % jesc(pj))
            rt = e5b
            rtok = (ok5b and '"valid": true' in e5b and '"errors": []' in e5b and '"warnings": []' in e5b)
        except Exception as ex:
            _mk("T5 EX " + str(ex))
    H["t5"] = (ok5 and rtok and ch is not None and abs(ch - 1.5) < 1e-6)
    _mk("T5 get=%s roundtrip=%s camH=%s" % (ok5, rtok, ch))

    # ---- T6 set_parameters: length patch + camera patch ------------------
    ok6a, e6a = call("set_parameters", '{"spec_patch": "%s"}' % jesc('{"length_mm":160}'))
    body = sc.Body; graph = sc.Graph
    okm3, em3 = call("measure_body", "{}", body, graph)
    bb3 = bbox_of(em3)
    grew = (bb3 is not None and abs(max(bb3) - 160.0) < 0.5)
    ok6b, e6b = call("set_parameters", '{"spec_patch": "%s"}' % jesc('{"camera":{"height_mm":2.2}}'))
    body = sc.Body; graph = sc.Graph
    ok6c, e6c = call("get_parameters", "{}")
    ch2 = None
    try:
        pj2 = e6c.split('"result": ')[1]
        ch2 = camera_height(pj2)
    except Exception: pass
    H["t6"] = (ok6a and grew and ok6b and ok6c and ch2 is not None and abs(ch2 - 2.2) < 1e-6)
    _mk("T6 len160=%s bbmax=%s camPatch=%s camH2=%s" % (ok6a, max(bb3) if bb3 else None, ok6b, ch2))

    # ---- T7 tensile specimen (override thickness=4) ----------------------
    ok7, e7 = call("generate_tensile_specimen",
                   '{"standard": "ASTM_D638_TypeI", "overrides": {"thickness_mm": 4}}')
    body = sc.Body; graph = sc.Graph
    okm4, em4 = call("measure_body", "{}", body, graph)
    bb4 = bbox_of(em4)
    created = 0
    try:
        seg = e7.split('"bodies_created": [')[1].split("]")[0]
        created = 0 if seg.strip() == "" else seg.count('"') // 2
    except Exception: pass
    H["t7"] = (ok7 and created >= 5 and bb4 is not None and abs(bb4[0] - 4.0) < 0.05)
    _mk("T7 spec=%s created=%d bbmin=%s" % (ok7, created, bb4[0] if bb4 else None))

    # ---- T8 bending fixture on the specimen ------------------------------
    ok8, e8 = call("create_bending_fixture", "{}", body, graph)
    span = num_after(e8, '"span_mm": ')
    fx = 0
    try:
        seg = e8.split('"bodies_created": [')[1].split("]")[0]
        fx = 0 if seg.strip() == "" else seg.count('"') // 2
    except Exception: pass
    H["t8"] = (ok8 and fx == 6 and span is not None and span > 0)
    _mk("T8 fixture=%s bodies=%d span=%s" % (ok8, fx, span))

    # ---- T9 laminate + detect_contacts ------------------------------------
    ok9, e9 = call("generate_laminate",
                   '{"width_mm": 100, "length_mm": 50, "layers": [' +
                   '{"name": "Layer_1", "thickness_mm": 0.5}, {"name": "Layer_2", "thickness_mm": 1.0}, ' +
                   '{"name": "Layer_3", "thickness_mm": 0.5}]}')
    body = sc.Body; graph = sc.Graph
    ok9b, e9b = call("detect_contacts", '{"keyword": "Layer", "tolerance_mm": 0.05}', body, graph)
    cnt = num_after(e9b, '"count": ')
    areas = []
    for pseg in e9b.split('"area_mm2": ')[1:]:
        j = 0
        while j < len(pseg) and (pseg[j].isdigit() or pseg[j] in "-.eE+"):
            j += 1
        try: areas.append(float(pseg[:j]))
        except Exception: pass
    area_ok = (len(areas) == 2 and all(abs(a - 5000.0) < 50.0 for a in areas))
    H["t9"] = (ok9 and '"total_thickness_mm": 2' in e9 and ok9b and cnt == 2
               and e9b.count('"type": "planar"') == 2 and area_ok)
    _mk("T9 lam=%s contacts=%s areas_ok=%s" % (ok9, cnt, area_ok))

    # ---- T10 simplify_bodies ----------------------------------------------
    ok10, e10 = call("simplify_bodies", '{"keyword": "Layer_2", "mode": "BoundingBox"}', body, graph)
    H["t10"] = (ok10 and '"matched": 1' in e10 and '"processed": 1' in e10)
    _mk("T10 simplify=%s %s" % (ok10, e10[:160]))

    # ---- T11 slab: cut_void + apply_operations ----------------------------
    ok11a, e11a = call("generate_phone", '{"stop_at_stage": "S00"}')
    body = sc.Body; graph = sc.Graph
    okm5, em5 = call("measure_body", "{}", body, graph)
    vs0 = num_after(em5, '"volume_mm3": ')
    ok11b, e11b = call("cut_void",
                       '{"shape": "Cuboid", "dim1_mm": 10, "dim2_mm": 10, "dim3_mm": 4, ' +
                       '"position_mm": [0, 0, 3.7], "mode": "Subtract"}', body, graph)
    okm6, em6 = call("measure_body", "{}", body, graph)
    vs1 = num_after(em6, '"volume_mm3": ')
    dv_ok = (vs0 is not None and vs1 is not None and abs((vs0 - vs1) - 400.0) < 4.0)
    ok11c, e11c = call("apply_operations",
                       '{"operations": [{"tool": "cut_void", "args": {"shape": "Cuboid", ' +
                       '"dim1_mm": 10, "dim2_mm": 10, "dim3_mm": 4, "position_mm": [30, 0, 3.7], ' +
                       '"mode": "Subtract"}}]}', body, graph)
    okm7, em7 = call("measure_body", "{}", body, graph)
    vs2 = num_after(em7, '"volume_mm3": ')
    batch_ok = (ok11c and '"applied": 1' in e11c and vs2 is not None and abs((vs1 - vs2) - 400.0) < 4.0)
    H["t11"] = (ok11a and ok11b and dv_ok and batch_ok)
    _mk("T11 slab=%s dV1=%s batch=%s dV2=%s" % (ok11a, (vs0 - vs1) if (vs0 and vs1) else None,
                                                 ok11c, (vs1 - vs2) if (vs1 and vs2) else None))

    # ---- T12 laminate_body (delete_original -> stale-session guard) -------
    ok12, e12 = call("laminate_body",
                     '{"layers": [{"thickness_mm": 3.7}, {"thickness_mm": 3.7}]}', body, graph)
    plies = 0
    try:
        seg = e12.split('"layers_created": [')[1].split("]")[0]
        plies = 0 if seg.strip() == "" else seg.count('"') // 2
    except Exception: pass
    # the guard must have rebound the session to a surviving body; measure must still work.
    body = sc.Body; graph = sc.Graph
    okm8, em8 = call("measure_body", "{}", body, graph)
    H["t12"] = (ok12 and plies == 2 and okm8 and '"volume_mm3": ' in em8)
    _mk("T12 lam_body=%s plies=%d session_alive=%s" % (ok12, plies, okm8))

    # ---- T13 rebind_active_body -------------------------------------------
    ok13, e13 = call("rebind_active_body", "{}")
    H["t13"] = (ok13 and '"bound": true' in e13)
    _mk("T13 rebind=%s" % ok13)

try:
    for f in [FRZ + ".scdocx", FRZ + "2.scdocx", FRZ + "2.stp"]:
        try:
            if File.Exists(f): File.Delete(f)
        except Exception: pass
    WriteBlock.ExecuteTask("g16", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name + ": " + e.Message)
    emit("WB THREW: %s: %s" % (e.GetType().Name, e.Message))

keys = ["t%d" % i for i in range(14)]
for k in keys:
    emit("%s %s" % (k.upper(), H.get(k)))
allp = all(bool(H.get(k)) for k in keys)
emit("G16_PASS ALL=%s (%d/14)" % (allp, sum(1 for k in keys if H.get(k))))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
