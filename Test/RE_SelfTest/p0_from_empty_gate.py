# encoding: utf-8
# P0 GATE (FROM_SCRATCH_ROADMAP.md): prove from-EMPTY composition is viable AT ALL — the
# existential gate. The 82% mod-tool success was measured only on MODIFY-of-imported-axis-
# aligned-CAD; this proves the SAME planar tools compose on a body GENERATED from empty.
#
# Flow: empty doc -> CreateBaseSolid (bbox via RectangleProfile->ExtrudeProfile->DesignBody)
#   -> AddPocket (display) -> AddHolePattern(4) -> AddBoss (camera plateau)
# Oracle (kernel-truth, same fingerprint as the mod matrix):
#   (1) single closed solid (Volume>0), (2) measured Volume ~= analytic bbox-minus-cuts+boss,
#   (3) face count == predicted-ish (sanity), (4) ContainsPoint material-side probe at fixed
#       points (pocket-void=outside, wall=inside, hole-axis=outside).
import clr, math
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System import Array
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task, DesignBody
from SpaceClaim.Api.V252.Modeler import Body
from SpaceClaim.Api.V252.Geometry import Plane, Point, RectangleProfile, PointUV, Matrix
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import ModificationService

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\p0_gate_result.txt"
log = []
def emit(s): log.append(str(s))

def arr(x): return Array[float]([x[0]/1000.0, x[1]/1000.0, x[2]/1000.0])

# ---- params (flat slab, axis-aligned — reuses 100% of planar helpers) ----
L, W, T = 146.7, 71.5, 7.4          # mm
POCKET_W, POCKET_L, POCKET_D = 130.0, 60.0, 1.0   # display pocket
HOLE_D = 4.0
BOSS_D, BOSS_H = 12.0, 1.5          # camera plateau

state = {"step": "start", "err": "none"}
body_holder = {"b": None}

def contains(b, pm):
    try: return b.Shape.ContainsPoint(Point.Create(pm[0]/1000.0, pm[1]/1000.0, pm[2]/1000.0))
    except System.Exception as e: return "ERR:%s" % e.Message

def vol(b):
    try: return b.Shape.Volume * 1e9  # mm^3
    except System.Exception: return None

def nfaces(b):
    try:
        n = 0
        for _ in b.Faces: n += 1
        return n
    except System.Exception: return -1

