# encoding: utf-8
# g19 (preset library): the two reference specs must parse CLEAN (0 errors, 0 warnings),
# generate END-TO-END with every advertised stage firing (per-stage log assertions), pass
# Tier-2 validation, and flow through the real MCP dispatcher path unchanged.
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import LlmToolDispatcher
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Generation import (
    SpecParser, GenerationService)

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g19_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g19_mark.txt"
def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass
log = []
def emit(s):
    try: log.append(str(s))
    except Exception: log.append("(unprintable)")

PRESETS = [
    ("iphone", r"D:\MXDigitalTwinModeller\Examples\presets\iphone-like.json",
     ["S04b punch success=True", "S05L lenses 3/3", "S07 ports 1/1 (flank=1)",
      "S10 antenna 2/2 (flank=2)", "S11 pinholes 2/2", "S12 fillet success=True"]),
    ("galaxy", r"D:\MXDigitalTwinModeller\Examples\presets\galaxy-like.json",
     ["S00a curvedBack success=True", "S02 chamfer success=True", "S04b punch success=True",
      "S05L lenses 3/3", "S07 ports 1/1 (flank=1)", "S08 grille success=True (1x6 back)",
      "S09 buttons 1/1", "S11 pinholes 1/1"]),
]

def jesc(s):
    return s.replace("\\", "\\\\").replace('"', '\\"').replace("\r", "").replace("\n", " ")

H = {}
def _do():
    _mk("do-start")
    for name, path, markers in PRESETS:
        raw = File.ReadAllText(path)
        pr = SpecParser.Parse(raw)
        clean = (pr.Success and pr.Errors.Count == 0 and pr.Warnings.Count == 0)
        _mk("%s parse ok=%s err=%d warn=%d %s" % (
            name, pr.Success, pr.Errors.Count, pr.Warnings.Count,
            "|".join(list(pr.Errors) + list(pr.Warnings))[:200]))
        if not clean:
            H[name] = False
            continue
        Document.Create()
        part = Window.ActiveWindow.Document.MainPart
        g = GenerationService().Generate(part, pr.Params, None)
        stages = list(g.StageLog)
        missing = [m for m in markers if not any(m in s for s in stages)]
        vol = g.Body.Shape.Volume * 1e9 if (g.Success and g.Body is not None) else 0.0
        H[name] = (g.Success and g.ValidationPass and len(missing) == 0 and vol > 0)
        _mk("%s gen=%s vpass=%s vol=%.1f missing=%s" % (
            name, g.Success, g.ValidationPass, vol, ";".join(missing) if missing else "-"))
        for s in stages: _mk("  " + s)

    # MCP dispatcher path: the same file content must flow through generate_phone_from_spec.
    raw2 = File.ReadAllText(PRESETS[0][1])
    env = LlmToolDispatcher.Dispatch(None, None, "generate_phone_from_spec",
        '{"spec_json": "%s"}' % jesc(raw2))
    H["mcp"] = ('"success": true' in env) and ('"generated": true' in env) \
        and ('"warnings": []' in env)
    _mk("mcp " + env[:200])

try:
    WriteBlock.ExecuteTask("g19", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name + ": " + e.Message)
    emit("WB THREW: %s: %s" % (e.GetType().Name, e.Message))

for k in ["iphone", "galaxy", "mcp"]:
    emit("%s %s" % (k.upper(), H.get(k)))
allp = all(bool(H.get(k)) for k in ["iphone", "galaxy", "mcp"])
emit("G19_PASS ALL=%s (%d/3)" % (allp, sum(1 for k in ["iphone", "galaxy", "mcp"] if H.get(k))))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
