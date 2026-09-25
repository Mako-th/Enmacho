using Microsoft.Data.Sqlite;
using TH09.Analysis;

namespace TH09.Shell.Data;

internal static class ReplayDetailQuery
{
    public const string SqlLink = """
        SELECT sr.session_id,
               sr.replay_id,
               --: ★走査で入った紐付きか、更新時刻からの推測か【2026-09-13】。
               --:   ★綴りは SQL に書かない ——★判定は RecordKinds.IsScanLink ただ 1 か所に置く
               sr.link_method
          FROM session_replays sr
         WHERE ($0 IS NULL OR sr.replay_id  = $0)
           AND ($1 IS NULL OR sr.session_id = $1)
         ORDER BY sr.session_id, sr.replay_id
        """;

    public const string SqlReplay = """
        SELECT r.replay_id,
               r.mode,
               r.difficulty,
               r.player_name,
               r.replay_date,
               r.mtime,
               r.p1_char,
               r.p2_char,
               r.p1_name,
               r.p2_name,
               r.is_own,
               r.owner_side,
               --: ★4 分類の原本（面 9 / 19 の ai）【2026-09-13】。★一番後ろへ足すこと
               r.decoded_json
          FROM replays r
         WHERE r.replay_id = $0
        """;

    public const string SqlSession = """
        SELECT s.session_id,
               s.started_at,
               s.status,
               m.game_mode,
               m.difficulty,
               m.p1_character,
               m.p2_character,
               m.p1_control,
               m.p2_control
          FROM sessions s
          LEFT JOIN session_metadata m ON m.session_id = s.session_id
         WHERE s.session_id = $0
        """;

    public const string SqlStages = """
        SELECT st.stage_record_id,
               st.stage_number,
               st.opponent_character,
               st.score_at_start,
               st.score_at_end,
               st.lives_at_end,
               st.winner_side,
               st.status,
               cb.total_bonus,
               st.field_id,
               st.battle_bgm_id
          FROM stages st
          LEFT JOIN clear_bonuses cb ON cb.clear_bonus_id = st.clear_bonus_id
         WHERE st.session_id = $0
         ORDER BY st.stage_number, st.stage_record_id
        """;

    public const string SqlRounds = """
        SELECT ro.round_record_id,
               ro.stage_record_id,
               ro.round_number,
               ro.duration_frames,
               ro.winner_side,
               ro.status,
               ro.score_1_at_start,
               ro.score_1_at_end,
               ro.score_2_at_end,
               ro.life_1_raw_at_end,
               ro.life_2_raw_at_end,
               ro.lives_1_at_end
          FROM rounds ro
         WHERE ro.session_id = $0
         ORDER BY ro.round_record_id
        """;

    public const string SqlCounterEvents = """
        SELECT e.event_id,
               e.event_type,
               e.side
          FROM events e
         WHERE e.session_id = $0
           AND e.event_type IN ('SPELL_ATTACK', 'BOSS_ATTACK', 'BOSS_REVERSAL', 'ROUND_COMPLETED')
         ORDER BY e.event_id
        """;

    public const string SqlFiles = """
        SELECT r.replay_id,
               p.full_path,
               r.source,
               p.is_current,
               sr.link_confidence,
               r.decode_status
          FROM session_replays sr
          JOIN replays r USING(replay_id)
          LEFT JOIN replay_paths p USING(replay_id)
         WHERE sr.session_id = $0
         ORDER BY p.is_current DESC, p.replay_path_id DESC
        """;

    public const string SqlFilesByReplay = """
        SELECT r.replay_id,
               p.full_path,
               r.source,
               p.is_current,
               NULL AS link_confidence,
               r.decode_status
          FROM replays r
          LEFT JOIN replay_paths p USING(replay_id)
         WHERE r.replay_id = $0
         ORDER BY p.is_current DESC, p.replay_path_id DESC
        """;

    private const int ModeMatch = 2;

    private const string EventSpell = "SPELL_ATTACK";
    private const string EventBoss = "BOSS_ATTACK";
    private const string EventReversal = "BOSS_REVERSAL";
    private const string EventRoundCompleted = "ROUND_COMPLETED";

