$ErrorActionPreference = "Stop"
$sc = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$scriptPath = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\mod_matrix_test.py"
$realCadDir = "D:\MXDigitalTwinModeller\Test\RealCAD"
$ansys = "d:\Program Files\ANSYS Inc\ANSYS Student\v252\SCDM\Library\SrModels"
function Cleanup { Get-Process SpaceClaim,ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue; Start-Sleep -Seconds 1 }
$cells = @(
  @{idx=4; name="as1-oc-214";   path="$realCadDir\stepcode\as1-oc-214.stp"; prim="MoveHole"},
  @{idx=2; name="samplemodel2"; path="$ansys\samplemodel2.scdoc";           prim="MirrorFeature"},
  @{idx=4; name="as1-oc-214";   path="$realCadDir\stepcode\as1-oc-214.stp"; prim="AddSlit"}
)
Cleanup
foreach ($c in $cells) {
  "$($c.idx)`t$($c.name)`t$($c.path)`t$($c.prim)" | Out-File -FilePath (Join-Path $realCadDir "solo_target.txt") -Encoding utf8
  $done = Join-Path $realCadDir "solo_done.txt"; Remove-Item $done -ErrorAction SilentlyContinue
  $rj = Join-Path $realCadDir ("matrix_results\{0:D2}_{1}_{2}.json" -f $c.idx,$c.name,$c.prim)
  Remove-Item $rj -ErrorAction SilentlyContinue
  $p = Start-Process -FilePath $sc -ArgumentList "/RunScript=$scriptPath" -PassThru -NoNewWindow
  $deadline = (Get-Date).AddSeconds(240)
  while ((Get-Date) -lt $deadline) { if (Test-Path $done) { break }; if ($p.HasExited) { break }; Start-Sleep -Seconds 2 }
  Cleanup
  if (Test-Path $rj) { $j = Get-Content $rj -Raw | ConvertFrom-Json; Write-Host ("RESULT`t{0}/{1}`t{2}`t{3}" -f $c.name,$c.prim,$j.verdict,$j.oracle) } else { Write-Host ("RESULT`t{0}/{1}`tNO-JSON" -f $c.name,$c.prim) }
}
Write-Host "DONE-ALL"
