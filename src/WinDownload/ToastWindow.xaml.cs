using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace WinDownload;

/// <summary>A small notification that fades in above the tray without taking focus.</summary>
public partial class ToastWindow : Window
{
    private const int ScreenGapDip = 12;
    private const int SlideDip = 10;
    private const double FadeInMs = 180;
    private const double FadeOutMs = 220;
    private static readonly TimeSpan VisibleTime = TimeSpan.FromSeconds(5);

    private readonly DispatcherTimer _animation;
    private readonly DispatcherTimer _dismissTimer;
    private IntPtr _hwnd;
    private int _x;
    private int _restingY;
    private int _slidePixels;
    private bool _fadingOut;
    private long _animationStart;
    private string[] _paths = Array.Empty<string>();
    private Point? _pressedAt;

    public ToastWindow()
    {
        InitializeComponent();
        _animation = new DispatcherTimer(TimeSpan.FromMilliseconds(15), DispatcherPriority.Render, (_, _) => OnAnimationTick(), Dispatcher);
        _animation.Stop();
        _dismissTimer = new DispatcherTimer(VisibleTime, DispatcherPriority.Normal, (_, _) => FadeOut(), Dispatcher);
        _dismissTimer.Stop();
    }

    public event Action? Clicked;

    public void Prepare()
    {
        _hwnd = new WindowInteropHelper(this).EnsureHandle();
        Native.ApplyFlyoutStyle(_hwnd, Native.WS_EX_NOACTIVATE | Native.WS_EX_LAYERED);
        SetAlpha(0);
    }

    /// <summary>Shows the new paths, unless Windows reports that notifications shouldn't interrupt right now.</summary>
    public void Announce(IReadOnlyList<string> addedPaths, IReadOnlyList<DownloadItem> listed)
    {
        if (!Native.CanInterrupt()) return;

        // The list is newest first, so its first new row is the latest arrival.
        DownloadItem? latest = listed.FirstOrDefault(item => addedPaths.Contains(item.Path, StringComparer.OrdinalIgnoreCase));
        string latestName = latest?.Name ?? Path.GetFileName(addedPaths[0]);

        bool single = addedPaths.Count == 1;
        Headline.Text = single ? latestName : $"{addedPaths.Count} new downloads";
        Detail.Text = single ? "New in Downloads" : latestName;
        DataContext = latest;
        _paths = addedPaths.ToArray();

        bool alreadyShown = IsVisible && !_fadingOut;
        if (!alreadyShown) ShowAnimated();
        RestartDismissTimer();
    }

    public void Dismiss()
    {
        _animation.Stop();
        _dismissTimer.Stop();
        _fadingOut = false;
        Hide();
    }

    private void ShowAnimated()
    {
        PlaceAboveTray();
        SetAlpha(0);
        Show();
        PlaceAboveTray();

        _fadingOut = false;
        _animationStart = Environment.TickCount64;
        _animation.Start();
    }

    private void FadeOut()
    {
        _dismissTimer.Stop();
        _fadingOut = true;
        _animationStart = Environment.TickCount64;
        _animation.Start();
    }

    private void RestartDismissTimer()
    {
        _dismissTimer.Stop();
        if (!IsMouseOver) _dismissTimer.Start();
    }

    private void OnAnimationTick()
    {
        double elapsed = Environment.TickCount64 - _animationStart;
        double progress = Math.Min(1, elapsed / (_fadingOut ? FadeOutMs : FadeInMs));
        double eased = 1 - Math.Pow(1 - progress, 3);

        double visibility = _fadingOut ? 1 - eased : eased;
        SetAlpha((byte)(visibility * 255));
        MoveTo(_restingY + (int)((1 - visibility) * _slidePixels));

        if (progress < 1) return;
        _animation.Stop();
        if (_fadingOut) Dismiss();
    }

    private void PlaceAboveTray()
    {
        Native.GetCursorPos(out Native.POINT cursor);
        IntPtr monitor = Native.MonitorFromPoint(cursor, Native.MONITOR_DEFAULTTONEAREST);
        var info = new Native.MONITORINFO { cbSize = Marshal.SizeOf<Native.MONITORINFO>() };
        if (!Native.GetMonitorInfo(monitor, ref info)) return;
        Native.GetDpiForMonitor(monitor, Native.MDT_EFFECTIVE_DPI, out uint dpi, out _);
        double scale = dpi / 96.0;

        int gap = (int)(ScreenGapDip * scale);
        int width = (int)Math.Ceiling(Width * scale);
        int height = (int)Math.Ceiling(Height * scale);
        _x = info.rcWork.Right - width - gap;
        _restingY = info.rcWork.Bottom - height - gap;
        _slidePixels = (int)(SlideDip * scale);

        Native.SetWindowPos(_hwnd, IntPtr.Zero, _x, _restingY + _slidePixels, width, height, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
    }

    private void MoveTo(int y) =>
        Native.SetWindowPos(_hwnd, IntPtr.Zero, _x, y, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);

    private void SetAlpha(byte alpha) => Native.SetLayeredWindowAttributes(_hwnd, 0, alpha, Native.LWA_ALPHA);

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        _dismissTimer.Stop();
        if (_fadingOut)
        {
            _fadingOut = false;
            _animationStart = Environment.TickCount64;
            _animation.Start();
        }
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_pressedAt == null) _dismissTimer.Start();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pressedAt = e.GetPosition(this);
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_pressedAt is not Point start) return;

        Vector moved = e.GetPosition(this) - start;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _pressedAt = null;
        _dismissTimer.Stop();
        FileDrag.Run(this, _paths, _hwnd, Dismiss);
        if (IsVisible) RestartDismissTimer();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        if (_pressedAt == null) return;

        _pressedAt = null;
        Dismiss();
        Clicked?.Invoke();
    }
}
