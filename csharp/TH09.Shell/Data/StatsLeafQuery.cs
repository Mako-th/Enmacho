using System.Globalization;
using TH09.Analysis;
using TH09.Record;

namespace TH09.Shell.Data;

internal sealed class StatsPlayRow
{
    public required long SessionId { get; init; }

    public required string WhenText { get; init; }

    public required int? Mode { get; init; }

    public required int? Difficulty { get; init; }

    public required int? MyCharacter { get; init; }

    public required bool IsOwn { get; init; }

    public required double? Lives { get; init; }

    public required long? Score { get; init; }

    public required long? Frames { get; init; }

    public required long? SpellPoints { get; init; }
}

internal sealed class StatsPayload
{
    public required IReadOnlyDictionary<StatsSection, List<StatsMatchRow>> Matches { get; init; }

    public required List<StatsStageRow> Stages { get; init; }

    public required List<StatsPlayRow> Plays { get; init; }

    public required IReadOnlyDictionary<long, long> ScanBySession { get; init; }

    public required IReadOnlyList<TH09.Record.ReplaySegmentScore> ReplaySegments { get; init; }

    public required IReadOnlyList<TH09.Record.ReplayFinalScore> ReplayFinals { get; init; }

    public required IReadOnlyList<TH09.Record.StoryPlay> StoryPlays { get; init; }

    public required int SessionCount { get; init; }

    public required IReadOnlyDictionary<StatsSection, int> Skips { get; init; }

    public required string? MyName { get; init; }

    public required IReadOnlyList<(string Name, int Count)> OwnNames { get; init; }

    public required IReadOnlyList<(string Name, int Count)> DroppedNames { get; init; }

    public required int Untagged { get; init; }

    public required int NameUsed { get; init; }

    public required int NameConflict { get; init; }

    public IReadOnlyList<StatsMatchRow> MatchRows(StatsSection section)
        => Matches.TryGetValue(section, out var v) ? v : [];
}

internal static class StatsLeafQuery
{
    public const int MinStageFrames = 300;


    private const string RoundCompleted = "completed";

    public const string SqlRounds = """
        SELECT session_id,stage_record_id,round_record_id,round_number,
               winner_side,duration_frames,lives_1_at_end,status,
               score_1_at_start,score_1_at_end,score_2_at_end,
               life_1_raw_at_end,life_2_raw_at_end
          FROM rounds
         ORDER BY round_record_id
        """;

    public const string SqlRoundMetrics = """
        SELECT round_record_id,p1_spell_points_total,p2_spell_points_total,
               p1_boss_present_ticks,p2_boss_present_ticks
          FROM round_metrics
        """;

    public const string SqlStages = """
        SELECT session_id,stage_record_id,stage_number,opponent_character,
               score_at_start,score_at_end,lives_at_end,winner_side,status,clear_bonus_id
          FROM stages
         ORDER BY stage_record_id
        """;

    public const string SqlClearBonuses = """
        SELECT clear_bonus_id,total_bonus FROM clear_bonuses
        """;

    public const string SqlNetPlay = """
        SELECT session_id,seat FROM session_net_play
        """;


    public const string SqlSessions = """
        SELECT s.session_id sid,s.started_at,s.status,
               m.game_mode,m.difficulty,m.p1_character,m.p2_character,
               m.p1_control,m.p2_control,m.execution_type,
               r.replay_id,r.source,r.p1_name,r.p2_name,r.owner_side,r.own_override
          FROM sessions s JOIN session_metadata m USING(session_id)
          LEFT JOIN session_replays sr USING(session_id)
          LEFT JOIN replays r ON r.replay_id=sr.replay_id
         ORDER BY s.session_id
        """;

    private sealed class RoundRec
    {
        public long SessionId;
        public long? StageRecordId;
        public long RoundRecordId;
        public int? Number;
        public int? WinnerSide;
        public int? Frames;
        public double? LivesAtEnd;
        public string? Status;
        public long? Score1AtStart;
        public long? Score1AtEnd;
        public long? Score2AtEnd;
        public int? Life1AtEnd;
        public int? Life2AtEnd;
    }

    private sealed class StageRec
    {
        public long SessionId;
        public long StageRecordId;
        public int? Number;
        public int? Opponent;
        public long? ScoreAtStart;
        public long? ScoreAtEnd;
        public double? LivesAtEnd;
        public int? WinnerSide;
        public long? ClearBonusId;
    }

    private sealed class MetricRec
    {
        public long? P1Spell;
        public long? P2Spell;
        public long? P1Boss;
        public long? P2Boss;

        public long? Spell(int side) => side == 1 ? P1Spell : P2Spell;
        public long? Boss(int side) => side == 1 ? P1Boss : P2Boss;
    }

