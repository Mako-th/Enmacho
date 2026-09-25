using System.Globalization;
using System.Text;

namespace TH09.Shell.Data;


internal sealed record PlayProgress(IReadOnlyList<PlayTextLine> Rows, string? StatusText, long? SessionId)
{
    public IReadOnlyList<string> Lines => [.. Rows.Select(r => r.Text)];

    public string Text => string.Join("\n", Lines);
}

internal sealed record PlayInsertBlock(long? Stage, long? Round, IReadOnlyList<PlayTextLine> Lines);

internal static class PlayLiveQuery
{
    private const string SessionSql = """
        SELECT s.session_id, s.status, m.game_mode, m.difficulty,
               m.p1_character, m.p2_character, m.execution_type
          FROM sessions s
          LEFT JOIN session_metadata m USING(session_id)
         ORDER BY s.session_id DESC LIMIT 1
        """;

    private const string StagesSql = """
        SELECT stage_record_id, stage_number, opponent_character, score_at_end, clear_bonus_id
          FROM stages WHERE session_id=$0 ORDER BY stage_record_id
        """;

    private const string RoundsSql = """
        SELECT stage_record_id, status, duration_frames, winner_side,
               score_1_at_start, life_1_raw_at_end, life_2_raw_at_end
          FROM rounds WHERE session_id=$0 ORDER BY round_record_id
        """;

    private const string BonusSql = """
        SELECT clear_bonus_id, stage_index, total_bonus, score_after_bonus
          FROM clear_bonuses WHERE session_id=$0 ORDER BY clear_bonus_id
        """;

    private const string ReplaySql = """
        SELECT p.full_path
          FROM session_replays sr JOIN replay_paths p USING(replay_id)
         WHERE sr.session_id=$0
         ORDER BY p.is_current DESC, p.replay_path_id DESC LIMIT 1
        """;

    public const int RuleWidth = 66;

    public const int IndentWidth = 11;

    public const int NarrowRuleWidth = 30;

    public const int NarrowIndentWidth = 3;

    public const int NumberColumnWidth = 3;

    public const string WaitingText = "Session待機中...";

    public static PlayProgress Load(TH09.Analysis.AnalysisDb db, int dots = 0, bool narrow = false,
                                    Func<long, IReadOnlyList<PlayInsertBlock>>? blockLookup = null)
    {
        long? sid = null;
        long? mode = null, difficulty = null, p1 = null, p2 = null;
        string status = "", execution = "";
        TrackerDb.ForEachRow(db, SessionSql, r =>
        {
            sid = r.GetInt64(0);
            status = r.GetString(1);
            mode = TrackerDb.Int64OrNull(r, 2);
            difficulty = TrackerDb.Int64OrNull(r, 3);
            p1 = TrackerDb.Int64OrNull(r, 4);
            p2 = TrackerDb.Int64OrNull(r, 5);
            execution = TrackerDb.StringOrNull(r, 6) ?? "";
        });
        if (sid is not long id)
            return new PlayProgress([new PlayTextLine(PlaySpan.Body(WaitingText))], null, null);

        var stages = new List<StageRow>();
        var rounds = new List<RoundRow>();
        var bonuses = new List<BonusRow>();
        string? replayPath = null;
        Query(db, StagesSql, id, r => stages.Add(new StageRow(
            r.GetInt64(0), TrackerDb.Int64OrNull(r, 1), TrackerDb.Int64OrNull(r, 2),
            TrackerDb.Int64OrNull(r, 3), TrackerDb.Int64OrNull(r, 4))));
        Query(db, RoundsSql, id, r => rounds.Add(new RoundRow(
            TrackerDb.Int64OrNull(r, 0), r.GetString(1), TrackerDb.Int64OrNull(r, 2),
            TrackerDb.Int64OrNull(r, 3), TrackerDb.Int64OrNull(r, 4),
            TrackerDb.Int64OrNull(r, 5), TrackerDb.Int64OrNull(r, 6))));
        Query(db, BonusSql, id, r => bonuses.Add(new BonusRow(
            r.GetInt64(0), TrackerDb.Int64OrNull(r, 1),
            TrackerDb.Int64OrNull(r, 2), TrackerDb.Int64OrNull(r, 3))));
        Query(db, ReplaySql, id, r => replayPath = TrackerDb.StringOrNull(r, 0));

        var blocks = blockLookup?.Invoke(id) ?? [];
        return new PlayProgress(
            ComposeRows(mode, difficulty, p1, p2, status, execution, stages, rounds, bonuses, replayPath,
                        dots, narrow, blocks),
            null, id);
    }

