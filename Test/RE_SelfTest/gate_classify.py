# encoding: utf-8
# gate_classify.py — W4-2c check: report hole/boss classification per model.
# Confirms 11752 H1 (a PIN) reclassifies hole->boss while real-hole models hold.
# Spec: gate_target.txt = "<path>"   Out: gate_result.json + gate_done.txt
import os, json, traceback
from datetime import datetime
REAL_CAD = r"D:\MXDigitalTwinModeller\Test\RealCAD"
ADDIN_DLL = r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll"
TARGET = os.path.join(REAL_CAD, "gate_target.txt")
DONE   = os.path.join(REAL_CAD, "gate_done.txt")
RESULT = os.path.join(REAL_CAD, "gate_result.json")
DEFAULT_MODEL = os.path.join(REAL_CAD, "pythonocc", "11752.stp")
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
    if pr is None or not pr.Success or pr.Graph is None:
        rep["verdict"] = "LOAD_FAIL"; finish(); raise SystemExit
    g = pr.Graph
    holes = []
    if g.Holes:
        for h in g.Holes:
            holes.append({"id": h.Id, "Dmm": round(float(h.DiameterMm),3)})
    bosses = []
    if g.Bosses:
        for b in g.Bosses:
            try: bosses.append({"id": b.Id, "Dmm": round(float(b.DiameterMm),3)})
            except Exception: bosses.append({"id": getattr(b,"Id","?")})
    rep["holes"] = holes
    rep["bosses"] = bosses
    rep["n_holes"] = len(holes)
    rep["n_bosses"] = len(bosses)
    rep["verdict"] = "OK"
    finish()
except SystemExit:
    finish()
except Exception as e:
    rep["verdict"] = "EXC"; rep["msg"] = str(e); rep["trace"] = traceback.format_exc(); finish()
