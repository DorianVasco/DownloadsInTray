using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DownloadsInTray;

/// <summary>Loads shell thumbnails and icons on a background STA thread so the list can show immediately.</summary>
internal sealed class ShellIconLoader
{
    private const int IconPixels = 64;
    private const int SIIGBF_BIGGERSIZEOK = 0x01;

    private readonly BlockingCollection<DownloadItem> _queue = new();
    private readonly System.Windows.Threading.Dispatcher _dispatcher;
    private readonly ConcurrentDictionary<string, ImageSource> _cache = new();

    public ShellIconLoader(System.Windows.Threading.Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        var thread = new Thread(ProcessQueue) { IsBackground = true, Name = "ShellIconLoader" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    /// <summary>Assigns cached icons, queues the rest, and drops cache entries no longer listed.</summary>
    public void Load(IReadOnlyCollection<DownloadItem> items)
    {
        var listed = new HashSet<string>(items.Select(item => item.IconKey));
        foreach (string key in _cache.Keys)
        {
            if (!listed.Contains(key)) _cache.TryRemove(key, out _);
        }

        foreach (DownloadItem item in items)
        {
            if (_cache.TryGetValue(item.IconKey, out ImageSource? icon))
            {
                item.Icon = icon;
            }
            else
            {
                _queue.Add(item);
            }
        }
    }

    private void ProcessQueue()
    {
        foreach (DownloadItem item in _queue.GetConsumingEnumerable())
        {
            // A refresh can queue an item again while an earlier copy is still waiting.
            if (!_cache.TryGetValue(item.IconKey, out ImageSource? icon))
            {
                icon = TryLoad(item.Path);
                if (icon == null) continue;
                _cache[item.IconKey] = icon;
            }

            ImageSource loaded = icon;
            _dispatcher.BeginInvoke(() => item.Icon = loaded);
        }
    }

    private static ImageSource? TryLoad(string path)
    {
        IntPtr hBitmap = IntPtr.Zero;
        try
        {
            Native.SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(Native.IShellItemImageFactory).GUID, out var factory);
            try
            {
                factory.GetImage(new Native.SIZE { cx = IconPixels, cy = IconPixels }, SIIGBF_BIGGERSIZEOK, out hBitmap);
            }
            finally
            {
                Marshal.ReleaseComObject(factory);
            }
            return ToBitmapSource(hBitmap);
        }
        catch (Exception e) when (e is COMException or ArgumentException or UnauthorizedAccessException or System.IO.IOException)
        {
            return null;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero) Native.DeleteObject(hBitmap);
        }
    }

    // Imaging.CreateBitmapSourceFromHBitmap discards the alpha channel, so copy the pixels directly.
    private static BitmapSource? ToBitmapSource(IntPtr hBitmap)
    {
        if (Native.GetObject(hBitmap, Marshal.SizeOf<Native.BITMAP>(), out Native.BITMAP info) == 0) return null;
        if (info.bmBitsPixel != 32) return null;

        int height = Math.Abs(info.bmHeight);
        var pixels = new byte[info.bmWidthBytes * height];
        if (Native.GetBitmapBits(hBitmap, pixels.Length, pixels) == 0) return null;

        BitmapSource bitmap = BitmapSource.Create(
            info.bmWidth, height, 96, 96, PixelFormats.Pbgra32, null, pixels, info.bmWidthBytes);
        bitmap.Freeze();
        return bitmap;
    }
}
