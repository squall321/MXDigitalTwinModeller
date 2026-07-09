# Headless gate for the fastener design (g21_fastener.py).
$ErrorActionPreference = "Stop"
$sc   = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$gate = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\g21_fastener.py"
$dir  = "D:\MXDigitalTwinModeller\Test\RE_SelfTest"
$done = Join-Path $dir "g21_done.txt"

Get-Process SpaceClaim, ansyscl -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
Remove-Item $done, (Join-Path $dir "g21_mark.txt"), (Join-Path $dir "g21_result.txt") -ErrorAction SilentlyContinue

$start = Get-Date
$p = Start-Process -FilePath $sc -ArgumentList @("/RunScript=$gate") -PassThru -NoNewWindow
$deadline = $start.AddSeconds(300)
while ((Get-Date) -lt $deadline) {
    if (Test-Path $done) { break }
    if ($p.HasExited) { break }
    Start-Sleep -Seconds 2
}
Write-Host ("g21 finished in {0:F1}s" -f (((Get-Date) - $start).TotalSeconds))
Get-Process SpaceClaim, ansyscl -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue

if (Test-Path (Join-Path $dir "g21_result.txt")) {
    Write-Host "---- g21_result ----"
    Get-Content (Join-Path $dir "g21_result.txt")
} else {
    Write-Host "NO RESULT FILE - marks:"
    if (Test-Path (Join-Path $dir "g21_mark.txt")) { Get-Content (Join-Path $dir "g21_mark.txt") }
}
