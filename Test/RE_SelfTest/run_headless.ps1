# =====================================================================
# run_headless.ps1
#
# SpaceClaim self-test 실행 wrapper.
# /Headless 옵션은 SC 25.2 Discovery/Student 에서 미지원 → 빼야 모달 안 뜸.
# 대신:
#   1. autosave 청소 (이전 강제종료 잔재)
#   2. SC 실행 (GUI 로 켜지지만 사용자 클릭 불필요)
#   3. __done__ 마커 polling
#   4. 마커 확인 시 SC PID + ansyscl 강제 kill
#   5. autosave 다시 청소 (강제kill 잔재)
#
# Usage:
#   .\run_headless.ps1                  # default script
#   .\run_headless.ps1 -ScriptPath ...  # custom
# =====================================================================
[CmdletBinding()]
param(
    [string]$ScriptPath = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\headless_selftest.py",
    [string]$ResultBase = "D:\MXDigitalTwinModeller\Test\RE_SelfTest",
    [int]$TimeoutSec = 600
)

$ErrorActionPreference = "Stop"
$sc = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
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

Write-Host "[4/6] Launch SpaceClaim (no /Headless — Discovery/Student edition doesn't support it)"
$startTime = Get-Date
# /RunScript 만 — /Headless 빼서 'file not found' modal 회피.
# /Exit 빼서 update-check timeout 으로 인한 modal 도 회피.
# 외부에서 __done__ 마커 보고 강제 kill 한다.
$p = Start-Process -FilePath $sc -ArgumentList @("/RunScript=$ScriptPath") -PassThru -NoNewWindow
Write-Host "    SC PID=$($p.Id) launched at $startTime"

Write-Host "[5/6] Polling for __done__ marker (timeout=${TimeoutSec}s)"
$deadline = $startTime.AddSeconds($TimeoutSec)
$marker = $null
while ((Get-Date) -lt $deadline) {
    if (Test-Path $logPath) {
        $logContent = Get-Content $logPath -Raw -ErrorAction SilentlyContinue
        if ($logContent -match "Total:|FATAL:|UNHANDLED:") {
            # script 완료. 잠깐 더 기다려 __done__ 마커 작성 확실히
            Start-Sleep -Seconds 2
            # 최신 run dir 의 __done__ 찾기
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
    # 결과 summary 출력
    if (Test-Path $logPath) {
        Write-Host ""
        Write-Host "===== LOG TAIL ====="
        Get-Content $logPath -Tail 18
    }
    exit 0
} else {
    Write-Host "[6/6] TIMEOUT — killing SC anyway"
    Cleanup-SC
    Cleanup-Autosave
    if (Test-Path $logPath) {
        Write-Host ""
        Write-Host "===== LOG (timeout) ====="
        Get-Content $logPath -Tail 30
    }
    exit 1
}
