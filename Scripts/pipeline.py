# encoding: utf-8
# ================================================================
# MX Digital Twin Modeller - Pipeline (전체 파이프라인)
# SpaceClaim Script Editor에서 실행
#
# 단순화 → 재료 → 접촉감지 → 메쉬 → 내보내기 순서로 자동 실행
# 각 단계를 ENABLE/DISABLE로 켜고 끌 수 있음
# ================================================================

# ┌───────────────────────────────────────────────────────────────┐
# │  전역 설정                                                    │
# └───────────────────────────────────────────────────────────────┘

CONTINUE_ON_ERROR = True     # True: 오류 발생해도 다음 단계 진행

# ┌───────────────────────────────────────────────────────────────┐
# │  Step 1: 단순화 (Simplify)                                   │
# └───────────────────────────────────────────────────────────────┘

STEP1_ENABLE = False          # True/False

# (키워드, 모드) 리스트 - 모드: "BoundingBox" 또는 "SolidToShell"
STEP1_RULES = [
    ("Bolt", "BoundingBox"),
    # ("Washer", "SolidToShell"),
]

# ┌───────────────────────────────────────────────────────────────┐
# │  Step 2: 재료 적용 (Material)                                │
# └───────────────────────────────────────────────────────────────┘

STEP2_ENABLE = True

# (바디키워드, 프리셋, 재료이름) - 프리셋: "Steel"/"Aluminum"/"CFRP"
# 바디키워드 빈값이면 전체 바디에 적용
STEP2_MATERIALS = [
    ("", "Steel", "Steel"),
    # ("Specimen", "CFRP", "CFRP_Specimen"),
]

# Custom: (바디키워드, "Custom", 이름, 밀도, E, nu, G, sigma, CTE, k, Cp)
STEP2_CUSTOM = [
    # ("Body", "Custom", "Ti6Al4V", 4.43e-9, 116000, 0.34, 43280, 900, 8.6e-6, 21.9, 5.2e8),
]

# ┌───────────────────────────────────────────────────────────────┐
# │  Step 3: 접촉 감지 (Contact Detection)                      │
# └───────────────────────────────────────────────────────────────┘

STEP3_ENABLE = True

STEP3_KEYWORD_A = ""         # 키워드 A (빈값=전체 모드)
STEP3_KEYWORD_B = ""         # 키워드 B
STEP3_TOLERANCE_MM = 1.0     # 허용 거리 (mm)
STEP3_AUTO_PREFIX = True     # KEYWORD_A 기반 접두사 자동 지정
STEP3_CREATE_NS = True       # Named Selection 자동 생성

# ┌───────────────────────────────────────────────────────────────┐
# │  Step 4: 메쉬 생성 (Mesh)                                   │
# └───────────────────────────────────────────────────────────────┘

STEP4_ENABLE = True

STEP4_KEYWORD = ""            # 바디 필터 (빈값=전체)
STEP4_SIZE_MM = 2.0           # 요소 크기 (mm)
STEP4_SHAPE = "Tet"           # Tet, Hex, Quad, Tri
STEP4_MIDSIDE = "Dropped"     # Dropped, Kept, Auto
STEP4_GROWTH = 1.8            # 성장률 (1.0~5.0)
STEP4_SIZE_FUNC = "Fixed"     # Fixed, Curv, Prox, Curv+Prox

# 바디별 개별 설정 (선택사항)
# (바디키워드, 크기mm, 형상, 중간절점, 성장률, 크기함수)
STEP4_PER_BODY = [
    # ("Specimen", 1.0, "Tet", "Dropped", 1.5, "Fixed"),
    # ("Fixture",  3.0, "Tet", "Dropped", 2.0, "Fixed"),
]

# ┌───────────────────────────────────────────────────────────────┐
# │  Step 5: 내보내기 (Export)                                   │
# └───────────────────────────────────────────────────────────────┘

STEP5_ENABLE = True

STEP5_FORMAT = "LS-DYNA"      # LS-DYNA, ANSYS, Abaqus, Fluent, CGNS
STEP5_PATH = r"C:\Users\Sonic\Desktop\model.k"
STEP5_PATCH_MATERIALS = True  # LS-DYNA 물성 교체
STEP5_APPEND_CONTROL = True   # LS-DYNA 제어카드

STEP5_EXPORT_STEP = False     # STEP 파일도 동시 내보내기
STEP5_STEP_PATH = r"C:\Users\Sonic\Desktop\model.stp"

# ┌───────────────────────────────────────────────────────────────┐
# │  실행 (아래는 수정 불필요)                                    │
# └───────────────────────────────────────────────────────────────┘

import clr
clr.AddReference("MXDigitalTwinModeller")

