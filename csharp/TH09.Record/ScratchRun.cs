using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace TH09.Record;

public static class ScratchRun
{
    public const string StartedAt = "1970-01-01T00:00:00+00:00";

    public const string LoggerVersion = "record-parity";

    public const string SessionStatus = "completed";

    public const string CloseStatus = "aborted";

    public sealed record Line(string Label, long SessionId, long TotalTicks, long KeptTicks,
                              long Events, string Note);

    public static RecordDb Create(string path, string? layer0Path, Action<string>? warn = null)
    {
        if (File.Exists(path)) File.Delete(path);
        var db = Schema.CreateFrom(path);
        var floor = Repository.Layer0SessionFloor(layer0Path, warn);
        Repository.SeedSessionHighWater(db.Connection, floor);
        return db;
    }

    public static List<Line> ReplayLayer0(RecordDb db, SqliteConnection layer0,
                                          IEnumerable<long> sources, Action<string>? log = null)
    {
        var lines = new List<Line>();
        foreach (var src in sources)
        {
            using var tx = db.Connection.BeginTransaction();
            var sid = Repository.InsertSession(db.Connection, StartedAt, "running", LoggerVersion);
            var r = TickReplay.ReplayInto(db.Connection, sid, layer0, src, CloseStatus, log);
            CloseSession(db.Connection, sid);
            tx.Commit();
            lines.Add(new Line("L0:" + src.ToString(CultureInfo.InvariantCulture), sid,
                               r.TotalTicks, r.KeptTicks, r.Events,
                               $"seg={r.Segments} rv={string.Join("/", r.RecordVersions)}"));
        }
        return lines;
    }

    public static List<Line> ReplaySynthetic(RecordDb db, IReadOnlyList<SyntheticTicks.Case> cases,
                                             Action<string>? log = null)
    {
        var lines = new List<Line>();
        foreach (var c in cases)
        {
            using var tx = db.Connection.BeginTransaction();
            var sid = Repository.InsertSession(db.Connection, StartedAt, "running", LoggerVersion);
            var sm = new StateMachine(db.Connection, sid, log);
            Snapshot? last = null;
            long kept = 0;
            foreach (var s in SyntheticTicks.Snapshots(c))
            {
                kept++;
                last = s;
                sm.Process(s);
            }
            sm.Close(last, CloseStatus, wallFallback: TickReplay.EpochWall(0));
            CloseSession(db.Connection, sid);
            tx.Commit();
            lines.Add(new Line("SYN:" + c.Name, sid, c.Rows.Count, kept, sm.EventCount, c.Why));
        }
        return lines;
    }

    private static void CloseSession(SqliteConnection c, long sid)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE sessions SET ended_at=$0,status=$1 WHERE session_id=$2";
        cmd.Parameters.AddWithValue("$0", StartedAt);
        cmd.Parameters.AddWithValue("$1", SessionStatus);
        cmd.Parameters.AddWithValue("$2", sid);
        cmd.ExecuteNonQuery();
    }

    public static void WriteLines(IReadOnlyList<Line> lines, string path, string side)
    {
        var sb = new StringBuilder();
        sb.Append("# th09-record-run-v1 side=").Append(side).Append('\n');
        sb.Append("# label\tseq\ttotal_ticks\tkept_ticks\tevents\n");
        for (var i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            sb.Append(l.Label).Append('\t').Append(i.ToString(CultureInfo.InvariantCulture)).Append('\t')
              .Append(l.TotalTicks.ToString(CultureInfo.InvariantCulture)).Append('\t')
              .Append(l.KeptTicks.ToString(CultureInfo.InvariantCulture)).Append('\t')
              .Append(l.Events.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }
        sb.Append("# 合計 ").Append(lines.Count.ToString(CultureInfo.InvariantCulture)).Append(" セッション / ")
          .Append(lines.Sum(x => x.KeptTicks).ToString(CultureInfo.InvariantCulture)).Append(" tick / ")
          .Append(lines.Sum(x => x.Events).ToString(CultureInfo.InvariantCulture)).Append(" イベント\n");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    public static (int Written, int Lines) DumpTombstones(RecordDb db, string path, string deletedAt)
    {
        var sids = new List<long>();
        using (var cmd = db.Connection.CreateCommand())
        {
            cmd.CommandText = "SELECT session_id FROM sessions ORDER BY session_id";
            using var r = cmd.ExecuteReader();
            while (r.Read()) sids.Add(r.GetInt64(0));
        }
        var written = 0;
        if (!db.ReadOnly)
        {
            using var tx = db.Connection.BeginTransaction();
            foreach (var sid in sids)
                if (Repository.WriteTombstone(db.Connection, sid, deletedAt)) written++;
            tx.Commit();
        }

        var sb = new StringBuilder();
        sb.Append("# th09-record-tombstone-v1\n");
        var n = 0;
        for (var i = 0; i < sids.Count; i++)
        {
            var text = db.ReadOnly
                ? Repository.TombstonePayload(db.Connection, sids[i])
                : Repository.TombstoneJson(db.Connection, sids[i]);
            if (text is null) continue;
            var canon = ParityValue.CanonJsonToken(text);
            sb.Append(i.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(canon).Append('\n');
            n++;
        }
        sb.Append("# 控え ").Append(n.ToString(CultureInfo.InvariantCulture)).Append(" 件（母数: sessions ")
          .Append(sids.Count.ToString(CultureInfo.InvariantCulture)).Append(" 行）\n");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        return (written, n);
    }
}
