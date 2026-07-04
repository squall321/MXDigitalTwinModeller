# PyAnsys / DPF Student-license PROBE (POSTPROCESS_IDEAS.md §10.1 / §10.5 decision gate).
# Isolates each step so we know EXACTLY where it fails: venv create -> pip install ->
# dpf.SERVER (DPF Community Server reachable?) -> launch_mechanical(v252) (headless Mechanical
# under the Student license?). Every step's outcome is appended to the result log; nothing here
# is destructive beyond creating .venv-pyansys (git-ignored).
$ErrorActionPreference = "Continue"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$venv = Join-Path $here ".venv-pyansys"
$log  = Join-Path $here "pyansys_probe_result.txt"
$py   = (Get-Command python -ErrorAction SilentlyContinue).Source

function Log($m) { $m | Tee-Object -FilePath $log -Append }

"" | Out-File $log   # truncate
Log "=== PyAnsys/DPF probe  (start) ==="
Log ("host python : {0}" -f $py)
Log ("python ver  : {0}" -f (& $py --version 2>&1))
Log ("AWP_ROOT252 : {0}" -f $env:AWP_ROOT252)
Log ""

# ---- Step 1: venv --------------------------------------------------------
Log "--- [1] create venv .venv-pyansys ---"
if (-not (Test-Path (Join-Path $venv "Scripts\python.exe"))) {
    & $py -m venv $venv 2>&1 | Tee-Object -FilePath $log -Append
}
$vpy = Join-Path $venv "Scripts\python.exe"
if (Test-Path $vpy) { Log ("venv python: {0}" -f $vpy); Log ("venv ver   : {0}" -f (& $vpy --version 2>&1)) }
else { Log "[FAIL] venv python not created"; Log "=== PROBE ABORTED (venv) ==="; exit 1 }
Log ""

# ---- Step 2: pip install (the Python-3.13-compat risk lives here) --------
Log "--- [2] pip install ansys-mechanical-core ansys-dpf-core ansys-dpf-post ---"
& $vpy -m pip install --upgrade pip -q 2>&1 | Tee-Object -FilePath $log -Append
& $vpy -m pip install ansys-mechanical-core ansys-dpf-core ansys-dpf-post 2>&1 | Tee-Object -FilePath $log -Append
$pipExit = $LASTEXITCODE
Log ("pip exit code: {0}" -f $pipExit)
Log "--- installed ansys-* versions ---"
& $vpy -m pip list 2>&1 | Select-String -Pattern "ansys" | ForEach-Object { Log $_.Line }
Log ""

if ($pipExit -ne 0) {
    Log "[GATE] pip install FAILED. Likely Python 3.13 wheel gap (DPF/gRPC often lag new CPython)."
    Log "       -> Re-probe under Python 3.10-3.12 before concluding PyAnsys is unavailable."
    Log "=== PROBE STOPPED at pip (see above) ==="
    exit 2
}

# ---- Step 3: dpf.SERVER (DPF Community Server) ---------------------------
Log "--- [3] dpf.SERVER (DPF Community Server reachable?) ---"
$dpfScript = Join-Path $here "_probe_dpf.py"
@'
import sys
try:
    from ansys.dpf import core as dpf
    print("import ansys.dpf.core: OK")
    srv = dpf.SERVER
    print("dpf.SERVER =", srv)
    print("DPF_STEP: OK")
except Exception as e:
    print("DPF_STEP: FAIL ->", type(e).__name__, str(e)[:400])
    sys.exit(3)
'@ | Out-File -FilePath $dpfScript -Encoding utf8
& $vpy $dpfScript 2>&1 | Tee-Object -FilePath $log -Append
$dpfExit = $LASTEXITCODE
Log ("dpf step exit: {0}" -f $dpfExit)
Log ""

# ---- Step 4: launch_mechanical(version=252) (headless, Student license) --
Log "--- [4] launch_mechanical(version=252) (headless Mechanical on Student) ---"
$mechScript = Join-Path $here "_probe_mech.py"
@'
import sys
try:
    from ansys.mechanical.core import launch_mechanical
    print("import launch_mechanical: OK")
    m = launch_mechanical(version="252")
    print("launched:", m)
    print("MECH_STEP: OK")
    try: m.exit()
    except Exception: pass
except Exception as e:
    print("MECH_STEP: FAIL ->", type(e).__name__, str(e)[:500])
    sys.exit(4)
'@ | Out-File -FilePath $mechScript -Encoding utf8
& $vpy $mechScript 2>&1 | Tee-Object -FilePath $log -Append
$mechExit = $LASTEXITCODE
Log ("mech step exit: {0}" -f $mechExit)
Log ""

# ---- Verdict -------------------------------------------------------------
Log "=== VERDICT ==="
Log ("  pip install : {0}" -f $(if ($pipExit -eq 0) {"OK"} else {"FAIL"}))
Log ("  dpf.SERVER  : {0}" -f $(if ($dpfExit -eq 0) {"OK"} else {"FAIL"}))
Log ("  launch_mech : {0}" -f $(if ($mechExit -eq 0) {"OK"} else {"FAIL"}))
$go = ($pipExit -eq 0) -and (($dpfExit -eq 0) -or ($mechExit -eq 0))
Log ("  Phase-2 gate: {0}" -f $(if ($go) {"GREEN (at least one PyAnsys path works)"} else {"RED (fallback to popup-only / assume Enterprise for power users)"}))
Log "=== probe done ==="
