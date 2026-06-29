# encoding: utf-8
# v2 g4 (P4): AddHoleOnFace cuts a lens hole into the CURVED back along the local face normal.
# Verify: success, dV<0 (material removed). Built on the Z-stack curved envelope (g1/g2).
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System import Array
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer import PhoneParameters
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Generation import GenerationService
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import ModificationService

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g4_facecut_result.txt"
log = []
def emit(s):
    try: log.append("".join(ch for ch in str(s) if ord(ch) >= 32 or ch == "\t"))
    except Exception: log.append("(unprintable)")

L, W, T, bulge, WALL = 146.7, 71.5, 7.4, 0.6, 0.6

def vol(b):
    try: return b.Shape.Volume*1e9
    except System.Exception: return 0

def params():
    p = PhoneParameters()
    p.LengthMm = L; p.WidthMm = W; p.ThicknessMm = T
    p.HollowWallMm = WALL; p.BackBulgeMm = bulge; p.CornerRadiusMm = 3.0
    p.Pocket.Enabled = False; p.Camera = None; p.Holes.Clear()
    return p

holder = {}
def _do():
    Document.Create()
    part = Window.ActiveWindow.Document.MainPart
    r = GenerationService().Generate(part, params(), None)
    b = r.Body
    holder["gen"] = r
    # CASE A: crown (y=0) - normal should be ~(0,0,1) (flat top slab)
    vb = vol(b)
    seedA = Array[float]([20.0, 0.0, T + bulge + 0.05])
    rcA = ModificationService.AddHoleOnFace(b, seedA, 3.0, 3.0)
    vA = vol(b)
    holder["okA"] = bool(getattr(rcA, "Success", False))
    holder["hintA"] = getattr(rcA, "HintMessage", None) or getattr(rcA, "ErrorMessage", "")
    holder["dVA"] = vA - vb
    # CASE B: the FLANK SIDE wall (a near-vertical face). Seed OUTSIDE the body in +Y at mid
    # height so the nearest face is the vertical flank (normal ~ +Y), NOT a slab tread on top.
    # The hole must enter SIDEWAYS - planar AddHole(FindPlanarFaceAtZ) cannot target this; this
    # is the real proof curved-face targeting works. Body half-width = W/2 = 35.75mm.
    seedB = Array[float]([-20.0, (W/2.0) + 0.05, 3.7])
    vb2 = vol(b)
    rcB = ModificationService.AddHoleOnFace(b, seedB, 2.0, 2.0)
    vB = vol(b)
    holder["okB"] = bool(getattr(rcB, "Success", False))
    holder["hintB"] = getattr(rcB, "HintMessage", None) or getattr(rcB, "ErrorMessage", "")
    holder["dVB"] = vB - vb2
    holder["b"] = b
WriteBlock.ExecuteTask("g4", Task(_do))

def parse_n(hint):
    # hint = "entered along face normal (x,y,z)" -> (x,y,z) floats, or None
    try:
        s = hint[hint.index("(")+1:hint.index(")")]
        return [float(t) for t in s.split(",")]
    except Exception:
        return None

r = holder["gen"]
emit("gen: success=%s vol=%.1f stages=%d" % (r.Success, r.MeasuredVolumeMm3, r.StageLog.Count))
nA = parse_n(holder["hintA"]); nB = parse_n(holder["hintB"])
emit("CASE A crown: success=%s dV=%.3f n=%s hint=%s" % (holder["okA"], holder["dVA"], nA, holder["hintA"]))
emit("CASE B flank: success=%s dV=%.3f n=%s hint=%s" % (holder["okB"], holder["dVB"], nB, holder["hintB"]))

# A: crown -> material removed AND normal ~ vertical (|nz| dominant)
A_ok = holder["okA"] and holder["dVA"] < -0.5 and nA is not None and abs(nA[2]) > 0.9
# B: flank -> material removed AND normal has a real HORIZONTAL component (this is the
# whole point - planar AddHole could not target this face). |nx|+|ny| meaningfully > 0.
B_horiz = (abs(nB[0]) + abs(nB[1])) if nB else 0.0
B_ok = holder["okB"] and holder["dVB"] < -0.3 and B_horiz > 0.2
allp = A_ok and B_ok
emit("G4_PASS crownVertical=%s flankRemoved=%s flankHoriz(%.2f>0.2)=%s ALL=%s" % (
    A_ok, holder["okB"], B_horiz, B_horiz > 0.2, allp))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