    public static StatsPayload LoadAll(AnalysisDb db)
    {
        var roundsBySession = new Dictionary<long, List<RoundRec>>();
        var roundsByStage = new Dictionary<long, List<RoundRec>>();
        TrackerDb.ForEachRow(db, SqlRounds, r =>
        {
            var rec = new RoundRec
            {
                SessionId = r.GetInt64(0),
                StageRecordId = TrackerDb.Int64OrNull(r, 1),
                RoundRecordId = r.GetInt64(2),
                Number = TrackerDb.Int32OrNull(r, 3),
                WinnerSide = TrackerDb.Int32OrNull(r, 4),
                Frames = TrackerDb.Int32OrNull(r, 5),
                LivesAtEnd = TrackerDb.DoubleOrNull(r, 6),
                Status = TrackerDb.StringOrNull(r, 7),
                Score1AtStart = TrackerDb.Int64OrNull(r, 8),
                Score1AtEnd = TrackerDb.Int64OrNull(r, 9),
                Score2AtEnd = TrackerDb.Int64OrNull(r, 10),
                Life1AtEnd = TrackerDb.Int32OrNull(r, 11),
                Life2AtEnd = TrackerDb.Int32OrNull(r, 12),
            };
            Add(roundsBySession, rec.SessionId, rec);
            if (rec.StageRecordId is long srid) Add(roundsByStage, srid, rec);
        });

        var metrics = new Dictionary<long, MetricRec>();
        try
        {
            TrackerDb.ForEachRow(db, SqlRoundMetrics, r => metrics[r.GetInt64(0)] = new MetricRec
            {
                P1Spell = TrackerDb.Int64OrNull(r, 1),
                P2Spell = TrackerDb.Int64OrNull(r, 2),
                P1Boss = TrackerDb.Int64OrNull(r, 3),
                P2Boss = TrackerDb.Int64OrNull(r, 4),
            });
        }
        catch (Exception) { metrics.Clear(); }

        var stagesBySession = new Dictionary<long, List<StageRec>>();
        TrackerDb.ForEachRow(db, SqlStages, r => Add(stagesBySession, r.GetInt64(0), new StageRec
        {
            SessionId = r.GetInt64(0),
            StageRecordId = r.GetInt64(1),
            Number = TrackerDb.Int32OrNull(r, 2),
            Opponent = TrackerDb.Int32OrNull(r, 3),
            ScoreAtStart = TrackerDb.Int64OrNull(r, 4),
            ScoreAtEnd = TrackerDb.Int64OrNull(r, 5),
            LivesAtEnd = TrackerDb.DoubleOrNull(r, 6),
            WinnerSide = TrackerDb.Int32OrNull(r, 7),
            ClearBonusId = TrackerDb.Int64OrNull(r, 9),
        }));

        var bonus = new Dictionary<long, long>();
        try
        {
            TrackerDb.ForEachRow(db, SqlClearBonuses, r =>
            {
                if (TrackerDb.Int64OrNull(r, 1) is long v) bonus[r.GetInt64(0)] = v;
            });
        }
        catch (Exception) { bonus.Clear(); }

        var seats = new Dictionary<long, int?>();
        try
        {
            TrackerDb.ForEachRow(db, SqlNetPlay, r => seats[r.GetInt64(0)] = TrackerDb.Int32OrNull(r, 1));
        }
        catch (Exception) { seats.Clear(); }

        var (keepNames, dropNames, myName) = OwnNames(db);
        var names = keepNames.Select(x => x.Name).ToList();

        var matches = new Dictionary<StatsSection, List<StatsMatchRow>>
        {
            [StatsSection.MatchCpu] = [],
            [StatsSection.MatchNet] = [],
            [StatsSection.MatchLocal] = [],
        };
        var stageRows = new List<StatsStageRow>();
        var plays = new List<StatsPlayRow>();
        var skips = new Dictionary<StatsSection, int>
        {
            [StatsSection.StoryExtra] = 0,
            [StatsSection.MatchCpu] = 0,
            [StatsSection.MatchNet] = 0,
            [StatsSection.MatchLocal] = 0,
        };
        var sessionCount = 0;
        var nameUsed = 0;
        var nameConflict = 0;

        TrackerDb.ForEachRow(db, SqlSessions, r =>
        {
            sessionCount++;
            var sid = r.GetInt64(0);
            var startedAt = TrackerDb.StringOrNull(r, 1);
            var gameMode = TrackerDb.Int32OrNull(r, 3);
            var difficulty = TrackerDb.Int32OrNull(r, 4);
            var p1Char = TrackerDb.Int32OrNull(r, 5);
            var p2Char = TrackerDb.Int32OrNull(r, 6);
            var p1Control = TrackerDb.Int32OrNull(r, 7);
            var p2Control = TrackerDb.Int32OrNull(r, 8);
            var execType = TrackerDb.StringOrNull(r, 9);
            var replayId = TrackerDb.Int64OrNull(r, 10);
            var source = TrackerDb.StringOrNull(r, 11);
            var p1Name = TrackerDb.StringOrNull(r, 12);
            var p2Name = TrackerDb.StringOrNull(r, 13);
            var ownerSide = TrackerDb.Int32OrNull(r, 14);
            var ownOverride = TrackerDb.Int32OrNull(r, 15);

            var section = Bucket(gameMode, source, execType, p1Control, p2Control);
            seats.TryGetValue(sid, out var seat);
            if (SideByName(p1Name, p2Name, names) is int byName)
            {
                nameUsed++;
                if (ownerSide is 1 or 2 && ownerSide != byName) nameConflict++;
            }
            var (side, isOwn, provisional) =
                SelfSide(ownOverride, ownerSide, p1Name, p2Name, names, replayId, p1Control, p2Control,
                         seat, execType);
            if (side is not int me)
            {
                skips[section]++;
                return;
            }
            var foe = me == 1 ? 2 : 1;

            var rs = roundsBySession.TryGetValue(sid, out var rl) ? rl : [];
            long frames = 0;
            var roundWins = 0;
            var roundLosses = 0;
            long spell = 0, boss = 0;
            var metricsMissing = 0;
            foreach (var x in rs)
            {
                frames += x.Frames ?? 0;
                if (x.WinnerSide == me) roundWins++;
                else if (x.WinnerSide == foe) roundLosses++;
                if (!metrics.TryGetValue(x.RoundRecordId, out var m)) { metricsMissing++; continue; }
                spell += m.Spell(me) ?? 0;
                boss += m.Boss(me) ?? 0;
            }
            long? spellPoints = metricsMissing > 0 ? null : spell;
            long? bossFrames = metricsMissing > 0 ? null : boss;
            long? totalFrames = frames == 0 ? null : frames;

            var whenText = FormatWhen(startedAt);
            var when = ReplayListQuery.ParseMtime(startedAt);
            var myChar = me == 1 ? p1Char : p2Char;
            var foeChar = me == 1 ? p2Char : p1Char;
            var stages = stagesBySession.TryGetValue(sid, out var sl) ? sl : [];

            if (section == StatsSection.StoryExtra)
            {
                long? maxEnd = null;
                foreach (var st in stages)
                {
                    if (st.Number is null) continue;
                    BuildStageRows(stageRows, sid, whenText, when, gameMode, difficulty, myChar,
                                   isOwn, st, roundsByStage, bonus);
                    if (st.ScoreAtEnd is long b && (maxEnd is null || b > maxEnd)) maxEnd = b;
                }
                double? lives = stages.Count > 0 ? stages[^1].LivesAtEnd : null;
                if (lives is null && rs.Count > 0) lives = rs[^1].LivesAtEnd;

                plays.Add(new StatsPlayRow
                {
                    SessionId = sid,
                    WhenText = whenText,
                    Mode = gameMode,
                    Difficulty = difficulty,
                    MyCharacter = myChar,
                    IsOwn = isOwn,
                    Lives = lives,
                    Score = maxEnd,
                    Frames = totalFrames,
                    SpellPoints = spellPoints,
                });
                return;
            }

            long? score = null;
            for (var i = rs.Count - 1; i >= 0; i--)
            {
                var v = me == 1 ? rs[i].Score1AtEnd : rs[i].Score2AtEnd;
                if (v is long got) { score = got; break; }
            }

            var win = stages.Count > 0 ? stages[0].WinnerSide : null;
            matches[section].Add(new StatsMatchRow
            {
                SessionId = sid,
                Section = section,
                WhenText = whenText,
                When = when,
                Difficulty = difficulty,
                MyCharacter = myChar,
                FoeCharacter = foeChar,
                OpponentName = (me == 1 ? p2Name : p1Name) ?? "",
                Win = win == me ? 1 : win is 1 or 2 ? 0 : null,
                Frames = totalFrames,
                RoundWins = roundWins,
                RoundLosses = roundLosses,
                Score = score,
                SpellPoints = spellPoints,
                BossFrames = bossFrames,
                IsOwn = isOwn,
                IsProvisional = provisional,
                Rounds = MakeRounds(rs.Select(x => (x.Number, x.Frames, x.Status, x.WinnerSide,
                                                   x.Life1AtEnd, x.Life2AtEnd)), me, null),
            });
        });

        var bests = TH09.Record.SelfBests.Read(db.Path);

        var story = TH09.Record.StoryRecords.Read(db.Path);

        return new StatsPayload
        {
            Matches = matches,
            Stages = stageRows,
            Plays = plays,
            ScanBySession = bests.ScanBySession,
            ReplaySegments = bests.ReplaySegments,
            ReplayFinals = bests.ReplayFinals,
            StoryPlays = story.Plays,
            SessionCount = sessionCount,
            Skips = skips,
            MyName = myName,
            OwnNames = keepNames,
            DroppedNames = dropNames,
            Untagged = matches.Values.Sum(v => v.Count) + plays.Count,
            NameUsed = nameUsed,
            NameConflict = nameConflict,
        };
    }

    private static void BuildStageRows(List<StatsStageRow> into, long sid, string whenText, DateTime? when,
                                       int? mode, int? difficulty, int? myChar, bool isOwn,
                                       StageRec st, Dictionary<long, List<RoundRec>> roundsByStage,
                                       Dictionary<long, long> bonus)
    {
        var a = st.ScoreAtStart;
        var b = st.ScoreAtEnd;
        long? seg = a is long s0 && b is long s1 && s1 - s0 > 0 ? s1 - s0 : null;
        long? cb = st.ClearBonusId is long id && bonus.TryGetValue(id, out var v) ? v : null;
        var srs = roundsByStage.TryGetValue(st.StageRecordId, out var list) ? list : [];

        var allRounds = MakeRounds(srs.Select(x => (x.Number, x.Frames, x.Status, x.WinnerSide,
                                                   x.Life1AtEnd, x.Life2AtEnd)), 1, null);

        for (var i = 0; i < srs.Count; i++)
        {
            var x = srs[i];
            var last = i == srs.Count - 1;
            var start = x.Score1AtStart;
            var end = last ? b : srs[i + 1].Score1AtStart;
            long? rseg = start is long p && end is long q ? q - p : null;
            into.Add(NewStageRow(sid, whenText, when, mode, difficulty, myChar, isOwn, st,
                                 rseg, x.Frames, end, x.Number, last ? cb : null,
                                 RoundStatus(x.Status), allRounds));
        }
        if (srs.Count == 0)
        {
            into.Add(NewStageRow(sid, whenText, when, mode, difficulty, myChar, isOwn, st,
                                 seg, null, b, null, cb, null, allRounds));
        }
    }

