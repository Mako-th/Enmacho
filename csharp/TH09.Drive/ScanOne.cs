using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Data.Sqlite;
using TH09.Record;

using Cols = TH09.Generated.DbColumns;
using ScanItems = TH09.Generated.DbColumns.ReplayScanItems;

namespace TH09.Drive;

public sealed record CaptureRequest(
    SqliteConnection Conn,
    CanonJson.Node Decoded,
    string? DecodedJson,
    HitWindows Windows,
    int Slot,
    double Timeout,
    double AttachDelaySec,
    bool Announce,
    Func<bool>? ShouldStop,
    Action<string> Log);

public sealed record SpareLockNames(string Writer, string Scan);

public sealed record ScanOneResult(
    int ExitCode, long JobId, long ItemId, CaptureResult Capture,
    string? VerifyStatus, bool Linked, SupersededPlan? Superseded, string? Problem);

[SupportedOSPlatform("windows")]
public static class ScanOne
{
    public const string JobNote = "Phase 4 single (manual playback)";

    private const string ManualHead = "―― ここから手動 ――――――――――――――――――――――――――――";

    private const string ManualTail = "――――――――――――――――――――――――――――――――――";

    private static readonly string[] SummaryHealth =
    [
        ScanItems.Layer0Ticks, ScanItems.Layer0Segments, ScanItems.HitWindows,
        ScanItems.HitsSeen, ScanItems.QuicksSeen, ScanItems.CoordTicks,
        ScanItems.HitWindowMinSlack,
    ];

    public sealed record AutoParts(
        int Pid,
        IMenuView View,
        Func<IInputSink> MakeSink,
        Func<IDisposable?>? CoordRing = null,
        IMenuMemory? Mem = null,
        bool NoSkip = false,
        string? NativeDir = null,
        ISeatHost? Host = null);

    public sealed record Options(
        string DbPath,
        long ReplayId,
        string? Layer0Db,
        int? Slot = null,
        bool Auto = false,
        double Timeout = 900.0,
        string? Speed = null,
        Func<CaptureRequest, CaptureResult>? Capture = null,
        HitWindows? Windows = null,
        AutoParts? Parts = null,
        string? ReplayDir = null,
        double? AttachDelaySec = null,
        SpareLockNames? SpareLocks = null,
        TextWriter? Out = null,
        Action<string>? Log = null,
        string? Path = null);

    public static string? RealLockNameIn(SpareLockNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        if (ScanLedger.IsRealLockName(names.Writer)) return names.Writer;
        if (ScanLedger.IsRealLockName(names.Scan)) return names.Scan;
        return null;
    }

