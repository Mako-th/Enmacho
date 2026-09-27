using Microsoft.Data.Sqlite;
using TH09.Layer0.Generated;

namespace TH09.Layer0;

public static class Layer0Restamp
{
    private static readonly (string Table, string KeyA, string KeyB)[] BlobTables =
    [
        ("session_ticks", "session_id", "segment_no"),
        ("session_hit_windows", "session_id", "window_no"),
    ];

    public sealed record TableStatus(string Table, long V1, long V2, long Other)
    {
        public long Total => V1 + V2 + Other;
    }

    public sealed record Status(IReadOnlyList<TableStatus> Tables)
    {
        public long TotalV1 => Tables.Sum(t => t.V1);

        public long TotalV2 => Tables.Sum(t => t.V2);

        public long EstimatedGrowthBytes =>
            TotalV1 * (SolidBrotli.HeaderBytesV2 - SolidBrotli.HeaderBytes);
    }

    public static Status Inspect(string dbPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(dbPath);
        using var conn = OpenReadOnly(dbPath);
        var list = new List<TableStatus>();
        foreach (var (table, _, _) in BlobTables)
        {
            long v1 = CountWhereEncoding(conn, null, table, SolidBrotli.Encoding);
            long v2 = CountWhereEncoding(conn, null, table, SolidBrotli.EncodingV2);
            long total = ScalarLong(conn, null, $"SELECT COUNT(*) FROM \"{table}\"");
            list.Add(new TableStatus(table, v1, v2, total - v1 - v2));
        }
        return new Status(list);
    }

    public sealed record Options(string DbPath, int BatchRows = 2000);

    public sealed record TableResult(string Table, long Converted, long AlreadyV2);

    public sealed record Result(IReadOnlyList<TableResult> Tables)
    {
        public long TotalConverted => Tables.Sum(t => t.Converted);
    }

    public static Result Run(Options o, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(o);
        ArgumentNullException.ThrowIfNull(log);
        using var conn = OpenReadWrite(o.DbPath);
        var results = new List<TableResult>();
        foreach (var (table, keyA, keyB) in BlobTables)
            results.Add(RestampTable(conn, o, table, keyA, keyB, log));
        return new Result(results);
    }

