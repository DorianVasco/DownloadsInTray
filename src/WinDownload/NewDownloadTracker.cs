using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WinDownload;

/// <summary>Reports items that are new to the whole folder, not just to the capped list shown in the popup.</summary>
internal sealed class NewDownloadTracker
{
    private HashSet<string> _known;

    public NewDownloadTracker()
    {
        _known = new HashSet<string>(ReadReadyPaths() ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Returns the paths that appeared since the last call, or nothing if the folder can't be read.</summary>
    public IReadOnlyList<string> TakeNew()
    {
        List<string>? ready = ReadReadyPaths();
        if (ready == null) return Array.Empty<string>();

        List<string> added = ready.Where(path => !_known.Contains(path)).ToList();
        _known = new HashSet<string>(ready, StringComparer.OrdinalIgnoreCase);
        return added;
    }

    /// <summary>Renaming a known item doesn't make it a new download.</summary>
    public void OnRenamed(string oldPath, string newPath)
    {
        if (_known.Remove(oldPath)) _known.Add(newPath);
    }

    private static List<string>? ReadReadyPaths()
    {
        try
        {
            return DownloadsFolder.EnumerateListed().Where(IsReady).Select(info => info.FullName).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Firefox creates an empty file under the final name and writes to "<name>.part" until it finishes.
    private static bool IsReady(FileSystemInfo info) =>
        info is not FileInfo file || (file.Length > 0 && !File.Exists(file.FullName + ".part"));
}
