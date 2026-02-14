# encoding: utf-8
# ================================================================
# MX Digital Twin Modeller - 16. Conformal Mesh from STEP
# SpaceClaim Script Editor에서 실행
# 대규모 STEP 어셈블리의 계면 검출 + Share Topology + Conformal Mesh
# ================================================================

# ┌───────────────────────────────────────────────────────────────┐
# │  설정 (이 부분만 수정하세요)                                  │
# └───────────────────────────────────────────────────────────────┘

# ── STEP 파일 ──
# 임포트 모드: "Current" (현재 파트 사용), "Open" (새 문서), "Insert" (컴포넌트 삽입)
IMPORT_MODE = "Current"
STEP_FILE = r""           # "Current" 모드에서는 빈 값 가능

# ── 계면 검출 ──
TOLERANCE_MM = 1.0         # 면 간격 허용치 (mm)
DETECT_PLANAR = True       # 평면 접촉 검출
DETECT_CYLINDRICAL = True  # 원통 접촉 검출
BODY_KEYWORD = ""          # 바디 필터 키워드 (빈값=전체)

# ── Share Topology ──
SHARE_TOPOLOGY = True                # Share Topology 활성화
CREATE_INTERFACE_NS = True           # 인터페이스 Named Selection 생성

# ── 메쉬 설정 ──
ELEMENT_SIZE_MM = 2.0      # 요소 크기 (mm)
MESH_STRATEGY = "Tet"      # "Tet" (사면체), "Hex" (육면체), "Mixed" (혼합)
GROWTH_RATE = 1.8          # 성장률 (1.0~5.0)
MIDSIDE_NODES = False      # 중간절점 유지
CURVATURE_PROXIMITY = True # 곡률+근접 크기 함수

# ── 실린더 처리 ──
SPLIT_CYLINDER_EDGES = False  # 원형 엣지 분할
CYLINDER_DIVISIONS = 8       # 원 분할 수 (기본 8)

# ── 내보내기 ──
AUTO_EXPORT = False        # 자동 내보내기
EXPORT_PATH = r""          # 내보내기 경로
EXPORT_FORMAT = "LS-DYNA"  # "LS-DYNA" 또는 "ANSYS"

# ┌───────────────────────────────────────────────────────────────┐
# │  실행 (아래는 수정 불필요)                                    │
# └───────────────────────────────────────────────────────────────┘

import clr
clr.AddReference("MXDigitalTwinModeller")

from SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ConformalMesh import (
    ConformalMeshParameters, StepImportMode, MeshStrategy)
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ConformalMesh import (
    ConformalMeshService)

MODE_MAP = {
    "Current": StepImportMode.UseCurrentPart,
    "Open": StepImportMode.OpenNew,
    "Insert": StepImportMode.InsertIntoCurrent,
}

STRATEGY_MAP = {
    "Tet": MeshStrategy.AutoTet,
    "Hex": MeshStrategy.AutoHex,
    "Mixed": MeshStrategy.Mixed,
}

