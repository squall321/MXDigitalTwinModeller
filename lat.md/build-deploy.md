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

둘 다 Release-V252 구성으로 빌드 후 ZIP/MSI 생성을 자동화한다.

## WiX 인스톨러

`[[Installer/MXDigitalTwinModeller.wxs]]` 가 MSI 정의. 한 번 실행으로:

1. SpaceClaim Add-In 을 `C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\` 에 설치
2. Mechanical ACT Extension 을 `%APPDATA%\ANSYS\v252\ACT\extensions\MXSimulator\` 에 설치
3. Shared DLL 양쪽에 복사

빌드된 MSI 는 `Installer/MXDigitalTwinModeller.msi`. 이게 최종 사용자에게 배포되는 단일 산출물.

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
