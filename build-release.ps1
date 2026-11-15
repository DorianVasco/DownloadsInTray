# Builds the release files into dist\:
#   <name>-<version>-win-x64.exe                small, needs the .NET 10 Desktop Runtime
#   <name>-<version>-win-x64-self-contained.exe runs anywhere, with the runtime bundled
#   <name>-<version>-setup.exe                  installer for the small exe; checks for the .NET 10 Desktop Runtime (needs Inno Setup 6 to build)
#   <name>-<version>-x64.msix                   package for the Microsoft Store (identity in installer\msix\store-identity.json)
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
    # Each variant gets its own build folders. Sharing them let the self-contained build's leftovers
    # end up in a later framework-dependent build, which then crashed at startup.
    $artifacts = Join-Path $dist "build-self-contained-$($build.SelfContained)"
    dotnet publish $project -c Release -p:SelfContained=$($build.SelfContained) --artifacts-path $artifacts -o $out --nologo -v quiet
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
    & $iscc /Q "/DAppName=$displayName" "/DAppFileName=$name" "/DAppVersion=$version" "/DSourceExe=$(Join-Path $dist $builds[0].Name)" "/DOutputDir=$dist" (Join-Path $PSScriptRoot 'installer\Setup.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed to build the setup program' }
}
else {
    Write-Warning 'Inno Setup 6 not found, so no setup program was built. Install it with: winget install JRSoftware.InnoSetup --scope user'
}

# MSIX package for the Microsoft Store, built from the self-contained exe. Unsigned: the Store signs it.
$msixFolder = Join-Path $PSScriptRoot 'installer\msix'
$identity = Get-Content -Raw (Join-Path $msixFolder 'store-identity.json') | ConvertFrom-Json
if ($identity.IdentityName -like 'PLACEHOLDER*') {
    Write-Warning 'installer\msix\store-identity.json still has placeholders; the MSIX builds but the Store will reject it until they match Partner Center.'
}

dotnet restore (Join-Path $PSScriptRoot 'tools\MsixTools\MsixTools.csproj') --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Could not fetch the MSIX packaging tool' }
$makeappx = Get-ChildItem (Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.sdk.buildtools') -Recurse -Filter makeappx.exe |
    Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1

$layout = Join-Path $dist 'msix-layout'
Remove-Item -Recurse -Force $layout -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $layout | Out-Null
Copy-Item (Join-Path $dist $builds[1].Name) (Join-Path $layout "$name.exe")
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'tools\build-icon.ps1') -StoreAssets (Join-Path $layout 'Assets') | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not render the Store logos' }

# MSIX versions have four parts.
$manifest = (Get-Content -Raw (Join-Path $msixFolder 'AppxManifest.xml')).
    Replace('{IdentityName}', $identity.IdentityName).
    Replace('{Publisher}', $identity.Publisher).
    Replace('{PublisherDisplayName}', [Security.SecurityElement]::Escape($identity.PublisherDisplayName)).
    Replace('{DisplayName}', [Security.SecurityElement]::Escape($displayName)).
    Replace('{FileName}', $name).
    Replace('{Version}', "$version.0")
[IO.File]::WriteAllText((Join-Path $layout 'AppxManifest.xml'), $manifest)

# The resource index lets Windows pick the right logo size and theme variant.
$makepri = Join-Path $makeappx.DirectoryName 'makepri.exe'
$priConfig = Join-Path $dist 'priconfig.xml'
& $makepri createconfig /cf $priConfig /dq en-US /o | Out-Null
& $makepri new /pr $layout /cf $priConfig /of (Join-Path $layout 'resources.pri') /mn (Join-Path $layout 'AppxManifest.xml') /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makepri failed to index the package resources' }
Remove-Item $priConfig

$msix = Join-Path $dist "$name-$version-x64.msix"
& $makeappx.FullName pack /o /d $layout /p $msix | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makeappx failed to build the MSIX package' }
Remove-Item -Recurse -Force $layout

Get-ChildItem $dist -Filter "$name-$version-*" | Where-Object Extension -in '.exe', '.msix' |
    ForEach-Object { '{0}  {1:N1} MB' -f $_.Name, ($_.Length / 1MB) }
