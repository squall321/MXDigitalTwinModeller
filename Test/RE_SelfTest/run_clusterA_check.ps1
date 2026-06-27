# Focused re-run of the 4 Cluster-A target cells to confirm the oracle fix flips them.
$ErrorActionPreference = "Stop"
$sc          = "D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe"
$scriptPath  = "D:\MXDigitalTwinModeller\Test\RE_SelfTest\mod_matrix_test.py"
$realCadDir  = "D:\MXDigitalTwinModeller\Test\RealCAD"
$ansys       = "d:\Program Files\ANSYS Inc\ANSYS Student\v252\SCDM\Library\SrModels"
$targetPath  = Join-Path $realCadDir "solo_target.txt"
$doneMarker  = Join-Path $realCadDir "solo_done.txt"
$resultsDir  = Join-Path $realCadDir "matrix_results"

function Cleanup-SC {
    Get-Process SpaceClaim, ansyscl -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
}

# idx, name, path, prim — must match run_mod_matrix.ps1 model order/indices
$cells = @(
    @{idx=2;  name="samplemodel2";   path="$ansys\samplemodel2.scdoc";                         prim="MoveHole"},
    @{idx=1;  name="SampleModel1";    path="$ansys\SampleModel1.scdoc";                         prim="MoveHole"},
    @{idx=9;  name="624ZZ_bearing";   path="$realCadDir\freecad\624ZZ_Ball_Bearing.stp";        prim="MoveBoss"},
    @{idx=10; name="Ventilator";      path="$realCadDir\pythonocc\Ventilator.stp";              prim="MoveBoss"}
)

Cleanup-SC
foreach ($c in $cells) {
    Write-Host ("=== {0} | {1} ===" -f $c.name, $c.prim)
    "$($c.idx)`t$($c.name)`t$($c.path)`t$($c.prim)" | Out-File -FilePath $targetPath -Encoding utf8
    Remove-Item $doneMarker -ErrorAction SilentlyContinue
    $rj = Join-Path $resultsDir ("{0:D2}_{1}_{2}.json" -f $c.idx, $c.name, $c.prim)
    Remove-Item $rj -ErrorAction SilentlyContinue
    $start = Get-Date
    $p = Start-Process -FilePath $sc -ArgumentList @("/RunScript=$scriptPath") -PassThru -NoNewWindow
    $deadline = $start.AddSeconds(300)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path $doneMarker) { break }
        if ($p.HasExited) { break }
        Start-Sleep -Seconds 2
    }
    Write-Host ("    done in {0:F1}s" -f (((Get-Date)-$start).TotalSeconds))
    Cleanup-SC
    if (Test-Path $rj) {
        $j = Get-Content $rj -Raw | ConvertFrom-Json
        Write-Host ("    VERDICT: {0} | {1}" -f $j.verdict, $j.msg)
    } else { Write-Host "    NO RESULT JSON" }
}
Write-Host "DONE-ALL"
