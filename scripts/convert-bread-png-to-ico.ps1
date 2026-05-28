# One-shot script: converts Assets\Brand\bread.png into a multi-resolution
# Assets\Brand\bread.ico. Re-run if you change the source PNG.
#
# Output format: ICO with PNG-encoded frames at 16, 24, 32, 48, 64, 128, 256.
# Vista+ Windows accepts PNG-embedded ICO frames; older Windows is not a target.

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$srcPath = Join-Path $root 'src\PrimeOSTuner.UI\Assets\Brand\bread.png'
$icoPath = Join-Path $root 'src\PrimeOSTuner.UI\Assets\Brand\bread.ico'

$src = [System.Drawing.Bitmap]::FromFile($srcPath)
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngBytes = @()

foreach ($s in $sizes) {
    $b = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($b)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($src, 0, 0, $s, $s)
    $g.Dispose()

    $ms = New-Object IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBytes += , $ms.ToArray()
    $ms.Dispose()
    $b.Dispose()
}
$src.Dispose()

$out = [IO.File]::Create($icoPath)
$bw = New-Object IO.BinaryWriter $out

# ICONDIR header
$bw.Write([uint16]0)              # reserved
$bw.Write([uint16]1)              # type = icon
$bw.Write([uint16]$sizes.Length)  # image count

$dataOffset = 6 + (16 * $sizes.Length)
for ($i = 0; $i -lt $sizes.Length; $i++) {
    $sz = $sizes[$i]
    # ICONDIRENTRY: width/height are 0 when the dimension is 256 (max)
    if ($sz -ge 256) { $w = 0 } else { $w = $sz }
    if ($sz -ge 256) { $h = 0 } else { $h = $sz }
    $bw.Write([byte]$w)            # width
    $bw.Write([byte]$h)            # height
    $bw.Write([byte]0)             # color palette
    $bw.Write([byte]0)             # reserved
    $bw.Write([uint16]1)           # planes
    $bw.Write([uint16]32)          # bpp
    $bw.Write([uint32]$pngBytes[$i].Length)
    $bw.Write([uint32]$dataOffset)
    $dataOffset += $pngBytes[$i].Length
}

foreach ($d in $pngBytes) { $bw.Write($d) }

$bw.Close()
$out.Close()

Write-Host "bread.ico written ($((Get-Item $icoPath).Length) bytes, $($sizes.Length) frames)"