    public static ScanOneResult Run(Options o)
    {
        ArgumentNullException.ThrowIfNull(o);
        var log = o.Log ?? ReplaySlots.DefaultLog;
        var w = o.Out ?? Console.Out;
        var rid = o.ReplayId;

        Head head;
        using (var ro = RecordDb.OpenReadOnly(o.DbPath))
        {
            if (ro is null)
                throw new ScanSetupFailed("エラー: 本体 DB を開けません: " + o.DbPath);
            head = ReadHead(ro.Connection, rid, o.Path);
        }
        var replayDir = o.ReplayDir ?? Paths.Default.GameReplayDir()
            ?? throw new ScanSetupFailed("エラー: ゲームの replay フォルダが見つかりません。");

        var decoded = ReplayStages.Parse(head.DecodedJson);
        var stages = ReplayStages.P1Stages(decoded);
        var rule = new string('=', ScanTargets.RuleWidth);
        w.Write(rule + "\n");
        w.Write(ScanTargets.DescribeReplay(head.Row, stages.Count) + "\n");
        w.Write("ファイル: " + head.SourceFile + "\n");
        if (head.OtherPaths.Count > 0)
        {
            w.Write("同じリプレイの別の置き場所: " + Num(head.OtherPaths.Count) + " か所\n");
            foreach (var other in head.OtherPaths) w.Write("  " + other + "\n");
        }
        w.Write(rule + "\n");

        ReplaySlots.RestoreLeftoverBackups(replayDir, log);
        using var ledger = OpenLedger(o.DbPath, o.SpareLocks, log);
        var conn = ledger.Connection;

        var hit = o.Windows ?? new HitWindows(null, log);
        hit.ReplaySource = head.Source;

        var jobId = ledger.StartJob(o.Speed, JobNote);
        var itemId = ledger.StartItem(jobId, rid, 1);

        var result = new CaptureResult();
        string? verifyStatus = null;
        var linked = false;
        SupersededPlan? plan = null;
        try
        {
            try
            {
                using var occ = new SlotOccupation(replayDir, head.SourceFile, o.Slot, jobId, log);
                occ.Enter();
                var req = new CaptureRequest(
                    Conn: conn, Decoded: decoded, DecodedJson: head.DecodedJson,
                    Windows: hit, Slot: occ.Slot, Timeout: o.Timeout,
                    AttachDelaySec: o.AttachDelaySec ?? Paths.Default.TickHookAttachDelaySec,
                    Announce: !o.Auto, ShouldStop: null, Log: log);
                var capture = o.Capture ?? throw new ScanSetupFailed(
                    "捕捉の役が渡されていません（読み口と Layer 0 の配線は単位 6b）。");
                if (o.Auto)
                {
                    result = PlayAutomatically(o, req, capture, log);
                }
                else
                {
                    WriteManualBanner(w, occ.Slot, o.Timeout);
                    result = capture(req);
                }
            }
            catch (Exception exc)
            {
                if (string.Equals(result.Status, CaptureStatus.NotRun, StringComparison.Ordinal))
                    result.Status = AutoPlay.StatusOf(exc);
                if (string.IsNullOrEmpty(result.Error))
                    result.Error = exc.GetType().Name + ": " + exc.Message;
                throw;
            }
        }
        finally
        {
            if (result.SessionId is long sid)
            {
                var v = VerifySession.Run(conn, sid, decoded);
                verifyStatus = v.Status;
                w.Write("\n");
                w.Write("突合（session=" + Num(sid) + "）: " + verifyStatus + "\n");
                foreach (var line in v.Lines) w.Write(line + "\n");
                if (string.Equals(verifyStatus, VerifySession.StatusOk, StringComparison.Ordinal))
                {
                    ScanLink.Link(conn, sid, rid);
                    linked = true;
                    w.Write("  → session_replays へ紐付けました（" + ScanLink.Method + " / "
                            + ScanLink.ConfidenceText + "）\n");
                    plan = ScanLink.Superseded(conn, rid, sid);
                    foreach (var line in Drop(conn, plan, o.Layer0Db)) w.Write("  " + line + "\n");
                }
                else
                {
                    w.Write("  → 突合が通らないので紐付けません（session=" + Num(sid)
                            + " は記録として残ります）\n");
                }
                w.Write("\n");
                foreach (var line in VerifySession.CaptureSummary(conn, sid)) w.Write("  " + line + "\n");
            }
            ledger.EndItem(itemId, new ScanItemResult(
                result.Status, result.SessionId, verifyStatus, result.GapCount,
                string.IsNullOrEmpty(result.EarlyMismatch) ? result.Error : result.EarlyMismatch,
                result.Health));
            ledger.EndJob(jobId, result.Status);
        }

        var problem = string.Equals(result.Status, CaptureStatus.Captured, StringComparison.Ordinal)
            ? CaptureHealth.Problem(result, stages.Count) : null;
        WriteSummary(w, result, problem, jobId, itemId, rule);
        var exitCode = !string.Equals(result.Status, CaptureStatus.Captured, StringComparison.Ordinal)
            ? 1
            : string.Equals(verifyStatus, VerifySession.StatusOk, StringComparison.Ordinal) ? 0 : 2;
        return new ScanOneResult(exitCode, jobId, itemId, result, verifyStatus, linked, plan, problem);
    }


    public static IDisposable AcquireExclusive(string dbPath, SpareLockNames? spareLocks = null,
                                               Action<string>? log = null) =>
        OpenLedger(dbPath, spareLocks, log ?? ReplaySlots.DefaultLog);

