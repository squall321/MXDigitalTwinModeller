# encoding: utf-8
# g22 (generalization sweep): the fastener + package systems must be PARAMETRIC, not
# tuned to the g20/g21 fixtures. Adversarial coverage of everything g21 did NOT touch:
#   A: 90-deg rotated stack (axis = +X)          — Matrix.CreateMapping path off-Z
#   B: 45-deg tilted stack                        — grip must stay EXACT (circle-edge fix)
#   C: hole-size sweep 2.2/4.5/6.6/11.0mm         — auto M2/M4/M6/M10 + socket + simplified
#   D: explicit overrides (nominal/pitch/length)  — pan head + rings honor every override
#   E: TWO sites in one doc                       — seed disambiguation picks the right hole
#   F: sub-standard 1.7mm hole                    — bolt fails LOUDLY, rivet still works
#   G: flat + countersunk rivet heads             — remaining head styles
#   H: package mixed-radius ball map + box-only layer + barrel bulge 1.5 slices 6
# All volumes vs INDEPENDENT python analytics. Small models only.
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
import math
from System.IO import File
from System.Text import UTF8Encoding
from System.Collections.Generic import List
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.Geometry import Matrix, Vector, Point, Direction, Line
from SpaceClaim.Api.V252.Modeler import Body
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry import BodyBuilder
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import LlmToolDispatcher
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Fastener import FastenerGenerationService

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g22_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g22_mark.txt"
DONE = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g22_done.txt"
PKG = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g22_mix_package.txt"

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

def make_plates(holes, ta=3.0, tb=4.0, rot_deg=0.0, rot_axis=None, plate=30.0):
    """Fresh doc, two plates z=[0,ta],[ta,ta+tb] with through-holes at each (x,y,d) in
    `holes`. Optionally rotate BOTH raw bodies about rot_axis (Direction) by rot_deg
    before materializing — same geometry, different world orientation."""
    Document.Create()
    part = Window.ActiveWindow.Document.MainPart
    first = None
    for name, t, z0 in [("PlateA", ta, 0.0), ("PlateB", tb, ta)]:
        blk = BodyBuilder.CreateBlock(plate / 1000.0, plate / 1000.0, t / 1000.0)
        blk.Transform(Matrix.CreateTranslation(Vector.Create(0, 0, z0 / 1000.0)))
        tools = List[Body]()
        for (hx, hy, hd) in holes:
            cut = BodyBuilder.CreateCylinder(hd / 2000.0, (t + 2.0) / 1000.0)
            cut.Transform(Matrix.CreateTranslation(Vector.Create(hx / 1000.0, hy / 1000.0, (z0 - 1.0) / 1000.0)))
            tools.Add(cut)
        blk.Subtract(tools)
        if rot_deg != 0.0:
            rot = Matrix.CreateRotation(Line.Create(Point.Create(0, 0, 0), rot_axis),
                                        rot_deg * math.pi / 180.0)
            blk.Transform(rot)
        db = BodyBuilder.CreateDesignBody(part, name, blk)
        if first is None: first = db
    return part, first

def hexA(w):
    return math.sqrt(3.0) / 2.0 * w * w

def dome_vol(a, h, slices=10):
    R = (a * a + h * h) / (2.0 * h)
    sh = h / slices
    v = 0.0
    for i in range(slices):
        z = (i + 0.5) / slices * h
        v += math.pi * max(R * R - (z - (h - R)) ** 2, 0.0) * sh
    return v

def taper_vol(r0, r1, h, slices=10):
    sh = h / slices
    return sum(math.pi * (r0 + (r1 - r0) * (i + 0.5) / slices) ** 2 * sh for i in range(slices))

def barrel_vol(r0, bulge, t, slices):
    rm = r0 * bulge
    c = (rm * rm - r0 * r0 - t * t / 4.0) / (2.0 * (rm - r0))
    R = rm - c
    sh = t / slices
    v = 0.0
    for i in range(slices):
        zm = (i + 0.5) / slices * t - t / 2.0
        r = max(c + math.sqrt(max(R * R - zm * zm, 0.0)), r0 * 0.2)
        v += math.pi * r * r * sh
    return v

