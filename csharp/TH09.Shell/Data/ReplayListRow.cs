using System.Globalization;

namespace TH09.Shell.Data;

internal enum ReplaySection
{
    StoryExtra,

    Match,
}

internal enum OwnSide
{
    None = 0,

    P1 = 1,

    P2 = 2,
}

internal enum MatchMode
{
    Unknown = -1,

    HumanVsHuman = 0,

    HumanVsCpu = 1,

    CpuVsHuman = 2,

    CpuVsCpu = 3,
}

internal enum ReplaySortKey
{
    DateTime,
    Difficulty,
    P1Character,
    P1Name,
    P2Character,
    P2Name,
    Score,
    Lives,
    Reach,
    Time,
    Result,
    OwnSide,
    Kind,
}

internal enum CellStyle
{
    Normal,
    Muted,
    P1,
    P2,
}

internal sealed record ReplayColumn(string Label, ReplaySortKey? Key, double Body,
                                    bool RightAligned = false, CellStyle Style = CellStyle.Normal)
{
    public double Width => Body + (IsSortable ? Data.SortMark.SeatWidth : 0);

    public bool IsSortable => Key is not null;
}

internal sealed record ReplayCell(string Text, double Width, bool RightAligned, CellStyle Style)
{
    public double Cushion { get; init; }

    public Avalonia.Media.IBrush CushionBrush => Cushion <= 0
        ? Avalonia.Media.Brushes.Transparent
        : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(
            (byte)Math.Round(Cushion * 255, MidpointRounding.AwayFromZero), 0x5b, 0xc8, 0xff));

    public bool IsMuted => Style == CellStyle.Muted;
    public bool IsP1 => Style == CellStyle.P1;
    public bool IsP2 => Style == CellStyle.P2;
}

internal static class ReplayColumns
{
    public static readonly ReplayColumn[] Story =
    [
        new("日時", ReplaySortKey.DateTime, 118, Style: CellStyle.Muted),
        new("難易度", ReplaySortKey.Difficulty, 84),
        new("1P", ReplaySortKey.P1Character, 76, Style: CellStyle.P1),
        new("Player1", ReplaySortKey.P1Name, 118),
        new("スコア", ReplaySortKey.Score, 112, RightAligned: true),
        new("残機", ReplaySortKey.Lives, 52, RightAligned: true),
        new("到達", ReplaySortKey.Reach, 56, RightAligned: true),
        new("時間", ReplaySortKey.Time, 58, RightAligned: true),
        new("結果", ReplaySortKey.Result, 56, Style: CellStyle.Muted),
        new(RecordKinds.Header, ReplaySortKey.Kind, RecordKinds.Width, Style: CellStyle.Muted),
    ];

    public static readonly ReplayColumn[] Match =
    [
        new("日時", ReplaySortKey.DateTime, 118, Style: CellStyle.Muted),
        new("1P", ReplaySortKey.P1Character, 76, Style: CellStyle.P1),
        new("Player1", ReplaySortKey.P1Name, 118),
        new("2P", ReplaySortKey.P2Character, 76, Style: CellStyle.P2),
        new("Player2", ReplaySortKey.P2Name, 118),
        new("難易度", ReplaySortKey.Difficulty, 84),
        new("合計", ReplaySortKey.Time, 52, RightAligned: true),
        new(RecordKinds.Header, ReplaySortKey.Kind, RecordKinds.Width, Style: CellStyle.Muted),
    ];

    public const double OwnMarkWidth = 30;

    public const double RoundTimesWidth = 172;

    public static ReplayColumn[] For(ReplaySection section)
        => section == ReplaySection.Match ? Match : Story;
}

internal sealed record RoundTime(int? Frames, int? WinnerSide)
{
    public bool HasNext { get; init; }

    public string Text => ReplayFormat.Frames(Frames);

    public bool IsP1Win => WinnerSide == 1;

    public bool IsP2Win => WinnerSide == 2;
}

internal sealed class ReplayListRow
{
    public required long ReplayId { get; init; }

    public required long? SessionId { get; init; }

    public required int SessionCount { get; init; }

    public required ReplaySection Section { get; init; }

    public required int? Mode { get; init; }

    public required int? Difficulty { get; init; }

    public string DifficultyText => ReplayLabels.Difficulty(Difficulty);

    public required DateTime? PlayedAt { get; init; }

    public required bool PlayedAtHasTime { get; init; }

    public string PlayedAtText
        => PlayedAt is not DateTime t ? ReplayFormat.Missing
           : t.ToString(PlayedAtHasTime ? "yy/MM/dd HH:mm" : "yy/MM/dd", CultureInfo.InvariantCulture);

    public required int? P1Character { get; init; }

    public required int? P2Character { get; init; }

    public string P1CharacterText => ReplayLabels.Character(P1Character);

    public string P2CharacterText => ReplayLabels.Character(P2Character);

    public required string? P1Name { get; init; }

    public required string? P2Name { get; init; }

    public string P1NameText => P1Name ?? ReplayFormat.Missing;

    public string P2NameText => P2Name ?? ReplayFormat.Missing;

    public required OwnSide Own { get; init; }

    public bool IsOwn => Own != OwnSide.None;

    public bool IsOwnP1 => Own == OwnSide.P1;

    public bool IsOwnP2 => Own == OwnSide.P2;

    public string OwnMark => Own == OwnSide.None ? "" : "●";

    public required MatchMode MatchMode { get; init; }

    public string MatchModeText => ReplayLabels.MatchMode(MatchMode);

    public required long? FinalScore { get; init; }

    public string FinalScoreText => FinalScore is long v
        ? v.ToString("N0", CultureInfo.InvariantCulture) : ReplayFormat.Missing;

    public required double? Lives { get; init; }

