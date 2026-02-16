# MXSimulator - ANSYS Mechanical ACT Extension

ANSYS Mechanical에 MX Digital Twin Simulator 기능을 추가하는 ACT Extension입니다.

## 구조

```
MXSimulator/
├── extension.xml          # ACT 확장 정의 (탭, 패널, 버튼, 콜백)
├── main.py                # IronPython 로직 (WPF 대화상자, 콜백)
├── bin/
│   └── MXDigitalTwinModeller.Core.dll  # Shared DLL (공용 로직)
├── images/
│   └── cap_vibration.png  # 리본 아이콘
├── IMPLEMENTATION_PLAN.md # 구현 계획서
└── README.md              # 이 파일
```

## 기능

### MXSimulator 탭
- **Load 패널**
  - **Cap Vibration Time Force** (Phase 1 구현 완료)

## Phase 1: STEP Import + Face Analysis + Named Selection

### 구현 완료 기능
✅ STEP 파일 임포트 (GeometryImport API)
✅ 면 법선 방향 자동 분석 (±X, ±Y, ±Z)
✅ 방향별 Named Selection 자동 생성 (`Contact_+Z`, `Contact_-Z`, etc.)
✅ 실시간 로그 출력

### 사용법

1. **Mechanical에서 모델 준비**
   - 기존 모델 열기 또는 새 프로젝트 생성
   - Geometry 추가 (기존 바디가 있어도 무방)

2. **Cap Vibration 대화창 열기**
   - `MXSimulator` 탭 → `Load` 패널 → `Cap Vibration` 버튼 클릭

3. **STEP 파일 임포트**
   - `Browse...` 버튼으로 STEP 파일 선택 (.stp, .step)
   - `Import and Analyze Faces` 버튼 클릭
   - 로그에서 진행 상황 확인:
     ```
     === Starting STEP Import ===
     Importing STEP file...
     Found 3 bodies in model
     Analyzing face normals...
       Body 0: 6 faces
       Body 1: 8 faces
       Body 2: 10 faces
     Face analysis complete: 24 faces

     Faces by direction:
       +X: 4 faces
       +Y: 3 faces
       +Z: 6 faces
       -X: 4 faces
       -Y: 3 faces
       -Z: 4 faces
     ```

4. **Named Selection 생성**
   - `Create Named Selections` 버튼 클릭
   - 방향별 NS 자동 생성: `Contact_+Z`, `Contact_-Z`, `Contact_+X`, ...
   - Model Tree에서 Named Selections 확인

### 검증 완료 API

| API | 상태 | 설명 |
|-----|------|------|
| `GeometryImport.Import()` | ✅ | STEP 파일 임포트 |
| `body.GetGeoBody()` | ✅ | IGeoBody 객체 획득 |
| `geo_body.Faces[i]` | ✅ | 면 컬렉션 인덱싱 |
| `face.GetFaceNormal(u, v)` | ✅ | 면 법선 계산 |
| `Model.AddNamedSelection()` | ✅ | NS 생성 |
| `SelectionManager.CreateSelectionInfo()` | ✅ | 면 선택 |

## 설치

### 수동 설치
1. 이 폴더(`MXSimulator/`)를 통째로 복사:
   ```
   %APPDATA%\ANSYS\v252\ACT\extensions\MXSimulator\
   ```
   실제 경로 예시:
   ```
   C:\Users\<UserName>\AppData\Roaming\ANSYS\v252\ACT\extensions\MXSimulator\
   ```

2. **Shared DLL 복사** (필수)
   - `bin\Debug\MXDigitalTwinModeller.Core.dll`을 `MXSimulator\bin\` 폴더에 복사
   - 또는 SpaceClaim 프로젝트 빌드 후 자동 복사

3. ANSYS Mechanical 재시작

4. 리본에서 `MXSimulator` 탭 확인

### MSI 인스톨러 (향후 계획)
`MXDigitalTwinModeller.msi` 설치 시 SpaceClaim Add-In + Mechanical ACT Extension 동시 배포

## 향후 개발 계획

### Phase 2: Contact Detection (다음 단계)
- 임포트된 STEP 면과 기존 모델의 접촉면 자동 검출
- 거리 기반 + 법선 반대 방향 체크
- Tolerance 설정 UI

### Phase 3: Time Force from CSV
- CSV 파일에서 시간-하중 데이터 읽기
- 방향별 Force 객체 자동 생성 (+Z 면 → +Z 방향 하중)
- Tabular Data 설정

### Phase 4: Modal Superposition Analysis (검증 필요)
- Modal Analysis 자동 생성
- Transient Structural (Modal Superposition) 연결
- 또는 Semi-Auto 모드 (사용자가 수동 연결)

## 개발 노트

### WPF UI
- `System.Windows.Controls` 기반 WPF 대화상자
- WinForms 대신 WPF 사용 이유: Mechanical ACT는 WPF를 권장
- ScrollViewer + TextBox로 로그 출력 구현

### Shared DLL 로딩
```python
dll_path = os.path.join(os.path.dirname(__file__), "bin", "MXDigitalTwinModeller.Core.dll")
if os.path.exists(dll_path):
    clr.AddReferenceToFileAndPath(dll_path)
    from MXDigitalTwinModeller.Core.Spatial import SpatialIndex
```

### ExtAPI 글로벌 변수
- ANSYS Mechanical 런타임에서 `ExtAPI`는 자동으로 제공됨
- `ExtAPI.DataModel.Project.Model`
- `ExtAPI.SelectionManager`

## 트러블슈팅

### "Import could not be resolved" 경고
- IDE(VS Code)에서 IronPython 모듈을 인식하지 못하는 정상적인 현상
- 실제 Mechanical 런타임에서는 정상 작동

### DLL 로드 실패
- `bin/MXDigitalTwinModeller.Core.dll` 파일이 존재하는지 확인
- SpaceClaim 프로젝트를 먼저 빌드했는지 확인

### Named Selection이 생성되지 않음
- 로그에서 에러 메시지 확인
- `geo_body.Faces[i]` 접근 실패 시 ANSYS 버전 확인

## API 참조

- [ANSYS ACT Developer Guide](https://ansyshelp.ansys.com)
- [GeometryImport API](https://scripting.mechanical.docs.pyansys.com/version/stable/api/ansys/mechanical/stubs/v242/Ansys/ACT/Automation/Mechanical/GeometryImport.html)
- [GetGeoBody Method](https://storage.ansys.com/corp/ACT_Reference_Guide_doc_v180/Mechanical/Reference.methode.Ansys.ACT.Automation.Mechanical.Body.GetGeoBody.html)
- [Get Face Normal Example](https://ansyshelp.ansys.com/public/Views/Secured/corp/v251/en/act_script/act_script_examples_get_normal_of_a_face.html)
