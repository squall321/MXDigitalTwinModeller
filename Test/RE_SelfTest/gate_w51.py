# encoding: utf-8
# =====================================================================
# gate_w51.py — validate W5-1: IntersectCurve-based LOCAL through-extent
# for Add* cutters on thin curved flanges (11752), judged by kernel truth.
#
# AddHole/AddSlit/AddRib size their cutter from the body BBOX projected on
# the placement normal. On a thin curved flange the bbox overshoots into air
# and the cutter (positioned from bbox) misses the local material → dV≈0
# (11752 Add* "volOk=False"). Fix: Body.IntersectCurve(normal line through P)
# gives the REAL entry/exit where the normal pierces solid AT P; size+position
# the cutter to that LOCAL segment.
#
# This gate, on 11752:
#   1) find a placeable planar face (largest, straddle-validated) → P + inward normal,
#   2) IntersectCurve a line through P along the normal → entry/exit params,
#   3) build a hole cutter over the LOCAL in-solid segment, Subtract on the live body,
#   4) assert dV < 0 (cutter engaged) — the bbox approach gives dV≈0 here.
#
# Spec: gate_target.txt = "<path>"   Out: gate_result.json + gate_done.txt
# =====================================================================
import os, sys, json, math, traceback
REAL_CAD = r"D:\MXDigitalTwinModeller\Test\RealCAD"
OUT_BASE = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest"
ADDIN_DLL = r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll"
TARGET = os.path.join(REAL_CAD, "gate_target.txt")
DONE   = os.path.join(REAL_CAD, "gate_done.txt")
RESULT = os.path.join(REAL_CAD, "gate_result.json")
DEFAULT_MODEL = os.path.join(REAL_CAD, "pythonocc", "11752.stp")
if OUT_BASE not in sys.path: sys.path.append(OUT_BASE)
rep = {"verdict": "ERROR"}
def finish():
    try:
        from System.IO import File as F
        from System.Text import UTF8Encoding
        F.WriteAllText(RESULT, json.dumps(rep, indent=2), UTF8Encoding(False))
        F.WriteAllText(DONE, "done\n", UTF8Encoding(False))
    except Exception: pass
