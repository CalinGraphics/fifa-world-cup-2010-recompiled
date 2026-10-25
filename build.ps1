# Builds the PC port from your own copy of the game, in one go.
#
#   powershell -ExecutionPolicy Bypass -File build.ps1 [-Game <your .iso or game folder>]
#              [-Config release|relwithdebinfo] [-Package]
#
# Steps: get the ReXGlue SDK ready (first run only), take default.xex and fifadllzf.xex from
# your game, generate the C++ and build fifawc10.exe. -Package also builds the launcher and
# puts everything a player needs in out\package plus a zip.
#
# -Game defaults to the first .iso in this folder. Everything the build makes from the game
# (out\, recomp\generated\) is git-ignored and stays on your PC.
param(
  [string]$Game = "",
  [string]$Config = "release",
  [switch]$Package
)
$ErrorActionPreference = "Stop"
$repo = $PSScriptRoot
$sdk = Join-Path $repo "rexglue-sdk"
$recomp = Join-Path $repo "recomp"
$gameFiles = Join-Path $repo "out\game-files"

if (-not $Game) {
  $Game = (Get-ChildItem $repo -Filter *.iso -File | Select-Object -First 1).FullName
  if (-not $Game -and (Test-Path (Join-Path $gameFiles "fifadllzf.xex"))) { $Game = $gameFiles }
  if (-not $Game) { throw "No game found. Put your .iso in this folder or pass -Game <path>." }
}

# 1. SDK
$rex = Join-Path $sdk "out\install\win-amd64\bin\rexglue.exe"
if (-not (Test-Path $rex)) { & (Join-Path $repo "tools\setup_sdk.ps1") }

# 2. Game executables
New-Item -ItemType Directory -Force $gameFiles | Out-Null
if (Test-Path $Game -PathType Leaf) {
  python (Join-Path $repo "tools\disc.py") extract $Game $gameFiles default.xex fifadllzf.xex
  if ($LASTEXITCODE) { throw "could not extract the game executables" }
} elseif ((Resolve-Path $Game).Path -ne (Resolve-Path $gameFiles).Path) {
  Copy-Item (Join-Path $Game "default.xex"), (Join-Path $Game "fifadllzf.xex") $gameFiles -Force
}

# 3. Code generation and build
Push-Location $recomp
try {
  & $rex codegen fifawc10_manifest.toml
  if ($LASTEXITCODE) { throw "codegen failed" }
  $vs = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath
  # One command per line: on a single line cmd expands %PATH% before vcvars64 has run.
  $cmake = (@(
    "call `"$vs\VC\Auxiliary\Build\vcvars64.bat`" >nul || exit /b 1",
    "set `"PATH=C:\Program Files\LLVM\bin;%PATH%`"",
    "cmake --preset win-amd64-$Config -Drexglue_DIR=`"$sdk\out\install\win-amd64\lib\cmake\rexglue`" || exit /b 1",
    "cmake --build --preset win-amd64-$Config || exit /b 1"
  ) -join "`r`n")
  # Run through a .cmd file: cmd /c mangles the nested quotes of a one-line command.
  $bat = Join-Path $env:TEMP "fifawc10-build-$PID.cmd"
  Set-Content $bat ("@echo off`r`n" + $cmake) -Encoding ASCII
  cmd /c "`"$bat`""
  $code = $LASTEXITCODE
  Remove-Item $bat -ErrorAction SilentlyContinue
  if ($code) { throw "build failed" }
} finally { Pop-Location }
Write-Host "Built: $repo\out\build\win-amd64-$Config\fifawc10.exe" -ForegroundColor Green

# 4. Player package
if ($Package) { & (Join-Path $repo "tools\package.ps1") -Config $Config }
