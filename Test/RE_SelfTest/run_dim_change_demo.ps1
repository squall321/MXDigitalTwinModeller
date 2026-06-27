# =====================================================================
# run_dim_change_demo.ps1
#
# Launcher for dim_change_demo.py — exercises the same ModificationService
# dispatch path that the "Change Dimension..." ribbon uses, against the
# ANSYS SampleModel1.scdoc sample.
#
# Mirrors run_real_cad.ps1:
#   1. Kill any existing SC + ansyscl, clean autosave.
#   2. Launch SC with /RunScript=dim_change_demo.py.
#   3. Poll the __done__ marker at the demo dir.
#   4. Kill SC + ansyscl when the marker appears (or on timeout).
#
# Usage: .\run_dim_change_demo.ps1
# =====================================================================
[CmdletBinding()]
param(
    [string]$ScriptPath = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\dim_change_demo.py",
    [string]$DemoDir    = "D:\MXDigitalTwinModeller\Test\RealCAD\demo",
    [string]$ResultBase = "D:\MXDigitalTwinModeller\Test\RE_SelfTest",
    [int]$TimeoutSec    = 600
)

$ErrorActionPreference = "Stop"
$sc      = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$autoDir = "$env:APPDATA\SpaceClaim\Autosave"
$logPath = Join-Path $ResultBase "headless_run.log"
$marker  = Join-Path $DemoDir "__done__"

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

Write-Host "[3/6] Remove old marker + log"
if (Test-Path $marker) { Remove-Item $marker -Force -ErrorAction SilentlyContinue }
Remove-Item $logPath -ErrorAction SilentlyContinue

# Make sure demo dir exists so the marker poll has a stable parent
if (-not (Test-Path $DemoDir)) {
    New-Item -ItemType Directory -Path $DemoDir -Force | Out-Null
}

Write-Host "[4/6] Launch SpaceClaim with /RunScript=$ScriptPath"
$startTime = Get-Date
$p = Start-Process -FilePath $sc -ArgumentList @("/RunScript=$ScriptPath") -PassThru -NoNewWindow
Write-Host "    SC PID=$($p.Id) launched at $startTime"

Write-Host "[5/6] Polling __done__ marker at $marker (timeout=${TimeoutSec}s)"
$deadline = $startTime.AddSeconds($TimeoutSec)
$found = $false
while ((Get-Date) -lt $deadline) {
    if (Test-Path $marker) {
        Write-Host "    __done__ marker found"
        $found = $true
        break
    }
    Start-Sleep -Seconds 5
}

if ($found) {
    Write-Host "[6/6] Script done — killing SC + ansyscl"
    Cleanup-SC
    Cleanup-Autosave
    $elapsed = (Get-Date) - $startTime
    Write-Host "    Total elapsed: $($elapsed.TotalSeconds.ToString('F1'))s"
    if (Test-Path $marker) {
        Write-Host ""
        Write-Host "===== __done__ ====="
        Get-Content $marker
    }
    if (Test-Path $logPath) {
        Write-Host ""
        Write-Host "===== LOG TAIL ====="
        Get-Content $logPath -Tail 80
    }
    $reportPath = Join-Path $DemoDir "dim_change_demo_report.md"
    if (Test-Path $reportPath) {
        Write-Host ""
        Write-Host "===== REPORT ====="
        Get-Content $reportPath
    }
    exit 0
} else {
    Write-Host "[6/6] TIMEOUT — killing SC anyway"
    Cleanup-SC
    Cleanup-Autosave
    if (Test-Path $logPath) {
        Write-Host ""
        Write-Host "===== LOG (timeout) ====="
        Get-Content $logPath -Tail 100
    }
    exit 1
}
