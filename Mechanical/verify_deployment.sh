#!/bin/bash
# MXSimulator 배포 상태 검증 스크립트

EXT_ROOT="/c/Users/Sonic/AppData/Roaming/Ansys/v252/ACT/extensions"
CACHE_DIR="/c/Users/Sonic/AppData/Roaming/Ansys/v252/Applets/DSApplet/en-us"

echo "========================================="
echo "MXSimulator 배포 상태 검증"
echo "========================================="
echo

# 1. XML 파일 확인
echo "[1] MXSimulator.xml 확인"
if [ -f "$EXT_ROOT/MXSimulator.xml" ]; then
    echo "  ✓ XML 파일 존재"
    if grep -q "show_material_twin_dialog" "$EXT_ROOT/MXSimulator.xml"; then
        echo "  ✓ Material Twin 버튼 정의됨"
    else
        echo "  ✗ Material Twin 버튼 없음"
    fi
else
    echo "  ✗ XML 파일 없음"
fi
echo

# 2. main.py 확인
echo "[2] main.py 확인"
if [ -f "$EXT_ROOT/MXSimulator/main.py" ]; then
    SIZE=$(stat -c%s "$EXT_ROOT/MXSimulator/main.py" 2>/dev/null || stat -f%z "$EXT_ROOT/MXSimulator/main.py")
    echo "  ✓ main.py 존재 (크기: $SIZE bytes)"

    if grep -q "def show_material_twin_dialog" "$EXT_ROOT/MXSimulator/main.py"; then
        echo "  ✓ show_material_twin_dialog 함수 존재"
    else
        echo "  ✗ show_material_twin_dialog 함수 없음"
    fi

    if grep -q "class MaterialTwinDialog" "$EXT_ROOT/MXSimulator/main.py"; then
        echo "  ✓ MaterialTwinDialog 클래스 존재"
    else
        echo "  ✗ MaterialTwinDialog 클래스 없음"
    fi
else
    echo "  ✗ main.py 파일 없음"
fi
echo

# 3. 아이콘 확인
echo "[3] material_twin.png 아이콘 확인"
if [ -f "$EXT_ROOT/MXSimulator/images/material_twin.png" ]; then
    SIZE=$(stat -c%s "$EXT_ROOT/MXSimulator/images/material_twin.png" 2>/dev/null || stat -f%z "$EXT_ROOT/MXSimulator/images/material_twin.png")
    echo "  ✓ 아이콘 존재 (크기: $SIZE bytes)"
else
    echo "  ✗ 아이콘 없음"
fi
echo

# 4. calibration 모듈 확인
echo "[4] calibration 모듈 확인"
if [ -d "$EXT_ROOT/MXSimulator/calibration" ]; then
    echo "  ✓ calibration/ 디렉토리 존재"
    if [ -f "$EXT_ROOT/MXSimulator/calibration/__init__.py" ]; then
        echo "  ✓ __init__.py 존재"
    fi
else
    echo "  ✗ calibration/ 디렉토리 없음"
fi
echo

# 5. venv 설정 파일 확인
echo "[5] venv 설정 파일 확인"
if [ -f "$EXT_ROOT/MXSimulator/setup_venv.bat" ]; then
    echo "  ✓ setup_venv.bat 존재"
else
    echo "  ✗ setup_venv.bat 없음"
fi
if [ -f "$EXT_ROOT/MXSimulator/requirements.txt" ]; then
    echo "  ✓ requirements.txt 존재"
else
    echo "  ✗ requirements.txt 없음"
fi
echo

# 6. 캐시 파일 확인
echo "[6] Workbench 캐시 상태"
CACHE_EXISTS=0
for f in ExternalActions.xml ribbonLayout.xml RibbonState.xml; do
    if [ -f "$CACHE_DIR/$f" ]; then
        echo "  ⚠ $f 존재 (재시작 후에도 문제 시 삭제 필요)"
        CACHE_EXISTS=1
    fi
done
if [ $CACHE_EXISTS -eq 0 ]; then
    echo "  ✓ 캐시 파일 없음 (정상)"
fi
echo

# 7. ANSYS 프로세스 확인
echo "[7] ANSYS 프로세스 확인"
if tasklist 2>/dev/null | grep -iq ansys; then
    echo "  ⚠ ANSYS 프로세스 실행 중 - 종료 후 재시작 필요"
    tasklist | grep -i ansys
else
    echo "  ✓ ANSYS 프로세스 없음 (재시작 가능)"
fi
echo

# 8. Python 문법 검증
echo "[8] Python 문법 검증"
cd "$EXT_ROOT/MXSimulator"
if python -m py_compile main.py 2>/dev/null; then
    echo "  ✓ Python 문법 오류 없음"
else
    echo "  ✗ Python 문법 오류 발견"
    python -m py_compile main.py
fi
echo

echo "========================================="
echo "검증 완료"
echo "========================================="
echo
echo "다음 단계:"
echo "1. ANSYS Workbench 완전 종료 (모든 프로세스)"
echo "2. Workbench 재시작"
echo "3. Mechanical 열기"
echo "4. 'MX Digital Twin Simulation' 툴바 확인"
echo "5. 'Material Twin' 버튼 클릭"
echo
