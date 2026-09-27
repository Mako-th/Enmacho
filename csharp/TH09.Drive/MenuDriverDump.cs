using TH09.Generated;

namespace TH09.Drive;

internal sealed class FakeClock
{
    public double T;

    public double Now() => T;

    public void SleepFor(double sec) => T += Math.Max(sec, 0.0);
}

internal sealed class Trace
{
    private static readonly string Tab = ((char)9).ToString();
    private readonly TextWriter _w;
    private readonly string _scenario;
    private int _seq;

    public Trace(TextWriter w, string scenario)
    {
        _w = w;
        _scenario = scenario;
    }

    public int Count => _seq;

    public void Add(string ev, params string[] fields)
    {
        var row = new List<string> { "t", _scenario, _seq.ToString(), ev };
        row.AddRange(fields);
        _seq++;
        _w.WriteLine(string.Join(Tab, row));
    }

    public static string B(bool v) => v ? "1" : "0";

    public static string Tri(bool? v) => v is null ? "none" : (v.Value ? "1" : "0");
}

internal sealed class RecordingView : IMenuView
{
    private readonly IMenuView _inner;
    private readonly Trace _t;

    public RecordingView(IMenuView inner, Trace t)
    {
        _inner = inner;
        _t = t;
    }

    public MenuState Read()
    {
        MenuState s = _inner.Read();
        _t.Add("read", s.Text());
        return s;
    }

    public bool CellOccupied(int cell)
    {
        bool r = _inner.CellOccupied(cell);
        _t.Add("occupied", cell.ToString(), Trace.B(r));
        return r;
    }

    public bool ReplayPlaying()
    {
        bool r = _inner.ReplayPlaying();
        _t.Add("playing", Trace.B(r));
        return r;
    }

    public bool? TitleDemo()
    {
        bool? r = _inner.TitleDemo();
        _t.Add("demo", Trace.Tri(r));
        return r;
    }
}

internal sealed class RecordingSink : IInputSink
{
    private readonly IInputSink _inner;
    private readonly Trace _t;

    public RecordingSink(IInputSink inner, Trace t)
    {
        _inner = inner;
        _t = t;
    }

    public bool Tap(uint mask, int ticks = 0)
    {
        bool r = _inner.Tap(mask, ticks);
        _t.Add("tap", MenuMap.KeyName(mask), ticks.ToString(), Trace.B(r));
        return r;
    }

    public bool SetHold(uint mask, uint fields = 0)
    {
        bool r = _inner.SetHold(mask, fields);
        _t.Add("hold", MenuMap.KeyName(mask), fields.ToString(), Trace.B(r));
        return r;
    }

    public bool ReleaseAll()
    {
        bool r = _inner.ReleaseAll();
        _t.Add("release", Trace.B(r));
        return r;
    }

    public bool LockInput
    {
        get => _inner.LockInput;
        set
        {
            _inner.LockInput = value;
            _t.Add("lock", value ? "on" : "off");
        }
    }

    public bool Renew()
    {
        bool r = _inner.Renew();
        _t.Add("renew", Trace.B(r));
        return r;
    }

    public uint? HookState() => _inner.HookState();
}

internal sealed class CountingSink : IInputSink
{
    public List<uint> Taps { get; } = [];

    public List<(uint Mask, uint Fields)> Holds { get; } = [];

    public bool Tap(uint mask, int ticks = 0)
    {
        Taps.Add(mask);
        return true;
    }

    public bool SetHold(uint mask, uint fields = 0)
    {
        Holds.Add((mask, fields));
        return false;
    }

    public bool ReleaseAll()
    {
        Holds.Add((0u, 0u));
        LockInput = false;
        return false;
    }

    public bool LockInput { get; set; }
}

internal sealed class ScriptedView : IMenuView
{
    private readonly List<MenuState> _script;

    public int Reads { get; private set; }

    public ScriptedView(IEnumerable<MenuState> script) => _script = [.. script];

    public MenuState Read()
    {
        Reads++;
        if (_script.Count == 1) return _script[0];
        MenuState head = _script[0];
        _script.RemoveAt(0);
        return head;
    }

    public bool CellOccupied(int cell) => true;

    public bool ReplayPlaying() => false;
}

internal sealed class FrozenMenuView : IMenuView
{
    public const uint FrozenBase = 0x0AB00000u;

    private readonly int _validReads;
    private int _reads;

    public FrozenMenuView(int validReads) => _validReads = validReads;

