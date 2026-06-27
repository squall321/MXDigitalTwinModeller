# encoding: utf-8
# =====================================================================
# headless_selftest.py
#
# SpaceClaim 의 IronPython 호스트에서 실행. 직접 SelfTestRunner 호출.
# 호출 패턴 (PowerShell):
#   SpaceClaim.exe /Headless /RunScript="D:\...\headless_selftest.py" /Exit
#
# 동작:
#   1. MX Add-In DLL 로드 (deployed V252 폴더)
#   2. 새 Document 생성 (또는 active 사용)
#   3. SelfTestRunner.RunAll(part, OUT_DIR) 호출
#   4. 완료 시 OUT_DIR/__done__ 마커 파일 작성
# =====================================================================

import sys
import os
import traceback
from datetime import datetime

# ---- 설정 ----
ADDIN_DLL = r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll"
OUT_BASE = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest"
RUN_TAG = "headless_" + datetime.now().strftime("%Y%m%d_%H%M%S")
OUT_DIR = os.path.join(OUT_BASE, RUN_TAG)
LATEST_LINK = os.path.join(OUT_BASE, "latest_headless")

# 로그 파일 (script output 캡쳐용)
LOG_PATH = os.path.join(OUT_BASE, "headless_run.log")


def log(msg):
    """STDOUT + 파일 로그 둘 다."""
    line = "[%s] %s" % (datetime.now().strftime("%H:%M:%S"), msg)
    print(line)
    try:
        with open(LOG_PATH, "a") as f:
            f.write(line + "\n")
    except Exception:
        pass


