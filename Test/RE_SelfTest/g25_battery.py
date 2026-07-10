# encoding: utf-8
# g25 (pouch battery): the cell generator must be kernel-true and its swell semantics
# EXACT (dome height solved so added volume == percent x core volume):
#   T1 flat default : 3 bodies; cell V vs analytic (core+terrace+flanges); tabs exact
#   T2 swell 8% both: V_swollen - V_flat == 0.08 * V_core within 1% of the delta
#   T3 height mode, single side: delta matches the closed-form ruled-loft integral
#   T4 folded flange + count=2 stack: same added flange volume but narrower bbox;
#      Cell_2 at thickness+gap pitch; 6 bodies
#   T5 loud errors: tab overreach, percent+height both, count 9, corner_r too big
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
import math
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.Geometry import Matrix
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import LlmToolDispatcher

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g25_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g25_mark.txt"
DONE = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g25_done.txt"

def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass
log = []
def emit(s):
    try: log.append(str(s))
    except Exception: log.append("(unprintable)")

H = {}
L, W, T = 40.0, 30.0, 4.0
R = 0.05 * min(L, W)                    # auto corner r = 1.5
VCORE = (L * W - (4 - math.pi) * R * R) * T
SINK = 0.05

def env_num(env, key):
    import re
    m = re.search('"%s": ([-0-9.eE]+)' % key, env)
    return float(m.group(1)) if m else float("nan")

def body_by_name(part, name):
    for b in part.Bodies:
        if b.Name == name: return b
    return None

def vol(part, name):
    b = body_by_name(part, name)
    return b.Shape.Volume * 1e9 if b is not None else float("nan")

def added_above(wb, hb, k, h):
    """Mirror of the service formula: rect pad drafted by uniform edge inset
    e(s) = E*s/h, E = min(wb,hb)*(1-k)/2; volume above the face (s from SINK to h)."""
    e = min(wb, hb) * (1 - k) / 2
    u0 = SINK / h
    return h * (wb * hb * (1 - u0) - e * (wb + hb) * (1 - u0 ** 2)
                + 4.0 / 3 * e * e * (1 - u0 ** 3))

def make(json_args):
    Document.Create()
    env = LlmToolDispatcher.Dispatch(None, None, "create_pouch_battery", json_args)
    return Window.ActiveWindow.Document.MainPart, env

