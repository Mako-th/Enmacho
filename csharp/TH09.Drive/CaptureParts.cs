using System.Globalization;
using System.Runtime.Versioning;
using TH09.Layer0;
using TH09.Record;
using TH09.TickBus;

using Hints = TH09.Generated.SectionHints;

namespace TH09.Drive;

public sealed record HitWindowOverrides(
    bool? Enabled = null,
    int? Before = null,
    int? After = null,
    bool? Quick = null,
    IReadOnlyDictionary<string, bool>? Scopes = null);

public sealed record WindowPlan(
    bool Enabled,
    int Before,
    int After,
    IReadOnlyList<string> Triggers,
    IReadOnlyDictionary<string, bool> Scopes,
    IReadOnlyList<string> Notes);

[SupportedOSPlatform("windows")]
public sealed class CaptureParts : IDisposable
{
    public const string NoPollingFallback =
        "（この道具はポーリングへ降格しません）。"
        + "tick フックが RUNNING にならない回は、その 1 本を失敗として記録します。";

    public const string TickHookOffUnsupported =
        "エラー: --tick-hook off は走査では使えません" + NoPollingFallback;

    public const string TickHookOff = "off";

    public const string Layer0DbMissing =
        "エラー: 走査が書く先（Layer 0）が渡されていません。"
        + "CaptureParts.PrepareLayer0 が返した Destination を Options.Layer0Db に入れてください。";

    public const string NoMenuReadReason =
        "注意: ゲームのメモリを読めないので、タイトルへ戻ったかどうかが分かりません。"
        + "この 1 本は、ゲームが終了したときにだけ閉じます（時間では閉じません）。";

    public const int SegmentTicks = 20000;

    private readonly Options _o;
    private readonly Action<string> _log;
    private readonly IDisposable? _coordSeat;
    private readonly HitWindows _windows;
    private readonly List<LivePorts> _ports = [];
    private readonly string? _layer0Db;
    private int _openedPorts;
    private bool _built;

    private CaptureParts(Options o, Action<string> log, IDisposable? coordSeat,
                         HitWindows windows, int pid, string? layer0Db)
    {
        _o = o;
        _log = log;
        _coordSeat = coordSeat;
        _windows = windows;
        _layer0Db = layer0Db;
        Pid = pid;
    }

    public sealed record Options(
        Paths? Paths = null,
        string? TickHook = null,
        Action<string>? Log = null,
        DateTime? Now = null,
        Func<Action<string>, IDisposable>? CoordSeat = null,
        Func<Action<string>, HitWindows>? MakeWindows = null,
        Func<int>? FindPid = null,
        ISeatHost? Host = null,
        Action<double>? Sleep = null,
        Func<TickBusReader>? OpenBus = null,
        string? Layer0Db = null,
        bool DropRetired = false,
        bool Emptying = false,
        bool Layer0Append = false,
        bool DropOldBackups = false,
        bool RemovalForced = false,
        AppSettings? Settings = null,
        HitWindowOverrides? HitWindowOverrides = null,
        Action<Layer0Ready, ScanOutcome, Layer0SwapResult>? AfterSwap = null,
        string? Layer0WriteEncoding = null);

    public int Pid { get; }

    public int FindPid() => (_o.FindPid ?? GamePid.Find)();

    public HitWindows Windows => _windows;

    public int OpenedPorts => _openedPorts;


    public static Layer0Ready PrepareLayer0(TextWriter w, Options o, bool backup,
                                            IReadOnlyList<string> replayFiles)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(o);
        var paths = o.Paths ?? TH09.Record.Paths.Default;
        var now = o.Now ?? DateTime.Now;
        var stamp = now.ToString(ScanBackup.StampFormat, CultureInfo.InvariantCulture);
        if (o.DropRetired && !backup && o.Emptying)
            w.Write(Layer0Swap.NoBackupAndDropWarning + Lf);

        BackupPlan? plan = null;
        if (backup)
        {
            plan = ScanBackup.Run(w, ScanBackup.Plan(
                new ScanBackup.Options(paths, replayFiles ?? [], now,
                                       o.DropOldBackups, o.RemovalForced)));
            if (Layer0Swap.BackupIncomplete(plan) is { } why)
                throw new ScanSetupFailed("エラー: 控えが完成していません（" + why + "）。");
        }

        if (!o.Emptying) return AppendToLive(w, paths, stamp, backup, plan, o);

