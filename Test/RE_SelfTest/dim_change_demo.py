# encoding: utf-8
# =====================================================================
# dim_change_demo.py
#
# Headless end-to-end demo of FeatureGraph-driven dimension change on a
# REAL CAD file (ANSYS SampleModel1.scdoc — the bracket sample).
#
# Pipeline (no GUI clicking):
#   1. Load MX Add-In DLL.
#   2. Document.Load(SampleModel1.scdoc)
#   3. PartBodyTraversal.FindFirstDesignBody -> body
#   4. FeatureExtractor().Extract(body) -> graph
#   5. Apply 3 mutations sequentially via ModificationService:
#        a. ChangeHoleDiameter(Holes[0], D * 1.5)
#        b. ChangeFilletRadius(FilletChains[0], R + 1.0)
#        c. ChangeWallThickness(Walls[0], T - 1.0)
#      (re-extract between each step so the next call uses fresh
#       feature ids / face indices.)
#   6. SaveAs <demoDir>/SampleModel1_modified.scdocx
#   7. Close doc, re-open, re-extract, log persisted dimensions.
#   8. Write demo report (.md) + __done__ marker.
#
# Same dispatch path as the "Change Dimension..." ribbon (ChangeDimensionCommand)
# — calls ModificationService.{ChangeHoleDiameter, ChangeFilletRadius,
# ChangeWallThickness} directly with body+graph+id+value.
# =====================================================================
import sys
import os
import traceback
from datetime import datetime

ADDIN_DLL = r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll"
SAMPLE    = r"D:\Program Files\ANSYS Inc\ANSYS Student\v252\SCDM\Library\SrModels\SampleModel1.scdoc"
DEMO_DIR  = r"D:\MXDigitalTwinModeller\Test\RealCAD\demo"
SAVE_PATH = os.path.join(DEMO_DIR, "SampleModel1_modified.scdocx")
REPORT    = os.path.join(DEMO_DIR, "dim_change_demo_report.md")
DONE      = os.path.join(DEMO_DIR, "__done__")
LOG_PATH  = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\headless_run.log"


def log(msg):
    line = "[%s] %s" % (datetime.now().strftime("%H:%M:%S"), msg)
    print(line)
    try:
        with open(LOG_PATH, "a") as f:
            f.write(line + "\n")
    except Exception:
        pass


def fmt(v, nd=3):
    try:
        return "%.*f" % (nd, float(v))
    except Exception:
        return str(v)


# Captured state, accumulates across run for the report.
report_lines = []
def rline(s=""):
    report_lines.append(s)


def safe_count(lst):
    try:
        return lst.Count if lst is not None else 0
    except Exception:
        return 0


def snapshot(graph):
    """Return (faces, walls, fillet_chains, holes, bosses) counts + first
    hole D / first fillet chain R / first wall T (or None if absent)."""
    snap = {
        "faces":   safe_count(graph.Faces),
        "walls":   safe_count(graph.Walls),
        "fillets": safe_count(graph.FilletChains),
        "holes":   safe_count(graph.Holes),
        "bosses":  safe_count(graph.Bosses),
        "hole0_id": None,
        "hole0_D":  None,
        "fc0_id":   None,
        "fc0_R":    None,
        "wall0_id": None,
        "wall0_T":  None,
    }
    try:
        if graph.Holes is not None and graph.Holes.Count > 0:
            h = graph.Holes[0]
            snap["hole0_id"] = h.Id
            snap["hole0_D"]  = h.DiameterMm
    except Exception:
        pass
    try:
        if graph.FilletChains is not None and graph.FilletChains.Count > 0:
            fc = graph.FilletChains[0]
            snap["fc0_id"] = fc.Id
            snap["fc0_R"]  = fc.RadiusMm
    except Exception:
        pass
    try:
        if graph.Walls is not None and graph.Walls.Count > 0:
            w = graph.Walls[0]
            snap["wall0_id"] = w.Id
            snap["wall0_T"]  = w.ThicknessMm
    except Exception:
        pass
    return snap


