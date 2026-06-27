# Generic probe launcher: 1 SC session, run given script, poll done marker.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Script,
    [int]$TimeoutSec = 240
)
$sc   = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$auto = "$env:APPDATA\SpaceClaim\Autosave"
$done = "D:\MXDigitalTwinModeller\Test\RealCAD\solo_done.txt"

Get-Process SpaceClaim, ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
if (Test-Path $auto) { Get-ChildItem $auto -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue }
Remove-Item $done -ErrorAction SilentlyContinue

$start = Get-Date
$p = Start-Process -FilePath $sc -ArgumentList @("/RunScript=$Script") -PassThru -NoNewWindow
$deadline = $start.AddSeconds($TimeoutSec)
while ((Get-Date) -lt $deadline) {
    if (Test-Path $done) { break }
    if ($p.HasExited) { Write-Host "SC exited early (crash?)"; break }
    Start-Sleep -Seconds 2
}
$elapsed = ((Get-Date) - $start).TotalSeconds
Get-Process SpaceClaim, ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
Write-Host ("probe done in {0:F1}s" -f $elapsed)