def _do():
    _mk("do-start")

    # ---- T1: flat default cell -----------------------------------------------
    part, env = make('{"length_mm": 40, "width_mm": 30, "thickness_mm": 4}')
    v_terr = W * 4.0 * (0.4 * T)                 # terrace: 4mm shelf, 40% thickness
    v_flange = 2 * L * 1.5 * 0.15                # flat: 2 sides x L x w x t
    exp_cell = VCORE + v_terr + v_flange
    v1 = vol(part, "Battery_Cell_1")
    vt = vol(part, "Battery_TabPos_1")
    ok = ('"success": true' in env and len(list(part.Bodies)) == 3
          and abs(v1 - exp_cell) < exp_cell * 0.003
          and abs(vt - 4.5 * 0.2 * 5.0) < 0.02
          and abs(env_num(env, "core_v_mm3") - VCORE) < VCORE * 0.001)
    _mk("T1 flat: cell=%.2f(exp %.2f) tab=%.3f(exp 4.5) bodies=%d -> %s" % (
        v1, exp_cell, vt, len(list(part.Bodies)), ok))
    H["T1"] = ok
    v_flat = v1

    # ---- T2: swell 8% both sides — EXACT percent semantics ---------------------
    part, env = make('{"length_mm": 40, "width_mm": 30, "thickness_mm": 4, "swell_percent": 8}')
    v2 = vol(part, "Battery_Cell_1")
    dv_exp = 0.08 * VCORE
    dv = v2 - v_flat
    ok = '"success": true' in env and abs(dv - dv_exp) < dv_exp * 0.01
    _mk("T2 swell8%%: dV=%.2f(exp %.2f) domeH=%.3f -> %s" % (
        dv, dv_exp, env_num(env, "dome_h_mm"), ok))
    if not ok: _mk("T2 env=" + env[:300])
    H["T2"] = ok

    # ---- T3: explicit height, single side --------------------------------------
    part, env = make('{"length_mm": 40, "width_mm": 30, "thickness_mm": 4, '
                     '"swell_height_mm": 0.5, "swell_both_sides": false}')
    exp_add = added_above(L - 2 * R, W - 2 * R, 0.55, 0.5 + SINK)
    v3 = vol(part, "Battery_Cell_1")
    ok = '"success": true' in env and abs((v3 - v_flat) - exp_add) < exp_add * 0.01
    _mk("T3 height: add=%.2f(exp %.2f) -> %s" % (v3 - v_flat, exp_add, ok))
    if not ok: _mk("T3 env=" + env[:300])
    H["T3"] = ok

    # ---- T4: folded flange + count=2 stack --------------------------------------
    part, env = make('{"length_mm": 40, "width_mm": 30, "thickness_mm": 4, '
                     '"flange": "folded", "count": 2, "gap_mm": 1.0}')
    ok = '"success": true' in env and len(list(part.Bodies)) == 6
    if ok:
        v4 = vol(part, "Battery_Cell_1")
        ok = abs(v4 - exp_cell) < exp_cell * 0.003    # same added volume as flat fold
        bb = body_by_name(part, "Battery_Cell_1").Shape.GetBoundingBox(Matrix.Identity)
        wy = (bb.MaxCorner.Y - bb.MinCorner.Y) * 1000
        ok = ok and abs(wy - (W + 2 * 0.15)) < 0.05   # folded: film t protrudes, not width
        bb2 = body_by_name(part, "Battery_Cell_2").Shape.GetBoundingBox(Matrix.Identity)
        z2 = bb2.MinCorner.Z * 1000
        ok = ok and abs(z2 - (T + 1.0)) < 0.01
        _mk("T4 folded+stack: cell=%.2f wy=%.3f(exp %.1f) cell2z=%.3f(exp 5) -> %s" % (
            v4, wy, W + 0.3, z2, ok))
    else:
        _mk("T4 env=" + env[:200])
    H["T4"] = ok

    # ---- T5: loud error paths ----------------------------------------------------
    _, e1 = make('{"length_mm": 40, "width_mm": 30, "thickness_mm": 4, "tab_pitch_mm": 28}')
    _, e2 = make('{"length_mm": 40, "width_mm": 30, "thickness_mm": 4, '
                 '"swell_percent": 5, "swell_height_mm": 0.5}')
    _, e3 = make('{"length_mm": 40, "width_mm": 30, "thickness_mm": 4, "count": 9}')
    _, e4 = make('{"length_mm": 40, "width_mm": 30, "thickness_mm": 4, "corner_r_mm": 20}')
    H["T5"] = ('exceed the cell width' in e1 and 'not both' in e2
               and 'count must be' in e3 and 'corner_r_mm must be' in e4
               and all('"success": false' in e for e in (e1, e2, e3, e4)))
    _mk("T5 errors -> %s | %s | %s | %s | %s" % (H["T5"], e1[:70], e2[:70], e3[:70], e4[:70]))

try:
    WriteBlock.ExecuteTask("g25", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name + ": " + e.Message)
    emit("WB THREW: %s: %s" % (e.GetType().Name, e.Message))

KEYS = ["T1", "T2", "T3", "T4", "T5"]
for k in KEYS:
    emit("%s %s" % (k, H.get(k)))
allp = all(bool(H.get(k)) for k in KEYS)
emit("G25_PASS ALL=%s (%d/5)" % (allp, sum(1 for k in KEYS if H.get(k))))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
File.WriteAllText(DONE, "done\n", UTF8Encoding(False))
