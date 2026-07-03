# encoding: utf-8
# probe_deepening.py - Phase-5 gate probes for the 82->85 push, ONE session, three questions:
#   P1 (WI-3 GO/NO-GO): do 3 AddHoleOnFace radial bores on the 624ZZ OD verify kernel-truth?
#   P2 (WI-4 decision): does the two-part clearance validator (deterministic bore-overlap +
#       multi-sample ring probe) keep all 4 Mirror-V models' twins, refuse SampleModel1/624ZZ,
#       and give boxy a passing twin that ACTUALLY mirrors (live op + kernel verify)?
#   P3 (WI-6 diagnosis): what do the two ChangeBossHeight paths measure on 11752's pin?
# Probing rules: ContainsPoint NEVER on-surface (>=0.25mm off), never single-point at cylinder
# boundaries (multi-sample majority) - oracle-probe-decisive + sc-kernel-landmines.
import os
import math
import clr

ADDIN_DLL = r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll"
REAL_CAD = r"D:\MXDigitalTwinModeller\Test\RealCAD"
OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\probe_deepening_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\probe_deepening_mark.txt"

clr.AddReferenceToFileAndPath(ADDIN_DLL)
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252.Geometry import Matrix, Point
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import (
    ModificationService, RealModelPipeline)

ANSYS = r"d:\Program Files\ANSYS Inc\ANSYS Student\v252\SCDM\Library\SrModels"
MODELS = {
    "SampleModel1": ANSYS + r"\SampleModel1.scdoc",
    "samplemodel2": ANSYS + r"\samplemodel2.scdoc",
    "nist_ctc_01": REAL_CAD + r"\nist\NIST-PMI-STEP-Files\nist_ctc_01_asme1_ap242-e1.stp",
    "boxy_with_diamsize": REAL_CAD + r"\caxif\boxy_with_diamsize.stp",
    "11752": REAL_CAD + r"\pythonocc\11752.stp",
    "as1_pe_203": REAL_CAD + r"\pythonocc\as1_pe_203.stp",
    "624ZZ_bearing": REAL_CAD + r"\freecad\624ZZ_Ball_Bearing.stp",
    "F623ZZ_bearing": REAL_CAD + r"\freecad\F623ZZ_Ball_Bearing.stp",
}

log = []
def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass
def emit(s):
    log.append(str(s)); _mk(str(s))

def arr(x):
    return System.Array[System.Double](list(x))

def contains_mm(body, p):
    try:
        return body.Shape.ContainsPoint(Point.Create(p[0]/1000.0, p[1]/1000.0, p[2]/1000.0))
    except Exception:
        return None

def norm3(v):
    m = math.sqrt(v[0]*v[0]+v[1]*v[1]+v[2]*v[2])
    if m < 1e-12: return None
    return (v[0]/m, v[1]/m, v[2]/m)

def perp_to_axis(p, o, d):
    dx = (p[0]-o[0], p[1]-o[1], p[2]-o[2])
    t = dx[0]*d[0]+dx[1]*d[1]+dx[2]*d[2]
    pp = (dx[0]-t*d[0], dx[1]-t*d[1], dx[2]-t*d[2])
    return math.sqrt(pp[0]**2+pp[1]**2+pp[2]**2)

def ortho_basis(a):
    # OrthoVec replica: least-aligned cardinal (tie-break x<=y<=z), Gram-Schmidt, v = a x u
    cands = [(1.0,0.0,0.0),(0.0,1.0,0.0),(0.0,0.0,1.0)]
    u0 = min(cands, key=lambda e: abs(e[0]*a[0]+e[1]*a[1]+e[2]*a[2]))
    d = u0[0]*a[0]+u0[1]*a[1]+u0[2]*a[2]
    u = norm3((u0[0]-d*a[0], u0[1]-d*a[1], u0[2]-d*a[2]))
    v = (a[1]*u[2]-a[2]*u[1], a[2]*u[0]-a[0]*u[2], a[0]*u[1]-a[1]*u[0])
    return u, v

def live_cyls(body):
    out = []
    for df in body.Faces:
        try:
            g = df.Shape.Geometry
            if type(g).__name__ != "Cylinder": continue
            o = g.Frame.Origin; az = g.Frame.DirZ.UnitVector
            bb = df.Shape.GetBoundingBox(Matrix.Identity)
            om = (o.X*1000.0, o.Y*1000.0, o.Z*1000.0)
            au = (az.X, az.Y, az.Z)
            # face span along axis from its bbox corners
            ts = []
            for cx in (bb.MinCorner.X, bb.MaxCorner.X):
                for cy in (bb.MinCorner.Y, bb.MaxCorner.Y):
                    for cz in (bb.MinCorner.Z, bb.MaxCorner.Z):
                        ts.append(((cx*1000-om[0])*au[0] + (cy*1000-om[1])*au[1] + (cz*1000-om[2])*au[2]))
            out.append({"r": float(g.Radius)*1000.0, "o": om, "a": au,
                        "t0": min(ts), "t1": max(ts)})
        except Exception: continue
    return out

