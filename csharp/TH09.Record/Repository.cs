using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using TH09.Record.Generated;

using ReplayPathCols = TH09.Generated.DbColumns.ReplayPaths;

namespace TH09.Record;

public static class Repository
{

    public static List<string> CurrentReplayFiles(SqliteConnection c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var found = new List<string>();
        try
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {ReplayPathCols.FullPath} FROM {ReplayPathCols.Table}"
                            + $" WHERE {ReplayPathCols.IsCurrent}=1"
                            + $" ORDER BY {ReplayPathCols.FullPath}";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                if (r.IsDBNull(0)) continue;
                var path = r.GetString(0);
                if (File.Exists(path)) found.Add(path);
            }
        }
        catch (SqliteException)
        {
            return found;
        }
        return found;
    }


    public static string? MetaGet(SqliteConnection c, string key)
    {
        try
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT value FROM db_meta WHERE key=$0";
            cmd.Parameters.AddWithValue("$0", key);
            var v = cmd.ExecuteScalar();
            return v is null || v is DBNull ? null : Convert.ToString(v, CultureInfo.InvariantCulture);
        }
        catch (SqliteException)
        {
            return null;
        }
    }

    public static bool MetaSet(SqliteConnection c, string key, string value)
    {
        try
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO db_meta(key,value) VALUES($0,$1)";
            cmd.Parameters.AddWithValue("$0", key);
            cmd.Parameters.AddWithValue("$1", value);
            cmd.ExecuteNonQuery();
            return true;
        }
        catch (SqliteException)
        {
            return false;
        }
    }


    public static long Layer0SessionFloor(string? layer0Path, Action<string>? warn = null)
    {
        if (layer0Path is null) return 0;
        if (!File.Exists(layer0Path)) return 0;
        try
        {
            var cs = new SqliteConnectionStringBuilder
            {
                DataSource = layer0Path,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
                DefaultTimeout = RecordDb.BusyTimeoutMs / 1000,
            }.ToString();
            using var conn = new SqliteConnection(cs);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT MAX(session_id) FROM session_ticks";
            var v = cmd.ExecuteScalar();
            return v is null or DBNull ? 0 : Convert.ToInt64(v, CultureInfo.InvariantCulture);
        }
        catch (Exception e)
        {
            warn?.Invoke($"[DB] 警告: Layer 0 ({layer0Path}) の最大 session_id を読めませんでした"
                         + $"（{e.GetType().Name}: {e.Message}）。"
                         + "本体DBだけ古い状態へ戻していた場合、session_id が再利用されるおそれがあります");
            return 0;
        }
    }

    public static bool SeedSessionHighWater(SqliteConnection c, long floor = 0)
    {
        long top;
        try
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT MAX(session_id) FROM sessions";
            var v = cmd.ExecuteScalar();
            top = v is null or DBNull ? 0 : Convert.ToInt64(v, CultureInfo.InvariantCulture);
        }
        catch (SqliteException)
        {
            return false;
        }
        top = Math.Max(top, floor);
        var cur = MetaGet(c, RecordLabels.SessionIdHighWater);
        var mark = cur is not null && long.TryParse(cur, NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) ? m : 0;
        if (cur is not null && mark >= top) return false;
        return MetaSet(c, RecordLabels.SessionIdHighWater,
                       Math.Max(top, mark).ToString(CultureInfo.InvariantCulture));
    }

    public static long NextSessionId(SqliteConnection c)
    {
        long top;
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT MAX(session_id) FROM sessions";
            var v = cmd.ExecuteScalar();
            top = v is null or DBNull ? 0 : Convert.ToInt64(v, CultureInfo.InvariantCulture);
        }
        var cur = MetaGet(c, RecordLabels.SessionIdHighWater);
        var mark = cur is not null && long.TryParse(cur, NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) ? m : 0;
        var sid = Math.Max(top, mark) + 1;
        MetaSet(c, RecordLabels.SessionIdHighWater, sid.ToString(CultureInfo.InvariantCulture));
        return sid;
    }

    public static long InsertSession(SqliteConnection c, string startedAt,
                                     string status = "running", string? loggerVersion = null)
    {
        var sid = NextSessionId(c);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO sessions(session_id,started_at,status,logger_version)"
                        + " VALUES($0,$1,$2,$3)";
        cmd.Parameters.AddWithValue("$0", sid);
        cmd.Parameters.AddWithValue("$1", startedAt);
        cmd.Parameters.AddWithValue("$2", status);
        cmd.Parameters.AddWithValue("$3", (object?)loggerVersion ?? DBNull.Value);
        cmd.ExecuteNonQuery();
        return sid;
    }


    private static bool TableExists(SqliteConnection c, string name)
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

    public static string TombstonePayload(SqliteConnection c, long sessionId)
    {
        var sb = new StringBuilder("{");
        sb.Append(Payload.JsonString("format")).Append(':').Append(RecordLabels.TombstoneFormat);
        sb.Append(',').Append(Payload.JsonString("tables")).Append(":{");
        var firstTable = true;
        foreach (var table in RecordLabels.TombstoneTables)
        {
            if (!TableExists(c, table)) continue;
            if (!firstTable) sb.Append(',');
            firstTable = false;
            sb.Append(Payload.JsonString(table)).Append(":[");
            AppendRows(c, sb, $"SELECT * FROM \"{table}\" WHERE session_id=$0", sessionId);
            sb.Append(']');
        }
        sb.Append("},").Append(Payload.JsonString("refs")).Append(":{");
        var firstRef = true;
        foreach (var (table, col) in RecordLabels.SessionRefNullable)
        {
            if (!TableExists(c, table)) continue;
            if (!firstRef) sb.Append(',');
            firstRef = false;
            sb.Append(Payload.JsonString(table)).Append(":[");
            using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT rowid FROM \"{table}\" WHERE \"{col}\"=$0";
            cmd.Parameters.AddWithValue("$0", sessionId);
            using var r = cmd.ExecuteReader();
            var first = true;
            while (r.Read())
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(r.GetInt64(0).ToString(CultureInfo.InvariantCulture));
            }
            sb.Append(']');
        }
        return sb.Append("}}").ToString();
    }

    private static void AppendRows(SqliteConnection c, StringBuilder sb, string sql, long sessionId)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$0", sessionId);
        using var r = cmd.ExecuteReader();
        var names = new string[r.FieldCount];
        for (var i = 0; i < r.FieldCount; i++) names[i] = r.GetName(i);
        var firstRow = true;
        while (r.Read())
        {
            if (!firstRow) sb.Append(',');
            firstRow = false;
            sb.Append('{');
            for (var i = 0; i < r.FieldCount; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Payload.JsonString(names[i])).Append(':');
                sb.Append(JsonOfSqliteValue(r, i));
            }
            sb.Append('}');
        }
    }

    private static string JsonOfSqliteValue(SqliteDataReader r, int i)
    {
        if (r.IsDBNull(i)) return "null";
        return r.GetFieldType(i) switch
        {
            var t when t == typeof(long) => r.GetInt64(i).ToString(CultureInfo.InvariantCulture),
            var t when t == typeof(double) => Payload.FloatText(r.GetDouble(i)),
            var t when t == typeof(byte[]) =>
                throw new InvalidDataException(
                    $"控えに BLOB 列（{r.GetName(i)}）が出てきた。Python 側の json.dumps も落ちるので、"
                    + "扱いを決めてから写すこと"),
            _ => Payload.JsonString(r.GetString(i)),
        };
    }

    public static bool WriteTombstone(SqliteConnection c, long sessionId, string deletedAt)
    {
        long n;
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM sessions WHERE session_id=$0";
            cmd.Parameters.AddWithValue("$0", sessionId);
            n = Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
        if (n == 0) return false;
        var payload = TombstonePayload(c, sessionId);
        using var ins = c.CreateCommand();
        ins.CommandText = "INSERT OR REPLACE INTO deleted_sessions(session_id,deleted_at,payload_json)"
                        + " VALUES($0,$1,$2)";
        ins.Parameters.AddWithValue("$0", sessionId);
        ins.Parameters.AddWithValue("$1", deletedAt);
        ins.Parameters.AddWithValue("$2", payload);
        ins.ExecuteNonQuery();
        return true;
    }

    public static List<long> TombstoneIds(SqliteConnection c)
    {
        var ids = new List<long>();
        try
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT session_id FROM deleted_sessions";
            using var r = cmd.ExecuteReader();
            while (r.Read()) ids.Add(r.GetInt64(0));
        }
        catch (SqliteException)
        {
            return [];
        }
        ids.Sort();
        return ids;
    }

    public static string? TombstoneJson(SqliteConnection c, long sessionId)
    {
        try
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT payload_json FROM deleted_sessions WHERE session_id=$0";
            cmd.Parameters.AddWithValue("$0", sessionId);
            var v = cmd.ExecuteScalar();
            return v is null or DBNull ? null : Convert.ToString(v, CultureInfo.InvariantCulture);
        }
        catch (SqliteException)
        {
            return null;
        }
    }


    public readonly record struct SegKey(long? Mode, long? Diff, long? Ch, long? Stage, long? Opp)
    {
        public static SegKey Of(long? mode, long? diff, long? ch, long? stage, long? opp) =>
            new(mode, diff, ch, stage,
                stage is not null && stage <= RecordLabels.OpponentKeyedMaxStage ? opp : null);

        public string Text() => string.Join("/", N(Mode), N(Diff), N(Ch), N(Stage), N(Opp));

        private static string N(long? v) => v is null ? "~" : v.Value.ToString(CultureInfo.InvariantCulture);
    }

    public sealed record Best(long Value, SortedSet<long> Holders, long Sid, long? Opp);

    public sealed class SegmentBest
    {
        public Best? Score { get; set; }
        public Best? Time { get; set; }
    }

    public static SortedDictionary<string, SegmentBest> SegmentBests(
        SqliteConnection c, long? excludeSid = null, bool ownOnly = true)
    {
        var data = new SortedDictionary<string, SegmentBest>(StringComparer.Ordinal);
        HashSet<long> skip = ownOnly ? ForeignSessionIds(c) : [];
        var times = StageTimeFrames(c);

        using var cmd = c.CreateCommand();
        var q = "SELECT st.session_id sid,st.stage_record_id srid,st.stage_number stage,"
              + "st.score_at_start ss,st.score_at_end se,st.opponent_character opp,"
              + "m.game_mode mode,m.p1_character ch,m.difficulty diff "
              + "FROM stages st JOIN session_metadata m USING(session_id) "
              + "WHERE m.game_mode IN (0,1) AND st.stage_number IS NOT NULL";
        if (excludeSid is not null)
        {
            q += " AND st.session_id<>$0";
            cmd.Parameters.AddWithValue("$0", excludeSid.Value);
        }
        cmd.CommandText = q;
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var sid = r.GetInt64(0);
            if (skip.Contains(sid)) continue;
            var srid = r.GetInt64(1);
            long? stage = r.IsDBNull(2) ? null : r.GetInt64(2);
            long? ss = r.IsDBNull(3) ? null : r.GetInt64(3);
            long? se = r.IsDBNull(4) ? null : r.GetInt64(4);
            long? opp = r.IsDBNull(5) ? null : r.GetInt64(5);
            long? mode = r.IsDBNull(6) ? null : r.GetInt64(6);
            long? ch = r.IsDBNull(7) ? null : r.GetInt64(7);
            long? diff = r.IsDBNull(8) ? null : r.GetInt64(8);

            var key = SegKey.Of(mode, diff, ch, stage, opp).Text();
            if (!data.TryGetValue(key, out var d)) data[key] = d = new SegmentBest();

            if (ss is not null && se is not null && se - ss > 0)
                d.Score = Update(d.Score, se.Value - ss.Value, sid, opp, better: (a, b) => a > b);
            if (times.TryGetValue(srid, out var t) && t >= RecordLabels.MinStageTimeFrames)
                d.Time = Update(d.Time, t, sid, opp, better: (a, b) => a < b);
        }
        return data;
    }

    private static Best Update(Best? cur, long val, long sid, long? opp, Func<long, long, bool> better)
    {
        if (cur is null) return new Best(val, [sid], sid, opp);
        if (val == cur.Value) { cur.Holders.Add(sid); return cur; }
        return better(val, cur.Value) ? new Best(val, [sid], sid, opp) : cur;
    }

    internal static Dictionary<long, long> StageTimeFrames(SqliteConnection c)
    {
        var outMap = new Dictionary<long, long>();
        using var cmd = c.CreateCommand();
        cmd.CommandText =
            "SELECT stage_record_id, COUNT(*) n, SUM(duration_frames) d,"
            + " SUM(CASE WHEN duration_frames IS NULL THEN 1 ELSE 0 END) z"
            + " FROM rounds WHERE stage_record_id IS NOT NULL GROUP BY stage_record_id";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var n = r.GetInt64(1);
            var z = r.GetInt64(3);
            if (n == 0 || z != 0 || r.IsDBNull(2)) continue;
            outMap[r.GetInt64(0)] = r.GetInt64(2);
        }
        return outMap;
    }

    public static HashSet<long> ForeignSessionIds(SqliteConnection c)
    {
        if (!HasReplayCols(c)) return [];
        var names = SessionSide.OwnReplayNames(c).Names;
        var ids = new HashSet<long>();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT s.session_id,m.execution_type,sr.replay_id,r.p1_name,r.p2_name,r.owner_side,r.own_override
              FROM sessions s
              JOIN session_metadata m USING(session_id)
              LEFT JOIN session_replays sr USING(session_id)
              LEFT JOIN replays r ON r.replay_id=sr.replay_id
            """;
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
        {
            var sid = rd.GetInt64(0);
            var execType = rd.IsDBNull(1) ? null : rd.GetString(1);
            long? replayId = rd.IsDBNull(2) ? null : rd.GetInt64(2);
            var p1Name = rd.IsDBNull(3) ? null : rd.GetString(3);
            var p2Name = rd.IsDBNull(4) ? null : rd.GetString(4);
            long? ownerSide = rd.IsDBNull(5) ? null : rd.GetInt64(5);
            long? ownOverride = rd.IsDBNull(6) ? null : rd.GetInt64(6);
            var (_, own, _) = SessionSide.Of(ownOverride, ownerSide, p1Name, p2Name, names,
                                             replayId, null, null, null, execType);
            if (!own) ids.Add(sid);
        }
        return ids;
    }

    private static bool HasReplayCols(SqliteConnection c)
    {
        try
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "PRAGMA table_info(replays)";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                if (string.Equals(r.GetString(1), "decode_status", StringComparison.Ordinal)) return true;
        }
        catch (SqliteException)
        {
            return false;
        }
        return false;
    }

    public static (int Keys, int Values) WriteSegmentBests(
        SortedDictionary<string, SegmentBest> data, string path)
    {
        var sb = new StringBuilder();
        var values = 0;
        foreach (var (key, d) in data)
        {
            foreach (var (metric, best) in new[] { ("score", d.Score), ("time", d.Time) })
            {
                if (best is null)
                {
                    sb.Append(key).Append('\t').Append(metric).Append("\t~\n");
                    continue;
                }
                values++;
                sb.Append(key).Append('\t').Append(metric).Append('\t')
                  .Append(best.Value.ToString(CultureInfo.InvariantCulture)).Append('\t')
                  .Append(best.Sid.ToString(CultureInfo.InvariantCulture)).Append('\t')
                  .Append(best.Opp is null ? "~" : best.Opp.Value.ToString(CultureInfo.InvariantCulture))
                  .Append('\t')
                  .Append(string.Join(",", best.Holders.Select(x => x.ToString(CultureInfo.InvariantCulture))))
                  .Append('\n');
            }
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        return (data.Count, values);
    }
}
