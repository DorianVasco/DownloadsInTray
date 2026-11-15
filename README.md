# WinDownload

A Windows 11 alternative to the macOS Dock Downloads stack. Click the pinned taskbar icon to see your recent downloads, newest first, and click one to open it.

![The WinDownload popup above the taskbar, listing recent downloads](docs/images/ui.png)

## Install

Requires the .NET 10 SDK (the installed app only needs the .NET 10 Desktop Runtime).

```powershell
./install.ps1
```

This publishes to `%LOCALAPPDATA%\Programs\WinDownload` and adds a Start menu shortcut. Windows doesn't let apps pin themselves, so open Start, search for WinDownload, right-click it and choose **Pin to taskbar**.

## Use

- **Click the icon** to open or close the popup. Clicking elsewhere or pressing Esc also closes it.
- **Click a row** to open the item, or right-click it for **Show in folder**. Arrow keys and Enter work too.

  ![Right-click menu on a download with Open and Show in folder](docs/images/context.png)
- **Running downloads** from Chrome, Edge and Firefox appear at the top with a moving bar, the size so far and the speed. A download that stops growing for 5 seconds shows as paused. If you watched a download here, the popup comes back without taking focus when it finishes, with the file marked, and closes again after the notification time unless you hover or click it. Browsers don't record the final size in their unfinished files, so there's no percentage.
- **Drag a row** into a folder or app. Explorer moves it within a drive and copies it across drives.
- **Notification:** a small notice fades in above the tray when something new lands in Downloads, and clicking it opens the file (or the list, when several arrived together). Right-click it for **Open**, **Show in folder** and **Show all downloads**. Drag it into a folder or app to move all the files it announces. It stays quiet while the popup is open and when Windows reports that you shouldn't be interrupted, such as during full-screen apps.
- **Tray icon:** it shows the app is running. Left-click toggles the popup. Right-click opens a menu with Show downloads, Open Downloads folder, Settings and Quit. Windows may hide it under the tray's ^ arrow until you drag it out.
- **⋯ menu:** **Settings** covers Start at login, which makes the first click after a reboot instant, how long notifications stay up (2 to 30 seconds), and how many items the popup shows before scrolling (3 to 30), which sets its height. **Quit WinDownload** stops the background process.

  ![The settings window with Start at login, notification time and visible items](docs/images/settings.png)

## How it works

A pinned icon can only launch an exe. The first launch stays running with a hidden popup, and each later launch signals it and exits. While none of its windows are showing, it lets Windows throttle it to save power (EcoQoS), and it runs at full speed again as soon as something opens.

The list shows files and folders from the Downloads known folder, sorted by creation time and capped at 50. Hidden and system items and unfinished downloads (`.crdownload`, `.part`, `.partial`, `.download`, `.tmp`) are left out.

## Uninstall

Turn off **Start at login** if you enabled it, choose **Quit WinDownload**, unpin the icon, then delete `%LOCALAPPDATA%\Programs\WinDownload`, the settings in `%LOCALAPPDATA%\WinDownload` and the WinDownload shortcut in the Start menu.