def main():
    log("=" * 70)
    log("MX RE Self-Test (headless)")
    log("=" * 70)
    log("Add-In DLL: %s" % ADDIN_DLL)
    log("Output dir: %s" % OUT_DIR)

    if not os.path.exists(ADDIN_DLL):
        log("FATAL: Add-In DLL not found")
        return 1

    if not os.path.exists(OUT_DIR):
        os.makedirs(OUT_DIR)

    # ---- Step 1: CLR + DLL 로드 ----
    try:
        import clr
        clr.AddReferenceToFileAndPath(ADDIN_DLL)
        log("Loaded: " + ADDIN_DLL)
    except Exception as e:
        log("FATAL: AddReference failed: %s" % e)
        log(traceback.format_exc())
        return 1

    # ---- Step 2: 필요한 type import ----
    try:
        from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import SelfTestRunner
        log("Imported SelfTestRunner")
    except Exception as e:
        log("FATAL: Import failed: %s" % e)
        log(traceback.format_exc())
        return 1

    # ---- Step 3: Document/Part 확보 ----
    # SpaceClaim scripting 환경에서 Window 는 글로벌
    try:
        from SpaceClaim.Api.V252 import Document, Window

        doc = None
        try:
            if Window.ActiveWindow is not None:
                doc = Window.ActiveWindow.Document
                log("Using active document")
        except Exception:
            doc = None

        if doc is None:
            try:
                doc = Document.Create()
                log("Created new document")
            except Exception as e:
                log("Document.Create failed: %s" % e)
                # 다른 시도
                pass

        if doc is None:
            log("FATAL: No document available")
            return 1

        part = doc.MainPart
        log("Got MainPart: %s" % (part.Name if hasattr(part, "Name") else "?"))
    except Exception as e:
        log("FATAL: Document setup failed: %s" % e)
        log(traceback.format_exc())
        return 1

    # ---- Step 4: SelfTestRunner 실행 ----
    try:
        # Opt-in: SC viewer 에서 face 별 feature 색상을 보고 싶으면 True 로.
        # 색상은 .scdocx 안에 저장되므로 파일을 더블클릭하면 보인다.
        # 기본 False — 기존 79/79 가시화 비활성.
        try:
            SelfTestRunner.VisualizeFeatureGraph = False
            log("VisualizeFeatureGraph = %s" % SelfTestRunner.VisualizeFeatureGraph)
        except Exception as e:
            log("WARN: could not set VisualizeFeatureGraph: %s" % e)

        runner = SelfTestRunner()
        log("Starting RunAll...")
        results = runner.RunAll(part, OUT_DIR)

        pass_count = 0
        fail_count = 0
        for r in results:
            if r.OverallPass:
                pass_count += 1
            else:
                fail_count += 1
            log("  [%s] %s" % ("PASS" if r.OverallPass else "FAIL", r.SpecName))

        log("Total: %d tests | PASS=%d | FAIL=%d" % (len(results), pass_count, fail_count))

    except Exception as e:
        log("FATAL: RunAll failed: %s" % e)
        log(traceback.format_exc())
        return 1

    # ---- Step 4b: RoundTripTestRunner 실행 ----
    rt_pass_count = 0
    rt_fail_count = 0
    rt_total = 0
    try:
        from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import RoundTripTestRunner
        log("Imported RoundTripTestRunner")

        rt_out_dir = os.path.join(OUT_DIR, "round_trip")
        if not os.path.exists(rt_out_dir):
            os.makedirs(rt_out_dir)

        try:
            rt_runner = RoundTripTestRunner()
            log("Starting RoundTrip RunAll...")
            rt_results = rt_runner.RunAll(part, rt_out_dir)

            for r in rt_results:
                if r.OverallPass:
                    rt_pass_count += 1
                else:
                    rt_fail_count += 1
                log("  [RT %s] %s" % ("PASS" if r.OverallPass else "FAIL", r.SpecName))

            rt_total = len(rt_results)
            log("RoundTrip Total: %d tests | PASS=%d | FAIL=%d" % (
                rt_total, rt_pass_count, rt_fail_count))

            # 누적 totals 에 합산
            pass_count += rt_pass_count
            fail_count += rt_fail_count

        except Exception as e:
            log("WARN: RoundTrip RunAll failed: %s" % e)
            log(traceback.format_exc())

    except Exception as e:
        log("WARN: RoundTripTestRunner import failed (skipping): %s" % e)
        log(traceback.format_exc())

    # ---- Step 4c: StepRoundTripRunner ----
    step_pass_count = 0
    step_fail_count = 0
    step_total = 0
    try:
        from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import StepRoundTripRunner
        log("Imported StepRoundTripRunner")
        step_out_dir = os.path.join(OUT_DIR, "step_roundtrip")
        if not os.path.exists(step_out_dir):
            os.makedirs(step_out_dir)
        try:
            step_runner = StepRoundTripRunner()
            # Format default = "scdocx" (native, headless-safe). Switch to "stp" once
            # SC's STEP translator dialog suppression is figured out — currently hangs.
            fmt_label = getattr(step_runner, "Format", "scdocx") or "scdocx"
            log_prefix = "STEP RT" if fmt_label == "stp" else "SCDOCX RT"
            log("Starting %s RunAll... (format=%s)" % (log_prefix, fmt_label))
            step_results = step_runner.RunAll(part, step_out_dir)
            for r in step_results:
                if r.OverallPass:
                    step_pass_count += 1
                else:
                    step_fail_count += 1
                log("  [%s %s] %s" % (log_prefix, "PASS" if r.OverallPass else "FAIL", r.SpecName))
                # surface first error for fast triage
                if (not r.OverallPass) and getattr(r, "ErrorMessage", None):
                    log("    err: %s" % r.ErrorMessage)
            step_total = len(step_results)
            log("%s Total: %d tests | PASS=%d | FAIL=%d" % (
                log_prefix, step_total, step_pass_count, step_fail_count))
            pass_count += step_pass_count
            fail_count += step_fail_count
        except Exception as e:
            log("WARN: RoundTrip(scdocx) RunAll failed: %s" % e)
            log(traceback.format_exc())
    except Exception as e:
        log("WARN: StepRoundTripRunner import failed: %s" % e)

    # ---- Step 4d: RealModelPipelineSelfTest ----
    # Headless verification of the full real-model pipeline using SYNTHETIC
    # .scdocx files. Proves the file → import → extract → visualize → report
    # path works before the user supplies a real STEP. Same Document.Create()
    # isolation pattern as Step4c (scdocx RT) to avoid path-retarget bugs.
    real_pass_count = 0
    real_fail_count = 0
    real_total = 0
    try:
        from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import RealModelPipelineSelfTest
        log("Imported RealModelPipelineSelfTest")
        real_out_dir = os.path.join(OUT_DIR, "real_model_selftest")
        if not os.path.exists(real_out_dir):
            os.makedirs(real_out_dir)
        try:
            real_runner = RealModelPipelineSelfTest()
            log("Starting REAL-MODEL RunAll...")
            real_results = real_runner.RunAll(part, real_out_dir)
            for r in real_results:
                if r.OverallPass:
                    real_pass_count += 1
                else:
                    real_fail_count += 1
                log("  [REAL-MODEL %s] %s" % ("PASS" if r.OverallPass else "FAIL", r.SpecName))
                # surface first error for fast triage
                if (not r.OverallPass) and getattr(r, "ErrorMessage", None):
                    log("    err: %s" % r.ErrorMessage)
            real_total = len(real_results)
            log("REAL-MODEL Total: %d tests | PASS=%d | FAIL=%d" % (
                real_total, real_pass_count, real_fail_count))
            pass_count += real_pass_count
            fail_count += real_fail_count
        except Exception as e:
            log("WARN: RealModelPipelineSelfTest RunAll failed: %s" % e)
            log(traceback.format_exc())
    except Exception as e:
        log("WARN: RealModelPipelineSelfTest import failed: %s" % e)

    # ---- Step 5: 완료 marker + latest 링크 ----
    try:
        with open(os.path.join(OUT_DIR, "__done__"), "w") as f:
            f.write("done at %s\npass=%d fail=%d\nrt_pass=%d rt_fail=%d\nstep_pass=%d step_fail=%d\nreal_pass=%d real_fail=%d\n" % (
                datetime.now().isoformat(), pass_count, fail_count,
                rt_pass_count, rt_fail_count,
                step_pass_count, step_fail_count,
                real_pass_count, real_fail_count))

        # latest_headless 폴더에 마지막 결과 복사 — symlink 대신 텍스트 파일로 경로 저장
        with open(LATEST_LINK + ".txt", "w") as f:
            f.write(OUT_DIR + "\n")

        log("__done__ marker written")
        log("Output dir: %s" % OUT_DIR)
    except Exception as e:
        log("WARN: marker write failed: %s" % e)

    return 0


# ---- Entry ----
try:
    rc = main()
    log("Exit code: %d" % rc)
except Exception as ex:
    log("UNHANDLED: %s" % ex)
    log(traceback.format_exc())
    rc = 99

# 외부 launcher 가 __done__ 마커 보고 SC PID 를 직접 kill 한다.
# (os._exit / Document.Exit 등으로는 SC native process 안 죽음 — 검증됨)
# 이 시점에서 python 은 단순히 main() 종료만.
