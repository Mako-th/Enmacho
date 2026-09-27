using System.Globalization;
using Microsoft.Data.Sqlite;

namespace TH09.Record;

public sealed record BestSource(string Kind, long? SessionId, long? ReplayId)
{
    public const string Live = "live";

    public const string Scan = "scan";

    public const string Replay = "replay";

    public static BestSource OfSession(long sessionId, IReadOnlyDictionary<long, long> scanBySession)
    {
        ArgumentNullException.ThrowIfNull(scanBySession);
        return scanBySession.TryGetValue(sessionId, out var rid)
            ? new BestSource(Scan, sessionId, rid)
            : new BestSource(Live, sessionId, null);
    }

    public static BestSource OfReplay(long replayId) => new(Replay, null, replayId);

    public string Text() => Kind switch
    {
        Scan => Scan + ":" + N(SessionId) + ":" + N(ReplayId),
        Replay => Replay + ":" + N(ReplayId),
        _ => Kind + ":" + N(SessionId),
    };

    private static string N(long? v) =>
        v is null ? "~" : v.Value.ToString(CultureInfo.InvariantCulture);
}

public sealed record ReplaySegmentScore(long? Mode, long? Character, long? Difficulty, long Stage,
                                        long Delta, long ScoreAtEnd, long ReplayId, long? Opponent,
                                        bool IsOwn);

public sealed record ReplayFinalScore(long? Mode, long? Character, long? Difficulty,
                                      long Score, long ReplayId, bool IsOwn);

public sealed record SegmentBestRawRow(long? Mode, long? Character, long? Difficulty, long? Stage,
                                       long? Opponent, long? Score, BestSource? ScoreSource,
                                       long? Time, BestSource? TimeSource);

internal readonly record struct ClearBonusIndex(IReadOnlyDictionary<long, long> ById,
                                                IReadOnlyDictionary<(long Sid, long Stage), long> ByStage)
{
    public long? Resolve(long sessionId, long? clearBonusId, long? stageNumber)
    {
        if (clearBonusId is long cbid && ById.TryGetValue(cbid, out var byId)) return byId;
        if (stageNumber is long sn && ByStage.TryGetValue((sessionId, sn), out var byStage)) return byStage;
        return null;
    }
}

public sealed record BestTableRow(long? Mode, long? Character, long? Difficulty, long? Stage,
                                  long? Opponent, long? Score, BestSource? ScoreSource,
                                  long? Time, BestSource? TimeSource);

public sealed record SelfBestSources(IReadOnlyList<ReplaySegmentScore> ReplaySegments,
                                     IReadOnlyList<ReplayFinalScore> ReplayFinals,
                                     IReadOnlyDictionary<long, long> ScanBySession);

public static class SelfBests
{
    public const int SegmentPairs = 8;

    public static List<ReplaySegmentScore> ReplaySegmentScores(
        SqliteConnection c, bool ownOnly = true, bool skipScanned = true)
    {
        ArgumentNullException.ThrowIfNull(c);
        var outRows = new List<ReplaySegmentScore>();
        if (!HasReplayCols(c)) return outRows;
        var scanned = skipScanned ? SessionProtection.Scanned(c).ReplacedReplayIds : [];

        foreach (var (rid, mode, diff, ch, isOwn, dj) in DecodedStoryReplays(c))
        {
            if (ownOnly && !isOwn) continue;
            if (scanned.Contains(rid)) continue;
            if (!TryStages(dj, out var stages)) continue;

            var score = new Dictionary<long, long?>();
            var oppo = new Dictionary<long, long?>();
            foreach (var s in stages)
            {
                if (s.Kind != CanonJson.Kind.Object)
                    throw new InvalidDataException(
                        "stages の要素がオブジェクトでない（原本は .get() で AttributeError）");
                if (!s.Members.TryGetValue("index", out var idx) || idx.Kind == CanonJson.Kind.Null)
                    continue;
                var key = ReplayStages.AsPythonInt(idx, "index");
                score[key] = Member(s, "score");
                oppo[key] = Member(s, "opponent");
            }

            for (var i = 0L; i < SegmentPairs; i++)
            {
                if (!score.TryGetValue(i, out var a) || a is null) continue;
                if (!score.TryGetValue(i + 1, out var b) || b is null) continue;
                var delta = b.Value - a.Value;
                if (delta <= 0) continue;
                oppo.TryGetValue(i, out var opp);
                outRows.Add(new ReplaySegmentScore(mode, ch, diff, i + 1, delta, b.Value,
                                                   rid, opp, isOwn));
            }
        }
        return outRows;
    }

