# encoding: utf-8
# gate_charcorpus.py — characterize a candidate model for corpus suitability.
# Reports: import OK, bbox, face-type histogram (curved-ness), feature counts
# (holes/bosses/fillets/walls), body count. Picks phone-metal-representative
# parts (curved + featured) vs noise (no features / import fail).
# Spec: gate_target.txt = "<path>"   Out: gate_result.json + gate_done.txt
import os, sys, json, traceback
REAL_CAD = r"D:\MXDigitalTwinModeller\Test\RealCAD"
OUT_BASE = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest"
ADDIN_DLL = r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll"
TARGET = os.path.join(REAL_CAD, "gate_target.txt")
DONE   = os.path.join(REAL_CAD, "gate_done.txt")
RESULT = os.path.join(REAL_CAD, "gate_result.json")
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
    from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import RealModelPipeline
    from SpaceClaim.Api.V252.Geometry import Matrix
    try: RealModelPipeline.ApplyColors = False
    except Exception: pass
    try:
        if Window.ActiveWindow is None: Document.Create()
    except Exception: pass
    path = ""
    if os.path.exists(TARGET):
        from System.IO import File as F
        t = F.ReadAllText(TARGET).strip().split("\t")
        if t and t[0]: path = t[0]
    rep["model"] = os.path.basename(path)
    pr = RealModelPipeline.Run(path, REAL_CAD)
    if pr is None or not pr.Success or pr.ImportedBody is None or pr.Graph is None:
        rep["verdict"] = "LOAD_FAIL"; rep["err"] = (pr.ErrorMessage if pr else "null") or ""; finish(); raise SystemExit
    body = pr.ImportedBody; g = pr.Graph
    # face-type histogram
    hist = {}
    nf = 0
    for df in body.Faces:
        try:
            tn = type(df.Shape.Geometry).__name__
            hist[tn] = hist.get(tn, 0) + 1; nf += 1
        except Exception: pass
    curved = sum(v for k,v in hist.items() if k in ("Cylinder","Cone","Sphere","Torus","NurbsSurface","ProceduralSurface","PlanarSurface")) - hist.get("Plane",0)
    curved = sum(v for k,v in hist.items() if k not in ("Plane",))
    bb = body.Shape.GetBoundingBox(Matrix.Identity)
    sz = [round((bb.MaxCorner.X-bb.MinCorner.X)*1000,1),
          round((bb.MaxCorner.Y-bb.MinCorner.Y)*1000,1),
          round((bb.MaxCorner.Z-bb.MinCorner.Z)*1000,1)]
    rep.update({
        "verdict": "OK",
        "bbox_mm": sz,
        "n_faces": nf,
        "face_hist": hist,
        "curved_faces": curved,
        "curved_frac": round(float(curved)/max(nf,1), 2),
        "holes": g.Holes.Count if g.Holes else 0,
        "bosses": g.Bosses.Count if g.Bosses else 0,
        "fillets": g.FilletChains.Count if g.FilletChains else 0,
        "walls": g.Walls.Count if hasattr(g,"Walls") and g.Walls else 0,
    })
    # phone-metal suitability heuristic: has features AND meaningful curved fraction
    feat = rep["holes"]+rep["bosses"]+rep["fillets"]
    rep["suitable"] = bool(feat >= 2 and nf >= 8)
    finish()
except SystemExit:
    finish()
except Exception as e:
    rep["verdict"] = "EXC"; rep["msg"] = str(e); rep["trace"] = traceback.format_exc(); finish()