    public static ReplayDetail Load(AnalysisDb db, long? replayId, long? sessionId,
                                    string? layer0Path = null)
    {
        if (replayId is null && sessionId is null)
            return new ReplayDetail { Status = "どのリプレイを出すかが決まっていません。" };

        var links = new List<(long Session, long Replay, string? Method)>();
        ForEachRow(db, SqlLink, [Box(replayId), Box(sessionId)],
                   r => links.Add((r.GetInt64(0), r.GetInt64(1), TrackerDb.StringOrNull(r, 2))));

        var sid = sessionId ?? (links.Count > 0 ? links[0].Session : (long?)null);
        var rid = replayId ?? (links.Count > 0 ? links[0].Replay : (long?)null);

        var inferredSession = sessionId is null && sid is not null;
        var scanLinked = sid is long slid && rid is long rlid
                         && links.Any(x => x.Session == slid && x.Replay == rlid
                                           && RecordKinds.IsScanLink(x.Method));

        int? mode = null, difficulty = null, p1Char = null, p2Char = null;
        int? isOwn = null, ownerSide = null, p1Control = null, p2Control = null;
        string? playerName = null, replayDate = null, mtime = null, p1NameRaw = null, p2NameRaw = null;
        string? decodedJson = null;
        var haveReplay = false;
        var haveSession = false;

        if (rid is long r0)
        {
            ForEachRow(db, SqlReplay, [r0], r =>
            {
                haveReplay = true;
                mode = TrackerDb.Int32OrNull(r, 1);
                difficulty = TrackerDb.Int32OrNull(r, 2);
                playerName = TrackerDb.StringOrNull(r, 3);
                replayDate = TrackerDb.StringOrNull(r, 4);
                mtime = TrackerDb.StringOrNull(r, 5);
                p1Char = TrackerDb.Int32OrNull(r, 6);
                p2Char = TrackerDb.Int32OrNull(r, 7);
                p1NameRaw = TrackerDb.StringOrNull(r, 8);
                p2NameRaw = TrackerDb.StringOrNull(r, 9);
                isOwn = TrackerDb.Int32OrNull(r, 10);
                ownerSide = TrackerDb.Int32OrNull(r, 11);
                decodedJson = TrackerDb.StringOrNull(r, 12);
            });
        }

        var replayDifficulty = difficulty;
        var replayP1Char = p1Char;
        var replayP2Char = p2Char;
        int? sessionDifficulty = null, sessionP1Char = null, sessionP2Char = null;

        if (sid is long s0)
        {
            ForEachRow(db, SqlSession, [s0], r =>
            {
                haveSession = true;
                mode ??= TrackerDb.Int32OrNull(r, 3);
                sessionDifficulty = TrackerDb.Int32OrNull(r, 4);
                sessionP1Char = TrackerDb.Int32OrNull(r, 5);
                sessionP2Char = TrackerDb.Int32OrNull(r, 6);
                difficulty ??= sessionDifficulty;
                p1Char ??= sessionP1Char;
                p2Char ??= sessionP2Char;
                p1Control = TrackerDb.Int32OrNull(r, 7);
                p2Control = TrackerDb.Int32OrNull(r, 8);
            });
        }

        string? linkNote = null;
        if (inferredSession && haveSession
            && !ReplayListQuery.SessionUsable(
                   scanLinked,
                   mode == ModeMatch ? ReplaySection.Match : ReplaySection.StoryExtra,
                   replayDifficulty, replayP1Char, replayP2Char,
                   sessionDifficulty, sessionP1Char, sessionP2Char))
        {
            linkNote = "このリプレイに付いている紐付き（session "
                       + sid!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                       + "）は更新時刻からの推測で、キャラか難易度が食い違います"
                       + "（別の対戦の記録である可能性が高い）。"
                       + "そのセッション側の値（ラウンド表・時間）は出していません。";
            sid = null;
            haveSession = false;
            p1Control = null;
            p2Control = null;
            difficulty = replayDifficulty;
            p1Char = replayP1Char;
            p2Char = replayP2Char;
        }

        if (!haveReplay && !haveSession)
            return new ReplayDetail
            {
                Status = "その記録が本体 DB にありません（"
                         + (rid is long a ? "replay " + a : "replay なし") + " / "
                         + (sid is long b ? "session " + b : "session なし") + "）。",
            };

        var section = mode == ModeMatch ? ReplaySection.Match : ReplaySection.StoryExtra;
        var own = ReplayListQuery.ResolveOwnSide(isOwn, ownerSide);
        var (playedAt, hasTime) = ReplayListQuery.ResolvePlayedAt(replayDate, mtime);

        var rows = new List<ReplayDetailRow>();
        var counted = 0;
        long totalFrames = 0;
        var anyFrames = false;

        if (sid is long s1)
        {
            var stages = LoadStages(db, s1);
            var rounds = LoadRounds(db, s1);
            var folded = FoldCounters(db, s1, rounds);
            var quick = FoldQuick(folded, LoadQuickMarks(layer0Path, s1));

            foreach (var (round, index) in WithIndex(rounds))
            {
                var stage = round.StageRecordId is long k && stages.TryGetValue(k, out var st) ? st : null;
                var last = IsLastOfStage(rounds, index);
                var counter = folded is null ? null : folded.Rows[index];
                if (counter is not null) counted++;
                if (round.Frames is int fr) { totalFrames += fr; anyFrames = true; }

                long? end;
                long? segment = null;
                if (section == ReplaySection.Match)
                {
                    end = round.Score1AtEnd;
                }
                else
                {
                    end = last ? stage?.ScoreAtEnd : rounds[index + 1].Score1AtStart;
                    if (end is long e && round.Score1AtStart is long s) segment = e - s;
                }

                rows.Add(new ReplayDetailRow
                {
                    Section = section,
                    RoundRecordId = round.RoundRecordId,
                    StageRecordId = round.StageRecordId,
                    StageNumber = stage?.StageNumber,
                    RoundNumber = round.RoundNumber,
                    Opponent = stage?.Opponent,
                    FieldId = stage?.FieldId,
                    BattleBgmId = stage?.BattleBgmId,
                    EndScore = end,
                    SegmentScore = segment,
                    ClearBonus = section == ReplaySection.Match || !last ? null : stage?.ClearBonus,
                    Lives = section == ReplaySection.Match ? null : round.Lives1AtEnd,
                    Life1Raw = round.Life1RawAtEnd,
                    Life2Raw = round.Life2RawAtEnd,
                    Frames = round.Frames,
                    WinnerSide = round.WinnerSide,
                    P1 = WithQuick(counter?.P1 ?? SideCounters.Unknown, quick, index, 1),
                    P2 = WithQuick(counter?.P2 ?? SideCounters.Unknown, quick, index, 2),
                });
            }
        }

        var header = new ReplayDetailHeader
        {
            ReplayId = rid,
            SessionId = sid,
            Section = section,
            Mode = mode,
            Difficulty = difficulty,
            PlayedAt = playedAt,
            PlayedAtHasTime = hasTime,
            P1Character = p1Char,
            P2Character = p2Char,
            P1Name = ReplayListQuery.ResolveSideName(section, 1, p1NameRaw, playerName, own, p1Control),
            P2Name = ReplayListQuery.ResolveSideName(section, 2, p2NameRaw, playerName, own, p2Control),
            MatchMode = ReplayListQuery.ResolveMatchMode(decodedJson, p1Control, p2Control),
            RoundCount = rows.Count,
            TotalFrames = anyFrames ? totalFrames : null,
        };

        return new ReplayDetail
        {
            Header = header,
            Rows = rows,
            LinkCount = links.Count,
            CountedRounds = counted,
            Status = linkNote ?? (sid is null
                ? "このリプレイに結び付いたセッションがないので、ラウンド表は出せません。"
                : rows.Count == 0
                    ? "このセッションにラウンドの記録がありません。"
                    : null),
        };
    }

