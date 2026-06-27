# encoding: utf-8
# W5-2 GATE probe (no C# change): confirm the as1-oc MoveHole non-manifold is a thin-wall
# TANGENCY issue (NOT a body-boundary issue), and that a wall-thickness clamp yields a safe
# new center the kernel accepts. Measures only; then EMPIRICALLY drills at the clamped center
# to find the smallest MIN_WALL the kernel will accept.
import math, json
import clr
ADDIN = r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll"
clr.AddReferenceToFileAndPath(ADDIN)
import System
from SpaceClaim.Api.V252 import Document, Window
from SpaceClaim.Api.V252.Geometry import Point
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import (
    ModificationService, RealModelPipeline)
from System.IO import File
from System.Text import UTF8Encoding

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\probe_w52_result.json"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\probe_w52_mark.txt"
REAL_CAD = r"D:\MXDigitalTwinModeller\Test\RealCAD"
MODEL = r"D:\MXDigitalTwinModeller\Test\RealCAD\stepcode\as1-oc-214.stp"
res = {"model": "as1-oc-214"}

def mark(s):
    try: File.AppendAllText(MARK, s + "\n", UTF8Encoding(False))
    except Exception: pass

mark("imports-ok")

def contains(body, pm):
    try: return body.Shape.ContainsPoint(Point.Create(pm[0]/1000.0, pm[1]/1000.0, pm[2]/1000.0))
    except Exception as e: return "ERR:%s" % e

def vol(body):
    try: return body.Shape.Volume*1e9
    except Exception: return None

try:
    try: RealModelPipeline.ExtractAllBodies = False
    except Exception: pass
    if Window.ActiveWindow is None: Document.Create()
    mark("doc-ok")
    pr = RealModelPipeline.Run(MODEL, REAL_CAD)
    mark("pipeline-ran success=%s" % (pr.Success if pr else "None"))
    if pr is None or not pr.Success:
        res["error"] = "pipeline fail"; raise SystemExit
    body = pr.ImportedBody; graph = pr.Graph
    mark("body=%s graph=%s" % (body is not None, graph is not None))
    res["nHoles"] = graph.Holes.Count if graph.Holes else 0
    if res["nHoles"] == 0: res["error"] = "no holes"; raise SystemExit
    h = graph.Holes[0]
    old = (h.PositionMm[0], h.PositionMm[1], h.PositionMm[2])
    d = float(h.DiameterMm); r = d/2.0
    from SpaceClaim.Api.V252.Geometry import Matrix
    bb = body.Shape.GetBoundingBox(Matrix.Identity)
    mn = (bb.MinCorner.X*1000, bb.MinCorner.Y*1000, bb.MinCorner.Z*1000)
    mx = (bb.MaxCorner.X*1000, bb.MaxCorner.Y*1000, bb.MaxCorner.Z*1000)
    sz = (mx[0]-mn[0], mx[1]-mn[1], mx[2]-mn[2])
    res["old"]=list(old); res["d"]=d; res["bbox_min"]=list(mn); res["bbox_max"]=list(mx); res["size"]=list(sz)

    # harness shift arithmetic
    shift = min(max(2.0*d, 3.0), max(sz[0]*0.25, 3.0), 20.0)
    res["shift"]=shift
    newx = old[0]+shift
    res["new_center_x"]=newx
    # CHECK A: remaining wall at the harness new center
    wallA = mx[0] - (newx + r)
    res["CHECK_A_wall_at_newx"]=wallA   # expect ~0 => tangency
    res["CHECK_A_tangency_confirmed"]= (abs(wallA) < 0.05)

    # CHECK B: clamp solver for a few MIN_WALL candidates
    res["CHECK_B_clamps"]={}
    for mw in (0.3, 0.5, 1.0):
        clamped_x = mx[0] - r - mw     # keep mw from x=xmax wall
        feasible = clamped_x - r > mn[0]  # near edge still inside
        res["CHECK_B_clamps"]["mw_%.1f"%mw] = {"clamped_x":clamped_x, "resulting_wall":mx[0]-(clamped_x+r), "feasible":feasible}

    # CHECK C: ContainsPoint footprint at clamped center (mw=0.5) - all inside?
    cxc = mx[0] - r - 0.5
    foot=[]
    foot.append(contains(body,(cxc,old[1],old[2])))
    for k in range(8):
        a=2*math.pi*k/8
        foot.append(contains(body,(cxc+ (r-0.2)*math.cos(a), old[1]+(r-0.2)*math.sin(a), old[2])))
    res["CHECK_C_clamped_footprint"]=foot
    res["CHECK_C_all_inside"]= all(x is True for x in foot)

    # EMPIRICAL: actually drill AddHole at clamped center (mw=0.5) and see if Subtract accepts
    v0=vol(body)
    newp_clamped=(cxc, old[1], old[2])
    try:
        ar = ModificationService.AddHole(body, System.Array[float]([newp_clamped[0]/1000.0,newp_clamped[1]/1000.0,newp_clamped[2]/1000.0]), d, True, 0.0, None)
        res["EMPIRICAL_clamped_addhole_success"]=getattr(ar,"Success",None)
        res["EMPIRICAL_clamped_addhole_msg"]=getattr(ar,"ErrorMessage",None) or getattr(ar,"HintMessage",None)
        res["EMPIRICAL_dV_mm3"]= (vol(body)-v0) if v0 is not None else None
    except Exception as e:
        res["EMPIRICAL_clamped_addhole_exc"]="%s"%e

    res["CONCLUSION"]="see CHECK_A (tangency) + EMPIRICAL (clamp accepted by kernel?)"
except SystemExit: pass
except Exception as e:
    res["error"]="%s"%e
finally:
    File.WriteAllText(OUT, json.dumps(res, indent=2), UTF8Encoding(False))
