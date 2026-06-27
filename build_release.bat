@echo off
cd /d "%~dp0"

echo ============================================================
echo  MX Digital Twin Modeller - Full Release Build
echo ============================================================
echo.

set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "CSPROJ=%~dp0MXDigitalTwinModeller.csproj"
set "VIEWER_EXE=%~dp0Mechanical\MXSimulator\postprocess\MXPostViewer.exe"
set "POSTPROCESS_DIR=%~dp0Mechanical\MXSimulator\postprocess"
set "CALIBRATOR_EXE=%~dp0Mechanical\MXSimulator\calibration\MaterialCalibrator.exe"
set "CALIBRATION_DIR=%~dp0Mechanical\MXSimulator\calibration"

:: ---- Step 1: MaterialCalibrator.exe ------------------------------------
echo [1/4] Checking MaterialCalibrator.exe...
if exist "%CALIBRATOR_EXE%" (
    echo       Found: %CALIBRATOR_EXE%
    goto step2
)

echo       Not found. Building with PyInstaller...
pushd "%CALIBRATION_DIR%"

where python >nul 2>&1
if errorlevel 1 (
    echo ERROR: Python not found in PATH.
    popd & pause & exit /b 1
)

python -m pip install --upgrade pyinstaller numpy scipy matplotlib -q
if errorlevel 1 ( echo pip failed. & popd & pause & exit /b 1 )

python -m PyInstaller --onefile --console --name MaterialCalibrator --hidden-import scipy.stats --hidden-import scipy.optimize --hidden-import scipy.signal --hidden-import numpy runner.py --noconfirm
if errorlevel 1 ( echo PyInstaller failed. & popd & pause & exit /b 1 )

copy "dist\MaterialCalibrator.exe" "MaterialCalibrator.exe" >nul
if errorlevel 1 ( echo Copy failed. & popd & pause & exit /b 1 )

popd
echo       Built: %CALIBRATOR_EXE%

:step2
echo.

:: ---- Step 2: MXPostViewer.exe ------------------------------------------
echo [2/4] Checking MXPostViewer.exe...
if exist "%VIEWER_EXE%" (
    echo       Found: %VIEWER_EXE%
    goto step3
)

echo       Not found. Building with PyInstaller...
pushd "%POSTPROCESS_DIR%"

where python >nul 2>&1
if errorlevel 1 (
    echo ERROR: Python not found in PATH.
    popd & pause & exit /b 1
)

python -m pip install --upgrade pyinstaller numpy scipy matplotlib PyQt5 -q
if errorlevel 1 ( echo pip failed. & popd & pause & exit /b 1 )

python -m PyInstaller --onefile --windowed --name MXPostViewer --hidden-import scipy.signal --hidden-import scipy.fft runner.py --noconfirm
if errorlevel 1 ( echo PyInstaller failed. & popd & pause & exit /b 1 )

copy "dist\MXPostViewer.exe" "MXPostViewer.exe" >nul
if errorlevel 1 ( echo Copy failed. & popd & pause & exit /b 1 )

popd
echo       Built: %VIEWER_EXE%

:step3
echo.

:: ---- Step 3: MSBuild (DLL + ACT deploy + WiX MSI) ----------------------
echo [3/4] Building via MSBuild...
if not exist "%MSBUILD%" (
    echo ERROR: MSBuild not found.
    echo        Expected: %MSBUILD%
    pause & exit /b 1
)

"%MSBUILD%" "%CSPROJ%" /p:Configuration=Debug /p:Platform=AnyCPU /nologo /v:minimal
if errorlevel 1 (
    echo.
    echo === MSBuild FAILED ===
    pause & exit /b 1
)
echo.

:: ---- Step 4: Done -------------------------------------------------------
echo [4/4] Build complete!
echo.
if exist "%~dp0Installer\MXDigitalTwinModeller.msi" (
    echo  MSI: %~dp0Installer\MXDigitalTwinModeller.msi
) else (
    echo  WARNING: MSI not found. Check WiX output above.
)
echo.
echo ============================================================
pause
