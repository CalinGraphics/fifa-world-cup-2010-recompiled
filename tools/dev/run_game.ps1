param([int]$Seconds = 40, [string]$LogLevel = "info", [string]$Config = "release", [string[]]$Extra = @(), [switch]$Keep, [string]$Game = "")
# Starts fifawc10.exe from a build (release, relwithdebinfo) or the package, logging to out\dev\run.log.
. "$PSScriptRoot\_paths.ps1"
$bin = if ($Config -eq "package") { Join-Path $Repo "out\package\FIFA World Cup 2010 Recompiled" } else { Join-Path $Repo "out\build\win-amd64-$Config" }
if (-not $Game) { $Game = $DefaultGame }
$log = Join-Path $Work "run.log"
Get-Process fifawc10 -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item $log -ErrorAction SilentlyContinue
$args = @("--game_data_root", "`"$Game`"", "--log_file", "`"$log`"", "--log_level", $LogLevel) + $Extra
$p = Start-Process (Join-Path $bin "fifawc10.exe") -WorkingDirectory $bin -ArgumentList $args -PassThru
for ($i = 0; $i -lt $Seconds; $i++) {
  Start-Sleep 1
  if ($p.HasExited) { break }
}
if ($p.HasExited) { "EXITED code=$($p.ExitCode) after ${i}s" }
elseif (-not $Keep) { Stop-Process -Id $p.Id -Force; "STILL RUNNING after ${Seconds}s (killed)" }
else { "STILL RUNNING after ${Seconds}s (left open, pid $($p.Id))" }
