# encoding: utf-8
# v2 g3: can RevolveTrimmedCurves build a SMOOTH cylindrical back (single Cylinder face) with a
# HORIZONTAL (DirX) axis + a large-R arc wedge? spec-63 only proved DirZ axis + cone. If this works
# we get a clean Cylinder face for robust P4 curved-face targeting; else Z-stack stays primary.
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g3_mark.txt"
def _mk(s):
    try:
        from System.IO import File as _F
        from System.Text import UTF8Encoding as _U
        _F.AppendAllText(MARK, s + "\n", _U(False))
    except Exception: pass
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
_mk("clr-ok")
import System
from System import Array
from System.Collections.Generic import List
from System.IO import File
from System.Text import UTF8Encoding
_mk("system-ok")
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task, DesignBody
_mk("core-ok")
from SpaceClaim.Api.V252 import Unsupported
_mk("unsupported-ok")
from SpaceClaim.Api.V252.Modeler import Body
_mk("body-ok")
from SpaceClaim.Api.V252.Geometry import Point, Direction, RectangleProfile, PointUV, Plane, Matrix
_mk("geom1-ok")
from SpaceClaim.Api.V252.Geometry import CurveSegment
_mk("curveseg-ok")
from SpaceClaim.Api.V252.Geometry import ITrimmedCurve
_mk("itrimmed-ok")

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g3_revolve_result.txt"
log = []
def emit(s): log.append(str(s))

L, W, T = 146.7, 71.5, 7.4
bulge = 0.6
halfW = W / 2.0
R = (halfW * halfW) / (2.0 * bulge) + bulge / 2.0
emit("params: W=%.1f bulge=%.2f -> R=%.1f mm" % (W, bulge, R))

def m(x): return x / 1000.0

holder = {}
def _do():
    _mk("do-start")
    Document.Create()
    part = Window.ActiveWindow.Document.MainPart
    prof = RectangleProfile(Plane.PlaneXY, m(L), m(W), PointUV.Create(0, 0), 0.0)
    box = Body.ExtrudeProfile(prof, m(T))
    body = DesignBody.Create(part, "SlabRev", box)
    holder["body"] = body
    _mk("slab-ok")

    eps = 1e-6
    overlap = 0.5e-3
    zCenter = m(T + bulge) - m(R)            # cylinder axis Z (large R => well below the slab)
    zArcBase = m(T) - overlap
    # Closed wedge in the Y-Z plane (x=0), revolved about X. Near-axis points offset by eps.
    pAxisBase = Point.Create(0, eps, zCenter)
    pApex = Point.Create(0, eps, zCenter + m(R))     # crown = axis + R
    pOuter = Point.Create(0, m(halfW), zArcBase)
    segs = List[ITrimmedCurve]()
    segs.Add(CurveSegment.Create(pAxisBase, pApex))
    segs.Add(CurveSegment.Create(pApex, pOuter))
    segs.Add(CurveSegment.Create(pOuter, pAxisBase))
    _mk("profile-built")
    arc = None
    try:
        _mk("revolve-calling")
        arc = Unsupported.BodyMethods.RevolveTrimmedCurves(segs, Point.Create(0, 0, zCenter), Direction.DirX, 360.0)
        _mk("revolve-returned")
        emit("revolve: returned body=%s" % (arc is not None))
    except System.Exception as e:
        emit("revolve THREW: %s: %s" % (e.GetType().Name, e.Message)); arc = None
    if arc is not None:
        try:
            isc = bool(arc.IsClosed); pc = int(arc.PieceCount)
            emit("arc: IsClosed=" + str(isc) + " PieceCount=" + str(pc))
            _mk("arc-props-ok")
            arc.Reverse()
            _mk("arc-reversed")
            body.Shape.Unite(Array[Body]([arc]))
            _mk("arc-united")
            emit("after Unite: IsClosed=" + str(bool(body.Shape.IsClosed)) +
                 " PieceCount=" + str(int(body.Shape.PieceCount)) +
                 " vol=" + ("%.1f" % (body.Shape.Volume * 1e9)))
        except System.Exception as ue:
            emit("Reverse/Unite THREW: " + ue.GetType().Name)
            _mk("arc-block-exc")
    _mk("arc-block-done")
    ncyl = 0
    try:
        for f in body.Faces:
            try:
                if type(f.Shape.Geometry).__name__ == "Cylinder": ncyl += 1
            except System.Exception: pass
        _mk("faces-counted")
        bb = body.Shape.GetBoundingBox(Matrix.Identity)
        zmaxv = bb.MaxCorner.Z * 1000.0
        emit("RESULT cylFaces=%d zmax=%.3f vol=%.1f" % (ncyl, zmaxv, body.Shape.Volume * 1e9))
        holder["ncyl"] = ncyl; holder["zmax"] = zmaxv
        _mk("result-emitted")
    except System.Exception as e:
        emit("POST-REVOLVE THREW: %s: %s" % (e.GetType().Name, e.Message))
        _mk("post-threw")

try:
    WriteBlock.ExecuteTask("g3_revolve", Task(_do))
    _mk("writeblock-done")
except System.Exception as e:
    emit("WRITEBLOCK THREW: %s: %s" % (e.GetType().Name, e.Message))
    _mk("writeblock-threw")
ncyl = holder.get("ncyl", 0); zmax = holder.get("zmax", 0)
ok = (ncyl >= 1) and (zmax > T + bulge * 0.5)
emit("G3_PASS smoothCylFace(%d>=1)=%s curvedZmax(%.2f>%.2f)=%s ALL=%s" % (
    ncyl, ncyl >= 1, zmax, T + bulge * 0.5, zmax > T + bulge * 0.5, ok))
# sanitize: drop any control/null bytes SC may have injected into messages
safe = []
for ln in log:
    try: safe.append("".join(ch for ch in ln if ord(ch) >= 32 or ch == "\t"))
    except Exception: safe.append("(unprintable)")
File.WriteAllText(OUT, "\n".join(safe) + "\n", UTF8Encoding(False))
