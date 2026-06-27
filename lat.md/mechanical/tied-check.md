---
lat:
  require-code-mention: true
---

# Tied Contact Check

Modal Analysis 결과에서 강체 모드 (Rigid Body Modes, RBM) 를 자동 검출하고, 해당 바디에 누락된 Tied Contact 를 자동으로 만들어준다. 메쉬가 인접 바디와 묶이지 않아 자유로이 떠다니면 모달 결과의 첫 6개 주파수가 거의 0 이 되는데, 이를 알고리즘으로 잡아낸다.

`Tied Check` 툴바 버튼 → `show_tied_check_dialog` → `TiedContactCheckDialog` (`[[Mechanical/MXSimulator/main.py#TiedContactCheckDialog]]`, 라인 3210). 모달 실행 → RBM 검출 → Tied 생성 → 모달 재실행의 4단계 작업.

## 모달 실행 + RBM 검출

`on_run_modal` 가 현재 Modal Analysis 를 Solve 하고 결과에서 RBM 을 검출한다 (`detect_rigid_bodies`, 라인 3460). 검출은 두 가지 방식:

1. **Connectivity 분석** (`_analyze_contact_connectivity`, 라인 3591) — 기존 Contact / Joint / Spring 그래프를 만들어 어느 바디가 고립됐는지 찾음.
2. **에너지 기반 검출** (`_detect_rbm_bodies_by_energy`, 라인 3717) — 첫 N 개 모드의 strain energy 가 거의 0 인 바디 → RBM 후보.

두 결과를 교차 검증해 신뢰도 높은 RBM 목록 도출. 결과는 다이얼로그의 ListBox 에 표시 (`update_rb_listbox`, 라인 3798).

이전에 face pair 중복 검출 (Tied 가 동일 면 쌍에 여러 번 생성되던 문제) 도 함께 픽스됨 (커밋 `e6f5de5`).

## Tied Contact 자동 생성

`on_create_contacts` (라인 3815) 가 RBM 바디들에 대해 자동으로 Bonded Contact 를 만든다. 각 바디마다:

1. `find_best_contact_target()` — 인접한 후보 바디들 중 가장 가까운 것을 BB 거리 + face facing 으로 선택. 알고리즘 라인 3950.
2. `bounding_box_distance()` (라인 4054) + `are_faces_facing()` (라인 4077) 로 면 쌍의 적합성 평가.
3. `create_contact_for_body()` (라인 3884) 가 실제 Contact 객체 추가 + Behavior=Bonded 설정.

## Suppress / Restore

수동 디버깅 옵션. 사용자가 RBM 으로 의심되지만 자동 픽스가 어려운 바디를 일시적으로 suppress 했다가 모달 재실행. 모든 suppress 해제도 한 클릭.

- `on_suppress` (라인 4115) — 선택 바디 suppress
- `on_restore_all` (라인 4159) — 모든 suppress 해제
- `on_rerun_modal` (라인 4153) — 모달 재실행

일반적인 사용 흐름: [[scenarios#Modal Analysis]] 에서 Modal 을 만든 뒤 → Tied Check 다이얼로그 열기 → Run Modal → RBM 목록 확인 → (옵션) [[face-pair-ns]] 에서 미리 만든 페어 NS 사용 → Create Contacts 클릭 → Bonded 자동 생성 → Rerun Modal → 강체 모드 사라졌는지 확인.
