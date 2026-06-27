[CmdletBinding()]
param([int]$TimeoutSec = 180)
$sc = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$auto = "$env:APPDATA\SpaceClaim\Autosave"
$script = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\debug_as1ac_hole.py"
$done = "D:\MXDigitalTwinModeller\Test\RealCAD\solo_done.txt"

Get-Process SpaceClaim, ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
if (Test-Path $auto) { Get-ChildItem $auto -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue }
Remove-Item $done -ErrorAction SilentlyContinue

$start = Get-Date
$p = Start-Process -FilePath $sc -ArgumentList @("/RunScript=$script") -PassThru -NoNewWindow
$deadline = $start.AddSeconds($TimeoutSec)
while ((Get-Date) -lt $deadline) {
    if (Test-Path $done) { break }
    Start-Sleep -Seconds 2
}
$elapsed = ((Get-Date) - $start).TotalSeconds
Get-Process SpaceClaim, ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1
Write-Host ("done in {0:F1}s" -f $elapsed)
