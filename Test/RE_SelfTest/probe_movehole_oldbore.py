# encoding: utf-8
# Decisive probe: does samplemodel2 MoveHole actually VACATE (solid-fill) the old bore,
# or does the old bore survive? Settles "benign filler artifact" vs "real un-fill bug".
# Method: load model, snapshot volume + old-bore solidity, run MoveHole (+x 20mm),
# then probe the OLD bore axis with ContainsPoint at multiple depths + compare volume.
import os, math, json
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

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\probe_movehole_result.json"
REAL_CAD = r"D:\MXDigitalTwinModeller\Test\RealCAD"
MODEL = r"d:\Program Files\ANSYS Inc\ANSYS Student\v252\SCDM\Library\SrModels\samplemodel2.scdoc"
res = {"model": "samplemodel2"}

def arr(x): return System.Array[float]([x[0]/1000.0, x[1]/1000.0, x[2]/1000.0])

def contains(body, pm):
    try:
        return body.Shape.ContainsPoint(Point.Create(pm[0]/1000.0, pm[1]/1000.0, pm[2]/1000.0))
    except Exception as e:
        return "ERR:%s" % e

def vol(body):
    try: return body.Shape.Volume * 1e9
    except Exception: return None

def cyl_count(body, d_mm):
    rT = d_mm/2.0; n=0
    for f in body.Faces:
        try:
            g=f.Shape.Geometry
            if type(g).__name__!="Cylinder": continue
            if abs(float(g.Radius)*1000.0 - rT) <= max(rT*0.05,0.05): n+=1
        except Exception: pass
    return n

try:
    try: RealModelPipeline.ApplyColors = False
    except Exception: pass
    try: RealModelPipeline.ExtractAllBodies = False
    except Exception: pass
    if Window.ActiveWindow is None: Document.Create()

    pr = RealModelPipeline.Run(MODEL, REAL_CAD)
    if pr is None or not pr.Success:
        res["error"] = "pipeline fail: %s" % (pr.ErrorMessage if pr else "None"); raise SystemExit
    body = pr.ImportedBody; graph = pr.Graph
    res["nHoles"] = graph.Holes.Count if graph.Holes else 0
    if res["nHoles"] == 0:
        res["error"] = "no holes"; raise SystemExit
    h = graph.Holes[0]
    old = (h.PositionMm[0], h.PositionMm[1], h.PositionMm[2])
    d = float(h.DiameterMm)
    ax = h.Axis
    am = math.sqrt(ax[0]**2+ax[1]**2+ax[2]**2)
    au = (ax[0]/am, ax[1]/am, ax[2]/am) if am>1e-9 else (0,0,1)
    res["old"]=list(old); res["d"]=d; res["axis"]=list(au)

    before_probe = []
    for t in (-2,-1,0,1,2):
        p = (old[0]+au[0]*t, old[1]+au[1]*t, old[2]+au[2]*t)
        before_probe.append(contains(body, p))
    res["before_oldaxis_contains"] = before_probe
    res["vol_before"] = vol(body)
    res["cyl_before"] = cyl_count(body, d)

    newp = (old[0]+20.0, old[1], old[2])
    res["newp"]=list(newp)
    mr = ModificationService.MoveHole(body, graph, h.Id, arr(newp))
    res["move_success"] = getattr(mr, "Success", None)
    res["move_hint"] = getattr(mr, "HintMessage", None)

    after_probe = []
    for t in (-2,-1,0,1,2):
        p = (old[0]+au[0]*t, old[1]+au[1]*t, old[2]+au[2]*t)
        after_probe.append(contains(body, p))
    res["after_oldaxis_contains"] = after_probe
    res["vol_after"] = vol(body)
    res["cyl_after"] = cyl_count(body, d)
    # EXACTLY what the driver verify() sees: single-point contains + the helper's verdict
    res["driver_single_contains_old"] = contains(body, old)
    # replicate old_bore_filled(span=2,n=5) majority logic with raw samples
    span=2.0; n=5; raw=[]
    for i in range(n):
        t = -span + (2.0*span)*i/(n-1)
        p = (old[0]+au[0]*t, old[1]+au[1]*t, old[2]+au[2]*t)
        raw.append(contains(body, p))
    res["helper_raw_samples"] = raw
    solid = sum(1 for x in raw if x is True); total = sum(1 for x in raw if x is not None and not str(x).startswith("ERR"))
    res["helper_solid_over_total"] = "%d/%d" % (solid, total)
    res["helper_verdict"] = (solid*2 >= total) if total>0 else None

    bf = res["vol_before"]; af = res["vol_after"]
    res["dV_mm3"] = (af-bf) if (bf is not None and af is not None) else None
    solid_now = sum(1 for x in after_probe if x is True)
    res["old_solid_samples_after"] = solid_now
    res["old_bore_filled"] = solid_now >= 3
    res["CONCLUSION"] = ("OLD BORE FILLED (benign artifact, oracle false-IC)"
                         if res["old_bore_filled"]
                         else "OLD BORE SURVIVES (real un-fill: genuine bug)")
except SystemExit:
    pass
except Exception as e:
    res["error"] = "%s" % e
finally:
    File.WriteAllText(OUT, json.dumps(res, indent=2), UTF8Encoding(False))