    public static List<ReplayFileRow> LoadFiles(AnalysisDb db, long? replayId, long? sessionId)
    {
        var rows = new List<ReplayFileRow>();
        var (sql, arg) = sessionId is long sid ? (SqlFiles, sid)
                       : replayId is long rid ? (SqlFilesByReplay, rid)
                       : (null, 0L);
        if (sql is null) return rows;

        ForEachRow(db, sql, [arg], r => rows.Add(new ReplayFileRow
        {
            ReplayId = r.GetInt64(0),
            FullPath = TrackerDb.StringOrNull(r, 1),
            Source = TrackerDb.StringOrNull(r, 2),
            IsCurrent = TrackerDb.Int32OrNull(r, 3),
            Confidence = TrackerDb.DoubleOrNull(r, 4),
            DecodeStatus = TrackerDb.StringOrNull(r, 5),
        }));
        return rows;
    }


    private sealed record StageRow(long StageRecordId, int? StageNumber, int? Opponent,
                                   long? ScoreAtStart, long? ScoreAtEnd, double? LivesAtEnd,
                                   int? WinnerSide, string? Status, long? ClearBonus,
                                   int? FieldId, int? BattleBgmId);

    private sealed record RoundRow(long RoundRecordId, long? StageRecordId, int? RoundNumber,
                                   int? Frames, int? WinnerSide, string? Status,
                                   long? Score1AtStart, long? Score1AtEnd, long? Score2AtEnd,
                                   int? Life1RawAtEnd, int? Life2RawAtEnd, double? Lives1AtEnd);

