using System.Globalization;
using System.Runtime.Versioning;
using TH09.Layer0;
using TH09.Record;

using Cols = TH09.Generated.DbColumns;

namespace TH09.Drive;

public sealed class ScanUsageError : Exception
{
    public ScanUsageError(string message) : base(message) { }
}

public sealed record ScanArgs(
    string Db,
    bool List = false,
    long? Single = null,
    long? Auto = null,
    long? Verify = null,
    long? Session = null,
    bool RestoreSlots = false,
    int? Slot = null,
    double? Timeout = null,
    bool NoSkip = false,
    string Speed = ScanCommand.SpeedDefault,
    string? TickHook = null,
    string? Mode = null,
    bool Own = false,
    int MinStages = 0,
    int Limit = ScanTargets.DefaultShow,
    IReadOnlyList<string>? Dirs = null,
    bool NoRecurse = false,
    string? Chars = null,
    string? Difficulty = null,
    string? MatchKind = null,
    int? MinRecordVersion = null,
    ScanRunMode Run = ScanRunMode.AddNew,
    bool HideDone = false,
    string Order = ScanTargets.OrderId,
    int? Max = null,
    bool Batch = false,
    double? MaxMinutes = null,
    bool DryRun = false,
    bool NoBackup = false,
    bool Backup = false,
    bool DropOldLayer0 = false,
    bool StampLayer0 = false,
    bool RestampLayer0 = false,
    bool DropOldBackups = false,
    bool KeepOldBackups = false,
    bool RemovalForced = false,
    bool? HitWindows = null,
    int? HitWindowBefore = null,
    int? HitWindowAfter = null,
    bool? HitWindowQuick = null,
    IReadOnlyDictionary<string, bool>? HitWindowScopes = null)
{
    public bool Rescan => Run == ScanRunMode.Rescan;
}

[SupportedOSPlatform("windows")]
public static class ScanCommand
{
    public const string Flag = "--scan";

    public const string SpeedDefault = "ReplaySkipFPS=6000";

    public const double TimeoutDefault = 900.0;

    public const double TimeoutNoSkip = 1800.0;

    public const string TimeoutRaised = "--no-skip 指定のため --timeout の既定値を1800秒へ引き上げました。";

    internal static CaptureParts.Options CaptureOptions(ScanArgs a, TextWriter w, Action<string> log,
                                                        Paths paths) =>
        new(Paths: paths,
            DropRetired: a.DropOldLayer0,
            Emptying: a.Run is ScanRunMode.ResetAll,
            DropOldBackups: a.DropOldBackups,
            RemovalForced: a.RemovalForced,
            HitWindowOverrides: HitWindowFlags(a),
            AfterSwap: (ready, outcome, swap) => RebuildLayer1(w, a, ready, outcome, swap, log),
            Layer0WriteEncoding: SolidBrotli.EncodingV2);

    private static HitWindowOverrides? HitWindowFlags(ScanArgs a) =>
        a.HitWindows is null && a.HitWindowBefore is null && a.HitWindowAfter is null
        && a.HitWindowQuick is null && a.HitWindowScopes is null
            ? null
            : new HitWindowOverrides(a.HitWindows, a.HitWindowBefore, a.HitWindowAfter,
                                     a.HitWindowQuick, a.HitWindowScopes);

    public const string HitWindowsFlag = "--hit-windows";

    public const string HitWindowBeforeFlag = "--hit-window-before";

    public const string HitWindowAfterFlag = "--hit-window-after";

    public const string HitWindowQuickFlag = "--hit-window-quick";

    public const string HitWindowScopesFlag = "--hit-window-scopes";

    private static readonly string[] Orders =
        [ScanTargets.OrderId, ScanTargets.OrderSize, ScanTargets.OrderMtime];

    public static readonly string[] OnOffWords = ["on", "off"];

    private static readonly string[] TickHooks = ["auto", "on", "off"];


