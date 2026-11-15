using System;
using System.IO;
using Microsoft.Win32;

namespace DownloadsInTray;

/// <summary>
/// The app was called WinDownload before. On first start under the new name, this carries over
/// its settings file and its start-at-login entry, then removes the old ones.
/// </summary>
internal static class LegacyMigration
{
    private const string OldName = "WinDownload";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void Run()
    {
        try
        {
            MoveSettings();
            MoveStartAtLogin();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Nothing is lost: the old files stay and the app starts with defaults.
        }
    }

    private static void MoveSettings()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string oldFolder = Path.Combine(appData, OldName);
        string oldFile = Path.Combine(oldFolder, "settings.json");
        string newFile = Path.Combine(appData, AppInfo.FileName, "settings.json");
        if (!File.Exists(oldFile)) return;

        if (!File.Exists(newFile))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(newFile)!);
            File.Copy(oldFile, newFile);
        }
        Directory.Delete(oldFolder, recursive: true);
    }

    private static void MoveStartAtLogin()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue(OldName) == null) return;

        key.DeleteValue(OldName, throwOnMissingValue: false);
        StartupRegistration.IsEnabled = true;
    }
}
