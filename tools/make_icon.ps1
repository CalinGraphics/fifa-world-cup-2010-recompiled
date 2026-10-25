# Draws the project icon (a ball-like badge with "10") and writes launcher\icon.ico
# with PNG frames at 256, 64, 48, 32 and 16 px.
Add-Type -AssemblyName System.Drawing
$out = Join-Path (Split-Path (Split-Path $PSScriptRoot)) "launcher\icon.ico"

function Frame([int]$n) {
  $b = New-Object Drawing.Bitmap($n, $n)
  $g = [Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = "AntiAlias"; $g.TextRenderingHint = "AntiAliasGridFit"
  $pad = [math]::Max(1, $n * 0.04)
  $g.FillEllipse((New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(255, 255, 182, 18))), $pad, $pad, $n - 2 * $pad, $n - 2 * $pad)
  $in = $n * 0.12
  $g.FillEllipse((New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(255, 15, 74, 51))), $in, $in, $n - 2 * $in, $n - 2 * $in)
  $font = New-Object Drawing.Font("Segoe UI", [single]($n * 0.36), [Drawing.FontStyle]::Bold, [Drawing.GraphicsUnit]::Pixel)
  $fmt = New-Object Drawing.StringFormat; $fmt.Alignment = "Center"; $fmt.LineAlignment = "Center"
  $g.DrawString("10", $font, [Drawing.Brushes]::White, (New-Object Drawing.RectangleF(0, ($n * 0.02), $n, $n)), $fmt)
  $g.Dispose()
  $ms = New-Object IO.MemoryStream
  $b.Save($ms, [Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return ,$ms.ToArray()
}

$sizes = 256, 64, 48, 32, 16
$frames = $sizes | % { ,(Frame $_) }
$fs = [IO.File]::Create($out); $w = New-Object IO.BinaryWriter($fs)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $s = $sizes[$i]; $d = $frames[$i]
  $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s }))); $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
  $w.Write([byte]0); $w.Write([byte]0); $w.Write([uint16]1); $w.Write([uint16]32)
  $w.Write([uint32]$d.Length); $w.Write([uint32]$offset); $offset += $d.Length
}
foreach ($d in $frames) { $w.Write($d) }
$w.Close()
"wrote $out"
