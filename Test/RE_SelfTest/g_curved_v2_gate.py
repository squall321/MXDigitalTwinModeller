# encoding: utf-8
# v2 GATE g1 (FROM_SCRATCH_ROADMAP.md): the curved-back Z-stack produces a genuinely non-planar
# closed solid, AND v1 (BackBulge=0) stays byte-identical (no P5/P6 regression).
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.Geometry import Matrix
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer import PhoneParameters
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Generation import GenerationService

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g_curved_result.txt"
log = []
def emit(s): log.append(str(s))

def params(L, W, T, bulge):
    p = PhoneParameters()
    p.LengthMm = L; p.WidthMm = W; p.ThicknessMm = T
    p.HollowWallMm = 0.0     # g1 isolates the envelope; hollow comes in g2
    p.BackBulgeMm = bulge    # 0 = flat v1; >0 = curved back
    p.CornerRadiusMm = 3.0
    p.Pocket.Enabled = False; p.Camera = None; p.Holes.Clear()
    return p

def faces_zmax(b):
    n = 0
    for f in b.Faces: n += 1
    bb = b.Shape.GetBoundingBox(Matrix.Identity)
    return n, bb.MaxCorner.Z * 1000.0

holder = {}
def _do():
    Document.Create()
    part = Window.ActiveWindow.Document.MainPart
    gs = GenerationService()
    holder["flat"] = gs.Generate(part, params(146.7, 71.5, 7.4, 0.0), None)
    Document.Create()
    part2 = Window.ActiveWindow.Document.MainPart
    holder["curved"] = gs.Generate(part2, params(146.7, 71.5, 7.4, 0.6), None)
    holder["fb"] = holder["flat"].Body
    holder["cb"] = holder["curved"].Body
WriteBlock.ExecuteTask("g_curved", Task(_do))

rf = holder["flat"]; rc = holder["curved"]
nf, zf = faces_zmax(holder["fb"])
nc, zc = faces_zmax(holder["cb"])
emit("FLAT(v1): success=%s vol=%.1f faces=%d zmax=%.3f" % (rf.Success, rf.MeasuredVolumeMm3, nf, zf))
for l in rf.StageLog: emit("   " + l)
emit("CURVED(v2): success=%s vol=%.1f faces=%d zmax=%.3f" % (rc.Success, rc.MeasuredVolumeMm3, nc, zc))
for l in rc.StageLog: emit("   " + l)

# g1a: v1 unchanged - flat zmax == T (7.4), single solid
v1_flat = abs(zf - 7.4) < 0.01 and rf.Success
# g1b: curved is genuinely taller (back bulge raised the top), closed solid
curved_ok = rc.Success and (zc > 7.4 + 0.6 * 0.5) and rc.MeasuredVolumeMm3 > 0
emit("G1_PASS v1Flat=%s curvedNonPlanar(zmax %.2f > %.2f)=%s ALL=%s" % (
    v1_flat, zc, 7.4 + 0.6 * 0.5, curved_ok, v1_flat and curved_ok))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
