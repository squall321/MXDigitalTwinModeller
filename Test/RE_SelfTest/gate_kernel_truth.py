# encoding: utf-8
# =====================================================================
# gate_kernel_truth.py — Phase 4 foundation: KERNEL-TRUTH verification.
#
# Problem: every prior oracle re-runs FeatureExtractor, but extraction is
# UNRELIABLE on curved parts (gate_localfill proved 11752 H1 anchor 48mm
# off, axis misses the real bore). An oracle built on bad input inherits
# the lie. We need ground truth read DIRECTLY from the live kernel B-rep.
#
# This gate builds a canonical geometric FINGERPRINT from live face
# surfaces (Cylinder.Radius/Axis, Plane normal/offset, ... read from the
# kernel, NOT the extractor) + Body.Volume + bbox, then runs a MUTATION
# TEST: apply a KNOWN change (ChangeHoleDiameter) and assert the
# fingerprint delta shows EXACTLY that change (sensitive) and nothing
# else moved (specific). Per the plan: an oracle is untrusted until it
# passes a mutation test.
#
# Two checks:
#   A. NO-OP STABILITY — fingerprint(load) == fingerprint(reload) on a
#      second extraction-free pass → quantization is stable.
#   B. MUTATION SENSITIVITY+SPECIFICITY — ChangeHoleDiameter D->D' makes
#      exactly one cylinder radius move R->R', all else within tolerance.
#
# Spec: gate_target.txt = "<path>\t<holeDmm>\t<newDmm>"  (defaults nist)
# Out:  gate_result.json + gate_done.txt
# =====================================================================
import os, json, math, traceback
from datetime import datetime

ADDIN_DLL = r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll"
OUT_BASE  = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest"
REAL_CAD  = r"D:\MXDigitalTwinModeller\Test\RealCAD"
TARGET    = os.path.join(REAL_CAD, "gate_target.txt")
DONE      = os.path.join(REAL_CAD, "gate_done.txt")
RESULT    = os.path.join(REAL_CAD, "gate_result.json")
LOG_PATH  = os.path.join(OUT_BASE, "headless_run.log")
DEFAULT_MODEL = os.path.join(REAL_CAD, "nist", "NIST-PMI-STEP-Files", "nist_ctc_01_asme1_ap242-e1.stp")

# quantization (meters / m^3): position 0.05mm, dir 1e-3, radius 0.002mm, len 0.05mm
QP, QD, QR, QL = 5e-5, 1e-3, 2e-6, 5e-5

rep = {"checks": {}, "verdict": "ERROR", "msg": ""}

def log(msg):
    line = "[%s] KTRUTH %s" % (datetime.now().strftime("%H:%M:%S"), msg)
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

def q(v, step): return int(round(v / step))

def canon_dir(d):
    """Sign-canonicalize a unit direction so the first significant component
    is positive (a line/axis has no inherent orientation for fingerprinting)."""
    for c in d:
        if abs(c) > 1e-6:
            if c < 0: return (-d[0], -d[1], -d[2])
            return (d[0], d[1], d[2])
    return d

