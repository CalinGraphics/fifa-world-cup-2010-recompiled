param([string[]]$Extra = @(), [int]$MatchSeconds = 40, [string]$Tag = "run", [switch]$Shots, [switch]$NoBoot, [string]$Config = "release")
# Boots the game with a fresh profile and drives it into a quick match (NOR v SWE),
# then lets the match play. Screenshots go to out\dev\<Tag>-*.png.
. "$PSScriptRoot\_paths.ps1"
$work = $Work
$saves = Join-Path $work "saves-$Tag"
Remove-Item $saves -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $saves | Out-Null
Remove-Item (Join-Path $work "$Tag-*.png") -ErrorAction SilentlyContinue
Add-Type -AssemblyName System.Drawing

function Probe([int]$x, [int]$y) {
  $f = Join-Path $work "$Tag-probe.png"
  Remove-Item $f -ErrorAction SilentlyContinue
  & "$PSScriptRoot\screenshot.ps1" -Out $f | Out-Null
  if (-not (Test-Path $f)) { return $null }
  $b = [Drawing.Bitmap]::FromFile($f)
  # Probe in 1280x720 coordinates, scaled to the real client size.
  $c = $b.GetPixel([int]($x * $b.Width / 1280), [int]($y * $b.Height / 720)); $b.Dispose()
  return $c
}
function Nav([string[]]$s, [string]$t) { & "$PSScriptRoot\nav.ps1" -Tag $t -Steps $s | Out-Null }
function Alive { [bool](Get-Process fifawc10 -ErrorAction SilentlyContinue) }

if ($NoBoot) { Nav @("Return:8") "$Tag-boot" } else { & "$PSScriptRoot\nav.ps1" -Config $Config -Start -Tag "$Tag-boot" -Extra (@("--user_data_root", "`"$saves`"") + $Extra) -Steps @("Return:8") | Out-Null }
if (-not (Alive)) { "game exited during boot"; exit 1 }
# START on the title screen: press until the profile setup panel (dark) covers the centre.
$ok = $false
for ($i = 0; $i -lt 25 -and -not $ok; $i++) {
  Nav @("X:2") "$Tag-title"
  $c = Probe 640 150
  if ($c -and $c.R -lt 90 -and $c.G -lt 60) { $ok = $true }
}
"title passed: $ok"
if (-not $ok) { exit 1 }
Nav @("Space:5", "Space:5", "Space:5", "Space:5",              # profile setup
      "Space:5", "Space:5", "Space:6",                         # hospitality, autosave, OK
      "Space:5", "Space:5", "Space:5", "Space:5",              # Kick-Off, home, away, home kit
      "Space:5", "A:2", "Space:6", "Space:4") "$Tag-menu"      # away kit, home side, Play Match
# Loading, stadium intro and line-ups: keep pressing A until the match starts.
$intro = @(); for ($i = 0; $i -lt 12; $i++) { $intro += "Space:5" }
Nav $intro "$Tag-intro"
$play = @(); for ($i = 0; $i -lt [math]::Ceiling($MatchSeconds / 10); $i++) { $play += "wait:10"; $play += "shot" }
Nav $play "$Tag-match"
"alive=$(Alive)"
