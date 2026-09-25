using Microsoft.Data.Sqlite;

namespace TH09.Record;

public sealed class RecordDb : IDisposable
{
    public const int BusyTimeoutMs = 5000;

    public const string SynchronousWal = "NORMAL";

    private readonly SqliteConnection _conn;

    public SqliteConnection Connection => _conn;

    public bool ReadOnly { get; }

    private RecordDb(SqliteConnection conn, bool readOnly)
    {
        _conn = conn;
        ReadOnly = readOnly;
    }

    public static RecordDb? OpenReadOnly(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var cs = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
                DefaultTimeout = BusyTimeoutMs / 1000,
            }.ToString();
            var conn = new SqliteConnection(cs);
            conn.Open();
            Exec(conn, "PRAGMA query_only=1");
            Exec(conn, $"PRAGMA busy_timeout={BusyTimeoutMs}");
            return new RecordDb(conn, readOnly: true);
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static RecordDb OpenReadWrite(string path)
    {
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            DefaultTimeout = BusyTimeoutMs / 1000,
        }.ToString();
        var conn = new SqliteConnection(cs);
        conn.Open();
        Exec(conn, "PRAGMA journal_mode=WAL");
        Exec(conn, $"PRAGMA busy_timeout={BusyTimeoutMs}");
        if (string.Equals(JournalMode(conn), "wal", StringComparison.OrdinalIgnoreCase))
            Exec(conn, $"PRAGMA synchronous={SynchronousWal}");
        Exec(conn, "PRAGMA foreign_keys=ON");
        return new RecordDb(conn, readOnly: false);
    }

    public static string? JournalMode(SqliteConnection conn)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode";
            return cmd.ExecuteScalar()?.ToString()?.ToLowerInvariant();
        }
        catch (SqliteException)
        {
            return null;
        }
    }

    private static long WalBytes(string walPath) => File.Exists(walPath) ? new FileInfo(walPath).Length : 0L;

    private static string? ProbeJournalMode(string path)
    {
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
            DefaultTimeout = BusyTimeoutMs / 1000,
        }.ToString();
        using var conn = new SqliteConnection(cs);
        conn.Open();
        Exec(conn, $"PRAGMA busy_timeout={BusyTimeoutMs}");
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode";
        return cmd.ExecuteScalar()?.ToString()?.ToLowerInvariant();
    }

    public static WalCheckpointResult Checkpoint(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path)) return WalCheckpointResult.Skip("無い");

        string? mode;
        try
        {
            mode = ProbeJournalMode(path);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 26)
        {
            return WalCheckpointResult.Skip("sqlite の schema ではありません（" + ex.Message + "）");
        }
        catch (Exception ex)
        {
            return WalCheckpointResult.Failed("journal_mode を読めません: " + ex.Message, 0);
        }
        if (mode is null) return WalCheckpointResult.Skip("sqlite の schema を読めません（sqlite ではない可能性）");
        if (!string.Equals(mode, "wal", StringComparison.OrdinalIgnoreCase))
            return WalCheckpointResult.Skip("journal_mode が WAL ではありません（" + mode + "）");

        var walPath = path + "-wal";
        var before = WalBytes(walPath);
        try
        {
            using var db = OpenReadWrite(path);
            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
            using var r = cmd.ExecuteReader();
            long busy = 0, log = -1, checkpointed = -1;
            if (r.Read())
            {
                busy = r.IsDBNull(0) ? 0 : r.GetInt64(0);
                log = r.IsDBNull(1) ? -1 : r.GetInt64(1);
                checkpointed = r.IsDBNull(2) ? -1 : r.GetInt64(2);
            }
            return new WalCheckpointResult(true, busy != 0, before, WalBytes(walPath), log, checkpointed,
                                           null, null);
        }
        catch (Exception ex)
        {
            return WalCheckpointResult.Failed(ex.Message, before);
        }
    }

    private static readonly byte[] SqliteMagic = "SQLite format 3\0"u8.ToArray();

    public static bool IsSqliteFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                                          FileShare.ReadWrite | FileShare.Delete);
            Span<byte> head = stackalloc byte[SqliteMagic.Length];
            return fs.ReadAtLeast(head, head.Length, throwOnEndOfStream: false) == head.Length
                && head.SequenceEqual(SqliteMagic);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static void BackupTo(string source, string destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        if (File.Exists(destination))
            throw new IOException("写す先が既にあります: " + destination);
        var src = new SqliteConnectionStringBuilder
        {
            DataSource = source,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
            DefaultTimeout = BusyTimeoutMs / 1000,
        }.ToString();
        var dst = new SqliteConnectionStringBuilder
        {
            DataSource = destination,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            DefaultTimeout = BusyTimeoutMs / 1000,
        }.ToString();
        using var a = new SqliteConnection(src);
        a.Open();
        Exec(a, $"PRAGMA busy_timeout={BusyTimeoutMs}");
        using var b = new SqliteConnection(dst);
        b.Open();
        a.BackupDatabase(b);
    }

    public static long DatabaseBytes(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var onDisk = new FileInfo(path).Length;
        if (!IsSqliteFile(path)) return onDisk;
        try
        {
            var cs = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
                DefaultTimeout = BusyTimeoutMs / 1000,
            }.ToString();
            using var conn = new SqliteConnection(cs);
            conn.Open();
            Exec(conn, $"PRAGMA busy_timeout={BusyTimeoutMs}");
            return Scalar(conn, "PRAGMA page_count") * Scalar(conn, "PRAGMA page_size");
        }
        catch (Exception)
        {
            return onDisk;
        }
    }

    private static long Scalar(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Exec(string sql) => Exec(_conn, sql);

    public string FileName()
    {
        try
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "PRAGMA database_list";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                if (r.GetString(1) != "main") continue;
                var file = r.IsDBNull(2) ? "" : r.GetString(2);
                return string.IsNullOrEmpty(file) ? "(memory)" : Path.GetFileName(file);
            }
        }
        catch (SqliteException)
        {
        }
        return "(unknown)";
    }

    public long CountRows(string table)
    {
        try
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
            return Convert.ToInt64(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (SqliteException)
        {
            return -1;
        }
    }

    public Dictionary<string, List<string>> TableColumns()
    {
        var outMap = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var table in TableNames())
        {
            var cols = new List<string>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
            using var r = cmd.ExecuteReader();
            while (r.Read()) cols.Add(r.GetString(1));
            outMap[table] = cols;
        }
        return outMap;
    }

    public List<string> TableNames()
    {
        var list = new List<string>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' "
                        + "AND name NOT LIKE 'sqlite_%' ORDER BY name";
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    public void Dispose() => _conn.Dispose();
}

