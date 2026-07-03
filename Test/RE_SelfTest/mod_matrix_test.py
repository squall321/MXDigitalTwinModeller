# encoding: utf-8
# =====================================================================
# mod_matrix_test.py — Phase 3 W3-1: 18-primitive matrix driver.
#
# One (model × primitive) cell per SC launch (same isolation policy as
# mod_atomic_test.py — kernel failures poison the process).
#
# Spec:  solo_target.txt = "<idx>\t<name>\t<path>\t<prim>[\t<flags>]"
#   prim ∈ { ChangeHoleDiameter, ChangeFilletRadius, ChangeWallThickness,
#            ChangeBossDiameter, ChangeBossHeight, AddBoss, AddHole,
#            MoveHole, MoveBoss, RemoveHole, RotateBoss, RotateHole,
#            MirrorFeature, AddHolePattern, AddSlit, AddPocket, AddRib,
#            AddChamfer }
#
# Every cell emits a row with verdict ∈ {VERIFIED, FAILED, INCONCLUSIVE, N_A}:
#   VERIFIED     — op returned Success AND the L1 oracle confirmed geometry
#   FAILED       — op failed OR oracle contradicts the request
#   INCONCLUSIVE — op OK but oracle couldn't measure (recorded, not trusted)
#   N_A          — model lacks the required feature
#
# L1 oracles (driver-level; per MOD_BREAKTHROUGH_PLAN W3-1):
#   Change*     — re-extract closest-match dimension == expected
#   Add*        — new feature at expected position/size + volume sign
#   Move/Rotate — same-size feature at NEW position, none at OLD
#   Remove      — feature absent at old position + volume increased
#   Mirror      — original kept + mirrored twin present
#   Slit/Pocket/Rib/Chamfer — face-count delta + volume delta sign
# =====================================================================
import os
import json
import math
import traceback
from datetime import datetime

ADDIN_DLL = r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll"
OUT_BASE  = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest"
REAL_CAD  = r"D:\MXDigitalTwinModeller\Test\RealCAD"
TARGET    = os.path.join(REAL_CAD, "solo_target.txt")
DONE      = os.path.join(REAL_CAD, "solo_done.txt")
RESULTS   = os.path.join(REAL_CAD, "matrix_results")
LOG_PATH  = os.path.join(OUT_BASE, "headless_run.log")


def log(msg):
    line = "[%s] %s" % (datetime.now().strftime("%H:%M:%S"), msg)
    try:
        print(line.encode("ascii", "replace") if isinstance(line, unicode) else line)
    except Exception: pass
    try:
        from System.IO import File as IoFile
        from System.Text import UTF8Encoding
        IoFile.AppendAllText(LOG_PATH, line + "\r\n", UTF8Encoding(False))
    except Exception: pass


def write_result(row, prim):
    if not os.path.exists(RESULTS):
        try: os.makedirs(RESULTS)
        except Exception: pass
    out = os.path.join(RESULTS, "%02d_%s_%s.json" % (row["idx"], row["name"], prim))
    try:
        from System.IO import File as IoFile
        from System.Text import UTF8Encoding
        IoFile.WriteAllText(out, json.dumps(row, indent=2), UTF8Encoding(False))
        log("  Result: %s" % out)
    except Exception as e:
        log("  WARN result write: %s" % e)


def write_done():
    try:
        from System.IO import File as IoFile
        from System.Text import UTF8Encoding
        IoFile.WriteAllText(DONE, "done\n", UTF8Encoding(False))
    except Exception: pass


def status_of(r):
    if r is None: return ("FAIL", "(null result)")
    if getattr(r, "Success", False): return ("OK", getattr(r, "HintMessage", None) or "")
    return ("FAIL", (getattr(r, "ErrorMessage", None) or "(no msg)"))


# ---------------- geometry helpers (mm domain) ----------------

def bbox_mm(body):
    from SpaceClaim.Api.V252.Geometry import Matrix
    bb = body.Shape.GetBoundingBox(Matrix.Identity)
    mn = (bb.MinCorner.X*1000, bb.MinCorner.Y*1000, bb.MinCorner.Z*1000)
    mx = (bb.MaxCorner.X*1000, bb.MaxCorner.Y*1000, bb.MaxCorner.Z*1000)
    sz = (mx[0]-mn[0], mx[1]-mn[1], mx[2]-mn[2])
    return mn, mx, sz


def volume_m3(body):
    try: return float(body.Shape.Volume)
    except Exception: return float("nan")


def face_count(body):
    n = 0
    for _ in body.Faces: n += 1
    return n


def dist3(a, b):
    return math.sqrt((a[0]-b[0])**2 + (a[1]-b[1])**2 + (a[2]-b[2])**2)


def perp_to_axis(p, ax_o, ax_d):
    """perp distance from point p to axis line (origin ax_o, unit dir ax_d), all mm."""
    dx = (p[0]-ax_o[0], p[1]-ax_o[1], p[2]-ax_o[2])
    dot = dx[0]*ax_d[0] + dx[1]*ax_d[1] + dx[2]*ax_d[2]
    pp = (dx[0]-dot*ax_d[0], dx[1]-dot*ax_d[1], dx[2]-dot*ax_d[2])
    return math.sqrt(pp[0]**2 + pp[1]**2 + pp[2]**2)


def find_hole(g, pos_mm, d_mm, pos_tol, d_tol_pct=0.05):
    """closest hole to pos_mm with D within tolerance; returns (hole, dist) or (None, None).
    Distance = perp distance from pos_mm to the hole's AXIS LINE when the hole
    has an axis (the extraction's PositionMm anchors at an arbitrary point along
    the bore — atomic-corpus lesson: point-to-point misses by the bore length).
    Falls back to point distance for axis-less holes."""
    best = None; best_d = None
    if g.Holes is None: return None, None
    for h in g.Holes:
        try:
            if h.PositionMm is None or len(h.PositionMm) < 3: continue
            if abs(float(h.DiameterMm) - d_mm) > max(d_mm*d_tol_pct, 0.05): continue
            hp = (h.PositionMm[0], h.PositionMm[1], h.PositionMm[2])
            if h.Axis is not None and len(h.Axis) >= 3:
                ax = (h.Axis[0], h.Axis[1], h.Axis[2])
                mag = math.sqrt(ax[0]**2 + ax[1]**2 + ax[2]**2)
                if mag > 1e-9:
                    axu = (ax[0]/mag, ax[1]/mag, ax[2]/mag)
                    dd = perp_to_axis(pos_mm, hp, axu)
                else:
                    dd = dist3(hp, pos_mm)
            else:
                dd = dist3(hp, pos_mm)
            if best is None or dd < best_d:
                best = h; best_d = dd
        except Exception: continue
    if best is not None and best_d <= pos_tol:
        return best, best_d
    return None, None


def count_holes_near(g, pos_mm, d_mm, pos_tol, d_tol_pct=0.05):
    """Number of holes within pos_tol of pos_mm (perp-to-axis) and matching D.
    Used to detect vacancy when a model has several identical holes — a single
    find_hole 'still matches' can be a DIFFERENT hole at a neighbouring slot."""
    n = 0
    if g is None or g.Holes is None: return 0
    for h in g.Holes:
        try:
            if h.PositionMm is None or len(h.PositionMm) < 3: continue
            if abs(float(h.DiameterMm) - d_mm) > max(d_mm*d_tol_pct, 0.05): continue
            hp = (h.PositionMm[0], h.PositionMm[1], h.PositionMm[2])
            if h.Axis is not None and len(h.Axis) >= 3:
                ax = (h.Axis[0], h.Axis[1], h.Axis[2])
                mag = math.sqrt(ax[0]**2 + ax[1]**2 + ax[2]**2)
                dd = perp_to_axis(pos_mm, hp, (ax[0]/mag, ax[1]/mag, ax[2]/mag)) if mag > 1e-9 else dist3(hp, pos_mm)
            else:
                dd = dist3(hp, pos_mm)
            if dd <= pos_tol: n += 1
        except Exception: continue
    return n


