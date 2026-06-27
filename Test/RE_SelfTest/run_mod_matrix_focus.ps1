# =====================================================================
# run_mod_matrix_focus.ps1 — run ONE primitive across all 6 matrix models
# for fast validation of a single fix (no full 108-cell rerun).
# Writes JSON into matrix_results (overwrites just those cells) so the
# next full aggregate stays consistent. Reports inline.
# =====================================================================
[CmdletBinding()]
param(
    [string]$Primitive = "MoveHole",
    [int]$PerCellTimeoutSec = 240
)

$ErrorActionPreference = "Stop"
$sc          = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$autoDir     = "$env:APPDATA\SpaceClaim\Autosave"
$scriptPath  = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\mod_matrix_test.py"
$realCadDir  = "D:\MXDigitalTwinModeller\Test\RealCAD"
$targetPath  = Join-Path $realCadDir "solo_target.txt"
$doneMarker  = Join-Path $realCadDir "solo_done.txt"
$resultsDir  = Join-Path $realCadDir "matrix_results"

$ansys = "d:\Program Files\ANSYS Inc\ANSYS Student\v252\SCDM\Library\SrModels"
$models = @(
    @{idx=1; name="SampleModel1";        path="$ansys\SampleModel1.scdoc"},
    @{idx=2; name="samplemodel2";        path="$ansys\samplemodel2.scdoc"},
    @{idx=3; name="nist_ctc_01";         path="$realCadDir\nist\NIST-PMI-STEP-Files\nist_ctc_01_asme1_ap242-e1.stp"},
    @{idx=4; name="as1-oc-214";          path="$realCadDir\stepcode\as1-oc-214.stp"},
    @{idx=5; name="boxy_with_diamsize";  path="$realCadDir\caxif\boxy_with_diamsize.stp"},
    @{idx=6; name="11752";               path="$realCadDir\pythonocc\11752.stp"},
    @{idx=7; name="face_recog_sample";   path="$realCadDir\pythonocc\face_recognition_sample_part.stp"},
    @{idx=8; name="as1_pe_203";          path="$realCadDir\pythonocc\as1_pe_203.stp"},
    @{idx=9; name="624ZZ_bearing";       path="$realCadDir\freecad\624ZZ_Ball_Bearing.stp"},
    @{idx=10; name="Ventilator";         path="$realCadDir\pythonocc\Ventilator.stp"},
    @{idx=11; name="RC_Buggy_susp";      path="$realCadDir\pythonocc\RC_Buggy_2_front_suspension.stp"},
    @{idx=12; name="F623ZZ_bearing";     path="$realCadDir\freecad\F623ZZ_Ball_Bearing.stp"}
)

function Cleanup-SC {
    Get-Process SpaceClaim, ansyscl -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
}
function Cleanup-Autosave {
    if (Test-Path $autoDir) {
        Get-ChildItem $autoDir -ErrorAction SilentlyContinue |
            Remove-Item -Force -ErrorAction SilentlyContinue
    }
}

Cleanup-SC; Cleanup-Autosave
if (!(Test-Path $resultsDir)) { New-Item -ItemType Directory -Path $resultsDir | Out-Null }

foreach ($m in $models) {
    Write-Host ("[{0}] {1} | {2}" -f $m.idx, $m.name, $Primitive)
    "$($m.idx)`t$($m.name)`t$($m.path)`t$Primitive" | Out-File -FilePath $targetPath -Encoding utf8
    Remove-Item $doneMarker -ErrorAction SilentlyContinue
    $resultJson = Join-Path $resultsDir ("{0:D2}_{1}_{2}.json" -f $m.idx, $m.name, $Primitive)
    Remove-Item $resultJson -ErrorAction SilentlyContinue

    $start = Get-Date
    $p = Start-Process -FilePath $sc -ArgumentList @("/RunScript=$scriptPath") -PassThru -NoNewWindow
    $deadline = $start.AddSeconds($PerCellTimeoutSec)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path $doneMarker) { break }
        if ($p.HasExited) { break }
        Start-Sleep -Seconds 2
    }
    Write-Host ("    done in {0:F1}s" -f (((Get-Date) - $start).TotalSeconds))
    Cleanup-SC; Cleanup-Autosave

    # W4-5 boss Boolean retry (mirror run_mod_matrix.ps1)
    if (($Primitive -eq "ChangeBossDiameter" -or $Primitive -eq "ChangeBossHeight") -and (Test-Path $resultJson)) {
        $rb = $null
        try { $rb = Get-Content $resultJson -Raw | ConvertFrom-Json } catch {}
        if (($rb -ne $null) -and (@("FAILED","INCONCLUSIVE") -contains $rb.verdict)) {
            Write-Host "    boss miss -> retry useBoolean"
            "$($m.idx)`t$($m.name)`t$($m.path)`t$Primitive`tuseBoolean" | Out-File -FilePath $targetPath -Encoding utf8
            Remove-Item $doneMarker -ErrorAction SilentlyContinue
            $s3 = Get-Date; $p3 = Start-Process -FilePath $sc -ArgumentList @("/RunScript=$scriptPath") -PassThru -NoNewWindow
            $dl3 = $s3.AddSeconds($PerCellTimeoutSec)
            while ((Get-Date) -lt $dl3) { if (Test-Path $doneMarker) { break }; if ($p3.HasExited) { break }; Start-Sleep -Seconds 2 }
            Cleanup-SC; Cleanup-Autosave
        }
    }
}

Write-Host ""; Write-Host "===== RESULTS ($Primitive) ====="
foreach ($m in $models) {
    $resultJson = Join-Path $resultsDir ("{0:D2}_{1}_{2}.json" -f $m.idx, $m.name, $Primitive)
    if (Test-Path $resultJson) {
        $r = Get-Content $resultJson -Raw | ConvertFrom-Json
        Write-Host ("[{0,-18}] {1,-12} | {2}" -f $r.name, $r.verdict, $r.oracle)
    } else {
        Write-Host ("[{0,-18}] NO RESULT" -f $m.name)
    }
}
exit 0
