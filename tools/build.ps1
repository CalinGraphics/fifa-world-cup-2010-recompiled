# Generates the C++ from your copy of the game and builds the Windows exe.
#
#   powershell -ExecutionPolicy Bypass -File tools\build.ps1 -Game <your .iso or game folder>
#              [-Sdk ..\rexglue-sdk] [-Config release|relwithdebinfo]
#
# Codegen reads default.xex and fifadllzf.xex; from a disc image they are extracted
# to ..\game-x360 first (the manifest's game_root). Nothing from the game ends up in
# this repository: generated\ and out\ are git-ignored.
param(
  [Parameter(Mandatory)] [string]$Game,
  [string]$Sdk = (Join-Path (Split-Path (Split-Path $PSScriptRoot)) "rexglue-sdk"),
  [string]$Config = "release"
)
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot
$x360 = Join-Path $repo "x360"
$gameDir = Join-Path (Split-Path $repo) "game-x360"

if (Test-Path $Game -PathType Leaf) {
  python (Join-Path $PSScriptRoot "disc.py") extract $Game $gameDir default.xex fifadllzf.xex
  if ($LASTEXITCODE) { throw "could not extract the game executables" }
} elseif ($Game -ne $gameDir) {
  New-Item -ItemType Directory -Force $gameDir | Out-Null
  Copy-Item (Join-Path $Game "default.xex"), (Join-Path $Game "fifadllzf.xex") $gameDir -Force
}

$rex = Join-Path $Sdk "out\install\win-amd64\bin\rexglue.exe"
if (-not (Test-Path $rex)) { throw "ReXGlue not found; run tools\setup_sdk.ps1 first" }
Push-Location $x360
try {
  & $rex codegen fifawc10_manifest.toml
  if ($LASTEXITCODE) { throw "codegen failed" }
  $vs = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath
  $cmake = "call `"$vs\VC\Auxiliary\Build\vcvars64.bat`" >nul && set `"PATH=C:\Program Files\LLVM\bin;%PATH%`" && " +
           "cmake --preset win-amd64-$Config -Drexglue_DIR=`"$Sdk\out\install\win-amd64\lib\cmake\rexglue`" && " +
           "cmake --build out/build/win-amd64-$Config"
  cmd /c $cmake
  if ($LASTEXITCODE) { throw "build failed" }
} finally { Pop-Location }
Write-Host "Built: $x360\out\build\win-amd64-$Config\fifawc10.exe" -ForegroundColor Green
