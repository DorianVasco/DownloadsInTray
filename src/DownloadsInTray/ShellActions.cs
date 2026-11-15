using System.ComponentModel;
using System.Diagnostics;

namespace DownloadsInTray;

/// <summary>Opens items the way Explorer does, so the shell handles file associations and security prompts.</summary>
internal static class ShellActions
{
    public static void Open(string path) =>
        Launch(new ProcessStartInfo(path) { UseShellExecute = true, ErrorDialog = true });

    public static void ShowInFolder(string path) =>
        Launch(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });

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
}
