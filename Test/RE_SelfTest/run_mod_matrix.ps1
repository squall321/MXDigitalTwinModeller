# =====================================================================
# run_mod_matrix.ps1 — Phase 3 W3-1: 18-primitive × 6-model matrix.
#
# T1 smoke slice: 6 representative models (2 scdoc + 4 STEP) × 18
# primitives = 108 cells max; N_A cells exit fast (~35s each), so
# wall-clock ≈ 60-75 min. Per-cell SC isolation (kernel failures poison
# the process — gate E-1).
# =====================================================================
[CmdletBinding()]
param(
    [int]$PerCellTimeoutSec = 240,
    # Resume: keep existing per-cell JSONs and skip cells already done (recover
    # from a mid-run stall without re-running the ~148 cells that succeeded).
    [switch]$Resume
)

$ErrorActionPreference = "Stop"
$sc          = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$autoDir     = "$env:APPDATA\SpaceClaim\Autosave"
$scriptPath  = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\mod_matrix_test.py"
$logPath     = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\headless_run.log"
$realCadDir  = "D:\MXDigitalTwinModeller\Test\RealCAD"
$targetPath  = Join-Path $realCadDir "solo_target.txt"
$doneMarker  = Join-Path $realCadDir "solo_done.txt"
$resultsDir  = Join-Path $realCadDir "matrix_results"
$summaryPath = Join-Path $realCadDir "matrix_summary.md"

$ansys = "d:\Program Files\ANSYS Inc\ANSYS Student\v252\SCDM\Library\SrModels"
$models = @(
    @{name="SampleModel1";        path="$ansys\SampleModel1.scdoc"},
    @{name="samplemodel2";        path="$ansys\samplemodel2.scdoc"},
    @{name="nist_ctc_01";         path="$realCadDir\nist\NIST-PMI-STEP-Files\nist_ctc_01_asme1_ap242-e1.stp"},
    @{name="as1-oc-214";          path="$realCadDir\stepcode\as1-oc-214.stp"},
    @{name="boxy_with_diamsize";  path="$realCadDir\caxif\boxy_with_diamsize.stp"},
    @{name="11752";               path="$realCadDir\pythonocc\11752.stp"},
    # W4 corpus expansion (2026-06-15): curved + assembly + clean-feature parts.
    @{name="face_recog_sample";   path="$realCadDir\pythonocc\face_recognition_sample_part.stp"},
    @{name="as1_pe_203";          path="$realCadDir\pythonocc\as1_pe_203.stp"},
    @{name="624ZZ_bearing";       path="$realCadDir\freecad\624ZZ_Ball_Bearing.stp"},
    # Phone-metal representativeness (2026-06-18 cold-review): curved-rich real parts.
    @{name="Ventilator";          path="$realCadDir\pythonocc\Ventilator.stp"},
    @{name="RC_Buggy_susp";       path="$realCadDir\pythonocc\RC_Buggy_2_front_suspension.stp"},
    @{name="F623ZZ_bearing";      path="$realCadDir\freecad\F623ZZ_Ball_Bearing.stp"}
)
$prims = @(
    "ChangeHoleDiameter", "ChangeFilletRadius", "ChangeWallThickness",
    "ChangeBossDiameter", "ChangeBossHeight",
    "AddBoss", "AddHole", "AddSlit", "AddPocket", "AddRib", "AddChamfer",
    "AddHolePattern",
    "MoveHole", "MoveBoss", "RemoveHole",
    "RotateBoss", "RotateHole", "MirrorFeature"
)

