# MX Digital Twin Modeller

복합재료 시편 모델링 및 시뮬레이션을 위한 ANSYS 확장 패키지

- **SpaceClaim Add-In**: 시편 모델링, 메쉬, 접촉 검출, Conformal Mesh
- **Mechanical ACT Extension**: 하중 정의 및 시뮬레이션 설정

**멀티버전 지원**: SpaceClaim V251/V252, Mechanical V252

## 기능

### SpaceClaim Add-In

- **시편 모델링**: 인장(26종), 굽힘, 압축, CAI, 피로, 접합, 적층 시편
- **메쉬**: 방향별 사이징, 배치 메쉬, 곡률/근접 크기 함수
- **접촉 검출**: 평면/원통/에지 접촉, Named Selection 자동 생성
- **Conformal Mesh**: STEP 임포트 → 계면 검출 → Share Topology → 메쉬 → .k 내보내기
- **일괄 파이프라인**: Simplify → Material → Contact → Mesh → Export
- **IronPython 스크립트**: 16개 개별 기능 + pipeline.py (PyAnsys 통합)

### Mechanical ACT Extension

- **MXSimulator 탭**: 하중 정의 및 시뮬레이션 설정
- **Cap Vibration**: 캡 진동 하중 자동 설정 (WPF 대화상자)

## 설치 방법

### 1. 환경 설정

`.env.example` 파일을 `.env`로 복사하고 실제 환경에 맞게 경로를 수정하세요.

```ini
# SpaceClaim V251 경로
SPACECLAIM_V251_INSTALL_PATH=C:\Program Files\ANSYS Inc\v251\SpaceClaim
SPACECLAIM_V251_API_PATH=C:\Program Files\ANSYS Inc\v251\SpaceClaim\Api\V251\bin\x64\Debug
SPACECLAIM_V251_EXE=C:\Program Files\ANSYS Inc\v251\SpaceClaim\SpaceClaim.exe

# SpaceClaim V252 경로
SPACECLAIM_V252_INSTALL_PATH=C:\Program Files\ANSYS Inc\v252\SpaceClaim
SPACECLAIM_V252_API_PATH=C:\Program Files\ANSYS Inc\v252\SpaceClaim\Api\V252\bin\x64\Debug
SPACECLAIM_V252_EXE=C:\Program Files\ANSYS Inc\v252\SpaceClaim\SpaceClaim.exe

# AddIn 출력 경로
ADDIN_OUTPUT_PATH_V251=C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V251
ADDIN_OUTPUT_PATH_V252=C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252
```

### 2. 빌드

Visual Studio 2022에서 솔루션을 열고 빌드 구성을 선택합니다.

#### 빌드 구성:
- **Debug-V251**: V251용 디버그 빌드 → `ADDIN_OUTPUT_PATH_V251`
- **Release-V251**: V251용 릴리즈 빌드 → `ADDIN_OUTPUT_PATH_V251`
- **Debug-V252**: V252용 디버그 빌드 → `ADDIN_OUTPUT_PATH_V252`
- **Release-V252**: V252용 릴리즈 빌드 → `ADDIN_OUTPUT_PATH_V252`

**기본 구성**: Debug-V252

### 3. SpaceClaim에서 확인

해당 버전의 SpaceClaim을 실행하고 Ribbon에서 "MX Modeller" 탭을 확인합니다.

## 개발 환경 설정

### Visual Studio 2022

1. .NET Framework 4.7.2 설치
2. 빌드 구성 선택 (Debug-V251 또는 Debug-V252)
3. 디버그 프로파일 선택:
   - **SpaceClaim V251 Debug**: V251에서 디버깅
   - **SpaceClaim V252 Debug**: V252에서 디버깅

`launchSettings.json`에 두 가지 프로파일이 설정되어 있습니다.

### 디버깅

1. 빌드 구성 선택 (예: Debug-V252)
2. 디버그 프로파일 선택 (예: SpaceClaim V252 Debug)
3. F5를 누르면 해당 버전의 SpaceClaim이 실행되고 AddIn이 로드됩니다
4. 중단점을 설정하여 디버깅할 수 있습니다

## 프로젝트 구조

```text
MXDigitalTwinModeller/
├── SpaceClaim/                     # SpaceClaim Add-In (C# .NET)
│   ├── Core/                       # 공통 모듈
│   │   ├── Geometry/              # 기하학 유틸리티
│   │   ├── Commands/              # 커맨드 기본 클래스
│   │   └── UI/                    # UI 헬퍼
│   ├── Commands/                  # 커맨드 구현
│   │   ├── TensileTest/
│   │   ├── ConformalMesh/
│   │   └── Pipeline/
│   ├── Services/                  # 비즈니스 로직
│   │   ├── Contact/               # 접촉 검출
│   │   ├── Mesh/                  # 메쉬 설정
│   │   ├── ConformalMesh/         # Conformal Mesh (SpatialIndex 포함)
│   │   └── Export/                # KFilePostProcessor
│   ├── Models/                    # 데이터 모델
│   ├── UI/Dialogs/               # WinForms 대화창
│   └── Scripts/                   # IronPython 스크립트 (01-16, pipeline.py)
│
├── Mechanical/                    # ANSYS Mechanical ACT Extension
│   └── MXSimulator/
│       ├── extension.xml          # ACT 확장 정의
│       ├── main.py                # IronPython 로직 (WPF UI)
│       ├── images/                # 리본 아이콘
│       └── README.md
│
├── Installer/                     # WiX 인스톨러
│   ├── MXDigitalTwinModeller.wxs
│   └── MXDigitalTwinModeller.msi
│
└── Docs/                          # 문서
    └── LSDyna/                    # LS-DYNA 키워드 참조
```

## 사용 방법

### SpaceClaim

1. SpaceClaim 실행
2. "MX Modeller" 탭 클릭
3. 원하는 기능 선택:
   - **Parametric**: 시편 모델링 (인장, 굽힘, 압축 등)
   - **Mesh**: 메쉬 설정, 접촉 검출, Conformal Mesh
   - **Pipeline**: 일괄 실행

### Mechanical

1. ANSYS Mechanical 실행
2. `MXSimulator` 탭 클릭
3. `Load` 패널 → `Cap Vibration` 버튼
4. 진동 파라미터 입력 후 Apply

### Python 스크립트 (PyAnsys)

```python
# SpaceClaim Script Editor에서 실행
exec(open(r'd:\MXDigitalTwinModeller\Scripts\16_conformal_mesh.py').read())
```

## 지원 규격

| 규격 | 타입 | 게이지 길이 | 게이지 폭 |
|------|------|------------|----------|
| ASTM E8 | Standard | 50 mm | 12.5 mm |
| ASTM E8 | SubSize | 25 mm | 6 mm |
| ASTM D638 | Type I | 50 mm | 13 mm |
| ASTM D638 | Type II | 57 mm | 6 mm |

## 라이선스

Copyright © 2026 MX

## 버전

v1.0.0
