# Builds the README title images and hero image from the logo and the rendered screenshots.
# Run with Windows PowerShell 5.1 (System.Drawing is built in), after rendering the pages:
#   resonance-cli __screenshot <repo>\docs\images dark
#   resonance-cli __screenshot <repo>\docs\images\light light
#   powershell -ExecutionPolicy Bypass -File docs\make-readme-art.ps1
param(
    [string]$Dark = "$PSScriptRoot\images",
    [string]$Light = "$PSScriptRoot\images\light",
    [string]$Icon = "$PSScriptRoot\..\assets\resonance.ico",
    [string]$Out = "$PSScriptRoot\images"
)
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

function Get-IconPng([string]$path) {
    # The largest entry of the .ico is stored as a PNG.
    $b = [IO.File]::ReadAllBytes($path)
    $count = [BitConverter]::ToUInt16($b, 4)
    $bestSize = 0; $bestOffset = 0
    for ($i = 0; $i -lt $count; $i++) {
        $e = 6 + 16 * $i
        $size = [BitConverter]::ToUInt32($b, $e + 8)
        if ($size -gt $bestSize) { $bestSize = $size; $bestOffset = [BitConverter]::ToUInt32($b, $e + 12) }
    }
    $ms = New-Object IO.MemoryStream (, $b[$bestOffset..($bestOffset + $bestSize - 1)])
    New-Object Drawing.Bitmap $ms
}

function New-RoundedPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x, $y, 2 * $r, 2 * $r, 180, 90)
    $p.AddArc($x + $w - 2 * $r, $y, 2 * $r, 2 * $r, 270, 90)
    $p.AddArc($x + $w - 2 * $r, $y + $h - 2 * $r, 2 * $r, 2 * $r, 0, 90)
    $p.AddArc($x, $y + $h - 2 * $r, 2 * $r, 2 * $r, 90, 90)
    $p.CloseFigure()
    $p
}

function Set-Quality($g) {
    $g.SmoothingMode = 'AntiAlias'
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.PixelOffsetMode = 'HighQuality'
    $g.CompositingQuality = 'HighQuality'
}

function Save-Title([string]$file, [Drawing.Color]$color) {
    $logo = Get-IconPng $Icon
    $bmp = New-Object Drawing.Bitmap 1120, 260
    $g = [Drawing.Graphics]::FromImage($bmp)
    Set-Quality $g
    $g.TextRenderingHint = 'AntiAlias'
    $g.DrawImage($logo, 10, 20, 220, 220)
    $font = New-Object Drawing.Font 'Segoe UI Semibold', 140, ([Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object Drawing.StringFormat; $fmt.LineAlignment = 'Center'
    $g.DrawString('Resonance', $font, (New-Object Drawing.SolidBrush $color), (New-Object Drawing.RectangleF 250, 0, 870, 260), $fmt)
    $bmp.Save($file, [Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose(); $logo.Dispose()
}

function Draw-Window($g, [string]$file, [single]$x, [single]$y, [single]$w) {
    $img = [Drawing.Image]::FromFile($file)
    $h = [single]($w * $img.Height / $img.Width)
    # Soft shadow: stacked translucent shapes.
    for ($i = 14; $i -ge 1; $i--) {
        $shadow = New-RoundedPath ($x - $i) ($y - $i + 12) ($w + 2 * $i) ($h + 2 * $i) (16 + $i)
        $g.FillPath((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(9, 0, 0, 20))), $shadow)
    }
    $clip = New-RoundedPath $x $y $w $h 14
    $g.SetClip($clip)
    $g.DrawImage($img, $x, $y, $w, $h)
    $g.ResetClip()
    $g.DrawPath((New-Object Drawing.Pen ([Drawing.Color]::FromArgb(70, 255, 255, 255)), 1.5), $clip)
    $img.Dispose()
}

Save-Title "$Out\title-dark.png" ([Drawing.Color]::White)
Save-Title "$Out\title-light.png" ([Drawing.Color]::FromArgb(255, 24, 24, 27))

# Hero: both themes over a colourful gradient, with the connection card in front.
$W = 1800; $H = 1010
$hero = New-Object Drawing.Bitmap $W, $H
$g = [Drawing.Graphics]::FromImage($hero)
Set-Quality $g
$rect = New-Object Drawing.Rectangle 0, 0, $W, $H
$grad = New-Object Drawing.Drawing2D.LinearGradientBrush $rect, ([Drawing.Color]::Black), ([Drawing.Color]::Black), 35.0
$blend = New-Object Drawing.Drawing2D.ColorBlend 3
$blend.Colors = @([Drawing.Color]::FromArgb(255, 124, 58, 237), [Drawing.Color]::FromArgb(255, 37, 99, 235), [Drawing.Color]::FromArgb(255, 6, 182, 212))
$blend.Positions = @([single]0, [single]0.5, [single]1)
$grad.InterpolationColors = $blend
$g.FillRectangle($grad, $rect)
Draw-Window $g "$Light\sound.png" 70 60 1100
Draw-Window $g "$Dark\equalizer.png" 620 170 1110
$card = [Drawing.Image]::FromFile("$Dark\card.png")
$g.DrawImage($card, [single]40, [single]($H - $card.Height - 10), [single]$card.Width, [single]$card.Height)
$card.Dispose()
$hero.Save("$Out\hero.png", [Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $hero.Dispose()
"title-dark.png, title-light.png and hero.png written to $Out"