def bolt_vol(d, pitch, grip, headstyle, thread, with_nut, with_washer,
             length=0.0, headDiaR=0.0, headHR=0.0):
    minor = d - 1.2269 * pitch
    wT = 0.15 * d if with_washer else 0.0
    nutH = 0.8 * d if with_nut else 0.0
    L = length if length > 0 else grip + 2 * wT + nutH + 2 * pitch
    ratios = {"hex": (1.5, 0.65), "socket_cap": (1.5, 1.0), "pan": (2.0, 0.4),
              "countersunk": (2.0, 0.5), "dome": (1.8, 0.5), "flat": (2.0, 0.3)}
    dr, hr = ratios[headstyle]
    hd, hh = (headDiaR or dr) * d, (headHR or hr) * d
    if thread == "none":
        shank = math.pi * (d / 2) ** 2 * L
        rings = 0.0
    else:
        tl = min(2.5 * d, L)
        shank = math.pi * (minor / 2) ** 2 * tl + math.pi * (d / 2) ** 2 * (L - tl)
        rings = (int(math.floor(tl / pitch)) * math.pi
                 * ((d / 2) ** 2 - (minor / 2) ** 2) * 0.4 * pitch) if thread == "rings" else 0.0
    if headstyle == "hex":
        head = hexA(hd) * hh
    elif headstyle == "countersunk":
        head = taper_vol(d / 2, hd / 2, hh)
    elif headstyle == "dome":
        head = dome_vol(hd / 2, hh)
    else:
        head = math.pi * (hd / 2) ** 2 * hh
    return shank + rings + head

