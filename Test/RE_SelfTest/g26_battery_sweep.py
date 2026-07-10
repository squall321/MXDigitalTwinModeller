# encoding: utf-8
# g26 (battery INTEGRITY + generalization sweep): g25 checked volumes on ONE fixture.
# This gate checks what volume alone cannot prove, across DIVERSE configurations:
#   * SINGLE SOLID: every cell body PieceCount == 1 and IsClosed (a silent multi-piece
#     union can still sum to the right volume - the loft landmine proved it)
#   * CONNECTIVITY: tab bbox min-X coincides with the cell terrace end face (separate
#     bodies must TOUCH for CAE contact); dome top/bottom z at the solved height
#   * PARAMETRIC GENERALITY: 6 configs sweeping size 20..90mm, aspect 5:1, corner r,
#     no-terrace, no-flange, folded flange, tab offset, swell percent/height,
#     single/both sides, count=3 stack - volume vs per-config analytics every time.
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
import math
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.Geometry import Matrix
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import LlmToolDispatcher

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g26_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g26_mark.txt"
DONE = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g26_done.txt"
SINK = 0.05

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

def bb_mm(db):
    bb = db.Shape.GetBoundingBox(Matrix.Identity)
    return (bb.MinCorner.X * 1000, bb.MinCorner.Y * 1000, bb.MinCorner.Z * 1000,
            bb.MaxCorner.X * 1000, bb.MaxCorner.Y * 1000, bb.MaxCorner.Z * 1000)

def core_v(L, W, T, r):
    return (L * W - (4 - math.pi) * r * r) * T

def added_above(wb, hb, k, h):
    e = min(wb, hb) * (1 - k) / 2
    u0 = SINK / h
    return h * (wb * hb * (1 - u0) - e * (wb + hb) * (1 - u0 ** 2)
                + 4.0 / 3 * e * e * (1 - u0 ** 3))

def flat_v(L, W, T, r, terr_l, terr_t, fl_mode, fl_w, fl_t):
    v = core_v(L, W, T, r)
    if terr_l > 0: v += W * terr_l * terr_t
    if fl_mode != "none": v += 2 * L * fl_w * fl_t   # flat and folded add the same volume
    return v

def check_cell(part, prefix, idx, exp_v, tol, terr_end_x, tab_t, tab_l, notes):
    """Integrity checks volume alone cannot prove."""
    cell = body_by_name(part, "%s_Cell_%d" % (prefix, idx))
    if cell is None: return False, "cell missing"
    if cell.Shape.PieceCount != 1: return False, "cell pieces=%d" % cell.Shape.PieceCount
    if not cell.Shape.IsClosed: return False, "cell not closed"
    v = cell.Shape.Volume * 1e9
    if abs(v - exp_v) > exp_v * tol:
        return False, "cell V=%.2f exp %.2f" % (v, exp_v)
    for tab in ("TabPos", "TabNeg"):
        tb = body_by_name(part, "%s_%s_%d" % (prefix, tab, idx))
        if tb is None: return False, tab + " missing"
        if tb.Shape.PieceCount != 1: return False, tab + " pieces"
        x0 = bb_mm(tb)[0]
        if abs(x0 - terr_end_x) > 1e-3:
            return False, "%s minX=%.4f != terrace end %.4f (floating!)" % (tab, x0, terr_end_x)
        tl = bb_mm(tb)[3] - x0
        if abs(tl - tab_l) > 0.01: return False, tab + " length wrong"
    notes.append("V=%.2f" % v)
    return True, "ok"

def make(json_args):
    Document.Create()
    env = LlmToolDispatcher.Dispatch(None, None, "create_pouch_battery", json_args)
    return Window.ActiveWindow.Document.MainPart, env

