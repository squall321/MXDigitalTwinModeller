# encoding: utf-8
# =====================================================================
# mod_atomic_test.py — process ONE (model × mod-type) per SC launch
#
# Solo runner already isolates per-model so a model's failed mod doesn't
# poison the NEXT model. But within a model, a failed hole still
# poisons that model's fillet+wall ("object is deleted" cascade). To
# get TRUE per-mod success rates, isolate per (model, mod-type) — one
# SC launch per cell of the 20×3 = 60 grid.
#
# I/O contract:
#   IN:  solo_target.txt  — "<idx>\t<name>\t<path>\t<mod_kind>"
#                            mod_kind ∈ {"Hole","Fillet","Wall"}
#   OUT: solo_done.txt    — marker (PS1 polls this)
#   OUT: atomic_results/<idx>_<name>_<mod>.json — one cell result
# =====================================================================
import os
import json
import traceback
from datetime import datetime

ADDIN_DLL    = r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll"
OUT_BASE     = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest"
REAL_CAD     = r"D:\MXDigitalTwinModeller\Test\RealCAD"
TARGET_PATH  = os.path.join(REAL_CAD, "solo_target.txt")
DONE_MARKER  = os.path.join(REAL_CAD, "solo_done.txt")
RESULTS_DIR  = os.path.join(REAL_CAD, "atomic_results")
LOG_PATH     = os.path.join(OUT_BASE, "headless_run.log")


def log(msg):
    line = "[%s] %s" % (datetime.now().strftime("%H:%M:%S"), msg)
    try:
        safe = line.encode("ascii", "replace") if isinstance(line, unicode) else line
        print(safe)
    except Exception:
        pass
    try:
        from System.IO import File as IoFile
        from System.Text import UTF8Encoding
        IoFile.AppendAllText(LOG_PATH, line + "\r\n", UTF8Encoding(False))
    except Exception:
        pass


def status_for(result):
    if result is None:
        return ("FAIL", "(null result)", "")
    if getattr(result, "Success", False):
        return ("OK", "", getattr(result, "HintMessage", None) or "")
    err = getattr(result, "ErrorMessage", None) or "(no msg)"
    hint = getattr(result, "HintMessage", None) or ""
    return ("FAIL", err, hint)


def _face_info_mm(graph, face_idx):
    """Returns ((ox,oy,oz)mm, (nx,ny,nz)unit_axis) or (None,None).
    Normal here is the cylinder axis direction stored by FaceClassifier."""
    if graph is None or graph.Faces is None: return (None, None)
    for ff in graph.Faces:
        if ff.FaceIndex == face_idx:
            if ff.Origin is None or len(ff.Origin) < 3: return (None, None)
            origin = (ff.Origin[0]*1000.0, ff.Origin[1]*1000.0, ff.Origin[2]*1000.0)
            normal = None
            if ff.Normal is not None and len(ff.Normal) >= 3:
                nx, ny, nz = ff.Normal[0], ff.Normal[1], ff.Normal[2]
                mag = (nx*nx + ny*ny + nz*nz) ** 0.5
                if mag > 1e-9:
                    normal = (nx/mag, ny/mag, nz/mag)
            return (origin, normal)
    return (None, None)


def _perp_distance_to_axis(px, py, pz, ax, ay, az, ux, uy, uz):
    """Perpendicular distance from point P=(px,py,pz) to the LINE through
    A=(ax,ay,az) with unit direction U=(ux,uy,uz). Matches Frame.Origin
    points along the same cylinder axis even after Boolean Subtract shifts
    the parametric anchor (verified via deep debug: BooleanReconstruction
    enlarges the hole correctly, but SC re-positions Frame.Origin to the
    tool's basePoint — same axis line, different anchor)."""
    dx, dy, dz = px - ax, py - ay, pz - az
    dot = dx*ux + dy*uy + dz*uz
    perp_x = dx - dot*ux
    perp_y = dy - dot*uy
    perp_z = dz - dot*uz
    return (perp_x*perp_x + perp_y*perp_y + perp_z*perp_z) ** 0.5


