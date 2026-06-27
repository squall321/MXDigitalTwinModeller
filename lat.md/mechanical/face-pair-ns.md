---
lat:
  require-code-mention: true
---

# Face Pair Named Selection

두 바디 사이의 접촉 면 페어를 검출해서 Named Selection 으로 저장한다. [[face-analysis]] 의 단순한 방향별 NS 보다 정교한 접근으로, 거리/법선/위치 기반 매칭을 거쳐 실제로 마주보는 면 쌍을 찾아낸다. Tied Contact ([[tied-check]]) 의 입력으로 사용됨.

`Face Pair NS` 툴바 버튼 → `show_face_pair_dialog` → `FacePairDialog` (라인 1103). 두 가지 모드 지원: **merge mode** (모든 페어를 두 개의 NS 로 묶음) 와 **per-pair mode** (페어별로 별도 NS).

## Face Pair 검출

`on_find` 핸들러가 사용자 입력 (target body 그룹 + other body 그룹 + tolerance) 을 받아 `_detect_pairs()` 실행. 알고리즘:

1. **거리 + 법선 조건**: 두 면 사이 거리 < tolerance AND 법선 dot product < -0.8 (거의 반대 방향).
2. **중복 제거**: 같은 face 가 여러 페어에 포함되지 않도록 geometric key (면 중심점 + 법선 양자화) 기반 dedupe.
3. **테이블 출력**: 각 페어를 (target face, other face, distance, normal alignment, geo_key) 으로 표시.

이전에 중복 검출 버그가 있었음 (커밋 `e6f5de5`) — 같은 면 쌍이 다른 face index 로 두 번 잡히는 문제를 geo_key 정규화로 해결.

진입점: `[[Mechanical/MXSimulator/main.py#FacePairDialog]]` (라인 1103). `_detect_pairs` 가 라인 1345.

## NS 생성 — Merge Mode

체크된 모든 페어를 모아 단 두 개의 NS 로 합친다 (`Pair_Target`, `Pair_Other`). 모든 target face 가 한 NS, 모든 other face 가 또 다른 NS 로. Tied Contact 한 번에 묶고 싶을 때 유용.

내부 메서드: `_create_ns_merged()` — `FacePairDialog` 클래스 내.

## NS 생성 — Per-Pair Mode

페어별로 별도 NS 두 개씩 생성 (`Pair_01_Target`, `Pair_01_Other`, `Pair_02_Target`, ...). 페어별로 다른 접촉 설정을 주거나, 어느 페어가 Tied 가 안 됐는지 디버깅할 때 유리.

내부 메서드: `_create_ns_per_pair()` — `FacePairDialog` 클래스 내. `on_create_ns` (라인 1478) 가 모드 분기를 처리.

## Include / Exclude 로직

테이블의 각 페어 옆에 체크박스가 있어 사용자가 NS 생성에 포함/제외 선택 가능. 이전에 토글이 반대로 적용되던 버그 (커밋 `d5314c6`) 가 있었음 — 체크된 페어만 포함되도록 수정.
