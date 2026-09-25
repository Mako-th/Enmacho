using System.Globalization;

namespace TH09.Shell.Data;

internal enum StatsSection
{
    StoryExtra,

    MatchCpu,

    MatchNet,

    MatchLocal,
}

internal enum StatsAxis
{
    ModeDifficulty,
    Difficulty,
    OpponentName,
    MyCharacter,
    Stage,
    FoeCharacter,
}

internal sealed record StatsLevel(StatsAxis Axis, string Title);

internal static class StatsLevels
{
    public const string ModeDifficultyTitle = "モード / 難易度";
    public const string DifficultyTitle = "難易度";
    public const string OpponentNameTitle = "相手プレイヤー";
    public const string MyCharacterTitle = "自キャラ";
    public const string StageTitle = "面";
    public const string FoeCharacterTitle = "相手キャラ";

    private static readonly StatsLevel[] Story =
    [
        new(StatsAxis.ModeDifficulty, ModeDifficultyTitle),
        new(StatsAxis.MyCharacter, MyCharacterTitle),
        new(StatsAxis.Stage, StageTitle),
        new(StatsAxis.FoeCharacter, FoeCharacterTitle),
    ];

    private static readonly StatsLevel[] Cpu =
    [
        new(StatsAxis.Difficulty, DifficultyTitle),
        new(StatsAxis.MyCharacter, MyCharacterTitle),
        new(StatsAxis.FoeCharacter, FoeCharacterTitle),
    ];

    private static readonly StatsLevel[] Versus =
    [
        new(StatsAxis.OpponentName, OpponentNameTitle),
        new(StatsAxis.MyCharacter, MyCharacterTitle),
        new(StatsAxis.FoeCharacter, FoeCharacterTitle),
    ];

    public static StatsLevel[] For(StatsSection section) => section switch
    {
        StatsSection.StoryExtra => Story,
        StatsSection.MatchCpu => Cpu,
        _ => Versus,
    };
}

internal static class StatsSections
{
    public static readonly StatsSection[] All =
    [
        StatsSection.StoryExtra, StatsSection.MatchCpu, StatsSection.MatchNet, StatsSection.MatchLocal,
    ];

    public static string Key(StatsSection s) => s switch
    {
        StatsSection.StoryExtra => "story",
        StatsSection.MatchCpu => "cpu",
        StatsSection.MatchNet => "net",
        _ => "local",
    };

    public static string Title(StatsSection s) => s switch
    {
        StatsSection.StoryExtra => "Story/Extra",
        StatsSection.MatchCpu => "Match(対CPU)",
        StatsSection.MatchNet => "Match(ネット)",
        _ => "Match(ローカル)",
    };

    public static bool IsMatch(StatsSection s) => s != StatsSection.StoryExtra;

    public static StatsSection[] Visible(IEnumerable<string>? hidden)
    {
        if (hidden is null) return All;
        var names = new HashSet<string>(hidden, StringComparer.Ordinal);
        var shown = Array.FindAll(All, s => !names.Contains(Title(s)));
        return shown.Length > 0 ? shown : All;
    }
}

internal static class StatsFormat
{
    public const string Missing = ReplayFormat.Missing;

    public static double JsRound(double v) => Math.Floor(v + 0.5);

    public static string AverageFrames(double? frames)
        => ReplayFormat.Frames(frames is double f ? (long)JsRound(f) : (long?)null);

    public static string Number(long? v)
        => v is long n ? n.ToString("N0", CultureInfo.InvariantCulture) : Missing;

    public static string RoundedNumber(double? v)
        => v is double d ? Number((long)JsRound(d)) : Missing;

    public static string Percent(long a, long b)
        => b == 0 ? Missing
                  : (100.0 * a / b).ToString("0.0", CultureInfo.InvariantCulture) + "%";

    public static string Fixed3(double v)
    {
        var s = v.ToString("F20", CultureInfo.InvariantCulture);
        var dot = s.IndexOf('.');
        var whole = long.Parse(s[..dot], CultureInfo.InvariantCulture);
        var frac = s[(dot + 1)..];
        var n = whole * 1000 + long.Parse(frac[..3], CultureInfo.InvariantCulture);
        if (frac.Length > 3 && frac[3] >= '5') n++;
        return (n / 1000).ToString(CultureInfo.InvariantCulture) + "."
               + (n % 1000).ToString("000", CultureInfo.InvariantCulture);
    }

    public static string Lives(double? v)
        => v is double d ? (JsRound(d * 10) / 10).ToString("0.#", CultureInfo.InvariantCulture) : Missing;
}

internal static class StatsLabels
{
    public const string UnknownCharacter = "（不明）";

    public const string UnknownDifficulty = "（難易度不明）";

    public const string UnknownMode = "（モード不明）";

