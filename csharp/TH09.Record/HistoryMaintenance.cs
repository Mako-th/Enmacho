using System.Globalization;
using Microsoft.Data.Sqlite;
using TH09.Record.Generated;

namespace TH09.Record;

public static class SessionProtection
{
    public sealed record FinalScoreBest(long? Mode, long? Character, long? Difficulty, long Score,
                                        string Source, long? SessionId, long? ReplayId);

    public sealed record ScannedLinks(HashSet<long> ReplacedReplayIds, Dictionary<long, long> ReplayBySession);

    public static HashSet<long> ProtectedSessionIds(SqliteConnection c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var prot = new HashSet<long>();
        foreach (var d in Repository.SegmentBests(c).Values)
        {
            if (d.Score is { } s) prot.UnionWith(s.Holders);
            if (d.Time is { } t) prot.UnionWith(t.Holders);
        }
        foreach (var r in FinalScoreBests(c, ownOnly: true))
        {
            if (r.Source is "live" or "scan" && r.SessionId is long sid && r.Score > 0) prot.Add(sid);
        }
        prot.UnionWith(Scanned(c).ReplayBySession.Keys);
        return prot;
    }

    public static List<FinalScoreBest> FinalScoreBests(SqliteConnection c, bool ownOnly = true,
                                                       long? excludeSid = null)
    {
        ArgumentNullException.ThrowIfNull(c);
        var links = Scanned(c);
        HashSet<long> skip = ownOnly ? Repository.ForeignSessionIds(c) : [];

        var live = new Dictionary<(long?, long?, long?), (long Score, long Sid)>();
        using (var cmd = c.CreateCommand())
        {
            var q = "SELECT s.session_id sid,m.game_mode mode,m.p1_character ch,m.difficulty diff,"
                  + "MAX(st.score_at_end) fin FROM sessions s JOIN session_metadata m USING(session_id)"
                  + " JOIN stages st ON st.session_id=s.session_id"
                  + " WHERE m.game_mode IN (0,1) AND st.score_at_end IS NOT NULL";
            if (excludeSid is long ex)
            {
                q += " AND s.session_id<>$0";
                cmd.Parameters.AddWithValue("$0", ex);
            }
            cmd.CommandText = q + " GROUP BY s.session_id";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var sid = r.GetInt64(0);
                if (r.IsDBNull(4) || skip.Contains(sid)) continue;
                var fin = r.GetInt64(4);
                var k = (Long(r, 1), Long(r, 2), Long(r, 3));
                if (!live.TryGetValue(k, out var cur) || fin > cur.Score) live[k] = (fin, sid);
            }
        }

        var rep = new Dictionary<(long?, long?, long?), (long Score, long Rid)>();
        foreach (var x in SelfBests.ReplayFinalScores(c, ownOnly))
        {
            var k = (x.Mode, x.Character, x.Difficulty);
            if (!rep.TryGetValue(k, out var cur) || x.Score > cur.Score) rep[k] = (x.Score, x.ReplayId);
        }

        var rows = new List<FinalScoreBest>();
        var keys = new HashSet<(long?, long?, long?)>(live.Keys);
        keys.UnionWith(rep.Keys);
        foreach (var k in keys)
        {
            var hasLive = live.TryGetValue(k, out var lv);
            var hasRep = rep.TryGetValue(k, out var rp);
            FinalScoreBest row;
            if (hasLive && (!hasRep || lv.Score >= rp.Score))
                row = links.ReplayBySession.TryGetValue(lv.Sid, out var linked)
                    ? new FinalScoreBest(k.Item1, k.Item2, k.Item3, lv.Score, "scan", lv.Sid, linked)
                    : new FinalScoreBest(k.Item1, k.Item2, k.Item3, lv.Score, "live", lv.Sid, null);
            else
                row = new FinalScoreBest(k.Item1, k.Item2, k.Item3, rp.Score, "replay", null, rp.Rid);
            rows.Add(row);
        }
        rows.Sort((a, b) =>
        {
            var x = N(a.Character).CompareTo(N(b.Character));
            if (x != 0) return x;
            x = N(a.Difficulty).CompareTo(N(b.Difficulty));
            return x != 0 ? x : N(a.Mode).CompareTo(N(b.Mode));
        });
        return rows;