def main():
    # 파라미터 생성
    p = ConformalMeshParameters()
    p.StepFilePath = STEP_FILE
    p.ImportMode = MODE_MAP.get(IMPORT_MODE, StepImportMode.UseCurrentPart)
    p.ToleranceMm = TOLERANCE_MM
    p.DetectPlanar = DETECT_PLANAR
    p.DetectCylindrical = DETECT_CYLINDRICAL
    p.BodyKeyword = BODY_KEYWORD
    p.EnableShareTopology = SHARE_TOPOLOGY
    p.CreateInterfaceNamedSelections = CREATE_INTERFACE_NS
    p.ElementSizeMm = ELEMENT_SIZE_MM
    p.Strategy = STRATEGY_MAP.get(MESH_STRATEGY, MeshStrategy.AutoTet)
    p.GrowthRate = GROWTH_RATE
    p.MidsideNodes = MIDSIDE_NODES
    p.UseCurvatureProximity = CURVATURE_PROXIMITY
    p.SplitCylinderEdges = SPLIT_CYLINDER_EDGES
    p.CylinderEdgeDivisions = CYLINDER_DIVISIONS
    p.AutoExport = AUTO_EXPORT
    p.ExportPath = EXPORT_PATH
    p.ExportFormat = EXPORT_FORMAT

    # 유효성 검증
    ok, err = p.Validate()
    if not ok:
        print("[ConformalMesh] 유효성 오류: %s" % err)
        return

    part = Window.ActiveWindow.Document.MainPart

    # STEP 임포트
    if IMPORT_MODE != "Current":
        print("[ConformalMesh] STEP 임포트: %s (%s)" % (STEP_FILE, IMPORT_MODE))
        part = ConformalMeshService.ImportStep(STEP_FILE, p.ImportMode)
        if part is None:
            print("[ConformalMesh] STEP 임포트 실패")
            return

    # 바디 수집
    bodies = ConformalMeshService.CollectBodies(part, BODY_KEYWORD)
    print("[ConformalMesh] 바디 수: %d" % bodies.Count)

    if bodies.Count < 2:
        print("[ConformalMesh] 바디가 2개 이상이어야 계면 검출이 가능합니다.")
        return

    # 계면 검출
    print("[ConformalMesh] 계면 검출 중... (허용치=%.1f mm)" % TOLERANCE_MM)
    interfaces = ConformalMeshService.DetectInterfaces(
        bodies, TOLERANCE_MM, DETECT_PLANAR, DETECT_CYLINDRICAL)
    print("[ConformalMesh] 검출된 계면: %d개" % interfaces.Count)

    if interfaces.Count == 0:
        print("[ConformalMesh] 계면이 검출되지 않았습니다.")
        return

    # 계면 요약
    planar_count = 0
    cyl_count = 0
    total_area = 0.0
    for iface in interfaces:
        if str(iface.Type) == "Planar":
            planar_count += 1
        elif str(iface.Type) == "Cylindrical":
            cyl_count += 1
        total_area += iface.TotalAreaMm2

    print("[ConformalMesh] 평면: %d, 원통: %d, 총 면적: %.1f mm²" % (
        planar_count, cyl_count, total_area))

    # Named Selection 생성
    if CREATE_INTERFACE_NS:
        print("[ConformalMesh] Named Selection 생성 중...")
        ConformalMeshService.CreateInterfaceNS(part, interfaces)

    # Share Topology
    if SHARE_TOPOLOGY:
        print("[ConformalMesh] Share Topology 활성화...")
        ConformalMeshService.EnableShareTopology(part)

    # 실린더 엣지 분할
    if SPLIT_CYLINDER_EDGES:
        print("[ConformalMesh] 실린더 엣지 분할 (%d분할)..." % CYLINDER_DIVISIONS)
        split_count = ConformalMeshService.SplitCylinderEdges(bodies, CYLINDER_DIVISIONS)
        print("[ConformalMesh] 분할 완료: %d개 엣지" % split_count)

    # 메쉬 생성
    print("[ConformalMesh] Conformal Mesh 생성 중... (크기=%.1f mm, 전략=%s)" % (
        ELEMENT_SIZE_MM, MESH_STRATEGY))
    ok = ConformalMeshService.GenerateConformalMesh(part, bodies, p)

    if ok:
        print("[ConformalMesh] 메쉬 생성 완료")
    else:
        print("[ConformalMesh] 메쉬 생성 실패")
        return

    # 내보내기
    if AUTO_EXPORT and EXPORT_PATH:
        print("[ConformalMesh] 내보내기: %s (%s)" % (EXPORT_PATH, EXPORT_FORMAT))
        ConformalMeshService.ExportMesh(EXPORT_PATH, EXPORT_FORMAT)
        print("[ConformalMesh] 내보내기 완료")

    # 진단 로그 출력
    log = ConformalMeshService.DiagnosticLog
    if log and log.Count > 0:
        print("\n[ConformalMesh] === 진단 로그 ===")
        for entry in log:
            print("  %s" % entry)

    print("[ConformalMesh] 완료")

main()
