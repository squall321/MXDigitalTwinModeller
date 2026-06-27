# encoding: utf-8
# =====================================================================
# mod_solo_test.py — process ONE corpus model per SC launch
#
# Why solo: ModificationService failures (OffsetFaces) leave SC's body
# in "deleted" state. The kernel then raises NullReferenceException on
# every subsequent Document.Open in that SC instance — so a single-launch
# batch can only get through the first 1-2 models before everything else
# silently fails to import. Process isolation (one SC launch per file)
# trades launch overhead (~10s/file) for clean state on every model.
#
# I/O contract:
#   IN:  D:\...\Test\RealCAD\solo_target.txt   — single line: file path
#   OUT: D:\...\Test\RealCAD\solo_done.txt     — marker (PS1 polls this)
#   OUT: D:\...\Test\RealCAD\solo_results\<idx>_<name>.json
#                                              — JSON row for this model
# =====================================================================
import os
import sys
import json
import traceback
from datetime import datetime

ADDIN_DLL    = r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll"
OUT_BASE     = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest"
REAL_CAD     = r"D:\MXDigitalTwinModeller\Test\RealCAD"
TARGET_PATH  = os.path.join(REAL_CAD, "solo_target.txt")
DONE_MARKER  = os.path.join(REAL_CAD, "solo_done.txt")
RESULTS_DIR  = os.path.join(REAL_CAD, "solo_results")
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
        return ("OK", "", "")
    err = getattr(result, "ErrorMessage", None) or "(no msg)"
    hint = getattr(result, "HintMessage", None) or ""
    return ("FAIL", err, hint)


def _origin_mm_of_face(graph, face_idx):
    """Look up graph.Faces[face_idx].Origin and return as (x_mm,y_mm,z_mm) or None."""
    if graph is None or graph.Faces is None: return None
    for ff in graph.Faces:
        if ff.FaceIndex == face_idx:
            if ff.Origin is None or len(ff.Origin) < 3: return None
            return (ff.Origin[0]*1000.0, ff.Origin[1]*1000.0, ff.Origin[2]*1000.0)
    return None


def find_by_center_then_value(graph, features, cx, cy, cz, has_center,
                              primary_face_idx_getter, value_getter, expected_mm):
    """Locate the modified feature in a re-extracted graph.
    Strategy: if mod result reported a target center, pick the feature whose
    primary face origin is closest to that center. Otherwise pick the feature
    whose value is closest to expected_mm (last-resort fallback)."""
    if features is None: return None
    feat_list = list(features)
    if not feat_list: return None
    if has_center:
        best = None; best_d2 = None
        for f in feat_list:
            try:
                idx = primary_face_idx_getter(f)
                orig = _origin_mm_of_face(graph, idx)
                if orig is None: continue
                d2 = ((orig[0]-cx)**2 + (orig[1]-cy)**2 + (orig[2]-cz)**2)
                if best is None or d2 < best_d2:
                    best = f; best_d2 = d2
            except Exception:
                continue
        if best is not None:
            try: return float(value_getter(best))
            except Exception: pass
    # value-closest fallback
    best = None; best_d = None
    for f in feat_list:
        try:
            v = float(value_getter(f))
            d = abs(v - expected_mm)
            if best is None or d < best_d:
                best = v; best_d = d
        except Exception: continue
    return best


