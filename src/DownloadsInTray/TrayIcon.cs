using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DownloadsInTray;

/// <summary>
/// Shows that the app is running. A left click toggles the popup, a right click opens its options.
/// While a download is running, a small badge with a sliding arrow animates in its corner.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private const string IconResource = "DownloadsInTray.ico";
    private const int FrameCount = 6;
    private static readonly TimeSpan FrameTime = TimeSpan.FromMilliseconds(160);

    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _icon;
    private readonly DispatcherTimer _animation;
    private Icon[]? _frames;
    private int _frame;

    public TrayIcon(PopupWindow popup)
    {
        _icon = DownloadsIcon.TrayIcon(SystemInformation.SmallIconSize.Width) ?? LoadOwnIcon();
        _notifyIcon = new NotifyIcon { Icon = _icon, Text = AppInfo.Name, Visible = true };
        _notifyIcon.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) popup.HandleLaunchRequest();
            else if (e.Button == MouseButtons.Right) popup.ShowOptionsMenu();
        };

        _animation = new DispatcherTimer(FrameTime, DispatcherPriority.Background, (_, _) => NextFrame(), Dispatcher.CurrentDispatcher);
        _animation.Stop();
        popup.DownloadingChanged += () => SetAnimating(popup.IsDownloading);
    }

    private void SetAnimating(bool animating)
    {
        if (animating)
        {
            _frames ??= CreateFrames();
            _frame = 0;
            _animation.Start();
            NextFrame();
        }
        else
        {
            _animation.Stop();
            _notifyIcon.Icon = _icon;
        }
    }

    private void NextFrame()
    {
        _notifyIcon.Icon = _frames![_frame];
        _frame = (_frame + 1) % FrameCount;
    }

    /// <summary>The base icon with an accent-colored badge whose arrow slides down and wraps around.</summary>
    private Icon[] CreateFrames()
    {
        using Bitmap source = _icon.ToBitmap();
        int size = source.Width;
        float badge = size * 0.62f;
        var circle = new RectangleF(size - badge, size - badge, badge, badge);
        Color accent = AccentColor();

        var frames = new Icon[FrameCount];
        for (int i = 0; i < FrameCount; i++)
        {
            using var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawImage(source, 0, 0, size, size);

                using var fill = new SolidBrush(accent);
                g.FillEllipse(fill, circle);

                using var clip = new GraphicsPath();
                clip.AddEllipse(circle);
                g.SetClip(clip);

                // The arrow moves one step down per frame and comes back in from the top.
                float travel = badge * 1.2f;
                float offset = travel * i / FrameCount - travel / 2;
                float cx = circle.X + badge / 2;
                float cy = circle.Y + badge / 2 + offset;
                float half = badge * 0.26f;
                using var pen = new Pen(Color.White, Math.Max(1.2f, size / 12f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                g.DrawLine(pen, cx, cy - half, cx, cy + half);
                g.DrawLines(pen, new[] { new PointF(cx - half * 0.8f, cy + half * 0.2f), new PointF(cx, cy + half), new PointF(cx + half * 0.8f, cy + half * 0.2f) });
            }

            IntPtr handle = bitmap.GetHicon();
            using (Icon borrowed = Icon.FromHandle(handle)) frames[i] = (Icon)borrowed.Clone();
            DestroyIcon(handle);
        }
        return frames;
    }

    // The Windows accent color, stored as 0xAABBGGRR.
    private static Color AccentColor()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
        if (key?.GetValue("AccentColor") is int abgr)
        {
            return Color.FromArgb(255, abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF);
        }
        return Color.FromArgb(0, 120, 212);
    }

    private static Icon LoadOwnIcon()
    {
        using var stream = typeof(TrayIcon).Assembly.GetManifestResourceStream(IconResource)!;
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public void Dispose()
    {
        _animation.Stop();
        _notifyIcon.Dispose();
        _icon.Dispose();
        if (_frames != null)
        {
            foreach (Icon frame in _frames) frame.Dispose();
        }
    }
}
