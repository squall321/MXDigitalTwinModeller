#!/bin/bash
# MXSimulator ACT Extension 배포 스크립트
# 사용:
#   bash deploy_mxsimulator.sh          # main.py만 배포 (Mechanical 재시작)
#   bash deploy_mxsimulator.sh --xml    # XML도 배포 + 캐시 삭제 (Workbench 재시작 필요)

SRC_DIR="$(cd "$(dirname "$0")" && pwd)"
EXT_ROOT="/c/Users/Sonic/AppData/Roaming/Ansys/v252/ACT/extensions"
CACHE_DIR="/c/Users/Sonic/AppData/Roaming/Ansys/v252/Applets/DSApplet/en-us"

echo "=== MXSimulator 배포 시작 ==="

# main.py 배포 (항상)
cp "$SRC_DIR/MXSimulator/main.py" "$EXT_ROOT/MXSimulator/main.py"
echo "[OK] main.py"

# images 배포 (항상)
cp -r "$SRC_DIR/MXSimulator/images/." "$EXT_ROOT/MXSimulator/images/"
echo "[OK] images/"

# calibration/ 배포 (Phase 0+, Python 모듈)
mkdir -p "$EXT_ROOT/MXSimulator/calibration"
if [ -d "$SRC_DIR/MXSimulator/calibration" ]; then
    cp -r "$SRC_DIR/MXSimulator/calibration/." "$EXT_ROOT/MXSimulator/calibration/"
    echo "[OK] calibration/ (Python module)"

    # MaterialCalibrator.exe 우선 배포 (있을 때만)
    if [ -f "$SRC_DIR/MXSimulator/calibration/MaterialCalibrator.exe" ]; then
        echo "[OK]   MaterialCalibrator.exe (통합 executable)"
    else
        echo "[--]   MaterialCalibrator.exe 없음 (build_calibrator.bat 실행 권장)"
    fi
fi

# calibration_env/ 배포 (venv는 큼 - 심볼릭 링크 또는 절대경로 사용)
# Note: venv는 로컬에서 setup_venv.bat로 생성, 배포하지 않음
if [ -d "$SRC_DIR/MXSimulator/calibration_env" ]; then
    echo "[--] calibration_env/ (venv는 로컬 유지, 배포 안 함)"
fi

# setup_venv.bat, requirements.txt 배포
if [ -f "$SRC_DIR/MXSimulator/setup_venv.bat" ]; then
    cp "$SRC_DIR/MXSimulator/setup_venv.bat" "$EXT_ROOT/MXSimulator/"
    cp "$SRC_DIR/MXSimulator/requirements.txt" "$EXT_ROOT/MXSimulator/"
    echo "[OK] setup_venv.bat, requirements.txt"
fi

# postprocess/ 배포 (항상)
mkdir -p "$EXT_ROOT/MXSimulator/postprocess"

# MXPostViewer.exe 우선 배포 (pyinstaller 빌드 결과물, 있을 때만)
if [ -f "$SRC_DIR/MXSimulator/postprocess/MXPostViewer.exe" ]; then
    cp "$SRC_DIR/MXSimulator/postprocess/MXPostViewer.exe" "$EXT_ROOT/MXSimulator/postprocess/"
    echo "[OK] postprocess/MXPostViewer.exe"
else
    echo "[--] MXPostViewer.exe 없음 (build_viewer.bat 실행 후 재배포)"
fi

# Python 소스 폴백용 (개발/디버그 + exe 없을 때 fallback)
cp "$SRC_DIR/MXSimulator/postprocess/runner.py"        "$EXT_ROOT/MXSimulator/postprocess/"
cp "$SRC_DIR/MXSimulator/postprocess/analyzer.py"      "$EXT_ROOT/MXSimulator/postprocess/"
cp "$SRC_DIR/MXSimulator/postprocess/visualizer.py"    "$EXT_ROOT/MXSimulator/postprocess/"
cp "$SRC_DIR/MXSimulator/postprocess/requirements.txt" "$EXT_ROOT/MXSimulator/postprocess/"
cp "$SRC_DIR/MXSimulator/postprocess/setup_venv.bat"   "$EXT_ROOT/MXSimulator/postprocess/"
cp "$SRC_DIR/MXSimulator/postprocess/build_viewer.bat" "$EXT_ROOT/MXSimulator/postprocess/"
echo "[OK] postprocess/ (Python sources)"

# MXSimulator.xml 배포 (항상)
cp "$SRC_DIR/MXSimulator.xml" "$EXT_ROOT/MXSimulator.xml"
echo "[OK] MXSimulator.xml"

# 리본 캐시 삭제 (항상 - 버튼 인식 문제 방지)
for f in ExternalActions.xml ribbonLayout.xml RibbonState.xml; do
    if [ -f "$CACHE_DIR/$f" ]; then
        rm "$CACHE_DIR/$f"
        echo "[OK] 캐시 삭제: $f"
    fi
done

echo ""
echo "=== 배포 완료 ==="
echo "※ Workbench 완전 종료 후 재시작 필요 (캐시 삭제됨)"