    public const string NoName = "（名前なし）";

    public static readonly string[] Modes = ["Story", "Extra", "Match"];

    public static string Character(int? v)
    {
        if (v is not int i) return UnknownCharacter;
        return i >= 0 && i < ReplayLabels.Characters.Length
            ? ReplayLabels.Characters[i]
            : "#" + i.ToString(CultureInfo.InvariantCulture);
    }

    public static string Difficulty(int? v)
    {
        if (v is not int i) return UnknownDifficulty;
        return i >= 0 && i < ReplayLabels.Difficulties.Length
            ? ReplayLabels.Difficulties[i]
            : i.ToString(CultureInfo.InvariantCulture);
    }

    public static string Mode(int? v)
    {
        if (v is not int i) return UnknownMode;
        return i >= 0 && i < Modes.Length ? Modes[i] : i.ToString(CultureInfo.InvariantCulture);
    }

    public static string ModeDifficulty(int? mode, int? difficulty)
    {
        var m = Mode(mode);
        var d = Difficulty(difficulty);
        return m == d ? m : m + " " + d;
    }

    public static string Round(int? v)
        => v is int i ? "R" + i.ToString(CultureInfo.InvariantCulture) : "R?";

    public static string Stage(int? v)
        => v is int i ? i.ToString(CultureInfo.InvariantCulture) + " 面" : UnknownCharacter;

    public static string OpponentName(string? v)
        => string.IsNullOrEmpty(v) ? NoName : v;

    public static string RoundStatus(string? v) => v switch
    {
        "running" => "進行中",
        "aborted" => "中断",
        null => "",
        _ => v,
    };
}

internal enum StatsLeafField
{
    When,
    Session,
    OpponentName,
    MyCharacter,
    FoeCharacter,
    Result,
    RoundScore,
    Time,
    RoundTimes,
    SpellScore,
    SideBasis,
    Stage,
    RoundNumber,
    Reach,
    ReachExBonus,
    Segment,
    ClearBonus,
    SegmentExBonus,
}

internal static class SortMark
{
    public const string Descending = "▼";

    public const string Ascending = "▲";

    public const double SeatWidth = 16;

    public static string Of(bool active, bool descending)
        => !active ? "" : descending ? Descending : Ascending;

    public static double LeftSeat(bool rightAligned) => rightAligned ? SeatWidth : 0;

    public static double RightSeat(bool rightAligned) => rightAligned ? 0 : SeatWidth;
}

internal sealed record StatsLeafColumn(StatsLeafField Field, string Label, double Body,
                                       bool RightAligned = false,
                                       CellStyle Style = CellStyle.Normal,
                                       string Tip = "",
                                       bool Sortable = true)
{
    public double Width => Body + SortMark.SeatWidth;
}

internal static class StatsLeafColumns
{
    public const string ReachName = "終了スコア";

    public const string ReachExBonusName = ReachName + "（CB除く）";

    public const string ReachTip =
        "終了スコア＝そのラウンドを終えた時点の通しスコア"
        + "（面の最後のラウンドは stages.score_at_end、それ以外は次のラウンドの rounds.score_1_at_start）";

    public const string ClearBonusTip =
        "clear_bonuses.total_bonus。ゲーム内表示は「貴方の付加価値」。"
        + "面をクリアしたラウンド（＝最後のラウンド）にだけ載る";

    public const string ReachExBonusTip =
        "終了スコア − そのラウンドのクリアボーナス"
        + "（ボーナスの無いラウンドは終了スコアそのもの。それまでの面のボーナスの累計は引いていない）";

    public const string SegmentName = "区間スコア";

    public const string SegmentExBonusName = "区間（CB除く）";

    public const string SegmentTip =
        "区間スコア＝そのラウンドを終えた時点の通しスコア − そのラウンドの"
        + " rounds.score_1_at_start（面の全ラウンドを足すと面の増分になる）";

    public const string SegmentExBonusTip =
        "区間スコア − クリアボーナス（ボーナスの無いラウンドは区間スコアそのもの）";

    public static string RoundTimesTip(string unit, bool marked)
        => unit + "の全ラウンドの時間を round_number 順に並べたもの（左の「時間」とは別物）。"
           + "時間の記録が無いラウンドは — と出す（0 とは書かない）。"
           + "中断・進行中のラウンドには（中断）（進行中）と付ける"
           + (marked ? "。太字がこの行のラウンド" : "");

    public static readonly StatsLeafColumn[] MatchCpu = BuildMatch(withOpponent: false);

    public static readonly StatsLeafColumn[] MatchVersus = BuildMatch(withOpponent: true);