    public static (long V1Remaining, long V2Checked, long V2Failed) VerifyAll(string dbPath, TextWriter log)
    {
        ArgumentException.ThrowIfNullOrEmpty(dbPath);
        ArgumentNullException.ThrowIfNull(log);
        using var conn = OpenReadOnly(dbPath);
        long v1Remaining = 0, checkedRows = 0, failed = 0;
        foreach (var (table, keyA, keyB) in BlobTables)
        {
            v1Remaining += CountWhereEncoding(conn, null, table, SolidBrotli.Encoding);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT \"{keyA}\",\"{keyB}\",\"blob\" FROM \"{table}\""
                              + " WHERE \"encoding\"=$e";
            cmd.Parameters.AddWithValue("$e", SolidBrotli.EncodingV2);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                checkedRows++;
                try
                {
                    SolidBrotli.UnpackV2((byte[])r.GetValue(2));
                }
                catch (Exception ex)
                {
                    failed++;
                    log.WriteLine($"  ★NG {table} {r.GetInt64(0)}/{r.GetInt64(1)}: "
                                  + $"{ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        return (v1Remaining, checkedRows, failed);
    }


    private static TableResult RestampTable(SqliteConnection conn, Options o,
                                            string table, string keyA, string keyB, TextWriter log)
    {
        long alreadyV2 = CountWhereEncoding(conn, null, table, SolidBrotli.EncodingV2);
        var todo = new List<(long A, long B)>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SELECT \"{keyA}\",\"{keyB}\" FROM \"{table}\""
                              + " WHERE \"encoding\"=$e ORDER BY \"" + keyA + "\",\"" + keyB + "\"";
            cmd.Parameters.AddWithValue("$e", SolidBrotli.Encoding);
            using var r = cmd.ExecuteReader();
            while (r.Read()) todo.Add((r.GetInt64(0), r.GetInt64(1)));
        }
        log.WriteLine($"{table}: v1 {todo.Count:N0} 行 / v2 済み {alreadyV2:N0} 行");
        if (todo.Count == 0) return new TableResult(table, 0, alreadyV2);

        long converted = 0;
        for (int start = 0; start < todo.Count; start += o.BatchRows)
        {
            var batch = todo.Skip(start).Take(o.BatchRows).ToList();
            using (var tx = conn.BeginTransaction())
            {
                foreach (var (a, b) in batch) RestampOne(conn, tx, table, keyA, keyB, a, b);
                tx.Commit();
            }
            converted += batch.Count;
            log.WriteLine($"  … {converted:N0} / {todo.Count:N0} 行");
        }
        return new TableResult(table, converted, alreadyV2);
    }

    private static void RestampOne(SqliteConnection conn, SqliteTransaction tx,
                                   string table, string keyA, string keyB, long a, long b)
    {
        byte[] oldBlob;
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = $"SELECT \"blob\",\"encoding\" FROM \"{table}\""
                              + $" WHERE \"{keyA}\"=$a AND \"{keyB}\"=$b";
            cmd.Parameters.AddWithValue("$a", a);
            cmd.Parameters.AddWithValue("$b", b);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) throw new InvalidDataException($"{table} {a}/{b}: 行が消えました");
            string enc = r.GetString(1);
            if (enc != SolidBrotli.Encoding)
                throw new InvalidDataException(
                    $"{table} {a}/{b}: encoding が v1 ではありません（{enc}）"
                    + "——選んだ後に別の書き手が触った可能性があります");
            oldBlob = (byte[])r.GetValue(0);
        }

        var before = SolidBrotli.Unpack(oldBlob);
        var newBlob = SolidBrotli.UpgradeToV2(oldBlob);

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = $"UPDATE \"{table}\" SET \"blob\"=$blob,\"encoding\"=$enc,"
                              + "\"compressed_bytes\"=$len"
                              + $" WHERE \"{keyA}\"=$a AND \"{keyB}\"=$b";
            cmd.Parameters.AddWithValue("$blob", newBlob);
            cmd.Parameters.AddWithValue("$enc", SolidBrotli.EncodingV2);
            cmd.Parameters.AddWithValue("$len", (long)newBlob.Length);
            cmd.Parameters.AddWithValue("$a", a);
            cmd.Parameters.AddWithValue("$b", b);
            int n = cmd.ExecuteNonQuery();
            if (n != 1)
                throw new InvalidDataException($"{table} {a}/{b}: 更新できた行が {n} 本です（期待は 1 本）");
        }

        byte[] rereadBlob;
        string rereadEnc;
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = $"SELECT \"blob\",\"encoding\" FROM \"{table}\""
                              + $" WHERE \"{keyA}\"=$a AND \"{keyB}\"=$b";
            cmd.Parameters.AddWithValue("$a", a);
            cmd.Parameters.AddWithValue("$b", b);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) throw new InvalidDataException($"{table} {a}/{b}: 書いた直後に行が消えました");
            rereadEnc = r.GetString(1);
            rereadBlob = (byte[])r.GetValue(0);
        }
        if (rereadEnc != SolidBrotli.EncodingV2)
            throw new InvalidDataException(
                $"{table} {a}/{b}: 書いた直後の encoding が v2 ではありません（{rereadEnc}）");
        var after = SolidBrotli.UnpackV2(rereadBlob);
        SolidBrotli.RequireSameChunks(before, after);
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

    private static SqliteConnection OpenReadWrite(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException("★書き換える相手がありません（ファイルがありません）: " + path);
        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            DefaultTimeout = Layer0Schema.BusyTimeoutMs / 1000,
        };
        var conn = new SqliteConnection(csb.ToString());
        conn.Open();
        ApplyPragmas(conn);
        return conn;
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


    private static long CountWhereEncoding(SqliteConnection conn, SqliteTransaction? tx,
                                           string table, string encoding)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT COUNT(*) FROM \"{table}\" WHERE \"encoding\"=$e";
        cmd.Parameters.AddWithValue("$e", encoding);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static long ScalarLong(SqliteConnection conn, SqliteTransaction? tx, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        var v = cmd.ExecuteScalar();
        return v is null or DBNull ? 0 : Convert.ToInt64(v);
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
