# encoding: utf-8
# =====================================================================
# probe_blast_radius.py — E-1: kernel poisoning blast-radius experiment
#
# Forces an OffsetFaces failure (linkrods Wall T=1mm — known to fail),
# then probes what still works in the SAME SC process:
#   T1: pristine pre-failure Body.Copy — op on it works?
#   T2: Document.Create — works?
#   T3: Document.Open SAME file again — works or NRE?
#   T4: Document.Open DIFFERENT file — works or NRE?
#   T5: close poisoned doc windows → Document.Open — recovers?
#   T8: corrective offset on pristine copy IN the same doc?  (fillet-revival scenario)
#
# This decides whether Copy-Probe-Commit + session-reuse (Phase 2 W2-1/W2-2)
# are feasible. Output: probe_blast_results.txt
# =====================================================================
import traceback
from datetime import datetime

ADDIN_DLL = r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll"
LINKRODS  = r"D:\MXDigitalTwinModeller\Test\RealCAD\occt\linkrods.step"
OTHERFILE = r"D:\MXDigitalTwinModeller\Test\RealCAD\stepcode\as1-oc-214.stp"
REAL_CAD  = r"D:\MXDigitalTwinModeller\Test\RealCAD"
OUT_LOG   = r"D:\MXDigitalTwinModeller\Test\RealCAD\probe_blast_results.txt"
DONE_MARK = r"D:\MXDigitalTwinModeller\Test\RealCAD\solo_done.txt"

LF = open(OUT_LOG, "w")
def log(msg):
    line = "[%s] %s" % (datetime.now().strftime("%H:%M:%S"), msg)
    try:
        print(line.encode("ascii", "replace") if isinstance(line, unicode) else line)
    except Exception: pass
    try: LF.write(line + "\n"); LF.flush()
    except Exception: pass

RESULTS = {}
def verdict(name, ok, detail=""):
    RESULTS[name] = (ok, detail)
    log("%s %s %s" % ("PASS" if ok else "FAIL", name, detail))

