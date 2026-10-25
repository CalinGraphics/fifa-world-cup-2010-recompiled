# Shared paths for the developer test scripts (dot-sourced).
$Repo = Split-Path (Split-Path $PSScriptRoot)
$Work = Join-Path $Repo "out\dev"
New-Item -ItemType Directory -Force $Work | Out-Null
# The game copy to run: $env:FIFAWC10_GAME, else the first .iso next to the repository.
$DefaultGame = if ($env:FIFAWC10_GAME) { $env:FIFAWC10_GAME } else {
  (Get-ChildItem (Split-Path $Repo) -Filter *.iso -File -ErrorAction SilentlyContinue | Select-Object -First 1).FullName
}
