[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectRoot
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$assetRoot = Join-Path $ProjectRoot 'assets'
New-Item -ItemType Directory -Path $assetRoot -Force | Out-Null

function New-LogoPngBytes {
    param([int]$Size)

    $renderSize = $Size * 4
    $bitmap = New-Object System.Drawing.Bitmap($renderSize, $renderSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bitmap.SetResolution(384, 384)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $s = [single]$renderSize
    $outer = New-Object System.Drawing.RectangleF(($s * 0.055), ($s * 0.055), ($s * 0.89), ($s * 0.89))
    $face = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $outer,
        [System.Drawing.Color]::FromArgb(255, 15, 29, 52),
        [System.Drawing.Color]::FromArgb(255, 27, 66, 83),
        45.0
    )
    $graphics.FillEllipse($face, $outer)

    $ringRect = New-Object System.Drawing.RectangleF(($s * 0.145), ($s * 0.145), ($s * 0.71), ($s * 0.71))
    $ring = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 103, 220, 205), ($s * 0.072))
    $ring.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $ring.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawArc($ring, $ringRect, -62, 302)

    $hand = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 246, 249, 250), ($s * 0.058))
    $hand.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $hand.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $center = $s * 0.5
    $graphics.DrawLine($hand, $center, $center, $center, ($s * 0.29))
    $graphics.DrawLine($hand, $center, $center, ($s * 0.68), ($s * 0.61))

    $hubBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 246, 249, 250))
    $graphics.FillEllipse($hubBrush, ($s * 0.465), ($s * 0.465), ($s * 0.07), ($s * 0.07))

    $accentBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 133, 100))
    $graphics.FillEllipse($accentBrush, ($s * 0.755), ($s * 0.145), ($s * 0.13), ($s * 0.13))

    $graphics.Dispose()
    $face.Dispose()
    $ring.Dispose()
    $hand.Dispose()
    $hubBrush.Dispose()
    $accentBrush.Dispose()

    $finalBitmap = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $finalGraphics = [System.Drawing.Graphics]::FromImage($finalBitmap)
    $finalGraphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $finalGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $finalGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $finalGraphics.DrawImage($bitmap, 0, 0, $Size, $Size)
    $finalGraphics.Dispose()
    $bitmap.Dispose()

    $stream = New-Object System.IO.MemoryStream
    $finalBitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $finalBitmap.Dispose()
    $bytes = $stream.ToArray()
    $stream.Dispose()
    Write-Output -NoEnumerate $bytes
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$images = @()
foreach ($size in $sizes) {
    $images += [pscustomobject]@{ Size = $size; Bytes = (New-LogoPngBytes -Size $size) }
}

$iconPath = Join-Path $assetRoot 'DesktopCountdown.ico'
$iconStream = [System.IO.File]::Create($iconPath)
$writer = New-Object System.IO.BinaryWriter($iconStream)
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$images.Count)
$offset = 6 + (16 * $images.Count)

foreach ($image in $images) {
    $dimension = if ($image.Size -ge 256) { [byte]0 } else { [byte]$image.Size }
    $writer.Write($dimension)
    $writer.Write($dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$image.Bytes.Length)
    $writer.Write([uint32]$offset)
    $offset += $image.Bytes.Length
}
foreach ($image in $images) { $writer.Write($image.Bytes) }
$writer.Dispose()
$iconStream.Dispose()

[System.IO.File]::WriteAllBytes((Join-Path $assetRoot 'logo-256.png'), ($images | Where-Object Size -eq 256).Bytes)
Write-Output "BRAND_ASSETS_OK=$iconPath"
