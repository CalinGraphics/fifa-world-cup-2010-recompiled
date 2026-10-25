param([string[]]$Steps = @(), [string]$Tag = "nav", [switch]$Start, [string[]]$Extra = @(), [int]$BootSeconds = 25, [string]$Config = "release")
# Drives the game with keyboard steps and saves a screenshot after each one (out\dev\<Tag>-NN.png).
# A step is "Key" or "Key:seconds" (wait after the press), or "wait:seconds", or "shot".
#   .\nav.ps1 -Start -Tag t -Steps Return:8,Space:4,shot
. "$PSScriptRoot\_paths.ps1"
if ($Start) {
  $env:FIFAWC10_PERF_LOG = "1"
  & "$PSScriptRoot\run_game.ps1" -Config $Config -Seconds $BootSeconds -Keep -Extra (@("--fullscreen=false", "--mnk_mode=true") + $Extra) | Out-Null
  $env:FIFAWC10_PERF_LOG = $null
}
$i = 0
foreach ($s in $Steps) {
  $key, $wait = $s.Split(":")
  $wait = if ($wait) { [double]$wait } else { 3 }
  if ($key -eq "wait") { Start-Sleep -Milliseconds ($wait * 1000); continue }
  if ($key -ne "shot") {
    & "$PSScriptRoot\press.ps1" -Keys $key -HoldMs 150 -GapMs 100 | Out-Null
    Start-Sleep -Milliseconds ($wait * 1000)
  }
  $i++
  & "$PSScriptRoot\screenshot.ps1" -Out (Join-Path $Work ("{0}-{1:D2}.png" -f $Tag, $i)) | Out-Null
}
"alive=$([bool](Get-Process fifawc10 -ErrorAction SilentlyContinue)) shots=$i"
