# =====================================================================
# run_mod_corpus_atomic.ps1
#
# Atomic per-(file, mod-type) runner: launches SC fresh for EACH cell
# of the 20-file × 3-mod-type matrix. Total: up to 60 SC launches.
# Removes failure cascades within a model — gives the true success
# rate per (file, mod-type) pair.
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
$summaryPath   = Join-Path $realCadDir "atomic_corpus_summary.md"

$ansys = "d:\Program Files\ANSYS Inc\ANSYS Student\v252\SCDM\Library\SrModels"
$corpus = @(
    @{name="SampleModel1";                 path="$ansys\SampleModel1.scdoc"},
    @{name="SampleModel4";                 path="$ansys\SampleModel4.scdoc"},
    @{name="samplemodel5";                 path="$ansys\samplemodel5.scdoc"},
    @{name="samplemodel2";                 path="$ansys\samplemodel2.scdoc"},
    @{name="nist_ctc_01";                  path="$realCadDir\nist\NIST-PMI-STEP-Files\nist_ctc_01_asme1_ap242-e1.stp"},
    @{name="nist_ftc_07";                  path="$realCadDir\nist\NIST-PMI-STEP-Files\nist_ftc_07_asme1_ap242-e2.stp"},
    @{name="nist_stc_06";                  path="$realCadDir\nist\NIST-PMI-STEP-Files\nist_stc_06_asme1_ap242-e3.stp"},
    @{name="as1-ac-214";                   path="$realCadDir\caxif\as1-ac-214.stp"},
    @{name="as1-md-214";                   path="$realCadDir\caxif\as1-md-214.stp"},
    @{name="as1-ug-214";                   path="$realCadDir\caxif\as1-ug-214.stp"},
    @{name="boxy_with_diamsize";           path="$realCadDir\caxif\boxy_with_diamsize.stp"},
    @{name="screw";                        path="$realCadDir\occt\screw.step"},
    @{name="linkrods";                     path="$realCadDir\occt\linkrods.step"},
    @{name="624ZZ_Ball_Bearing";           path="$realCadDir\freecad\624ZZ_Ball_Bearing.stp"},
    @{name="ISO4032_Hex_Nut_M6";           path="$realCadDir\freecad\ISO4032_Hex_Nut_M6.stp"},
    @{name="as1-oc-214";                   path="$realCadDir\stepcode\as1-oc-214.stp"},
    @{name="splinecage";                   path="$realCadDir\pythonocc\splinecage.stp"},
    @{name="face_recognition_sample_part"; path="$realCadDir\pythonocc\face_recognition_sample_part.stp"},
    @{name="Ventilator";                   path="$realCadDir\pythonocc\Ventilator.stp"},
    @{name="11752";                        path="$realCadDir\pythonocc\11752.stp"}
)
$mods = @("Hole", "Fillet", "Wall")

