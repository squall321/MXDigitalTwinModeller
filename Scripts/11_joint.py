# encoding: utf-8
# ================================================================
# MX Digital Twin Modeller - 11. Joint Specimen (접합부 시편 생성)
# SpaceClaim Script Editor에서 실행
# ================================================================

# ┌───────────────────────────────────────────────────────────────┐
# │  설정 (이 부분만 수정하세요)                                  │
# └───────────────────────────────────────────────────────────────┘

# 접합부 타입:
#   "D1002_SingleLap" - ASTM D1002 단일 겹침 이음
#   "D3528_DoubleLap" - ASTM D3528 이중 겹침 이음
#   "Scarf"           - 스카프 이음
#   "Butt"            - 맞대기 이음
#   "T_Joint"         - T 이음
#   "Custom"          - 사용자 정의
JOINT_TYPE = "D1002_SingleLap"

# ── 공통 판재 치수 (mm) ──
ADHEREND_WIDTH = 25.4     # 판재 폭 (Y)
ADHEREND_LENGTH = 100.0   # 판재 길이 (X)
ADHEREND_THICKNESS = 1.6  # 판재 두께 (Z)

# ── Lap Joint (Single/Double) ──
OVERLAP_LENGTH = 25.4     # 겹침 길이 (mm)

# ── 접착층 ──
ADHESIVE_THICKNESS = 0.2  # 접착층 두께 (mm)
CREATE_ADHESIVE = True    # 접착층 별도 바디 생성 여부

# ── Scarf Joint ──
SCARF_ANGLE = 5.0         # 스카프 각도 (도)

# ── T-Joint ──
FLANGE_LENGTH = 100.0     # 플랜지 길이 (mm)
WEB_HEIGHT = 50.0         # 웹 높이 (mm)
WEB_THICKNESS = 2.0       # 웹 두께 (mm)
FILLET_BOND_SIZE = 5.0    # 필렛 본드 크기 (mm)

# ── 옵션 ──
CREATE_GRIPS = False      # 그립/지그 생성 여부
TAB_LENGTH = 25.0         # 탭 길이 (mm)
TAB_THICKNESS = 1.6       # 탭 두께 (mm)

# ┌───────────────────────────────────────────────────────────────┐
# │  실행 (아래는 수정 불필요)                                    │
# └───────────────────────────────────────────────────────────────┘

import clr
clr.AddReference("MXDigitalTwinModeller")

from SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Joint import (
    JointSpecimenParameters, JointSpecimenType)
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Joint import JointSpecimenService

TYPE_MAP = {
    "D1002_SingleLap": JointSpecimenType.ASTM_D1002_SingleLap,
    "D3528_DoubleLap": JointSpecimenType.ASTM_D3528_DoubleLap,
    "Scarf": JointSpecimenType.Scarf_Joint,
    "Butt": JointSpecimenType.Butt_Joint,
    "T_Joint": JointSpecimenType.T_Joint,
    "Custom": JointSpecimenType.Custom,
}

def main():
    part = Window.ActiveWindow.Document.MainPart

    joint_type = TYPE_MAP.get(JOINT_TYPE)
    if joint_type is None:
        print("[Joint] 알 수 없는 타입: %s" % JOINT_TYPE)
        print("[Joint] 사용 가능: %s" % ", ".join(sorted(TYPE_MAP.keys())))
        return

    p = JointSpecimenParameters()
    p.SpecimenType = joint_type
    p.AdherendWidth = ADHEREND_WIDTH
    p.AdherendLength = ADHEREND_LENGTH
    p.AdherendThickness = ADHEREND_THICKNESS
    p.OverlapLength = OVERLAP_LENGTH
    p.AdhesiveThickness = ADHESIVE_THICKNESS
    p.CreateAdhesiveBody = CREATE_ADHESIVE
    p.ScarfAngle = SCARF_ANGLE
    p.FlangeLength = FLANGE_LENGTH
    p.WebHeight = WEB_HEIGHT
    p.WebThickness = WEB_THICKNESS
    p.FilletBondSize = FILLET_BOND_SIZE
    p.CreateGrips = CREATE_GRIPS
    p.TabLength = TAB_LENGTH
    p.TabThickness = TAB_THICKNESS

    ok, err = p.Validate()
    if not ok:
        print("[Joint] 유효성 오류: %s" % err)
        return

    print("[Joint] 타입: %s" % JOINT_TYPE)
    if JOINT_TYPE == "T_Joint":
        print("[Joint] 플랜지=%.1f mm, 웹=%.1f×%.1f mm" % (FLANGE_LENGTH, WEB_HEIGHT, WEB_THICKNESS))
    elif JOINT_TYPE == "Scarf":
        print("[Joint] 각도=%.1f°, 판재=%.1f×%.1f mm" % (SCARF_ANGLE, ADHEREND_LENGTH, ADHEREND_WIDTH))
    else:
        print("[Joint] 판재=%.1f×%.1f mm, 겹침=%.1f mm" % (ADHEREND_LENGTH, ADHEREND_WIDTH, OVERLAP_LENGTH))

    print("[Joint] 생성 중...")

    service = JointSpecimenService()
    bodies = service.CreateJointSpecimen(part, p)

    if bodies and bodies.Count > 0:
        names = ", ".join([b.Name or "Unnamed" for b in bodies])
        print("[Joint] 완료: %d개 바디 (%s)" % (bodies.Count, names))
    else:
        print("[Joint] 생성 실패")

main()
