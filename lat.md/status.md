# 현황 (2026-09-02)

프로젝트 전체의 **현재 상태 스냅샷**. 무엇이 만들어져 있고, 무엇이 설치 파일에 실리고, 무엇이 실리지 않고 있었는지를
한 곳에 모은다. 갱신 규칙: 릴리스(MSI)를 만들 때마다 이 문서의 "릴리스" 절을 새로 쓴다.

관련: [[architecture]] · [[build-deploy]] · [[mechanical-act]] · [[spaceclaim-addin]] · [[postprocess]]

## 한눈에

| 축 | 상태 | 근거 |
|---|---|---|
| 저장소 | `main` @ `652ee41` (2026-07-12), 커밋 63개, 2026-02-07 시작, 추적 C# 218 파일 | `git log`, `git ls-files` |
| SpaceClaim Add-In | 리본 커맨드 22그룹, 무에서 폰 생성 13스테이지, CAD 수정 18 primitive (trustworthy **83.2%** — 5b97ba2 2026-07-04; EXPERIENCE_LOG.md §0 의 82.0% 는 그 이전 값), 역설계 FeatureGraph | `git log` 5b97ba2, Commands/ |
| MCP 서버 | LLM 도구 **63개** (README 의 "46개" 는 구버전 표기; 61→63 은 f5d0c0a ODB++ import 에서) | `Services/ReverseEngineer/LlmToolRegistry.cs` `new ToolDef(` 63개 |
| Mechanical ACT (MXSimulator) | 툴바 버튼 **9개** (이번에 `Vibration Energy` 추가), `main.py` 6,313 줄 (`EnergyDialog` 5644행~) | [[mechanical-act]] |
| 포스트프로세스 뷰어 | 탭 8개 (Summary/TimeHistory/FFT/FRF/Fatigue/**Energy**/Reactions/Sweep), metadata schema **2.1**, `energy.json` schema energy-1.0 | [[postprocess]] |
| 물성 캘리브레이터 | Elastic/Plastic, `MaterialCalibrator.exe` | [[material-calibrator]] |
| 설치 파일 | `Installer/MXDigitalTwinModeller.msi` **1.6.0** (이번 빌드) — 이전 1.5.0 (2026-07-12) | [[build-deploy#MSI 동기화 체크리스트]] |
| 환경 | ANSYS Student **v252** (v251 도 설치), WiX 6.0.2, MSBuild 16.11 (VS2019 BuildTools), Python 3.13.7 | — |

## 이번 사이클에서 한 것 (2026-09-02)

### 1. 파트별 진동에너지 — 버튼 하나 (`Vibration Energy`)

Modal / Harmonic / Transient 결과에서 body 별 에너지 점유율을 뽑아 **의미 있는 파트만 남기고** 트리를 정리하고,
`EnergyContribution` 모자이크와 최대 에너지 body 의 Total Deformation Figure 를 자동 생성한다. 상세는 [[postprocess#Vibration Energy]].

- ACT 발견: `EnergyContribution` 가 POSTPROCESS_IDEAS.md M10 (DPF/High 로 잡혀 있던 것) 을 ACT 한 줄로 해결. `Result.Total` / `AddFigure` 는 베이스 클래스에 있어 XML 파생 클래스 목록에 안 보였던 것.
- 고친 버그: `strain_energy` 가 총합이 아니라 **최대 요소 1개 값**이었고 뷰어가 그걸로 % 를 찍고 있었다 → `.Total` + basis 기록, schema 2.0→2.1, basis 가 Total 이 아니면 % 를 그리지 않음.
- 검증: `selftest_tabs.py` → `TABS_OK` + `ENERGY_OK`. 런타임 GATE 는 `verify_energy_api.py` (아직 안 돌림 — solve 된 모델에서 실행 필요).

### 2. 설치 파일 감사 — "추가한 기능이 전부 한 번의 설치로 업데이트되는가"

**아니었다.** 세 가지 방식으로 빠지고 있었고 전부 고쳤다. 상세는 [[build-deploy#MSI 동기화 체크리스트]].

| # | 구멍 | 영향 | 조치 |
|---|---|---|---|
| 1 | **stale EXE** — `build_release.ps1` 이 EXE 를 "없을 때만" 빌드 | `MXPostViewer.exe` 2월 18일 빌드가 7월 12일 MSI 에 실림 → 설치 사용자는 Sweep/Energy/Fatigue 탭을 본 적이 없음 (런처가 EXE 우선). `MaterialCalibrator.exe` 도 2월 22일 (소스는 6월 1일) | `build_release.ps1` 재작성: 소스가 EXE 보다 새로우면 재빌드, `-Rebuild`/`-SkipMsi` 스위치 |
| 2 | **PyInstaller 오염** — 시스템 Python 에 torch 2.12 → `hook-torch` 가 수 GB 를 끌어들임 | 재빌드 시 EXE 폭증 (첫 시도에서 실제 발생, 중단) | 전용 `.venv-build` 에서만 빌드, torch 부재 단언 |
| 3 | **wxs 누락** — `kfile_export.png`, `tied_check.png` 가 MSI 에 없음 (1.5.0 File 테이블 41개로 실증) | Export K-File / Tied Check 버튼이 아이콘 없이 설치됨 | wxs 에 추가 (8/8) |
| 4 | **버전** — 같은 1.5.0 으로 재빌드하면 `MajorUpgrade` 가 설치본을 교체하지 않음 | 업데이트 불가 | `MXVersion` 1.5.0 → **1.6.0** (README 동기) |
| 5 | `build_release.ps1` 뷰어 빌드에 `--hidden-import rainflow` 누락 (`build_viewer.bat` 에는 있음) | ps1 로 만든 EXE 는 Fatigue 탭 실패 | 추가 |

MCP 브리지 EXE 2종은 소스보다 최신이라 그대로. `csproj` 에 `SkipMsi` 조건 추가.

### 3. 릴리스 빌드 결과 (1.6.0)

| 항목 | 결과 |
|---|---|
| MSI | `Installer/MXDigitalTwinModeller.msi` — **1.6.0**, 237.9 MB, File 테이블 **43개** (1.5.0 은 41개; +`kfile_export.png` +`tied_check.png`). **2026-09-03 09:59 재생성** — 검증 결함 수정분(main.py·visualizer·wxs·csproj) 반영 |
| 검증 | WindowsInstaller COM 으로 ProductVersion=1.6.0 확인, 필수 16개 파일 전부 존재 → `MSI_VERIFY: OK` (두 번 모두) |
| EXE | `MXPostViewer.exe` 96.7 MB (**09-03 09:58, 수정된 뷰어로 재빌드**) / `MaterialCalibrator.exe` 96.7 MB (09-02) — 전용 `.venv-build` (PyInstaller 6.22.2, torch 없음). 첫 시도는 시스템 Python 의 torch 가 딸려 들어와 중단 |
| 빌드 스크립트 실증 | 재작성한 `build_release.ps1` 을 **끝까지 실제 실행**: venv 생성 → freshness 표(뷰어만 REBUILD, 나머지 fresh) → PyInstaller(`$PyArgs` 스플랫 정상) → MSBuild → MSI → 리본 캐시. PyInstaller 산출물은 `.venv-build\pyi\` 에만 생겨 **저장소가 더러워지지 않음** (`git status` 에 build/dist/spec 없음) |
| 스모크 | 뷰어: `QT_QPA_PLATFORM=offscreen` 으로 25초 생존(rc=124) → Qt + visualizer import 정상. 캘리브레이터: 인자 없이 실행 시 usage 출력(rc=1) 정상 |
| 개발 머신 배포 | MSBuild `DeployMechanicalExtension` 이 `%APPDATA%\ANSYS\v252\ACT\extensions\` 에 main.py(259KB, `EnergyDialog` 포함)·MXSimulator.xml(`VibrationEnergy`)·새 EXE 2종·아이콘 8개 복사, 리본 캐시 삭제 완료. **Workbench 재시작 필요** |
| 빌드 중 잡은 것 | wxs 아이콘 경로의 `\t` 가 파이썬 heredoc 을 거치며 TAB 이 되어 `WIX0103` — 스크립트 파일 방식으로 복구. 검증 스크립트의 PowerShell 배열 언롤링 버그(버전 "1" 출력)도 수정 |
| git | MSI 는 추적 대상 아님 (`Installer/` 에서는 `.wxs` 만 추적). 이전 1.5.0 MSI 와 2월 EXE 는 세션 스크래치에 백업 |
| **배포 (2026-09-03 10:25)** | (1) 개발 머신: MSBuild 배포본을 `verify_deployment.sh` 로 재검증 — 전 항목 ✓, SpaceClaim DLL 09:58 빌드본, 리본 캐시 비움. (2) **MSI 1.6.0 을 이 머신에 설치** (`msiexec /i /qn`, UAC 승인, exit 0, 레지스트리 `MX Digital Twin Modeller 1.6.0`). 이전 설치본이 없었으므로 **신규 설치 검증**이지 1.5.0→1.6.0 업그레이드 검증은 아니다 |
| 설치 결과 | ProgramData Add-In: DLL·Core.dll·Manifest·`Libs\gmsh`·`mcp_bridge`(exe 2·py 2·bat·README). ACT 확장: `MXSimulator.xml`, main.py(263,621 B = 저장소), 아이콘 8, postprocess 8(뷰어 EXE 96,731,671 B = 저장소), `batch\` 4, `bin\Core.dll`, calibration EXE, **`setup_venv.bat`/`requirements.txt` 가 루트에** (wxs 이동 반영, `calibration\` 에는 없음). 커스텀 액션 `RegisterClaudeDesktop` 실행 → Claude Desktop 설정에 `mxdtm-spaceclaim` → `...\V252\mcp_bridge\mxdtm_mcp_bridge.exe` 등록 (설치 전 0 → 후 1) |
| 재시작 필요 | Workbench/Mechanical (리본 캐시 비움 + 새 main.py/xml), SpaceClaim (새 DLL), **Claude Desktop** (설치 중 실행 중이었음 — MCP 등록은 재시작 후 인식) |

## 검증 (2026-09-03) — 독립 리뷰 + 적대적 검증

"다 만들었어?" 에 근거로 답하기 위해 세션 산출물 전체를 워크플로로 검증했다: 리뷰어 7명(에너지 다이얼로그 /
뷰어+버그수정 / 인스톨러 / 빌드 스크립트 / 문서 정확성 / GATE 스크립트 / 완전성 비평) → 발견 32건 → 상위 24건을
렌즈 3개(코드 사실 / 런타임 영향 / 수정 필요성)로 적대 검증 → **확정 16건, 기각 8건, 미검증 8건**(캡). 확정 16건은
전부 고쳤고, 미검증 8건과 검증 에이전트가 세션 한도로 죽은 4건은 직접 판단해 타당한 것을 고쳤다.

| # | 확정 결함 | 조치 |
|---|---|---|
| high | `build_release.ps1` `Invoke-PyInstaller` 의 파라미터 이름 `$Args` 가 자동변수 `$args` 에 가려져 `@Args` 스플랫이 **빈 값** → 재빌드 경로 전부 실패 (PS 5.1 에서 실증) | `$PyArgs` 로 개명 + 빈 인자 방어. 스플랫 5개 vs 구버전 0개로 재실증 |
| med | Material Twin: main.py 가 venv 게이트를 EXE 검사보다 먼저 통과시켜 **순수 MSI 설치에서 캘리브레이션 불가** (세션 이전부터; 1.6.0 에 실림). wxs 는 `setup_venv.bat`/`requirements.txt` 를 `calibration\` 에 넣어 csproj/sh 와 불일치 | main.py: `MaterialCalibrator.exe` 먼저 검사, venv 게이트는 폴백에만. wxs: 두 컴포넌트를 `MechanicalComponents`(루트) 로 이동 |
| med | 하모닉/트랜지언트는 세트 1개만 평가 (요청은 셋 다 세트별) | 셋 다 `SetNumber` 스캔, 세트 값(f/t) 기록, 모달은 `MaximumModesToFind` 클램프, 클램프된 세트 반복 감지로 종료, `Max sets` 도달 경고 |
| med | status.md 의 MCP 도구 수 61 → 실제 63 (`LlmToolRegistry.cs` `new ToolDef(` 63개) | 63 (README 도) |
| med | GATE: `CreateResultsAtAllSets` 생성분을 `id()` 로 diff → 래퍼가 바뀌면 **사용자 결과까지 삭제** 위험; 판정 `OK` 가 G2/G6 을 무시 | `ObjectId`/Name 안정 키로 diff, 불확실하면 자동 정리 생략; OK 조건에 프로퍼티 쓰기·SetNumber 실효성 포함, body scoping 실패 시 Total 을 증거로 안 씀 |
| low | 동명 body 가 이름 키로 합쳐짐 / worst 뷰가 엉뚱한 body | `이름#ObjectId` 키, worst 뷰는 probe 때 잡은 body 객체 사용 |
| low | 뷰어: basis 를 첫 body 에서만 읽음(혼합 시 % 오류); 손상된 energy.json 이 뷰어 전체를 죽임; Summary `StrainE` 열 의미 변화 무표시 | 전 body basis 집합(`mixed` 처리), `_normalize_ej` 검증 + try/except, 열 이름 `StrainE Total`, basis≠Total 은 `*`+툴팁 |
| low | csproj: `-SkipMsi` 인데 "EXE 없음" 메시지; `sweep_analyzer.py`·`batch/` 가 MSI 에는 있고 개발배포(csproj/sh)엔 없음 | 메시지 조건 분리, 두 목록 동기화 |
| low | 문서 줄번호 전부 stale (EnergyDialog 5621→5644 등), status.md §561→§595, 219 파일→218 | `regen_doc_lines.py` 로 main.py 정의 위치에서 재생성, 나머지 수정 |

미검증 중 조치한 것: ps1 이 PyInstaller 산출물(`build/`·`dist/`·`*.spec` — **git 추적 대상**임을 확인)을 저장소 안에 쓰던 것 →
`.venv-build\pyi\` 스크래치로; wxs 의 `Version` 폴백 1.4.0 → 1.6.0; README 46 → 63 + Energy 탭·빌드 절차 갱신;
"Sweep 탭 미착수" 표기는 낡음(이미 `SweepTab`). 그대로 둔 것: GATE 에 try/finally 없음(중단 시 `GATE_*` 수동 삭제 — 헤더에 명시),
GATE 가 `.claude/`(gitignore) 에만 있음(결정 항목).

## 커밋 (이번 사이클, 2026-09-05)

기반 HEAD `652ee41` 위에 논리 단위 3개로 커밋했다:

| 커밋 | 범위 | 파일 |
|---|---|---|
| `21fd848` | Vibration Energy 기능 + `strain_energy` Total 버그 수정 + Material Twin EXE-우선 | `main.py`, `MXSimulator.xml`, `visualizer.py`, `selftest_tabs.py` |
| `01ce3b3` | 릴리스 파이프라인: stale EXE 재빌드, MSI 동기화 구멍, 1.6.0 | `.gitignore`, `wxs`, `csproj`, `build_release.ps1/.bat`, `deploy_mxsimulator.sh` |
| (이 다음 커밋) | 문서: 이 스냅샷, 인스톨러 감사, Vibration Energy, 줄번호 재생성 | `README.md`, `POSTPROCESS_IDEAS.md`, `lat.md/*` |

의도적으로 미추적으로 둔 것: `.agents/` (`.claude/skills` 의 6.8 MB 미러 — 커밋 여부는 결정 항목), `Test/RE_SelfTest/*` 마커 61개
(이전 사이클 셀프테스트 산출물). `.venv-build/`, `Installer/*.msi`, `*.wixpdb` 는 ignore. **push 는 하지 않았다** (요청 범위 밖;
`origin` = `github.com/squall321/MXDigitalTwinModeller`). 1.5.0 MSI 와 2월 EXE 백업은 세션 스크래치에만 있다.

파일별 변경 내용:

| 파일 | 내용 |
|---|---|
| `Mechanical/MXSimulator/main.py` | `EnergyDialog` + `show_energy_dialog` (파일 끝 자기완결형; 모달/하모닉/트랜지언트 세트 스캔, 동명 body 키), `strain_energy` `.Total` 폴백, schema 2.1, Material Twin EXE-우선 |
| `Mechanical/MXSimulator.xml` | `Vibration Energy` 툴바 엔트리 |
| `Mechanical/MXSimulator/postprocess/visualizer.py` | `EnergyTab` 재작성 (energy.json 검증/정규화, basis 혼합 경고, 세트 종류별 라벨, ASCII 차트 텍스트), `SummaryTab` `StrainE Total` 열 |
| `Mechanical/deploy_mxsimulator.sh` | `sweep_analyzer.py`, `batch/` 복사 추가 (MSI 목록과 동기) |
| `Mechanical/MXSimulator/postprocess/selftest_tabs.py` | energy 케이스 4종 추가 (`ENERGY_OK`) |
| `Mechanical/MXSimulator/postprocess/MXPostViewer.exe` | 재빌드 (2026-02-18 → 2026-09-02, 96.7 MB) |
| `Mechanical/MXSimulator/calibration/MaterialCalibrator.exe` | 재빌드 (2026-02-22 → 2026-09-02, 96.7 MB) |
| `Installer/MXDigitalTwinModeller.wxs` | 아이콘 2개 추가, `setup_venv.bat`/`requirements.txt` 를 확장 루트로 이동, `Version` 폴백 1.6.0 |
| `MXDigitalTwinModeller.csproj` | `MXVersion` 1.6.0, `SkipMsi` 조건·메시지, 개발배포에 `sweep_analyzer.py`·`batch/` |
| `build_release.ps1` | 재작성 (freshness / `.venv-build` / rainflow / `-Rebuild` `-SkipMsi` / `$PyArgs` / 산출물 스크래치 경로) |
| `build_release.bat` | ps1 호출 래퍼로 축소 (이전엔 같은 결함을 가진 별도 구현) |
| `.gitignore` | `.venv-build/` 추가 |
| `README.md` | 버전 1.6.0 |
| `lat.md/lat.md` | 인덱스에 `[[status]]` 링크 |
| `POSTPROCESS_IDEAS.md` | 진행 기록 |
| `lat.md/{status,build-deploy,mechanical-act,mechanical/postprocess}.md` | 이 문서 + 갱신 |
| `.claude/skills/ansys-api-catalog/verification/verify_energy_api.py` | GATE (gitignore 대상 → **커밋되지 않음**, `.agents/` 미러도 untracked) |

`Test/RE_SelfTest/` 아래 untracked 산출물 61개는 이전 사이클의 셀프테스트 마커 — 이번 변경과 무관.

## DPF 사이드카 서버 (2026-09-16)

"서버로 띄워서 할 수 있는 기능" 조사 → DPF-over-.rst 가 1순위 (헤드리스 실증 완료, GUI·PyMechanical 불필요) → `tools/dpf_server/`
신설. 상세 설계·결정은 [[dpf-server]].

| 항목 | 내용 |
|---|---|
| 형태 | FastAPI `python -m mxdpf` — REST (업로드/공유경로 제출, 상태·결과·로그·취소·삭제, `/health?deep`) + MCP `/mcp` 도구 5개 |
| 실행 | 잡마다 `mx_batch.py` 서브프로세스 (데스크톱과 동일 CLI·스키마), 큐·동시성(기본 1)·타임아웃·트리 kill·TTL·재시작 시 `interrupted` |
| 보안 | Bearer 토큰, 경로 제출 루트 제한(realpath), Origin 차단, `.rst`·크기 제한, 기본 127.0.0.1 |
| 검증 | pytest 30개 통과 (가짜 백엔드), 실 uvicorn + 공식 MCP SDK 클라이언트 왕복. **실제 DPF·라이선스 미실행** — 라이선스는 운영 서버에서만 |
| 배포물 | `requirements.txt`(mx_batch 요구사항 포함), `run_server.bat/.sh`, `deploy/mxdpf.service` + env 예시, `selftest_live.py` (실서버 GATE) |
| 기타 | `README.md` 구조도·하단 버전 정정, `.gitignore` 에 `tools/dpf_server/.venv/`·`.pytest_cache/` |

## 다음

1. **GATE 실행** — solve 된 모델을 열고 `verify_energy_api.py` 를 돌려 `ENERGY_GATE` 판정 확인. G4(EnergyContribution 숫자 회수) 결과에 따라 KE 가 숫자인지 차트 전용인지 확정. G6 이 하모닉/트랜지언트 `SetNumber` 의미까지는 검증하지 않으므로, 하모닉·트랜지언트 모델에서 `Vibration Energy` 로그의 세트 값(f/t) 이 실제 주파수/시간과 맞는지 확인.
2. **1.6.0 MSI 설치 테스트** — 신규 설치는 이 머신에서 완료·검증(위 "배포"). 남은 것: (a) 1.5.0 이 깔린 머신에서 in-place 업그레이드, (b) Workbench 재시작 후 `Vibration Energy` 버튼과 아이콘 8개 확인, Post-Process 뷰어에서 Energy 탭이 보이는지(= 새 EXE 가 실렸는지), Material Twin 캘리브레이션이 venv 없이 EXE 로 도는지, (c) Claude Desktop 재시작 후 `mxdtm-spaceclaim` 도구가 보이는지 — 셋 다 GUI 상호작용이라 사람이 해야 한다.
3. ~~`verify_energy_api.py` 를 추적 대상 트리로 옮길지 결정~~ → **`Test/gates/` 에 추적 사본** (2026-09-16, `.claude/` 원본과 함께 유지).
4. `lat.md/mechanical/postprocess.md` 의 K-File 절 메서드 줄번호는 재생성했지만, 그 절의 설명 자체는 이번 세션 이전 것 — 내용 재검토는 별도.
5. POSTPROCESS_IDEAS.md "### 다음 (미착수)" (§595) 항목 중 LLM 리포터(API 키 제외 대상)와 DOE driver 는 그대로. "sweep_analyzer 를 뷰어 Sweep 탭으로 노출"은 이미 되어 있다 (`visualizer.py` `SweepTab`, 폴더 열기 포함) — 그 문서의 미착수 표기가 낡았다.
6. **DPF 서버 운영 서버 배포** — venv 설치 → `GET /health?deep=true` 의 `deep.ok` → `selftest_live.py` `LIVE_GATE_OK` (가능하면 `--modal-example` 또는 실제 모달 `.rst`). 이후 포털 MCP 게이트웨이에 네임스페이스 등록.
7. **mx_batch `hotspots` 좌표 단위** — `eps_mm=2.0` 이 단위 변환 없이 쓰인다. 실결과 GATE 에서 `n_clusters` 가 1 로 뭉치면 `coordinates_field.unit` 기반 변환 추가 (데스크톱 공유 스크립트).
8. `MXDPF_MAX_CONCURRENCY` > 1 의 라이선스 동시성 확인.