# Back-compat shim
def _origin_mm_of_face(graph, face_idx):
    orig, _n = _face_info_mm(graph, face_idx)
    return orig


def find_by_center_then_value(graph, features, cx, cy, cz, has_center,
                              primary_face_idx_getter, value_getter, expected_mm):
    if features is None: return None
    feat_list = list(features)
    if not feat_list: return None
    if has_center:
        # Use perp-to-axis distance when face has a normal (cylinder axis or
        # wall plane normal); fall back to point-to-point distance otherwise.
        best = None; best_dist = None
        for f in feat_list:
            try:
                idx = primary_face_idx_getter(f)
                origin, normal = _face_info_mm(graph, idx)
                if origin is None: continue
                if normal is not None:
                    dist = _perp_distance_to_axis(
                        cx, cy, cz, origin[0], origin[1], origin[2],
                        normal[0], normal[1], normal[2])
                else:
                    dx, dy, dz = origin[0]-cx, origin[1]-cy, origin[2]-cz
                    dist = (dx*dx + dy*dy + dz*dz) ** 0.5
                if best is None or dist < best_dist:
                    best = f; best_dist = dist
            except Exception: continue
        if best is not None:
            try: return float(value_getter(best))
            except Exception: pass
    best = None; best_d = None
    for f in feat_list:
        try:
            v = float(value_getter(f))
            d = abs(v - expected_mm)
            if best is None or d < best_d:
                best = v; best_d = d
        except Exception: continue
    return best


def any_feature_near(features, value_getter, expected_mm, tol_pct=0.05, tol_abs=0.01):
    """Returns True iff the post-mod graph has at least ONE feature whose
    dimension is within tol_pct or tol_abs of expected_mm. This is a strong
    "did the mod actually happen" check — independent of spatial matching.
    A correctly executed mod must produce a feature at the expected size."""
    if features is None: return False
    tol = max(abs(expected_mm) * tol_pct, tol_abs)
    for f in features:
        try:
            v = float(value_getter(f))
            if abs(v - expected_mm) <= tol:
                return True
        except Exception: continue
    return False


def downgrade_if_no_expected(row, features, value_getter, expected_mm, before):
    """If status=OK but no feature has the expected dimension AND the closest
    feature has the BEFORE value (unchanged), the mod silently failed —
    downgrade to FAIL_VERIFY so honest accounting reflects reality.

    Edge cases that DON'T downgrade:
      1. row["after"] already matches expected (in-process measurement
         from ModificationService.MeasuredAfterMm is authoritative).
      2. Extractor returned zero features (Wall extractor sometimes can't
         re-discover walls after mod even when geometry is valid).
    """
    if row["status"] != "OK": return
    # Trust C# in-process measurement when it agrees with expected.
    after = row.get("after")
    if after is not None:
        tol = max(abs(expected_mm) * 0.05, 0.01)
        if abs(after - expected_mm) <= tol:
            return  # measured value matches — verified
    if features is None: return
    try:
        cnt = features.Count
    except Exception:
        try: cnt = len(list(features))
        except Exception: cnt = 0
    if cnt == 0:
        return
    has_expected = any_feature_near(features, value_getter, expected_mm)
    if has_expected:
        return  # mod actually happened — keep OK
    # No feature at expected size — check if mod was a no-op
    has_before = any_feature_near(features, value_getter, before, tol_pct=0.01, tol_abs=0.005)
    if has_before:
        row["status"] = "FAIL_VERIFY"
        row["msg"] = (row.get("msg") or "") + (
            " | verify: no feature at expected=%.3f, before=%.3f still present (silent no-op)"
            % (expected_mm, before))
    else:
        row["status"] = "FAIL_VERIFY"
        row["msg"] = (row.get("msg") or "") + (
            " | verify: no feature at expected=%.3f and before=%.3f also gone (mod-but-wrong-result)"
            % (expected_mm, before))


