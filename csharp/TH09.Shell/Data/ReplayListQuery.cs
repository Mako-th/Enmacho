using System.Globalization;
using TH09.Analysis;

namespace TH09.Shell.Data;

internal sealed record ReplayListFilter(
    ReplaySection Section,
    bool OwnOnly = false,
    int? Difficulty = null,
    int? AnyCharacter = null,
    int? P1Character = null,
    int? P2Character = null,
    MatchMode? Mode = null,
    string? PlayerName = null);

internal static class ReplayListQuery
{
    public const string SqlReplays = """
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
               (SELECT MIN(sr.session_id) FROM session_replays sr WHERE sr.replay_id = r.replay_id) AS session_id,
               (SELECT COUNT(*)           FROM session_replays sr WHERE sr.replay_id = r.replay_id) AS session_count,
               sm.p1_control,
               sm.p2_control,
               r.decode_status,
               r.source,
               --: ★★置き場は Python の Repository.replay_list() と★同じ副問い合わせ
               --:   （is_current → replay_path_id の順で 1 本）。★別の選び方を作らない（ここ 1 か所に揃える）
               (SELECT full_path FROM replay_paths p WHERE p.replay_id = r.replay_id
                 ORDER BY p.is_current DESC, p.replay_path_id DESC LIMIT 1) AS full_path,
               --: 人が覆した所有。列としては出さないが、左端の印と「自分のみ」には効く
               --:   （原本 self_side の優先順位 1 と同じ倒し方。ResolveOwnSide の但し書き）。
               --: 一番後ろに足してある —— 途中へ入れると、読み出しの添字が全部ずれる。
               r.own_override,
               --: ★★ここから 4 つは【2026-09-13】。★同じく一番後ろへ足すこと。
               --:   ★`decoded_json` …… リプレイ自身が持つ 4 分類（面 9 / 19 の `ai`）。
               --:   ★残り 3 つ …… ★<b>その紐付きが「同じ対戦」かを確かめる材料</b>
               --:     （★推測で入れた紐付きを信じてよいかの判定。SessionUsable）。
               --: ★★別名を付けてある ——★<c>r.difficulty</c> と <c>sm.difficulty</c> は
               --:   ★<b>素で書くと列名がぶつかる</b>（C# は添字で読むので気づかないが、
               --:   ★<b>この SQL をそのまま流す検査は後勝ちで 1 つを失う</b>）。
               r.decoded_json,
               sm.p1_character AS session_p1_character,
               sm.p2_character AS session_p2_character,
               sm.difficulty   AS session_difficulty
          FROM replays r
          LEFT JOIN session_metadata sm
                 ON sm.session_id = (SELECT MIN(sr.session_id) FROM session_replays sr WHERE sr.replay_id = r.replay_id)
         ORDER BY r.replay_id
        """;

    public const string SqlStages = """
        SELECT st.session_id,
               st.stage_record_id,
               st.stage_number,
               st.status,
               st.winner_side,
               st.lives_at_end,
               st.score_at_end
          FROM stages st
         ORDER BY st.session_id, st.stage_number, st.stage_record_id
        """;

    public const string SqlRounds = """
        SELECT ro.session_id,
               ro.stage_record_id,
               ro.round_number,
               ro.duration_frames,
               ro.winner_side
          FROM rounds ro
         ORDER BY ro.session_id, ro.round_record_id
        """;

    private const int ModeMatch = 2;

    private const int FinalStageNumber = 9;

    private const int ControlCpu = 1;

    private sealed class StageFold
    {
        public long? MaxScore;
        public int? LastStageNumber;
        public string? LastStageStatus;
        public int? LastStageWinner;
        public double? LastLives;
        public readonly Dictionary<long, int> StageNumberById = [];
    }

    private sealed class RoundFold
    {
        public long TotalFrames;
        public bool AnyFrames;
        public long? LastStageRecordId;
        public int? LastRoundNumber;
        public readonly List<RoundTime> Rounds = [];
    }

