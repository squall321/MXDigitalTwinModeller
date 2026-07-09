# encoding: utf-8
# g21 (fastener design): two stacked plates with a CONCENTRIC through-hole must (T1) be
# detected as ONE fastening site (hole d / grip / 2 bodies), (T2) yield ISO design
# recommendations via the MCP suggest_fastener path (M5 for a 5.5 hole), (T3) grow a full
# hex bolt + cosmetic thread rings + nut + washers at kernel-true volumes, (T4) grow a
# dome-head rivet with bucked tail, (T5) grow a countersunk no-thread bolt (taper stack),
# and (T6) reject a far-away seed LOUDLY. Small models only - memory-safe.
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
import math
from System.IO import File
from System.Text import UTF8Encoding
from System.Collections.Generic import List
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.Geometry import Matrix, Vector
from SpaceClaim.Api.V252.Modeler import Body
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry import BodyBuilder
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import LlmToolDispatcher
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Fastener import FastenerGenerationService

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g21_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g21_mark.txt"
DONE = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g21_done.txt"

def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass
log = []
def emit(s):
    try: log.append(str(s))
    except Exception: log.append("(unprintable)")

H = {}
HOLE_D = 5.5
HOLE_X = 5.0
TA, TB = 3.0, 4.0     # plate thicknesses -> grip 7.0

def vol_mm3(db):
    return db.Shape.Volume * 1e9

def body_by_name(part, name):
    for b in part.Bodies:
        if b.Name == name: return b
    return None

def make_plates():
    """Fresh doc: PlateA 30x30x3 z=[0,3] + PlateB 30x30x4 z=[3,7], coaxial hole
    d=5.5 at (HOLE_X, 0) through both. Returns (part, plateA designbody)."""
    Document.Create()
    part = Window.ActiveWindow.Document.MainPart
    for name, t, z0 in [("PlateA", TA, 0.0), ("PlateB", TB, TA)]:
        blk = BodyBuilder.CreateBlock(0.030, 0.030, t / 1000.0)
        blk.Transform(Matrix.CreateTranslation(Vector.Create(0, 0, z0 / 1000.0)))
        cut = BodyBuilder.CreateCylinder(HOLE_D / 2000.0, (t + 2.0) / 1000.0)
        cut.Transform(Matrix.CreateTranslation(Vector.Create(HOLE_X / 1000.0, 0, (z0 - 1.0) / 1000.0)))
        tools = List[Body]()
        tools.Add(cut)
        blk.Subtract(tools)
        BodyBuilder.CreateDesignBody(part, name, blk)
    return part, body_by_name(part, "PlateA")

def hexA(w):          # regular hexagon area, across-flats w
    return math.sqrt(3.0) / 2.0 * w * w

def dome_vol(a, h, slices=10):
    """Analytic stacked-disc dome volume — SAME slicing rule as the service (mid-radius,
    10 slices) so the kernel result must match within the overlap epsilon."""
    R = (a * a + h * h) / (2.0 * h)
    sh = h / slices
    v = 0.0
    for i in range(slices):
        s = (i + 0.5) / slices
        z = s * h
        r2 = max(R * R - (z - (h - R)) * (z - (h - R)), 0.0)
        v += math.pi * r2 * sh
    return v

def taper_vol(r0, r1, h, slices=10):
    sh = h / slices
    v = 0.0
    for i in range(slices):
        s = (i + 0.5) / slices
        r = r0 + (r1 - r0) * s
        v += math.pi * r * r * sh
    return v