        static long N(long? v) => v ?? 99;
    }

    public static ScannedLinks Scanned(SqliteConnection c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var rid = new HashSet<long>();
        var bySid = new Dictionary<long, long>();
        if (!HasReplayCols(c))
        {
            using var old = c.CreateCommand();
            old.CommandText = "SELECT sr.session_id,sr.replay_id FROM session_replays sr"
                            + " JOIN sessions s USING(session_id) WHERE sr.link_method=$0";
            old.Parameters.AddWithValue("$0", RecordLabels.ScanLinkMethod);
            using var r0 = old.ExecuteReader();
            while (r0.Read()) bySid.TryAdd(r0.GetInt64(0), r0.GetInt64(1));
            return new ScannedLinks(rid, bySid);
        }
        using var cmd = c.CreateCommand();
        cmd.CommandText =
            "SELECT sr.session_id sid,sr.replay_id rid,r.decoded_json dj,r.mode rm,r.difficulty rd,r.p1_char rc,"
            + "m.game_mode sm,m.difficulty sd,m.p1_character sc,"
            + "(SELECT COUNT(*) FROM stages st WHERE st.session_id=sr.session_id) nst"
            + " FROM session_replays sr JOIN sessions s USING(session_id) JOIN replays r USING(replay_id)"
            + " LEFT JOIN session_metadata m USING(session_id) WHERE sr.link_method=$0";
        cmd.Parameters.AddWithValue("$0", RecordLabels.ScanLinkMethod);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var sid = r.GetInt64(0);
            var replay = r.GetInt64(1);
            bySid.TryAdd(sid, replay);
            if (Long(r, 6) != Long(r, 3) || Long(r, 7) != Long(r, 4) || Long(r, 8) != Long(r, 5)) continue;
            int want;
            try
            {
                var dj = r.IsDBNull(2) ? null : r.GetString(2);
                want = ReplayStages.P1Stages(dj).Count;
            }
            catch (Exception exc) when (exc is InvalidDataException or OverflowException)
            {
                continue;
            }
            if (want > 0 && r.GetInt64(9) >= want) rid.Add(replay);
        }
        return new ScannedLinks(rid, bySid);
    }

    public static string SessionCategory(SqliteConnection c, long sessionId, string? executionType)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (executionType == RecordLabels.ExecReplay) return "replay";
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM stages WHERE session_id=$0 AND status='completed' LIMIT 1";
        cmd.Parameters.AddWithValue("$0", sessionId);
        return cmd.ExecuteScalar() is not null ? "completed" : "aborted";
    }

    internal static bool TryFinalOfReplay(string? decodedJson, out long fin)
    {
        fin = 0;
        CanonJson.Node root;
        try
        {
            if (decodedJson is null) return false;
            root = ReplayStages.Parse(decodedJson);
        }
        catch (InvalidDataException)
        {
            return false;
        }
        if (root.Kind != CanonJson.Kind.Object) return false;
        var stages = root.Members.TryGetValue("stages", out var v) ? v : null;
        if (stages is null || stages.Kind == CanonJson.Kind.Null) stages = null;
        if (stages is null) return false;
        if (stages.Kind != CanonJson.Kind.Array)
            throw new InvalidDataException("decoded_json の stages が配列でない（原本は for が回らずに落ちる）");
        var found = false;
        foreach (var s in stages.Items)
        {
            if (s.Kind != CanonJson.Kind.Object)
                throw new InvalidDataException("stages の要素がオブジェクトでない（原本は .get() で AttributeError）");
            if (!s.Members.TryGetValue("index", out var idx) || idx.Kind == CanonJson.Kind.Null) continue;
            if (ReplayStages.AsPythonInt(idx, "index") > 8) continue;
            if (!s.Members.TryGetValue("score", out var sc) || sc.Kind == CanonJson.Kind.Null) continue;
            var value = ReplayStages.AsPythonInt(sc, "score");
            if (!found || value > fin) { fin = value; found = true; }
        }
        return found;
    }

    private static long? Long(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetInt64(i);

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
}

public sealed record PrunePlan(IReadOnlyList<long> ToDelete, IReadOnlySet<long> Protected,
                               IReadOnlyList<long> AbortedOrReplay, IReadOnlyList<long> Completed);

public static class HistoryMaintenance
{
    public static HashSet<long> ProtectedSessionIds(string mainDb)
    {
        using var db = OpenReadOnly(mainDb);
        return SessionProtection.ProtectedSessionIds(db.Connection);
    }

    public static PrunePlan Plan(string mainDb, int keepAbortedReplay, int keepCompleted)
    {
        using var db = OpenReadOnly(mainDb);
        return Plan(db.Connection, keepAbortedReplay, keepCompleted);
    }

    public static PrunePlan Plan(SqliteConnection c, int keepAbortedReplay, int keepCompleted)
    {
        ArgumentNullException.ThrowIfNull(c);
        var protectedIds = SessionProtection.ProtectedSessionIds(c);
        var abrep = new List<long>();
        var completed = new List<long>();
        var listed = new List<(long Sid, string? ExecutionType)>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT s.session_id sid,m.execution_type et FROM sessions s"
                            + " LEFT JOIN session_metadata m USING(session_id) ORDER BY s.session_id DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read()) listed.Add((r.GetInt64(0), r.IsDBNull(1) ? null : r.GetString(1)));
        }
        foreach (var (sid, et) in listed)
        {
            var cat = SessionProtection.SessionCategory(c, sid, et);
            (cat == "completed" ? completed : abrep).Add(sid);
        }
        var toDelete = new List<long>();
        toDelete.AddRange(Over(abrep, keepAbortedReplay, protectedIds));
        toDelete.AddRange(Over(completed, keepCompleted, protectedIds));
        return new PrunePlan(toDelete, protectedIds, abrep, completed);
    }

    public static SessionDeleteResult Delete(string mainDb, string? layer0Db, IReadOnlyList<long> sessionIds,
                                             Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(sessionIds);
        if (!File.Exists(mainDb)) throw new FileNotFoundException("本体 DB がありません: " + mainDb, mainDb);
        using var db = RecordDb.OpenReadWrite(mainDb);
        return SessionDelete.Run(db.Connection, sessionIds, layer0Db, log);
    }

    public static bool PointsAtRealData(string mainDb, string? layer0Db)
        => RealDbGuard.InMainDbDir(mainDb) || (layer0Db is not null && RealDbGuard.InLayer0Dir(layer0Db));

    private static List<long> Over(List<long> ordered, int keep, HashSet<long> protectedIds)
    {
        var extra = new List<long>();
        if (keep <= 0) return extra;
        var kept = 0;
        foreach (var sid in ordered)
        {
            if (protectedIds.Contains(sid)) continue;
            kept++;
            if (kept > keep) extra.Add(sid);
        }
        return extra;
    }

    private static RecordDb OpenReadOnly(string mainDb)
        => RecordDb.OpenReadOnly(mainDb)
           ?? throw new FileNotFoundException("本体 DB を読み取り専用で開けません: " + mainDb, mainDb);
}