def write_result(row):
    if not os.path.exists(RESULTS_DIR):
        try: os.makedirs(RESULTS_DIR)
        except Exception: pass
    idx = row.get("idx", 0)
    name = row.get("name", "model")
    out = os.path.join(RESULTS_DIR, "%02d_%s.json" % (idx, name))
    try:
        from System.IO import File as IoFile
        from System.Text import UTF8Encoding
        IoFile.WriteAllText(out, json.dumps(row, indent=2), UTF8Encoding(False))
        log("  Result written: %s" % out)
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
    log("MX Real-CAD MODIFICATION solo test")
    log("=" * 70)

    if not os.path.exists(TARGET_PATH):
        log("FATAL: target file not found: %s" % TARGET_PATH)
        return 1

    # Read target spec: "<idx>\t<name>\t<path>" on a single line.
    spec = ""
    try:
        from System.IO import File as IoFile
        spec = IoFile.ReadAllText(TARGET_PATH).strip()
    except Exception as e:
        log("FATAL: target read failed: %s" % e)
        return 1
    parts = spec.split("\t")
    if len(parts) != 3:
        log("FATAL: target spec malformed: %r" % spec)
        return 1
    idx, name, path = int(parts[0]), parts[1], parts[2]
    log("Target: [%d] %s — %s" % (idx, name, path))

    row = {
        "idx": idx, "name": name, "path": path,
        "load": "MISSING", "extract": "-",
        "hole":   {"action": "-", "status": "-", "msg": "", "before": None, "after": None},
        "fillet": {"action": "-", "status": "-", "msg": "", "before": None, "after": None},
        "wall":   {"action": "-", "status": "-", "msg": "", "before": None, "after": None},
    }

    if not os.path.exists(path):
        log("  MISSING: %s" % path)
        write_result(row); write_done(); return 0

    # Load addin + types
    try:
        import clr
        clr.AddReferenceToFileAndPath(ADDIN_DLL)
    except Exception as e:
        row["load"] = "FAIL: clr.AddReference: %s" % e
        log("  FATAL: AddReference: %s" % e); log(traceback.format_exc())
        write_result(row); write_done(); return 1
    try:
        from SpaceClaim.Api.V252 import Document, Window
        from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import (
            FeatureExtractor, ModificationService, RealModelPipeline,
        )
    except Exception as e:
        row["load"] = "FAIL: import: %s" % e
        log("  FATAL: type import: %s" % e); log(traceback.format_exc())
        write_result(row); write_done(); return 1

    try: RealModelPipeline.ApplyColors = False
    except Exception: pass
    try: RealModelPipeline.ExtractAllBodies = False
    except Exception: pass
    try:
        if Window.ActiveWindow is None:
            Document.Create()
    except Exception: pass

    # Pipeline import
    log("  >>> calling pipeline")
    try:
        pr = RealModelPipeline.Run(path, REAL_CAD)
    except Exception as e:
        row["load"] = "FAIL: pipeline EXC: %s" % repr(e)
        log("  Pipeline EXC: %s" % repr(e))
        write_result(row); write_done(); return 0
    if pr is None or not pr.Success:
        err = (pr.ErrorMessage if pr is not None else "Run returned None") or "(no msg)"
        row["load"] = "OK" if pr is not None else "FAIL"
        row["extract"] = "FAIL: " + err
        log("  Pipeline FAIL: %s" % err)
        write_result(row); write_done(); return 0
    row["load"] = "OK"
    body = pr.ImportedBody
    graph = pr.Graph
    if body is None or graph is None:
        row["extract"] = "no-body" if body is None else "no-graph"
        log("  Pipeline OK but body=%s graph=%s" % (body is not None, graph is not None))
        write_result(row); write_done(); return 0
    row["extract"] = "OK"
    log("  extract OK: faces=%d holes=%d chains=%d walls=%d bosses=%d" % (
        graph.Faces.Count if graph.Faces else 0,
        graph.Holes.Count if graph.Holes else 0,
        graph.FilletChains.Count if graph.FilletChains else 0,
        graph.Walls.Count if graph.Walls else 0,
        graph.Bosses.Count if graph.Bosses else 0,
    ))

    extractor = FeatureExtractor()

    # --- Hole change ---
    if graph.Holes is not None and graph.Holes.Count > 0:
        try:
            h = graph.Holes[0]
            cur = float(h.DiameterMm); new = cur * 1.2
            row["hole"]["action"] = "ChangeHoleDiameter %s: %.3f->%.3f" % (h.Id, cur, new)
            row["hole"]["before"] = cur
            r = ModificationService.ChangeHoleDiameter(body, graph, h.Id, new)
            st, msg, hint = status_for(r)
            row["hole"]["status"] = st
            row["hole"]["msg"] = msg + (" | HINT: " + hint if hint else (
                " | HINT: " + r.HintMessage if r is not None and getattr(r, "HintMessage", None) else ""))
            if st == "OK":
                try:
                    g2 = extractor.Extract(body)
                    cx = r.TargetCenterXMm; cy = r.TargetCenterYMm; cz = r.TargetCenterZMm
                    has_c = bool(r.UseTargetCenter)
                    after = find_by_center_then_value(
                        g2, g2.Holes, cx, cy, cz, has_c,
                        lambda x: x.CylinderFaceIndex,
                        lambda x: x.DiameterMm,
                        new)
                    if after is not None: row["hole"]["after"] = after
                    graph = g2
                except Exception as e:
                    log("  hole verify EXC: %s" % e)
            log("  hole: %s (%s)" % (st, msg))
        except Exception as e:
            row["hole"]["status"] = "EXC"; row["hole"]["msg"] = str(e)
            log("  hole EXC: %s" % e)
    else:
        row["hole"]["action"] = "(no holes)"

    # If hole failed, body may be corrupt — but since this is solo (process
    # isolation), continuing won't poison the next file. Still, attempting
    # fillet/wall on a deleted body will just produce an EXC row, which is
    # honest data. So don't skip — try them and capture EXC.

    # --- Fillet change ---
    if graph.FilletChains is not None and graph.FilletChains.Count > 0:
        try:
            fc = graph.FilletChains[0]
            cur = float(fc.RadiusMm); new = cur * 1.2 if cur > 0 else 1.0
            row["fillet"]["action"] = "ChangeFilletRadius %s: %.3f->%.3f" % (fc.Id, cur, new)
            row["fillet"]["before"] = cur
            r = ModificationService.ChangeFilletRadius(body, graph, fc.Id, new)
            st, msg, hint = status_for(r)
            row["fillet"]["status"] = st
            row["fillet"]["msg"] = msg + (
                " | HINT: " + r.HintMessage if r is not None and getattr(r, "HintMessage", None) else "")
            if st == "OK":
                try:
                    g2 = extractor.Extract(body)
                    cx = r.TargetCenterXMm; cy = r.TargetCenterYMm; cz = r.TargetCenterZMm
                    has_c = bool(r.UseTargetCenter)
                    after = find_by_center_then_value(
                        g2, g2.FilletChains, cx, cy, cz, has_c,
                        lambda x: x.FaceIndices[0] if x.FaceIndices is not None and x.FaceIndices.Count > 0 else -1,
                        lambda x: x.RadiusMm,
                        new)
                    if after is not None: row["fillet"]["after"] = after
                    graph = g2
                except Exception as e:
                    log("  fillet verify EXC: %s" % e)
            log("  fillet: %s (%s)" % (st, msg))
        except Exception as e:
            row["fillet"]["status"] = "EXC"; row["fillet"]["msg"] = str(e)
            log("  fillet EXC: %s" % e)
    else:
        row["fillet"]["action"] = "(no chains)"

    # --- Wall change ---
    if graph.Walls is not None and graph.Walls.Count > 0:
        try:
            w = graph.Walls[0]
            cur = float(w.ThicknessMm); new = cur * 0.9
            row["wall"]["action"] = "ChangeWallThickness %s: %.3f->%.3f" % (w.Id, cur, new)
            row["wall"]["before"] = cur
            r = ModificationService.ChangeWallThickness(body, graph, w.Id, new)
            st, msg, hint = status_for(r)
            row["wall"]["status"] = st
            row["wall"]["msg"] = msg + (
                " | HINT: " + r.HintMessage if r is not None and getattr(r, "HintMessage", None) else "")
            if st == "OK":
                try:
                    g2 = extractor.Extract(body)
                    cx = r.TargetCenterXMm; cy = r.TargetCenterYMm; cz = r.TargetCenterZMm
                    has_c = bool(r.UseTargetCenter)
                    after = find_by_center_then_value(
                        g2, g2.Walls, cx, cy, cz, has_c,
                        lambda x: x.FaceA,
                        lambda x: x.ThicknessMm,
                        new)
                    if after is not None: row["wall"]["after"] = after
                except Exception as e:
                    log("  wall verify EXC: %s" % e)
            log("  wall: %s (%s)" % (st, msg))
        except Exception as e:
            row["wall"]["status"] = "EXC"; row["wall"]["msg"] = str(e)
            log("  wall EXC: %s" % e)
    else:
        row["wall"]["action"] = "(no walls)"

    write_result(row); write_done()
    return 0


try:
    rc = main()
    log("Exit code: %d" % rc)
except Exception as ex:
    log("UNHANDLED: %s" % ex)
    log(traceback.format_exc())
    rc = 99
