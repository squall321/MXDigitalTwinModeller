# encoding: utf-8
# ================================================================
# MX Digital Twin Modeller - 10. Fatigue Specimen (피로 시편 생성)
# SpaceClaim Script Editor에서 실행
# ================================================================

# ┌───────────────────────────────────────────────────────────────┐
# │  설정 (이 부분만 수정하세요)                                  │
# └───────────────────────────────────────────────────────────────┘

# 시편 규격 타입:
#   "E466_Uniform"    - ASTM E466 균일 게이지 (HCF)
#   "E466_Hourglass"  - ASTM E466 모래시계 (HCF)
#   "E606"            - ASTM E606 원형 단면 (LCF)
#   "E647_CT"         - ASTM E647 Compact Tension (균열성장)
#   "E647_MT"         - ASTM E647 Middle Tension (균열성장)
#   "E2207"           - ASTM E2207 박벽 튜브 (다축피로)
#   "Custom"          - 사용자 정의
SPECIMEN_TYPE = "E466_Uniform"

# 단면 형상: "Rectangular", "Circular", "Tubular"
SECTION_SHAPE = "Rectangular"

# ── 공통 치수 (E466/E606) ──
GAUGE_LENGTH = 75.0       # 게이지 길이 (mm)
GAUGE_WIDTH = 12.5        # 게이지 폭 (mm, 직사각형)
THICKNESS = 6.0           # 두께 (mm, 직사각형)
GAUGE_DIAMETER = 6.35     # 게이지 직경 (mm, 원형 E606)
GRIP_WIDTH = 20.0         # 그립 폭 (mm)
GRIP_LENGTH = 50.0        # 그립 길이 (mm)
TOTAL_LENGTH = 200.0      # 전체 길이 (mm)
FILLET_RADIUS = 50.0      # 필렛 반경 (mm)
HOURGLASS_RADIUS = 100.0  # 모래시계 반경 (mm, E466_Hourglass)

# ── CT 시편 (E647_CT) ──
CT_WIDTH = 50.0           # CT 폭 W (mm)
CT_THICKNESS = 12.5       # CT 두께 B (mm)
INITIAL_CRACK = 25.0      # 초기 균열 길이 a0 (mm)
PIN_HOLE_DIA = 12.5       # 핀홀 직경 (mm)
NOTCH_WIDTH = 1.0         # 노치 폭 (mm)

# ── MT 시편 (E647_MT) ──
MT_WIDTH = 75.0           # MT 폭 (mm)
MT_LENGTH = 300.0         # MT 길이 (mm)
MT_THICKNESS = 6.0        # MT 두께 (mm)
SLOT_HALF_LENGTH = 10.0   # 슬롯 반길이 a0 (mm)
SLOT_WIDTH = 0.5          # 슬롯 폭 (mm)

# ── 튜브 시편 (E2207) ──
TUBE_OD = 22.0            # 외경 (mm)
TUBE_ID = 20.0            # 내경 (mm)
TUBE_GAUGE = 20.0         # 게이지 길이 (mm)
TUBE_TOTAL = 120.0        # 전체 길이 (mm)
TUBE_GRIP_OD = 28.0       # 그립부 외경 (mm)

# ── 옵션 ──
CREATE_GRIPS = True       # 그립/지그 생성 여부

# ┌───────────────────────────────────────────────────────────────┐
# │  실행 (아래는 수정 불필요)                                    │
# └───────────────────────────────────────────────────────────────┘

import clr
clr.AddReference("MXDigitalTwinModeller")

from SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Fatigue import (
    FatigueSpecimenParameters, FatigueSpecimenType, FatigueSectionShape)
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Fatigue import FatigueSpecimenService

TYPE_MAP = {
    "E466_Uniform": FatigueSpecimenType.ASTM_E466_Uniform,
    "E466_Hourglass": FatigueSpecimenType.ASTM_E466_Hourglass,
    "E606": FatigueSpecimenType.ASTM_E606,
    "E647_CT": FatigueSpecimenType.ASTM_E647_CT,
    "E647_MT": FatigueSpecimenType.ASTM_E647_MT,
    "E2207": FatigueSpecimenType.ASTM_E2207,
    "Custom": FatigueSpecimenType.Custom,
}

SHAPE_MAP = {
    "Rectangular": FatigueSectionShape.Rectangular,
    "Circular": FatigueSectionShape.Circular,
    "Tubular": FatigueSectionShape.Tubular,
}

def main():
    part = Window.ActiveWindow.Document.MainPart

    spec_type = TYPE_MAP.get(SPECIMEN_TYPE)
    if spec_type is None:
        print("[Fatigue] 알 수 없는 타입: %s" % SPECIMEN_TYPE)
        print("[Fatigue] 사용 가능: %s" % ", ".join(sorted(TYPE_MAP.keys())))
        return

    p = FatigueSpecimenParameters()
    p.SpecimenType = spec_type
    p.SectionShape = SHAPE_MAP.get(SECTION_SHAPE, FatigueSectionShape.Rectangular)

    # 공통
    p.GaugeLength = GAUGE_LENGTH
    p.GaugeWidth = GAUGE_WIDTH
    p.Thickness = THICKNESS
    p.GaugeDiameter = GAUGE_DIAMETER
    p.GripWidth = GRIP_WIDTH
    p.GripLength = GRIP_LENGTH
    p.TotalLength = TOTAL_LENGTH
    p.FilletRadius = FILLET_RADIUS
    p.HourglassRadius = HOURGLASS_RADIUS

    # CT
    p.CTWidth = CT_WIDTH
    p.CTThickness = CT_THICKNESS
    p.InitialCrackLength = INITIAL_CRACK
    p.PinHoleDiameter = PIN_HOLE_DIA
    p.NotchWidth = NOTCH_WIDTH

    # MT
    p.MTWidth = MT_WIDTH
    p.MTLength = MT_LENGTH
    p.MTThickness = MT_THICKNESS
    p.SlotHalfLength = SLOT_HALF_LENGTH
    p.SlotWidth = SLOT_WIDTH

    # Tube
    p.TubeOuterDiameter = TUBE_OD
    p.TubeInnerDiameter = TUBE_ID
    p.TubeGaugeLength = TUBE_GAUGE
    p.TubeTotalLength = TUBE_TOTAL
    p.TubeGripOuterDiameter = TUBE_GRIP_OD

    # 옵션
    p.CreateGrips = CREATE_GRIPS

    ok, err = p.Validate()
    if not ok:
        print("[Fatigue] 유효성 오류: %s" % err)
        return

    print("[Fatigue] 타입: %s (%s)" % (SPECIMEN_TYPE, SECTION_SHAPE))
    print("[Fatigue] 생성 중...")

    service = FatigueSpecimenService()
    bodies = service.CreateFatigueSpecimen(part, p)

    if bodies and bodies.Count > 0:
        names = ", ".join([b.Name or "Unnamed" for b in bodies])
        print("[Fatigue] 완료: %d개 바디 (%s)" % (bodies.Count, names))
    else:
        print("[Fatigue] 생성 실패")

main()
