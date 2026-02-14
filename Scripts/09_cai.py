# encoding: utf-8
# ================================================================
# MX Digital Twin Modeller - 09. CAI Specimen (충격 후 압축 시편 생성)
# SpaceClaim Script Editor에서 실행
# ================================================================

# ┌───────────────────────────────────────────────────────────────┐
# │  설정 (이 부분만 수정하세요)                                  │
# └───────────────────────────────────────────────────────────────┘

# 시편 규격 타입:
#   "D7137"       - ASTM D7137
#   "D6264"       - ASTM D6264
#   "BSS7260"     - Boeing BSS 7260
#   "Custom"      - 사용자 정의
SPECIMEN_TYPE = "D7137"

# ── 패널 치수 (mm) ──
PANEL_LENGTH = 150.0      # 패널 길이 (X, 압축 하중 방향)
PANEL_WIDTH = 100.0       # 패널 폭 (Y)
THICKNESS = 4.0           # 패널 두께 (Z)

# ── Anti-Buckling 지그 ──
CREATE_JIG = True         # 지그 생성 여부
JIG_THICKNESS = 10.0      # 지그 두께 (mm)
WINDOW_LENGTH = 75.0      # 지그 윈도우 길이 (X)
WINDOW_WIDTH = 50.0       # 지그 윈도우 폭 (Y)
JIG_CLEARANCE = 0.5       # 지그-패널 간격 (mm)

# ── 손상 영역 (Damage Zone) ──
CREATE_DAMAGE = True      # 손상 영역 생성 여부
IS_ELLIPTICAL = False     # True=타원, False=원형
DAMAGE_DIAMETER = 25.0    # 원형 손상 직경 (mm)
DAMAGE_MAJOR = 30.0       # 타원 장축 (mm)
DAMAGE_MINOR = 20.0       # 타원 단축 (mm)
DAMAGE_DEPTH_PCT = 50.0   # 손상 깊이 비율 (0~100%)

# ┌───────────────────────────────────────────────────────────────┐
# │  실행 (아래는 수정 불필요)                                    │
# └───────────────────────────────────────────────────────────────┘

import clr
clr.AddReference("MXDigitalTwinModeller")

from SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.CAI import (
    CAISpecimenParameters, CAISpecimenType)
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.CAI import CAISpecimenService

TYPE_MAP = {
    "D7137": CAISpecimenType.ASTM_D7137,
    "D6264": CAISpecimenType.ASTM_D6264,
    "BSS7260": CAISpecimenType.Boeing_BSS7260,
    "Custom": CAISpecimenType.Custom,
}

def main():
    part = Window.ActiveWindow.Document.MainPart

    spec_type = TYPE_MAP.get(SPECIMEN_TYPE)
    if spec_type is None:
        print("[CAI] 알 수 없는 타입: %s" % SPECIMEN_TYPE)
        print("[CAI] 사용 가능: %s" % ", ".join(sorted(TYPE_MAP.keys())))
        return

    p = CAISpecimenParameters()
    p.SpecimenType = spec_type
    p.PanelLength = PANEL_LENGTH
    p.PanelWidth = PANEL_WIDTH
    p.Thickness = THICKNESS
    p.CreateJig = CREATE_JIG
    p.JigThickness = JIG_THICKNESS
    p.WindowLength = WINDOW_LENGTH
    p.WindowWidth = WINDOW_WIDTH
    p.JigClearance = JIG_CLEARANCE
    p.CreateDamageZone = CREATE_DAMAGE
    p.IsEllipticalDamage = IS_ELLIPTICAL
    p.DamageDiameter = DAMAGE_DIAMETER
    p.DamageMajorAxis = DAMAGE_MAJOR
    p.DamageMinorAxis = DAMAGE_MINOR
    p.DamageDepthPercent = DAMAGE_DEPTH_PCT

    ok, err = p.Validate()
    if not ok:
        print("[CAI] 유효성 오류: %s" % err)
        return

    print("[CAI] %s - %.1f×%.1f×%.1f mm" % (SPECIMEN_TYPE, PANEL_LENGTH, PANEL_WIDTH, THICKNESS))
    if CREATE_JIG:
        print("[CAI] 지그: Window %.1f×%.1f mm" % (WINDOW_LENGTH, WINDOW_WIDTH))
    if CREATE_DAMAGE:
        if IS_ELLIPTICAL:
            print("[CAI] 손상: 타원 %.1f×%.1f mm, 깊이 %.0f%%" % (DAMAGE_MAJOR, DAMAGE_MINOR, DAMAGE_DEPTH_PCT))
        else:
            print("[CAI] 손상: 원형 D=%.1f mm, 깊이 %.0f%%" % (DAMAGE_DIAMETER, DAMAGE_DEPTH_PCT))

    print("[CAI] 생성 중...")

    service = CAISpecimenService()
    bodies = service.CreateCAISpecimen(part, p)

    if bodies and bodies.Count > 0:
        names = ", ".join([b.Name or "Unnamed" for b in bodies])
        print("[CAI] 완료: %d개 바디 (%s)" % (bodies.Count, names))
    else:
        print("[CAI] 생성 실패")

main()
