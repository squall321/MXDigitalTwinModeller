# encoding: utf-8
# g23 (general-CAD primitives): the six new MCP tools must produce KERNEL-TRUE geometry
# vs independent analytics:
#   R1 revolve 360  : washer pi(R^2-r^2)h exact          R2 revolve 90deg + X-axis variant
#   S1 sweep solid  : Pappus pi r^2 * path-length exact  S2 sweep pipe (wall)
#   L1 loft frustum : cone frustum exact                 L2 loft rect pyramid frustum
#   P1 split        : piece volumes sum to the original, original deleted
#   D1 draft        : integral (w +/- 2 z tan a)^2 dz matches one taper sense, 4 faces
#   T1 move pattern : 3 copies + original, centers stepped
#   T2 rotate pattern + scale: 5 circular copies; scale x2 -> volume x8
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
import math
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.Geometry import Matrix, Vector
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry import BodyBuilder
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import LlmToolDispatcher

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g23_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g23_mark.txt"
DONE = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g23_done.txt"

def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass
log = []
def emit(s):
    try: log.append(str(s))
    except Exception: log.append("(unprintable)")

H = {}

def vol_mm3(db):
    return db.Shape.Volume * 1e9

def body_by_name(part, name):
    for b in part.Bodies:
        if b.Name == name: return b
    return None

def env_vol(env, key="volume_mm3"):
    import re
    m = re.search('"%s": ([-0-9.eE]+)' % key, env)
    return float(m.group(1)) if m else float("nan")

def fresh_box(name="Box", w=10.0, d=10.0, h=8.0):
    Document.Create()
    part = Window.ActiveWindow.Document.MainPart
    blk = BodyBuilder.CreateBlock(w / 1000.0, d / 1000.0, h / 1000.0)
    return part, BodyBuilder.CreateDesignBody(part, name, blk)