    private static StatsStageRow NewStageRow(long sid, string whenText, DateTime? when,
                                             int? mode, int? difficulty, int? myChar, bool isOwn,
                                             StageRec st, long? segment, int? frames, long? reach,
                                             int? round, long? clearBonus, string? status,
                                             IReadOnlyList<StatsRound> allRounds)
        => new()
        {
            SessionId = sid,
            WhenText = whenText,
            When = when,
            Mode = mode,
            Difficulty = difficulty,
            MyCharacter = myChar,
            IsOwn = isOwn,
            Stage = st.Number,
            FoeCharacter = st.Opponent,
            Round = round,
            Frames = frames,
            Reach = reach,
            Segment = segment,
            ClearBonus = clearBonus,
            Status = status,
            AllRoundsOfStage = allRounds,
        };

    private static List<StatsRound> MakeRounds(
        IEnumerable<(int? Number, int? Frames, string? Status, int? Winner, int? Life1, int? Life2)> src,
        int selfSide, int? current)
    {
        var list = src.Select(x => new StatsRound(x.Number, x.Frames, RoundStatus(x.Status), x.Winner)
        {
            IsCurrent = current is int c && x.Number == c,
            IsSelfWin = x.Winner == selfSide,
            IsFoeWin = x.Winner is 1 or 2 && x.Winner != selfSide,
            Life = selfSide == 1 ? x.Life1 : x.Life2,
        }).ToList();
        list.Sort((p, q) => Order(p.Number).CompareTo(Order(q.Number)));
        for (var i = 0; i < list.Count - 1; i++) list[i] = list[i] with { HasNext = true };
        return list;

        static long Order(int? v) => v ?? 1_000_000_000L;
    }

    private static string? RoundStatus(string? v) => v == RoundCompleted ? null : v;

