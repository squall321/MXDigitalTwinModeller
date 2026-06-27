# =====================================================================
# run_real_cad.ps1
#
# Launcher for real_cad_test.py — exercises RealModelPipeline.Run
# against the ANSYS sample library + downloaded public corpus.
#
# Mirrors run_headless.ps1:
#   1. Cleanup existing SC + autosave.
#   2. Launch SC with /RunScript=real_cad_test.py (no /Headless).
#   3. Poll for __done__ marker in latest headless_* OUT_DIR.
#   4. Kill SC + ansyscl on completion.
#
# Usage: .\run_real_cad.ps1
# =====================================================================
[CmdletBinding()]
param(
    [string]$ScriptPath = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\real_cad_test.py",
    [string]$ResultBase = "D:\MXDigitalTwinModeller\Test\RE_SelfTest",
    [int]$TimeoutSec    = 1800   # 30 min — real STEPs can be heavy
)

$ErrorActionPreference = "Stop"
$sc      = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$autoDir = "$env:APPDATA\SpaceClaim\Autosave"
$logPath = Join-Path $ResultBase "headless_run.log"

function Cleanup-SC {
    Get-Process SpaceClaim, ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 3
}

function Cleanup-Autosave {
    if (Test-Path $autoDir) {
        Get-ChildItem $autoDir -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "[1/6] Cleanup existing SC processes"
Cleanup-SC

Write-Host "[2/6] Cleanup autosave dir"
Cleanup-Autosave

Write-Host "[3/6] Remove old run log"
Remove-Item $logPath -ErrorAction SilentlyContinue

Write-Host "[4/6] Launch SpaceClaim (no /Headless — Discovery/Student edition)"
$startTime = Get-Date
$p = Start-Process -FilePath $sc -ArgumentList @("/RunScript=$ScriptPath") -PassThru -NoNewWindow
Write-Host "    SC PID=$($p.Id) launched at $startTime"

Write-Host "[5/6] Polling for __done__ marker (timeout=${TimeoutSec}s)"
$deadline = $startTime.AddSeconds($TimeoutSec)
$marker = $null
while ((Get-Date) -lt $deadline) {
    if (Test-Path $logPath) {
        $logContent = Get-Content $logPath -Raw -ErrorAction SilentlyContinue
        if ($logContent -match "Total:|FATAL:|UNHANDLED:") {
            Start-Sleep -Seconds 2
            $latestRun = Get-ChildItem $ResultBase -Directory -Filter "headless_*" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
            if ($latestRun) {
                $marker = Join-Path $latestRun.FullName "__done__"
                if (Test-Path $marker) {
                    Write-Host "    __done__ marker found: $marker"
                    break
                }
            }
        }
    }
    Start-Sleep -Seconds 5
}

if ($marker -and (Test-Path $marker)) {
    Write-Host "[6/6] Script done — killing SC + ansyscl"
    Cleanup-SC
    Cleanup-Autosave
    $elapsed = (Get-Date) - $startTime
    Write-Host "    Total elapsed: $($elapsed.TotalSeconds.ToString('F1'))s"
    if (Test-Path $logPath) {
        Write-Host ""
        Write-Host "===== LOG TAIL ====="
        Get-Content $logPath -Tail 60
    }
    exit 0
} else {
    Write-Host "[6/6] TIMEOUT — killing SC anyway"
    Cleanup-SC
    Cleanup-Autosave
    if (Test-Path $logPath) {
        Write-Host ""
        Write-Host "===== LOG (timeout) ====="
        Get-Content $logPath -Tail 80
    }
    exit 1
}
