# encoding: utf-8
# Final: confirm OdbImportDialog constructs with the expected control set, logging
# only ASCII-safe facts (the dialog's Unicode Text tripped the test logger's UTF-8
# writer earlier - a probe artifact, not a dialog defect).
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
clr.AddReference("System.Windows.Forms")
import System
from System.IO import File
from System.Text import UTF8Encoding
from System.Windows.Forms import NumericUpDown, ComboBox, CheckBox, Button
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task, Command

MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\odb_ui_mark.txt"
DONE = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\odb_ui_done.txt"

def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass

_mk("start")
odb = Command.GetCommand("MXDigitalTwinModeller.ImportOdb")
_mk("cmd registered=%s" % (odb is not None))
WriteBlock.ExecuteTask("mkdoc", Task(lambda: Document.Create()))
part = Window.ActiveWindow.Document.MainPart
from SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs import OdbImportDialog
try:
    d = OdbImportDialog(part)
    nud = len([c for c in d.Controls if isinstance(c, NumericUpDown)])
    combo = len([c for c in d.Controls if isinstance(c, ComboBox)])
    chk = len([c for c in d.Controls if isinstance(c, CheckBox)])
    btn = len([c for c in d.Controls if isinstance(c, Button)])
    title_len = d.Text.Length   # .NET String.Length - never round-trips through str()
    _mk("Odb ctor OK: controls=%d numerics=%d combos=%d checks=%d buttons=%d titlelen=%d" % (
        d.Controls.Count, nud, combo, chk, btn, title_len))
    d.Dispose()
except System.Exception as e:
    _mk("Odb FAILED %s: %s" % (e.GetType().Name, e.Message))
File.WriteAllText(DONE, "done\n", UTF8Encoding(False))
_mk("end")
