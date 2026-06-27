# encoding: utf-8
# =====================================================================
# mod_corpus_test.py — real-CAD MODIFICATION batch test
#
# For each of the 20 RealCAD models:
#   1) Open via Document.Open
#   2) Extract FeatureGraph
#   3) If holes exist  → ChangeHoleDiameter(first hole, D *= 1.2)
#   4) If chains exist → ChangeFilletRadius(first chain, R *= 1.2)
#   5) If walls exist  → ChangeWallThickness(first wall, T *= 0.9)
#   6) Re-extract → confirm change persisted
#
# Discovers REAL-CAD modification gaps. Extraction was 20/20 PASS;
# modification gaps are mostly unknown.
# =====================================================================
import os
import sys
import traceback
from datetime import datetime

ADDIN_DLL    = r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll"
OUT_BASE     = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest"
REAL_CAD     = r"D:\MXDigitalTwinModeller\Test\RealCAD"
SUMMARY_PATH = os.path.join(REAL_CAD, "mod_corpus_summary.md")
DONE_MARKER  = os.path.join(REAL_CAD, "mod_corpus_done")
LOG_PATH     = os.path.join(OUT_BASE, "headless_run.log")

# Reuse same corpus list as real_cad_test.py
ANSYS = r"d:\Program Files\ANSYS Inc\ANSYS Student\v252\SCDM\Library\SrModels"
CORPUS = [
    ("SampleModel1",                   os.path.join(ANSYS, "SampleModel1.scdoc")),
    ("SampleModel4",                   os.path.join(ANSYS, "SampleModel4.scdoc")),
    ("samplemodel5",                   os.path.join(ANSYS, "samplemodel5.scdoc")),
    ("samplemodel2",                   os.path.join(ANSYS, "samplemodel2.scdoc")),
    ("nist_ctc_01",                    os.path.join(REAL_CAD, "nist", "NIST-PMI-STEP-Files", "nist_ctc_01_asme1_ap242-e1.stp")),
    ("nist_ftc_07",                    os.path.join(REAL_CAD, "nist", "NIST-PMI-STEP-Files", "nist_ftc_07_asme1_ap242-e2.stp")),
    ("nist_stc_06",                    os.path.join(REAL_CAD, "nist", "NIST-PMI-STEP-Files", "nist_stc_06_asme1_ap242-e3.stp")),
    ("as1-ac-214",                     os.path.join(REAL_CAD, "caxif", "as1-ac-214.stp")),
    ("as1-md-214",                     os.path.join(REAL_CAD, "caxif", "as1-md-214.stp")),
    ("as1-ug-214",                     os.path.join(REAL_CAD, "caxif", "as1-ug-214.stp")),
    ("boxy_with_diamsize",             os.path.join(REAL_CAD, "caxif", "boxy_with_diamsize.stp")),
    ("screw",                          os.path.join(REAL_CAD, "occt", "screw.step")),
    ("linkrods",                       os.path.join(REAL_CAD, "occt", "linkrods.step")),
    ("624ZZ_Ball_Bearing",             os.path.join(REAL_CAD, "freecad", "624ZZ_Ball_Bearing.stp")),
    ("ISO4032_Hex_Nut_M6",             os.path.join(REAL_CAD, "freecad", "ISO4032_Hex_Nut_M6.stp")),
    ("as1-oc-214",                     os.path.join(REAL_CAD, "stepcode", "as1-oc-214.stp")),
    ("splinecage",                     os.path.join(REAL_CAD, "pythonocc", "splinecage.stp")),
    ("face_recognition_sample_part",   os.path.join(REAL_CAD, "pythonocc", "face_recognition_sample_part.stp")),
    ("Ventilator",                     os.path.join(REAL_CAD, "pythonocc", "Ventilator.stp")),
    ("11752",                          os.path.join(REAL_CAD, "pythonocc", "11752.stp")),
]


def log(msg):
    # IronPython's `open()` text-mode write uses sys.getdefaultencoding() (ascii),
    # so non-ASCII content (SC kernel Korean error messages) raises UnicodeEncodeError
    # that the surrounding except: pass swallows silently — the line just vanishes.
    # Use System.IO.File.AppendAllText with UTF8Encoding to guarantee write, and
    # sanitize the console print to ASCII so the PS pipe never drops bytes either.
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


def safe_str(v):
    try:
        return str(v)
    except Exception:
        return "?"


