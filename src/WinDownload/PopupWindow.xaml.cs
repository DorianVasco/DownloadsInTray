using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace WinDownload;

public partial class PopupWindow : Window
{
    private const double MaxPopupHeight = 600;
    private const int ScreenGapDip = 12;

    // Clicking the pinned icon hides the popup through deactivation (mouse down), then launches
    // a process (mouse up) whose request must not reopen it. Only that click's request is ignored:
    // the window runs from mouse release, so a later click on the icon always opens the popup.
    private const int ToggleWindowMs = 350;

    private readonly ShellIconLoader _icons;
    private readonly DispatcherTimer _refreshDebounce;
    private readonly DispatcherTimer _mouseReleaseWatch;
    private FileSystemWatcher? _watcher;
    private IntPtr _hwnd;
    private long _ignoreLaunchUntil;
    private bool _hidingSilently;
    private bool _quitting;
    private Native.POINT _anchor;

    public PopupWindow()
    {
        InitializeComponent();
        _icons = new ShellIconLoader(Dispatcher);
        _refreshDebounce = new DispatcherTimer(
            TimeSpan.FromMilliseconds(300),
            DispatcherPriority.Background,
            (_, _) => OnFolderChanged(),
            Dispatcher);
        _refreshDebounce.Stop();
        _mouseReleaseWatch = new DispatcherTimer(
            TimeSpan.FromMilliseconds(15),
            DispatcherPriority.Input,
            (_, _) => OnMouseReleaseTick(),
            Dispatcher);
        _mouseReleaseWatch.Stop();
        DpiChanged += (_, _) => PositionNearAnchor();
    }

    /// <summary>Creates the hidden window and starts watching Downloads so the first show is instant.</summary>
    public void Prepare()
    {
        _hwnd = new WindowInteropHelper(this).EnsureHandle();

        long exStyle = Native.GetWindowLongPtr(_hwnd, Native.GWL_EXSTYLE).ToInt64();
        exStyle = (exStyle | Native.WS_EX_TOOLWINDOW) & ~(long)Native.WS_EX_APPWINDOW;
        Native.SetWindowLongPtr(_hwnd, Native.GWL_EXSTYLE, new IntPtr(exStyle));

        int corner = Native.DWMWCP_ROUND;
        Native.DwmSetWindowAttribute(_hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
        int backdrop = Native.DWMSBT_TRANSIENTWINDOW;
        Native.DwmSetWindowAttribute(_hwnd, Native.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));

        StartWatching();
        ShowItems(DownloadsFolder.ReadRecent());
    }

    public void HandleLaunchRequest()
    {
        if (IsVisible)
        {
            HideSilently();
            return;
        }

        bool closedByThisClick = Environment.TickCount64 < _ignoreLaunchUntil;
        ClearToggleWindow();
        if (closedByThisClick) return;

        ShowPopup();
    }