    public static string FormatWhen(string? startedAt)
    {
        if (string.IsNullOrEmpty(startedAt)) return "";
        var dt = ReplayListQuery.ParseMtime(startedAt);
        return dt is DateTime t
            ? t.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture)
            : startedAt;
    }

    public static StatsSection Bucket(int? gameMode, string? source, string? execType,
                                      int? p1Control, int? p2Control)
        => TH09.Record.SessionSide.Bucket(gameMode, source, execType, p1Control, p2Control) switch
        {
            TH09.Record.SessionBucket.Story => StatsSection.StoryExtra,
            TH09.Record.SessionBucket.Net => StatsSection.MatchNet,
            TH09.Record.SessionBucket.Cpu => StatsSection.MatchCpu,
            _ => StatsSection.MatchLocal,
        };

    public static (int? Side, bool IsOwn, bool Provisional) SelfSide(
        int? ownOverride, int? ownerSide, string? p1Name, string? p2Name, IReadOnlyList<string> names,
        long? replayId, int? p1Control, int? p2Control, int? seat, string? execType)
        => TH09.Record.SessionSide.Of(ownOverride, ownerSide, p1Name, p2Name, names,
                                      replayId, p1Control, p2Control, seat, execType);

    public static int? SideByName(string? p1Name, string? p2Name, IReadOnlyList<string> names)
        => TH09.Record.SessionSide.SideByName(p1Name, p2Name, names);

    public static (List<(string Name, int Count)> Keep, List<(string Name, int Count)> Drop, string? MyName)
        OwnNames(AnalysisDb db)
    {
        ArgumentNullException.ThrowIfNull(db);
        TH09.Record.OwnNames names;
        try
        {
            names = TH09.Record.SessionSide.OwnReplayNames(db.Path);
        }
        catch (Exception)
        {
            return ([], [], null);
        }
        return (names.Keep.Select(x => (x.Name, x.Count)).ToList(),
                names.Drop.Select(x => (x.Name, x.Count)).ToList(),
                names.MyName);
    }

    private static void Add<T>(Dictionary<long, List<T>> map, long key, T value)
    {
        if (!map.TryGetValue(key, out var list)) map[key] = list = [];
        list.Add(value);
    }


    public const string AnyKey = "*";

    public static string AxisKey(StatsAxis axis, StatsMatchRow row) => axis switch
    {
        StatsAxis.Difficulty => Key(row.Difficulty),
        StatsAxis.OpponentName => row.OpponentName,
        StatsAxis.MyCharacter => Key(row.MyCharacter),
        StatsAxis.FoeCharacter => Key(row.FoeCharacter),
        _ => throw new InvalidOperationException("Match に " + axis + " の軸は無い"),
    };

    public static string AxisKey(StatsAxis axis, StatsStageRow row) => axis switch
    {
        StatsAxis.ModeDifficulty => Key(row.Mode) + "," + Key(row.Difficulty),
        StatsAxis.MyCharacter => Key(row.MyCharacter),
        StatsAxis.Stage => Key(row.Stage),
        StatsAxis.FoeCharacter => Key(row.FoeCharacter),
        _ => throw new InvalidOperationException("Story に " + axis + " の軸は無い"),
    };

    public static string AxisKey(StatsAxis axis, StatsPlayRow row) => axis switch
    {
        StatsAxis.ModeDifficulty => Key(row.Mode) + "," + Key(row.Difficulty),
        StatsAxis.MyCharacter => Key(row.MyCharacter),
        _ => throw new InvalidOperationException("プレイ単位に " + axis + " の軸は無い"),
    };

    private static string Key(int? v) => v is int i ? i.ToString(CultureInfo.InvariantCulture) : "null";

    public static string AxisLabel(StatsAxis axis, string key) => axis switch
    {
        StatsAxis.ModeDifficulty => ModeDifficultyLabel(key),
        StatsAxis.Difficulty => StatsLabels.Difficulty(ParseKey(key)),
        StatsAxis.OpponentName => StatsLabels.OpponentName(key),
        StatsAxis.MyCharacter or StatsAxis.FoeCharacter => StatsLabels.Character(ParseKey(key)),
        StatsAxis.Stage => StatsLabels.Stage(ParseKey(key)),
        _ => key,
    };

    private static string ModeDifficultyLabel(string key)
    {
        var parts = key.Split(',');
        return StatsLabels.ModeDifficulty(parts.Length > 0 ? ParseKey(parts[0]) : null,
                                          parts.Length > 1 ? ParseKey(parts[1]) : null);
    }

    private static int? ParseKey(string key)
        => int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    public static List<T> ApplyPath<T>(IEnumerable<T> rows, StatsLevel[] levels,
                                       IReadOnlyList<string> path, Func<StatsAxis, T, string> keyOf)
    {
        var outRows = rows.ToList();
        for (var i = 0; i < path.Count && i < levels.Length; i++)
        {
            if (path[i] == AnyKey) continue;
            var axis = levels[i].Axis;
            var want = path[i];
            outRows = outRows.Where(r => keyOf(axis, r) == want).ToList();
        }
        return outRows;
    }


    public const string MatrixTitle = "全組み合わせ（縦＝自キャラ / 横＝相手キャラ、セルは「勝 / n」）";

    private const string PickSuffix = " ／ 押すと絞り込む（パンくずに積む）";

    private const double CpuTintAlpha = 0.300;

    public static StatsMatrix? BuildMatrix(StatsSection section, IReadOnlyList<StatsMatchRow> rows,
                                           StatsLevel[] levels, IReadOnlyList<string> path,
                                           StatsCharFilter filter)
    {
        if (!StatsSections.IsMatch(section)) return null;
        if (path.Count >= levels.Length - 1) return null;
        if (rows.Count == 0) return null;

        var chars = ReplayLabels.Characters;
        int[] meAxis = [.. ReplayLabels.DisplayOrder.Where(filter.Self.Contains)];
        int[] foAxis = [.. ReplayLabels.DisplayOrder.Where(filter.Foe.Contains)];
        var agg = new Dictionary<(int Me, int Fo), (int N, int W, int L)>();
        var outside = 0;
        foreach (var r in rows)
        {
            if (r.MyCharacter is not int me || r.FoeCharacter is not int fo
                || me < 0 || me >= chars.Length || fo < 0 || fo >= chars.Length)
            {
                outside++;
                continue;
            }
            agg.TryGetValue((me, fo), out var v);
            agg[(me, fo)] = (v.N + 1, v.W + (r.Win == 1 ? 1 : 0), v.L + (r.Win == 0 ? 1 : 0));
        }

        var head = new List<StatsMatrixCell>
        {
            NewCell("", StatsMatrixLayout.HeaderWidth, "", header: true, rowHeader: true,
                    self: false, StatsMatrixTint.None, 0, null),
        };
        foreach (var fo in foAxis)
            head.Add(NewCell(chars[fo], StatsMatrixLayout.CellWidth,
                             "相手キャラ " + chars[fo] + PickSuffix, header: true, rowHeader: false,
                             self: false, StatsMatrixTint.None, 0, PickPath(levels, path, null, fo)));

        var grid = new List<StatsMatrixRow> { new(head, IsHeaderRow: true) };
        foreach (var me in meAxis)
        {
            var line = new List<StatsMatrixCell>
            {
                NewCell(chars[me], StatsMatrixLayout.HeaderWidth,
                        "自キャラ " + chars[me] + PickSuffix, header: true, rowHeader: true,
                        self: false, StatsMatrixTint.None, 0, PickPath(levels, path, me, null)),
            };
            foreach (var fo in foAxis)
            {
                var pair = chars[me] + " → " + chars[fo];
                var pick = PickPath(levels, path, me, fo);
                if (!agg.TryGetValue((me, fo), out var c))
                {
                    line.Add(NewCell(StatsFormat.Missing, StatsMatrixLayout.CellWidth,
                                     pair + "（まだ当たっていない）" + PickSuffix,
                                     header: false, rowHeader: false, self: me == fo,
                                     StatsMatrixTint.None, 0, pick));
                    continue;
                }
                var (tint, alpha) = CellColor(section, c.W, c.L);
                line.Add(NewCell(
                    c.W.ToString(CultureInfo.InvariantCulture) + "/"
                        + c.N.ToString(CultureInfo.InvariantCulture),
                    StatsMatrixLayout.CellWidth,
                    pair + "  " + c.W + " 勝 " + c.L + " 敗 / n=" + c.N
                        + "  勝率 " + StatsFormat.Percent(c.W, c.W + c.L) + PickSuffix,
                    header: false, rowHeader: false, self: me == fo, tint, alpha, pick));
            }
            grid.Add(new StatsMatrixRow(line, IsHeaderRow: false));
        }

        var legend = new List<string> { "—＝まだ当たっていない" };
        legend.Add(section == StatsSection.MatchCpu
            ? "色＝一度でも勝ったことがある組（勝っていない組は未対戦と同じく無色）"
            : "色＝勝率（青いほど勝ち越し / 橙いほど負け越し / 50% 付近は無色）");
        if (section != StatsSection.MatchCpu) legend.Add("勝敗の付いた対戦が無いセルは無色");
        legend.Add("★押すと絞り込む: 行頭＝自キャラ / 列頭＝相手キャラ / "
                   + "セル＝両方（パンくずに積む。飛ばした段は「すべて」と出る）");
        if (outside > 0)
            legend.Add("★キャラが分からない " + outside + " 件は、どの升にも入っていない");

        return new StatsMatrix
        {
            Rows = grid,
            Title = MatrixTitle,
            Legend = legend,
            SourceCount = rows.Count,
            OutsideCount = outside,
        };
    }

    private static StatsMatrixCell NewCell(string text, double width, string tip, bool header,
                                           bool rowHeader, bool self, StatsMatrixTint tint,
                                           double alpha, IReadOnlyList<string>? pick)
        => new()
        {
            Text = text,
            Width = width,
            Tip = tip,
            IsHeader = header,
            IsRowHeader = rowHeader,
            IsSelf = self,
            Tint = tint,
            Alpha = alpha,
            Pick = pick,
        };

    private static (StatsMatrixTint Tint, double Alpha) CellColor(StatsSection section, int w, int l)
    {
        var d = w + l;
        if (d == 0) return (StatsMatrixTint.None, 0);
        if (section == StatsSection.MatchCpu)
            return w > 0 ? (StatsMatrixTint.P1, CpuTintAlpha) : (StatsMatrixTint.None, 0);
        var r = (double)w / d;
        return (r >= 0.5 ? StatsMatrixTint.P1 : StatsMatrixTint.P2,
                0.05 + 0.33 * Math.Abs(r - 0.5) * 2);
    }

    public static List<string>? PickPath(StatsLevel[] levels, IReadOnlyList<string> path,
                                         int? me, int? fo)
    {
        var last = -1;
        for (var i = 0; i < levels.Length; i++)
            if (Want(levels[i].Axis) is not null) last = i;
        if (last < 0) return null;

        var p = new List<string>();
        for (var i = 0; i <= last; i++)
        {
            var v = Want(levels[i].Axis);
            p.Add(v is int x ? x.ToString(CultureInfo.InvariantCulture)
                             : i < path.Count ? path[i] : AnyKey);
        }
        return p;

        int? Want(StatsAxis axis) => axis switch
        {
            StatsAxis.MyCharacter => me,
            StatsAxis.FoeCharacter => fo,
            _ => null,
        };
    }

    public static List<StatsGroup> GroupMatches(IReadOnlyList<StatsMatchRow> rows, StatsAxis axis,
                                                StatsAggColumn[] cols, StatsGroupColumn[] lead)
    {
        var groups = new List<StatsGroup>();
        foreach (var g in rows.GroupBy(r => AxisKey(axis, r), StringComparer.Ordinal))
        {
            var list = g.ToList();
            var g2 = new StatsGroup(
                g.Key, AxisLabel(axis, g.Key), list.Count,
                list.Select(r => r.SessionId).Distinct().Count(),
                list.Count(r => r.Win == 1), list.Count(r => r.Win == 0),
                list.Sum(r => (long)r.RoundWins), list.Sum(r => (long)r.RoundLosses))
            {
                Cells = StatsAggColumns.Cells(cols, MatchValues(list)),
            };
            groups.Add(g2);
        }
        Sort(groups, axis);
        ApplyCushion(groups, lead);
        return groups;
    }

    public static List<StatsGroup> GroupStages(IReadOnlyList<StatsStageRow> rows, StatsAxis axis,
                                               StatsAggColumn[] cols, StatsGroupColumn[] lead,
                                               IReadOnlyList<StatsPlayRow>? plays,
                                               bool withSub = false)
    {
        var playsByKey = plays?.GroupBy(r => AxisKey(axis, r), StringComparer.Ordinal)
                               .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var groups = new List<StatsGroup>();
        foreach (var g in rows.GroupBy(r => AxisKey(axis, r), StringComparer.Ordinal))
        {
            var list = g.ToList();
            var ps = playsByKey is null ? null
                   : playsByKey.TryGetValue(g.Key, out var v) ? v : [];
            var values = ps is null ? StageValues(list) : PlayValues(ps);
            var playCount = ps?.Count ?? list.Select(r => r.SessionId).Distinct().Count();
            var g2 = new StatsGroup(g.Key, AxisLabel(axis, g.Key), list.Count,
                                    playCount, 0, 0, 0, 0)
            {
                Cells = StatsAggColumns.Cells(cols, values),
                Subs = withSub ? SubGroups(list, g.Key, cols, lead) : [],
            };
            groups.Add(g2);
        }
        Sort(groups, axis);
        ApplyCushion(groups, lead);
        return groups;
    }

    private static List<StatsGroup> SubGroups(List<StatsStageRow> rows, string stageKey,
                                              StatsAggColumn[] cols, StatsGroupColumn[] lead)
    {
        var subs = new List<StatsGroup>();
        foreach (var g in rows.GroupBy(
                     r => AxisKey(StatsAxis.FoeCharacter, r) + "/" + RoundKey(r),
                     StringComparer.Ordinal))
        {
            var list = g.ToList();
            var label = SubIndent
                + AxisLabel(StatsAxis.FoeCharacter, AxisKey(StatsAxis.FoeCharacter, list[0]))
                + "/" + StatsLabels.Round(list[0].Round);
            var sub = new StatsGroup(g.Key, label, list.Count,
                                     list.Select(r => r.SessionId).Distinct().Count(), 0, 0, 0, 0)
            {
                Cells = StatsAggColumns.Cells(cols, StageValues(list)),
                IsSub = true,
                Steps = [stageKey, AxisKey(StatsAxis.FoeCharacter, list[0])],
            };
            subs.Add(sub);
        }
        subs = [.. subs.OrderByDescending(s2 => s2.Count)];
        for (var i = 0; i < subs.Count; i++)
            subs[i] = subs[i] with { Lead = StatsGroupColumns.Cells(lead, subs[i].Text) };
        return subs;
    }

    public const string SubIndent = "　";

    private static string RoundKey(StatsStageRow r)
        => r.Round?.ToString(CultureInfo.InvariantCulture) ?? "null";

    private static StatsAggValues MatchValues(IReadOnlyList<StatsMatchRow> rows)
    {
        var v = new StatsAggValues();
        foreach (var r in rows)
        {
            v.Add(StatsAggValue.Score, r.Score);
            v.Add(StatsAggValue.Frames, r.Frames);
            foreach (var x in r.Rounds)
            {
                v.Add(StatsAggValue.RoundFrames, x.Frames);
                v.Add(StatsAggValue.Life, x.Life);
            }
        }
        return v;
    }

    private static StatsAggValues PlayValues(IReadOnlyList<StatsPlayRow> rows)
    {
        var v = new StatsAggValues();
        foreach (var r in rows)
        {
            v.Add(StatsAggValue.Lives, r.Lives);
            v.Add(StatsAggValue.Score, r.Score);
            v.Add(StatsAggValue.Frames, r.Frames);
        }
        return v;
    }

    private static StatsAggValues StageValues(IReadOnlyList<StatsStageRow> rows)
    {
        var v = new StatsAggValues();
        foreach (var r in rows)
        {
            v.Add(StatsAggValue.Reach, r.Reach);
            v.Add(StatsAggValue.ClearBonus, r.ClearBonus);
            v.Add(StatsAggValue.ReachExBonus, r.ReachExBonus);
            v.Add(StatsAggValue.Segment, r.Segment);
            v.Add(StatsAggValue.SegmentExBonus, r.SegmentExBonus);
            v.Add(StatsAggValue.Frames, r.Frames);
        }
        return v;
    }

    private static void Sort(List<StatsGroup> groups, StatsAxis axis)
    {
        var byChar = axis is StatsAxis.MyCharacter or StatsAxis.FoeCharacter;
        var byValue = byChar || axis is StatsAxis.Stage or StatsAxis.Difficulty
                                    or StatsAxis.ModeDifficulty;
        groups.Sort((a, b) => byValue
            ? Order(a.Key).CompareTo(Order(b.Key))
            : b.Count != a.Count ? b.Count.CompareTo(a.Count) : string.CompareOrdinal(a.Key, b.Key));

        long Order(string key)
            => byChar ? CharOrder(key) : KeyOrder(key);

        static long CharOrder(string key)
            => int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
               && ReplayLabels.DisplayRank(id) is int rank ? rank : 999;

        static long KeyOrder(string key)
        {
            var head = key.Split(',');
            long v = 0;
            for (var i = 0; i < head.Length; i++)
                v = v * 1000 + (int.TryParse(head[i], NumberStyles.Integer, CultureInfo.InvariantCulture,
                                             out var n) ? n : 999);
            return v;
        }
    }

    public static List<IStatsLeafRow> SortLeaf(List<IStatsLeafRow> rows, StatsLeafColumn[] cols)
    {
        StatsSort.Leaf(rows, DefaultLeafSort, descending: true, cols);
        return rows;
    }

    public const StatsLeafField DefaultLeafSort = StatsLeafField.When;


    public const string ProvisionalCardKey = "★うち仮確定（自分側が未確定）";

    public static List<StatsCardGroup> MatchCards(IReadOnlyList<StatsMatchRow> rows,
                                                  string longestRoundSource = "")
    {
        var w = rows.Count(r => r.Win == 1);
        var l = rows.Count(r => r.Win == 0);
        var unknown = rows.Count - w - l;
        long rw = rows.Sum(r => (long)r.RoundWins), rl = rows.Sum(r => (long)r.RoundLosses);
        var fs = rows.Where(r => r.Frames is not null).Select(r => (double)r.Frames!.Value).ToList();
        var rfs = rows.SelectMany(r => r.Rounds).Where(x => x.Frames is not null)
                      .Select(x => (double)x.Frames!.Value).ToList();
        var sc = rows.Where(r => r.Score is not null).Select(r => r.Score!.Value).ToList();
        var spOk = rows.Where(r => r.SpellPoints is not null).Select(r => (double)r.SpellPoints!.Value).ToList();
        var bpOk = rows.Where(r => r.BossFrames is not null).Select(r => (double)r.BossFrames!.Value).ToList();
        var provisional = rows.Count(r => r.IsProvisional);

        var groups = new List<StatsCardGroup>
        {
            new([
                new StatsCard("対戦回数", StatsFormat.Number(rows.Count), unknown > 0 ? "勝敗不明 " + unknown : ""),
                new StatsCard("勝 / 負", w + " / " + l, ""),
                new StatsCard("勝率（マッチ）", StatsFormat.Percent(w, w + l), ""),
                new StatsCard("勝率（ラウンド）", StatsFormat.Percent(rw, rw + rl), rw + " / " + (rw + rl)),
            ]),
            new([
                new StatsCard("平均マッチ時間", StatsFormat.AverageFrames(Avg(fs)), "n=" + fs.Count),
                new StatsCard("ラウンド最長時間", StatsFormat.AverageFrames(Max(rfs)), "n=" + rfs.Count)
                    { Source = longestRoundSource },
                new StatsCard("ラウンド平均時間", StatsFormat.AverageFrames(Avg(rfs)), ""),
                new StatsCard("ラウンド最短時間", StatsFormat.AverageFrames(Min(rfs)), ""),
            ]),
            new([
                new StatsCard("最高スコア", StatsFormat.Number(sc.Count > 0 ? sc.Max() : null), "n=" + sc.Count),
            ]),
        };
        var reference = new List<StatsCard>
        {
            new("SP 由来スコア（1 対戦平均）", StatsFormat.RoundedNumber(Avg(spOk)),
                rows.Count - spOk.Count > 0 ? "未集計 " + (rows.Count - spOk.Count) : ""),
            new("自陣にボスが居た時間（平均）", StatsFormat.AverageFrames(Avg(bpOk)),
                rows.Count - bpOk.Count > 0 ? "未集計 " + (rows.Count - bpOk.Count) : ""),
        };
        if (provisional > 0)
            reference.Add(new StatsCard(ProvisionalCardKey, StatsFormat.Number(provisional), "1P と置いた"));
        groups.Add(new StatsCardGroup(reference));
        return groups;
    }

    public static List<StatsCardGroup> StoryPlayCards(IReadOnlyList<StatsPlayRow> rows,
                                                      string finalScoreSource = "")
    {
        var sc = rows.Where(r => r.Score is not null).Select(r => (double)r.Score!.Value).ToList();
        var fs = rows.Where(r => r.Frames is not null).Select(r => (double)r.Frames!.Value).ToList();
        var lv = rows.Where(r => r.Lives is not null).Select(r => r.Lives!.Value).ToList();
        var spOk = rows.Where(r => r.SpellPoints is not null).Select(r => (double)r.SpellPoints!.Value).ToList();

        return
        [
            new([new StatsCard("プレイ回数", StatsFormat.Number(rows.Count), "")]),
            new([
                new StatsCard("最高残機", StatsFormat.Lives(Max(lv)), "n=" + lv.Count),
                new StatsCard("最高最終スコア", StatsFormat.RoundedNumber(Max(sc)), "n=" + sc.Count)
                    { Source = finalScoreSource },
                new StatsCard("最長プレイ時間", StatsFormat.AverageFrames(Max(fs)), "n=" + fs.Count),
            ]),
            new([
                new StatsCard("平均残機", StatsFormat.Lives(Avg(lv)), ""),
                new StatsCard("平均最終スコア", StatsFormat.RoundedNumber(Avg(sc)), ""),
                new StatsCard("平均プレイ時間", StatsFormat.AverageFrames(Avg(fs)), ""),
            ]),
            new([
                new StatsCard("最低残機", StatsFormat.Lives(Min(lv)), ""),
                new StatsCard("最低最終スコア", StatsFormat.RoundedNumber(Min(sc)), ""),
                new StatsCard("最短プレイ時間", StatsFormat.AverageFrames(Min(fs)), ""),
            ]),
            new([
                new StatsCard("SP 由来スコア（1 プレイ平均）", StatsFormat.RoundedNumber(Avg(spOk)),
                              rows.Count - spOk.Count > 0 ? "未集計 " + (rows.Count - spOk.Count) : ""),
            ]),
        ];
    }

    public static List<StatsCardGroup> StoryStageCards(IReadOnlyList<StatsStageRow> rows,
                                                       string reachSource = "")
    {
        var rs = rows.Where(r => r.Reach is not null).Select(r => (double)r.Reach!.Value).ToList();
        var sc = rows.Where(r => r.Segment is not null).Select(r => r.Segment!.Value).ToList();
        var fs = rows.Where(r => r.Frames is not null).Select(r => (double)r.Frames!.Value).ToList();
        var cb = rows.Where(r => r.ClearBonus is not null).Select(r => r.ClearBonus!.Value).ToList();
        var fast = Min(fs.Where(v => v >= MinStageFrames).ToList());
        var name = StatsLeafColumns.ReachName;

        return
        [
            new([new StatsCard("ラウンドの記録 n", StatsFormat.Number(rows.Count),
                               "プレイ " + rows.Select(r => r.SessionId).Distinct().Count())]),
            new([
                new StatsCard(name + "の最高（＝その地点での自己ベスト）", StatsFormat.RoundedNumber(Max(rs)),
                              "n=" + rs.Count) { Source = reachSource },
                new StatsCard(name + "の平均", StatsFormat.RoundedNumber(Avg(rs)), ""),
                new StatsCard(name + "の最低", StatsFormat.RoundedNumber(Min(rs)), ""),
            ]),
            new([
                new StatsCard("ラウンドの最長時間", StatsFormat.AverageFrames(Max(fs)), "n=" + fs.Count),
                new StatsCard("ラウンドの平均時間", StatsFormat.AverageFrames(Avg(fs)), ""),
                new StatsCard("ラウンドの最短時間", StatsFormat.AverageFrames(fast),
                              "≥" + MinStageFrames + "f のみ"),
            ]),
            new([
                new StatsCard("区間スコアの合計", sc.Count > 0 ? StatsFormat.Number(sc.Sum()) : StatsFormat.Missing,
                              "n=" + sc.Count),
                new StatsCard("うちクリアボーナスの合計",
                              cb.Count > 0 ? StatsFormat.Number(cb.Sum()) : StatsFormat.Missing,
                              "n=" + cb.Count),
            ]),
        ];
    }

    private static void ApplyCushion(List<StatsGroup> groups, StatsGroupColumn[] lead)
    {
        var max = 0;
        foreach (var g in groups) max = Math.Max(max, g.Count);
        for (var i = 0; i < groups.Count; i++)
        {
            var a = StatsCushion.Alpha(groups[i].Count, max);
            groups[i] = groups[i] with
            {
                Cushion = a,
                Lead = StatsGroupColumns.Cells(lead, groups[i].Text, a),
            };
        }
    }

    private static double? Avg(IReadOnlyList<double> xs)
        => xs.Count == 0 ? null : xs.Sum() / xs.Count;

    private static double? Max(IReadOnlyList<double> xs) => xs.Count == 0 ? null : xs.Max();

    private static double? Min(IReadOnlyList<double> xs) => xs.Count == 0 ? null : xs.Min();
}