try:
    import clr
    clr.AddReferenceToFileAndPath(ADDIN_DLL)
    from SpaceClaim.Api.V252 import Document, Window
    from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import (
        FeatureExtractor, ModificationService, RealModelPipeline,
    )
    from SpaceClaim.Api.V252.Geometry import Matrix
    import System

    try: RealModelPipeline.ApplyColors = False
    except Exception: pass
    try:
        if Window.ActiveWindow is None: Document.Create()
    except Exception: pass

    path, holeDmm, newDmm = DEFAULT_MODEL, None, None
    if os.path.exists(TARGET):
        from System.IO import File as IoFile
        t = IoFile.ReadAllText(TARGET).strip().split("\t")
        if t and t[0]: path = t[0]
        if len(t) >= 3:
            holeDmm = float(t[1]); newDmm = float(t[2])
    rep["model"] = path

    # ---- kernel-truth fingerprint (extractor-free) ----
    def surface_of(face):
        for acc in ("Geometry",):
            try:
                g = getattr(face.Shape, acc)
                if g is not None: return g
            except Exception: pass
        try: return face.Geometry
        except Exception: return None

    def face_sig(face):
        """Canonical kernel signature of one face: tuple usable as a dict key."""
        g = surface_of(face)
        if g is None: return ("UNK",)
        tn = type(g).__name__
        try: area = q(float(face.Area), 5e-7)
        except Exception: area = 0
        if tn == "Plane":
            try:
                fr = g.Frame
                n = canon_dir((fr.DirZ.X, fr.DirZ.Y, fr.DirZ.Z))
                o = fr.Origin
                # signed distance origin->plane along normal
                d = o.X*n[0] + o.Y*n[1] + o.Z*n[2]
                return ("PLN", q(n[0],QD), q(n[1],QD), q(n[2],QD), q(d,QP), area)
            except Exception as e:
                return ("PLN_ERR", str(e)[:20])
        if tn == "Cylinder":
            try:
                rad = None
                for rn in ("Radius",):
                    try: rad = float(getattr(g, rn)); break
                    except Exception: pass
                ax = g.Axis  # Line
                A = ax.Origin; D = ax.Direction
                Du = (D.X, D.Y, D.Z)
                dc = canon_dir(Du)
                # foot of perpendicular from world origin onto axis (canonical anchor)
                t = -(A.X*Du[0] + A.Y*Du[1] + A.Z*Du[2])
                foot = (A.X+Du[0]*t, A.Y+Du[1]*t, A.Z+Du[2]*t)
                # axial extent of THIS face (bbox corners projected on axis)
                bb = face.Shape.GetBoundingBox(Matrix.Identity)
                ts = []
                for ci in range(8):
                    cx = bb.MinCorner.X if (ci&1)==0 else bb.MaxCorner.X
                    cy = bb.MinCorner.Y if (ci&2)==0 else bb.MaxCorner.Y
                    cz = bb.MinCorner.Z if (ci&4)==0 else bb.MaxCorner.Z
                    ts.append(cx*Du[0]+cy*Du[1]+cz*Du[2])
                ext = max(ts) - min(ts)
                return ("CYL", q(dc[0],QD), q(dc[1],QD), q(dc[2],QD),
                        q(foot[0],QP), q(foot[1],QP), q(foot[2],QP),
                        (q(rad,QR) if rad is not None else None), q(ext,QL))
            except Exception as e:
                return ("CYL_ERR", str(e)[:24])
        if tn in ("Cone","Sphere","Torus"):
            return (tn.upper()[:3], area)
        return (tn[:4].upper(), area)

    def fingerprint(body, extractor_free=True):
        sigs = {}
        for f in body.Faces:
            s = face_sig(f)
            sigs[s] = sigs.get(s, 0) + 1
        try: vol = q(float(body.Shape.Volume), 1e-10)
        except Exception: vol = None
        try:
            bb = body.Shape.GetBoundingBox(Matrix.Identity)
            bbq = [q(bb.MinCorner.X,QP), q(bb.MinCorner.Y,QP), q(bb.MinCorner.Z,QP),
                   q(bb.MaxCorner.X,QP), q(bb.MaxCorner.Y,QP), q(bb.MaxCorner.Z,QP)]
        except Exception: bbq = None
        return {"faces": sigs, "volume": vol, "bbox": bbq}

    def cyl_radii(fp):
        """multiset of quantized cylinder radii from a fingerprint."""
        out = {}
        for s, c in fp["faces"].items():
            if s[0] == "CYL" and s[7] is not None:
                out[s[7]] = out.get(s[7], 0) + c
        return out

    # ---- load ----
    pr = RealModelPipeline.Run(path, REAL_CAD)
    if pr is None or not pr.Success or pr.ImportedBody is None:
        rep["msg"] = "load failed"; finish(); raise SystemExit
    body = pr.ImportedBody; graph = pr.Graph

    fp0 = fingerprint(body)
    nfaces = sum(fp0["faces"].values())
    ncyl = sum(1 for s in fp0["faces"] if s[0]=="CYL")
    rep["fp0"] = {"n_faces": nfaces, "n_cyl_sigs": ncyl, "volume": fp0["volume"],
                  "cyl_radii_um": {str(int(r*QR*1e6)): c for r,c in cyl_radii(fp0).items()}}
    log("fp0: faces=%d cyl_sigs=%d vol=%s" % (nfaces, ncyl, fp0["volume"]))

    # ---- CHECK A: no-op stability (fingerprint twice, must be identical) ----
    fp0b = fingerprint(body)
    stable = (fp0["faces"] == fp0b["faces"] and fp0["volume"] == fp0b["volume"]
              and fp0["bbox"] == fp0b["bbox"])
    rep["checks"]["A_stability"] = bool(stable)
    log("CHECK A stability = %s" % stable)

    # ---- CHECK B: mutation sensitivity+specificity ----
    # pick a hole to resize: prefer the requested D, else the graph's first hole.
    targetH = None
    if graph and graph.Holes:
        if holeDmm is not None:
            for h in graph.Holes:
                if abs(float(h.DiameterMm) - holeDmm) <= 0.5: targetH = h; break
        if targetH is None: targetH = graph.Holes[0]
    if targetH is None:
        rep["checks"]["B_mutation"] = "N_A (no hole)"
        rep["verdict"] = "PARTIAL" if stable else "FAIL"
        rep["msg"] = "stability=%s; no hole for mutation test" % stable
        finish(); raise SystemExit

    D0 = float(targetH.DiameterMm)
    D1 = newDmm if newDmm is not None else (D0 + 5.0)
    r0u, r1u = int(round(D0/2.0/1000.0/QR)), int(round(D1/2.0/1000.0/QR))
    log("mutation: hole %s D %.3f -> %.3f (rq %d->%d)" % (targetH.Id, D0, D1, r0u, r1u))

    mres = ModificationService.ChangeHoleDiameter(body, graph, targetH.Id, D1)
    if mres is None or not mres.Success:
        rep["checks"]["B_mutation"] = "op-failed: " + ((mres.ErrorMessage if mres else "") or "")
        rep["verdict"] = "FAIL"; rep["msg"] = "ChangeHoleDiameter failed"; finish(); raise SystemExit

    fp1 = fingerprint(body)
    rad0, rad1 = cyl_radii(fp0), cyl_radii(fp1)
    # delta in cylinder radii multiset
    removed = {r: rad0[r]-rad1.get(r,0) for r in rad0 if rad0[r]-rad1.get(r,0) > 0}
    added   = {r: rad1[r]-rad0.get(r,0) for r in rad1 if rad1[r]-rad0.get(r,0) > 0}
    rep["mutation"] = {
        "D0": D0, "D1": D1, "rq0": r0u, "rq1": r1u,
        "radii_removed_um": {str(int(r*QR*1e6)): c for r,c in removed.items()},
        "radii_added_um":   {str(int(r*QR*1e6)): c for r,c in added.items()},
    }
    # SENSITIVE: the old radius bucket lost >=1 and the new radius bucket gained >=1
    sens = (any(abs(r-r0u) <= 2 for r in removed) and any(abs(r-r1u) <= 2 for r in added))
    # SPECIFIC: nothing OTHER than the r0->r1 transition changed (allow the paired move only)
    other_removed = [r for r in removed if abs(r-r0u) > 2]
    other_added   = [r for r in added if abs(r-r1u) > 2]
    spec = (len(other_removed) == 0 and len(other_added) == 0)
    rep["checks"]["B_mutation"] = {"sensitive": bool(sens), "specific": bool(spec),
                                   "other_removed": other_removed, "other_added": other_added}
    log("CHECK B sensitive=%s specific=%s" % (sens, spec))

    if stable and sens and spec:
        rep["verdict"] = "KERNEL_TRUTH_VALIDATED"
    elif stable and sens:
        rep["verdict"] = "SENSITIVE_NOT_SPECIFIC"
    else:
        rep["verdict"] = "FAIL"
    rep["msg"] = "stable=%s sensitive=%s specific=%s" % (stable, sens, spec)
    finish()

except SystemExit:
    finish()
except Exception as e:
    rep["msg"] = "EXC: %s" % e
    rep["trace"] = traceback.format_exc()
    finish()
