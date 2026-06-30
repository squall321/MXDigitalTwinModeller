$ErrorActionPreference = "Stop"
. "$PSScriptRoot\_sc_kill.ps1"
$sc = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$probe = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\g11_onface_tools.py"
$out = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\g11_onface_result.txt"
$mark = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\g11_mark.txt"
Stop-SpaceClaimTree | Out-Null
Remove-Item $out,$mark -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $sc -ArgumentList "/RunScript=$probe" -PassThru -NoNewWindow
$deadline = (Get-Date).AddSeconds(300)
while ((Get-Date) -lt $deadline) { if (Test-Path $out) { Start-Sleep -Seconds 1; break }; if ($p.HasExited) { Start-Sleep 2; break }; Start-Sleep -Seconds 3 }
Stop-SpaceClaimTree | Out-Null
if (Test-Path $out) { Get-Content $out -Raw } else { Write-Host "NO RESULT"; if (Test-Path $mark) { "--- MARK ---"; Get-Content $mark -Raw } }
