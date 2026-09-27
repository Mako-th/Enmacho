using System.Globalization;
using System.Runtime.Versioning;
using TH09.BusHost;
using TH09.Record;
using TH09.TickBus;

namespace TH09.Drive;

public sealed class ScanSetupFailed : Exception
{
    public ScanSetupFailed(string message) : base(message) { }
}

[SupportedOSPlatform("windows")]
public interface ISeatHost
{
    IDisposable TakeSeat(Action<string> log);

    (bool Ok, string Detail) EnsureHook(IDisposable seat, int pid, string nativeDir);

    (bool Ok, string Detail) SetVpatchSkip(int pid, string nativeDir, bool on);
}

[SupportedOSPlatform("windows")]
public sealed class BusHostSeats : ISeatHost
{
    public static readonly BusHostSeats Instance = new();

    public IDisposable TakeSeat(Action<string> log) => TickBusHost.EnsureHosted(log);

    public (bool Ok, string Detail) EnsureHook(IDisposable seat, int pid, string nativeDir)
    {
        if (seat is not BusSeat real)
        {
            throw new ArgumentException("本物の注入には本物の席が要ります（"
                                        + (seat?.GetType().Name ?? "null") + " が来ました）");
        }
        var run = InjectorControl.EnsureHookRunning(real, pid, nativeDir);
        return (run.Ok, run.Detail);
    }

    public (bool Ok, string Detail) SetVpatchSkip(int pid, string nativeDir, bool on)
        => InjectorControl.SetVpatchSkip(nativeDir, pid, on);

    public static IDisposable TakeCoordSeat(Action<string> log) =>
        TickBusHost.EnsureCoordHosted(log);
}

[SupportedOSPlatform("windows")]
public sealed class LivePorts : IDisposable
{
    public const double RewindSlackSeconds = 3.0;

    public const int RewindMaxTicks = 1200;

    public const double RewindTicksPerSecond = 60.0;

    public const string GameMissing = "エラー: th09.exe が見つかりません。ゲームを起動してください。";

    private readonly IDisposable _seat;
    private readonly Action<string> _log;
    private readonly double _startedAt;
    private readonly Func<double> _clock;
    private readonly Func<TickBusReader> _openBus;

    private LivePorts(IDisposable seat, Action<string> log, double startedAt, Func<double> clock,
                      Func<TickBusReader> openBus, string hookDetail)
    {
        _seat = seat;
        _log = log;
        _startedAt = startedAt;
        _clock = clock;
        _openBus = openBus;
        HookDetail = hookDetail;
    }

    public string HookDetail { get; }

    public sealed record Options(
        int Pid,
        Action<string> Log,
        double AttachDelaySec = 0.0,
        string? NativeDir = null,
        ISeatHost? Host = null,
        Action<double>? Sleep = null,
        Func<double>? Clock = null,
        Func<TickBusReader>? OpenBus = null);

    public static LivePorts Open(Options o)
    {
        ArgumentNullException.ThrowIfNull(o);
        var log = o.Log;
        var host = o.Host ?? BusHostSeats.Instance;
        var clock = o.Clock ?? (() => (double)Environment.TickCount64 / 1000.0);
        var startedAt = clock();
        if (o.Pid <= 0)
            throw new ScanSetupFailed(GameMissing);

        IDisposable seat = host.TakeSeat(log);
        try
        {
            if (o.AttachDelaySec > 0) (o.Sleep ?? DefaultSleep)(o.AttachDelaySec);
            var (ok, detail) = host.EnsureHook(seat, o.Pid, o.NativeDir ?? Paths.Default.NativeDir);
            if (!ok)
            {
                throw new ScanSetupFailed(
                    "エラー: tickフックを RUNNING にできません（" + detail + "）。" + Lf
                    + "       入力注入にはフックが必要です。--single で手動再生してください。");
            }
            return new LivePorts(seat, log, startedAt, clock,
                                 o.OpenBus ?? (() => TickBusReader.OpenVerified(TickBusReader.DefaultName)),
                                 detail);
        }
        catch
        {
            seat.Dispose();
            throw;
        }
    }

    public LiveTickSource OpenSource(bool adonisLoaded, Func<bool>? gameAlive)
    {
        var rewind = RewindTicks(_clock() - _startedAt);
        return new LiveTickSource(_openBus(), new LiveTickSource.Options(RingRewind: rewind), _log)
        {
            AdonisLoaded = adonisLoaded,
            GameAlive = gameAlive,
        };
    }

    public static int RewindTicks(double elapsedSeconds) =>
        Math.Min(RewindMaxTicks,
                 (int)((elapsedSeconds + RewindSlackSeconds) * RewindTicksPerSecond));

    public void Dispose() => _seat.Dispose();

    private static void DefaultSleep(double seconds)
    {
        var ms = (int)Math.Round(seconds * 1000.0);
        if (ms > 0) Thread.Sleep(ms);
    }

    private static readonly string Lf = ((char)10).ToString();
}

[SupportedOSPlatform("windows")]
public static class AutoPlay
{
    public const string VpatchLogPrefix = "[vpatch] ";

    public const string LockLogPrefix = "[lock] ";