def find_live_cyl_near(body, pos, d, pos_tol):
    rT = d/2.0; best = None; bd = None
    for c in live_cyls(body):
        if abs(c["r"] - rT) > max(rT*0.05, 0.05): continue
        dd = perp_to_axis(pos, c["o"], c["a"])
        if best is None or dd < bd: best = c["r"]; bd = dd
    return (best, bd) if (best is not None and bd <= pos_tol) else (None, None)

def count_live_cyls(body, d):
    rT = d/2.0; n = 0
    for c in live_cyls(body):
        if abs(c["r"] - rT) <= max(rT*0.05, 0.05): n += 1
    return n

def load(name):
    pr = RealModelPipeline.Run(MODELS[name], REAL_CAD)
    if pr is None or not pr.Success or pr.ImportedBody is None:
        emit("%s LOAD FAIL: %s" % (name, getattr(pr, "ErrorMessage", "?")))
        return None, None
    return pr.ImportedBody, pr.Graph

def bbox_mm(body):
    bb = body.Shape.GetBoundingBox(Matrix.Identity)
    mn = (bb.MinCorner.X*1000, bb.MinCorner.Y*1000, bb.MinCorner.Z*1000)
    mx = (bb.MaxCorner.X*1000, bb.MaxCorner.Y*1000, bb.MaxCorner.Z*1000)
    return mn, mx

# =====================================================================
# P1 - OD-chord radial bores on 624ZZ
# =====================================================================
def probe_p1():
    emit("=== P1 OD-chord (624ZZ) ===")
    body, graph = load("624ZZ_bearing")
    if body is None: return False
    # outward cylinder scan: largest R whose outside is AIR and inside is SOLID (2/3 angles)
    best = None
    for c in sorted(live_cyls(body), key=lambda c: -c["r"]):
        a = norm3(c["a"])
        if a is None: continue
        u, v = ortho_basis(a)
        tmid = (c["t0"] + c["t1"]) / 2.0
        foot = (c["o"][0]+a[0]*tmid, c["o"][1]+a[1]*tmid, c["o"][2]+a[2]*tmid)
        ok = 0
        for th in (0.3, 2.4, 4.5):
            rd = (u[0]*math.cos(th)+v[0]*math.sin(th), u[1]*math.cos(th)+v[1]*math.sin(th),
                  u[2]*math.cos(th)+v[2]*math.sin(th))
            pO = (foot[0]+rd[0]*(c["r"]+0.25), foot[1]+rd[1]*(c["r"]+0.25), foot[2]+rd[2]*(c["r"]+0.25))
            pI = (foot[0]+rd[0]*(c["r"]-0.25), foot[1]+rd[1]*(c["r"]-0.25), foot[2]+rd[2]*(c["r"]-0.25))
            if contains_mm(body, pO) is False and contains_mm(body, pI) is True: ok += 1
        if ok >= 2:
            best = {"c": c, "a": a, "u": u, "v": v, "foot": foot, "band": c["t1"]-c["t0"]}
            break
    if best is None:
        emit("P1 NO outward cylinder found -> NO-GO"); return False
    c = best["c"]
    emit("P1 OD: R=%.3f band=%.3f axis=(%.2f,%.2f,%.2f)" % (c["r"], best["band"], best["a"][0], best["a"][1], best["a"][2]))
    d2 = max(min(best["band"]/3.0, 1.6), 0.8)
    depth = max(d2, 1.0)
    n0 = count_live_cyls(body, d2)
    found = 0
    for k in range(3):
        th = 2.0*math.pi*k/3.0
        rd = (best["u"][0]*math.cos(th)+best["v"][0]*math.sin(th),
              best["u"][1]*math.cos(th)+best["v"][1]*math.sin(th),
              best["u"][2]*math.cos(th)+best["v"][2]*math.sin(th))
        seed = (best["foot"][0]+rd[0]*c["r"], best["foot"][1]+rd[1]*c["r"], best["foot"][2]+rd[2]*c["r"])
        r = ModificationService.AddHoleOnFace(body, arr(seed), d2, depth)
        okc, dd = find_live_cyl_near(body, seed, d2, max(2.0, d2))
        emit("P1 seed%d op=%s bore=%s dd=%s" % (k, getattr(r, "Success", False), okc is not None, dd))
        if okc is not None: found += 1
    n1 = count_live_cyls(body, d2)
    go = (found >= 2)
    emit("P1 d=%.2f found=%d/3 cyl %d->%d => %s" % (d2, found, n0, n1, "GO" if go else "NO-GO"))
    return go

