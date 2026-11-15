using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DownloadsInTray;

/// <summary>
/// The Windows Downloads folder icon, loaded from Windows itself at runtime rather than shipped with
/// the app, since the icon belongs to Microsoft. The exe and setup keep the app's own icon for that reason;
/// the Start menu shortcut points at this one (see installer\Setup.iss).
/// </summary>
internal static class DownloadsIcon
{
    // Where Windows keeps the Downloads known folder's icon, as its folder description records it.
    public static readonly string Library = Path.Combine(Environment.SystemDirectory, "imageres.dll");
    public const int ResourceId = 184;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHDefExtractIcon(string pszIconFile, int iIndex, uint uFlags, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIconSize);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>A WPF image of the icon at the given pixel size, or null if Windows doesn't provide it.</summary>
    public static ImageSource? Image(int pixels)
    {
        IntPtr icon = Load(pixels);
        if (icon == IntPtr.Zero) return null;
        try
        {
            BitmapSource image = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        finally
        {
            DestroyIcon(icon);
        }
    }

    /// <summary>A Windows Forms icon for the tray at the given pixel size, or null if Windows doesn't provide it.</summary>
    public static System.Drawing.Icon? TrayIcon(int pixels)
    {
        IntPtr icon = Load(pixels);
        if (icon == IntPtr.Zero) return null;
        try
        {
            using var borrowed = System.Drawing.Icon.FromHandle(icon);
            return (System.Drawing.Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(icon);
        }
    }

    // A negative index asks for a resource ID rather than a position.
    private static IntPtr Load(int pixels)
    {
        int result = SHDefExtractIcon(Library, -ResourceId, 0, out IntPtr large, out IntPtr small, (uint)pixels);
        if (small != IntPtr.Zero) DestroyIcon(small);
        return result == 0 ? large : IntPtr.Zero;
    }
}
