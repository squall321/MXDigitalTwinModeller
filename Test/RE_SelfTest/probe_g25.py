# encoding: utf-8
# Minimal repro probe: rounded-rect core + loft dome -> where do the bodies sit, and
# what does Unite actually do with a Fuse-capped loft solid as the tool?
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from System.Collections.Generic import List
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.Geometry import Matrix
from SpaceClaim.Api.V252.Modeler import Body
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry import BodyBuilder
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.CadOps import CadPrimitivesService

MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\probe_g25_mark.txt"
DONE = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g25_done.txt"

def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass

def bbx(b):
    bb = b.GetBoundingBox(Matrix.Identity)
    return "z[%.3f..%.3f] x[%.2f..%.2f] y[%.2f..%.2f] V=%.3f pieces=%d closed=%s" % (
        bb.MinCorner.Z * 1000, bb.MaxCorner.Z * 1000,
        bb.MinCorner.X * 1000, bb.MaxCorner.X * 1000,
        bb.MinCorner.Y * 1000, bb.MaxCorner.Y * 1000,
        b.Volume * 1e9, b.PieceCount, b.IsClosed)

def dome(zbase, h, k=0.55):
    secs = List[CadPrimitivesService.LoftSection]()
    s1 = CadPrimitivesService.LoftSection()
    s1.Shape = "rounded_rect"; s1.WMm = 27.0; s1.HMm = 37.0; s1.CornerRMm = 1.5
    s1.CenterMm = System.Array[float]([0.0, 0.0, zbase])
    s2 = CadPrimitivesService.LoftSection()
    s2.Shape = "rounded_rect"; s2.WMm = 27.0 * k; s2.HMm = 37.0 * k; s2.CornerRMm = 1.5 * k
    s2.CenterMm = System.Array[float]([0.0, 0.0, zbase + h])
    secs.Add(s1); secs.Add(s2)
    return CadPrimitivesService().Loft(secs, System.Array[float]([0.0, 0.0, 1.0]), True)

def _do():
    _mk("probe-start")
    Document.Create()
    part = Window.ActiveWindow.Document.MainPart

    core = BodyBuilder.CreateBlock(0.040, 0.030, 0.004)   # plain block core, z 0..4
    _mk("core  " + bbx(core))
    d = dome(3.95, 0.55)
    _mk("dome  " + bbx(d))

    tools = List[Body](); tools.Add(d)
    try:
        core.Unite(tools)
        _mk("unite OK -> " + bbx(core))
    except System.Exception as e:
        _mk("unite THREW " + e.GetType().Name + ": " + e.Message)

    # reverse direction for comparison
    core2 = BodyBuilder.CreateBlock(0.040, 0.030, 0.004)
    d2 = dome(3.95, 0.55)
    t2 = List[Body](); t2.Add(core2)
    try:
        d2.Unite(t2)
        _mk("rev-unite OK -> " + bbx(d2))
    except System.Exception as e:
        _mk("rev-unite THREW " + e.GetType().Name + ": " + e.Message)

try:
    WriteBlock.ExecuteTask("probe", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name + ": " + e.Message)
File.WriteAllText(DONE, "done\n", UTF8Encoding(False))