def log_snapshot(label, s):
    log("  %s: faces=%d walls=%d fillet_chains=%d holes=%d bosses=%d"
        % (label, s["faces"], s["walls"], s["fillets"], s["holes"], s["bosses"]))
    log("    H0=%s D=%s | FC0=%s R=%s | W0=%s T=%s"
        % (s["hole0_id"], fmt(s["hole0_D"]),
           s["fc0_id"],   fmt(s["fc0_R"]),
           s["wall0_id"], fmt(s["wall0_T"])))


def write_done(status, msg=""):
    try:
        if not os.path.exists(DEMO_DIR):
            os.makedirs(DEMO_DIR)
        with open(DONE, "w") as f:
            f.write("status=%s\nat=%s\nmsg=%s\n" %
                    (status, datetime.now().isoformat(), msg))
    except Exception:
        pass


def write_report():
    try:
        if not os.path.exists(DEMO_DIR):
            os.makedirs(DEMO_DIR)
        from System.IO import File as IoFile
        from System.Text import UTF8Encoding
        IoFile.WriteAllText(REPORT, "\n".join(report_lines), UTF8Encoding(False))
    except Exception as ex:
        # last-ditch fallback
        try:
            with open(REPORT, "w") as f:
                f.write("\n".join(report_lines))
        except Exception:
            log("WARN: report write failed: %s" % ex)


