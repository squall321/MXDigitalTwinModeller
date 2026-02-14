# encoding: utf-8
# ================================================================
# MX Digital Twin Modeller - 07. Bending Specimen (굽힘 시편 생성)
# SpaceClaim Script Editor에서 실행
# 3점 굽힘, 4점 굽힘, DMA 인장 시편 통합
# ================================================================

# ┌───────────────────────────────────────────────────────────────┐
# │  설정 (이 부분만 수정하세요)                                  │
# └───────────────────────────────────────────────────────────────┘

# 시험 모드: "3Point", "4Point", "DMATensile"
TEST_MODE = "3Point"

# ── 시편 타입 ──
# "Standard" 또는 "Custom"
SPECIMEN_TYPE = "Standard"

# ┌───────────────────────────────────────┐
# │  3점 굽힘 (ASTM D790 / ISO 178)     │
# └───────────────────────────────────────┘

BEND3_LENGTH = 80.0          # 시편 길이 (mm)
BEND3_WIDTH = 10.0           # 시편 폭 (mm)
BEND3_THICKNESS = 4.0        # 시편 두께 (mm)
BEND3_SPAN = 64.0            # 지지점 간격 (mm)
BEND3_SUPPORT_DIA = 8.0      # 하부 지지점 직경 (mm)
BEND3_NOSE_DIA = 8.0         # 상부 로딩노즈 직경 (mm)
BEND3_SUPPORT_HEIGHT = 20.0  # 지지점 높이 (mm)
BEND3_NOSE_HEIGHT = 20.0     # 로딩노즈 높이 (mm)

# ┌───────────────────────────────────────┐
# │  4점 굽힘 (ASTM C1161/D6272)        │
# └───────────────────────────────────────┘

BEND4_LENGTH = 100.0         # 시편 길이 (mm)
BEND4_WIDTH = 10.0           # 시편 폭 (mm)
BEND4_THICKNESS = 4.0        # 시편 두께 (mm)
BEND4_OUTER_SPAN = 80.0      # 외부 스팬 (mm)
BEND4_INNER_SPAN = 40.0      # 내부 스팬 (mm)
BEND4_SUPPORT_DIA = 8.0      # 하부 지지점 직경 (mm)
BEND4_NOSE_DIA = 8.0         # 상부 로딩노즈 직경 (mm)
BEND4_SUPPORT_HEIGHT = 20.0  # 지지점 높이 (mm)
BEND4_NOSE_HEIGHT = 20.0     # 로딩노즈 높이 (mm)

# ┌───────────────────────────────────────┐
# │  DMA 인장 (ASTM D4065 / ISO 6721)   │
# └───────────────────────────────────────┘

# 시편 형상: "Rectangle" 또는 "DogBone"
DMA_SHAPE = "Rectangle"

DMA_LENGTH = 50.0            # 전체 길이 (mm)
DMA_WIDTH = 10.0             # 폭 (mm)
DMA_THICKNESS = 3.0          # 두께 (mm)
DMA_GAUGE_LENGTH = 20.0      # 게이지 길이 (mm)
DMA_GRIP_LENGTH = 10.0       # 그립 길이 (mm)
DMA_GRIP_WIDTH = 15.0        # 그립 장비 폭 (mm)
DMA_GRIP_HEIGHT = 30.0       # 그립 장비 높이 (mm)
DMA_FILLET_RADIUS = 5.0      # 필렛 반경 (DogBone만)

# ┌───────────────────────────────────────────────────────────────┐
# │  실행 (아래는 수정 불필요)                                    │
# └───────────────────────────────────────────────────────────────┘

import clr
clr.AddReference("MXDigitalTwinModeller")

from SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.DMA import (
    DMA3PointBendingParameters, DMA4PointBendingParameters,
    DMATensileParameters, DMASpecimenType, DMASpecimenShape)
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.DMA import (
    DMA3PointBendingService, DMA4PointBendingService, DMATensileService)

SPEC_MAP = {"Standard": DMASpecimenType.Standard, "Custom": DMASpecimenType.Custom}
SHAPE_MAP = {"Rectangle": DMASpecimenShape.Rectangle, "DogBone": DMASpecimenShape.DogBone}

