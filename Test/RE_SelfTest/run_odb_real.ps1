# Validate the ODB++ importer against REAL designs (rigidflex sample + P3_EUR board).
$ErrorActionPreference = "Stop"
$sc   = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$gate = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\probe_odb_real.py"
$dir  = "D:\MXDigitalTwinModeller\Test\RE_SelfTest"
$done = Join-Path $dir "odb_real_done.txt"
. (Join-Path $dir "_lic_watchdog.ps1")
Get-Process SpaceClaim, ansyscl -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
Remove-Item $done, (Join-Path $dir "odb_real_mark.txt") -ErrorAction SilentlyContinue

$start = Get-Date
$p = Start-Process -FilePath $sc -ArgumentList @("/RunScript=$gate") -PassThru -NoNewWindow
$deadline = $start.AddSeconds(560)
while ((Get-Date) -lt $deadline) {
    if ([LicDlg]::Dismiss()) { Write-Host "license dialog dismissed" }
    if (Test-Path $done) { break }
    if ($p.HasExited) { break }
    Start-Sleep -Seconds 2
}
Write-Host ("odb_real finished in {0:F1}s" -f (((Get-Date) - $start).TotalSeconds))
Get-Process SpaceClaim, ansyscl -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue
if (Test-Path (Join-Path $dir "odb_real_mark.txt")) { Get-Content (Join-Path $dir "odb_real_mark.txt") }
else { Write-Host "NO MARKS" }
