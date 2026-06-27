---
lat:
  require-code-mention: true
---

# Post-Process

Modal/Transient 해석 결과를 분석하고, 별도의 외부 viewer 를 띄우거나 LS-DYNA `.k` 파일로 내보낸다. 두 개의 다이얼로그가 이 카테고리: PostProcessDialog (결과 추출 + 외부 뷰어) 와 ExportKFileDialog (Mechanical 메쉬를 `.k` 변환).

`Post-Process` / `Export K-File` 두 툴바 버튼. 외부 viewer 는 PyAnsys-PostProcess + matplotlib 의 standalone 환경.

## Post-Process Dialog

`show_postprocess_dialog` → `PostProcessDialog` (`[[Mechanical/MXSimulator/main.py#PostProcessDialog]]`, 라인 1642). 사용자가 분석을 선택하고 NS 패턴을 입력하면, 그 NS 들에 대한 결과 (Total Deformation, Stress, Strain 등) 를 추출해 외부 뷰어로 launch.

### 분석 선택

`_populate_analyses()` (라인 1799) 가 현재 프로젝트의 모든 Analysis 를 ListBox 에 표시. 사용자가 선택하면 `_get_selected_analysis()` (라인 1823) 로 객체 획득.

### NS 패턴 필터

사용자가 `Contact_+*` 같은 와일드카드 패턴을 입력하면 그 패턴에 매칭되는 NS 만 결과 추출 대상으로. `_get_ns_patterns()` (라인 1829) 가 입력 텍스트를 파싱.

### 결과 추가

`on_add_results` (라인 1851) 가 선택된 NS 들에 대해 Total Deformation, Equivalent Stress 등 결과를 자동으로 Solution 트리에 추가. `_safe_float()` 헬퍼로 None 안전 변환.

### 외부 뷰어 실행

`on_export_launch` (라인 1997) 가 결과 메타데이터를 JSON 으로 dump 한 뒤 외부 Python 프로세스로 viewer 를 띄움. Python 인터프리터 자동 검출은 `_find_python()` (라인 2110), 실제 launch 는 `_launch_viewer()` (라인 2077).

뷰어 자체:
- `[[Mechanical/MXSimulator/postprocess/visualizer.py]]` — matplotlib 기반 시각화 (500+ 라인)
- `[[Mechanical/MXSimulator/postprocess/analyzer.py]]` — 결과 분석 로직
- `[[Mechanical/MXSimulator/postprocess/runner.py]]` — 외부 진입점
- `[[Mechanical/MXSimulator/postprocess/build_viewer.bat]]` — PyInstaller 빌드 스크립트

자세한 환경 설정은 [[build-deploy#Python 환경 (Material Calibrator)]].

## K-File Export

`show_export_kfile_dialog` → `ExportKFileDialog` (`[[Mechanical/MXSimulator/main.py#ExportKFileDialog]]`, 라인 2173). Mechanical 측 메쉬를 LS-DYNA 의 `.k` 키워드 파일로 변환한다 ([[mesh#Mesh 내보내기 (.k)]] 의 Mechanical 측 버전).

### 메쉬 노드/엘리먼트

`_write_nodes()` (라인 2425) 가 모든 노드를 `*NODE` 카드로. `_write_elements_parts()` (라인 2481) 가 엘리먼트를 Part 별로 분류해 `*ELEMENT_SOLID` / `*ELEMENT_SHELL` 카드로 출력. Solid 카드 2번째 행은 헬퍼 `_solid_card2()` 가 처리.

### 재료

`_write_materials()` (라인 2655) 가 Engineering Data 의 재료를 `*MAT_*` 카드로 변환. body → part ID 매핑은 호출자가 전달한 `body_pid_map`.

### Named Selection

`_write_named_selections()` (라인 2711) 가 모든 NS 를 `*SET_NODE_LIST` 로 변환. 두 가지 노드 추출 방식:

- `_get_ns_node_ids()` (라인 2772) — 일반 NS (mesh 기반)
- `_get_ns_node_ids_geometry()` (라인 2811) — 지오메트리 기반 NS (face/edge 에서 노드 lookup, tolerance 안에 들어오는 메쉬 노드)

### 접촉 / Segment Set

LS-DYNA Tied/Contact 카드를 위한 Segment Set 생성:

- `_collect_elements()` (라인 2918) — 모든 엘리먼트 모음
- `_enumerate_element_faces()` (라인 2948) — 각 엘리먼트의 면 열거
- `_extract_surface_faces()` (라인 2975) — 외부면 (한 번만 사용된 면) 추출
- `_average_elem_size()` (라인 2981) — 엘리먼트 평균 크기 (tolerance 자동 조정용)
- `_get_contact_segment_faces()` (라인 2997) — NS 가 가리키는 면을 segment 로 매핑
- `_write_segment_set()` (라인 3035) — `*SET_SEGMENT` 카드 출력
- `_write_contacts()` (라인 3049) — `*CONTACT_TIED_*` 카드 출력

샘플 출력은 `[[Mechanical/MXSimulator/test.k]]` (10000+ 라인, 실제 ASTM E8 시편 사례). LS-DYNA 키워드 참조는 `[[Docs/LSDyna/Vol_I.txt]]`.

### 진입점

`on_export` (라인 2295) 가 메인 핸들러. `on_browse` (라인 2284) 가 출력 경로 선택. 진행 상황은 `log()` (라인 2278) 로 다이얼로그 텍스트박스에 출력.
