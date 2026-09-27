using System.Collections.Frozen;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.Sqlite;
using TH09.Layer0;
using TH09.Record.Generated;

using ScanItems = TH09.Generated.DbColumns.ReplayScanItems;

namespace TH09.Record;

public sealed class CaptureResult
{
    public const string NotRun = CaptureStatus.NotRun;

    public long? SessionId { get; set; }

    public string Status { get; set; } = NotRun;

    public long GapCount { get; set; }

    public string? SourceKind { get; set; }

    public double Elapsed { get; set; }

    public string? Error { get; set; }

    public string? EarlyMismatch { get; set; }

    public IReadOnlyDictionary<string, long?> Health { get; set; } =
        new Dictionary<string, long?>(StringComparer.Ordinal);
}

public static class CaptureStatus
{
    public const string NotRun = "not-run";

    public const string Captured = "captured";

    public const string Disconnected = "disconnected";

    public const string Interrupted = "interrupted";

    public const string Timeout = "timeout";

    public const string SetupFailed = "setup-failed";

    public const string MenuFailed = "menu-failed";

    public const string Error = "error";

    public static readonly FrozenDictionary<string, (string Reason, string SessionStatus)> SessionClose =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            [Captured] = ("aborted", "completed"),
            [Disconnected] = ("disconnected", "disconnected"),
            [Interrupted] = ("stopped", "stopped"),
            [Timeout] = ("timeout", "timeout"),
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static readonly (string Reason, string SessionStatus) Fallback = ("aborted", "aborted");

    public static (string Reason, string SessionStatus) CloseFor(string status) =>
        SessionClose.TryGetValue(status, out var v) ? v : Fallback;
}

public static class FailureStatus
{
    public static string Of(Exception exc) =>
        exc is OperationCanceledException ? CaptureStatus.Interrupted : CaptureStatus.Error;
}

public static class EarlyMismatchRule
{
    public static string? Of(Snapshot s, CanonJson.Node decoded)
    {
        var bad = new List<string>();
        var difficulty = Member(decoded, "difficulty");
        if (difficulty is not null && !SameNumber(s.Difficulty, difficulty))
        {
            bad.Add("難易度 実測=" + MonitorRules.Name(RecordLabels.Difficulties, s.Difficulty)
                    + " / リプレイ=" + DifficultyName(difficulty));
        }
        var p1 = Member(decoded, "p1_char");
        if (p1 is not null && !SameNumber(s.P1Character, p1))
            bad.Add("1Pキャラ 実測=" + MonitorRules.Char(s.P1Character) + " / リプレイ=" + CharName(p1));
        return bad.Count > 0 ? string.Join(" / ", bad) : null;
    }

    private static CanonJson.Node? Member(CanonJson.Node decoded, string name) =>
        decoded.Members.TryGetValue(name, out var v) && v.Kind != CanonJson.Kind.Null ? v : null;

    private static bool SameNumber(long got, CanonJson.Node want) => want.Kind switch
    {
        CanonJson.Kind.Bool => got == (want.Bool ? 1 : 0),
        CanonJson.Kind.Int => long.TryParse(want.Text, NumberStyles.Integer,
                                            CultureInfo.InvariantCulture, out var n) && n == got,
        _ => false,
    };

