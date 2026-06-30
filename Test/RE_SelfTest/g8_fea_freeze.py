# encoding: utf-8
# v2 g8 (FEA-freeze hop): generate a curved phone, freeze it for FEA, verify the artifact.
# The ONE deliberate hand-off crossing at the FEA boundary.
#  CASE A: default scdocx freeze (license-free, headless-safe) + reopenAndVerify=True
#          -> Document.Load round-trip, kernel-truth fingerprint parity (vol+bbox).
#  CASE B: STEP requested. ANSYS STUDENT license REFUSES part.Export(Step); the service must
#          AUTO-DOWNGRADE to scdocx (DowngradedFromStep=True) rather than emit a corrupt artifact.
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File, Path
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer import PhoneParameters
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Generation import GenerationService, FeaFreezeService

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g8_freeze_result.txt"
BASE_A = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g8_frozen_A"   # scdocx default
BASE_B = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g8_frozen_B"   # step-requested
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g8_mark.txt"
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
    p.Pocket.Enabled = False; p.Camera = None; p.Holes.Clear()
    return p

def fr_dump(tag, fr):
    bbox = list(fr.BboxSizeMm) if fr.BboxSizeMm is not None else None
    emit("%s: success=%s fmt=%s downgraded=%s closed=%s vol=%.1f bbox=%s bytes=%s reopened=%s fpMatch=%s err=%s" % (
        tag, fr.Success, fr.Format, fr.DowngradedFromStep, fr.ClosedSolid, fr.VolumeMm3,
        ("[%.2f,%.2f,%.2f]" % (bbox[0], bbox[1], bbox[2]) if bbox else "None"),
        fr.FileBytes, fr.Reopened, fr.FingerprintMatch, fr.Error))

H = {}
def _do():
    _mk("do-start")
    for ext in [".scdocx", ".stp"]:
        for b in [BASE_A, BASE_B]:
            try:
                if File.Exists(b + ext): File.Delete(b + ext)
            except System.Exception: pass
    Document.Create()
    part = Window.ActiveWindow.Document.MainPart
    r = GenerationService().Generate(part, params(), None)
    H["gen"] = r
    _mk("gen-done vol=%.1f" % r.MeasuredVolumeMm3)
    # CASE A: default scdocx + reopen-verify (headless-safe).
    H["A"] = FeaFreezeService.Freeze(r.Body, BASE_A + ".scdocx", True, "scdocx")
    _mk("A-done ok=%s fmt=%s" % (H["A"].Success, H["A"].Format))
    # CASE B: request STEP -> expect license auto-downgrade to scdocx. No reopen (avoid any
    # Document.Open path under the downgrade; scdocx reopen already covered by A).
    H["B"] = FeaFreezeService.Freeze(r.Body, BASE_B + ".stp", False, "step")
    _mk("B-done ok=%s fmt=%s downgraded=%s" % (H["B"].Success, H["B"].Format, H["B"].DowngradedFromStep))
try:
    WriteBlock.ExecuteTask("g8", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name)
    emit("WRITEBLOCK THREW: %s: %s" % (e.GetType().Name, e.Message))

if "gen" in H and "A" in H and "B" in H:
    r = H["gen"]; A = H["A"]; B = H["B"]
    emit("gen: success=%s vol=%.1f" % (r.Success, r.MeasuredVolumeMm3))
    fr_dump("A_scdocx", A)
    fr_dump("B_step", B)
    aFile = A.StepPath is not None and File.Exists(A.StepPath)
    bFile = B.StepPath is not None and File.Exists(B.StepPath)
    emit("artifacts: A=%s (%s)  B=%s (%s)" % (aFile, A.StepPath, bFile, B.StepPath))

    # verdicts
    # A: scdocx freeze succeeded, fingerprint captured, AND Document.Load reopen matched kernel-truth.
    A_ok = A.Success and A.Format == "scdocx" and A.ClosedSolid and A.Reopened and A.FingerprintMatch and aFile
    # B: STEP requested but Student license -> graceful downgrade to a valid scdocx artifact.
    B_ok = B.Success and B.DowngradedFromStep and B.Format == "scdocx" and bFile
    allp = r.Success and A_ok and B_ok
    emit("G8_PASS scdocxFreeze+reopen=%s stepDowngrade=%s ALL=%s" % (A_ok, B_ok, allp))
else:
    emit("gen/freeze MISSING")
    emit("G8_PASS scdocxFreeze+reopen=False stepDowngrade=False ALL=False")
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
