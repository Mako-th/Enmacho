using Microsoft.Data.Sqlite;
using TH09.Layer0.Generated;

namespace TH09.Layer0;

public sealed class Layer0Reader : IDisposable
{
    public const int BusyTimeoutMs = Layer0Schema.BusyTimeoutMs;

    private readonly SqliteConnection _conn;

    public Layer0Reader(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Layer 0 の DB が見つかりません: " + path, path);
        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            DefaultTimeout = BusyTimeoutMs / 1000,
        };
        _conn = new SqliteConnection(csb.ToString());
        _conn.Open();
        using var pragma = _conn.CreateCommand();
        pragma.CommandText = "PRAGMA query_only=1;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _conn.Dispose();

    public sealed record Segment(long SessionId, long SegmentNo, int RecordVersion,
                                 int TickCount, string FieldOrderText, string Encoding, byte[] Blob);

    public sealed record Window(long SessionId, long WindowNo, int TickCount, int SlotCount,
                                string Quant, byte[] FieldOrderBlob, string FieldOrderEncoding,
                                string Encoding, byte[] Blob);

    public Segment? ReadSegment(long sessionId, long segmentNo)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            "SELECT record_version,tick_count,field_order,encoding,blob FROM session_ticks"
            + " WHERE session_id=$s AND segment_no=$g";
        cmd.Parameters.AddWithValue("$s", sessionId);
        cmd.Parameters.AddWithValue("$g", segmentNo);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new Segment(sessionId, segmentNo, r.GetInt32(0), r.GetInt32(1),
                           r.GetString(2), r.GetString(3), (byte[])r.GetValue(4));
    }

    public Window? ReadWindow(long sessionId, long windowNo)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            "SELECT tick_count,slot_count,quant,field_order,field_order_encoding,encoding,blob"
            + " FROM session_hit_windows WHERE session_id=$s AND window_no=$w";
        cmd.Parameters.AddWithValue("$s", sessionId);
        cmd.Parameters.AddWithValue("$w", windowNo);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new Window(sessionId, windowNo, r.GetInt32(0), r.GetInt32(1), r.GetString(2),
                          (byte[])r.GetValue(3), r.GetString(4), r.GetString(5), (byte[])r.GetValue(6));
    }
}
