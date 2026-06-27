# encoding: utf-8
# =====================================================================
# gate_localfill_probe.py — Phase 4 keystone gate.
#
# Hypothesis: MoveHole/RemoveHole "fill General Failure" on curved/concave
# parts (11752) is caused by the bbox-PROJECTION fill — a filler cylinder
# spanning the ENTIRE bbox extent along the hole axis pokes OUTSIDE the
# solid where the body is non-convex, so Unite produces an invalid body.
#
# Fix to validate: Body.IntersectCurve(axisLine) gives the REAL entry/exit
# points where the axis pierces the solid. Filling ONLY the in-solid
# segment (entry->exit through the bore) should Unite cleanly.
#
# This probe, on 11752:
#   1) finds a hole with an axis,
#   2) builds an (extended) axis line and calls body.Shape.IntersectCurve,
#      dumping the return TYPE + each hit point/param/face,
#   3) classifies in-solid segments via midpoint ContainsPoint,
#   4) builds a filler over (a) the bbox extent and (b) the local in-solid
#      extent, on a DETACHED COPY each, and reports which Unite SUCCEEDS.
#
# Spec: gate_target.txt = "<path>"   (defaults to 11752 if absent)
# Out:  gate_result.json + gate_done.txt
# =====================================================================
import os, json, math, traceback
from datetime import datetime

ADDIN_DLL = r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll"
OUT_BASE  = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest"
REAL_CAD  = r"D:\MXDigitalTwinModeller\Test\RealCAD"
TARGET    = os.path.join(REAL_CAD, "gate_target.txt")
DONE      = os.path.join(REAL_CAD, "gate_done.txt")
RESULT    = os.path.join(REAL_CAD, "gate_result.json")
LOG_PATH  = os.path.join(OUT_BASE, "headless_run.log")
DEFAULT_MODEL = os.path.join(REAL_CAD, "pythonocc", "11752.stp")

rep = {"steps": [], "verdict": "ERROR", "msg": ""}

def log(msg):
    line = "[%s] GATE %s" % (datetime.now().strftime("%H:%M:%S"), msg)
    try: print(line)
    except Exception: pass
    try:
        from System.IO import File as IoFile
        from System.Text import UTF8Encoding
        IoFile.AppendAllText(LOG_PATH, line + "\r\n", UTF8Encoding(False))
    except Exception: pass

def finish():
    try:
        from System.IO import File as IoFile
        from System.Text import UTF8Encoding
        IoFile.WriteAllText(RESULT, json.dumps(rep, indent=2), UTF8Encoding(False))
        IoFile.WriteAllText(DONE, "done\n", UTF8Encoding(False))
    except Exception: pass

