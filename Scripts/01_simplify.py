# encoding: utf-8
# ================================================================
# MX Digital Twin Modeller - 01. Simplify (바디 단순화)
# SpaceClaim Script Editor에서 실행
# ================================================================

# ┌───────────────────────────────────────────────────────────────┐
# │  설정 (이 부분만 수정하세요)                                  │
# └───────────────────────────────────────────────────────────────┘

# 단순화 규칙 리스트: (키워드, 모드)
# 모드: "BoundingBox" = 경계상자로 대체
#       "SolidToShell" = 솔리드 → 셸 변환
RULES = [
    ("Bolt", "BoundingBox"),
    # ("Washer", "SolidToShell"),
    # ("Nut", "BoundingBox"),
]

# ┌───────────────────────────────────────────────────────────────┐
# │  실행 (아래는 수정 불필요)                                    │
# └───────────────────────────────────────────────────────────────┘

import clr
clr.AddReference("MXDigitalTwinModeller")

from System.Collections.Generic import List
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Simplify import SimplifyRule, SimplifyMode
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Simplify import SimplifyService

def main():
    part = Window.ActiveWindow.Document.MainPart

    rules = List[SimplifyRule]()
    for keyword, mode_str in RULES:
        if not keyword:
            continue
        mode = SimplifyMode.SolidToShell if mode_str == "SolidToShell" else SimplifyMode.BoundingBox
        rules.Add(SimplifyRule(keyword, mode))

    if rules.Count == 0:
        print("[Simplify] 규칙이 없습니다. RULES를 설정하세요.")
        return

    print("[Simplify] %d개 규칙 실행 중..." % rules.Count)

    result = SimplifyService.ExecuteBatch(part, rules)

    print("[Simplify] 완료:")
    print("  매칭: %d개" % result.MatchedCount)
    print("  처리: %d개" % result.ProcessedCount)
    print("  실패: %d개" % result.FailedCount)
    for entry in result.Log:
        print("  %s" % entry)

main()
