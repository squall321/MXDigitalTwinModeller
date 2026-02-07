@echo off
REM ============================================
REM MXDigitalTwinModeller Build Script
REM ============================================

setlocal EnableDelayedExpansion

REM .env 파일에서 환경 변수 로드
if exist .env (
    echo [INFO] .env 파일에서 설정 로드 중...
    for /f "usebackq tokens=1,* delims==" %%a in (".env") do (
        set "line=%%a"
        REM 주석(#)과 빈 줄 무시
        if not "!line:~0,1!"=="#" (
            if not "%%a"=="" (
                if not "%%b"=="" (
                    set "%%a=%%b"
                )
            )
        )
    )
) else (
    echo [WARNING] .env 파일이 없습니다. 기본 경로를 사용합니다.
)

REM MSBuild 경로 설정
if defined MSBUILD_PATH (
    set MSBUILD="%MSBUILD_PATH%"
    echo [INFO] .env에서 MSBuild 경로 로드: !MSBUILD!
) else (
    REM 기본 경로
    set MSBUILD="D:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    echo [INFO] 기본 MSBuild 경로 사용: !MSBUILD!
)

REM 프로젝트 파일
set PROJECT=MXDigitalTwinModeller.csproj

REM MSBuild 존재 확인
if not exist !MSBUILD! (
    echo [ERROR] MSBuild를 찾을 수 없습니다: !MSBUILD!
    echo.
    echo 해결 방법:
    echo 1. Visual Studio가 설치되어 있는지 확인
    echo 2. .env 파일에서 MSBUILD_PATH 경로를 확인하세요
    echo.
    echo 예시:
    echo   MSBUILD_PATH=C:\Program Files ^(x86^)\Microsoft Visual Studio\2017\WDExpress\MSBuild\15.0\Bin\MSBuild.exe
    echo.
    if not "%CI%"=="true" pause
    exit /b 1
)

echo ============================================
echo MXDigitalTwinModeller Build Script
echo ============================================
echo MSBuild: !MSBUILD!
echo.

REM 인자 확인
if "%1"=="" goto :default_build
if /I "%1"=="all" goto :build_all
if /I "%1"=="v251" goto :build_v251
if /I "%1"=="v252" goto :build_v252
if /I "%1"=="clean" goto :clean
if /I "%1"=="help" goto :help

echo [ERROR] 알 수 없는 옵션: %1
goto :help

:default_build
echo [INFO] 기본 빌드: Debug (V252)
echo.
!MSBUILD! %PROJECT% /p:Configuration=Debug /t:Build /v:m
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] 빌드 실패!
    if not "%CI%"=="true" pause
    exit /b %ERRORLEVEL%
)
echo.
echo [SUCCESS] 빌드 완료: Debug (V252)
goto :end

:build_all
echo [INFO] 모든 버전 빌드 (Debug + Release)
echo.

echo [1/2] Debug 빌드 중...
!MSBUILD! %PROJECT% /p:Configuration=Debug /t:Build /v:m
if %ERRORLEVEL% NEQ 0 goto :build_error

echo [2/2] Release 빌드 중...
!MSBUILD! %PROJECT% /p:Configuration=Release /t:Build /v:m
if %ERRORLEVEL% NEQ 0 goto :build_error

echo.
echo [SUCCESS] 모든 빌드 완료!
goto :end

:build_v251
echo [ERROR] V251은 아직 구성되지 않았습니다.
echo [INFO] 현재 V252만 지원됩니다.
if not "%CI%"=="true" pause
exit /b 1

:build_v252
echo [INFO] V252 빌드
echo.

echo [1/2] Debug 빌드 중...
!MSBUILD! %PROJECT% /p:Configuration=Debug /t:Build /v:m
if %ERRORLEVEL% NEQ 0 goto :build_error

echo [2/2] Release 빌드 중...
!MSBUILD! %PROJECT% /p:Configuration=Release /t:Build /v:m
if %ERRORLEVEL% NEQ 0 goto :build_error

echo.
echo [SUCCESS] V252 빌드 완료!
goto :end

:clean
echo [INFO] 빌드 결과물 정리 중...
!MSBUILD! %PROJECT% /t:Clean /v:m
echo [SUCCESS] 정리 완료!
goto :end

:build_error
echo.
echo [ERROR] 빌드 실패!
if not "%CI%"=="true" pause
exit /b 1

:help
echo.
echo 사용법: build.bat [option]
echo.
echo 옵션:
echo   (없음)    - 기본 빌드 (Debug)
echo   all       - 모든 구성 빌드 (Debug + Release)
echo   v252      - V252 빌드 (Debug + Release)
echo   clean     - 빌드 결과물 정리
echo   help      - 이 도움말 표시
echo.
echo 예제:
echo   build.bat           기본 빌드
echo   build.bat all       모든 구성 빌드
echo   build.bat v252      V252 빌드
echo   build.bat clean     정리
echo.
echo 환경 설정:
echo   .env 파일에서 MSBUILD_PATH를 설정하세요
echo   현재 MSBuild: !MSBUILD!
echo.
echo 참고:
echo   현재 V252만 구성되어 있습니다.
echo   V251 지원은 향후 추가될 예정입니다.
echo.
goto :end

:end
echo.
if not "%CI%"=="true" pause
endlocal