def contains_mm(body, pt_mm):
    """ContainsPoint at a mm point (converts to meters)."""
    from SpaceClaim.Api.V252.Geometry import Point as _P
    try:
        return body.Shape.ContainsPoint(_P.Create(pt_mm[0]/1000.0, pt_mm[1]/1000.0, pt_mm[2]/1000.0))
    except Exception:
        return None


def old_bore_filled(body, old_mm, axis_u, span=2.0, n=5):
    """Robust 'old bore was filled' test: sample ContainsPoint at n points spaced
    ±span mm along the hole axis through old_mm; True iff the MAJORITY are solid.
    A single contains_mm(old) gives false-IC when the .scdoc extraction anchor sits
    just off the true bore centre (probe_movehole_oldbore.py: samplemodel2 centre-only
    contains=False, yet 5/5 axis samples solid → the old bore IS filled). Axis-aligned
    sampling stays inside the filled column even when the anchor is radially off."""
    if axis_u is None:
        return contains_mm(body, old_mm) is True
    solid = 0; total = 0
    for i in range(n):
        t = -span + (2.0*span)*i/(n-1) if n > 1 else 0.0
        p = (old_mm[0]+axis_u[0]*t, old_mm[1]+axis_u[1]*t, old_mm[2]+axis_u[2]*t)
        c = contains_mm(body, p)
        if c is None:
            continue
        total += 1
        if c is True:
            solid += 1
    if total == 0:
        return None
    return solid*2 >= total


def find_live_cylinder_near(body, pos_mm, d_mm, axis_u=None, pos_tol=2.0, d_tol_pct=0.05):
    """Scan LIVE body faces for a cylinder of ~d_mm whose axis passes near pos_mm.
    Used when the FeatureExtractor fails to re-recognize a moved/rotated boss
    (run 7 lesson: op succeeds but graph.Bosses misses the re-added boss).
    Returns (radius_mm, perp_dist_mm) or (None, None)."""
    rTgt = d_mm/2.0
    best = None; best_d = None
    for df in body.Faces:
        try:
            g = df.Shape.Geometry
            if type(g).__name__ != "Cylinder": continue
            rmm = float(g.Radius)*1000.0
            if abs(rmm - rTgt) > max(rTgt*d_tol_pct, 0.05): continue
            o = g.Frame.Origin; az = g.Frame.DirZ.UnitVector
            om = (o.X*1000.0, o.Y*1000.0, o.Z*1000.0)
            au = (az.X, az.Y, az.Z)
            dd = perp_to_axis(pos_mm, om, au)
            if best is None or dd < best_d:
                best = rmm; best_d = dd
        except Exception: continue
    if best is not None and best_d <= pos_tol:
        return best, best_d
    return None, None


def count_live_cylinders(body, d_mm, d_tol_pct=0.05):
    """Number of LIVE cylinder faces of radius ~d_mm/2. Move conserves this count
    (fill old −1, add new +1); an unfilled old bore leaves +1. Robust to neighbour
    holes (they don't change the delta) and to off-bore extraction anchors (no
    point-position dependence) — unlike a ContainsPoint(old)/find-near(old) check."""
    rTgt = d_mm/2.0
    n = 0
    for df in body.Faces:
        try:
            g = df.Shape.Geometry
            if type(g).__name__ != "Cylinder": continue
            rmm = float(g.Radius)*1000.0
            if abs(rmm - rTgt) <= max(rTgt*d_tol_pct, 0.05): n += 1
        except Exception: continue
    return n


def _ortho_basis(a):
    """OrthoVec replica (matches ModificationService): least-aligned cardinal (tie x<=y<=z),
    Gram-Schmidt orthogonalize, v = a x u. Returns (u, v) spanning the plane perp to a."""
    cands = [(1.0,0.0,0.0),(0.0,1.0,0.0),(0.0,0.0,1.0)]
    u0 = min(cands, key=lambda e: abs(e[0]*a[0]+e[1]*a[1]+e[2]*a[2]))
    dd = u0[0]*a[0]+u0[1]*a[1]+u0[2]*a[2]
    ux = (u0[0]-dd*a[0], u0[1]-dd*a[1], u0[2]-dd*a[2])
    um = math.sqrt(ux[0]**2+ux[1]**2+ux[2]**2) or 1.0
    u = (ux[0]/um, ux[1]/um, ux[2]/um)
    v = (a[1]*u[2]-a[2]*u[1], a[2]*u[0]-a[0]*u[2], a[0]*u[1]-a[1]*u[0])
    return u, v


def find_outward_cylinder(body):
    """Largest LIVE cylinder whose OUTSIDE is air and INSIDE is solid (the OD of a
    bearing ring / a boss OD). Probes 3 angles at mid-span, ±0.25mm off-surface
    (never on-surface — sc-kernel-landmines), 2/3 majority. Returns a dict
    {r, o(mm), a(unit), foot(mm), band(mm)} or None. Used for the OD-chord pattern
    branch: the pattern seeds lie ON this cylinder and drill radially inward."""
    from SpaceClaim.Api.V252.Geometry import Matrix
    best = None
    cyls = []
    for df in body.Faces:
        try:
            g = df.Shape.Geometry
            if type(g).__name__ != "Cylinder": continue
            o = g.Frame.Origin; az = g.Frame.DirZ.UnitVector
            bb = df.Shape.GetBoundingBox(Matrix.Identity)
            om = (o.X*1000.0, o.Y*1000.0, o.Z*1000.0)
            au = (az.X, az.Y, az.Z)
            am = math.sqrt(au[0]**2+au[1]**2+au[2]**2) or 1.0
            au = (au[0]/am, au[1]/am, au[2]/am)
            ts = []
            for cx in (bb.MinCorner.X, bb.MaxCorner.X):
                for cy in (bb.MinCorner.Y, bb.MaxCorner.Y):
                    for cz in (bb.MinCorner.Z, bb.MaxCorner.Z):
                        ts.append((cx*1000-om[0])*au[0]+(cy*1000-om[1])*au[1]+(cz*1000-om[2])*au[2])
            cyls.append({"r": float(g.Radius)*1000.0, "o": om, "a": au,
                         "t0": min(ts), "t1": max(ts)})
        except Exception: continue
    for c in sorted(cyls, key=lambda c: -c["r"]):
        u, v = _ortho_basis(c["a"])
        tmid = (c["t0"]+c["t1"])/2.0
        foot = (c["o"][0]+c["a"][0]*tmid, c["o"][1]+c["a"][1]*tmid, c["o"][2]+c["a"][2]*tmid)
        ok = 0
        for th in (0.3, 2.4, 4.5):
            rd = (u[0]*math.cos(th)+v[0]*math.sin(th), u[1]*math.cos(th)+v[1]*math.sin(th),
                  u[2]*math.cos(th)+v[2]*math.sin(th))
            pO = (foot[0]+rd[0]*(c["r"]+0.25), foot[1]+rd[1]*(c["r"]+0.25), foot[2]+rd[2]*(c["r"]+0.25))
            pI = (foot[0]+rd[0]*(c["r"]-0.25), foot[1]+rd[1]*(c["r"]-0.25), foot[2]+rd[2]*(c["r"]-0.25))
            if contains_mm(body, pO) is False and contains_mm(body, pI) is True: ok += 1
        if ok >= 2:
            best = {"r": c["r"], "o": c["o"], "a": c["a"], "foot": foot,
                    "band": c["t1"]-c["t0"], "u": u, "v": v}
            break
    return best


