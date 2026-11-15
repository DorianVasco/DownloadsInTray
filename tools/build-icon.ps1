# Renders src\DownloadsInTray\Assets\icon.svg into the app's .ico (16 to 256 px, PNG-compressed).
# Run with Windows PowerShell (powershell.exe), which renders WPF on an STA thread:
#   powershell -NoProfile -File tools\build-icon.ps1
# Handles what icon.svg uses: filled paths sharing one linear gradient, as exported by Affinity and others.
param(
    [string]$Svg = (Join-Path $PSScriptRoot '..\src\DownloadsInTray\Assets\icon.svg'),
    [string]$Ico = (Join-Path $PSScriptRoot '..\src\DownloadsInTray\Assets\DownloadsInTray.ico'),
    # Every size Windows 11 asks for: title bar, tray, taskbar and Start at 100 to 400 % scaling.
    [int[]]$Sizes = @(16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 128, 256)
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

$doc = New-Object System.Xml.XmlDocument
$doc.XmlResolver = $null
$settings = New-Object System.Xml.XmlReaderSettings
$settings.DtdProcessing = [System.Xml.DtdProcessing]::Ignore
$reader = [System.Xml.XmlReader]::Create((Resolve-Path $Svg).Path, $settings)
$doc.Load($reader); $reader.Dispose()
$ns = New-Object System.Xml.XmlNamespaceManager $doc.NameTable
$ns.AddNamespace('s', 'http://www.w3.org/2000/svg')
$viewBox = $doc.DocumentElement.GetAttribute('viewBox') -split '[\s,]+' | ForEach-Object { [double]::Parse($_, [cultureinfo]::InvariantCulture) }

$gradient = $doc.SelectSingleNode('//s:linearGradient', $ns)
$brush = New-Object System.Windows.Media.LinearGradientBrush
$brush.MappingMode = 'Absolute'
function Num($value) { if ($value) { [double]::Parse($value, [cultureinfo]::InvariantCulture) } else { 0.0 } }
$start = New-Object System.Windows.Point (Num $gradient.x1), (Num $gradient.y1)
$end = New-Object System.Windows.Point (Num $gradient.x2), (Num $gradient.y2)
# Design tools often export unit coordinates moved by a gradientTransform matrix.
if ($gradient.gradientTransform) {
    $m = [regex]::Matches($gradient.gradientTransform, '-?[\d.]+(?:e-?\d+)?') | ForEach-Object { Num $_.Value }
    $matrix = New-Object System.Windows.Media.Matrix $m[0], $m[1], $m[2], $m[3], $m[4], $m[5]
    $start = $matrix.Transform($start); $end = $matrix.Transform($end)
}
$brush.StartPoint = $start
$brush.EndPoint = $end
foreach ($stop in $gradient.SelectNodes('s:stop', $ns)) {
    $value = if ($stop.'stop-color') { $stop.'stop-color' } else { [regex]::Match($stop.style, 'stop-color:\s*([^;]+)').Groups[1].Value }
    $rgb = [regex]::Match($value, 'rgb\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)')
    $color = if ($rgb.Success) { [System.Windows.Media.Color]::FromRgb([byte]$rgb.Groups[1].Value, [byte]$rgb.Groups[2].Value, [byte]$rgb.Groups[3].Value) } else { [System.Windows.Media.ColorConverter]::ConvertFromString($value.Trim()) }
    $brush.GradientStops.Add((New-Object System.Windows.Media.GradientStop $color, (Num $stop.offset)))
}
$brush.Freeze()

$geometry = New-Object System.Windows.Media.GeometryGroup
foreach ($path in $doc.SelectNodes('//s:path', $ns)) {
    $geometry.Children.Add([System.Windows.Media.Geometry]::Parse($path.d))
}
$geometry.Freeze()

$images = foreach ($size in $Sizes) {
    $visual = New-Object System.Windows.Media.DrawingVisual
    $context = $visual.RenderOpen()
    $scale = $size / [Math]::Max($viewBox[2], $viewBox[3])
    $context.PushTransform((New-Object System.Windows.Media.ScaleTransform $scale, $scale))
    $context.PushTransform((New-Object System.Windows.Media.TranslateTransform (-$viewBox[0]), (-$viewBox[1])))
    $context.DrawGeometry($brush, $null, $geometry)
    $context.Close()

    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap $size, $size, 96, 96, ([System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = New-Object System.IO.MemoryStream
    $encoder.Save($stream)
    , $stream.ToArray()
}

# ICO container: header, one directory entry per size, then the PNG images.
$out = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $out
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$Sizes.Count)
$offset = 6 + 16 * $Sizes.Count
for ($i = 0; $i -lt $Sizes.Count; $i++) {
    $dimension = if ($Sizes[$i] -ge 256) { 0 } else { $Sizes[$i] }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($image in $images) { $writer.Write([byte[]]$image) }
$writer.Flush()
[System.IO.File]::WriteAllBytes((Resolve-Path -LiteralPath (Split-Path $Ico)).Path + '\' + (Split-Path $Ico -Leaf), $out.ToArray())
"Wrote $Ico ($($Sizes -join ', ') px)"