    public static readonly StatsLeafColumn[] Story =
    [
        new(StatsLeafField.When, "日時", 128),
        new(StatsLeafField.Session, "session", 62, Style: CellStyle.Muted),
        new(StatsLeafField.Stage, "面", 44, RightAligned: true),
        new(StatsLeafField.RoundNumber, "ラウンド", 56),
        new(StatsLeafField.FoeCharacter, "相手", 78, Style: CellStyle.P2),
        new(StatsLeafField.Reach, ReachName, 112, RightAligned: true, Tip: ReachTip),
        new(StatsLeafField.ReachExBonus, ReachExBonusName, 132, RightAligned: true,
            Style: CellStyle.Muted, Tip: ReachExBonusTip),
        new(StatsLeafField.Segment, SegmentName, 112, RightAligned: true,
            Style: CellStyle.Muted, Tip: SegmentTip),
        new(StatsLeafField.ClearBonus, "うちクリアボーナス", 124, RightAligned: true,
            Style: CellStyle.Muted, Tip: ClearBonusTip),
        new(StatsLeafField.SegmentExBonus, SegmentExBonusName, 132, RightAligned: true,
            Style: CellStyle.Muted, Tip: SegmentExBonusTip),
        new(StatsLeafField.Time, "時間", 62, RightAligned: true,
            Tip: "そのラウンドの duration_frames"),
        new(StatsLeafField.RoundTimes, "面のラウンドごと", 240, Tip: RoundTimesTip("面", marked: true),
            Sortable: false),
    ];

    private static StatsLeafColumn[] BuildMatch(bool withOpponent)
    {
        var cols = new List<StatsLeafColumn>
        {
            new(StatsLeafField.When, "日時", 128),
            new(StatsLeafField.Session, "session", 62, Style: CellStyle.Muted),
        };
        if (withOpponent) cols.Add(new(StatsLeafField.OpponentName, StatsLevels.OpponentNameTitle, 110));
        cols.Add(new(StatsLeafField.MyCharacter, StatsLevels.MyCharacterTitle, 72, Style: CellStyle.P1));
        cols.Add(new(StatsLeafField.FoeCharacter, StatsLevels.FoeCharacterTitle, 78, Style: CellStyle.P2));
        cols.Add(new(StatsLeafField.Result, "結果", 44));
        cols.Add(new(StatsLeafField.RoundScore, "ラウンド", 62));
        cols.Add(new(StatsLeafField.Time, "時間", 62, RightAligned: true,
                     Tip: "マッチの全ラウンドの duration_frames の合計"));
        cols.Add(new(StatsLeafField.RoundTimes, "マッチのラウンドごと", 190,
                     Tip: RoundTimesTip("マッチ", marked: false), Sortable: false));
        cols.Add(new(StatsLeafField.SpellScore, "SP 由来スコア", 110, RightAligned: true));
        cols.Add(new(StatsLeafField.SideBasis, "自分側の根拠", 112, Style: CellStyle.Muted));
        return [.. cols];
    }

    public static StatsLeafColumn[] For(StatsSection section) => section switch
    {
        StatsSection.StoryExtra => Story,
        StatsSection.MatchCpu => MatchCpu,
        _ => MatchVersus,
    };
}


internal enum StatsGroupField
{
    Name,
    Count,
    PlayCount,
    Wins,
    Losses,
    WinRate,
    RoundWinRate,
}

internal sealed record StatsGroupColumn(StatsGroupField Field, string Label, double Body,
                                        bool RightAligned = false,
                                        CellStyle Style = CellStyle.Normal)
{
    public double Width => Body + SortMark.SeatWidth;
}

internal static class StatsGroupColumns
{
    public const string LeadBandName = "総合";

    public const string RoundCountName = "ラウンド";

    public const string MatchCountName = "マッチ";

    private static readonly StatsGroupColumn[] Plain =
    [
        new(StatsGroupField.Name, "", 200),
        new(StatsGroupField.PlayCount, "プレイ", 70, RightAligned: true, Style: CellStyle.Muted),
        new(StatsGroupField.Count, RoundCountName, 70, RightAligned: true),
    ];

    private static readonly StatsGroupColumn[] Match =
    [
        new(StatsGroupField.Name, "", 200),
        new(StatsGroupField.Count, MatchCountName, 70, RightAligned: true),
        new(StatsGroupField.Wins, "勝", 52, RightAligned: true, Style: CellStyle.P1),
        new(StatsGroupField.Losses, "負", 52, RightAligned: true, Style: CellStyle.P2),
        new(StatsGroupField.WinRate, "勝率", 70, RightAligned: true),
        new(StatsGroupField.RoundWinRate, "ラウンド勝率", 90, RightAligned: true,
            Style: CellStyle.Muted),
    ];

    public static StatsGroupColumn[] For(StatsSection section)
        => StatsSections.IsMatch(section) ? Match : Plain;

