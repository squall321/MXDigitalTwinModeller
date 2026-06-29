$ErrorActionPreference = "Stop"
$sc = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$probe = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\g7_flank_port.py"
$out = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\g7_flank_result.txt"
$mark = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\g7_mark.txt"
Get-Process SpaceClaim,ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
Remove-Item $out,$mark -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $sc -ArgumentList "/RunScript=$probe" -PassThru -NoNewWindow
$deadline = (Get-Date).AddSeconds(300)
while ((Get-Date) -lt $deadline) { if (Test-Path $out) { Start-Sleep -Seconds 1; break }; if ($p.HasExited) { Start-Sleep 2; break }; Start-Sleep -Seconds 3 }
Get-Process SpaceClaim,ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
if (Test-Path $out) { Get-Content $out -Raw } else { Write-Host "NO RESULT"; if (Test-Path $mark) { "--- MARK ---"; Get-Content $mark -Raw } }
