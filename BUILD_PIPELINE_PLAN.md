# Build & Deploy Pipeline 정리 계획

> **목적**: 현재 4개 매커니즘에 분산된 빌드/배포 흐름을 단일 source-of-truth + 단일 entry point 로 통합.
> **범위**: 이 메인 repo 의 SpaceClaim Add-In + Mechanical ACT Extension. Phone Designer ([[PHONE_DESIGNER_PLAN.md]]) 는 별도.

---

## 1. 배경 (현재 상태 진단)

### 1.1 현재 빌드/배포 매커니즘 4개

| 매커니즘 | 위치 | 실행 방식 | 책임 범위 |
|---|---|---|---|
| **MSBuild PostBuild Targets** | [MXDigitalTwinModeller.csproj:310-379](MXDigitalTwinModeller.csproj#L310-L379) | 매 빌드 자동 | Shared DLL 빌드, ACT 부분 deploy, MSI 빌드 |
| **build_release.bat / .ps1** | [build_release.ps1](build_release.ps1), [build_release.bat](build_release.bat) | 수동 | PyInstaller EXE 빌드 → MSBuild 호출 |
| **deploy_mxsimulator.sh** | [Mechanical/deploy_mxsimulator.sh](Mechanical/deploy_mxsimulator.sh) | 수동 | 직접 파일 복사 (재빌드 없이) |
| **WiX MSI Installer** | [Installer/MXDigitalTwinModeller.wxs](Installer/MXDigitalTwinModeller.wxs) | MSBuild 가 호출 | 최종 사용자 배포 MSI 생성 |

### 1.2 확인된 결함

1. **🔴 MSBuild Target 의 calibration/ 폴더 누락**
   - MSBuild 만 돌리면 `Mechanical/MXSimulator/calibration/` 가 ACT extension 으로 deploy 안 됨
   - Material Twin 기능 silent fail
   - `deploy_mxsimulator.sh` 를 추가로 돌려야 작동 — 문서화 안 됨

2. **🔴 ribbon 캐시 클리어 경로 불일치**
   - MSBuild 단독: ❌
   - `build_release.bat`: ❌
   - `build_release.ps1`: ✅
   - `deploy_mxsimulator.sh`: ✅
   - → `.bat` 사용자는 새 버튼/아이콘 인식 안 될 수 있음

3. **🔴 V251 멀티버전 인프라가 broken**
   - [Directory.Build.props](Directory.Build.props) 에 V251/V252 분기 코드 있음
   - 하지만 [csproj:19](MXDigitalTwinModeller.csproj#L19) 의 `OutputPath` 가 V252 하드코딩
   - [csproj:312](MXDigitalTwinModeller.csproj#L312) 의 `MechanicalExtDir` 도 V252 하드코딩
   - 실제 csproj 에는 `Debug|AnyCPU`, `Release|AnyCPU` 두 configuration 만 정의
   - README 의 "4 configurations" (Debug-V251, Release-V251, Debug-V252, Release-V252) 주장과 불일치
   - V251 빌드 시도 시 `OutputPath 미설정` 에러

4. **🟡 SpaceClaim DLL lock 자동 처리 없음**
   - 빌드 중 SpaceClaim 떠있으면 dll lock 으로 실패
   - [kill_ansys.bat](Mechanical/kill_ansys.bat) 가 있지만 어느 빌드 스크립트도 자동 호출 안 함
   - 매번 사용자가 수동 종료

5. **🟡 PyInstaller EXE 책임 모호**
   - `MaterialCalibrator.exe`, `MXPostViewer.exe` 빌드 책임:
     - `build_release.bat/ps1`: 빌드함 (없으면)
     - MSBuild Target: 없으면 copy 만 skip (조용히)
     - WiX MSI: 없으면 build 실패
   - → MSBuild 만 돌리고 MSI 만들려고 하면 silent fail (EXE 없는 채로 WiX 가 실패)

6. **🟡 Shared DLL 빌드 산출물 위치 명시 안 됨**
   - SpaceClaim Add-In 의 `MXDigitalTwinModeller.dll` 은 V252\ 폴더로 배포되지만
   - `MXDigitalTwinModeller.Core.dll` 은 같이 안 가서 SpaceClaim 측이 어떻게 참조하는지 불명
   - 현재는 동작하는 것 같지만 명시적이지 않음

7. **🟢 빌드 entry point 3개 (bat/ps1/sh)**
   - 사용자가 어느 걸 써야 할지 모호
   - 셋 다 다른 책임 범위

### 1.3 영향받는 사용자 시나리오

- **시나리오 1**: 개발자가 IDE 에서 빌드 (MSBuild 만) → Material Twin 작동 안 함 (silent)
- **시나리오 2**: `.bat` 으로 빌드 → 새 아이콘이 Workbench 에서 안 보임 (캐시 stale)
- **시나리오 3**: V251 시도 → 빌드 자체 실패
- **시나리오 4**: SpaceClaim 열어둔 채 빌드 → dll lock 에러 (왜 실패하는지 명확한 에러 메시지 없음)
- **시나리오 5**: 최종 사용자에게 MSI 배포 → MaterialCalibrator.exe 미빌드 상태면 wix 실패

---

## 2. 목표 / Non-goals

### 2.1 목표

1. **Single Source of Truth**: MSBuild Target 이 모든 deploy logic 의 canonical 정의. 다른 스크립트는 부분집합만 수행, 절대 logic 중복 X.
2. **Canonical Build Entry Point**: 전체 빌드는 오직 `build.ps1`. `.sh` 는 보조 유틸리티 (Python-only 빠른 동기, 검증, 프로세스 종료).
3. **완전한 deploy**: MSBuild 한 번으로 ACT extension 전체 + Shared DLL + PyInstaller EXE + MSI 까지 일관 산출.
4. **명시적 실패**: silent fail 제거. EXE 없으면 명확 에러. ANSYS 떠있으면 친절한 메시지 + 자동 종료 옵션.
5. **검증 가능한 deploy**: 빌드 후 자동 검증 스크립트로 모든 산출물 위치/존재 확인.
6. **V252 단일화**: V251 멀티버전 인프라 폐기 (결정 5.1.E1).

### 2.2 Non-goals

- GitHub Actions / Cloud CI 구축 — 로컬 개발 환경만
- macOS / Linux 빌드 지원 — Windows 전용
- Docker / containerization — out of scope
- 배포 자동 업로드 (S3, GitHub Releases) — MSI 생성까지만
- 새 기능 추가 — 빌드 파이프라인 정리만, 기능 변경 없음

---

## 3. Target Architecture

### 3.1 정리 후 흐름 (단일 entry point)

```
PS> .\build.ps1
  │
  ▼
┌─────────────────────────────────────────────────┐
│ Step 0: Pre-flight checks                       │
│  - Python 존재 확인                              │
│  - MSBuild 존재 확인                             │
│  - 환경 변수 (.env) 로드                         │
│  - 기존 ANSYS 프로세스 검출                       │
│    └─ 있으면 사용자 확인 후 kill (또는 -Force)    │
└────────────────┬────────────────────────────────┘
                 ▼
┌─────────────────────────────────────────────────┐
│ Step 1: PyInstaller EXEs (skip if exists)       │
│  - MaterialCalibrator.exe                       │
│  - MXPostViewer.exe                             │
│  (옵션: -Rebuild 플래그로 강제 재빌드)            │
└────────────────┬────────────────────────────────┘
                 ▼
┌─────────────────────────────────────────────────┐
│ Step 2: MSBuild                                 │
│  C:\Program Files (x86)\...\MSBuild.exe         │
│  /p:Configuration=Debug                          │
│  /p:Platform=AnyCPU                              │
│                                                 │
│  내부적으로 다음 Target 자동 실행:                │
│   - Build (메인 Add-In dll)                      │
│   - DeployMechanicalExtension                   │
│       * Shared Core DLL 빌드                     │
│       * MXSimulator.xml → extensions/           │
│       * main.py, icons, postprocess, calibration│
│         → extensions/MXSimulator/                │
│       * Shared Core DLL → extensions/.../bin/   │
│       * 🆕 ribbon 캐시 클리어                    │
│   - BuildInstaller                              │
│       * wix build → Installer/*.msi             │
└────────────────┬────────────────────────────────┘
                 ▼
┌─────────────────────────────────────────────────┐
│ Step 3: Post-deploy verification                │
│  - 모든 산출물 파일 존재 확인                     │
│  - Python 문법 검증 (main.py 등)                 │
│  - 결과 표 (체크리스트) 출력                     │
└─────────────────────────────────────────────────┘
```

### 3.2 산출물 위치 (최종)

| 산출물 | 위치 | 책임 |
|---|---|---|
| `MXDigitalTwinModeller.dll` | `C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\` | MSBuild OutputPath |
| `MXDigitalTwinModeller.Manifest.xml` | 같은 곳 | csproj `<None Include>` |
| `MXDigitalTwinModeller.Core.dll` (V252\) | 같은 곳 | 🆕 MSBuild Target 추가 |
| `MXSimulator.xml` | `%APPDATA%\Ansys\v252\ACT\extensions\` | MSBuild Target |
| ACT extension 전체 (main.py + icons + calibration/ + postprocess/) | `%APPDATA%\Ansys\v252\ACT\extensions\MXSimulator\` | MSBuild Target (확장) |
| `MaterialCalibrator.exe` | `Mechanical/MXSimulator/calibration/` + ACT 폴더 | build.ps1 + MSBuild Copy |
| `MXPostViewer.exe` | `Mechanical/MXSimulator/postprocess/` + ACT 폴더 | build.ps1 + MSBuild Copy |
| `MXDigitalTwinModeller.msi` | `Installer/` | MSBuild Target (WiX) |

### 3.3 폐기/대체 + 유지

| 파일 | 결정 | 이유 |
|---|---|---|
| `build_release.bat` | ❌ 폐기 | `build.ps1` 와 중복, 캐시 클리어 누락 |
| `Mechanical/deploy_mxsimulator.sh` | ✅ **유지** (rename: `sync_python.sh`) | Python-only 빠른 동기 (MSBuild 없이 1초). iteration 가치 |
| `Mechanical/verify_deployment.sh` | ✅ **유지** | standalone bash 검증. `build.ps1` Step 3 와 별도로 디버깅용 |
| `Mechanical/kill_ansys.bat` | ✅ 유지 | 독립 유틸 |
| Directory.Build.props 의 V251 분기 | ❌ 폐기 | V252 단일화 결정 (5.1.E1) |
| README 의 "4 configurations" | 🔧 수정 | "Debug / Release" 2개로 정정 |

### 3.4 `.sh` 역할 분리 원칙

`.sh` 들은 MSBuild Target 의 **부분집합** 만 수행. 절대 logic 중복 안 함:

| 도구 | 책임 범위 | 무거운 작업 (DLL/MSI/Shared DLL) |
|---|---|---|
| `build.ps1` / MSBuild | 모든 것 | ✅ |
| `sync_python.sh` | Python 파일 + 캐시 클리어만 | ❌ |
| `verify_deployment.sh` | 검증만 (read-only) | ❌ |
| `kill_ansys.bat` | 프로세스 종료만 | ❌ |

---

## 4. Phase 별 작업 계획

### Phase A — 진단 + 결정 사항 확정 (반나절)

**작업**:
- [ ] 본 계획서 검토 + 사용자 결정사항 답변
- [ ] 결정 1: V251 지원 — **폐기** vs **진짜 구현** (5.1 참조)
- [ ] 결정 2: ribbon 캐시 클리어 — 매 빌드 vs `-ClearCache` 플래그 시에만
- [ ] 결정 3: ANSYS 프로세스 자동 종료 — 기본 동작 vs `-Force` 시에만
- [ ] 결정 4: PyInstaller EXE 미존재 시 — 자동 빌드 vs 명시적 에러

**산출물**: 결정 사항 본 계획서에 기록

**검증**: 모든 결정 사항이 명확하고, 다음 Phase 에서 referenced

---

### Phase B — MSBuild Target 완성 (1일)

**목표**: MSBuild 한 번이 모든 deploy 책임. 다른 스크립트 없어도 완전 작동.

**작업**:
- [ ] `DeployMechanicalExtension` Target 에 calibration/ 복사 추가:
  - [ ] `Mechanical/MXSimulator/calibration/__init__.py`
  - [ ] `Mechanical/MXSimulator/calibration/runner.py`
  - [ ] `Mechanical/MXSimulator/calibration/elastic_calibrator.py`
  - [ ] `Mechanical/MXSimulator/calibration/run_elastic_calibration.py`
  - [ ] `Mechanical/MXSimulator/calibration/utils/yaml_parser.py`
  - [ ] `Mechanical/MXSimulator/calibration/utils/csv_parser.py`
  - [ ] `Mechanical/MXSimulator/calibration/utils/__init__.py`
  - [ ] `Mechanical/MXSimulator/calibration/MaterialCalibrator.exe` (있을 때만)
  - [ ] `Mechanical/MXSimulator/setup_venv.bat`
  - [ ] `Mechanical/MXSimulator/requirements.txt`
  - [ ] `Mechanical/MXSimulator/run_cap_vibration.py`
- [ ] images 폴더 추가 아이콘 누락 확인 + 복사 추가:
  - [ ] `tied_check.png`, `material_twin.png`, `kfile_export.png` (현재 5개만 명시, 나머지 누락)
- [ ] Shared Core DLL 을 V252\ 폴더에도 복사 (현재는 bin\Debug\ 만)
- [ ] 🆕 Ribbon 캐시 클리어 Target 추가:
  ```xml
  <Target Name="ClearWorkbenchRibbonCache" AfterTargets="DeployMechanicalExtension">
    <Delete Files="$(APPDATA)\Ansys\v252\Applets\DSApplet\en-us\ExternalActions.xml;
                   $(APPDATA)\Ansys\v252\Applets\DSApplet\en-us\ribbonLayout.xml;
                   $(APPDATA)\Ansys\v252\Applets\DSApplet\en-us\RibbonState.xml" 
            ContinueOnError="true" />
  </Target>
  ```
- [ ] PyInstaller EXE 미존재 시 MSBuild 가 친절한 경고 출력 (실패 X, MSI 단계에서만 실패)
- [ ] WiX BuildInstaller Target 의 EXE 의존성 명시:
  - [ ] EXE 없으면 wix build 스킵 + 경고 (현재는 실패)
  - [ ] 또는 BeforeTargets 에 EXE 존재 확인 Target 추가

**산출물**: 확장된 [MXDigitalTwinModeller.csproj](MXDigitalTwinModeller.csproj)

**검증**:
- [ ] `MSBuild ... /p:Configuration=Debug` 단독 실행 → ACT extension 폴더에 calibration/ 까지 모두 deploy 확인
- [ ] `tree %APPDATA%\Ansys\v252\ACT\extensions\MXSimulator` 결과가 WiX `<ComponentGroup>` 들과 1:1 매칭
- [ ] Workbench 재시작 후 MX Material Twin 버튼 클릭 → 정상 동작
- [ ] EXE 없는 상태에서 빌드 시도 → MSBuild 성공, wix build 만 경고 (silent fail 아닌 명확 메시지)

---

### Phase C — `build.ps1` 단일 entry point (반나절)

**목표**: 사용자가 외울 명령 1개: `.\build.ps1`. 모든 옵션은 플래그로.

**작업**:
- [ ] `build.ps1` 재작성:
  - [ ] Step 0: Pre-flight checks
    - [ ] Python, MSBuild 존재 확인 (각각 명확한 에러 메시지)
    - [ ] `.env` 로드 (있으면)
    - [ ] ANSYS 프로세스 검출 (`Get-Process` 로 ansys*, spaceclaim*, AnsysWBU 등)
    - [ ] 검출 시 사용자에게 확인 + `Stop-Process` (또는 `-Force` 자동)
  - [ ] Step 1: PyInstaller EXEs
    - [ ] `-Rebuild` 또는 EXE 미존재 시 빌드
    - [ ] venv 자동 생성 또는 시스템 Python 사용 결정
    - [ ] 빌드 실패 시 명확 에러
  - [ ] Step 2: MSBuild
    - [ ] `/p:Configuration=Debug /p:Platform=AnyCPU` 호출
    - [ ] 빌드 실패 시 exit
  - [ ] Step 3: Verify
    - [ ] 검증 함수 (Phase B 의 verify_deployment.sh 를 PowerShell 로 포팅)
    - [ ] 모든 산출물 존재 확인 + 표로 출력
    - [ ] 실패 항목 빨간색 표시
- [ ] 옵션 플래그:
  - [ ] `-Rebuild` — PyInstaller EXE 강제 재빌드
  - [ ] `-Force` — ANSYS 프로세스 자동 종료 (확인 없이)
  - [ ] `-SkipMsi` — MSI 빌드 건너뛰기 (개발 iteration 시)
  - [ ] `-Configuration Release` — Release 빌드
  - [ ] `-Verbose` — 상세 로그
- [ ] `build_release.bat` 삭제 (중복)
- [ ] `Mechanical/deploy_mxsimulator.sh` → `Mechanical/sync_python.sh` 로 rename + 역할 명확화:
  - [ ] Python 파일 (main.py, calibration/*.py, postprocess/*.py) + 캐시 클리어만 수행
  - [ ] DLL / Shared DLL / EXE / MSI 관련 logic 제거 (MSBuild 의 책임)
  - [ ] 헤더 주석에 "Python iteration only, 전체 빌드는 build.ps1" 명시
- [ ] `Mechanical/verify_deployment.sh` 유지 (bash 표준 검증 도구로 standalone)
- [ ] build.ps1 Step 3 의 PowerShell 검증은 verify_deployment.sh 와 **동일 항목** 체크 (logic 중복 OK — 한쪽은 PS, 한쪽은 bash 로 자유롭게 호출 가능하도록)

**산출물**:
- `build.ps1` (확장)
- 폐기 파일 3개 삭제

**검증**:
- [ ] 깨끗한 상태에서 `.\build.ps1` 한 번에 모든 산출물 생성
- [ ] `.\build.ps1 -Force` 로 SpaceClaim 떠있어도 자동 처리
- [ ] `.\build.ps1 -SkipMsi` 로 iteration 빌드 빠르게 동작
- [ ] 산출물 누락 시 Step 3 가 명확 보고
- [ ] PowerShell ExecutionPolicy 가 Restricted 환경에서도 동작 (또는 친절한 가이드)

---

### Phase D — WiX MSI 동기 검증 (반나절)

**목표**: MSI 가 MSBuild Target 의 deploy 와 1:1 동일 산출물 보장.

**작업**:
- [ ] WiX `.wxs` 의 모든 `<File>` element 와 MSBuild Target 의 `<Copy>` SourceFiles 비교
- [ ] 불일치 항목 정리:
  - [ ] WiX 에 있고 MSBuild 없는 것 → MSBuild Target 에 추가
  - [ ] MSBuild 에 있고 WiX 없는 것 → WiX `<File>` 추가
- [ ] 검증 스크립트 작성 (`tools/verify_wix_msbuild_sync.ps1`):
  - [ ] WiX 의 모든 Source= 경로 추출
  - [ ] MSBuild Target 의 모든 SourceFiles 추출
  - [ ] Diff 출력
- [ ] MSI 설치 테스트:
  - [ ] 가상 머신 또는 깨끗한 사용자 계정으로 MSI 설치
  - [ ] 설치된 파일 트리 = MSBuild deploy 결과 일치 확인
  - [ ] 설치 후 SpaceClaim + Mechanical 양쪽 정상 동작 확인
- [ ] (옵션) WiX Heat tool 로 ComponentGroup 자동 생성 검토 — 수동 동기 부담 줄이기

**산출물**:
- 동기화된 `.wxs`
- `tools/verify_wix_msbuild_sync.ps1`

**검증**:
- [ ] `verify_wix_msbuild_sync.ps1` 통과 (diff 없음)
- [ ] 깨끗한 환경에서 MSI 설치 → 모든 기능 정상

---

### Phase E — 멀티버전 정리 (V251 폐기 또는 구현) (1일)

**목표**: README/Directory.Build.props/csproj 가 한 방향으로 일치. 사용자 혼란 제거.

#### 옵션 E1: V251 폐기 (추천, 작업량 작음)

**작업**:
- [ ] [Directory.Build.props](Directory.Build.props) 의 V251 관련 라인 모두 삭제
- [ ] V252 만 단일화 (조건문 제거)
- [ ] [README.md](README.md) 의 4 configurations 설명 → 2 (Debug, Release) 로 수정
- [ ] [.env.example](.env.example) 의 V251_* 변수 삭제
- [ ] `.vscode/launch.json` 의 V251 Debug 프로파일 제거
- [ ] lat.md `[[architecture#멀티버전 지원]]` 섹션 폐기 또는 "V252 only" 로 단순화
- [ ] lat.md `[[build-deploy#빌드 구성]]` 섹션 V251 부분 삭제

**검증**:
- [ ] Grep 으로 `V251` 잔존 0개
- [ ] `.\build.ps1` 동작 변화 없음

#### 옵션 E2: V251 진짜 구현 (작업량 큼)

**작업**:
- [ ] [csproj](MXDigitalTwinModeller.csproj) 에 4 configuration `<PropertyGroup Condition>` 추가:
  - [ ] `'$(Configuration)|$(Platform)' == 'Debug-V251|AnyCPU'`
  - [ ] `'$(Configuration)|$(Platform)' == 'Release-V251|AnyCPU'`
  - [ ] `'$(Configuration)|$(Platform)' == 'Debug-V252|AnyCPU'`
  - [ ] `'$(Configuration)|$(Platform)' == 'Release-V252|AnyCPU'`
- [ ] OutputPath 를 Configuration 별로 분기
- [ ] DefineConstants 에 V251 또는 V252 분기
- [ ] HintPath 의 ANSYS SpaceClaim.Api.V251.dll vs V252.dll 분기 reference
- [ ] MSBuild Target 의 `$(APPDATA)\Ansys\v252\` 도 분기
- [ ] WiX `.wxs` 의 V252 경로도 분기 (또는 두 MSI 생성)
- [ ] V251 환경에서 실제 빌드 + Workbench 테스트 (V251 SpaceClaim 필요)

**검증**:
- [ ] `.\build.ps1 -Configuration Debug-V251` 성공
- [ ] V251 SpaceClaim 에서 AddIn 로드 확인

→ **추천: E1 (폐기)**. ANSYS Student v252 + 단일 사용자 환경에서 멀티버전 인프라 유지 비용 큼.

**산출물**: 정리된 csproj/Directory.Build.props/README/lat.md

---

### Phase F — 문서화 + lat.md 업데이트 (반나절)

**작업**:
- [ ] [README.md](README.md) 업데이트:
  - [ ] 빌드 명령: `.\build.ps1` 한 줄
  - [ ] 옵션 플래그 설명
  - [ ] 산출물 위치 표
  - [ ] V252 단일 (또는 4 config) 명확화
- [ ] lat.md 업데이트:
  - [ ] [lat.md/build-deploy.md](lat.md/build-deploy.md) 갱신
    - [ ] 새 entry point (`build.ps1`)
    - [ ] 폐기된 파일 (bat, sh) 표기
    - [ ] Ribbon 캐시 클리어 자동화
  - [ ] [lat.md/architecture.md](lat.md/architecture.md) 의 빌드 구성 부분 갱신
  - [ ] [lat.md/api-learnings.md](lat.md/api-learnings.md) 의 "SpaceClaim DLL 잠금" 섹션에 자동 처리 언급
- [ ] `.env.example` 업데이트 (V251 제거 또는 V251 포함 명확화)
- [ ] **새 사용자 가이드** (`docs/BUILD.md` 또는 README 의 한 섹션):
  - [ ] "처음 빌드하려면": prerequisite → clone → build.ps1
  - [ ] "iteration 빠르게": -SkipMsi
  - [ ] "MSI 배포": -Configuration Release
  - [ ] Troubleshooting (DLL lock, EXE 미빌드, etc.)

**산출물**: 갱신된 README + lat.md + (선택) docs/BUILD.md

**검증**:
- [ ] 새 개발자가 README 만 보고 `.\build.ps1` 로 빌드 성공
- [ ] lat.md 의 build-deploy 관련 wiki 링크 모두 valid
- [ ] V251 잔존 reference 0개 (옵션 E1 선택 시)

---

## 5. 결정 사항 (Phase A 확정 — 2026-05-26)

### 5.1 V251 지원 → **E1 (폐기) ✅**

V252 단일화. 단일 사용자 + ANSYS Student v252 환경. V251 멀티버전 인프라가 broken 인 채로 코드만 남아있어 혼란만 야기.

### 5.2 Ribbon 캐시 클리어 → **매 빌드 ✅**

`DeployMechanicalExtension` Target 의 AfterTargets 로 `ClearWorkbenchRibbonCache` Target 자동 실행. 빌드마다 캐시 삭제 → 새 버튼/아이콘 누락 위험 0.

### 5.3 ANSYS 프로세스 자동 종료 → **확인 prompt ✅**

ANSYS / SpaceClaim 프로세스 검출 시 사용자에게 `[Y/N]` prompt 표시. Y → `Stop-Process`. N → 빌드 중단.
플래그 `-Force` 사용 시 prompt 생략하고 자동 종료.

### 5.4 PyInstaller EXE → **자동 빌드 ✅**

EXE 미존재 시 build.ps1 가 자동으로 PyInstaller 호출. 명시적 `-Rebuild` 플래그 시 강제 재빌드. 현재 [build_release.ps1](build_release.ps1) 의 패턴 그대로 유지.

### 5.5 `.bat` / `.sh` 정책 → **부분 유지 ✅**

| 파일 | 결정 |
|---|---|
| `build_release.bat` | ❌ 폐기 (build.ps1 과 중복) |
| `Mechanical/deploy_mxsimulator.sh` | ✅ 유지 (rename: `sync_python.sh`, Python iteration 전용) |
| `Mechanical/verify_deployment.sh` | ✅ 유지 (standalone bash 검증) |
| `Mechanical/kill_ansys.bat` | ✅ 유지 (독립 유틸) |

**원칙**: `.sh` 는 MSBuild Target 의 부분집합만 수행. DLL/MSI/Shared DLL 같은 무거운 작업 X.

---

## 6. 영향받는 파일 목록

### 6.1 수정 (modify)

| 파일 | Phase | 변경 내용 |
|---|---|---|
| `MXDigitalTwinModeller.csproj` | B, E | calibration/ 복사 추가, 캐시 클리어 Target, V251 정리 |
| `build_release.ps1` | C | `build.ps1` 로 재작성 + 확장 |
| `Installer/MXDigitalTwinModeller.wxs` | D | MSBuild Target 과 동기 |
| `Directory.Build.props` | E | V251 분기 제거 (E1) 또는 정비 (E2) |
| `README.md` | F | 빌드 명령 단일화, 산출물 표 |
| `.env.example` | E, F | V251 변수 제거 |
| `lat.md/build-deploy.md` | F | 새 흐름 반영 |
| `lat.md/architecture.md` | F | 멀티버전 섹션 정리 |
| `lat.md/api-learnings.md` | F | DLL lock 자동 처리 언급 |

### 6.2 삭제 (delete)

| 파일 | Phase | 이유 |
|---|---|---|
| `build_release.bat` | C | `build.ps1` 와 중복, 캐시 클리어 누락 |

### 6.3 Rename + 역할 변경

| 파일 | Phase | 새 이름 / 역할 |
|---|---|---|
| `Mechanical/deploy_mxsimulator.sh` | C | → `Mechanical/sync_python.sh` (Python iteration 전용) |

### 6.4 유지

| 파일 | 역할 (변경 없음) |
|---|---|
| `Mechanical/verify_deployment.sh` | standalone bash 검증 |
| `Mechanical/kill_ansys.bat` | ANSYS 프로세스 종료 유틸 |

### 6.5 신규 (create)

| 파일 | Phase | 용도 |
|---|---|---|
| `build.ps1` | C | Canonical 전체 빌드 entry point (build_release.ps1 → rename + 확장) |
| `tools/verify_wix_msbuild_sync.ps1` | D | WiX/MSBuild 동기 검증 |
| `docs/BUILD.md` (선택) | F | 빌드 가이드 별도 페이지 |

---

## 7. 검증 / 회귀 테스트

### 7.1 각 Phase 끝 회귀 테스트 (필수)

매 Phase 완료 시:
1. **Clean build**: `Remove-Item -Recurse bin, obj, %APPDATA%\Ansys\v252\ACT\extensions\MXSimulator` 후 `.\build.ps1` 1회
2. **산출물 체크**: build.ps1 Step 3 의 검증 결과 모든 항목 ✓
3. **SpaceClaim 측 동작 확인**:
   - SpaceClaim 실행 → MX Modeller 탭 → ApplyMeshSettings 클릭 → 다이얼로그 정상 표시
4. **Mechanical 측 동작 확인**:
   - Workbench → Mechanical 실행 → MX Digital Twin Simulation 툴바 확인
   - 각 버튼 1회 클릭 (에러 없이 다이얼로그 뜨는지만)
5. **MSI 설치 테스트** (Phase D 이후 필수):
   - 깨끗한 사용자 계정 또는 VM 에서 MSI 더블클릭
   - 설치 완료 → ANSYS 양쪽 정상 동작

### 7.2 Critical 시나리오 회귀 (Phase 완료 후 1회)

[1.3] 의 5개 시나리오 모두 정상화 확인:
- [ ] IDE 빌드만 → Material Twin 정상
- [ ] 새 아이콘 추가 후 빌드 → Workbench 에서 즉시 인식
- [ ] V251 명령 시 — 명확한 에러 또는 정상 (E1/E2 따라)
- [ ] SpaceClaim 열어둔 채 빌드 → 자동 처리 (정책 따라)
- [ ] EXE 미빌드 상태 + MSI 시도 → 명확 에러 메시지

---

## 8. 예상 일정

| Phase | 기간 |
|---|---|
| A. 진단 + 결정 | 반나절 |
| B. MSBuild Target 완성 | 1일 |
| C. build.ps1 단일화 | 반나절 |
| D. WiX 동기 검증 | 반나절 |
| E. V251 정리 (E1 선택 시) | 반나절 |
| F. 문서화 | 반나절 |
| **총** | **약 3.5일** |

E2 (V251 진짜 구현) 선택 시 +1일 → 약 4.5일.

---

## 9. 위험 요소

| 위험 | 영향 | 완화책 |
|---|---|---|
| MSBuild Target 확장 후 기존 빌드 실패 | 🔴 작업 중단 | Phase B 작업은 별도 branch 에서. 각 변경 후 즉시 빌드 검증 |
| WiX 동기화 실패 → MSI 손상 | 🟡 최종 배포 영향 | Phase D 의 verify 스크립트 + 가상머신 설치 테스트 |
| `kill_ansys.bat` 자동 호출이 사용자 작업 손실 유발 | 🟡 사용자 불만 | 기본을 "확인 후 종료" (5.3.b) 로 |
| `.sh` 폐기로 macOS/Linux 개발자 영향 | 🟢 없음 (Windows-only repo) | 영향 없음 |
| V251 폐기 후 V251 사용자 발견 | 🟡 추후 재작업 | Phase A 결정 시 사용자 확인 |

---

## 10. 다음 단계

**Phase A 완료** (2026-05-26):
- ✅ 5.1 V251 폐기 결정
- ✅ 5.2 Ribbon 캐시 클리어 — 매 빌드
- ✅ 5.3 ANSYS 프로세스 — 확인 prompt
- ✅ 5.4 PyInstaller EXE — 자동 빌드
- ✅ 5.5 `.bat`/`.sh` — 부분 유지 (sync_python.sh + verify_deployment.sh + kill_ansys.bat)

**다음**: Phase B (MSBuild Target 완성) 시작 준비. 작업 순서:
1. `MXDigitalTwinModeller.csproj` 의 `DeployMechanicalExtension` Target 에 calibration/ + utils/ + setup_venv.bat + requirements.txt 복사 추가
2. 누락 아이콘 (tied_check.png, material_twin.png, kfile_export.png) 복사 추가
3. Shared Core DLL 을 V252\ 폴더에도 복사 (현재 bin\Debug\ 만)
4. 신규 `ClearWorkbenchRibbonCache` Target 추가
5. PyInstaller EXE 미존재 시 친절한 경고 + WiX 단계만 skip
6. 빌드 → 결과 검증 → MX Material Twin 버튼 실제 동작 확인

---

## 참고

- [현재 빌드 흐름 진단 (대화 기록)](#) — 이 계획서가 기반
- [PHONE_DESIGNER_PLAN.md](PHONE_DESIGNER_PLAN.md) — 별도 sub-project (이 계획과 분리)
- [lat.md/build-deploy.md](lat.md/build-deploy.md) — 현재 빌드 흐름 도메인 문서 (Phase F 에서 갱신)
- [MEMORY.md](C:/Users/Sonic/.claude/projects/d--MXDigitalTwinModeller/memory/MEMORY.md) — 빌드 관련 메모리