def _do():
    _mk("do-start")

    # ---- A: tiny thin cell, NO terrace, NO flange (pure core + tabs) ----------
    part, env = make('{"length_mm": 20, "width_mm": 10, "thickness_mm": 2, '
                     '"terrace_length_mm": 0, "flange": "none", "name_prefix": "A"}')
    notes = []
    okA, why = (False, "env fail") if '"success": true' not in env else check_cell(
        part, "A", 1, core_v(20, 10, 2, 0.5), 0.003, 10.0, 0.2, 5.0, notes)
    _mk("A tiny no-terrace/flange: %s %s %s" % (okA, why, notes))
    H["A"] = okA

    # ---- B: large cell, big corner, folded flange, offset tabs ----------------
    part, env = make('{"length_mm": 90, "width_mm": 60, "thickness_mm": 6, '
                     '"corner_r_mm": 5, "flange": "folded", "flange_width_mm": 2.5, '
                     '"flange_thickness_mm": 0.2, "terrace_length_mm": 6, '
                     '"terrace_thickness_mm": 3, "tab_offset_mm": -5, "name_prefix": "B"}')
    notes = []
    expB = flat_v(90, 60, 6, 5, 6, 3, "folded", 2.5, 0.2)
    okB, why = (False, "env fail") if '"success": true' not in env else check_cell(
        part, "B", 1, expB, 0.003, 51.0, 0.2, 5.0, notes)
    if okB:
        x0, y0, z0, x1, y1, z1 = bb_mm(body_by_name(part, "B_Cell_1"))
        okB = abs((y1 - y0) - 60.4) < 0.05    # folded: W + 2 x film t
        why = "bboxY=%.3f" % (y1 - y0)
        # offset tabs stay centered at -5 +/- pitch/2
        ty = bb_mm(body_by_name(part, "B_TabPos_1"))
        tyc = (ty[1] + ty[4]) / 2
        okB = okB and abs(tyc - (-5 + 0.4 * 60 / 2)) < 0.01
    _mk("B large folded offset: %s %s %s" % (okB, why, notes))
    H["B"] = okB

    # ---- C: swell 12% SINGLE side + folded flange -------------------------------
    part, env = make('{"length_mm": 50, "width_mm": 35, "thickness_mm": 5, '
                     '"flange": "folded", "swell_percent": 12, '
                     '"swell_both_sides": false, "name_prefix": "C"}')
    okC = '"success": true' in env
    if okC:
        r = 0.05 * 35
        dv = 0.12 * core_v(50, 35, 5, r)
        expC = flat_v(50, 35, 5, r, 4, 2, "folded", 1.5, 0.15) + dv
        notes = []
        okC, why = check_cell(part, "C", 1, expC, 0.004, 29.0, 0.2, 5.0, notes)
        if okC:
            z1 = bb_mm(body_by_name(part, "C_Cell_1"))[5]
            hd = env_num(env, "dome_h_mm")
            okC = abs(z1 - (5 + hd)) < 0.01 and abs(bb_mm(body_by_name(part, "C_Cell_1"))[2]) < 1e-6
            why = "topZ=%.3f(exp %.3f) botZ=%.4f" % (z1, 5 + hd, bb_mm(body_by_name(part, "C_Cell_1"))[2])
    else:
        why, notes = "env fail", []
    _mk("C swell12 single: %s %s %s" % (okC, why, notes))
    H["C"] = okC

    # ---- D: swell height + count=3 stack (dome tips must not touch) -------------
    part, env = make('{"length_mm": 40, "width_mm": 30, "thickness_mm": 4, '
                     '"swell_height_mm": 0.4, "count": 3, "gap_mm": 1.0, "name_prefix": "D"}')
    okD = '"success": true' in env
    if okD:
        r = 0.05 * 30
        add = added_above(40 - 2 * r, 30 - 2 * r, 0.55, 0.4 + SINK)
        expD = flat_v(40, 30, 4, r, 4, 1.6, "flat", 1.5, 0.15) + 2 * add
        vols, pieces = [], []
        for i in (1, 2, 3):
            c = body_by_name(part, "D_Cell_%d" % i)
            vols.append(c.Shape.Volume * 1e9 if c else -1)
            pieces.append(c.Shape.PieceCount if c else -1)
        okD = (len(list(part.Bodies)) == 9 and pieces == [1, 1, 1]
               and all(abs(v - expD) < expD * 0.004 for v in vols))
        # adjacent domes: cell i top dome tip vs cell i+1 bottom dome tip must not overlap
        gap12 = bb_mm(body_by_name(part, "D_Cell_2"))[2] - bb_mm(body_by_name(part, "D_Cell_1"))[5]
        okD = okD and gap12 > 0.05
        _mk("D stack3 swell: ok=%s vols=%s pieces=%s tipGap=%.3f" % (okD, ["%.1f" % v for v in vols], pieces, gap12))
    else:
        _mk("D env=" + env[:200])
    H["D"] = okD

    # ---- E: extreme aspect 5:1 with swell (inset default = corner r) ------------
    part, env = make('{"length_mm": 100, "width_mm": 20, "thickness_mm": 3, '
                     '"swell_percent": 6, "name_prefix": "E"}')
    okE = '"success": true' in env
    if okE:
        r = 0.05 * 20
        expE = flat_v(100, 20, 3, r, 4, 1.2, "flat", 1.5, 0.15) + 0.06 * core_v(100, 20, 3, r)
        notes = []
        okE, why = check_cell(part, "E", 1, expE, 0.004, 54.0, 0.2, 5.0, notes)
    else:
        why, notes = "env fail", []
    _mk("E aspect5:1 swell: %s %s %s" % (okE, why, notes))
    H["E"] = okE

    # ---- F: dims echo consistency (dome height reported == kernel bbox) ---------
    part, env = make('{"length_mm": 60, "width_mm": 45, "thickness_mm": 5, '
                     '"swell_percent": 10, "swell_top_scale": 0.7, "name_prefix": "F"}')
    okF = '"success": true' in env
    if okF:
        hd = env_num(env, "dome_h_mm")
        z0, z1 = bb_mm(body_by_name(part, "F_Cell_1"))[2], bb_mm(body_by_name(part, "F_Cell_1"))[5]
        okF = abs(z1 - (5 + hd)) < 0.01 and abs(z0 - (-hd)) < 0.01
        # and the solved height must reproduce the requested percent analytically
        r = 0.05 * 45
        add2 = 2 * added_above(60 - 2 * r, 45 - 2 * r, 0.7, hd + SINK)
        okF = okF and abs(add2 - 0.10 * core_v(60, 45, 5, r)) < 0.001 * core_v(60, 45, 5, r)
        _mk("F echo: h=%.4f topZ=%.3f botZ=%.3f addChk=%.2f/%.2f -> %s" % (
            hd, z1, z0, add2, 0.10 * core_v(60, 45, 5, r), okF))
    else:
        _mk("F env=" + env[:200])
    H["F"] = okF

try:
    WriteBlock.ExecuteTask("g26", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name + ": " + e.Message)
    emit("WB THREW: %s: %s" % (e.GetType().Name, e.Message))

KEYS = ["A", "B", "C", "D", "E", "F"]
for k in KEYS:
    emit("%s %s" % (k, H.get(k)))
allp = all(bool(H.get(k)) for k in KEYS)
emit("G26_PASS ALL=%s (%d/6)" % (allp, sum(1 for k in KEYS if H.get(k))))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
File.WriteAllText(DONE, "done\n", UTF8Encoding(False))
