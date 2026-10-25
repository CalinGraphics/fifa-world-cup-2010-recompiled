param([string[]]$Variants = @(""), [int]$Samples = 45, [string]$Config = "release", [string[]]$Base = @("--resolution_scale=2"))
# For each variant (a space-separated list of extra switches), opens Training Ground and
# samples the window once a second: prints how many samples were all black.
# Results also go to out\dev\black_test.txt.
. "$PSScriptRoot\_paths.ps1"
$out = Join-Path $Work "black_test.txt"
$n = 0
foreach ($v in $Variants) {
  $n++
  $extra = $Base + @($v.Split(" ", [StringSplitOptions]::RemoveEmptyEntries))
  $r = & "$PSScriptRoot\to_training.ps1" -Config $Config -Tag "bt$n" -Seconds 0 -Extra $extra
  $ok = ($r -join " ") -match "training ground reached: True"
  # Play while sampling: run, shoot, run the other way, pass, sprint.
  $keys = @("W", "Backspace", "S", "A", "Space", "D", "E", "Backspace")
  $res = if ($ok) { & "$PSScriptRoot\burst.ps1" -Tag "bt$n-s" -Count $Samples -GapMs 600 -Keys $keys } else { "navigation failed" }
  Get-Process fifawc10 -ErrorAction SilentlyContinue | Stop-Process -Force
  $line = "[$v] $res"
  $line
  Add-Content $out $line
  Start-Sleep 2
}
