@echo off
REM Material Twin - Virtual Environment Setup
REM Creates isolated Python environment with scipy optimization tools

echo ========================================
echo MX Material Twin - venv Setup
echo ========================================
echo.

set VENV_DIR=%~dp0calibration_env

REM Check if venv already exists
if exist "%VENV_DIR%" (
    echo [WARNING] Virtual environment already exists at:
    echo %VENV_DIR%
    echo.
    choice /C YN /M "Do you want to recreate it? (This will delete existing environment)"
    if errorlevel 2 goto :skip_create
    if errorlevel 1 (
        echo Removing existing environment...
        rmdir /s /q "%VENV_DIR%"
    )
)

:create_venv
echo [1/3] Creating virtual environment...
python -m venv "%VENV_DIR%"
if errorlevel 1 (
    echo [ERROR] Failed to create virtual environment
    echo Make sure Python 3.7+ is installed and in PATH
    pause
    exit /b 1
)
echo [OK] Virtual environment created

echo.
echo [2/3] Activating environment...
call "%VENV_DIR%\Scripts\activate.bat"

echo.
echo [3/3] Installing dependencies...
echo Installing: numpy, scipy, scikit-learn, matplotlib, pandas
python -m pip install --upgrade pip
pip install -r "%~dp0requirements.txt"
if errorlevel 1 (
    echo [ERROR] Failed to install dependencies
    pause
    exit /b 1
)

echo.
echo ========================================
echo [SUCCESS] Material Twin venv ready!
echo ========================================
echo.
echo Location: %VENV_DIR%
echo.
echo To activate manually:
echo   %VENV_DIR%\Scripts\activate.bat
echo.
echo Testing scipy import...
python -c "import scipy; import numpy; import sklearn; print('All modules OK - scipy version:', scipy.__version__)"
echo.
pause
goto :end

:skip_create
echo Skipping venv creation. Existing environment preserved.

:end
