# encoding: utf-8
# =====================================================================
# gate_w44_char.py — characterize multi-body assembly structure for W4-4.
#
# RealModelPipeline picks FindFirstDesignBody (ONE body) but assemblies have
# many. as1-oc MoveHole → "non-manifold" because the new hole position crosses
# a body boundary. This gate reports, for an assembly:
#   • how many DesignBodies (PartBodyTraversal.FindAllDesignBodies),
#   • each body's volume + bbox,
#   • which body OWNS hole H1 (ContainsPoint just inside the bore),
#   • whether the MoveHole new position (old+shift) is inside the OWNING body
#     or crosses to another / to void → the non-manifold root cause.
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
DEFAULT_MODEL = os.path.join(REAL_CAD, "stepcode", "as1-oc-214.stp")
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
    from SpaceClaim.Api.V252 import Document, Window
    from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import (
        RealModelPipeline, PartBodyTraversal)
    from SpaceClaim.Api.V252.Geometry import Matrix, Point
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
    rep["model"] = os.path.basename(path)
    pr = RealModelPipeline.Run(path, REAL_CAD)
    if pr is None or not pr.Success or pr.ImportedBody is None:
        rep["verdict"] = "LOAD_FAIL"; finish(); raise SystemExit
    imported = pr.ImportedBody; g = pr.Graph
    doc = Window.ActiveWindow.Document if Window.ActiveWindow else None
    if doc is None:
        rep["verdict"] = "NO_DOC"; finish(); raise SystemExit

    allBodies = list(PartBodyTraversal.FindAllDesignBodies(doc))
    rep["n_bodies"] = len(allBodies)
    bodies_info = []
    for i, db in enumerate(allBodies):
        try:
            sh = db.Shape
            vol = round(float(sh.Volume)*1e9, 1)
            bb = sh.GetBoundingBox(Matrix.Identity)
            ctr = ((bb.MinCorner.X+bb.MaxCorner.X)/2.0, (bb.MinCorner.Y+bb.MaxCorner.Y)/2.0, (bb.MinCorner.Z+bb.MaxCorner.Z)/2.0)
            sz = [round((bb.MaxCorner.X-bb.MinCorner.X)*1000,1), round((bb.MaxCorner.Y-bb.MinCorner.Y)*1000,1), round((bb.MaxCorner.Z-bb.MinCorner.Z)*1000,1)]
            is_imported = (db.Equals(imported)) if hasattr(db,"Equals") else (db is imported)
            bodies_info.append({"i": i, "vol_mm3": vol, "size_mm": sz, "is_imported": bool(is_imported), "ctr": ctr})
        except Exception as e:
            bodies_info.append({"i": i, "err": str(e)})
    rep["bodies"] = [{k:v for k,v in b.items() if k!="ctr"} for b in bodies_info]

    # hole H1 ownership + MoveHole new-position body
    if g.Holes and g.Holes.Count > 0:
        h = g.Holes[0]
        pm = h.PositionMm
        d = float(h.DiameterMm)
        oldM = (pm[0]/1000.0, pm[1]/1000.0, pm[2]/1000.0)
        shift_mm = min(max(2.0*d,3.0), 20.0)
        newM = (oldM[0]+shift_mm/1000.0, oldM[1], oldM[2])
        def owner(ptM):
            owners = []
            for b in bodies_info:
                if "err" in b: continue
                try:
                    if allBodies[b["i"]].Shape.ContainsPoint(Point.Create(ptM[0],ptM[1],ptM[2])):
                        owners.append(b["i"])
                except Exception: pass
            return owners
        # probe slightly inside along axis to land in bore wall region — but bore centre
        # is void; instead probe a point offset perpendicular into wall (radius*0.6)
        rep["hole"] = {"id": h.Id, "Dmm": d, "posMm": list(pm), "shift_mm": shift_mm}
        rep["old_centre_owners"] = owner(oldM)
        rep["new_centre_owners"] = owner(newM)
        # nearby-solid owner: sample around old at radius 0.7*r perpendicular-ish
        rr = d/2.0/1000.0*0.7
        rep["old_wall_owners"] = owner((oldM[0]+rr, oldM[1], oldM[2]))
        rep["new_wall_owners"] = owner((newM[0]+rr, newM[1], newM[2]))
    rep["verdict"] = "OK"
    finish()
except SystemExit:
    finish()
except Exception as e:
    rep["verdict"] = "EXC"; rep["msg"] = str(e); rep["trace"] = traceback.format_exc(); finish()