        var destination = Layer0Swap.Scanning(paths.Layer0Db, stamp);
        if (File.Exists(destination))
        {
            throw new ScanSetupFailed(
                "エラー: 今回の走査の行き先が既にあります: " + destination + Lf
                + "       この道具は空から建てます（上書きしません）。"
                + "前の回の残りなら、退けるか消してください。");
        }
        w.Write("今回の走査は、生きている Layer 0 とは別の名前へ書きます: " + destination + Lf);
        w.Write("  生きている Layer 0（" + paths.Layer0Db
                + "）には、走査の間 1 バイトも書きません。" + Lf);
        return new Layer0Ready(paths.Layer0Db, destination, stamp, backup, plan, o.DropRetired,
                               o.Emptying);
    }

    private static Layer0Ready AppendToLive(TextWriter w, TH09.Record.Paths paths, string stamp,
                                            bool backup, BackupPlan? plan, Options o)
    {
        var live = paths.Layer0Db;
        var exists = File.Exists(live);
        if (exists)
        {
            var state = Layer0Stamp.Inspect(live);
            if (!state.Stamped) throw new ScanSetupFailed(NeedsStamp(live, state));
            w.Write("この走り方は追記です。生きている Layer 0 へ★書き足します: " + live
                    + "（" + ScanBackup.Size(state.Bytes) + " ／ 印 "
                    + state.StampRows.ToString(CultureInfo.InvariantCulture) + " 行）" + Lf);
        }
        else
        {
            w.Write("この走り方は追記ですが、Layer 0 がまだありません。"
                    + "本来の名前で新しく建てます: " + live + Lf);
        }
        w.Write("  走査が終わっても入れ替えは起きません（最初から本来の名前へ書くので）。" + Lf);
        return new Layer0Ready(live, live, stamp, backup, plan, o.DropRetired,
                               o.Emptying, Appending: exists);
    }

    public static string NeedsStamp(string live, Layer0StampState state) =>
        "エラー: この走り方は追記ですが、行き先に印（" + Layer0Stamp.Table + "）がありません: "
        + live + Lf
        + "       この道具は、印の無い Layer 0 へは書き足しません"
        + "（誰かが意図して開けた場所にしか書きません）。" + Lf
        + "       先に " + ScanCommand.Flag + " " + Layer0StampCommand.ScanCommandBackupFlag
        + " で控えを取り、" + ScanCommand.Flag + " " + Layer0StampCommand.Flag
        + " で印を押してください。" + Lf
        + "       空から建て直したいなら " + ScanRunModes.Flag + " "
        + ScanRunModes.NameResetScans + " を選んでください（そちらは印が要りません）。"
        + (state.Readable ? "" : Lf + "       ※この Layer 0 は読めませんでした（"
                                 + (state.Unreadable ?? "理由不明") + "）。");

    public Layer0Writer? OpenArchive()
    {
        if (_layer0Db is null) return null;
        var mode = (_built || _o.Layer0Append) ? Layer0OpenMode.Append : Layer0OpenMode.Create;
        var writer = Layer0Writer.Open(
            ArchiveOptions(_layer0Db, mode, _o.Layer0WriteEncoding ?? TickArchive.Encoding));
        _built = true;
        return writer;
    }

    public static Layer0Writer.Options ArchiveOptions(
        string dbPath, Layer0OpenMode mode, string writeEncoding = TickArchive.Encoding) =>
        new(DbPath: dbPath,
            Mode: mode,
            WriteEncoding: writeEncoding,
            RecordVersion: (int)TickBusLayout.Version,
            Fields: [.. TickBusLayout.RecordFields.Select(
                x => new Layer0Writer.FieldSlot(x.Name, x.Offset >> 2))],
            Policy: Policy,
            MaxSegmentTicks: SegmentTicks);

    public static TickEncoder.SectionPolicy Policy => new()
    {
        InvariantHint = new HashSet<string>(Hints.Invariant, StringComparer.Ordinal),
        ChangeHint = new HashSet<string>(Hints.Change, StringComparer.Ordinal),
    };


    public static CaptureParts Open(Options o)
    {
        ArgumentNullException.ThrowIfNull(o);
        var log = o.Log ?? ReplaySlots.DefaultLog;
        if (string.Equals(o.TickHook, TickHookOff, StringComparison.Ordinal))
            throw new ScanSetupFailed(TickHookOffUnsupported);
        if (string.IsNullOrEmpty(o.Layer0Db)) throw new ScanSetupFailed(Layer0DbMissing);

        var plan = PlanWindows(o.Settings ?? ConfigStore.Load((o.Paths ?? Paths.Default).ConfigPath),
                               o.HitWindowOverrides);
        foreach (var line in plan.Notes) log(line);

        var seat = plan.Enabled ? (o.CoordSeat ?? BusHostSeats.TakeCoordSeat)(log) : null;
        HitWindows windows;
        try
        {
            windows = o.MakeWindows is { } make ? make(log) : DefaultWindows(plan, log);
        }
        catch
        {
            seat?.Dispose();
            throw;
        }
        var pid = (o.FindPid ?? GamePid.Find)();
        return new CaptureParts(o, log, seat, windows, pid, o.Layer0Db);
    }

    internal static WindowPlan PlanWindows(AppSettings s, HitWindowOverrides? over)
    {
        ArgumentNullException.ThrowIfNull(s);
        var notes = new List<string>();
        foreach (var note in s.Notes)
        {
            if (IsHitWindowKey(note.Key)
                && !string.Equals(note.Kind, ConfigStore.NoteMissing, StringComparison.Ordinal))
            {
                notes.Add(HitWindowLogPrefix + note.Text);
            }
        }
        var enabled = over?.Enabled ?? s.HitWindows;
        var wantBefore = over?.Before ?? s.HitWindowBefore;
        var wantAfter = over?.After ?? s.HitWindowAfter;
        var (before, after) = HitWindowLengths.Clamp(wantBefore, wantAfter);
        if (before != wantBefore)
            notes.Add(ClampNote(ConfigStore.HitWindowBeforeKey, wantBefore, before));
        if (after != wantAfter)
            notes.Add(ClampNote(ConfigStore.HitWindowAfterKey, wantAfter, after));
        var quick = over?.Quick ?? s.HitWindowQuick;
        var triggers = ConfigStore.HitWindowTriggers(quick);
        var scopes = new Dictionary<string, bool>(s.HitWindowScopes, StringComparer.Ordinal);
        if (over?.Scopes is { } chosen)
        {
            foreach (var (name, on) in chosen) scopes[name] = on;
        }
        var normalized = CaptureScope.Normalize(scopes);
        if (!enabled)
        {
            notes.Add(HitWindowsOffNote);
            return new WindowPlan(false, before, after, triggers, normalized, notes);
        }
        var off = normalized.Where(kv => !kv.Value).Select(kv => CaptureScope.Label(kv.Key)).ToList();
        if (off.Count > 0) notes.Add(ScopesOffNote + string.Join(" / ", off));
        notes.Add(HitWindowLogPrefix + HitWindowLengths.Note(before, after, countValid: true)
                  + " ／ 起点 " + string.Join("/", triggers));
        return new WindowPlan(true, before, after, triggers, normalized, notes);
    }

    private static bool IsHitWindowKey(string key) =>
        key is ConfigStore.HitWindowsKey or ConfigStore.HitWindowBeforeKey
            or ConfigStore.HitWindowAfterKey or ConfigStore.HitWindowScopesKey
            or ConfigStore.HitWindowQuickKey
        || key.StartsWith(ConfigStore.HitWindowScopesKey + ".", StringComparison.Ordinal);

    public const string HitWindowLogPrefix = "[hitwin] ";

    public static readonly string HitWindowsOffNote =
        HitWindowLogPrefix + "無効: " + ConfigStore.HitWindowsKey
        + "=false（座標リングも作りません）";

    public static readonly string ScopesOffNote = HitWindowLogPrefix + "取らない遊び方: ";

    private static string ClampNote(string key, int want, int got) =>
        HitWindowLogPrefix + key + "=" + Num(want) + " は範囲の外なので " + Num(got) + " にしました";

    private static HitWindows DefaultWindows(WindowPlan plan, Action<string> log) =>
        plan.Enabled
            ? new HitWindows(CoordBusReader.OpenVerified(CoordLayout.DefaultName), log,
                             plan.Triggers, plan.Before, plan.After, plan.Scopes)
            : new HitWindows(null, log);


    public CaptureResult Capture(CaptureRequest req)
    {
        ArgumentNullException.ThrowIfNull(req);
        return CaptureLoop.Run(new CaptureLoop.Options(
            Conn: req.Conn,
            MakeSource: () => MakeSource(req.AttachDelaySec, req.Log),
            Timeout: req.Timeout,
            OpenArchive: OpenArchive,
            Windows: req.Windows,
            Decoded: req.Decoded,
            ShouldStop: req.ShouldStop,
            Announce: req.Announce,
            Log: req.Log));
    }

    public CaptureResult CaptureMonitor(SessionWriter writer, CancellationToken cancel,
                                        string loggerVersion, double attachDelaySec)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var keepPorts = Math.Max(1, _ports.Count);
        var demo = OpenDemoMemory();
        using var demoOwner = demo.Owner;
        try
        {
            return CaptureLoop.Run(new CaptureLoop.Options(
                Conn: writer.Connection,
                MakeSource: () => MakeMonitorSource(keepPorts, attachDelaySec),
                Timeout: double.PositiveInfinity,
                OpenArchive: OpenArchive,
                Windows: _windows,
                SkipSession: MakeRecordGate(demo.View).Skip,
                AtTitleOrMenu: MakeTitleOrMenuGate(demo.View),
                Announce: false,
                Cancel: cancel,
                Log: _log,
                SessionLoggerVersion: loggerVersion));
        }
        finally
        {
            ReleaseMonitorPorts(keepPorts);
        }
    }

    private MonitorRecordGate MakeRecordGate(ProcViewMenuMemory? menuMem)
    {
        var paths = _o.Paths ?? TH09.Record.Paths.Default;
        return new MonitorRecordGate(
            paths.ReadRecordReplayPlayback, paths.ReadRecordTitleDemo,
            menuMem is null ? null : () => MenuMap.CheckTitleDemo(menuMem, menuMem), _log);
    }

    private Func<bool> MakeTitleOrMenuGate(ProcViewMenuMemory? menuMem)
    {
        if (menuMem is null)
        {
            var warned = false;
            return () =>
            {
                if (!warned)
                {
                    warned = true;
                    _log(NoMenuReadReason);
                }
                return false;
            };
        }
        var view = new LiveMenuView(menuMem);
        var told = false;
        return () =>
        {
            if (!view.AtTitleOrMenu()) return false;
            if (!told)
            {
                told = true;
                _log("タイトル／メニューに戻りました。ここまでを 1 本として閉じます: "
                     + view.Read().Text());
            }
            return true;
        };
    }

    private (IDisposable? Owner, ProcViewMenuMemory? View) OpenDemoMemory()
    {
        var pid = FindPid();
        if (pid <= 0) return (null, null);
        var mem = GamePid.OpenMemory(pid, out _);
        return mem is null ? (null, null) : (mem, new ProcViewMenuMemory(mem));
    }

    private LiveTickSource MakeMonitorSource(int keepPorts, double attachDelaySec)
    {
        ReleaseMonitorPorts(keepPorts);
        return MakeSource(attachDelaySec, _log);
    }

    private LiveTickSource MakeSource(double attachDelaySec, Action<string> log)
    {
        var pid = FindPid();
        var ports = LivePorts.Open(new LivePorts.Options(
            Pid: pid, Log: log, AttachDelaySec: attachDelaySec,
            Host: _o.Host, Sleep: _o.Sleep, OpenBus: _o.OpenBus));
        _ports.Add(ports);
        _openedPorts++;
        log("[tick] tickフック稼働中: " + ports.HookDetail);
        return ports.OpenSource(GamePid.AdonisLoaded(pid), () => GamePid.StillAlive(pid));
    }

    private void ReleaseMonitorPorts(int keepCount)
    {
        for (var i = _ports.Count - 1; i >= keepCount; i--)
        {
            try { _ports[i].Dispose(); }
            catch (Exception exc) { _log("警告: 監視の余分な席を手放せません: " + exc.Message); }
            finally { _ports.RemoveAt(i); }
        }
    }

    public ScanOne.AutoParts AutoParts(int pid, IMenuView view, Func<IInputSink> makeSink,
                                       IMenuMemory? mem, bool noSkip) =>
        new(Pid: pid, View: view, MakeSink: makeSink,
            CoordRing: () => _coordSeat,
            Mem: mem, NoSkip: noSkip, NativeDir: null, Host: _o.Host);

    public (ScanOne.AutoParts Parts, IDisposable? Mem) LiveAutoParts(bool noSkip)
    {
        var pid = FindPid();
        if (pid <= 0) throw new ScanSetupFailed(LivePorts.GameMissing);
        var mem = GamePid.OpenMemory(pid, out var error);
        if (mem is null)
        {
            throw new ScanSetupFailed(
                "エラー: th09.exe を読み口として開けません（pid=" + Num(pid)
                + " / Win32 error=" + Num(error) + "）。");
        }
        var menuMem = new ProcViewMenuMemory(mem);
        return (AutoParts(pid, new LiveMenuView(menuMem),
                          () => new TickBusInput(new TickBusChannel()), menuMem, noSkip), mem);
    }


    public static int RunSingle(TextWriter w, ScanArgs a, IReadOnlyList<string> replayFiles,
                                Action<string>? log = null, Options? o = null)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(a);
        var rid = a.Auto ?? a.Single
                  ?? throw new ScanSetupFailed("エラー: --single か --auto を指定してください。");
        var opts = (o ?? new Options()) with { Log = log ?? (o?.Log) };
        return RunWithSwap(w, a, opts, replayFiles, parts =>
        {
            var r = parts.Scan(w, a, rid, a.Auto is not null, log);
            return (r.ExitCode, ScanOutcome.FromOne(r.ExitCode, r.Problem));
        });
    }

    public static int RunBatch(TextWriter w, ScanArgs a, IReadOnlyList<ScanTarget> targets,
                               IReadOnlyList<string> replayFiles,
                               Action<string>? log = null, Options? o = null)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Count == 0)
        {
            w.Write(ScanBatch.NoTargets + "\n");
            return 0;
        }
        var opts = (o ?? new Options()) with { Log = log ?? (o?.Log) };
        return RunWithSwap(w, a, opts, replayFiles, parts =>
        {
            var r = ScanBatch.Run(new ScanBatch.Options(
                Targets: targets,
                RunOne: t =>
                {
                    var one = parts.Scan(w, a, t.ReplayId, auto: true, log, t.Path);
                    return new ScanBatchItem(one.ExitCode, one.Problem);
                },
                MaxMinutes: a.MaxMinutes,
                Exclusive: () => ScanOne.AcquireExclusive(a.Db, null, log),
                Out: w, Log: log));
            return (r.ExitCode, ScanOutcome.FromBatch(r));
        });
    }

    private static int RunWithSwap(TextWriter w, ScanArgs a, Options opts,
                                   IReadOnlyList<string> replayFiles,
                                   Func<CaptureParts, (int ExitCode, ScanOutcome Outcome)> body)
    {
        var ready = PrepareLayer0(w, opts, !a.NoBackup, replayFiles);
        var outcome = ScanOutcome.NotRun;
        try
        {
            using var parts = Open(opts with
            {
                TickHook = a.TickHook,
                Layer0Db = ready.Destination,
                Layer0Append = ready.Appending,
            });
            var got = body(parts);
            outcome = got.Outcome;
            return got.ExitCode;
        }
        finally
        {
            var swap = Layer0Swap.Finish(w, ready, outcome);
            opts.AfterSwap?.Invoke(ready, outcome, swap);
        }
    }

    public ScanOneResult Scan(TextWriter w, ScanArgs a, long replayId, bool auto,
                              Action<string>? log = null, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(a);
        IDisposable? mem = null;
        try
        {
            ScanOne.AutoParts? autoParts = null;
            if (auto) (autoParts, mem) = LiveAutoParts(a.NoSkip);
            return ScanOne.Run(new ScanOne.Options(
                DbPath: a.Db,
                ReplayId: replayId,
                Layer0Db: _layer0Db,
                Slot: a.Slot,
                Auto: auto,
                Timeout: a.Timeout ?? ScanCommand.TimeoutDefault,
                Speed: a.Speed,
                Capture: Capture,
                Windows: _windows,
                Parts: autoParts,
                Out: w,
                Log: log,
                Path: path));
        }
        finally
        {
            mem?.Dispose();
        }
    }


    public void Dispose()
    {
        foreach (var ports in _ports)
        {
            try { ports.Dispose(); }
            catch (Exception exc) { _log("警告: 読み口の席を手放せません: " + exc.Message); }
        }
        _ports.Clear();
        try { _windows.Close(); }
        catch (Exception exc) { _log("警告: 被弾窓を閉じられません: " + exc.Message); }
        try { _coordSeat?.Dispose(); }
        catch (Exception exc) { _log("警告: 座標リングの席を手放せません: " + exc.Message); }
    }


    private static readonly string Lf = ((char)10).ToString();

    private static string Num(long value) => value.ToString(CultureInfo.InvariantCulture);
}
