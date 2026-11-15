using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;

namespace WinDownload;

/// <summary>
/// Downloads still being written, found by the partial files browsers keep in Downloads:
/// Chrome and Edge use "name.crdownload", Firefox "name.part". Those files don't record the
/// final size, so progress is shown as bytes received and speed rather than a percentage.
/// </summary>
internal sealed class ActiveDownloads
{
    private static readonly string[] PartialExtensions = { ".crdownload", ".part", ".partial" };

    public ObservableCollection<ActiveDownload> Items { get; } = new();

    /// <summary>Rereads the partial files and updates the rows in place, so progress bars keep animating.</summary>
    public void Poll()
    {
        Dictionary<string, FileInfo> partials;
        try
        {
            partials = new DirectoryInfo(DownloadsFolder.Path)
                .EnumerateFiles()
                .Where(file => PartialExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(file => file.FullName, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (ActiveDownload gone in Items.Where(item => !partials.ContainsKey(item.Path)).ToList())
        {
            Items.Remove(gone);
        }

        long now = Environment.TickCount64;
        foreach (FileInfo file in partials.Values.OrderBy(file => file.CreationTime))
        {
            ActiveDownload? item = Items.FirstOrDefault(existing => existing.Path.Equals(file.FullName, StringComparison.OrdinalIgnoreCase));
            if (item == null)
            {
                item = new ActiveDownload(file.FullName, DisplayName(file.Name));
                Items.Insert(0, item);
            }
            // Folder listings report the size of a file that is still open for writing late, so ask the file itself.
            file.Refresh();
            item.Update(file.Exists ? file.Length : 0, now);
        }
    }

    // "report.pdf.crdownload" becomes "report.pdf". Chrome names a download "Unconfirmed 123.crdownload"
    // until its name is known, which is kept as it is.
    private static string DisplayName(string fileName) => System.IO.Path.GetFileNameWithoutExtension(fileName);
}

internal sealed class ActiveDownload : INotifyPropertyChanged
{
    // A download that hasn't grown for this long is shown as paused, as browsers keep the file when pausing.
    private const long PausedAfterMs = 5000;

    private long _bytes = -1;
    private long _lastGrowth;
    private double _bytesPerSecond;
    private long _lastSample;

    public ActiveDownload(string path, string name)
    {
        Path = path;
        Name = name;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Path { get; }
    public string Name { get; }

    /// <summary>Where the browser renames the file once it's done.</summary>
    public string FinalPath => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!, Name);

    public bool IsPaused { get; private set; }
    public string Details { get; private set; } = string.Empty;

    public void Update(long bytes, long now)
    {
        if (_bytes >= 0 && now > _lastSample)
        {
            double sample = Math.Max(0, bytes - _bytes) * 1000.0 / (now - _lastSample);

            // Smoothed, so the speed doesn't jump with every chunk the browser writes.
            _bytesPerSecond = _bytesPerSecond == 0 ? sample : _bytesPerSecond * 0.6 + sample * 0.4;
        }
        if (bytes != _bytes) _lastGrowth = now;

        _bytes = bytes;
        _lastSample = now;
        IsPaused = now - _lastGrowth > PausedAfterMs;
        Details = IsPaused
            ? $"{DownloadItem.FormatSize(bytes)} · Paused"
            : _bytesPerSecond > 0
                ? $"{DownloadItem.FormatSize(bytes)} · {DownloadItem.FormatSize((long)_bytesPerSecond)}/s"
                : DownloadItem.FormatSize(bytes);

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPaused)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Details)));
    }
}
