# Mechanical ACT Extension (MXSimulator)

ANSYS Mechanical 에 `MXSimulator` 탭을 추가하는 ACT Extension. IronPython + WPF 로 작성되었고, 모든 다이얼로그가 `[[Mechanical/MXSimulator/main.py]]` 안에 정의되어 있다 (단일 파일 5000+ 라인).

진입점은 `[[Mechanical/MXSimulator.xml]]` (ACT 정의 파일) — 이 파일이 두 개의 툴바를 정의하고, 각 버튼이 `main.py` 의 `show_*_dialog` 함수를 호출한다.

## 툴바 구조

### MX Digital Twin Simulation (메인)

| 버튼 | 콜백 | 기능 |
|---|---|---|
| Face Pair NS | `show_face_pair_dialog` | [[face-pair-ns]] |
| Named Selections | `show_ns_dialog` | [[face-analysis]] |
| Modal Analysis | `show_modal_dialog` | [[scenarios#Modal Analysis]] |
| Add Scenario | `show_scenario_dialog` | [[scenarios#Cap Vibration Scenario]] |
| Post-Process | `show_postprocess_dialog` | [[postprocess#Post-Process Dialog]] |
| Export K-File | `show_export_kfile_dialog` | [[postprocess#K-File Export]] |
| Tied Check | `show_tied_check_dialog` | [[tied-check]] |

### MX Material Twin Simulation (재료 캘리브레이션)

| 버튼 | 콜백 | 기능 |
|---|---|---|
| Tensile Test | `show_material_twin_dialog` | [[material-calibrator]] |

## 단일 파일 구조

모든 다이얼로그가 `[[Mechanical/MXSimulator/main.py]]` 한 파일에 들어있다. WPF `Window` 를 상속한 8개 클래스가 정의됨:

- `NSDialog` — [[face-analysis]] (라인 186)
- `ModalDialog` — [[scenarios#Modal Analysis]] (라인 564)
- `ScenarioDialog` — [[scenarios#Cap Vibration Scenario]] (라인 689)
- `FacePairDialog` — [[face-pair-ns]] (라인 1103)
- `PostProcessDialog` — [[postprocess#Post-Process Dialog]] (라인 1642)
- `ExportKFileDialog` — [[postprocess#K-File Export]] (라인 2173)
- `TiedContactCheckDialog` — [[tied-check]] (라인 3210)
- `MaterialTwinDialog` — [[material-calibrator]] (라인 4210)

각 클래스의 `show_*_dialog(analysis)` 진입점이 외부에서 호출 가능한 인터페이스. 다이얼로그는 `analysis` (선택된 Analysis 객체) 를 인자로 받는다.

## 공용 헬퍼

- `on_init` (라인 58) — Extension 초기화 콜백. Shared DLL 로드 시도.
- `classify_normal_direction()` (라인 87) — 면 법선을 ±X/Y/Z 6방향으로 분류. [[face-analysis]] 와 [[face-pair-ns]] 양쪽에서 사용.
- `_lbl`, `_tb`, `_row`, `_hdr` (라인 128–171) — WPF 위젯 생성 헬퍼.
- `Initialize` / `Finalize` (라인 5256, 5260) — ACT 라이프사이클 콜백.

## 공용 Core DLL 로드

Shared `MXDigitalTwinModeller.Core.dll` 을 main.py 시작 시 동적으로 로드한다:

```python
dll_path = os.path.join(os.path.dirname(__file__), "bin", "MXDigitalTwinModeller.Core.dll")
if os.path.exists(dll_path):
    clr.AddReferenceToFileAndPath(dll_path)
    from MXDigitalTwinModeller.Core.Spatial import SpatialIndex
```

DLL 자체는 빌드 시 [[build-deploy#Mechanical ACT Extension 자동 배포]] 로 자동 복사된다.

## 부속 자료

- `[[Mechanical/MXSimulator/README.md]]` — 사용자 설치 가이드
- `[[Mechanical/MXSimulator/USAGE.md]]` — 기능별 사용법
- `[[Mechanical/MXSimulator/TROUBLESHOOTING.md]]` — 문제 해결
- `[[Mechanical/MXSimulator/IMPLEMENTATION_PLAN.md]]` — Cap Vibration 단계별 구현 계획서 (Phase 1/2/3/4)
- `[[Mechanical/MXSimulator/ANSYS_Mechanical_API_Reference.txt]]` — 검증된 ACT API 모음 (1000+ 라인)
- `[[Mechanical/MXSimulator/MATERIAL_TWIN_STATUS.md]]` — Material Calibrator 진행 현황

## 진단 / 환경 검증 스크립트

main.py 와 별도로, ACT 환경 자체를 점검하는 standalone 스크립트가 있다:

- `[[Mechanical/MXSimulator/diagnose_act.py]]` — ACT 로드 상태 점검
- `[[Mechanical/MXSimulator/diagnose_extensions.py]]` — 설치된 Extension 목록 + 충돌 검사
- `[[Mechanical/MXSimulator/diagnose_material_twin.py]]` — Material Twin venv 상태
- `[[Mechanical/MXSimulator/verify_all_apis.py]]` — main.py 가 쓰는 모든 ACT API 의 실존 여부 검증
- `[[Mechanical/MXSimulator/explore_geometry_api.py]]` — Geometry API 탐색용 sandbox
- `[[Mechanical/MXSimulator/test_python_env.py]]` — IronPython 환경 자체 점검
