# =====================================================================
# run_mod_corpus_solo.ps1
#
# Process-isolation runner for mod_solo_test.py: launches SC once per
# model so a per-model OffsetFaces failure cannot corrupt SC's state
# for subsequent files.
#
# For each of 20 models:
#   1. Write spec to solo_target.txt   (idx\tname\tpath)
#   2. Launch SC with /RunScript=mod_solo_test.py
#   3. Poll solo_done.txt for completion
#   4. Kill SC + ansyscl
#   5. Read solo_results/<idx>_<name>.json into the rollup
# Final: write solo_corpus_summary.md from aggregated rows.
# =====================================================================
[CmdletBinding()]
param(
    [int]$PerFileTimeoutSec = 180
)

$ErrorActionPreference = "Stop"
$sc            = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$autoDir       = "$env:APPDATA\SpaceClaim\Autosave"
$scriptPath    = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\mod_solo_test.py"
$logPath       = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\headless_run.log"
$realCadDir    = "D:\MXDigitalTwinModeller\Test\RealCAD"
$targetPath    = Join-Path $realCadDir "solo_target.txt"
$doneMarker    = Join-Path $realCadDir "solo_done.txt"
$resultsDir    = Join-Path $realCadDir "solo_results"
$summaryPath   = Join-Path $realCadDir "solo_corpus_summary.md"

# Corpus matches mod_corpus_test.py (same 20 paths).
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

Write-Host "[init] Pre-clean SC + results dir"
Cleanup-SC
Cleanup-Autosave
Remove-Item $logPath -ErrorAction SilentlyContinue
if (Test-Path $resultsDir) {
    Get-ChildItem $resultsDir -Filter "*.json" -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue
} else {
    New-Item -ItemType Directory -Path $resultsDir | Out-Null
}

$startAll = Get-Date
$total = $corpus.Count

for ($i = 0; $i -lt $total; $i++) {
    $idx = $i + 1
    $m = $corpus[$i]
    Write-Host ("[{0}/{1}] {2}" -f $idx, $total, $m.name)

    # Write target spec (idx\tname\tpath)
    "$idx`t$($m.name)`t$($m.path)" | Out-File -FilePath $targetPath -Encoding utf8

    # Clear done marker + previous result for this idx
    Remove-Item $doneMarker -ErrorAction SilentlyContinue
    $resultJson = Join-Path $resultsDir ("{0:D2}_{1}.json" -f $idx, $m.name)
    Remove-Item $resultJson -ErrorAction SilentlyContinue

    # Launch SC fresh
    $start = Get-Date
    $p = Start-Process -FilePath $sc -ArgumentList @("/RunScript=$scriptPath") -PassThru -NoNewWindow
    $deadline = $start.AddSeconds($PerFileTimeoutSec)

    while ((Get-Date) -lt $deadline) {
        if (Test-Path $doneMarker) { break }
        Start-Sleep -Seconds 2
    }
    $elapsed = ((Get-Date) - $start).TotalSeconds

    if (Test-Path $doneMarker) {
        Write-Host ("    done in {0:F1}s" -f $elapsed)
    } else {
        Write-Host ("    TIMEOUT after {0:F1}s — killing" -f $elapsed)
    }

    Cleanup-SC
    Cleanup-Autosave
}

# Aggregate
Write-Host ""
Write-Host "===== AGGREGATING ====="
$rows = @()
for ($i = 0; $i -lt $total; $i++) {
    $idx = $i + 1
    $m = $corpus[$i]
    $resultJson = Join-Path $resultsDir ("{0:D2}_{1}.json" -f $idx, $m.name)
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

$holeOk    = ($rows | Where-Object { $_.hole.status   -eq "OK"   }).Count
$holeFail  = ($rows | Where-Object { $_.hole.status   -in @("FAIL","EXC") }).Count
$filOk     = ($rows | Where-Object { $_.fillet.status -eq "OK"   }).Count
$filFail   = ($rows | Where-Object { $_.fillet.status -in @("FAIL","EXC") }).Count
$wallOk    = ($rows | Where-Object { $_.wall.status   -eq "OK"   }).Count
$wallFail  = ($rows | Where-Object { $_.wall.status   -in @("FAIL","EXC") }).Count

$lines = @()
$lines += "# Real-CAD Modification Corpus (process-isolated)"
$lines += ""
$lines += "Generated: $((Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))"
$lines += "Total wall-clock: $([math]::Round(((Get-Date) - $startAll).TotalSeconds, 1))s"
$lines += "Rows collected: $($rows.Count) / $total"
$lines += ""
$lines += "## Aggregate"
$lines += "- Hole:   OK=$holeOk  FAIL=$holeFail"
$lines += "- Fillet: OK=$filOk  FAIL=$filFail"
$lines += "- Wall:   OK=$wallOk  FAIL=$wallFail"
$lines += ""
$lines += "| # | Model | Load | Extract | Hole | Fillet | Wall |"
$lines += "|---|---|---|---|---|---|---|"
foreach ($r in ($rows | Sort-Object idx)) {
    $lines += "| $($r.idx) | $($r.name) | $($r.load) | $($r.extract) | $($r.hole.status) | $($r.fillet.status) | $($r.wall.status) |"
}
$lines += ""
$lines += "## Per-model details"
$lines += ""
foreach ($r in ($rows | Sort-Object idx)) {
    $lines += "### $($r.idx). $($r.name)"
    $lines += "- load: $($r.load)"
    $lines += "- extract: $($r.extract)"
    foreach ($k in @("hole","fillet","wall")) {
        $d = $r.$k
        $line = "- $($k): $($d.status) — $($d.action)"
        if ($d.before -ne $null -and $d.after -ne $null) {
            $line += " — before=$([math]::Round($d.before,3)) after=$([math]::Round($d.after,3))"
        }
        $lines += $line
        if ($d.msg) { $lines += "    msg: $($d.msg)" }
    }
    $lines += ""
}

$lines | Out-File -FilePath $summaryPath -Encoding utf8
Write-Host "Summary written: $summaryPath"
Write-Host ""
Write-Host "Aggregate: Hole OK=$holeOk/$total | Fillet OK=$filOk/$total | Wall OK=$wallOk/$total"
exit 0
