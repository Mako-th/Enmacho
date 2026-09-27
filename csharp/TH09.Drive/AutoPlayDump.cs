using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using TH09.Generated;
using TH09.Record;

namespace TH09.Drive;

[SupportedOSPlatform("windows")]
internal sealed class FakeSeats : ISeatHost
{
    private readonly Trace _t;
    private readonly bool _ok;
    private readonly bool _vpatchOk;
    private readonly string _detail;

    public FakeSeats(Trace t, bool ok, string detail, bool? vpatchOk = null)
    {
        _t = t;
        _ok = ok;
        _vpatchOk = vpatchOk ?? ok;
        _detail = detail;
    }

    public int Seats { get; private set; }

    public int Hooks { get; private set; }

    public IDisposable TakeSeat(Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(log);
        Seats++;
        _t.Add("seat");
        return new FakeSeat(_t);
    }

    public (bool Ok, string Detail) EnsureHook(IDisposable seat, int pid, string nativeDir)
    {
        Hooks++;
        _t.Add("hook", Num(pid), Trace.B(_ok), _detail);
        return (_ok, _detail);
    }

    public (bool Ok, string Detail) SetVpatchSkip(int pid, string nativeDir, bool on)
    {
        _t.Add("vpatch", Num(pid), on ? "on" : "off", Trace.B(_vpatchOk));
        return (_vpatchOk, _vpatchOk ? "vpatch（偽）: " + (on ? "on" : "off")
                                     : "vpatch（偽）: できません");
    }

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);
}

internal sealed class FakeSeat : IDisposable
{
    private readonly Trace _t;
    private bool _closed;

    public FakeSeat(Trace t) => _t = t;

    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
        _t.Add("seat_closed");
    }
}

internal sealed class PathMemory : IMenuMemory
{
    private readonly byte[] _bytes;

    public PathMemory(string path)
    {
        var raw = Encoding.ASCII.GetBytes(path);
        _bytes = new byte[MenuMapLayout.ReplayPathMax];
        Array.Copy(raw, _bytes, Math.Min(raw.Length, _bytes.Length - 1));
    }

    public bool TryReadUInt32(uint address, out uint value, out string error)
    {
        value = 0;
        error = "この読み口はパスしか返しません";
        return false;
    }

    public bool TryReadBytes(uint address, int size, out byte[] value, out string error)
    {
        if (address != MenuMapLayout.AReplayPath || size > _bytes.Length)
        {
            value = [];
            error = "この読み口はパスしか返しません";
            return false;
        }
        value = _bytes[..size];
        error = "";
        return true;
    }
}

[SupportedOSPlatform("windows")]
internal static class AutoPlayDump
{
    public const string Flag = "--dump-auto-play";

    public const double Poll = 1.0 / 120.0;

    public const int MaxStops = 40;

    public const double StopAdvanceSeconds = 4.0;

    public const int ScriptPid = 4321;

    private static readonly string Tab = ((char)9).ToString();

    private static void Row(TextWriter w, params string[] fields) => w.WriteLine(string.Join(Tab, fields));


    public const string DecodedHead =
        "{\"stages\": [{\"index\": 0, \"score\": 0}, {\"index\": 1, \"score\": 500}]}";

    public const string DecodedMid =
        "{\"stages\": [{\"index\": 0, \"score\": 0}, {\"index\": 1, \"score\": 0},"
        + " {\"index\": 2, \"score\": 12345}]}";

    public const string HookOkDetail = "台本: 既に稼働中";

    public const string HookNgDetail = "台本: hook_state=IDLE / bus=not mapped";

    public const string LeftoverFile = "th9_01.rpy";

    public const string LeftoverWhy = "th9_01.rpy.tickscan.owned が残っている（台本）";

    public const string SamePath = "replay/th9_01.rpy";

    public const string OtherPath = "replay/th9_09.rpy";


    private static Func<Func<bool>, CaptureResult> ScriptedCapture(Trace t, FakeClock clk)
        => stop =>
        {
            t.Add("capture");
            for (int i = 0; i < MaxStops; i++)
            {
                if (i > 0) clk.T += StopAdvanceSeconds;
                bool done = stop();
                t.Add("stop", Trace.B(done));
                if (done) break;
            }
            return Captured();
        };

    private static CaptureResult Captured() => new()
    {
        SessionId = 4242,
        Status = CaptureStatus.Captured,
        GapCount = 7,
        SourceKind = "tick",
        Elapsed = 1.5,
    };

