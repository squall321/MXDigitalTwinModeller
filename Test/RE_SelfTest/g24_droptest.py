# encoding: utf-8
# g24 (drop-test environment): pose/floor/impactor tools must be kernel-true:
#   T1 corner pose  : rigid (total volume EXACT-invariant), lowest point == gap
#   T2 edge pose    : fresh device, 45-deg edge attitude, same invariants
#   T3 floor        : sized from posed footprint+margin, top at z=0, and EXCLUDED
#                     from a subsequent re-pose (bodies_posed stays 2)
#   T4 ball impactor: sphere volume window (cube->RoundEdges trick), rest height
#   T5 pen impactor : frustum nose + shank analytic volume, cone_h echo, length
#   T6 error paths  : cancelling feature, unknown token, tip >= shank radius
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

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g24_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g24_mark.txt"
DONE = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g24_done.txt"

def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass
log = []
def emit(s):
    try: log.append(str(s))
    except Exception: log.append("(unprintable)")

H = {}

def env_num(env, key):
    import re
    m = re.search('"%s": ([-0-9.eE]+)' % key, env)
    return float(m.group(1)) if m else float("nan")

def body_by_name(part, name):
    for b in part.Bodies:
        if b.Name == name: return b
    return None

def device_doc():
    """Phone slab 30x15x8 + camera bump 6x6x2 on top. Total V = 3600 + 72 = 3672."""
    Document.Create()
    part = Window.ActiveWindow.Document.MainPart
    blk = BodyBuilder.CreateBlock(0.030, 0.015, 0.008)
    phone = BodyBuilder.CreateDesignBody(part, "Phone", blk)
    cam = BodyBuilder.CreateBlock(0.006, 0.006, 0.002)
    cam.Transform(Matrix.CreateTranslation(Vector.Create(0.008, 0.003, 0.008)))
    BodyBuilder.CreateDesignBody(part, "Camera", cam)
    return part, phone

def min_z_mm(part, exclude_prefixes=("DropFloor", "Impactor")):
    z = 1e9
    for b in part.Bodies:
        if any((b.Name or "").startswith(p) for p in exclude_prefixes): continue
        z = min(z, b.Shape.GetBoundingBox(Matrix.Identity).MinCorner.Z * 1000)
    return z

VDEV = 3672.0