try:
    import clr
    clr.AddReferenceToFileAndPath(ADDIN_DLL)
    from SpaceClaim.Api.V252 import Document, Window, DesignBody, WriteBlock
    from SpaceClaim.Api.V252.Modeler import Body, Face as MFace, Edge as MEdge
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

    # ---- import linkrods ----
    log("Pipeline.Run linkrods")
    pr = RealModelPipeline.Run(LINKRODS, REAL_CAD)
    if pr is None or not pr.Success:
        log("FATAL: pipeline failed"); raise SystemExit
    body = pr.ImportedBody
    graph = pr.Graph
    log("faces=%d walls=%d" % (
        graph.Faces.Count if graph.Faces else 0,
        graph.Walls.Count if graph.Walls else 0))

    # ---- pristine checkpoint BEFORE failure ----
    shape = body.Shape
    fm = clr.Reference[System.Collections.Generic.IDictionary[MFace, MFace]]()
    em = clr.Reference[System.Collections.Generic.IDictionary[MEdge, MEdge]]()
    pristine = None
    try:
        pristine = shape.Copy(fm, em)
        log("Pristine copy: faces=%d (orig %d), faceMap=%d" % (
            sum(1 for _ in pristine.Faces), sum(1 for _ in shape.Faces),
            fm.Value.Count if fm.Value is not None else -1))
    except Exception as e:
        log("Pristine copy FAILED: %s" % e)

    # map the wall's faceA to the copy
    wall = graph.Walls[0]
    liveFaceA = None
    i = 0
    for df in body.Faces:
        if i == wall.FaceA: liveFaceA = df.Shape
        i += 1
    copyFaceA = None
    if pristine is not None and fm.Value is not None and liveFaceA is not None:
        for kv in fm.Value:
            if kv.Key.Equals(liveFaceA):
                copyFaceA = kv.Value; break
    log("liveFaceA=%s copyFaceA=%s" % (liveFaceA is not None, copyFaceA is not None))

    # ---- FORCE the failure on live body ----
    # Raw single OffsetFaces (NOT ModificationService — its full strategy chain
    # incl. scale-trick CRASHED the process on the first probe attempt). One raw
    # failing offset reproduces the original poisoning event in isolation.
    log("=" * 60)
    log("Forcing RAW OffsetFaces failure (corpus-exact magnitude 0.05mm)")
    # First probe run: ChangeWallThickness full chain → SC PROCESS CRASH.
    # Second: raw -0.45mm single offset → SC PROCESS CRASH.
    # Corpus runs with ±0.05mm steps produced clean exceptions, so use that.
    poisoned = False
    for attempt, off in enumerate([-0.00005, 0.00005, -0.0001, 0.0001]):
        if poisoned: break
        log("  force attempt %d: offset=%.5f" % (attempt + 1, off))
        try:
            def _force():
                body.Shape.OffsetFaces(System.Array[MFace]([liveFaceA]), off)
            WriteBlock.ExecuteTask("force fail %d" % attempt, _force)
            log("  ...succeeded (no poison yet)")
        except Exception as eF:
            poisoned = True
            log("Forced failure achieved: %s" % str(eF)[:120])
    if not poisoned:
        log("WARNING: could not poison body — T-series results will be weaker")

    # ---- T8 FIRST: swap-in (attach pristine copy via DesignBody.Create).
    # Probe A established that ops on DETACHED bodies CRASH the SC process,
    # so the copy must be attached before any op (T1 then runs on it).
    log("=" * 60)
    attachedDb = None
    try:
        from SpaceClaim.Api.V252 import Part
        part = body.Parent if hasattr(body, "Parent") else None
        p2 = part if isinstance(part, Part) else (part.GetAncestor[Part]() if part is not None else None)
        if p2 is None:
            p2 = body.Document.MainPart
        newDb = [None]
        def _swap():
            newDb[0] = DesignBody.Create(p2, "swapin_probe", pristine)
        WriteBlock.ExecuteTask("swap-in pristine", _swap)
        attachedDb = newDb[0]
        verdict("T8 swap-in DesignBody.Create(copy) after failure", attachedDb is not None, "")
    except Exception as e:
        verdict("T8 swap-in DesignBody.Create(copy) after failure", False, str(e)[:120])

    # ---- T1: op on the ATTACHED pristine copy AFTER live failure ----
    try:
        if attachedDb is None or copyFaceA is None:
            verdict("T1 copy-op after failure", False, "no attached copy / mapped face")
        else:
            def _offc():
                attachedDb.Shape.OffsetFaces(System.Array[MFace]([copyFaceA]), -0.00005)
            WriteBlock.ExecuteTask("offset on attached pristine copy", _offc)
            verdict("T1 copy-op after failure", True, "attached pristine copy operable")
    except Exception as e:
        verdict("T1 copy-op after failure", False, str(e)[:120])

    # ---- T2: Document.Create ----
    log("=" * 60)
    try:
        d2 = Document.Create()
        verdict("T2 Document.Create after failure", d2 is not None, "")
    except Exception as e:
        verdict("T2 Document.Create after failure", False, str(e)[:120])

    # ---- T3: Document.Open SAME file ----
    try:
        d3 = Document.Open(LINKRODS, None)
        verdict("T3 reopen same file", d3 is not None, "")
    except Exception as e:
        verdict("T3 reopen same file", False, str(e)[:120])

    # ---- T4: Document.Open DIFFERENT file ----
    try:
        d4 = Document.Open(OTHERFILE, None)
        verdict("T4 open different file", d4 is not None, "")
    except Exception as e:
        verdict("T4 open different file", False, str(e)[:120])

    # ---- T5: close poisoned doc windows, then open ----
    try:
        closedAny = False
        try:
            poisonedDoc = body.Document
            for w in Window.GetWindows(poisonedDoc):
                w.Close(); closedAny = True
        except Exception as eC:
            log("INFO T5 close threw: %s" % str(eC)[:100])
        d5 = Document.Open(OTHERFILE, None)
        verdict("T5 open after closing poisoned doc", d5 is not None, "closedAny=%s" % closedAny)
    except Exception as e:
        verdict("T5 open after closing poisoned doc", False, str(e)[:120])

    # ---- T6: full pipeline run AFTER failure (the real session-reuse test) ----
    log("=" * 60)
    try:
        pr2 = RealModelPipeline.Run(OTHERFILE, REAL_CAD)
        ok6 = pr2 is not None and pr2.Success
        verdict("T6 full pipeline after failure", ok6,
                "" if ok6 else (pr2.ErrorMessage if pr2 else "None")[:120])
        # and a full mod on the fresh import
        if ok6 and pr2.Graph.Holes is not None and pr2.Graph.Holes.Count > 0:
            h = pr2.Graph.Holes[0]
            r2 = ModificationService.ChangeHoleDiameter(pr2.ImportedBody, pr2.Graph, h.Id, float(h.DiameterMm) * 1.2)
            verdict("T7 full mod cycle after failure", bool(r2.Success),
                    (r2.ErrorMessage or "")[:120])
    except Exception as e:
        verdict("T6 full pipeline after failure", False, str(e)[:120])

    log("=" * 60)
    npass = sum(1 for k, v in RESULTS.items() if v[0])
    log("SUMMARY %d/%d PASS" % (npass, len(RESULTS)))
    for k in sorted(RESULTS.keys()):
        ok, det = RESULTS[k]
        log("  %s %s %s" % ("PASS" if ok else "FAIL", k, det))

except SystemExit:
    pass
except Exception as ex:
    log("UNHANDLED: %s" % ex)
    log(traceback.format_exc())
finally:
    LF.close()

try:
    with open(DONE_MARK, "w") as f: f.write("done\n")
except Exception: pass
