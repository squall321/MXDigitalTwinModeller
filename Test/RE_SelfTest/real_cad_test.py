# encoding: utf-8
# =====================================================================
# real_cad_test.py
#
# Real-CAD discovery driver. Runs RealModelPipeline.Run against the
# ANSYS sample library + downloaded public corpus (NIST/CAx-IF/OCCT/
# FreeCAD/STEPcode/pythonocc-demos).
#
# Goal: discover what works and what breaks on actual files. Failures
# here are EXPECTED first contact — they are the deliverable.
#
# Same execution pattern as headless_selftest.py:
#   1. Load MX Add-In DLL.
#   2. For each file: RealModelPipeline.Run(path, output_dir).
#   3. Log feature counts / errors per model.
#   4. Write master summary at Test/RealCAD/00_master_summary.md.
#   5. Drop __done__ marker so run_real_cad.ps1 can kill SC.
# =====================================================================
import sys
import os
import traceback
from datetime import datetime

# ---- 설정 ----
ADDIN_DLL = r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll"
OUT_BASE = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest"
REAL_CAD_BASE = r"D:\MXDigitalTwinModeller\Test\RealCAD"
RUN_TAG = "headless_" + datetime.now().strftime("%Y%m%d_%H%M%S")
OUT_DIR = os.path.join(OUT_BASE, RUN_TAG)
LATEST_LINK = os.path.join(OUT_BASE, "latest_headless")

# real_cad reports live under Test/RealCAD/reports/{filename}/
REPORTS_BASE = os.path.join(REAL_CAD_BASE, "reports")
SUMMARY_PATH = os.path.join(REAL_CAD_BASE, "00_master_summary.md")

# 로그 파일 (run_real_cad.ps1 polling 용 — Total: 마커 emit 한다)
LOG_PATH = os.path.join(OUT_BASE, "headless_run.log")


# ---- Corpus ----
# Hardcoded list per task: ANSYS samples + manifest corpus subset.
# Keep the manifest-side list bounded so SC doesn't wedge on the 12 MB+
# KUKA / RC-buggy STEPs in a single run. We pick a representative
# spread across all 6 manifest sources.
ANSYS_SAMPLES = [
    r"d:\Program Files\ANSYS Inc\ANSYS Student\v252\SCDM\Library\SrModels\SampleModel1.scdoc",
    r"d:\Program Files\ANSYS Inc\ANSYS Student\v252\SCDM\Library\SrModels\SampleModel4.scdoc",
    r"d:\Program Files\ANSYS Inc\ANSYS Student\v252\SCDM\Library\SrModels\samplemodel5.scdoc",
    r"d:\Program Files\ANSYS Inc\ANSYS Student\v252\SCDM\Library\SrModels\samplemodel2.scdoc",
]

NIST_DIR = os.path.join(REAL_CAD_BASE, "nist", "NIST-PMI-STEP-Files")
CAXIF_DIR = os.path.join(REAL_CAD_BASE, "caxif")
OCCT_DIR = os.path.join(REAL_CAD_BASE, "occt")
FREECAD_DIR = os.path.join(REAL_CAD_BASE, "freecad")
STEPCODE_DIR = os.path.join(REAL_CAD_BASE, "stepcode")
PYTHONOCC_DIR = os.path.join(REAL_CAD_BASE, "pythonocc")

MANIFEST_FILES = [
    # NIST — 1 file per CTC/FTC/STC family, smallest variant of each (keep runtime sane)
    os.path.join(NIST_DIR, "nist_ctc_01_asme1_ap242-e1.stp"),
    os.path.join(NIST_DIR, "nist_ftc_07_asme1_ap242-e2.stp"),
    os.path.join(NIST_DIR, "nist_stc_06_asme1_ap242-e3.stp"),
    # CAx-IF — different originating CAD systems on the same AS1 assembly
    os.path.join(CAXIF_DIR, "as1-ac-214.stp"),   # AutoCAD
    os.path.join(CAXIF_DIR, "as1-md-214.stp"),   # MicroStation
    os.path.join(CAXIF_DIR, "as1-ug-214.stp"),   # Unigraphics
    os.path.join(CAXIF_DIR, "boxy_with_diamsize.stp"),
    # OCCT samples
    os.path.join(OCCT_DIR, "screw.step"),
    os.path.join(OCCT_DIR, "linkrods.step"),
    # FreeCAD library — bearings + nuts (cylindrical + threads)
    os.path.join(FREECAD_DIR, "624ZZ_Ball_Bearing.stp"),
    os.path.join(FREECAD_DIR, "ISO4032_Hex_Nut_M6.step"),
    # STEPcode AS1 (OpenCascade translation)
    os.path.join(STEPCODE_DIR, "as1-oc-214.stp"),
    # pythonocc-demos — exercise NURBS / feature-recognition / mid-size assemblies
    os.path.join(PYTHONOCC_DIR, "splinecage.stp"),
    os.path.join(PYTHONOCC_DIR, "face_recognition_sample_part.stp"),
    os.path.join(PYTHONOCC_DIR, "Ventilator.stp"),
    os.path.join(PYTHONOCC_DIR, "11752.stp"),
]

