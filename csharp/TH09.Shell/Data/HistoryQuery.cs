using System.Security.Cryptography;
using System.Text;
using TH09.Analysis;

namespace TH09.Shell.Data;

internal sealed record HistoryFilter(HistoryKind Kind);

internal sealed record HistorySnapshot(List<HistoryRow> Rows, HashSet<long> Protected, string Signature);

internal static class HistoryQuery
{
    public const string SqlSessions = """
        SELECT s.session_id,
               s.started_at,
               s.status,
               m.game_mode,
               m.difficulty,
               m.p1_character,
               m.p2_character,
               m.execution_type,
               (SELECT COUNT(*)        FROM session_replays x WHERE x.session_id = s.session_id) AS replay_count,
               (SELECT MIN(x.replay_id) FROM session_replays x WHERE x.session_id = s.session_id) AS replay_id
          FROM sessions s
          LEFT JOIN session_metadata m ON m.session_id = s.session_id
         ORDER BY s.session_id DESC
        """;

    public const string SqlStages = """
        SELECT st.session_id,
               st.stage_record_id,
               st.lives_at_end,
               st.score_at_end
          FROM stages st
         ORDER BY st.session_id, st.stage_record_id
        """;

    public const string SqlRounds = """
        SELECT ro.session_id,
               ro.round_record_id,
               ro.lives_1_at_end
          FROM rounds ro
         ORDER BY ro.session_id, ro.round_record_id
        """;

    public const string SqlClearBonuses = """
        SELECT cb.session_id,
               cb.clear_bonus_id,
               cb.score_after_bonus
          FROM clear_bonuses cb
         ORDER BY cb.session_id, cb.clear_bonus_id
        """;

    private sealed class StageFold
    {
        public double? LastLives;
        public long? LastScore;
    }

    public static List<HistoryRow> LoadAll(AnalysisDb db)
    {
        var stages = FoldStages(db);
        var lastRoundLives = FoldLastRoundLives(db);
        var lastBonusScore = FoldLastBonusScore(db);
        var scanLinks = RecordKinds.ReadScanLinks(db);
        var rows = new List<HistoryRow>();

        TrackerDb.ForEachRow(db, SqlSessions, r =>
        {
            var sessionId = r.GetInt64(0);
            var startedAt = TrackerDb.StringOrNull(r, 1);
            var status = TrackerDb.StringOrNull(r, 2);
            var gameMode = TrackerDb.Int32OrNull(r, 3);
            var difficulty = TrackerDb.Int32OrNull(r, 4);
            var p1 = TrackerDb.Int32OrNull(r, 5);
            var p2 = TrackerDb.Int32OrNull(r, 6);
            var executionType = TrackerDb.StringOrNull(r, 7);
            var replayCount = r.GetInt32(8);
            var replayId = TrackerDb.Int64OrNull(r, 9);

            var sf = stages.TryGetValue(sessionId, out var s) ? s : null;
            double? finalLives = null;
            long? finalScore = null;
            if (sf is not null)
            {
                finalLives = sf.LastLives ?? (lastRoundLives.TryGetValue(sessionId, out var lv) ? lv : null);
                finalScore = sf.LastScore ?? (lastBonusScore.TryGetValue(sessionId, out var sc) ? sc : null);
            }

            rows.Add(new HistoryRow
            {
                SessionId = sessionId,
                StartedAtRaw = startedAt,
                Status = status,
                GameMode = gameMode,
                Difficulty = difficulty,
                P1Character = p1,
                P2Character = p2,
                ExecutionType = executionType,
                HasReplay = replayCount > 0,
                ReplayId = replayId,
                ReplayCount = replayCount,
                Kind = RecordKinds.Of(scanLinks.Sessions.Contains(sessionId), replayCount > 0),
                FinalLives = finalLives,
                FinalScore = finalScore,
            });
        });

        return rows;
    }

    public static HistorySnapshot Read(string mainDb)
    {
        List<HistoryRow> rows;
        using (var db = TrackerDb.OpenMainDb())
            rows = LoadAll(db);
        var prot = TH09.Record.HistoryMaintenance.ProtectedSessionIds(mainDb);
        foreach (var row in rows) row.IsProtected = prot.Contains(row.SessionId);
        return new HistorySnapshot(rows, prot, Signature(rows, prot));
    }

