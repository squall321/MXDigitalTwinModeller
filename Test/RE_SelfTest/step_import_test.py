# encoding: utf-8
# =====================================================================
# step_import_test.py
#
# SpaceClaim 의 IronPython 호스트에서 실행. StepImportService 검증용.
#
# 호출 패턴 (PowerShell):
#   SpaceClaim.exe /Headless /RunScript="D:\...\step_import_test.py" /Exit
#
# 입력 STEP 경로 결정 순서 (먼저 발견되는 것 사용):
#   1) 환경변수 MX_STEP_PATH
#   2) 아래 STEP_PATH_FALLBACK
#
# 동작:
#   1) MX Add-In DLL 로드
#   2) StepImportService.ImportAndExtract(STEP_PATH)
#   3) 성공 시 FeatureGraph 를 JSON 으로 OUT_DIR 에 저장 + 콘솔로 요약 출력
#   4) 실패 시 ErrorMessage 출력
#
# 주의: 아직 사용자 STEP 파일이 없으므로 이 스크립트는 *skeleton* 이다 —
# STEP 경로를 채우기만 하면 즉시 돌아간다.
# =====================================================================

import os
import sys
import traceback
from datetime import datetime

# ---- 설정 ----
ADDIN_DLL = r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll"
OUT_BASE = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest"
RUN_TAG = "step_import_" + datetime.now().strftime("%Y%m%d_%H%M%S")
OUT_DIR = os.path.join(OUT_BASE, RUN_TAG)
LOG_PATH = os.path.join(OUT_BASE, "step_import_run.log")

# 사용자가 실제 STEP 파일을 넣을 때 채울 자리.
# 아래 두 곳 중 한 곳만 설정해도 됨:
#   - 환경변수 MX_STEP_PATH=...
#   - STEP_PATH_FALLBACK = r"D:\path\to\file.stp"
STEP_PATH_FALLBACK = r""  # 예: r"D:\MXDigitalTwinModeller\Examples\iphone\iphone.stp"


def log(msg):
    line = "[%s] %s" % (datetime.now().strftime("%H:%M:%S"), msg)
    print(line)
    try:
        with open(LOG_PATH, "a") as f:
            f.write(line + "\n")
    except Exception:
        pass


def resolve_step_path():
    env_path = os.environ.get("MX_STEP_PATH", "").strip()
    if env_path:
        return env_path
    return STEP_PATH_FALLBACK


def main():
    log("=" * 70)
    log("MX RE STEP Import Test (headless)")
    log("=" * 70)
    log("Add-In DLL: %s" % ADDIN_DLL)
    log("Output dir: %s" % OUT_DIR)

    step_path = resolve_step_path()
    if not step_path:
        log("FATAL: STEP 경로가 지정되지 않았습니다.")
        log("  -> 환경변수 MX_STEP_PATH=... 또는 STEP_PATH_FALLBACK 을 설정하세요.")
        return 2

    log("STEP path: %s" % step_path)
    if not os.path.exists(step_path):
        log("FATAL: STEP file not found: %s" % step_path)
        return 2

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
        from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import (
            StepImportService,
            FeatureGraphJsonWriter,
        )
        log("Imported StepImportService + FeatureGraphJsonWriter")
    except Exception as e:
        log("FATAL: Import failed: %s" % e)
        log(traceback.format_exc())
        return 1

    # ---- Step 3: ImportAndExtract ----
    try:
        log("Calling StepImportService.ImportAndExtract ...")
        result = StepImportService.ImportAndExtract(step_path)
    except Exception as e:
        log("FATAL: ImportAndExtract threw: %s" % e)
        log(traceback.format_exc())
        return 1

    if not result.Success:
        log("Import FAILED: %s" % (result.ErrorMessage or "(no message)"))
        return 1

    log("Import OK")
    log("  body name : %s" % (result.ImportedBody.Name if result.ImportedBody else "(none)"))
    g = result.ExtractedGraph
    if g is not None:
        log("  bbox (mm) : %.2f x %.2f x %.2f" % (
            g.BboxSizeMm[0], g.BboxSizeMm[1], g.BboxSizeMm[2]))
        log("  faces=%d, walls=%d, fillets=%d (chains=%d), holes=%d, bosses=%d, slits=%d" % (
            g.Faces.Count, g.Walls.Count, g.Fillets.Count, g.FilletChains.Count,
            g.Holes.Count, g.Bosses.Count, g.Slits.Count))

    # ---- Step 4: FeatureGraph JSON 저장 ----
    if g is not None:
        json_path = os.path.join(OUT_DIR, "feature_graph.json")
        try:
            FeatureGraphJsonWriter.WriteToFile(g, json_path)
            log("Saved FeatureGraph JSON: %s" % json_path)
        except Exception as e:
            log("WARN: JSON write failed: %s" % e)

    # ---- Step 5: 완료 marker ----
    try:
        with open(os.path.join(OUT_DIR, "__done__"), "w") as f:
            f.write("done at %s\nstep=%s\nbody=%s\n" % (
                datetime.now().isoformat(),
                step_path,
                result.ImportedBody.Name if result.ImportedBody else "(none)",
            ))
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

# 외부 launcher 가 __done__ 마커 보고 SC PID 를 kill 한다.
