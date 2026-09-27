using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TH09.Shell.Data;

internal static class TimerResolution
{
    public static bool Wanted { get; } =
        Environment.GetEnvironmentVariable("TH09_FINE_TIMER") != "0";

    public const uint Milliseconds = 1;

    private static int _depth;
    private static readonly object _gate = new();

    public static void Begin()
    {
        if (!OperatingSystem.IsWindows() || !Wanted) return;
        lock (_gate)
        {
            if (_depth++ > 0) return;
            ThrottleOff();
            try { LastResult = (int)TimeBeginPeriod(Milliseconds); }
            catch (DllNotFoundException) { LastResult = -1; _depth = 0; }
            catch (EntryPointNotFoundException) { LastResult = -1; _depth = 0; }
        }
    }

    public static void End()
    {
        if (!OperatingSystem.IsWindows()) return;
        lock (_gate)
        {
            if (_depth == 0) return;
            if (--_depth > 0) return;
            ThrottleBack();
            try { TimeEndPeriod(Milliseconds); } catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
    }

    public static int LastResult { get; private set; } = -2;

    public static bool Active => _depth > 0;

    public static string ThrottleNote { get; private set; } = "まだ呼んでいない";

    private const int ProcessPowerThrottling = 4;
    private const uint ThrottlingVersion = 1;
    private const uint ExecutionSpeed = 0x1;
    private const uint IgnoreTimerResolution = 0x4;

    [StructLayout(LayoutKind.Sequential)]
    private struct PowerThrottlingState
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    private static void ThrottleOff() => Throttle(ExecutionSpeed | IgnoreTimerResolution, 0);

    private static void ThrottleBack() => Throttle(0, 0);

    private static void Throttle(uint control, uint state)
    {
        try
        {
            var info = new PowerThrottlingState
            {
                Version = ThrottlingVersion,
                ControlMask = control,
                StateMask = state,
            };
            bool ok = SetProcessInformation(GetCurrentProcess(), ProcessPowerThrottling,
                                            ref info, Marshal.SizeOf<PowerThrottlingState>());
            if (control != 0)
                ThrottleNote = ok ? "除外できた"
                                  : "断られた(" + Marshal.GetLastWin32Error() + ")";
        }
        catch (EntryPointNotFoundException) { ThrottleNote = "この Windows には無い"; }
        catch (DllNotFoundException) { ThrottleNote = "この Windows には無い"; }
    }

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessInformation(IntPtr process, int infoClass,
                                                     ref PowerThrottlingState info, int size);

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [SupportedOSPlatform("windows")]
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint ms);

    [SupportedOSPlatform("windows")]
    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint ms);
}
