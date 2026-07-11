# Launch SC, wait, then enumerate its top-level window titles (is a modal blocking?)
$ErrorActionPreference = "Stop"
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;
public class WinEnum {
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lp);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    delegate bool EnumWindowsProc(IntPtr h, IntPtr lp);
    public static List<string> TitlesForPid(uint pid) {
        var result = new List<string>();
        EnumWindows((h, lp) => {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p == pid && IsWindowVisible(h)) {
                var sb = new StringBuilder(256);
                GetWindowText(h, sb, 256);
                result.Add(sb.ToString());
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }
}
"@
Get-Process SpaceClaim, ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
$exe = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$p = Start-Process -FilePath $exe -ArgumentList "/RunScript=D:\MXDigitalTwinModeller\Test\RE_SelfTest\probe_boot.py" -PassThru
Start-Sleep -Seconds 75
$titles = [WinEnum]::TitlesForPid([uint32]$p.Id)
Write-Host ("windows: " + ($titles -join " || "))
Get-Process SpaceClaim, ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