def _do():
    _mk("do-start")

    # ---- R1/R2: revolve ------------------------------------------------------
    Document.Create()
    env = LlmToolDispatcher.Dispatch(None, None, "revolve_profile",
        '{"profile_rz_mm": [[3,0],[5,0],[5,2],[3,2]], "name": "Washer"}')
    v = env_vol(env)
    exp = math.pi * (25 - 9) * 2
    ok = '"success": true' in env and abs(v - exp) < exp * 0.005
    _mk("R1 washer=%.4f(exp %.4f) -> %s" % (v, exp, ok))
    H["R1"] = ok

    Document.Create()
    envA = LlmToolDispatcher.Dispatch(None, None, "revolve_profile",
        '{"profile_rz_mm": [[3,0],[5,0],[5,2],[3,2]], "angle_deg": 90, "name": "Quarter"}')
    vA = env_vol(envA)
    Document.Create()
    envB = LlmToolDispatcher.Dispatch(None, None, "revolve_profile",
        '{"profile_rz_mm": [[3,0],[5,0],[5,2],[3,2]], "axis_point_mm": [0,0,10], '
        '"axis_dir": [1,0,0], "name": "XRev"}')
    vB = env_vol(envB)
    ok = (abs(vA - exp / 4) < exp / 4 * 0.005 and abs(vB - exp) < exp * 0.005)
    _mk("R2 quarter=%.4f(exp %.4f) xAxis=%.4f(exp %.4f) -> %s" % (vA, exp / 4, vB, exp, ok))
    H["R2"] = ok

    # ---- S1/S2: sweep --------------------------------------------------------
    plen = 15.0 + 10.0 + math.pi / 2 * 5.0
    Document.Create()
    env = LlmToolDispatcher.Dispatch(None, None, "sweep_profile",
        '{"path_mm": [[0,0,0],[20,0,0],[20,15,0]], "dia_mm": 3, "corner_r_mm": 5, "name": "Tube"}')
    v = env_vol(env)
    exp = math.pi * 1.5 * 1.5 * plen
    ok = '"success": true' in env and abs(v - exp) < exp * 0.01
    _mk("S1 tube=%.3f(exp %.3f) -> %s" % (v, exp, ok))
    H["S1"] = ok

    Document.Create()
    env = LlmToolDispatcher.Dispatch(None, None, "sweep_profile",
        '{"path_mm": [[0,0,0],[20,0,0],[20,15,0]], "dia_mm": 3, "corner_r_mm": 5, '
        '"wall_mm": 0.5, "name": "Pipe"}')
    v = env_vol(env)
    exp2 = math.pi * (1.5 ** 2 - 1.0 ** 2) * plen
    ok = '"success": true' in env and abs(v - exp2) < exp2 * 0.01
    _mk("S2 pipe=%.3f(exp %.3f) -> %s" % (v, exp2, ok))
    H["S2"] = ok

    # ---- L1/L2: loft ---------------------------------------------------------
    Document.Create()
    env = LlmToolDispatcher.Dispatch(None, None, "loft_profiles",
        '{"sections": [{"shape": "circle", "dia_mm": 10, "center_mm": [0,0,0]}, '
        '{"shape": "circle", "dia_mm": 4, "center_mm": [0,0,8]}], "ruled": true, "name": "Funnel"}')
    v = env_vol(env)
    exp = math.pi * 8 / 3 * (25 + 10 + 4)
    ok = '"success": true' in env and abs(v - exp) < exp * 0.01
    _mk("L1 frustum=%.3f(exp %.3f) -> %s" % (v, exp, ok))
    H["L1"] = ok

    Document.Create()
    env = LlmToolDispatcher.Dispatch(None, None, "loft_profiles",
        '{"sections": [{"shape": "rect", "w_mm": 8, "h_mm": 8, "center_mm": [0,0,0]}, '
        '{"shape": "rect", "w_mm": 4, "h_mm": 4, "center_mm": [0,0,6]}], "ruled": true, "name": "Pyr"}')
    v = env_vol(env)
    exp2 = 6.0 / 3 * (64 + 16 + math.sqrt(64 * 16))
    ok = '"success": true' in env and abs(v - exp2) < exp2 * 0.01
    _mk("L2 pyrFrustum=%.3f(exp %.3f) -> %s" % (v, exp2, ok))
    H["L2"] = ok

    # ---- P1: split -----------------------------------------------------------
    part, box = fresh_box("Box", 10, 10, 8)
    env = LlmToolDispatcher.Dispatch(box, None, "split_body",
        '{"plane_point_mm": [0,0,3], "plane_normal": [0,0,1]}')
    vb, va = env_vol(env, "volume_below_mm3"), env_vol(env, "volume_above_mm3")
    ok = ('"success": true' in env and abs(vb - 300) < 1 and abs(va - 500) < 1
          and body_by_name(part, "Box") is None
          and body_by_name(part, "Box_Below") is not None
          and body_by_name(part, "Box_Above") is not None)
    _mk("P1 split below=%.2f above=%.2f -> %s" % (vb, va, ok))
    H["P1"] = ok

    # ---- S3/S4: sweep corner-fit + hairpin must fail LOUDLY --------------------
    Document.Create()
    env = LlmToolDispatcher.Dispatch(None, None, "sweep_profile",
        '{"path_mm": [[0,0,0],[3,0,0],[3,30,0]], "dia_mm": 2, "corner_r_mm": 5}')
    H["S3"] = '"success": false' in env and 'does not fit' in env
    _mk("S3 oversized corner -> %s | %s" % (H["S3"], env[:150]))
    env = LlmToolDispatcher.Dispatch(None, None, "sweep_profile",
        '{"path_mm": [[0,0,0],[20,0,0],[0,1.75,0]], "dia_mm": 2, "corner_r_mm": 2}')
    H["S4"] = '"success": false' in env and 'near-reversal' in env
    _mk("S4 hairpin -> %s | %s" % (H["S4"], env[:150]))

    # ---- P2/P3: far plane point (anchor fix) + plane miss ----------------------
    part, box = fresh_box("FBox", 10, 10, 8)
    env = LlmToolDispatcher.Dispatch(box, None, "split_body",
        '{"plane_point_mm": [500,-300,3], "plane_normal": [0,0,1]}')
    vb, va = env_vol(env, "volume_below_mm3"), env_vol(env, "volume_above_mm3")
    H["P2"] = ('"success": true' in env and abs(vb - 300) < 1 and abs(va - 500) < 1
               and '"session_bound_to": "FBox_Below"' in env)
    _mk("P2 far-point split below=%.2f above=%.2f -> %s" % (vb, va, H["P2"]))
    part, box = fresh_box("MBox", 10, 10, 8)
    env = LlmToolDispatcher.Dispatch(box, None, "split_body",
        '{"plane_point_mm": [0,0,20], "plane_normal": [0,0,1]}')
    H["P3"] = '"success": false' in env and 'does not cut' in env
    _mk("P3 plane-miss -> %s | %s" % (H["P3"], env[:140]))

    # ---- T3E: copy=false with count>1 must fail LOUDLY -------------------------
    part, box = fresh_box("EBox", 5, 5, 5)
    env = LlmToolDispatcher.Dispatch(box, None, "transform_body",
        '{"op": "move", "translation_mm": [10,0,0], "copy": false, "count": 3}')
    H["T3E"] = '"success": false' in env and 'copy=false requires count=1' in env
    _mk("T3E copy=false pattern -> %s" % H["T3E"])

    # ---- D1: draft -----------------------------------------------------------
    part, box = fresh_box("DBox", 10, 10, 6)
    env = LlmToolDispatcher.Dispatch(box, None, "draft_body",
        '{"neutral_point_mm": [0,0,0], "pull_dir": [0,0,1], "angle_deg": 5}')
    t = math.tan(5 * math.pi / 180)
    def draft_v(sign):
        # V = integral over z of (10 + sign*2*z*t)^2 dz, z in [0, 6]
        return 100 * 6 + sign * 20 * t * 36 + (4 * t * t / 3) * 216
    v = env_vol(env, "volume_after_mm3")
    okp = abs(v - draft_v(+1)) < draft_v(+1) * 0.005
    okm = abs(v - draft_v(-1)) < draft_v(-1) * 0.005
    ok = '"faces": 4' in env and '"skipped_nonplanar_faces": 0' in env and (okp or okm)
    _mk("D1 draft v=%.3f (grow %.3f / shrink %.3f) sense=%s -> %s" % (
        v, draft_v(+1), draft_v(-1), "grow" if okp else ("shrink" if okm else "?"), ok))
    H["D1"] = ok

    # ---- T1: linear move pattern ----------------------------------------------
    part, box = fresh_box("Cube", 5, 5, 5)
    env = LlmToolDispatcher.Dispatch(box, None, "transform_body",
        '{"op": "move", "translation_mm": [10,0,0], "copy": true, "count": 3}')
    ok = '"success": true' in env
    if ok:
        names = ["Cube", "Cube_1", "Cube_2", "Cube_3"]
        vols = [body_by_name(part, n) for n in names]
        ok = all(b is not None and abs(vol_mm3(b) - 125) < 0.5 for b in vols)
        cx = (body_by_name(part, "Cube_3").Shape.GetBoundingBox(Matrix.Identity).MinCorner.X
              + body_by_name(part, "Cube_3").Shape.GetBoundingBox(Matrix.Identity).MaxCorner.X) / 2 * 1000
        ok = ok and abs(cx - 30.0) < 0.05
        _mk("T1 4 cubes, last cx=%.2f(exp 30) -> %s" % (cx, ok))
    else:
        _mk("T1 env=" + env[:160])
    H["T1"] = ok

    # ---- T2: circular pattern + scale ------------------------------------------
    Document.Create()
    part = Window.ActiveWindow.Document.MainPart
    cyl = BodyBuilder.CreateCylinder(2.0 / 1000, 5.0 / 1000)
    cyl.Transform(Matrix.CreateTranslation(Vector.Create(15.0 / 1000, 0, 0)))
    db = BodyBuilder.CreateDesignBody(part, "Peg", cyl)
    env = LlmToolDispatcher.Dispatch(db, None, "transform_body",
        '{"op": "rotate", "axis_dir": [0,0,1], "angle_deg": 60, "copy": true, "count": 5}')
    ok = '"success": true' in env
    if ok:
        vexp = math.pi * 4 * 5
        cnt = sum(1 for b in part.Bodies if abs(vol_mm3(b) - vexp) < vexp * 0.01)
        env2 = LlmToolDispatcher.Dispatch(db, None, "transform_body",
            '{"op": "scale", "body_name": "Peg_3", "factor": 2}')
        v3 = vol_mm3(body_by_name(part, "Peg_3"))
        ok = cnt == 6 and '"success": true' in env2 and abs(v3 - vexp * 8) < vexp * 8 * 0.01
        _mk("T2 pegs=%d(exp 6) scaled=%.2f(exp %.2f) -> %s" % (cnt, v3, vexp * 8, ok))
    else:
        _mk("T2 env=" + env[:160])
    H["T2"] = ok

try:
    WriteBlock.ExecuteTask("g23", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name + ": " + e.Message)
    emit("WB THREW: %s: %s" % (e.GetType().Name, e.Message))

KEYS = ["R1", "R2", "S1", "S2", "S3", "S4", "L1", "L2", "P1", "P2", "P3", "D1", "T1", "T2", "T3E"]
for k in KEYS:
    emit("%s %s" % (k, H.get(k)))
allp = all(bool(H.get(k)) for k in KEYS)
emit("G23_PASS ALL=%s (%d/15)" % (allp, sum(1 for k in KEYS if H.get(k))))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
File.WriteAllText(DONE, "done\n", UTF8Encoding(False))
