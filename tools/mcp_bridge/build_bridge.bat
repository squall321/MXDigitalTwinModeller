@echo off
REM Build the MCP bridge + registrar as standalone Windows EXEs (no Python needed at the
REM user's machine). Mirrors the project's PyInstaller pattern (build_viewer.bat).
REM Output: mxdtm_mcp_bridge.exe and register_claude_desktop.exe in THIS folder,
REM which the MSI then bundles. Both are CONSOLE apps:
REM   - the bridge is an MCP stdio server (needs stdin/stdout) -> NOT --windowed
REM   - the registrar prints progress -> console
cd /d "%~dp0"
echo === MXDTM MCP bridge: PyInstaller build ===
echo.

where python >nul 2>&1
if errorlevel 1 (
    echo ERROR: Python not found in PATH. Install Python 3.9+ (build machine only).
    pause & exit /b 1
)

echo Installing / upgrading PyInstaller...
python -m pip install --upgrade pyinstaller -q
if errorlevel 1 ( echo pip failed. & pause & exit /b 1 )

echo.
echo Building mxdtm_mcp_bridge.exe (--onefile, console) ...
python -m PyInstaller --onefile --console --name mxdtm_mcp_bridge mxdtm_mcp_bridge.py
if errorlevel 1 ( echo. & echo === BRIDGE BUILD FAILED === & pause & exit /b 1 )

echo.
echo Building register_claude_desktop.exe (--onefile, console) ...
python -m PyInstaller --onefile --console --name register_claude_desktop register_claude_desktop.py
if errorlevel 1 ( echo. & echo === REGISTRAR BUILD FAILED === & pause & exit /b 1 )

echo.
echo Copying exes next to the sources (for the MSI to pick up) ...
copy /y "dist\mxdtm_mcp_bridge.exe" "%~dp0mxdtm_mcp_bridge.exe" >nul
copy /y "dist\register_claude_desktop.exe" "%~dp0register_claude_desktop.exe" >nul

echo.
echo === Build complete ===
echo   %~dp0mxdtm_mcp_bridge.exe
echo   %~dp0register_claude_desktop.exe
echo.
pause
