using System.Runtime.InteropServices;

namespace DownloadsInTray;

/// <summary>Whether the app runs from its MSIX package (the Microsoft Store version) or a normal install.</summary>
internal static class Packaging
{
    private const int AppModelErrorNoPackage = 15700;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);

    public static bool IsPackaged { get; } = Check();

    private static bool Check()
    {
        int length = 0;
        return GetCurrentPackageFullName(ref length, null) != AppModelErrorNoPackage;
    }
}
