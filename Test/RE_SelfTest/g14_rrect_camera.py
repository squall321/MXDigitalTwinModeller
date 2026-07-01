# encoding: utf-8
# v2 g14 (camera plateau upgrade): S05 builds a ROUNDED-RECT camera bump (real phone shape) when
# width/length are set, and the legacy CYLINDER when they aren't. Verify via the real pipeline.
#   T1  rounded-rect camera: spec parses, builds a closed solid, S05 log says "rrect", dV>0 (bump
#       adds material), and the corners are actually ROUNDED (cylindrical corner faces present).
#   T2  cylinder camera (legacy): width/length omitted -> S05 log says "cyl", builds, dV>0.
#   T3  invalid rounded-rect (corner_r too big for the rect) is REJECTED by Validate.
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.Geometry import Matrix
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Generation import SpecParser, GenerationService

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g14_rrect_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g14_mark.txt"
def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass
log = []
def emit(s):
    try: log.append("".join(ch for ch in str(s) if ord(ch) >= 32 or ch == "\t"))
    except Exception: log.append("(unprintable)")

# rounded-rect camera on a SOLID flat phone (hollow_wall=0 so the top face is closed material for
# the boss to Unite to — isolates the S05 camera shape from the separate open-tray-camera concern).
SPEC_RRECT = ('{"length_mm":150,"width_mm":72,"thickness_mm":8,"corner_r":3,"min_wall":0.4,'
              '"hollow_wall_mm":0,"pocket":false,'
              '"camera":{"x_mm":0,"y_mm":20,"width_mm":20,"length_mm":16,"height_mm":1.5,"corner_r":3}}')
SPEC_CYL = ('{"length_mm":150,"width_mm":72,"thickness_mm":8,"corner_r":3,"min_wall":0.4,'
            '"hollow_wall_mm":0,"pocket":false,'
            '"camera":{"x_mm":0,"y_mm":20,"diameter_mm":14,"height_mm":1.5}}')
BAD = ('{"length_mm":150,"width_mm":72,"thickness_mm":8,"min_wall":0.4,'
       '"camera":{"x_mm":0,"y_mm":20,"width_mm":10,"length_mm":8,"height_mm":1.5,"corner_r":9}}')

def _vol(s):
    try: return float(s.split("vol=")[1].split()[0])
    except Exception: return None

def stage_dV(stages, _a, target):
    # delta at the `target` stage vs the previous stage line that carries a vol=.
    prev = None
    for s in stages or []:
        v = _vol(s)
        if v is None: continue
        if s.startswith(target):
            return (v - prev) if prev is not None else 0.0
        prev = v
    return 0.0

def face_types(body):
    # count faces by geometry type name — corner rounds may be Cylinder OR another surface type.
    counts = {}
    try:
        for f in body.Shape.Faces:
            try:
                t = type(f.Shape.Geometry).__name__
                counts[t] = counts.get(t, 0) + 1
            except System.Exception: pass
    except System.Exception: pass
    return counts

def cyl_face_count(body):
    c = face_types(body)
    return c.get("Cylinder", 0)

def vol_of(body):
    try: return body.Shape.Volume * 1e9
    except System.Exception: return 0.0

H = {}
def _do():
    _mk("do-start")
    # T1 rounded-rect: build full, read StageLog vols to get the S04->S05 delta directly.
    r1 = SpecParser.Parse(SPEC_RRECT)
    H["r1_ok"] = r1.Success; H["r1_err"] = list(r1.Errors)
    if r1.Success:
        Document.Create()
        part = Window.ActiveWindow.Document.MainPart
        g = GenerationService().Generate(part, r1.Params, None)
        H["r1_gen"] = g.Success; H["r1_vol"] = g.MeasuredVolumeMm3; H["r1_vpass"] = g.ValidationPass
        H["r1_stages"] = list(g.StageLog)
        H["r1_faces"] = face_types(g.Body)
        H["r1_cyl_faces"] = cyl_face_count(g.Body)
        # dV at S05 vs the previous stage (StageLog "vol=" markers)
        H["r1_dV"] = stage_dV(H["r1_stages"], "S04", "S05")
        _mk("r1 gen=%s vol=%.1f dV=%.2f faces=%s" % (g.Success, g.MeasuredVolumeMm3, H["r1_dV"], H["r1_faces"]))
    # T2 cylinder legacy (fresh doc)
    r2 = SpecParser.Parse(SPEC_CYL)
    if r2.Success:
        Document.Create()
        part2 = Window.ActiveWindow.Document.MainPart
        g2 = GenerationService().Generate(part2, r2.Params, None)
        H["r2_gen"] = g2.Success; H["r2_vol"] = g2.MeasuredVolumeMm3
        H["r2_stages"] = list(g2.StageLog)
        H["r2_dV"] = stage_dV(H["r2_stages"], "S04", "S05")
        _mk("r2 gen=%s dV=%.2f" % (g2.Success, H["r2_dV"]))
    # T3 invalid
    rb = SpecParser.Parse(BAD)
    H["bad_ok"] = rb.Success; H["bad_err"] = list(rb.Errors)
    _mk("bad ok=%s" % rb.Success)
try:
    WriteBlock.ExecuteTask("g14", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name + ": " + e.Message)
    emit("WB THREW: %s: %s" % (e.GetType().Name, e.Message))

def s05line(stages):
    for s in stages or []:
        if s.startswith("S05"): return s
    return "(no S05)"

SHARP = 20.0 * 16.0 * 1.5   # a sharp 20x16x1.5 rect would add exactly this volume
emit("T1 rrect: parse=%s gen=%s vol=%.1f dV=%.2f vpass=%s faces=%s" % (
    H.get("r1_ok"), H.get("r1_gen"), H.get("r1_vol",0.0), H.get("r1_dV",0.0), H.get("r1_vpass"), H.get("r1_faces")))
emit("   " + s05line(H.get("r1_stages")))
emit("   sharp-rect vol=%.1f; rounded dV=%.2f (< sharp means corners were shaved)" % (SHARP, H.get("r1_dV",0.0)))
emit("T2 cyl:   gen=%s dV=%.2f" % (H.get("r2_gen"), H.get("r2_dV",0.0)))
emit("   " + s05line(H.get("r2_stages")))
emit("T3 bad:   parse=%s errs=%s" % (H.get("bad_ok"), H.get("bad_err")))

# verdicts
r1s = s05line(H.get("r1_stages"))
# rrect: built closed solid, S05 says rrect, bump added material, AND the corners are rounded -
# proven by dV strictly LESS than a sharp rect of the same footprint (rounding shaves the corners)
# while still adding most of the block (a failed Unite would give ~0).
dV1 = H.get("r1_dV", 0.0)
rounded = (dV1 < SHARP - 1.0) and (dV1 > SHARP * 0.9)
T1 = (bool(H.get("r1_gen")) and bool(H.get("r1_vpass")) and ("rrect" in r1s) and rounded)
r2s = s05line(H.get("r2_stages"))
T2 = bool(H.get("r2_gen")) and ("cyl" in r2s) and H.get("r2_dV",0.0) > 5.0
T3 = (H.get("bad_ok") is False) and any("corner_r" in e for e in H.get("bad_err", []))
allp = T1 and T2 and T3
emit("G14_PASS rrectBump=%s cylLegacy=%s invalidRejected=%s ALL=%s" % (T1, T2, T3, allp))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
