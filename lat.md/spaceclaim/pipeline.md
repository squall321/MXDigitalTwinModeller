---
lat:
  require-code-mention: true
---

# Pipeline

여러 단계 (Simplify → Material → Contact → Mesh → Export) 를 한 번에 실행하는 자동화. UI 다이얼로그로 일괄 실행하는 BatchPipeline 과, SpaceClaim Script Editor 에서 직접 실행하는 IronPython 스크립트 16개 + `pipeline.py` 두 가지 진입점을 제공한다.

자동화의 목표는 동일한 시편 형상에 대해 사용자가 다이얼로그를 16번 클릭하지 않고도 처음부터 끝까지 가도록 하는 것. PyAnsys 통합도 IronPython 스크립트에 포함되어 있어 외부 Python 환경에서도 호출 가능.

## BatchPipeline 커맨드

리본 버튼 한 번 클릭으로 Simplify → Material → Contact → Mesh → Export 5단계를 순차 실행. 각 단계는 사용자가 미리 다른 다이얼로그에서 저장한 설정을 그대로 사용한다.

체크박스로 단계 토글 가능 (예: Simplify 는 건너뛰고 Contact 부터 실행). 진행률 + 단계별 로그 표시.

리본 진입점: `[[Commands/Pipeline/BatchPipelineCommand.cs#BatchPipelineCommand]]`. 다이얼로그: `[[UI/Dialogs/BatchPipelineDialog.cs]]`.

## IronPython 스크립트

`Scripts/` 폴더에 단계별 16개 + 통합 1개로 정리되어 있다. 각 스크립트는 단일 기능에 대응하고, 헤드리스 모드 (다이얼로그 없이) 로 실행 가능. SpaceClaim 의 Script Editor 또는 외부 PyAnsys 환경에서 호출 가능.

### 단계별 스크립트 (01–16)

각 번호는 워크플로우의 단계 순서를 의미한다:

- `[[Scripts/01_simplify.py]]` — 작은 fillet/hole 제거
- `[[Scripts/02_material.py]]` — 재료 물성 설정
- `[[Scripts/03_contact.py]]` — 접촉면 자동 검출
- `[[Scripts/04_mesh.py]]` — 메쉬 사이징 + 생성
- `[[Scripts/05_export.py]]` — `.k` 출력
- `[[Scripts/06_tensile.py]]` — ASTM 인장시편 생성
- `[[Scripts/07_bending.py]]` — 굽힘 시편 생성
- `[[Scripts/08_compression.py]]` — 압축 시편 생성
- `[[Scripts/09_cai.py]]` — CAI 시편
- `[[Scripts/10_fatigue.py]]` — 피로 시편
- `[[Scripts/11_joint.py]]` — Joint 시편
- `[[Scripts/12_laminate.py]]` — 적층 시편
- `[[Scripts/13_load.py]]` — 하중 정의
- `[[Scripts/14_simulation.py]]` — 시뮬레이션 설정
- `[[Scripts/15_step_export.py]]` — STEP 출력
- `[[Scripts/16_conformal_mesh.py]]` — Conformal Mesh

각 스크립트는 단일 함수 진입점 패턴: `def main(): ...`. 호출 예: `exec(open(r'd:\MXDigitalTwinModeller\Scripts\16_conformal_mesh.py').read())`.

### 통합 파이프라인 스크립트

여러 단계를 묶어 한 번에 실행. PyAnsys (`ansys-mechanical-core`) 와 연동하여 SpaceClaim → Mechanical 까지 끊김 없이 자동화.

진입점: `[[Scripts/pipeline.py]]`.

## Specimen YAML

시편 생성 명령 (`[[specimens]]` 참조) 들이 생성하는 메타데이터 파일. ASTM 규격, 게이지 치수, 재료, 라미나 정보 등을 YAML 로 저장. Mechanical 측 [[material-calibrator]] 가 이 YAML 을 읽어 specimen detection 수행.

작성자: `[[Services/TensileTest/SpecimenMetadataService.cs#SpecimenMetadataService]]`. YAML 직렬화 유틸: `[[Core/IO/YamlWriter.cs]]`.

예시 출력은 `[[test_specimen.yaml]]` (인장 시편), `[[Test/ASTM_E8_optimization/specimen.yaml]]` (실제 시나리오).