def find_boss(g, d_mm=None, h_mm=None, d_tol=0.05):
    best = None; best_score = None
    if g.Bosses is None: return None
    for b in g.Bosses:
        try:
            score = 0.0
            if d_mm is not None: score += abs(float(b.DiameterMm) - d_mm)
            if h_mm is not None: score += abs(float(b.HeightMm) - h_mm)
            if best is None or score < best_score:
                best = b; best_score = score
        except Exception: continue
    return best


# ---------------- main ----------------
try:
    import clr
    clr.AddReferenceToFileAndPath(ADDIN_DLL)
    from SpaceClaim.Api.V252 import Document, Window
    from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import (
        FeatureExtractor, ModificationService, RealModelPipeline,
    )
    import System

    try: RealModelPipeline.ApplyColors = False
    except Exception: pass
    try: RealModelPipeline.ExtractAllBodies = False
    except Exception: pass
    try:
        if Window.ActiveWindow is None: Document.Create()
    except Exception: pass

    from System.IO import File as IoFile
    spec = IoFile.ReadAllText(TARGET).strip()
    parts = spec.split("\t")
    idx, name, path, prim = int(parts[0]), parts[1], parts[2], parts[3]
    flags = parts[4] if len(parts) >= 5 else ""
    force_scale = "forceScale" in flags
    use_boolean = "useBoolean" in flags  # W4-5 harness retry: OffsetFaces-replacement Boolean
    log("MATRIX cell [%d] %s × %s flags=%s" % (idx, name, prim, flags or "-"))

    row = {"idx": idx, "name": name, "prim": prim, "mod_kind": prim,
           "load": "MISSING", "extract": "-", "action": "-",
           "status": "-", "verdict": "N_A", "msg": "",
           "before": None, "after": None, "oracle": ""}

    if not os.path.exists(path):
        write_result(row, prim); write_done(); raise SystemExit

    pr = RealModelPipeline.Run(path, REAL_CAD)
    if pr is None or not pr.Success:
        row["load"] = "FAIL"; row["extract"] = "FAIL: " + ((pr.ErrorMessage if pr else "") or "")
        row["verdict"] = "FAILED"
        write_result(row, prim); write_done(); raise SystemExit
    row["load"] = "OK"
    body = pr.ImportedBody; graph = pr.Graph
    if body is None or graph is None:
        row["extract"] = "no-body"; row["verdict"] = "FAILED"
        write_result(row, prim); write_done(); raise SystemExit
    row["extract"] = "OK"
    extractor = FeatureExtractor()

    mn, mx, sz = bbox_mm(body)
    minDim = max(min(sz[0], sz[1], sz[2]), 0.5)

    # Add*-placement anchor (matrix run 5 fix): place on a REAL planar face's
    # interior point using LIVE geometry. FaceFeature.Origin for a Plane is the
    # frame origin (an arbitrary on-plane point, NOT the centroid) — using it
    # dropped tools into void on bracket/annular faces (dV=0 failure class).
    # Here we scan live body.Faces, pick the largest plane, and project its bbox
    # center onto the surface to get a guaranteed-interior anchor + true normal.
    def safe_placement():
        """Return (point_mm, outward_normal, face_mm) for a planar face that is
        VERIFIED placeable: the bbox-center projection lands on the face material
        AND a small probe confirms one side is solid, the other air. Iterates
        planar faces largest-first and returns the first that passes the
        ContainsPoint test. Absolute 0.2mm probe (face-relative eps put the probe
        in a different region on big faces — run-5 AddBoss STEP failures)."""
        from SpaceClaim.Api.V252.Geometry import Matrix as _M, Point as _P
        cands = []
        for df in body.Faces:
            try:
                g = df.Shape.Geometry
                if type(g).__name__ != "Plane": continue
                cands.append((float(df.Area), df, g))
            except Exception: continue
        cands.sort(key=lambda t: -t[0])
        EPS = 0.0002  # 0.2mm absolute
        for area, df, g in cands:
            try:
                bb = df.Shape.GetBoundingBox(_M.Identity)
                cen = ((bb.MinCorner.X+bb.MaxCorner.X)/2.0,
                       (bb.MinCorner.Y+bb.MaxCorner.Y)/2.0,
                       (bb.MinCorner.Z+bb.MaxCorner.Z)/2.0)
                nz = g.Frame.DirZ.UnitVector
                nrm0 = (nz.X, nz.Y, nz.Z)
                # in-plane basis from the plane frame
                ux = g.Frame.DirX.UnitVector; uy = g.Frame.DirY.UnitVector
                hx = (bb.MaxCorner.X-bb.MinCorner.X)/2.0
                hy = (bb.MaxCorner.Y-bb.MinCorner.Y)/2.0
                hz = (bb.MaxCorner.Z-bb.MinCorner.Z)/2.0
                halfdiag = math.sqrt(hx*hx+hy*hy+hz*hz)
                # candidate in-plane offsets: centre first, then ring-material probes
                # (bbox centre of an ANNULAR face lands in the central bore void; offset
                # samples toward the rim find solid material). 8 directions × 2 radii.
                offs = [(0.0, 0.0)]
                for frac in (0.35, 0.48):
                    for k in range(8):
                        th = math.pi*k/4.0
                        offs.append((math.cos(th)*halfdiag*frac, math.sin(th)*halfdiag*frac))
                for (ox, oy) in offs:
                    cp = _P.Create(cen[0]+ux.X*ox+uy.X*oy,
                                   cen[1]+ux.Y*ox+uy.Y*oy,
                                   cen[2]+ux.Z*ox+uy.Z*oy)
                    ev = g.ProjectPoint(cp)
                    if ev is None: continue
                    p = ev.Point
                    pPlus = _P.Create(p.X+nrm0[0]*EPS, p.Y+nrm0[1]*EPS, p.Z+nrm0[2]*EPS)
                    pMinus = _P.Create(p.X-nrm0[0]*EPS, p.Y-nrm0[1]*EPS, p.Z-nrm0[2]*EPS)
                    plusIn = body.Shape.ContainsPoint(pPlus)
                    minusIn = body.Shape.ContainsPoint(pMinus)
                    if plusIn == minusIn:
                        continue  # both air (off material) or both solid (internal)
                    s = 1.0 if (minusIn and not plusIn) else -1.0
                    dims = sorted([(bb.MaxCorner.X-bb.MinCorner.X)*1000.0,
                                   (bb.MaxCorner.Y-bb.MinCorner.Y)*1000.0,
                                   (bb.MaxCorner.Z-bb.MinCorner.Z)*1000.0])
                    facemm = dims[1]
                    return ((p.X*1000.0, p.Y*1000.0, p.Z*1000.0),
                            (nz.X*s, nz.Y*s, nz.Z*s), facemm)
            except Exception: continue
        return ((mn[0]+mx[0])/2.0, (mn[1]+mx[1])/2.0, mx[2]), (0.0, 0.0, 1.0), min(sz[0], sz[1])

    topCenter, topNormal, topFaceMm = safe_placement()
    # feature size: comfortably inside the face + absolute ceiling (large parts).
    # minDim/8 collapsed featMm to sub-mm on THIN parts (624ZZ bearing 5mm thick →
    # 0.6mm features the extractor can't recognise / that don't engage). Use minDim/3
    # and a recognition FLOOR (~1.2mm), but bound the floor by the local face so it
    # never overflows a small placement face.
    featMm = min(minDim/3.0, topFaceMm/4.0, 10.0)
    featMm = max(featMm, min(1.2, topFaceMm/6.0))
    v0 = volume_m3(body)
    f0 = face_count(body)
    nH0 = graph.Holes.Count if graph.Holes else 0
    nB0 = graph.Bosses.Count if graph.Bosses else 0

    def reextract():
        return extractor.Extract(body)

    def arr(x):
        return System.Array[System.Double](list(x))

    r = None
    verify = None  # callable(g2) -> (verdict, oracle_text, after_value)

    # ============ dispatch ============
    if prim == "ChangeHoleDiameter":
        if nH0 == 0:
            row["action"] = "(no holes)"; write_result(row, prim); write_done(); raise SystemExit
        h = graph.Holes[0]; cur = float(h.DiameterMm); new = cur * 1.2
        row["before"] = cur
        row["action"] = "D %.3f->%.3f" % (cur, new)
        r = ModificationService.ChangeHoleDiameter(body, graph, h.Id, new)
        def verify(g2):
            m = float(r.MeasuredAfterMm)
            if m == m and abs(m-new) <= max(new*0.05, 0.01):
                return "VERIFIED", "MeasuredAfterMm=%.3f" % m, m
            return "FAILED", "measured=%.3f vs %.3f" % (m, new), m

    elif prim == "ChangeFilletRadius":
        if not graph.FilletChains or graph.FilletChains.Count == 0:
            row["action"] = "(no chains)"; write_result(row, prim); write_done(); raise SystemExit
        fc = graph.FilletChains[0]; cur = float(fc.RadiusMm); new = cur*1.2 if cur > 0 else 1.0
        row["before"] = cur
        row["action"] = "R %.3f->%.3f" % (cur, new)
        r = ModificationService.ChangeFilletRadius(body, graph, fc.Id, new, False, force_scale)
        # SIGN_INVERTED closed-loop retry (ported from mod_atomic_test):
        # mechanically-successful wrong-direction mod doesn't poison the
        # process, so a fresh in-process re-import + flipSign is decisive.
        try:
            if (not r.Success) and "SIGN_INVERTED" in (r.ErrorMessage or ""):
                log("  SIGN_INVERTED -> fresh re-import + flipSign retry")
                pr2 = RealModelPipeline.Run(path, REAL_CAD)
                if pr2 is not None and pr2.Success and pr2.Graph.FilletChains.Count > 0:
                    body = pr2.ImportedBody; graph = pr2.Graph
                    fc2 = graph.FilletChains[0]
                    r = ModificationService.ChangeFilletRadius(body, graph, fc2.Id, new, True, force_scale)
        except Exception as eR:
            log("  retry EXC: %s" % eR)
        def verify(g2):
            m = float(r.MeasuredAfterMm)
            if m == m and abs(m-new) <= max(new*0.05, 0.01):
                return "VERIFIED", "MeasuredAfterMm=%.3f" % m, m
            return "FAILED", "measured=%.3f vs %.3f" % (m, new), m

    elif prim == "ChangeWallThickness":
        if not graph.Walls or graph.Walls.Count == 0:
            row["action"] = "(no walls)"; write_result(row, prim); write_done(); raise SystemExit
        w = graph.Walls[0]; cur = float(w.ThicknessMm); new = cur * 0.9
        row["before"] = cur
        row["action"] = "T %.3f->%.3f" % (cur, new)
        r = ModificationService.ChangeWallThickness(body, graph, w.Id, new)
        def verify(g2):
            m = float(r.MeasuredAfterMm)
            if m == m and abs(m-new) <= max(new*0.05, 0.01):
                return "VERIFIED", "MeasuredAfterMm=%.3f" % m, m
            return "FAILED", "measured=%.3f vs %.3f" % (m, new), m

    elif prim == "ChangeBossDiameter":
        if nB0 == 0:
            row["action"] = "(no bosses)"; write_result(row, prim); write_done(); raise SystemExit
        b = graph.Bosses[0]; cur = float(b.DiameterMm); new = cur * 1.2
        row["before"] = cur
        bdpos = (b.BasePositionMm[0], b.BasePositionMm[1], b.BasePositionMm[2]) if b.BasePositionMm else (0.0,0.0,0.0)
        bdax = b.Axis
        _bm = math.sqrt(bdax[0]**2+bdax[1]**2+bdax[2]**2) if (bdax is not None and len(bdax)>=3) else 0.0
        bdau = (bdax[0]/_bm, bdax[1]/_bm, bdax[2]/_bm) if _bm > 1e-9 else None
        row["action"] = "BossD %.3f->%.3f %s" % (cur, new, b.Id)
        r = ModificationService.ChangeBossDiameter(body, graph, b.Id, new, use_boolean)
        def verify(g2):
            # KERNEL-TRUTH (cold-review #4): the FE find_boss(g2,new) gave false-negatives
            # — samplemodel2 Boolean grew the boss but the extractor didn't re-recognise a
            # 732mm boss. Probe the LIVE body for a cylinder of radius ~new/2 on the boss
            # axis (+ confirm the old radius is gone).
            rNew, ddN = find_live_cylinder_near(body, bdpos, new, bdau, pos_tol=max(new, 3.0))
            rOld, _ = find_live_cylinder_near(body, bdpos, cur, bdau, pos_tol=max(cur/4.0, 1.0))
            if rNew is not None and abs(rNew - new/2.0) <= max(new/2.0*0.05, 0.05):
                return "VERIFIED", "live cyl D=%.3f at boss axis (kernel-truth)" % (rNew*2.0), rNew*2.0
            if rNew is not None:
                return "INCONCLUSIVE", "cyl found D=%.3f vs target %.3f" % (rNew*2.0, new), rNew*2.0
            return "FAILED", "no live cyl ~D=%.3f at boss axis (rOld=%s)" % (new, rOld), None

    elif prim == "ChangeBossHeight":
        if nB0 == 0:
            row["action"] = "(no bosses)"; write_result(row, prim); write_done(); raise SystemExit
        b = graph.Bosses[0]; cur = float(b.HeightMm); new = cur * 1.2 if cur > 0 else 1.0
        row["before"] = cur
        row["action"] = "BossH %.3f->%.3f %s" % (cur, new, b.Id)
        # Extraction reports HeightMm=0 for non-Z bosses → request an absolute
        # target height. C# estimates the real current height internally, offsets
        # the cap to reqH, and reports the post-mod measured height (MeasuredAfterMm),
        # which the oracle checks — independent of the FeatureExtractor.
        reqH = (cur*1.2) if cur > 0 else 5.0
        row["action"] = "BossH %.3f->%.3f %s" % (cur, reqH, b.Id)
        # C# ChangeBossHeight is Boolean-FIRST (W4-5): relocates the live boss cylinder
        # and Unites/Subtracts a coaxial cylinder (no OffsetFaces poison), falling back
        # to OffsetFaces only for degenerate bosses. No in-process retry needed.
        r = ModificationService.ChangeBossHeight(body, graph, b.Id, reqH, use_boolean)
        def verify(g2):
            m = float(r.MeasuredAfterMm)
            if m == m and m > 0 and abs(m - reqH) <= max(reqH*0.10, 0.1):
                return "VERIFIED", "MeasuredAfterMm=%.3f (target %.3f)" % (m, reqH), m
            if m == m and m > 0:
                return "INCONCLUSIVE", "measured %.3f vs target %.3f" % (m, reqH), m
            return "FAILED", "no height measurement", None

    elif prim == "AddBoss":
        d = featMm; ht = featMm
        pos = topCenter
        row["action"] = "AddBoss D=%.2f H=%.2f on face n=%s" % (d, ht, tuple(round(x,2) for x in topNormal))
        r = ModificationService.AddBoss(body, arr(pos), d, ht, arr(topNormal))
        def verify(g2):
            v1 = volume_m3(body)
            volOk = (v1 == v1 and v0 == v0 and v1 > v0)
            bb2 = find_boss(g2, d_mm=d)
            if bb2 is not None and abs(float(bb2.DiameterMm)-d) <= max(d*0.05, 0.05) and volOk:
                return "VERIFIED", "boss D=%.3f, dV>0" % float(bb2.DiameterMm), float(bb2.DiameterMm)
            if volOk and face_count(body) > f0:
                return "INCONCLUSIVE", "dV>0+faces+%d but extractor missed boss" % (face_count(body)-f0), None
            return "FAILED", "no new boss (volOk=%s)" % volOk, None

    elif prim == "AddHole":
        d = featMm
        pos = topCenter
        row["action"] = "AddHole D=%.2f through on face n=%s" % (d, tuple(round(x,2) for x in topNormal))
        r = ModificationService.AddHole(body, arr(pos), d, True, 0.0, arr(topNormal))
        def verify(g2):
            v1 = volume_m3(body)
            volOk = (v1 == v1 and v0 == v0 and v1 < v0)
            h2, dd = find_hole(g2, pos, d, pos_tol=max(2.0, d))
            if h2 is not None and volOk:
                return "VERIFIED", "hole D=%.3f at %.2fmm from request, dV<0" % (float(h2.DiameterMm), dd), float(h2.DiameterMm)
            if volOk:
                return "INCONCLUSIVE", "dV<0 but extractor missed hole", None
            return "FAILED", "no new hole (volOk=%s)" % volOk, None

    elif prim == "MoveHole":
        if nH0 == 0:
            row["action"] = "(no holes)"; write_result(row, prim); write_done(); raise SystemExit
        h = graph.Holes[0]
        if h.PositionMm is None:
            row["action"] = "(hole has no position)"; write_result(row, prim); write_done(); raise SystemExit
        old = (h.PositionMm[0], h.PositionMm[1], h.PositionMm[2])
        d = float(h.DiameterMm)
        shift = min(max(2.0*d, 3.0), max(sz[0]*0.25, 3.0), 20.0)
        # NOTE: a bbox-rim clamp on the shift was tried for as1-oc (Cluster G) and
        # MEASURED to neither fix as1-oc's non-manifold (cause is NOT shift size) nor
        # change any other cell — reverted as a no-op. The as1-oc MoveHole fail is a
        # genuine AddHole-subtract non-manifold limit on that geometry, not a shift bug.
        newp = (old[0]+shift, old[1], old[2])
        row["before"] = old[0]
        oldTol = max(min(1.0, d/4.0), 0.5)
        hax = h.Axis
        _hm = math.sqrt(hax[0]**2+hax[1]**2+hax[2]**2) if (hax is not None and len(hax) >= 3) else 0.0
        au = (hax[0]/_hm, hax[1]/_hm, hax[2]/_hm) if _hm > 1e-9 else None
        row["action"] = "MoveHole %s +x %.2f" % (h.Id, shift)
        nCylBefore = count_live_cylinders(body, d)
        r = ModificationService.MoveHole(body, graph, h.Id, arr(newp))
        def verify(g2):
            # MoveHole verification is genuinely hard — NO single live-body signal is clean:
            #   • ContainsPoint(old): false-IC on off-bore extraction anchors (.scdoc/complex)
            #   • count-conservation: false-IC when the fill leaves a filler-wall cylinder
            #     face (nist 4->6 even though the old bore IS filled — oldSolid=True)
            # So OR two SUFFICIENT signals: old centre solid (fill reached) OR cylinder
            # count conserved (clean move, no artifacts). A genuine op failure has no new
            # bore (rNew None → FAILED), so OR-ing cannot create a false positive.
            rNew, ddN = find_live_cylinder_near(body, newp, d, au, pos_tol=max(2.0, d/2.0))
            nCylAfter = count_live_cylinders(body, d)
            # oldSolid via AXIS-MULTI-SAMPLE (decisive kernel-truth, proven by
            # probe_movehole_oldbore.py): a single ContainsPoint(old) gives false-IC when the
            # .scdoc extraction anchor sits just off the true bore centre (samplemodel2:
            # before=[F×5] void → after=[T×5] solid, yet centre-only contains_mm=False). Sample
            # ±2mm along the hole axis; majority-solid ⇒ the old bore was genuinely filled.
            oldSolid = old_bore_filled(body, old, au)
            # 3-signal union — match the proven RotateHole oracle. RotateHole IS
            # `return MoveHole(...)` (ModificationService.cs:2326): byte-identical kernel
            # op, so the Move oracle MUST use the same vacated test. The missing signal
            # was (rO is None): no live cylinder survives at the OLD position ⇒ vacated.
            # OR'd & sufficient; a real failure still has rNew=None ⇒ FAILED (no false +).
            rO, _ = find_live_cylinder_near(body, old, d, au, pos_tol=max(1.0, d/4.0))
            vacated = (rO is None) or (oldSolid is True) or (nCylAfter == nCylBefore)
            if rNew is not None and vacated:
                return "VERIFIED", "bore at new (%.2fmm off); old filled (solid=%s, cyl %d->%d)" % (ddN, oldSolid, nCylBefore, nCylAfter), newp[0]
            if rNew is not None:
                return "INCONCLUSIVE", "bore at new but old not vacated (solid=%s, cyl %d->%d)" % (oldSolid, nCylBefore, nCylAfter), newp[0]
            return "FAILED", "no live bore at new position", None

    elif prim == "MoveBoss":
        if nB0 == 0:
            row["action"] = "(no bosses)"; write_result(row, prim); write_done(); raise SystemExit
        b = graph.Bosses[0]
        if b.BasePositionMm is None:
            row["action"] = "(boss has no position)"; write_result(row, prim); write_done(); raise SystemExit
        old = (b.BasePositionMm[0], b.BasePositionMm[1], b.BasePositionMm[2])
        d = float(b.DiameterMm)
        shift = min(max(2.0*d, 3.0), max(sz[0]*0.25, 3.0), 20.0)
        newp = (old[0]+shift, old[1], old[2])
        row["before"] = old[0]
        row["action"] = "MoveBoss %s +x %.2f" % (b.Id, shift)
        baxis = b.Axis
        _bm = math.sqrt(baxis[0]**2+baxis[1]**2+baxis[2]**2) if (baxis is not None and len(baxis)>=3) else 0.0
        bau = (baxis[0]/_bm, baxis[1]/_bm, baxis[2]/_bm) if _bm > 1e-9 else None
        nCylBefore = count_live_cylinders(body, d)
        r = ModificationService.MoveBoss(body, graph, b.Id, arr(newp))
        def verify(g2):
            # KERNEL-TRUTH (cold-review #4): the extractor drops a re-added boss → probe
            # the LIVE body. Boss cylinder of diameter d at the new axis + the old slot
            # vacated (oldSolid OR cyl-count conserved — same OR test as MoveHole, robust
            # to off-axis anchors and filler artefacts).
            rN, ddN = find_live_cylinder_near(body, newp, d, bau, pos_tol=max(2.0, d/2.0))
            nCylAfter = count_live_cylinders(body, d)
            rO, _ = find_live_cylinder_near(body, old, d, bau, pos_tol=max(1.0, d/4.0))
            # 3-signal union — match the proven RotateBoss oracle. RotateBoss IS
            # `return MoveBoss(...)` (ModificationService.cs:2285): identical kernel op.
            # The missing signal was (oldSolid is True): the old boss centre is now solid
            # material (the boss was relocated, old slot filled). OR'd & sufficient.
            oldSolid = contains_mm(body, old)
            vacated = (rO is None) or (oldSolid is True) or (nCylAfter == nCylBefore)
            if rN is not None and vacated:
                return "VERIFIED", "live boss cyl at new (%.2fmm), old vacated (cyl %d->%d)" % (ddN, nCylBefore, nCylAfter), newp[0]
            if rN is not None:
                return "INCONCLUSIVE", "boss cyl at new but old not vacated (cyl %d->%d)" % (nCylBefore, nCylAfter), newp[0]
            return "FAILED", "no live boss cyl at new position", None

    elif prim == "RemoveHole":
        if nH0 == 0:
            row["action"] = "(no holes)"; write_result(row, prim); write_done(); raise SystemExit
        h = graph.Holes[0]
        old = (h.PositionMm[0], h.PositionMm[1], h.PositionMm[2]) if h.PositionMm is not None else None
        d = float(h.DiameterMm)
        row["before"] = d
        row["action"] = "RemoveHole %s (D=%.2f)" % (h.Id, d)
        r = ModificationService.RemoveHole(body, graph, h.Id)
        def verify(g2):
            v1 = volume_m3(body)
            volOk = (v1 == v1 and v0 == v0 and v1 > v0)
            n2 = g2.Holes.Count if g2.Holes else 0
            gone = True
            if old is not None:
                hOld, _ = find_hole(g2, old, d, pos_tol=min(1.0, d/4.0))
                gone = hOld is None
            if gone and n2 < nH0 and volOk:
                return "VERIFIED", "count %d->%d, vacated, dV>0" % (nH0, n2), float(n2)
            if gone and volOk:
                return "INCONCLUSIVE", "vacated+dV>0 but count %d->%d" % (nH0, n2), float(n2)
            return "FAILED", "hole still present or volume unchanged", float(n2)

    elif prim in ("RotateBoss", "RotateHole"):
        isBoss = (prim == "RotateBoss")
        feats = graph.Bosses if isBoss else graph.Holes
        nf = feats.Count if feats else 0
        if nf == 0:
            row["action"] = "(no %s)" % ("bosses" if isBoss else "holes")
            write_result(row, prim); write_done(); raise SystemExit
        f = feats[0]
        posF = f.BasePositionMm if isBoss else f.PositionMm
        if posF is None:
            row["action"] = "(no position)"; write_result(row, prim); write_done(); raise SystemExit
        old = (posF[0], posF[1], posF[2])
        d = float(f.DiameterMm)
        center = ((mn[0]+mx[0])/2.0, (mn[1]+mx[1])/2.0, (mn[2]+mx[2])/2.0)
        ang = 15.0
        row["action"] = "%s %s 15deg about Z@center" % (prim, f.Id)
        fAxis = f.Axis
        _fm = math.sqrt(fAxis[0]**2+fAxis[1]**2+fAxis[2]**2) if (fAxis is not None and len(fAxis)>=3) else 0.0
        au = (fAxis[0]/_fm, fAxis[1]/_fm, fAxis[2]/_fm) if _fm > 1e-9 else None
        nCylBefore = count_live_cylinders(body, d)
        r = (ModificationService.RotateBoss if isBoss else ModificationService.RotateHole)(
            body, graph, f.Id, arr(center), arr((0.0, 0.0, 1.0)), ang)
        # expected rotated position
        ca, sa = math.cos(math.radians(ang)), math.sin(math.radians(ang))
        rel = (old[0]-center[0], old[1]-center[1])
        expp = (center[0] + rel[0]*ca - rel[1]*sa, center[1] + rel[0]*sa + rel[1]*ca, old[2])
        def verify(g2):
            # KERNEL-TRUTH (cold-review #4): probe the LIVE body — a cylinder of diameter d
            # at the rotated position, with the old slot vacated (oldSolid OR cyl-count
            # conserved). The extractor often drops a re-placed feature, so no g2 re-extract.
            rN, ddN = find_live_cylinder_near(body, expp, d, au, pos_tol=max(2.0, d/2.0))
            nCylAfter = count_live_cylinders(body, d)
            rO, _ = find_live_cylinder_near(body, old, d, au, pos_tol=max(1.0, d/4.0))
            oldSolid = contains_mm(body, old)
            vacated = (rO is None) or (oldSolid is True) or (nCylAfter == nCylBefore)
            if rN is not None and vacated:
                return "VERIFIED", "live cyl at rotated (%.2fmm), old vacated (cyl %d->%d)" % (ddN, nCylBefore, nCylAfter), expp[0]
            if rN is not None:
                return "INCONCLUSIVE", "cyl at rotated but old not vacated (cyl %d->%d)" % (nCylBefore, nCylAfter), expp[0]
            return "FAILED", "no live cyl at rotated position", None

    elif prim == "MirrorFeature":
        if nH0 == 0:
            row["action"] = "(no holes to mirror)"; write_result(row, prim); write_done(); raise SystemExit
        h = graph.Holes[0]
        if h.PositionMm is None:
            row["action"] = "(no position)"; write_result(row, prim); write_done(); raise SystemExit
        old = (h.PositionMm[0], h.PositionMm[1], h.PositionMm[2])
        d = float(h.DiameterMm)
        # Mirror-plane choice (matrix run 3 lesson): the mirror direction must be
        # PERPENDICULAR to the hole axis — mirroring an X-axis hole across an
        # X-normal plane shifts it along its own bore (out of the body). Pick the
        # cardinal direction most perpendicular to the axis, offset toward the
        # bbox center so the twin stays inside.
        hax = (h.Axis[0], h.Axis[1], h.Axis[2]) if (h.Axis is not None and len(h.Axis) >= 3) else (0.0, 0.0, 1.0)
        hmag = math.sqrt(hax[0]**2 + hax[1]**2 + hax[2]**2) or 1.0
        haxu = (hax[0]/hmag, hax[1]/hmag, hax[2]/hmag)
        cands = [(1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)]
        nrm = min(cands, key=lambda e: abs(e[0]*haxu[0] + e[1]*haxu[1] + e[2]*haxu[2]))
        # Twin must land where the body has MATERIAL along the hole axis (run 7
        # lesson: center-reflect sent the twin into void on asymmetric parts).
        # Sweep small twin offsets along nrm (both directions) and ContainsPoint-
        # probe a point near the hole-axis surface; pick the first offset whose
        # twin axis passes through solid. Mirror plane = midpoint(old, twin).
        def axis_hits_solid(twin):
            # sample 5 points along the hole axis through the twin, within bbox
            for tt in (-0.4, -0.2, 0.0, 0.2, 0.4):
                sp = (twin[0]+haxu[0]*tt*max(d*4,8.0),
                      twin[1]+haxu[1]*tt*max(d*4,8.0),
                      twin[2]+haxu[2]*tt*max(d*4,8.0))
                if contains_mm(body, sp): return True
            return False
        chosen = None
        for off in (max(d*2,4.0), max(d*3,6.0), max(d*1.5,3.0), max(d*4,8.0)):
            for sgn in (1.0, -1.0):
                tw = (old[0]+nrm[0]*sgn*off, old[1]+nrm[1]*sgn*off, old[2]+nrm[2]*sgn*off)
                # keep twin inside bbox
                if not (mn[0]-d <= tw[0] <= mx[0]+d and mn[1]-d <= tw[1] <= mx[1]+d and mn[2]-d <= tw[2] <= mx[2]+d):
                    continue
                if axis_hits_solid(tw):
                    chosen = (tw, sgn*off); break
            if chosen: break
        if chosen is None:
            row["action"] = "(no valid mirror twin position found)"
            row["verdict"] = "N_A"; write_result(row, prim); write_done(); raise SystemExit
        expp, usedOff = chosen
        planeO = ((old[0]+expp[0])/2.0, (old[1]+expp[1])/2.0, (old[2]+expp[2])/2.0)
        row["action"] = "Mirror %s n=%s twin@off %.2f" % (h.Id, nrm, usedOff)
        r = ModificationService.MirrorFeature(body, graph, h.Id, arr(nrm), arr(planeO))
        def verify(g2):
            # KERNEL-TRUTH (cold-review #4): live cylinder of diameter d at the mirrored
            # twin position AND the original kept — extractor-independent.
            rM2, dd = find_live_cylinder_near(body, expp, d, haxu, pos_tol=max(2.0, d/2.0))
            rO2, _ = find_live_cylinder_near(body, old, d, haxu, pos_tol=max(1.0, d/4.0))
            if rM2 is not None and rO2 is not None:
                return "VERIFIED", "live twin bore at mirror (%.2fmm off) + original kept" % dd, expp[0]
            if rM2 is not None:
                return "INCONCLUSIVE", "twin bore found but original missing?", expp[0]
            return "FAILED", "no live twin bore at mirror", None

    elif prim == "AddHolePattern":
        # All 3 holes must fit on the chosen face (run 8 lesson: a part-wide
        # spacing put 2/3 holes off-face → only 1 drilled). Pattern spreads IN
        # the face plane (perp to topNormal), compact spacing ≤ ¼ face. Smaller
        # holes than the single AddHole so 3 fit comfortably.
        d = max(min(featMm, topFaceMm/8.0), 0.5)
        spacing = max(min(topFaceMm/4.0, 15.0), d*2.0)
        nz = topNormal
        # in-plane spread direction = a cardinal axis perpendicular to the normal
        spread = min([(1.0,0.0,0.0),(0.0,1.0,0.0),(0.0,0.0,1.0)],
                     key=lambda e: abs(e[0]*nz[0]+e[1]*nz[1]+e[2]*nz[2]))
        # center the 3-hole row on topCenter: start one spacing back along spread
        start = (topCenter[0]-spread[0]*spacing, topCenter[1]-spread[1]*spacing, topCenter[2]-spread[2]*spacing)

        # WI-3 OD-chord branch (geometry-gated, model-name-free). The legacy linear pattern
        # drills DOWN the topNormal; a hole only becomes a real bore where the solid run under
        # the entry is at least the hole diameter d (a thin ring wall gives a tiny run -> the
        # cutter passes through the bore gap = the 624ZZ IC class). Count how many of the 3
        # legacy positions have a drillable solid run >= d by marching ContainsPoint along
        # -topNormal from just above the entry. An annular bearing ring scores <=1 (its planar
        # face sits over the central void / thin rims); every currently-VERIFIED pattern model
        # scores >=2 on a genuine slab face. Trigger OD-chord ONLY when drillable <=1 AND an
        # outward cylinder exists -> all 10 V cells keep the legacy linear path.
        def _drillable(p, need):
            step = max(need/8.0, 0.1)
            n = int((need*2.0)/step) + 1
            started = False; run0 = 0.0
            for j in range(n+1):
                t = j*step
                q = (p[0]-nz[0]*t, p[1]-nz[1]*t, p[2]-nz[2]*t)
                solid = contains_mm(body, q)
                if solid is True:
                    if not started: started = True; run0 = t
                elif solid is False and started:
                    return (t - run0) >= need
            return started and ((n*step - run0) >= need)
        # Require a solid run of 1.5*d: a bore needs clearance beyond its own diameter to be a
        # clean cylinder, not a nick into a ring groove / ball raceway.
        legacyDrill = sum(1 for k in range(3)
                          if _drillable((start[0]+spread[0]*k*spacing,
                                         start[1]+spread[1]*k*spacing,
                                         start[2]+spread[2]*k*spacing), d*1.5))
        # OD-chord fires when the legacy linear pattern is NOT fully drillable (legacyDrill<3)
        # AND an outward cylinder exists (find_outward_cylinder returns None on a genuine slab,
        # so SampleModel1/as1-oc/11752/nist/samplemodel2 with legacyDrill<3 but no OD ring stay
        # on the legacy path). Both bearings (624ZZ/F623ZZ) have the ring → OD branch.
        od = find_outward_cylinder(body) if legacyDrill < 3 else None
        # Extra guard: if the OD ring's radius is LARGE relative to the part (a slab's stray
        # fillet cylinder, not a bearing race), the legacy slab pattern is better — keep legacy
        # when a well-drillable slab already exists (legacyDrill >= 2 AND the band is not thin).
        if od is not None and legacyDrill >= 2 and od["band"] > topFaceMm * 2.0:
            od = None
        row["odbranch"] = "legacyDrill=%d od=%s" % (legacyDrill, "yes" if od else "no")
        log("  AddHolePattern gate: legacyDrill=%d od=%s" % (legacyDrill, "yes" if od else "no"))

        if od is not None:
            # OD-chord: 3 radial bores at theta = 2πk/3 on the outward cylinder. The circular
            # generator (spacing = R_od) emits seeds ON the OD; AddHoleOnFace snaps each to the
            # cylinder and drills along its own local outward normal (radially inward). Blind
            # depth = max(d2, 1.0) (a breach into the ball groove still leaves the entry bore).
            d2 = max(min(od["band"]/3.0, 1.6), 0.8)
            depth = max(d2, 1.0)
            axis_u = od["a"]; u = od["u"]; v = od["v"]; foot = od["foot"]; R = od["r"]
            row["action"] = "AddHolePattern 3x D=%.2f OD-chord R=%.2f" % (d2, R)
            nCylP0 = count_live_cylinders(body, d2)
            r = ModificationService.AddHolePattern(
                body, arr(foot), arr(axis_u), "circular", 3, R, d2, False,
                0, 0, 0.0, 0.0, 0.0, None, True, depth)
            def verify(g2):
                # KERNEL-TRUTH: on a curved OD, radial bores' entry cylinders are the honest
                # signal. A find-near test keyed at the OD SURFACE seed is unreliable (the entry
                # cylinder's axis passes through the seed but its Frame.Origin can sit deep, and
                # two opposed radial bores through a thin ring can MERGE into one through-cylinder
                # — 624ZZ showed cyl 0->2 for 3 drills). So the primary signal is the net
                # live-cylinder COUNT increase of radius ~d2/2 (>=2 = the pattern bit), with the
                # per-seed find as a corroborating count.
                found = 0
                for k in range(3):
                    th = 2.0*math.pi*k/3.0
                    rd = (u[0]*math.cos(th)+v[0]*math.sin(th),
                          u[1]*math.cos(th)+v[1]*math.sin(th),
                          u[2]*math.cos(th)+v[2]*math.sin(th))
                    seed = (foot[0]+rd[0]*R, foot[1]+rd[1]*R, foot[2]+rd[2]*R)
                    rk, _ = find_live_cylinder_near(body, seed, d2, rd, pos_tol=max(3.0, d2*2))
                    if rk is not None: found += 1
                nCylP1 = count_live_cylinders(body, d2)
                grew = (nCylP1 - nCylP0)
                if grew >= 2 or found >= 2:
                    return "VERIFIED", "%d/3 OD bores, cyl %d->%d" % (found, nCylP0, nCylP1), float(max(found, grew))
                if grew >= 1 or found >= 1:
                    return "INCONCLUSIVE", "%d/3 OD bores, cyl %d->%d" % (found, nCylP0, nCylP1), float(max(found, grew))
                return "FAILED", "0/3 OD-chord holes", 0.0
        else:
            row["action"] = "AddHolePattern 3x D=%.2f sp=%.2f dir=%s" % (d, spacing, spread)
            nzm = math.sqrt(nz[0]**2+nz[1]**2+nz[2]**2) or 1.0
            nau = (nz[0]/nzm, nz[1]/nzm, nz[2]/nzm)
            nCylP0 = count_live_cylinders(body, d)
            r = ModificationService.AddHolePattern(body, arr(start), arr(spread),
                                                   "linear", 3, spacing, d, True,
                                                   0, 0, 0.0, 0.0, 0.0, arr(topNormal))
            def verify(g2):
                # KERNEL-TRUTH (cold-review #4): count live cylinders of diameter d on the
                # LIVE body (drilled along topNormal) at the 3 predicted positions + net
                # live-cylinder count increase — extractor-independent.
                found = 0
                for k in range(3):
                    pk = (start[0]+spread[0]*k*spacing, start[1]+spread[1]*k*spacing, start[2]+spread[2]*k*spacing)
                    rk, _ = find_live_cylinder_near(body, pk, d, nau, pos_tol=max(2.0, d))
                    if rk is not None: found += 1
                nCylP1 = count_live_cylinders(body, d)
                grew = (nCylP1 - nCylP0)
                if (found == 3) or (found >= 2 and grew >= 2):
                    return "VERIFIED", "%d/3 live bores, cyl %d->%d" % (found, nCylP0, nCylP1), float(found)
                if found > 0 or grew > 0:
                    return "INCONCLUSIVE", "%d/3 live bores, cyl %d->%d" % (found, nCylP0, nCylP1), float(found)
                return "FAILED", "0/3 pattern holes", 0.0

    elif prim in ("AddSlit", "AddPocket", "AddRib", "AddChamfer"):
        if prim == "AddSlit":
            wMm = max(featMm*0.5, 0.5); lMm = max(featMm*2.0, 2.0); dp = max(featMm*0.5, 0.5)
            row["action"] = "AddSlit w=%.2f l=%.2f d=%.2f" % (wMm, lMm, dp)
            # in-plane long axis = a cardinal axis perpendicular to the face normal
            slitDir = min([(1.0,0.0,0.0),(0.0,1.0,0.0),(0.0,0.0,1.0)],
                          key=lambda e: abs(e[0]*topNormal[0]+e[1]*topNormal[1]+e[2]*topNormal[2]))
            r = ModificationService.AddSlit(body, arr(topCenter), wMm, lMm, dp, arr(slitDir), arr(topNormal))
            expectVolSign = -1
        elif prim == "AddPocket":
            wMm = featMm; lMm = featMm; dp = max(featMm*0.5, 0.5)
            row["action"] = "AddPocket %.2fx%.2f d=%.2f" % (wMm, lMm, dp)
            r = ModificationService.AddPocket(body, arr(topCenter), wMm, lMm, dp, arr(topNormal))
            expectVolSign = -1
        elif prim == "AddRib":
            wMm = max(featMm*0.5, 0.5); ht = max(featMm*0.5, 0.5)
            # rib centerline lies IN the face plane: pick a cardinal perp to normal
            ribDir = min([(1.0,0.0,0.0),(0.0,1.0,0.0),(0.0,0.0,1.0)],
                         key=lambda e: abs(e[0]*topNormal[0]+e[1]*topNormal[1]+e[2]*topNormal[2]))
            half = max(min(sz[0],sz[1],sz[2])*0.15, wMm)
            s = (topCenter[0]-ribDir[0]*half, topCenter[1]-ribDir[1]*half, topCenter[2]-ribDir[2]*half)
            e = (topCenter[0]+ribDir[0]*half, topCenter[1]+ribDir[1]*half, topCenter[2]+ribDir[2]*half)
            row["action"] = "AddRib w=%.2f h=%.2f" % (wMm, ht)
            r = ModificationService.AddRib(body, arr(s), arr(e), wMm, ht, arr(topNormal))
            expectVolSign = +1
        else:  # AddChamfer
            # 'all' instead of 'top' — the +Z 'top' edge filter finds nothing on
            # non-upright parts (matrix: "No edges matched filter 'top'").
            wMm = max(featMm*0.1, 0.2)
            row["action"] = "AddChamfer w=%.2f all" % wMm
            r = ModificationService.AddChamfer(body, wMm, "all")
            expectVolSign = -1
        def verify(g2):
            v1 = volume_m3(body)
            f1 = face_count(body)
            dv = v1 - v0
            volOk = (dv > 0) if expectVolSign > 0 else (dv < 0)
            facesOk = f1 > f0
            if volOk and facesOk:
                return "VERIFIED", "dV sign ok (%.2e), faces +%d" % (dv, f1-f0), dv
            if volOk or facesOk:
                return "INCONCLUSIVE", "partial: dV=%.2e faces %+d" % (dv, f1-f0), dv
            return "FAILED", "no volume/face evidence (dV=%.2e, faces %+d)" % (dv, f1-f0), dv

    else:
        row["action"] = "(unknown prim)"
        row["verdict"] = "FAILED"
        write_result(row, prim); write_done(); raise SystemExit

    # ============ status + oracle ============
    st, msg = status_of(r)
    row["status"] = st
    row["msg"] = msg
    log("  op: %s (%s)" % (st, (msg or "")[:120]))
    if st != "OK":
        # "NOT_APPLICABLE: <reason>" is the machine-parsable sentinel for HONEST structural
        # refusals (pin-not-a-hole, no safe bore center, no clearing mirror twin) -> N_A,
        # never FAILED. Substring (not startswith): parent ops wrap child messages
        # ("MoveHole.subtract failed: NOT_APPLICABLE: ..."). Precedent: SIGN_INVERTED sniff.
        row["verdict"] = "N_A" if ("NOT_APPLICABLE:" in (msg or "")) else "FAILED"
    else:
        try:
            g2 = reextract()
            verdict, oracle, after = verify(g2)
            row["verdict"] = verdict
            row["oracle"] = oracle
            if after is not None:
                try: row["after"] = float(after)
                except Exception: pass
            log("  oracle: %s (%s)" % (verdict, oracle))
        except Exception as eV:
            row["verdict"] = "INCONCLUSIVE"
            row["oracle"] = "verify EXC: %s" % str(eV)[:120]
            log("  oracle EXC: %s" % eV)

    write_result(row, prim)
    write_done()

except SystemExit:
    pass
except Exception as ex:
    log("UNHANDLED: %s" % ex)
    log(traceback.format_exc())
    try: write_done()
    except Exception: pass
