# encoding: utf-8
# v2 g7 (P4 S07 wired): full pipeline with PortsOnFlank=True + a flank USB-C port. Verify S07
# routes it through AddSlitOnFace (StageLog (flank=1)) and the slot entered through the SIDE
# (material removed). Built on the curved Z-stack back so the flank is the curved arc face.
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer import PhoneParameters
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Generation import GenerationService

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g7_flank_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g7_mark.txt"
def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass
log = []
def emit(s):
    try: log.append("".join(ch for ch in str(s) if ord(ch) >= 32 or ch == "\t"))
    except Exception: log.append("(unprintable)")

L, W, T, bulge, WALL = 146.7, 71.5, 7.4, 0.6, 0.6

def params():
    p = PhoneParameters()
    p.LengthMm = L; p.WidthMm = W; p.ThicknessMm = T
    p.HollowWallMm = WALL; p.BackBulgeMm = bulge; p.CornerRadiusMm = 3.0
    p.PortsOnFlank = True
    p.Pocket.Enabled = False; p.Camera = None; p.Holes.Clear()
    port = PhoneParameters.PortSpec()
    port.Type = "usbc"; port.OnFace = "flank"
    port.XMm = 0.0; port.YMm = -(W/2.0); port.ZMm = T/2.0
    port.WidthMm = 9.0; port.HeightMm = 3.0
    p.Ports.Add(port)
    return p

H = {}
def _do():
    _mk("do-start")
    Document.Create()
    part = Window.ActiveWindow.Document.MainPart
    H["r"] = GenerationService().Generate(part, params(), None)
    _mk("gen-done")
try:
    WriteBlock.ExecuteTask("g7", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name)
    emit("WRITEBLOCK THREW: %s: %s" % (e.GetType().Name, e.Message))

if "r" in H:
    r = H["r"]
    emit("pipeline: success=%s vol=%.1f validationPass=%s minWall=%.3f" % (
        r.Success, r.MeasuredVolumeMm3, r.ValidationPass, r.MinWallMm))
    s07 = None
    for l in r.StageLog:
        emit("   " + l)
        if l.startswith("S07"): s07 = l
    emit("   validationIssues=%s" % ("; ".join([i for i in r.ValidationIssues]) if r.ValidationIssues.Count else "none"))
    flank_routed = s07 is not None and "flank=1" in s07
    both = s07 is not None and "1/1" in s07
    ok = r.Success and flank_routed and both
    emit("G7_PASS pipelineOk=%s flankRouted=%s portCut=%s ALL=%s" % (
        r.Success, flank_routed, both, ok))
else:
    emit("gen: MISSING")
    emit("G7_PASS pipelineOk=False flankRouted=False portCut=False ALL=False")
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
