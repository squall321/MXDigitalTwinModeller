$ErrorActionPreference = "Stop"
$sc = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$scriptPath = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\mod_matrix_test.py"
$realCadDir = "D:\MXDigitalTwinModeller\Test\RealCAD"
Get-Process SpaceClaim,ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
"4`tas1-oc-214`t$realCadDir\stepcode\as1-oc-214.stp`tMoveHole" | Out-File -FilePath (Join-Path $realCadDir "solo_target.txt") -Encoding utf8
$done = Join-Path $realCadDir "solo_done.txt"
Remove-Item $done -ErrorAction SilentlyContinue
$rj = Join-Path $realCadDir "matrix_results\04_as1-oc-214_MoveHole.json"
Remove-Item $rj -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $sc -ArgumentList "/RunScript=$scriptPath" -PassThru -NoNewWindow
$deadline = (Get-Date).AddSeconds(240)
while ((Get-Date) -lt $deadline) { if (Test-Path $done) { break }; if ($p.HasExited) { break }; Start-Sleep -Seconds 2 }
Get-Process SpaceClaim,ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
if (Test-Path $rj) { $j = Get-Content $rj -Raw | ConvertFrom-Json; Write-Host "VERDICT: $($j.verdict)"; Write-Host "ORACLE: $($j.oracle)"; Write-Host "MSG: $($j.msg)"; Write-Host "ACTION: $($j.action)" } else { Write-Host "NO JSON" }
