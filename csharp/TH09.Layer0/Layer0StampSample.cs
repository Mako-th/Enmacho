using Microsoft.Data.Sqlite;
using TH09.Layer0.Generated;

namespace TH09.Layer0;

public static class Layer0StampSample
{
    public static void BuildUnstamped(Layer0Writer.Options options,
                                      IReadOnlyList<(long Session, int Ticks)> sessions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(sessions);
        var words = new uint[options.Fields.Max(f => f.WordIndex) + 1];
        using (var writer = Layer0Writer.Open(options))
        {
            foreach (var (session, ticks) in sessions)
            {
                writer.BeginSession(session);
                for (var t = 0; t < ticks; t++)
                {
                    for (var i = 0; i < words.Length; i++)
                        words[i] = (uint)(i * 7 + t * 13 + session);
                    writer.Add([(uint[])words.Clone()]);
                }
                writer.EndSession();
            }
        }
        using var conn = OpenReadWrite(options.DbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DROP TABLE IF EXISTS " + Layer0Stamp.Table;
        cmd.ExecuteNonQuery();
    }

    public static List<string> SegmentDigest(string dbPath)
    {
        ArgumentNullException.ThrowIfNull(dbPath);
        var rows = new List<string>();
        if (!File.Exists(dbPath)) return rows;
        try
        {
            using var conn = OpenReadOnly(dbPath);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT session_id,segment_no,tick_count,encoding,"
                              + "length(blob),hex(substr(blob,1,32)) FROM session_ticks "
                              + "ORDER BY session_id,segment_no";
            using var read = cmd.ExecuteReader();
            while (read.Read())
            {
                var cells = new string[read.FieldCount];
                for (var i = 0; i < read.FieldCount; i++)
                    cells[i] = read.GetValue(i).ToString() ?? "";
                rows.Add(string.Join("|", cells));
            }
        }
        catch (SqliteException)
        {
        }
        return rows;
    }


    private static SqliteConnection OpenReadWrite(string path)
    {
        var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            DefaultTimeout = Layer0Schema.BusyTimeoutMs / 1000,
        }.ToString());
        conn.Open();
        return conn;
    }

    private static SqliteConnection OpenReadOnly(string path)
    {
        var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            DefaultTimeout = Layer0Schema.BusyTimeoutMs / 1000,
        }.ToString());
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA query_only=1;";
        cmd.ExecuteNonQuery();
        return conn;
    }
}