def run_3point(part):
    p = DMA3PointBendingParameters()
    p.SpecimenType = SPEC_MAP.get(SPECIMEN_TYPE, DMASpecimenType.Custom)
    p.Length = BEND3_LENGTH
    p.Width = BEND3_WIDTH
    p.Thickness = BEND3_THICKNESS
    p.Span = BEND3_SPAN
    p.SupportDiameter = BEND3_SUPPORT_DIA
    p.LoadingNoseDiameter = BEND3_NOSE_DIA
    p.SupportHeight = BEND3_SUPPORT_HEIGHT
    p.LoadingNoseHeight = BEND3_NOSE_HEIGHT

    ok, err = p.Validate()
    if not ok:
        print("[3Point] 유효성 오류: %s" % err)
        return

    print("[3Point] L=%.1f, W=%.1f, T=%.1f, Span=%.1f mm" % (
        BEND3_LENGTH, BEND3_WIDTH, BEND3_THICKNESS, BEND3_SPAN))
    print("[3Point] 생성 중...")

    service = DMA3PointBendingService()
    result = service.Create3PointBendingSpecimen(part, p)
    if result:
        print("[3Point] 완료: %s" % (result.Name or "Unnamed"))
    else:
        print("[3Point] 생성 실패")

def run_4point(part):
    p = DMA4PointBendingParameters()
    p.SpecimenType = SPEC_MAP.get(SPECIMEN_TYPE, DMASpecimenType.Custom)
    p.Length = BEND4_LENGTH
    p.Width = BEND4_WIDTH
    p.Thickness = BEND4_THICKNESS
    p.OuterSpan = BEND4_OUTER_SPAN
    p.InnerSpan = BEND4_INNER_SPAN
    p.SupportDiameter = BEND4_SUPPORT_DIA
    p.LoadingNoseDiameter = BEND4_NOSE_DIA
    p.SupportHeight = BEND4_SUPPORT_HEIGHT
    p.LoadingNoseHeight = BEND4_NOSE_HEIGHT

    ok, err = p.Validate()
    if not ok:
        print("[4Point] 유효성 오류: %s" % err)
        return

    print("[4Point] L=%.1f, W=%.1f, T=%.1f, OS=%.1f, IS=%.1f mm" % (
        BEND4_LENGTH, BEND4_WIDTH, BEND4_THICKNESS, BEND4_OUTER_SPAN, BEND4_INNER_SPAN))
    print("[4Point] 생성 중...")

    service = DMA4PointBendingService()
    result = service.Create4PointBendingSpecimen(part, p)
    if result:
        print("[4Point] 완료: %s" % (result.Name or "Unnamed"))
    else:
        print("[4Point] 생성 실패")

def run_dma_tensile(part):
    p = DMATensileParameters()
    p.SpecimenType = SPEC_MAP.get(SPECIMEN_TYPE, DMASpecimenType.Custom)
    p.Shape = SHAPE_MAP.get(DMA_SHAPE, DMASpecimenShape.Rectangle)
    p.Length = DMA_LENGTH
    p.Width = DMA_WIDTH
    p.Thickness = DMA_THICKNESS
    p.GaugeLength = DMA_GAUGE_LENGTH
    p.GripLength = DMA_GRIP_LENGTH
    p.GripWidth = DMA_GRIP_WIDTH
    p.GripHeight = DMA_GRIP_HEIGHT
    p.FilletRadius = DMA_FILLET_RADIUS

    ok, err = p.Validate()
    if not ok:
        print("[DMATensile] 유효성 오류: %s" % err)
        return

    print("[DMATensile] L=%.1f, W=%.1f, T=%.1f, GL=%.1f mm (%s)" % (
        DMA_LENGTH, DMA_WIDTH, DMA_THICKNESS, DMA_GAUGE_LENGTH, DMA_SHAPE))
    print("[DMATensile] 생성 중...")

    service = DMATensileService()
    result = service.CreateDMATensileSpecimen(part, p)
    if result:
        print("[DMATensile] 완료: %s" % (result.Name or "Unnamed"))
    else:
        print("[DMATensile] 생성 실패")

def main():
    part = Window.ActiveWindow.Document.MainPart

    if TEST_MODE == "3Point":
        run_3point(part)
    elif TEST_MODE == "4Point":
        run_4point(part)
    elif TEST_MODE == "DMATensile":
        run_dma_tensile(part)
    else:
        print("[Bending] 알 수 없는 모드: %s (3Point, 4Point, DMATensile)" % TEST_MODE)

    print("[Bending] 완료")

main()