import time
from System import Array
from System.IO import File, FileInfo
from System.Collections.Generic import List
from SpaceClaim.Api.V252.Scripting.Commands import *
from SpaceClaim.Api.V252.Scripting.Commands.CommandOptions import *
from SpaceClaim.Api.V252.Scripting.Selection import Selection, BodySelection
from SpaceClaim.Api.V252.Analysis import MeshMethods
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Simplify import SimplifyRule, SimplifyMode
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Simplify import SimplifyService
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Material import MaterialService
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Contact import ContactDetectionService
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Export import KFilePostProcessor

# ─── 공통 유틸 ───

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

def get_preset(name):
    if name == "Aluminum":
        return MaterialService.GetAluminumDefaults()
    elif name == "CFRP":
        return MaterialService.GetCFRPDefaults()
    return MaterialService.GetSteelDefaults()

# ─── Step 실행 함수 ───

def run_step1(part):
    """단순화"""
    rules = List[SimplifyRule]()
    for kw, mode_str in STEP1_RULES:
        if not kw:
            continue
        mode = SimplifyMode.SolidToShell if mode_str == "SolidToShell" else SimplifyMode.BoundingBox
        rules.Add(SimplifyRule(kw, mode))

    if rules.Count == 0:
        print("  규칙 없음 - 스킵")
        return

    result = SimplifyService.ExecuteBatch(part, rules)
    print("  매칭: %d, 처리: %d, 실패: %d" % (
        result.MatchedCount, result.ProcessedCount, result.FailedCount))

def run_step2(part):
    """재료 적용"""
    entries = []
    for item in STEP2_MATERIALS:
        kw, preset, name = item[0], item[1], item[2]
        vals = get_preset(preset)
        entries.append((kw, name, vals[0], vals[1], vals[2], vals[3],
                        vals[4], vals[5], vals[6], vals[7]))
    for item in STEP2_CUSTOM:
        entries.append((item[0], item[2], item[3], item[4], item[5], item[6],
                        item[7], item[8], item[9], item[10]))

    for entry in entries:
        kw, name = entry[0], entry[1]
        bodies = get_filtered_bodies(part, kw)
        if not bodies:
            print("  '%s' 키워드 매칭 바디 없음" % kw)
            continue

        net_bodies = List[DesignBody]()
        for b in bodies:
            net_bodies.Add(b)
        log = List[str]()

        MaterialService.ApplyMaterial(part, name,
            entry[2], entry[3], entry[4], entry[5],
            entry[6], entry[7], entry[8], entry[9],
            net_bodies, log)
        print("  '%s' → %d개 바디" % (name, len(bodies)))

def run_step3(part):
    """접촉 감지"""
    pairs = ContactDetectionService.DetectContacts(
        part, STEP3_KEYWORD_A, STEP3_KEYWORD_B, STEP3_TOLERANCE_MM)
    print("  %d개 페어 감지" % pairs.Count)

    if STEP3_AUTO_PREFIX and STEP3_KEYWORD_A:
        ContactDetectionService.AssignPrefixes(pairs, STEP3_KEYWORD_A)

    ContactDetectionService.MarkExistingPairs(part, pairs)
    new_count = sum(1 for p in pairs if not p.IsExisting)

    if STEP3_CREATE_NS and new_count > 0:
        ContactDetectionService.CreateNamedSelections(part, pairs)
        print("  %d개 NS 생성" % new_count)
    else:
        print("  NS 생성 스킵 (신규: %d)" % new_count)

def run_step4(part):
    """메쉬 생성"""
    InitMeshSettings.Execute(PhysicsType.Structural, None)

    if STEP4_PER_BODY:
        handled = set()
        for rule in STEP4_PER_BODY:
            kw, sz, sh, mid, gr, sf = rule
            bodies = get_filtered_bodies(part, kw)
            bodies = [b for b in bodies if id(b) not in handled]
            for b in bodies:
                handled.add(id(b))
            if bodies:
                _mesh_group(bodies, sz, sh, mid, gr, sf, "'%s'" % kw)

        remaining = [b for b in get_filtered_bodies(part, "") if id(b) not in handled]
        if remaining:
            _mesh_group(remaining, STEP4_SIZE_MM, STEP4_SHAPE, STEP4_MIDSIDE,
                        STEP4_GROWTH, STEP4_SIZE_FUNC, "나머지")
    else:
        bodies = get_filtered_bodies(part, STEP4_KEYWORD)
        if not bodies:
            print("  바디 없음")
            return
        _mesh_group(bodies, STEP4_SIZE_MM, STEP4_SHAPE, STEP4_MIDSIDE,
                    STEP4_GROWTH, STEP4_SIZE_FUNC, "전체")

