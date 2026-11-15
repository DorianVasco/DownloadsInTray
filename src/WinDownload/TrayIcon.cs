using System;
using System.Drawing;
using System.Windows.Forms;

namespace WinDownload;

/// <summary>Shows that the app is running. A left click toggles the popup, a right click opens its options.</summary>
internal sealed class TrayIcon : IDisposable
{
    private const string IconResource = "WinDownload.ico";

    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _icon;

    public TrayIcon(PopupWindow popup)
    {
        using var stream = typeof(TrayIcon).Assembly.GetManifestResourceStream(IconResource)!;
        _icon = new Icon(stream, SystemInformation.SmallIconSize);
        _notifyIcon = new NotifyIcon { Icon = _icon, Text = AppInfo.Name, Visible = true };
        _notifyIcon.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) popup.HandleLaunchRequest();
            else if (e.Button == MouseButtons.Right) popup.ShowOptionsMenu();
        };
    }

    public void Dispose()
    {
        _notifyIcon.Dispose();
        _icon.Dispose();
    }
}