    /// <summary>Opens the popup with its options menu, for a right-click on the tray icon.</summary>
    public void ShowOptionsMenu()
    {
        ClearToggleWindow();
        if (!IsVisible) ShowPopup();
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => OnMenuClick(MenuButton, new RoutedEventArgs()));
    }

    private void ClearToggleWindow()
    {
        _mouseReleaseWatch.Stop();
        _ignoreLaunchUntil = 0;
    }

    private void ShowPopup()
    {
        Native.GetCursorPos(out _anchor);
        ShowItems(DownloadsFolder.ReadRecent());
        Scroller.ScrollToTop();
        PositionNearAnchor();
        Show();
        PositionNearAnchor();
        BringToForeground();
        FocusFirstItem();
    }

    private void StartWatching()
    {
        try
        {
            _watcher = new FileSystemWatcher(DownloadsFolder.Path)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
            };
            // A running download writes constantly but isn't listed until it is renamed to its final name.
            FileSystemEventHandler changed = (_, e) =>
            {
                if (!DownloadsFolder.IsInProgress(e.Name)) ScheduleRefresh();
            };
            _watcher.Created += changed;
            _watcher.Deleted += changed;
            _watcher.Changed += changed;
            _watcher.Renamed += (_, e) =>
            {
                if (!DownloadsFolder.IsInProgress(e.Name) || !DownloadsFolder.IsInProgress(e.OldName)) ScheduleRefresh();
            };
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException)
        {
            // Without a watcher the list still refreshes each time the popup opens.
            _watcher = null;
        }
    }

    private void ScheduleRefresh() => Dispatcher.BeginInvoke(() =>
    {
        _refreshDebounce.Stop();
        _refreshDebounce.Start();
    });

    private void OnFolderChanged()
    {
        _refreshDebounce.Stop();

        // Rebuilding the rows drops keyboard focus and open menus, so only do it for a real change.
        var items = DownloadsFolder.ReadRecent();
        if (Items.ItemsSource is IEnumerable<DownloadItem> current && current.SequenceEqual(items, SameListing.Instance)) return;

        ShowItems(items);
        if (IsVisible)
        {
            PositionNearAnchor();
            FocusFirstItem();
        }
    }

    private void ShowItems(IReadOnlyList<DownloadItem> items)
    {
        Items.ItemsSource = items;
        EmptyMessage.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _icons.Load(items);
    }

    private void PositionNearAnchor()
    {
        Native.POINT cursor = _anchor;
        IntPtr monitor = Native.MonitorFromPoint(cursor, Native.MONITOR_DEFAULTTONEAREST);
        var info = new Native.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFO>() };
        if (!Native.GetMonitorInfo(monitor, ref info)) return;
        Native.GetDpiForMonitor(monitor, Native.MDT_EFFECTIVE_DPI, out uint dpi, out _);
        double scale = dpi / 96.0;

        Native.RECT work = info.rcWork;
        int gap = (int)(ScreenGapDip * scale);

        Root.Measure(new Size(Width, double.PositiveInfinity));
        double availableDip = (work.Bottom - work.Top - 2 * gap) / scale;
        double heightDip = Math.Min(Math.Min(Root.DesiredSize.Height, MaxPopupHeight), availableDip);

        int width = (int)Math.Ceiling(Width * scale);
        int height = (int)Math.Ceiling(heightDip * scale);

        // The taskbar sits on the edge where the work area is inset from the monitor.
        Native.RECT mon = info.rcMonitor;
        int x = cursor.X - width / 2;
        int y = cursor.Y - height / 2;
        if (work.Top > mon.Top) y = work.Top + gap;
        else if (work.Left > mon.Left) x = work.Left + gap;
        else if (work.Right < mon.Right) x = work.Right - width - gap;
        else y = work.Bottom - height - gap;

        x = Math.Clamp(x, work.Left + gap, Math.Max(work.Left + gap, work.Right - width - gap));
        y = Math.Clamp(y, work.Top + gap, Math.Max(work.Top + gap, work.Bottom - height - gap));

        Native.SetWindowPos(_hwnd, IntPtr.Zero, x, y, width, height, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
    }

    private void BringToForeground()
    {
        Activate();
        if (Native.GetForegroundWindow() == _hwnd) return;

        // Fallback for launches without foreground rights, such as start at login followed by Win+number.
        uint foregroundThread = Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), IntPtr.Zero);
        uint thisThread = Native.GetCurrentThreadId();
        Native.AttachThreadInput(thisThread, foregroundThread, true);
        Native.SetForegroundWindow(_hwnd);
        Native.AttachThreadInput(thisThread, foregroundThread, false);
    }

    private void FocusFirstItem()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (Items.Items.Count == 0) return;
            if (Items.ItemContainerGenerator.ContainerFromIndex(0) is DependencyObject container)
            {
                FindButton(container)?.Focus();
            }
        });
    }

    private static Button? FindButton(DependencyObject parent)
    {
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is Button button) return button;
            if (FindButton(child) is Button nested) return nested;
        }
        return null;
    }

    // Hides on the app's own initiative; the resulting deactivation must not swallow the next launch.
    private void HideSilently()
    {
        _hidingSilently = true;
        Hide();
        _hidingSilently = false;
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_hidingSilently) return;

        if (IsPrimaryButtonDown())
        {
            _ignoreLaunchUntil = long.MaxValue;
            _mouseReleaseWatch.Start();
        }
        else
        {
            _ignoreLaunchUntil = Environment.TickCount64 + ToggleWindowMs;
        }
        Hide();
    }

    private void OnMouseReleaseTick()
    {
        if (IsPrimaryButtonDown()) return;
        _mouseReleaseWatch.Stop();
        _ignoreLaunchUntil = Environment.TickCount64 + ToggleWindowMs;
    }

    private static bool IsPrimaryButtonDown()
    {
        int button = Native.GetSystemMetrics(Native.SM_SWAPBUTTON) != 0 ? Native.VK_RBUTTON : Native.VK_LBUTTON;
        return (Native.GetAsyncKeyState(button) & 0x8000) != 0;
    }
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideSilently();
            e.Handled = true;
        }
    }

    private void OnItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DownloadItem item })
        {
            HideSilently();
            Launch(new ProcessStartInfo(item.Path) { UseShellExecute = true, ErrorDialog = true });
        }
    }

    private void OnShowInFolderClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DownloadItem item })
        {
            HideSilently();
            Launch(new ProcessStartInfo("explorer.exe", $"/select,\"{item.Path}\"") { UseShellExecute = true });
        }
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        HideSilently();
        Launch(new ProcessStartInfo(DownloadsFolder.Path) { UseShellExecute = true, ErrorDialog = true });
    }

    private static void Launch(ProcessStartInfo startInfo)
    {
        try
        {
            Process.Start(startInfo)?.Dispose();
        }
        catch (Win32Exception)
        {
            // The shell has already reported the failure, or the user cancelled.
        }
    }

    private void OnMenuClick(object sender, RoutedEventArgs e)
    {
        StartAtLoginItem.IsChecked = StartupRegistration.IsEnabled;
        ContextMenu menu = MenuButton.ContextMenu;
        menu.PlacementTarget = MenuButton;
        menu.IsOpen = true;
    }

    private void OnStartAtLoginClick(object sender, RoutedEventArgs e)
    {
        StartupRegistration.IsEnabled = StartAtLoginItem.IsChecked;
    }

    // A closed WPF window can't be shown again, so Alt+F4 and similar only hide the popup.
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_quitting) return;
        e.Cancel = true;
        HideSilently();
    }

    private void OnQuitClick(object sender, RoutedEventArgs e)
    {
        _quitting = true;
        Application.Current.Shutdown();
    }

    private sealed class SameListing : IEqualityComparer<DownloadItem>
    {
        public static readonly SameListing Instance = new();

        public bool Equals(DownloadItem? a, DownloadItem? b) =>
            a != null && b != null && a.Path == b.Path && a.Size == b.Size && a.Added == b.Added && a.Modified == b.Modified;

        public int GetHashCode(DownloadItem item) => item.Path.GetHashCode();
    }
}
