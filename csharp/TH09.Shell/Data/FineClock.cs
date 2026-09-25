using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TH09.Shell.Data;

internal sealed class FineClock : IDisposable
{
    private const uint HighResolution = 0x2;
    private const uint TimerAllAccess = 0x1F0003;
    private const uint WaitObject0 = 0;

    private const uint WakeTimeoutMs = 50;

    private readonly Action _tick;
    private readonly Thread? _thread;
    private readonly IntPtr _timer;
    private readonly System.Threading.Timer? _fallback;
    private volatile bool _stop;

    public string Kind { get; }

    public FineClock(double periodMs, Action tick)
    {
        _tick = tick;
        var ms = Math.Max(0.5, periodMs);

        if (OperatingSystem.IsWindows())
        {
            _timer = Create();
            if (_timer != IntPtr.Zero)
            {
                long due = -(long)(ms * 10_000);
                if (SetWaitableTimer(_timer, ref due, (int)Math.Max(1, Math.Round(ms)),
                                     IntPtr.Zero, IntPtr.Zero, false))
                {
                    Kind = "高分解能の待ち時計";
                    _thread = new Thread(Loop)
                    {
                        IsBackground = true,
                        Name = "th09-playback-clock",
                        Priority = ThreadPriority.AboveNormal,
                    };
                    _thread.Start();
                    return;
                }
                CloseHandle(_timer);
                _timer = IntPtr.Zero;
            }
        }

        Kind = "普通のタイマ（分解能に縛られる）";
        _fallback = new System.Threading.Timer(_ => Beat(), null, 0,
                                               (int)Math.Max(1, Math.Round(ms)));
    }

    private void Loop()
    {
        while (!_stop)
        {
            if (WaitForSingleObject(_timer, WakeTimeoutMs) != WaitObject0) continue;
            if (_stop) return;
            Beat();
        }
    }

    private void Beat()
    {
        try { _tick(); }
        catch (InvalidOperationException) { }
    }

    public void Dispose()
    {
        _stop = true;
        _fallback?.Dispose();
        if (_timer == IntPtr.Zero) return;
        try { CancelWaitableTimer(_timer); } catch (EntryPointNotFoundException) { }
        if (_thread is null || _thread.Join(500)) CloseHandle(_timer);
    }

    [SupportedOSPlatform("windows")]
    private static IntPtr Create()
    {
        try { return CreateWaitableTimerExW(IntPtr.Zero, null, HighResolution, TimerAllAccess); }
        catch (EntryPointNotFoundException) { return IntPtr.Zero; }
        catch (DllNotFoundException) { return IntPtr.Zero; }
    }

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWaitableTimerExW(IntPtr attributes, string? name,
                                                        uint flags, uint access);

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWaitableTimer(IntPtr timer, ref long dueTime, int periodMs,
                                                IntPtr routine, IntPtr arg,
                                                [MarshalAs(UnmanagedType.Bool)] bool resume);

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CancelWaitableTimer(IntPtr timer);

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint ms);

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
