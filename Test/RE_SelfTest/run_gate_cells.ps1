# Targeted single-cell gate for the mod-matrix deepening WIs (Stage C). Runs a handful of
# cells one SpaceClaim launch each via the solo_target.txt / solo_done.txt protocol, with a
# warm-up first (fresh-DLL JIT), and prints each cell's verdict. PASS = expected flips land
# and sentinel cells keep their verdict.
$ErrorActionPreference = "Continue"
. "$PSScriptRoot\_sc_kill.ps1"
$sc = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$realCadDir = "D:\MXDigitalTwinModeller\Test\RealCAD"
$ansys = "d:\Program Files\ANSYS Inc\ANSYS Student\v252\SCDM\Library\SrModels"
$target = Join-Path $realCadDir "solo_target.txt"
$done = Join-Path $realCadDir "solo_done.txt"
$results = Join-Path $realCadDir "matrix_results"
$matrix = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\mod_matrix_test.py"
$warm = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\_warm.py"

# (idx, name, path, prim, expected)  -- idx matches run_mod_matrix.ps1 model order
# WI-3 focus: the two OD-chord flips + every legacy-path pattern sentinel that must stay V.
$cells = @(
  @{idx=9;  name="624ZZ_bearing";     path="$realCadDir\freecad\624ZZ_Ball_Bearing.stp";  prim="AddHolePattern"; exp="VERIFIED"}
  @{idx=12; name="F623ZZ_bearing";    path="$realCadDir\freecad\F623ZZ_Ball_Bearing.stp"; prim="AddHolePattern"; exp="VERIFIED"}
  @{idx=1;  name="SampleModel1";      path="$ansys\SampleModel1.scdoc";                    prim="AddHolePattern"; exp="VERIFIED"}
  @{idx=2;  name="samplemodel2";      path="$ansys\samplemodel2.scdoc";                    prim="AddHolePattern"; exp="VERIFIED"}
  @{idx=3;  name="nist_ctc_01";       path="$realCadDir\nist\NIST-PMI-STEP-Files\nist_ctc_01_asme1_ap242-e1.stp"; prim="AddHolePattern"; exp="VERIFIED"}
  @{idx=4;  name="as1-oc-214";        path="$realCadDir\stepcode\as1-oc-214.stp";          prim="AddHolePattern"; exp="VERIFIED"}
  @{idx=6;  name="11752";             path="$realCadDir\pythonocc\11752.stp";              prim="AddHolePattern"; exp="VERIFIED"}
  @{idx=10; name="Ventilator";        path="$realCadDir\pythonocc\Ventilator.stp";         prim="AddHolePattern"; exp="VERIFIED"}
)

Stop-SpaceClaimTree | Out-Null
# warm-up (fresh-DLL JIT so the first real cell doesn't time out)
$warmMarker = Join-Path $realCadDir "warm_done.txt"
if (Test-Path $warm) {
  Remove-Item $warmMarker -ErrorAction SilentlyContinue
  $pw = Start-Process -FilePath $sc -ArgumentList "/RunScript=$warm" -PassThru -NoNewWindow
  $wd = (Get-Date).AddSeconds(300)
  while ((Get-Date) -lt $wd) { if (Test-Path $warmMarker) { break }; if ($pw.HasExited) { break }; Start-Sleep -Seconds 3 }
  Stop-SpaceClaimTree | Out-Null
  Write-Host ("[warm] done marker={0}" -f (Test-Path $warmMarker))
}

$report = @()
foreach ($c in $cells) {
  $rj = Join-Path $results ("{0:D2}_{1}_{2}.json" -f $c.idx, $c.name, $c.prim)
  Remove-Item $done,$rj -ErrorAction SilentlyContinue
  "$($c.idx)`t$($c.name)`t$($c.path)`t$($c.prim)" | Out-File -FilePath $target -Encoding utf8
  $p = Start-Process -FilePath $sc -ArgumentList "/RunScript=$matrix" -PassThru -NoNewWindow
  $dl = (Get-Date).AddSeconds(360)
  while ((Get-Date) -lt $dl) { if (Test-Path $done) { Start-Sleep 1; break }; if ($p.HasExited) { Start-Sleep 2; break }; Start-Sleep 3 }
  Stop-SpaceClaimTree | Out-Null
  $verdict = "NO_RESULT"; $action = ""; $oracle = ""; $msg = ""
  if (Test-Path $rj) {
    try {
      $j = Get-Content $rj -Raw | ConvertFrom-Json
      $verdict = $j.verdict; $action = $j.action; $oracle = $j.oracle; $msg = $j.msg
    } catch { $verdict = "PARSE_ERR" }
  }
  $ok = ($verdict -eq $c.exp)
  $report += [pscustomobject]@{ Cell="$($c.name)|$($c.prim)"; Expected=$c.exp; Got=$verdict; OK=$ok; Action=$action; Oracle=$oracle; Msg=$msg }
  Write-Host ("[{0,-28}] exp={1,-9} got={2,-12} {3}  {4}" -f "$($c.name)|$($c.prim)", $c.exp, $verdict, $(if($ok){"PASS"}else{"**FAIL**"}), $action)
}
Write-Host "`n=== GATE SUMMARY ==="
$report | ForEach-Object { Write-Host ("{0,-30} exp={1,-9} got={2,-12} {3}" -f $_.Cell, $_.Expected, $_.Got, $(if($_.OK){"PASS"}else{"**FAIL** "+$_.Oracle+" / "+$_.Msg})) }
$pass = ($report | Where-Object { $_.OK } | Measure-Object).Count
Write-Host ("`nGATE {0}/{1} PASS" -f $pass, $report.Count)
