# encoding: utf-8
# v2 g5 (P4 wired): full GenerationService pipeline with LensOnCurvedBack=True and a lens hole
# flagged OnCurvedBack. Verify S06 routes it through AddHoleOnFace (StageLog shows curved=1) and
# the hole removed material from the curved back. A second PLAIN corner hole must stay planar.
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer import PhoneParameters
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Generation import GenerationService

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g5_lens_result.txt"
log = []
def emit(s):
    try: log.append("".join(ch for ch in str(s) if ord(ch) >= 32 or ch == "\t"))
    except Exception: log.append("(unprintable)")

L, W, T, bulge, WALL = 146.7, 71.5, 7.4, 0.6, 0.6

def params():
    p = PhoneParameters()
    p.LengthMm = L; p.WidthMm = W; p.ThicknessMm = T
    p.HollowWallMm = WALL; p.BackBulgeMm = bulge; p.CornerRadiusMm = 3.0
    p.LensOnCurvedBack = True
    p.Pocket.Enabled = False; p.Camera = None
    p.Holes.Clear()
    # lens hole on the curved back (crown, near center) - routed through AddHoleOnFace
    lens = PhoneParameters.HoleSpec()
    lens.XMm = 20.0; lens.YMm = 0.0; lens.DiameterMm = 4.0; lens.Through = False; lens.DepthMm = 1.0
    lens.OnCurvedBack = True
    p.Holes.Add(lens)
    # plain corner hole - stays planar straight-down
    corner = PhoneParameters.HoleSpec()
    corner.XMm = -55.0; corner.YMm = 25.0; corner.DiameterMm = 2.0; corner.Through = True
    corner.OnCurvedBack = False
    p.Holes.Add(corner)
    return p

holder = {}
def _do():
    Document.Create()
    part = Window.ActiveWindow.Document.MainPart
    holder["r"] = GenerationService().Generate(part, params(), None)
WriteBlock.ExecuteTask("g5", Task(_do))

r = holder["r"]
emit("pipeline: success=%s vol=%.1f validationPass=%s minWall=%.3f" % (
    r.Success, r.MeasuredVolumeMm3, r.ValidationPass, r.MinWallMm))
s06 = None
for l in r.StageLog:
    emit("   " + l)
    if l.startswith("S06"): s06 = l
emit("   validationIssues=%s" % ("; ".join([i for i in r.ValidationIssues]) if r.ValidationIssues.Count else "none"))

curved_routed = s06 is not None and "curved=1" in s06
both = s06 is not None and "2/2" in s06
ok = r.Success and curved_routed and both
emit("G5_PASS pipelineOk=%s curvedRouted=%s bothHoles=%s ALL=%s" % (
    r.Success, curved_routed, both, ok))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