    public static ReplayCell[] Cells(StatsGroupColumn[] cols, Func<StatsGroupField, string> text,
                                     double cushion = 0)
    {
        var cells = new ReplayCell[cols.Length];
        for (var i = 0; i < cols.Length; i++)
            cells[i] = new ReplayCell(text(cols[i].Field), cols[i].Width,
                                      cols[i].RightAligned, cols[i].Style)
            {
                Cushion = cols[i].Field == StatsGroupField.Count ? cushion : 0,
            };
        return cells;
    }
}


internal enum StatsAggStat
{
    Max,
    Avg,
    Min,
}

internal enum StatsAggValue
{
    Lives,
    Score,
    Frames,
    RoundFrames,
    Reach,
    ClearBonus,
    ReachExBonus,
    Segment,
    SegmentExBonus,
    Life,
}

internal sealed class StatsAggValues
{
    private readonly List<double> _lives = [];
    private readonly List<double> _score = [];
    private readonly List<double> _frames = [];
    private readonly List<double> _roundFrames = [];
    private readonly List<double> _reach = [];
    private readonly List<double> _clearBonus = [];
    private readonly List<double> _reachExBonus = [];
    private readonly List<double> _segment = [];
    private readonly List<double> _segmentExBonus = [];
    private readonly List<double> _life = [];

    public void Add(StatsAggValue kind, double? value)
    {
        if (value is double v) List(kind).Add(v);
    }

    public IReadOnlyList<double> Of(StatsAggValue kind) => List(kind);

    private List<double> List(StatsAggValue kind) => kind switch
    {
        StatsAggValue.Lives => _lives,
        StatsAggValue.Score => _score,
        StatsAggValue.Frames => _frames,
        StatsAggValue.RoundFrames => _roundFrames,
        StatsAggValue.Reach => _reach,
        StatsAggValue.ClearBonus => _clearBonus,
        StatsAggValue.ReachExBonus => _reachExBonus,
        StatsAggValue.Segment => _segment,
        StatsAggValue.SegmentExBonus => _segmentExBonus,
        StatsAggValue.Life => _life,
        _ => throw new InvalidOperationException("知らない集計の値: " + kind),
    };
}

internal enum StatsAggNameForm
{
    BandFirst,
    HeadFirst,
    HeadOnly,
}

internal sealed record StatsAggColumn(StatsAggValue Value, StatsAggStat Stat,
                                      string Head, string Band, StatsAggNameForm Form,
                                      double Width, bool IsGroupStart = false,
                                      string Tip = "", bool GuardMin = false)
{
    public string Label => Form switch
    {
        StatsAggNameForm.BandFirst => Band + Head,
        StatsAggNameForm.HeadFirst => Head + " " + Band,
        StatsAggNameForm.HeadOnly => Head,
        _ => throw new InvalidOperationException("知らない見出しの組み立て方: " + Form),
    };

    public string Top => IsGroupStart ? Band : "";

    public string? TipOrNull => Tip.Length == 0 ? null : Tip;
}

internal sealed record StatsAggCell(string Text, string Tip, double Width, bool IsGroupStart)
{
    public double? Value { get; init; }

    public string? TipOrNull => Tip.Length == 0 ? null : Tip;

    public bool IsRecord { get; init; }
}

internal static class StatsAggColumns
{
    public static readonly (StatsAggStat Stat, string Name, string TimeName)[] Bands =
    [
        (StatsAggStat.Max, "最高", "最長時間"),
        (StatsAggStat.Avg, "平均", "平均時間"),
        (StatsAggStat.Min, "最低", "最短時間"),
    ];

    private const double ScoreWidth = 104, TimeWidth = 66, LivesWidth = 56;

    private const double BonusWidth = 104, ExBonusWidth = 118;

    private const double LongTimeWidth = 104;

    private const double LifeWidth = 74;

    private const string RoundTimeName = "ラウンド";
    private const string MatchTimeName = "マッチ";
    private const string PlayTimeName = "通し";

    public const string RoundTimeTip =
        "ラウンド 1 本ずつの時間（rounds.duration_frames）。マッチ全体の合計とは別物";

    public const string MatchTimeTip =
        "そのマッチの全ラウンドの duration_frames の合計";

    public const string LifeTip =
        "ラウンドが終わった時点の自分の残ライフ（rounds.life_N_raw_at_end。0〜10）。"
        + "勝ったラウンドは 1 以上・負けたラウンドは 0。残機（lives）とは別のもの";

    public const string LivesTip =
        "最後の面の stages.lives_at_end（無ければ最後のラウンドの rounds.lives_1_at_end）"
        + "＝ th09_app の履歴タブ『最終残機』と同じ式";

    private const string SelfBestTip = "＝その地点での自己ベスト";