    public static List<ReplayFinalScore> ReplayFinalScores(SqliteConnection c, bool ownOnly = true)
    {
        ArgumentNullException.ThrowIfNull(c);
        var outRows = new List<ReplayFinalScore>();
        if (!HasReplayCols(c)) return outRows;
        var scanned = SessionProtection.Scanned(c).ReplacedReplayIds;
        foreach (var (rid, mode, diff, ch, isOwn, dj) in DecodedStoryReplays(c))
        {
            if (ownOnly && !isOwn) continue;
            if (scanned.Contains(rid)) continue;
            if (!SessionProtection.TryFinalOfReplay(dj, out var fin)) continue;
            outRows.Add(new ReplayFinalScore(mode, ch, diff, fin, rid, isOwn));
        }
        return outRows;
    }

    public static List<BestTableRow> BestTable(SqliteConnection c, bool ownOnly = true,
                                               long? excludeSid = null)
    {
        ArgumentNullException.ThrowIfNull(c);
        var scanBySid = SessionProtection.Scanned(c).ReplayBySession;
        var live = Repository.SegmentBests(c, excludeSid: excludeSid, ownOnly: ownOnly);

        var rep = new Dictionary<string, (long Delta, long Rid, long? Opp)>(StringComparer.Ordinal);
        foreach (var s in ReplaySegmentScores(c, ownOnly: ownOnly))
        {
            var k = Repository.SegKey.Of(s.Mode, s.Difficulty, s.Character, s.Stage, s.Opponent).Text();
            if (!rep.TryGetValue(k, out var cur) || s.Delta > cur.Delta)
                rep[k] = (s.Delta, s.ReplayId, s.Opponent);
        }

        var keys = new HashSet<string>(live.Keys, StringComparer.Ordinal);
        keys.UnionWith(rep.Keys);
        var rows = new List<BestTableRow>();
        foreach (var k in keys)
        {
            var key = ParseSegKey(k);
            live.TryGetValue(k, out var lv);
            var ls = lv?.Score;
            var lt = lv?.Time;
            var hasRep = rep.TryGetValue(k, out var rs);

            long? best;
            BestSource? src;
            long? opp;
            if (ls is not null && hasRep)
            {
                if (ls.Value >= rs.Delta)
                    (best, src, opp) = (ls.Value, BestSource.OfSession(ls.Sid, scanBySid), ls.Opp);
                else
                    (best, src, opp) = (rs.Delta, BestSource.OfReplay(rs.Rid), rs.Opp);
            }
            else if (ls is not null)
            {
                (best, src, opp) = (ls.Value, BestSource.OfSession(ls.Sid, scanBySid), ls.Opp);
            }
            else if (hasRep)
            {
                (best, src, opp) = (rs.Delta, BestSource.OfReplay(rs.Rid), rs.Opp);
            }
            else
            {
                (best, src, opp) = (null, null, null);
            }

            if (best is null && lt is null) continue;
            rows.Add(new BestTableRow(
                key.Mode, key.Ch, key.Diff, key.Stage,
                key.Opp ?? opp,
                best, src,
                lt?.Value, lt is null ? null : BestSource.OfSession(lt.Sid, scanBySid)));
        }

        rows.Sort((a, b) =>
        {
            var x = N(a.Character).CompareTo(N(b.Character));
            if (x != 0) return x;
            x = N(a.Difficulty).CompareTo(N(b.Difficulty));
            if (x != 0) return x;
            x = N(a.Mode).CompareTo(N(b.Mode));
            if (x != 0) return x;
            x = N(a.Stage).CompareTo(N(b.Stage));
            return x != 0 ? x : N(a.Opponent).CompareTo(N(b.Opponent));
        });
        return rows;

        static long N(long? v) => v ?? 99;
    }

