# encoding: utf-8
# ================================================================
# MX Digital Twin Modeller - 03. Contact Detection (접촉 감지)
# SpaceClaim Script Editor에서 실행
# ================================================================

# ┌───────────────────────────────────────────────────────────────┐
# │  설정 (이 부분만 수정하세요)                                  │
# └───────────────────────────────────────────────────────────────┘

# 키워드 필터 (감지 모드)
# 모드 1 - 전체: 두 키워드 모두 빈값 → 모든 바디 간 접촉 감지
# 모드 2 - 단일: KEYWORD_A만 입력 → 해당 바디 ↔ 나머지 간 접촉
# 모드 3 - 쌍:   KEYWORD_A + KEYWORD_B → 두 그룹 간 접촉만
KEYWORD_A = ""
KEYWORD_B = ""

# 허용 거리 (mm) - 면 간 최대 허용 간격
TOLERANCE_MM = 1.0

# 접두사 자동 지정 (KEYWORD_A 기반, 빈값이면 기본 접두사 사용)
# 기본 접두사: NodeSet, EdgeContact, CylContact, PCContact
AUTO_PREFIX = True

# Named Selection 자동 생성 (False면 감지만 하고 NS 미생성)
CREATE_NS = True

# ┌───────────────────────────────────────────────────────────────┐
# │  실행 (아래는 수정 불필요)                                    │
# └───────────────────────────────────────────────────────────────┘

import clr
clr.AddReference("MXDigitalTwinModeller")

from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Contact import ContactDetectionService

def main():
    part = Window.ActiveWindow.Document.MainPart

    # 감지 모드 출력
    if not KEYWORD_A and not KEYWORD_B:
        print("[Contact] 모드: 전체 (모든 바디 간)")
    elif KEYWORD_A and not KEYWORD_B:
        print("[Contact] 모드: 단일 ('%s' ↔ 나머지)" % KEYWORD_A)
    else:
        print("[Contact] 모드: 쌍 ('%s' ↔ '%s')" % (KEYWORD_A, KEYWORD_B))
    print("[Contact] 허용거리: %.2f mm" % TOLERANCE_MM)

    # 접촉 감지
    print("[Contact] 감지 실행 중...")
    pairs = ContactDetectionService.DetectContacts(part, KEYWORD_A, KEYWORD_B, TOLERANCE_MM)
    print("[Contact] %d개 페어 감지됨" % pairs.Count)

    # 접두사 지정
    if AUTO_PREFIX and KEYWORD_A:
        ContactDetectionService.AssignPrefixes(pairs, KEYWORD_A)
        print("[Contact] 접두사 자동 지정 완료")

    # 기존 NS 확인
    ContactDetectionService.MarkExistingPairs(part, pairs)
    existing = sum(1 for p in pairs if p.IsExisting)
    new_pairs = sum(1 for p in pairs if not p.IsExisting)
    print("[Contact] 기존 NS: %d개, 신규: %d개" % (existing, new_pairs))

    # 페어 목록 출력
    for p in pairs:
        body_a = p.BodyA.Name if p.BodyA else "?"
        body_b = p.BodyB.Name if p.BodyB else "?"
        status = "기존" if p.IsExisting else "신규"
        print("  [%s] %s ↔ %s (접두사: %s, %s)" % (
            p.Type, body_a, body_b, p.Prefix, status))

    # Named Selection 생성
    if CREATE_NS and new_pairs > 0:
        print("[Contact] NS 생성 중...")
        ContactDetectionService.CreateNamedSelections(part, pairs)
        print("[Contact] %d개 NS 생성 완료" % new_pairs)
    elif not CREATE_NS:
        print("[Contact] NS 생성 스킵 (CREATE_NS=False)")
    else:
        print("[Contact] 신규 페어 없음 - NS 생성 스킵")

    # 진단 로그
    diag = ContactDetectionService.DiagnosticLog
    if diag and diag.Count > 0:
        print("[Contact] 진단 로그 (%d항목):" % diag.Count)
        for entry in diag:
            print("  %s" % entry)

    print("[Contact] 완료")

main()
