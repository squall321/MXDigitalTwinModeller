# Build the ODB++ demo document (odb_demo.scdocx) headlessly.
$ErrorActionPreference = "Stop"
$sc   = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$gate = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\probe_odb_demo.py"
$dir  = "D:\MXDigitalTwinModeller\Test\RE_SelfTest"
$done = Join-Path $dir "odb_demo_done.txt"
. (Join-Path $dir "_lic_watchdog.ps1")
Get-Process SpaceClaim, ansyscl -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
Remove-Item $done, (Join-Path $dir "odb_demo_mark.txt"), (Join-Path $dir "odb_demo.scdocx") -ErrorAction SilentlyContinue

$start = Get-Date
$p = Start-Process -FilePath $sc -ArgumentList @("/RunScript=$gate") -PassThru -NoNewWindow
$deadline = $start.AddSeconds(300)
while ((Get-Date) -lt $deadline) {
    if ([LicDlg]::Dismiss()) { Write-Host "license dialog dismissed" }
    if (Test-Path $done) { break }
    if ($p.HasExited) { break }
    Start-Sleep -Seconds 2
}
Write-Host ("demo finished in {0:F1}s" -f (((Get-Date) - $start).TotalSeconds))
Get-Process SpaceClaim, ansyscl -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue
if (Test-Path (Join-Path $dir "odb_demo_mark.txt")) { Get-Content (Join-Path $dir "odb_demo_mark.txt") }
if (Test-Path (Join-Path $dir "odb_demo.scdocx")) {
    $f = Get-Item (Join-Path $dir "odb_demo.scdocx")
    Write-Host ("SCDOCX OK: {0} bytes" -f $f.Length)
} else { Write-Host "NO SCDOCX" }
