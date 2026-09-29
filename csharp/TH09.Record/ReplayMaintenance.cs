using System.Globalization;
using Microsoft.Data.Sqlite;
using TH09.Record.Generated;

using Cols = TH09.Generated.DbColumns;

namespace TH09.Record;

public sealed record ReplayDescribeRow(long ReplayId, string Sha256, IReadOnlyList<(string Path, bool IsCurrent)> Paths,
                                       int ScanSessions, int GuessedLinks);

public sealed record ReplayDescription(IReadOnlyList<ReplayDescribeRow> Rows, IReadOnlyList<long> Missing)
{
    public int ScanSessions => Rows.Sum(r => r.ScanSessions);

    public int GuessedLinks => Rows.Sum(r => r.GuessedLinks);

    public int PathRows => Rows.Sum(r => r.Paths.Count);
}

public static class ReplayMaintenance
{
    public static ReplayDescription Describe(string mainDb, IReadOnlyList<long> replayIds)
    {
        ArgumentNullException.ThrowIfNull(replayIds);
        using var db = RecordDb.OpenReadOnly(mainDb)
            ?? throw new FileNotFoundException("本体 DB を読み取り専用で開けません: " + mainDb, mainDb);
        var c = db.Connection;
        var rows = new List<ReplayDescribeRow>();
        var missing = new List<long>();
        foreach (var rid in replayIds.Distinct())
        {
            string? sha;
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = $"SELECT {Cols.Replays.Sha256} FROM {Cols.Replays.Table}"
                                + $" WHERE {Cols.Replays.ReplayId}=$0";
                cmd.Parameters.AddWithValue("$0", rid);
                sha = cmd.ExecuteScalar() as string;
            }
            if (sha is null) { missing.Add(rid); continue; }

            var paths = new List<(string, bool)>();
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = $"SELECT {Cols.ReplayPaths.FullPath},{Cols.ReplayPaths.IsCurrent}"
                                + $" FROM {Cols.ReplayPaths.Table} WHERE {Cols.ReplayPaths.ReplayId}=$0"
                                + $" ORDER BY {Cols.ReplayPaths.ReplayPathId}";
                cmd.Parameters.AddWithValue("$0", rid);
                using var r = cmd.ExecuteReader();
                while (r.Read()) paths.Add((r.GetString(0), r.GetInt64(1) != 0));
            }

            int scan = 0, guessed = 0;
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = $"SELECT {Cols.SessionReplays.LinkMethod} FROM {Cols.SessionReplays.Table}"
                                + $" WHERE {Cols.SessionReplays.ReplayId}=$0";
                cmd.Parameters.AddWithValue("$0", rid);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var method = r.IsDBNull(0) ? null : r.GetString(0);
                    if (string.Equals(method, RecordLabels.ScanLinkMethod, StringComparison.Ordinal)) scan++;
                    else guessed++;
                }
            }
            rows.Add(new ReplayDescribeRow(rid, sha, paths, scan, guessed));
        }
        return new ReplayDescription(rows, missing);
    }

    public static ReplayDeleteResult Delete(string mainDb, string? layer0Db, IReadOnlyList<long> replayIds,
                                            Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(replayIds);
        if (!File.Exists(mainDb)) throw new FileNotFoundException("本体 DB がありません: " + mainDb, mainDb);
        using var db = RecordDb.OpenReadWrite(mainDb);
        return ReplayDelete.Run(db.Connection, replayIds, layer0Db, log);
    }

    public static string Line(ReplayDescription d)
    {
        ArgumentNullException.ThrowIfNull(d);
        return "記録から外します: リプレイ " + N(d.Rows.Count) + " 本 / " + Breakdown(d);
    }

    public static string SessionsLine(ReplayDescription d)
    {
        ArgumentNullException.ThrowIfNull(d);
        return "記録から外します: " + Breakdown(d);
    }

    private static string Breakdown(ReplayDescription d)
    {
        var line = "走査のセッション " + N(d.ScanSessions) + " 本（消す）/ 紐付きだけ外すセッション "
                 + N(d.GuessedLinks) + " 本 / 置き場 " + N(d.PathRows) + " 行";
        if (d.Missing.Count > 0) line += " ／ 行が無かった " + N(d.Missing.Count) + " 件";
        return line;
    }

    private static string N(int v) => v.ToString("N0", CultureInfo.InvariantCulture);

    public static bool PointsAtRealData(string mainDb, string? layer0Db)
        => HistoryMaintenance.PointsAtRealData(mainDb, layer0Db);
}