    private static ScanLedger OpenLedger(string dbPath, SpareLockNames? spareLocks,
                                         Action<string> log) =>
        (spareLocks is { } spare
            ? ScanLedger.OpenWithSpareLocks(dbPath, spare.Writer, spare.Scan, log)
            : ScanLedger.Open(dbPath, log))
        ?? throw new ScanSetupFailed(
            "エラー: 別のプロセスが同じDBへセッションを書いています。" + Lf
            + "       GUI の「監視」を OFF にするか、走査を止めてから実行してください。" + Lf
            + "       （走査と通常の監視は同時に動かせません）");


    public static int RestoreSlots(TextWriter w, string? replayDir = null, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(w);
        var dir = replayDir ?? Paths.Default.GameReplayDir()
            ?? throw new ScanSetupFailed("エラー: ゲームの replay フォルダが見つかりません。");
        var sweep = ReplaySlots.RestoreLeftoverBackups(dir, log);
        w.Write("復元: " + Num(sweep.Restored) + " 件（" + dir + "）\n");
        return 0;
    }

    public static int Verify(TextWriter w, SqliteConnection conn, long replayId, long sessionId)
    {
        ArgumentNullException.ThrowIfNull(w);
        var (row, _status, _source) = ReadRow(conn, replayId);
        var decoded = ReplayStages.Parse(row.DecodedJson);
        var v = VerifySession.Run(conn, sessionId, decoded);
        w.Write(ScanTargets.DescribeReplay(row, ReplayStages.P1Stages(decoded).Count) + "\n");
        w.Write("session=" + Num(sessionId) + " との突合: " + v.Status + "\n");
        foreach (var line in v.Lines) w.Write(line + "\n");
        w.Write("\n");
        foreach (var line in VerifySession.CaptureSummary(conn, sessionId)) w.Write("  " + line + "\n");
        return string.Equals(v.Status, VerifySession.StatusOk, StringComparison.Ordinal) ? 0 : 1;
    }


    private static CaptureResult PlayAutomatically(Options o, CaptureRequest req,
                                                   Func<CaptureRequest, CaptureResult> capture,
                                                   Action<string> log)
    {
        var parts = o.Parts ?? throw new ScanSetupFailed(
            "エラー: --auto には pid とメニューの読み書きが要ります（単位 6b が渡します）。");
        return AutoPlay.Run(new AutoPlay.Options(
            Slot: req.Slot,
            Pid: parts.Pid,
            View: parts.View,
            MakeSink: parts.MakeSink,
            Capture: stop => capture(req with { Announce = false, ShouldStop = stop }),
            CoordRing: parts.CoordRing,
            Mem: parts.Mem,
            DecodedJson: req.DecodedJson,
            NoSkip: parts.NoSkip,
            NativeDir: parts.NativeDir,
            Host: parts.Host,
            Log: log));
    }


