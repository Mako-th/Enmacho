using System.Globalization;

namespace TH09.Shell.Data;

internal sealed record StatsRecordColumn(string Label, double Width, bool RightAligned);

internal sealed record StatsRecordCell(string Text, double Width, bool RightAligned);

internal sealed record StatsRecordRow(long Mode, long Character, long? BestScore,
                                      IReadOnlyList<StatsRecordCell> Cells);

internal sealed record StatsRecordsTable(string Title, IReadOnlyList<StatsRecordColumn> Columns,
                                         IReadOnlyList<StatsRecordRow> Rows,
                                         IReadOnlyList<string> Notes, double Width);

internal static class StatsRecords
{
    public const string ToggleLabel = "キャラ別記録";

    public const string NoReach = ReplayFormat.Missing;

    private static readonly StatsRecordColumn[] Cols =
    [
        new("モード", 64, false),
        new("キャラ", 92, false),
        new("回数", 60, true),
        new("最高スコア", 124, true),
        new("最低ミス", 76, true),
        new("最高残機", 76, true),
        new("到達面", 64, true),
    ];

    public static IReadOnlyList<StatsRecordColumn> Columns => Cols;

    public const string ReachNote =
        "最低ミスと最高残機は 9 面まで到達した回だけで比べている"
        + "（途中で落ちた回はミスが少なくて当然なので）。—は 9 面到達が無いという意味。";

    public const string DifficultyNote =
        "難易度では分けていない（原本と同じ集計の鍵 ＝ モード × 自キャラ）。"
        + "モード / 難易度で 1 段降りても、この表は同じものを出す。";

    public static StatsRecordsTable Build(IReadOnlyList<TH09.Record.StoryPlay> plays,
                                          IReadOnlyDictionary<long, bool> ownBySession,
                                          bool foreign, StatsCharFilter chars)
    {
        ArgumentNullException.ThrowIfNull(plays);
        ArgumentNullException.ThrowIfNull(ownBySession);
        ArgumentNullException.ThrowIfNull(chars);

        var kept = new List<TH09.Record.StoryPlay>();
        int unknownSide = 0;
        foreach (var p in plays)
        {
            if (!ownBySession.TryGetValue(p.SessionId, out var isOwn))
            {
                unknownSide++;
                continue;
            }
            if (!foreign && !isOwn) continue;
            if (!chars.KeepsSelf(p.Character is long ch ? checked((int)ch) : null)) continue;
            kept.Add(p);
        }

        var recs = TH09.Record.StoryRecords.StoryCharacterRecords(kept);

        var ordered = recs
            .OrderByDescending(r => r.BestScore ?? long.MinValue)
            .ThenBy(r => r.Mode)
            .ThenBy(r => ReplayLabels.DisplayRank(checked((int)r.Character)) ?? int.MaxValue)
            .ToList();

        var rows = new List<StatsRecordRow>(ordered.Count);
        foreach (var r in ordered)
        {
            var cells = new StatsRecordCell[Cols.Length];
            cells[0] = Cell(0, ModeText(r.Mode));
            cells[1] = Cell(1, ReplayLabels.Character(checked((int)r.Character)));
            cells[2] = Cell(2, Num(r.Plays));
            cells[3] = Cell(3, Num(r.BestScore));
            cells[4] = Cell(4, Num(r.MinMisses));
            cells[5] = Cell(5, Lives(r.MaxFinalLives));
            cells[6] = Cell(6, Num(r.StageMax));
            rows.Add(new StatsRecordRow(r.Mode, r.Character, r.BestScore, cells));
        }

        var notes = new List<string>
        {
            ReachNote,
            DifficultyNote,
            "集計したプレイ " + Num(kept.Count) + " 本"
            + "（材料 " + Num(plays.Count) + " 本のうち。"
            + "材料は「面が 1 つ以上閉じている Story / Extra のプレイ」）。",
        };
        if (unknownSide > 0)
        {
            notes.Add("自分側が決まらず外した " + Num(unknownSide) + " 件を含まない。");
        }

        return new StatsRecordsTable(
            Title, Cols, rows, notes, Cols.Sum(c => c.Width));
    }

    public const string Title = "Story / Extra のキャラ別記録（モード × 自キャラ）";

    private static string ModeText(long mode)
        => mode == 0 ? "Story" : mode == 1 ? "Extra" : ReplayFormat.Missing;

    private static StatsRecordCell Cell(int i, string text)
        => new(text, Cols[i].Width, Cols[i].RightAligned);

    private static string Num(long? v)
        => v is long x ? x.ToString("N0", CultureInfo.InvariantCulture) : NoReach;

    private static string Num(int? v)
        => v is int x ? x.ToString("N0", CultureInfo.InvariantCulture) : NoReach;

    private static string Num(int v) => v.ToString("N0", CultureInfo.InvariantCulture);

    private static string Lives(double? v)
        => v is not double x ? NoReach
           : x == Math.Floor(x) ? ((long)x).ToString("N0", CultureInfo.InvariantCulture)
           : x.ToString("0.###", CultureInfo.InvariantCulture);
}
