using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TH09.Layer0;
using TH09.Record.Generated;

namespace TH09.Record;

public static class SessionRestoreDump
{
    public const string Flag = "--session-restore";

    public const string NoLayer0 = SessionDeleteDump.NoLayer0;

    private const string SyntheticLoggerVersion = "th09_record restore";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(TextWriter w, string dbPath, string layer0Path,
                          IReadOnlyList<long> sessionIds, bool dryRun, bool allowNoTombstone)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(dbPath);
        ArgumentNullException.ThrowIfNull(layer0Path);
        ArgumentNullException.ThrowIfNull(sessionIds);
        var layer0 = layer0Path == NoLayer0 ? null : layer0Path;

        if (HistoryMaintenance.PointsAtRealData(dbPath, layer0))
        {
            Console.Error.WriteLine("★本物の記録のフォルダは受け付けません（合成の対を渡してください）: "
                                    + dbPath + " / " + (layer0 ?? NoLayer0));
            return 3;
        }
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine("本体 DB がありません: " + dbPath);
            return 1;
        }

        Row(w, "fact", "db", Path.GetFullPath(dbPath));
        Row(w, "fact", "real_main_db", Paths.Default.MainDb);
        Row(w, "fact", "real_layer0_db", Paths.Default.Layer0Db);

        SqliteConnection? layer0Conn = null;
        var layer0Ids = new HashSet<long>();
        RecordDb? rw = null;
        RecordDb? ro = null;
        try
        {
            if (layer0 is not null && File.Exists(layer0))
            {
                layer0Conn = TickReplay.OpenLayer0(layer0);
                layer0Ids = [.. TickReplay.Sessions(layer0Conn)];
            }

            SqliteConnection conn;
            if (dryRun)
            {
                ro = RecordDb.OpenReadOnly(dbPath);
                if (ro is null)
                {
                    Console.Error.WriteLine("本体 DB を読み取り専用で開けません: " + dbPath);
                    return 1;
                }
                conn = ro.Connection;
            }
            else
            {
                rw = RecordDb.OpenReadWrite(dbPath);
                conn = rw.Connection;
            }

            if (!allowNoTombstone && Repository.TombstoneIds(conn).Count == 0 && !HasTable(conn, "deleted_sessions"))
                Row(w, "log", "0", "注意: deleted_sessions（控え）がありません。"
                                  + "アプリを一度起動すると移行で作られます。");

            var ok = 0;
            var failed = 0;
            foreach (var sid in sessionIds.Distinct().Order())
            {
                if (SessionExists(conn, sid))
                {
                    Row(w, "plan", Num(sid), "skip", "already_in_sessions");
                    failed++;
                    continue;
                }
                var tombstoneJson = Repository.TombstoneJson(conn, sid);
                string source, sessionWall, payloadJson;
                if (tombstoneJson is not null)
                {
                    source = "tombstone"; sessionWall = "exact"; payloadJson = tombstoneJson;
                }
                else if (allowNoTombstone && layer0Ids.Contains(sid))
                {
                    source = "layer0"; sessionWall = "unknown"; payloadJson = SyntheticPayload(sid);
                }
                else
                {
                    var reason = allowNoTombstone ? "nothing_to_restore" : "no_tombstone";
                    Row(w, "plan", Num(sid), "skip", reason);
                    failed++;
                    continue;
                }

                var (started, status) = SessionsHead(payloadJson);
                var hasTicks = layer0Ids.Contains(sid);
                var (segments, ticks) = hasTicks ? SegmentsInfo(layer0Conn!, sid) : (0L, 0L);
                Row(w, "plan", Num(sid), source, started ?? "", status ?? "",
                    hasTicks ? Num(segments) : "", hasTicks ? Num(ticks) : "");
                if (dryRun)
                {
                    ok++;
                    continue;
                }

                try
                {
                    if (hasTicks)
                    {
                        var closeStatus = status is not null
                            && RecordLabels.SessionCloseReason.TryGetValue(status, out var cs) ? cs : "aborted";
                        var result = TickReplay.ReplayInto(conn, sid, layer0Conn!, sid, closeStatus);
                        Row(w, "replay", Num(sid), Num(result.TotalTicks), Num(result.KeptTicks), Num(result.Events));
                    }
                    else
                    {
                        Row(w, "log", Num(sid), "生tickがありません（stages/rounds/events は作れません）");
                    }
                    var wrote = SessionRestore.Restore(conn, sid, payloadJson, source, sessionWall);
                    Row(w, "result", Num(sid),
                        string.Join(",", wrote.Select(kv => kv.Key + "=" + Num(kv.Value))));
                    ok++;
                }
                catch (Exception exc)
                {
                    Row(w, "raised", Num(sid), exc.GetType().Name,
                        exc.Message.Replace("\n", "\\n", StringComparison.Ordinal));
                    failed++;
                }
            }
            Row(w, "end", Num(ok), Num(failed));
        }
        finally
        {
            rw?.Dispose();
            ro?.Dispose();
            layer0Conn?.Dispose();
        }
        return 0;
    }

    private static bool SessionExists(SqliteConnection c, long sessionId)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sessions WHERE session_id=$0";
        cmd.Parameters.AddWithValue("$0", sessionId);
        return cmd.ExecuteScalar() is not null;
    }

    private static bool HasTable(SqliteConnection c, string name)
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

    private static (string? Started, string? Status) SessionsHead(string payloadJson)
    {
        using var doc = JsonDocument.Parse(payloadJson);
        var rows = doc.RootElement.GetProperty("tables").GetProperty("sessions");
        var first = rows[0];
        var started = first.TryGetProperty("started_at", out var s) && s.ValueKind == JsonValueKind.String
            ? s.GetString() : null;
        var status = first.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.String
            ? st.GetString() : null;
        return (started, status);
    }

    private static (long Segments, long Ticks) SegmentsInfo(SqliteConnection layer0, long sessionId)
    {
        var segs = SegmentReader.SegmentsOf(layer0, sessionId);
        long ticks = 0;
        foreach (var seg in segs) ticks += TickArchive.ParseFieldOrder(seg.FieldOrderText).TickCount;
        return (segs.Count, ticks);
    }

    private static string SyntheticPayload(long sessionId) =>
        "{\"format\":" + RecordLabels.TombstoneFormat.ToString(Inv) + ",\"refs\":{},\"tables\":{\"sessions\":[{"
        + "\"session_id\":" + sessionId.ToString(Inv)
        + ",\"started_at\":" + Payload.JsonString(RecordLabels.UnknownStartedAt)
        + ",\"ended_at\":null"
        + ",\"status\":" + Payload.JsonString(RecordLabels.UnknownStatus)
        + ",\"logger_version\":" + Payload.JsonString(SyntheticLoggerVersion)
        + ",\"notes\":null}]}}";

    private static string Num(long value) => value.ToString(Inv);

    private static void Row(TextWriter w, params string[] cells) =>
        w.Write(string.Join("\t", cells) + "\n");
}