function Cleanup-SC {
    # Stop-Process for the common case. taskkill fallback for stuck-process
    # corner case — wrap in try/catch because taskkill returns a "failed to
    # run" native exception when there's nothing to kill, which trips the
    # script-wide ErrorActionPreference=Stop and aborts the whole atomic run.
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

Write-Host "[init] Pre-clean SC + results dir"
Cleanup-SC; Cleanup-Autosave
Remove-Item $logPath -ErrorAction SilentlyContinue
if (Test-Path $resultsDir) {
    Get-ChildItem $resultsDir -Filter "*.json" -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue
} else {
    New-Item -ItemType Directory -Path $resultsDir | Out-Null
}

$startAll = Get-Date
$totalCells = $corpus.Count * $mods.Count
$cellNum = 0

for ($i = 0; $i -lt $corpus.Count; $i++) {
    $idx = $i + 1
    $m = $corpus[$i]
    foreach ($mod in $mods) {
        $cellNum++
        Write-Host ("[{0}/{1}] {2} | {3}" -f $cellNum, $totalCells, $m.name, $mod)

        "$idx`t$($m.name)`t$($m.path)`t$mod" | Out-File -FilePath $targetPath -Encoding utf8
        Remove-Item $doneMarker -ErrorAction SilentlyContinue
        $resultJson = Join-Path $resultsDir ("{0:D2}_{1}_{2}.json" -f $idx, $m.name, $mod)
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

        # Orchestrator-level kernel-failure retry (Fillet only): a kernel
        # rejection poisons the SC process, so the ScaleNormalized retry MUST
        # be a fresh launch — decided here, not in-process. One retry per cell.
        if ($mod -eq "Fillet" -and (Test-Path $resultJson)) {
            $r0 = $null
            try { $r0 = Get-Content $resultJson -Raw | ConvertFrom-Json } catch {}
            $kernelFail = ($r0 -ne $null) -and ($r0.status -in @("FAIL", "EXC")) -and
                          ($r0.msg -match "Operation failed|object is deleted|deleted")
            if ($kernelFail) {
                Write-Host "    kernel-fail -> retry with forceScale"
                "$idx`t$($m.name)`t$($m.path)`t$mod`tforceScale" | Out-File -FilePath $targetPath -Encoding utf8
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
                # Keep the BETTER result: if retry also failed, restore note.
                try {
                    $r1 = Get-Content $resultJson -Raw | ConvertFrom-Json
                    if ($r1.status -ne "OK") {
                        $r1.msg = "RETRY(forceScale) also failed: " + $r1.msg
                        $r1 | ConvertTo-Json | Out-File -FilePath $resultJson -Encoding utf8
                    }
                } catch {}
            }
        }
    }
}

# Aggregate
Write-Host ""
Write-Host "===== AGGREGATING ====="
$rows = @()
for ($i = 0; $i -lt $corpus.Count; $i++) {
    $idx = $i + 1
    $m = $corpus[$i]
    foreach ($mod in $mods) {
        $resultJson = Join-Path $resultsDir ("{0:D2}_{1}_{2}.json" -f $idx, $m.name, $mod)
        if (Test-Path $resultJson) {
            try {
                $row = Get-Content $resultJson -Raw | ConvertFrom-Json
                $rows += $row
            } catch {
                Write-Host "    parse fail: $resultJson"
            }
        } else {
            Write-Host "    missing: $resultJson"
        }
    }
}

function CountByMod($rows, $mod, $status) {
    return ($rows | Where-Object { $_.mod_kind -eq $mod -and $_.status -eq $status }).Count
}
$holeOk   = CountByMod $rows "Hole"   "OK"
$holeFail = (CountByMod $rows "Hole" "FAIL") + (CountByMod $rows "Hole" "EXC")
$filOk    = CountByMod $rows "Fillet" "OK"
$filFail  = (CountByMod $rows "Fillet" "FAIL") + (CountByMod $rows "Fillet" "EXC")
$wallOk   = CountByMod $rows "Wall"   "OK"
$wallFail = (CountByMod $rows "Wall" "FAIL") + (CountByMod $rows "Wall" "EXC")

$lines = @()
$lines += "# Real-CAD Modification Corpus — ATOMIC (per-cell isolation)"
$lines += ""
$lines += "Generated: $((Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))"
$lines += "Total wall-clock: $([math]::Round(((Get-Date) - $startAll).TotalSeconds, 1))s"
$lines += "Cells collected: $($rows.Count) / $totalCells"
$lines += ""
$lines += "Each (model, mod-type) cell ran in its own SC launch — no cross-mod"
$lines += "state poisoning. Compare to solo run (per-model) and original run"
$lines += "(per-batch) to see how much the failure cascade was hiding."
$lines += ""
$lines += "## Aggregate"
$lines += "- Hole:   OK=$holeOk  FAIL=$holeFail"
$lines += "- Fillet: OK=$filOk  FAIL=$filFail"
$lines += "- Wall:   OK=$wallOk  FAIL=$wallFail"
$lines += ""
$lines += "## Per-cell"
$lines += ""
$lines += "| # | Model | Hole | Fillet | Wall |"
$lines += "|---|---|---|---|---|"
for ($i = 0; $i -lt $corpus.Count; $i++) {
    $idx = $i + 1
    $m = $corpus[$i]
    $byMod = @{}
    foreach ($mod in $mods) {
        $r = $rows | Where-Object { $_.idx -eq $idx -and $_.mod_kind -eq $mod } | Select-Object -First 1
        if ($r -eq $null) { $byMod[$mod] = "?" }
        else { $byMod[$mod] = $r.status }
    }
    $lines += "| $idx | $($m.name) | $($byMod['Hole']) | $($byMod['Fillet']) | $($byMod['Wall']) |"
}
$lines += ""
$lines += "## Per-cell details"
$lines += ""
foreach ($r in ($rows | Sort-Object idx, mod_kind)) {
    $lines += "### $($r.idx). $($r.name) — $($r.mod_kind)"
    $lines += "- load: $($r.load)"
    $lines += "- extract: $($r.extract)"
    $lines += "- status: $($r.status)"
    $lines += "- action: $($r.action)"
    if ($r.before -ne $null -and $r.after -ne $null) {
        $lines += "- before=$([math]::Round($r.before,3)) after=$([math]::Round($r.after,3))"
    } elseif ($r.before -ne $null) {
        $lines += "- before=$([math]::Round($r.before,3))"
    }
    if ($r.msg) { $lines += "- msg: $($r.msg)" }
    $lines += ""
}

$lines | Out-File -FilePath $summaryPath -Encoding utf8
Write-Host "Summary written: $summaryPath"
Write-Host ""
Write-Host "Aggregate: Hole OK=$holeOk | Fillet OK=$filOk | Wall OK=$wallOk"
exit 0
