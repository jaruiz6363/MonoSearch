# make-icon.ps1 — draws the application icon: icon.png (256 px) and icon.ico (the exe, the window, the
# installer, shortcuts and .monosearch files).
#
# The picture is a monocentric lens: an outer shell and a ball about one centre, three ray bundles
# focusing on a concentric curved image. Run it again only to change the picture; the outputs are committed.
#
#   pwsh src/MonoSearch.App/Assets/make-icon.ps1

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path

function Draw-Icon([int]$Size) {
    $bmp = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = $Size / 256.0

    # Rounded square background.
    $r = 48 * $s; $m = 8 * $s; $w = $Size - 2 * $m
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($m, $m, 2 * $r, 2 * $r, 180, 90)
    $path.AddArc($m + $w - 2 * $r, $m, 2 * $r, 2 * $r, 270, 90)
    $path.AddArc($m + $w - 2 * $r, $m + $w - 2 * $r, 2 * $r, 2 * $r, 0, 90)
    $path.AddArc($m, $m + $w - 2 * $r, 2 * $r, 2 * $r, 90, 90)
    $path.CloseFigure()
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.PointF 0, 0), (New-Object System.Drawing.PointF 0, $Size), ([System.Drawing.Color]::FromArgb(255, 18, 32, 64)), ([System.Drawing.Color]::FromArgb(255, 10, 18, 38))
    $g.FillPath($bg, $path)
    $g.SetClip($path)

    # Lens about the centre (cx, cy): shell radius R1, ball radius R2; image arc radius RI.
    $cx = 96 * $s; $cy = 128 * $s; $R1 = 70 * $s; $R2 = 32 * $s; $RI = 126 * $s

    # Three fields: each a cone from the rear of the shell to its focus on the image arc.
    $colours = @(
        [System.Drawing.Color]::FromArgb(255, 255, 255, 255),
        [System.Drawing.Color]::FromArgb(255, 255, 196, 64),
        [System.Drawing.Color]::FromArgb(255, 255, 110, 140))
    $angles = @(0, -34, 34)
    for ($i = 0; $i -lt 3; $i++) {
        $a = $angles[$i] * [Math]::PI / 180
        $fx = $cx + $RI * [Math]::Cos($a); $fy = $cy + $RI * [Math]::Sin($a)
        $e1 = $a - 0.42; $e2 = $a + 0.42
        $cone = New-Object System.Drawing.Drawing2D.GraphicsPath
        $cone.AddPolygon([System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF ($cx + $R1 * [Math]::Cos($e1)), ($cy + $R1 * [Math]::Sin($e1))),
            (New-Object System.Drawing.PointF ($cx + $R1 * [Math]::Cos($e2)), ($cy + $R1 * [Math]::Sin($e2))),
            (New-Object System.Drawing.PointF $fx, $fy)))
        $c = $colours[$i]
        $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(200, $c.R, $c.G, $c.B))), $cone)
    }

    $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 150, 196, 236))), $cx - $R1, $cy - $R1, 2 * $R1, 2 * $R1)
    $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 70, 130, 200))), $cx - $R2, $cy - $R2, 2 * $R2, 2 * $R2)

    # The curved image surface.
    $img = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 230, 236, 245)), (9 * $s)
    $img.StartCap = 'Round'; $img.EndCap = 'Round'
    $g.DrawArc($img, $cx - $RI, $cy - $RI, 2 * $RI, 2 * $RI, -42, 84)

    $g.Dispose()
    return $bmp
}

function Png-Bytes([int]$Size) {
    $bmp = Draw-Icon $Size
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

[System.IO.File]::WriteAllBytes((Join-Path $Here 'icon.png'), (Png-Bytes 256))

# icon.ico: a directory of PNG images (Windows Vista and later read PNG entries).
$sizes = 16, 24, 32, 48, 64, 128, 256
$images = $sizes | ForEach-Object { , (Png-Bytes $_) }
$ico = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ico
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $d = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $bw.Write([byte]$d); $bw.Write([byte]$d); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$images[$i].Length); $bw.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $bw.Write($img) }
$bw.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $Here 'icon.ico'), $ico.ToArray())

Write-Host "Wrote icon.png and icon.ico in $Here"
