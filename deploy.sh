#!/bin/bash
# =============================================================
# MX Digital Twin Modeller - 전체 배포 스크립트
# =============================================================
# 사용:
#   bash deploy.sh            # SpaceClaim + Mechanical 모두 배포
#   bash deploy.sh spaceclaim # SpaceClaim AddIn만 빌드+배포
#   bash deploy.sh mechanical # Mechanical ACT Extension만 배포
#   bash deploy.sh --xml      # Mechanical XML 포함 배포 (Workbench 재시작 필요)

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"

MSBUILD="C:/Program Files (x86)/Microsoft Visual Studio/2019/BuildTools/MSBuild/Current/Bin/MSBuild.exe"
CSPROJ="$PROJECT_DIR/MXDigitalTwinModeller.csproj"

SC_OUTPUT="C:/ProgramData/SpaceClaim/AddIns/MXDigitalTwinModeller/V252"
EXT_ROOT="C:/Users/Sonic/AppData/Roaming/Ansys/v252/ACT/extensions"
CACHE_DIR="C:/Users/Sonic/AppData/Roaming/Ansys/v252/Applets/DSApplet/en-us"

DEPLOY_XML=false
[[ "$*" == *"--xml"* ]] && DEPLOY_XML=true

MODE="${1/--xml/}"
MODE="$(echo $MODE | xargs)"  # trim
[[ -z "$MODE" ]] && MODE="all"

# ---------------------------------------------------------------
deploy_spaceclaim() {
    echo ""
    echo "=== [1/2] SpaceClaim AddIn 빌드 + 배포 ==="

    if [ ! -f "$MSBUILD" ]; then
        echo "[ERROR] MSBuild를 찾을 수 없습니다: $MSBUILD"
        return 1
    fi

    echo "[빌드] MSBuild Debug..."
    "$MSBUILD" "$CSPROJ" /p:Configuration=Debug /p:Platform=AnyCPU /v:m /nologo
    if [ $? -ne 0 ]; then
        echo "[ERROR] 빌드 실패"
        return 1
    fi
    echo "[OK] 빌드 완료"

    # DLL이 출력 폴더에 있는지 확인 후 배포
    BUILD_OUT="$PROJECT_DIR/bin/Debug"
    if [ -f "$BUILD_OUT/MXDigitalTwinModeller.dll" ]; then
        mkdir -p "$SC_OUTPUT"
        cp "$BUILD_OUT/MXDigitalTwinModeller.dll" "$SC_OUTPUT/"
        cp "$BUILD_OUT/MXDigitalTwinModeller.Manifest.xml" "$SC_OUTPUT/" 2>/dev/null || true
        echo "[OK] SpaceClaim 배포 완료: $SC_OUTPUT"
    else
        echo "[ERROR] DLL을 찾을 수 없습니다: $BUILD_OUT"
        return 1
    fi
}

# ---------------------------------------------------------------
deploy_mechanical() {
    echo ""
    echo "=== [2/2] Mechanical ACT Extension 배포 ==="

    mkdir -p "$EXT_ROOT/MXSimulator/images"
    mkdir -p "$EXT_ROOT/MXSimulator/bin"

    # main.py (항상)
    cp "$PROJECT_DIR/Mechanical/MXSimulator/main.py" "$EXT_ROOT/MXSimulator/main.py"
    echo "[OK] main.py"

    # images (항상)
    cp -r "$PROJECT_DIR/Mechanical/MXSimulator/images/." "$EXT_ROOT/MXSimulator/images/"
    echo "[OK] images/"

    # Shared DLL (있으면)
    if [ -f "$PROJECT_DIR/bin/Debug/MXDigitalTwinModeller.Core.dll" ]; then
        cp "$PROJECT_DIR/bin/Debug/MXDigitalTwinModeller.Core.dll" "$EXT_ROOT/MXSimulator/bin/"
        echo "[OK] MXDigitalTwinModeller.Core.dll"
    fi

    if [ "$DEPLOY_XML" = true ]; then
        # MXSimulator.xml + 캐시 삭제 (버튼 구조 변경 시)
        cp "$PROJECT_DIR/Mechanical/MXSimulator.xml" "$EXT_ROOT/MXSimulator.xml"
        echo "[OK] MXSimulator.xml"

        for f in ExternalActions.xml ribbonLayout.xml RibbonState.xml; do
            if [ -f "$CACHE_DIR/$f" ]; then
                rm "$CACHE_DIR/$f"
                echo "[OK] 캐시 삭제: $f"
            fi
        done
        echo ""
        echo "※ XML 변경됨 → Workbench 완전 종료 후 재시작 필요"
    else
        echo ""
        echo "※ main.py만 변경됨 → Mechanical 재시작만으로 반영"
    fi
}

# ---------------------------------------------------------------
echo "============================================"
echo " MX Digital Twin Modeller 배포"
echo " 모드: $MODE $([ "$DEPLOY_XML" = true ] && echo "+ XML")"
echo "============================================"

case "$MODE" in
    spaceclaim)
        deploy_spaceclaim
        ;;
    mechanical)
        deploy_mechanical
        ;;
    all|"")
        deploy_spaceclaim && deploy_mechanical
        ;;
    *)
        echo "사용법: bash deploy.sh [spaceclaim|mechanical|all] [--xml]"
        exit 1
        ;;
esac

echo ""
echo "============================================"
echo " 배포 완료"
echo "============================================"
