# encoding: utf-8
# v2 g13 (stage completion S02/S10/S11/S12): a full spec exercising the newly-wired late stages
# builds a closed solid and each stage shows up in the StageLog. Runs the whole pipeline via
# SpecParser -> Generate (the real path).
#   T1  a rich spec (edge chamfer + antenna slit + pinholes + final fillet, on a curved back)
#       parses, builds a closed solid (validationPass), and StageLog contains S02, S10, S11, S12.
#   T2  an invalid late-stage spec (edge_chamfer too large for T) is REJECTED by Validate.
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Generation import SpecParser, GenerationService

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g13_late_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g13_mark.txt"
def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass
log = []
def emit(s):
    try: log.append("".join(ch for ch in str(s) if ord(ch) >= 32 or ch == "\t"))
    except Exception: log.append("(unprintable)")

# Rich spec: curved back + all four late stages. antenna on flank, one pinhole on curved back +
# one flat pinhole, edge chamfer (top) + final fillet (top).
SPEC = """{
  "length_mm": 150, "width_mm": 72, "thickness_mm": 8, "corner_r": 3, "min_wall": 0.4,
  "hollow_wall_mm": 0.6, "back_bulge_mm": 0.7, "pocket": false,
  "edge_chamfer_mm": 0.4, "edge_chamfer_filter": "top",
  "final_fillet_mm": 0.3, "final_fillet_filter": "top",
  "antenna_slits": [ { "x_mm": -50, "y_mm": -36, "z_mm": 4, "length_mm": 8, "width_mm": 0.3, "depth_mm": 0.6, "on_flank": true } ],
  "pin_holes": [
    { "x_mm": 55, "y_mm": 30, "diameter_mm": 0.8, "through": true },
    { "x_mm": 20, "y_mm": 0, "diameter_mm": 1.0, "through": false, "depth_mm": 1.0, "on_curved_back": true }
  ]
}"""
# invalid: chamfer bigger than half the thickness
BAD = '{"length_mm":150,"width_mm":72,"thickness_mm":8,"min_wall":0.4,"edge_chamfer_mm":9}'

H = {}
def _do():
    _mk("do-start")
    rp = SpecParser.Parse(SPEC)
    H["parse_ok"] = rp.Success
    H["parse_err"] = list(rp.Errors)
    _mk("parsed ok=%s errs=%d" % (rp.Success, rp.Errors.Count))
    if rp.Success:
        Document.Create()
        part = Window.ActiveWindow.Document.MainPart
        _mk("generating...")
        try:
            g = GenerationService().Generate(part, rp.Params, None)
            H["gen_ok"] = g.Success
            H["gen_err"] = getattr(g, "Error", None)
            H["vol"] = g.MeasuredVolumeMm3
            H["vpass"] = g.ValidationPass
            H["stages"] = list(g.StageLog)
            _mk("gen ok=%s vol=%.1f err=%s" % (g.Success, g.MeasuredVolumeMm3, getattr(g, "Error", None)))
            for s in g.StageLog: _mk("  " + s)
        except System.Exception as ge:
            H["gen_ok"] = False
            H["gen_err"] = "%s: %s" % (ge.GetType().Name, ge.Message)
            _mk("gen THREW " + ge.GetType().Name + ": " + ge.Message)
    rb = SpecParser.Parse(BAD)
    H["bad_ok"] = rb.Success
    H["bad_err"] = list(rb.Errors)
    _mk("bad ok=%s" % rb.Success)
try:
    WriteBlock.ExecuteTask("g13", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name + ": " + e.Message)
    emit("WRITEBLOCK THREW: %s: %s" % (e.GetType().Name, e.Message))

emit("parse: success=%s errs=%s" % (H.get("parse_ok"), H.get("parse_err")))
if "gen_ok" in H:
    emit("gen: success=%s vol=%.1f validationPass=%s" % (H["gen_ok"], H.get("vol",0.0), H.get("vpass")))
    for s in H.get("stages", []): emit("   " + s)
emit("bad-spec: success=%s errs=%s" % (H.get("bad_ok"), H.get("bad_err")))

stages_txt = " ".join(H.get("stages", []))
has_all = all(tag in stages_txt for tag in ["S02", "S10", "S11", "S12"])
T1 = bool(H.get("parse_ok")) and bool(H.get("gen_ok")) and bool(H.get("vpass")) and H.get("vol",0.0) > 1000.0 and has_all
T2 = (H.get("bad_ok") is False) and any("chamfer" in e for e in H.get("bad_err", []))
allp = T1 and T2
emit("G13_PASS builtWithLateStages=%s allStagesLogged=%s invalidRejected=%s ALL=%s" % (
    T1, has_all, T2, allp))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