    public static string GuardTip
        => "自己ベストと同じ下限ガード "
           + StatsLeafQuery.MinStageFrames.ToString(CultureInfo.InvariantCulture) + "f";

    private static readonly StatsAggColumn[] PlayWithLives = Play(lives: true);
    private static readonly StatsAggColumn[] Stage = BuildStage(gain: false);
    private static readonly StatsAggColumn[] StageGain = BuildStage(gain: true);
    private static readonly StatsAggColumn[] Match = BuildMatch();

    public static StatsAggColumn[] For(StatsSection section, int depth) => section switch
    {
        StatsSection.StoryExtra => depth >= 2 ? Stage : PlayWithLives,
        _ => Match,
    };

    public static List<StatsAggCell> Cells(StatsAggColumn[] cols, StatsAggValues values)
    {
        var cells = new List<StatsAggCell>(cols.Length);
        foreach (var c in cols)
        {
            var xs = values.Of(c.Value);
            var use = c.GuardMin && c.Stat == StatsAggStat.Min
                ? xs.Where(v => v >= StatsLeafQuery.MinStageFrames).ToList() : xs;
            var v = Stat(use, c.Stat);
            var text = c.Value switch
            {
                StatsAggValue.Lives or StatsAggValue.Life => StatsFormat.Lives(v),
                StatsAggValue.Frames or StatsAggValue.RoundFrames => StatsFormat.AverageFrames(v),
                _ => StatsFormat.RoundedNumber(v),
            };
            cells.Add(new StatsAggCell(text, "n=" + xs.Count.ToString(CultureInfo.InvariantCulture),
                                       c.Width, c.IsGroupStart)
            {
                Value = v,
                IsRecord = c.Stat == StatsAggStat.Max,
            });
        }
        return cells;
    }

    private static double? Stat(IReadOnlyList<double> xs, StatsAggStat stat)
    {
        if (xs.Count == 0) return null;
        return stat switch
        {
            StatsAggStat.Max => xs.Max(),
            StatsAggStat.Min => xs.Min(),
            StatsAggStat.Avg => xs.Sum() / xs.Count,
            _ => throw new InvalidOperationException("知らない集計: " + stat),
        };
    }

    private static StatsAggColumn[] Play(bool lives)
    {
        var cols = new List<StatsAggColumn>();
        foreach (var b in Bands)
        {
            var head = true;
            if (lives)
            {
                cols.Add(new(StatsAggValue.Lives, b.Stat, "残機", b.Name, StatsAggNameForm.BandFirst,
                             LivesWidth, IsGroupStart: true, Tip: LivesTip));
                head = false;
            }
            cols.Add(new(StatsAggValue.Score, b.Stat, "スコア", b.Name, StatsAggNameForm.BandFirst,
                         ScoreWidth, IsGroupStart: head));
            cols.Add(new(StatsAggValue.Frames, b.Stat, PlayTimeName + b.TimeName, b.Name,
                         StatsAggNameForm.HeadOnly, LongTimeWidth));
        }
        return [.. cols];
    }

    private static StatsAggColumn[] BuildStage(bool gain)
    {
        var cols = new List<StatsAggColumn>();
        foreach (var b in Bands)
        {
            var best = b.Stat == StatsAggStat.Max;
            cols.Add(gain
                ? new(StatsAggValue.Segment, b.Stat, StatsLeafColumns.SegmentName, b.Name,
                      StatsAggNameForm.HeadFirst, ScoreWidth, IsGroupStart: true,
                      Tip: StatsLeafColumns.SegmentTip)
                : new(StatsAggValue.Reach, b.Stat, StatsLeafColumns.ReachName, b.Name,
                      StatsAggNameForm.HeadFirst, ScoreWidth, IsGroupStart: true,
                      Tip: best ? SelfBestTip : StatsLeafColumns.ReachTip));
            cols.Add(new(StatsAggValue.ClearBonus, b.Stat, "うちクリアボーナス", b.Name,
                         StatsAggNameForm.HeadFirst, BonusWidth, Tip: StatsLeafColumns.ClearBonusTip));
            cols.Add(gain
                ? new(StatsAggValue.SegmentExBonus, b.Stat,
                      StatsLeafColumns.SegmentExBonusName, b.Name, StatsAggNameForm.HeadFirst,
                      ExBonusWidth, Tip: StatsLeafColumns.SegmentExBonusTip)
                : new(StatsAggValue.ReachExBonus, b.Stat,
                      StatsLeafColumns.ReachExBonusName, b.Name, StatsAggNameForm.HeadFirst,
                      ExBonusWidth, Tip: StatsLeafColumns.ReachExBonusTip));
            var guard = b.Stat == StatsAggStat.Min;
            cols.Add(new(StatsAggValue.Frames, b.Stat, RoundTimeName + b.TimeName, b.Name,
                         StatsAggNameForm.HeadOnly, LongTimeWidth,
                         Tip: guard ? GuardTip : "", GuardMin: guard));
        }
        return [.. cols];
    }