try:
    import clr
    clr.AddReferenceToFileAndPath(ADDIN_DLL)
    from SpaceClaim.Api.V252 import Document, Window, DesignBody
    from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import RealModelPipeline
    from SpaceClaim.Api.V252.Geometry import (Point, Direction, Frame, Plane, CircleProfile,
                                              PointUV, Line, Matrix)
    from SpaceClaim.Api.V252.Modeler import Body
    import System
    try: RealModelPipeline.ApplyColors = False
    except Exception: pass
    try:
        if Window.ActiveWindow is None: Document.Create()
    except Exception: pass
    path = DEFAULT_MODEL
    if os.path.exists(TARGET):
        from System.IO import File as F
        t = F.ReadAllText(TARGET).strip().split("\t")
        if t and t[0]: path = t[0]
    rep["model"] = path
    pr = RealModelPipeline.Run(path, REAL_CAD)
    if pr is None or not pr.Success or pr.ImportedBody is None:
        rep["verdict"] = "LOAD_FAIL"; finish(); raise SystemExit
    body = pr.ImportedBody
    vol0 = float(body.Shape.Volume)
    rep["vol0_mm3"] = round(vol0*1e9, 3)

    # --- placement: largest planar face, straddle-validated centre ---
    def containsM(p):
        try: return body.Shape.ContainsPoint(p)
        except Exception: return None
    cands = []
    for df in body.Faces:
        try:
            g = df.Shape.Geometry
            if type(g).__name__ != "Plane": continue
            cands.append((float(df.Area), df, g))
        except Exception: continue
    cands.sort(key=lambda t: -t[0])
    EPS = 2e-4
    place = None
    for area, df, g in cands:
        try:
            bb = df.Shape.GetBoundingBox(Matrix.Identity)
            cen = Point.Create((bb.MinCorner.X+bb.MaxCorner.X)/2.0,
                               (bb.MinCorner.Y+bb.MaxCorner.Y)/2.0,
                               (bb.MinCorner.Z+bb.MaxCorner.Z)/2.0)
            ev = g.ProjectPoint(cen)
            if ev is None: continue
            P = ev.Point
            nz = g.Frame.DirZ.UnitVector
            nrm = (nz.X, nz.Y, nz.Z)
            plus = containsM(Point.Create(P.X+nrm[0]*EPS, P.Y+nrm[1]*EPS, P.Z+nrm[2]*EPS))
            minus = containsM(Point.Create(P.X-nrm[0]*EPS, P.Y-nrm[1]*EPS, P.Z-nrm[2]*EPS))
            if plus == minus: continue
            inward = (-nrm[0],-nrm[1],-nrm[2]) if plus is False else (nrm[0],nrm[1],nrm[2])
            place = (P, inward, area)
            break
        except Exception: continue
    if place is None:
        rep["verdict"] = "NO_PLACEMENT"; finish(); raise SystemExit
    P, inward, area = place
    rep["place"] = {"P_mm": [round(P.X*1000,2), round(P.Y*1000,2), round(P.Z*1000,2)],
                    "inward": [round(x,3) for x in inward], "faceArea_mm2": round(area*1e6,1)}

    # --- IntersectCurve along the inward normal line through P ---
    line = Line.Create(P, Direction.Create(inward[0], inward[1], inward[2]))
    hits = body.Shape.IntersectCurve(line)
    ts = []
    for ip in hits:
        try:
            pt = ip.Point
            t = (pt.X-P.X)*inward[0] + (pt.Y-P.Y)*inward[1] + (pt.Z-P.Z)*inward[2]
            ts.append(t)
        except Exception: continue
    ts.sort()
    rep["intersect_t_mm"] = [round(t*1000,3) for t in ts]
    # local in-solid segment starting at/just past P (t≈0 is the surface). Find the
    # first segment [a,b] with a>=~0 whose midpoint is solid.
    seg = None
    cand_ts = [0.0] + [t for t in ts if t > 1e-6]
    cand_ts = sorted(set(cand_ts))
    for i in range(len(cand_ts)-1):
        a, b = cand_ts[i], cand_ts[i+1]
        mid = (a+b)/2.0
        mp = Point.Create(P.X+inward[0]*mid, P.Y+inward[1]*mid, P.Z+inward[2]*mid)
        if containsM(mp):
            seg = (a, b); break
    if seg is None:
        rep["verdict"] = "NO_LOCAL_SOLID"; finish(); raise SystemExit
    a, b = seg
    localDepthMm = (b-a)*1000.0
    rep["local_extent_mm"] = round(localDepthMm, 3)

    # --- build a hole cutter over the LOCAL segment, Subtract, measure dV ---
    rMm = max(min(localDepthMm/3.0, 2.0), 0.5)
    rM = rMm/1000.0
    ov = max(localDepthMm*0.5, 0.5)/1000.0  # protrude both ends
    axD = Direction.Create(inward[0], inward[1], inward[2])
    fdx = axD.ArbitraryPerpendicular
    fdy = Direction.Cross(axD, fdx)
    base = Point.Create(P.X+inward[0]*(a-ov), P.Y+inward[1]*(a-ov), P.Z+inward[2]*(a-ov))
    prof = CircleProfile(Plane.Create(Frame.Create(base, fdx, fdy)), rM, PointUV.Create(0.0,0.0), 0.0)
    # diagnostic: is the cutter mid actually inside solid? (should be — flange material)
    midp = Point.Create(P.X+inward[0]*(a+(b-a)/2.0), P.Y+inward[1]*(a+(b-a)/2.0), P.Z+inward[2]*(a+(b-a)/2.0))
    rep["cutter_mid_solid"] = bool(containsM(midp))
    try:
        cutter = Body.ExtrudeProfile(prof, (b-a) + 2.0*ov)
        rep["cutter_vol_mm3"] = round(float(cutter.Volume)*1e9, 3)
        addDb = DesignBody.Create(body.Parent, "_w51cut", cutter)
        rep["vol_after_create_mm3"] = round(float(body.Shape.Volume)*1e9, 3)  # body only, pre-subtract
        body.Shape.Subtract(System.Array[Body]([addDb.Shape]))
        rep["vol_after_subtract_mm3"] = round(float(body.Shape.Volume)*1e9, 3)
        try: addDb.Delete()
        except Exception: pass
        rep["subtract"] = "OK"
    except Exception as e:
        rep["subtract"] = "FAIL: %s" % e
        rep["verdict"] = "SUBTRACT_FAILED"; finish(); raise SystemExit
    vol1 = float(body.Shape.Volume)
    dV = (vol1 - vol0)*1e9
    rep["dV_mm3"] = round(dV, 5)
    rep["cutter_r_mm"] = round(rMm, 3)
    # cutter engaged ⟺ volume dropped meaningfully (a real bore was removed)
    ok = dV < -1e-4
    rep["verdict"] = "W51_VERIFIED" if ok else "NO_ENGAGE"
    finish()
except SystemExit:
    finish()
except Exception as e:
    rep["verdict"] = "EXC"; rep["msg"] = str(e); rep["trace"] = traceback.format_exc(); finish()
