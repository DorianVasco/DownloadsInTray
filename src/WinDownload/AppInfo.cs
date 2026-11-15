using System.Reflection;

namespace WinDownload;

/// <summary>
/// The app's display name, taken from &lt;Product&gt; in WinDownload.csproj. Renaming the app there renames
/// the exe, its windows, menus, settings folder, start-at-login entry and the setup program.
/// </summary>
internal static class AppInfo
{
    public static string Name { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "WinDownload";
}