    private sealed record RoundCounters(SideCounters P1, SideCounters P2);

    private sealed record Folded(RoundCounters?[] Rows, List<int>[] SpellRounds, List<int>[] BossRounds);

    private static Dictionary<long, StageRow> LoadStages(AnalysisDb db, long sessionId)
    {
        var map = new Dictionary<long, StageRow>();
        ForEachRow(db, SqlStages, [sessionId], r =>
        {
            var id = r.GetInt64(0);
            map[id] = new StageRow(id,
                                   TrackerDb.Int32OrNull(r, 1),
                                   TrackerDb.Int32OrNull(r, 2),
                                   TrackerDb.Int64OrNull(r, 3),
                                   TrackerDb.Int64OrNull(r, 4),
                                   TrackerDb.DoubleOrNull(r, 5),
                                   TrackerDb.Int32OrNull(r, 6),
                                   TrackerDb.StringOrNull(r, 7),
                                   TrackerDb.Int64OrNull(r, 8),
                                   TrackerDb.Int32OrNull(r, 9),
                                   TrackerDb.Int32OrNull(r, 10));
        });
        return map;
    }

    private static List<RoundRow> LoadRounds(AnalysisDb db, long sessionId)
    {
        var list = new List<RoundRow>();
        ForEachRow(db, SqlRounds, [sessionId], r =>
            list.Add(new RoundRow(r.GetInt64(0),
                                  TrackerDb.Int64OrNull(r, 1),
                                  TrackerDb.Int32OrNull(r, 2),
                                  TrackerDb.Int32OrNull(r, 3),
                                  TrackerDb.Int32OrNull(r, 4),
                                  TrackerDb.StringOrNull(r, 5),
                                  TrackerDb.Int64OrNull(r, 6),
                                  TrackerDb.Int64OrNull(r, 7),
                                  TrackerDb.Int64OrNull(r, 8),
                                  TrackerDb.Int32OrNull(r, 9),
                                  TrackerDb.Int32OrNull(r, 10),
                                  TrackerDb.DoubleOrNull(r, 11))));
        return list;
    }

