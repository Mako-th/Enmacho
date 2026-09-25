using Microsoft.Data.Sqlite;
using TH09.Layer0;
using TH09.Record.Generated;

using SessionCols = TH09.Generated.DbColumns.Sessions;
using RoundCols = TH09.Generated.DbColumns.Rounds;
using TombCols = TH09.Generated.DbColumns.DeletedSessions;

namespace TH09.Record;

public sealed record SessionDeleteResult(
    IReadOnlyList<long> Deleted, long Layer0Rows, long Floor, bool Seeded,
    IReadOnlyList<long> Tombstoned, IReadOnlyList<long> WithoutTombstone,
    IReadOnlyList<long> Running);

public static class SessionDelete
{
    public static SessionDeleteResult Run(SqliteConnection c, IReadOnlyList<long> sessionIds,
                                          string? layer0Path, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(sessionIds);
        var say = log ?? (_ => { });
        var ids = sessionIds.ToList();
        if (ids.Count == 0)
            return new SessionDeleteResult([], 0, 0, false, [], [], []);

        var running = RunningIn(c, ids);
        if (running.Count > 0)
            say("[layer0] 注意: 記録中のセッション " + ScanLink.PyList(running) + " を削除します。"
                + "記録側がまだ書いていると、そのぶんの生tickが孤児として残ります"
                + "（`python tools/tick_archive.py orphans` で確認・掃除できます）");

        var floor = Repository.Layer0SessionFloor(layer0Path, say);

        long removed = 0;
        if (layer0Path is not null)
        {
            removed = Layer0Delete.DeleteSessions(layer0Path, ids);
            if (removed > 0)
                say($"[layer0] {ids.Count} セッションぶんのセグメント {removed} 件を削除しました");
        }

        var seeded = Repository.SeedSessionHighWater(c, floor);

        var kept = WriteTombstones(c, ids, say);
        var withoutTombstone = HasTable(c, TombCols.Table)
            ? ids.Where(x => !kept.Contains(x)).ToList() : [];
        if (withoutTombstone.Count > 0)
            say("[DB] 注意: session=" + ScanLink.PyList(withoutTombstone)
                + " は控え（tombstone）なしで削除します"
                + "（生tickが残っていても実時間は戻せません）");

        DeleteFromMain(c, ids);
        return new SessionDeleteResult(ids, removed, floor, seeded, kept, withoutTombstone, running);
    }

    private static List<long> RunningIn(SqliteConnection c, IReadOnlyList<long> ids)
    {
        var found = new List<long>();
        try
        {
            using var cmd = c.CreateCommand();
            var slots = string.Join(",", ids.Select((_, i) => "$" + i));
            cmd.CommandText = $"SELECT {SessionCols.SessionId} FROM {SessionCols.Table}"
                            + $" WHERE {SessionCols.Status}='running'"
                            + $" AND {SessionCols.SessionId} IN ({slots})";
            for (var i = 0; i < ids.Count; i++) cmd.Parameters.AddWithValue("$" + i, ids[i]);
            using var r = cmd.ExecuteReader();
            while (r.Read()) found.Add(r.GetInt64(0));
        }
        catch (SqliteException)
        {
            return [];
        }
        return found;
    }

    private static List<long> WriteTombstones(SqliteConnection c, IReadOnlyList<long> ids,
                                              Action<string> say)
    {
        if (!HasTable(c, TombCols.Table))
        {
            say("[DB] 警告: deleted_sessions テーブルがありません（移行前のDB）。"
                + "削除は続けますが、**この削除は後から復元できません**"
                + "（アプリを起動し直すと移行で作られます）");
            return [];
        }
        var done = new List<long>();
        var at = LiveTickSource.NowIso();
        foreach (var sid in ids)
        {
            try
            {
                if (Repository.WriteTombstone(c, sid, at)) done.Add(sid);
            }
            catch (Exception exc) when (exc is SqliteException or InvalidDataException)
            {
                say($"[DB] 警告: session={sid} の控え（tombstone）を書けませんでした（{exc.Message}）。"
                    + "削除は続けますが、**この1本は後から復元できません**");
            }
        }
        return done;
    }

    private static void DeleteFromMain(SqliteConnection c, IReadOnlyList<long> ids)
    {
        RecordDb.Exec(c, "PRAGMA foreign_keys=OFF");
        using var tx = c.BeginTransaction();
        foreach (var sid in ids)
        {
            foreach (var table in RecordLabels.RoundChildTables)
            {
                if (!HasTable(c, table)) continue;
                Run(c, tx, $"DELETE FROM \"{table}\" WHERE {RoundCols.RoundRecordId} IN"
                           + $" (SELECT {RoundCols.RoundRecordId} FROM {RoundCols.Table}"
                           + $" WHERE {RoundCols.SessionId}=$0)", sid);
            }
            foreach (var table in RecordLabels.SessionChildTables)
                Run(c, tx, $"DELETE FROM \"{table}\" WHERE {SessionCols.SessionId}=$0", sid);
            foreach (var (table, col) in RecordLabels.SessionRefNullable)
            {
                if (!HasTable(c, table)) continue;
                Run(c, tx, $"UPDATE \"{table}\" SET \"{col}\"=NULL WHERE \"{col}\"=$0", sid);
            }
            Run(c, tx, $"DELETE FROM {SessionCols.Table} WHERE {SessionCols.SessionId}=$0", sid);
        }
        tx.Commit();
    }

    private static void Run(SqliteConnection c, SqliteTransaction tx, string sql, long sid)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$0", sid);
        cmd.ExecuteNonQuery();
    }

    private static bool HasTable(SqliteConnection c, string name)
    {
        try
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$0";
            cmd.Parameters.AddWithValue("$0", name);
            return cmd.ExecuteScalar() is not null;
        }
        catch (SqliteException)
        {
            return false;
        }
    }
}
