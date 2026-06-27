# encoding: utf-8
# P2 GATE (FROM_SCRATCH_ROADMAP.md): the from-scratch slab is now HOLLOWED into a uniform-wall
# tray (CurvedShellBuilder.HollowToTray via GenerationService S00b). Verify:
#   (a) single closed solid, (b) volume == analytic (outer - cavity) within 2%,
#   (c) UNIFORM WALL: ray-MARCH thickness at flanks + floor == wall +/- 0.05mm (the true
#       shell-not-slab proof; march, not single ContainsPoint, since the latter is flaky),
#   (d) determinism across regenerates.
import clr, math
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.Geometry import Point
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer import PhoneParameters
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Generation import GenerationService

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\p2_shell_result.txt"
log = []
def emit(s): log.append(str(s))

WALL = 0.6

def contains(b, xmm, ymm, zmm):
    try: return b.Shape.ContainsPoint(Point.Create(xmm/1000.0, ymm/1000.0, zmm/1000.0))
    except System.Exception: return None

def march_thickness(b, x0, y0, z0, dx, dy, dz, span_mm, step=0.02):
    """March from (x0,y0,z0) mm along (dx,dy,dz) unit dir; return length of the FIRST
    contiguous solid run (mm). A transition between consecutive opposite ContainsPoint
    results is reliable even though a single isolated probe is not."""
    n = int(span_mm/step)
    inside = False; run0 = None
    for i in range(n+1):
        t = i*step
        c = contains(b, x0+dx*t, y0+dy*t, z0+dz*t)
        if c is True and not inside:
            inside = True; run0 = t
        elif c is not True and inside:
            return t - run0   # solid run length
    return (n*step - run0) if inside and run0 is not None else 0.0

def make_params(L, W, T):
    p = PhoneParameters()
    p.LengthMm = L; p.WidthMm = W; p.ThicknessMm = T
    p.HollowWallMm = WALL
    p.CornerRadiusMm = 3.0
    p.Pocket.Enabled = False   # P2 gate isolates the shell; features come in S04+ later
    p.Camera = None
    p.Holes.Clear()
    return p

def gen(L, W, T, tag):
    holder = {"r": None}
    def _do():
        Document.Create()
        part = Window.ActiveWindow.Document.MainPart
        holder["r"] = GenerationService().Generate(part, make_params(L, W, T), None)
    WriteBlock.ExecuteTask("P2 gen " + tag, Task(_do))
    r = holder["r"]
    b = r.Body
    # analytic hollow tray volume: outer slab minus the cavity (inner footprint x (T-wall)).
    v_outer = L*W*T
    v_cavity = (L-2*WALL)*(W-2*WALL)*(T-WALL)
    v_analytic = v_outer - v_cavity
    vmeas = r.MeasuredVolumeMm3
    dpct = 100.0*(vmeas-v_analytic)/v_analytic if v_analytic else 0.0
    emit("[%s] success=%s vol_meas=%.1f vol_analytic=%.1f delta=%.2f%% err=%s" % (
        tag, r.Success, vmeas, v_analytic, dpct, r.Error or "none"))
    for line in r.StageLog: emit("    " + line)
    # --- WALL ORACLE (march), away from corners ---
    # floor: march +Z from below the part at (L/4, 0): solid run should = wall
    tf = march_thickness(b, L/4.0, 0.0, -0.2, 0,0,1, T+0.5)
    # +X flank: march +X from inside the cavity at mid-height z=(wall+T)/2 toward +X wall
    zmid = (WALL + T)/2.0
    txf = march_thickness(b, L/2.0 - WALL - 2.0, 0.0, zmid, 1,0,0, WALL+3.0)
    # +Y flank
    tyf = march_thickness(b, 0.0, W/2.0 - WALL - 2.0, zmid, 0,1,0, WALL+3.0)
    emit("    WALL floor=%.3f +Xflank=%.3f +Yflank=%.3f (target=%.2f)" % (tf, txf, tyf, WALL))
    walls_ok = all(abs(x - WALL) < 0.05 for x in (tf, txf, tyf))
    vol_ok = (r.Success and abs(dpct) < 2.0)
    return (vol_ok and walls_ok), vmeas, (tf, txf, tyf)

try:
    oA, vA, wA = gen(146.7, 71.5, 7.4, "A")
    oB, vB, wB = gen(160.0, 75.0, 8.0, "B")
    oA2, vA2, _ = gen(146.7, 71.5, 7.4, "A2")
    det = abs(vA - vA2) < 1e-3
    emit("DETERMINISM vA=%.3f vA2=%.3f identical=%s" % (vA, vA2, det))
    emit("P2_PASS A=%s B=%s determinism=%s ALL=%s" % (oA, oB, det, oA and oB and det))
except System.Exception as e:
    emit("EXC: %s:%s" % (e.GetType().Name, e.Message))

File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
