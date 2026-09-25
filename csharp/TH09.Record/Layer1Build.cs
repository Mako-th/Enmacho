using System.Globalization;
using Microsoft.Data.Sqlite;
using TH09.Generated;
using TH09.Record.Generated;

using MetricCols = TH09.Generated.DbColumns.RoundMetrics;
using RoundCols = TH09.Generated.DbColumns.Rounds;

namespace TH09.Record;

public static class Layer1Build
{
    public sealed record BuildPlan(
        IReadOnlyDictionary<long, List<long>> Build,
        int Fresh,
        int Stale,
        IReadOnlyList<long> Orphans)
    {
        public int Total => Build.Values.Sum(v => v.Count);
    }

    public sealed record Summary(
        int Sessions,
        int Rows,
        int Deleted,
        int Fresh,
        int Stale,
        IReadOnlyList<(long SessionId, string Why)> Skipped,
        double Seconds);


    public static BuildPlan Plan(SqliteConnection main, long? onlySession, bool rebuildAll)
    {
        ArgumentNullException.ThrowIfNull(main);
        var build = new Dictionary<long, List<long>>();
        var fresh = 0;
        var stale = 0;

        using (var cmd = main.CreateCommand())
        {
            cmd.CommandText =
                $"SELECT r.{RoundCols.SessionId},r.{RoundCols.RoundRecordId},"
                + $"m.{MetricCols.RoundRecordId} IS NULL,m.{MetricCols.AnalysisVersion}"
                + $" FROM {RoundCols.Table} r LEFT JOIN {MetricCols.Table} m"
                + $" ON m.{MetricCols.RoundRecordId}=r.{RoundCols.RoundRecordId}"
                + (onlySession is null ? "" : $" WHERE r.{RoundCols.SessionId}=$0")
                + $" ORDER BY r.{RoundCols.SessionId},r.{RoundCols.RoundRecordId}";
            if (onlySession is { } sid) cmd.Parameters.AddWithValue("$0", sid);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var sessionId = r.GetInt64(0);
                var roundId = r.GetInt64(1);
                var isNew = r.GetInt64(2) != 0;
                long? version = r.IsDBNull(3) ? null : r.GetInt64(3);
                if (isNew) fresh++;
                else if (version != RecordLabels.RoundMetricsAnalysisVersion) stale++;
                else if (!rebuildAll && onlySession is null) continue;
                if (!build.TryGetValue(sessionId, out var list)) build[sessionId] = list = [];
                list.Add(roundId);
            }
        }