    private static string DifficultyName(CanonJson.Node v) =>
        v.Kind == CanonJson.Kind.Int
        && long.TryParse(v.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? MonitorRules.Name(RecordLabels.Difficulties, n)
            : "Unknown(" + PyText(v) + ")";

    private static string CharName(CanonJson.Node v) =>
        v.Kind == CanonJson.Kind.Int
        && long.TryParse(v.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? MonitorRules.Char(n)
            : "Unknown(" + PyText(v) + ")";

    private static string PyText(CanonJson.Node v) => v.Kind switch
    {
        CanonJson.Kind.Bool => v.Bool ? "True" : "False",
        CanonJson.Kind.Int => v.Text,
        CanonJson.Kind.String => v.Text,
        _ => throw new InvalidDataException(
            "difficulty / p1_char が整数でも文字列でもない: " + CanonJson.Canon(v)),
    };
}

public static class CaptureHealth
{
    public const int MinTicksPerStage = 1200;

    public static Dictionary<string, long?> Layer0Health(Layer0Writer? writer)
    {
        var health = new Dictionary<string, long?>(StringComparer.Ordinal);
        if (writer is null) return health;
        try
        {
            var s = writer.GetStats();
            health[ScanItems.Layer0Segments] = s.Segments;
            health[ScanItems.Layer0Ticks] = s.Ticks;
            health[ScanItems.Layer0Bytes] = s.Bytes;
            health[ScanItems.Layer0Lost] = s.Lost;
            health[ScanItems.Layer0Torn] = s.Torn;
            health[ScanItems.Layer0VerifyFailures] = s.VerifyFailures;
        }
        catch (Exception)
        {
            return new Dictionary<string, long?>(StringComparer.Ordinal);
        }
        return health;
    }

    public static Dictionary<string, long?> Merge(IReadOnlyDictionary<string, long?> layer0,
                                                 IReadOnlyDictionary<string, long?> hit)
    {
        var all = new Dictionary<string, long?>(layer0, StringComparer.Ordinal);
        foreach (var (k, v) in hit) all[k] = v;
        return all;
    }

    public static string? Problem(CaptureResult result, int stageCount)
    {
        var h = result.Health;
        long stages = Math.Max(1, stageCount);
        if (Get(h, ScanItems.Layer0Ticks) is long ticks)
        {
            if (ticks == 0)
                return "Layer 0 に1 tick も入っていない（生レコードが来ていない）";
            long floor = MinTicksPerStage * stages;
            if (ticks < floor)
            {
                return string.Format(CultureInfo.InvariantCulture,
                    "Layer 0 の tick が少なすぎる（{0} tick / {1} 面。下限 {2}）", ticks, stages, floor);
            }
        }
        foreach (var (key, what) in new[]
                 {
                     (ScanItems.Layer0VerifyFailures, "Layer 0 のセグメント"),
                     (ScanItems.HitWindowVerifyFailures, "被弾窓"),
                 })
        {
            if (Get(h, key) is long n && n != 0)
            {
                return string.Format(CultureInfo.InvariantCulture,
                    "{0} を {1} 本、書いた直後に読み戻せなかった", what, n);
            }
        }
        if (Get(h, ScanItems.CoordTicks) is long coord && coord == 0)
        {
            return "座標リングに DLL が1 tick も書いていない"
                 + "（フック未注入か、注入より後に座標リングを作った）";
        }
        if (Get(h, ScanItems.HitsSeen) is long hits && hits > 0
            && (Get(h, ScanItems.HitWindows) ?? 0) == 0)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "窓の起点を {0} 件（うちクイック {1} 件）検出したのに被弾窓が1本も保存されていない",
                hits, Quicks(h));
        }
        return null;
    }

    private static long? Get(IReadOnlyDictionary<string, long?> h, string key) =>
        h.TryGetValue(key, out var v) ? v : null;

    private static string Quicks(IReadOnlyDictionary<string, long?> h) =>
        h.TryGetValue(ScanItems.QuicksSeen, out var v)
            ? v?.ToString(CultureInfo.InvariantCulture) ?? "None"
            : "—";
}

public static class CaptureLoop
{
    public const string LoggerVersion = "replay-scan-v1";

    public const int NewGameStreak = 3;

    public const string Layer0SinkName = "layer0";

    public const double PollSleepSeconds = 0.002;

    public const double UnavailableSleepSeconds = 0.1;

    public const double AbsentSleepSeconds = 1.0;

