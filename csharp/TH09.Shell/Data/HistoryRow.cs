using System.Globalization;

namespace TH09.Shell.Data;

internal enum HistoryKind
{
    LivePlay,

    ReplayPlayback,

    All,
}

internal enum HistorySortKey
{
    SessionId,
    StartedAt,
    Mode,
    Difficulty,
    P1Character,
    P2Character,
    Lives,
    Score,
    Status,
    HasReplay,
    Kind,
}

internal sealed record HistoryColumn(string Label, HistorySortKey? Key, double Body,
                                     bool RightAligned = false, CellStyle Style = CellStyle.Normal)
{
    public double Width => Body + (IsSortable ? Data.SortMark.SeatWidth : 0);

    public bool IsSortable => Key is not null;
}

internal static class HistoryColumns
{
    public static readonly HistoryColumn[] All =
    [
        new("ID", HistorySortKey.SessionId, 70, RightAligned: true, Style: CellStyle.Muted),
        new("開始日時", HistorySortKey.StartedAt, 180, Style: CellStyle.Muted),
        new("Mode", HistorySortKey.Mode, 90),
        new("難易度", HistorySortKey.Difficulty, 95),
        new("1P", HistorySortKey.P1Character, 115, Style: CellStyle.P1),
        new("2P", HistorySortKey.P2Character, 115, Style: CellStyle.P2),
        new("最終残機", HistorySortKey.Lives, 100, RightAligned: true),
        new("最終スコア", HistorySortKey.Score, 140, RightAligned: true),
        new("状態", HistorySortKey.Status, 100),
        new("Replay", HistorySortKey.HasReplay, 85),
        new(RecordKinds.Header, HistorySortKey.Kind, RecordKinds.Width, Style: CellStyle.Muted),
    ];
}

internal sealed class HistoryRow
{
    public required long SessionId { get; init; }

    public required string? StartedAtRaw { get; init; }

    public string StartedAtText
    {
        get
        {
            if (StartedAtRaw is not string s || s.Length == 0) return ReplayFormat.Missing;
            var head = s.Length >= 19 ? s[..19] : s;
            return head.Replace('T', ' ');
        }
    }

    public required string? Status { get; init; }

    public string StatusText => Status ?? ReplayFormat.Missing;

    public required int? GameMode { get; init; }

    public string ModeText => HistoryLabels.Mode(GameMode);

    public bool IsStory => GameMode is 0 or 1;

    public required int? Difficulty { get; init; }

    public string DifficultyText => ReplayLabels.Difficulty(Difficulty);

    public required int? P1Character { get; init; }

    public required int? P2Character { get; init; }

    public string P1CharacterText => ReplayLabels.Character(P1Character);

    public string P2CharacterText => IsStory ? ReplayFormat.Missing : ReplayLabels.Character(P2Character);

    public required string? ExecutionType { get; init; }

    public bool IsReplayPlayback
        => string.Equals(ExecutionType, HistoryLabels.ReplayPlaybackType, StringComparison.Ordinal);

    public required bool HasReplay { get; init; }

    public string HasReplayText => HasReplay ? "有" : "無";

    public required RecordKind Kind { get; init; }

    public string KindText => RecordKinds.Text(Kind);

    public required long? ReplayId { get; init; }

    public required int ReplayCount { get; init; }

    public required double? FinalLives { get; init; }

    public string FinalLivesText
        => IsStory && FinalLives is not null ? ReplayFormat.Lives(FinalLives) : ReplayFormat.Missing;

    public required long? FinalScore { get; init; }

    public string FinalScoreText
        => IsStory && FinalScore is long v
           ? v.ToString("N0", CultureInfo.InvariantCulture) : ReplayFormat.Missing;

    public bool IsProtected { get; set; }

    public bool IsRecordless
        => !IsStory || string.Equals(Status, "running", StringComparison.Ordinal);

    private ReplayCell[]? _cells;

    public IReadOnlyList<ReplayCell> Cells
    {
        get
        {
            if (_cells is not null) return _cells;
            var cols = HistoryColumns.All;
            var cells = new ReplayCell[cols.Length];
            for (var i = 0; i < cols.Length; i++)
                cells[i] = new ReplayCell(Text(cols[i].Key), cols[i].Width, cols[i].RightAligned, cols[i].Style);
            return _cells = cells;
        }
    }

    public string Text(HistorySortKey? key) => key switch
    {
        HistorySortKey.SessionId => SessionId.ToString(CultureInfo.InvariantCulture),
        HistorySortKey.StartedAt => StartedAtText,
        HistorySortKey.Mode => ModeText,
        HistorySortKey.Difficulty => DifficultyText,
        HistorySortKey.P1Character => P1CharacterText,
        HistorySortKey.P2Character => P2CharacterText,
        HistorySortKey.Lives => FinalLivesText,
        HistorySortKey.Score => FinalScoreText,
        HistorySortKey.Status => StatusText,
        HistorySortKey.HasReplay => HasReplayText,
        HistorySortKey.Kind => KindText,
        _ => "",
    };
}

internal static class HistoryLabels
{
    public static readonly string[] Modes = ["Story", "Extra", "Match"];

    public const string ReplayPlaybackType = "Replay Playback";

    public static string Mode(int? v)
        => v is int i && i >= 0 && i < Modes.Length ? Modes[i] : ReplayFormat.Missing;

    public static string Kind(HistoryKind kind) => kind switch
    {
        HistoryKind.LivePlay => "実プレイ",
        HistoryKind.ReplayPlayback => "リプレイ再生",
        _ => "全て",
    };

    public static string ConfigValue(HistoryKind kind) => kind switch
    {
        HistoryKind.LivePlay => "live",
        HistoryKind.ReplayPlayback => "replay",
        _ => "all",
    };

    public static HistoryKind KindFromConfig(string? value) => value switch
    {
        "replay" => HistoryKind.ReplayPlayback,
        "all" => HistoryKind.All,
        _ => HistoryKind.LivePlay,
    };
}
