Add-Type -AssemblyName System.Drawing

$srcPath = if ($args[0]) { $args[0] } else { "$PSScriptRoot\concept4_source.jpg" }
$dstPng  = if ($args[1]) { $args[1] } else { "$PSScriptRoot\app_icon.png" }
$dstIco  = if ($args[2]) { $args[2] } else { "$PSScriptRoot\app.ico" }

if (-not (Test-Path $srcPath)) {
    Write-Error "Source image not found: $srcPath"
    exit 1
}

$src = [System.Drawing.Bitmap]::FromFile($srcPath)

# Точные границы Fluent 2 сквиркла внутри исходника (без окружающего белого фона)
$sqX = 183
$sqY = 182
$sqSize = 658

# 1. Извлекаем сквиркл 658x658 в 96 DPI буфер
$cropBmp = New-Object System.Drawing.Bitmap($sqSize, $sqSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$cropBmp.SetResolution(96, 96)
$cg = [System.Drawing.Graphics]::FromImage($cropBmp)
$cg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$cg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$cg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

$destCrop = New-Object System.Drawing.Rectangle(0, 0, $sqSize, $sqSize)
$srcCrop = New-Object System.Drawing.Rectangle($sqX, $sqY, $sqSize, $sqSize)
$cg.DrawImage($src, $destCrop, $srcCrop, [System.Drawing.GraphicsUnit]::Pixel)
$cg.Dispose()

# 2. Создаем мастер-иконку 512x512 во весь рост на чистом прозрачном фоне
$targetSize = 512
$bmp = New-Object System.Drawing.Bitmap($targetSize, $targetSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$bmp.SetResolution(96, 96)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.Clear([System.Drawing.Color]::Transparent)

# Сквиркл во весь рост (2px субпиксельный запас под антиалиасинг углов)
$margin = 2
$bodySize = $targetSize - ($margin * 2)
# Радиус скругления Fluent 2 (18.2% от размера)
$radius = [int]($bodySize * 0.182)
$d = $radius * 2

$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$path.AddArc($margin, $margin, $d, $d, 180, 90)
$path.AddArc($margin + $bodySize - $d, $margin, $d, $d, 270, 90)
$path.AddArc($margin + $bodySize - $d, $margin + $bodySize - $d, $d, $d, 0, 90)
$path.AddArc($margin, $margin + $bodySize - $d, $d, $d, 90, 90)
$path.CloseFigure()

$g.SetClip($path)
$destFinal = New-Object System.Drawing.Rectangle($margin, $margin, $bodySize, $bodySize)
$g.DrawImage($cropBmp, $destFinal, 0, 0, $sqSize, $sqSize, [System.Drawing.GraphicsUnit]::Pixel)
$g.ResetClip()

$g.Dispose()
$path.Dispose()
$cropBmp.Dispose()
$src.Dispose()

# Сохраняем PNG для GitHub и репозитория
$bmp.Save($dstPng, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "PNG saved: $dstPng (512x512, transparent background, full height)"

# Размеры для ICO: стандартный набор Windows (16, 24, 32, 48, 64, 128, 256)
$sizes = @(16, 24, 32, 48, 64, 128, 256)

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)

$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$sizes.Count)

$bmpDataList = New-Object System.Collections.ArrayList

foreach ($s in $sizes) {
    $scaled = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $scaled.SetResolution(96, 96)
    $sg = [System.Drawing.Graphics]::FromImage($scaled)
    $sg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $sg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $sg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $sg.Clear([System.Drawing.Color]::Transparent)
    $sDest = New-Object System.Drawing.Rectangle(0, 0, $s, $s)
    $sg.DrawImage($bmp, $sDest, 0, 0, $targetSize, $targetSize, [System.Drawing.GraphicsUnit]::Pixel)
    $sg.Dispose()

    $lockRect = New-Object System.Drawing.Rectangle(0, 0, $s, $s)
    $bmpData = $scaled.LockBits($lockRect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = [Math]::Abs($bmpData.Stride)
    $bytes = New-Object byte[] ($stride * $s)
    [System.Runtime.InteropServices.Marshal]::Copy($bmpData.Scan0, $bytes, 0, $bytes.Length)
    $scaled.UnlockBits($bmpData)

    # ICO BMP data is bottom-up
    $flipped = New-Object byte[] ($bytes.Length)
    for ($row = 0; $row -lt $s; $row++) {
        $srcOff = $row * $stride
        $dstOff = ($s - 1 - $row) * $stride
        [Array]::Copy($bytes, $srcOff, $flipped, $dstOff, $stride)
    }

    # AND mask (1 bit per pixel, padded to 32 bits per row)
    $andMaskRowBytes = [int]([Math]::Ceiling($s / 32.0)) * 4
    $andMaskSize = $andMaskRowBytes * $s
    $andMask = New-Object byte[] $andMaskSize

    $dibMs = New-Object System.IO.MemoryStream
    $dibBw = New-Object System.IO.BinaryWriter($dibMs)

    # BITMAPINFOHEADER
    $dibBw.Write([uint32]40)
    $dibBw.Write([int32]$s)
    $dibBw.Write([int32]($s * 2))
    $dibBw.Write([uint16]1)
    $dibBw.Write([uint16]32)
    $dibBw.Write([uint32]0)
    $dibBw.Write([uint32]($flipped.Length + $andMaskSize))
    $dibBw.Write([int32]0)
    $dibBw.Write([int32]0)
    $dibBw.Write([uint32]0)
    $dibBw.Write([uint32]0)

    $dibBw.Write($flipped)
    $dibBw.Write($andMask)
    $dibBw.Flush()
    $dibData = $dibMs.ToArray()
    $null = $bmpDataList.Add($dibData)
    $dibBw.Dispose()
    $dibMs.Dispose()

    # Для 256 в ICONDIRENTRY ширина и высота записываются как 0
    $iconDim = if ($s -ge 256) { 0 } else { [byte]$s }
    $bw.Write([byte]$iconDim)
    $bw.Write([byte]$iconDim)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$dibData.Length)
    $bw.Write([uint32]0)

    $scaled.Dispose()
}

$headerSize = 6 + 16 * $sizes.Count
$offset = $headerSize
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $ms.Position = 6 + 16 * $i + 12
    $bw.Write([uint32]$offset)
    $offset += $bmpDataList[$i].Length
}

$ms.Position = $ms.Length
foreach ($dd in $bmpDataList) { $bw.Write($dd) }

[System.IO.File]::WriteAllBytes($dstIco, $ms.ToArray())
$bw.Dispose()
$ms.Dispose()
$bmp.Dispose()

Write-Host "ICO saved: $dstIco ($($sizes.Count) sizes including 256x256, transparent corners, full height)"

# Также синхронизируем dist/app.ico
$distIco = Join-Path (Split-Path $PSScriptRoot -Parent) "dist\app.ico"
if (Test-Path (Split-Path $distIco -Parent)) {
    Copy-Item $dstIco -Destination $distIco -Force
    Write-Host "Synced to dist: $distIco"
}
