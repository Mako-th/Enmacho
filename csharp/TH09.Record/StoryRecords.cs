using Microsoft.Data.Sqlite;

namespace TH09.Record;

public sealed record StoryPlay(long SessionId, long? Mode, long? Character, long? Difficulty,
                               long? StageMax, long? FinalScore, int Misses, int Extends,
                               double? InitialLives, double? FinalLives);

public sealed record StoryCharacterRecord(long Mode, long Character, int Plays,
                                          long? BestScore, long? BestScoreSession,
                                          int? MinMisses, long? MinMissSession,
                                          double? MaxFinalLives, long? MaxFinalLivesSession,
                                          long? StageMax);

public static class StoryRecords
{
    public const int FinalStage = 9;

    public static List<StoryPlay> StoryPlays(SqliteConnection c)
    {
        ArgumentNullException.ThrowIfNull(c);

        var head = new Dictionary<long, (long? Mode, long? Diff, long? Ch, double? InitLives,
                                         long? StageMax, long? FinalScore)>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText =
                "SELECT s.session_id,m.game_mode,m.difficulty,m.p1_character,m.initial_lives,"
              + "MAX(st.stage_number) sm,MAX(st.score_at_end) fin "
              + "FROM sessions s JOIN session_metadata m USING(session_id) "
              + "JOIN stages st ON st.session_id=s.session_id "
              + "WHERE m.game_mode IN (0,1) AND st.stage_number IS NOT NULL "
              + "AND st.score_at_end IS NOT NULL GROUP BY s.session_id";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var sid = r.GetInt64(0);
                head[sid] = (MatchBests.Long(r, 1), MatchBests.Long(r, 2), MatchBests.Long(r, 3),
                            r.IsDBNull(4) ? null : r.GetDouble(4),
                            MatchBests.Long(r, 5), MatchBests.Long(r, 6));
            }
        }

        var counts = new Dictionary<(long Sid, string Type), int>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText =
                "SELECT session_id,event_type,COUNT(*) FROM events "
              + "WHERE event_type IN ('LIVES_LOST','EXTEND') AND side=1 "
              + "GROUP BY session_id,event_type";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                counts[(r.GetInt64(0), r.GetString(1))] = checked((int)r.GetInt64(2));
        }

        var rows = new List<StoryPlay>();
        foreach (var (sid, h) in head)
        {
            var misses = counts.TryGetValue((sid, "LIVES_LOST"), out var m) ? m : 0;
            var extends = counts.TryGetValue((sid, "EXTEND"), out var e) ? e : 0;
            double? finalLives = h.InitLives is double init ? init + extends - misses : null;
            rows.Add(new StoryPlay(sid, h.Mode, h.Ch, h.Diff, h.StageMax, h.FinalScore,
                                   misses, extends, h.InitLives, finalLives));
        }
        rows.Sort((a, b) => a.SessionId.CompareTo(b.SessionId));
        return rows;
    }

    public static List<StoryCharacterRecord> StoryCharacterRecords(IEnumerable<StoryPlay> plays)
    {
        ArgumentNullException.ThrowIfNull(plays);
        var acc = new Dictionary<(long Mode, long Character), Agg>();
        foreach (var p in plays)
        {
            if (p.Mode is not (0L or 1L) || p.Character is not long ch) continue;
            var key = (p.Mode.Value, ch);
            if (!acc.TryGetValue(key, out var a)) acc[key] = a = new Agg();
            a.Plays++;
            if (p.FinalScore is long score && (a.BestScore is null || score > a.BestScore))
            {
                a.BestScore = score;
                a.BestScoreSession = p.SessionId;
            }
            if (p.StageMax is long sm && (a.StageMax is null || sm > a.StageMax)) a.StageMax = sm;

            if (p.StageMax is long reached && reached >= FinalStage)
            {
                if (a.MinMisses is null || p.Misses < a.MinMisses)
                {
                    a.MinMisses = p.Misses;
                    a.MinMissSession = p.SessionId;
                }
                if (p.FinalLives is double fl && (a.MaxFinalLives is null || fl > a.MaxFinalLives))
                {
                    a.MaxFinalLives = fl;
                    a.MaxFinalLivesSession = p.SessionId;
                }
            }
        }

        var rows = new List<StoryCharacterRecord>();
        foreach (var (key, a) in acc)
            rows.Add(new StoryCharacterRecord(key.Mode, key.Character, a.Plays, a.BestScore,
                                              a.BestScoreSession, a.MinMisses, a.MinMissSession,
                                              a.MaxFinalLives, a.MaxFinalLivesSession, a.StageMax));
        rows.Sort((x, y) =>
        {
            var c1 = x.Mode.CompareTo(y.Mode);
            return c1 != 0 ? c1 : x.Character.CompareTo(y.Character);
        });
        return rows;
    }

    public static (IReadOnlyList<StoryPlay> Plays, IReadOnlyList<StoryCharacterRecord> Records)
        Read(string mainDb)
    {
        using var db = RecordDb.OpenReadOnly(mainDb)
            ?? throw new FileNotFoundException("本体 DB を読み取り専用で開けません: " + mainDb, mainDb);
        var plays = StoryPlays(db.Connection);
        return (plays, StoryCharacterRecords(plays));
    }

    private sealed class Agg
    {
        public int Plays;
        public long? BestScore;
        public long? BestScoreSession;
        public int? MinMisses;
        public long? MinMissSession;
        public double? MaxFinalLives;
        public long? MaxFinalLivesSession;
        public long? StageMax;
    }
}
