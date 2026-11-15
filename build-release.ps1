# Builds the release files into dist\:
#   <name>-<version>-win-x64.exe                small, needs the .NET 10 Desktop Runtime
#   <name>-<version>-win-x64-self-contained.exe runs anywhere, with the runtime bundled
#   <name>-<version>-setup.exe                  installer for the self-contained build (needs Inno Setup 6 to build)
# <name> is <AssemblyName> in src\DownloadsInTray\DownloadsInTray.csproj, the version is <Version> there,
# and the setup shows <Product> as the app's name.
$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'src\DownloadsInTray\DownloadsInTray.csproj'
$dist = Join-Path $PSScriptRoot 'dist'
$properties = ([xml](Get-Content $project)).Project.PropertyGroup
$displayName = $properties.Product | Where-Object { $_ } | Select-Object -First 1
$name = $properties.AssemblyName | Where-Object { $_ } | Select-Object -First 1
$version = $properties.Version | Where-Object { $_ } | Select-Object -First 1

$builds = @(
    @{ SelfContained = 'false'; Name = "$name-$version-win-x64.exe" },
    @{ SelfContained = 'true'; Name = "$name-$version-win-x64-self-contained.exe" }
)

New-Item -ItemType Directory -Force $dist | Out-Null
foreach ($build in $builds) {
    $out = Join-Path $dist "obj-$($build.SelfContained)"
    dotnet publish $project -c Release -p:SelfContained=$($build.SelfContained) -o $out --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (self-contained: $($build.SelfContained))" }

    Move-Item -Force (Join-Path $out "$name.exe") (Join-Path $dist $build.Name)
    Remove-Item -Recurse -Force $out
}

$iscc = @(
    (Get-Command iscc -ErrorAction SilentlyContinue).Source,
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if ($iscc) {
    & $iscc /Q "/DAppName=$displayName" "/DAppFileName=$name" "/DAppVersion=$version" "/DSourceExe=$(Join-Path $dist $builds[1].Name)" "/DOutputDir=$dist" (Join-Path $PSScriptRoot 'installer\Setup.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed to build the setup program' }
}
else {
    Write-Warning 'Inno Setup 6 not found, so no setup program was built. Install it with: winget install JRSoftware.InnoSetup --scope user'
}

Get-ChildItem $dist -Filter "$name-$version-*.exe" |
    ForEach-Object { '{0}  {1:N1} MB' -f $_.Name, ($_.Length / 1MB) }