    public MenuState Read()
    {
        _reads++;
        if (_reads <= _validReads)
        {
            return MenuMap.MakeState(FrozenBase, 9u, (uint)MenuMapLayout.SubDetail,
                                     (uint)MenuMapLayout.ScreenReplayList, 7u, 0u, 24u);
        }
        return MenuMap.InvalidState(
            "再生中に MainMenu が " + _reads + " 回 tick していません（anim=7 のまま。解放済みブロックの残骸）",
            FrozenBase);
    }

    public bool CellOccupied(int cell) => true;

    public bool ReplayPlaying() => true;
}

internal sealed class FailingSink : IInputSink
{
    private readonly uint? _hookState;

    public FailingSink(uint? hookState = null) => _hookState = hookState;

    public bool Tap(uint mask, int ticks = 0) => true;

    public bool SetHold(uint mask, uint fields = 0) => false;

    public bool ReleaseAll() => false;

    public bool Renew() => false;

    public bool LockInput { get; set; }

    public uint? HookState() => _hookState;
}

internal sealed class RenewFailsSink : IInputSink
{
    private readonly IInputSink _game;

    public int RenewCalls { get; private set; }

    public RenewFailsSink(IInputSink game) => _game = game;

    public bool Tap(uint mask, int ticks = 0) => _game.Tap(mask, ticks);

    public bool SetHold(uint mask, uint fields = 0) => _game.SetHold(mask, fields);

    public bool ReleaseAll() => _game.ReleaseAll();

    public bool Renew()
    {
        RenewCalls++;
        return false;
    }

    public bool LockInput
    {
        get => _game.LockInput;
        set => _game.LockInput = value;
    }
}

internal sealed class LegacyDriver : MenuDriver
{
    public LegacyDriver(IMenuView view, IInputSink sink, Action<string>? log = null,
                        Func<double>? clock = null, Action<double>? sleep = null,
                        double poll = 1 / 120.0, int tapTicks = 6)
        : base(view, sink, log, clock, sleep, poll, tapTicks) { }

    public override void StartPlayback(double timeout = 10.0)
    {
        MenuState state = Observe();
        if (!(state.Valid && state.Substate == MenuMapLayout.SubDetail))
            throw new UnexpectedScreen("概要画面に居ません: " + state.Text());
        Commit(MenuMapLayout.BitOk, s => !s.Valid || s.Substate != MenuMapLayout.SubDetail, timeout);
    }

    public override MenuState EnsureTitle(int maxCancels = 8, double timeout = 60.0)
    {
        double deadline = Clock() + Math.Max(0.0, timeout);
        int cancels = 0;
        while (true)
        {
            MenuState state = Observe();
            if (state.Valid && state.ScreenId == MenuMapLayout.ScreenTitle)
            {
                if (state.Substate == MenuMapLayout.SubReady && !state.Transitioning) return state;
            }
            else if (Sendable(state))
            {
                if (cancels >= maxCancels)
                {
                    throw new MenuLost("X を " + cancels + " 回送ってもタイトルへ戻れません（最後の状態: "
                                       + state.Text() + "）。");
                }
                Sink.Tap(MenuMapLayout.BitCancel, TapTicks);
                Taps++;
                cancels++;
                uint before = state.ScreenId;
                try
                {
                    WaitFor(s => !s.Valid || s.ScreenId != before,
                            Math.Min(2.0, Math.Max(0.0, deadline - Clock())));
                }
                catch (MenuTimeout) { }
                continue;
            }
            if (Clock() >= deadline)
            {
                throw new MenuLost("タイトルへ戻れないまま " + timeout.ToString("G6")
                                   + " 秒が過ぎました（最後の状態: " + state.Text() + "）");
            }
            Sleep(Poll);
        }
    }
}

internal sealed class LegacyListWait : MenuDriver
{
    public LegacyListWait(IMenuView view, IInputSink sink, Action<string>? log = null,
                          Func<double>? clock = null, Action<double>? sleep = null,
                          double poll = 1 / 120.0, int tapTicks = 6)
        : base(view, sink, log, clock, sleep, poll, tapTicks) { }

    public override MenuState WaitListAgain(double timeout = 900.0)
    {
        double deadline = Clock() + Math.Max(0.0, timeout);
        while (Clock() < deadline)
        {
            MenuState state = Observe();
            if (state.Valid && state.ScreenId == MenuMapLayout.ScreenReplayList
                && state.Substate == MenuMapLayout.SubReady && !state.Transitioning)
            {
                return state;
            }
            Sleep(Poll);
        }
        throw new MenuTimeout("旧実装: 期限切れ");
    }
}

