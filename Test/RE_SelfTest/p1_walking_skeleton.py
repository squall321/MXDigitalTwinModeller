# encoding: utf-8
# P1 walking skeleton (FROM_SCRATCH_ROADMAP.md): drive GenerationService directly with a
# PhoneParameters, verify the generated solid by kernel-truth volume, and confirm 3 back-to-
# back regenerates with different (L,W,T) each produce a fresh oracle-passing solid.
# This also settles the mm-vs-meter question: GenerationService passes TRUE mm to the mod
# tools (no arr()/1000), so a PASS here proves the production unit convention is correct.
import clr, math
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.Geometry import Matrix
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer import PhoneParameters
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Generation import GenerationService

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\p1_skeleton_result.txt"
log = []
def emit(s): log.append(str(s))

def make_params(L, W, T):
    p = PhoneParameters()
    p.LengthMm = L; p.WidthMm = W; p.ThicknessMm = T
    p.Pocket.WidthMm = L - 16.0; p.Pocket.LengthMm = W - 12.0; p.Pocket.DepthMm = 1.0
    # camera plateau centre
    cam = PhoneParameters.CameraIsland(); cam.XMm = 0.0; cam.YMm = 0.0; cam.DiameterMm = 12.0; cam.HeightMm = 1.5
    p.Camera = cam
    # 4 corner holes OUTSIDE the pocket footprint (pocket ~ X[-(L-16)/2,..], Y[-(W-12)/2,..])
    hx = L/2.0 - 4.0; hy = W/2.0 - 3.0
    for (sx, sy) in [(-1,-1),(1,-1),(-1,1),(1,1)]:
        h = PhoneParameters.HoleSpec(); h.XMm = sx*hx; h.YMm = sy*hy; h.DiameterMm = 4.0; h.Through = True
        p.Holes.Add(h)
    return p

def gen_once(L, W, T, tag):
    res_holder = {"r": None}
    def _do():
        if Window.ActiveWindow is None: Document.Create()
        else: Document.Create()
        part = Window.ActiveWindow.Document.MainPart
        p = make_params(L, W, T)
        gs = GenerationService()
        res_holder["r"] = gs.Generate(part, p, None)
    WriteBlock.ExecuteTask("P1 generate " + tag, Task(_do))
    r = res_holder["r"]
    # analytic volume
    v_bbox = L*W*T
    v_pocket = (L-16.0)*(W-12.0)*1.0
    v_cam = math.pi*6.0**2*1.5
    v_holes = 4*math.pi*2.0**2*T
    v_analytic = v_bbox - v_pocket + v_cam - v_holes
    vmeas = r.MeasuredVolumeMm3
    dpct = 100.0*(vmeas - v_analytic)/v_analytic if v_analytic else 0.0
    emit("[%s] success=%s vol_meas=%.1f vol_analytic=%.1f delta=%.2f%% err=%s" % (
        tag, r.Success, vmeas, v_analytic, dpct, r.Error or "none"))
    for line in r.StageLog: emit("    " + line)
    ok = bool(r.Success) and abs(dpct) < 2.0
    return ok, vmeas

try:
    # 3 regenerates with different envelopes
    o1, v1 = gen_once(146.7, 71.5, 7.4, "A")
    o2, v2 = gen_once(160.0, 75.0, 8.0, "B")
    o3, v3 = gen_once(130.0, 64.0, 6.5, "C")
    # determinism: same params twice -> identical volume
    o4, v4 = gen_once(146.7, 71.5, 7.4, "A2")
    det = (abs(v1 - v4) < 1e-3)
    emit("DETERMINISM A vs A2: v1=%.3f v4=%.3f identical=%s" % (v1, v4, det))
    emit("P1_PASS regenA=%s regenB=%s regenC=%s determinism=%s ALL=%s" % (
        o1, o2, o3, det, o1 and o2 and o3 and det))
except System.Exception as e:
    emit("EXC: %s:%s" % (e.GetType().Name, e.Message))

File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
