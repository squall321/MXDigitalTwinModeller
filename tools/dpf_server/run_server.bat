@echo off
REM MX DPF server - Windows launcher (run on the machine where the ANSYS license is available)
REM First time:  py -3 -m venv .venv  &&  .venv\Scripts\pip install -r requirements.txt
setlocal
cd /d "%~dp0"

if not exist ".venv\Scripts\python.exe" (
  echo [mxdpf] .venv not found. Run first:  py -3 -m venv .venv ^&^& .venv\Scripts\pip install -r requirements.txt
  exit /b 1
)

REM ---- Override as needed (unset = defaults in mxdpf\config.py) ----
if not defined MXDPF_HOST            set MXDPF_HOST=127.0.0.1
if not defined MXDPF_PORT            set MXDPF_PORT=8770
if not defined MXDPF_WORK_DIR        set MXDPF_WORK_DIR=%LOCALAPPDATA%\MXDTM\dpf_jobs
if not defined MXDPF_MAX_CONCURRENCY set MXDPF_MAX_CONCURRENCY=1
REM set MXDPF_TOKEN=...                       (required when binding 0.0.0.0)
REM set MXDPF_ALLOWED_ROOTS=\\fileserver\cae;D:\results
REM set AWP_ROOT252=C:\Program Files\ANSYS Inc\v252
REM set ANSYSLMD_LICENSE_FILE=1055@license-server

".venv\Scripts\python.exe" -m mxdpf %*