internal static class MenuDriverDump
{
    private static readonly string Tab = ((char)9).ToString();

    private static void Row(TextWriter w, params string[] fields) => w.WriteLine(string.Join(Tab, fields));

    private static void Guard(Trace t, string name, string args, Action body)
    {
        t.Add("call", name, args);
        try
        {
            body();
            t.Add("ret", "");
        }
        catch (MenuDriverError exc)
        {
            t.Add("raise", exc.GetType().Name, "1", exc.Message);
        }
    }

    private static void GuardState(Trace t, string name, string args, Func<MenuState> body)
    {
        t.Add("call", name, args);
        try
        {
            MenuState st = body();
            t.Add("ret", st.Text());
        }
        catch (MenuDriverError exc)
        {
            t.Add("raise", exc.GetType().Name, "1", exc.Message);
        }
    }

    private static void GuardBool(Trace t, string name, string args, Func<bool> body)
    {
        t.Add("call", name, args);
        try
        {
            t.Add("ret", Trace.B(body()));
        }
        catch (MenuDriverError exc)
        {
            t.Add("raise", exc.GetType().Name, "1", exc.Message);
        }
    }

    private static (MenuDriver Drv, FakeClock Clk) MakeDriver(Trace t, IMenuView view, IInputSink sink,
                                                              double poll = 1 / 120.0)
    {
        var clk = new FakeClock();
        var drv = new MenuDriver(new RecordingView(view, t), new RecordingSink(sink, t),
                                 msg => t.Add("log", msg), clk.Now, clk.SleepFor, poll);
        return (drv, clk);
    }

    private static void DumpGame(Trace t, FakeGame g)
    {
        t.Add("count", "frame", g.Frame.ToString());
        t.Add("count", "taps", g.Taps.ToString());
        t.Add("count", "taps_while_invalid", g.TapsWhileInvalid.ToString());
        t.Add("count", "taps_while_entering", g.TapsWhileEntering.ToString());
        t.Add("count", "cancels_on_title", g.CancelsOnTitle.ToString());
        t.Add("count", "dropped_entering", g.DroppedEntering.ToString());
        t.Add("count", "dropped_gap", g.DroppedGap.ToString());
        t.Add("count", "dropped_random", g.DroppedRandom.ToString());
        t.Add("count", "dropped_forced", g.DroppedForced.ToString());
        t.Add("count", "dropped_demo", g.DroppedDemo.ToString());
        t.Add("count", "demo_ok_taps", g.DemoOkTaps.ToString());
        t.Add("count", "drop_next", g.DropNext.ToString());
        t.Add("count", "playbacks", g.Playbacks.ToString());
        t.Add("count", "rejected_opens", g.RejectedOpens.ToString());
        t.Add("count", "demo_frames_left", g.DemoFramesLeft.ToString());
        t.Add("count", "hold", g.Hold.ToString());
        t.Add("count", "hold_fields", g.HoldFields.ToString());
        t.Add("count", "valid", Trace.B(g.Valid));
        t.Add("count", "applied", string.Join(",", g.Applied));
        t.Add("count", "cells", string.Join(",", g.Cells.Keys.Order().Select(c => c + ":" + g.Cells[c])));
    }

    private static void DumpDriver(Trace t, MenuDriver d, FakeClock clk)
    {
        t.Add("count", "drv_taps", d.Taps.ToString());
        t.Add("count", "drv_resends", d.Resends.ToString());
        t.Add("count", "clock", clk.T.ToString("F6"));
    }


    private const uint Bait = 9u;
    private const uint ListBase = 0x0AC00000u;

    private static MenuState ListState(uint cursor) =>
        MenuMap.MakeState(ListBase, cursor, (uint)MenuMapLayout.SubReady,
                          (uint)MenuMapLayout.ScreenReplayList, 1u, 0u, 0u);

    private static MenuState Gone() =>
        MenuMap.InvalidState("再生中（MainMenu は解放済み）", ListBase);

    private static List<MenuState> ScriptStillReadable()
    {
        var s = new List<MenuState>();
        for (int i = 0; i < 3; i++) s.Add(ListState(Bait));
        for (int i = 0; i < 10; i++) s.Add(Gone());
        s.Add(ListState(0));
        return s;
    }

    private static List<MenuState> ScriptGarbageFlash()
    {
        var s = new List<MenuState>();
        for (int i = 0; i < 2; i++) s.Add(Gone());
        s.Add(ListState(Bait));
        for (int i = 0; i < 3; i++) s.Add(Gone());
        s.Add(ListState(0));
        return s;
    }