def _do():
    _mk("do-start")

    # ---- T1: site detection ------------------------------------------------
    part, plate = make_plates()
    svc = FastenerGenerationService()
    sites = svc.DetectSites(part)
    ok = (sites.Count == 1)
    if ok:
        s = sites[0]
        ok = (abs(s.HoleDiaMm - HOLE_D) < 0.05 and abs(s.GripMm - (TA + TB)) < 0.05
              and s.BodyNames.Count == 2 and abs(abs(s.AxisDir[2]) - 1.0) < 1e-6
              and abs(s.AxisPointMm[0] - HOLE_X) < 0.05)
        _mk("t1 sites=1 d=%.3f grip=%.3f bodies=%d dirZ=%.3f x=%.3f -> %s" % (
            s.HoleDiaMm, s.GripMm, s.BodyNames.Count, s.AxisDir[2], s.AxisPointMm[0], ok))
    else:
        _mk("t1 sites=%d (expected 1)" % sites.Count)
    H["t1"] = ok

    # ---- T2: suggest_fastener (MCP dispatcher, LLM-guidance data) -----------
    env = LlmToolDispatcher.Dispatch(plate, None, "suggest_fastener",
        '{"seed_mm": [%.1f, 0, 3.5]}' % HOLE_X)
    H["t2"] = ('"success": true' in env and '"site_count": 1' in env
               and '"designation": "M5"' in env and '"pitch_mm": 0.8' in env
               and '"type": "rivet"' in env and 'rationale' in env)
    _mk("t2 %s -> %s" % (env[:260], H["t2"]))

    # ---- T3: hex bolt + rings + nut + washers (kernel-true volumes) ---------
    env3 = LlmToolDispatcher.Dispatch(plate, None, "add_fastener",
        '{"seed_mm": [%.1f, 0, 3.5], "type": "bolt", "head": "hex", '
        '"thread": "rings", "with_nut": true, "with_washer": true}' % HOLE_X)
    ok3 = '"success": true' in env3 and '"generated": true' in env3
    d, pitch = 5.0, 0.8
    minor = d - 1.2269 * pitch
    wT, nutH = 0.15 * d, 0.8 * d
    grip = TA + TB
    length = grip + 2 * wT + nutH + 2 * pitch
    threadLen = min(2.5 * d, length)
    nrings = int(math.floor(threadLen / pitch))
    headW, headH = 1.5 * d, 0.65 * d
    bore = 1.05 * d
    exp_bolt = (math.pi * (minor / 2) ** 2 * threadLen
                + math.pi * (d / 2) ** 2 * (length - threadLen)
                + nrings * math.pi * ((d / 2) ** 2 - (minor / 2) ** 2) * 0.4 * pitch
                + hexA(headW) * headH)
    exp_nut = (hexA(1.5 * d) - math.pi * (bore / 2) ** 2) * nutH
    exp_wash = math.pi * ((2 * d / 2) ** 2 - (bore / 2) ** 2) * wT
    if ok3:
        vb = vol_mm3(body_by_name(part, "Fastener_Bolt"))
        vn = vol_mm3(body_by_name(part, "Fastener_Nut"))
        vw = vol_mm3(body_by_name(part, "Fastener_WasherTop"))
        names4 = all(body_by_name(part, n) is not None for n in
                     ["Fastener_Bolt", "Fastener_Nut", "Fastener_WasherTop", "Fastener_WasherBottom"])
        bb = body_by_name(part, "Fastener_Bolt").Shape.GetBoundingBox(Matrix.Identity)
        zmax = bb.MaxCorner.Z * 1000
        ok3 = (names4 and abs(vb - exp_bolt) < exp_bolt * 0.03
               and abs(vn - exp_nut) < exp_nut * 0.02
               and abs(vw - exp_wash) < exp_wash * 0.02
               and abs(zmax - (grip + wT + headH)) < 0.05
               and '"nominal_d": 5' in env3)
        _mk("t3 bolt=%.1f(exp %.1f) nut=%.1f(exp %.1f) wash=%.2f(exp %.2f) zmax=%.2f(exp %.2f) -> %s" % (
            vb, exp_bolt, vn, exp_nut, vw, exp_wash, zmax, grip + wT + headH, ok3))
    else:
        _mk("t3 env=" + env3[:260])
    H["t3"] = ok3

    # ---- T4: dome rivet (fresh doc) -----------------------------------------
    part4, plate4 = make_plates()
    env4 = LlmToolDispatcher.Dispatch(plate4, None, "add_fastener",
        '{"seed_mm": [%.1f, 0, 3.5], "type": "rivet", "name_prefix": "R1"}' % HOLE_X)
    ok4 = '"success": true' in env4
    if ok4:
        ds = HOLE_D * 0.98
        headDia, headH4 = 1.8 * ds, 0.5 * ds
        tailDia, tailH = 1.6 * ds, 0.6 * ds
        exp_rivet = (math.pi * (ds / 2) ** 2 * grip
                     + dome_vol(headDia / 2, headH4) + dome_vol(tailDia / 2, tailH))
        vr = vol_mm3(body_by_name(part4, "R1_Rivet"))
        only1 = '"bodies_created": ["R1_Rivet"]' in env4
        ok4 = only1 and abs(vr - exp_rivet) < exp_rivet * 0.04
        _mk("t4 rivet=%.1f(exp %.1f) single=%s -> %s" % (vr, exp_rivet, only1, ok4))
    else:
        _mk("t4 env=" + env4[:260])
    H["t4"] = ok4

    # ---- T5: countersunk bolt, no thread, no nut (taper stack) --------------
    part5, plate5 = make_plates()
    env5 = LlmToolDispatcher.Dispatch(plate5, None, "add_fastener",
        '{"seed_mm": [%.1f, 0, 3.5], "type": "bolt", "head": "countersunk", '
        '"thread": "none", "with_nut": false, "name_prefix": "C1"}' % HOLE_X)
    ok5 = '"success": true' in env5
    if ok5:
        len5 = grip + 2 * 0.8            # no washers/nut: grip + 2*pitch
        exp5 = math.pi * (5.0 / 2) ** 2 * len5 + taper_vol(2.5, 5.0, 0.5 * 5.0)
        v5 = vol_mm3(body_by_name(part5, "C1_Bolt"))
        only1 = '"bodies_created": ["C1_Bolt"]' in env5
        ok5 = only1 and abs(v5 - exp5) < exp5 * 0.03
        _mk("t5 cskBolt=%.1f(exp %.1f) single=%s -> %s" % (v5, exp5, only1, ok5))
    else:
        _mk("t5 env=" + env5[:260])
    H["t5"] = ok5

    # ---- T6: far seed must fail LOUDLY --------------------------------------
    env6 = LlmToolDispatcher.Dispatch(plate5, None, "add_fastener",
        '{"seed_mm": [50, 50, 0]}')
    H["t6"] = ('"success": false' in env6 and 'seed is' in env6)
    _mk("t6 %s -> %s" % (env6[:180], H["t6"]))

try:
    WriteBlock.ExecuteTask("g21", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name + ": " + e.Message)
    emit("WB THREW: %s: %s" % (e.GetType().Name, e.Message))

KEYS = ["t1", "t2", "t3", "t4", "t5", "t6"]
for k in KEYS:
    emit("%s %s" % (k.upper(), H.get(k)))
allp = all(bool(H.get(k)) for k in KEYS)
emit("G21_PASS ALL=%s (%d/6)" % (allp, sum(1 for k in KEYS if H.get(k))))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
File.WriteAllText(DONE, "done\n", UTF8Encoding(False))