    public static ScanArgs Parse(string[] argv, Paths? paths = null)
    {
        ArgumentNullException.ThrowIfNull(argv);
        var p = paths ?? Paths.Default;
        var a = new ScanArgs(Db: p.MainDb);
        var dirs = new List<string>();
        ScanRunMode? chosen = null;
        string? chosenBy = null;
        for (var i = 0; i < argv.Length; i++)
        {
            var (name, inline) = Split(argv[i]);
            ScanRunMode Choose(ScanRunMode mode, string flag)
            {
                if (chosen is ScanRunMode already && already != mode)
                {
                    throw new ScanUsageError(
                        "走り方は 1 つだけです（" + chosenBy + " と " + flag + " の両方が来ました）。");
                }
                chosen = mode;
                chosenBy = flag;
                return mode;
            }
            string Next()
            {
                if (inline is not null) return inline;
                if (i + 1 >= argv.Length) throw new ScanUsageError("値がありません: " + name);
                return argv[++i];
            }
            void NoValue()
            {
                if (inline is not null)
                    throw new ScanUsageError("値を取らない旗です: " + name);
            }
            switch (name)
            {
                case "--db": a = a with { Db = Next() }; break;
                case "--list": NoValue(); a = a with { List = true }; break;
                case "--single": a = a with { Single = Int64(name, Next()) }; break;
                case "--auto": a = a with { Auto = Int64(name, Next()) }; break;
                case "--verify": a = a with { Verify = Int64(name, Next()) }; break;
                case "--session": a = a with { Session = Int64(name, Next()) }; break;
                case "--restore-slots": NoValue(); a = a with { RestoreSlots = true }; break;
                case "--slot": a = a with { Slot = Int32(name, Next()) }; break;
                case "--timeout": a = a with { Timeout = Double(name, Next()) }; break;
                case "--no-skip": NoValue(); a = a with { NoSkip = true }; break;
                case "--speed": a = a with { Speed = Next() }; break;
                case "--tick-hook": a = a with { TickHook = Choice(name, Next(), TickHooks) }; break;
                case "--mode": a = a with { Mode = Next() }; break;
                case "--own": NoValue(); a = a with { Own = true }; break;
                case "--min-stages": a = a with { MinStages = Int32(name, Next()) }; break;
                case "--limit": a = a with { Limit = Int32(name, Next()) }; break;
                case "--dir": dirs.Add(Next()); break;
                case "--no-recurse": NoValue(); a = a with { NoRecurse = true }; break;
                case "--chars": a = a with { Chars = Next() }; break;
                case "--difficulty": a = a with { Difficulty = Next() }; break;
                case "--match-kind": a = a with { MatchKind = Next() }; break;
                case "--min-record-version":
                    a = a with { MinRecordVersion = Int32(name, Next() ) }; break;
                case ScanRunModes.Flag:
                    a = a with { Run = Choose(ScanRunModes.Parse(Next()), name) }; break;
                case "--rescan":
                    NoValue(); a = a with { Run = Choose(ScanRunMode.Rescan, name) }; break;
                case "--hide-done": NoValue(); a = a with { HideDone = true }; break;
                case "--order": a = a with { Order = Choice(name, Next(), Orders) }; break;
                case "--max": a = a with { Max = Int32(name, Next()) }; break;
                case "--batch": NoValue(); a = a with { Batch = true }; break;
                case "--max-minutes": a = a with { MaxMinutes = Double(name, Next()) }; break;
                case "--dry-run": NoValue(); a = a with { DryRun = true }; break;
                case "--no-backup": NoValue(); a = a with { NoBackup = true }; break;
                case Layer0Swap.DropOldFlag:
                    NoValue(); a = a with { DropOldLayer0 = true }; break;
                case "--backup": NoValue(); a = a with { Backup = true }; break;
                case ScanCommand.HitWindowsFlag:
                    a = a with { HitWindows = OnOff(name, Next()) }; break;
                case ScanCommand.HitWindowBeforeFlag:
                    a = a with { HitWindowBefore = Int32(name, Next()) }; break;
                case ScanCommand.HitWindowAfterFlag:
                    a = a with { HitWindowAfter = Int32(name, Next()) }; break;
                case ScanCommand.HitWindowQuickFlag:
                    a = a with { HitWindowQuick = OnOff(name, Next()) }; break;
                case ScanCommand.HitWindowScopesFlag:
                    a = a with { HitWindowScopes = Scopes(name, Next()) }; break;
                case Layer0StampCommand.Flag:
                    NoValue(); a = a with { StampLayer0 = true }; break;
                case Layer0RestampCommand.Flag:
                    NoValue(); a = a with { RestampLayer0 = true }; break;
                case ScanBackup.DropOldBackupsFlag:
                    NoValue(); a = a with { DropOldBackups = true, RemovalForced = true }; break;
                case ScanBackup.KeepOldBackupsFlag:
                    NoValue(); a = a with { KeepOldBackups = true }; break;
                default:
                    throw new ScanUsageError("知らない引数です: " + argv[i]);
            }
        }
        return dirs.Count == 0 ? a : a with { Dirs = dirs };
    }

