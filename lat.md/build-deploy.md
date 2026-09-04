# Build & Deploy

빌드 명령, 출력 경로, 배포 방식, 인스톨러 구성 등 개발 환경 운영 정보를 모은다. SpaceClaim Add-In 측과 Mechanical ACT Extension 측의 배포 흐름이 분리되어 있고, 둘 다 자동화되어 있다.

## MSBuild

빌드는 Visual Studio 2019 Build Tools 의 MSBuild 로 한다:

```
C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe
```

대상 csproj: `[[MXDigitalTwinModeller.csproj]]`. 기본 명령:

```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe" `
  d:\MXDigitalTwinModeller\MXDigitalTwinModeller.csproj `
  /p:Configuration=Debug-V252 /p:Platform=AnyCPU
```

루트의 `[[build_spaceclaim.bat]]` 가 이 명령을 한 줄로 래핑한 헬퍼.

## 빌드 구성

`[[MXDigitalTwinModeller.csproj]]` 에 4개의 Configuration 정의:

| Configuration | 정의 심볼 | 출력 경로 |
|---|---|---|
| Debug-V251 | `DEBUG; V251` | `ADDIN_OUTPUT_PATH_V251` (env var) |
| Release-V251 | `V251` | `ADDIN_OUTPUT_PATH_V251` |
| Debug-V252 | `DEBUG; V252` | `ADDIN_OUTPUT_PATH_V252` |
| Release-V252 | `V252` | `ADDIN_OUTPUT_PATH_V252` |

**기본**: Debug-V252. 환경 변수는 `.env` 에서 로드 (`[[.env.example]]` 템플릿 참조).

## 출력 경로

빌드 산출물이 자동으로 복사되는 곳:

