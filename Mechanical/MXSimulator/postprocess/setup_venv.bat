@echo off
cd /d "%~dp0"
echo === MX Post-Process: Setting up Python virtual environment ===

where python >nul 2>&1
if errorlevel 1 (
    echo ERROR: Python not found in PATH. Install Python 3.9+ first.
    pause
    exit /b 1
)

echo Creating venv...
python -m venv venv
if errorlevel 1 ( echo Failed to create venv. & pause & exit /b 1 )

echo Installing packages...
venv\Scripts\pip install --upgrade pip -q
venv\Scripts\pip install -r requirements.txt
if errorlevel 1 ( echo Failed to install packages. & pause & exit /b 1 )

echo.
echo === Setup complete. venv\Scripts\python.exe is ready. ===
pause
