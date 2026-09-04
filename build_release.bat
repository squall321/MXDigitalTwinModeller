@echo off
:: MX Digital Twin Modeller - Full Release Build (CMD wrapper)
::
:: 실제 로직은 build_release.ps1 하나에만 있다 (단일 진입점, BUILD_PIPELINE_PLAN.md §3.1).
:: 예전에는 이 .bat 이 ps1 과 별도 구현이라 "EXE 없을 때만 빌드 / 시스템 Python / rainflow 누락" 세 결함을
:: 그대로 갖고 있었다 (lat.md/build-deploy.md "MSI 동기화 체크리스트"). 이제는 인자를 그대로 넘긴다.
::
::   build_release.bat            일반 빌드 (EXE 는 소스보다 오래됐을 때만 재빌드)
::   build_release.bat -Rebuild   PyInstaller EXE 3종 강제 재빌드
::   build_release.bat -SkipMsi   MSI 생략 (개발 iteration)
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build_release.ps1" %*
exit /b %ERRORLEVEL%
