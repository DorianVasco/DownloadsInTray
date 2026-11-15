using System;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace DownloadsInTray;

/// <summary>Tells whether a screen point is on this app's own taskbar or tray icon.</summary>
internal static class TaskbarProbe
{
    private static readonly string AppName = AppInfo.Name;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    /// <summary>UI Automation is slow on first use, so load it before the first click needs an answer.</summary>
    public static void WarmUp() => Task.Run(() => IsOwnIconAt(new Native.POINT()));

    /// <summary>Runs off the UI thread because a query can take hundreds of milliseconds.</summary>
    public static async Task<bool> IsOwnIconAsync(Native.POINT point)
    {
        try
        {
            return await Task.Run(() => IsOwnIconAt(point)).WaitAsync(Timeout);
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static bool IsOwnIconAt(Native.POINT point)
    {
        try
        {
            var screenPoint = new System.Windows.Point(point.X, point.Y);

            // Hit testing often stops at the taskbar window, so look for the icon inside it.
            // Other windows can carry the app's name too, such as Explorer showing this project's folder.
            AutomationElement? taskbar = AutomationElement.FromPoint(screenPoint);
            for (AutomationElement? parent = taskbar; parent != null && parent != AutomationElement.RootElement;
                 parent = TreeWalker.ControlViewWalker.GetParent(parent))
            {
                taskbar = parent;
            }
            if (taskbar?.Current.ClassName is not ("Shell_TrayWnd" or "Shell_SecondaryTrayWnd")) return false;

            var isButton = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button);
            foreach (AutomationElement button in taskbar.FindAll(TreeScope.Descendants, isButton))
            {
                if (button.Current.Name.StartsWith(AppName, StringComparison.OrdinalIgnoreCase) &&
                    button.Current.BoundingRectangle.Contains(screenPoint))
                {
                    return true;
                }
            }
        }
        catch (Exception e) when (e is ElementNotAvailableException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            // Treated as a click elsewhere: at worst the popup reopens.
        }
        return false;
    }
}
