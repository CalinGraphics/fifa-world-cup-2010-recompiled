# Puts the built game, the launcher and the runtime files in one folder and zips it.
#
#   powershell -ExecutionPolicy Bypass -File installer\package.ps1 [-Config release]   (or: build.ps1 -Package)
#
# Output: out\package\FIFA World Cup 2010 Recompiled\ and out\FIFA-World-Cup-2010-Recompiled-<version>-win-x64.zip
# The package holds no game files: players point the launcher at their own disc image.
param(
  [string]$Sdk = "",
  [string]$Config = "release"
)
$ErrorActionPreference = "Stop"
if (-not $Sdk) { $Sdk = Join-Path (Split-Path $PSScriptRoot) "rexglue-sdk" }
$repo = Split-Path $PSScriptRoot
$version = (Get-Content (Join-Path $repo "VERSION")).Trim()
$bin = Join-Path $repo "out\build\win-amd64-$Config"
$outRoot = Join-Path $repo "out"
$pkg = Join-Path $outRoot "package\FIFA World Cup 2010 Recompiled"
Remove-Item $pkg -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $pkg, "$pkg\licenses" | Out-Null

# Game
$game = @("fifawc10.exe", "fifawc10_fifadllzf.dll", "rexruntime.dll", "rexgpu-xenos.dll")
if ($Config -ne "release") { $game = @("fifawc10.exe", "fifawc10_fifadllzf.dll", "rexruntimerd.dll", "rexgpu-xenosrd.dll") }
foreach ($f in $game) { Copy-Item (Join-Path $bin $f) $pkg }

# Launcher (one self-contained exe)
dotnet publish (Join-Path $repo "launcher\Launcher.csproj") -c Release -o (Join-Path $outRoot "launcher") -nologo -v q
if ($LASTEXITCODE) { throw "launcher publish failed" }
Copy-Item (Join-Path $outRoot "launcher\Launcher.exe") $pkg

# Visual C++ runtime, app-local, so players don't need to install it
$vs = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath
$crt = Get-ChildItem "$vs\VC\Redist\MSVC\*\x64\Microsoft.VC*.CRT" -Directory | Sort-Object FullName | Select-Object -Last 1
foreach ($f in "msvcp140.dll", "msvcp140_atomic_wait.dll", "vcruntime140.dll", "vcruntime140_1.dll") {
  Copy-Item (Join-Path $crt.FullName $f) $pkg
}

# Licenses: this project, the ReXGlue SDK, and the libraries built into the runtime
Copy-Item (Join-Path $repo "LICENSE") (Join-Path $pkg "LICENSE.txt")
Copy-Item (Join-Path $Sdk "LICENSE") (Join-Path $pkg "licenses\rexglue-sdk.txt")
Get-ChildItem (Join-Path $Sdk "thirdparty") -Directory | ForEach-Object {
  $lic = Get-ChildItem $_.FullName -File | Where-Object Name -match '^(LICEN[CS]E|COPYING|COPYRIGHT|UNLICENSE)' | Select-Object -First 1
  if (-not $lic) {  # some keep it one folder down (libmspack/libmspack/COPYING.LIB)
    $lic = Get-ChildItem $_.FullName -File -Recurse -Depth 1 | Where-Object Name -match '^(LICEN[CS]E|COPYING)' | Select-Object -First 1
  }
  if ($lic) { Copy-Item $lic.FullName (Join-Path $pkg "licenses\$($_.Name).txt") }
}
Copy-Item (Join-Path $repo "installer\README.txt") (Join-Path $pkg "README.txt")

# Zip
$zip = Join-Path $outRoot "FIFA-World-Cup-2010-Recompiled-$version-win-x64.zip"
Remove-Item $zip -ErrorAction SilentlyContinue
Compress-Archive -Path $pkg -DestinationPath $zip -CompressionLevel Optimal
$size = [math]::Round((Get-Item $zip).Length / 1MB, 1)

# The same game and launcher in the repository folder too, so it plays from here
# (git ignores *.exe and *.dll).
Get-ChildItem $pkg -File | Where-Object Extension -in ".exe", ".dll" | Copy-Item -Destination $repo -Force
Write-Host "Package: $pkg" -ForegroundColor Green
Write-Host "Zip:     $zip ($size MB)" -ForegroundColor Green
