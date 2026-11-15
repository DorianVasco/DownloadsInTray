using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DownloadsInTray;

/// <summary>
/// Opts the process into EcoQoS while it waits in the tray, so Windows can run it on efficient cores
/// at low clock speeds and coalesce its timers, as it sees fit. The popup opts out for as long as it
/// is visible, so opening and scrolling stay quick.
/// </summary>
internal static class PowerMode
{
    private const int ProcessPowerThrottling = 4;
    private const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
    private const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;
    private const uint PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION = 0x4;

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_POWER_THROTTLING_STATE
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(IntPtr hProcess, int processInformationClass, ref PROCESS_POWER_THROTTLING_STATE info, int size);

    public static void Idle() => Apply(efficient: true);

    public static void Active() => Apply(efficient: false);

    private static void Apply(bool efficient)
    {
        uint mask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED | PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION;
        var state = new PROCESS_POWER_THROTTLING_STATE
        {
            Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
            ControlMask = mask,
            StateMask = efficient ? mask : 0,
        };

        using Process self = Process.GetCurrentProcess();
        SetProcessInformation(self.Handle, ProcessPowerThrottling, ref state, Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>());
    }
}
