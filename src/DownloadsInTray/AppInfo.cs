using System.Reflection;

namespace DownloadsInTray;

/// <summary>
/// The app's names, taken from DownloadsInTray.csproj: &lt;Product&gt; is the display name shown in
/// windows, menus and the Start menu, and &lt;AssemblyName&gt; the file name, also used for the
/// settings folder and the start-at-login entry.
/// </summary>
internal static class AppInfo
{
    public static string Name { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "Downloads in Tray";

    public static string FileName { get; } = typeof(AppInfo).Assembly.GetName().Name ?? "DownloadsInTray";
}
