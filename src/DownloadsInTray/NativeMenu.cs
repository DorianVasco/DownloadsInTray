using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DownloadsInTray;

/// <summary>
/// A standard Windows popup menu, like Explorer's. Windows only draws these menus dark for apps
/// that opt in, through uxtheme functions exported by ordinal only. File Explorer and many tray
/// apps use the same calls.
/// </summary>
internal sealed class NativeMenu
{
    private const uint MF_STRING = 0x0000;
    private const uint MF_SEPARATOR = 0x0800;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_NONOTIFY = 0x0080;
    private const uint TPM_RETURNCMD = 0x0100;
    private const uint WM_NULL = 0x0000;
    private const int PreferredAppModeAllowDark = 1;

    private readonly List<(string? Label, Action? Action, bool IsDefault)> _entries = new();

    public NativeMenu Add(string label, Action action, bool isDefault = false)
    {
        _entries.Add((label, action, isDefault));
        return this;
    }

    public NativeMenu AddSeparator()
    {
        _entries.Add((null, null, false));
        return this;
    }

    /// <summary>Lets menus follow the system's dark or light app mode, now and whenever it changes.</summary>
    public static void FollowSystemTheme()
    {
        try
        {
            SetPreferredAppMode(PreferredAppModeAllowDark);
            FlushMenuThemes();
            SystemEvents.UserPreferenceChanged += (_, e) =>
            {
                if (e.Category != UserPreferenceCategory.General) return;
                RefreshImmersiveColorPolicyState();
                FlushMenuThemes();
            };
        }
        catch (EntryPointNotFoundException)
        {
            // Older Windows builds: menus stay light.
        }
    }

    /// <summary>Opts a window's menus into dark mode when the system uses it.</summary>
    public static void AllowDarkMenus(IntPtr hwnd)
    {
        try
        {
            AllowDarkModeForWindow(hwnd, true);
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    /// <summary>Shows the menu at a screen point and runs the chosen entry, if any, after the menu closes.</summary>
    public void Show(IntPtr owner, int x, int y)
    {
        IntPtr menu = CreatePopupMenu();
        try
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                (string? label, _, bool isDefault) = _entries[i];
                if (label == null)
                {
                    AppendMenu(menu, MF_SEPARATOR, UIntPtr.Zero, null);
                    continue;
                }

                // An ampersand marks a keyboard shortcut, so file names need theirs doubled.
                uint id = (uint)i + 1;
                AppendMenu(menu, MF_STRING, (UIntPtr)id, label.Replace("&", "&&"));
                if (isDefault) SetMenuDefaultItem(menu, id, false);
            }

            // Without the foreground, the menu wouldn't close on an outside click or Esc.
            SetForegroundWindow(owner);
            uint chosen = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_NONOTIFY, x, y, owner, IntPtr.Zero);
            PostMessage(owner, WM_NULL, IntPtr.Zero, IntPtr.Zero);

            if (chosen > 0) _entries[(int)chosen - 1].Action?.Invoke();
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    public void ShowAtCursor(IntPtr owner)
    {
        Native.GetCursorPos(out Native.POINT cursor);
        Show(owner, cursor.X, cursor.Y);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "AppendMenuW")]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool SetMenuDefaultItem(IntPtr hMenu, uint uItem, bool fByPos);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("uxtheme.dll", EntryPoint = "#104")]
    private static extern void RefreshImmersiveColorPolicyState();

    [DllImport("uxtheme.dll", EntryPoint = "#133")]
    private static extern bool AllowDarkModeForWindow(IntPtr hwnd, bool allow);

    [DllImport("uxtheme.dll", EntryPoint = "#135")]
    private static extern int SetPreferredAppMode(int mode);

    [DllImport("uxtheme.dll", EntryPoint = "#136")]
    private static extern void FlushMenuThemes();
}