CORPUS = ANSYS_SAMPLES + MANIFEST_FILES


def log(msg):
    """STDOUT + file log."""
    line = "[%s] %s" % (datetime.now().strftime("%H:%M:%S"), msg)
    print(line)
    try:
        with open(LOG_PATH, "a") as f:
            f.write(line + "\n")
    except Exception:
        pass


def safe_name(path):
    """Use file name without extension as report dir name."""
    try:
        return os.path.splitext(os.path.basename(path))[0]
    except Exception:
        return "model"


def main():
    log("=" * 70)
    log("MX Real-CAD discovery run")
    log("=" * 70)
    log("Add-In DLL: %s" % ADDIN_DLL)
    log("OUT_DIR   : %s" % OUT_DIR)
    log("REPORTS   : %s" % REPORTS_BASE)
    log("Files     : %d" % len(CORPUS))

    if not os.path.exists(ADDIN_DLL):
        log("FATAL: Add-In DLL not found")
        return 1

    if not os.path.exists(OUT_DIR):
        os.makedirs(OUT_DIR)
    if not os.path.exists(REPORTS_BASE):
        os.makedirs(REPORTS_BASE)

    # ---- Step 1: CLR + DLL ----
    try:
        import clr
        clr.AddReferenceToFileAndPath(ADDIN_DLL)
        log("Loaded: " + ADDIN_DLL)
    except Exception as e:
        log("FATAL: AddReference failed: %s" % e)
        log(traceback.format_exc())
        return 1

    try:
        from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import RealModelPipeline
        log("Imported RealModelPipeline")
    except Exception as e:
        log("FATAL: Import RealModelPipeline failed: %s" % e)
        log(traceback.format_exc())
        return 1

    # Disable color tinting — we only care about JSON + report on disk.
    try:
        RealModelPipeline.ApplyColors = False
        log("RealModelPipeline.ApplyColors = False")
    except Exception as e:
        log("WARN: could not set ApplyColors: %s" % e)

    # Multi-shell mode is opt-in per file (toggled below in the loop) — AS1
    # assemblies are known to carry 3+ separate DesignBodies but single-part
    # STEPs would just waste cycles re-extracting the same body. We default
    # the flag OFF here; the loop enables it ONLY for AS1 files.
    try:
        RealModelPipeline.ExtractAllBodies = False
        log("RealModelPipeline.ExtractAllBodies = False (default; toggled per-file)")
    except Exception as e:
        log("WARN: could not set ExtractAllBodies: %s" % e)

    # We need at least one active document for SC scripting context. The
    # AnalyzeRealModelCommand path doesn't strictly need one (Document.Load
    # creates one), but having a host doc avoids the "no active window"
    # IronPython glitch.
    try:
        from SpaceClaim.Api.V252 import Document, Window
        if Window.ActiveWindow is None:
            Document.Create()
            log("Created host document (no active window before)")
    except Exception as e:
        log("WARN: Document setup glitch: %s" % e)

    # ---- Step 2: iterate corpus ----
    summary_rows = []
    pass_count = 0
    fail_count = 0
    missing_count = 0

    for idx, fpath in enumerate(CORPUS, start=1):
        name = safe_name(fpath)
        log("-" * 60)
        log("[%d/%d] %s" % (idx, len(CORPUS), fpath))

        report_dir = os.path.join(REPORTS_BASE, name)
        if not os.path.exists(report_dir):
            try:
                os.makedirs(report_dir)
            except Exception as e:
                log("  ERR: mkdir failed: %s" % e)

        row = {
            "idx": idx,
            "name": name,
            "path": fpath,
            "exists": os.path.exists(fpath),
            "size_bytes": 0,
            "success": False,
            "error": "",
            "faces": 0,
            "walls": 0,
            "fillets": 0,
            "fillet_chains": 0,
            "holes": 0,
            "bosses": 0,
            "slits": 0,
            "hole_patterns": 0,
            "boss_patterns": 0,
            "symmetries": 0,
            "composites": 0,
            "cone_count": 0,
            "unknown_count": 0,
            "wall_inverted_count": 0,
            "bbox_size": "",
            "face_type_counts": {},
            "report_path": "",
            "json_path": "",
            "elapsed_s": 0.0,
            # Multi-shell extension fields (populated when ExtractAllBodies=True)
            "shell_count": 0,
            "shell_face_counts": [],
            "aggregate_faces": 0,
            "aggregate_holes": 0,
            "aggregate_bosses": 0,
        }

        if not row["exists"]:
            log("  MISSING — skip")
            row["error"] = "file not found"
            missing_count += 1
            summary_rows.append(row)
            continue

        try:
            row["size_bytes"] = os.path.getsize(fpath)
        except Exception:
            pass

        # Per-file multi-shell toggle. AS1 CAx-IF assemblies (as1-ac, as1-md,
        # as1-ug, as1-oc) are documented multi-body STEPs — turn on
        # ExtractAllBodies so we get per-shell breakdowns. Everything else
        # stays in the original single-body path so PASS counts don't shift.
        try:
            is_as1 = name.lower().startswith("as1-")
            RealModelPipeline.ExtractAllBodies = is_as1
            if is_as1:
                log("  ExtractAllBodies = True (AS1 multi-shell assembly)")
        except Exception as e:
            log("  WARN: per-file ExtractAllBodies toggle failed: %s" % e)

        t0 = datetime.now()
        try:
            pr = RealModelPipeline.Run(fpath, report_dir)
        except Exception as ex:
            elapsed = (datetime.now() - t0).total_seconds()
            row["elapsed_s"] = elapsed
            row["error"] = "EXCEPTION: %s" % ex
            log("  THREW (%.1fs): %s" % (elapsed, ex))
            log(traceback.format_exc())
            fail_count += 1
            summary_rows.append(row)
            continue

        elapsed = (datetime.now() - t0).total_seconds()
        row["elapsed_s"] = elapsed

        if pr is None:
            row["error"] = "Run returned None"
            log("  FAIL (%.1fs): Run returned None" % elapsed)
            fail_count += 1
            summary_rows.append(row)
            continue

        row["success"] = bool(pr.Success)
        row["error"] = pr.ErrorMessage if pr.ErrorMessage else ""
        row["report_path"] = pr.ReportPath if pr.ReportPath else ""
        row["json_path"] = pr.JsonPath if pr.JsonPath else ""

        g = pr.Graph
        if g is not None:
            row["faces"] = g.Faces.Count if g.Faces is not None else 0
            row["walls"] = g.Walls.Count if g.Walls is not None else 0
            row["fillets"] = g.Fillets.Count if g.Fillets is not None else 0
            row["fillet_chains"] = g.FilletChains.Count if g.FilletChains is not None else 0
            row["holes"] = g.Holes.Count if g.Holes is not None else 0
            row["bosses"] = g.Bosses.Count if g.Bosses is not None else 0
            row["slits"] = g.Slits.Count if g.Slits is not None else 0
            row["hole_patterns"] = g.HolePatterns.Count if g.HolePatterns is not None else 0
            row["boss_patterns"] = g.BossPatterns.Count if g.BossPatterns is not None else 0
            row["symmetries"] = g.Symmetries.Count if g.Symmetries is not None else 0
            row["composites"] = g.Composites.Count if g.Composites is not None else 0

            # face_type_counts
            ft = {}
            if g.FaceTypeCounts is not None:
                for kv in g.FaceTypeCounts:
                    ft[kv.Key] = kv.Value
            row["face_type_counts"] = ft
            row["cone_count"] = ft.get("cone", 0) + ft.get("Cone", 0)
            row["unknown_count"] = ft.get("unknown", 0) + ft.get("Unknown", 0)

            # Wall.IsInverted
            inv = 0
            if g.Walls is not None:
                for w in g.Walls:
                    try:
                        if w.IsInverted:
                            inv += 1
                    except Exception:
                        pass
            row["wall_inverted_count"] = inv

            # Bounding box
            if g.BboxSizeMm is not None and len(g.BboxSizeMm) >= 3:
                row["bbox_size"] = "%.1f x %.1f x %.1f" % (
                    g.BboxSizeMm[0], g.BboxSizeMm[1], g.BboxSizeMm[2])

            # Multi-shell breakdown (only populated when ExtractAllBodies=True)
            try:
                if g.Shells is not None and g.Shells.Count > 0:
                    row["shell_count"] = g.ShellCount
                    row["aggregate_faces"] = g.AggregateFaceCount
                    row["aggregate_holes"] = g.AggregateHoleCount
                    row["aggregate_bosses"] = g.AggregateBossCount
                    counts = []
                    for sh in g.Shells:
                        if sh is None or sh.Faces is None:
                            counts.append(0)
                        else:
                            counts.append(sh.Faces.Count)
                    row["shell_face_counts"] = counts
            except Exception as ex:
                log("  WARN: shell harvest failed: %s" % ex)

        if row["success"]:
            pass_count += 1
            log("  OK (%.1fs) faces=%d walls=%d fillets=%d(chains=%d) holes=%d bosses=%d slits=%d cones=%d unknown=%d bbox=%s" % (
                elapsed, row["faces"], row["walls"], row["fillets"], row["fillet_chains"],
                row["holes"], row["bosses"], row["slits"],
                row["cone_count"], row["unknown_count"], row["bbox_size"]))
            if row["hole_patterns"] or row["boss_patterns"] or row["symmetries"] or row["composites"]:
                log("    patterns: holePat=%d bossPat=%d sym=%d comp=%d" % (
                    row["hole_patterns"], row["boss_patterns"],
                    row["symmetries"], row["composites"]))
            if row["wall_inverted_count"]:
                log("    Wall.IsInverted hits: %d" % row["wall_inverted_count"])
            if row["shell_count"] > 1:
                log("    multi-shell: count=%d faces=%s agg_faces=%d agg_holes=%d agg_bosses=%d" % (
                    row["shell_count"],
                    ",".join(str(c) for c in row["shell_face_counts"]),
                    row["aggregate_faces"], row["aggregate_holes"], row["aggregate_bosses"]))
        else:
            fail_count += 1
            log("  FAIL (%.1fs): %s" % (elapsed, row["error"]))

        summary_rows.append(row)

    # ---- Step 3: master summary ----
    try:
        write_summary(summary_rows, pass_count, fail_count, missing_count)
        log("Master summary written: %s" % SUMMARY_PATH)
    except Exception as e:
        log("WARN: summary write failed: %s" % e)
        log(traceback.format_exc())

    total = len(CORPUS)
    log("=" * 70)
    log("Total: %d files | PASS=%d | FAIL=%d | MISSING=%d" % (
        total, pass_count, fail_count, missing_count))

    # ---- Step 4: __done__ marker ----
    try:
        with open(os.path.join(OUT_DIR, "__done__"), "w") as f:
            f.write("done at %s\npass=%d fail=%d missing=%d total=%d\n" % (
                datetime.now().isoformat(), pass_count, fail_count, missing_count, total))
        with open(LATEST_LINK + ".txt", "w") as f:
            f.write(OUT_DIR + "\n")
        log("__done__ marker written: %s" % OUT_DIR)
    except Exception as e:
        log("WARN: marker write failed: %s" % e)

    return 0