    public static string StatusOf(Exception exc)
    {
        ArgumentNullException.ThrowIfNull(exc);
        if (exc is ScanSetupFailed) return CaptureStatus.SetupFailed;
        if (exc is MenuDriverError) return CaptureStatus.MenuFailed;
        return FailureStatus.Of(exc);
    }

    public sealed record Options(
        int Slot,
        int Pid,
        IMenuView View,
        Func<IInputSink> MakeSink,
        Func<Func<bool>, CaptureResult> Capture,
        Func<IDisposable?>? CoordRing = null,
        IMenuMemory? Mem = null,
        string? DecodedJson = null,
        bool NoSkip = false,
        string? NativeDir = null,
        ISeatHost? Host = null,
        Action<string>? Log = null,
        Func<double>? Clock = null,
        Action<double>? Sleep = null,
        double Poll = 1.0 / 120.0);

    public static CaptureResult Run(Options o)
    {
        ArgumentNullException.ThrowIfNull(o);
        var log = o.Log ?? MenuDriver.DefaultLog;
        var host = o.Host ?? BusHostSeats.Instance;
        var clock = o.Clock ?? (() => (double)Environment.TickCount64 / 1000.0);

        int cell = MenuMap.CellOfSlot(o.Slot);
        if (o.Pid <= 0)
            throw new ScanSetupFailed(LivePorts.GameMissing);

        _ = o.CoordRing?.Invoke();

        var ports = LivePorts.Open(new LivePorts.Options(
            Pid: o.Pid, Log: log, AttachDelaySec: 0.0, NativeDir: o.NativeDir,
            Host: host, Sleep: o.Sleep, Clock: clock));
        IInputSink? sink = null;
        bool vpatched = false;
        try
        {
            if (!o.NoSkip)
            {
                try
                {
                    var (vok, vdetail) = host.SetVpatchSkip(
                        o.Pid, o.NativeDir ?? Paths.Default.NativeDir, on: true);
                    vpatched = vok;
                    log(VpatchLogPrefix + (vok ? vdetail : "警告: " + vdetail));
                }
                catch (Exception exc)
                {
                    log(VpatchLogPrefix + "警告: vpatch の参照先を差し替えられません: "
                        + exc.GetType().Name + ": " + exc.Message);
                }
            }
            IInputSink live = o.MakeSink();
            sink = live;
            live.LockInput = true;
            log(LockLogPrefix + "走査の間ずっとパッドとキーボードを締め出します"
                + "（止めるときは器の「停止」。送り直しが 30 秒途切れれば自動で解けます）。");
            var drv = new MenuDriver(o.View, live, log, clock, o.Sleep, o.Poll);
            if (!o.NoSkip)
            {
                drv.SkipFields = vpatched ? 0u : MenuDriver.SkipHoldFields;
                log(VpatchLogPrefix + (vpatched
                    ? "ミラー（案A）は外します（vpatch が [2] を見ているので要りません）。"
                    : "ミラー（案A）を入れたまま走ります（差し替えが入らなかったため）。"));
            }

            ReplaySlots.RecoverLeftoverPlayback(drv, o.View, o.Mem, log);
            drv.EnsureTitle();
            drv.EnterReplayList();
            if (!o.View.CellOccupied(cell))
            {
                throw new ScanSetupFailed(
                    "エラー: セル " + cell.ToString(CultureInfo.InvariantCulture)
                    + "（スロット " + Slot2(o.Slot) + "）が空です。リプレイを置けていません。");
            }
            drv.OpenCell(cell);

            int wantStage = 0;
            var stages = ReplayStages.P1Stages(o.DecodedJson);
            if (stages.Count > 0) wantStage = (int)stages[0].Index;
            if (wantStage != 0)
            {
                log("このリプレイは " + Num(wantStage + 1) + " 面から始まります（先頭 "
                    + Num(wantStage) + " 面はデータ無し）。");
                drv.SelectStartStage(wantStage);
            }
            drv.StartPlayback();
            log("再生を開始しました（スロット " + Slot2(o.Slot) + " / セル "
                + Num(cell) + "）。");

            double lastRenew = 0.0;
            bool engaged = false;
            drv.ResetListReturn();

            bool BackInMenu()
            {
                double now = clock();
                if (!engaged)
                {
                    if (!o.NoSkip) drv.HoldSkip();
                    engaged = true;
                    lastRenew = now;
                    log(o.NoSkip ? "等速で再生します（--no-skip）。"
                                 : "捕捉が始まったので skip を入れます。");
                }
                else if (now - lastRenew >= MenuDriver.RenewSec)
                {
                    live.Renew();
                    lastRenew = now;
                }
                return drv.ListReturned();
            }

            return o.Capture(BackInMenu);
        }
        finally
        {
            if (sink is not null)
            {
                try { sink.ReleaseAll(); }
                catch (Exception) { }
            }
            if (vpatched)
            {
                try
                {
                    var (rok, rdetail) = host.SetVpatchSkip(
                        o.Pid, o.NativeDir ?? Paths.Default.NativeDir, on: false);
                    log(VpatchLogPrefix + (rok ? rdetail : "警告: " + rdetail));
                }
                catch (Exception exc)
                {
                    log(VpatchLogPrefix + "警告: vpatch の参照先を戻せません: " + exc.Message);
                }
            }
            ports.Dispose();
        }
    }


    private static string Slot2(int slot) => slot.ToString("D2", CultureInfo.InvariantCulture);

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);
}
