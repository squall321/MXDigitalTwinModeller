# Shared SpaceClaim cleanup — kill the WHOLE process tree, not just the parent.
# Orphaned /RunScript children were leaking as 0-MB zombies that hold the AddIns DLL lock.
# Usage:  . "$PSScriptRoot\_sc_kill.ps1"; Stop-SpaceClaimTree
function Stop-SpaceClaimTree {
    param([int]$SettleSeconds = 3)

    $names = @('SpaceClaim','ansyscl','scdm','AnsysFWW')

    # 1) Collect every matching PID, then expand to full descendant trees (children of
    #    children) so a /RunScript-spawned child cannot survive its parent.
    $roots = @()
    foreach ($n in $names) {
        Get-Process -Name $n -ErrorAction SilentlyContinue | ForEach-Object { $roots += $_.Id }
    }
    $all = New-Object System.Collections.Generic.HashSet[int]
    function Add-Tree([int]$treePid) {
        if (-not $all.Add($treePid)) { return }
        Get-CimInstance Win32_Process -Filter "ParentProcessId=$treePid" -ErrorAction SilentlyContinue |
            ForEach-Object { Add-Tree ([int]$_.ProcessId) }
    }
    foreach ($r in $roots) { Add-Tree $r }

    # 2) Kill leaves-first (children before parents) via both Stop-Process and WMI Terminate.
    foreach ($procId in ($all | Sort-Object -Descending)) {
        try { Stop-Process -Id $procId -Force -ErrorAction Stop } catch {
            try { (Get-CimInstance Win32_Process -Filter "ProcessId=$procId" -ErrorAction Stop) |
                    Invoke-CimMethod -MethodName Terminate -ErrorAction SilentlyContinue | Out-Null } catch {}
        }
    }
    Start-Sleep -Seconds $SettleSeconds

    # 3) Report any survivors (un-killable privileged zombies need Task Manager).
    $left = @()
    foreach ($n in $names) {
        Get-Process -Name $n -ErrorAction SilentlyContinue | ForEach-Object { $left += "$($_.ProcessName)($($_.Id))" }
    }
    if ($left.Count -gt 0) { Write-Host ("ZOMBIES REMAIN (need Task Manager): " + ($left -join ', ')) }
    else { Write-Host "SpaceClaim tree clean" }
    return $left.Count
}