    private static AutoPlay.Options Make(Trace t, FakeClock clk, FakeGame game, FakeSeats seats,
                                         int slot, string decodedJson, string path,
                                         bool noSkip = false)
        => new(
            Slot: slot,
            Pid: ScriptPid,
            View: new RecordingView(game, t),
            MakeSink: () => new RecordingSink(game, t),
            Capture: ScriptedCapture(t, clk),
            CoordRing: () => { t.Add("coord"); return null; },
            Mem: new PathMemory(path),
            DecodedJson: decodedJson,
            NoSkip: noSkip,
            NativeDir: "",
            Host: seats,
            Log: msg => t.Add("log", msg),
            Clock: clk.Now,
            Sleep: clk.SleepFor,
            Poll: Poll);

    private static void Guard(Trace t, AutoPlay.Options options)
    {
        try
        {
            CaptureResult r = AutoPlay.Run(options);
            t.Add("ret", r.Status, Num(r.SessionId), Num(r.GapCount), r.SourceKind ?? "None");
        }
        catch (ScanSetupFailed exc)
        {
            t.Add("raise", nameof(ScanSetupFailed), AutoPlay.StatusOf(exc), Esc(exc.Message));
        }
        catch (MenuDriverError exc)
        {
            t.Add("raise", exc.GetType().Name, AutoPlay.StatusOf(exc), Esc(exc.Message));
        }
    }

    private static string Esc(string text)
        => text.Replace(((char)13).ToString(), "<CR>")
               .Replace(((char)10).ToString(), "<LF>")
               .Replace(((char)9).ToString(), "<TAB>");

    private static void DumpCounts(Trace t, FakeGame g, FakeClock clk)
    {
        t.Add("count", "taps", Num(g.Taps));
        t.Add("count", "taps_while_invalid", Num(g.TapsWhileInvalid));
        t.Add("count", "taps_while_entering", Num(g.TapsWhileEntering));
        t.Add("count", "cancels_on_title", Num(g.CancelsOnTitle));
        t.Add("count", "playbacks", Num(g.Playbacks));
        t.Add("count", "rejected_opens", Num(g.RejectedOpens));
        t.Add("count", "hold", Num((int)g.Hold));
        t.Add("count", "hold_fields", Num((int)g.HoldFields));
        t.Add("count", "valid", Trace.B(g.Valid));
        t.Add("count", "frame", Num(g.Frame));
        t.Add("count", "applied", string.Join(",", g.Applied));
        t.Add("count", "clock", clk.T.ToString("F6", CultureInfo.InvariantCulture));
    }

    private static void StartLeftoverPlayback(Trace t, FakeClock clk, FakeGame game, int cell)
    {
        var setup = new MenuDriver(new RecordingView(game, t), new RecordingSink(game, t),
                                   msg => t.Add("log", msg), clk.Now, clk.SleepFor, Poll);
        setup.OpenCell(cell);
        setup.StartPlayback();
    }


    private static void Head(Trace t)
    {
        var clk = new FakeClock();
        var game = new FakeGame(screen: MenuMapLayout.ScreenTitle,
                                cells: new Dictionary<int, string> { [0] = "a" }, playbackFrames: 8);
        var seats = new FakeSeats(t, true, HookOkDetail);
        Guard(t, Make(t, clk, game, seats, slot: 1, DecodedHead, SamePath));
        DumpCounts(t, game, clk);
        DumpSeats(t, seats);
    }

    private static void MidStart(Trace t)
    {
        var clk = new FakeClock();
        var game = new FakeGame(screen: MenuMapLayout.ScreenTitle,
                                cells: new Dictionary<int, string> { [2] = "b" }, playbackFrames: 8);
        var seats = new FakeSeats(t, true, HookOkDetail);
        Guard(t, Make(t, clk, game, seats, slot: 3, DecodedMid, SamePath));
        DumpCounts(t, game, clk);
        DumpSeats(t, seats);
    }

    private static void EmptyCellScript(Trace t)
    {
        var clk = new FakeClock();
        var game = new FakeGame(screen: MenuMapLayout.ScreenTitle, playbackFrames: 8);
        var seats = new FakeSeats(t, true, HookOkDetail);
        Guard(t, Make(t, clk, game, seats, slot: 1, DecodedHead, SamePath));
        DumpCounts(t, game, clk);
        DumpSeats(t, seats);
    }

