@echo off
cd /d "%~dp0"
echo === MXPostViewer: PyInstaller build ===
echo.

where python >nul 2>&1
if errorlevel 1 (
    echo ERROR: Python not found in PATH.
    echo        Activate the venv first, or install Python 3.9+.
    pause & exit /b 1
)

echo Installing / upgrading PyInstaller...
python -m pip install --upgrade pyinstaller -q
if errorlevel 1 ( echo pip failed. & pause & exit /b 1 )

echo.
echo Building MXPostViewer.exe (--onefile --windowed) ...
python -m PyInstaller ^
    --onefile ^
    --windowed ^
    --name MXPostViewer ^
    --hidden-import scipy.signal ^
    --hidden-import scipy.fft ^
    runner.py

if errorlevel 1 (
    echo.
    echo === BUILD FAILED ===
    pause & exit /b 1
)

echo.
echo === Build complete! ===
echo Output: dist\MXPostViewer.exe
echo.
echo Copy it to this folder for deployment:
echo   copy dist\MXPostViewer.exe "%~dp0MXPostViewer.exe"
echo.

set /p COPY=Copy now? [Y/n]:
if /i not "%COPY%"=="n" (
    copy "dist\MXPostViewer.exe" "%~dp0MXPostViewer.exe"
    echo [OK] Copied to: %~dp0MXPostViewer.exe
    echo Now run: bash Mechanical/deploy_mxsimulator.sh
)
pause
