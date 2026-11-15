# Builds the two release files into dist\:
#   WinDownload-<version>-win-x64.exe                small, needs the .NET 10 Desktop Runtime
#   WinDownload-<version>-win-x64-self-contained.exe runs anywhere, with the runtime bundled
$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'src\WinDownload\WinDownload.csproj'
$dist = Join-Path $PSScriptRoot 'dist'
$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

$builds = @(
    @{ SelfContained = 'false'; Name = "WinDownload-$version-win-x64.exe" },
    @{ SelfContained = 'true'; Name = "WinDownload-$version-win-x64-self-contained.exe" }
)

New-Item -ItemType Directory -Force $dist | Out-Null
foreach ($build in $builds) {
    $out = Join-Path $dist "obj-$($build.SelfContained)"
    dotnet publish $project -c Release -p:SelfContained=$($build.SelfContained) -o $out --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (self-contained: $($build.SelfContained))" }

    Move-Item -Force (Join-Path $out 'WinDownload.exe') (Join-Path $dist $build.Name)
    Remove-Item -Recurse -Force $out
}

Get-ChildItem $dist -Filter "WinDownload-$version-*.exe" |
    ForEach-Object { '{0}  {1:N1} MB' -f $_.Name, ($_.Length / 1MB) }
