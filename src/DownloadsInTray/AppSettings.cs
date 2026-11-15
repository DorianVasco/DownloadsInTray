using System;
using System.IO;
using System.Text.Json;

namespace DownloadsInTray;

/// <summary>User preferences, kept in %LOCALAPPDATA%\<app name>\settings.json.</summary>
internal sealed class AppSettings
{
    public const int MinNotificationSeconds = 2;
    public const int MaxNotificationSeconds = 30;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.FileName, "settings.json");

    public const int MinVisibleItems = 3;
    public const int MaxVisibleItems = 30;

    private int _notificationSeconds = 5;
    private int _visibleItems = 10;

    public static AppSettings Current { get; } = Load();

    public int NotificationSeconds
    {
        get => _notificationSeconds;
        set => _notificationSeconds = Math.Clamp(value, MinNotificationSeconds, MaxNotificationSeconds);
    }

    /// <summary>How many rows the popup shows before scrolling, which sets its height.</summary>
    public int VisibleItems
    {
        get => _visibleItems;
        set => _visibleItems = Math.Clamp(value, MinVisibleItems, MaxVisibleItems);
    }

    /// <summary>Shows the tray icon in plain white or black, matching the taskbar, like Windows' own tray icons.</summary>
    public bool MonochromeTrayIcon { get; set; }

    /// <summary>Whether the one-time "icon is hidden" notification was shown.</summary>
    public bool HiddenTipShown { get; set; }

    /// <summary>Whether the one-time "pin me" notification was shown.</summary>
    public bool PinTipShown { get; set; }

    /// <summary>Raised after a setting changes and is saved.</summary>
    public static event Action? Changed;

    public void Save()
    {
        Changed?.Invoke();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The setting still applies until the app restarts.
        }
    }

    private static AppSettings Load()
    {
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }
}
