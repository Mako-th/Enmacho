using System.Globalization;
using Microsoft.Data.Sqlite;
using TH09.Record.Generated;

using Cols = TH09.Generated.DbColumns;

namespace TH09.Record;

public sealed record ReplayDeleteResult(
    IReadOnlyList<long> Requested, IReadOnlyList<long> Missing,
    SessionDeleteResult ScanSessions, IReadOnlyList<long> SharedSessionsKept,
    int GuessedLinksDetached, int ScanItemRows, int PathRows, int ReplayRows);

public static class ReplayDelete
{
    public static ReplayDeleteResult Run(SqliteConnection c, IReadOnlyList<long> replayIds, string? layer0Path,
                                         Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(replayIds);
        var say = log ?? (_ => { });
        var requested = replayIds.Distinct().ToList();
        var noSessions = new SessionDeleteResult([], 0, 0, false, [], [], []);
        if (requested.Count == 0)
            return new ReplayDeleteResult([], [], noSessions, [], 0, 0, 0, 0);

        var existing = Existing(c, requested);
        var missing = requested.Where(x => !existing.Contains(x)).ToList();
        if (missing.Count > 0)
            say("[DB] 記録から外す対象のうち、行が無かった replay_id が " + missing.Count + " 件あります: "
                + ScanLink.PyList(missing));
        var target = requested.Where(existing.Contains).ToList();
        if (target.Count == 0)
            return new ReplayDeleteResult(requested, missing, noSessions, [], 0, 0, 0, 0);

        var (scanSessions, shared) = ScanOnlySessions(c, target);

        var sessionResult = scanSessions.Count > 0
            ? SessionDelete.Run(c, scanSessions, layer0Path, say)
            : noSessions;

        int items, links, paths, replays;
        using (var tx = c.BeginTransaction())
        {
            var slots = Slots(target);
            items = Exec(c, tx, $"DELETE FROM {Cols.ReplayScanItems.Table}"
                                + $" WHERE {Cols.ReplayScanItems.ReplayId} IN ({slots})", target);
            links = Exec(c, tx, $"DELETE FROM {Cols.SessionReplays.Table}"
                                + $" WHERE {Cols.SessionReplays.ReplayId} IN ({slots})", target);
            paths = Exec(c, tx, $"DELETE FROM {Cols.ReplayPaths.Table}"
                                + $" WHERE {Cols.ReplayPaths.ReplayId} IN ({slots})", target);
            replays = Exec(c, tx, $"DELETE FROM {Cols.Replays.Table}"
                                  + $" WHERE {Cols.Replays.ReplayId} IN ({slots})", target);
            tx.Commit();
        }
        if (shared.Count > 0)
            say("[DB] 注意: 他のリプレイにも走査の紐付きがあるので、セッション " + ScanLink.PyList(shared)
                + " は消さずに紐付きだけ外しました");
        return new ReplayDeleteResult(requested, missing, sessionResult, shared, links, items, paths, replays);
    }

    private static HashSet<long> Existing(SqliteConnection c, IReadOnlyList<long> ids)
    {
        var found = new HashSet<long>();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT {Cols.Replays.ReplayId} FROM {Cols.Replays.Table}"
                        + $" WHERE {Cols.Replays.ReplayId} IN ({Slots(ids)})";
        Bind(cmd, ids);
        using var r = cmd.ExecuteReader();
        while (r.Read()) found.Add(r.GetInt64(0));
        return found;
    }

    private static (List<long> Delete, List<long> Shared) ScanOnlySessions(
        SqliteConnection c, IReadOnlyList<long> ids)
    {
        var sessions = new List<long>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = $"SELECT DISTINCT {Cols.SessionReplays.SessionId} FROM {Cols.SessionReplays.Table}"
                            + $" WHERE {Cols.SessionReplays.LinkMethod}=$m"
                            + $" AND {Cols.SessionReplays.ReplayId} IN ({Slots(ids)})"
                            + $" ORDER BY {Cols.SessionReplays.SessionId}";
            cmd.Parameters.AddWithValue("$m", RecordLabels.ScanLinkMethod);
            Bind(cmd, ids);
            using var r = cmd.ExecuteReader();
            while (r.Read()) sessions.Add(r.GetInt64(0));
        }
        var del = new List<long>();
        var shared = new List<long>();
        foreach (var sid in sessions)
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {Cols.SessionReplays.Table}"
                            + $" WHERE {Cols.SessionReplays.SessionId}=$s AND {Cols.SessionReplays.LinkMethod}=$m"
                            + $" AND {Cols.SessionReplays.ReplayId} NOT IN ({Slots(ids)})";
            cmd.Parameters.AddWithValue("$s", sid);
            cmd.Parameters.AddWithValue("$m", RecordLabels.ScanLinkMethod);
            Bind(cmd, ids);
            (Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) == 0 ? del : shared).Add(sid);
        }
        return (del, shared);
    }

    private static string Slots(IReadOnlyList<long> ids)
        => string.Join(",", ids.Select((_, i) => "$i" + i.ToString(CultureInfo.InvariantCulture)));

    private static void Bind(SqliteCommand cmd, IReadOnlyList<long> ids)
    {
        for (var i = 0; i < ids.Count; i++)
            cmd.Parameters.AddWithValue("$i" + i.ToString(CultureInfo.InvariantCulture), ids[i]);
    }

    private static int Exec(SqliteConnection c, SqliteTransaction tx, string sql, IReadOnlyList<long> ids)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        Bind(cmd, ids);
        return cmd.ExecuteNonQuery();
    }
}
