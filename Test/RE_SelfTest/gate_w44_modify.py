# encoding: utf-8
# gate_w44_modify.py — W4-4 end-to-end: body-TARGETED modification on an
# assembly's NON-first body. Picks the feature-richest shell (plate, 12 holes)
# of as1-oc, ChangeHoleDiameter on one of ITS holes using ITS body, verifies via
# kernel truth (MeasuredAfterMm). Proves the multi-body capability works, not just
# extraction. Also tries AddHole + RemoveHole on the targeted body.
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
rep = {"verdict": "ERROR", "steps": {}}
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
        RealModelPipeline, PartBodyTraversal, ModificationService)
    import kernel_truth as kt
    try: RealModelPipeline.ApplyColors = False
    except Exception: pass
    RealModelPipeline.ExtractAllBodies = True
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
    if pr is None or not pr.Success or pr.Graph is None or not pr.Graph.Shells:
        rep["verdict"] = "LOAD_FAIL"; finish(); raise SystemExit
    g = pr.Graph
    shells = list(g.Shells)
    doc = Window.ActiveWindow.Document
    allBodies = list(PartBodyTraversal.FindAllDesignBodies(doc))
    if len(allBodies) != len(shells):
        rep["verdict"] = "PARALLEL_MISMATCH"; rep["nb"]=len(allBodies); rep["ns"]=len(shells); finish(); raise SystemExit

    # pick the shell with the most HOLES (the plate)
    best_i = -1; best_h = -1
    for i, sg in enumerate(shells):
        nh = sg.Holes.Count if sg.Holes else 0
        if nh > best_h: best_h = nh; best_i = i
    tgtBody = allBodies[best_i]; tgtGraph = shells[best_i]
    rep["target"] = {"shell": best_i, "body": (tgtGraph.BodyName or "")[:20], "n_holes": best_h}

    # --- ChangeHoleDiameter on a hole of the TARGET (non-first) body ---
    h = tgtGraph.Holes[0]
    cur = float(h.DiameterMm); new = cur * 1.3
    fp0 = kt.fingerprint(tgtBody)
    r = ModificationService.ChangeHoleDiameter(tgtBody, tgtGraph, h.Id, new)
    ok = bool(r and r.Success)
    meas = None
    try: meas = float(r.MeasuredAfterMm)
    except Exception: pass
    # kernel-truth corroboration: a cylinder radius bucket moved cur/2 -> new/2
    fp1 = kt.fingerprint(tgtBody)
    rq0 = int(round(cur/2.0/1000.0/kt.QR)); rq1 = int(round(new/2.0/1000.0/kt.QR))
    c0 = kt.cyl_radii(fp0); c1 = kt.cyl_radii(fp1)
    moved = any(abs(rr-rq1)<=2 for rr in c1 if c1[rr]>c0.get(rr,0))
    chd_ok = bool(ok and meas==meas and meas and abs(meas-new) <= max(new*0.05,0.05) and moved)
    rep["steps"]["ChangeHoleDiameter"] = {
        "hole": h.Id, "cur": round(cur,3), "new": round(new,3),
        "success": ok, "measured": round(meas,3) if meas==meas and meas else None,
        "kt_radius_moved": bool(moved), "verified": chd_ok,
        "faceIdx": h.CylinderFaceIndex,
        "err": (r.ErrorMessage if (r and not r.Success) else "") or ""
    }
    # diagnostic: does GetFaceByIndex-equivalent find the hole cylinder on tgtBody?
    try:
        live = list(tgtBody.Faces)
        fi = h.CylinderFaceIndex
        ft = "oob"
        if 0 <= fi < len(live):
            gg = live[fi].Shape.Geometry
            ft = type(gg).__name__
            if ft == "Cylinder": ft = "Cylinder r=%.2fmm" % (float(gg.Radius)*1000.0)
        rep["steps"]["faceIdx_type"] = ft
    except Exception as e:
        rep["steps"]["faceIdx_type"] = "err:%s" % e

    # --- AddHole on the TARGET body (no existing-face dependency) at a safe point ---
    # use another hole's position offset to stay on plate material
    try:
        from SpaceClaim.Api.V252.Geometry import Matrix as _M
        bb = tgtBody.Shape.GetBoundingBox(_M.Identity)
        # plate is thin in one axis; drill through the thin axis at bbox centre region
        cx = (bb.MinCorner.X+bb.MaxCorner.X)/2.0*1000.0
        cy = (bb.MinCorner.Y+bb.MaxCorner.Y)/2.0*1000.0
        cz = (bb.MinCorner.Z+bb.MaxCorner.Z)/2.0*1000.0
        vol0 = float(tgtBody.Shape.Volume)
        import System
        rA = ModificationService.AddHole(tgtBody, System.Array[System.Double]([cx,cy,cz]), 4.0, True, 0.0)
        vol1 = float(tgtBody.Shape.Volume)
        rep["steps"]["AddHole"] = {"success": bool(rA and rA.Success), "dV_mm3": round((vol1-vol0)*1e9,4),
                                   "engaged": bool((vol1-vol0) < -1e-5),
                                   "err": (rA.ErrorMessage if (rA and not rA.Success) else "") or ""}
    except Exception as e:
        rep["steps"]["AddHole"] = {"exc": str(e)}

    # --- DIRECT TEST: pure coaxial Subtract to ENLARGE the plate's H1 ---
    # hypothesis: on a non-active body, Subtract works (AddHole proved it) but
    # Unite/ReplaceFaceGeometry no-op. So enlarging a hole = Subtract a bigger
    # coaxial cylinder (the annulus [oldR,newR]; [0,oldR] is already void).
    try:
        from SpaceClaim.Api.V252.Geometry import (Point as _P, Direction as _D, Frame as _F,
                                                  Plane as _PL, CircleProfile as _CP, PointUV as _UV)
        from SpaceClaim.Api.V252.Modeler import Body as _B
        from SpaceClaim.Api.V252 import DesignBody as _DB
        import System as _S
        h2 = tgtGraph.Holes[1] if tgtGraph.Holes.Count > 1 else tgtGraph.Holes[0]
        pm2 = h2.PositionMm; d2 = float(h2.DiameterMm)
        ax2 = h2.Axis; am = math.sqrt(ax2[0]**2+ax2[1]**2+ax2[2]**2) or 1.0
        au2 = (ax2[0]/am, ax2[1]/am, ax2[2]/am)
        baseP = (pm2[0]/1000.0, pm2[1]/1000.0, pm2[2]/1000.0)
        rec = kt.find_cylinder_near(tgtBody, baseP, au2, d2/2.0/1000.0, pos_tol_m=max(d2/1000.0,3e-3))
        if rec is not None:
            foot=rec["axis_foot"]; dd=rec["axis_dir"]; tlo=rec["t_lo"]; thi=rec["t_hi"]
            newR = d2/2.0/1000.0*1.3; ov=5e-4
            fp_a = kt.fingerprint(tgtBody)
            axD=_D.Create(dd[0],dd[1],dd[2]); fdx=axD.ArbitraryPerpendicular; fdy=_D.Cross(axD,fdx)
            bp=_P.Create(foot[0]+dd[0]*(tlo-ov),foot[1]+dd[1]*(tlo-ov),foot[2]+dd[2]*(tlo-ov))
            prof=_CP(_PL.Create(_F.Create(bp,fdx,fdy)),newR,_UV.Create(0.0,0.0),0.0)
            cyl=_B.ExtrudeProfile(prof,(thi-tlo)+2*ov)
            cdb=_DB.Create(tgtBody.Parent,"_enl",cyl)
            tgtBody.Shape.Subtract(_S.Array[_B]([cdb.Shape]))
            try: cdb.Delete()
            except Exception: pass
            fp_b=kt.fingerprint(tgtBody)
            c_a=kt.cyl_radii(fp_a); c_b=kt.cyl_radii(fp_b)
            rqN=int(round(newR/kt.QR))
            grew=any(abs(rr-rqN)<=3 for rr in c_b if c_b[rr]>c_a.get(rr,0))
            rep["steps"]["DirectSubtractEnlarge"]={"hole":h2.Id,"oldD":round(d2,2),
                "newR_mm":round(newR*1000,3),"radius_grew":bool(grew)}
    except Exception as e:
        rep["steps"]["DirectSubtractEnlarge"]={"exc":str(e)[:80]}

    rep["verdict"] = "W44_MODIFY_VERIFIED" if chd_ok else "W44_MODIFY_FAIL"
    finish()
except SystemExit:
    finish()
except Exception as e:
    rep["verdict"] = "EXC"; rep["msg"] = str(e); rep["trace"] = traceback.format_exc(); finish()