    private static void LeftoverSame(Trace t)
    {
        var clk = new FakeClock();
        var game = new FakeGame(screen: MenuMapLayout.ScreenReplayList,
                                cells: new Dictionary<int, string> { [0] = "a" }, playbackFrames: 6);
        StartLeftoverPlayback(t, clk, game, cell: 0);
        ReplaySlots.NoteLeftoverEvidence(LeftoverFile, LeftoverWhy);
        var seats = new FakeSeats(t, true, HookOkDetail);
        Guard(t, Make(t, clk, game, seats, slot: 1, DecodedHead, SamePath));
        DumpCounts(t, game, clk);
        DumpSeats(t, seats);
    }

    private static void LeftoverOther(Trace t)
    {
        var clk = new FakeClock();
        var game = new FakeGame(screen: MenuMapLayout.ScreenReplayList,
                                cells: new Dictionary<int, string> { [0] = "a" }, playbackFrames: 6);
        StartLeftoverPlayback(t, clk, game, cell: 0);
        ReplaySlots.NoteLeftoverEvidence(LeftoverFile, LeftoverWhy);
        var seats = new FakeSeats(t, true, HookOkDetail);
        Guard(t, Make(t, clk, game, seats, slot: 1, DecodedHead, OtherPath));
        DumpCounts(t, game, clk);
        DumpSeats(t, seats);
    }

    private static void NoSkip(Trace t)
    {
        var clk = new FakeClock();
        var game = new FakeGame(screen: MenuMapLayout.ScreenTitle,
                                cells: new Dictionary<int, string> { [0] = "a" }, playbackFrames: 8);
        var seats = new FakeSeats(t, true, HookOkDetail);
        Guard(t, Make(t, clk, game, seats, slot: 1, DecodedHead, SamePath, noSkip: true));
        DumpCounts(t, game, clk);
        DumpSeats(t, seats);
    }

    private static void VpatchNg(Trace t)
    {
        var clk = new FakeClock();
        var game = new FakeGame(screen: MenuMapLayout.ScreenTitle,
                                cells: new Dictionary<int, string> { [0] = "a" }, playbackFrames: 8);
        var seats = new FakeSeats(t, true, HookOkDetail, vpatchOk: false);
        Guard(t, Make(t, clk, game, seats, slot: 1, DecodedHead, SamePath));
        DumpCounts(t, game, clk);
        DumpSeats(t, seats);
    }

    private static void HookFails(Trace t)
    {
        var clk = new FakeClock();
        var game = new FakeGame(screen: MenuMapLayout.ScreenTitle,
                                cells: new Dictionary<int, string> { [0] = "a" }, playbackFrames: 8);
        var seats = new FakeSeats(t, false, HookNgDetail);
        Guard(t, Make(t, clk, game, seats, slot: 1, DecodedHead, SamePath));
        DumpCounts(t, game, clk);
        DumpSeats(t, seats);
    }

    private static void DumpSeats(Trace t, FakeSeats seats)
    {
        t.Add("count", "fake_seats", Num(seats.Seats));
        t.Add("count", "fake_hooks", Num(seats.Hooks));
    }

    public static int Run(TextWriter w)
    {
        var scenarios = new (string Name, Action<Trace> Body)[]
        {
            ("head", Head),
            ("mid_start", MidStart),
            ("empty_cell", EmptyCellScript),
            ("leftover_same", LeftoverSame),
            ("leftover_other", LeftoverOther),
            ("no_skip", NoSkip),
            ("vpatch_ng", VpatchNg),
            ("hook_fails", HookFails),
        };

        foreach (var (name, body) in scenarios)
        {
            ReplaySlots.TakeLeftoverEvidence();
            var t = new Trace(w, name);
            body(t);
            Row(w, "scenario", name, t.Count.ToString(CultureInfo.InvariantCulture));
        }

        Row(w, "autoconst", "RENEW_SEC", MenuDriver.RenewSec.ToString("F6", CultureInfo.InvariantCulture));
        Row(w, "autoconst", "SETUP_FAILED", CaptureStatus.SetupFailed);
        Row(w, "autoconst", "MENU_FAILED", CaptureStatus.MenuFailed);
        Row(w, "autoconst", "CAPTURED", CaptureStatus.Captured);
        Row(w, "counts", "scenarios", scenarios.Length.ToString(CultureInfo.InvariantCulture));
        Row(w, "counts", "max_stops", MaxStops.ToString(CultureInfo.InvariantCulture));
        Row(w, "counts", "stop_advance",
            StopAdvanceSeconds.ToString("F6", CultureInfo.InvariantCulture));
        Row(w, "counts", "pid", ScriptPid.ToString(CultureInfo.InvariantCulture));
        return 0;
    }

    private static string Num(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Num(long? value) =>
        value is null ? "None" : value.Value.ToString(CultureInfo.InvariantCulture);
}
