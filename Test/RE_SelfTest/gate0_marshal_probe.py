# encoding: utf-8
# GATE-0 headless probe (MCP_SERVER_PLAN.md): prove the thread-marshalling spine of
# Approach C WITHOUT a ribbon click. Reproduces, in IronPython, the exact pattern the
# future HttpListener callback will use:
#   background thread -> Application.ExecuteOnMainThread(Task) -> WriteBlock.ExecuteTask
# and asserts the WriteBlock body ran on the SC API/main thread (not the bg thread),
# committed a body, and propagated an exception across the boundary.
#
# Run via: SpaceClaim.exe /RunScript=gate0_marshal_probe.py  (a fresh SC; /RunScript runs
# on the API thread, so the thread captured at top-level IS the API thread).
OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\gate0_marshal_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\gate0_mark.txt"

def _mark(s):
    try:
        from System.IO import File as _F
        from System.Text import UTF8Encoding as _U
        _F.AppendAllText(MARK, s + "\n", _U(False))
    except Exception:
        pass

import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
_mark("clr-ok")
import System
from System.Threading import Thread, ManualResetEventSlim, ThreadStart
from System.IO import File
from System.Text import UTF8Encoding
_mark("system-ok")
from SpaceClaim.Api.V252 import Application, WriteBlock, Window, Document
_mark("sc-core-ok")
try:
    from SpaceClaim.Api.V252 import Task
    _mark("Task-from-V252-ok")
except Exception as _e:
    _mark("Task-from-V252-FAIL=%s" % _e)
    Task = None
# Body is in .Modeler (xml:11975 Body.ExtrudeProfile), but DesignBody is in the TOP
# SpaceClaim.Api.V252 namespace (xml:7163) — different namespaces.
from SpaceClaim.Api.V252.Modeler import Body
_mark("body-ok")
from SpaceClaim.Api.V252 import DesignBody
_mark("designbody-ok")
from SpaceClaim.Api.V252.Geometry import Plane, CircleProfile, PointUV
_mark("geometry-ok")
lines = []
def emit(s): lines.append(s)

# Ensure an open document (a fresh SC may have none).
try:
    if Window.ActiveWindow is None:
        Document.Create()
except Exception as e:
    emit("doc-create-exc=%s" % e)

api_tid = Thread.CurrentThread.ManagedThreadId
emit("# GATE-0 headless marshal probe")
emit("apiThreadId=%d" % api_tid)

# DIRECT case: on the /RunScript MAIN thread, call WriteBlock.ExecuteTask with a Task
# delegate (NO ExecuteOnMainThread). This isolates "does the WriteBlock+Task+ExtrudeProfile
# pattern work at all in IronPython" from the marshalling question. The marshalling case
# below necessarily deadlocks under /RunScript (main thread blocks on done.Wait, so the
# message pump can't run the queued Task) — that's a probe artifact, not a marshalling
# failure; the real HttpListener callback runs on a separate thread and won't block the pump.
_d = {"ran_tid": -1, "inside_wb": False, "body": False, "err": "none"}
try:
    def _direct():
        _d["ran_tid"] = Thread.CurrentThread.ManagedThreadId
        _d["inside_wb"] = WriteBlock.IsActive
        win = Window.ActiveWindow
        if win is not None and win.Document is not None:
            main = win.Document.MainPart
            prof = CircleProfile(Plane.PlaneXY, 0.005, PointUV.Create(0, 0), 0.0)
            b = Body.ExtrudeProfile(prof, 0.005)
            DesignBody.Create(main, "GateBoxDirect", b)
            _d["body"] = True
    WriteBlock.ExecuteTask("GATE direct", Task(_direct))
except System.Exception as e:
    _d["err"] = "%s:%s" % (e.GetType().Name, e.Message)
emit("[direct] ranOnThreadId=%d onApiThread=%s insideWriteBlock=%s bodyCreated=%s err=%s" % (
    _d["ran_tid"], (_d["ran_tid"] == api_tid), _d["inside_wb"], _d["body"], _d["err"]))

def run_one(label, throw_inside):
    state = {"ran_tid": -1, "inside_wb": False, "body": False, "err": "none"}
    done = ManualResetEventSlim(False)

    def on_main():
        try:
            def wb_body():
                state["ran_tid"] = Thread.CurrentThread.ManagedThreadId
                state["inside_wb"] = WriteBlock.IsActive
                if throw_inside:
                    raise System.InvalidOperationException("GATE intentional throw")
                win = Window.ActiveWindow
                if win is not None and win.Document is not None:
                    main = win.Document.MainPart
                    prof = CircleProfile(Plane.PlaneXY, 0.005, PointUV.Create(0, 0), 0.0)
                    b = Body.ExtrudeProfile(prof, 0.005)
                    DesignBody.Create(main, "GateBox_%s" % label, b)
                    state["body"] = True
            WriteBlock.ExecuteTask("GATE marshal %s" % label, Task(wb_body))
        except System.Exception as e:
            state["err"] = "%s:%s" % (e.GetType().Name, e.Message)
        finally:
            done.Set()

    # Background thread mimics the future HttpListener callback thread.
    def bg():
        Application.ExecuteOnMainThread(Task(on_main))
    t = Thread(ThreadStart(bg))
    t.IsBackground = True
    t.Start()
    completed = done.Wait(30000)  # 30s bounded wait
    emit("[%s] completed=%s ranOnThreadId=%d onApiThread=%s insideWriteBlock=%s bodyCreated=%s err=%s" % (
        label, completed, state["ran_tid"], (state["ran_tid"] == api_tid),
        state["inside_wb"], state["body"], state["err"]))

try:
    run_one("create", False)
    run_one("throw", True)
except System.Exception as e:
    emit("PROBE-EXC=%s:%s" % (e.GetType().Name, e.Message))

# verdict
def parse(line, key):
    for tok in line.split():
        if tok.startswith(key + "="): return tok.split("=", 1)[1]
    return None
create_line = next((l for l in lines if l.startswith("[create]")), "")
throw_line = next((l for l in lines if l.startswith("[throw]")), "")
ok_a = parse(create_line, "completed") == "True"
ok_b = parse(create_line, "onApiThread") == "True"
ok_c = parse(create_line, "insideWriteBlock") == "True"
ok_d = parse(create_line, "bodyCreated") == "True"
ok_e = (parse(throw_line, "err") not in (None, "none")) and (parse(throw_line, "bodyCreated") == "False")
emit("VERDICT a(completed)=%s b(apiThread)=%s c(writeBlock)=%s d(body)=%s e(throwCaught)=%s" % (
    ok_a, ok_b, ok_c, ok_d, ok_e))
emit("GATE0_PASS=%s" % (ok_a and ok_b and ok_c and ok_d and ok_e))

File.WriteAllText(OUT, "\n".join(lines) + "\n", UTF8Encoding(False))
