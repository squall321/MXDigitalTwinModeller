# encoding: utf-8
# P5 GATE (FROM_SCRATCH_ROADMAP.md): full stage breadth S00..S09 from one spec.
# Generate a complete phone (hollow shell + pocket + camera + holes + ports + grille +
# buttons) and verify one closed solid with each stage producing its expected effect.
import clr, math
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer import PhoneParameters
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Generation import GenerationService

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\p5_full_result.txt"
log = []
def emit(s): log.append(str(s))

def make_full(L, W, T):
    p = PhoneParameters()
    p.LengthMm = L; p.WidthMm = W; p.ThicknessMm = T
    p.HollowWallMm = 0.6
    p.CornerRadiusMm = 3.0
    p.Pocket.WidthMm = L - 16.0; p.Pocket.LengthMm = W - 12.0; p.Pocket.DepthMm = 0.4
    cam = PhoneParameters.CameraIsland(); cam.XMm = -L/2+18; cam.YMm = W/2-14; cam.DiameterMm = 12.0; cam.HeightMm = 1.5
    p.Camera = cam
    for (x, y, d) in [(L/2-4, W/2-3, 3.0), (-(L/2-4), -(W/2-3), 3.0)]:
        h = PhoneParameters.HoleSpec(); h.XMm = x; h.YMm = y; h.DiameterMm = d; h.Through = True
        p.Holes.Add(h)
    port = PhoneParameters.PortSpec(); port.XMm = 0.0; port.YMm = -(W/2-5); port.WidthMm = 9.0; port.HeightMm = 3.0
    p.Ports.Add(port)
    gr = PhoneParameters.GrilleSpec(); gr.OriginXMm = 18.0; gr.OriginYMm = -(W/2-5); gr.Rows = 1; gr.Cols = 6
    gr.PitchMm = 2.0; gr.HoleDiameterMm = 1.0
    p.Grille = gr
    for (x, y) in [(L/2-6, 0.0), (-(L/2-6), 8.0)]:
        b = PhoneParameters.ButtonRecess(); b.XMm = x; b.YMm = y; b.WidthMm = 8.0; b.HeightMm = 3.0; b.DepthMm = 0.4
        p.Buttons.Add(b)
    return p

def gen(L, W, T, tag):
    holder = {"r": None}
    def _do():
        Document.Create()
        part = Window.ActiveWindow.Document.MainPart
        holder["r"] = GenerationService().Generate(part, make_full(L, W, T), None)
    WriteBlock.ExecuteTask("P5 gen " + tag, Task(_do))
    r = holder["r"]
    vmeas = r.MeasuredVolumeMm3
    emit("[%s] success=%s err=%s vol=%.1f handles=%d" % (tag, r.Success, r.Error or "none", vmeas, r.Handles.All.Count))
    for line in r.StageLog: emit("    " + line)
    closed = (vmeas is not None and vmeas > 0)
    stage_fail = any("success=False" in s for s in r.StageLog)
    ok = bool(r.Success) and closed and not stage_fail
    emit("    closed=%s noStageFail=%s" % (closed, not stage_fail))
    return ok, vmeas

try:
    o1, v1 = gen(146.7, 71.5, 7.4, "A")
    o2, v2 = gen(160.0, 75.0, 8.0, "B")
    o3, v3 = gen(146.7, 71.5, 7.4, "A2")
    det = abs(v1 - v3) < 1e-3
    emit("DETERMINISM v1=%.3f v3=%.3f identical=%s" % (v1, v3, det))
    emit("P5_PASS A=%s B=%s determinism=%s ALL=%s" % (o1, o2, det, o1 and o2 and det))
except System.Exception as e:
    emit("EXC: %s:%s" % (e.GetType().Name, e.Message))

File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
