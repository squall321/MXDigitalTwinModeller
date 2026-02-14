# encoding: utf-8
# ================================================================
# MX Digital Twin Modeller - 04. Mesh (메쉬 생성)
# SpaceClaim Script Editor에서 실행
# ================================================================

# ┌───────────────────────────────────────────────────────────────┐
# │  설정 (이 부분만 수정하세요)                                  │
# └───────────────────────────────────────────────────────────────┘

# 바디 선택
KEYWORD = ""              # 바디 이름 필터 (빈값=전체 바디)

# 메쉬 크기
ELEMENT_SIZE_MM = 2.0     # 요소 크기 (mm)

# 요소 형상: "Tet" (사면체), "Hex" (육면체), "Quad" (사각 우세), "Tri" (삼각형)
SHAPE = "Tet"

# 중간 절점: "Dropped" (제거), "Kept" (유지), "Auto" (자동)
MIDSIDE = "Dropped"

# 성장률 (1.0 ~ 5.0)
GROWTH_RATE = 1.8

# 크기 함수: "Fixed" (고정), "Curv" (곡률), "Prox" (근접), "Curv+Prox" (곡률+근접)
SIZE_FUNCTION = "Fixed"

# 메쉬 방법: "Prime" (고품질 Tet), "Auto" (자동)
MESH_METHOD = "Prime"

# 바디별 개별 설정 (선택사항, 이 설정이 있으면 위 전역 설정 대신 사용)
# (바디키워드, 크기mm, 형상, 중간절점, 성장률, 크기함수)
PER_BODY_SETTINGS = [
    # ("Specimen", 1.0, "Tet", "Dropped", 1.5, "Fixed"),
    # ("Fixture", 3.0, "Tet", "Dropped", 2.0, "Fixed"),
]

# ┌───────────────────────────────────────────────────────────────┐
# │  실행 (아래는 수정 불필요)                                    │
# └───────────────────────────────────────────────────────────────┘

from SpaceClaim.Api.V252.Scripting.Commands import *
from SpaceClaim.Api.V252.Scripting.Commands.CommandOptions import *
from SpaceClaim.Api.V252.Scripting.Selection import Selection, BodySelection

SHAPE_MAP = {
    "Tet": ElementShapeType.Tetrahedral,
    "Hex": ElementShapeType.Hexahedral,
    "Quad": ElementShapeType.QuadDominant,
    "Tri": ElementShapeType.Triangle,
}

MIDSIDE_MAP = {
    "Dropped": MidsideNodesType.Dropped,
    "Kept": MidsideNodesType.Kept,
    "Auto": MidsideNodesType.BasedOnPhysics,
}

SIZEFUNC_MAP = {
    "Fixed": SizeFunctionType.Fixed,
    "Curv": SizeFunctionType.Curvature,
    "Prox": SizeFunctionType.Proximity,
    "Curv+Prox": SizeFunctionType.CurvatureAndProximity,
}

def get_filtered_bodies(part, keyword=""):
    bodies = []
    _collect(part, bodies)
    if not keyword:
        return bodies
    kw = keyword.lower()
    return [b for b in bodies if kw in (b.Name or "").lower()]

def _collect(part, result):
    for b in part.Bodies:
        result.append(b)
    for c in part.Components:
        try:
            if c.Content:
                _collect(c.Content, result)
        except:
            pass

def create_mesh_group(bodies, size_mm, shape, midside, growth, size_func, label=""):
    if len(bodies) == 0:
        return

    elem_m = size_mm / 1000.0
    shape_enum = SHAPE_MAP.get(shape, ElementShapeType.Tetrahedral)
    mid_enum = MIDSIDE_MAP.get(midside, MidsideNodesType.Dropped)
    sf_enum = SIZEFUNC_MAP.get(size_func, SizeFunctionType.Fixed)

    opts = CreateMeshOptions()
    opts.ElementSize = elem_m
    opts.SolidElementShape = shape_enum
    opts.MidsideNodes = mid_enum
    opts.GrowthRate = growth
    opts.SizeFunctionType = sf_enum
    if shape_enum == ElementShapeType.Tetrahedral and MESH_METHOD == "Prime":
        opts.MeshMethod = MeshMethod.Prime

    names = ", ".join([(b.Name or "Unnamed") for b in bodies[:5]])
    if len(bodies) > 5:
        names += " ... (+%d)" % (len(bodies) - 5)
    print("  [Mesh] %s %d개 바디, %.2fmm %s %s: %s" % (
        label, len(bodies), size_mm, shape, size_func, names))

    from System import Array
    body_arr = Array[DesignBody](bodies)
    body_sel = BodySelection.Create(body_arr)
    empty_sel = Selection.Empty()

    try:
        result = CreateMesh.Execute(body_sel, empty_sel, opts, None)
        print("    성공=%s" % result.Success)
    except Exception as ex:
        print("    [WARN] 배치 실패, 개별 메싱 시도...")
        for b in bodies:
            try:
                single_sel = BodySelection.Create(b)
                r = CreateMesh.Execute(single_sel, empty_sel, opts, None)
                print("    %s: %s" % (b.Name or "Unnamed", r.Success))
            except Exception as ex2:
                print("    [FAIL] %s: %s" % (b.Name or "Unnamed", str(ex2)))

def main():
    part = Window.ActiveWindow.Document.MainPart

    # Step 1: InitMeshSettings
    print("[Mesh] 초기화 (Structural)...")
    InitMeshSettings.Execute(PhysicsType.Structural, None)

    # 바디별 개별 설정 있는 경우
    if PER_BODY_SETTINGS:
        print("[Mesh] 바디별 개별 설정 모드 (%d개 규칙)" % len(PER_BODY_SETTINGS))
        handled = set()

        for rule in PER_BODY_SETTINGS:
            kw, size, shape, mid, growth, sf = rule
            bodies = get_filtered_bodies(part, kw)
            bodies = [b for b in bodies if id(b) not in handled]
            for b in bodies:
                handled.add(id(b))
            if bodies:
                create_mesh_group(bodies, size, shape, mid, growth, sf, "'%s'" % kw)

        # 나머지 바디 (전역 설정 적용)
        all_bodies = get_filtered_bodies(part, "")
        remaining = [b for b in all_bodies if id(b) not in handled]
        if remaining:
            print("[Mesh] 나머지 %d개 바디 → 전역 설정" % len(remaining))
            create_mesh_group(remaining, ELEMENT_SIZE_MM, SHAPE, MIDSIDE,
                              GROWTH_RATE, SIZE_FUNCTION, "전역")
    else:
        # 전역 설정으로 전체 메싱
        bodies = get_filtered_bodies(part, KEYWORD)
        if len(bodies) == 0:
            print("[Mesh] 바디가 없습니다.")
            return

        print("[Mesh] %d개 바디 메싱 시작" % len(bodies))
        create_mesh_group(bodies, ELEMENT_SIZE_MM, SHAPE, MIDSIDE,
                          GROWTH_RATE, SIZE_FUNCTION, "전체")

    print("[Mesh] 완료")

main()
