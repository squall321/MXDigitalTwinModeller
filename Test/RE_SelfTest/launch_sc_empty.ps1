$sc = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
Get-Process SpaceClaim,ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
$hs = "$env:LOCALAPPDATA\MXDTM\mcp_handshake.json"
Remove-Item $hs -ErrorAction SilentlyContinue
Start-Process -FilePath $sc -PassThru | Out-Null
$deadline = (Get-Date).AddSeconds(180)
while ((Get-Date) -lt $deadline) { if (Test-Path $hs) { Write-Output "HS: $(Get-Content $hs -Raw)"; break }; Start-Sleep -Seconds 3 }
if (-not (Test-Path $hs)) { Write-Output "NO HANDSHAKE" }
