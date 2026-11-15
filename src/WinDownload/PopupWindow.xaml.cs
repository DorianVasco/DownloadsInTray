using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    // a process (mouse up) whose request must not reopen it. The launch can take over a second, so
    // a click on the app's own icon waits that long. Any other click only needs to cover a race,
    // so that the next click on the icon always opens the popup.
    private const int OwnIconToggleWindowMs = 3000;
    private const int OtherToggleWindowMs = 150;

    private readonly ShellIconLoader _icons;
    private readonly ToastWindow _toast = new();
    private readonly NewDownloadTracker _newDownloads = new();
    private readonly ActiveDownloads _active = new();
    private readonly DispatcherTimer _activePoll;
    private SettingsWindow? _settings;
    private readonly DispatcherTimer _refreshDebounce;
    private readonly DispatcherTimer _mouseReleaseWatch;
    private FileSystemWatcher? _watcher;
    private IntPtr _hwnd;
    private Task<bool>? _closingClickProbe;
    private long _closingClickReleasedAt;
    private bool _hidingSilently;
    private bool _quitting;
    private Point? _dragStart;
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
        ActiveItems.ItemsSource = _active.Items;
        _activePoll = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => PollActive(), Dispatcher);
        _activePoll.Stop();
        IsVisibleChanged += (_, _) =>
        {
            // Partial files are only polled while someone can see the progress.
            if (IsVisible) _activePoll.Start();
            else _activePoll.Stop();
        };
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

        Native.ApplyFlyoutStyle(_hwnd);
        _toast.Prepare();
        _toast.ShowListRequested += ShowFromToast;

        TaskbarProbe.WarmUp();
        StartWatching();
        ShowItems(DownloadsFolder.ReadRecent());
    }
    public async void HandleLaunchRequest()
    {
        if (IsVisible)
        {
            HideSilently();
            return;
        }

        Task<bool>? closingClickProbe = _closingClickProbe;
        ClearClosingClick();
        if (closingClickProbe != null)
        {
            long requestedAt = Environment.TickCount64;
            long releasedAt = _closingClickReleasedAt;
            int window = await closingClickProbe ? OwnIconToggleWindowMs : OtherToggleWindowMs;
            if (requestedAt - releasedAt < window) return;
        }

        ShowPopup();
    }

    /// <summary>Opens the popup with its options menu, for a right-click on the tray icon.</summary>
    public void ShowOptionsMenu()
    {
        ClearClosingClick();
        if (!IsVisible) ShowPopup();
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => OnMenuClick(MenuButton, new RoutedEventArgs()));
    }

    private void ShowFromToast()
    {
        ClearClosingClick();
        if (!IsVisible) ShowPopup();
    }

    private void ClearClosingClick()
    {
        _mouseReleaseWatch.Stop();
        _closingClickProbe = null;
    }

    private void ShowPopup()
    {
        _toast.Dismiss();
        Native.GetCursorPos(out _anchor);
        ShowItems(DownloadsFolder.ReadRecent());
        PollActive();
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
                Dispatcher.BeginInvoke(() => _newDownloads.OnRenamed(e.OldFullPath, e.FullPath));
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
        if (Items.ItemsSource is not IEnumerable<DownloadItem> current || !current.SequenceEqual(items, SameListing.Instance))
        {
            ShowItems(items);
            if (IsVisible)
            {
                PositionNearAnchor();
                FocusFirstItem();
            }
        }

        // The open popup already shows new items as they arrive.
        IReadOnlyList<string> added = _newDownloads.TakeNew();
        if (added.Count > 0 && !IsVisible) _toast.Announce(added, items);
    }

    private void ShowItems(IReadOnlyList<DownloadItem> items)
    {
        Items.ItemsSource = items;
        EmptyMessage.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _icons.Load(items);
    }

    private void PollActive()
    {
        int before = _active.Items.Count;
        _active.Poll();
        ActiveItems.Visibility = _active.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (IsVisible && _active.Items.Count != before) PositionNearAnchor();
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

        Hide();

        Native.GetCursorPos(out Native.POINT cursor);
        _closingClickProbe = TaskbarProbe.IsOwnIconAsync(cursor);

        if (IsPrimaryButtonDown())
        {
            _closingClickReleasedAt = long.MaxValue;
            _mouseReleaseWatch.Start();
        }
        else
        {
            _closingClickReleasedAt = Environment.TickCount64;
        }
    }

    private void OnMouseReleaseTick()
    {
        if (IsPrimaryButtonDown()) return;
        _mouseReleaseWatch.Stop();
        _closingClickReleasedAt = Environment.TickCount64;
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
            ShellActions.Open(item.Path);
        }
    }

    private void OnRowMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
    }

    private void OnRowMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is not Point start || e.LeftButton != MouseButtonState.Pressed)
        {
            _dragStart = null;
            return;
        }

        Vector moved = e.GetPosition(this) - start;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _dragStart = null;
        if (sender is Button { DataContext: DownloadItem item } row) DragToOtherApp(row, item);
    }

    private void DragToOtherApp(Button row, DownloadItem item)
    {
        DragDropEffects result = FileDrag.Run(row, new[] { item.Path }, _hwnd, HideSilently);
        if (IsVisible && result != DragDropEffects.None) HideSilently();
    }

    private void OnShowInFolderClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DownloadItem item })
        {
            HideSilently();
            ShellActions.ShowInFolder(item.Path);
        }
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        HideSilently();
        ShellActions.Open(DownloadsFolder.Path);
    }

    private void OnMenuClick(object sender, RoutedEventArgs e)
    {
        ContextMenu menu = MenuButton.ContextMenu;
        menu.PlacementTarget = MenuButton;
        menu.IsOpen = true;
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        HideSilently();
        if (_settings == null)
        {
            _settings = new SettingsWindow();
            _settings.Closed += (_, _) => _settings = null;
        }
        _settings.Show();
        _settings.Activate();
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