        var orphans = new List<long>();
        using (var cmd = main.CreateCommand())
        {
            cmd.CommandText =
                $"SELECT {MetricCols.RoundRecordId} FROM {MetricCols.Table}"
                + $" WHERE {MetricCols.RoundRecordId} NOT IN"
                + $" (SELECT {RoundCols.RoundRecordId} FROM {RoundCols.Table})";
            using var r = cmd.ExecuteReader();
            while (r.Read()) orphans.Add(r.GetInt64(0));
        }
        return new BuildPlan(build, fresh, stale, orphans);
    }

    public static void CheckColumns(SqliteConnection main)
    {
        ArgumentNullException.ThrowIfNull(main);
        var have = new List<string>();
        using (var cmd = main.CreateCommand())
        {
            cmd.CommandText = $"PRAGMA table_info({MetricCols.Table})";
            using var r = cmd.ExecuteReader();
            while (r.Read()) have.Add(r.GetString(1));
        }
        var want = new List<string> { MetricCols.RoundRecordId };
        want.AddRange(RecordLabels.RoundMetricsColumns);
        if (have.SequenceEqual(want, StringComparer.Ordinal)) return;
        throw new InvalidDataException(
            "round_metrics の列がツール側の並びと違います。"
            + "\n  DB   : [" + string.Join(", ", have) + "]"
            + "\n  ツール: [" + string.Join(", ", want) + "]");
    }


    public static (int Rows, string? Why) BuildSession(SqliteConnection main, SqliteConnection layer0,
                                                       long sessionId, IReadOnlySet<long>? wanted,
                                                       Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(layer0);
        ArgumentNullException.ThrowIfNull(log);

        var (data, why) = RoundRanges.LoadColumns(layer0, sessionId);
        if (data is null) return (0, why ?? "?");
        var mode = RoundMetrics.SessionMode(main, sessionId) ?? data.Mode[0];
        var (idMap, dup) = RoundMetrics.RoundIdMap(main, sessionId);
        if (dup > 0)
            log($"  注意: session={Num(sessionId)} は (面,ラウンド) が重複しています（{Num(dup)} 件）。"
                + "先に見つかった方を使います");
        var ranges = RoundRanges.Compute(data);
        var kurai = RoundMetrics.KuraiCounts(main, sessionId, mode);

        var rows = new List<(long RoundId, Dictionary<string, object?> Values)>();
        var unmatched = 0;
        foreach (var (key, parts) in ranges.OrderBy(t => t.Parts[0].Start))
        {
            if (!idMap.TryGetValue(key, out var rid))
            {
                unmatched++;
                continue;
            }
            if (wanted is not null && !wanted.Contains(rid)) continue;
            var k1 = new RoundMetrics.KuraiKey(key.StageNumber, key.RoundNumber, 1);
            var k2 = new RoundMetrics.KuraiKey(key.StageNumber, key.RoundNumber, 2);
            rows.Add((rid, RoundMetrics.BuildRowValues(data, parts,
                                                      kurai.GetValueOrDefault(k1),
                                                      kurai.GetValueOrDefault(k2))));
        }
        if (unmatched > 0)
            log($"  注意: session={Num(sessionId)} は Layer 0 から切り出した {Num(unmatched)} ラウンドが"
                + " `rounds` に見つかりません");

        WriteRows(main, rows);
        return (rows.Count, null);
    }

    public static Summary Run(ScanLedger ledger, string layer0Path, long? onlySession, bool rebuildAll,
                              Action<string> log, double progressSeconds = 1.0)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(log);
        var main = ledger.Connection;

        CheckColumns(main);
        var todo = Plan(main, onlySession, rebuildAll);
        if (todo.Orphans.Count > 0)
        {
            DeleteOrphans(main, todo.Orphans);
            log($"孤児 {Num(todo.Orphans.Count)} 行を消しました（`rounds` に無い round_record_id）");
        }

        var sessions = todo.Build.Keys.Order().ToList();
        var skipped = new List<(long SessionId, string Why)>();
        if (sessions.Count == 0)
        {
            log("作り直すラウンドはありません（増分更新は何もしませんでした）");
            return new Summary(0, 0, todo.Orphans.Count, todo.Fresh, todo.Stale, skipped, 0.0);
        }
        if (!File.Exists(layer0Path))
        {
            log($"Layer 0 がありません: {layer0Path}"
                + $"（{Num(sessions.Count)} セッションぶんの集計を作れませんでした）");
            foreach (var sid in sessions) skipped.Add((sid, "Layer 0 の DB が無い"));
            return new Summary(0, 0, todo.Orphans.Count, todo.Fresh, todo.Stale, skipped, 0.0);
        }

        using var layer0 = TickReplay.OpenLayer0(layer0Path);
        var t0 = DateTime.UtcNow;
        var last = t0;
        var doneSessions = 0;
        var doneRows = 0;
        for (var n = 1; n <= sessions.Count; n++)
        {
            var sid = sessions[n - 1];
            var (rows, why) = BuildSession(main, layer0, sid,
                                           todo.Build[sid].ToHashSet(), log);
            doneSessions++;
            doneRows += rows;
            if (why is not null) skipped.Add((sid, why));
            var now = DateTime.UtcNow;
            if ((now - last).TotalSeconds >= progressSeconds || n == sessions.Count)
            {
                log(ScanProgressLines.Layer1Progress(n, sessions.Count, doneRows,
                                                     (now - t0).TotalSeconds));
                last = now;
            }
        }
        var seconds = (DateTime.UtcNow - t0).TotalSeconds;
        if (skipped.Count > 0)
            log($"見送り {Num(skipped.Count)} セッション（Layer 0 が無い等）: "
                + string.Join(", ", skipped.Take(5).Select(s => $"({Num(s.SessionId)}, {s.Why})")));
        log($"完了: {Num(doneSessions)} セッション / {Num(doneRows)} 行 / {Secs(seconds)} 秒");
        return new Summary(doneSessions, doneRows, todo.Orphans.Count, todo.Fresh, todo.Stale,
                           skipped, seconds);
    }


    public static int RunCheck(TextWriter w, string dbPath, long? onlySession, bool rebuildAll)
    {
        ArgumentNullException.ThrowIfNull(w);
        using var db = RecordDb.OpenReadOnly(dbPath);
        if (db is null)
        {
            Console.Error.WriteLine("本体 DB を読み取り専用で開けません: " + dbPath);
            return 1;
        }
        var main = db.Connection;
        if (!HasRoundMetrics(main))
        {
            w.WriteLine("round_metrics がまだありません（アプリか本ツールを 1 回動かすと移行で作られます）");
            w.WriteLine($"  作る {Num(CountRounds(main))} 行 / 作り直す 0 行 / 消す 0 行");
            return 0;
        }
        var todo = Plan(main, onlySession, rebuildAll);
        w.WriteLine($"analysis_version={Num(RecordLabels.RoundMetricsAnalysisVersion)}");
        w.WriteLine($"  作る       {Num(todo.Fresh)} 行（round_metrics に無いラウンド）");
        w.WriteLine($"  作り直す   {Num(todo.Stale)} 行（analysis_version が違うラウンド）");
        w.WriteLine($"  消す       {Num(todo.Orphans.Count)} 行（rounds に無い孤児）");
        w.WriteLine($"  書く合計   {Num(todo.Total)} 行 / {Num(todo.Build.Count)} セッション");
        return 0;
    }


    private static void WriteRows(SqliteConnection main,
                                  List<(long RoundId, Dictionary<string, object?> Values)> rows)
    {
        if (rows.Count == 0) return;
        var names = RecordLabels.RoundMetricsColumns;
        var sql = $"INSERT OR REPLACE INTO {MetricCols.Table}({MetricCols.RoundRecordId},"
                  + string.Join(",", names) + ") VALUES("
                  + string.Join(",", Enumerable.Range(0, names.Length + 1).Select(i => "$" + Num(i)))
                  + ")";
        using var tx = main.BeginTransaction();
        using var cmd = main.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$0", 0L);
        for (var i = 0; i < names.Length; i++) cmd.Parameters.AddWithValue("$" + Num(i + 1), DBNull.Value);
        cmd.Prepare();
        foreach (var (roundId, values) in rows)
        {
            cmd.Parameters[0].Value = roundId;
            for (var i = 0; i < names.Length; i++)
                cmd.Parameters[i + 1].Value = values[names[i]] ?? DBNull.Value;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private static void DeleteOrphans(SqliteConnection main, IReadOnlyList<long> orphans)
    {
        using var tx = main.BeginTransaction();
        using var cmd = main.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"DELETE FROM {MetricCols.Table} WHERE {MetricCols.RoundRecordId}=$0";
        cmd.Parameters.AddWithValue("$0", 0L);
        foreach (var id in orphans)
        {
            cmd.Parameters[0].Value = id;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private static bool HasRoundMetrics(SqliteConnection main)
    {
        using var cmd = main.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$0";
        cmd.Parameters.AddWithValue("$0", MetricCols.Table);
        return cmd.ExecuteScalar() is not null;
    }

    private static long CountRounds(SqliteConnection main)
    {
        using var cmd = main.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {RoundCols.Table}";
        return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static string Num(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Secs(double value) => value.ToString("F1", CultureInfo.InvariantCulture);
}