    public static List<ReplayListRow> LoadAll(AnalysisDb db)
    {
        var stages = FoldStages(db);
        var rounds = FoldRounds(db);
        var scanLinks = RecordKinds.ReadScanLinks(db);
        var rows = new List<ReplayListRow>();

        TrackerDb.ForEachRow(db, SqlReplays, r =>
        {
            var replayId = r.GetInt64(0);
            var mode = TrackerDb.Int32OrNull(r, 1);
            var difficulty = TrackerDb.Int32OrNull(r, 2);
            var playerName = TrackerDb.StringOrNull(r, 3);
            var replayDate = TrackerDb.StringOrNull(r, 4);
            var mtime = TrackerDb.StringOrNull(r, 5);
            var p1Char = TrackerDb.Int32OrNull(r, 6);
            var p2Char = TrackerDb.Int32OrNull(r, 7);
            var p1NameRaw = TrackerDb.StringOrNull(r, 8);
            var p2NameRaw = TrackerDb.StringOrNull(r, 9);
            var isOwn = TrackerDb.Int32OrNull(r, 10);
            var ownerSideRaw = TrackerDb.Int32OrNull(r, 11);
            var sessionId = TrackerDb.Int64OrNull(r, 12);
            var sessionCount = r.GetInt32(13);
            var p1Control = TrackerDb.Int32OrNull(r, 14);
            var p2Control = TrackerDb.Int32OrNull(r, 15);
            var decodeStatus = TrackerDb.StringOrNull(r, 16);
            var source = TrackerDb.StringOrNull(r, 17);
            var fullPath = TrackerDb.StringOrNull(r, 18);
            var ownOverride = TrackerDb.Int32OrNull(r, 19);
            var decodedJson = TrackerDb.StringOrNull(r, 20);
            var sessionP1Char = TrackerDb.Int32OrNull(r, 21);
            var sessionP2Char = TrackerDb.Int32OrNull(r, 22);
            var sessionDifficulty = TrackerDb.Int32OrNull(r, 23);

            var section = mode == ModeMatch ? ReplaySection.Match : ReplaySection.StoryExtra;
            var own = ResolveOwnSide(isOwn, ownerSideRaw, ownOverride);

            var scanLinked = sessionId is long slid
                             && scanLinks.Pairs.Contains((slid, replayId));
            var useSession = sessionId is not null
                             && SessionUsable(scanLinked, section, difficulty, p1Char, p2Char,
                                              sessionDifficulty, sessionP1Char, sessionP2Char);
            if (!useSession) sessionId = null;

            var matchMode = ResolveMatchMode(decodedJson,
                                             useSession ? p1Control : null,
                                             useSession ? p2Control : null);
            var (playedAt, hasTime) = ResolvePlayedAt(replayDate, mtime);

            StageFold? sf = sessionId is long sid && stages.TryGetValue(sid, out var s) ? s : null;
            RoundFold? rf = sessionId is long sid2 && rounds.TryGetValue(sid2, out var f) ? f : null;

            int? reachStage = null;
            if (rf?.LastStageRecordId is long srid && sf is not null
                && sf.StageNumberById.TryGetValue(srid, out var num)) reachStage = num;
            reachStage ??= sf?.LastStageNumber;

            var isStory = section == ReplaySection.StoryExtra;

            rows.Add(new ReplayListRow
            {
                ReplayId = replayId,
                SessionId = sessionId,
                SessionCount = sessionCount,
                Section = section,
                Mode = mode,
                Difficulty = difficulty,
                PlayedAt = playedAt,
                PlayedAtHasTime = hasTime,
                P1Character = p1Char,
                P2Character = p2Char,
                P1Name = ResolveSideName(section, 1, p1NameRaw, playerName, own,
                                         useSession ? p1Control : null),
                P2Name = ResolveSideName(section, 2, p2NameRaw, playerName, own,
                                         useSession ? p2Control : null),
                Own = own,
                MatchMode = matchMode,
                FinalScore = isStory ? sf?.MaxScore : null,
                Lives = isStory ? sf?.LastLives : null,
                ReachStage = isStory ? reachStage : null,
                ReachRound = isStory ? rf?.LastRoundNumber : null,
                TotalFrames = rf is not null && rf.AnyFrames ? rf.TotalFrames : null,
                Completed = isStory ? ResolveCompleted(sf) : null,
                Rounds = section == ReplaySection.Match && rf is not null ? rf.Rounds : [],
                DecodeStatus = decodeStatus,
                Source = source,
                FullPath = fullPath,
                OwnOverride = ownOverride,
                Kind = RecordKinds.Of(scanLinks.Replays.Contains(replayId), hasReplay: true),
            });
        });

        return rows;
    }

    public static OwnSide ResolveOwnSide(int? isOwn, int? ownerSide, int? ownOverride = null)
        => ownOverride is int over
            ? (over is 1 or 2 ? (OwnSide)over : OwnSide.None)
            : (isOwn == 1 && ownerSide is 1 or 2 ? (OwnSide)ownerSide.Value : OwnSide.None);

    public static bool SessionUsable(bool scanLinked, ReplaySection section,
                                     int? replayDifficulty, int? replayP1, int? replayP2,
                                     int? sessionDifficulty, int? sessionP1, int? sessionP2)
    {
        if (scanLinked) return true;
        if (sessionDifficulty is null || sessionP1 is null) return false;
        if (replayDifficulty != sessionDifficulty) return false;
        if (replayP1 != sessionP1) return false;
        return section != ReplaySection.Match || replayP2 == sessionP2;
    }