def _do():
    _mk("do-start")
    svc = FastenerGenerationService()
    D55, GRIP = 5.5, 7.0

    # ---- A: 90-deg rotation about Y -> hole axis = X -------------------------
    part, plate = make_plates([(5.0, 0.0, D55)], rot_deg=90.0, rot_axis=Direction.DirY)
    sites = svc.DetectSites(part)
    okA = sites.Count == 1
    if okA:
        s = sites[0]
        okA = (abs(s.HoleDiaMm - D55) < 0.05 and abs(s.GripMm - GRIP) < 0.05
               and abs(abs(s.AxisDir[0]) - 1.0) < 1e-3 and s.AxisDir[0] > 0)
        env = LlmToolDispatcher.Dispatch(plate, None, "add_fastener",
            '{"seed_mm": [3.5, 0, -5], "type": "bolt", "head": "hex", "thread": "rings", '
            '"with_nut": true, "with_washer": true}')
        vb = vol_mm3(body_by_name(part, "Fastener_Bolt")) if '"success": true' in env else -1
        exp = bolt_vol(5.0, 0.8, GRIP, "hex", "rings", True, True)
        okA = okA and '"success": true' in env and abs(vb - exp) < exp * 0.03
        _mk("A x-axis: d=%.3f grip=%.3f dirX=%.3f bolt=%.1f(exp %.1f) -> %s" % (
            s.HoleDiaMm, s.GripMm, s.AxisDir[0], vb, exp, okA))
    else:
        _mk("A sites=%d" % sites.Count)
    H["A"] = okA

    # ---- B: 45-deg tilt -> grip must stay EXACT (circle-edge span) ----------
    part, plate = make_plates([(5.0, 0.0, D55)], rot_deg=45.0, rot_axis=Direction.DirY)
    sites = svc.DetectSites(part)
    okB = sites.Count == 1
    if okB:
        s = sites[0]
        c45 = math.sqrt(0.5)
        okB = (abs(s.HoleDiaMm - D55) < 0.05 and abs(s.GripMm - GRIP) < 0.02
               and abs(s.AxisDir[0] - c45) < 1e-3 and abs(s.AxisDir[2] - c45) < 1e-3)
        seed = [(5.0 + 3.5) * c45, 0.0, (3.5 - 5.0) * c45]   # rotated hole midpoint
        env = LlmToolDispatcher.Dispatch(plate, None, "add_fastener",
            '{"seed_mm": [%.3f, %.3f, %.3f], "type": "bolt", "head": "hex", '
            '"thread": "rings", "with_nut": true, "with_washer": true}' % tuple(seed))
        vb = vol_mm3(body_by_name(part, "Fastener_Bolt")) if '"success": true' in env else -1
        vn = vol_mm3(body_by_name(part, "Fastener_Nut")) if '"success": true' in env else -1
        expB = bolt_vol(5.0, 0.8, GRIP, "hex", "rings", True, True)
        expN = (hexA(7.5) - math.pi * (5.25 / 2) ** 2) * 4.0
        okB = okB and abs(vb - expB) < expB * 0.03 and abs(vn - expN) < expN * 0.02
        _mk("B 45deg: grip=%.4f(exp 7) dir=(%.3f,0,%.3f) bolt=%.1f(exp %.1f) nut=%.1f(exp %.1f) -> %s" % (
            s.GripMm, s.AxisDir[0], s.AxisDir[2], vb, expB, vn, expN, okB))
    else:
        _mk("B sites=%d" % sites.Count)
    H["B"] = okB

    # ---- C: hole-size sweep, socket_cap + simplified thread ------------------
    sweep = [(2.2, 2.0, 0.4), (4.5, 4.0, 0.7), (6.6, 6.0, 1.0), (11.0, 10.0, 1.5)]
    okC = True
    for (hd, expN_, expP) in sweep:
        part, plate = make_plates([(0.0, 0.0, hd)], ta=1.5, tb=2.0, plate=26.0)
        env = LlmToolDispatcher.Dispatch(plate, None, "add_fastener",
            '{"seed_mm": [0, 0, 1.7], "type": "bolt", "head": "socket_cap", '
            '"thread": "simplified", "with_nut": true, "with_washer": false}')
        ok1 = ('"success": true' in env
               and '"nominal_d": %g' % expN_ in env and '"pitch": %g' % expP in env)
        vb = vol_mm3(body_by_name(part, "Fastener_Bolt")) if ok1 else -1
        exp = bolt_vol(expN_, expP, 3.5, "socket_cap", "simplified", True, False)
        ok1 = ok1 and abs(vb - exp) < exp * 0.03
        okC = okC and ok1
        _mk("C hole=%.1f -> M%g p%g bolt=%.2f(exp %.2f) -> %s" % (hd, expN_, expP, vb, exp, ok1))
    H["C"] = okC

    # ---- D: explicit overrides: nominal 4 / pitch 0.5 / length 20, pan ------
    part, plate = make_plates([(0.0, 0.0, 6.6)], ta=1.5, tb=2.0, plate=26.0)
    env = LlmToolDispatcher.Dispatch(plate, None, "add_fastener",
        '{"seed_mm": [0, 0, 1.7], "type": "bolt", "head": "pan", "thread": "rings", '
        '"nominal_d_mm": 4, "pitch_mm": 0.5, "length_mm": 20, '
        '"with_nut": false, "with_washer": false}')
    okD = ('"success": true' in env and '"nominal_d": 4' in env
           and '"pitch": 0.5' in env and '"length": 20' in env)
    if okD:
        vb = vol_mm3(body_by_name(part, "Fastener_Bolt"))
        exp = bolt_vol(4.0, 0.5, 3.5, "pan", "rings", False, False, length=20.0)
        okD = abs(vb - exp) < exp * 0.03 and '"bodies_created": ["Fastener_Bolt"]' in env
        _mk("D overrides: bolt=%.2f(exp %.2f) -> %s" % (vb, exp, okD))
    else:
        _mk("D env=" + env[:220])
    H["D"] = okD

    # ---- E: two sites, seed picks the right hole -----------------------------
    part, plate = make_plates([(5.0, 0.0, D55), (-8.0, 0.0, 3.4)])
    envS = LlmToolDispatcher.Dispatch(plate, None, "suggest_fastener", '{}')
    two = '"site_count": 2' in envS
    env = LlmToolDispatcher.Dispatch(plate, None, "add_fastener",
        '{"seed_mm": [-8, 0, 3.5], "type": "bolt", "head": "hex", "thread": "none", '
        '"with_nut": false, "name_prefix": "S2"}')
    okE = two and '"success": true' in env and '"nominal_d": 3' in env
    if okE:
        bb = body_by_name(part, "S2_Bolt").Shape.GetBoundingBox(Matrix.Identity)
        cx = (bb.MinCorner.X + bb.MaxCorner.X) / 2 * 1000
        okE = abs(cx - (-8.0)) < 0.1
        _mk("E two-site: count2=%s M3=%s cx=%.2f -> %s" % (two, True, cx, okE))
    else:
        _mk("E envS=%s env=%s" % (envS[:120], env[:160]))
    H["E"] = okE

    # ---- F: 1.7mm hole: bolt LOUD fail, rivet OK ------------------------------
    part, plate = make_plates([(0.0, 0.0, 1.7)], ta=1.0, tb=1.0, plate=15.0)
    envB = LlmToolDispatcher.Dispatch(plate, None, "add_fastener",
        '{"seed_mm": [0, 0, 1], "type": "bolt"}')
    envR = LlmToolDispatcher.Dispatch(plate, None, "add_fastener",
        '{"seed_mm": [0, 0, 1], "type": "rivet", "name_prefix": "F1"}')
    okF = ('"success": false' in envB and 'no standard nominal' in envB
           and '"success": true' in envR)
    if okF:
        ds = 1.7 * 0.98
        exp = (math.pi * (ds / 2) ** 2 * 2.0
               + dome_vol(1.8 * ds / 2, 0.5 * ds) + dome_vol(1.6 * ds / 2, 0.6 * ds))
        vr = vol_mm3(body_by_name(part, "F1_Rivet"))
        okF = abs(vr - exp) < exp * 0.05
        _mk("F tiny hole: boltFail=True rivet=%.3f(exp %.3f) -> %s" % (vr, exp, okF))
    else:
        _mk("F envB=%s envR=%s" % (envB[:140], envR[:140]))
    H["F"] = okF

    # ---- G: flat + countersunk RIVET heads ------------------------------------
    okG = True
    for style, prefix in [("flat", "GF"), ("countersunk", "GC")]:
        part, plate = make_plates([(5.0, 0.0, D55)])
        env = LlmToolDispatcher.Dispatch(plate, None, "add_fastener",
            '{"seed_mm": [5, 0, 3.5], "type": "rivet", "head": "%s", "name_prefix": "%s"}'
            % (style, prefix))
        ok1 = '"success": true' in env
        if ok1:
            ds = D55 * 0.98
            if style == "flat":
                head = math.pi * (2.0 * ds / 2) ** 2 * (0.3 * ds)
            else:
                head = taper_vol(ds / 2, 2.0 * ds / 2, 0.5 * ds)
            exp = (math.pi * (ds / 2) ** 2 * GRIP + head + dome_vol(1.6 * ds / 2, 0.6 * ds))
            vr = vol_mm3(body_by_name(part, prefix + "_Rivet"))
            ok1 = abs(vr - exp) < exp * 0.04
            _mk("G %s rivet=%.1f(exp %.1f) -> %s" % (style, vr, exp, ok1))
        else:
            _mk("G %s env=%s" % (style, env[:160]))
        okG = okG and ok1
    H["G"] = okG

    # ---- H: package mixed radii + box-only layer + barrel 1.5x6 ---------------
    File.WriteAllText(PKG, "\n".join([
        "*Layer,BoxOnly", "Location,0,0", "Length,12,12", "Thickness,0.4",
        "MeshSizeInPlane,0.5", "Box,0,0,4,3",
        "*Layer,MIX", "Location,0,0", "Length,12,12", "Thickness,0.5",
        "Cylinder,-3,0,0.4", "Cylinder,3,0,0.6", "",
    ]), UTF8Encoding(False))
    Document.Create()
    env = LlmToolDispatcher.Dispatch(None, None, "generate_package_from_file",
        '{"path": "%s", "ball_shape": "barrel", "barrel_bulge_ratio": 1.5, "barrel_slices": 6}'
        % PKG.replace("\\", "\\\\"))
    okH = '"success": true' in env and '"total_bodies": 5' in env
    if okH:
        p8 = Window.ActiveWindow.Document.MainPart
        v1 = vol_mm3(body_by_name(p8, "MIX_Ball_0001"))
        v2 = vol_mm3(body_by_name(p8, "MIX_Ball_0002"))
        vd = vol_mm3(body_by_name(p8, "BoxOnly_Die_1"))
        vmB = vol_mm3(body_by_name(p8, "BoxOnly_Matrix"))
        e1 = barrel_vol(0.4, 1.5, 0.5, 6)
        e2 = barrel_vol(0.6, 1.5, 0.5, 6)
        emB = 12 * 12 * 0.4 - 4 * 3 * 0.4
        okH = (abs(v1 - e1) < e1 * 0.05 and abs(v2 - e2) < e2 * 0.05
               and abs(vd - 4 * 3 * 0.4) < 0.03 and abs(vmB - emB) < emB * 0.01)
        _mk("H mix: b1=%.4f(exp %.4f) b2=%.4f(exp %.4f) die=%.3f mB=%.2f(exp %.2f) -> %s" % (
            v1, e1, v2, e2, vd, vmB, emB, okH))
    else:
        _mk("H env=" + env[:240])
    H["H"] = okH

try:
    WriteBlock.ExecuteTask("g22", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name + ": " + e.Message)
    emit("WB THREW: %s: %s" % (e.GetType().Name, e.Message))

KEYS = ["A", "B", "C", "D", "E", "F", "G", "H"]
for k in KEYS:
    emit("%s %s" % (k, H.get(k)))
allp = all(bool(H.get(k)) for k in KEYS)
emit("G22_PASS ALL=%s (%d/8)" % (allp, sum(1 for k in KEYS if H.get(k))))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
File.WriteAllText(DONE, "done\n", UTF8Encoding(False))