    private static void Query(TH09.Analysis.AnalysisDb db, string sql, long sid,
                              Action<Microsoft.Data.Sqlite.SqliteDataReader> onRow)
    {
        using var cmd = db.Command(sql, sid);
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) onRow(reader);
    }

    internal sealed record StageRow(long StageRecordId, long? StageNumber, long? Opponent,
                                    long? ScoreAtEnd, long? ClearBonusId);

    internal sealed record RoundRow(long? StageRecordId, string Status, long? DurationFrames,
                                    long? WinnerSide, long? Score1AtStart,
                                    long? Life1RawAtEnd, long? Life2RawAtEnd);

    internal sealed record BonusRow(long ClearBonusId, long? StageIndex,
                                    long? TotalBonus, long? ScoreAfterBonus);

    public static IReadOnlyList<string> Compose(
        long? mode, long? difficulty, long? p1Character, long? p2Character,
        string status, string executionType,
        IReadOnlyList<StageRow> stages, IReadOnlyList<RoundRow> rounds,
        IReadOnlyList<BonusRow> bonuses, string? replayFullPath, int dots, bool narrow = false,
        IReadOnlyList<PlayInsertBlock>? blocks = null)
        => [.. ComposeRows(mode, difficulty, p1Character, p2Character, status, executionType,
                           stages, rounds, bonuses, replayFullPath, dots, narrow, blocks)
                  .Select(r => r.Text)];

    public static IReadOnlyList<PlayTextLine> ComposeRows(
        long? mode, long? difficulty, long? p1Character, long? p2Character,
        string status, string executionType,
        IReadOnlyList<StageRow> stages, IReadOnlyList<RoundRow> rounds,
        IReadOnlyList<BonusRow> bonuses, string? replayFullPath, int dots, bool narrow = false,
        IReadOnlyList<PlayInsertBlock>? blocks = null)
    {
        var rule = narrow ? NarrowRuleWidth : RuleWidth;
        var lines = new List<PlayTextLine>();
        void Add(params PlaySpan[] spans) => lines.Add(new PlayTextLine(spans));

        Add(PlaySpan.Title(
            LiveFormat.ModeOrUnknown(mode) + " / " + LiveFormat.DifficultyOrUnknown(difficulty)
            + " / " + (executionType.Length > 0 ? executionType : "Live Play")));
        if (mode == 2)
            Add(PlaySpan.Label("1P: "), PlaySpan.Value(ReplayLabels.Character(ToInt(p1Character))),
                PlaySpan.Label("    2P: "), PlaySpan.Value(ReplayLabels.Character(ToInt(p2Character))));
        else
            Add(PlaySpan.Label("Player: "), PlaySpan.Value(ReplayLabels.Character(ToInt(p1Character))));
        Add(PlaySpan.Separator(new string('═', rule)));

        var byId = new Dictionary<long, BonusRow>();
        var byStage = new Dictionary<long, BonusRow>();
        foreach (var b in bonuses)
        {
            byId[b.ClearBonusId] = b;
            if (b.StageIndex is long si) byStage[si + 1] = b;
        }

        var pad = new string(' ', narrow ? NarrowIndentWidth : IndentWidth);
        var blockByKey = new Dictionary<(long? Stage, long? Round), IReadOnlyList<PlayTextLine>>();
        if (blocks is not null)
            foreach (var b in blocks) blockByKey[(b.Stage, b.Round)] = b.Lines;
        var blockPad = narrow ? "" : pad;
        void InsertBlock(long? stageKey, long? roundKey)
        {
            if (blockByKey.TryGetValue((stageKey, roundKey), out var blockLines))
                foreach (var bl in blockLines) lines.Add(bl.Indent(blockPad));
        }

        long checkpoint = 0;
        foreach (var stage in stages)
        {
            if (mode != 2)
                Add(PlaySpan.Label("Stage "),
                    PlaySpan.Value(Ljust(Str(stage.StageNumber), NumberColumnWidth)),
                    PlaySpan.Label(" 相手: "),
                    PlaySpan.Value(ReplayLabels.Character(ToInt(stage.Opponent))));

            var stageRounds = rounds.Where(r => r.StageRecordId == stage.StageRecordId).ToList();
            BonusRow? bonus = null;
            if (stage.ClearBonusId is long cbid) byId.TryGetValue(cbid, out bonus);
            if (bonus is null && stage.StageNumber is long sn) byStage.TryGetValue(sn, out bonus);

            for (var index = 0; index < stageRounds.Count; index++)
            {
                var r = stageRounds[index];
                var number = index + 1;
                if (r.Status == "running")
                {
                    Add(PlaySpan.Label("Round "),
                        PlaySpan.Value(Ljust(number.ToString(CultureInfo.InvariantCulture), NumberColumnWidth)),
                        PlaySpan.Value(" Now Playing" + new string('.', Math.Max(0, dots))));
                }
                else if (mode == 2)
                {
                    Add(PlaySpan.Label("Round "),
                        PlaySpan.Value(Ljust(number.ToString(CultureInfo.InvariantCulture), NumberColumnWidth)),
                        PlaySpan.Label(" Time: "),
                        PlaySpan.Value(ReplayFormat.Frames(r.DurationFrames)));
                    var lifeValue = LiveFormat.Life(r.Life1RawAtEnd) + " - " + LiveFormat.Life(r.Life2RawAtEnd);
                    if (narrow)
                    {
                        Add(PlaySpan.Body(pad), PlaySpan.Label("Winner: "),
                            PlaySpan.Value(Str(r.WinnerSide) + "P"));
                        Add(PlaySpan.Body(pad), PlaySpan.Label("life: "), PlaySpan.Value(lifeValue));
                    }
                    else
                    {
                        Add(PlaySpan.Body(pad), PlaySpan.Label("Winner: "),
                            PlaySpan.Value(Str(r.WinnerSide)), PlaySpan.Label("P | "),
                            PlaySpan.Label("life: "), PlaySpan.Value(lifeValue));
                    }
                }
                else
                {
                    Add(PlaySpan.Label("Round "),
                        PlaySpan.Value(Ljust(number.ToString(CultureInfo.InvariantCulture), NumberColumnWidth)),
                        PlaySpan.Label(" Time: "),
                        PlaySpan.Value(ReplayFormat.Frames(r.DurationFrames)));
                    var next = index + 1 < stageRounds.Count ? stageRounds[index + 1] : null;
                    if (next?.Score1AtStart is long roundScore)
                    {
                        Add(PlaySpan.Body(pad), PlaySpan.Label("Score: "),
                            PlaySpan.Value(LiveFormat.Grouped(roundScore)));
                        Add(PlaySpan.Body(pad), PlaySpan.Label("Diff:  "),
                            PlaySpan.Signed(LiveFormat.SignedGrouped(roundScore - checkpoint)));
                        checkpoint = roundScore;
                    }
                }
                InsertBlock(stage.StageNumber, (long)number);
            }

            if (mode != 2 && (stage.ScoreAtEnd is not null || bonus is not null))
            {
                var final = stage.ScoreAtEnd ?? bonus!.ScoreAfterBonus ?? 0;
                var clear = bonus?.TotalBonus ?? 0;
                if (narrow)
                {
                    Add(PlaySpan.Body(pad), PlaySpan.Label("Score: "),
                        PlaySpan.Value(LiveFormat.Grouped(final)));
                    Add(PlaySpan.Body(pad), PlaySpan.Label("Clear Bonus: "),
                        PlaySpan.Value(LiveFormat.Grouped(clear)));
                }
                else
                {
                    Add(PlaySpan.Body(pad), PlaySpan.Label("Score: "),
                        PlaySpan.Value(LiveFormat.Grouped(final)),
                        PlaySpan.Label(" | Clear Bonus: "),
                        PlaySpan.Value(LiveFormat.Grouped(clear)));
                }
                Add(PlaySpan.Body(pad), PlaySpan.Label("Diff:  "),
                    PlaySpan.Signed(LiveFormat.SignedGrouped(final - checkpoint)));
                checkpoint = final;
            }
            if (mode != 2) InsertBlock(stage.StageNumber, null);
            else if (stage == stages[^1]) InsertBlock(null, null);
            Add(PlaySpan.Separator(new string('─', rule)));
        }

        if (replayFullPath is not null && status != "running")
        {
            var shown = DisplayPath(replayFullPath);
            Add(PlaySpan.Label("Replay Saved: "),
                PlaySpan.Value(narrow ? FileName(shown) : shown));
        }

        Add(PlaySpan.Separator(new string('═', rule)));
        Add(PlaySpan.Label("Status: "),
            PlaySpan.Value(status == "running" ? "Recording"
                           : status == "completed" ? "Completed"
                           : Title(status)));
        return lines;
    }

    public static string DisplayPath(string fullPath)
    {
        var parts = fullPath.Split(['/', Path.DirectorySeparatorChar],
                                   StringSplitOptions.RemoveEmptyEntries);
        var lower = parts.Select(x => x.ToLowerInvariant()).ToArray();
        foreach (var marker in new[] { "replayautosavetest", "replayautosave", "replay" })
        {
            var at = Array.IndexOf(lower, marker);
            if (at >= 0) return string.Join(Path.DirectorySeparatorChar, parts[at..]);
        }
        return parts.Length > 0 ? parts[^1] : fullPath;
    }

    public static string FileName(string path)
    {
        var at = path.LastIndexOfAny(['/', '\\']);
        return at >= 0 ? path[(at + 1)..] : path;
    }

    private static string Title(string s)
    {
        var sb = new StringBuilder(s.Length);
        var head = true;
        foreach (var ch in s)
        {
            if (char.IsLetter(ch))
            {
                sb.Append(head ? char.ToUpperInvariant(ch) : char.ToLowerInvariant(ch));
                head = false;
            }
            else { sb.Append(ch); head = true; }
        }
        return sb.ToString();
    }

    private static string Ljust(string s, int width) => s.Length >= width ? s : s.PadRight(width);

    private static string Str(long? v) => v is long i ? i.ToString(CultureInfo.InvariantCulture) : "None";

    private static int? ToInt(long? v) => v is long i ? (int)i : null;
}