- **SpaceClaim Add-In V252**: `C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\`
- **SpaceClaim Add-In V251**: `C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V251\`
- **Mechanical ACT Extension**: `%APPDATA%\ANSYS\v252\ACT\extensions\MXSimulator\`

## Mechanical ACT Extension 자동 배포

`[[MXDigitalTwinModeller.csproj]]` 에 PostBuild target 으로 Mechanical 측 배포가 통합되어 있다. 빌드 시 다음이 자동으로 일어남:

1. `Mechanical/MXSimulator/` 전체를 `%APPDATA%\ANSYS\v252\ACT\extensions\MXSimulator\` 로 복사
2. `Shared/MXDigitalTwinModeller.Core.dll` 을 그 안의 `bin/` 폴더에 복사
3. `Mechanical/MXSimulator.xml` 을 `%APPDATA%\ANSYS\v252\ACT\extensions\` 에 복사 (실제 사용되는 ACT 정의 파일)

배포 검증 스크립트: `[[Mechanical/verify_deployment.sh]]`.

수동 배포가 필요할 경우 `[[Mechanical/deploy_mxsimulator.sh]]` 사용.

## SpaceClaim DLL 잠금 회피

빌드 중 SpaceClaim 이 실행 중이면 DLL 잠금으로 실패. 빌드 전 ANSYS 프로세스 일괄 종료:

```
[[Mechanical/kill_ansys.bat]]
```

이 배치는 `SpaceClaim.exe`, `AnsysWBU.exe`, `AnsysAct.exe` 등을 한꺼번에 죽인다. 이 패턴은 [[api-learnings#SpaceClaim DLL 잠금]] 도 참조.

## Release 빌드 스크립트

CI 없이 로컬에서 배포 가능한 패키지를 만들 때:

- `[[build_release.bat]]` — Windows CMD 용
- `[[build_release.ps1]]` — PowerShell 용

`build_release.ps1` 이 단일 진입점: venv 준비 → EXE freshness 검사/재빌드 → MSBuild **Debug** 구성(`/p:Configuration=Debug`; 이전 문서의 "Release-V252" 는 실제와 달랐다) → WiX MSI. `.bat` 는 이 ps1 을 호출한다.

## WiX 인스톨러

`[[Installer/MXDigitalTwinModeller.wxs]]` 가 MSI 정의. 한 번 실행으로:

1. SpaceClaim Add-In 을 `C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\` 에 설치
2. Mechanical ACT Extension 을 `%APPDATA%\ANSYS\v252\ACT\extensions\MXSimulator\` 에 설치
3. Shared DLL 양쪽에 복사

빌드된 MSI 는 `Installer/MXDigitalTwinModeller.msi`. 이게 최종 사용자에게 배포되는 단일 산출물.

### MSI 동기화 체크리스트 (2026-09-02 감사 결과)

"한 번의 설치로 추가한 기능이 전부 업데이트되는가" 를 감사했더니 **세 가지 방식으로 조용히 빠지고 있었다.**

**1. stale EXE — 가장 큰 구멍.** `[[build_release.ps1]]` Step 1~3 은 PyInstaller EXE 를 **없을 때만** 빌드하고,
csproj `BuildInstaller` 의 `CanBuildMsi` 도 존재 여부만 본다. 그래서 소스가 바뀌어도 EXE 는 그대로 MSI 에 실린다:

| EXE | 빌드일 | 소스 최신 수정 | 결과 |
|---|---|---|---|
| `postprocess/MXPostViewer.exe` | 2026-02-18 | visualizer.py 2026-07-06 (+09-02) | 07-12 MSI 사용자는 Sweep/Energy/Fatigue 탭을 본 적이 없다. `_launch_viewer` 가 EXE 를 **우선** 실행하므로 `visualizer.py` 를 같이 실어도 무의미 |
| `calibration/MaterialCalibrator.exe` | 2026-02-22 | runner.py / elastic_calibrator.py 2026-06-01 | 6월 캘리브레이터 수정이 MSI 에 없음 |
| `tools/mcp_bridge/*.exe` | 2026-07-01 | 2026-07-01 (EXE 가 더 늦음) | OK |

→ `build_release.ps1` 을 재작성: **EXE 가 소스보다 오래됐으면 재빌드** (`Test-Stale`), 시작 시 freshness 표 출력,
`-Rebuild`(강제) / `-SkipMsi`(csproj `SkipMsi` 프로퍼티) 스위치. `build_release.bat` 는 ps1 을 호출하는 래퍼로 축소.

**2. PyInstaller 는 깨끗한 venv 에서만.** 시스템 Python 3.13 에 torch 2.12 등이 깔려 있어 PyInstaller 가
`hook-torch` 를 타고 수 GB 를 끌어들인다 (2월 EXE 는 102MB; 재빌드 첫 시도에서 실제로 발생해 중단). `build_viewer.bat`
주석("venv 먼저 활성화")이 맞다. 이제 ps1 이 루트 `.venv-build` 를 만들어 거기서만 빌드하고 torch 부재를 단언한다.
또 ps1 의 뷰어 빌드에 `--hidden-import rainflow` 가 없었다 (`build_viewer.bat` 에는 있음) → ps1 로 만든 EXE 는 Fatigue 탭이
깨졌을 것. 추가함.

**3. wxs 파일 목록 누락.** `MXSimulator.xml` 이 참조하는 아이콘 8개 중 `kfile_export.png`, `tied_check.png` 가
`MechanicalImages` 에 없었다 — csproj `DeployMechanicalExtension` 은 8개를 다 복사했지만 (주석에 "누락 아이콘 3개
추가" 라고 써 놓고) MSI 쪽은 빠뜨렸다. 1.5.0 MSI File 테이블(41개)로 실증. **→ 추가함 (8/8).**

**4. 버전 범프.** `MajorUpgrade` 는 `MXVersion` 이 올라가야 설치본을 교체한다. 같은 1.5.0 으로 다시 빌드하면
"already installed" 로 끝난다. **→ 1.6.0** (csproj 단일 원천, README 도 동기; wxs 의 bare-build 폴백도 1.6.0).

**5. 재작성한 `build_release.ps1` 자체의 결함 (2026-09-03 독립 리뷰에서 발견).**
- `Invoke-PyInstaller` 의 파라미터 이름을 `$Args` 로 두면 PowerShell 자동변수 `$args` 에 가려져 `@Args` 스플랫이
  **빈 값**이 된다 → PyInstaller 가 스크립트 없이 호출돼 "scriptname required" 로 실패. 재빌드 경로가 한 번도 안 도는 한
  증상이 안 보인다 (당일 EXE 가 전부 fresh 였다). PS 5.1 에서 실증(파라미터 5개 → 본문에서 0개). **→ `$PyArgs` + 빈 인자 방어.**
- PyInstaller 산출물(`build/`, `dist/`, `*.spec`)을 소스 폴더에 쓰고 있었는데, `calibration/build/*`·`MaterialCalibrator.spec`
  은 **git 추적 대상**이라 빌드가 저장소를 더럽혔다. **→ `.venv-build\pyi\<Name>\` 스크래치로.**
- 외부 `PYTHONPATH`(OpenCASCADE/NGSolve site-packages)가 venv 안으로 새어 들어온다 (실행 로그의 모듈 탐색 경로에서 확인).
  **→ 빌드 전 비운다.**
- `-SkipMsi` 가 csproj 에 전달되지 않았다 → `CanBuildMsi` 조건 + 전용 메시지 추가. `sweep_analyzer.py`·`batch/` 는 MSI 에는
  있고 개발배포(csproj `DeployMechanicalExtension`, `deploy_mxsimulator.sh`)에는 없었다 → 목록 동기화.
- Material Twin: MSI 가 `setup_venv.bat`/`requirements.txt` 를 `calibration\` 에 넣어 csproj/sh(루트) 와 달랐고, main.py 는
  venv 게이트를 EXE 검사보다 먼저 통과시켜 **순수 MSI 설치에서 캘리브레이션이 아예 안 됐다** (세션 이전부터). → wxs 컴포넌트를
  루트로, main.py 는 `MaterialCalibrator.exe` 먼저.

**실행 검증 (2026-09-03).** 수정된 `build_release.ps1` 을 인자 없이 끝까지 실행: `.venv-build` 생성(torch 부재 확인) →
freshness 표에서 `MXPostViewer.exe` 만 REBUILD(소스 09-03 > EXE 09-02), 나머지 3개 fresh → PyInstaller 가 스크립트/옵션을
정상 수신해 `.venv-build\pyi\MXPostViewer\dist\` 에 96.7 MB EXE 생성 → MSBuild(DLL, ACT 배포, 리본 캐시) → `wix build` →
MSI 1.6.0 (43 파일, COM 검증 OK). 실행 후 `git status` 에 build/dist/spec 없음. 자동화 시에는 `< /dev/null` 로 stdin 을 막아
끝의 `Read-Host` 가 대기하지 않게 할 것.

**설치 검증 (2026-09-03).** 그 MSI 를 개발 머신에 `msiexec /i ... /qn /norestart /l*v` 로 설치(perMachine → UAC 승인 필요;
`Start-Process msiexec -Verb RunAs -Wait` 로 띄우면 승인 창이 뜬다). 결과: exit 0, 레지스트리 `MX Digital Twin Modeller 1.6.0`,
로그에 `RegisterClaudeDesktop` 실행(반환값 1) + "Installation completed successfully". 설치 경로 확인 — ProgramData Add-In 에
DLL/Core.dll/Manifest/`Libs\gmsh`/`mcp_bridge`, `%APPDATA%\Ansys\v252\ACT\extensions` 에 xml/main.py/아이콘 8/postprocess(뷰어 EXE 포함)/
`batch`/`bin`/calibration EXE, 그리고 **루트의 `setup_venv.bat`·`requirements.txt`** (컴포넌트 이동 반영). Claude Desktop 설정에
`mxdtm-spaceclaim` 서버가 `...\V252\mcp_bridge\mxdtm_mcp_bridge.exe` 로 등록됨. 주의: (1) 이 머신엔 이전 버전이 없었으므로
MajorUpgrade 경로(1.5.0→1.6.0)는 아직 실측 전. (2) MSI 는 리본 캐시를 지우지 않는다 — 개발 머신에선 MSBuild 가 지웠지만
엔드유저 머신에선 Workbench 가 새 버튼을 못 볼 수 있다 → `ClearWorkbenchRibbonCache` 에 해당하는 커스텀 액션 추가를 검토할 것.
(3) 개발 머신에 MSI 를 깔면 MSBuild 출력 경로와 MSI 컴포넌트가 같은 파일을 가리킨다 — 이후 MSBuild 가 덮어써도 동작엔 문제
없지만, MSI 제거 시 그 파일들이 사라지므로 다시 빌드해야 한다.

**MSI 내용 검증 (msilib 는 py3.13 에서 제거됨 → WindowsInstaller COM).** `SELECT FileName, FileSize FROM File` 과
`SELECT Value FROM Property WHERE Property='ProductVersion'` 을 COM 으로 읽는다. PowerShell 은 1행 결과를 스칼라로
풀어버리므로 `return ,$rows` / `@(...)` 로 막을 것 (처음 짠 버전은 그것 때문에 버전이 "1" 로 찍혔다).

## Python 환경 (Material Calibrator)

Mechanical 사이드의 Material Calibrator 는 별도의 Python venv 에서 작업한다. 셋업 스크립트:

- `[[Mechanical/MXSimulator/setup_venv.bat]]` — 메인 venv (calibration, postprocess 양쪽 용)
- `[[Mechanical/MXSimulator/calibration/build_calibrator.bat]]` — PyInstaller 로 `MaterialCalibrator.exe` 빌드
- `[[Mechanical/MXSimulator/postprocess/setup_venv.bat]]` — 시각화용 별도 venv
- `[[Mechanical/MXSimulator/postprocess/build_viewer.bat]]` — 뷰어 빌드

자세한 작동 방식은 [[material-calibrator]] 와 [[postprocess]] 참조.

## 디버깅 프로파일

`.vscode/launch.json` (Visual Studio 측은 launchSettings.json) 에 두 디버그 프로파일:

- **SpaceClaim V251 Debug** — V251 자동 실행 + AddIn 로드
- **SpaceClaim V252 Debug** — V252 자동 실행 + AddIn 로드 (기본)

F5 누르면 SpaceClaim 이 뜨면서 AddIn 이 로드되어 중단점 사용 가능.