    public static IReadOnlyList<string> Drop(SqliteConnection conn, SupersededPlan plan,
                                             string? layer0Db)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Empty) return [];
        if (plan.KeptOnly) return [ScanLink.KeptLine(plan.Keep)];
        var notes = new List<string>();
        var done = SessionDelete.Run(conn, plan.Delete, layer0Db, notes.Add);
        if (done.Deleted.Count == 0)
        {
            return ["★古いセッション " + ScanLink.PyList(plan.Delete) + " を消せませんでした（"
                    + (notes.Count > 0 ? string.Join("; ", notes) : "理由不明") + "）"];
        }
        return ["古いセッションを消しました: " + ScanLink.JoinSorted(done.Deleted)
                + "（同じリプレイの再走査で置き換え）"];
    }

    private static void WriteManualBanner(TextWriter w, int slot, double timeout)
    {
        w.Write("\n");
        w.Write(ManualHead + "\n");
        w.Write("  1. ゲームのタイトルで REPLAY を選ぶ\n");
        w.Write("  2. スロット " + Pad2(slot) + " を選んで再生を開始する\n");
        w.Write("  3. 再生が終わってリザルト／タイトルへ戻るまで待つ\n");
        w.Write("  （" + ScanTargets.F(timeout, 0)
                + " 秒でタイムアウト。Ctrl+C で中止してもスロットは元に戻ります）\n");
        w.Write(ManualTail + "\n");
        w.Write("\n");
    }

    private static void WriteSummary(TextWriter w, CaptureResult result, string? problem,
                                     long jobId, long itemId, string rule)
    {
        var h = result.Health;
        var v = SummaryHealth.Select(k => h.TryGetValue(k, out var x) && x is long n
                                          ? n.ToString(CultureInfo.InvariantCulture)
                                          : Dash).ToArray();
        w.Write("\n");
        w.Write(rule + "\n");
        w.Write("結果: " + result.Status + " / 取得経路=" + (result.SourceKind ?? "None")
                + " / 欠落=" + Num(result.GapCount) + " 件\n");
        w.Write("記録: Layer 0 " + v[0] + " tick / " + v[1] + " セグメント・被弾窓 " + v[2] + " 本"
                + "（起点 " + v[3] + " 件・うちクイック " + v[4] + " 件 / 座標リング " + v[5]
                + " tick / 最小余裕 " + v[6] + " tick）\n");
        if (problem is not null)
            w.Write("★注意: captured ですが中身が伴っていません → " + problem + "\n");
        w.Write("所要時間: " + ScanTargets.F(result.Elapsed, 1) + " 秒（検証基準6のバッチ見積根拠）\n");
        w.Write("job_id=" + Num(jobId) + " item_id=" + Num(itemId) + "\n");
        w.Write(rule + "\n");
    }


    private sealed record Head(ReplayRow Row, string? Source, string? DecodedJson, string SourceFile,
                               IReadOnlyList<string> OtherPaths);

    private static Head ReadHead(SqliteConnection conn, long replayId, string? path)
    {
        var (row, status, source) = ReadRow(conn, replayId);
        if (!string.Equals(status, ScanTargets.DecodedStatus, StringComparison.Ordinal))
        {
            throw new ScanSetupFailed(
                "エラー: replay_id=" + Num(replayId) + " はデコードされていません "
                + "（decode_status=" + (status ?? "None") + "）。突合できないのでスキャンしません。");
        }
        var all = ScanTargets.ExistingPaths(conn, replayId);
        var resolved = path ?? (all.Count > 0 ? all[0] : null)
            ?? throw new ScanSetupFailed(
                "エラー: replay_id=" + Num(replayId) + " の実ファイルが見つかりません。");
        var others = all.Where(p => !string.Equals(p, resolved, StringComparison.Ordinal)).ToList();
        return new Head(row, source, row.DecodedJson, resolved, others);
    }

    private static (ReplayRow Row, string? Status, string? Source) ReadRow(
        SqliteConnection conn, long replayId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT " + Cols.Replays.Mode + "," + Cols.Replays.Difficulty + ","
                        + Cols.Replays.P1Char + "," + Cols.Replays.IsOwn + ","
                        + Cols.Replays.DecodeStatus + "," + Cols.Replays.DecodedJson + ","
                        + Cols.Replays.Source
                        + " FROM " + Cols.Replays.Table + " WHERE " + Cols.Replays.ReplayId + "=$0";
        cmd.Parameters.AddWithValue("$0", replayId);
        using var r = cmd.ExecuteReader();
        if (!r.Read())
            throw new ScanSetupFailed("エラー: replay_id=" + Num(replayId) + " がありません。");
        var row = new ReplayRow(
            replayId,
            r.IsDBNull(0) ? null : r.GetInt64(0),
            r.IsDBNull(1) ? null : r.GetInt64(1),
            r.IsDBNull(2) ? null : r.GetInt64(2),
            r.IsDBNull(3) ? null : r.GetInt64(3),
            r.IsDBNull(5) ? null : r.GetString(5));
        return (row, r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(6) ? null : r.GetString(6));
    }

    private static readonly string Lf = ((char)10).ToString();

    private const string Dash = "—";

    private static string Num(long v) => v.ToString(CultureInfo.InvariantCulture);

    private static string Pad2(int v) => v.ToString("00", CultureInfo.InvariantCulture);
}
