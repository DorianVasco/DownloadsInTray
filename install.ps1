# Quick install for development: publishes Downloads in Tray to %LOCALAPPDATA%\Programs\DownloadsInTray
# and adds a Start menu shortcut, without building the setup. Requires no admin rights.
# For releases, use build-release.ps1 and its setup program instead.
$ErrorActionPreference = 'Stop'

$target = Join-Path $env:LOCALAPPDATA 'Programs\DownloadsInTray'
$project = Join-Path $PSScriptRoot 'src\DownloadsInTray\DownloadsInTray.csproj'
$running = Get-Process -Name DownloadsInTray, WinDownload -ErrorAction SilentlyContinue
if ($running) { $running | Stop-Process -Force; $running | Wait-Process }

dotnet publish $project -c Release -o $target --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

$shortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'Downloads in Tray.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $target 'DownloadsInTray.exe'
$shortcut.WorkingDirectory = $target
$shortcut.Description = 'Recent downloads popup'
$shortcut.Save()

Write-Host "Installed to $target"
Write-Host "Start menu shortcut: $shortcutPath"
