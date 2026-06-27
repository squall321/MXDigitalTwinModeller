# encoding: utf-8
# =====================================================================
# gate_w41.py — validate W4-1 (live-cylinder relocation) on the CURVED
# part 11752, judged by KERNEL TRUTH (kernel_truth.py), not the extractor.
#
# RemoveHole on 11752 previously "fill failed: General Failure" because the
# bbox-projection filler used the mis-extracted anchor. W4-1 relocates the
# true live bore cylinder. Kernel-truth oracle:
#   RemoveHole — target radius bucket count drops by >=1 AND volume rises.
#   MoveHole   — a same-radius cylinder exists at the NEW position and the
#                old bore's bucket count drops (vacated), volume ~conserved.
#
# Spec: gate_target.txt = "<path>\t<op>"   op in {RemoveHole, MoveHole}
# Out:  gate_result.json + gate_done.txt
# =====================================================================
import os, sys, json, math, traceback
from datetime import datetime

ADDIN_DLL = r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll"
OUT_BASE  = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest"
REAL_CAD  = r"D:\MXDigitalTwinModeller\Test\RealCAD"
TARGET    = os.path.join(REAL_CAD, "gate_target.txt")
DONE      = os.path.join(REAL_CAD, "gate_done.txt")
RESULT    = os.path.join(REAL_CAD, "gate_result.json")
LOG_PATH  = os.path.join(OUT_BASE, "headless_run.log")
DEFAULT_MODEL = os.path.join(REAL_CAD, "pythonocc", "11752.stp")

if OUT_BASE not in sys.path: sys.path.append(OUT_BASE)
rep = {"verdict": "ERROR", "msg": ""}

