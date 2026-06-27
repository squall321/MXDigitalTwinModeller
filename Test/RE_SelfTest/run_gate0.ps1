$ErrorActionPreference = "Stop"
$sc = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$probe = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\gate0_marshal_probe.py"
$out = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\gate0_marshal_result.txt"
Get-Process SpaceClaim,ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
Remove-Item $out -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $sc -ArgumentList "/RunScript=$probe" -PassThru -NoNewWindow
$deadline = (Get-Date).AddSeconds(150)
while ((Get-Date) -lt $deadline) { if (Test-Path $out) { Start-Sleep -Seconds 1; break }; if ($p.HasExited) { Start-Sleep -Seconds 2; break }; Start-Sleep -Seconds 3 }
Get-Process SpaceClaim,ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
if (Test-Path $out) { Write-Host "=== GATE0 RESULT ==="; Get-Content $out -Raw } else { Write-Host "NO RESULT (probe deadlocked or crashed)" }