# =====================================================================
# P2 - Mirror twin clearance validator
# =====================================================================
def twin_sweep(body, graph, mn, mx):
    """Replicate the harness twin-candidate sweep for holes[0]. Returns
    (h, d, old, haxu, nrm, candidates[(tw, off)]) or None."""
    if graph.Holes is None or graph.Holes.Count == 0: return None
    h = graph.Holes[0]
    old = (float(h.PositionMm[0]), float(h.PositionMm[1]), float(h.PositionMm[2]))
    d = float(h.DiameterMm)
    hax = (h.Axis[0], h.Axis[1], h.Axis[2]) if (h.Axis is not None and len(list(h.Axis)) >= 3) else (0.0, 0.0, 1.0)
    haxu = norm3(hax) or (0.0, 0.0, 1.0)
    cands = [(1.0,0.0,0.0),(0.0,1.0,0.0),(0.0,0.0,1.0)]
    nrm = min(cands, key=lambda e: abs(e[0]*haxu[0]+e[1]*haxu[1]+e[2]*haxu[2]))
    out = []
    for off in (max(d*2,4.0), max(d*3,6.0), max(d*1.5,3.0), max(d*4,8.0)):
        for sgn in (1.0, -1.0):
            tw = (old[0]+nrm[0]*sgn*off, old[1]+nrm[1]*sgn*off, old[2]+nrm[2]*sgn*off)
            if not (mn[0]-d <= tw[0] <= mx[0]+d and mn[1]-d <= tw[1] <= mx[1]+d and mn[2]-d <= tw[2] <= mx[2]+d):
                continue
            out.append((tw, sgn*off))
    return (h, d, old, haxu, nrm, out)

def axis_hits_solid(body, tw, haxu, d):
    for tt in (-0.4, -0.2, 0.0, 0.2, 0.4):
        sp = (tw[0]+haxu[0]*tt*max(d*4,8.0), tw[1]+haxu[1]*tt*max(d*4,8.0), tw[2]+haxu[2]*tt*max(d*4,8.0))
        if contains_mm(body, sp): return True
    return False

def bore_overlap(cyls, tw, r_twin, haxu, old, d):
    """Deterministic: any near-parallel live bore whose axis is closer than r+r+0.5 to the
    twin axis - EXCLUDING the original bore itself (its cylinder axis passes within d/4 of old)."""
    for c in cyls:
        a = norm3(c["a"])
        if a is None: continue
        if abs(a[0]*haxu[0]+a[1]*haxu[1]+a[2]*haxu[2]) < 0.9: continue
        if c["r"] > 25.0: continue  # huge ODs are not bores
        if perp_to_axis(old, c["o"], a) < max(1.0, d/4.0): continue  # the original itself
        if perp_to_axis(tw, c["o"], a) < r_twin + c["r"] + 0.5:
            return True
    return False

def ring_ok(body, tw, r_twin, haxu, mn, mx, d):
    """Multi-sample material probe: 5 stations along the twin axis inside bbox; per station
    axis point SOLID + 8-ring at r+0.5 SOLID; >=4/5 stations must pass."""
    u, v = ortho_basis(haxu)
    span = max(d*4, 8.0)
    okst = 0
    for tt in (-0.4, -0.2, 0.0, 0.2, 0.4):
        st = (tw[0]+haxu[0]*tt*span, tw[1]+haxu[1]*tt*span, tw[2]+haxu[2]*tt*span)
        if not (mn[0] <= st[0] <= mx[0] and mn[1] <= st[1] <= mx[1] and mn[2] <= st[2] <= mx[2]):
            continue
        if contains_mm(body, st) is not True: continue
        ring = 0
        for j in range(8):
            th = 2.0*math.pi*j/8.0
            rr = r_twin + 0.5
            p = (st[0]+(u[0]*math.cos(th)+v[0]*math.sin(th))*rr,
                 st[1]+(u[1]*math.cos(th)+v[1]*math.sin(th))*rr,
                 st[2]+(u[2]*math.cos(th)+v[2]*math.sin(th))*rr)
            if contains_mm(body, p) is True: ring += 1
        if ring >= 7: okst += 1
    return okst >= 4