def _do():
    _mk("do-start")

    # ---- T1: corner pose ------------------------------------------------------
    part, phone = device_doc()
    env = LlmToolDispatcher.Dispatch(phone, None, "pose_for_drop",
        '{"feature": "bottom_front_left", "gap_mm": 0.1}')
    vb, va = env_num(env, "volume_before_mm3"), env_num(env, "volume_after_mm3")
    mz = min_z_mm(part)
    H["T1"] = ('"success": true' in env and abs(vb - VDEV) < 0.01 and abs(va - VDEV) < 0.01
               and abs(env_num(env, "min_z_mm") - 0.1) < 0.005 and abs(mz - 0.1) < 0.005
               and env_num(env, "rotation_deg") > 1)
    _mk("T1 corner: V %.3f->%.3f rot=%.1f minz=%.4f -> %s" % (
        vb, va, env_num(env, "rotation_deg"), mz, H["T1"]))

    # ---- T2: edge pose (45 deg) ------------------------------------------------
    part, phone = device_doc()
    env = LlmToolDispatcher.Dispatch(phone, None, "pose_for_drop",
        '{"feature": "bottom_front", "gap_mm": 0.1}')
    va = env_num(env, "volume_after_mm3")
    H["T2"] = ('"success": true' in env and abs(va - VDEV) < 0.01
               and abs(env_num(env, "rotation_deg") - 45.0) < 0.5
               and abs(min_z_mm(part) - 0.1) < 0.005)
    _mk("T2 edge: rot=%.2f(exp 45) minz=%.4f -> %s" % (
        env_num(env, "rotation_deg"), min_z_mm(part), H["T2"]))

    # ---- T3: floor sized from footprint, excluded from re-pose ------------------
    env = LlmToolDispatcher.Dispatch(phone, None, "add_drop_floor",
        '{"margin_mm": 20, "thickness_mm": 10}')
    ok3 = '"success": true' in env
    if ok3:
        fl = body_by_name(part, "DropFloor")
        fb = fl.Shape.GetBoundingBox(Matrix.Identity)
        fw = (fb.MaxCorner.X - fb.MinCorner.X) * 1000
        fd = (fb.MaxCorner.Y - fb.MinCorner.Y) * 1000
        vexp = fw * fd * 10.0
        topz = fb.MaxCorner.Z * 1000
        # device xy bbox must sit inside floor minus margin tolerance
        db = phone.Shape.GetBoundingBox(Matrix.Identity)
        inside = (fb.MinCorner.X < db.MinCorner.X and fb.MaxCorner.X > db.MaxCorner.X
                  and fb.MinCorner.Y < db.MinCorner.Y and fb.MaxCorner.Y > db.MaxCorner.Y)
        env2 = LlmToolDispatcher.Dispatch(phone, None, "pose_for_drop",
            '{"feature": "bottom", "gap_mm": 0.1}')
        ok3 = (abs(env_num(env, "volume_mm3") - vexp) < vexp * 0.001 and abs(topz) < 0.01
               and inside and '"bodies_posed": 2' in env2)
        _mk("T3 floor: V=%.1f(exp %.1f) top=%.4f inside=%s reposed2=%s -> %s" % (
            env_num(env, "volume_mm3"), vexp, topz, inside, '"bodies_posed": 2' in env2, ok3))
    else:
        _mk("T3 env=" + env[:160])
    H["T3"] = ok3

    # ---- T4: ball impactor ------------------------------------------------------
    env = LlmToolDispatcher.Dispatch(phone, None, "add_impactor",
        '{"type": "ball", "target_mm": [0, 0, 8.1], "ball_dia_mm": 32}')
    ok4 = '"success": true' in env
    if ok4:
        vexp = 4.0 / 3 * math.pi * 16 ** 3
        v = env_num(env, "volume_mm3")
        bl = body_by_name(part, "Impactor_Ball")
        bb = bl.Shape.GetBoundingBox(Matrix.Identity)
        bmz = bb.MinCorner.Z * 1000
        dia = (bb.MaxCorner.Z - bb.MinCorner.Z) * 1000
        ok4 = (abs(v - vexp) < vexp * 0.015 and abs(bmz - 8.2) < 0.02 and abs(dia - 32) < 0.1)
        _mk("T4 ball: V=%.1f(exp %.1f) minz=%.3f(exp 8.2) dia=%.2f -> %s" % (v, vexp, bmz, dia, ok4))
    else:
        _mk("T4 env=" + env[:160])
    H["T4"] = ok4

    # ---- T5: pen impactor -------------------------------------------------------
    env = LlmToolDispatcher.Dispatch(phone, None, "add_impactor",
        '{"type": "pen", "target_mm": [5, 0, 8.1], "pen_tip_r_mm": 0.35, '
        '"pen_cone_deg": 120, "pen_shank_dia_mm": 6, "pen_len_mm": 60}')
    ok5 = '"success": true' in env
    if ok5:
        rs, rt = 3.0, 0.35
        coneH = (rs - rt) / math.tan(math.radians(60))
        # frustum + shank analytics (stacked-disc nose ~ frustum within slices error)
        vfr = math.pi * coneH / 3 * (rs * rs + rs * rt + rt * rt)
        vsh = math.pi * rs * rs * (60 - coneH)
        vexp = vfr + vsh
        v = env_num(env, "volume_mm3")
        pn = body_by_name(part, "Impactor_Pen")
        bb = pn.Shape.GetBoundingBox(Matrix.Identity)
        pmz = bb.MinCorner.Z * 1000
        plen = (bb.MaxCorner.Z - bb.MinCorner.Z) * 1000
        ok5 = (abs(v - vexp) < vexp * 0.02 and abs(env_num(env, "cone_h_mm") - coneH) < 0.01
               and abs(pmz - 8.2) < 0.02 and abs(plen - 60) < 0.1)
        _mk("T5 pen: V=%.1f(exp %.1f) coneH=%.3f(exp %.3f) minz=%.3f len=%.2f -> %s" % (
            v, vexp, env_num(env, "cone_h_mm"), coneH, pmz, plen, ok5))
    else:
        _mk("T5 env=" + env[:160])
    H["T5"] = ok5

    # ---- T6: loud error paths ---------------------------------------------------
    e1 = LlmToolDispatcher.Dispatch(phone, None, "pose_for_drop", '{"feature": "bottom_top"}')
    e2 = LlmToolDispatcher.Dispatch(phone, None, "pose_for_drop", '{"feature": "diagonal"}')
    e3 = LlmToolDispatcher.Dispatch(phone, None, "add_impactor",
        '{"type": "pen", "target_mm": [0,0,0], "pen_tip_r_mm": 4, "pen_shank_dia_mm": 6}')
    H["T6"] = ('cancels out' in e1 and 'unknown feature token' in e2
               and 'must be <' in e3
               and all('"success": false' in e for e in (e1, e2, e3)))
    _mk("T6 errors -> %s | %s | %s | %s" % (H["T6"], e1[:80], e2[:80], e3[:80]))

try:
    WriteBlock.ExecuteTask("g24", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name + ": " + e.Message)
    emit("WB THREW: %s: %s" % (e.GetType().Name, e.Message))

KEYS = ["T1", "T2", "T3", "T4", "T5", "T6"]
for k in KEYS:
    emit("%s %s" % (k, H.get(k)))
allp = all(bool(H.get(k)) for k in KEYS)
emit("G24_PASS ALL=%s (%d/6)" % (allp, sum(1 for k in KEYS if H.get(k))))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
File.WriteAllText(DONE, "done\n", UTF8Encoding(False))
