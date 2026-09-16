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
런타임 실측은 `[[Test/gates/verify_energy_api.py]]` (GATE, `ENERGY_GATE: OK/PARTIAL/BLOCKED`). 추적 사본이며 원본은
`.claude/skills/ansys-api-catalog/verification/` (gitignore) — 고치면 두 곳을 같이 맞춘다.

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

`show_export_kfile_dialog` → `ExportKFileDialog` (`[[Mechanical/MXSimulator/main.py#ExportKFileDialog]]`, 라인 2475). Mechanical 측 메쉬를 LS-DYNA 의 `.k` 키워드 파일로 변환한다 ([[mesh#Mesh 내보내기 (.k)]] 의 Mechanical 측 버전). 제어 카드·하중 곡선은 쓰지 않는다 — 메쉬·파트·재료·노드셋·접촉만.

출력 카드: `*KEYWORD` · `*NODE` · `*SECTION_SOLID`/`*SECTION_SHELL` · `*PART` · `*ELEMENT_SOLID`/`*ELEMENT_SHELL` · `*MAT_ELASTIC` · `*SET_NODE_TITLE` · `*SET_SEGMENT_TITLE` · `*CONTACT_TIED_SURFACE_TO_SURFACE`/`*CONTACT_AUTOMATIC_SURFACE_TO_SURFACE` · `*END`.

다이얼로그 옵션: 단위계 (mm-tonne-s 또는 SI m-kg-s — 좌표에 ×1000 적용), 재료/NS/접촉 포함 체크박스, 기하 tolerance (mm, 기본 0.1).

### 메쉬 노드/엘리먼트

`_write_nodes()` (라인 2729) 가 `MeshData.NodeById(1..NodeCount)` 로 노드를 읽어 `*NODE` (I8, 3×F16) 로 쓴다. 하나도 못 읽으면 `NodeByIndex` 폴백. 좌표는 m 로 보관(`node_coords_m`)하고 단위계 배율만 곱해 출력.

`_write_elements_parts()` (라인 2785) 는 body 열거 순서로 PID(1..N) 를 매기고, 엘리먼트의 `PartId`/`BodyId` 속성으로 파트에 묶어 파트마다 `*SECTION` + `*PART`(mid = pid) + `*ELEMENT_*` 블록을 쓴다. 헬퍼 `_solid_card2()` 가 노드 수로 LS-DYNA 형식을 고른다:

| 노드 수 | 처리 | ELFORM |
|---|---|---|
| 4 / 10 | Tet4 (Tet10 은 코너 4개로 **선형화**) | 10 |
| 6 / 15 | Wedge6 (Wedge15 선형화) | 15 |
| 8 / 20 | Hex8 (Hex20 선형화) | 1 |
| 5 | Pyramid → 퇴화 hex | 1 |

셸은 Tri3/Tri6 → `N4=N3`, Quad4/Quad8 → 코너 4개, `*SECTION_SHELL` ELFORM=2 (Belytschko-Tsay), NIP=3.

### 재료

`_write_materials()` (라인 2960) 는 파트(=body)마다 `*MAT_ELASTIC` 카드를 하나씩 쓴다. **현재 물성값은 Engineering Data 에서 읽지 않는다** — 모든 파트에 기본 강재(SI: ρ=7850, E=2e11, ν=0.3 / mm-tonne-s: ρ=7.85e-9, E=2e5)를 쓰고, body 의 재료 이름은 `$ Part: … Mat: …` 주석으로만 남는다. `on_export` 가 계산하는 `mat_e_scale`/`mat_rho_scale` 도 쓰이지 않는다. 아래 "알려진 한계" 참고.

### Named Selection

`_write_named_selections()` (라인 3017) 가 NS 마다 `*SET_NODE_TITLE` (nsid 1부터, 한 줄 8개) 을 쓴다. 노드 추출은 `_get_ns_node_ids()` (라인 3078) 하나뿐 — `ns.Generate()` 후 `ns.Location.Ids` 를 `MeshData.GetNodeIdsFromRegionIds` 로 노드 ID 로 바꾼다. 노드가 0 개인 NS 는 건너뛴다.

`_get_ns_node_ids_geometry()` (라인 3117 — face 중심·법선 평면에서 tolerance 안의 노드를 고르는 방식) 는 남아 있지만 **어디서도 호출되지 않는다**. 그래서 다이얼로그의 tolerance 값은 현재 출력에 영향이 없다 (로그에만 찍힘).

### 접촉 / Segment Set

`Connections` 의 모든 `ContactRegion` 을 LS-DYNA 접촉으로 변환:

- `_collect_elements()` (라인 3224) → `_enumerate_element_faces()` (라인 3254, `_FACE_TOPO` 로 요소 면 열거, 2차 요소는 코너 노드만) → `_extract_surface_faces()` (라인 3281, 한 번만 쓰인 면 = 외부면)
- `_get_contact_segment_faces()` (라인 3303) — Source/Target 의 region ID → `GetNodeIdsFromRegionIds` → 모든 노드가 그 region 에 속한 외부면만 segment 로
- `_average_elem_size()` (라인 3287) — 평균 edge 길이. **더 촘촘한 쪽이 Slave**
- `_write_segment_set()` (라인 3341) — `*SET_SEGMENT_TITLE` (ID 100 부터 접촉당 2개)
- `_write_contacts()` (라인 3356) — `ContactType` 이 Bonded 면 `*CONTACT_TIED_SURFACE_TO_SURFACE`, 아니면 `*CONTACT_AUTOMATIC_SURFACE_TO_SURFACE`. Frictional 이면 `FrictionCoefficient` (읽기 실패 시 0.3) 를 FS=FD 로. Card 3 는 필수라 기본값으로 쓰고, DT 는 비워 LS-DYNA 기본(1e20)을 쓰게 한다

샘플 출력은 `[[Mechanical/MXSimulator/test.k]]` (10000+ 라인, 실제 ASTM E8 시편 사례). LS-DYNA 키워드 참조는 `[[Docs/LSDyna/Vol_I.txt]]`.

### 진입점

`on_export` (라인 2598) 가 메인 핸들러 — MeshData('Global') 확인(노드 0 이면 "Generate Mesh" 안내) → 노드 → 요소/파트 → (옵션) 재료 → (옵션) NS → (옵션) 접촉 → `*END` → 파일 쓰기 → 요약 MessageBox. `on_browse` (라인 2586) 가 출력 경로 선택, `log()` (라인 2580) 가 다이얼로그 텍스트박스 로그.

### 알려진 한계 (2026-09-16 코드 리뷰)

문서를 코드와 대조하다 확인한 것. 전부 Mechanical GUI 에서만 검증 가능해서 코드는 아직 고치지 않았다.

| # | 한계 | 영향 |
|---|---|---|
| 1 | **재료 물성이 항상 기본 강재** (`_write_materials`) | 알루미늄·수지 등 다른 재료 파트의 `.k` 강성·밀도가 틀림 — 가장 큰 문제. 후보 API `Material.GetAsDictionary()` 는 XML 에만 있고 실측 전 |
| 2 | 셸 두께가 항상 `1.0` (단위계 무관) | SI 로 내보내면 1 m 두께 셸 |
| 3 | PID = body 열거 순서, 엘리먼트 파트는 `PartId`/`BodyId` 속성 | 두 번호 체계가 다르면 파트·재료가 엇갈릴 수 있음 (속성을 못 읽으면 전부 PID 1) |
| 4 | `NodeById`/`ElementById` 를 `1..Count` 로 순회 | ID 가 연속이 아니면 일부 노드·요소가 **조용히 빠짐** (하나라도 읽히면 폴백 안 탐) |
| 5 | 파트 ELFORM 을 첫 요소 노드 수로 결정 | hex+wedge 혼합 파트에서 섹션 형식이 일부 요소와 불일치 |
| 6 | 2차 요소는 코너 노드로 선형화 | 의도된 동작이지만 정확도 저하 — 출력에 표시 없음 |
| 7 | tolerance 입력 미사용 (지오메트리 NS 경로가 호출 안 됨) | UI 가 오해를 줌 |
