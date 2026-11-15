using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DownloadsInTray;

internal static class DownloadsFolder
{
    private const int MaxItems = 50;

    private static readonly HashSet<string> InProgressExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".crdownload", ".part", ".partial", ".download", ".tmp",
    };

    public static string Path { get; } = Native.SHGetKnownFolderPath(Native.FOLDERID_Downloads, 0, IntPtr.Zero);

    /// <summary>
    /// Newest first by creation time, the closest Windows has to the Finder's "Date Added":
    /// unlike the modified date, it reflects when an item arrived rather than when its source was last edited.
    /// </summary>
    public static IReadOnlyList<DownloadItem> ReadRecent()
    {
        try
        {
            return EnumerateListed()
                .OrderByDescending(info => info.CreationTime)
                .Take(MaxItems)
                .Select(ToItem)
                .ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<DownloadItem>();
        }
    }

    public static IEnumerable<FileSystemInfo> EnumerateListed() =>
        new DirectoryInfo(Path).EnumerateFileSystemInfos().Where(IsListed);

    public static bool IsInProgress(string? name) =>
        InProgressExtensions.Contains(System.IO.Path.GetExtension(name) ?? string.Empty);

    private static bool IsListed(FileSystemInfo info)
    {
        if ((info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) return false;
        if (info is DirectoryInfo) return true;

        // Firefox keeps an empty file under the final name until "<name>.part" is complete.
        return !IsInProgress(info.Name) && !(info is FileInfo { Length: 0 } && File.Exists(info.FullName + ".part"));
    }

    private static DownloadItem ToItem(FileSystemInfo info) => new(
        info.FullName,
        info.Name,
        info is DirectoryInfo,
        info is FileInfo file ? file.Length : 0,
        info.CreationTime,
        info.LastWriteTime);
}