def probe_p2():
    emit("=== P2 Mirror twin clearance ===")
    V_MODELS = ["samplemodel2", "nist_ctc_01", "as1_pe_203", "F623ZZ_bearing"]
    NA_MODELS = ["SampleModel1", "624ZZ_bearing"]
    verdicts = {}
    for name in V_MODELS + NA_MODELS + ["boxy_with_diamsize"]:
        body, graph = load(name)
        if body is None: verdicts[name] = "LOAD_FAIL"; continue
        mn, mx = bbox_mm(body)
        sw = twin_sweep(body, graph, mn, mx)
        if sw is None:
            verdicts[name] = "NO_HOLES"; emit("P2 %s: no holes" % name); continue
        h, d, old, haxu, nrm, cands = sw
        cyls = live_cyls(body)
        legacy = None; valid = None
        for (tw, off) in cands:
            hits = axis_hits_solid(body, tw, haxu, d)
            if hits and legacy is None: legacy = (tw, off)
            if hits and not bore_overlap(cyls, tw, d/2.0, haxu, old, d) and ring_ok(body, tw, d/2.0, haxu, mn, mx, d):
                if valid is None: valid = (tw, off)
        legacy_ok = (legacy is not None and valid is not None and
                     abs(legacy[1] - valid[1]) < 1e-9)
        verdicts[name] = {"legacy": legacy and legacy[1], "valid": valid and valid[1],
                          "legacy_is_valid": legacy_ok}
        emit("P2 %s: legacy_off=%s valid_off=%s legacy_passes_validator=%s" % (
            name, legacy and round(legacy[1],2), valid and round(valid[1],2), legacy_ok))
        # boxy: run the live op on the VALIDATOR-passing twin and kernel-verify
        if name == "boxy_with_diamsize" and valid is not None:
            tw = valid[0]
            planeO = ((old[0]+tw[0])/2.0, (old[1]+tw[1])/2.0, (old[2]+tw[2])/2.0)
            r = ModificationService.MirrorFeature(body, graph, h.Id, arr(nrm), arr(planeO))
            rM, dd = find_live_cyl_near(body, tw, d, max(2.0, d/2.0))
            rO, _ = find_live_cyl_near(body, old, d, max(1.0, d/4.0))
            emit("P2 boxy LIVE: op=%s twin_bore=%s (dd=%s) orig_kept=%s" % (
                getattr(r, "Success", False), rM is not None, dd, rO is not None))
            verdicts["boxy_live"] = (getattr(r, "Success", False) and rM is not None and rO is not None)
    # decision
    a = bool(verdicts.get("boxy_live"))
    b = all(isinstance(verdicts.get(m), dict) and verdicts[m]["legacy_is_valid"] for m in V_MODELS)
    c = all(isinstance(verdicts.get(m), dict) and verdicts[m]["valid"] is None for m in NA_MODELS)
    emit("P2 decision: a(boxy_live)=%s b(V_twins_pass)=%s c(NA_refused)=%s" % (a, b, c))
    return a, b, c

# =====================================================================
# P3 - 11752 ChangeBossHeight diagnosis
# =====================================================================
def probe_p3():
    emit("=== P3 11752 ChangeBossHeight ===")
    body, graph = load("11752")
    if body is None: return
    if graph.Bosses is None or graph.Bosses.Count == 0:
        emit("P3 no bosses"); return
    b = graph.Bosses[0]
    emit("P3 boss %s D=%.3f H=%.3f base=(%.2f,%.2f,%.2f) axis=(%s)" % (
        b.Id, float(b.DiameterMm), float(b.HeightMm),
        b.BasePositionMm[0], b.BasePositionMm[1], b.BasePositionMm[2],
        ",".join("%.2f" % x for x in list(b.Axis)) if b.Axis is not None else "?"))
    r1 = ModificationService.ChangeBossHeight(body, graph, b.Id, 5.0)
    emit("P3 default path: ok=%s measured=%s msg=%s hint=%s" % (
        getattr(r1, "Success", False), getattr(r1, "MeasuredAfterMm", float("nan")),
        (getattr(r1, "ErrorMessage", "") or "")[:120], (getattr(r1, "HintMessage", "") or "")[:160]))
    body2, graph2 = load("11752")
    if body2 is None: return
    b2 = graph2.Bosses[0]
    r2 = ModificationService.ChangeBossHeight(body2, graph2, b2.Id, 5.0, True)
    emit("P3 useBoolean path: ok=%s measured=%s msg=%s hint=%s" % (
        getattr(r2, "Success", False), getattr(r2, "MeasuredAfterMm", float("nan")),
        (getattr(r2, "ErrorMessage", "") or "")[:120], (getattr(r2, "HintMessage", "") or "")[:160]))

try:
    _mk("probe-start")
    p1 = probe_p1()
    p2 = probe_p2()
    probe_p3()
    emit("PROBE_DONE p1_go=%s p2=%s" % (p1, p2))
except System.Exception as e:
    emit("PROBE THREW(CLR): %s: %s" % (e.GetType().Name, e.Message))
except Exception as e:
    emit("PROBE THREW(PY): %s" % e)
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