    public static ScanArgs Resolve(ScanArgs a, Paths? paths = null, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(a);
        var p = paths ?? Paths.Default;
        if (a.Timeout is null)
        {
            if (a.NoSkip)
            {
                a = a with { Timeout = TimeoutNoSkip };
                log?.Invoke(TimeoutRaised);
            }
            else
            {
                a = a with { Timeout = TimeoutDefault };
            }
        }
        if (a.Slot is null) a = a with { Slot = p.ReplayScanSlot };
        if (!a.DropOldBackups && !a.KeepOldBackups)
        {
            a = a with { DropOldBackups = ConfigStore.Load(p.ConfigPath).BackupKeepOne };
        }
        if (a.Slot < ReplaySlots.SlotMin || a.Slot > ReplaySlots.SlotMax)
        {
            throw new ScanSetupFailed(
                "エラー: --slot は " + Num(ReplaySlots.SlotMin) + "〜"
                + Num(ReplaySlots.SlotMax) + " です。");
        }
        return a;
    }

    public static IReadOnlyList<string> Describe(ScanArgs a)
    {
        ArgumentNullException.ThrowIfNull(a);
        return
        [
            "db=" + a.Db,
            "list=" + Bit(a.List),
            "single=" + Opt(a.Single),
            "auto=" + Opt(a.Auto),
            "verify=" + Opt(a.Verify),
            "session=" + Opt(a.Session),
            "restore_slots=" + Bit(a.RestoreSlots),
            "slot=" + Opt(a.Slot),
            "timeout=" + (a.Timeout is double t ? t.ToString("R", Inv) : "-"),
            "no_skip=" + Bit(a.NoSkip),
            "speed=" + a.Speed,
            "tick_hook=" + (a.TickHook ?? "-"),
            "mode=" + (a.Mode ?? "-"),
            "own=" + Bit(a.Own),
            "min_stages=" + Num(a.MinStages),
            "limit=" + Num(a.Limit),
            "dir=" + string.Join("|", a.Dirs ?? []),
            "no_recurse=" + Bit(a.NoRecurse),
            "chars=" + (a.Chars ?? "-"),
            "min_record_version=" + Opt(a.MinRecordVersion),
            "rescan=" + Bit(a.Rescan),
            "hide_done=" + Bit(a.HideDone),
            "order=" + a.Order,
            "max=" + Opt(a.Max),
            "batch=" + Bit(a.Batch),
            "max_minutes=" + (a.MaxMinutes is double m ? m.ToString("R", Inv) : "-"),
            "dry_run=" + Bit(a.DryRun),
        ];
    }


    public static int Run(TextWriter w, ScanArgs a, Action<string>? log = null, Paths? paths = null)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(a);
        log ??= ReplaySlots.DefaultLog;
        var effectivePaths = paths ?? Paths.Default;

        if (a.RestoreSlots) return ScanOne.RestoreSlots(w, null, log);
        if (a.StampLayer0)
            return Layer0StampCommand.Run(w, Layer0StampCommand.Plan(null), a.DryRun);
        if (a.RestampLayer0)
            return Layer0RestampCommand.Run(w, Layer0RestampCommand.Plan(null), a.DryRun);

