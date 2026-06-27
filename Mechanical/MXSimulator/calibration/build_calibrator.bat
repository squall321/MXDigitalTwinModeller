@echo off
cd /d "%~dp0"
echo === Elastic Calibrator: PyInstaller build ===
echo.

where python >nul 2>&1
if errorlevel 1 (
    echo ERROR: Python not found in PATH.
    echo        Activate calibration_env first:
    echo        cd ..\
    echo        calibration_env\Scripts\activate.bat
    pause & exit /b 1
)

echo Installing / upgrading PyInstaller...
python -m pip install --upgrade pyinstaller -q
if errorlevel 1 ( echo pip failed. & pause & exit /b 1 )

echo.
echo Building MaterialCalibrator.exe (--onefile --console) ...
echo   - Unified calibrator for all phases (elastic, plastic, visco, hyper)
python -m PyInstaller ^
    --onefile ^
    --console ^
    --name MaterialCalibrator ^
    --hidden-import scipy.stats ^
    --hidden-import scipy.optimize ^
    --hidden-import scipy.signal ^
    --hidden-import numpy ^
    runner.py

if errorlevel 1 (
    echo.
    echo === BUILD FAILED ===
    pause & exit /b 1
)

echo.
echo === Build complete! ===
echo Output: dist\MaterialCalibrator.exe
echo.
echo Copy it to this folder for deployment:
echo   copy dist\MaterialCalibrator.exe "%~dp0MaterialCalibrator.exe"
echo.

set /p COPY=Copy now? [Y/n]:
if /i not "%COPY%"=="n" (
    copy "dist\MaterialCalibrator.exe" "%~dp0MaterialCalibrator.exe"
    echo [OK] Copied to: %~dp0MaterialCalibrator.exe
    echo Now run: bash Mechanical/deploy_mxsimulator.sh
)
pause
