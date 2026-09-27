using System.Globalization;
using Microsoft.Data.Sqlite;

using Cols = TH09.Generated.DbColumns;

namespace TH09.Record;

public static class ScanResultsClear
{
    public sealed record Counts(IReadOnlyList<long> SessionIds, bool All, long JobRows, long ItemRows,
                                long ReplayRows, long ReplayPathRows);

    public static Counts Count(SqliteConnection c, bool all)
    {
        ArgumentNullException.ThrowIfNull(c);
        var ids = all
            ? AllSessionIds(c)
            : SessionProtection.Scanned(c).ReplayBySession.Keys.OrderBy(x => x).ToArray();
        return new Counts(ids, all,
                          RowCount(c, Cols.ReplayScanJobs.Table),
                          RowCount(c, Cols.ReplayScanItems.Table),
                          all ? RowCount(c, Cols.Replays.Table) : 0,
                          all ? RowCount(c, Cols.ReplayPaths.Table) : 0);
    }

    public static string Describe(Counts n)
    {
        ArgumentNullException.ThrowIfNull(n);
        var head = n.All ? "実プレイも含め全セッション " : "走査由来のセッション ";
        var line = "空にします: " + head + N(n.SessionIds.Count) + " 本 / 作業履歴 "
                 + N(n.JobRows + n.ItemRows) + " 件（" + Cols.ReplayScanJobs.Table + " " + N(n.JobRows)
                 + " / " + Cols.ReplayScanItems.Table + " " + N(n.ItemRows) + "）";
        if (n.All)
        {
            line += " / 登録 " + N(n.ReplayRows + n.ReplayPathRows) + " 件（"
                  + Cols.Replays.Table + " " + N(n.ReplayRows) + " / "
                  + Cols.ReplayPaths.Table + " " + N(n.ReplayPathRows) + "）";
        }
        return line;
    }

    public static SessionDeleteResult Run(SqliteConnection c, Counts n, string? layer0Path,
                                          Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(n);
        var result = SessionDelete.Run(c, n.SessionIds, layer0Path, log);
        Exec(c, "DELETE FROM " + Cols.ReplayScanItems.Table);
        Exec(c, "DELETE FROM " + Cols.ReplayScanJobs.Table);
        if (n.All)
        {
            Exec(c, "DELETE FROM " + Cols.ReplayPaths.Table);
            Exec(c, "DELETE FROM " + Cols.Replays.Table);
        }
        return result;
    }

    private static IReadOnlyList<long> AllSessionIds(SqliteConnection c)
    {
        var ids = new List<long>();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT " + Cols.Sessions.SessionId + " FROM " + Cols.Sessions.Table
                         + " ORDER BY " + Cols.Sessions.SessionId;
        using var r = cmd.ExecuteReader();
        while (r.Read()) ids.Add(r.GetInt64(0));
        return ids;
    }

    private static long RowCount(SqliteConnection c, string table)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM " + table;
        return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static string N(long v) => v.ToString(CultureInfo.InvariantCulture);
}
