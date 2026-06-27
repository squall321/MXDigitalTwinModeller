# Re-run MoveHole across ALL 12 models to confirm the axis-multi-sample oldSolid fix
# recovers samplemodel2/SampleModel1 WITHOUT regressing the others.
$ErrorActionPreference = "Stop"
$sc          = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$scriptPath  = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\mod_matrix_test.py"
$realCadDir  = "D:\MXDigitalTwinModeller\Test\RealCAD"
$ansys       = "d:\Program Files\ANSYS Inc\ANSYS Student\v252\SCDM\Library\SrModels"
$targetPath  = Join-Path $realCadDir "solo_target.txt"
$doneMarker  = Join-Path $realCadDir "solo_done.txt"
$resultsDir  = Join-Path $realCadDir "matrix_results"
function Cleanup-SC { Get-Process SpaceClaim,ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue; Start-Sleep -Seconds 1 }

$models = @(
  @{idx=1; name="SampleModel1";       path="$ansys\SampleModel1.scdoc"},
  @{idx=2; name="samplemodel2";       path="$ansys\samplemodel2.scdoc"},
  @{idx=3; name="nist_ctc_01";        path="$realCadDir\nist\NIST-PMI-STEP-Files\nist_ctc_01_asme1_ap242-e1.stp"},
  @{idx=4; name="as1-oc-214";         path="$realCadDir\stepcode\as1-oc-214.stp"},
  @{idx=5; name="boxy_with_diamsize"; path="$realCadDir\caxif\boxy_with_diamsize.stp"},
  @{idx=6; name="11752";              path="$realCadDir\pythonocc\11752.stp"},
  @{idx=7; name="face_recog_sample";  path="$realCadDir\pythonocc\face_recognition_sample_part.stp"},
  @{idx=8; name="as1_pe_203";         path="$realCadDir\pythonocc\as1_pe_203.stp"},
  @{idx=9; name="624ZZ_bearing";      path="$realCadDir\freecad\624ZZ_Ball_Bearing.stp"},
  @{idx=10;name="Ventilator";         path="$realCadDir\pythonocc\Ventilator.stp"},
  @{idx=11;name="RC_Buggy_susp";      path="$realCadDir\pythonocc\RC_Buggy_2_front_suspension.stp"},
  @{idx=12;name="F623ZZ_bearing";     path="$realCadDir\freecad\F623ZZ_Ball_Bearing.stp"}
)
Cleanup-SC
foreach ($m in $models) {
    "$($m.idx)`t$($m.name)`t$($m.path)`tMoveHole" | Out-File -FilePath $targetPath -Encoding utf8
    Remove-Item $doneMarker -ErrorAction SilentlyContinue
    $rj = Join-Path $resultsDir ("{0:D2}_{1}_MoveHole.json" -f $m.idx, $m.name)
    Remove-Item $rj -ErrorAction SilentlyContinue
    $start = Get-Date
    $p = Start-Process -FilePath $sc -ArgumentList @("/RunScript=$scriptPath") -PassThru -NoNewWindow
    $deadline = $start.AddSeconds(300)
    while ((Get-Date) -lt $deadline) { if (Test-Path $doneMarker) { break }; if ($p.HasExited) { break }; Start-Sleep -Seconds 2 }
    Cleanup-SC
    $v = "NO-JSON"
    if (Test-Path $rj) { $j = Get-Content $rj -Raw | ConvertFrom-Json; $v = "$($j.verdict) | $($j.oracle)" }
    Write-Host ("RESULT`t{0}`t{1}" -f $m.name, $v)
}
Write-Host "DONE-ALL"
