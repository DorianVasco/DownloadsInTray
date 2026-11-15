using System;
using System.Drawing;
using System.Windows.Forms;

namespace DownloadsInTray;

/// <summary>Shows that the app is running. A left click toggles the popup, a right click opens its options.</summary>
internal sealed class TrayIcon : IDisposable
{
    private const string IconResource = "DownloadsInTray.ico";

    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _icon;

    public TrayIcon(PopupWindow popup)
    {
        _icon = DownloadsIcon.TrayIcon(SystemInformation.SmallIconSize.Width) ?? LoadOwnIcon();
        _notifyIcon = new NotifyIcon { Icon = _icon, Text = AppInfo.Name, Visible = true };
        _notifyIcon.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) popup.HandleLaunchRequest();
            else if (e.Button == MouseButtons.Right) popup.ShowOptionsMenu();
        };
    }

    private static Icon LoadOwnIcon()
    {
        using var stream = typeof(TrayIcon).Assembly.GetManifestResourceStream(IconResource)!;
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    public void Dispose()
    {
        _notifyIcon.Dispose();
        _icon.Dispose();
    }
}