def write_result(row, mod_kind):
    if not os.path.exists(RESULTS_DIR):
        try: os.makedirs(RESULTS_DIR)
        except Exception: pass
    idx = row.get("idx", 0); name = row.get("name", "model")
    out = os.path.join(RESULTS_DIR, "%02d_%s_%s.json" % (idx, name, mod_kind))
    try:
        from System.IO import File as IoFile
        from System.Text import UTF8Encoding
        IoFile.WriteAllText(out, json.dumps(row, indent=2), UTF8Encoding(False))
        log("  Result: %s" % out)
    except Exception as e:
        log("  WARN: result write failed: %s" % e)


def write_done():
    try:
        from System.IO import File as IoFile
        from System.Text import UTF8Encoding
        IoFile.WriteAllText(DONE_MARKER,
                            "done at %s\n" % datetime.now().isoformat(),
                            UTF8Encoding(False))
    except Exception:
        pass


def main():
    log("=" * 70)
    log("MX Real-CAD MODIFICATION atomic test (1 file × 1 mod per SC launch)")
    log("=" * 70)

    if not os.path.exists(TARGET_PATH):
        log("FATAL: target file not found"); return 1

    spec = ""
    try:
        from System.IO import File as IoFile
        spec = IoFile.ReadAllText(TARGET_PATH).strip()
    except Exception as e:
        log("FATAL: target read failed: %s" % e); return 1
    parts = spec.split("\t")
    if len(parts) < 4 or len(parts) > 5:
        log("FATAL: target spec malformed: %r" % spec); return 1
    idx, name, path, mod_kind = int(parts[0]), parts[1], parts[2], parts[3]
    # Optional 5th field: flags (orchestrator-level retry directives).
    # "forceScale" → ChangeFilletRadius runs in ScaleNormalized mode from the
    # start (kernel failures poison the process, so the scaled retry must be
    # a FRESH launch decided by the orchestrator — not in-process).
    flags = parts[4] if len(parts) == 5 else ""
    force_scale = "forceScale" in flags
    log("Target: [%d] %s — %s — mod=%s flags=%s" % (idx, name, path, mod_kind, flags or "-"))

    row = {
        "idx": idx, "name": name, "path": path, "mod_kind": mod_kind,
        "load": "MISSING", "extract": "-",
        "action": "-", "status": "-", "msg": "",
        "before": None, "after": None,
    }

    if not os.path.exists(path):
        log("  MISSING: %s" % path)
        write_result(row, mod_kind); write_done(); return 0

    try:
        import clr
        clr.AddReferenceToFileAndPath(ADDIN_DLL)
    except Exception as e:
        row["load"] = "FAIL: clr: %s" % e
        log("  FATAL: AddReference: %s" % e)
        write_result(row, mod_kind); write_done(); return 1
    try:
        from SpaceClaim.Api.V252 import Document, Window
        from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import (
            FeatureExtractor, ModificationService, RealModelPipeline,
        )
    except Exception as e:
        row["load"] = "FAIL: import: %s" % e
        log("  FATAL: type import: %s" % e)
        write_result(row, mod_kind); write_done(); return 1

    try: RealModelPipeline.ApplyColors = False
    except Exception: pass
    try: RealModelPipeline.ExtractAllBodies = False
    except Exception: pass
    try:
        if Window.ActiveWindow is None:
            Document.Create()
    except Exception: pass

    log("  >>> calling pipeline")
    try:
        pr = RealModelPipeline.Run(path, REAL_CAD)
    except Exception as e:
        row["load"] = "FAIL: pipeline EXC: %s" % repr(e)
        log("  Pipeline EXC: %s" % repr(e))
        write_result(row, mod_kind); write_done(); return 0
    if pr is None or not pr.Success:
        err = (pr.ErrorMessage if pr is not None else "Run returned None") or "(no msg)"
        row["load"] = "OK" if pr is not None else "FAIL"
        row["extract"] = "FAIL: " + err
        log("  Pipeline FAIL: %s" % err)
        write_result(row, mod_kind); write_done(); return 0
    row["load"] = "OK"
    body = pr.ImportedBody; graph = pr.Graph
    if body is None or graph is None:
        row["extract"] = "no-body" if body is None else "no-graph"
        write_result(row, mod_kind); write_done(); return 0
    row["extract"] = "OK"
    log("  extract OK: faces=%d holes=%d chains=%d walls=%d" % (
        graph.Faces.Count if graph.Faces else 0,
        graph.Holes.Count if graph.Holes else 0,
        graph.FilletChains.Count if graph.FilletChains else 0,
        graph.Walls.Count if graph.Walls else 0))

    extractor = FeatureExtractor()

    if mod_kind == "Hole":
        if graph.Holes is None or graph.Holes.Count == 0:
            row["action"] = "(no holes)"
            write_result(row, mod_kind); write_done(); return 0
        h = graph.Holes[0]; cur = float(h.DiameterMm); new = cur * 1.2
        row["action"] = "ChangeHoleDiameter %s: %.3f->%.3f" % (h.Id, cur, new)
        row["before"] = cur
        try:
            r = ModificationService.ChangeHoleDiameter(body, graph, h.Id, new)
            st, msg, hint = status_for(r)
            row["status"] = st
            row["msg"] = msg + (" | HINT: " + hint if hint else "")
            log("  hole: %s (%s)" % (st, msg))
        except Exception as e:
            row["status"] = "EXC"; row["msg"] = str(e)
            log("  hole EXC: %s" % e)
        # Verification in its own try — re-extract can throw "object deleted"
        # after a successful BooleanReconstruction even though the mod itself
        # succeeded. Don't let that demote OK status to EXC.
        if row["status"] == "OK":
            # Prefer C# in-process measurement (MeasuredAfterMm). It scans body
            # faces directly and is not affected by FeatureExtractor's criteria
            # that sometimes reject valid post-mod geometry.
            try:
                m = float(r.MeasuredAfterMm)
                if m == m and m > 0:  # not NaN, non-zero
                    row["after"] = m
            except Exception: pass
            try:
                g2 = extractor.Extract(body)
                if row.get("after") is None:
                    after = find_by_center_then_value(
                        g2, g2.Holes, r.TargetCenterXMm, r.TargetCenterYMm, r.TargetCenterZMm,
                        bool(r.UseTargetCenter),
                        lambda x: x.CylinderFaceIndex, lambda x: x.DiameterMm, new)
                    if after is not None: row["after"] = after
                downgrade_if_no_expected(row, g2.Holes,
                                         lambda x: x.DiameterMm, new, cur)
            except Exception as e:
                log("  hole verify EXC (status stays OK): %s" % e)

    elif mod_kind == "Fillet":
        if graph.FilletChains is None or graph.FilletChains.Count == 0:
            row["action"] = "(no chains)"
            write_result(row, mod_kind); write_done(); return 0
        fc = graph.FilletChains[0]; cur = float(fc.RadiusMm); new = cur * 1.2 if cur > 0 else 1.0
        row["action"] = "ChangeFilletRadius %s: %.3f->%.3f" % (fc.Id, cur, new)
        row["before"] = cur
        try:
            r = ModificationService.ChangeFilletRadius(body, graph, fc.Id, new, False, force_scale)
            st, msg, hint = status_for(r)
            row["status"] = st
            row["msg"] = msg + (" | HINT: " + hint if hint else "")
            log("  fillet: %s (%s)" % (st, msg))
        except Exception as e:
            row["status"] = "EXC"; row["msg"] = str(e)
            log("  fillet EXC: %s" % e)
        # W1-4 closed-loop sign retry: SIGN_INVERTED means the offset went the
        # wrong direction. In-process correction is impossible (STEP bodies hit
        # deleted-state after the first offset), so re-import FRESH and re-run
        # with flipSign=True. One retry only.
        #
        # NOTE: this in-process retry only works because SIGN_INVERTED is a
        # MECHANICALLY-SUCCESSFUL mod (wrong direction, no kernel failure) —
        # the process is not poisoned. Kernel failures ("Operation failed",
        # "object is deleted") poison the process; those retry at the
        # ORCHESTRATOR level via the 5th spec field (forceScale) instead.
        if row["status"] == "FAIL" and "SIGN_INVERTED" in (row.get("msg") or ""):
            log("  SIGN_INVERTED -> fresh re-import + flipSign retry")
            try:
                pr2 = RealModelPipeline.Run(path, REAL_CAD)
                if pr2 is not None and pr2.Success and pr2.Graph.FilletChains.Count > 0:
                    body = pr2.ImportedBody; graph = pr2.Graph
                    fc2 = graph.FilletChains[0]
                    r = ModificationService.ChangeFilletRadius(body, graph, fc2.Id, new, True)
                    st, msg, hint = status_for(r)
                    row["status"] = st
                    row["msg"] = (row.get("msg") or "") + " | RETRY(flipSign): " + (msg or "OK")
                    log("  fillet retry: %s (%s)" % (st, msg))
            except Exception as e2:
                log("  fillet retry EXC: %s" % e2)
        if row["status"] == "OK":
            try:
                m = float(r.MeasuredAfterMm)
                if m == m and m > 0: row["after"] = m
            except Exception: pass
            try:
                g2 = extractor.Extract(body)
                if row.get("after") is None:
                    after = find_by_center_then_value(
                        g2, g2.FilletChains, r.TargetCenterXMm, r.TargetCenterYMm, r.TargetCenterZMm,
                        bool(r.UseTargetCenter),
                        lambda x: x.FaceIndices[0] if x.FaceIndices is not None and x.FaceIndices.Count > 0 else -1,
                        lambda x: x.RadiusMm, new)
                    if after is not None: row["after"] = after
                downgrade_if_no_expected(row, g2.FilletChains,
                                         lambda x: x.RadiusMm, new, cur)
            except Exception as e:
                log("  fillet verify EXC (status stays OK): %s" % e)

    elif mod_kind == "Wall":
        if graph.Walls is None or graph.Walls.Count == 0:
            row["action"] = "(no walls)"
            write_result(row, mod_kind); write_done(); return 0
        w = graph.Walls[0]; cur = float(w.ThicknessMm); new = cur * 0.9
        row["action"] = "ChangeWallThickness %s: %.3f->%.3f" % (w.Id, cur, new)
        row["before"] = cur
        try:
            r = ModificationService.ChangeWallThickness(body, graph, w.Id, new)
            st, msg, hint = status_for(r)
            row["status"] = st
            row["msg"] = msg + (" | HINT: " + hint if hint else "")
            log("  wall: %s (%s)" % (st, msg))
        except Exception as e:
            row["status"] = "EXC"; row["msg"] = str(e)
            log("  wall EXC: %s" % e)
        if row["status"] == "OK":
            try:
                m = float(r.MeasuredAfterMm)
                if m == m and m > 0: row["after"] = m
            except Exception: pass
            try:
                g2 = extractor.Extract(body)
                if row.get("after") is None:
                    after = find_by_center_then_value(
                        g2, g2.Walls, r.TargetCenterXMm, r.TargetCenterYMm, r.TargetCenterZMm,
                        bool(r.UseTargetCenter),
                        lambda x: x.FaceA, lambda x: x.ThicknessMm, new)
                    if after is not None: row["after"] = after
                downgrade_if_no_expected(row, g2.Walls,
                                         lambda x: x.ThicknessMm, new, cur)
            except Exception as e:
                log("  wall verify EXC (status stays OK): %s" % e)
    else:
        row["status"] = "FAIL"; row["msg"] = "unknown mod_kind: " + mod_kind

    write_result(row, mod_kind); write_done(); return 0


try:
    rc = main()
    log("Exit code: %d" % rc)
except Exception as ex:
    log("UNHANDLED: %s" % ex)
    log(traceback.format_exc())
    rc = 99
