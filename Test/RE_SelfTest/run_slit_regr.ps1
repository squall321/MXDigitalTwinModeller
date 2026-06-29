$sc = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$scriptPath = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\mod_matrix_test.py"
$realCadDir = "D:\MXDigitalTwinModeller\Test\RealCAD"
$ansys = "d:\Program Files\ANSYS Inc\ANSYS Student\v252\SCDM\Library\SrModels"
function Cleanup { Get-Process SpaceClaim,ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue; Start-Sleep 1 }
$models = @(
  @{idx=1;name="SampleModel1";path="$ansys\SampleModel1.scdoc"},
  @{idx=2;name="samplemodel2";path="$ansys\samplemodel2.scdoc"},
  @{idx=3;name="nist_ctc_01";path="$realCadDir\nist\NIST-PMI-STEP-Files\nist_ctc_01_asme1_ap242-e1.stp"},
  @{idx=5;name="boxy_with_diamsize";path="$realCadDir\caxif\boxy_with_diamsize.stp"},
  @{idx=8;name="as1_pe_203";path="$realCadDir\pythonocc\as1_pe_203.stp"}
)
Cleanup
foreach ($m in $models) {
  "$($m.idx)`t$($m.name)`t$($m.path)`tAddSlit" | Out-File -FilePath (Join-Path $realCadDir "solo_target.txt") -Encoding utf8
  $done = Join-Path $realCadDir "solo_done.txt"; Remove-Item $done -ErrorAction SilentlyContinue
  $rj = Join-Path $realCadDir ("matrix_results\{0:D2}_{1}_AddSlit.json" -f $m.idx,$m.name)
  Remove-Item $rj -ErrorAction SilentlyContinue
  $p = Start-Process -FilePath $sc -ArgumentList "/RunScript=$scriptPath" -PassThru -NoNewWindow
  $dl=(Get-Date).AddSeconds(240); while((Get-Date) -lt $dl){if(Test-Path $done){break};if($p.HasExited){break};Start-Sleep 2}
  Cleanup
  if(Test-Path $rj){$j=Get-Content $rj -Raw|ConvertFrom-Json;Write-Host("RESULT`t{0}`t{1}`t{2}" -f $m.name,$j.verdict,$j.oracle)}else{Write-Host("RESULT`t{0}`tNO-JSON" -f $m.name)}
}
Write-Host "DONE"