    private static Dictionary<int, string> FullCells()
    {
        var cells = new Dictionary<int, string>();
        for (int c = 0; c < MenuMapLayout.CellCount; c++) cells[c] = "x";
        return cells;
    }

    private static FakeGame StaleGame(bool clearSlots = true) =>
        new(screen: MenuMapLayout.ScreenReplayList,
            cells: new Dictionary<int, string> { [0] = "th9_01" },
            playbackFrames: 100000, staleBlock: true, clearSlots: clearSlots);

    private static void Happy(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenTitle,
                                cells: new Dictionary<int, string> { [0] = "th9_01", [3] = "th9_04" });
        var (drv, clk) = MakeDriver(t, game, game);
        GuardState(t, "ensure_title", "", () => drv.EnsureTitle());
        GuardState(t, "enter_replay_list", "", () => drv.EnterReplayList());
        GuardState(t, "open_cell", "0", () => drv.OpenCell(0));
        Guard(t, "start_playback", "", () => drv.StartPlayback());
        GuardState(t, "wait_list_again", "", () => drv.WaitListAgain());
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void DropsEntering(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenTitle,
                                cells: new Dictionary<int, string> { [0] = "a" }, entering: true);
        game.DropNext = 2;
        var (drv, clk) = MakeDriver(t, game, game);
        GuardState(t, "enter_replay_list", "", () => drv.EnterReplayList());
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void DropsGap(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenReplayList, cells: FullCells(),
                                gapFrames: 4, honorGap: false);
        var (drv, clk) = MakeDriver(t, game, game);
        GuardState(t, "move_cursor_to", "3", () => drv.MoveCursorTo(3));
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void DropsRandom(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenReplayList, cells: FullCells(),
                                dropRate: 0.35, seed: 7);
        var (drv, clk) = MakeDriver(t, game, game);
        GuardState(t, "move_cursor_to", "26", () => drv.MoveCursorTo(26));
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void DropsAll(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenReplayList,
                                cells: new Dictionary<int, string> { [0] = "a" }, dropRate: 1.0);
        var (drv, clk) = MakeDriver(t, game, game, poll: 0.05);
        GuardState(t, "open_cell", "0", () => drv.OpenCell(0));
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void SafetyEmptyCell(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenReplayList,
                                cells: new Dictionary<int, string> { [0] = "a" });
        var (drv, clk) = MakeDriver(t, game, game);
        GuardState(t, "open_cell", "5", () => drv.OpenCell(5));
        GuardState(t, "observe", "", () => drv.Observe());
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void SafetyPlaying(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenReplayList,
                                cells: new Dictionary<int, string> { [0] = "a" }, playbackFrames: 40);
        var (drv, clk) = MakeDriver(t, game, game);
        GuardState(t, "open_cell", "0", () => drv.OpenCell(0));
        Guard(t, "start_playback", "", () => drv.StartPlayback());
        GuardState(t, "wait_list_again", "", () => drv.WaitListAgain());
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void SafetyRecover(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenMusicRoom,
                                cells: new Dictionary<int, string> { [0] = "a" });
        var (drv, clk) = MakeDriver(t, game, game);
        GuardState(t, "ensure_title", "", () => drv.EnsureTitle());
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void SafetyLost(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenCharSelect,
                                cells: new Dictionary<int, string> { [0] = "a" }, dropRate: 1.0);
        var (drv, clk) = MakeDriver(t, game, game, poll: 0.05);
        GuardState(t, "ensure_title", "3", () => drv.EnsureTitle(maxCancels: 3));
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void Reenumerate(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenReplayList,
                                cells: new Dictionary<int, string> { [0] = "one" }, playbackFrames: 30);
        var (drv, clk) = MakeDriver(t, game, game);
        t.Add("label", "0", game.CellLabel(0) ?? "none");
        GuardState(t, "open_cell", "0", () => drv.OpenCell(0));
        Guard(t, "start_playback", "", () => drv.StartPlayback());
        game.Files[0] = "two";
        GuardState(t, "wait_list_again", "", () => drv.WaitListAgain());
        t.Add("label", "0", game.CellLabel(0) ?? "none");
        GuardState(t, "open_cell", "0", () => drv.OpenCell(0));
        Guard(t, "start_playback", "", () => drv.StartPlayback());
        GuardState(t, "wait_list_again", "", () => drv.WaitListAgain());
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void StaleLegacy(Trace t)
    {
        FakeGame game = StaleGame();
        var clk = new FakeClock();
        var drv = new LegacyDriver(new RecordingView(game, t), new RecordingSink(game, t),
                                   msg => t.Add("log", msg), clk.Now, clk.SleepFor, 0.05);
        GuardState(t, "open_cell", "0", () => drv.OpenCell(0));
        Guard(t, "start_playback", "", () => drv.StartPlayback());
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void StaleNew(Trace t)
    {
        FakeGame game = StaleGame();
        var (drv, clk) = MakeDriver(t, game, game);
        GuardState(t, "open_cell", "0", () => drv.OpenCell(0));
        t.Add("count", "taps_before", game.Taps.ToString());
        Guard(t, "start_playback", "", () => drv.StartPlayback());
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void StaleNoClear(Trace t)
    {
        FakeGame game = StaleGame(clearSlots: false);
        var (drv, clk) = MakeDriver(t, game, game);
        GuardState(t, "open_cell", "0", () => drv.OpenCell(0));
        Guard(t, "start_playback", "", () => drv.StartPlayback());
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void StaleDropOne(Trace t)
    {
        FakeGame game = StaleGame();
        var (drv, clk) = MakeDriver(t, game, game);
        GuardState(t, "open_cell", "0", () => drv.OpenCell(0));
        game.DropNext = 1;
        t.Add("count", "taps_before", game.Taps.ToString());
        Guard(t, "start_playback", "", () => drv.StartPlayback());
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void StaleLegacyNormal(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenReplayList,
                                cells: new Dictionary<int, string> { [0] = "a" });
        var clk = new FakeClock();
        var drv = new LegacyDriver(new RecordingView(game, t), new RecordingSink(game, t),
                                   msg => t.Add("log", msg), clk.Now, clk.SleepFor);
        GuardState(t, "open_cell", "0", () => drv.OpenCell(0));
        Guard(t, "start_playback", "", () => drv.StartPlayback());
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void FrozenRecovery(Trace t)
    {
        var view = new FrozenMenuView(validReads: 30);
        var sink = new CountingSink();
        var (drv, clk) = MakeDriver(t, view, sink);
        GuardState(t, "ensure_title", "8,5.0", () => drv.EnsureTitle(maxCancels: 8, timeout: 5.0));
        t.Add("count", "sink_taps", sink.Taps.Count.ToString());
        t.Add("count", "sink_holds", sink.Holds.Count.ToString());
        DumpDriver(t, drv, clk);
    }

    private static void ListReturnScript(Trace t, List<MenuState> script, bool legacy)
    {
        var view = new ScriptedView(script);
        var sink = new CountingSink();
        var clk = new FakeClock();
        MenuDriver drv = legacy
            ? new LegacyListWait(new RecordingView(view, t), new RecordingSink(sink, t),
                                 msg => t.Add("log", msg), clk.Now, clk.SleepFor)
            : new MenuDriver(new RecordingView(view, t), new RecordingSink(sink, t),
                             msg => t.Add("log", msg), clk.Now, clk.SleepFor);
        GuardState(t, "wait_list_again", "60.0", () => drv.WaitListAgain(60.0));
        t.Add("count", "reads", view.Reads.ToString());
        DumpDriver(t, drv, clk);
    }

    private static void ListReturnStable(Trace t)
    {
        var script = new List<MenuState> { Gone(), Gone(), ListState(0) };
        var view = new ScriptedView(script);
        var sink = new CountingSink();
        var (drv, clk) = MakeDriver(t, view, sink);
        Guard(t, "reset_list_return", "", () => drv.ResetListReturn());
        for (int i = 0; i < MenuDriver.StableReads + 3; i++)
            GuardBool(t, "list_returned", i.ToString(), () => drv.ListReturned());
        t.Add("count", "reads", view.Reads.ToString());
        DumpDriver(t, drv, clk);
    }

    private static void ListReturnGate(Trace t)
    {
        var view = new ScriptedView([ListState(0)]);
        var sink = new CountingSink();
        var (drv, clk) = MakeDriver(t, view, sink);
        Guard(t, "reset_list_return", "", () => drv.ResetListReturn());
        for (int i = 0; i < 50; i++)
            GuardBool(t, "list_returned", i.ToString(), () => drv.ListReturned());
        t.Add("count", "reads", view.Reads.ToString());
        DumpDriver(t, drv, clk);
    }

    private static void ListReturnReset(Trace t)
    {
        var view = new ScriptedView([Gone(), ListState(0)]);
        var sink = new CountingSink();
        var (drv, clk) = MakeDriver(t, view, sink);
        Guard(t, "reset_list_return", "", () => drv.ResetListReturn());
        int guard = 0;
        while (!drv.ListReturned())
        {
            guard++;
            if (guard > 100) break;
        }
        t.Add("count", "loops", guard.ToString());
        Guard(t, "reset_list_return", "", () => drv.ResetListReturn());
        GuardBool(t, "list_returned", "after_reset", () => drv.ListReturned());
        t.Add("count", "reads", view.Reads.ToString());
        DumpDriver(t, drv, clk);
    }

    private static void SkipAck(Trace t, uint? hookState)
    {
        var game = new FakeGame();
        var sink = new FailingSink(hookState);
        var (drv, clk) = MakeDriver(t, game, sink);
        Guard(t, "hold_skip", "", () => drv.HoldSkip());
        Guard(t, "release_skip", "", () => drv.ReleaseSkip());
        DumpDriver(t, drv, clk);
    }

    private static void SkipAckRenewFails(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenReplayList,
                                cells: new Dictionary<int, string> { [0] = "one" },
                                playbackFrames: 1300);
        var sink = new RenewFailsSink(game);
        var (drv, clk) = MakeDriver(t, game, sink);
        GuardState(t, "open_cell", "0", () => drv.OpenCell(0));
        Guard(t, "start_playback", "", () => drv.StartPlayback());
        Guard(t, "hold_skip", "", () => drv.HoldSkip());
        GuardState(t, "wait_list_again", "900.0", () => drv.WaitListAgain(900.0));
        t.Add("count", "renew_calls", sink.RenewCalls.ToString());
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void DemoLegacy(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenTitle);
        game.StartDemo();
        var clk = new FakeClock();
        var drv = new LegacyDriver(new RecordingView(game, t), new RecordingSink(game, t),
                                   msg => t.Add("log", msg), clk.Now, clk.SleepFor, 0.05);
        GuardState(t, "ensure_title", "5.0", () => drv.EnsureTitle(timeout: 5.0));
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void DemoNew(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenTitle);
        game.StartDemo();
        var (drv, clk) = MakeDriver(t, game, game);
        GuardState(t, "ensure_title", "5.0", () => drv.EnsureTitle(timeout: 5.0));
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void DemoUnknown(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenTitle);
        game.StartDemo(frames: 200, known: null);
        var (drv, clk) = MakeDriver(t, game, game);
        GuardState(t, "ensure_title", "30.0", () => drv.EnsureTitle(timeout: 30.0));
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void DemoRealPlayback(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenReplayList,
                                cells: new Dictionary<int, string> { [0] = "th9_01" },
                                playbackFrames: 40);
        var (drv, clk) = MakeDriver(t, game, game);
        GuardState(t, "open_cell", "0", () => drv.OpenCell(0));
        Guard(t, "start_playback", "", () => drv.StartPlayback());
        t.Add("count", "valid_after_start", Trace.B(game.Valid));
        GuardState(t, "ensure_title", "30.0", () => drv.EnsureTitle(timeout: 30.0));
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void UnexpectedPaths(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenTitle,
                                cells: new Dictionary<int, string> { [0] = "a" });
        var (drv, clk) = MakeDriver(t, game, game);
        Guard(t, "start_playback", "", () => drv.StartPlayback());
        GuardState(t, "leave_replay_list", "", () => drv.LeaveReplayList());
        GuardState(t, "select_start_stage", "3", () => drv.SelectStartStage(3));
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void LeaveListHappy(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenReplayList,
                                cells: new Dictionary<int, string> { [0] = "a" });
        var (drv, clk) = MakeDriver(t, game, game);
        GuardState(t, "open_cell", "0", () => drv.OpenCell(0));
        GuardState(t, "select_start_stage", "0", () => drv.SelectStartStage(0));
        GuardState(t, "leave_replay_list", "", () => drv.LeaveReplayList());
        GuardState(t, "wait_ready", "1", () => drv.WaitReady(MenuMapLayout.ScreenTitle, 5.0));
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void SelectStageTimeout(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenReplayList,
                                cells: new Dictionary<int, string> { [0] = "a" });
        var (drv, clk) = MakeDriver(t, game, game, poll: 0.05);
        GuardState(t, "open_cell", "0", () => drv.OpenCell(0));
        GuardState(t, "select_start_stage", "2", () => drv.SelectStartStage(2));
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void ApiDirect(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenReplayList, cells: FullCells());
        var (drv, clk) = MakeDriver(t, game, game);
        GuardState(t, "wait_for", "valid,1.0", () => drv.WaitFor(s => s.Valid, 1.0));
        GuardState(t, "commit", "down,5.0",
                   () => drv.Commit(MenuMapLayout.BitDown, s => s.Valid && s.Cursor == 1u, 5.0));
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void FakeGameDrops(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenTitle,
                                cells: new Dictionary<int, string> { [0] = "a" });
        var view = new RecordingView(game, t);
        var sink = new RecordingSink(game, t);
        view.Read();
        for (int i = 0; i < MenuMapLayout.TitleItemReplay; i++) sink.Tap(MenuMapLayout.BitDown);
        view.Read();
        sink.Tap(MenuMapLayout.BitOk);
        view.Read();
        sink.Tap(MenuMapLayout.BitDown);
        t.Add("count", "dropped_entering_here", game.DroppedEntering.ToString());
        game.StartDemo();
        sink.Tap(MenuMapLayout.BitDown);
        sink.Tap(MenuMapLayout.BitOk);
        DumpGame(t, game);
    }

    private static void AbortNotPlaying(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenReplayList,
                                cells: new Dictionary<int, string> { [0] = "a" });
        var (drv, clk) = MakeDriver(t, game, game);
        GuardState(t, "end_replay_playback", "not_playing",
                   () => drv.EndReplayPlayback("テスト: 再生していない"));
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void AbortDemo(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenTitle);
        game.StartDemo();
        var (drv, clk) = MakeDriver(t, game, game);
        GuardState(t, "end_replay_playback", "demo",
                   () => drv.EndReplayPlayback("テスト: デモ中"));
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void AbortSuccess(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenReplayList,
                                cells: new Dictionary<int, string> { [0] = "a" }, playbackFrames: 40);
        var (drv, clk) = MakeDriver(t, game, game);
        GuardState(t, "open_cell", "0", () => drv.OpenCell(0));
        Guard(t, "start_playback", "", () => drv.StartPlayback());
        GuardState(t, "end_replay_playback", "success",
                   () => drv.EndReplayPlayback("テスト: 走査が残した再生"));
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void AbortFailed(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenReplayList,
                                cells: new Dictionary<int, string> { [0] = "a" },
                                playbackFrames: 1000000);
        var (drv, clk) = MakeDriver(t, game, game, poll: 0.5);
        GuardState(t, "open_cell", "0", () => drv.OpenCell(0));
        Guard(t, "start_playback", "", () => drv.StartPlayback());
        GuardState(t, "end_replay_playback", "max_holds",
                   () => drv.EndReplayPlayback("テスト: 終わらない再生", maxHolds: 2));
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void AbortHoldNotAcked(Trace t)
    {
        var game = new FakeGame(screen: MenuMapLayout.ScreenReplayList,
                                cells: new Dictionary<int, string> { [0] = "a" },
                                playbackFrames: 1000000);
        var (setup, _) = MakeDriver(t, game, game);
        GuardState(t, "open_cell", "0", () => setup.OpenCell(0));
        Guard(t, "start_playback", "", () => setup.StartPlayback());
        var sink = new FailingSink(TickWords.HookStates.Idle);
        var clk = new FakeClock();
        var drv = new MenuDriver(new RecordingView(game, t), new RecordingSink(sink, t),
                                 msg => t.Add("log", msg), clk.Now, clk.SleepFor, 0.5);
        GuardState(t, "end_replay_playback", "not_acked",
                   () => drv.EndReplayPlayback("テスト: 保持が届かない", timeout: 60.0, maxHolds: 2));
        DumpGame(t, game);
        DumpDriver(t, drv, clk);
    }

    private static void DumpRandom(TextWriter w)
    {
        foreach (int seed in new[] { 7, 12345 })
        {
            var r1 = new MtRandom(seed);
            for (int i = 0; i < 1000; i++)
                Row(w, "mt", seed.ToString(), "random53", i.ToString(), r1.NextRandom53().ToString());
            var r2 = new MtRandom(seed);
            for (int i = 0; i < 1000; i++)
                Row(w, "mt", seed.ToString(), "getrandbits32", i.ToString(), r2.GetRandBits(32).ToString());
            var r3 = new MtRandom(seed);
            for (int i = 0; i < 1000; i++)
            {
                int a = r3.Choice([0, 3, 61234]);
                int b = r3.Choice([0, 1, 7]);
                int c = r3.Choice([4, 5, 8223]);
                Row(w, "mt", seed.ToString(), "garbage", i.ToString(), a + "," + b + "," + c);
            }
        }
    }

    public static int Run(TextWriter w)
    {
        var scenarios = new (string Name, Action<Trace> Body)[]
        {
            ("happy", Happy),
            ("drops_entering", DropsEntering),
            ("drops_gap", DropsGap),
            ("drops_random", DropsRandom),
            ("drops_all", DropsAll),
            ("safety_empty_cell", SafetyEmptyCell),
            ("safety_playing", SafetyPlaying),
            ("safety_recover", SafetyRecover),
            ("safety_lost", SafetyLost),
            ("reenumerate", Reenumerate),
            ("stale_legacy", StaleLegacy),
            ("stale_new", StaleNew),
            ("stale_no_clear", StaleNoClear),
            ("stale_drop_one", StaleDropOne),
            ("stale_legacy_normal", StaleLegacyNormal),
            ("frozen_recovery", FrozenRecovery),
            ("list_return_readable_legacy", t => ListReturnScript(t, ScriptStillReadable(), true)),
            ("list_return_readable_new", t => ListReturnScript(t, ScriptStillReadable(), false)),
            ("list_return_garbage_legacy", t => ListReturnScript(t, ScriptGarbageFlash(), true)),
            ("list_return_garbage_new", t => ListReturnScript(t, ScriptGarbageFlash(), false)),
            ("list_return_stable", ListReturnStable),
            ("list_return_gate", ListReturnGate),
            ("list_return_reset", ListReturnReset),
            ("skip_ack_idle", t => SkipAck(t, TickWords.HookStates.Idle)),
            ("skip_ack_armed", t => SkipAck(t, TickWords.HookStates.Armed)),
            ("skip_ack_running", t => SkipAck(t, TickWords.HookStates.Running)),
            ("skip_ack_nobus", t => SkipAck(t, null)),
            ("skip_ack_renew_fails", SkipAckRenewFails),
            ("demo_legacy", DemoLegacy),
            ("demo_new", DemoNew),
            ("demo_unknown", DemoUnknown),
            ("demo_real_playback", DemoRealPlayback),
            ("unexpected_paths", UnexpectedPaths),
            ("leave_list_happy", LeaveListHappy),
            ("select_stage_timeout", SelectStageTimeout),
            ("api_direct", ApiDirect),
            ("abort_not_playing", AbortNotPlaying),
            ("abort_demo", AbortDemo),
            ("abort_success", AbortSuccess),
            ("abort_failed", AbortFailed),
            ("abort_hold_not_acked", AbortHoldNotAcked),
            ("fake_game_drops", FakeGameDrops),
        };

        foreach (var (name, body) in scenarios)
        {
            var t = new Trace(w, name);
            body(t);
            Row(w, "scenario", name, t.Count.ToString());
        }

        DumpRandom(w);

        Row(w, "tickconst", "CMD_FIELD_MIRROR_P1", TickWords.CmdFieldBits.MirrorP1.ToString());
        Row(w, "tickconst", "STATE_IDLE", TickWords.HookStates.Idle.ToString());
        Row(w, "tickconst", "STATE_ARMED", TickWords.HookStates.Armed.ToString());
        Row(w, "tickconst", "STATE_RUNNING", TickWords.HookStates.Running.ToString());
        Row(w, "tickconst", "STATE_DETACHED", TickWords.HookStates.Detached.ToString());
        Row(w, "tickconst", "STATE_ERROR", TickWords.HookStates.Error.ToString());
        Row(w, "tickconst", "SKIP_HOLD_FIELDS", MenuDriver.SkipHoldFields.ToString());
        foreach (uint state in new[]
                 {
                     TickWords.HookStates.Idle, TickWords.HookStates.Armed,
                     TickWords.HookStates.Running, TickWords.HookStates.Detached,
                     TickWords.HookStates.Error, 4u, 99u,
                 })
            Row(w, "statetext", state.ToString(), TickWords.HookStates.Text(state));

        Row(w, "drvconst", "STABLE_READS", MenuDriver.StableReads.ToString());
        Row(w, "drvconst", "RENEW_SEC", MenuDriver.RenewSec.ToString("F6"));
        Row(w, "drvconst", "ABORT_TIMEOUT", MenuDriver.AbortTimeout.ToString("F6"));
        Row(w, "drvconst", "ABORT_MAX_HOLDS", MenuDriver.AbortMaxHolds.ToString());
        Row(w, "drvconst", "ABORT_HOLD_SEC", MenuDriver.AbortHoldSec.ToString("F6"));

        Row(w, "counts", "scenarios", scenarios.Length.ToString());
        return 0;
    }
}