def _mesh_group(bodies, size_mm, shape, midside, growth, sf, label):
    opts = CreateMeshOptions()
    opts.ElementSize = size_mm / 1000.0
    opts.SolidElementShape = SHAPE_MAP.get(shape, ElementShapeType.Tetrahedral)
    opts.MidsideNodes = MIDSIDE_MAP.get(midside, MidsideNodesType.Dropped)
    opts.GrowthRate = growth
    opts.SizeFunctionType = SIZEFUNC_MAP.get(sf, SizeFunctionType.Fixed)
    if opts.SolidElementShape == ElementShapeType.Tetrahedral:
        opts.MeshMethod = MeshMethod.Prime

    body_sel = BodySelection.Create(Array[DesignBody](bodies))
    empty_sel = Selection.Empty()

    ok = False
    try:
        result = CreateMesh.Execute(body_sel, empty_sel, opts, None)
        ok = result.Success
    except:
        pass

    if ok:
        print("  %s: %d개 바디 %.2fmm %s [OK]" % (label, len(bodies), size_mm, shape))
    else:
        print("  %s: 배치 실패 → 개별 폴백" % label)
        for b in bodies:
            try:
                r = CreateMesh.Execute(BodySelection.Create(b), empty_sel, opts, None)
                print("    %s: %s" % (b.Name or "Unnamed", r.Success))
            except Exception as ex2:
                print("    %s: FAIL - %s" % (b.Name or "Unnamed", str(ex2)))

def run_step5(part):
    """내보내기"""
    fmt = STEP5_FORMAT
    path = STEP5_PATH

    if fmt == "LS-DYNA":
        MeshMethods.SaveDYNA(path)
    elif fmt == "ANSYS":
        MeshMethods.SaveANSYS(path)
    elif fmt == "Abaqus":
        MeshMethods.SaveAbaqus(path)
    elif fmt == "Fluent":
        MeshMethods.SaveFluentMesh(path)
    elif fmt == "CGNS":
        MeshMethods.SaveCGNS(path)

    if File.Exists(path):
        info = FileInfo(path)
        if info.Length < 1024 * 1024:
            print("  %s → %.1f KB" % (fmt, info.Length / 1024.0))
        else:
            print("  %s → %.1f MB" % (fmt, info.Length / (1024.0 * 1024.0)))
    else:
        print("  [WARN] 파일 미생성 (메쉬 확인)")

    # LS-DYNA 후처리
    if fmt == "LS-DYNA" and File.Exists(path):
        mat_log = List[str]()
        if STEP5_PATCH_MATERIALS:
            try:
                n = KFilePostProcessor.PatchMaterials(path, part, mat_log)
                print("  물성 교체: %d개" % n)
            except Exception as ex:
                print("  물성 교체 실패: %s" % str(ex))
        if STEP5_APPEND_CONTROL:
            try:
                n = KFilePostProcessor.AppendControlCards(path, mat_log)
                print("  제어카드: %d개 블록" % n)
            except Exception as ex:
                print("  제어카드 실패: %s" % str(ex))

    # STEP 내보내기
    if STEP5_EXPORT_STEP:
        try:
            part.Export(PartExportFormat.Step, STEP5_STEP_PATH, True, None)
            print("  STEP 내보내기: %s" % STEP5_STEP_PATH)
        except Exception as ex:
            print("  STEP 내보내기 실패: %s" % str(ex))

# ─── 메인 ───

def main():
    part = Window.ActiveWindow.Document.MainPart
    start = time.time()

    steps = []
    if STEP1_ENABLE: steps.append(("1. 단순화", run_step1))
    if STEP2_ENABLE: steps.append(("2. 재료", run_step2))
    if STEP3_ENABLE: steps.append(("3. 접촉감지", run_step3))
    if STEP4_ENABLE: steps.append(("4. 메쉬", run_step4))
    if STEP5_ENABLE: steps.append(("5. 내보내기", run_step5))

    if not steps:
        print("[Pipeline] 활성화된 단계 없음")
        return

    print("=" * 50)
    print("[Pipeline] %d개 단계 실행" % len(steps))
    print("=" * 50)

    success = 0
    fail = 0

    for i, (name, func) in enumerate(steps):
        print("")
        print("--- [%d/%d] %s ---" % (i + 1, len(steps), name))
        try:
            func(part)
            success += 1
            print("  [OK]")
        except Exception as ex:
            fail += 1
            print("  [FAIL] %s" % str(ex))
            if not CONTINUE_ON_ERROR:
                print("[Pipeline] 오류로 중단")
                break

    elapsed = time.time() - start
    print("")
    print("=" * 50)
    print("[Pipeline] 완료: 성공 %d, 실패 %d (%.1f초)" % (success, fail, elapsed))
    print("=" * 50)

main()