    public sealed record Options(
        SqliteConnection Conn,
        Func<LiveTickSource> MakeSource,
        double Timeout,
        double IdleClose = 2.0,
        Func<Layer0Writer?>? OpenArchive = null,
        HitWindows? Windows = null,
        CanonJson.Node? Decoded = null,
        Func<bool>? ShouldStop = null,
        Func<Snapshot, string?>? SkipSession = null,
        Func<bool>? AtTitleOrMenu = null,
        bool Announce = true,
        CancellationToken Cancel = default,
        Action<string>? Log = null,
        Action? Idle = null,
        string SessionLoggerVersion = LoggerVersion);

    public static CaptureResult Run(Options o)
    {
        var result = new CaptureResult();
        var log = o.Log ?? (static _ => { });
        var hit = o.Windows ?? new HitWindows(null, log);
        var idle = o.Idle ?? (() => Sleep(PollSleepSeconds));

        LiveTickSource? source = null;
        Layer0Writer? writer = null;
        StateMachine? sm = null;
        long? sessionId = null;
        var started = Monotonic();
        double? unavailableSince = null;
        var candidate = 0;
        var broke = false;
        var backFromWait = false;

        try
        {
            writer = o.OpenArchive?.Invoke();
            while (Monotonic() - started < o.Timeout)
            {
                if (o.Cancel.IsCancellationRequested)
                {
                    result.Status = CaptureStatus.Interrupted;
                    broke = true;
                    break;
                }
                if (o.ShouldStop is { } shouldStop && sessionId is not null && shouldStop())
                {
                    result.Status = CaptureStatus.Captured;
                    broke = true;
                    break;
                }
                if (source is null)
                {
                    source = o.MakeSource();
                    source.Start();
                    AttachLayer0(source, writer);
                    hit.Attach(source, writer);
                    result.SourceKind = LiveTickSource.Kind;
                    log("取得経路: " + source.Status());
                    if (o.Announce) log("リプレイの再生を開始してください。");
                }
                try
                {
                    var snaps = source.Read();
                    if (snaps.Count > 0) unavailableSince = null;
                    foreach (var s in snaps)
                    {
                        if (sessionId is null)
                        {
                            candidate = NewGameRule.IsNewGame(s) ? candidate + 1 : 0;
                            if (candidate < NewGameStreak) continue;
                            if (o.SkipSession is { } skip && skip(s) is not null)
                            {
                                candidate = 0;
                                continue;
                            }
                            sessionId = OpenSession(o.Conn, hit, writer, s, o.Decoded, result, log,
                                                    o.SessionLoggerVersion);
                            sm = new StateMachine(o.Conn, sessionId.Value);
                        }
                        else if (backFromWait && o.AtTitleOrMenu is not null
                                 && SamePlayRule.Broken(sm!.Prev, s) is { } changed)
                        {
                            log("別のプレイが始まったようです（" + changed
                                + "）。ここまでを 1 本として閉じます。");
                            result.Status = CaptureStatus.Captured;
                            broke = true;
                            break;
                        }
                        backFromWait = false;
                        sm!.Process(s);
                    }
                    if (broke) break;
                    idle();
                }
                catch (TickHookLostException exc)
                {
                    log("警告: " + exc.Message
                        + " ★ポーリングへは降格しません（この 1 本を失敗として返します）。");
                    result.Status = FailureStatus.Of(exc);
                    result.Error = exc.GetType().Name + ": " + exc.Message;
                    broke = true;
                    break;
                }
                catch (SnapshotUnavailableException)
                {
                    if (source.GameAlive is not { } alive || alive())
                    {
                        unavailableSince ??= Monotonic();
                        if (sessionId is not null && CloseWhileUnavailable(o, unavailableSince.Value))
                        {
                            result.Status = CaptureStatus.Captured;
                            broke = true;
                            break;
                        }
                        if (sessionId is not null) backFromWait = true;
                        Sleep(UnavailableSleepSeconds);
                        continue;
                    }
                    if (sessionId is not null)
                    {
                        result.Status = CaptureStatus.Disconnected;
                        broke = true;
                        break;
                    }
                    source.Dispose();
                    source = null;
                    Sleep(AbsentSleepSeconds);
                }
            }
            if (!broke)
            {
                result.Status = CaptureStatus.Timeout;
            }
        }
        finally
        {
            if (sessionId is not null && sm is not null)
            {
                var (reason, sessionStatus) = CaptureStatus.CloseFor(result.Status);
                try
                {
                    sm.Close(sm.Prev, reason, wallFallback: LiveTickSource.NowIso());
                    CloseSession(o.Conn, sessionId.Value, sessionStatus);
                    log(MonitorLines.SessionClosed(sessionId.Value));
                }
                catch (Exception exc)
                {
                    log(string.Format(CultureInfo.InvariantCulture,
                        "警告: session={0} を閉じられませんでした: {1}: {2}",
                        sessionId.Value, exc.GetType().Name, exc.Message));
                    result.Error = "close failed: " + exc.GetType().Name + ": " + exc.Message;
                }
            }
            hit.End();
            writer?.EndSession();
            result.Health = CaptureHealth.Merge(CaptureHealth.Layer0Health(writer), hit.Health);
            writer?.Dispose();
            hit.Detach();
            if (source is not null)
            {
                result.GapCount = source.Ring.LostRecords;
                source.Dispose();
            }
        }
        result.Elapsed = Monotonic() - started;
        return result;
    }

