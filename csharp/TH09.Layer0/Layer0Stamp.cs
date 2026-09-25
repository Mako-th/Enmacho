using Microsoft.Data.Sqlite;
using TH09.Layer0.Generated;

namespace TH09.Layer0;

public sealed record Layer0StampState(bool FileExists, long Bytes, bool Readable, bool IsLayer0,
                                      bool Stamped, IReadOnlyList<string> MissingTables,
                                      long StampRows, string? Unreadable);

public static class Layer0Stamp
{
    public const string Table = "writer_stamp";

    public const string StateCreated = "created";

    public const string StateOpened = "opened";

    public const string StateClosed = "closed";

    public const string StatePressed = "stamped";

    public static readonly string CreateSql =
        "CREATE TABLE " + Table + " ("
        + "seq INTEGER PRIMARY KEY AUTOINCREMENT,"
        + "state TEXT NOT NULL,"
        + "tool TEXT NOT NULL,"
        + "at TEXT NOT NULL)";

    public const string NotLayer0 =
        "★これは Layer 0 ではありません（Layer 0 の表がそろっていません）。"
        + "印を押すのはやめました（関係の無いファイルに表を足しません）。";

    public const string NoFile = "★印を押す相手がありません（ファイルがありません）: ";

    public const string Already = "★もう印があります（何もしていません）: ";


    public static Layer0StampState Inspect(string dbPath)
    {
        ArgumentNullException.ThrowIfNull(dbPath);
        if (!File.Exists(dbPath))
            return new Layer0StampState(false, 0L, false, false, false, Layer0Schema.Tables,
                                        0L, null);
        var bytes = new FileInfo(dbPath).Length;
        try
        {
            using var conn = OpenReadOnly(dbPath);
            var missing = Layer0Schema.Tables.Where(t => !HasTable(conn, t)).ToArray();
            var stamped = HasTable(conn, Table);
            var rows = stamped ? Count(conn, "SELECT COUNT(*) FROM " + Table) : 0L;
            return new Layer0StampState(true, bytes, true, missing.Length == 0, stamped,
                                        missing, rows, null);
        }
        catch (SqliteException exc)
        {
            return new Layer0StampState(true, bytes, false, false, false, Layer0Schema.Tables,
                                        0L, exc.Message);
        }
    }


    public static bool Press(string dbPath, string tool)
    {
        ArgumentNullException.ThrowIfNull(dbPath);
        ArgumentNullException.ThrowIfNull(tool);
        var state = Inspect(dbPath);
        if (!state.FileExists) throw new InvalidOperationException(NoFile + dbPath);
        if (!state.Readable || !state.IsLayer0)
            throw new InvalidOperationException(NotLayer0 + "（" + dbPath + "）");
        if (state.Stamped) return false;

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
        Exec(conn, "PRAGMA busy_timeout=" + Layer0Schema.BusyTimeoutMs + ";");
        Create(conn);
        Write(conn, StatePressed, tool);
        return true;
    }


    public static void Create(SqliteConnection conn)
    {
        ArgumentNullException.ThrowIfNull(conn);
        Exec(conn, CreateSql);
    }

    public static void Write(SqliteConnection conn, string state, string tool)
    {
        ArgumentNullException.ThrowIfNull(conn);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO " + Table + "(state,tool,at) VALUES($s,$t,$a)";
        cmd.Parameters.AddWithValue("$s", state);
        cmd.Parameters.AddWithValue("$t", tool);
        cmd.Parameters.AddWithValue("$a", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.ExecuteNonQuery();
    }

    public static bool HasTable(SqliteConnection conn, string name)
    {
        ArgumentNullException.ThrowIfNull(conn);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$n";
        cmd.Parameters.AddWithValue("$n", name);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }


    private static SqliteConnection OpenReadOnly(string path)
    {
        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            DefaultTimeout = Layer0Schema.BusyTimeoutMs / 1000,
        };
        var conn = new SqliteConnection(csb.ToString());
        conn.Open();
        Exec(conn, "PRAGMA query_only=1;");
        return conn;
    }

    private static long Count(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
