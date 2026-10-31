# Publishes WinDownload to %LOCALAPPDATA%\Programs\WinDownload and adds a Start menu shortcut.
# Requires no admin rights. Afterwards, pin the shortcut: Start > WinDownload > right-click > Pin to taskbar.
$ErrorActionPreference = 'Stop'

$target = Join-Path $env:LOCALAPPDATA 'Programs\WinDownload'
$project = Join-Path $PSScriptRoot 'src\WinDownload\WinDownload.csproj'
$running = Get-Process -Name WinDownload -ErrorAction SilentlyContinue
if ($running) { $running | Stop-Process -Force; $running | Wait-Process }

dotnet publish $project -c Release -o $target --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

$shortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'WinDownload.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $target 'WinDownload.exe'
$shortcut.WorkingDirectory = $target
$shortcut.Description = 'Recent downloads popup'
$shortcut.Save()

Write-Host "Installed to $target"
Write-Host "Start menu shortcut: $shortcutPath"