        var plan = ScanRunModes.Plan(a.Run, paths);
        var reading = a.DryRun || a.List || a.Verify is not null || a.Backup;
        if (!reading && plan.Blocked is { } why)
        {
            ScanRunModes.Describe(w, plan, null, effectivePaths);
            w.Write(why + Lf);
            return 1;
        }
        if (!reading && a.Run == ScanRunMode.ImportOnly)
        {
            WatchCommand.ImportExisting(w, a.Db, paths);
            return 0;
        }
        if (!reading && !a.Batch && a.Single is null && a.Auto is null)
        {
            Help(w);
            return 1;
        }
        var readingOnly = a.DryRun || a.List || a.Verify is not null;
        if (!readingOnly && (a.Batch || a.Single is not null || a.Auto is not null || a.Backup)
            && NonDefaultDb(a.Db, effectivePaths))
        {
            w.Write("エラー: --db を既定と違う場所にしたときは走査しません"
                    + "（Layer 0・控え・ログの行き先は既定の場所のままなので）。" + Lf);
            return 2;
        }
        var dbMissing = ReplaysEmpty(a.Db);
        if (!reading && plan.Mode is ScanRunMode.ResetScans or ScanRunMode.ResetAll
            && (a.Batch || a.Single is not null || a.Auto is not null))
        {
            var reset = RunReset(w, a, plan, effectivePaths, log);
            if (reset.Code is int code) return code;
            a = reset.Args;
        }

        if (PrepareMissingDb(w, a, plan, dbMissing,
                             () => WatchCommand.ImportExisting(w, a.Db, paths)) is int stop)
            return stop;

        using var db = RecordDb.OpenReadOnly(a.Db);
        if (db is null)
        {
            w.Write("エラー: 本体 DB を開けません: " + a.Db + "\n");
            return 1;
        }
        var conn = db.Connection;
        if (a.Backup) return RunBackup(w, a, Repository.CurrentReplayFiles(conn), effectivePaths);
        var replayDir = effectivePaths.GameReplayDir();
        var shared = SharedFilter(a);
        if (shared.Dirs is { Count: > 0 } checkDirs)
        {
            var watchRoots = effectivePaths.ReplayWatchDirs();
            foreach (var dir in checkDirs)
            {
                if (!ScanTargets.UnderAnyWatchRoot(dir, watchRoots))
                    w.Write("このフォルダは登録の元に入っていません（設定の記録タブで追加できます）: "
                            + dir + Lf);
            }
        }

