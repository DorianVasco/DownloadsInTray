using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Media;

namespace WinDownload;

public sealed class DownloadItem : INotifyPropertyChanged
{
    private ImageSource? _icon;
    private bool _isHighlighted;

    public DownloadItem(string path, string name, bool isFolder, long size, DateTime added, DateTime modified)
    {
        Path = path;
        Name = name;
        IsFolder = isFolder;
        Size = size;
        Added = added;
        Modified = modified;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Path { get; }
    public string Name { get; }
    public bool IsFolder { get; }
    public long Size { get; }
    public DateTime Added { get; }
    public DateTime Modified { get; }

    public string Details => IsFolder
        ? $"Folder · {FormatAge(Added, DateTime.Now)}"
        : $"{FormatSize(Size)} · {FormatAge(Added, DateTime.Now)}";

    public ImageSource? Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
        }
    }

    /// <summary>Marks a download that just finished while the user was watching it.</summary>
    public bool IsHighlighted
    {
        get => _isHighlighted;
        set
        {
            if (_isHighlighted == value) return;
            _isHighlighted = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsHighlighted)));
        }
    }

    public string IconKey => $"{Path}|{Modified.Ticks}";

    private static string FormatAge(DateTime added, DateTime now)
    {
        TimeSpan age = now - added;
        if (age < TimeSpan.FromMinutes(1)) return "Just now";
        if (age < TimeSpan.FromHours(1)) return $"{(int)age.TotalMinutes} min ago";
        if (added.Date == now.Date) return added.ToString("t", CultureInfo.CurrentCulture);
        if (added.Date == now.Date.AddDays(-1)) return "Yesterday";
        return added.ToString("d", CultureInfo.CurrentCulture);
    }

    public static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0
            ? $"{bytes} B"
            : string.Format(CultureInfo.CurrentCulture, "{0:0.#} {1}", value, units[unit]);
    }
}
