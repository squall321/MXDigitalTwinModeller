# encoding: utf-8
# v2 g15 (camera on the BACK): S05 now places the camera on the closed back face (z=0, -Z), so a
# camera bump on a HOLLOW phone (open top) actually ADDS material - the bug g14 surfaced. Verify via
# the real pipeline on a HOLLOW phone (the failing case before).
#   T1  rrect camera on a hollow phone: builds closed solid, S05 "rrect ... back", dV>0 (rounded).
#   T2  cylinder camera on a hollow phone: S05 "cyl ... back", dV>0.
#   T3  the bump is BELOW the back (min Z < 0) - it protrudes on the back, not the top.
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.Geometry import Matrix
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Generation import SpecParser, GenerationService

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g15_back_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g15_mark.txt"
def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass
log = []
def emit(s):
    try: log.append("".join(ch for ch in str(s) if ord(ch) >= 32 or ch == "\t"))
    except Exception: log.append("(unprintable)")

# HOLLOW phone (the case that gave dV=0 with a top camera). Camera on the back now.
SPEC_RRECT = ('{"length_mm":150,"width_mm":72,"thickness_mm":8,"corner_r":3,"min_wall":0.4,'
              '"hollow_wall_mm":0.6,"pocket":false,'
              '"camera":{"x_mm":0,"y_mm":20,"width_mm":20,"length_mm":16,"height_mm":1.5,"corner_r":3}}')
SPEC_CYL = ('{"length_mm":150,"width_mm":72,"thickness_mm":8,"corner_r":3,"min_wall":0.4,'
            '"hollow_wall_mm":0.6,"pocket":false,'
            '"camera":{"x_mm":0,"y_mm":20,"diameter_mm":14,"height_mm":1.5}}')

def stage_dV(stages, target):
    prev = None
    for s in stages or []:
        try: v = float(s.split("vol=")[1].split()[0])
        except Exception: continue
        if s.startswith(target): return (v - prev) if prev is not None else 0.0
        prev = v
    return 0.0

def minZ_mm(body):
    try:
        bb = body.Shape.GetBoundingBox(Matrix.Identity)
        return bb.MinCorner.Z * 1000.0
    except System.Exception: return 0.0

def s05(stages):
    for s in stages or []:
        if s.startswith("S05"): return s
    return "(no S05)"

H = {}
def _do():
    _mk("do-start")
    r1 = SpecParser.Parse(SPEC_RRECT)
    if r1.Success:
        Document.Create()
        part = Window.ActiveWindow.Document.MainPart
        g = GenerationService().Generate(part, r1.Params, None)
        H["r1_gen"] = g.Success; H["r1_vpass"] = g.ValidationPass; H["r1_stages"] = list(g.StageLog)
        H["r1_dV"] = stage_dV(H["r1_stages"], "S05")
        H["r1_minz"] = minZ_mm(g.Body)
        _mk("r1 gen=%s dV=%.2f minz=%.2f" % (g.Success, H["r1_dV"], H["r1_minz"]))
    r2 = SpecParser.Parse(SPEC_CYL)
    if r2.Success:
        Document.Create()
        part2 = Window.ActiveWindow.Document.MainPart
        g2 = GenerationService().Generate(part2, r2.Params, None)
        H["r2_gen"] = g2.Success; H["r2_stages"] = list(g2.StageLog)
        H["r2_dV"] = stage_dV(H["r2_stages"], "S05")
        H["r2_minz"] = minZ_mm(g2.Body)
        _mk("r2 gen=%s dV=%.2f minz=%.2f" % (g2.Success, H["r2_dV"], H["r2_minz"]))
try:
    WriteBlock.ExecuteTask("g15", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name + ": " + e.Message)
    emit("WB THREW: %s: %s" % (e.GetType().Name, e.Message))

emit("T1 rrect(hollow): gen=%s vpass=%s dV=%.2f minZ=%.2f" % (
    H.get("r1_gen"), H.get("r1_vpass"), H.get("r1_dV",0.0), H.get("r1_minz",0.0)))
emit("   " + s05(H.get("r1_stages")))
emit("T2 cyl(hollow):   gen=%s dV=%.2f minZ=%.2f" % (H.get("r2_gen"), H.get("r2_dV",0.0), H.get("r2_minz",0.0)))
emit("   " + s05(H.get("r2_stages")))

r1s = s05(H.get("r1_stages")); r2s = s05(H.get("r2_stages"))
# On a HOLLOW phone the 1.5mm embed passes through the 0.6mm floor, leaving a 0.9mm-tall pedestal
# INSIDE the cavity under the plateau footprint (the P0 lesson: a full 1.5mm embed guarantees a
# robust Boolean; a real camera module occupies interior volume anyway). Expected dV therefore is
# rounded-outer + pedestal: rrect ~468+288=~756 (measured 749.5), cyl ~231+138=~369 (measured 369.5,
# exact pi*r^2*(h+0.9)). Verdict: bump ADDS substantial volume AND protrudes on the BACK (minZ ~ -h).
T1 = (bool(H.get("r1_gen")) and bool(H.get("r1_vpass")) and ("rrect" in r1s) and ("back" in r1s)
      and H.get("r1_dV",0.0) > 400.0 and H.get("r1_dV",0.0) < 800.0 and H.get("r1_minz",0.0) < -1.0)
T2 = (bool(H.get("r2_gen")) and ("cyl" in r2s) and ("back" in r2s)
      and H.get("r2_dV",0.0) > 300.0 and H.get("r2_dV",0.0) < 450.0 and H.get("r2_minz",0.0) < -1.0)
allp = T1 and T2
emit("G15_PASS rrectBackAddsVol=%s cylBackAddsVol=%s ALL=%s" % (T1, T2, allp))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
