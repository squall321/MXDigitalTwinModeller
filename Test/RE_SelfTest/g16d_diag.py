# encoding: utf-8
# g16d: diagnose T11 — dump the FULL cut_void envelope (service Log carries the [SKIP] exception).
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import WriteBlock, Task
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import LlmToolDispatcher
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Mcp import SessionContext

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g16d_result.txt"
log = []
def _do():
    sc = SessionContext.Instance
    e0 = LlmToolDispatcher.Dispatch(None, None, "generate_phone", '{"stop_at_stage": "S00"}')
    log.append("GEN: " + e0)
    body = sc.Body; graph = sc.Graph
    e1 = LlmToolDispatcher.Dispatch(body, graph, "cut_void",
        '{"shape": "Cuboid", "dim1_mm": 10, "dim2_mm": 10, "dim3_mm": 4, '
        '"position_mm": [0, 0, 3.7], "mode": "Subtract"}')
    log.append("CUT: " + e1)
try:
    WriteBlock.ExecuteTask("g16d", Task(_do))
except System.Exception as e:
    log.append("WB THREW: %s: %s" % (e.GetType().Name, e.Message))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
