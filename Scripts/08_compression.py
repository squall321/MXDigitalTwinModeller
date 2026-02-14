# encoding: utf-8
# ================================================================
# MX Digital Twin Modeller - 08. Compression Specimen (압축 시편 생성)
# SpaceClaim Script Editor에서 실행
# ================================================================

# ┌───────────────────────────────────────────────────────────────┐
# │  설정 (이 부분만 수정하세요)                                  │
# └───────────────────────────────────────────────────────────────┘

# 시편 규격 타입:
#   "D695_Prism"     - ASTM D695 직육면체
#   "D695_Cylinder"  - ASTM D695 원기둥
#   "ISO_604_Mod"    - ISO 604 탄성계수용
#   "ISO_604_Str"    - ISO 604 강도용
#   "E9_Short"       - ASTM E9 단주 (L/D=2)
#   "E9_Medium"      - ASTM E9 중주 (L/D=3)
#   "Custom"         - 사용자 정의
SPECIMEN_TYPE = "D695_Prism"

# 시편 형상: "Prism" (직육면체) 또는 "Cylinder" (원기둥)
SHAPE = "Prism"

# ── 직육면체 치수 (mm) ──
WIDTH = 12.7              # 폭 (X 방향)
DEPTH = 12.7              # 깊이 (Y 방향)
HEIGHT = 25.4             # 높이 (Z 방향, 하중 방향)

# ── 원기둥 치수 (mm) ──
DIAMETER = 12.7           # 직경

# ── 압축판 (Platen) ──
CREATE_PLATENS = True     # 플래튼 생성 여부
PLATEN_DIAMETER = 50.0    # 플래튼 직경 (mm)
PLATEN_HEIGHT = 20.0      # 플래튼 높이 (mm)

# ┌───────────────────────────────────────────────────────────────┐
# │  실행 (아래는 수정 불필요)                                    │
# └───────────────────────────────────────────────────────────────┘

import clr
clr.AddReference("MXDigitalTwinModeller")

from SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Compression import (
    CompressionSpecimenParameters, CompressionSpecimenType, CompressionSpecimenShape)
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Compression import CompressionSpecimenService

TYPE_MAP = {
    "D695_Prism": CompressionSpecimenType.ASTM_D695_Prism,
    "D695_Cylinder": CompressionSpecimenType.ASTM_D695_Cylinder,
    "ISO_604_Mod": CompressionSpecimenType.ISO_604_Modulus,
    "ISO_604_Str": CompressionSpecimenType.ISO_604_Strength,
    "E9_Short": CompressionSpecimenType.ASTM_E9_Short,
    "E9_Medium": CompressionSpecimenType.ASTM_E9_Medium,
    "Custom": CompressionSpecimenType.Custom,
}

SHAPE_MAP = {
    "Prism": CompressionSpecimenShape.Prism,
    "Cylinder": CompressionSpecimenShape.Cylinder,
}

def main():
    part = Window.ActiveWindow.Document.MainPart

    spec_type = TYPE_MAP.get(SPECIMEN_TYPE)
    if spec_type is None:
        print("[Compression] 알 수 없는 타입: %s" % SPECIMEN_TYPE)
        print("[Compression] 사용 가능: %s" % ", ".join(sorted(TYPE_MAP.keys())))
        return

    p = CompressionSpecimenParameters()
    p.SpecimenType = spec_type
    p.Shape = SHAPE_MAP.get(SHAPE, CompressionSpecimenShape.Prism)
    p.WidthMm = WIDTH
    p.DepthMm = DEPTH
    p.HeightMm = HEIGHT
    p.DiameterMm = DIAMETER
    p.CreatePlatens = CREATE_PLATENS
    p.PlatenDiameterMm = PLATEN_DIAMETER
    p.PlatenHeightMm = PLATEN_HEIGHT

    ok, err = p.Validate()
    if not ok:
        print("[Compression] 유효성 오류: %s" % err)
        return

    if SHAPE == "Prism":
        print("[Compression] %s - %.1f×%.1f×%.1f mm" % (SPECIMEN_TYPE, WIDTH, DEPTH, HEIGHT))
    else:
        print("[Compression] %s - D=%.1f, H=%.1f mm" % (SPECIMEN_TYPE, DIAMETER, HEIGHT))

    print("[Compression] 생성 중...")

    service = CompressionSpecimenService()
    bodies = service.CreateCompressionSpecimen(part, p)

    if bodies and bodies.Count > 0:
        names = ", ".join([b.Name or "Unnamed" for b in bodies])
        print("[Compression] 완료: %d개 바디 (%s)" % (bodies.Count, names))
    else:
        print("[Compression] 생성 실패")

main()
