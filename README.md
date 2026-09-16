# MX Digital Twin Modeller

폰 프론트-메탈 설계 + CAE 디지털 트윈을 위한 ANSYS 확장 패키지. **Claude Desktop에서
자연어로 지시하면 실제 SpaceClaim 지오메트리가 생성·수정됩니다** (MCP 브리지).

- **SpaceClaim Add-In**: 무에서 폰 생성(곡면 back/멀티렌즈/플랭크 포트/그릴/버튼/안테나),
  피처 수정, CAE 시편·메싱·라미네이트, 역설계(FeatureGraph)
- **MCP 서버**: 63개 LLM 도구를 Claude Desktop에 노출 (stdio 브리지; `Services/ReverseEngineer/LlmToolRegistry.cs`)
- **Mechanical ACT Extension**: 접촉면 검출, 모달 해석, 시나리오, 포스트프로세스, **파트별 진동에너지**, 물성 캘리브레이션
- **DPF 서버** (`tools/dpf_server`): 해석 끝난 `.rst` 를 서버에서 DPF 로 분석 (고유진동수·유효질량·MAC·변형에너지·응력 핫스팟) — REST + MCP

**대상 환경**: SpaceClaim / ANSYS **v252** (Student 시트 기준 `mesh_with_gmsh`만 STEP-export
라이선스 필요). 현재 버전 **1.6.0**.

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

- 접촉면 자동 검출·명명, 모달 해석, 시나리오 생성, 포스트프로세스 뷰어(MXPostViewer —
  Summary / Time History / FFT / FRF / Fatigue / Energy / Reactions / Sweep 탭), 물성 캘리브레이터(MaterialCalibrator)
- **Vibration Energy** (버튼 하나): Modal/Harmonic/Transient 결과에서 파트별 진동에너지 점유율을 뽑아
  의미 있는 파트만 남기고, `EnergyContribution` 모자이크와 최대 에너지 파트의 Total Deformation 뷰를
  자동 생성, `energy.json` 으로 뷰어에 연결

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

이 스크립트가 전용 venv(`.venv-build`)에서 PyInstaller로 MXPostViewer.exe / MaterialCalibrator.exe /
MCP 브리지 exe 2개를 **소스보다 오래됐거나 없으면** 빌드한 뒤 MSBuild로 DLL + ACT 배포 + WiX MSI 를
생성합니다 (`-Rebuild` = EXE 강제 재빌드, `-SkipMsi` = MSI 생략). 시스템 Python 으로 직접 PyInstaller 를
돌리지 마세요 — torch 같은 무거운 패키지가 딸려 들어와 EXE 가 수 GB 가 됩니다. **⚠️ 빌드 전 SpaceClaim을
닫으세요** (DLL 잠금). 상세: `lat.md/build-deploy.md`.

DLL만 빠르게:

```powershell
MSBuild MXDigitalTwinModeller.csproj /p:Configuration=Debug /p:Platform=AnyCPU
```

빌드 구성은 **Debug / Release** 두 가지이며 **v252 전용**입니다 (csproj의 `MXVersion` 한 값이
DLL + MSI ProductVersion 을 함께 구동 → 이 값만 올리면 in-place 업그레이드).

## 프로젝트 구조

```text
MXDigitalTwinModeller/
├── AddIn.cs                        # SpaceClaim Add-In 진입점 (MX Modeller 리본 + MCP 서버 기동)
├── Commands/                       # 리본 버튼 → 다이얼로그 (TensileTest, DMA, Laminate, Mesh,
│                                   #   GmshMesher, ReverseEngineer, Odb, Package, Pipeline …)
├── Services/                       # 비즈니스 로직
│   ├── ReverseEngineer/            # FeatureGraph 역설계 + CAD 수정 + LLM 도구
│   │   ├── Generation/             # 무에서 폰 생성 (SpecParser, GenerationService, FeaFreeze)
│   │   └── Mcp/                    # 인프로세스 MCP 서버 (127.0.0.1 HttpListener)
│   ├── GmshMesher/ ConformalMesh/ Mesh/ Contact/ Export/   # 메싱·접촉·K-File
│   ├── TensileTest/ DMA/ CAI/ Fatigue/ Joint/ Laminate/ … # CAE 시편
│   └── Odb/ Package/ Pcb/ Battery/ Fastener/ DropTest/ CadOps/
├── Models/                         # 데이터 모델
├── UI/Dialogs/                     # WinForms 대화창
├── Core/                           # SpaceClaim 측 공통 모듈 (Commands, Geometry, IO, UI)
├── Shared/MXDigitalTwinModeller.Core/  # SpaceClaim·Mechanical 공용 .NET 라이브러리
├── Scripts/                        # IronPython 스크립트 (01-16, pipeline.py)
├── Mechanical/                     # ANSYS Mechanical ACT Extension
│   ├── MXSimulator.xml             # ACT 확장 정의 (툴바/버튼 → main.py 콜백)
│   └── MXSimulator/
│       ├── main.py                 # IronPython 로직 (WPF 다이얼로그, Vibration Energy 포함)
│       ├── images/                 # 리본 아이콘 8개
│       ├── postprocess/            # 뷰어 (visualizer.py / analyzer.py / sweep_analyzer.py / MXPostViewer.exe)
│       ├── batch/                  # DPF 사이드카 (mx_batch.py, .rst → dpf_sidecar.json)
│       └── calibration/            # 물성 캘리브레이터 (runner.py / MaterialCalibrator.exe)
├── tools/mcp_bridge/               # Claude Desktop stdio ↔ HTTP 브리지 + 자동 등록기
├── tools/dpf_server/               # .rst DPF 후처리 서버 (FastAPI REST + MCP, mx_batch.py 를 잡으로 실행)
├── Test/gates/                     # GUI 에서 돌리는 런타임 GATE 스크립트 추적 사본
├── Installer/                      # WiX 인스톨러 (.wxs 추적, .msi 는 빌드 산출물)
├── Examples/                       # 인장 CSV 예제, presets/{iphone,galaxy}-like.json, packages/
├── Test/                           # RE_SelfTest 하네스
├── lat.md/                         # 설계·운영 문서 (status.md = 현황 스냅샷)
└── Docs/LSDyna/                    # LS-DYNA 키워드 참조
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
2. `MX Digital Twin Simulation` 툴바 (Face Pair NS · Named Selections · Modal Analysis · Add Scenario ·
   Post-Process · **Vibration Energy** · Export K-File · Tied Check), `MX Material Twin Simulation` 툴바 (Tensile Test)
3. 예: solve 된 모달/트랜지언트 해석에서 `Vibration Energy` → Analyze → `energy.json` 내보내기 → Post-Process 뷰어 Energy 탭

### DPF 서버 (라이선스 서버에서)

```bash
cd tools/dpf_server && python -m venv .venv && .venv/bin/pip install -r requirements.txt
./run_server.sh        # Windows: run_server.bat  →  http://<서버>:8770/docs , MCP: /mcp
```

설정·검증(`/health?deep=true`, `selftest_live.py`)·MCP 연결은 `tools/dpf_server/README.md`.

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

v1.6.0 (변경 이력은 `lat.md/status.md` 와 `git log` 참고)
