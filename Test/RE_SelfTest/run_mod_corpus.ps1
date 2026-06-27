# =====================================================================
# run_mod_corpus.ps1
#
# Launcher for mod_corpus_test.py — real-CAD MODIFICATION batch test.
# For each of 20 RealCAD models: ChangeHoleDiameter / ChangeFilletRadius
# / ChangeWallThickness on first feature of each kind.
#
# Polls a dedicated marker (RealCAD/mod_corpus_done) rather than the
# headless_* timestamped dir.
# =====================================================================
[CmdletBinding()]
param(
    [string]$ScriptPath = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\mod_corpus_test.py",
    [string]$LogPath    = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\headless_run.log",
    [string]$DoneMarker = "D:\MXDigitalTwinModeller\Test\RealCAD\mod_corpus_done",
    [int]   $TimeoutSec = 1800
)

$ErrorActionPreference = "Stop"
$sc      = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$autoDir = "$env:APPDATA\SpaceClaim\Autosave"

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

Write-Host "[3/6] Remove old log + done marker"
Remove-Item $LogPath -ErrorAction SilentlyContinue
Remove-Item $DoneMarker -ErrorAction SilentlyContinue

Write-Host "[4/6] Launch SpaceClaim (no /Headless)"
$startTime = Get-Date
$p = Start-Process -FilePath $sc -ArgumentList @("/RunScript=$ScriptPath") -PassThru -NoNewWindow
Write-Host "    SC PID=$($p.Id) launched at $startTime"

Write-Host "[5/6] Polling for mod_corpus_done marker (timeout=${TimeoutSec}s)"
$deadline = $startTime.AddSeconds($TimeoutSec)
while ((Get-Date) -lt $deadline) {
    if (Test-Path $DoneMarker) {
        Write-Host "    mod_corpus_done marker found"
        break
    }
    Start-Sleep -Seconds 5
}

if (Test-Path $DoneMarker) {
    Write-Host "[6/6] Done — killing SC + ansyscl"
    Cleanup-SC
    Cleanup-Autosave
    $elapsed = (Get-Date) - $startTime
    Write-Host "    Total elapsed: $($elapsed.TotalSeconds.ToString('F1'))s"
    if (Test-Path $LogPath) {
        Write-Host ""
        Write-Host "===== LOG TAIL ====="
        Get-Content $LogPath -Tail 60
    }
    exit 0
} else {
    Write-Host "[6/6] TIMEOUT — killing SC"
    Cleanup-SC
    Cleanup-Autosave
    if (Test-Path $LogPath) {
        Get-Content $LogPath -Tail 80
    }
    exit 1
}