def first(coll):
    try:
        for x in coll:
            return x
    except Exception:
        return None
    return None


def status_for(result):
    if result is None:
        return ("FAIL", "(null result)", "")
    if getattr(result, "Success", False):
        return ("OK", "", "")
    err = getattr(result, "ErrorMessage", None) or "(no msg)"
    hint = getattr(result, "HintMessage", None) or ""
    return ("FAIL", err, hint)


def main():
    log("=" * 70)
    log("MX Real-CAD MODIFICATION batch test")
    log("=" * 70)

    if not os.path.exists(ADDIN_DLL):
        log("FATAL: Add-In DLL not found at %s" % ADDIN_DLL)
        return 1

    try:
        import clr
        clr.AddReferenceToFileAndPath(ADDIN_DLL)
    except Exception as e:
        log("FATAL: AddReference failed: %s" % e)
        log(traceback.format_exc())
        return 1

    try:
        from SpaceClaim.Api.V252 import Document, Window
        from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import (
            FeatureExtractor,
            ModificationService,
            PartBodyTraversal,
            RealModelPipeline,
        )
    except Exception as e:
        log("FATAL: type import failed: %s" % e)
        log(traceback.format_exc())
        return 1

    # Disable color tinting + use the working import path that real_cad_test.py uses.
    try: RealModelPipeline.ApplyColors = False
    except Exception: pass
    try: RealModelPipeline.ExtractAllBodies = False
    except Exception: pass
    # Host doc so SC's /RunScript context has at least one active window.
    try:
        if Window.ActiveWindow is None:
            Document.Create()
    except Exception: pass

    extractor = FeatureExtractor()
    rows = []
    # Run STEP files FIRST, .scdoc samples LAST. Real-CAD STEP modification is
    # the actual unknown we're trying to measure; running them after 4 .scdoc
    # modifications corrupts SC's internal DesignWindow state (NullReferenceException
    # on next Document.Open), so the previously-validated .scdoc samples go last
    # and absorb the cumulative state damage instead of the discovery corpus.
    ordered = sorted(CORPUS, key=lambda nv: 0 if nv[1].lower().endswith((".stp", ".step")) else 1)
    log("Corpus size: %d (STEP-first ordering)" % len(ordered))

    for idx, (name, path) in enumerate(ordered, start=1):
        log("-" * 60)
        log("[%d/%d] %s" % (idx, len(ordered), path))
        row = {
            "name": name,
            "load": "MISSING",
            "extract": "-",
            "hole":   {"action": "-", "status": "-", "msg": "", "before": None, "after": None},
            "fillet": {"action": "-", "status": "-", "msg": "", "before": None, "after": None},
            "wall":   {"action": "-", "status": "-", "msg": "", "before": None, "after": None},
        }

        if not os.path.exists(path):
            log("  MISSING: %s" % path)
            rows.append(row)
            continue

        # Use RealModelPipeline.Run — handles STEP via StepImportService AND
        # .scdoc via Document.Open + PartBodyTraversal. This is the same path
        # real_cad_test.py uses (verified 20/20 PASS), so STEP imports won't
        # silently no-op like raw Document.Open from IronPython did.
        log("  >>> calling pipeline")
        try:
            pr = RealModelPipeline.Run(path, REAL_CAD)
        except Exception as e:
            row["load"] = "FAIL: " + repr(e)
            log("  Pipeline EXC: %s" % repr(e))
            rows.append(row)
            continue
        log("  >>> pipeline returned: type=%s success=%s" % (
            type(pr).__name__ if pr is not None else "None",
            getattr(pr, "Success", "n/a") if pr is not None else "n/a"))
        if pr is None:
            row["load"] = "FAIL: Run returned None"
            log("  Pipeline returned None")
            rows.append(row)
            continue
        if not pr.Success:
            row["load"] = "OK"
            row["extract"] = "FAIL: " + (pr.ErrorMessage or "(no msg)")
            log("  Pipeline FAIL: %s" % (pr.ErrorMessage or "(no msg)"))
            rows.append(row)
            continue
        row["load"] = "OK"
        body = pr.ImportedBody
        graph = pr.Graph
        if body is None or graph is None:
            row["extract"] = "no-body" if body is None else "no-graph"
            log("  Pipeline OK but body=%s graph=%s" % (body is not None, graph is not None))
            rows.append(row)
            continue
        row["extract"] = "OK"
        # Diagnostic: log feature counts so silent-zero cases are visible.
        log("  extract OK: faces=%d holes=%d chains=%d walls=%d bosses=%d" % (
            graph.Faces.Count if graph.Faces else 0,
            graph.Holes.Count if graph.Holes else 0,
            graph.FilletChains.Count if graph.FilletChains else 0,
            graph.Walls.Count if graph.Walls else 0,
            graph.Bosses.Count if graph.Bosses else 0,
        ))

        # --- Hole change: D *= 1.2 ---
        if graph.Holes is not None and graph.Holes.Count > 0:
            try:
                h = graph.Holes[0]
                cur = float(h.DiameterMm)
                new = cur * 1.2
                row["hole"]["action"] = "ChangeHoleDiameter %s: %.3f→%.3f" % (h.Id, cur, new)
                row["hole"]["before"] = cur
                r = ModificationService.ChangeHoleDiameter(body, graph, h.Id, new)
                st, msg, hint = status_for(r)
                row["hole"]["status"] = st
                row["hole"]["msg"] = msg + ("\n        HINT: " + hint if hint else "")
                if st == "OK":
                    try:
                        g2 = extractor.Extract(body)
                        if g2.Holes.Count > 0:
                            row["hole"]["after"] = float(g2.Holes[0].DiameterMm)
                        graph = g2
                    except Exception:
                        pass
                log("  hole: %s (%s)" % (st, msg))
            except Exception as e:
                row["hole"]["status"] = "EXC"
                row["hole"]["msg"] = str(e)
                log("  hole EXC: %s" % e)
        else:
            row["hole"]["action"] = "(no holes)"

        # Skip-after-failure guard: when a mod fails (especially OffsetFaces
        # boolean failure), SC's body Shape is left in a partially-rolled-back
        # state that often raises "The object is deleted" on subsequent
        # operations and corrupts the next Document.Open with NRE. Once any
        # mod fails on this body, abandon the remaining mods on it.
        body_corrupt = (row["hole"]["status"] in ("FAIL", "EXC"))

        # --- Fillet chain R *= 1.2 ---
        if body_corrupt:
            row["fillet"]["action"] = "(skipped — prior mod failed)"
        elif graph.FilletChains is not None and graph.FilletChains.Count > 0:
            try:
                fc = graph.FilletChains[0]
                cur = float(fc.RadiusMm)
                new = cur * 1.2 if cur > 0 else 1.0
                row["fillet"]["action"] = "ChangeFilletRadius %s: %.3f→%.3f" % (fc.Id, cur, new)
                row["fillet"]["before"] = cur
                r = ModificationService.ChangeFilletRadius(body, graph, fc.Id, new)
                st, msg, hint = status_for(r)
                row["fillet"]["status"] = st
                row["fillet"]["msg"] = msg
                if st == "OK":
                    try:
                        g2 = extractor.Extract(body)
                        if g2.FilletChains.Count > 0:
                            row["fillet"]["after"] = float(g2.FilletChains[0].RadiusMm)
                        graph = g2
                    except Exception:
                        pass
                log("  fillet: %s (%s)" % (st, msg))
            except Exception as e:
                row["fillet"]["status"] = "EXC"
                row["fillet"]["msg"] = str(e)
                log("  fillet EXC: %s" % e)
        else:
            row["fillet"]["action"] = "(no chains)"

        body_corrupt = body_corrupt or (row["fillet"]["status"] in ("FAIL", "EXC"))

        # --- Wall T *= 0.9 ---
        if body_corrupt:
            row["wall"]["action"] = "(skipped — prior mod failed)"
            log("  wall: SKIP (prior mod failed; body may be corrupt)")
        elif graph.Walls is not None and graph.Walls.Count > 0:
            try:
                w = graph.Walls[0]
                cur = float(w.ThicknessMm)
                new = cur * 0.9
                row["wall"]["action"] = "ChangeWallThickness %s: %.3f→%.3f" % (w.Id, cur, new)
                row["wall"]["before"] = cur
                r = ModificationService.ChangeWallThickness(body, graph, w.Id, new)
                st, msg, hint = status_for(r)
                row["wall"]["status"] = st
                row["wall"]["msg"] = msg
                if st == "OK":
                    try:
                        g2 = extractor.Extract(body)
                        if g2.Walls.Count > 0:
                            row["wall"]["after"] = float(g2.Walls[0].ThicknessMm)
                    except Exception:
                        pass
                log("  wall: %s (%s)" % (st, msg))
            except Exception as e:
                row["wall"]["status"] = "EXC"
                row["wall"]["msg"] = str(e)
                log("  wall EXC: %s" % e)
        else:
            row["wall"]["action"] = "(no walls)"

        rows.append(row)

        # State-reset between iterations: a FAILED modification (OffsetFaces
        # boolean failure, etc.) leaves SC's body in "deleted" state, and
        # the next Document.Open then throws NullReferenceException because
        # SC's ActiveWindow points into the corrupted doc. Creating a fresh
        # blank doc here pushes a clean ActiveWindow on top, so the next
        # iteration's Document.Open has a known-good context to land in.
        try:
            Document.Create()
        except Exception as e:
            log("  WARN: post-iter Document.Create failed: %s" % e)

    # Aggregate
    def count_status(rs, key, status):
        return sum(1 for r in rs if r[key]["status"] == status)

    hole_ok   = count_status(rows, "hole",   "OK")
    hole_fail = count_status(rows, "hole",   "FAIL") + count_status(rows, "hole", "EXC")
    fillet_ok   = count_status(rows, "fillet", "OK")
    fillet_fail = count_status(rows, "fillet", "FAIL") + count_status(rows, "fillet", "EXC")
    wall_ok   = count_status(rows, "wall",   "OK")
    wall_fail = count_status(rows, "wall",   "FAIL") + count_status(rows, "wall", "EXC")

    log("=" * 70)
    log("Modification corpus complete.")
    log("Hole:   OK=%d  FAIL=%d  (n/a=%d)" % (hole_ok, hole_fail, len(rows) - hole_ok - hole_fail))
    log("Fillet: OK=%d  FAIL=%d  (n/a=%d)" % (fillet_ok, fillet_fail, len(rows) - fillet_ok - fillet_fail))
    log("Wall:   OK=%d  FAIL=%d  (n/a=%d)" % (wall_ok, wall_fail, len(rows) - wall_ok - wall_fail))

    # Write markdown summary
    try:
        import codecs
        with codecs.open(SUMMARY_PATH, "w", encoding="utf-8") as f:
            f.write("# Real-CAD Modification Corpus Summary\n\n")
            f.write("Generated: %s\n\n" % datetime.now().strftime("%Y-%m-%d %H:%M:%S"))
            f.write("Aggregate:\n")
            f.write("- Hole:   OK=%d  FAIL=%d\n" % (hole_ok, hole_fail))
            f.write("- Fillet: OK=%d  FAIL=%d\n" % (fillet_ok, fillet_fail))
            f.write("- Wall:   OK=%d  FAIL=%d\n\n" % (wall_ok, wall_fail))
            f.write("| # | Model | Load | Extract | Hole | Fillet | Wall |\n")
            f.write("|---|---|---|---|---|---|---|\n")
            for i, r in enumerate(rows, start=1):
                f.write("| %d | %s | %s | %s | %s | %s | %s |\n" % (
                    i, r["name"], r["load"], r["extract"],
                    r["hole"]["status"], r["fillet"]["status"], r["wall"]["status"]
                ))
            f.write("\n---\n\n## Per-model details\n\n")
            for r in rows:
                f.write("### %s\n" % r["name"])
                f.write("- load: %s\n" % r["load"])
                f.write("- extract: %s\n" % r["extract"])
                for kind in ("hole", "fillet", "wall"):
                    d = r[kind]
                    f.write("- %s: %s — %s" % (kind, d["status"], d["action"]))
                    if d["before"] is not None and d["after"] is not None:
                        f.write(" — before=%.3f after=%.3f" % (d["before"], d["after"]))
                    if d["msg"]:
                        f.write("\n    msg: %s" % d["msg"])
                    f.write("\n")
                f.write("\n")
        log("Summary written: %s" % SUMMARY_PATH)
    except Exception as e:
        log("WARN: summary write failed: %s" % e)

    log("Total: %d files | HoleOK=%d FilletOK=%d WallOK=%d" %
        (len(rows), hole_ok, fillet_ok, wall_ok))

    try:
        with open(DONE_MARKER, "w") as f:
            f.write("done at %s\n" % datetime.now().isoformat())
    except Exception:
        pass

    return 0


try:
    rc = main()
    log("Exit code: %d" % rc)
except Exception as ex:
    log("UNHANDLED: %s" % ex)
    log(traceback.format_exc())
    rc = 99
