using System;
using System.Linq;
using System.Threading;
using System.Windows;

namespace WinDownload;

/// <summary>
/// A pinned taskbar icon can only launch an exe, so every click starts a process.
/// The first process stays resident with a hidden popup; later ones signal it and exit
/// before WPF is loaded, which keeps the click-to-popup time low.
/// </summary>
internal static class Program
{
    private const string MutexName = @"Local\WinDownload.Instance";
    private const string ShowEventName = @"Local\WinDownload.Show";
    private const string BackgroundArgument = "--background";

    [STAThread]
    private static void Main(string[] args)
    {
        bool background = args.Contains(BackgroundArgument, StringComparer.OrdinalIgnoreCase);

        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool isFirstInstance);
        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);

        if (!isFirstInstance)
        {
            if (!background)
            {
                // This process was launched by the shell, so it holds the foreground right
                // that the resident process needs to activate its popup.
                Native.AllowSetForegroundWindow(Native.ASFW_ANY);
                showEvent.Set();
            }
            return;
        }

        RunResident(showEvent, showImmediately: !background);
    }

    private static void RunResident(EventWaitHandle showEvent, bool showImmediately)
    {
        var app = new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
            ThemeMode = ThemeMode.System,
        };

        var popup = new PopupWindow();
        popup.Prepare();

        var tray = new TrayIcon(popup);
        app.Exit += (_, _) => tray.Dispose();

        var listener = new Thread(() =>
        {
            while (true)
            {
                showEvent.WaitOne();
                app.Dispatcher.BeginInvoke(popup.HandleLaunchRequest);
            }
        })
        {
            IsBackground = true,
            Name = "LaunchListener",
        };
        listener.Start();

        if (showImmediately)
        {
            app.Dispatcher.BeginInvoke(popup.HandleLaunchRequest);
        }

        app.Run();
    }
}
