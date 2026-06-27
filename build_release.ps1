# MX Digital Twin Modeller - Full Release Build
# Run from PowerShell: .\build_release.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " MX Digital Twin Modeller - Full Release Build" -ForegroundColor Cyan
Write-Host "============================================================"
Write-Host ""

$msbuild    = "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
$csproj     = Join-Path $root "MXDigitalTwinModeller.csproj"
$viewerExe     = Join-Path $root "Mechanical\MXSimulator\postprocess\MXPostViewer.exe"
$ppDir         = Join-Path $root "Mechanical\MXSimulator\postprocess"
$calibratorExe = Join-Path $root "Mechanical\MXSimulator\calibration\MaterialCalibrator.exe"
$calibDir      = Join-Path $root "Mechanical\MXSimulator\calibration"
$msiOut        = Join-Path $root "Installer\MXDigitalTwinModeller.msi"

# ---- Step 1: MaterialCalibrator.exe (Material Twin) --------------------
Write-Host "[1/4] Checking MaterialCalibrator.exe..." -ForegroundColor Yellow
if (Test-Path $calibratorExe) {
    Write-Host "      Found: $calibratorExe" -ForegroundColor Green
} else {
    Write-Host "      Not found. Building with PyInstaller..." -ForegroundColor Yellow
    Push-Location $calibDir
    try {
        $py = (Get-Command python -ErrorAction Stop).Source
        Write-Host "      Python: $py"
        & $py -m pip install --upgrade pyinstaller numpy scipy matplotlib -q
        if ($LASTEXITCODE -ne 0) { throw "pip failed" }

        & $py -m PyInstaller --onefile --console --name MaterialCalibrator `
            --hidden-import scipy.stats --hidden-import scipy.optimize `
            --hidden-import scipy.signal --hidden-import numpy `
            runner.py --noconfirm
        if ($LASTEXITCODE -ne 0) { throw "PyInstaller failed" }

        Copy-Item "dist\MaterialCalibrator.exe" "MaterialCalibrator.exe" -Force
        Write-Host "      Built: $calibratorExe" -ForegroundColor Green
    } finally {
        Pop-Location
    }
}
Write-Host ""

# ---- Step 2: MXPostViewer.exe -------------------------------------------
Write-Host "[2/4] Checking MXPostViewer.exe..." -ForegroundColor Yellow
if (Test-Path $viewerExe) {
    Write-Host "      Found: $viewerExe" -ForegroundColor Green
} else {
    Write-Host "      Not found. Building with PyInstaller..." -ForegroundColor Yellow
    Push-Location $ppDir
    try {
        $py = (Get-Command python -ErrorAction Stop).Source
        Write-Host "      Python: $py"
        & $py -m pip install --upgrade pyinstaller numpy scipy matplotlib PyQt5 -q
        if ($LASTEXITCODE -ne 0) { throw "pip failed" }

        & $py -m PyInstaller --onefile --windowed --name MXPostViewer `
            --hidden-import scipy.signal --hidden-import scipy.fft `
            runner.py --noconfirm
        if ($LASTEXITCODE -ne 0) { throw "PyInstaller failed" }

        Copy-Item "dist\MXPostViewer.exe" "MXPostViewer.exe" -Force
        Write-Host "      Built: $viewerExe" -ForegroundColor Green
    } finally {
        Pop-Location
    }
}
Write-Host ""

# ---- Step 3: MSBuild (DLL + ACT deploy + WiX MSI) -----------------------
Write-Host "[3/4] Building via MSBuild..." -ForegroundColor Yellow
if (-not (Test-Path $msbuild)) {
    Write-Error "MSBuild not found: $msbuild"
    exit 1
}

& $msbuild $csproj /p:Configuration=Debug /p:Platform=AnyCPU /nologo /v:minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "=== MSBuild FAILED ===" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}
Write-Host ""

# ---- Step 4: Clear Workbench ribbon cache (for new buttons/icons) -------
Write-Host "[4/4] Clearing Workbench ribbon cache..." -ForegroundColor Yellow
$cacheDir = Join-Path $env:APPDATA "Ansys\v252\Applets\DSApplet\en-us"
foreach ($f in @("ExternalActions.xml", "ribbonLayout.xml", "RibbonState.xml")) {
    $p = Join-Path $cacheDir $f
    if (Test-Path $p) { Remove-Item $p -Force; Write-Host "      Deleted: $f" }
}
Write-Host ""

# ---- Step 5: Done -------------------------------------------------------
Write-Host "[5/5] Build complete!" -ForegroundColor Green
Write-Host ""
if (Test-Path $msiOut) {
    $size = (Get-Item $msiOut).Length / 1MB
    Write-Host ("  MSI : {0}" -f $msiOut) -ForegroundColor Green
    Write-Host ("  Size: {0:F1} MB" -f $size) -ForegroundColor Green
} else {
    Write-Host "  WARNING: MSI not found. Check WiX output above." -ForegroundColor Red
}
Write-Host ""
Write-Host "============================================================"
Read-Host "Press Enter to exit"