    private static bool CloseWhileUnavailable(Options o, double unavailableSince) =>
        o.AtTitleOrMenu is { } atTitleOrMenu
            ? atTitleOrMenu()
            : Monotonic() - unavailableSince >= o.IdleClose;

    private static long OpenSession(SqliteConnection conn, HitWindows hit, Layer0Writer? writer,
                                    Snapshot s, CanonJson.Node? decoded, CaptureResult result,
                                     Action<string> log, string loggerVersion)
    {
        var sessionId = Repository.InsertSession(conn, ScanLedger.NowIso(),
                                                 ScanLedger.StatusRunning, loggerVersion);
        writer?.BeginSession(sessionId);
        hit.Begin(sessionId);
        result.SessionId = sessionId;
        log(MonitorLines.SessionOpenedPrefix + string.Format(CultureInfo.InvariantCulture,
            "{0} ({1} / {2} {3} / {4})",
            sessionId, s.ExecutionType,
            MonitorRules.Name(RecordLabels.Modes, s.Mode),
            MonitorRules.Name(RecordLabels.Difficulties, s.Difficulty),
            MonitorRules.Char(s.P1Character)));
        if (decoded is not null && EarlyMismatchRule.Of(s, decoded) is { } why)
        {
            result.EarlyMismatch = why;
            log(new string('*', 68));
            log("警告: 指定した replay_id と中身が食い違っています → " + why);
            log("      別のリプレイを選んでいませんか？ 続けても構いませんが、");
            log("      突合が通らないので session_replays には紐付けません。");
            log("      やり直すなら中断してください（スロットは必ず元に戻ります）。");
            log(new string('*', 68));
        }
        return sessionId;
    }

    private static void AttachLayer0(LiveTickSource source, Layer0Writer? writer)
    {
        if (writer is null) return;
        source.AddSink(Layer0SinkName, recs => writer.Add(
            [.. recs.Select(r => r.Words)], source.Ring.LostRecords, source.Ring.TornRecords));
    }

    private static void CloseSession(SqliteConnection conn, long sessionId, string status)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE sessions SET ended_at=$0,status=$1 WHERE session_id=$2";
        cmd.Parameters.AddWithValue("$0", ScanLedger.NowIso());
        cmd.Parameters.AddWithValue("$1", status);
        cmd.Parameters.AddWithValue("$2", sessionId);
        cmd.ExecuteNonQuery();
    }

    private static double Monotonic() => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

    private static void Sleep(double seconds)
    {
        var ms = (int)Math.Round(seconds * 1000.0);
        if (ms > 0) Thread.Sleep(ms);
    }
}
