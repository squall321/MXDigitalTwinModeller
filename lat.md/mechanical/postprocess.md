---
lat:
  require-code-mention: true
---

# Post-Process

Modal/Transient 해석 결과를 분석하고, 별도의 외부 viewer 를 띄우거나 LS-DYNA `.k` 파일로 내보낸다. 두 개의 다이얼로그가 이 카테고리: PostProcessDialog (결과 추출 + 외부 뷰어) 와 ExportKFileDialog (Mechanical 메쉬를 `.k` 변환).

`Post-Process` / `Vibration Energy` / `Export K-File` 세 툴바 버튼. 외부 viewer 는 PyAnsys-PostProcess + matplotlib 의 standalone 환경.

## Post-Process Dialog

`show_postprocess_dialog` → `PostProcessDialog` (`[[Mechanical/MXSimulator/main.py#PostProcessDialog]]`, 라인 1651). 사용자가 분석을 선택하고 NS 패턴을 입력하면, 그 NS 들에 대한 결과 (Total Deformation, Stress, Strain 등) 를 추출해 외부 뷰어로 launch.

### 분석 선택

`_populate_analyses()` (라인 1818) 가 현재 프로젝트의 모든 Analysis 를 ListBox 에 표시. 사용자가 선택하면 `_get_selected_analysis()` (라인 1842) 로 객체 획득.

### NS 패턴 필터

사용자가 `Contact_+*` 같은 와일드카드 패턴을 입력하면 그 패턴에 매칭되는 NS 만 결과 추출 대상으로. `_get_ns_patterns()` (라인 1849) 가 입력 텍스트를 파싱.

### 결과 추가

`on_add_results` (라인 1973) 가 선택된 NS 들에 대해 Total Deformation, Equivalent Stress 등 결과를 자동으로 Solution 트리에 추가. `_safe_float()` 헬퍼로 None 안전 변환.

### 외부 뷰어 실행

`on_export_launch` (라인 2182) 가 결과 메타데이터를 JSON 으로 dump 한 뒤 외부 Python 프로세스로 viewer 를 띄움. Python 인터프리터 자동 검출은 `_find_python()` (라인 2412), 실제 launch 는 `_launch_viewer()` (라인 2379).

뷰어 자체:
- `[[Mechanical/MXSimulator/postprocess/visualizer.py]]` — matplotlib 기반 시각화 (500+ 라인)
- `[[Mechanical/MXSimulator/postprocess/analyzer.py]]` — 결과 분석 로직
- `[[Mechanical/MXSimulator/postprocess/runner.py]]` — 외부 진입점
- `[[Mechanical/MXSimulator/postprocess/build_viewer.bat]]` — PyInstaller 빌드 스크립트

