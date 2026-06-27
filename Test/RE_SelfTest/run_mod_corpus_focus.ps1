# =====================================================================
# run_mod_corpus_focus.ps1
#
# Runs ONLY the previously-failing cells from the atomic corpus test,
# to quickly validate fixes without a full 35-min rerun.
# =====================================================================
[CmdletBinding()]
param(
    [int]$PerCellTimeoutSec = 180
)

$ErrorActionPreference = "Stop"
$sc            = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$autoDir       = "$env:APPDATA\SpaceClaim\Autosave"
$scriptPath    = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\mod_atomic_test.py"
$logPath       = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\headless_run.log"
$realCadDir    = "D:\MXDigitalTwinModeller\Test\RealCAD"
$targetPath    = Join-Path $realCadDir "solo_target.txt"
$doneMarker    = Join-Path $realCadDir "solo_done.txt"
$resultsDir    = Join-Path $realCadDir "atomic_results"

# Phase-2 validation set: 3 remaining fail cells + scaled-path regression.
$ansys = "d:\Program Files\ANSYS Inc\ANSYS Student\v252\SCDM\Library\SrModels"
$cells = @(
    @{idx=13; name="linkrods";             path="$realCadDir\occt\linkrods.step";                                       mod="Wall"},
    @{idx=14; name="624ZZ_Ball_Bearing";   path="$realCadDir\freecad\624ZZ_Ball_Bearing.stp";                          mod="Wall"},
    @{idx=6;  name="nist_ftc_07";          path="$realCadDir\nist\NIST-PMI-STEP-Files\nist_ftc_07_asme1_ap242-e2.stp"; mod="Fillet"},
    @{idx=16; name="as1-oc-214";           path="$realCadDir\stepcode\as1-oc-214.stp";                                  mod="Wall"},
    @{idx=9;  name="as1-md-214";           path="$realCadDir\caxif\as1-md-214.stp";                                     mod="Wall"},
    @{idx=5;  name="nist_ctc_01";          path="$realCadDir\nist\NIST-PMI-STEP-Files\nist_ctc_01_asme1_ap242-e1.stp"; mod="Fillet"}
)

function Cleanup-SC {
    Get-Process SpaceClaim, ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 2
}
function Cleanup-Autosave {
    if (Test-Path $autoDir) {
        Get-ChildItem $autoDir -ErrorAction SilentlyContinue |
            Remove-Item -Force -ErrorAction SilentlyContinue
    }
}

Cleanup-SC; Cleanup-Autosave
Remove-Item $logPath -ErrorAction SilentlyContinue
if (!(Test-Path $resultsDir)) { New-Item -ItemType Directory -Path $resultsDir | Out-Null }

$startAll = Get-Date
$total = $cells.Count
for ($i = 0; $i -lt $total; $i++) {
    $c = $cells[$i]
    Write-Host ("[{0}/{1}] {2} | {3}" -f ($i+1), $total, $c.name, $c.mod)

    "$($c.idx)`t$($c.name)`t$($c.path)`t$($c.mod)" | Out-File -FilePath $targetPath -Encoding utf8
    Remove-Item $doneMarker -ErrorAction SilentlyContinue
    $resultJson = Join-Path $resultsDir ("{0:D2}_{1}_{2}.json" -f $c.idx, $c.name, $c.mod)
    Remove-Item $resultJson -ErrorAction SilentlyContinue

    $start = Get-Date
    $p = Start-Process -FilePath $sc -ArgumentList @("/RunScript=$scriptPath") -PassThru -NoNewWindow
    $deadline = $start.AddSeconds($PerCellTimeoutSec)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path $doneMarker) { break }
        Start-Sleep -Seconds 2
    }
    $elapsed = ((Get-Date) - $start).TotalSeconds
    if (Test-Path $doneMarker) {
        Write-Host ("    done in {0:F1}s" -f $elapsed)
    } else {
        Write-Host ("    TIMEOUT after {0:F1}s" -f $elapsed)
    }
    Cleanup-SC; Cleanup-Autosave

    # Kernel-failure retry (Fillet only) — same policy as atomic runner.
    if ($c.mod -eq "Fillet" -and (Test-Path $resultJson)) {
        $r0 = $null
        try { $r0 = Get-Content $resultJson -Raw | ConvertFrom-Json } catch {}
        $kernelFail = ($r0 -ne $null) -and ($r0.status -in @("FAIL", "EXC")) -and
                      ($r0.msg -match "Operation failed|object is deleted|deleted")
        if ($kernelFail) {
            Write-Host "    kernel-fail -> retry with forceScale"
            "$($c.idx)`t$($c.name)`t$($c.path)`t$($c.mod)`tforceScale" | Out-File -FilePath $targetPath -Encoding utf8
            Remove-Item $doneMarker -ErrorAction SilentlyContinue
            $start2 = Get-Date
            $p2 = Start-Process -FilePath $sc -ArgumentList @("/RunScript=$scriptPath") -PassThru -NoNewWindow
            $deadline2 = $start2.AddSeconds($PerCellTimeoutSec)
            while ((Get-Date) -lt $deadline2) {
                if (Test-Path $doneMarker) { break }
                Start-Sleep -Seconds 2
            }
            Write-Host ("    retry done in {0:F1}s" -f (((Get-Date) - $start2).TotalSeconds))
            Cleanup-SC; Cleanup-Autosave
        }
    }
}

# Report just the 6 cells inline
Write-Host ""
Write-Host "===== RESULTS ====="
foreach ($c in $cells) {
    $resultJson = Join-Path $resultsDir ("{0:D2}_{1}_{2}.json" -f $c.idx, $c.name, $c.mod)
    if (Test-Path $resultJson) {
        $r = Get-Content $resultJson -Raw | ConvertFrom-Json
        $hintLine = if ($r.msg) { " | msg: $($r.msg)" } else { "" }
        Write-Host ("[{0} {1}] status={2}{3}" -f $r.name, $r.mod_kind, $r.status, $hintLine)
    } else {
        Write-Host ("[{0} {1}] NO RESULT" -f $c.name, $c.mod)
    }
}
$elapsedAll = ((Get-Date) - $startAll).TotalSeconds
Write-Host ("Total wall-clock: {0:F1}s" -f $elapsedAll)
exit 0
