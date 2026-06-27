---
lat:
  require-code-mention: true
---

# Face Analysis

STEP 파일을 임포트해 각 면의 법선 방향을 분석하고, 그 결과를 방향별 Named Selection 으로 자동 생성한다. 이게 Cap Vibration / Modal Analysis 워크플로우의 [[mechanical-act#툴바 구조]] Phase 1 진입점이며, 후속 [[face-pair-ns]] / [[tied-check]] 의 입력이 된다.

`Named Selections` 툴바 버튼 클릭 → `show_ns_dialog` 콜백 → `NSDialog` (`[[Mechanical/MXSimulator/main.py]]` 라인 186) 실행. STEP 파일 선택 → 임포트 → 면 분석 → ±X/Y/Z 6방향으로 분류 → 사용자가 선택한 방향만 NS 생성.

## STEP Import + 면 분석

`NSDialog` 의 `on_find` 핸들러가 다음을 수행:

1. **STEP 임포트**: `geometry.AddGeometryImport().Import(...)` — 자세한 호출은 [[api-learnings#STEP Import — `GeometryImport`]] 참조.
2. **임포트된 바디 필터링**: `body.IsImported` 플래그로 새로 들어온 바디만 골라냄.
3. **면 순회 + 법선 계산**: 각 바디의 `GetGeoBody().Faces[i]` 를 순회하며 면 중심에서 `NormalAtParam(u, v)` 호출. 주의 사항은 [[api-learnings#SpaceClaim Face Normal — `NormalAtParam` 만 동작]].
4. **방향 분류**: 법선의 가장 큰 성분으로 ±X/Y/Z 분류. 공용 함수 `classify_normal_direction()` 사용 ([[mechanical-act#공용 헬퍼]]).
5. **테이블 표시**: 각 면의 (body, face index, normal, direction) 을 다이얼로그 테이블에 출력.

진입점: `[[Mechanical/MXSimulator/main.py#NSDialog]]` (`on_find` 메서드).

## 방향별 Named Selection 생성

`on_create_ns` 핸들러가 사용자가 체크한 면들을 모아 방향별 NS 를 만든다. 명명 규칙: `Contact_+Z`, `Contact_-Z`, `Contact_+X`, ... 6개 방향.

동일 이름 NS 가 이미 존재하면 `_2`, `_3` 등 카운터 자동 부여. 카운터 유틸리티는 `_next_ns_counter()`. NS 이름을 분석할 때는 `_ns_to_axis()`, `_is_negative_ns()` 헬퍼 사용.

진입점: `[[Mechanical/MXSimulator/main.py#NSDialog]]` (`on_create_ns` 메서드).

## 접촉 검출 (보조 기능)

같은 다이얼로그 안에 (선택적) 접촉 검출 기능도 포함되어 있다. 임포트된 STEP 면과 기존 모델 바디 사이의 접촉을 거리 + 법선 반대 조건으로 페어링. tolerance 입력 가능. 자세한 페어링 로직과 그 발전형은 [[face-pair-ns]] 참조.

내부 메서드: `_detect_contact()` — `NSDialog` 클래스 내. 메인 사용 사례에서는 거의 안 쓰고 [[face-pair-ns]] 의 `FacePairDialog` 로 대체된 레거시 경로.
