using Microsoft.Data.Sqlite;
using TH09.Layer0.Generated;

namespace TH09.Layer0;

public static class Layer0Delete
{
    public const string SessionIdColumn = "session_id";

    public static long DeleteSessions(string dbPath, IReadOnlyCollection<long> sessionIds)
    {
        ArgumentException.ThrowIfNullOrEmpty(dbPath);
        ArgumentNullException.ThrowIfNull(sessionIds);
        if (sessionIds.Count == 0) return 0;
        if (!File.Exists(dbPath)) return 0;

        var tables = Layer0Schema.Tables;
        if (tables.Length == 0)
            throw new InvalidOperationException(
                "★Layer 0 の表が 1 つも生成されていません（生成物 Layer0Schema を読めていない）。");

        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            DefaultTimeout = Layer0Schema.BusyTimeoutMs / 1000,
        };
        using var conn = new SqliteConnection(csb.ToString());
        conn.Open();
        ApplyPragmas(conn);

        var missing = Missing(conn, tables);
        if (missing.Count > 0)
            throw new InvalidOperationException(
                "★Layer 0 の形が違います（" + string.Join(" / ", missing) + "）: " + dbPath
                + "。★この口は表を建てないので、1 行も消さずに止めます"
                + "（★本体 DB もこのあと消えません）。");

        long removed = 0;
        using var tx = conn.BeginTransaction();
        foreach (var table in tables)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = $"DELETE FROM \"{table}\" WHERE \"{SessionIdColumn}\"=$s";
            var p = cmd.CreateParameter();
            p.ParameterName = "$s";
            cmd.Parameters.Add(p);
            foreach (var sid in sessionIds)
            {
                p.Value = sid;
                removed += cmd.ExecuteNonQuery();
            }
        }
        tx.Commit();
        return removed;
    }

    private static List<string> Missing(SqliteConnection conn, IReadOnlyList<string> tables)
    {
        var bad = new List<string>();
        foreach (var table in tables)
        {
            var found = false;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$n";
                cmd.Parameters.AddWithValue("$n", table);
                found = Convert.ToInt64(cmd.ExecuteScalar()) > 0;
            }
            if (!found)
            {
                bad.Add("表 " + table + " が無い");
                continue;
            }
            using var info = conn.CreateCommand();
            info.CommandText = $"SELECT COUNT(*) FROM pragma_table_info($t) WHERE name=$c";
            info.Parameters.AddWithValue("$t", table);
            info.Parameters.AddWithValue("$c", SessionIdColumn);
            if (Convert.ToInt64(info.ExecuteScalar()) == 0)
                bad.Add(table + " に " + SessionIdColumn + " 列が無い");
        }
        return bad;
    }

    private static void ApplyPragmas(SqliteConnection conn)
    {
        Exec(conn, $"PRAGMA busy_timeout={Layer0Schema.BusyTimeoutMs};");
        using var read = conn.CreateCommand();
        read.CommandText = "PRAGMA journal_mode;";
        var mode = read.ExecuteScalar() as string ?? "";
        if (string.Equals(mode, "wal", StringComparison.OrdinalIgnoreCase))
            Exec(conn, $"PRAGMA synchronous={Layer0Schema.SynchronousWal};");
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
