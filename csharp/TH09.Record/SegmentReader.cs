using Microsoft.Data.Sqlite;

namespace TH09.Record;

internal static class SegmentReader
{
    public sealed record Row(long SegmentNo, int RecordVersion, string FieldOrderText, string Encoding, byte[] Blob);

    public static List<Row> SegmentsOf(SqliteConnection layer0, long sessionId)
    {
        var list = new List<Row>();
        using var cmd = layer0.CreateCommand();
        cmd.CommandText = "SELECT segment_no,record_version,field_order,encoding,blob"
                         + " FROM session_ticks WHERE session_id=$0 ORDER BY segment_no";
        cmd.Parameters.AddWithValue("$0", sessionId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Row(r.GetInt64(0), r.GetInt32(1), r.GetString(2), r.GetString(3), (byte[])r.GetValue(4)));
        return list;
    }
}
