# Generates the C++ from your copy of the game and builds the Windows exe.
#
#   powershell -ExecutionPolicy Bypass -File tools\build\build.ps1 -Game <your .iso or game folder>
#              [-Sdk ..\rexglue-sdk] [-Config release|relwithdebinfo]
#
# Codegen reads default.xex and fifadllzf.xex; from a disc image they are extracted
# to ..\game-x360 first (the manifest's game_root). Nothing from the game ends up in
# this repository: generated\ and out\ are git-ignored.
param(
  [Parameter(Mandatory)] [string]$Game,
  [string]$Sdk = "",
  [string]$Config = "release"
)
$ErrorActionPreference = "Stop"
if (-not $Sdk) { $Sdk = Join-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot))) "rexglue-sdk" }
$repo = Split-Path (Split-Path $PSScriptRoot)
$x360 = Join-Path $repo "x360"
$gameDir = Join-Path (Split-Path $repo) "game-x360"

if (Test-Path $Game -PathType Leaf) {
  python (Join-Path $repo "tools\disc.py") extract $Game $gameDir default.xex fifadllzf.xex
  if ($LASTEXITCODE) { throw "could not extract the game executables" }
} elseif ($Game -ne $gameDir) {
  New-Item -ItemType Directory -Force $gameDir | Out-Null
  Copy-Item (Join-Path $Game "default.xex"), (Join-Path $Game "fifadllzf.xex") $gameDir -Force
}

$rex = Join-Path $Sdk "out\install\win-amd64\bin\rexglue.exe"
if (-not (Test-Path $rex)) { throw "ReXGlue not found; run tools\build\setup_sdk.ps1 first" }
Push-Location $x360
try {
  & $rex codegen fifawc10_manifest.toml
  if ($LASTEXITCODE) { throw "codegen failed" }
  $vs = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath
  # One command per line: on a single line cmd expands %PATH% before vcvars64 has run.
  $cmake = (@(
    "call `"$vs\VC\Auxiliary\Build\vcvars64.bat`" >nul || exit /b 1",
    "set `"PATH=C:\Program Files\LLVM\bin;%PATH%`"",
    "cmake --preset win-amd64-$Config -Drexglue_DIR=`"$Sdk\out\install\win-amd64\lib\cmake\rexglue`" || exit /b 1",
    "cmake --build out/build/win-amd64-$Config || exit /b 1"
  ) -join "`r`n")
  # Run through a .cmd file: cmd /c mangles the nested quotes of a one-line command.
  $bat = Join-Path $env:TEMP "fifawc10-build-$PID.cmd"
  Set-Content $bat ("@echo off`r`n" + $cmake) -Encoding ASCII
  cmd /c "`"$bat`""
  $code = $LASTEXITCODE
  Remove-Item $bat -ErrorAction SilentlyContinue
  if ($code) { $global:LASTEXITCODE = $code }
  if ($LASTEXITCODE) { throw "build failed" }
} finally { Pop-Location }
Write-Host "Built: $x360\out\build\win-amd64-$Config\fifawc10.exe" -ForegroundColor Green
