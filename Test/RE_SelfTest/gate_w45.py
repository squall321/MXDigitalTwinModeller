# encoding: utf-8
# =====================================================================
# gate_w45.py — validate OffsetFaces-REPLACEMENT for boss height via a
# coaxial-cylinder Boolean, judged by KERNEL TRUTH (kernel_truth.py).
#
# OffsetFaces no-ops / "Operation failed" on some boss caps (samplemodel2,
# 624ZZ). Hypothesis: extend the boss by UNITE-ing a coaxial cylinder
# (radius = boss R, height = +dH) on top of the cap — the added wall is a
# natural continuation of the boss wall, so the coincident-surface risk is
# far lower than a hole-fill. Shrink = SUBTRACT a coaxial cylinder at the cap.
#
# Steps on a model with a boss:
#   1) relocate the live boss cylinder (kernel truth: axis/radius/extent),
#   2) Unite a coaxial cylinder of +dH on the +axis cap (grow),
#   3) re-measure the boss extent → expect old + dH.
#
# Spec: gate_target.txt = "<path>\t<dHmm>"   Out: gate_result.json + gate_done.txt
# =====================================================================
import os, sys, json, math, traceback
from datetime import datetime
REAL_CAD = r"D:\MXDigitalTwinModeller\Test\RealCAD"
OUT_BASE = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest"
ADDIN_DLL = r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll"
TARGET = os.path.join(REAL_CAD, "gate_target.txt")
DONE   = os.path.join(REAL_CAD, "gate_done.txt")
RESULT = os.path.join(REAL_CAD, "gate_result.json")
DEFAULT_MODEL = r"d:\Program Files\ANSYS Inc\ANSYS Student\v252\SCDM\Library\SrModels\samplemodel2.scdoc"
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
    from SpaceClaim.Api.V252.Geometry import Point, Direction, Frame, Plane, CircleProfile, PointUV
    from SpaceClaim.Api.V252.Modeler import Body
    import kernel_truth as kt
    try: RealModelPipeline.ApplyColors = False
    except Exception: pass
    try:
        if Window.ActiveWindow is None: Document.Create()
    except Exception: pass
    path, dHmm = DEFAULT_MODEL, 10.0
    if os.path.exists(TARGET):
        from System.IO import File as F
        t = F.ReadAllText(TARGET).strip().split("\t")
        if t and t[0]: path = t[0]
        if len(t) >= 2 and t[1]: dHmm = float(t[1])
    rep["model"] = path; rep["dHmm"] = dHmm
    pr = RealModelPipeline.Run(path, REAL_CAD)
    if pr is None or not pr.Success or pr.ImportedBody is None or pr.Graph is None:
        rep["verdict"] = "LOAD_FAIL"; finish(); raise SystemExit
    body = pr.ImportedBody; graph = pr.Graph
    if not graph.Bosses:
        rep["verdict"] = "N_A (no boss)"; finish(); raise SystemExit
    b = graph.Bosses[0]
    rM = float(b.DiameterMm)/2.0/1000.0
    ax = b.Axis
    am = math.sqrt(ax[0]**2+ax[1]**2+ax[2]**2) or 1.0
    axu = (ax[0]/am, ax[1]/am, ax[2]/am)
    basePt = ((b.BasePositionMm[0])/1000.0, (b.BasePositionMm[1])/1000.0, (b.BasePositionMm[2])/1000.0) if b.BasePositionMm else (0.0,0.0,0.0)
    rec = kt.find_cylinder_near(body, basePt, axu, rM, pos_tol_m=max(rM*2,3e-3), rad_tol_m=max(rM*0.05,3e-4))
    if rec is None:
        rep["verdict"] = "RELOC_FAIL"; finish(); raise SystemExit
    foot = rec["axis_foot"]; d = rec["axis_dir"]; tlo = rec["t_lo"]; thi = rec["t_hi"]; R = rec["radius"]
    h0 = (thi - tlo) * 1000.0
    rep["before"] = {"R_mm": round(R*1000,3), "H_mm": round(h0,3), "boss": b.Id}
    # CAP = the free end (air just beyond along the axis); the other end is the base
    # (attached to the part). axis_dir is sign-canonical, so test both ends.
    def air_beyond(tend, sgn):
        from SpaceClaim.Api.V252.Geometry import Point as _P
        eps = 3e-4
        q = (foot[0]+d[0]*(tend+sgn*eps), foot[1]+d[1]*(tend+sgn*eps), foot[2]+d[2]*(tend+sgn*eps))
        try: return not body.Shape.ContainsPoint(_P.Create(q[0],q[1],q[2]))
        except Exception: return False
    hi_air = air_beyond(thi, +1.0)
    lo_air = air_beyond(tlo, -1.0)
    if hi_air and not lo_air:
        capT, capDir = thi, (d[0], d[1], d[2])
    elif lo_air and not hi_air:
        capT, capDir = tlo, (-d[0], -d[1], -d[2])
    else:
        capT, capDir = thi, (d[0], d[1], d[2])  # ambiguous → default +axis
    rep["cap"] = {"hi_air": bool(hi_air), "lo_air": bool(lo_air), "capT_mm": round(capT*1000,2)}
    cap = (foot[0]+capDir[0]*abs(capT) if False else foot[0]+d[0]*capT,
           foot[1]+d[1]*capT, foot[2]+d[2]*capT)
    dHm = dHmm/1000.0
    # build coaxial cylinder: base slightly INSIDE the cap (overlap so Unite merges),
    # extend OUTWARD (capDir) by dH+overlap.
    ov = 1e-4
    base = Point.Create(cap[0]-capDir[0]*ov, cap[1]-capDir[1]*ov, cap[2]-capDir[2]*ov)
    axD = Direction.Create(capDir[0], capDir[1], capDir[2])
    fdx = axD.ArbitraryPerpendicular
    fdy = Direction.Cross(axD, fdx)
    # 4-arg ctor (plane, radius, location PointUV, angle) — IronPython binds this
    # overload (the C# 2-arg uses defaults). location = plane origin (0,0).
    pl = Plane.Create(Frame.Create(base, fdx, fdy))
    prof = CircleProfile(pl, R, PointUV.Create(0.0, 0.0), 0.0)
    try:
        import System
        ext = Body.ExtrudeProfile(prof, dHm + ov)
        part = body.Parent
        addDb = DesignBody.Create(part, "_bossext", ext)
        body.Shape.Unite(System.Array[Body]([addDb.Shape]))
        try: addDb.Delete()
        except Exception: pass
        rep["unite"] = "OK"
    except Exception as e:
        rep["unite"] = "FAIL: %s" % e
        rep["verdict"] = "UNITE_FAILED"; finish(); raise SystemExit
    # re-measure
    rec2 = kt.find_cylinder_near(body, basePt, axu, rM, pos_tol_m=max(rM*2,3e-3), rad_tol_m=max(rM*0.05,3e-4))
    h1 = (rec2["t_hi"]-rec2["t_lo"])*1000.0 if rec2 else -1.0
    rep["after"] = {"H_mm": round(h1,3) if h1>0 else None}
    target = h0 + dHmm
    ok = (h1 > 0 and abs(h1 - target) <= max(target*0.03, 0.2))
    rep["target_H_mm"] = round(target,3)
    rep["verdict"] = "W45_VERIFIED" if ok else ("GREW_PARTIAL" if (h1>h0+0.1) else "NO_GROWTH")
    finish()
except SystemExit:
    finish()
except Exception as e:
    rep["verdict"] = "EXC"; rep["msg"] = str(e); rep["trace"] = traceback.format_exc(); finish()
