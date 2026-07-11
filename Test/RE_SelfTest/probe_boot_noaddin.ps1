# Boot probe with the add-in disabled: does SpaceClaim start without our DLL?
$ErrorActionPreference = "Stop"
$addin = "C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller"
$mark = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\boot_mark.txt"
$exe = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"

Get-Process SpaceClaim, ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
Remove-Item $mark -ErrorAction SilentlyContinue
Rename-Item -Path $addin -NewName "MXDigitalTwinModeller_off"
try {
    $p = Start-Process -FilePath $exe -ArgumentList "/RunScript=D:\MXDigitalTwinModeller\Test\RE_SelfTest\probe_boot.py" -PassThru -NoNewWindow
    $deadline = (Get-Date).AddSeconds(120)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path $mark) { Start-Sleep -Seconds 3; break }
        if ($p.HasExited) { break }
        Start-Sleep -Seconds 3
    }
    Get-Process SpaceClaim, ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
} finally {
    Rename-Item -Path ($addin + "_off") -NewName "MXDigitalTwinModeller"
}
if (Test-Path $mark) { Write-Host "SC BOOTS WITHOUT ADD-IN"; Get-Content $mark }
else { Write-Host "STILL HUNG without add-in -> environment problem" }