    public static string Signature(IReadOnlyList<HistoryRow> rows, IReadOnlySet<long> protectedIds)
    {
        var sb = new StringBuilder();
        sb.Append(rows.Count).Append('\n');
        foreach (var row in rows)
        {
            sb.Append(row.SessionId).Append('\t');
            foreach (var col in HistoryColumns.All)
                sb.Append(row.Text(col.Key)).Append('\t');
            sb.Append(protectedIds.Contains(row.SessionId) ? '1' : '0').Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    public static List<HistoryRow> Filter(IEnumerable<HistoryRow> rows, HistoryFilter f)
    {
        var outRows = new List<HistoryRow>();
        foreach (var row in rows)
        {
            if (f.Kind == HistoryKind.LivePlay && row.IsReplayPlayback) continue;
            if (f.Kind == HistoryKind.ReplayPlayback && !row.IsReplayPlayback) continue;
            outRows.Add(row);
        }
        return outRows;
    }

    public static void Sort(List<HistoryRow> rows, HistorySortKey key, bool descending)
    {
        rows.Sort((a, b) =>
        {
            var c = CompareBy(a, b, key, descending);
            return c != 0 ? c : b.SessionId.CompareTo(a.SessionId);
        });
    }

    private static int CompareBy(HistoryRow a, HistoryRow b, HistorySortKey key, bool desc) => key switch
    {
        HistorySortKey.SessionId => Cmp<long>(a.SessionId, b.SessionId, desc),
        HistorySortKey.StartedAt => CmpText(a.StartedAtRaw, b.StartedAtRaw, desc),
        HistorySortKey.Mode => Cmp(a.GameMode, b.GameMode, desc),
        HistorySortKey.Difficulty => Cmp(a.Difficulty, b.Difficulty, desc),
        HistorySortKey.P1Character => Cmp(ReplayLabels.DisplayRank(a.P1Character),
                                          ReplayLabels.DisplayRank(b.P1Character), desc),
        HistorySortKey.P2Character => Cmp(ReplayLabels.DisplayRank(a.IsStory ? null : a.P2Character),
                                          ReplayLabels.DisplayRank(b.IsStory ? null : b.P2Character), desc),
        HistorySortKey.Lives => Cmp(a.IsStory ? a.FinalLives : null,
                                    b.IsStory ? b.FinalLives : null, desc),
        HistorySortKey.Score => Cmp(a.IsStory ? a.FinalScore : null,
                                    b.IsStory ? b.FinalScore : null, desc),
        HistorySortKey.Status => CmpText(a.Status, b.Status, desc),
        HistorySortKey.HasReplay => Cmp<int>(a.HasReplay ? 1 : 0, b.HasReplay ? 1 : 0, desc),
        HistorySortKey.Kind => Cmp<int>((int)a.Kind, (int)b.Kind, desc),
        _ => 0,
    };

    private static int Cmp<T>(T? a, T? b, bool desc) where T : struct, IComparable<T>
    {
        if (a is null) return b is null ? 0 : 1;
        if (b is null) return -1;
        var c = a.Value.CompareTo(b.Value);
        return desc ? -c : c;
    }

    private static int CmpText(string? a, string? b, bool desc)
    {
        if (string.IsNullOrWhiteSpace(a)) return string.IsNullOrWhiteSpace(b) ? 0 : 1;
        if (string.IsNullOrWhiteSpace(b)) return -1;
        var c = string.CompareOrdinal(a, b);
        return desc ? -c : c;
    }

    private static Dictionary<long, StageFold> FoldStages(AnalysisDb db)
    {
        var map = new Dictionary<long, StageFold>();
        TrackerDb.ForEachRow(db, SqlStages, r =>
        {
            var sid = r.GetInt64(0);
            if (!map.TryGetValue(sid, out var f)) map[sid] = f = new StageFold();
            f.LastLives = TrackerDb.DoubleOrNull(r, 2);
            f.LastScore = TrackerDb.Int64OrNull(r, 3);
        });
        return map;
    }

    private static Dictionary<long, double> FoldLastRoundLives(AnalysisDb db)
    {
        var map = new Dictionary<long, double>();
        TrackerDb.ForEachRow(db, SqlRounds, r =>
        {
            var sid = r.GetInt64(0);
            var v = TrackerDb.DoubleOrNull(r, 2);
            if (v is double d) map[sid] = d; else map.Remove(sid);
        });
        return map;
    }

    private static Dictionary<long, long> FoldLastBonusScore(AnalysisDb db)
    {
        var map = new Dictionary<long, long>();
        TrackerDb.ForEachRow(db, SqlClearBonuses, r =>
        {
            var sid = r.GetInt64(0);
            var v = TrackerDb.Int64OrNull(r, 2);
            if (v is long n) map[sid] = n; else map.Remove(sid);
        });
        return map;
    }
}