    public static StatsAggColumn[] Gain(StatsSection section, int depth)
        => section == StatsSection.StoryExtra && depth >= 2 ? StageGain : [];

    private static StatsAggColumn[] BuildMatch()
    {
        var cols = new List<StatsAggColumn>();
        foreach (var b in Bands)
        {
            cols.Add(new(StatsAggValue.Life, b.Stat, "残ライフ", b.Name,
                         StatsAggNameForm.BandFirst, LifeWidth, IsGroupStart: true, Tip: LifeTip));
            cols.Add(new(StatsAggValue.RoundFrames, b.Stat, RoundTimeName + b.TimeName, b.Name,
                         StatsAggNameForm.HeadOnly, LongTimeWidth, Tip: RoundTimeTip));
            cols.Add(new(StatsAggValue.Frames, b.Stat, MatchTimeName + b.TimeName, b.Name,
                         StatsAggNameForm.HeadOnly, LongTimeWidth, Tip: MatchTimeTip));
        }
        return [.. cols];
    }
}

internal sealed record StatsRound(int? Number, int? Frames, string? Status, int? WinnerSide)
{
    public bool HasNext { get; init; }

    public bool IsCurrent { get; init; }

    public bool IsSelfWin { get; init; }

    public bool IsFoeWin { get; init; }

    public int? Life { get; init; }

    public string Text
    {
        get
        {
            var s = StatsLabels.Round(Number) + " " + ReplayFormat.Frames(Frames);
            var st = StatsLabels.RoundStatus(Status);
            return st.Length == 0 ? s : s + "（" + st + "）";
        }
    }
}

internal interface IStatsLeafRow
{
    long SessionId { get; }

    IReadOnlyList<ReplayCell> Cells { get; }

    IReadOnlyList<ReplayCell> LeadCells { get; }

    IReadOnlyList<ReplayCell> TailCells { get; }

    IReadOnlyList<StatsRound> Rounds { get; }

    double RoundTimesWidth { get; }

    double? SortNumber(StatsLeafField f);
}

internal sealed class StatsMatchRow : IStatsLeafRow
{
    public required long SessionId { get; init; }

    public required StatsSection Section { get; init; }

    public required string WhenText { get; init; }

    public required DateTime? When { get; init; }

    public required int? Difficulty { get; init; }

    public required int? MyCharacter { get; init; }

    public required int? FoeCharacter { get; init; }

    public required string OpponentName { get; init; }

    public required int? Win { get; init; }

    public required long? Frames { get; init; }

    public required int RoundWins { get; init; }

    public required int RoundLosses { get; init; }

    public required long? Score { get; init; }

    public required long? SpellPoints { get; init; }

    public required long? BossFrames { get; init; }

    public required bool IsOwn { get; init; }

    public required bool IsProvisional { get; init; }

    public required IReadOnlyList<StatsRound> Rounds { get; init; }

    public bool IsWin => Win == 1;

    public bool IsLoss => Win == 0;

    public double RoundTimesWidth
        => StatsLeafColumns.For(Section).First(c => c.Field == StatsLeafField.RoundTimes).Width;

    private ReplayCell[]? _cells;

    public IReadOnlyList<ReplayCell> Cells => _cells ??= StatsCellBuilder.Build(StatsLeafColumns.For(Section), Text, StyleOf);

    private ReplayCell[]? _lead;
    private ReplayCell[]? _tail;

    public IReadOnlyList<ReplayCell> LeadCells =>
        _lead ??= StatsCellBuilder.BeforeRoundTimes(StatsLeafColumns.For(Section), Cells);

    public IReadOnlyList<ReplayCell> TailCells =>
        _tail ??= StatsCellBuilder.AfterRoundTimes(StatsLeafColumns.For(Section), Cells);

    public string Text(StatsLeafField f) => f switch
    {
        StatsLeafField.When => WhenText,
        StatsLeafField.Session => SessionId.ToString(CultureInfo.InvariantCulture),
        StatsLeafField.OpponentName => StatsLabels.OpponentName(OpponentName),
        StatsLeafField.MyCharacter => StatsLabels.Character(MyCharacter),
        StatsLeafField.FoeCharacter => StatsLabels.Character(FoeCharacter),
        StatsLeafField.Result => Win switch { 1 => "勝", 0 => "負", _ => StatsFormat.Missing },
        StatsLeafField.RoundScore => RoundWins.ToString(CultureInfo.InvariantCulture) + " - "
                                     + RoundLosses.ToString(CultureInfo.InvariantCulture),
        StatsLeafField.Time => ReplayFormat.Frames(Frames),
        StatsLeafField.RoundTimes => StatsCellBuilder.RoundTimesText(Rounds),
        StatsLeafField.SpellScore => StatsFormat.Number(SpellPoints),
        StatsLeafField.SideBasis => IsProvisional ? "★仮（1P と置いた）" : "確定",
        _ => throw new InvalidOperationException("Match の葉に " + f + " の升目が書かれていない"),
    };

