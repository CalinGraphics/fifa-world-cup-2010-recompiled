param([string[]]$Extra = @(), [string]$Tag = "train", [string]$Config = "release", [int]$Seconds = 40)
# Boots the game with a fresh profile and opens Training Ground from the main menu,
# then takes a screenshot every 10 s. Screenshots: out\dev\<Tag>-*.png and <Tag>-sheet.png.
. "$PSScriptRoot\_paths.ps1"
$saves = Join-Path $Work "saves-$Tag"
Remove-Item $saves -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $saves | Out-Null
Remove-Item (Join-Path $Work "$Tag-*.png") -ErrorAction SilentlyContinue
Add-Type -AssemblyName System.Drawing
# True when the profile setup panel is up: its orange title bar at the top, dark panel below.
function ProfilePanel {
  $f = Join-Path $Work "$Tag-probe.png"
  & "$PSScriptRoot\screenshot.ps1" -Out $f | Out-Null
  if (-not (Test-Path $f)) { return $false }
  $b = [Drawing.Bitmap]::FromFile($f)
  $t = $b.GetPixel([int](640 * $b.Width / 1280), [int](80 * $b.Height / 720))
  $p = $b.GetPixel([int](1000 * $b.Width / 1280), [int](560 * $b.Height / 720))
  $b.Dispose(); Remove-Item $f -ErrorAction SilentlyContinue
  return ($t.R -gt 140 -and $t.G -gt 35 -and $t.G -lt 90 -and $t.B -lt 40 -and $p.R -lt 70 -and $p.G -lt 35)
}
function Nav([string[]]$s, [string]$t) { & "$PSScriptRoot\nav.ps1" -Tag $t -Steps $s | Out-Null }

& "$PSScriptRoot\nav.ps1" -Config $Config -Start -Tag "$Tag-boot" -BootSeconds 25 `
  -Extra (@("--user_data_root", "`"$saves`"") + $Extra) -Steps @("Space:4") | Out-Null
# Language screen and title: press A / Start until the dark profile panel covers the centre.
$ok = $false
for ($i = 0; $i -lt 30 -and -not $ok; $i++) {
  Nav @(@("Space:2", "Return:2")[$i % 2]) "$Tag-title"
  if (ProfilePanel) { $ok = $true }
}
"profile setup reached: $ok"
if (-not $ok) { exit 1 }
Nav @("Space:4", "Space:4", "Space:4", "Space:4",                 # profile check, skill, controls, team
      "Space:4", "Space:4", "Space:4") "$Tag-profile"              # hospitality, autosave, OK
# The main menu: its orange background and the dark brown of the globe's rim.
function MainMenu {
  $f = Join-Path $Work "$Tag-probe.png"
  & "$PSScriptRoot\screenshot.ps1" -Out $f | Out-Null
  if (-not (Test-Path $f)) { return $false }
  $b = [Drawing.Bitmap]::FromFile($f)
  $a = $b.GetPixel([int](60 * $b.Width / 1280), [int](300 * $b.Height / 720))
  $g = $b.GetPixel([int](1150 * $b.Width / 1280), [int](150 * $b.Height / 720))
  $l = $b.GetPixel([int](330 * $b.Width / 1280), [int](140 * $b.Height / 720))   # the logo's red box
  $b.Dispose(); Remove-Item $f -ErrorAction SilentlyContinue
  return ($a.R -gt 225 -and $a.G -gt 125 -and $a.G -lt 170 -and $a.B -lt 30 -and
          $g.R -gt 140 -and $g.R -lt 185 -and $g.G -gt 65 -and $g.G -lt 100 -and $g.B -lt 25 -and
          $l.R -gt 140 -and $l.G -lt 80 -and $l.B -lt 40)
}
$menu = $false
for ($i = 0; $i -lt 8 -and -not $menu; $i++) {
  # A press can get lost while a dialog fades in: if the menu isn't up after a few
  # seconds, confirm once more. Waiting first keeps a press from landing on the menu
  # as it slides in (it would pick Kick-Off).
  Start-Sleep 4
  if (MainMenu) { $menu = $true } elseif ($i -lt 3) { Nav @("Space:1") "$Tag-wait" }
}
"main menu reached: $menu"
if (-not $menu) { exit 1 }
Start-Sleep 2
Nav @("S:1.2", "S:1.2", "S:1.2", "S:1.2", "S:1.2", "S:1.2", "S:2", "Space:20") "$Tag-menu"
$inTraining = -not (MainMenu)
"training ground reached: $inTraining"
$steps = @()
for ($i = 0; $i -lt [math]::Ceiling($Seconds / 10); $i++) { $steps += "wait:10"; $steps += "shot" }
if ($steps) { & "$PSScriptRoot\nav.ps1" -Tag $Tag -Steps $steps }
& "$PSScriptRoot\sheet.ps1" -Tag $Tag -Cols 6
