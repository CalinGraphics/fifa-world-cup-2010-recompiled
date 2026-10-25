# Shared paths for the developer test scripts (dot-sourced).
$Repo = Split-Path (Split-Path $PSScriptRoot)
$Work = Join-Path $Repo "out\dev"
New-Item -ItemType Directory -Force $Work | Out-Null
# The game copy to run: $env:FIFAWC10_GAME, else the first .iso in the repository folder.
$DefaultGame = if ($env:FIFAWC10_GAME) { $env:FIFAWC10_GAME } else {
  (Get-ChildItem $Repo -Filter *.iso -File -ErrorAction SilentlyContinue | Select-Object -First 1).FullName
}