internal static class StatsNotes
{
    public static string Sessions(StatsPayload p)
    {
        static string N(int v) => v.ToString(CultureInfo.InvariantCulture);
        return "セッション " + N(p.SessionCount) + " 本"
             + " / ネット対戦 " + N(p.MatchRows(StatsSection.MatchNet).Count)
             + " ・ Story/Extra " + N(p.Plays.Count)
             + " ・ 対 CPU " + N(p.MatchRows(StatsSection.MatchCpu).Count)
             + " ・ ローカル対人 " + N(p.MatchRows(StatsSection.MatchLocal).Count)
             + "（本体 DB だけを読んでいる。Layer 0 は開かない）";
    }

    public static string Counts(StatsPayload p)
    {
        static string N(int v) => v.ToString("N0", CultureInfo.InvariantCulture);
        var total = p.Skips.Values.Sum();
        var parts = StatsSections.All.Where(s => p.Skips[s] > 0)
            .Select(s => StatsSections.Key(s) + " "
                         + p.Skips[s].ToString(CultureInfo.InvariantCulture));
        return "自分側が決められず集計から外した記録 " + N(total) + " 件"
             + (total > 0 ? "（内訳: " + string.Join(" / ", parts) + "）" : "")
             + " ／ 名前で自分側を決めた " + N(p.NameUsed)
             + " 件・そのうち replays.owner_side と食い違い " + N(p.NameConflict) + " 件";
    }
}

