---
lat:
  require-code-mention: true
---

# Material Calibrator

실험 데이터 (인장 stress-strain CSV) 로부터 재료 파라미터 (Elastic Modulus, Yield, Bilinear Tangent 등) 를 역산해서 ANSYS Engineering Data 에 자동 등록하는 도구. Mechanical ACT 다이얼로그 + 외부 PyInstaller EXE 의 2-tier 구조.

`Tensile Test` 툴바 버튼 (`MX Material Twin Simulation` 툴바) → `show_material_twin_dialog` → `MaterialTwinDialog` (라인 4210). 사용자가 시편 메타데이터 + CSV 를 선택하면 외부 EXE 가 calibration 을 돌리고 결과를 가져와 Engineering Data 에 추가.

## 다이얼로그 흐름

`[[Mechanical/MXSimulator/main.py#MaterialTwinDialog]]` (라인 4210) 가 메인 UI. 단계:

1. **Specimen Detection** — 시편 메타데이터 자동 검출. 세 가지 소스 시도 ([[material-calibrator#Specimen Detection 소스]]):
   - Workbench Parameters
   - SpaceClaim Specimen YAML
   - JSON fallback
2. **CSV 선택** — 사용자가 실험 데이터 CSV 선택 (`on_select_csv`, 라인 4876). 단순 파싱 (`parse_csv_simple`).
3. **Calibration 실행** — Elastic 또는 Plastic 모드 (`on_calibrate_elastic`, 라인 4961). 외부 EXE 호출.
4. **재료 생성** — 결과를 받아 Engineering Data 에 새 재료 추가 (`on_create_material`, 라인 5151).

## Specimen Detection 소스

`on_detect_specimen` (라인 4479) 가 3개 소스를 순차로 시도:

### Workbench Parameters

`detect_specimen_from_workbench()` (라인 4527) — Workbench 의 Input Parameters 에서 시편 치수/규격을 읽음. SpaceClaim 측 BatchPipeline ([[pipeline#BatchPipeline 커맨드]]) 이 Workbench Parameters 를 채우는 경우 가장 신뢰도 높음.

### Specimen YAML

`detect_specimen_from_yaml()` (라인 4561) — SpaceClaim 측 `[[Services/TensileTest/SpecimenMetadataService.cs#SpecimenMetadataService]]` 가 출력한 YAML 파일을 읽음. 자세한 포맷은 [[pipeline#Specimen YAML]].

YAML 이 없으면 pending 디렉토리에서 후보 파일을 찾아 자동 생성 (`_generate_yaml_from_pending`, 라인 4752).

### JSON Fallback

`detect_specimen_from_json()` (라인 4842) — 위 둘이 모두 실패할 때 사용. 간단한 JSON 입력 형식.

## 외부 Calibrator EXE

복잡한 최적화 (SciPy + 자동미분 의존) 는 IronPython 에서 못 돌리기 때문에 별도 CPython EXE 로 분리. PyInstaller 로 빌드된 standalone 실행파일.

### 빌드

PyInstaller 빌드 스크립트: `[[Mechanical/MXSimulator/calibration/build_calibrator.bat]]`. spec 파일: `[[Mechanical/MXSimulator/calibration/MaterialCalibrator.spec]]`. 결과물: `[[Mechanical/MXSimulator/calibration/MaterialCalibrator.exe]]` (100MB+, build 산출물은 `[[Mechanical/MXSimulator/calibration/build/]]`).

자세한 환경 설정은 [[build-deploy#Python 환경 (Material Calibrator)]].

### 입출력 프로토콜

`MaterialCalibrator.exe` 는 JSON 입력 파일을 인자로 받고, 같은 디렉토리에 `*_result.json` 을 출력. 예시:

- 입력: `[[Mechanical/MXSimulator/calibration/test_input.json]]`
- 출력: `[[Mechanical/MXSimulator/calibration/test_input_result.json]]`

호출은 sub-process. `on_calibrate_elastic` (라인 4961) 안에서 `subprocess.Popen()` 으로 실행 후 result JSON 폴링.

### 핵심 캘리브레이션 로직

PyInstaller 로 패키징되기 전 소스:

- `[[Mechanical/MXSimulator/calibration/elastic_calibrator.py]]` — Elastic Modulus 역산 (initial slope + linear regression)
- `[[Mechanical/MXSimulator/calibration/run_elastic_calibration.py]]` — Elastic mode 진입점
- `[[Mechanical/MXSimulator/calibration/runner.py]]` — 공용 runner (CLI 인자 파싱, 결과 dispatch)
- `[[Mechanical/MXSimulator/calibration/utils/csv_parser.py]]` — CSV 파싱
- `[[Mechanical/MXSimulator/calibration/utils/yaml_parser.py]]` — YAML 파싱
- `[[Mechanical/MXSimulator/calibration/tests/synthetic_data.py]]` — 합성 데이터 생성 (테스트용)

## 재료 등록

캘리브레이션 결과 (`{E, nu, yield, tangent_modulus}` 등) 를 받아 `on_create_material` (라인 5151) 이 Engineering Data 에 새 재료를 만든다. Bilinear Isotropic Hardening, Linear Elastic 등 모델별로 분기.

생성된 재료는 즉시 모든 바디에 할당 가능. 시편 메타데이터 표시는 `show_specimen_detected` (라인 5229) 가 다이얼로그 상단에 출력.

Calibration 검증용 예시 데이터는 `[[Examples/]]` 디렉토리:

- `[[Examples/Steel_ASTM_E8_Elastic.csv]]` — 표준 강철
- `[[Examples/Aluminum_6061_Elastic.csv]]` — 알루미늄
- `[[Examples/Copper_Elastic.csv]]` — 구리
- `[[Examples/ABS_Plastic_ASTM_D638.csv]]` — 플라스틱
- `[[Examples/Steel_Bilinear_Plastic.csv]]` — Bilinear plasticity 검증
- `[[Examples/Steel_Reference_LowNoise.csv]]` — 노이즈 적은 reference 데이터

각 파일의 형식과 사용법은 `[[Examples/README.md]]` 참조.
