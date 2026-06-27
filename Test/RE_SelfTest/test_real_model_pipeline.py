# encoding: utf-8
# =====================================================================
# test_real_model_pipeline.py
#
# SpaceClaim IronPython 호스트에서 실행. RealModelPipeline 검증용.
#
# 호출 패턴 (PowerShell):
#   SpaceClaim.exe /Headless /RunScript="...test_real_model_pipeline.py" /Exit
#
# 입력 결정 순서:
#   1) 환경변수 MX_REAL_MODEL — 실모델 (.stp/.step/.scdocx) 경로
#   2) (env 미설정 시) 존재하지 않는 더미 경로로 pipeline 호출 →
#      Run() 이 graceful 하게 ErrorMessage 를 반환하는지 검증.
#
# 종료 코드:
#   0 = OK (실모델 분석 성공 또는 dummy-path graceful-fail 검증 성공)
#   1 = FAIL (예외, 또는 Run() 이 dummy-path 에서도 Success=True)
# =====================================================================

import os
import sys
import traceback
from datetime import datetime

ADDIN_DLL = r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll"
OUT_BASE = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest"
RUN_TAG = "real_model_" + datetime.now().strftime("%Y%m%d_%H%M%S")
OUT_DIR = os.path.join(OUT_BASE, RUN_TAG)
LOG_PATH = os.path.join(OUT_BASE, "real_model_run.log")


def log(msg):
    line = "[%s] %s" % (datetime.now().strftime("%H:%M:%S"), msg)
    print(line)
    try:
        with open(LOG_PATH, "a") as f:
            f.write(line + "\n")
    except Exception:
        pass


def load_pipeline():
    try:
        import clr
        clr.AddReferenceToFileAndPath(ADDIN_DLL)
    except Exception as e:
        log("FATAL: AddReference failed: %s" % e)
        log(traceback.format_exc())
        return None

    try:
        from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import (
            RealModelPipeline,
        )
        return RealModelPipeline
    except Exception as e:
        log("FATAL: import RealModelPipeline failed: %s" % e)
        log(traceback.format_exc())
        return None


def verify_real_model(pipeline, real_path):
    log("Verifying with real model: %s" % real_path)
    if not os.path.exists(real_path):
        log("FATAL: MX_REAL_MODEL points to a missing file: %s" % real_path)
        return 1

    if not os.path.exists(OUT_DIR):
        os.makedirs(OUT_DIR)

    try:
        result = pipeline.Run(real_path, OUT_DIR)
    except Exception as e:
        log("FATAL: pipeline.Run threw: %s" % e)
        log(traceback.format_exc())
        return 1

    if result is None:
        log("FAIL: result is None")
        return 1

    if not result.Success:
        log("FAIL: pipeline reported error: %s" %
            (result.ErrorMessage or "(no message)"))
        return 1

    g = result.Graph
    if g is None:
        log("FAIL: Graph is None on success")
        return 1

    log("OK: pipeline succeeded")
    log("  bbox (mm) : %.2f x %.2f x %.2f" % (
        g.BboxSizeMm[0], g.BboxSizeMm[1], g.BboxSizeMm[2]))
    log("  faces=%d walls=%d fillets=%d holes=%d bosses=%d slits=%d" % (
        g.Faces.Count, g.Walls.Count, g.Fillets.Count,
        g.Holes.Count, g.Bosses.Count, g.Slits.Count))
    log("  json   -> %s" % result.JsonPath)
    log("  report -> %s" % result.ReportPath)

    # marker
    try:
        with open(os.path.join(OUT_DIR, "__done__"), "w") as f:
            f.write("ok\nmodel=%s\n" % real_path)
    except Exception:
        pass
    return 0


def verify_missing_file_graceful(pipeline):
    """No MX_REAL_MODEL set — verify that Run() on a non-existent path
       returns a result with Success=False rather than throwing."""
    bogus = r"C:\__no_such_file__\does_not_exist.stp"
    log("No MX_REAL_MODEL set; verifying graceful failure on: %s" % bogus)

    try:
        result = pipeline.Run(bogus, OUT_BASE)
    except Exception as e:
        log("FAIL: pipeline.Run threw on missing file (expected graceful): %s" % e)
        log(traceback.format_exc())
        return 1

    if result is None:
        log("FAIL: result is None")
        return 1
    if result.Success:
        log("FAIL: Success=True on a bogus path — should have failed gracefully")
        return 1
    if not result.ErrorMessage:
        log("FAIL: Success=False but ErrorMessage is empty")
        return 1

    log("OK: graceful failure with message: %s" % result.ErrorMessage)
    return 0


def main():
    log("=" * 70)
    log("MX Real-Model Pipeline Test (headless)")
    log("=" * 70)

    if not os.path.exists(ADDIN_DLL):
        log("FATAL: Add-In DLL not found: %s" % ADDIN_DLL)
        return 1

    pipeline = load_pipeline()
    if pipeline is None:
        return 1

    real_path = os.environ.get("MX_REAL_MODEL", "").strip()
    if real_path:
        return verify_real_model(pipeline, real_path)
    return verify_missing_file_graceful(pipeline)


try:
    rc = main()
    log("Exit code: %d" % rc)
except Exception as ex:
    log("UNHANDLED: %s" % ex)
    log(traceback.format_exc())
    rc = 99

# 외부 launcher 는 __done__ 마커 보고 종료 (있을 때만).
