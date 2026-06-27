$ErrorActionPreference = "Stop"
$sc = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$probe = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\probe_w52_wallguard.py"
$out = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\probe_w52_result.json"
Get-Process SpaceClaim,ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
Remove-Item $out -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $sc -ArgumentList @("/RunScript=$probe") -PassThru -NoNewWindow
$deadline = (Get-Date).AddSeconds(200)
while ((Get-Date) -lt $deadline) { if (Test-Path $out) { Start-Sleep -Seconds 1; break }; if ($p.HasExited) { break }; Start-Sleep -Seconds 2 }
Get-Process SpaceClaim,ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
if (Test-Path $out) { Write-Host "RESULT-START"; Get-Content $out -Raw; Write-Host "RESULT-END" } else { Write-Host "NO RESULT" }
