# encoding: utf-8
# Real-file layer subdivision: import the P3_EUR board with subdivide_layers on and a
# size filter, save the layered stack as .scdocx. Proves the feature scales to a real
# board (only big ICs subdivide -> bounded body count) and stays all-closed-solid.
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import LlmToolDispatcher

MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\odb_layreal_mark.txt"
DONE = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\odb_layreal_done.txt"
SP = r"C:\Users\Sonic\AppData\Local\Temp\claude\d--MXDigitalTwinModeller\1c7b49ff-c49d-4bd8-a1ce-4f2c20933184\scratchpad"
ROOT = SP + r"\p3eur"
OUTDOC = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\odb_p3eur_layered.scdocx"

def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass

def env_num(env, key):
    import re
    m = re.search('"%s": ([-0-9.eE]+)' % key, env)
    return float(m.group(1)) if m else float("nan")

def _do():
    _mk("do-start")
    Document.Create()
    # big ICs only (footprint >= 4mm); pads off to keep body count bounded for the demo
    env = LlmToolDispatcher.Dispatch(None, None, "import_odbpp",
        '{"path": "%s", "board_thickness_mm": 1.0, "include_pads": false, '
        '"max_components": 2000, "subdivide_layers": true, '
        '"min_layer_footprint_mm": 4.0}' % ROOT.replace("\\", "\\\\"))
    ok = '"success": true' in env
    _mk("import ok=%s built=%.0f layered=%.0f layers=%.0f bodies=%.0f" % (
        ok, env_num(env, "components_built"), env_num(env, "components_layered"),
        env_num(env, "layers_created"), env_num(env, "total_bodies")))
    if not ok:
        _mk("FAIL env=" + env[:300]); return
    part = Window.ActiveWindow.Document.MainPart
    total = 0; closed = 0
    for b in part.Bodies:
        total += 1
        try:
            if b.Shape.IsClosed and b.Shape.Volume > 0: closed += 1
        except Exception: pass
    _mk("bodies=%d closed_solids=%d all_closed=%s" % (total, closed, total == closed))
    # sample a few layer body names to show substrate/die/mold made it in
    names = [b.Name for b in part.Bodies if "_die" in b.Name or "_mold" in b.Name
             or "_substrate" in b.Name or "_leadframe" in b.Name]
    _mk("layer body sample: " + ", ".join(names[:8]))
    try:
        Window.ActiveWindow.Document.SaveAs(OUTDOC)
        _mk("SAVED " + OUTDOC)
    except System.Exception as e:
        _mk("SAVE FAILED: " + e.Message)

try:
    WriteBlock.ExecuteTask("odb_layreal", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name + ": " + e.Message)
File.WriteAllText(DONE, "done\n", UTF8Encoding(False))
