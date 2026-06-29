# encoding: utf-8
# v2 g6 (P4 oriented variants): AddSlitOnFace / AddPocketOnFace / AddBossOnFace on the curved
# Z-stack back, each entering along the LOCAL face normal. Also re-asserts AddHoleOnFace still
# works after the shared-ResolveFaceNormal refactor (regression guard for g4/g5).
# Verify per op: success, correct dV sign (cut<0 / boss>0), and the resolved normal printed.
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

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g6_oriented_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g6_mark.txt"
def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass
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

def crownZ(y):
    return T + bulge*(1.0 - (y/(W/2.0))**2)

H = {}
def _do():
    _mk("do-start")
    Document.Create()
    part = Window.ActiveWindow.Document.MainPart
    r = GenerationService().Generate(part, params(), None)
    b = r.Body
    H["gen"] = r
    _mk("gen-done vol=%.1f" % vol(b))

    def run(name, fn):
        _mk(name + "-start")
        v0 = vol(b)
        try:
            rc = fn()
            v1 = vol(b)
            H[name] = (bool(getattr(rc, "Success", False)),
                       getattr(rc, "HintMessage", None) or getattr(rc, "ErrorMessage", ""),
                       v1 - v0)
            _mk(name + "-done ok=%s dV=%.3f" % (H[name][0], H[name][2]))
        except System.Exception as e:
            H[name] = (False, "%s: %s" % (e.GetType().Name, e.Message), 0.0)
            _mk(name + "-THREW " + e.GetType().Name)

    # 1) AddHoleOnFace - regression: crown lens (n~(0,0,1)), removes material
    run("hole", lambda: ModificationService.AddHoleOnFace(
        b, Array[float]([20.0, 0.0, crownZ(0.0) + 0.05]), 3.0, 3.0))
    # 2) AddSlitOnFace on the crown - long axis toward +X (USB-C-like slot following the back),
    #    orientation seed offset in +X from the slit center.
    run("slit", lambda: ModificationService.AddSlitOnFace(
        b, Array[float]([-30.0, 0.0, crownZ(0.0) + 0.05]), 2.0, 8.0, 2.0,
        Array[float]([0.0, 0.0, crownZ(0.0) + 0.05])))
    # 3) AddPocketOnFace on the crown - recessed window, removes material
    run("pocket", lambda: ModificationService.AddPocketOnFace(
        b, Array[float]([40.0, 0.0, crownZ(0.0) + 0.05]), 6.0, 6.0, 1.5))
    # 4) AddBossOnFace on the crown - raised ring, ADDS material (dV>0)
    run("boss", lambda: ModificationService.AddBossOnFace(
        b, Array[float]([0.0, 20.0, crownZ(20.0) + 0.05]), 5.0, 1.0))
    H["b"] = b
    _mk("do-end")
try:
    WriteBlock.ExecuteTask("g6", Task(_do))
    _mk("writeblock-done")
except System.Exception as e:
    _mk("writeblock-THREW " + e.GetType().Name + ": " + e.Message)
    emit("WRITEBLOCK THREW: %s: %s" % (e.GetType().Name, e.Message))

if "gen" in H:
    r = H["gen"]
    emit("gen: success=%s vol=%.1f stages=%d" % (r.Success, r.MeasuredVolumeMm3, r.StageLog.Count))
else:
    emit("gen: MISSING (Generate did not complete)")

def get(name):
    return H.get(name, (False, "(not run)", 0.0))
for name in ["hole", "slit", "pocket", "boss"]:
    ok, hint, dV = get(name)
    emit("%-7s success=%s dV=%+.3f hint=%s" % (name, ok, dV, hint))

# verdicts: cuts remove (dV<0), boss adds (dV>0); all succeed.
hole_ok   = get("hole")[0]   and get("hole")[2]   < -0.5
slit_ok   = get("slit")[0]   and get("slit")[2]   < -0.2
pocket_ok = get("pocket")[0] and get("pocket")[2] < -0.5
boss_ok   = get("boss")[0]   and get("boss")[2]   > +0.5
allp = hole_ok and slit_ok and pocket_ok and boss_ok
emit("G6_PASS hole=%s slit=%s pocket=%s boss=%s ALL=%s" % (
    hole_ok, slit_ok, pocket_ok, boss_ok, allp))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