    public double? SortNumber(StatsLeafField f) => f switch
    {
        StatsLeafField.Session => SessionId,
        StatsLeafField.Result => Win,
        StatsLeafField.Time => Frames,
        StatsLeafField.SpellScore => SpellPoints,
        StatsLeafField.MyCharacter => ReplayLabels.DisplayRank(MyCharacter),
        StatsLeafField.FoeCharacter => ReplayLabels.DisplayRank(FoeCharacter),
        StatsLeafField.When or StatsLeafField.OpponentName
            or StatsLeafField.RoundScore
            or StatsLeafField.RoundTimes or StatsLeafField.SideBasis => null,
        _ => throw new InvalidOperationException("Match の葉に " + f + " の並べ替えが書かれていない"),
    };

    private CellStyle StyleOf(StatsLeafColumn c)
        => c.Field == StatsLeafField.Result
           ? (Win switch { 1 => CellStyle.P1, 0 => CellStyle.P2, _ => CellStyle.Muted })
           : c.Style;
}

internal sealed class StatsStageRow : IStatsLeafRow
{
    public required long SessionId { get; init; }

    public required string WhenText { get; init; }

    public required DateTime? When { get; init; }

    public required int? Mode { get; init; }

    public required int? Difficulty { get; init; }

    public required int? MyCharacter { get; init; }

    public required bool IsOwn { get; init; }

    public required int? Stage { get; init; }

    public required int? FoeCharacter { get; init; }

    public required int? Round { get; init; }

    public required int? Frames { get; init; }

    public required long? Reach { get; init; }

    public required long? Segment { get; init; }

    public required long? ClearBonus { get; init; }

    public required string? Status { get; init; }

    public long? ReachExBonus => Reach is long v ? v - (ClearBonus ?? 0) : null;

    public long? SegmentExBonus => Segment is long v ? v - (ClearBonus ?? 0) : null;

    public required IReadOnlyList<StatsRound> AllRoundsOfStage { get; init; }

    public IReadOnlyList<StatsRound> Rounds => _rounds ??= MarkCurrent();

    private IReadOnlyList<StatsRound>? _rounds;

    private IReadOnlyList<StatsRound> MarkCurrent()
    {
        var list = new StatsRound[AllRoundsOfStage.Count];
        for (var i = 0; i < list.Length; i++)
        {
            var r = AllRoundsOfStage[i];
            list[i] = r with { IsCurrent = Round is int cur && r.Number == cur };
        }
        return list;
    }

    public double RoundTimesWidth
        => StatsLeafColumns.Story.First(c => c.Field == StatsLeafField.RoundTimes).Width;

    private ReplayCell[]? _cells;

    public IReadOnlyList<ReplayCell> Cells
        => _cells ??= StatsCellBuilder.Build(StatsLeafColumns.Story, Text, c => c.Style);

    private ReplayCell[]? _lead;
    private ReplayCell[]? _tail;

    public IReadOnlyList<ReplayCell> LeadCells =>
        _lead ??= StatsCellBuilder.BeforeRoundTimes(StatsLeafColumns.Story, Cells);

    public IReadOnlyList<ReplayCell> TailCells =>
        _tail ??= StatsCellBuilder.AfterRoundTimes(StatsLeafColumns.Story, Cells);

    public string Text(StatsLeafField f) => f switch
    {
        StatsLeafField.When => WhenText,
        StatsLeafField.Session => SessionId.ToString(CultureInfo.InvariantCulture),
        StatsLeafField.Stage => Stage is int s ? s.ToString(CultureInfo.InvariantCulture) : "",
        StatsLeafField.RoundNumber => StatsLabels.Round(Round),
        StatsLeafField.FoeCharacter => StatsLabels.Character(FoeCharacter),
        StatsLeafField.Reach => StatsFormat.Number(Reach),
        StatsLeafField.ReachExBonus => StatsFormat.Number(ReachExBonus),
        StatsLeafField.Segment => StatsFormat.Number(Segment),
        StatsLeafField.ClearBonus => StatsFormat.Number(ClearBonus),
        StatsLeafField.SegmentExBonus => StatsFormat.Number(SegmentExBonus),
        StatsLeafField.Time => ReplayFormat.Frames(Frames),
        StatsLeafField.RoundTimes => StatsCellBuilder.RoundTimesText(Rounds),
        _ => throw new InvalidOperationException("Story の葉に " + f + " の升目が書かれていない"),
    };

