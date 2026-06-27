$sc = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$model = "D:\MXDigitalTwinModeller\Test\RealCAD\stepcode\as1-oc-214.stp"
Get-Process SpaceClaim,ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
$hs = "$env:LOCALAPPDATA\MXDTM\mcp_handshake.json"
Remove-Item $hs -ErrorAction SilentlyContinue
# Open the model directly so a DesignBody is active.
Start-Process -FilePath $sc -ArgumentList "`"$model`"" -PassThru | Out-Null
Write-Output "SC launching with $model ; waiting for handshake..."
$deadline = (Get-Date).AddSeconds(180)
while ((Get-Date) -lt $deadline) {
  if (Test-Path $hs) { Write-Output "HANDSHAKE: $(Get-Content $hs -Raw)"; break }
  Start-Sleep -Seconds 3
}
if (-not (Test-Path $hs)) { Write-Output "NO HANDSHAKE (server did not start in 180s)" }
