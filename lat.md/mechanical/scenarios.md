---
lat:
  require-code-mention: true
---

# Scenarios

해석 시나리오 (Modal Analysis + 그 위에 얹은 Transient/Harmonic 시나리오) 자동 생성. Cap Vibration Time Force 워크플로우 ([[mechanical-act]] Phase 1–4) 의 중간 단계로, [[face-analysis]] 에서 만든 Named Selection 위에 하중과 해석 설정을 배치한다.

`Modal Analysis` + `Add Scenario` 두 개의 툴바 버튼이 이 카테고리. 모달 결과를 베이스로 시나리오 (Transient Modal Superposition) 를 쌓아올리는 흐름.

## Modal Analysis

Modal Analysis 를 자동 생성하고 주파수 범위, 모드 수, 메쉬 사이즈 등 기본 설정을 한 다이얼로그에서 처리. 기존 Modal 이 있으면 재사용/덮어쓰기 선택.

핵심 호출:
- `Model.Analyses.Add(AnalysisType.Modal)` — Modal Analysis 생성
- `modal.AnalysisSettings.ModalRangeMaximum = Quantity(...)` — 주파수 상한. ⚠️ `RangeMaximum` 아님 ([[api-learnings#Mechanical Modal Range — `ModalRangeMaximum`]] 참조)

진입점: `[[Mechanical/MXSimulator/main.py#ModalDialog]]` (라인 564). 콜백: `show_modal_dialog`.

다이얼로그가 열릴 때 `_check_existing()` 가 현재 프로젝트에 Modal Analysis 가 있는지 확인하고 UI 상태를 조정 (있으면 "재사용" 옵션 활성화). 같은 메서드를 `Add Scenario` 의 `ScenarioDialog._update_modal_status()` 도 호출해 Modal 의존성을 검증.

## Cap Vibration Scenario

Modal 결과 위에 Transient Modal Superposition 시나리오를 만든다. CSV 시간-하중 데이터를 읽어 방향별 Force 객체를 자동 생성하고 Tabular Data 로 연결.

흐름:
1. 사용자가 CSV 파일 선택 (`on_browse_csv`) → 시간 컬럼 + 각 방향 force 컬럼 파싱 (`_parse_csv`).
2. 적용할 Named Selection 들 선택 (`_load_ns_list` 로 NS 목록을 다이얼로그에 표시). 보통 [[face-analysis]] 에서 만든 `Contact_+Z` 같은 NS.
3. `on_create_scenario` 가 Transient Analysis 생성 + 각 NS 에 `AddForce()` 호출 + `force.XComponent.Output.DiscreteValues = [...]` 로 Tabular Data 셋.
4. Transient 의 시간 설정은 `settings.SetStepEndTime(1, Quantity(t, 's'))` ([[api-learnings#Mechanical Transient — `SetStepEndTime`]] 참조).

진입점: `[[Mechanical/MXSimulator/main.py#ScenarioDialog]]` (라인 689). 콜백: `show_scenario_dialog`. 헤드리스 실행 진입점은 `[[Mechanical/MXSimulator/run_cap_vibration.py]]`.

CSV 포맷은 표준 — 첫 컬럼이 시간 (초), 나머지 컬럼이 각 방향 force (N). 헤더 행 필수. 샘플 파일:

- `[[Mechanical/MXSimulator/sample_force.csv]]` — 일반 샘플
- `[[Mechanical/MXSimulator/force_sine_100hz.csv]]` — 100Hz 사인파
- `[[Mechanical/MXSimulator/force_impulse.csv]]` — 임펄스
- `[[Mechanical/MXSimulator/force_damped_sine.csv]]` — 감쇠 사인파