    public double? SortNumber(StatsLeafField f) => f switch
    {
        StatsLeafField.Session => SessionId,
        StatsLeafField.Stage => Stage,
        StatsLeafField.RoundNumber => Round,
        StatsLeafField.Reach => Reach,
        StatsLeafField.ReachExBonus => ReachExBonus,
        StatsLeafField.Segment => Segment,
        StatsLeafField.ClearBonus => ClearBonus,
        StatsLeafField.SegmentExBonus => SegmentExBonus,
        StatsLeafField.Time => Frames,
        StatsLeafField.FoeCharacter => ReplayLabels.DisplayRank(FoeCharacter),
        StatsLeafField.When or StatsLeafField.RoundTimes => null,
        _ => throw new InvalidOperationException("Story の葉に " + f + " の並べ替えが書かれていない"),
    };
}

internal static class StatsCellBuilder
{
    public static ReplayCell[] Build(StatsLeafColumn[] cols, Func<StatsLeafField, string> text,
                                     Func<StatsLeafColumn, CellStyle> style)
    {
        var cells = new ReplayCell[cols.Length];
        for (var i = 0; i < cols.Length; i++)
            cells[i] = new ReplayCell(text(cols[i].Field), cols[i].Width, cols[i].RightAligned, style(cols[i]));
        return cells;
    }

    public static string RoundTimesText(IReadOnlyList<StatsRound> rounds)
        => string.Join(" / ", rounds.Select(r => r.Text));

    public static ReplayCell[] BeforeRoundTimes(StatsLeafColumn[] cols, IReadOnlyList<ReplayCell> cells)
        => Split(cols, cells, before: true);

    public static ReplayCell[] AfterRoundTimes(StatsLeafColumn[] cols, IReadOnlyList<ReplayCell> cells)
        => Split(cols, cells, before: false);

    private static ReplayCell[] Split(StatsLeafColumn[] cols, IReadOnlyList<ReplayCell> cells,
                                      bool before)
    {
        int at = Array.FindIndex(cols, c => c.Field == StatsLeafField.RoundTimes);
        if (at < 0 || at >= cells.Count) return before ? [.. cells] : [];
        var outv = new List<ReplayCell>(cells.Count);
        for (var i = 0; i < cells.Count; i++)
            if (i != at && (i < at) == before) outv.Add(cells[i]);
        return [.. outv];
    }
}

internal sealed record StatsCard(string Key, string Value, string Note)
{
    public bool HasNote => Note.Length > 0;

    public string Source { get; init; } = "";

    public bool HasSource => Source.Length > 0;
}

internal sealed record StatsCardGroup(IReadOnlyList<StatsCard> Cards)
{
    public bool IsFirst { get; init; }

    public bool HasDivider => !IsFirst && Cards.Count > 1;
}


internal enum StatsMatrixTint
{
    None,
    P1,
    P2,
}

internal static class StatsMatrixLayout
{
    public const double HeaderWidth = 84;

    public const double CellWidth = 68;
}

internal sealed class StatsMatrixCell
{
    public required string Text { get; init; }

    public required double Width { get; init; }

    public required string Tip { get; init; }

    public required bool IsHeader { get; init; }

    public required bool IsRowHeader { get; init; }

    public required bool IsSelf { get; init; }

    public required StatsMatrixTint Tint { get; init; }

    public required double Alpha { get; init; }

    public required IReadOnlyList<string>? Pick { get; init; }

    public bool CanPick => Pick is not null;

    public string? TipOrNull => Tip.Length == 0 ? null : Tip;

    public string ColorSpec => Tint == StatsMatrixTint.None
        ? ""
        : "background:rgba(var(--" + (Tint == StatsMatrixTint.P1 ? "cush" : "cush2") + "),"
          + StatsFormat.Fixed3(Alpha) + ")";

    public Avalonia.Media.IBrush Background => Tint == StatsMatrixTint.None
        ? Avalonia.Media.Brushes.Transparent
        : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(
            (byte)Math.Round(Alpha * 255, MidpointRounding.AwayFromZero),
            Tint == StatsMatrixTint.P1 ? (byte)0x5b : (byte)0xff,
            Tint == StatsMatrixTint.P1 ? (byte)0xc8 : (byte)0x8a,
            Tint == StatsMatrixTint.P1 ? (byte)0xff : (byte)0x5b));
}

internal sealed record StatsMatrixRow(IReadOnlyList<StatsMatrixCell> Cells, bool IsHeaderRow);

internal sealed class StatsMatrix
{
    public required IReadOnlyList<StatsMatrixRow> Rows { get; init; }

    public required string Title { get; init; }

    public required IReadOnlyList<string> Legend { get; init; }

    public required int SourceCount { get; init; }

    public required int OutsideCount { get; init; }
}
