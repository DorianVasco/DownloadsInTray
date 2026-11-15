using System;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DownloadsInTray;

/// <summary>
/// One-time Windows notifications after startup: one if the tray icon sits hidden in the overflow,
/// otherwise one if the app isn't pinned to the taskbar. Each is shown once.
/// </summary>
internal static class StartupTips
{
    // Explorer registers a new tray icon shortly after it appears, and the desktop may still be loading at login.
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(10);

    public static void Schedule(TrayIcon tray)
    {
        var timer = new DispatcherTimer { Interval = Delay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Show(tray);
        };
        timer.Start();
    }

    private static void Show(TrayIcon tray)
    {
        AppSettings settings = AppSettings.Current;
        if (!settings.HiddenTipShown && IsTrayIconHidden())
        {
            tray.ShowNotification(
                $"Hi, {AppInfo.Name} is hidden",
                "My tray icon is tucked away under the ^ arrow. Click here, then turn me on under Other system tray icons to keep it on the taskbar.",
                () => ShellActions.Open("ms-settings:taskbar"));
            settings.HiddenTipShown = true;
            settings.Save();
        }
        else if (!settings.PinTipShown && !IsPinned())
        {
            tray.ShowNotification(
                $"Pin {AppInfo.Name} to the taskbar",
                "One click on the pinned icon shows your downloads. Open Start, right-click Downloads in Tray and choose Pin to taskbar.",
                null);
            settings.PinTipShown = true;
            settings.Save();
        }
    }

    /// <summary>Windows 11 records per tray icon whether the user promoted it out of the overflow.</summary>
    private static bool IsTrayIconHidden()
    {
        using RegistryKey? icons = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings");
        if (icons == null) return false;

        foreach (string id in icons.GetSubKeyNames())
        {
            using RegistryKey? icon = icons.OpenSubKey(id);
            if (string.Equals(icon?.GetValue("ExecutablePath") as string, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
            {
                return icon?.GetValue("IsPromoted") is not int promoted || promoted == 0;
            }
        }

        // Not registered yet: new icons start out hidden on Windows 11.
        return true;
    }

    private static bool IsPinned()
    {
        string pinned = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");
        if (!Directory.Exists(pinned)) return false;

        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null) return false;
        dynamic shell = Activator.CreateInstance(shellType)!;
        return Directory.EnumerateFiles(pinned, "*.lnk").Any(link =>
            string.Equals((string)shell.CreateShortcut(link).TargetPath, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase));
    }
}
