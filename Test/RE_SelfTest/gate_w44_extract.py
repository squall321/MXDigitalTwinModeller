# encoding: utf-8
# gate_w44_extract.py — W4-4 keystone: does ExtractAllBodies surface features
# from EVERY body of an assembly? (currently only body[0] is extracted, so 4/5
# of as1-oc's bodies are invisible to extract/modify/verify). Reports per-shell
# feature counts + confirms shells[i] ↔ allBodies[i] parallel order for targeting.
# Spec: gate_target.txt = "<path>"   Out: gate_result.json + gate_done.txt
import os, sys, json, traceback
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
    from SpaceClaim.Api.V252.Geometry import Matrix
    try: RealModelPipeline.ApplyColors = False
    except Exception: pass
    RealModelPipeline.ExtractAllBodies = True   # <-- the keystone toggle
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
    if pr is None or not pr.Success or pr.Graph is None:
        rep["verdict"] = "LOAD_FAIL"; finish(); raise SystemExit
    g = pr.Graph
    rep["top_graph"] = {"holes": g.Holes.Count if g.Holes else 0,
                        "bosses": g.Bosses.Count if g.Bosses else 0,
                        "fillets": g.FilletChains.Count if g.FilletChains else 0}
    shells = list(g.Shells) if g.Shells else []
    rep["shell_count"] = len(shells)
    per = []
    totH=totB=totF=0
    for i, sg in enumerate(shells):
        h = sg.Holes.Count if sg.Holes else 0
        b = sg.Bosses.Count if sg.Bosses else 0
        fil = sg.FilletChains.Count if sg.FilletChains else 0
        nm = sg.BodyName or ""
        per.append({"i": i, "body": nm[:30], "H": h, "B": b, "Fil": fil})
        totH+=h; totB+=b; totF+=fil
    rep["per_shell"] = per
    rep["aggregate"] = {"holes": totH, "bosses": totB, "fillets": totF}
    # parallel order check: allBodies count == shell count?
    doc = Window.ActiveWindow.Document if Window.ActiveWindow else None
    if doc is not None:
        nb = len(list(PartBodyTraversal.FindAllDesignBodies(doc)))
        rep["n_bodies"] = nb
        rep["parallel_ok"] = (nb == len(shells))
    # keystone success: >1 shell AND aggregate features > top-graph (other bodies surfaced)
    topfeat = rep["top_graph"]["holes"]+rep["top_graph"]["bosses"]+rep["top_graph"]["fillets"]
    aggfeat = totH+totB+totF
    rep["verdict"] = "W44_EXTRACT_OK" if (len(shells) > 1 and aggfeat > topfeat) else "NO_GAIN"
    finish()
except SystemExit:
    finish()
except Exception as e:
    rep["verdict"] = "EXC"; rep["msg"] = str(e); rep["trace"] = traceback.format_exc(); finish()
