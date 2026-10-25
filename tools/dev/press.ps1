param([string[]]$Keys = @("Return"), [int]$HoldMs = 120, [int]$GapMs = 600, [string]$Process = "fifawc10")
Add-Type @"
using System; using System.Runtime.InteropServices;
public class K { [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
 [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint type);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
 [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h); }
"@
$vk = @{ Return = 0x0D; Space = 0x20; Escape = 0x1B; Up = 0x26; Down = 0x28; Left = 0x25; Right = 0x27;
         X = 0x58; Z = 0x5A; Tab = 0x09; Backspace = 0x08; W = 0x57; A = 0x41; S = 0x53; D = 0x44; E = 0x45; Q = 0x51 }
$p = Get-Process $Process -ErrorAction SilentlyContinue | Select -First 1
if (-not $p) { "game not running"; exit 1 }
$h = $p.MainWindowHandle
& "$PSScriptRoot\focus.ps1" -Process $Process | Out-Null
if ([K]::GetForegroundWindow() -ne $h) { "could not focus the game window"; exit 2 }
Start-Sleep -Milliseconds 150
foreach ($k in $Keys) {
  $code = [byte]$vk[$k]
  $scan = [byte][K]::MapVirtualKey($code, 0)
  $ext = if ($code -in 0x25..0x28) { 1 } else { 0 }
  [K]::keybd_event($code, $scan, $ext, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds $HoldMs
  [K]::keybd_event($code, $scan, 2 -bor $ext, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds $GapMs
}
"pressed $($Keys -join ',')"
