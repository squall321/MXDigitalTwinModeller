# encoding: utf-8
# =====================================================================
# test_llm_dispatch.py
#
# Stage 5 LLM tool dispatcher self-test.
# Runs inside SpaceClaim's IronPython host.
#
# Call pattern (PowerShell):
#   SpaceClaim.exe /Headless /RunScript="D:\MXDigitalTwinModeller\Test\RE_SelfTest\test_llm_dispatch.py" /Exit
#
# What it does:
#   1) Load MX Add-In DLL.
#   2) Build a body containing 1 through hole + 1 boss (TestModelGenerator).
#   3) Extract FeatureGraph.
#   4) Dispatch 3-4 hardcoded "tool_use" requests via LlmToolDispatcher:
#        - find_features_by_type
#        - change_hole_diameter
#        - change_boss_diameter
#        - add_hole
#   5) Check {"success": true, ...} envelope.
# =====================================================================

import os
import sys
import traceback
import re
from datetime import datetime

ADDIN_DLL = r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll"
OUT_BASE = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest"
RUN_TAG = "llm_dispatch_" + datetime.now().strftime("%Y%m%d_%H%M%S")
OUT_DIR = os.path.join(OUT_BASE, RUN_TAG)
LOG_PATH = os.path.join(OUT_BASE, "llm_dispatch_run.log")


def log(msg):
    line = "[%s] %s" % (datetime.now().strftime("%H:%M:%S"), msg)
    print(line)
    try:
        with open(LOG_PATH, "a") as f:
            f.write(line + "\n")
    except Exception:
        pass


def _envelope_success(json_text):
    """Tiny ad-hoc parser: search for "success": true (avoids JSON dep)."""
    if not json_text:
        return False
    m = re.search(r'"success"\s*:\s*(true|false)', json_text)
    return (m is not None) and (m.group(1) == "true")


def _envelope_error(json_text):
    if not json_text:
        return "(empty)"
    m = re.search(r'"error"\s*:\s*"([^"]*)"', json_text)
    return m.group(1) if m else "(none)"