def main():
    log("=" * 70)
    log("dim_change_demo — headless end-to-end dimension change on SampleModel1")
    log("=" * 70)

    if not os.path.exists(DEMO_DIR):
        try:
            os.makedirs(DEMO_DIR)
            log("Created demo dir: %s" % DEMO_DIR)
        except Exception as ex:
            log("FATAL: mkdir DEMO_DIR failed: %s" % ex)
            write_done("FAIL", "mkdir DEMO_DIR failed: %s" % ex)
            return 1

    rline("# Dim Change Demo — SampleModel1 (ANSYS bracket sample)")
    rline("")
    rline("Generated: %s" % datetime.now().strftime("%Y-%m-%d %H:%M:%S"))
    rline("")
    rline("Source     : `%s`" % SAMPLE)
    rline("Output     : `%s`" % SAVE_PATH)
    rline("Add-In DLL : `%s`" % ADDIN_DLL)
    rline("")

    if not os.path.exists(ADDIN_DLL):
        log("FATAL: Add-In DLL not found at %s" % ADDIN_DLL)
        rline("**FATAL** — Add-In DLL not found.")
        write_report()
        write_done("FAIL", "DLL missing")
        return 1
    if not os.path.exists(SAMPLE):
        log("FATAL: SampleModel1.scdoc not found at %s" % SAMPLE)
        rline("**FATAL** — SampleModel1.scdoc not found.")
        write_report()
        write_done("FAIL", "Sample missing")
        return 1

    # ---- Load CLR + DLL ----
    try:
        import clr
        clr.AddReferenceToFileAndPath(ADDIN_DLL)
        log("Loaded Add-In DLL")
    except Exception as ex:
        log("FATAL: AddReference failed: %s" % ex)
        log(traceback.format_exc())
        rline("**FATAL** — AddReference failed: `%s`" % ex)
        write_report()
        write_done("FAIL", str(ex))
        return 1

    try:
        from SpaceClaim.Api.V252 import Document, Window
        from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import (
            FeatureExtractor, ModificationService, PartBodyTraversal,
        )
        log("Imported MX services")
    except Exception as ex:
        log("FATAL: import failed: %s" % ex)
        log(traceback.format_exc())
        rline("**FATAL** — import: `%s`" % ex)
        write_report()
        write_done("FAIL", str(ex))
        return 1

    # ---- Step 1: open the source document ----
    # Some imports must be resolved BEFORE we start using activate_doc, which
    # in turn references Window.GetWindows. We already have Window imported
    # above; activate_doc() is defined later in main() — re-implement a tiny
    # inline version here to avoid forward-ref.
    def _activate_inline(d):
        if d is None: return
        try:
            ws = Window.GetWindows(d)
            if ws is not None:
                for w in ws:
                    try: w.SetActive(); return
                    except Exception: pass
        except Exception:
            pass

    try:
        # Prefer Document.Open (creates a window) over Document.Load (no window
        # in /RunScript context — silently breaks SaveAs).
        try:
            doc = Document.Open(SAMPLE, None)
        except Exception:
            doc = Document.Load(SAMPLE)
        _activate_inline(doc)
        log("Opened SampleModel1: %s" % SAMPLE)
    except Exception as ex:
        log("FATAL: Document.Load failed: %s" % ex)
        log(traceback.format_exc())
        rline("**FATAL** — Document.Load failed: `%s`" % ex)
        write_report()
        write_done("FAIL", str(ex))
        return 1
    if doc is None:
        log("FATAL: Document.Load returned None")
        rline("**FATAL** — Document.Load returned None.")
        write_report()
        write_done("FAIL", "null doc")
        return 1

    # ---- Step 2: find first DesignBody ----
    try:
        body = PartBodyTraversal.FindFirstDesignBody(doc)
    except Exception as ex:
        log("FATAL: FindFirstDesignBody threw: %s" % ex)
        log(traceback.format_exc())
        rline("**FATAL** — FindFirstDesignBody: `%s`" % ex)
        write_report()
        write_done("FAIL", str(ex))
        return 1
    if body is None:
        log("FATAL: no DesignBody found in SampleModel1")
        rline("**FATAL** — no DesignBody found in source doc.")
        write_report()
        write_done("FAIL", "no body")
        return 1
    log("Found DesignBody: %s" % body.Name)

    # ---- Step 3: initial extraction ----
    extractor = FeatureExtractor()
    try:
        graph = extractor.Extract(body)
    except Exception as ex:
        log("FATAL: initial Extract failed: %s" % ex)
        log(traceback.format_exc())
        rline("**FATAL** — initial Extract: `%s`" % ex)
        write_report()
        write_done("FAIL", str(ex))
        return 1
    if graph is None:
        log("FATAL: Extract returned None")
        rline("**FATAL** — initial Extract returned None.")
        write_report()
        write_done("FAIL", "null graph")
        return 1

    # ---- Helper: re-establish (doc, body, graph) after a failed mutation.
    # Real-CAD topology rejections (OffsetFaces -> "Operation failed") can leave
    # the body reference in a "The object is deleted" state. We mirror what the
    # interactive workflow does: re-open the doc and re-find the body so the
    # NEXT independent dispatch step can still be exercised.
    # -------------------------------------------------------------------
    def close_doc(d):
        """Close all windows for doc d (best-effort)."""
        if d is None: return
        try:
            ws = Window.GetWindows(d)
            if ws is not None:
                for w in ws:
                    try: w.Close()
                    except Exception: pass
        except Exception:
            pass

    def activate_doc(d):
        """Make d's window the active window so SaveAs / WriteBlock work
        against the correct doc instance. SpaceClaim ribbon scripts run in
        the context of the *host* doc — Document.Load alone does not make
        the loaded doc 'active', which silently routes ribbon-style API
        calls (incl. SaveAs through some paths) to the empty host doc."""
        if d is None:
            return False
        try:
            ws = Window.GetWindows(d)
            if ws is not None:
                for w in ws:
                    try:
                        w.SetActive()
                        return True
                    except Exception:
                        pass
        except Exception:
            pass
        return False

    def reload_from(path, current_doc=None):
        """Close current_doc, Document.Load(path), find body, extract."""
        close_doc(current_doc)
        try:
            # Document.Open creates a window (unlike Document.Load), which
            # is necessary so SaveAs and other window-bound APIs work.
            try:
                d = Document.Open(path, None)
            except Exception:
                d = Document.Load(path)
        except Exception as ex:
            log("  reload Document.Load failed: %s" % ex)
            return (None, None, None)
        if d is None:
            return (None, None, None)
        # Ensure the loaded doc is the active window so subsequent operations
        # (mutations, SaveAs) target the right instance.
        activate_doc(d)
        try:
            b = PartBodyTraversal.FindFirstDesignBody(d)
        except Exception as ex:
            log("  reload FindFirstDesignBody failed: %s" % ex)
            return (d, None, None)
        if b is None:
            return (d, None, None)
        try:
            g = extractor.Extract(b)
        except Exception as ex:
            log("  reload Extract failed: %s" % ex)
            return (d, b, None)
        return (d, b, g)

    def safe_reextract():
        """Try to re-extract on the current body, returns graph or None."""
        try:
            return extractor.Extract(body)
        except Exception as ex:
            log("  re-extract failed (body invalid?): %s" % ex)
            return None

    s0 = snapshot(graph)
    log_snapshot("INITIAL", s0)
    rline("## 1. Initial feature counts")
    rline("")
    rline("- faces         : %d" % s0["faces"])
    rline("- walls         : %d" % s0["walls"])
    rline("- fillet chains : %d" % s0["fillets"])
    rline("- holes         : %d" % s0["holes"])
    rline("- bosses        : %d" % s0["bosses"])
    rline("")
    rline("Selected initial dimensions:")
    rline("- first hole         : id=`%s`  D=%s mm" % (s0["hole0_id"], fmt(s0["hole0_D"])))
    rline("- first fillet chain : id=`%s`  R=%s mm" % (s0["fc0_id"],   fmt(s0["fc0_R"])))
    rline("- first wall         : id=`%s`  T=%s mm" % (s0["wall0_id"], fmt(s0["wall0_T"])))
    rline("")

    # Targets — picked per spec (current * 1.5, +1.0, -1.0).
    target_D = (s0["hole0_D"] * 1.5) if s0["hole0_D"] is not None else None
    target_R = (s0["fc0_R"]   + 1.0) if s0["fc0_R"]   is not None else None
    target_T = (s0["wall0_T"] - 1.0) if s0["wall0_T"] is not None else None

    rline("Requested changes (sequential — re-extract between each):")
    rline("- hole D : %s -> %s mm  (x1.5)" % (fmt(s0["hole0_D"]), fmt(target_D)))
    rline("- fillet R: %s -> %s mm  (+1.0)" % (fmt(s0["fc0_R"]),   fmt(target_R)))
    rline("- wall T : %s -> %s mm  (-1.0)" % (fmt(s0["wall0_T"]), fmt(target_T)))
    rline("")
    rline("## 2. Sequential dimension changes")
    rline("")

    step_results = []  # list of dicts: step, requested, actual_after, success, error

    # ---- Step a: ChangeHoleDiameter ----
    rline("### Step (a) — ChangeHoleDiameter")
    rline("")
    if s0["hole0_id"] is None or target_D is None:
        log("STEP A SKIPPED — no hole in initial graph")
        rline("- **SKIPPED** — no hole found in initial graph.")
        rline("")
        step_results.append({"step": "a", "name": "ChangeHoleDiameter",
                              "requested": target_D, "actual": None,
                              "success": False, "error": "no hole in graph"})
    else:
        log("STEP A: ChangeHoleDiameter %s  %s -> %s" %
            (s0["hole0_id"], fmt(s0["hole0_D"]), fmt(target_D)))
        try:
            res_a = ModificationService.ChangeHoleDiameter(body, graph, s0["hole0_id"], target_D)
            ok_a = bool(res_a.Success)
            err_a = res_a.ErrorMessage if res_a.ErrorMessage else ""
        except Exception as ex:
            ok_a = False
            err_a = "EXCEPTION: %s" % ex
            log("STEP A EXCEPTION: %s" % ex)
            log(traceback.format_exc())

        # Re-extract to read the actual persisted value + use for step (b).
        # If the body got invalidated by a failed WriteBlock, reload from
        # source so the NEXT step can still dispatch a fresh body.
        g_re = safe_reextract()
        if g_re is None:
            log("STEP A: body invalidated, reloading from source")
            doc, body, g_re = reload_from(SAMPLE, doc)
            err_a = (err_a + " | reloaded source").strip(" |")
        graph = g_re
        s_after_a = snapshot(graph) if graph is not None else s0
        actual_a = s_after_a["hole0_D"]
        log_snapshot("AFTER A", s_after_a)

        step_results.append({"step": "a", "name": "ChangeHoleDiameter",
                              "requested": target_D, "actual": actual_a,
                              "success": ok_a, "error": err_a})

        rline("- target id     : `%s`" % s0["hole0_id"])
        rline("- current D     : %s mm" % fmt(s0["hole0_D"]))
        rline("- requested D   : %s mm" % fmt(target_D))
        rline("- actual D after: %s mm" % fmt(actual_a))
        rline("- success       : %s" % ("yes" if ok_a else "NO"))
        if err_a:
            rline("- error         : `%s`" % err_a.replace("`", "'"))
        rline("")

    # ---- Step b: ChangeFilletRadius (using freshly re-extracted graph) ----
    rline("### Step (b) — ChangeFilletRadius")
    rline("")
    s_b = snapshot(graph) if graph is not None else s0
    fc_id_b = s_b["fc0_id"]
    cur_R_b = s_b["fc0_R"]
    tgt_R_b = (cur_R_b + 1.0) if cur_R_b is not None else None

    if fc_id_b is None or tgt_R_b is None:
        log("STEP B SKIPPED — no fillet chain after step a")
        rline("- **SKIPPED** — no fillet chain after step a.")
        rline("")
        step_results.append({"step": "b", "name": "ChangeFilletRadius",
                              "requested": tgt_R_b, "actual": None,
                              "success": False, "error": "no fillet chain"})
    else:
        log("STEP B: ChangeFilletRadius %s  %s -> %s" %
            (fc_id_b, fmt(cur_R_b), fmt(tgt_R_b)))
        try:
            res_b = ModificationService.ChangeFilletRadius(body, graph, fc_id_b, tgt_R_b)
            ok_b = bool(res_b.Success)
            err_b = res_b.ErrorMessage if res_b.ErrorMessage else ""
        except Exception as ex:
            ok_b = False
            err_b = "EXCEPTION: %s" % ex
            log("STEP B EXCEPTION: %s" % ex)
            log(traceback.format_exc())

        g_re = safe_reextract()
        if g_re is None:
            log("STEP B: body invalidated, reloading from source")
            doc, body, g_re = reload_from(SAMPLE, doc)
            err_b = (err_b + " | reloaded source").strip(" |")
        graph = g_re
        s_after_b = snapshot(graph) if graph is not None else s_b
        actual_b = s_after_b["fc0_R"]
        log_snapshot("AFTER B", s_after_b)

        step_results.append({"step": "b", "name": "ChangeFilletRadius",
                              "requested": tgt_R_b, "actual": actual_b,
                              "success": ok_b, "error": err_b})

        rline("- target id     : `%s`" % fc_id_b)
        rline("- current R     : %s mm" % fmt(cur_R_b))
        rline("- requested R   : %s mm" % fmt(tgt_R_b))
        rline("- actual R after: %s mm" % fmt(actual_b))
        rline("- success       : %s" % ("yes" if ok_b else "NO"))
        if err_b:
            rline("- error         : `%s`" % err_b.replace("`", "'"))
        rline("")

    # ---- Step c: ChangeWallThickness ----
    rline("### Step (c) — ChangeWallThickness")
    rline("")
    s_c = snapshot(graph) if graph is not None else s_b
    w_id_c  = s_c["wall0_id"]
    cur_T_c = s_c["wall0_T"]
    tgt_T_c = (cur_T_c - 1.0) if cur_T_c is not None else None

    if w_id_c is None or tgt_T_c is None or tgt_T_c <= 0:
        reason = "no wall after step b" if w_id_c is None else ("invalid target T=%s" % fmt(tgt_T_c))
        log("STEP C SKIPPED — %s" % reason)
        rline("- **SKIPPED** — %s." % reason)
        rline("")
        step_results.append({"step": "c", "name": "ChangeWallThickness",
                              "requested": tgt_T_c, "actual": None,
                              "success": False, "error": reason})
    else:
        log("STEP C: ChangeWallThickness %s  %s -> %s" %
            (w_id_c, fmt(cur_T_c), fmt(tgt_T_c)))
        try:
            res_c = ModificationService.ChangeWallThickness(body, graph, w_id_c, tgt_T_c)
            ok_c = bool(res_c.Success)
            err_c = res_c.ErrorMessage if res_c.ErrorMessage else ""
        except Exception as ex:
            ok_c = False
            err_c = "EXCEPTION: %s" % ex
            log("STEP C EXCEPTION: %s" % ex)
            log(traceback.format_exc())

        g_re = safe_reextract()
        if g_re is None:
            log("STEP C: body invalidated, reloading from source")
            doc, body, g_re = reload_from(SAMPLE, doc)
            err_c = (err_c + " | reloaded source").strip(" |")
        graph = g_re
        s_after_c = snapshot(graph) if graph is not None else s_c
        actual_c = s_after_c["wall0_T"]
        log_snapshot("AFTER C", s_after_c)

        step_results.append({"step": "c", "name": "ChangeWallThickness",
                              "requested": tgt_T_c, "actual": actual_c,
                              "success": ok_c, "error": err_c})

        rline("- target id     : `%s`" % w_id_c)
        rline("- current T     : %s mm" % fmt(cur_T_c))
        rline("- requested T   : %s mm" % fmt(tgt_T_c))
        rline("- actual T after: %s mm" % fmt(actual_c))
        rline("- success       : %s" % ("yes" if ok_c else "NO"))
        if err_c:
            rline("- error         : `%s`" % err_c.replace("`", "'"))
        rline("")

    # ---- Step 4: SaveAs ----
    # Resolve the doc through the (currently valid) body — after a reload,
    # `doc` may be the second-generation handle while body.Document is the
    # canonical owner of the mutated geometry.
    save_doc = doc
    try:
        if body is not None:
            bd = body.Document
            if bd is not None:
                save_doc = bd
    except Exception as ex:
        log("WARN: body.Document lookup failed: %s" % ex)

    # Best-effort: delete any stale output before SaveAs so we know the file
    # we observe afterwards was produced by THIS run.
    try:
        if os.path.exists(SAVE_PATH):
            os.remove(SAVE_PATH)
    except Exception as ex:
        log("WARN: could not remove stale %s: %s" % (SAVE_PATH, ex))

    # Ensure save_doc has an active window (SaveAs sometimes needs one even
    # when the doc reference is otherwise valid — symptom is a Korean
    # "Sequence contains no elements" LINQ error from inside SaveAs).
    try:
        save_wins = Window.GetWindows(save_doc)
        if save_wins is not None:
            for sw in save_wins:
                try:
                    sw.SetActive()
                    log("Activated save_doc window")
                    break
                except Exception as ex:
                    log("WARN: window SetActive failed: %s" % ex)
    except Exception as ex:
        log("WARN: GetWindows on save_doc failed: %s" % ex)

    log("Saving modified doc -> %s" % SAVE_PATH)
    save_ok = False
    save_err = ""
    saved_to = SAVE_PATH

    def try_save(d, label, path):
        """Returns (ok, err). ALWAYS checks file-on-disk afterwards because
        SC can throw a benign "Sequence contains no elements" *after*
        successfully writing the file."""
        if d is None:
            return (False, "%s: doc is None" % label)
        try:
            d.SaveAs(path)
            on_disk = os.path.exists(path)
            return (on_disk, "" if on_disk else "%s: no exception but file not on disk" % label)
        except Exception as e:
            on_disk = os.path.exists(path)
            if on_disk:
                # File written; the exception came from post-save bookkeeping
                # (known LINQ "Sequence contains no elements" issue on docs
                # opened headless via Document.Load).
                log("%s: SaveAs threw BUT file is on disk — treating as success (%s)" % (label, e))
                return (True, "")
            return (False, "%s: %s" % (label, e))

    # Diagnostic: where is the body really? Compare the three candidate docs.
    try:
        b_doc = body.Document if body is not None else None
        aw = Window.ActiveWindow
        awd = aw.Document if aw is not None else None
        try: b_bcount = b_doc.MainPart.Bodies.Count if b_doc is not None else -1
        except Exception: b_bcount = -1
        try: d_bcount = doc.MainPart.Bodies.Count if doc is not None else -1
        except Exception: d_bcount = -1
        try: aw_bcount = awd.MainPart.Bodies.Count if awd is not None else -1
        except Exception: aw_bcount = -1
        log("DIAG save context:")
        log("  body.Document.MainPart.Bodies    = %d" % b_bcount)
        log("  doc.MainPart.Bodies              = %d" % d_bcount)
        log("  ActiveWindow.Doc.MainPart.Bodies = %d" % aw_bcount)
    except Exception as ex:
        log("DIAG block failed: %s" % ex)

    # Headless context bug: SpaceClaim's /RunScript driver keeps an EMPTY
    # "host" document as Window.ActiveWindow. Document.Load brings in our
    # mutated doc but never auto-activates it, so:
    #   - save_doc.SaveAs (body.Document) throws "Sequence contains no
    #     elements" because some internal LINQ query expects an active
    #     window to belong to that doc.
    #   - Window.ActiveWindow.Document.SaveAs succeeds but writes the
    #     empty host doc, NOT our mutations.
    #
    # The fix: explicitly SetActive a window of save_doc BEFORE SaveAs.
    save_ok = False
    save_err = ""
    activated = False
    try:
        save_wins = Window.GetWindows(save_doc)
        if save_wins is not None:
            for sw in save_wins:
                try:
                    sw.SetActive()
                    activated = True
                    log("Activated a window of save_doc before SaveAs")
                    break
                except Exception as ex:
                    log("WARN: window SetActive failed: %s" % ex)
        if not activated:
            log("WARN: save_doc has no window to activate "
                "(headless Document.Load did not create one)")
    except Exception as ex:
        log("WARN: GetWindows(save_doc) failed: %s" % ex)

    save_ok, save_err = try_save(save_doc, "save_doc", SAVE_PATH)
    if save_ok:
        log("Saved OK via save_doc: %s" % SAVE_PATH)
    else:
        log("Primary save failed: %s" % save_err)
        # Try doc handle (often the same as save_doc, but cheap to attempt)
        ok2, err2 = try_save(doc, "doc", SAVE_PATH)
        if ok2:
            save_ok = True
            save_err = ""
            log("Saved OK via doc fallback")
        else:
            log("doc fallback failed: %s" % err2)
            save_err = (save_err + " | " + err2).strip(" |")
        # Deliberately NOT falling back to Window.ActiveWindow.Document —
        # that path silently writes the EMPTY host doc when the mutated doc
        # is not the active one (verified via the diagnostic above).
    # Track which doc we saved to so re-open uses a clean reference.
    doc = save_doc

    rline("## 3. Save")
    rline("")
    rline("- requested path : `%s`" % SAVE_PATH)
    rline("- actual path    : `%s`" % saved_to)
    rline("- saved          : %s" % ("yes" if save_ok else "NO"))
    if save_err:
        rline("- error          : `%s`" % save_err.replace("`", "'"))
    rline("")

    # ---- Step 5: close source doc ----
    try:
        wins = Window.GetWindows(doc)
        if wins is not None:
            for w in wins:
                try: w.Close()
                except Exception as ex: log("  WARN: window close: %s" % ex)
        log("Closed source doc windows")
    except Exception as ex:
        log("WARN: close source doc failed: %s" % ex)

    # ---- Step 6: re-open + final extraction ----
    persisted = None
    if save_ok:
        try:
            log("Re-opening saved file: %s" % saved_to)
            try:
                doc2 = Document.Open(saved_to, None)
            except Exception:
                doc2 = Document.Load(saved_to)
        except Exception as ex:
            log("FATAL: re-open Document.Load failed: %s" % ex)
            log(traceback.format_exc())
            doc2 = None

        if doc2 is not None:
            try:
                body2 = PartBodyTraversal.FindFirstDesignBody(doc2)
            except Exception as ex:
                log("re-open FindFirstDesignBody threw: %s" % ex)
                body2 = None

            if body2 is None:
                log("FAIL: re-opened doc has no DesignBody via PartBodyTraversal")
                # Diagnostic: dump what the doc DOES have.
                try:
                    mp = doc2.MainPart
                    log("  diag: doc2.MainPart=%s" % (mp.Name if mp is not None else None))
                    if mp is not None:
                        bcount = 0
                        try: bcount = mp.Bodies.Count
                        except Exception: pass
                        ccount = 0
                        try: ccount = mp.Components.Count
                        except Exception: pass
                        log("  diag: MainPart.Bodies=%d, Components=%d" % (bcount, ccount))
                    pcount = 0
                    try: pcount = doc2.Parts.Count
                    except Exception: pass
                    log("  diag: doc2.Parts=%d" % pcount)
                except Exception as exD:
                    log("  diag dump failed: %s" % exD)
            else:
                try:
                    graph2 = extractor.Extract(body2)
                    persisted = snapshot(graph2)
                    log_snapshot("PERSISTED", persisted)
                except Exception as ex:
                    log("re-open Extract failed: %s" % ex)
                    log(traceback.format_exc())
        else:
            log("FAIL: doc2 is None")
    else:
        log("Skipping re-open: save failed")

    rline("## 4. Persisted (after Close + Document.Load)")
    rline("")
    if persisted is None:
        rline("- **could not verify persistence** (see log).")
    else:
        rline("- faces         : %d" % persisted["faces"])
        rline("- walls         : %d" % persisted["walls"])
        rline("- fillet chains : %d" % persisted["fillets"])
        rline("- holes         : %d" % persisted["holes"])
        rline("- bosses        : %d" % persisted["bosses"])
        rline("")
        rline("- first hole         : id=`%s`  D=%s mm" % (persisted["hole0_id"], fmt(persisted["hole0_D"])))
        rline("- first fillet chain : id=`%s`  R=%s mm" % (persisted["fc0_id"],   fmt(persisted["fc0_R"])))
        rline("- first wall         : id=`%s`  T=%s mm" % (persisted["wall0_id"], fmt(persisted["wall0_T"])))
    rline("")

    # ---- Verdict ----
    rline("## 5. Verdict")
    rline("")
    rline("| step | op                  | requested | actual_after | success | note |")
    rline("|------|---------------------|-----------|--------------|---------|------|")
    for sr in step_results:
        rline("| %s | %s | %s mm | %s mm | %s | %s |" % (
            sr["step"], sr["name"],
            fmt(sr["requested"]) if sr["requested"] is not None else "-",
            fmt(sr["actual"])    if sr["actual"]    is not None else "-",
            "yes" if sr["success"] else "NO",
            (sr["error"] or "").replace("|", "/")[:80],
        ))
    rline("")
    n_success = sum(1 for sr in step_results if sr["success"])
    rline("- successes : %d / 3" % n_success)
    rline("- saved     : %s" % ("yes" if save_ok else "NO"))
    rline("- persisted : %s" % ("yes" if persisted is not None else "NO"))
    rline("")

    end_to_end = (n_success >= 1) and save_ok and (persisted is not None)
    if end_to_end:
        rline("**END-TO-END DIMENSION CHANGE: WORKED** "
              "(at least one mutation applied, saved, re-opened, re-extracted).")
    else:
        rline("**END-TO-END DIMENSION CHANGE: NOT FULLY VERIFIED** "
              "(see step + save + persistence rows).")
    rline("")

    write_report()
    log("Wrote report: %s" % REPORT)

    write_done("OK" if end_to_end else "PARTIAL",
               "successes=%d/3 save=%s persisted=%s" %
               (n_success, save_ok, persisted is not None))
    log("Wrote __done__ marker: %s" % DONE)

    # The outer launcher polls __done__ and kills SC.
    return 0 if end_to_end else 2


# ---- Entry ----
try:
    rc = main()
    log("dim_change_demo exit code: %d" % rc)
    log("Total: dim_change_demo done")  # marker for run_real_cad.ps1-style pollers
except Exception as ex:
    log("UNHANDLED: %s" % ex)
    log(traceback.format_exc())
    try:
        rline("\n## UNHANDLED")
        rline("")
        rline("```\n%s\n```" % traceback.format_exc())
        write_report()
    except Exception:
        pass
    write_done("CRASH", str(ex))
    log("Total: dim_change_demo crashed")