    public static MatchMode ResolveMatchMode(string? decodedJson, int? p1Control, int? p2Control)
        => TH09.Record.ReplayStages.MatchSidesOf(decodedJson) is { } sides
               ? sides switch
               {
                   TH09.Record.MatchSides.HumanVsHuman => MatchMode.HumanVsHuman,
                   TH09.Record.MatchSides.HumanVsCpu => MatchMode.HumanVsCpu,
                   TH09.Record.MatchSides.CpuVsHuman => MatchMode.CpuVsHuman,
                   TH09.Record.MatchSides.CpuVsCpu => MatchMode.CpuVsCpu,
                   _ => MatchMode.Unknown,
               }
               : ResolveMatchMode(p1Control, p2Control);

    public static MatchMode ResolveMatchMode(int? p1Control, int? p2Control)
    {
        if (p1Control is not int c1 || p2Control is not int c2) return MatchMode.Unknown;
        var cpu1 = c1 == ControlCpu;
        var cpu2 = c2 == ControlCpu;
        return (cpu1, cpu2) switch
        {
            (false, false) => MatchMode.HumanVsHuman,
            (false, true) => MatchMode.HumanVsCpu,
            (true, false) => MatchMode.CpuVsHuman,
            (true, true) => MatchMode.CpuVsCpu,
        };
    }

    public static string? ResolveSideName(ReplaySection section, int side,
                                          string? sideName, string? playerName,
                                          OwnSide own, int? control)
    {
        var named = Blank(playerName) ? null : (LooksLikeSaveTime(playerName!) ? null : playerName);
        if (section == ReplaySection.StoryExtra) return side == 1 ? named : null;
        if (!Blank(sideName)) return sideName;
        if (control == ControlCpu) return ReplayLabels.CpuName;
        if ((int)own == side) return named;
        return null;
    }

    public static (DateTime? PlayedAt, bool HasTime) ResolvePlayedAt(string? replayDate, string? mtime)
    {
        var date = ParseReplayDate(replayDate);
        var file = ParseMtime(mtime);

        if (date is DateTime d && file is DateTime m)
        {
            var gap = Math.Abs((m.Date - d.Date).TotalDays);
            return gap <= 1.0 ? (m, true) : (d, false);
        }
        if (date is DateTime only) return (only, false);
        if (file is DateTime f) return (f, true);
        return (null, false);
    }

    public static DateTime? ParseReplayDate(string? value)
    {
        if (Blank(value)) return null;
        return DateTime.TryParseExact(value, "yy/MM/dd", CultureInfo.InvariantCulture,
                                      DateTimeStyles.None, out var d) ? d : null;
    }

