@echo off
REM One-click: register the MXDigitalTwinModeller MCP bridge with Claude Desktop.
REM Double-click this file. It finds Python and runs register_claude_desktop.py,
REM which safely merges an 'mxdtm-spaceclaim' server into your Claude Desktop config
REM (existing servers/settings are preserved; a backup is made first).
setlocal
set "HERE=%~dp0"
set "REG=%HERE%register_claude_desktop.py"

REM Prefer the Windows 'py' launcher, then 'python' on PATH.
where py >nul 2>nul
if %ERRORLEVEL%==0 (
    py "%REG%" %*
    goto :done
)
where python >nul 2>nul
if %ERRORLEVEL%==0 (
    python "%REG%" %*
    goto :done
)

echo.
echo [error] Python was not found on PATH.
echo         Install Python 3.8+ (https://www.python.org/downloads/) and re-run this file,
echo         or register manually using the instructions in README.md.
echo.

:done
echo.
pause
endlocal