function Cleanup-SC {
    Get-Process SpaceClaim, ansyscl -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
    if ((Get-Process SpaceClaim, ansyscl -ErrorAction SilentlyContinue | Measure-Object).Count -gt 0) {
        try { & taskkill.exe /F /T /IM SpaceClaim.exe 2>$null | Out-Null } catch {}
        try { & taskkill.exe /F /T /IM ansyscl.exe 2>$null | Out-Null } catch {}
        Start-Sleep -Seconds 2
    }
}
function Cleanup-Autosave {
    if (Test-Path $autoDir) {
        Get-ChildItem $autoDir -ErrorAction SilentlyContinue |
            Remove-Item -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "[init] clean"
Cleanup-SC; Cleanup-Autosave
if (-not $Resume) { Remove-Item $logPath -ErrorAction SilentlyContinue }
if (Test-Path $resultsDir) {
    if ($Resume) {
        $kept = (Get-ChildItem $resultsDir -Filter "*.json" -ErrorAction SilentlyContinue | Measure-Object).Count
        Write-Host ("[init] RESUME — keeping {0} existing result JSONs, skipping done cells" -f $kept)
    } else {
        Get-ChildItem $resultsDir -Filter "*.json" -ErrorAction SilentlyContinue |
            Remove-Item -Force -ErrorAction SilentlyContinue
    }
} else { New-Item -ItemType Directory -Path $resultsDir | Out-Null }

# Determinism fix (cold-review #2): warm the OS file cache + SC modeling stack with
# one throwaway launch so the FIRST real cells don't time out on a cold cache (the
# ±3 MISSING-cell non-determinism). Wait up to 240s for the warm marker.
$warmMarker = Join-Path $realCadDir "warm_done.txt"
$warmScript = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\_warm.py"
if (Test-Path $warmScript) {
    Write-Host "[init] warm-up"
    Remove-Item $warmMarker -ErrorAction SilentlyContinue
    $pw = Start-Process -FilePath $sc -ArgumentList @("/RunScript=$warmScript") -PassThru -NoNewWindow
    $wd = (Get-Date).AddSeconds(300)
    while ((Get-Date) -lt $wd) { if (Test-Path $warmMarker) { break }; if ($pw.HasExited) { break }; Start-Sleep -Seconds 3 }
    Cleanup-SC; Cleanup-Autosave
    Write-Host ("[init] warm-up done (marker={0})" -f (Test-Path $warmMarker))
}

$startAll = Get-Date
$totalCells = $models.Count * $prims.Count
$cellNum = 0

for ($i = 0; $i -lt $models.Count; $i++) {
    $idx = $i + 1
    $m = $models[$i]
    foreach ($prim in $prims) {
        $cellNum++
        $resultJson = Join-Path $resultsDir ("{0:D2}_{1}_{2}.json" -f $idx, $m.name, $prim)
        # Resume: a non-empty result JSON means this cell already completed — skip it.
        if ($Resume -and (Test-Path $resultJson) -and ((Get-Item $resultJson).Length -gt 0)) {
            Write-Host ("[{0}/{1}] {2} | {3}  -- SKIP (already done)" -f $cellNum, $totalCells, $m.name, $prim)
            continue
        }
        Write-Host ("[{0}/{1}] {2} | {3}" -f $cellNum, $totalCells, $m.name, $prim)
        "$idx`t$($m.name)`t$($m.path)`t$prim" | Out-File -FilePath $targetPath -Encoding utf8
        Remove-Item $doneMarker -ErrorAction SilentlyContinue
        Remove-Item $resultJson -ErrorAction SilentlyContinue

        $start = Get-Date
        $p = Start-Process -FilePath $sc -ArgumentList @("/RunScript=$scriptPath") -PassThru -NoNewWindow
        $deadline = $start.AddSeconds($PerCellTimeoutSec)
        while ((Get-Date) -lt $deadline) {
            if (Test-Path $doneMarker) { break }
            if ($p.HasExited) { Write-Host "    SC exited early (crash)"; break }
            Start-Sleep -Seconds 2
        }
        Write-Host ("    done in {0:F1}s" -f (((Get-Date) - $start).TotalSeconds))
        Cleanup-SC; Cleanup-Autosave

        # Fillet kernel-fail retry with forceScale (proven policy)
        if ($prim -eq "ChangeFilletRadius" -and (Test-Path $resultJson)) {
            $r0 = $null
            try { $r0 = Get-Content $resultJson -Raw | ConvertFrom-Json } catch {}
            $kernelFail = ($r0 -ne $null) -and (@("FAIL","EXC") -contains $r0.status) -and
                          ($r0.msg -match "Operation failed|deleted")
            if ($kernelFail) {
                Write-Host "    kernel-fail -> retry forceScale"
                "$idx`t$($m.name)`t$($m.path)`t$prim`tforceScale" | Out-File -FilePath $targetPath -Encoding utf8
                Remove-Item $doneMarker -ErrorAction SilentlyContinue
                $start2 = Get-Date
                $p2 = Start-Process -FilePath $sc -ArgumentList @("/RunScript=$scriptPath") -PassThru -NoNewWindow
                $deadline2 = $start2.AddSeconds($PerCellTimeoutSec)
                while ((Get-Date) -lt $deadline2) {
                    if (Test-Path $doneMarker) { break }
                    if ($p2.HasExited) { break }
                    Start-Sleep -Seconds 2
                }
                Cleanup-SC; Cleanup-Autosave
            }
        }

        # W4-5: ChangeBoss{Diameter,Height} OffsetFaces fail/no-op → retry on a FRESH
        # body with useBoolean (coaxial-cylinder Boolean; OffsetFaces poison means no
        # in-process recovery, so the retry must be a clean re-launch).
        if (($prim -eq "ChangeBossDiameter" -or $prim -eq "ChangeBossHeight") -and (Test-Path $resultJson)) {
            $rb = $null
            try { $rb = Get-Content $resultJson -Raw | ConvertFrom-Json } catch {}
            if (($rb -ne $null) -and (@("FAILED","INCONCLUSIVE") -contains $rb.verdict)) {
                Write-Host "    boss OffsetFaces miss -> retry useBoolean"
                "$idx`t$($m.name)`t$($m.path)`t$prim`tuseBoolean" | Out-File -FilePath $targetPath -Encoding utf8
                Remove-Item $doneMarker -ErrorAction SilentlyContinue
                $start3 = Get-Date
                $p3 = Start-Process -FilePath $sc -ArgumentList @("/RunScript=$scriptPath") -PassThru -NoNewWindow
                $deadline3 = $start3.AddSeconds($PerCellTimeoutSec)
                while ((Get-Date) -lt $deadline3) {
                    if (Test-Path $doneMarker) { break }
                    if ($p3.HasExited) { break }
                    Start-Sleep -Seconds 2
                }
                Cleanup-SC; Cleanup-Autosave
            }
        }
    }
}

# ---- aggregate ----
Write-Host ""; Write-Host "===== AGGREGATING ====="
$rows = @()
Get-ChildItem $resultsDir -Filter "*.json" | ForEach-Object {
    try { $rows += (Get-Content $_.FullName -Raw | ConvertFrom-Json) } catch {
        Write-Host "    parse fail: $($_.Name)"
    }
}

$lines = @()
$lines += "# Phase 3 — 18-Primitive Matrix (T1 smoke slice)"
$lines += ""
$lines += "Generated: $((Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))"
$lines += "Wall-clock: $([math]::Round(((Get-Date)-$startAll).TotalMinutes,1)) min | Cells: $($rows.Count)/$totalCells"
$lines += ""
$lines += "## Per-primitive verdicts"
$lines += ""
$lines += "| Primitive | VERIFIED | INCONCLUSIVE | FAILED | N_A |"
$lines += "|---|---|---|---|---|"
foreach ($prim in $prims) {
    $sub = $rows | Where-Object { $_.prim -eq $prim }
    $v = ($sub | Where-Object { $_.verdict -eq "VERIFIED" }).Count
    $ic = ($sub | Where-Object { $_.verdict -eq "INCONCLUSIVE" }).Count
    $f = ($sub | Where-Object { $_.verdict -eq "FAILED" }).Count
    $na = ($sub | Where-Object { $_.verdict -eq "N_A" }).Count
    $lines += "| $prim | $v | $ic | $f | $na |"
}
$lines += ""
$lines += "## Per-cell detail"
$lines += ""
$lines += "| Model | Primitive | Verdict | Action | Oracle |"
$lines += "|---|---|---|---|---|"
foreach ($r in ($rows | Sort-Object idx, prim)) {
    $lines += "| $($r.name) | $($r.prim) | $($r.verdict) | $($r.action) | $($r.oracle) |"
}
$lines | Out-File -FilePath $summaryPath -Encoding utf8
Write-Host "Summary: $summaryPath"

$tv = ($rows | Where-Object { $_.verdict -eq "VERIFIED" }).Count
$ti = ($rows | Where-Object { $_.verdict -eq "INCONCLUSIVE" }).Count
$tf = ($rows | Where-Object { $_.verdict -eq "FAILED" }).Count
$tn = ($rows | Where-Object { $_.verdict -eq "N_A" }).Count
Write-Host "TOTAL: VERIFIED=$tv INCONCLUSIVE=$ti FAILED=$tf N_A=$tn"
exit 0
