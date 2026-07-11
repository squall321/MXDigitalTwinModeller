# encoding: utf-8
# REAL ODB++ validation: parse + import two real designs and save native .scdocx:
#   1. designodb_rigidflex (Siemens sample, Mentor Expedition, cellular flip-phone,
#      INCH, 692 CMPs, arcs/RC/CR/SQ package outlines, '&' string tables, BOM records)
#   2. P3_EUR_REV03 (production board, Mentor Board Station, 'U MM' profile record,
#      558 CMPs, INCH eda/components)
# Adaptive: parse first; if total pad instances would exceed the cap, retry the
# import without pads (still proves board + component solids).
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import LlmToolDispatcher

MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\odb_real_mark.txt"
DONE = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\odb_real_done.txt"
SP = r"C:\Users\Sonic\AppData\Local\Temp\claude\d--MXDigitalTwinModeller\1c7b49ff-c49d-4bd8-a1ce-4f2c20933184\scratchpad"
DESIGNS = [
    ("rigidflex", SP + r"\rigidflex\designodb_rigidflex"),
    ("p3eur", SP + r"\p3eur"),
]

def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass

def env_num(env, key):
    import re
    m = re.search('"%s": ([-0-9.eE]+)' % key, env)
    return float(m.group(1)) if m else float("nan")

def _do():
    _mk("do-start")
    for tag, root in DESIGNS:
        rootj = root.replace("\\", "\\\\")
        env = LlmToolDispatcher.Dispatch(None, None, "parse_odbpp", '{"path": "%s"}' % rootj)
        _mk("[%s] parse: %s" % (tag, env[:400]))
        if '"success": true' not in env:
            _mk("[%s] PARSE FAILED" % tag)
            continue
        pins = env_num(env, "total_pins")
        pads_json = "true" if pins <= 4500 else "false"
        _mk("[%s] total_pins=%s -> include_pads=%s" % (tag, pins, pads_json))

        Document.Create()
        env = LlmToolDispatcher.Dispatch(None, None, "import_odbpp",
            '{"path": "%s", "board_thickness_mm": 1.0, "max_components": 2000, '
            '"max_total_pads": 8000, "include_pads": %s}' % (rootj, pads_json))
        if '"success": true' not in env:
            _mk("[%s] import attempt1 failed: %s" % (tag, env[:300]))
            Document.Create()
            env = LlmToolDispatcher.Dispatch(None, None, "import_odbpp",
                '{"path": "%s", "board_thickness_mm": 1.0, "max_components": 2000, '
                '"include_pads": false}' % rootj)
        if '"success": true' not in env:
            _mk("[%s] IMPORT FAILED: %s" % (tag, env[:400]))
            continue
        _mk("[%s] import: total_bodies=%.0f built=%.0f skipped=%.0f pads=%.0f boardV=%.2f" % (
            tag, env_num(env, "total_bodies"), env_num(env, "components_built"),
            env_num(env, "components_skipped"), env_num(env, "pads_built"),
            env_num(env, "board_volume_mm3")))
        part = Window.ActiveWindow.Document.MainPart
        solids = 0
        total = 0
        for b in part.Bodies:
            total += 1
            try:
                if b.Shape.IsClosed and b.Shape.Volume > 0: solids += 1
            except Exception: pass
        bb = None
        for b in part.Bodies:
            if b.Name.endswith("_Board"):
                from SpaceClaim.Api.V252.Geometry import Matrix
                box = b.Shape.GetBoundingBox(Matrix.Identity)
                bb = (box.MinCorner.X*1000, box.MinCorner.Y*1000,
                      box.MaxCorner.X*1000, box.MaxCorner.Y*1000)
                break
        _mk("[%s] bodies=%d closed_solids=%d board_bbox_mm=%s" % (tag, total, solids, bb))
        out = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\odb_real_%s.scdocx" % tag
        try:
            Window.ActiveWindow.Document.SaveAs(out)
            _mk("[%s] SAVED %s" % (tag, out))
        except System.Exception as e:
            _mk("[%s] SAVE FAILED: %s" % (tag, e.Message))

try:
    WriteBlock.ExecuteTask("odb_real", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name + ": " + e.Message)
File.WriteAllText(DONE, "done\n", UTF8Encoding(False))