    public string LivesText => ReplayFormat.Lives(Lives);

    public required int? ReachStage { get; init; }

    public required int? ReachRound { get; init; }

    public string ReachText => ReachStage is int s
        ? "S" + s.ToString(CultureInfo.InvariantCulture)
          + (ReachRound is int r ? "R" + r.ToString(CultureInfo.InvariantCulture) : "")
        : ReplayFormat.Missing;

    public int? ReachOrder => ReachStage is int s ? s * 100 + (ReachRound ?? 0) : null;

    public required long? TotalFrames { get; init; }

    public string TotalTimeText => ReplayFormat.Frames(TotalFrames);

    public required bool? Completed { get; init; }

    public string ResultText => Completed switch
    {
        true => "完走",
        false => "中断",
        null => ReplayFormat.Missing,
    };

    public required IReadOnlyList<RoundTime> Rounds { get; init; }

    public required string? DecodeStatus { get; init; }

    public required string? Source { get; init; }

    public required string? FullPath { get; init; }

    public required int? OwnOverride { get; init; }

    public required RecordKind Kind { get; init; }

    public string KindText => RecordKinds.Text(Kind);

    public bool HasNoRounds => Rounds.Count == 0;

    public bool IsMatchRow => Section == ReplaySection.Match;

    private ReplayCell[]? _cells;

    public IReadOnlyList<ReplayCell> Cells
    {
        get
        {
            if (_cells is not null) return _cells;
            var cols = ReplayColumns.For(Section);
            var cells = new ReplayCell[cols.Length];
            for (var i = 0; i < cols.Length; i++)
                cells[i] = new ReplayCell(Text(cols[i].Key), cols[i].Width, cols[i].RightAligned, cols[i].Style);
            return _cells = cells;
        }
    }

    public string Text(ReplaySortKey? key) => key switch
    {
        ReplaySortKey.DateTime => PlayedAtText,
        ReplaySortKey.Difficulty => DifficultyText,
        ReplaySortKey.P1Character => P1CharacterText,
        ReplaySortKey.P2Character => P2CharacterText,
        ReplaySortKey.P1Name => P1NameText,
        ReplaySortKey.P2Name => P2NameText,
        ReplaySortKey.Score => FinalScoreText,
        ReplaySortKey.Lives => LivesText,
        ReplaySortKey.Reach => ReachText,
        ReplaySortKey.Time => TotalTimeText,
        ReplaySortKey.Result => ResultText,
        ReplaySortKey.OwnSide => OwnMark,
        ReplaySortKey.Kind => KindText,
        _ => "",
    };
}

internal static class ReplayLabels
{
    public static readonly string[] Characters =
    [
        "霊夢", "魔理沙", "咲夜", "妖夢", "鈴仙", "チルノ", "リリカ", "ミスティア",
        "てゐ", "幽香", "文", "メディスン", "小町", "映姫", "メルラン", "ルナサ",
    ];

    public static readonly int[] DisplayOrder =
        [0, 1, 2, 3, 4, 5, 6, 14, 15, 7, 8, 10, 11, 9, 12, 13];

    public static int? DisplayRank(int? id)
    {
        if (id is not int v) return null;
        var at = Array.IndexOf(DisplayOrder, v);
        return at >= 0 ? at : null;
    }

    public static readonly string[] Difficulties = ["Easy", "Normal", "Hard", "Lunatic", "Extra"];

    public const string HumanVsHumanText = "人対人";
    public const string HumanVsCpuText = "人対式";
    public const string CpuVsHumanText = "式対人";
    public const string CpuVsCpuText = "式対式";

    public const string CpuName = "CPU";

    public static string Character(int? v)
        => v is int i && i >= 0 && i < Characters.Length ? Characters[i] : ReplayFormat.Missing;

    public static string Difficulty(int? v)
        => v is int i && i >= 0 && i < Difficulties.Length ? Difficulties[i] : ReplayFormat.Missing;

    public static string MatchMode(MatchMode m) => m switch
    {
        Data.MatchMode.HumanVsHuman => HumanVsHumanText,
        Data.MatchMode.HumanVsCpu => HumanVsCpuText,
        Data.MatchMode.CpuVsHuman => CpuVsHumanText,
        Data.MatchMode.CpuVsCpu => CpuVsCpuText,
        _ => ReplayFormat.Missing,
    };
}

internal static class ReplayFormat
{
    public const string Missing = "—";

    public static string Frames(long? frames)
    {
        if (frames is not long f) return Missing;
        var seconds = Math.Round(f / 60.0, MidpointRounding.ToEven);
        var minutes = (int)(seconds / 60);
        var rest = (int)(seconds - minutes * 60);
        return minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + rest.ToString("00", CultureInfo.InvariantCulture);
    }

    public static string FineFrames(long? frames)
    {
        if (frames is not long f) return Missing;
        var tenths = (Math.Abs(f) + 3) / 6;
        var minutes = tenths / 600;
        var rest = tenths - minutes * 600;
        return (f < 0 ? "-" : "")
               + minutes.ToString("00", CultureInfo.InvariantCulture) + ":"
               + (rest / 10).ToString("00", CultureInfo.InvariantCulture) + "."
               + (rest % 10).ToString(CultureInfo.InvariantCulture);
    }

    public static string SignedFineFrames(long frames)
        => (frames < 0 ? "-" : "+") + FineFrames(Math.Abs(frames));

    public static string Frames(int? frames) => Frames(frames is int v ? v : (long?)null);

    public static string Lives(double? v)
    {
        if (v is not double d) return Missing;
        return Math.Abs(d - Math.Round(d)) < 1e-9
            ? ((long)Math.Round(d)).ToString(CultureInfo.InvariantCulture)
            : d.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