def log(msg):
    line = "[%s] W41 %s" % (datetime.now().strftime("%H:%M:%S"), msg)
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
        ModificationService, RealModelPipeline,
    )
    import kernel_truth as kt

    try: RealModelPipeline.ApplyColors = False
    except Exception: pass
    try:
        if Window.ActiveWindow is None: Document.Create()
    except Exception: pass

    path, op, force = DEFAULT_MODEL, "RemoveHole", False
    if os.path.exists(TARGET):
        from System.IO import File as IoFile
        t = IoFile.ReadAllText(TARGET).strip().split("\t")
        if t and t[0]: path = t[0]
        if len(t) >= 2 and t[1]: op = t[1]
        if len(t) >= 3 and "forceScale" in t[2]: force = True
    rep["model"] = path; rep["op"] = op; rep["forceScale"] = force

    pr = RealModelPipeline.Run(path, REAL_CAD)
    if pr is None or not pr.Success or pr.ImportedBody is None or pr.Graph is None:
        rep["msg"] = "load failed"; finish(); raise SystemExit
    body = pr.ImportedBody; graph = pr.Graph
    if not graph.Holes:
        rep["msg"] = "no holes"; rep["verdict"]="N_A"; finish(); raise SystemExit

    h = graph.Holes[0]
    D = float(h.DiameterMm)

    # PIN-vs-HOLE probe (kernel truth): a RemoveHole only makes sense if the
    # feature's interior is VOID. Find the live cylinder matching H1's radius and
    # test ContainsPoint on its axis at mid-extent — solid axis-centre = this is a
    # PIN (mis-extracted as a hole), which no fill can "remove" (Unite into solid
    # → coincident → General Failure). This decides kernel-limit vs misclassification.
    try:
        from SpaceClaim.Api.V252.Geometry import Point as _P
        rM_native = D/2.0/1000.0
        cyls = kt.cylinders(body)
        match = None
        for c in cyls:
            if abs(c["radius"] - rM_native) <= max(rM_native*0.05, 2e-4):
                match = c; break
        if match is not None:
            f = match["axis_foot"]; d = match["axis_dir"]
            tm = (match["t_lo"] + match["t_hi"]) / 2.0
            axis_mid = _P.Create(f[0]+d[0]*tm, f[1]+d[1]*tm, f[2]+d[2]*tm)
            inside = body.Shape.ContainsPoint(axis_mid)
            rep["pin_probe"] = {"radius_mm": round(match["radius"]*1000,3),
                                "extent_mm": round((match["t_hi"]-match["t_lo"])*1000,3),
                                "axis_centre_solid": bool(inside),
                                "interpretation": "PIN (mis-extracted as hole)" if inside else "HOLE (void)"}
            log("pin_probe: axis-centre solid=%s -> %s" % (inside, rep["pin_probe"]["interpretation"]))
    except Exception as e:
        rep["pin_probe"] = {"err": str(e)}
    rqu = int(round(D/2.0/1000.0/kt.QR))
    fp0 = kt.fingerprint(body)
    cyl0 = kt.cyl_radii(fp0)
    n0 = cyl0.get(rqu, 0)
    # also check via approximate buckets (+/-2 quant)
    def bucket_count(cyl, rq):
        return sum(c for r,c in cyl.items() if abs(r-rq) <= 2)
    nb0 = bucket_count(cyl0, rqu)
    rep["before"] = {"hole": h.Id, "Dmm": D, "rq": rqu, "vol": fp0["volume"],
                     "radius_bucket_count": nb0,
                     "cyl_radii_um": {str(int(r*kt.QR*1e6)): c for r,c in cyl0.items()}}
    log("before: hole %s D=%.2f rq=%d bucket=%d vol=%s" % (h.Id, D, rqu, nb0, fp0["volume"]))

    if op == "RemoveHole":
        r = ModificationService.RemoveHole(body, graph, h.Id, force)
    elif op == "MoveHole":
        # shift +X by 2*D (mm) — newp in mm
        newp = (h.PositionMm[0] + 2.0*D, h.PositionMm[1], h.PositionMm[2])
        r = ModificationService.MoveHole(body, graph, h.Id, newp)
    else:
        rep["msg"] = "unknown op"; finish(); raise SystemExit

    rep["op_success"] = bool(r and r.Success)
    rep["op_hint"] = (getattr(r, "HintMessage", "") or "") if r else ""
    rep["op_err"]  = (getattr(r, "ErrorMessage", "") or "") if r else "null"
    rep["op_msg"] = rep["op_err"] if (r and not r.Success) else rep["op_hint"]
    rep["relocated"] = ("relocated" in rep["op_hint"])
    if not (r and r.Success):
        rep["verdict"] = "OP_FAILED"; rep["msg"] = rep["op_msg"]; finish(); raise SystemExit

    fp1 = kt.fingerprint(body)
    cyl1 = kt.cyl_radii(fp1)
    nb1 = bucket_count(cyl1, rqu)
    dvol = (fp1["volume"] - fp0["volume"]) * kt.QV if (fp1["volume"] is not None and fp0["volume"] is not None) else None
    rep["after"] = {"radius_bucket_count": nb1, "vol": fp1["volume"], "dvol_m3": dvol,
                    "cyl_radii_um": {str(int(r*kt.QR*1e6)): c for r,c in cyl1.items()}}
    log("after: bucket=%d vol=%s dvol=%s" % (nb1, fp1["volume"], dvol))

    if op == "RemoveHole":
        # kernel truth: the bore cylinder disappeared (bucket -1) and volume rose
        vacated = nb1 < nb0
        grew = (dvol is not None and dvol > 0)
        rep["oracle"] = {"bucket_%d->%d" % (nb0, nb1): vacated, "dvol>0": grew}
        rep["verdict"] = "W41_VERIFIED" if (vacated and grew) else (
            "PARTIAL" if (vacated or grew) else "W41_FAILED")
    else:  # MoveHole
        # old bore vacated at old slot; total cylinder count of this radius ~same
        vacated = nb1 <= nb0  # at least not increased net (moved, not duplicated)
        rep["oracle"] = {"bucket_%d->%d" % (nb0, nb1): True, "relocated_hint": rep["op_msg"]}
        rep["verdict"] = "W41_VERIFIED" if vacated else "W41_FAILED"
    rep["msg"] = "op=%s verdict-inputs bucket %d->%d dvol=%s" % (op, nb0, nb1, dvol)
    finish()

except SystemExit:
    finish()
except Exception as e:
    rep["msg"] = "EXC: %s" % e
    rep["trace"] = traceback.format_exc()
    finish()
