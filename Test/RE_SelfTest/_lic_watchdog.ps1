# Shared license-dialog watchdog for headless SpaceClaim gate runners.
# Since 2026-07-11 the Student license pops an "ANSYS LICENSE MANAGER MESSAGE"
# expiry-warning modal at startup ("license will expire in 20 day(s)"), which blocks
# /RunScript forever. Dot-source this file, then call [LicDlg]::Dismiss() inside the
# wait loop: it clicks the OK button (BM_CLICK) and posts WM_CLOSE as fallback.
if (-not ("LicDlg" -as [type])) {
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class LicDlg {
    [DllImport("user32.dll", CharSet=CharSet.Auto)]
    public static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll", CharSet=CharSet.Auto)]
    public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    public static bool Dismiss() {
        IntPtr h = FindWindow(null, "ANSYS LICENSE MANAGER MESSAGE");
        if (h == IntPtr.Zero) return false;
        IntPtr btn = FindWindowEx(h, IntPtr.Zero, "Button", null);
        if (btn != IntPtr.Zero) SendMessage(btn, 0x00F5, IntPtr.Zero, IntPtr.Zero); // BM_CLICK
        PostMessage(h, 0x0010, IntPtr.Zero, IntPtr.Zero);                           // WM_CLOSE
        return true;
    }
}
"@
}
