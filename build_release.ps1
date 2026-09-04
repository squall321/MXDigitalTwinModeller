# MX Digital Twin Modeller - Full Release Build
# Run from PowerShell: .\build_release.ps1 [-Rebuild] [-SkipMsi]
#
#   -Rebuild  : PyInstaller EXE 4종을 무조건 다시 만든다
#   -SkipMsi  : MSBuild 는 돌리되 MSI 는 만들지 않는다 (개발 iteration)
#
# 2026-09-02 감사에서 드러난 결함을 고쳤다 (lat.md/build-deploy.md "MSI 동기화 체크리스트"):
#   1. EXE 를 "없을 때만" 빌드하던 것 -> "소스보다 오래됐으면" 재빌드 (stale EXE 가 MSI 에 실리던 원인)
#   2. 시스템 Python 대신 전용 venv(.venv-build) 에서 PyInstaller 실행
#      (시스템 Python 에 torch 가 있으면 hook-torch 가 수 GB 를 끌어들인다)
#   3. 뷰어 빌드에 --hidden-import rainflow 추가 (build_viewer.bat 에는 있었고 여기엔 없어 Fatigue 탭이 깨졌다)
#   4. PyInstaller 산출물(build/ dist/ *.spec)을 저장소 안이 아니라 .venv-build\pyi\ 아래에 둔다
#      (저장소의 추적 파일을 덮어쓰지 않도록)
#   5. Invoke-PyInstaller 의 파라미터 이름 $Args -> $PyArgs (자동변수 $args 에 가려져 스플랫이 비던 버그)
param(
    [switch]$Rebuild,
    [switch]$SkipMsi
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " MX Digital Twin Modeller - Full Release Build" -ForegroundColor Cyan
Write-Host "============================================================"
Write-Host ""

$msbuild       = "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
$csproj        = Join-Path $root "MXDigitalTwinModeller.csproj"
$ppDir         = Join-Path $root "Mechanical\MXSimulator\postprocess"
$viewerExe     = Join-Path $ppDir "MXPostViewer.exe"
$calibDir      = Join-Path $root "Mechanical\MXSimulator\calibration"
$calibratorExe = Join-Path $calibDir "MaterialCalibrator.exe"
$bridgeDir     = Join-Path $root "tools\mcp_bridge"
$bridgeExe     = Join-Path $bridgeDir "mxdtm_mcp_bridge.exe"
$registerExe   = Join-Path $bridgeDir "register_claude_desktop.exe"
$msiOut        = Join-Path $root "Installer\MXDigitalTwinModeller.msi"
$venvDir       = Join-Path $root ".venv-build"
$venvPy        = Join-Path $venvDir "Scripts\python.exe"

# ---- helpers ------------------------------------------------------------
function Test-Stale {
    # EXE 가 없거나, 소스 글롭 중 하나라도 EXE 보다 새로우면 $true
    param([string]$Exe, [string[]]$SourceGlobs)
    if (-not (Test-Path $Exe)) { return $true }
    $exeTime = (Get-Item $Exe).LastWriteTime
    foreach ($g in $SourceGlobs) {
        foreach ($f in (Get-ChildItem $g -File -ErrorAction SilentlyContinue)) {
            if ($f.LastWriteTime -gt $exeTime) { return $true }
        }
    }
    return $false
}

function Report-Freshness {
    param([string]$Label, [string]$Exe, [string[]]$SourceGlobs)
    $stale = Test-Stale $Exe $SourceGlobs
    $exeStr = if (Test-Path $Exe) { (Get-Item $Exe).LastWriteTime.ToString("yyyy-MM-dd HH:mm") } else { "(missing)" }
    $newest = $null
    foreach ($g in $SourceGlobs) {
        foreach ($f in (Get-ChildItem $g -File -ErrorAction SilentlyContinue)) {
            if ($newest -eq $null -or $f.LastWriteTime -gt $newest) { $newest = $f.LastWriteTime }
        }
    }
    $srcStr = if ($newest) { $newest.ToString("yyyy-MM-dd HH:mm") } else { "?" }
    $tag = if ($stale) { "REBUILD" } else { "fresh" }
    Write-Host ("      {0,-22} exe {1}   src {2}   -> {3}" -f $Label, $exeStr, $srcStr, $tag)
    return $stale
}

function Invoke-PyInstaller {
    # 파라미터 이름을 $Args 로 두면 PowerShell 자동변수 $args 에 가려져 스플랫이 빈 값이 된다
    # (PyInstaller 가 스크립트 없이 호출돼 "scriptname required" 로 실패). 반드시 다른 이름을 쓴다.
    # 산출물(build/ dist/ *.spec)은 저장소가 아니라 .venv-build\pyi\<Name>\ 에 둔다.
    param([string]$WorkDir, [string[]]$PyArgs, [string]$Name)
    if (-not $PyArgs -or $PyArgs.Count -eq 0) { throw "Invoke-PyInstaller: empty argument list for $WorkDir" }
    if (-not $Name) { throw "Invoke-PyInstaller: Name is required" }
    $scratch = Join-Path $venvDir ("pyi\" + $Name)
    New-Item -ItemType Directory -Force -Path $scratch | Out-Null
    Push-Location $WorkDir
    try {
        & $venvPy -m PyInstaller @PyArgs --noconfirm `
            --distpath (Join-Path $scratch "dist") `
            --workpath (Join-Path $scratch "build") `
            --specpath $scratch
        if ($LASTEXITCODE -ne 0) { throw "PyInstaller failed in $WorkDir" }
    } finally { Pop-Location }
    $exe = Join-Path $scratch ("dist\" + $Name + ".exe")
    if (-not (Test-Path $exe)) { throw "PyInstaller produced no $exe" }
    return $exe
}

# ---- Step 0: build venv (PyInstaller 는 깨끗한 환경에서만) ------------------
Write-Host "[0/5] Build venv ($venvDir)..." -ForegroundColor Yellow
if (-not (Test-Path $venvPy)) {
    $py = (Get-Command python -ErrorAction Stop).Source
    Write-Host "      Creating venv with $py"
    & $py -m venv $venvDir
    if ($LASTEXITCODE -ne 0) { throw "venv creation failed" }
}
& $venvPy -m pip install -q --upgrade pip
& $venvPy -m pip install -q -r (Join-Path $ppDir "requirements.txt") pyinstaller
if ($LASTEXITCODE -ne 0) { throw "pip install failed" }
# torch 같은 무거운 패키지가 섞여 있으면 EXE 가 수 GB 가 된다 - 미리 막는다
& $venvPy -c "import importlib.util as u, sys; sys.exit(0 if u.find_spec('torch') is None else 1)"
if ($LASTEXITCODE -ne 0) { throw "torch is importable inside .venv-build - the venv is contaminated; delete it and rerun" }
Write-Host "      venv OK (torch absent)" -ForegroundColor Green
# 외부 PYTHONPATH(예: OpenCASCADE/NGSolve site-packages)가 venv 안으로 새어 들어오면 PyInstaller 의
# 모듈 탐색 경로가 오염된다 - 빌드는 venv 의 패키지만으로 한다 (2026-09-03 실행 로그에서 확인).
if ($env:PYTHONPATH) { Write-Host "      clearing PYTHONPATH for the build ($env:PYTHONPATH)" -ForegroundColor Yellow; $env:PYTHONPATH = "" }
Write-Host ""

# ---- Freshness report ----------------------------------------------------
Write-Host "      EXE freshness (exe older than any source => REBUILD):" -ForegroundColor Yellow
$viewerSrc = @((Join-Path $ppDir "*.py"))
$calibSrc  = @((Join-Path $calibDir "*.py"), (Join-Path $calibDir "utils\*.py"), (Join-Path $calibDir "tests\*.py"))
$bridgeSrc = @((Join-Path $bridgeDir "mxdtm_mcp_bridge.py"))
$regSrc    = @((Join-Path $bridgeDir "register_claude_desktop.py"))
$viewerStale = Report-Freshness "MXPostViewer.exe"        $viewerExe     $viewerSrc
$calibStale  = Report-Freshness "MaterialCalibrator.exe"  $calibratorExe $calibSrc
$bridgeStale = Report-Freshness "mxdtm_mcp_bridge.exe"    $bridgeExe     $bridgeSrc
$regStale    = Report-Freshness "register_claude_desktop" $registerExe   $regSrc
if ($Rebuild) { Write-Host "      -Rebuild: forcing all four" -ForegroundColor Yellow; $viewerStale = $calibStale = $bridgeStale = $regStale = $true }
Write-Host ""

# ---- Step 1: MaterialCalibrator.exe (Material Twin) --------------------
Write-Host "[1/5] MaterialCalibrator.exe..." -ForegroundColor Yellow
if ($calibStale) {
    $built = Invoke-PyInstaller $calibDir @("--onefile","--console","--name","MaterialCalibrator",
        "--hidden-import","scipy.stats","--hidden-import","scipy.optimize",
        "--hidden-import","scipy.signal","--hidden-import","numpy","runner.py") "MaterialCalibrator"
    Copy-Item $built $calibratorExe -Force
    Write-Host "      Built: $calibratorExe" -ForegroundColor Green
} else { Write-Host "      fresh - skipped" -ForegroundColor Green }
Write-Host ""

# ---- Step 2: MXPostViewer.exe -------------------------------------------
Write-Host "[2/5] MXPostViewer.exe..." -ForegroundColor Yellow
if ($viewerStale) {
    $built = Invoke-PyInstaller $ppDir @("--onefile","--windowed","--name","MXPostViewer",
        "--hidden-import","scipy.signal","--hidden-import","scipy.fft",
        "--hidden-import","rainflow","runner.py") "MXPostViewer"
    Copy-Item $built $viewerExe -Force
    Write-Host "      Built: $viewerExe" -ForegroundColor Green
} else { Write-Host "      fresh - skipped" -ForegroundColor Green }
Write-Host ""

# ---- Step 3: MCP bridge EXEs (Claude Desktop stdio bridge + registrar) --
# The MSI HARD-REQUIRES both EXEs (Installer\MXDigitalTwinModeller.wxs McpBridgeComponents).
Write-Host "[3/5] MCP bridge EXEs..." -ForegroundColor Yellow
if ($bridgeStale) {
    $built = Invoke-PyInstaller $bridgeDir @("--onefile","--console","--name","mxdtm_mcp_bridge","mxdtm_mcp_bridge.py") "mxdtm_mcp_bridge"
    Copy-Item $built $bridgeExe -Force
    Write-Host "      Built: $bridgeExe" -ForegroundColor Green
} else { Write-Host "      mxdtm_mcp_bridge.exe fresh - skipped" -ForegroundColor Green }
if ($regStale) {
    $built = Invoke-PyInstaller $bridgeDir @("--onefile","--console","--name","register_claude_desktop","register_claude_desktop.py") "register_claude_desktop"
    Copy-Item $built $registerExe -Force
    Write-Host "      Built: $registerExe" -ForegroundColor Green
} else { Write-Host "      register_claude_desktop.exe fresh - skipped" -ForegroundColor Green }
Write-Host ""

# ---- Step 4: MSBuild (DLL + ACT deploy + WiX MSI) -----------------------
Write-Host "[4/5] Building via MSBuild..." -ForegroundColor Yellow
if (-not (Test-Path $msbuild)) {
    Write-Error "MSBuild not found: $msbuild"
    exit 1
}
$msbuildArgs = @($csproj, "/p:Configuration=Debug", "/p:Platform=AnyCPU", "/nologo", "/v:minimal")
if ($SkipMsi) { $msbuildArgs += "/p:SkipMsi=true" }
& $msbuild @msbuildArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "=== MSBuild FAILED ===" -ForegroundColor Red
    if ([Environment]::UserInteractive -and $Host.Name -eq "ConsoleHost") { try { Read-Host "Press Enter to exit" } catch {} }
    exit 1
}
Write-Host ""

# ---- Step 5: Clear Workbench ribbon cache (for new buttons/icons) -------
Write-Host "[5/5] Clearing Workbench ribbon cache..." -ForegroundColor Yellow
$cacheDir = Join-Path $env:APPDATA "Ansys\v252\Applets\DSApplet\en-us"
foreach ($f in @("ExternalActions.xml", "ribbonLayout.xml", "RibbonState.xml")) {
    $p = Join-Path $cacheDir $f
    if (Test-Path $p) { Remove-Item $p -Force; Write-Host "      Deleted: $f" }
}
Write-Host ""

# ---- Done ---------------------------------------------------------------
Write-Host "Build complete!" -ForegroundColor Green
Write-Host ""
if ($SkipMsi) {
    Write-Host "  MSI : skipped (-SkipMsi)" -ForegroundColor Yellow
} elseif (Test-Path $msiOut) {
    $size = (Get-Item $msiOut).Length / 1MB
    Write-Host ("  MSI : {0}" -f $msiOut) -ForegroundColor Green
    Write-Host ("  Size: {0:F1} MB" -f $size) -ForegroundColor Green
} else {
    Write-Host "  WARNING: MSI not found. Check WiX output above." -ForegroundColor Red
}
Write-Host ""
Write-Host "============================================================"
# Pause only in an interactive console (double-click); stays automation-safe in CI.
if ([Environment]::UserInteractive -and $Host.Name -eq "ConsoleHost") {
    try { Read-Host "Press Enter to exit" } catch {}
}