def write_summary(rows, pass_count, fail_count, missing_count):
    """Master Markdown summary of all corpus runs."""
    lines = []
    lines.append("# Real CAD Discovery Run — Master Summary")
    lines.append("")
    lines.append("Generated: %s" % datetime.now().strftime("%Y-%m-%d %H:%M:%S"))
    lines.append("")
    lines.append("- **Total**: %d" % len(rows))
    lines.append("- **PASS** : %d" % pass_count)
    lines.append("- **FAIL** : %d" % fail_count)
    lines.append("- **MISSING (file not found)**: %d" % missing_count)
    lines.append("")
    lines.append("Pipeline: `RealModelPipeline.Run(filePath, outputDir)`")
    lines.append("Reports : `Test/RealCAD/reports/{filename}/{name}_report.md` + `{name}_features.json`")
    lines.append("")
    lines.append("---")
    lines.append("")
    lines.append("## Result table")
    lines.append("")
    lines.append("| # | Name | Status | Faces | Walls | Fil | Holes | Boss | Slit | Cone | Unk | Pat(H/B/Sym/Cmp) | Inv | BBox (mm) | Sec |")
    lines.append("|---|------|--------|-------|-------|-----|-------|------|------|------|-----|--------------------|-----|-----------|-----|")
    for r in rows:
        if not r["exists"]:
            status = "MISSING"
        elif r["success"]:
            status = "PASS"
        else:
            status = "FAIL"
        pat = "%d/%d/%d/%d" % (r["hole_patterns"], r["boss_patterns"], r["symmetries"], r["composites"])
        lines.append("| %d | %s | %s | %d | %d | %d | %d | %d | %d | %d | %d | %s | %d | %s | %.1f |" % (
            r["idx"], r["name"], status,
            r["faces"], r["walls"], r["fillets"], r["holes"], r["bosses"],
            r["slits"], r["cone_count"], r["unknown_count"],
            pat, r["wall_inverted_count"], r["bbox_size"], r["elapsed_s"]))

    lines.append("")
    lines.append("---")
    lines.append("")
    lines.append("## Per-file details")
    lines.append("")
    for r in rows:
        lines.append("### %d. %s" % (r["idx"], r["name"]))
        lines.append("- path: `%s`" % r["path"])
        lines.append("- size: %d bytes" % r["size_bytes"])
        if not r["exists"]:
            lines.append("- **MISSING** — file not on disk")
            lines.append("")
            continue
        lines.append("- success: %s" % ("yes" if r["success"] else "no"))
        if r["error"]:
            lines.append("- error: `%s`" % r["error"].replace("`", "'"))
        lines.append("- elapsed: %.2fs" % r["elapsed_s"])
        if r["success"]:
            lines.append("- bbox (mm): %s" % r["bbox_size"])
            lines.append("- counts: faces=%d walls=%d fillets=%d (chains=%d) holes=%d bosses=%d slits=%d" % (
                r["faces"], r["walls"], r["fillets"], r["fillet_chains"],
                r["holes"], r["bosses"], r["slits"]))
            lines.append("- patterns: hole=%d boss=%d sym=%d composites=%d" % (
                r["hole_patterns"], r["boss_patterns"],
                r["symmetries"], r["composites"]))
            lines.append("- cone faces: %d" % r["cone_count"])
            lines.append("- unknown faces: %d" % r["unknown_count"])
            lines.append("- wall_inverted: %d" % r["wall_inverted_count"])
            if r["shell_count"] > 1:
                lines.append("- multi-shell: count=%d agg_faces=%d agg_holes=%d agg_bosses=%d" % (
                    r["shell_count"], r["aggregate_faces"],
                    r["aggregate_holes"], r["aggregate_bosses"]))
                lines.append("- per-shell face counts: %s" % (
                    ", ".join(str(c) for c in r["shell_face_counts"])))
            if r["face_type_counts"]:
                lines.append("- face types:")
                for k in sorted(r["face_type_counts"].keys()):
                    lines.append("  - %s: %d" % (k, r["face_type_counts"][k]))
            if r["report_path"]:
                lines.append("- report: `%s`" % r["report_path"])
            if r["json_path"]:
                lines.append("- json  : `%s`" % r["json_path"])
        lines.append("")

    try:
        if not os.path.exists(os.path.dirname(SUMMARY_PATH)):
            os.makedirs(os.path.dirname(SUMMARY_PATH))
        # Use System.IO so IronPython's default ascii codec doesn't trip on
        # ANSYS-emitted non-ASCII bytes inside report content.
        from System.IO import File as IoFile
        from System.Text import UTF8Encoding
        IoFile.WriteAllText(SUMMARY_PATH, "\n".join(lines), UTF8Encoding(False))
    except Exception as e:
        # final fallback: drop summary in OUT_DIR via raw byte write
        fallback = os.path.join(OUT_DIR, "00_master_summary.md")
        try:
            from System.IO import File as IoFile2
            from System.Text import UTF8Encoding as Enc2
            IoFile2.WriteAllText(
                fallback,
                "# Real CAD summary (fallback path)\nOriginal write failed: %s\n\n%s" % (e, "\n".join(lines)),
                Enc2(False))
        except Exception:
            pass


# ---- Entry ----
try:
    rc = main()
    log("Exit code: %d" % rc)
except Exception as ex:
    log("UNHANDLED: %s" % ex)
    log(traceback.format_exc())
    rc = 99
# Outer launcher kills SC after seeing __done__.