internal sealed class StatsCharFilter
{
    public static int CharacterCount => ReplayLabels.Characters.Length;

    public static StatsCharFilter All { get; } = new(AllIds(), AllIds());

    private static HashSet<int> AllIds() => [.. Enumerable.Range(0, CharacterCount)];

    private readonly HashSet<int> _self;
    private readonly HashSet<int> _foe;

    public StatsCharFilter(IEnumerable<int> self, IEnumerable<int> foe)
    {
        _self = [.. self];
        _foe = [.. foe];
    }

    public IReadOnlySet<int> Self => _self;

    public IReadOnlySet<int> Foe => _foe;

    public bool IsAll => _self.Count == CharacterCount && _foe.Count == CharacterCount;

    public bool KeepsSelf(int? me) => me is int i && _self.Contains(i);

    public bool Keeps(int? me, int? foe)
        => KeepsSelf(me) && (foe is not int f || _foe.Contains(f));
}

internal sealed class StatsView
{
    public StatsView(StatsPayload payload, StatsSection section, IReadOnlyList<string> path,
                     bool foreign, StatsCharFilter chars)
    {
        Payload = payload;
        Section = section;
        Levels = StatsLevels.For(section);
        Path = path.Count > Levels.Length ? path.Take(Levels.Length).ToList() : path;
        Foreign = foreign;
        Chars = chars;

        if (section == StatsSection.StoryExtra)
        {
            SectionCount = payload.Stages.Count;
            var baseRows = payload.Stages
                .Where(r => (foreign || r.IsOwn) && chars.Keeps(r.MyCharacter, r.FoeCharacter))
                .ToList();
            BaseCount = baseRows.Count;
            var rows = StatsLeafQuery.ApplyPath(baseRows, Levels, Path, StatsLeafQuery.AxisKey);
            StageRows = rows;
            MatchRows = [];
            Plays = StatsLeafQuery.ApplyPath(
                payload.Plays.Where(r => (foreign || r.IsOwn) && chars.KeepsSelf(r.MyCharacter))
                             .ToList(),
                Levels, Path.Take(2).ToList(), StatsLeafQuery.AxisKey);
        }
        else
        {
            var all = payload.MatchRows(section);
            SectionCount = all.Count;
            var baseRows = all
                .Where(r => (foreign || r.IsOwn) && chars.Keeps(r.MyCharacter, r.FoeCharacter))
                .ToList();
            BaseCount = baseRows.Count;
            MatchRows = StatsLeafQuery.ApplyPath(baseRows, Levels, Path, StatsLeafQuery.AxisKey);
            StageRows = [];
            Plays = [];
        }
    }