        if (a.DryRun)
        {
            var batch = ScanTargets.ForBatch(shared, plan.IncludesDone, a.Max);
            ScanTargets.SelectionCounts? counts = null;
            IReadOnlyList<ScanTarget> targets;
            if (plan.Capture)
            {
                targets = ScanTargets.SelectTargets(conn, batch, log, out var c);
                counts = c;
            }
            else
            {
                targets = [];
            }
            ScanRunModes.Describe(w, plan, shared, effectivePaths);
            if (counts is { } cnt && targets.Count == 0)
                w.Write(ScanTargets.DescribeZeroCounts(cnt) + Lf);
            var scanPlan = ScanTargets.WriteDryRun(w, targets, batch, plan.IncludesDone, a.Limit,
                                                    a.Slot!.Value, replayDir, plan);
            w.Write(ScanProgressLines.ScanPlanLine(scanPlan.TargetCount, scanPlan.DoneNote,
                                                    scanPlan.Hours, scanPlan.CopyCount,
                                                    scanPlan.InSlotCount) + "\n");
            return 0;
        }
        if (a.Batch)
        {
            ScanRunModes.Describe(w, plan, shared, effectivePaths);
            var batchTargets = ScanTargets.SelectTargets(
                conn, ScanTargets.ForBatch(shared, plan.IncludesDone, a.Max), log, out var batchCounts);
            if (batchTargets.Count == 0)
                w.Write(ScanTargets.DescribeZeroCounts(batchCounts) + Lf);
            return CaptureParts.RunBatch(
                w, a, batchTargets, Repository.CurrentReplayFiles(conn), log,
                CaptureOptions(a, w, log, effectivePaths));
        }
        if (a.List)
        {
            var targets = ScanTargets.SelectTargets(
                conn, ScanTargets.ForList(shared, plan.IncludesDone, a.HideDone, a.Limit), log);
            ScanRunModes.Describe(w, plan, shared, effectivePaths);
            ScanTargets.WriteList(w, targets, replayDir);
            return 0;
        }
        if (a.Verify is long rid)
        {
            if (a.Session is not long sid)
                throw new ScanSetupFailed("エラー: --verify は --session と併用してください。");
            return ScanOne.Verify(w, conn, rid, sid);
        }
        if (a.Single is not null || a.Auto is not null)
            return CaptureParts.RunSingle(w, a, Repository.CurrentReplayFiles(conn), log,
                                          CaptureOptions(a, w, log, effectivePaths));
        Help(w);
        return 1;
    }

    internal static ScanFilter SharedFilter(ScanArgs a) => new(
        Dirs: a.Dirs, Recurse: !a.NoRecurse, Modes: ScanTargets.ModesArg(a.Mode), Own: a.Own,
        MinStages: a.MinStages, Order: a.Order, Chars: ScanTargets.CharsArg(a.Chars),
        Difficulties: ScanTargets.DifficultyArg(a.Difficulty),
        MatchKinds: ScanTargets.MatchKindsArg(a.MatchKind),
        MinRecordVersion: a.MinRecordVersion);

    internal static int? PrepareMissingDb(TextWriter w, ScanArgs a, ScanRunPlan plan, bool missing,
                                          Func<int> register)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(register);
        if (!missing) return null;
        var dbFileMissing = !File.Exists(a.Db);
        var head = dbFileMissing
            ? "本体 DB がまだありません。始めると DB を作り、リプレイを登録"
            : "リプレイがまだ登録されていません。始めると登録";
        if (a.DryRun)
        {
            w.Write("本体 DB: " + a.Db + Lf);
            w.Write(head + (plan.Capture ? "してから走査します。" : "します。") + Lf);
            return 1;
        }
        if (a.Batch && !plan.ImportReplays)
        {
            w.Write((dbFileMissing
                        ? "本体 DB が無いので作り、リプレイを登録してから走査します: "
                        : "リプレイがまだ登録されていないので、登録してから走査します: ")
                    + a.Db + Lf);
            register();
        }
        return null;
    }

    internal static bool ReplaysEmpty(string db)
    {
        ArgumentException.ThrowIfNullOrEmpty(db);
        if (!File.Exists(db)) return true;
        using var ro = RecordDb.OpenReadOnly(db);
        if (ro is null) return true;
        if (!ro.TableNames().Contains(Cols.Replays.Table, StringComparer.Ordinal)) return true;
        using var cmd = ro.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM " + Cols.Replays.Table;
        return Convert.ToInt64(cmd.ExecuteScalar(), Inv) == 0;
    }

    private static int RunBackup(TextWriter w, ScanArgs a, IReadOnlyList<string> replayFiles,
                                 Paths paths)
    {
        var plan = ScanBackup.Plan(BackupOptions(a, replayFiles, paths));
        if (a.DryRun)
        {
            ScanBackup.Describe(w, plan);
            w.Write("**控えは取っていません。**\n");
            return 0;
        }
        ScanBackup.Run(w, plan);
        return 0;
    }

    internal static ScanBackup.Options BackupOptions(ScanArgs a, IReadOnlyList<string> replayFiles,
                                                      Paths paths) =>
        new(paths, ReplayFiles: replayFiles, DropOldBackups: a.DropOldBackups,
            RemovalForced: a.RemovalForced);

    private static (int? Code, ScanArgs Args) RunReset(TextWriter w, ScanArgs a, ScanRunPlan plan,
                                                        Paths paths, Action<string> log)
    {
        var all = plan.Mode == ScanRunMode.ResetAll;
        using (var ledger = ScanLedger.Open(a.Db, log))
        {
            if (ledger is null)
            {
                w.Write("エラー: 別のプロセスが同じ DB へセッションを書いています。" + Lf
                        + "       GUI の「監視」を OFF にするか、走査を止めてから実行してください。" + Lf);
                return (1, a);
            }
            var conn = ledger.Connection;
            if (!a.NoBackup)
            {
                var backupPlan = ScanBackup.Run(w, ScanBackup.Plan(new ScanBackup.Options(
                    paths, Repository.CurrentReplayFiles(conn),
                    DropOldBackups: a.DropOldBackups,
                    RemovalForced: a.RemovalForced)));
                if (Layer0Swap.BackupIncomplete(backupPlan) is { } why)
                    throw new ScanSetupFailed("エラー: 控えが完成していません（" + why + "）。");
            }
            var counts = ScanResultsClear.Count(conn, all);
            w.Write(ScanResultsClear.Describe(counts) + Lf);
            var layer0Path = all ? null : paths.Layer0Db;
            ScanResultsClear.Run(conn, counts, layer0Path, log);
        }
        if (plan.ImportReplays) WatchCommand.ImportExisting(w, a.Db, paths);
        return (null, a.NoBackup ? a : a with { NoBackup = true });
    }

    private static void RebuildLayer1(TextWriter w, ScanArgs a, Layer0Ready ready,
                                      ScanOutcome outcome, Layer0SwapResult swap,
                                      Action<string> log)
    {
        if (outcome.StoppedBy is not null || outcome.Recorded <= 0) return;
        if (ready.Emptying && !swap.Swapped) return;

        try
        {
            using var ledger = ScanLedger.Open(a.Db, log);
            if (ledger is null)
            {
                w.Write("Layer 1 は作り直せませんでした（本体 DB の排他を取れません）。" + Lf);
                return;
            }
            var todo = Layer1Build.Plan(ledger.Connection, null, false);
            w.Write("Layer 1 を作り直します: 追加 " + Num(todo.Fresh) + " 行 / 更新 " + Num(todo.Stale)
                    + " 行 / 孤児 " + Num(todo.Orphans.Count) + " 行（" + Num(todo.Build.Count)
                    + " セッション）" + Lf);
            var summary = Layer1Build.Run(ledger, ready.Live, null, false, log);
            w.Write("Layer 1 の作り直しが終わりました: 追加 " + Num(summary.Fresh) + " 行 / 更新 "
                    + Num(summary.Stale) + " 行 / 消した " + Num(summary.Deleted) + " 行" + Lf);
        }
        catch (Exception exc)
        {
            w.Write("Layer 1 の作り直しに失敗しました（走査の結果はそのまま残ります）: "
                    + exc.GetType().Name + ": " + exc.Message + Lf);
        }
    }

    public static void Help(TextWriter w)
    {
        ArgumentNullException.ThrowIfNull(w);
        w.Write($"""
{Program.ExeName} --scan — リプレイを再生させて捕捉し replay_id へ紐付ける（段階 5）

  --list                候補のリプレイを出す（--mode 0 で Story / --own / --min-stages）
  --dry-run             再生せず、対象・順序・所要見積・ファイル操作の予定だけ出す
  --restore-slots       残骸の .tickscan.bak を復元して終わる
  --verify N --session M  既存セッションと突合だけする
  --backup              走る前の控え（バックアップ）を取る（--dry-run と併せると予定だけ）
  --single N / --auto N / --batch   対象を捕捉する

  行き先: --db PATH（本体 DB。★既定は共有の置き場から引く）

  走り方（★1 つだけ選ぶ。既定は new）:
    --run new           新規のみ走査して DB 追加（記録済みは除外）
    --run rescan        上書きして新たに走査（＝ --rescan。記録済みもやり直す）
    --run reset-scans   実プレイは残し、走査の結果だけ消して走査（リプレイの登録も残す）
    --run reset-all     DB ごと空にして走査（実プレイも消えます。登録からやり直す）
    --run import-only   リプレイの登録のみ（走査しない。監視先を 1 回だけ登録）

  絞り（★すべて AND）: --mode 0,2 / --difficulty 3,2（どちらも数か表示名。複数可）/ --own / --min-stages N
        --dir PATH / --no-recurse / --chars 1,13
        --match-kind cpu-vs-human ほか（Match の対戦区分。数 0〜3 でも可。複数可）
              ★Match だけに効きます（Story / Extra は素通り）。
              ★式対人（cpu-vs-human）も 2026-09-16 から倍速で回ります（実測 23.2 倍）。
                ★それ以前は等速で期限切れになったので、外して回す使い方がありました。
        --hide-done / --order id|size|mtime / --min-record-version N
  上限: --max N（対象の本数）/ --limit N（--list の表示件数）/ --max-minutes M
  再生: --slot N / --timeout S / --no-skip / --speed S / --tick-hook auto|on|off
  被弾窓（★既定は config.json。★指定はその走査の 1 回だけで、設定は書き換えません）:
        --hit-windows on|off（off なら座標リングも作らない＝費用も止まる）
        --hit-window-before N / --hit-window-after N（VALID tick。★範囲外は丸めて 1 行出す）
        --hit-window-quick on|off（クイックカードアタックも窓の起点にするか）
        --hit-window-scopes net=off,story=on（遊び方ごと。★書いた語だけ上書き）
  控え: --no-backup（一括の前にバックアップを取らない。★既定は取る）
        --drop-old-layer0（入れ替えたあと、退けた古い Layer 0 を消す。★既定は残す）
        ★上の 2 つは別物 ——★控えは「走る前」、こちらは「入れ替えのとき」。
        ★消すのは入れ替えが成功した後だけ（★入れ替えなかった回は 1 本も消えない）
        --drop-old-backups（控えを取ったあと、前の回の控えを消す。★消す前に大きさを出します。
        ★既定は設定 backup_keep_one（既定オン）なので、ふだんは書かなくても消えます。
        ★この旗を書いた回だけ「縮んでいても消す」まで通ります）
        --keep-old-backups（前の回の控えを消さない。★設定をこの回だけ切ります）
        ★控えは既定で「完成した 1 つ」だけになります ——★未完成の控え（目印の無いフォルダ）は
        ★控えと見做さずに消します（★10 分以上そのままのものだけ。書きかけは触りません）
  印:  --stamp-layer0（いま生きている Layer 0 に「書き足してよい」の印を押す。
        ★追記の走り方に 1 度だけ要る。★控えを取ってから。--dry-run で予定だけ）
  付け直し: --restamp-layer0（いま生きている Layer 0 の solid-brotli-v1 行を、検査和つきの
        solid-brotli-v2 へ付け直す。★brotli は圧縮し直さない。★控えを取ってから。
        ★走査・監視が同時に書いていれば断る。--dry-run で予定だけ）

★知らない引数は読み飛ばさずに落とします。
★th09.exe を起動・終了・前面化せず、OS 入力も使いません。
★捕捉を伴う走り方では既存のゲームを探して読み、入力は Tick Bus 経由だけです。
""");
    }


    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static readonly string Lf = ((char)10).ToString();

    private static (string Name, string? Inline) Split(string arg)
    {
        var i = arg.IndexOf('=', StringComparison.Ordinal);
        return i < 0 ? (arg, null) : (arg[..i], arg[(i + 1)..]);
    }

    private static long Int64(string name, string text) =>
        long.TryParse(text, NumberStyles.AllowLeadingSign, Inv, out var v)
            ? v : throw new ScanUsageError("整数ではありません（" + name + "）: " + text);

    private static int Int32(string name, string text) =>
        int.TryParse(text, NumberStyles.AllowLeadingSign, Inv, out var v)
            ? v : throw new ScanUsageError("整数ではありません（" + name + "）: " + text);

    private static double Double(string name, string text) =>
        double.TryParse(text, NumberStyles.Float, Inv, out var v)
            ? v : throw new ScanUsageError("数ではありません（" + name + "）: " + text);

    private static bool OnOff(string name, string text) =>
        string.Equals(Choice(name, text, OnOffWords), OnOffWords[0], StringComparison.Ordinal);

    private static IReadOnlyDictionary<string, bool> Scopes(string name, string text)
    {
        var map = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var i = part.IndexOf('=', StringComparison.Ordinal);
            if (i <= 0)
            {
                throw new ScanUsageError("遊び方は <語>=on|off で書きます（" + name + "）: " + part);
            }
            var word = part[..i].Trim();
            if (!CaptureScope.All.Contains(word, StringComparer.Ordinal))
            {
                throw new ScanUsageError("知らない遊び方です（" + name + " は "
                                         + string.Join(" / ", CaptureScope.All) + "）: " + word);
            }
            map[word] = OnOff(name, part[(i + 1)..].Trim());
        }
        if (map.Count == 0) throw new ScanUsageError("遊び方が 1 つも書かれていません: " + name);
        return map;
    }

    private static string Choice(string name, string text, string[] allowed) =>
        allowed.Contains(text, StringComparer.Ordinal)
            ? text
            : throw new ScanUsageError("取れない値です（" + name + " は "
                                       + string.Join(" / ", allowed) + "）: " + text);

    private static string Bit(bool v) => v ? "1" : "0";

    private static string Opt(long? v) => v is long n ? n.ToString(Inv) : "-";

    private static string Opt(int? v) => v is int n ? n.ToString(Inv) : "-";

    private static string Num(int v) => v.ToString(Inv);

    private static bool NonDefaultDb(string db, Paths paths) =>
        !string.Equals(System.IO.Path.GetFullPath(db), System.IO.Path.GetFullPath(paths.MainDb),
                       StringComparison.OrdinalIgnoreCase);
}
