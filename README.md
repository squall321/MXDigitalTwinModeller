# MX Digital Twin Modeller

폰 프론트-메탈 설계 + CAE 디지털 트윈을 위한 ANSYS 확장 패키지. **Claude Desktop에서
자연어로 지시하면 실제 SpaceClaim 지오메트리가 생성·수정됩니다** (MCP 브리지).

- **SpaceClaim Add-In**: 무에서 폰 생성(곡면 back/멀티렌즈/플랭크 포트/그릴/버튼/안테나),
  피처 수정, CAE 시편·메싱·라미네이트, 역설계(FeatureGraph)
- **MCP 서버**: 46개 LLM 도구를 Claude Desktop에 노출 (stdio 브리지)
- **Mechanical ACT Extension**: 접촉면 검출, 모달 해석, 시나리오, 포스트프로세스, 물성 캘리브레이션

**대상 환경**: SpaceClaim / ANSYS **v252** (Student 시트에서 45/46 도구 가동; `mesh_with_gmsh`만
STEP-export 라이선스 필요). 현재 버전 **1.5.0**.

## 기능

### 무에서 폰 생성 (13스테이지 S00–S12)

한 개 JSON spec → 슬랩 → 곡면 back → 중공 셸 → 코너 라운딩 → 엣지 챔퍼 → 디스플레이 포켓 →
전면 펀치홀 → 카메라 섬(+멀티렌즈) → 마운팅 홀 → USB-C 플랭크 포트 → back 그릴 → 버튼 →
안테나 슬릿 → 마이크 핀홀 → 최종 필렛. 프리셋: `Examples/presets/{iphone,galaxy}-like.json`.

### CAE

- **인장 시편 26종** (ASTM/ISO/IPC/DMA), 3점 벤딩 리그, Gmsh/Conformal 메싱, 라미네이트
  (생성/슬라이스/면기반), cut_void, simplify, 접촉 검출
- **역설계**: 수입 CAD → 홀/보스/슬릿/필렛/벽/패턴/대칭 FeatureGraph (mod-matrix 검증율 ~83%)

### Mechanical ACT Extension

- 접촉면 자동 검출·명명, 모달 해석, 시나리오 생성, 포스트프로세스 뷰어(MXPostViewer),
  물성 캘리브레이터(MaterialCalibrator)

## 설치 (엔드유저)

`Installer\MXDigitalTwinModeller.msi` 를 실행하면 다음이 한 번에 설치됩니다:

- SpaceClaim Add-In (`...\AddIns\MXDigitalTwinModeller\V252\`)
- Claude Desktop MCP 브리지 (Python-free exe) + **자동 등록**
- 번들 Gmsh, Mechanical ACT 확장, 포스트프로세스 뷰어, 물성 캘리브레이터

설치 후 Claude Desktop 재시작 → SpaceClaim 실행 → 자연어로 설계 (별도 Python/JSON 편집 불필요).

## 빌드 (개발자)

전체 릴리스(EXE들 + MSI):

```powershell
.\build_release.ps1
```

이 스크립트가 PyInstaller로 MXPostViewer.exe / MaterialCalibrator.exe / MCP 브리지 exe 2개를
(없으면) 빌드한 뒤 MSBuild로 DLL + ACT 배포 + WiX MSI 를 생성합니다. **⚠️ 빌드 전 SpaceClaim을
닫으세요** (DLL 잠금).

DLL만 빠르게:

```powershell
MSBuild MXDigitalTwinModeller.csproj /p:Configuration=Debug /p:Platform=AnyCPU
```

빌드 구성은 **Debug / Release** 두 가지이며 **v252 전용**입니다 (csproj의 `MXVersion` 한 값이
DLL + MSI ProductVersion 을 함께 구동 → 이 값만 올리면 in-place 업그레이드).

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
