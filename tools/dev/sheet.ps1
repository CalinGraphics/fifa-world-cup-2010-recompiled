param([string]$Tag, [int]$Cols = 3, [int]$W = 426, [int]$H = 240)
# Puts out\dev\<Tag>-NN.png screenshots on one numbered contact sheet: out\dev\<Tag>-sheet.png
. "$PSScriptRoot\_paths.ps1"
Add-Type -AssemblyName System.Drawing
$files = Get-ChildItem $Work -Filter "$Tag-??.png" | Sort-Object Name
$rows = [math]::Ceiling($files.Count / $Cols)
$sheet = New-Object Drawing.Bitmap(($W * $Cols), ($H * [math]::Max(1, $rows)))
$g = [Drawing.Graphics]::FromImage($sheet)
$font = New-Object Drawing.Font("Arial", 16)
$i = 0
foreach ($f in $files) {
  $img = [Drawing.Image]::FromFile($f.FullName)
  $x = ($i % $Cols) * $W; $y = [math]::Floor($i / $Cols) * $H
  $g.DrawImage($img, $x, $y, $W, $H)
  $g.DrawString($f.BaseName.Split("-")[-1], $font, [Drawing.Brushes]::Yellow, $x + 4, $y + 4)
  $img.Dispose(); $i++
}
$out = Join-Path $Work "$Tag-sheet.png"
$sheet.Save($out); $g.Dispose(); $sheet.Dispose()
"saved $out ($i shots)"
