# encoding: utf-8
# Import the hardened ODB++ fixture and SAVE the result as a native .scdocx so the
# user can open it in SpaceClaim - proof the import yields real MCAD solids.
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import LlmToolDispatcher

MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\odb_demo_mark.txt"
DONE = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\odb_demo_done.txt"
FIX3 = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\odb_fixture_hard"
OUTDOC = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\odb_demo.scdocx"

def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass

def _do():
    _mk("do-start")
    doc = Document.Create()
    env = LlmToolDispatcher.Dispatch(None, None, "import_odbpp",
        '{"path": "%s", "board_thickness_mm": 1.2}' % FIX3.replace("\\", "\\\\"))
    _mk("import env: " + env[:220])
    part = Window.ActiveWindow.Document.MainPart
    names = [b.Name for b in part.Bodies]
    solids = 0
    for b in part.Bodies:
        if b.Shape.IsClosed and b.Shape.Volume > 0: solids += 1
    _mk("bodies=%d closed_solids=%d" % (len(names), solids))
    _mk("names: " + ", ".join(names))
    try:
        Window.ActiveWindow.Document.SaveAs(OUTDOC)
        _mk("SAVED " + OUTDOC)
    except System.Exception as e:
        _mk("SAVE FAILED: " + e.Message)

try:
    WriteBlock.ExecuteTask("odb_demo", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name + ": " + e.Message)
File.WriteAllText(DONE, "done\n", UTF8Encoding(False))
