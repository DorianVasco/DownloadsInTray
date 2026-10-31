using System;
using System.IO;
using System.Text.Json;

namespace WinDownload;

/// <summary>User preferences, kept in %LOCALAPPDATA%\WinDownload\settings.json.</summary>
internal sealed class AppSettings
{
    public const int MinNotificationSeconds = 2;
    public const int MaxNotificationSeconds = 30;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinDownload", "settings.json");

    private int _notificationSeconds = 5;

    public static AppSettings Current { get; } = Load();

    public int NotificationSeconds
    {
        get => _notificationSeconds;
        set => _notificationSeconds = Math.Clamp(value, MinNotificationSeconds, MaxNotificationSeconds);
    }

    public void Save()
    {
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