try:
    import clr
    clr.AddReferenceToFileAndPath(ADDIN_DLL)
    from SpaceClaim.Api.V252 import Document, Window
    from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import (
        FeatureExtractor, ModificationService, RealModelPipeline,
    )
    from SpaceClaim.Api.V252.Geometry import (Point, Direction, Line, Frame, Plane,
                                              CircleProfile, Matrix)
    from SpaceClaim.Api.V252.Modeler import Body
    import System

    try: RealModelPipeline.ApplyColors = False
    except Exception: pass
    try:
        if Window.ActiveWindow is None: Document.Create()
    except Exception: pass

    path = DEFAULT_MODEL
    if os.path.exists(TARGET):
        from System.IO import File as IoFile
        t = IoFile.ReadAllText(TARGET).strip()
        if t: path = t
    rep["model"] = path
    log("model = %s" % path)

    pr = RealModelPipeline.Run(path, REAL_CAD)
    if pr is None or not pr.Success or pr.ImportedBody is None or pr.Graph is None:
        rep["msg"] = "load failed"; finish(); raise SystemExit
    body = pr.ImportedBody; graph = pr.Graph

    # --- pick a hole with an axis ---
    hole = None
    if graph.Holes:
        for h in graph.Holes:
            if h.Axis is not None and len(h.Axis) >= 3 and h.PositionMm is not None:
                hole = h; break
    if hole is None:
        rep["msg"] = "no hole with axis"; finish(); raise SystemExit

    P = (hole.PositionMm[0]/1000.0, hole.PositionMm[1]/1000.0, hole.PositionMm[2]/1000.0)
    a = hole.Axis
    amag = math.sqrt(a[0]**2 + a[1]**2 + a[2]**2)
    axisU = (a[0]/amag, a[1]/amag, a[2]/amag)
    dM = hole.DiameterMm/1000.0
    rep["hole"] = {"id": hole.Id, "Dmm": hole.DiameterMm, "posMm": list(hole.PositionMm), "axis": list(axisU)}
    log("hole %s Dmm=%.3f axis=(%.3f,%.3f,%.3f)" % (hole.Id, hole.DiameterMm, axisU[0],axisU[1],axisU[2]))

    # bbox along-axis extent (the CURRENT bbox-projection approach)
    bb = body.Shape.GetBoundingBox(Matrix.Identity)
    tmin, tmax = 1e30, -1e30
    for ci in range(8):
        cx = bb.MinCorner.X if (ci&1)==0 else bb.MaxCorner.X
        cy = bb.MinCorner.Y if (ci&2)==0 else bb.MaxCorner.Y
        cz = bb.MinCorner.Z if (ci&4)==0 else bb.MaxCorner.Z
        t = (cx-P[0])*axisU[0] + (cy-P[1])*axisU[1] + (cz-P[2])*axisU[2]
        tmin = min(tmin, t); tmax = max(tmax, t)
    rep["bbox_extent_mm"] = round((tmax-tmin)*1000.0, 3)

    # --- STEP 2: Body.IntersectCurve(axis line) ---
    # Build a line spanning the bbox extent (+pad) along the axis.
    pad = (tmax - tmin) * 0.05 + 1e-3
    line = Line.Create(Point.Create(P[0], P[1], P[2]), Direction.Create(axisU[0], axisU[1], axisU[2]))
    hits = None; hit_info = []
    try:
        hits = body.Shape.IntersectCurve(line)
        rep.setdefault("steps", []).append("IntersectCurve OK type=%s" % type(hits).__name__)
        for ip in hits:
            # discover members defensively
            pt = None; param = None
            for attr in ("Point",):
                try: pt = getattr(ip, attr)
                except Exception: pass
            for attr in ("Parameter", "Param", "EvaluationParameter"):
                try:
                    v = getattr(ip, attr)
                    param = float(v); break
                except Exception: pass
            rec = {}
            if pt is not None:
                rec["pt"] = [round(pt.X*1000,3), round(pt.Y*1000,3), round(pt.Z*1000,3)]
                # signed param along axis from P
                rec["t_mm"] = round(((pt.X-P[0])*axisU[0]+(pt.Y-P[1])*axisU[1]+(pt.Z-P[2])*axisU[2])*1000.0, 3)
            if param is not None: rec["curve_param"] = round(param, 6)
            rec["members"] = [m for m in dir(ip) if not m.startswith("_")][:12]
            hit_info.append(rec)
        rep["intersect_count"] = len(hit_info)
        rep["intersections"] = hit_info
        log("IntersectCurve -> %d hits" % len(hit_info))
    except Exception as e:
        rep["steps"].append("IntersectCurve FAIL: %s" % e)
        log("IntersectCurve FAIL: %s" % e)

    # --- STEP 3: in-solid segments via midpoint ContainsPoint ---
    ts = sorted([h["t_mm"]/1000.0 for h in hit_info if "t_mm" in h])
    in_solid_segs = []
    for i in range(len(ts)-1):
        mid = (ts[i]+ts[i+1])/2.0
        mp = Point.Create(P[0]+axisU[0]*mid, P[1]+axisU[1]*mid, P[2]+axisU[2]*mid)
        try: inside = body.Shape.ContainsPoint(mp)
        except Exception: inside = None
        if inside:
            in_solid_segs.append((ts[i], ts[i+1]))
    rep["in_solid_segments_mm"] = [[round(s*1000,3), round(e*1000,3)] for s,e in in_solid_segs]
    log("in-solid segments: %s" % rep["in_solid_segments_mm"])

    # --- STEP 4: compare Unite success: bbox-extent vs local-extent ---
    def try_fill(base_t, height_t, label):
        """Build a filler cylinder over [base_t, base_t+height_t] along axis on a
        DETACHED COPY of the body and report Unite Success."""
        from SpaceClaim.Api.V252.Modeler import Body as _B
        res = {"label": label, "ok": False, "err": ""}
        try:
            faceMap = None; edgeMap = None
            copy = body.Shape.Copy()  # detached Body
        except Exception as e:
            res["err"] = "copy fail: %s" % e; return res
        try:
            axD = Direction.Create(axisU[0], axisU[1], axisU[2])
            base = Point.Create(P[0]+axisU[0]*base_t, P[1]+axisU[1]*base_t, P[2]+axisU[2]*base_t)
            fdx = axD.ArbitraryPerpendicular
            fdy = Direction.Cross(axD, fdx)
            frame = Frame.Create(base, fdx, fdy)
            prof = CircleProfile(Plane.Create(frame), dM/2.0)
            filler = _B.ExtrudeProfile(prof, height_t)
            copy.Unite([filler])
            res["ok"] = True
        except Exception as e:
            res["err"] = str(e)
        return res

    rep["fill_bbox"]  = try_fill(tmin, (tmax-tmin), "bbox_extent")
    if in_solid_segs:
        s0, e0 = in_solid_segs[0]
        rep["fill_local"] = try_fill(s0 - 0.0005, (e0 - s0) + 0.001, "local_in_solid")
    else:
        rep["fill_local"] = {"label": "local_in_solid", "ok": False, "err": "no in-solid segment"}
    log("fill bbox ok=%s | local ok=%s" % (rep["fill_bbox"]["ok"], rep["fill_local"]["ok"]))

    # verdict: keystone CONFIRMED if local Unite succeeds (esp. where bbox fails)
    if rep["fill_local"]["ok"] and not rep["fill_bbox"]["ok"]:
        rep["verdict"] = "KEYSTONE_CONFIRMED"
    elif rep["fill_local"]["ok"] and rep["fill_bbox"]["ok"]:
        rep["verdict"] = "BOTH_OK"
    else:
        rep["verdict"] = "INCONCLUSIVE"
    rep["msg"] = "bbox_ok=%s local_ok=%s" % (rep["fill_bbox"]["ok"], rep["fill_local"]["ok"])
    finish()

except SystemExit:
    finish()
except Exception as e:
    rep["msg"] = "EXC: %s" % e
    rep["trace"] = traceback.format_exc()
    finish()