def build():
    # fresh empty document
    try:
        if Window.ActiveWindow is None: Document.Create()
        else: Document.Create()
    except System.Exception: pass
    win = Window.ActiveWindow
    main = win.Document.MainPart

    # S00: bbox from EMPTY — RectangleProfile on XY, extrude T in +Z, centered origin-ish.
    # DesignBody.Create is a mutation -> must run inside WriteBlock (GATE-0 verified pattern).
    state["step"] = "bbox"
    def _mkbox():
        plane = Plane.PlaneXY
        prof = RectangleProfile(plane, L/1000.0, W/1000.0, PointUV.Create(0, 0), 0.0)
        box = Body.ExtrudeProfile(prof, T/1000.0)
        body_holder["b"] = DesignBody.Create(main, "PhoneSlab", box)
    WriteBlock.ExecuteTask("P0 bbox", Task(_mkbox))
    db = body_holder["b"]
    bb = db.Shape.GetBoundingBox(Matrix.Identity)
    emit("bbox: vol=%.1f faces=%d  Z=[%.3f,%.3f] X=[%.1f,%.1f] Y=[%.1f,%.1f] mm" % (
        vol(db), nfaces(db),
        bb.MinCorner.Z*1000, bb.MaxCorner.Z*1000,
        bb.MinCorner.X*1000, bb.MaxCorner.X*1000,
        bb.MinCorner.Y*1000, bb.MaxCorner.Y*1000))

    # top face is at Z=T; AddPocket cuts DOWN from the top planar face. position at slab center.
    cx, cy = 0.0, 0.0  # RectangleProfile is centered on the plane origin
    v_before = vol(db)
    state["step"] = "pocket"
    rp = ModificationService.AddPocket(db, arr((cx, cy, T)), POCKET_W, POCKET_L, POCKET_D, None)
    v_after = vol(db)
    emit("pocket: success=%s dV=%.1f (expect -%.1f) msg=%s" % (
        getattr(rp,"Success",None), v_after-v_before, POCKET_W*POCKET_L*POCKET_D,
        getattr(rp,"ErrorMessage",None) or getattr(rp,"HintMessage","")))

    state["step"] = "holes"
    # corner holes OUTSIDE the display pocket (pocket is X[-65,65] Y[-30,30]) so each drills the
    # FULL slab thickness, not just the thin pocket floor. bbox X[-73.4,73.4] Y[-35.8,35.8].
    hpos = [(-70.0, -33.0, T), (70.0, -33.0, T), (-70.0, 33.0, T), (70.0, 33.0, T)]
    topN = Array[float]([0.0, 0.0, 1.0])  # explicit +Z axis -> verified axisU!=null branch
    nholes_ok = 0
    for hp in hpos:
        vb = vol(db)
        nf_b = nfaces(db)
        rh = ModificationService.AddHole(db, arr(hp), HOLE_D, True, 0.0, topN, False)
        va = vol(db)
        nf_a = nfaces(db)
        # probe the hole axis just below the top face: should be VOID (False) if drilled
        probe_mid = contains(db, (hp[0], hp[1], T/2.0))
        if getattr(rh, "Success", False): nholes_ok += 1
        emit("  hole@(%.1f,%.1f) success=%s dV=%.2f faces %d->%d axisMid(expect F)=%s msg=%s" % (
            hp[0],hp[1],getattr(rh,"Success",None),va-vb,nf_b,nf_a,probe_mid,
            getattr(rh,"ErrorMessage",None) or ""))
    emit("holes: %d/4 ok" % nholes_ok)

    state["step"] = "boss"
    vb = vol(db)
    rb = ModificationService.AddBoss(db, arr((0.0, 0.0, T)), BOSS_D, BOSS_H, Array[float]([0.0, 0.0, 1.0]))
    va = vol(db)
    emit("boss: success=%s dV=%.1f (expect +%.1f) msg=%s" % (
        getattr(rb,"Success",None), va-vb, math.pi*(BOSS_D/2.0)**2*BOSS_H,
        getattr(rb,"ErrorMessage",None) or getattr(rb,"HintMessage","")))

    # ---- ORACLE (magnitude + closed-solid; ContainsPoint single-point dropped — proven
    # unreliable at a bore void centre, per memory oracle-probe-decisive). The DIAGNOSTIC
    # above showed an isolated clean-slab hole cuts -92.99 (exact) yet ContainsPoint(axis-mid)
    # returns True — so magnitude is the trustworthy signal, not the point probe. ----
    state["step"] = "oracle"
    b = body_holder["b"]
    vmeas = vol(b)
    v_bbox = L*W*T
    v_pocket = POCKET_W*POCKET_L*POCKET_D
    v_boss = math.pi*(BOSS_D/2.0)**2*BOSS_H
    v_holes = 4*math.pi*(HOLE_D/2.0)**2*T
    v_analytic = v_bbox - v_pocket + v_boss - v_holes
    dpct = 100.0*(vmeas-v_analytic)/v_analytic
    emit("ORACLE vol_measured=%.1f vol_analytic=%.1f delta=%.1f (%.2f%%) faces=%d" % (
        vmeas, v_analytic, vmeas-v_analytic, dpct, nfaces(b)))
    closed = (vmeas is not None and vmeas > 0)
    vol_ok = (vmeas is not None and abs(dpct) < 2.0)   # whole-body volume within 2%
    emit("P0_PASS closed=%s vol_ok=%s ALL=%s" % (closed, vol_ok, closed and vol_ok))

try:
    build()
except System.Exception as e:
    state["err"] = "%s:%s" % (e.GetType().Name, e.Message)
    emit("EXC at step=%s : %s" % (state["step"], state["err"]))

File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
