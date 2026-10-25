param([string]$Process = "fifawc10")
# Brings a process's main window to the foreground (attaches to the current foreground
# thread's input first, which Windows requires for a background caller).
Add-Type @"
using System; using System.Runtime.InteropServices;
public class Fg {
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr pid);
 [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
 [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attach);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
 [DllImport("user32.dll")] public static extern IntPtr SetFocus(IntPtr h);
 [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
 [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
 public static bool Focus(IntPtr h) {
   if (GetForegroundWindow() == h) return true;
   if (IsIconic(h)) ShowWindow(h, 9);
   uint fg = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
   uint me = GetCurrentThreadId();
   uint target = GetWindowThreadProcessId(h, IntPtr.Zero);
   AttachThreadInput(me, fg, true); AttachThreadInput(target, fg, true);
   keybd_event(0x12, 0, 0, UIntPtr.Zero); keybd_event(0x12, 0, 2, UIntPtr.Zero);
   BringWindowToTop(h); SetForegroundWindow(h); SetFocus(h);
   AttachThreadInput(target, fg, false); AttachThreadInput(me, fg, false);
   return GetForegroundWindow() == h;
 } }
"@
$p = Get-Process $Process -ErrorAction SilentlyContinue | Select -First 1
if (-not $p) { "not running"; exit 1 }
for ($i = 0; $i -lt 5; $i++) { if ([Fg]::Focus($p.MainWindowHandle)) { "focused"; exit 0 }; Start-Sleep -Milliseconds 300 }
"could not focus"; exit 2
