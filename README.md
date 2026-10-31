# WinDownload

A Windows 11 alternative to the macOS Dock Downloads stack. Click the pinned taskbar icon to see your recent downloads, newest first, and click one to open it.

## Install

Requires the .NET 10 SDK (the installed app only needs the .NET 10 Desktop Runtime).

```powershell
./install.ps1
```

This publishes to `%LOCALAPPDATA%\Programs\WinDownload` and adds a Start menu shortcut. Windows doesn't let apps pin themselves, so open Start, search for WinDownload, right-click it and choose **Pin to taskbar**.

## Use

- **Click the icon** to open or close the popup. Clicking elsewhere or pressing Esc also closes it.
- **Click a row** to open the item, or right-click it for **Show in folder**. Arrow keys and Enter work too.
- **Drag a row** into a folder or app. Explorer moves it within a drive and copies it across drives.
- **Notification:** a small notice fades in above the tray when something new lands in Downloads, and clicking it opens the list. It stays quiet while the popup is open and when Windows reports that you shouldn't be interrupted, such as during full-screen apps.
- **Tray icon:** it shows the app is running. Left-click toggles the popup and right-click opens the options. Windows may hide it under the tray's ^ arrow until you drag it out.
- **⋯ menu:** **Start at login** makes the first click after a reboot instant. **Quit WinDownload** stops the background process.

## How it works

A pinned icon can only launch an exe. The first launch stays running with a hidden popup, and each later launch signals it and exits.

The list shows files and folders from the Downloads known folder, sorted by creation time and capped at 50. Hidden and system items and unfinished downloads (`.crdownload`, `.part`, `.partial`, `.download`, `.tmp`) are left out.

## Uninstall

Turn off **Start at login** if you enabled it, choose **Quit WinDownload**, unpin the icon, then delete `%LOCALAPPDATA%\Programs\WinDownload` and the WinDownload shortcut in the Start menu.
