# Gets the ReXGlue SDK ready for this project: clone, pin, apply patches, build, install.
#
#   powershell -ExecutionPolicy Bypass -File tools\setup_sdk.ps1 [-Sdk <folder>]   (build.ps1 runs it for you)
#
# Needs git, Visual Studio (C++ workload), LLVM in "C:\Program Files\LLVM" and Python.
param(
  [string]$Sdk = "",
  [string]$Commit = "bd833a2"
)
$ErrorActionPreference = "Stop"
if (-not $Sdk) { $Sdk = Join-Path (Split-Path $PSScriptRoot) "rexglue-sdk" }
$repo = Split-Path $PSScriptRoot

if (-not (Test-Path $Sdk)) {
  git clone -c core.autocrlf=false -c core.symlinks=true https://github.com/rexglue/rexglue-sdk $Sdk
}
Push-Location $Sdk
try {
  git checkout -q $Commit
  git submodule update --init --recursive
  # On Windows some submodules check out git symlinks as small text files.
  python (Join-Path $PSScriptRoot "fix_symlinks.py") $Sdk

  # Our patches, on a clean tree (re-running the script is safe).
  git checkout -q -- .
  foreach ($p in Get-ChildItem (Join-Path $repo "patches\rexglue-sdk\*.patch") | Sort-Object Name) {
    git apply $p.FullName
    if ($LASTEXITCODE) { throw "patch failed: $($p.Name)" }
    Write-Host "applied $($p.Name)"
  }

  $vs = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath
  $cmd = (@(
    "call `"$vs\VC\Auxiliary\Build\vcvars64.bat`" >nul || exit /b 1",
    "set `"PATH=C:\Program Files\LLVM\bin;%PATH%`"",
    "cmake --preset win-amd64 || exit /b 1",
    "cmake --build out/build/win-amd64 --config RelWithDebInfo --target install || exit /b 1",
    "cmake --build out/build/win-amd64 --config Release --target install || exit /b 1"
  ) -join "`r`n")
  # Run through a .cmd file: cmd /c mangles the nested quotes of a one-line command.
  $bat = Join-Path $env:TEMP "fifawc10-sdk-$PID.cmd"
  Set-Content $bat ("@echo off`r`n" + $cmd) -Encoding ASCII
  cmd /c "`"$bat`""
  $code = $LASTEXITCODE
  Remove-Item $bat -ErrorAction SilentlyContinue
  if ($code) { $global:LASTEXITCODE = $code }
  if ($LASTEXITCODE) { throw "SDK build failed" }
  # lzxdec.exe for tools\xeximage.py (LZX-compressed XEX images), from the SDK's libmspack.
  $mspack = Join-Path $Sdk "thirdparty\libmspack\libmspack\mspack"
  & "C:\Program Files\LLVM\bin\clang.exe" -O2 -w -D_CRT_SECURE_NO_WARNINGS -I"$mspack" `
    (Join-Path $repo "tools\lzxdec\lzxdec.c") (Join-Path $mspack "lzxd.c") -o (Join-Path $repo "tools\lzxdec.exe")
} finally { Pop-Location }
Write-Host "SDK ready: $Sdk\out\install\win-amd64" -ForegroundColor Green
