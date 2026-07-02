# Live full-relay E2E: plain GUI SpaceClaim (Add-In starts McpServer -> handshake file),
# then the SHIPPED stdio bridge exe relays JSON-RPC from a request file - the exact path a
# Claude Desktop user exercises. Verifies the 43-tool surface end-to-end (tools/list count
# + a real CAE tool building geometry in the live session).
$ErrorActionPreference = "Stop"
. "$PSScriptRoot\_sc_kill.ps1"
$sc = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$bridge = "D:\MXDigitalTwinModeller\tools\mcp_bridge\dist\mxdtm_mcp_bridge.exe"
$req = $args[0]
$out = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\e2e_bridge_result.txt"
$hs = "$env:LOCALAPPDATA\MXDTM\mcp_handshake.json"

Stop-SpaceClaimTree | Out-Null
Remove-Item $out -ErrorAction SilentlyContinue
Remove-Item $hs -ErrorAction SilentlyContinue

$p = Start-Process -FilePath $sc -PassThru -NoNewWindow
# wait for the Add-In's MCP handshake (fresh file = server is listening)
$deadline = (Get-Date).AddSeconds(240)
while ((Get-Date) -lt $deadline) {
    if (Test-Path $hs) { break }
    if ($p.HasExited) { break }
    Start-Sleep -Seconds 3
}
if (-not (Test-Path $hs)) {
    "NO HANDSHAKE (SC exited=$($p.HasExited))" | Out-File $out -Encoding utf8
    Stop-SpaceClaimTree | Out-Null
    Get-Content $out -Raw
    exit 1
}
Start-Sleep -Seconds 5   # let the listener settle

# drive the SHIPPED bridge exe with clean bytes (cmd redirection - PS pipes corrupt UTF-8)
cmd /c "`"$bridge`" < `"$req`" > `"$out`" 2>nul"

Stop-SpaceClaimTree | Out-Null
Get-Content $out -Raw