    public static DateTime? ParseMtime(string? value)
    {
        if (Blank(value)) return null;
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out var o) ? o.DateTime : null;
    }

    private static bool? ResolveCompleted(StageFold? sf)
    {
        if (sf?.LastStageNumber is not int last) return null;
        return last == FinalStageNumber
               && string.Equals(sf.LastStageStatus, "completed", StringComparison.Ordinal)
               && sf.LastStageWinner == 1;
    }

    private static Dictionary<long, StageFold> FoldStages(AnalysisDb db)
    {
        var map = new Dictionary<long, StageFold>();
        TrackerDb.ForEachRow(db, SqlStages, r =>
        {
            var sid = r.GetInt64(0);
            if (!map.TryGetValue(sid, out var f)) map[sid] = f = new StageFold();
            var stageRecordId = r.GetInt64(1);
            var stageNumber = TrackerDb.Int32OrNull(r, 2);
            var status = TrackerDb.StringOrNull(r, 3);
            var winner = TrackerDb.Int32OrNull(r, 4);
            var lives = TrackerDb.DoubleOrNull(r, 5);
            var score = TrackerDb.Int64OrNull(r, 6);

            if (stageNumber is int n) f.StageNumberById[stageRecordId] = n;
            if (score is long v && (f.MaxScore is null || v > f.MaxScore)) f.MaxScore = v;
            f.LastStageNumber = stageNumber;
            f.LastStageStatus = status;
            f.LastStageWinner = winner;
            f.LastLives = lives;
        });
        return map;
    }

    private static Dictionary<long, RoundFold> FoldRounds(AnalysisDb db)
    {
        var map = new Dictionary<long, RoundFold>();
        TrackerDb.ForEachRow(db, SqlRounds, r =>
        {
            var sid = r.GetInt64(0);
            if (!map.TryGetValue(sid, out var f)) map[sid] = f = new RoundFold();
            var stageRecordId = TrackerDb.Int64OrNull(r, 1);
            var roundNumber = TrackerDb.Int32OrNull(r, 2);
            var frames = TrackerDb.Int32OrNull(r, 3);
            var winner = TrackerDb.Int32OrNull(r, 4);

            if (frames is int fr) { f.TotalFrames += fr; f.AnyFrames = true; }
            if (stageRecordId is long srid) f.LastStageRecordId = srid;
            f.LastRoundNumber = roundNumber;
            f.Rounds.Add(new RoundTime(frames, winner));
        });
        foreach (var f in map.Values)
            for (var i = 0; i < f.Rounds.Count - 1; i++)
                f.Rounds[i] = f.Rounds[i] with { HasNext = true };
        return map;
    }

    public static List<ReplayListRow> Filter(IEnumerable<ReplayListRow> rows, ReplayListFilter f)
    {
        var outRows = new List<ReplayListRow>();
        foreach (var row in rows)
        {
            if (row.Section != f.Section) continue;
            if (f.OwnOnly && !row.IsOwn) continue;
            if (f.Difficulty is int d && row.Difficulty != d) continue;
            if (f.Mode is MatchMode m && row.MatchMode != m) continue;
            if (f.AnyCharacter is int any && row.P1Character != any && row.P2Character != any) continue;
            if (f.P1Character is int p1 && row.P1Character != p1) continue;
            if (f.P2Character is int p2 && row.P2Character != p2) continue;
            if (!Blank(f.PlayerName)
                && !string.Equals(row.P1Name, f.PlayerName, StringComparison.Ordinal)
                && !string.Equals(row.P2Name, f.PlayerName, StringComparison.Ordinal)) continue;
            outRows.Add(row);
        }
        return outRows;
    }

    public static void Sort(List<ReplayListRow> rows, ReplaySortKey key, bool descending)
    {
        rows.Sort((a, b) =>
        {
            var c = CompareBy(a, b, key, descending);
            return c != 0 ? c : b.ReplayId.CompareTo(a.ReplayId);
        });
    }

    private static int CompareBy(ReplayListRow a, ReplayListRow b, ReplaySortKey key, bool desc) => key switch
    {
        ReplaySortKey.DateTime => Cmp(a.PlayedAt, b.PlayedAt, desc),
        ReplaySortKey.Difficulty => Cmp(a.Difficulty, b.Difficulty, desc),
        ReplaySortKey.P1Character => Cmp(ReplayLabels.DisplayRank(a.P1Character),
                                         ReplayLabels.DisplayRank(b.P1Character), desc),
        ReplaySortKey.P2Character => Cmp(ReplayLabels.DisplayRank(a.P2Character),
                                         ReplayLabels.DisplayRank(b.P2Character), desc),
        ReplaySortKey.P1Name => CmpText(a.P1Name, b.P1Name, desc),
        ReplaySortKey.P2Name => CmpText(a.P2Name, b.P2Name, desc),
        ReplaySortKey.Score => Cmp(a.FinalScore, b.FinalScore, desc),
        ReplaySortKey.Lives => Cmp(a.Lives, b.Lives, desc),
        ReplaySortKey.Reach => Cmp(a.ReachOrder, b.ReachOrder, desc),
        ReplaySortKey.Time => Cmp(a.TotalFrames, b.TotalFrames, desc),
        ReplaySortKey.Result => Cmp(a.Completed, b.Completed, desc),
        ReplaySortKey.OwnSide => Cmp<int>((int)a.Own, (int)b.Own, desc),
        ReplaySortKey.Kind => Cmp<int>((int)a.Kind, (int)b.Kind, desc),
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
        if (Blank(a)) return Blank(b) ? 0 : 1;
        if (Blank(b)) return -1;
        var c = string.CompareOrdinal(a, b);
        return desc ? -c : c;
    }

    public static bool LooksLikeSaveTime(string value)
    {
        var i = 0;
        var digits = 0;
        while (i < value.Length && char.IsAsciiDigit(value[i])) { i++; digits++; }
        if (digits is < 1 or > 2) return false;
        for (var group = 0; group < 2; group++)
        {
            if (i >= value.Length || value[i] != ':') return false;
            i++;
            if (i + 1 >= value.Length) return false;
            if (!char.IsAsciiDigit(value[i]) || !char.IsAsciiDigit(value[i + 1])) return false;
            i += 2;
        }
        return i == value.Length;
    }

    private static bool Blank(string? s) => string.IsNullOrWhiteSpace(s);
}
