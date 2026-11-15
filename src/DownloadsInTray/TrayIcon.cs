using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Threading;

namespace DownloadsInTray;

/// <summary>
/// Shows that the app is running. A left click toggles the popup, a right click opens its options.
/// While a download is running, the arrow in the icon slides down into the tray bar.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private const string IconResource = "DownloadsInTray.ico";
    private const int FrameCount = 8;
    private static readonly TimeSpan FrameTime = TimeSpan.FromMilliseconds(120);

    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _icon;
    private readonly DispatcherTimer _animation;
    private Icon[]? _frames;
    private int _frame;

    public TrayIcon(PopupWindow popup)
    {
        _icon = LoadOwnIcon();
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
            _frames ??= IconAnimation.CreateFrames(SystemInformation.SmallIconSize.Width, FrameCount);
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
