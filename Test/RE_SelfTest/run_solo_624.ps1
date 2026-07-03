$ErrorActionPreference = "Continue"
. "$PSScriptRoot\_sc_kill.ps1"
$sc = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$realCadDir = "D:\MXDigitalTwinModeller\Test\RealCAD"
$target = Join-Path $realCadDir "solo_target.txt"
$done = Join-Path $realCadDir "solo_done.txt"
$rj = Join-Path $realCadDir "matrix_results\09_624ZZ_bearing_AddHolePattern.json"
$warm = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\_warm.py"
$warmMarker = Join-Path $realCadDir "warm_done.txt"
$matrix = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\mod_matrix_test.py"

Stop-SpaceClaimTree | Out-Null
Remove-Item $warmMarker -ErrorAction SilentlyContinue
$pw = Start-Process -FilePath $sc -ArgumentList "/RunScript=$warm" -PassThru -NoNewWindow
$wd = (Get-Date).AddSeconds(300)
while ((Get-Date) -lt $wd) { if (Test-Path $warmMarker) { break }; if ($pw.HasExited) { break }; Start-Sleep 3 }
Stop-SpaceClaimTree | Out-Null

Remove-Item $done, $rj -ErrorAction SilentlyContinue
"9`t624ZZ_bearing`t$realCadDir\freecad\624ZZ_Ball_Bearing.stp`tAddHolePattern" | Out-File -FilePath $target -Encoding utf8
$p = Start-Process -FilePath $sc -ArgumentList "/RunScript=$matrix" -PassThru -NoNewWindow
$dl = (Get-Date).AddSeconds(360)
while ((Get-Date) -lt $dl) { if (Test-Path $done) { Start-Sleep 1; break }; if ($p.HasExited) { Start-Sleep 2; break }; Start-Sleep 3 }
Stop-SpaceClaimTree | Out-Null
if (Test-Path $rj) {
    (Get-Content $rj -Raw | ConvertFrom-Json) | Select-Object action, odbranch, oracle, verdict | Format-List
} else { "NO RESULT" }