자세한 환경 설정은 [[build-deploy#Python 환경 (Material Calibrator)]].

## Vibration Energy

`show_energy_dialog` → `EnergyDialog` (`[[Mechanical/MXSimulator/main.py#EnergyDialog]]`, 라인 5644). 버튼 하나로
Modal / Harmonic / Transient 결과에서 **파트별 진동에너지**를 뽑고, 의미 있는 파트만 남긴다.

### 한 번 누르면 일어나는 일 (`on_run`)

1. 대상 해석의 모든 body 에 `ElementalStrainEnergy` 를 붙여 set 별로 평가 — 모달=모드, 하모닉=주파수 세트, 트랜지언트=시간 세트 (셋 다 `Result.SetNumber`; 하모닉/트랜지언트의 SetNumber 의미는 GATE 미실측이라 세트 값 f/t 를 함께 기록), 그 밖의 해석 타입=단일. 같은 이름의 body 는 `이름#ObjectId` 로 구분
2. body 별 총 에너지 → 점유율(share). **`Result.Total` 우선, 없으면 `MaximumOfMaximumOverTime` 폴백** — 어느 쪽을 썼는지 `energy_basis` 로 기록
3. 필터: set 당 Top-N(6) / 누적 점유율 컷(90%) / 최소 점유율(2%). 단일 body 가 50% 이상이면 `localized` 플래그
4. 탈락한 body 의 결과 객체는 트리에서 삭제, 남은 것은 `E1 [62%] BodyName` 으로 개명 → 트리 자체가 랭킹표
5. `EnergyContribution` 모자이크 3종(Strain/Kinetic/Total) 추가 — `TopBodiesToDisplay`, 모달이면 `ModeSelection=ModalEffectiveMass`
6. 최대 에너지 body 에 Total Deformation + `AddFigure()` (`WORST [70%] BodyA set3`)
7. `energy.json` (schema `energy-1.0`) 내보내기 → 뷰어 `EnergyTab` 이 읽는다

세트 값(모드/하모닉 → `ReportedFrequency` [Hz], 트랜지언트 → `Time` [s])은 body 1개에만 scoping 한 전용
`TotalDeformation` 프로브에서 읽는다 (`_probe_set_value`) — set 단위 값이라 scoping 을 좁혀도 정확하고 평가 비용이
최소다. 모달은 `MaximumModesToFind` 로 스캔 상한을 클램프하고, 범위를 넘긴 SetNumber 가 마지막 세트로 클램프돼
(세트값, 총에너지, 1위 body) 가 직전과 같아지면 스캔을 끝낸다. `Max sets` 에 걸려 끊기면 로그에 경고한다.

### 핵심 API 발견

`Ansys.ACT.Automation.Mechanical.Results.FrequencyResponseResults.EnergyContribution` 이 POSTPROCESS_IDEAS.md M10
("DPF / effort H" 로 잡혀 있던 것) 을 ACT 한 줄로 해결한다. `Results.Result` 베이스에 `Total` / `PlotData` /
`TabularData` / `AddFigure` / `ExportToTextFile` 이 있다 — 파생 클래스 XML 에 상속 멤버가 안 실려서 놓쳤던 것.
런타임 실측은 `[[.claude/skills/ansys-api-catalog/verification/verify_energy_api.py]]` (GATE, `ENERGY_GATE: OK/PARTIAL/BLOCKED`).
**⚠ `.claude/` 는 gitignore 대상 — 이 스크립트는 커밋되지 않는다.**

### 고친 버그 — strain_energy 가 총합이 아니었다

`PostProcessDialog` 는 `ElementalStrainEnergy` 에 `MaximumOfMaximumOverTime` 을 써서 `strain_energy` 를 채웠다.
그건 **그 body 에서 가장 뜨거운 요소 1개** 값이라 body 별로 합산해도 총합이 되지 않는데, 뷰어 `EnergyTab` 은
그걸 합산해 `%` 를 찍고 있었다. `.Total` 우선 + 폴백으로 바꾸고 `strain_energy_basis` 를 남겼다.
**metadata schema_version 2.0 → 2.1** (필드 추가가 아니라 `strain_energy` 의 의미가 바뀌었으므로).

### 뷰어 EnergyTab (`[[Mechanical/MXSimulator/postprocess/visualizer.py#EnergyTab]]`)

- `energy.json` 이 있으면 그것을, 없으면 metadata 의 `strain_energy` 를 그린다
- **basis 가 `Total` 이 아니면 점유율(%)을 아예 그리지 않고 경고 배너** — 틀린 숫자를 그럴듯하게 보여주지 않는다
- set 선택 콤보 (`mode 3 (340.0 Hz) ◆ localized`), 1위 바 색 강조, body 종합 뷰
- 경고 상태는 `_warn_active` 로 노출 — Qt `isVisible()` 은 창을 띄우기 전엔 항상 False 라 셀프테스트에서 검증 불가
- matplotlib 차트 안 텍스트는 ASCII 만 (DejaVu Sans 에 한글 글리프 없음 → □ 로 깨짐). Qt 라벨은 한글 OK

셀프테스트: `[[Mechanical/MXSimulator/postprocess/selftest_tabs.py]]` → `TABS_OK` + `ENERGY_OK`
(energy.json basis 2종 + metadata 2.1 + 레거시 폴백).

### 아직 확정 안 된 것

바디별 **운동에너지**를 숫자로 주는 ACT 결과는 `EnergyContribution` 뿐인데, 모자이크 차트 결과라 `Total`/`PlotData`
회수가 막혀 있을 수 있다 (GATE G4 가 판정). 막히면 SE 는 숫자, KE 는 차트로만 — 코드는 그렇게 degrade 한다.

## K-File Export

`show_export_kfile_dialog` → `ExportKFileDialog` (`[[Mechanical/MXSimulator/main.py#ExportKFileDialog]]`, 라인 2475). Mechanical 측 메쉬를 LS-DYNA 의 `.k` 키워드 파일로 변환한다 ([[mesh#Mesh 내보내기 (.k)]] 의 Mechanical 측 버전).

### 메쉬 노드/엘리먼트

`_write_nodes()` (라인 2729) 가 모든 노드를 `*NODE` 카드로. `_write_elements_parts()` (라인 2785) 가 엘리먼트를 Part 별로 분류해 `*ELEMENT_SOLID` / `*ELEMENT_SHELL` 카드로 출력. Solid 카드 2번째 행은 헬퍼 `_solid_card2()` 가 처리.

### 재료

`_write_materials()` (라인 2960) 가 Engineering Data 의 재료를 `*MAT_*` 카드로 변환. body → part ID 매핑은 호출자가 전달한 `body_pid_map`.

### Named Selection

`_write_named_selections()` (라인 3017) 가 모든 NS 를 `*SET_NODE_LIST` 로 변환. 두 가지 노드 추출 방식:

- `_get_ns_node_ids()` (라인 3078) — 일반 NS (mesh 기반)
- `_get_ns_node_ids_geometry()` (라인 3117) — 지오메트리 기반 NS (face/edge 에서 노드 lookup, tolerance 안에 들어오는 메쉬 노드)

### 접촉 / Segment Set

LS-DYNA Tied/Contact 카드를 위한 Segment Set 생성:

- `_collect_elements()` (라인 3224) — 모든 엘리먼트 모음
- `_enumerate_element_faces()` (라인 3254) — 각 엘리먼트의 면 열거
- `_extract_surface_faces()` (라인 3281) — 외부면 (한 번만 사용된 면) 추출
- `_average_elem_size()` (라인 3287) — 엘리먼트 평균 크기 (tolerance 자동 조정용)
- `_get_contact_segment_faces()` (라인 3303) — NS 가 가리키는 면을 segment 로 매핑
- `_write_segment_set()` (라인 3341) — `*SET_SEGMENT` 카드 출력
- `_write_contacts()` (라인 3356) — `*CONTACT_TIED_*` 카드 출력

샘플 출력은 `[[Mechanical/MXSimulator/test.k]]` (10000+ 라인, 실제 ASTM E8 시편 사례). LS-DYNA 키워드 참조는 `[[Docs/LSDyna/Vol_I.txt]]`.

### 진입점

`on_export` (라인 2598) 가 메인 핸들러. `on_browse` (라인 2586) 가 출력 경로 선택. 진행 상황은 `log()` (라인 2580) 로 다이얼로그 텍스트박스에 출력.