public readonly record struct WalCheckpointResult(
    bool Ok, bool Busy, long BeforeBytes, long AfterBytes,
    long LogFrames, long CheckpointedFrames, string? SkipReason, string? Error)
{
    public static WalCheckpointResult Skip(string reason) => new(true, false, 0, 0, -1, -1, reason, null);

    public static WalCheckpointResult Failed(string reason, long before) =>
        new(false, true, before, before, -1, -1, null, reason);

    public string Describe(string label)
    {
        if (SkipReason is { } why) return "[wal] " + label + ": 何もしていません（" + why + "）";
        var n = System.Globalization.CultureInfo.InvariantCulture;
        if (!Ok)
            return "★注意: " + label + " の WAL を吐き出せませんでした（" + Error + "）。"
                 + "控えは backup API で写すので中身は欠けませんが、-wal は畳めていません。";
        var note = "[wal] " + label + ": " + BeforeBytes.ToString(n) + " B → " + AfterBytes.ToString(n)
                 + " B（frame " + CheckpointedFrames.ToString(n) + "/" + LogFrames.ToString(n) + "）";
        return Busy
            ? "★注意: " + label + " の WAL を完全には切り詰められませんでした（busy）。"
              + "控えは backup API で写すので中身は欠けませんが、-wal は畳めていません。 " + note
            : note;
    }
}
