param([string]$Out = "$(Split-Path (Split-Path $PSScriptRoot))\out\dev\shot.png", [string]$Process = "fifawc10")
# Captures the game window's own content (PrintWindow, full render), which also works
# for a D3D12 flip-model swap chain where a screen copy comes out black.
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public class W {
 [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out R r);
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
 [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 public struct R { public int L, T, Rt, B; } }
"@
[W]::SetProcessDPIAware() | Out-Null
$p = Get-Process $Process -ErrorAction SilentlyContinue | Select -First 1
if (-not $p -or $p.MainWindowHandle -eq 0) { "game window not found"; exit 1 }
$r = New-Object W+R; [W]::GetClientRect($p.MainWindowHandle, [ref]$r) | Out-Null
$w = [math]::Max(1, $r.Rt - $r.L); $h = [math]::Max(1, $r.B - $r.T)
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$dc = $g.GetHdc()
# 1 = PW_CLIENTONLY, 2 = PW_RENDERFULLCONTENT
$ok = [W]::PrintWindow($p.MainWindowHandle, $dc, 3)
$g.ReleaseHdc($dc)
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
"saved $Out (${w}x${h}, ok=$ok)"
