# encoding: utf-8
# ================================================================
# MX Digital Twin Modeller - 06. Tensile Specimen (인장 시편 생성)
# SpaceClaim Script Editor에서 실행
# ================================================================

# ┌───────────────────────────────────────────────────────────────┐
# │  설정 (이 부분만 수정하세요)                                  │
# └───────────────────────────────────────────────────────────────┘

# 시편 규격 타입:
#   금속 인장:   "E8_Standard", "E8_SubSize", "ISO_6892_1"
#   플라스틱:    "D638_TypeI", "D638_TypeII", "D638_TypeIII", "D638_TypeIV", "D638_TypeV"
#               "ISO_527_2_1A", "ISO_527_2_1B"
#   노치 인장:   "E602_VNotch", "E602_UNotch", "E338", "E292"
#   구멍 시편:   "D5766_OHT", "D6484_OHC", "D6742_FHT", "D5961_Bearing"
#   복합재:      "D3039"
#   PCB:        "IPC_Tensile", "IPC_PTHPull"
#   DMA 인장:    "DMA_Rectangle", "DMA_DogBone"
#   전단:        "D5379_Iosipescu", "D7078_VNotchRailShear"
#   사용자정의:   "Custom"
SPECIMEN_TYPE = "E8_Standard"

# ── 공통 치수 (mm) ──
GAUGE_LENGTH = 50.0       # 게이지 길이
GAUGE_WIDTH = 12.5        # 게이지 폭
THICKNESS = 3.0           # 두께
GRIP_WIDTH = 20.0         # 그립 폭
TOTAL_LENGTH = 200.0      # 전체 길이
FILLET_RADIUS = 12.5      # 필렛 반경
GRIP_LENGTH = 50.0        # 그립 길이

# ── 노치 파라미터 (E602/E338/E292/Iosipescu/VNotchRailShear) ──
NOTCH_DEPTH = 2.0         # 노치 깊이
NOTCH_RADIUS = 1.0        # U-노치 반경
NOTCH_ANGLE = 60.0        # V-노치 각도 (도)
IS_DOUBLE_NOTCH = True    # 양면 노치 (True) / 단면 (False)

# ── 구멍 파라미터 (OHT/OHC/FHT/Bearing) ──
HOLE_DIAMETER = 6.35      # 구멍 직경
IS_ELLIPTICAL_HOLE = False  # True=타원, False=원형
HOLE_MAJOR_AXIS = 8.0     # 타원 장축 (타원일 때)
HOLE_MINOR_AXIS = 4.0     # 타원 단축 (타원일 때)

# ── 직사각형 시편 / 탭 파라미터 (D3039, IPC) ──
IS_RECTANGULAR = False    # True=직사각형 (dog-bone 아님)
TAB_LENGTH = 50.0         # 탭 길이
TAB_THICKNESS = 1.5       # 탭 두께

# ┌───────────────────────────────────────────────────────────────┐
# │  실행 (아래는 수정 불필요)                                    │
# └───────────────────────────────────────────────────────────────┘

import clr
clr.AddReference("MXDigitalTwinModeller")

from SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.TensileTest import (
    TensileSpecimenParameters, ASTMSpecimenType)
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.TensileTest import SpecimenModelingService

TYPE_MAP = {
    "E8_Standard": ASTMSpecimenType.ASTM_E8_Standard,
    "E8_SubSize": ASTMSpecimenType.ASTM_E8_SubSize,
    "ISO_6892_1": ASTMSpecimenType.ISO_6892_1,
    "D638_TypeI": ASTMSpecimenType.ASTM_D638_TypeI,
    "D638_TypeII": ASTMSpecimenType.ASTM_D638_TypeII,
    "D638_TypeIII": ASTMSpecimenType.ASTM_D638_TypeIII,
    "D638_TypeIV": ASTMSpecimenType.ASTM_D638_TypeIV,
    "D638_TypeV": ASTMSpecimenType.ASTM_D638_TypeV,
    "ISO_527_2_1A": ASTMSpecimenType.ISO_527_2_Type1A,
    "ISO_527_2_1B": ASTMSpecimenType.ISO_527_2_Type1B,
    "E602_VNotch": ASTMSpecimenType.ASTM_E602_VNotch,
    "E602_UNotch": ASTMSpecimenType.ASTM_E602_UNotch,
    "E338": ASTMSpecimenType.ASTM_E338,
    "E292": ASTMSpecimenType.ASTM_E292,
    "D5766_OHT": ASTMSpecimenType.ASTM_D5766_OHT,
    "D6484_OHC": ASTMSpecimenType.ASTM_D6484_OHC,
    "D6742_FHT": ASTMSpecimenType.ASTM_D6742_FHT,
    "D5961_Bearing": ASTMSpecimenType.ASTM_D5961_Bearing,
    "D3039": ASTMSpecimenType.ASTM_D3039,
    "IPC_Tensile": ASTMSpecimenType.IPC_TM650_Tensile,
    "IPC_PTHPull": ASTMSpecimenType.IPC_TM650_PTHPull,
    "DMA_Rectangle": ASTMSpecimenType.DMA_Tensile_Rectangle,
    "DMA_DogBone": ASTMSpecimenType.DMA_Tensile_DogBone,
    "D5379_Iosipescu": ASTMSpecimenType.ASTM_D5379_Iosipescu,
    "D7078_VNotchRailShear": ASTMSpecimenType.ASTM_D7078_VNotchRailShear,
    "Custom": ASTMSpecimenType.Custom,
}

def main():
    part = Window.ActiveWindow.Document.MainPart

    spec_type = TYPE_MAP.get(SPECIMEN_TYPE)
    if spec_type is None:
        print("[Tensile] 알 수 없는 시편 타입: %s" % SPECIMEN_TYPE)
        print("[Tensile] 사용 가능: %s" % ", ".join(sorted(TYPE_MAP.keys())))
        return

    p = TensileSpecimenParameters()
    p.SpecimenType = spec_type
    p.GaugeLength = GAUGE_LENGTH
    p.GaugeWidth = GAUGE_WIDTH
    p.Thickness = THICKNESS
    p.GripWidth = GRIP_WIDTH
    p.TotalLength = TOTAL_LENGTH
    p.FilletRadius = FILLET_RADIUS
    p.GripLength = GRIP_LENGTH
    p.NotchDepth = NOTCH_DEPTH
    p.NotchRadius = NOTCH_RADIUS
    p.NotchAngle = NOTCH_ANGLE
    p.IsDoubleNotch = IS_DOUBLE_NOTCH
    p.HoleDiameter = HOLE_DIAMETER
    p.IsEllipticalHole = IS_ELLIPTICAL_HOLE
    p.HoleMajorAxis = HOLE_MAJOR_AXIS
    p.HoleMinorAxis = HOLE_MINOR_AXIS
    p.IsRectangular = IS_RECTANGULAR
    p.TabLength = TAB_LENGTH
    p.TabThickness = TAB_THICKNESS

    ok, err = p.Validate()
    if not ok:
        print("[Tensile] 유효성 오류: %s" % err)
        return

    print("[Tensile] 타입: %s" % SPECIMEN_TYPE)
    print("[Tensile] GL=%.1f, GW=%.1f, T=%.1f mm" % (GAUGE_LENGTH, GAUGE_WIDTH, THICKNESS))
    print("[Tensile] 생성 중...")

    service = SpecimenModelingService()
    result = service.CreateTensileSpecimen(part, p)

    if result:
        print("[Tensile] 완료: %s" % (result.Name or "Unnamed"))
    else:
        print("[Tensile] 생성 실패")

main()
