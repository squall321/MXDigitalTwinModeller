$ErrorActionPreference = "Stop"
$sc = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$probe = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\probe_w52_wallguard.py"
$out = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\probe_w52_result.json"
$mark = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\probe_w52_mark.txt"
Get-Process SpaceClaim,ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
Remove-Item $out,$mark -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $sc -ArgumentList @("/RunScript=$probe") -PassThru -NoNewWindow
$deadline = (Get-Date).AddSeconds(200)
while ((Get-Date) -lt $deadline) { if (Test-Path $out) { Start-Sleep -Seconds 1; break }; if ($p.HasExited) { Start-Sleep -Seconds 2; break }; Start-Sleep -Seconds 3 }
Get-Process SpaceClaim,ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Write-Host "=== MARK ==="
if (Test-Path $mark) { Get-Content $mark } else { Write-Host "(no mark - died before imports)" }
Write-Host "=== JSON ==="
if (Test-Path $out) { Get-Content $out -Raw } else { Write-Host "(no json)" }
