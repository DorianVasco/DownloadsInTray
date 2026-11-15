# Renders src\DownloadsInTray\Assets\icon.svg into the app's .ico (16 to 256 px, PNG-compressed).
# Run with Windows PowerShell (powershell.exe), which renders WPF on an STA thread:
#   powershell -NoProfile -File tools\build-icon.ps1
# Handles what icon.svg uses: filled paths and one linear gradient in user space.
param(
    [string]$Svg = (Join-Path $PSScriptRoot '..\src\DownloadsInTray\Assets\icon.svg'),
    [string]$Ico = (Join-Path $PSScriptRoot '..\src\DownloadsInTray\Assets\DownloadsInTray.ico'),
    [int[]]$Sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

[xml]$doc = Get-Content -Raw $Svg
$ns = New-Object System.Xml.XmlNamespaceManager $doc.NameTable
$ns.AddNamespace('s', 'http://www.w3.org/2000/svg')
$viewBox = $doc.svg.viewBox -split '\s+' | ForEach-Object { [double]$_ }

$gradient = $doc.SelectSingleNode('//s:linearGradient', $ns)
$brush = New-Object System.Windows.Media.LinearGradientBrush
$brush.MappingMode = 'Absolute'
$brush.StartPoint = New-Object System.Windows.Point ([double]$gradient.x1), ([double]$gradient.y1)
$brush.EndPoint = New-Object System.Windows.Point ([double]$gradient.x2), ([double]$gradient.y2)
foreach ($stop in $gradient.SelectNodes('s:stop', $ns)) {
    $color = [System.Windows.Media.ColorConverter]::ConvertFromString($stop.'stop-color')
    $brush.GradientStops.Add((New-Object System.Windows.Media.GradientStop $color, ([double]$stop.offset)))
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