    private static Folded? FoldCounters(AnalysisDb db, long sessionId, List<RoundRow> rounds)
    {
        var buckets = new List<int[]>();
        var current = NewBucket();
        var any = false;
        var spellRounds = new List<int>[] { [], [] };
        var bossRounds = new List<int>[] { [], [] };
        var roundIndex = 0;

        ForEachRow(db, SqlCounterEvents, [sessionId], r =>
        {
            var type = r.GetString(1);
            if (string.Equals(type, EventRoundCompleted, StringComparison.Ordinal))
            {
                buckets.Add(current);
                current = NewBucket();
                roundIndex++;
                return;
            }
            var side = TrackerDb.Int32OrNull(r, 2);
            if (side is not (1 or 2)) return;
            var kind = type switch
            {
                EventSpell => 0,
                EventBoss => 1,
                EventReversal => 2,
                _ => -1,
            };
            if (kind < 0) return;
            if (kind == 0) spellRounds[side.Value - 1].Add(roundIndex);
            if (kind == 1) bossRounds[side.Value - 1].Add(roundIndex);
            current[(side.Value - 1) * 3 + kind]++;
            any = true;
        });

        if (current.Any(v => v != 0)) return null;
        if (!any && buckets.Count == 0) return null;
        if (buckets.Count != rounds.Count) return null;

        var outRows = new RoundCounters?[rounds.Count];
        for (var i = 0; i < rounds.Count; i++)
        {
            var b = buckets[i];
            outRows[i] = new RoundCounters(
                new SideCounters(b[0], null, null, b[1], b[2]),
                new SideCounters(b[3], null, null, b[4], b[5]));
        }
        return new Folded(outRows, spellRounds, bossRounds);
    }

    private static int[] NewBucket() => new int[6];

    private static QuickTable? FoldQuick(Folded? folded, QuickCards.SessionMarks? marks)
    {
        if (folded is null || marks is null) return null;
        var rounds = folded.Rows.Length;
        for (var side = 1; side <= 2; side++)
        {
            if (marks.Side(side).Count != folded.SpellRounds[side - 1].Count) return null;
            if (marks.BossSide(side).Count != folded.BossRounds[side - 1].Count) return null;
        }

        var card = FoldOne(rounds, marks.Side, folded.SpellRounds);
        var boss = FoldOne(rounds, marks.BossSide, folded.BossRounds);
        if (card is null || boss is null) return null;
        return new QuickTable(card, boss);
    }

    private static int?[,]? FoldOne(int rounds, Func<int, IReadOnlyList<bool?>> marksOf,
                                    List<int>[] where)
    {
        var outCells = new int?[rounds, 2];
        for (var i = 0; i < rounds; i++)
        {
            outCells[i, 0] = 0;
            outCells[i, 1] = 0;
        }
        for (var side = 1; side <= 2; side++)
        {
            var mark = marksOf(side);
            var at = where[side - 1];
            for (var k = 0; k < mark.Count; k++)
            {
                var round = at[k];
                if (round < 0 || round >= rounds) return null;
                if (mark[k] is not bool quick)
                {
                    outCells[round, side - 1] = null;
                    continue;
                }
                if (quick && outCells[round, side - 1] is int n) outCells[round, side - 1] = n + 1;
            }
        }
        return outCells;
    }

    private sealed record QuickTable(int?[,] Card, int?[,] Boss);

    private static SideCounters WithQuick(SideCounters counters, QuickTable? quick, int round, int side)
    {
        if (quick is null || round < 0 || round >= quick.Card.GetLength(0)) return counters;
        if (counters.Spell is null || counters.Boss is null) return counters;
        return counters with
        {
            QuickSpell = quick.Card[round, side - 1],
            QuickBoss = quick.Boss[round, side - 1],
        };
    }

    private static QuickCards.SessionMarks? LoadQuickMarks(string? layer0Path, long sessionId)
    {
        var path = !string.IsNullOrEmpty(layer0Path) ? layer0Path
                   : OperatingSystem.IsWindows() ? TH09.Record.Paths.Default.Layer0Db
                   : null;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        try
        {
            using var l0 = new AnalysisDb(path);
            if (!l0.HasTable("session_ticks")) return null;
            return QuickCards.Load(l0, sessionId);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool IsLastOfStage(List<RoundRow> rounds, int index)
        => index + 1 >= rounds.Count || rounds[index + 1].StageRecordId != rounds[index].StageRecordId;

    private static IEnumerable<(RoundRow Row, int Index)> WithIndex(List<RoundRow> rounds)
    {
        for (var i = 0; i < rounds.Count; i++) yield return (rounds[i], i);
    }

    private static object Box(long? v) => v is long x ? x : DBNull.Value;

    private static void ForEachRow(AnalysisDb db, string sql, object[] args, Action<SqliteDataReader> onRow)
    {
        using var cmd = db.Command(sql, args);
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) onRow(reader);
    }
}