def main():
    log("=" * 70)
    log("MX LLM Dispatch Self-Test")
    log("=" * 70)

    if not os.path.exists(ADDIN_DLL):
        log("FATAL: Add-In DLL not found at " + ADDIN_DLL)
        return 1
    if not os.path.exists(OUT_DIR):
        os.makedirs(OUT_DIR)

    # ---- Step 1: CLR + DLL ----
    try:
        import clr
        clr.AddReferenceToFileAndPath(ADDIN_DLL)
        log("Loaded: " + ADDIN_DLL)
    except Exception as e:
        log("FATAL: AddReference failed: %s" % e)
        log(traceback.format_exc())
        return 1

    # ---- Step 2: imports ----
    try:
        from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import (
            LlmToolRegistry, LlmToolDispatcher, TestModelGenerator, FeatureExtractor,
        )
        from SpaceClaim.Api.V252 import Document, Window
        log("Imported LlmToolRegistry/Dispatcher/TestModelGenerator/FeatureExtractor")
    except Exception as e:
        log("FATAL: Import failed: %s" % e)
        log(traceback.format_exc())
        return 1

    # ---- Step 3: Document/Part ----
    try:
        doc = None
        try:
            if Window.ActiveWindow is not None:
                doc = Window.ActiveWindow.Document
        except Exception:
            doc = None
        if doc is None:
            doc = Document.Create()
            log("Created new document")
        else:
            log("Using active document")
        part = doc.MainPart
    except Exception as e:
        log("FATAL: Document setup failed: %s" % e)
        log(traceback.format_exc())
        return 1

    # ---- Step 4: build a body (BoxWithFilletAndHoles has BOTH a hole + a fillet) ----
    try:
        gen = TestModelGenerator()
        specs = gen.CreateAllSpecs()
        # Find BoxWithBoss + BoxWithThroughHole + BoxWithFilletAndHoles by Name.
        target = None
        # Preferred: BoxWithFilletAndHoles — has both holes and a fillet chain.
        # Fallback: BoxWithThroughHole — has 1 hole; then BoxWithBoss for boss-only.
        preferred_names = ["07_box_fillet_R2_4holes", "04_box_thru_hole", "06_box_boss"]
        for want in preferred_names:
            for s in specs:
                if s.Name == want:
                    target = s
                    break
            if target is not None:
                break
        if target is None:
            log("FATAL: could not find a suitable test spec")
            return 1
        log("Building spec: %s" % target.Name)
        design_body = target.Builder(part)
    except Exception as e:
        log("FATAL: spec.Builder failed: %s" % e)
        log(traceback.format_exc())
        return 1

    # ---- Step 5: tool list sanity check ----
    try:
        tools = LlmToolRegistry.GetAllTools()
        log("LlmToolRegistry.GetAllTools() returned %d tools" % tools.Count)
        if tools.Count < 18:
            log("WARN: expected >= 18 tools, got %d" % tools.Count)
        # Print first 5 names.
        for i in range(min(5, tools.Count)):
            log("  tool[%d]: %s" % (i, tools[i].Name))
    except Exception as e:
        log("WARN: tool list inspection failed: %s" % e)

    # ---- Step 6: extract FeatureGraph ----
    try:
        extractor = FeatureExtractor()
        graph = extractor.Extract(design_body)
        log("Extracted graph: holes=%d bosses=%d walls=%d filletChains=%d" % (
            graph.Holes.Count, graph.Bosses.Count, graph.Walls.Count, graph.FilletChains.Count))
    except Exception as e:
        log("FATAL: FeatureExtractor failed: %s" % e)
        log(traceback.format_exc())
        return 1

    # ---- Step 7: dispatch sample tool_use calls ----
    passes = 0
    fails = 0

    samples = []

    # Sample A: find_features_by_type "hole"
    samples.append((
        "find_features_by_type",
        '{"feature_type": "hole"}',
        True,
    ))

    # Sample B: change_hole_diameter on the first hole (if any).
    first_hole_id = graph.Holes[0].Id if graph.Holes.Count > 0 else None
    if first_hole_id is not None:
        samples.append((
            "change_hole_diameter",
            '{"hole_id": "%s", "new_diameter_mm": 7.0}' % first_hole_id,
            True,
        ))
    else:
        log("WARN: no holes — skipping change_hole_diameter sample")

    # Sample C: get_feature_graph (read-only).
    samples.append((
        "get_feature_graph",
        '{}',
        True,
    ))

    # Sample D: error-path — invalid hole id should return success=false.
    samples.append((
        "change_hole_diameter",
        '{"hole_id": "H_NOT_EXIST", "new_diameter_mm": 5.0}',
        False,  # expected success
    ))

    for tool_name, input_json, expected_success in samples:
        try:
            log("--- Dispatch: %s   input=%s" % (tool_name, input_json))
            resp = LlmToolDispatcher.Dispatch(design_body, graph, tool_name, input_json)
            ok = _envelope_success(resp)
            log("    response (truncated): %s" % (resp[:240] + ("..." if len(resp) > 240 else "")))
            if ok == expected_success:
                passes += 1
                log("    => PASS (success=%s, expected=%s)" % (ok, expected_success))
            else:
                fails += 1
                log("    => FAIL: success=%s, expected=%s, error=%s" % (
                    ok, expected_success, _envelope_error(resp)))
        except Exception as e:
            fails += 1
            log("    => FAIL (exception): %s" % e)
            log(traceback.format_exc())

    log("=" * 70)
    log("LLM dispatch test summary: PASS=%d FAIL=%d" % (passes, fails))
    log("=" * 70)

    # write done marker
    try:
        marker = os.path.join(OUT_DIR, "__done__")
        with open(marker, "w") as f:
            f.write("pass=%d fail=%d\n" % (passes, fails))
    except Exception:
        pass

    return 0 if fails == 0 else 1


if __name__ == "__main__":
    rc = main()
    sys.exit(rc)
