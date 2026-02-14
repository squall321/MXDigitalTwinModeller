# encoding: utf-8
# ================================================================
# MX Digital Twin Modeller - 12. Laminate Model (적층 모델 생성)
# SpaceClaim Script Editor에서 실행
# 3가지 모드: Rectangular (직사각형), Surface (면 기반), Solid (솔리드 기반)
# ================================================================

# ┌───────────────────────────────────────────────────────────────┐
# │  설정 (이 부분만 수정하세요)                                  │
# └───────────────────────────────────────────────────────────────┘

# 모드: "Rectangular", "Surface", "Solid"
MODE = "Rectangular"

# ── 레이어 정의 (모든 모드 공통) ──
# (레이어이름, 두께mm) 리스트
LAYERS = [
    ("CFRP_0", 0.125),
    ("CFRP_45", 0.125),
    ("CFRP_-45", 0.125),
    ("CFRP_90", 0.125),
    ("CFRP_90", 0.125),    # 이름 중복 시 오류 → 접미사 자동 추가
    ("CFRP_-45", 0.125),
    ("CFRP_45", 0.125),
    ("CFRP_0", 0.125),
]
# ※ 레이어 이름은 고유해야 합니다. 중복 시 자동으로 _2, _3 등 추가

# 인터페이스 Named Selection 생성 여부
CREATE_INTERFACE_NS = True

# ┌───────────────────────────────────────┐
# │  Rectangular 모드 설정               │
# └───────────────────────────────────────┘

RECT_WIDTH = 100.0        # 폭 (mm)
RECT_LENGTH = 100.0       # 길이 (mm)

# 적층 방향: "X", "Y", "Z"
RECT_DIRECTION = "Z"

# Share Topology 활성화
RECT_SHARE_TOPOLOGY = True

# ┌───────────────────────────────────────┐
# │  Surface 모드 설정                   │
# └───────────────────────────────────────┘

# 면 선택: SpaceClaim에서 면 1개를 선택한 상태에서 실행
# 오프셋 방향: "Normal" (법선) 또는 "Reverse" (역방향)
SURFACE_DIRECTION = "Normal"

# ┌───────────────────────────────────────┐
# │  Solid 모드 설정                     │
# └───────────────────────────────────────┘

# 바디 선택: 바디 키워드 (빈값=첫 번째 바디)
SOLID_BODY_KEYWORD = ""

# 원본 바디 삭제 여부
SOLID_DELETE_ORIGINAL = True

# ┌───────────────────────────────────────────────────────────────┐
# │  실행 (아래는 수정 불필요)                                    │
# └───────────────────────────────────────────────────────────────┘

import clr
clr.AddReference("MXDigitalTwinModeller")

from System.Collections.Generic import List
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Laminate import (
    RectangularLaminateParameters, SurfaceLaminateParameters,
    SolidLaminateParameters, LaminateLayerDefinition,
    StackingDirection, OffsetDirection)
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Laminate import (
    RectangularLaminateService, SurfaceLaminateService, SolidLaminateService)

DIR_MAP = {"X": StackingDirection.X, "Y": StackingDirection.Y, "Z": StackingDirection.Z}
OFFSET_MAP = {"Normal": OffsetDirection.Normal, "Reverse": OffsetDirection.Reverse}

def make_unique_layers():
    """레이어 이름 중복 방지"""
    seen = {}
    result = List[LaminateLayerDefinition]()
    for name, thick in LAYERS:
        if name in seen:
            seen[name] += 1
            unique_name = "%s_%d" % (name, seen[name])
        else:
            seen[name] = 1
            unique_name = name
        result.Add(LaminateLayerDefinition(unique_name, thick))
    return result

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