    public StatsPayload Payload { get; }

    public StatsSection Section { get; }

    public StatsLevel[] Levels { get; }

    public IReadOnlyList<string> Path { get; }

    public bool Foreign { get; }

    public StatsCharFilter Chars { get; }

    public int SectionCount { get; }

    public int BaseCount { get; }

    public List<StatsStageRow> StageRows { get; }

    public List<StatsMatchRow> MatchRows { get; }

    public List<StatsPlayRow> Plays { get; }

    public int RowCount => Section == StatsSection.StoryExtra ? StageRows.Count : MatchRows.Count;

    public bool IsLeaf => Path.Count >= Levels.Length;

    public StatsLevel? NextLevel => IsLeaf ? null : Levels[Path.Count];

    public StatsLeafColumn[] Columns => StatsLeafColumns.For(Section);

    public List<StatsCardGroup> Cards()
    {
        var cards = Section != StatsSection.StoryExtra
            ? StatsLeafQuery.MatchCards(MatchRows, LongestRoundSource())
            : Path.Count >= 2 ? StatsLeafQuery.StoryStageCards(StageRows, ReachSource())
                              : StatsLeafQuery.StoryPlayCards(Plays, FinalScoreSource());
        return StatsCardLayout.Apply(cards, OrderOf(cards: true), HiddenOf(cards: true));
    }

    private string FinalScoreSource()
    {
        long? bestVal = null;
        long? bestSid = null;
        foreach (var r in Plays)
        {
            if (r.Score is not long v) continue;
            if (bestVal is null || v > bestVal) { bestVal = v; bestSid = r.SessionId; }
        }
        var live = bestSid is long sid
            ? TH09.Record.BestSource.OfSession(sid, Payload.ScanBySession) : null;
        var (repSrc, repVal) = StatsBestSource.BestReplayFinal(
            Payload.ReplayFinals, Levels, [.. Path.Take(2)], Foreign, Chars);
        if (repVal is not null && bestVal is not null && repVal <= bestVal) (repSrc, repVal) = (null, null);
        return StatsBestSource.Line(live, repSrc, repVal, final: true);
    }

    private string ReachSource()
    {
        long? bestVal = null;
        long? bestSid = null;
        foreach (var r in StageRows)
        {
            if (r.Reach is not long v) continue;
            if (bestVal is null || v > bestVal) { bestVal = v; bestSid = r.SessionId; }
        }
        var live = bestSid is long sid
            ? TH09.Record.BestSource.OfSession(sid, Payload.ScanBySession) : null;
        var (repSrc, repVal) = StatsBestSource.BestReplayReach(
            Payload.ReplaySegments, Levels, Path, Foreign, Chars);
        if (repVal is not null && bestVal is not null && repVal <= bestVal) (repSrc, repVal) = (null, null);
        return StatsBestSource.Line(live, repSrc, repVal, final: false);
    }

    private string LongestRoundSource()
    {
        long? bestVal = null;
        long? bestSid = null;
        foreach (var r in MatchRows)
        {
            foreach (var x in r.Rounds)
            {
                if (x.Frames is not int f) continue;
                if (bestVal is null || f > bestVal) { bestVal = f; bestSid = r.SessionId; }
            }
        }
        var live = bestSid is long sid
            ? TH09.Record.BestSource.OfSession(sid, Payload.ScanBySession) : null;
        return StatsBestSource.Line(live, null, null, final: false);
    }

    private StatsMatrix? _matrix;
    private bool _matrixBuilt;

    public StatsMatrix? Matrix()
    {
        if (_matrixBuilt) return _matrix;
        _matrixBuilt = true;
        _matrix = StatsLeafQuery.BuildMatrix(Section, MatchRows, Levels, Path, Chars);
        return _matrix;
    }

    private bool _recordsBuilt;
    private StatsRecordsTable? _records;

    public StatsRecordsTable? Records()
    {
        if (_recordsBuilt) return _records;
        _recordsBuilt = true;
        if (Section != StatsSection.StoryExtra || Path.Count > 0) return _records = null;
        var own = new Dictionary<long, bool>(Payload.Plays.Count);
        foreach (var p in Payload.Plays) own[p.SessionId] = p.IsOwn;
        return _records = StatsRecords.Build(Payload.StoryPlays, own, Foreign, Chars);
    }

    private StatsOrderMap? _order;
    private StatsOrderMap? _hidden;
    private StatsLayoutResult? _layout, _layoutGain;

    public void UseLayout(StatsOrderMap? order, StatsOrderMap? hidden)
    {
        _order = order;
        _hidden = hidden;
        _layout = null;
        _layoutGain = null;
    }

    private StatsLayoutPart PartOf(bool cards) => StatsLayout.PartOf(Section, Path.Count, cards);

    private IReadOnlyList<string> OrderOf(bool cards)
    {
        var part = PartOf(cards);
        return _order?.Of(part.PageKey, part.Key) ?? [];
    }

    private IReadOnlyList<string> HiddenOf(bool cards)
    {
        var part = PartOf(cards);
        return _hidden?.Of(part.PageKey, part.Key) ?? [];
    }

    private StatsLayoutResult Layout
        => _layout ??= StatsLayout.Apply(StatsGroupColumns.For(Section),
                                         StatsAggColumns.For(Section, Path.Count),
                                         OrderOf(cards: false), HiddenOf(cards: false));

    private StatsLayoutResult LayoutGain
        => _layoutGain ??= StatsLayout.Apply(StatsGroupColumns.For(Section),
                                             StatsAggColumns.Gain(Section, Path.Count),
                                             OrderOf(cards: false), HiddenOf(cards: false));

    public StatsAggColumn[] GroupColumns
        => IsLeaf ? [] : [.. Layout.Agg.Select(x => x.Column)];

    public StatsGroupColumn[] LeadColumns => IsLeaf ? [] : [.. LeadLayout.Select(x => x.Column)];

    public IReadOnlyList<StatsLayoutLead> LeadLayout => IsLeaf ? [] : Layout.Lead;

    public StatsAggColumn[] GroupColumnsGain
        => IsLeaf ? [] : [.. LayoutGain.Agg.Select(x => x.Column)];

    public bool HasGain => GroupColumnsGain.Length > 0;

    public bool HasSub => NextLevel?.Axis == StatsAxis.Stage;

    public string GroupCaption
        => HasGain ? StatsLeafColumns.ReachName + "と時間（" + AxisName + "別）" : "";

    public string GainCaption
        => HasGain ? "その面で稼いだスコア＝" + StatsLeafColumns.SegmentName
                     + "（" + AxisName + "別）" : "";

    private string AxisName => NextLevel?.Title ?? "";

    public List<StatsGroup> GroupsGain()
    {
        if (!HasGain || NextLevel is not StatsLevel level) return [];
        return StatsLeafQuery.GroupStages(StageRows, level.Axis, GroupColumnsGain, LeadColumns, null,
                                          HasSub);
    }

