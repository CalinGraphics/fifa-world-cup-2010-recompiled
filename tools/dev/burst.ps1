param([int]$Count = 40, [int]$GapMs = 150, [string]$Tag = "burst", [switch]$Screen, [string[]]$Keys = @())
# -Keys: pressed in turn, one before each capture (to keep the game busy: run, shoot, pass).
# Captures the game window $Count times, $GapMs apart, and reports the share of all-black frames
# (black: the centre and four other sample points are all near zero).
# -Screen copies the window's area from the desktop instead of PrintWindow.
. "$PSScriptRoot\_paths.ps1"
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public class WB {
 [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out R r);
 [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref P p);
 [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 public struct R { public int L, T, Rt, B; } public struct P { public int X, Y; } }
"@
[WB]::SetProcessDPIAware() | Out-Null
$black = 0; $marks = ""
for ($i = 0; $i -lt $Count; $i++) {
  if ($Keys) { & "$PSScriptRoot\press.ps1" -Keys $Keys[$i % $Keys.Count] -HoldMs 400 -GapMs 0 | Out-Null }
  $f = Join-Path $Work ("{0}-{1:D2}.png" -f $Tag, $i)
  if ($Screen) {
    $h = (Get-Process fifawc10 | Select -First 1).MainWindowHandle
    $r = New-Object WB+R; [WB]::GetClientRect($h, [ref]$r) | Out-Null
    $p = New-Object WB+P; [WB]::ClientToScreen($h, [ref]$p) | Out-Null
    $bm = New-Object Drawing.Bitmap ($r.Rt), ($r.B)
    $g = [Drawing.Graphics]::FromImage($bm); $g.CopyFromScreen($p.X, $p.Y, 0, 0, $bm.Size); $g.Dispose()
    $bm.Save($f); $bm.Dispose()
  } else {
    & "$PSScriptRoot\screenshot.ps1" -Out $f | Out-Null
  }
  $b = [Drawing.Bitmap]::FromFile($f)
  $dark = $true
  foreach ($q in @(@(0.5, 0.5), @(0.25, 0.3), @(0.75, 0.3), @(0.25, 0.7), @(0.75, 0.7))) {
    $c = $b.GetPixel([int]($q[0] * ($b.Width - 1)), [int]($q[1] * ($b.Height - 1)))
    if ($c.R + $c.G + $c.B -gt 12) { $dark = $false }
  }
  $b.Dispose()
  if ($dark) { $black++; $marks += "#" } else { $marks += "." }
  Start-Sleep -Milliseconds $GapMs
}
"black $black / $Count   $marks"