def run_rectangular(part):
    layers = make_unique_layers()
    p = RectangularLaminateParameters()
    p.WidthMm = RECT_WIDTH
    p.LengthMm = RECT_LENGTH
    p.Direction = DIR_MAP.get(RECT_DIRECTION, StackingDirection.Z)
    p.Layers = layers
    p.EnableShareTopology = RECT_SHARE_TOPOLOGY
    p.CreateInterfaceNamedSelections = CREATE_INTERFACE_NS

    ok, err = p.Validate()
    if not ok:
        print("[Laminate] 유효성 오류: %s" % err)
        return

    total = sum(t for _, t in LAYERS)
    print("[Laminate] Rectangular %.1f×%.1f mm, %d layers (%.3f mm)" % (
        RECT_WIDTH, RECT_LENGTH, len(LAYERS), total))
    print("[Laminate] 생성 중...")

    service = RectangularLaminateService()
    bodies = service.CreateRectangularLaminate(part, p)

    if bodies and bodies.Count > 0:
        print("[Laminate] 완료: %d개 레이어 바디" % bodies.Count)
    else:
        print("[Laminate] 생성 실패")

def run_surface(part):
    # 선택된 면 가져오기
    sel = Window.ActiveWindow.ActiveContext.Selection
    face = None
    for item in sel:
        try:
            f = item.GetGeometry()
            if hasattr(item, 'GetFaces'):
                faces = item.GetFaces()
                if faces and faces.Count > 0:
                    face = faces[0]
                    break
        except:
            pass

    # DesignFace 직접 추출 시도
    if face is None:
        try:
            from SpaceClaim.Api.V252 import DesignFace
            for item in sel:
                if isinstance(item, DesignFace):
                    face = item
                    break
        except:
            pass

    if face is None:
        print("[Laminate] 면을 선택한 상태에서 실행하세요.")
        return

    layers = make_unique_layers()
    p = SurfaceLaminateParameters()
    p.Direction = OFFSET_MAP.get(SURFACE_DIRECTION, OffsetDirection.Normal)
    p.Layers = layers
    p.CreateInterfaceNamedSelections = CREATE_INTERFACE_NS

    ok, err = p.Validate()
    if not ok:
        print("[Laminate] 유효성 오류: %s" % err)
        return

    total = sum(t for _, t in LAYERS)
    print("[Laminate] Surface 모드, %d layers (%.3f mm), 방향: %s" % (
        len(LAYERS), total, SURFACE_DIRECTION))
    print("[Laminate] 생성 중...")

    service = SurfaceLaminateService()
    bodies = service.CreateSurfaceLaminate(part, face, p)

    if bodies and bodies.Count > 0:
        print("[Laminate] 완료: %d개 레이어 바디" % bodies.Count)
    else:
        print("[Laminate] 생성 실패")

def run_solid(part):
    bodies = get_filtered_bodies(part, SOLID_BODY_KEYWORD)
    if not bodies:
        print("[Laminate] 바디를 찾을 수 없습니다. (키워드: '%s')" % SOLID_BODY_KEYWORD)
        return

    target = bodies[0]
    print("[Laminate] 대상 바디: %s" % (target.Name or "Unnamed"))

    service = SolidLaminateService()
    analysis = service.AnalyzeSolid(target)

    if not analysis.IsValid:
        print("[Laminate] 솔리드 분석 실패: %s" % analysis.ErrorMessage)
        return

    print("[Laminate] 두께=%.3f mm, 법선=(%s)" % (
        analysis.ThicknessMm, analysis.StackingNormal))

    layers = make_unique_layers()
    p = SolidLaminateParameters()
    p.Layers = layers
    p.CreateInterfaceNamedSelections = CREATE_INTERFACE_NS
    p.DeleteOriginalBody = SOLID_DELETE_ORIGINAL

    ok, err = p.Validate()
    if not ok:
        print("[Laminate] 유효성 오류: %s" % err)
        return

    total = sum(t for _, t in LAYERS)
    print("[Laminate] Solid 모드, %d layers (%.3f mm)" % (len(LAYERS), total))
    print("[Laminate] 생성 중...")

    result = service.CreateSolidLaminate(part, target, analysis, p)

    if result and result.Count > 0:
        print("[Laminate] 완료: %d개 레이어 바디" % result.Count)
    else:
        print("[Laminate] 생성 실패")

def main():
    part = Window.ActiveWindow.Document.MainPart

    if MODE == "Rectangular":
        run_rectangular(part)
    elif MODE == "Surface":
        run_surface(part)
    elif MODE == "Solid":
        run_solid(part)
    else:
        print("[Laminate] 알 수 없는 모드: %s (Rectangular, Surface, Solid)" % MODE)

main()