    internal static ClearBonusIndex LoadClearBonusIndex(SqliteConnection c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var byId = new Dictionary<long, long>();
        var byStage = new Dictionary<(long, long), long>();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT clear_bonus_id,session_id,stage_index,total_bonus FROM clear_bonuses";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            if (r.IsDBNull(3)) continue;
            var bonus = r.GetInt64(3);
            byId[r.GetInt64(0)] = bonus;
            if (!r.IsDBNull(2)) byStage[(r.GetInt64(1), r.GetInt64(2) + 1)] = bonus;
        }
        return new ClearBonusIndex(byId, byStage);
    }

    public static (IReadOnlyList<SegmentBestRawRow> Rows, int StagesWithBonus, int StagesWithoutBonus)
        BestTableRaw(SqliteConnection c, bool ownOnly = true, long? excludeSid = null)
    {
        ArgumentNullException.ThrowIfNull(c);
        var skip = ownOnly ? Repository.ForeignSessionIds(c) : [];
        var idx = LoadClearBonusIndex(c);
        var scanBySid = SessionProtection.Scanned(c).ReplayBySession;

        var best = new Dictionary<string, Repository.Best>(StringComparer.Ordinal);
        var withBonus = 0;
        var withoutBonus = 0;

        using (var cmd = c.CreateCommand())
        {
            var q = "SELECT st.session_id sid,st.stage_number stage,st.score_at_start ss,st.score_at_end se,"
                  + "st.opponent_character opp,st.clear_bonus_id cbid,"
                  + "m.game_mode mode,m.p1_character ch,m.difficulty diff "
                  + "FROM stages st JOIN session_metadata m USING(session_id) "
                  + "WHERE m.game_mode IN (0,1) AND st.stage_number IS NOT NULL";
            if (excludeSid is long ex)
            {
                q += " AND st.session_id<>$0";
                cmd.Parameters.AddWithValue("$0", ex);
            }
            cmd.CommandText = q;
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var sid = r.GetInt64(0);
                if (skip.Contains(sid)) continue;
                long? stage = r.IsDBNull(1) ? null : r.GetInt64(1);
                long? ss = r.IsDBNull(2) ? null : r.GetInt64(2);
                long? se = r.IsDBNull(3) ? null : r.GetInt64(3);
                long? opp = r.IsDBNull(4) ? null : r.GetInt64(4);
                long? cbid = r.IsDBNull(5) ? null : r.GetInt64(5);
                long? mode = r.IsDBNull(6) ? null : r.GetInt64(6);
                long? ch = r.IsDBNull(7) ? null : r.GetInt64(7);
                long? diff = r.IsDBNull(8) ? null : r.GetInt64(8);
                if (ss is null || se is null || se.Value - ss.Value <= 0) continue;

                var bonus = idx.Resolve(sid, cbid, stage);
                if (bonus is not null) withBonus++; else withoutBonus++;
                var net = se.Value - ss.Value - (bonus ?? 0);

                var key = Repository.SegKey.Of(mode, diff, ch, stage, opp).Text();
                if (!best.TryGetValue(key, out var cur))
                    best[key] = new Repository.Best(net, [sid], sid, opp);
                else if (net > cur.Value)
                    best[key] = new Repository.Best(net, [sid], sid, opp);
                else if (net == cur.Value)
                    cur.Holders.Add(sid);
            }
        }

        var timeByKey = new Dictionary<string, (long Time, BestSource Src)>(StringComparer.Ordinal);
        foreach (var row in BestTable(c, ownOnly: ownOnly, excludeSid: excludeSid))
        {
            if (row.Time is not long t || row.TimeSource is not BestSource ts) continue;
            var key = Repository.SegKey.Of(row.Mode, row.Difficulty, row.Character, row.Stage, row.Opponent).Text();
            timeByKey[key] = (t, ts);
        }

        var keys = new HashSet<string>(best.Keys, StringComparer.Ordinal);
        keys.UnionWith(timeByKey.Keys);
        var rows = new List<SegmentBestRawRow>();
        foreach (var k in keys)
        {
            var key = ParseSegKey(k);
            best.TryGetValue(k, out var b);
            var hasTime = timeByKey.TryGetValue(k, out var tv);
            rows.Add(new SegmentBestRawRow(
                key.Mode, key.Ch, key.Diff, key.Stage,
                key.Opp ?? b?.Opp,
                b?.Value, b is null ? null : BestSource.OfSession(b.Sid, scanBySid),
                hasTime ? tv.Time : null, hasTime ? tv.Src : null));
        }

        rows.Sort((a, b) =>
        {
            var x = N(a.Character).CompareTo(N(b.Character));
            if (x != 0) return x;
            x = N(a.Difficulty).CompareTo(N(b.Difficulty));
            if (x != 0) return x;
            x = N(a.Mode).CompareTo(N(b.Mode));
            if (x != 0) return x;
            x = N(a.Stage).CompareTo(N(b.Stage));
            return x != 0 ? x : N(a.Opponent).CompareTo(N(b.Opponent));
        });
        return (rows, withBonus, withoutBonus);

        static long N(long? v) => v ?? 99;
    }

    public static SelfBestSources Read(string mainDb)
    {
        using var db = RecordDb.OpenReadOnly(mainDb)
            ?? throw new FileNotFoundException("本体 DB を読み取り専用で開けません: " + mainDb, mainDb);
        var c = db.Connection;
        return new SelfBestSources(
            ReplaySegmentScores(c, ownOnly: false),
            ReplayFinalScores(c, ownOnly: false),
            SessionProtection.Scanned(c).ReplayBySession);
    }


    private static IEnumerable<(long Rid, long? Mode, long? Diff, long? Ch, bool IsOwn, string? Dj)>
        DecodedStoryReplays(SqliteConnection c)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT replay_id,mode,difficulty,p1_char,is_own,decoded_json FROM replays"
                        + " WHERE decode_status='decoded' AND mode IN (0,1)";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            yield return (
                r.GetInt64(0),
                r.IsDBNull(1) ? null : r.GetInt64(1),
                r.IsDBNull(2) ? null : r.GetInt64(2),
                r.IsDBNull(3) ? null : r.GetInt64(3),
                !r.IsDBNull(4) && r.GetInt64(4) != 0,
                r.IsDBNull(5) ? null : r.GetString(5));
        }
    }

    private static bool TryStages(string? decodedJson, out IReadOnlyList<CanonJson.Node> stages)
    {
        stages = [];
        if (decodedJson is null) return false;
        CanonJson.Node root;
        try
        {
            root = ReplayStages.Parse(decodedJson);
        }
        catch (InvalidDataException)
        {
            return false;
        }
        if (root.Kind != CanonJson.Kind.Object) return false;
        if (!root.Members.TryGetValue("stages", out var v)) return true;
        if (v.Kind == CanonJson.Kind.Null)
        {
            throw new InvalidDataException("decoded_json の stages が null（原本は for が TypeError）");
        }
        if (v.Kind != CanonJson.Kind.Array)
            throw new InvalidDataException("decoded_json の stages が配列でない（原本は for が落ちる）");
        stages = v.Items;
        return true;
    }

    private static long? Member(CanonJson.Node stage, string name)
        => stage.Members.TryGetValue(name, out var v) && v.Kind != CanonJson.Kind.Null
            ? ReplayStages.AsPythonInt(v, name)
            : null;

    private static (long? Mode, long? Diff, long? Ch, long? Stage, long? Opp) ParseSegKey(string text)
    {
        var p = text.Split('/');
        if (p.Length != 5)
            throw new InvalidDataException("区間ベストの鍵の形が違う（SegKey.Text と対にならない）: " + text);
        return (V(p[0]), V(p[1]), V(p[2]), V(p[3]), V(p[4]));

        static long? V(string s) => s == "~"
            ? null
            : long.Parse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
    }

    private static bool HasReplayCols(SqliteConnection c)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(replays)";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            if (string.Equals(r.GetString(1), "decode_status", StringComparison.Ordinal)) return true;
        return false;
    }
}
