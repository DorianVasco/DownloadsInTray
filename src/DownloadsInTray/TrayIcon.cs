using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DownloadsInTray;

/// <summary>
/// Shows that the app is running. A left click toggles the popup, a right click opens its options.
/// While a download is running, the arrow in the icon slides down into the tray bar. With the
/// monochrome setting, the icon is plain white on a dark taskbar and near-black on a light one.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private const string IconResource = "DownloadsInTray.ico";
    private const string ColorSvg = "icon.svg";
    private const string WhiteSvg = "icon2-mono-dark.svg";
    private const string BlackSvg = "icon2-mono-light.svg";
    private const int FrameCount = 8;
    private static readonly TimeSpan FrameTime = TimeSpan.FromMilliseconds(120);

    private readonly NotifyIcon _notifyIcon;
    private readonly DispatcherTimer _animation;
    private readonly PopupWindow _popup;
    private Icon _icon;
    private Icon[]? _frames;
    private string? _framesSource;
    private int _frame;

    public TrayIcon(PopupWindow popup)
    {
        _popup = popup;
        _icon = LoadIcon();
        _notifyIcon = new NotifyIcon { Icon = _icon, Text = AppInfo.Name, Visible = true };
        _notifyIcon.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) popup.HandleLaunchRequest();
            else if (e.Button == MouseButtons.Right) popup.ShowOptionsMenu();
        };

        _animation = new DispatcherTimer(FrameTime, DispatcherPriority.Background, (_, _) => NextFrame(), Dispatcher.CurrentDispatcher);
        _animation.Stop();
        popup.DownloadingChanged += () => SetAnimating(popup.IsDownloading);

        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        AppSettings.Changed += () => dispatcher.BeginInvoke(RefreshIcon);
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General) dispatcher.BeginInvoke(RefreshIcon);
        };
    }

    /// <summary>The SVG the tray icon is drawn from right now.</summary>
    private static string CurrentSvg()
    {
        if (!AppSettings.Current.MonochromeTrayIcon) return ColorSvg;
        return TaskbarIsLight() ? BlackSvg : WhiteSvg;
    }

    // The taskbar follows Windows' mode, which can differ from the apps' mode.
    private static bool TaskbarIsLight()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
    }

    private Icon LoadIcon()
    {
        string svg = CurrentSvg();
        if (svg != ColorSvg) return IconAnimation.CreateFrames(svg, SystemInformation.SmallIconSize.Width, 1)[0];

        using var stream = typeof(TrayIcon).Assembly.GetManifestResourceStream(IconResource)!;
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    /// <summary>Redraws the icon after the monochrome setting or the taskbar theme changes.</summary>
    private void RefreshIcon()
    {
        Icon old = _icon;
        _icon = LoadIcon();
        if (!_animation.IsEnabled) _notifyIcon.Icon = _icon;
        old.Dispose();

        if (_framesSource != CurrentSvg()) DisposeFrames();
        if (_animation.IsEnabled) SetAnimating(true);
    }

    private void SetAnimating(bool animating)
    {
        if (animating)
        {
            if (_frames == null)
            {
                _framesSource = CurrentSvg();
                _frames = IconAnimation.CreateFrames(_framesSource, SystemInformation.SmallIconSize.Width, FrameCount);
            }
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

    private void DisposeFrames()
    {
        if (_frames == null) return;
        foreach (Icon frame in _frames) frame.Dispose();
        _frames = null;
    }

    public void Dispose()
    {
        _animation.Stop();
        _notifyIcon.Dispose();
        _icon.Dispose();
        DisposeFrames();
    }
}
