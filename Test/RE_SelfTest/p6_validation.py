# encoding: utf-8
# P6 GATE (FROM_SCRATCH_ROADMAP.md): Tier-2 post-generation validation.
# (1) a valid phone PASSES validation (closed solid, min-wall OK)
# (2) Tier-1 rejects an impossible spec (bump_height >= thickness) BEFORE geometry
# (3) min-wall oracle reports the true wall thickness
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer import PhoneParameters
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Generation import GenerationService

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\p6_validation_result.txt"
log = []
def emit(s): log.append(str(s))

def valid_phone():
    p = PhoneParameters()
    p.LengthMm = 146.7; p.WidthMm = 71.5; p.ThicknessMm = 7.4
    p.HollowWallMm = 0.6; p.MinWallMm = 0.4; p.CornerRadiusMm = 3.0
    p.Pocket.Enabled = False; p.Camera = None; p.Holes.Clear()
    return p

def bad_phone():
    p = valid_phone()
    cam = PhoneParameters.CameraIsland(); cam.HeightMm = 8.0  # > T=7.4 -> Tier-1 reject
    p.Camera = cam
    return p

holder = {}
def _do():
    Document.Create()
    part = Window.ActiveWindow.Document.MainPart
    gs = GenerationService()
    holder["valid"] = gs.Generate(part, valid_phone(), None)
    holder["bad"] = gs.Generate(part, bad_phone(), None)
WriteBlock.ExecuteTask("P6 validate", Task(_do))

rv = holder["valid"]
issues = "; ".join([i for i in rv.ValidationIssues]) if rv.ValidationIssues.Count else "none"
emit("VALID phone: success=%s validationPass=%s minWall=%.3f issues=%s" % (
    rv.Success, rv.ValidationPass, rv.MinWallMm, issues))

rb = holder["bad"]
emit("BAD spec (bump 8.0 > T 7.4): success=%s err=%s" % (rb.Success, rb.Error or "none"))

c1 = bool(rv.Success) and bool(rv.ValidationPass) and abs(rv.MinWallMm - 0.6) < 0.1
c2 = (not rb.Success) and ("invalid spec" in (rb.Error or ""))
emit("P6_PASS validPhonePasses=%s badSpecRejected=%s ALL=%s" % (c1, c2, c1 and c2))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
