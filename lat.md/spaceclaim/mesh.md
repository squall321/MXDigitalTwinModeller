---
lat:
  require-code-mention: true
---

# Mesh

해석 전처리의 메쉬/재료/접촉/내보내기 전 과정을 자동화한다. 단순 메쉬 설정부터 STEP 임포트 후 계면을 인식해 Share Topology 까지 묶어주는 Conformal Mesh, 외부 Gmsh CLI 연동까지 포함한다.

이 카테고리의 모든 기능은 [[spaceclaim-addin#리본 구조]] 의 `Mesh` / `DT Mesher` 그룹에 등록된 Command 들이다.

## Material 정의

복합재/금속 재료 물성을 입력해 SpaceClaim 의 Property Table 에 저장한다. 이후 `.k` 익스포트 시 자동으로 `*MAT_*` 카드로 변환됨.

리본 진입점: `[[Commands/Material/MaterialCommand.cs#MaterialCommand]]`. 다이얼로그: `[[UI/Dialogs/MaterialDialog.cs]]`.

## Mesh 설정

방향별 사이징, 곡률/근접 크기 함수, 배치 메쉬 설정을 한 다이얼로그에서 구성. 메쉬 미리보기는 SpaceClaim Mesh 모드 전환으로 확인.

리본 진입점: `[[Commands/Mesh/ApplyMeshSettingsCommand.cs#ApplyMeshSettingsCommand]]`. 다이얼로그: `[[UI/Dialogs/MeshSettingsDialog.cs]]`.

## Mesh 내보내기 (.k)

설정된 메쉬를 LS-DYNA `.k` 파일로 출력. 노드, 엘리먼트, 파트, 재료, 접촉, Segment Set, Named Selection 까지 일관되게 변환. KFile 변환 규칙은 `[[Docs/LSDyna/Vol_I.txt]]` 의 키워드 참조에 맞춰 검증됨.

리본 진입점: `[[Commands/Mesh/ExportMeshCommand.cs#ExportMeshCommand]]`.

## Body Simplify

해석에 불필요한 작은 fillet, hole, chamfer 를 자동 제거. 임계값 기반.

리본 진입점: `[[Commands/Simplify/SimplifyCommand.cs#SimplifyCommand]]`. 다이얼로그: `[[UI/Dialogs/SimplifyDialog.cs]]`.

## 접촉면 검출

바디 간 접촉면을 자동 검출 (평면/원통/에지). 거리 + 법선 반대 방향 조건으로 페어링하고, 각 접촉 쌍에 대해 Named Selection 을 생성한다. 후속 Mechanical 측 Tied Contact ([[tied-check]]) 의 입력이 됨.

리본 진입점: `[[Commands/Contact/DetectContactCommand.cs#DetectContactCommand]]`. 다이얼로그: `[[UI/Dialogs/ContactDetectionDialog.cs]]`.

## STEP 내보내기

선택된 바디(들)을 STEP (.stp / .step) 으로 출력. Mechanical 측 [[face-analysis]] 의 입력이 됨.

리본 진입점: `[[Commands/Export/ExportStepCommand.cs#ExportStepCommand]]`.

## 하중 정의

면 또는 Named Selection 에 하중(Force, Pressure)을 정의. SpaceClaim Group 으로 저장되어 `.k` 익스포트 시 `*LOAD_*` 카드로 변환.

리본 진입점: `[[Commands/Load/LoadCommand.cs#LoadCommand]]`. 다이얼로그: `[[UI/Dialogs/LoadDefinitionDialog.cs]]`.

## 시뮬레이션 설정

해석 타입 (Static / Modal / Transient), 시간/주파수 범위, output 빈도 등 솔버 입력 메타데이터를 한 폼에서 설정. SpaceClaim Property 로 저장되며 `.k` 출력 시 헤더 카드 (`*CONTROL_*`) 로 반영.

리본 진입점: `[[Commands/Simulation/SimulationSetupCommand.cs#SimulationSetupCommand]]`. 다이얼로그: `[[UI/Dialogs/SimulationSetupDialog.cs]]`.

## Conformal Mesh

STEP 파일을 임포트해 기존 모델과의 계면을 자동 검출하고 Share Topology 처리 후 메쉬 → `.k` 출력까지 한 번에 처리한다. SpatialIndex 로 거리 기반 페어링 가속.

리본 진입점: `[[Commands/ConformalMesh/ConformalMeshCommand.cs#ConformalMeshCommand]]`. 다이얼로그: `[[UI/Dialogs/ConformalMeshDialog.cs]]`. 서비스: `[[Services/ConformalMesh/ConformalMeshService.cs]]`, `[[Services/ConformalMesh/SpatialIndex.cs]]`.

## Void Cut

지정된 패턴 (랜덤 분포, 격자, 곡선 따라) 으로 모재에서 보이드(빈 공간)를 컷 한다. 라미나 결함 시뮬레이션이나 위상최적화 결과 반영에 사용.

리본 진입점: `[[Commands/VoidCut/VoidCutCommand.cs#VoidCutCommand]]`. 다이얼로그: `[[UI/Dialogs/VoidCutDialog.cs]]`. 서비스: `[[Services/VoidCut/VoidCutService.cs#VoidCutResult]]`.

## Gmsh Mesher

내장 SpaceClaim 메셔 대신 외부 [Gmsh](https://gmsh.info) CLI 를 호출해 메쉬를 만든다. 더 정교한 사이즈 컨트롤과 hex-dominant 메쉬 지원이 필요할 때 사용. `.geo` 작성 → Gmsh 실행 → `.msh` 파싱 → SpaceClaim 시각화 또는 직접 `.k` 출력의 4단계 파이프라인.

### Gmsh 커맨드

리본 버튼과 다이얼로그. 사용자가 메쉬 파라미터를 입력하고 Run 을 누르면 아래 4개 서브 단계가 순차 실행됨.

진입점: `[[Commands/GmshMesher/GmshMesherCommand.cs#GmshMesherCommand]]`. 다이얼로그: `[[UI/Dialogs/GmshMesherDialog.cs]]`.

### Gmsh CLI 엔진

Gmsh 실행파일을 찾고 sub-process 로 호출한다. stdout/stderr 캡처해 다이얼로그에 표시. timeout 처리, exit code 검사.

구현: `[[Services/GmshMesher/GmshCliEngine.cs#GmshCliEngine]]`. 인터페이스: `[[Services/GmshMesher/IGmshEngine.cs]]`.

### .geo 라이터

SpaceClaim 의 바디/면을 Gmsh `.geo` 스크립트 형식으로 직렬화한다. 사이즈 필드, Physical Group (Named Selection 매핑) 포함.

구현: `[[Services/GmshMesher/GmshGeoWriter.cs]]`.

### .k 파일 변환

Gmsh 가 출력한 `.msh` 를 LS-DYNA `.k` 로 변환. Physical Group → Part / Named Segment Set 매핑.

구현: `[[Services/GmshMesher/GmshKFileWriter.cs]]`.

### .msh 파서

Gmsh `.msh` v4 포맷 파서. 노드/엘리먼트/Physical Group 읽기. 메쉬 시각화 ([[mesh#Mesh Visualization]]) 의 입력.

구현: `[[Services/GmshMesher/GmshMshParser.cs]]`.

### Mesh Visualization

Gmsh 결과를 SpaceClaim 의 임시 디자인 객체로 표시 (와이어프레임 또는 surface). 사용자가 메쉬 품질을 시각적으로 확인한 뒤 Save 를 누르면 `.k` 로 변환됨.

구현: `[[Services/GmshMesher/MeshVisualizationService.cs]]`.