    public List<StatsGroup> Groups()
    {
        if (NextLevel is not StatsLevel level) return [];
        var cols = GroupColumns;
        return Section == StatsSection.StoryExtra
            ? StatsLeafQuery.GroupStages(StageRows, level.Axis, cols, LeadColumns,
                                         Path.Count >= 2 ? null : Plays, HasSub)
            : StatsLeafQuery.GroupMatches(MatchRows, level.Axis, cols, LeadColumns);
    }

    public List<IStatsLeafRow> LeafRows()
    {
        if (!IsLeaf) return [];
        return StatsLeafQuery.SortLeaf(
            Section == StatsSection.StoryExtra ? [.. StageRows] : [.. MatchRows], Columns);
    }

    public List<StatsPathLabel> PathLabels()
    {
        var outList = new List<StatsPathLabel>();
        for (var i = 0; i < Path.Count && i < Levels.Length; i++)
        {
            var any = Path[i] == StatsLeafQuery.AnyKey;
            outList.Add(new StatsPathLabel(
                i, Levels[i].Title,
                any ? "すべて" : StatsLeafQuery.AxisLabel(Levels[i].Axis, Path[i]), any));
        }
        return outList;
    }

    public List<StatsCrumb> Crumbs()
    {
        var crumbs = new List<StatsCrumb> { new(0, StatsSections.Title(Section), false) };
        foreach (var x in PathLabels())
            crumbs.Add(new StatsCrumb(x.Index + 1, x.Title + " " + x.Label, false));
        crumbs[^1] = crumbs[^1] with { IsCurrent = true };
        return crumbs;
    }

    public List<StatsFixed> Fixed()
        => [.. PathLabels().Where(x => !x.IsAny && x.Title.Length > 0)
                           .Select(x => new StatsFixed(x.Title, x.Label))];
}

internal readonly record struct StatsPathLabel(int Index, string Title, string Label, bool IsAny);

internal sealed record StatsFixed(string Key, string Value);

internal sealed record StatsCrumb(int Depth, string Label, bool IsCurrent);

internal static class StatsCushion
{
    public const double Floor = 0.06;

    public const double Span = 0.34;

    public static double Alpha(int n, int max)
        => Floor + Span * (max > 0 ? Math.Min(1.0, (double)n / max) : 0.0);

    public static string Spec(double alpha)
        => "background:rgba(var(--cush)," + StatsFormat.Fixed3(alpha) + ")";
}

internal sealed record StatsGroup(string Key, string Label, int Count, int PlayCount,
                                int Wins, int Losses, long RoundWins, long RoundLosses)
{
    public IReadOnlyList<StatsAggCell> Cells { get; init; } = [];

    public string WinRate => StatsFormat.Percent(Wins, Wins + Losses);

    public string RoundWinRate => StatsFormat.Percent(RoundWins, RoundWins + RoundLosses);

    public string CountText => Count.ToString("N0", CultureInfo.InvariantCulture);

    public string PlayCountText => PlayCount.ToString("N0", CultureInfo.InvariantCulture);

    public IReadOnlyList<ReplayCell> Lead { get; init; } = [];

    public double Cushion { get; init; }

    public IReadOnlyList<string> Steps { get; init; } = [];

    public IReadOnlyList<string> Drill => Steps.Count > 0 ? Steps : [Key];

    public IReadOnlyList<StatsGroup> Subs { get; init; } = [];

    public bool IsSub { get; init; }

    public bool IsBandOdd { get; init; }

    public string CushionSpec => StatsCushion.Spec(Cushion);

    public string Text(StatsGroupField f) => f switch
    {
        StatsGroupField.Name => Label,
        StatsGroupField.Count => CountText,
        StatsGroupField.PlayCount => PlayCountText,
        StatsGroupField.Wins => Wins.ToString(CultureInfo.InvariantCulture),
        StatsGroupField.Losses => Losses.ToString(CultureInfo.InvariantCulture),
        StatsGroupField.WinRate => WinRate,
        StatsGroupField.RoundWinRate => RoundWinRate,
        _ => throw new InvalidOperationException("内訳表に " + f + " の升目が書かれていない"),
    };

    public double? Number(StatsGroupField f) => f switch
    {
        StatsGroupField.Name => null,
        StatsGroupField.Count => Count,
        StatsGroupField.PlayCount => PlayCount,
        StatsGroupField.Wins => Wins,
        StatsGroupField.Losses => Losses,
        StatsGroupField.WinRate => Wins + Losses > 0 ? (double)Wins / (Wins + Losses) : null,
        StatsGroupField.RoundWinRate => RoundWins + RoundLosses > 0
            ? (double)RoundWins / (RoundWins + RoundLosses) : null,
        _ => throw new InvalidOperationException("内訳表に " + f + " の並べ替えが書かれていない"),
    };
}


internal sealed record StatsGroupSortKey(StatsGroupField? Lead,
                                         StatsAggValue? Value = null,
                                         StatsAggStat? Stat = null);

internal static class StatsSort
{
    public static int IndexOf(StatsAggColumn[] cols, StatsAggValue value, StatsAggStat stat)
        => Array.FindIndex(cols, c => c.Value == value && c.Stat == stat);

    public static bool CanSort(StatsGroupSortKey key, StatsAggColumn[] cols,
                               StatsGroupColumn[] lead)
    {
        if (key.Lead is StatsGroupField f) return Array.Exists(lead, c => c.Field == f);
        if (key.Value is not StatsAggValue v || key.Stat is not StatsAggStat s) return false;
        return IndexOf(cols, v, s) >= 0;
    }

    public static void Groups(List<StatsGroup> rows, StatsGroupSortKey key, bool descending,
                              StatsAggColumn[] cols, StatsAxis? axis = null)
    {
        if (key.Lead is StatsGroupField f)
        {
            if (f == StatsGroupField.Name)
            {
                if (axis is StatsAxis.MyCharacter or StatsAxis.FoeCharacter)
                    Apply(rows, r => CharRank(r.Key), r => r.Label, descending);
                else Apply(rows, r => null, r => r.Label, descending);
            }
            else Apply(rows, r => r.Number(f), r => r.Text(f), descending);
            return;
        }
        if (key.Value is not StatsAggValue v || key.Stat is not StatsAggStat s) return;
        var at = IndexOf(cols, v, s);
        if (at < 0) throw new InvalidOperationException("いまの内訳表に無い集計列で並べようとした");
        Apply(rows, r => at < r.Cells.Count ? r.Cells[at].Value : null,
              r => at < r.Cells.Count ? r.Cells[at].Text : "", descending);
    }

    private static double? CharRank(string key)
        => int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
           ? ReplayLabels.DisplayRank(id) : null;

    public static void Leaf(List<IStatsLeafRow> rows, StatsLeafField field, bool descending,
                            StatsLeafColumn[] cols)
    {
        var at = Array.FindIndex(cols, c => c.Field == field);
        if (at < 0) throw new InvalidOperationException("いまの葉に無い列で並べようとした: " + field);
        Apply(rows, r => r.SortNumber(field), r => at < r.Cells.Count ? r.Cells[at].Text : "",
              descending);
    }

    private static void Apply<T>(List<T> rows, Func<T, double?> number, Func<T, string> text,
                                 bool descending)
    {
        var numeric = rows.Exists(r => number(r) is not null);
        var indexed = rows.Select((r, i) => (Row: r, Index: i)).ToList();
        indexed.Sort((a, b) =>
        {
            int c;
            if (numeric)
            {
                var x = number(a.Row);
                var y = number(b.Row);
                if (x is null && y is null) c = 0;
                else if (x is null) return 1;
                else if (y is null) return -1;
                else c = x.Value.CompareTo(y.Value);
            }
            else c = string.CompareOrdinal(text(a.Row), text(b.Row));
            if (c == 0) c = a.Index.CompareTo(b.Index);
            return descending ? -c : c;
        });
        rows.Clear();
        rows.AddRange(indexed.Select(x => x.Row));
    }
}
