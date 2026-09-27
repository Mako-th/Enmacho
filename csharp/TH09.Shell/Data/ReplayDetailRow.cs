using System.Globalization;

namespace TH09.Shell.Data;

internal enum DetailField
{
    Label,
    Opponent,
    Field,
    Bgm,
    EndScore,
    EndScoreExCb,
    SegmentScore,
    ClearBonus,
    SegmentExCb,
    Lives,
    Life,
    Time,
    Cards,
    Boss,
    Reversal,
    Quick,
    Winner,
}

internal sealed record ReplayDetailColumn(string Label, DetailField Field, double Width,
                                          bool RightAligned = false,
                                          CellStyle Style = CellStyle.Normal);

internal static class ReplayDetailColumns
{
    public static readonly ReplayDetailColumn[] Match =
    [
        new("R", DetailField.Label, 46),
        new("相手", DetailField.Opponent, 78, Style: CellStyle.P2),
        new("ステージ", DetailField.Field, 96, Style: CellStyle.Muted),
        new("戦闘曲", DetailField.Bgm, 292, Style: CellStyle.Muted),
        new("終了スコア", DetailField.EndScore, 112, RightAligned: true),
        new("終了スコア（CB除く）", DetailField.EndScoreExCb, 130, RightAligned: true, Style: CellStyle.Muted),
        new("残ライフ", DetailField.Life, 78, RightAligned: true),
        new("時間", DetailField.Time, 58, RightAligned: true),
        new("カード", DetailField.Cards, 52, RightAligned: true),
        new("ボス", DetailField.Boss, 46, RightAligned: true),
        new("リバサ", DetailField.Reversal, 46, RightAligned: true),
        new("クイック（カード-ボス）", DetailField.Quick, 130, RightAligned: true),
        new("勝ち", DetailField.Winner, 52),
    ];

    public static readonly ReplayDetailColumn[] Story =
    [
        new("面", DetailField.Label, 56),
        new("相手", DetailField.Opponent, 72, Style: CellStyle.P2),
        new("ステージ", DetailField.Field, 96, Style: CellStyle.Muted),
        new("戦闘曲", DetailField.Bgm, 292, Style: CellStyle.Muted),
        new("終了スコア", DetailField.EndScore, 110, RightAligned: true),
        new("終了スコア（CB除く）", DetailField.EndScoreExCb, 130, RightAligned: true, Style: CellStyle.Muted),
        new("区間スコア", DetailField.SegmentScore, 96, RightAligned: true),
        new("うち CB", DetailField.ClearBonus, 96, RightAligned: true, Style: CellStyle.Muted),
        new("区間（CB除く）", DetailField.SegmentExCb, 110, RightAligned: true, Style: CellStyle.Muted),
        new("残機", DetailField.Lives, 48, RightAligned: true),
        new("残ライフ", DetailField.Life, 62, RightAligned: true),
        new("時間", DetailField.Time, 54, RightAligned: true),
        new("カード", DetailField.Cards, 52, RightAligned: true),
        new("ボス", DetailField.Boss, 46, RightAligned: true),
        new("リバサ", DetailField.Reversal, 46, RightAligned: true),
        new("クイック（カード-ボス）", DetailField.Quick, 130, RightAligned: true),
    ];

    public static ReplayDetailColumn[] For(ReplaySection section)
        => section == ReplaySection.Match ? Match : Story;
}

internal sealed record SidePair(string P1, string P2)
{
    public string Text => P1 + "/" + P2;
}

internal sealed record ReplayDetailCell(string Text, SidePair? Pair, double Width,
                                        bool RightAligned, CellStyle Style)
{
    public bool IsPair => Pair is not null;
    public bool IsPlain => Pair is null;
    public bool IsMuted => Style == CellStyle.Muted;
    public bool IsP1 => Style == CellStyle.P1;
    public bool IsP2 => Style == CellStyle.P2;
}

internal sealed record SideCounters(int? Spell, int? QuickSpell, int? QuickBoss,
                                    int? Boss, int? Reversal)
{
    public static readonly SideCounters Unknown = new(null, null, null, null, null);
}

internal sealed class ReplayDetailRow
{
    public required ReplaySection Section { get; init; }

    public required long RoundRecordId { get; init; }

    public required long? StageRecordId { get; init; }

    public required int? StageNumber { get; init; }

    public required int? RoundNumber { get; init; }

    public required int? Opponent { get; init; }

    public required int? FieldId { get; init; }

    public required int? BattleBgmId { get; init; }

    public required long? EndScore { get; init; }

    public required long? SegmentScore { get; init; }

    public required long? ClearBonus { get; init; }

    public required double? Lives { get; init; }

    public required int? Life1Raw { get; init; }

    public required int? Life2Raw { get; init; }

    public required int? Frames { get; init; }

    public required int? WinnerSide { get; init; }

    public required SideCounters P1 { get; init; }

    public required SideCounters P2 { get; init; }

    public bool IsMatchRow => Section == ReplaySection.Match;

    public bool IsP1Win => WinnerSide == 1;

    public bool IsP2Win => WinnerSide == 2;


    public string LabelText
    {
        get
        {
            var r = RoundNumber is int n ? "R" + n.ToString(CultureInfo.InvariantCulture) : "";
            if (StageNumber is not int s) return r.Length == 0 ? ReplayFormat.Missing : r;
            return "S" + s.ToString(CultureInfo.InvariantCulture) + r;
        }
    }

    public string OpponentText => ReplayLabels.Character(Opponent);

    public string FieldText => ReplayDetailLabels.Field(FieldId);

    public string BgmText => ReplayDetailLabels.Bgm(BattleBgmId);

    public long? EndScoreExCb => EndScore is long v ? v - (ClearBonus ?? 0) : null;

    public long? SegmentExCb => SegmentScore is long v ? v - (ClearBonus ?? 0) : null;

    public SidePair LifePair => new(ReplayDetailFormat.Life(Life1Raw), ReplayDetailFormat.Life(Life2Raw));

    public SidePair CardsPair => new(ReplayDetailFormat.Count(P1.Spell),
                                     ReplayDetailFormat.Count(P2.Spell));

    public SidePair BossPair => new(ReplayDetailFormat.Count(P1.Boss),
                                    ReplayDetailFormat.Count(P2.Boss));

    public SidePair ReversalPair => new(ReplayDetailFormat.Count(P1.Reversal),
                                        ReplayDetailFormat.Count(P2.Reversal));

    public SidePair QuickPair => new(ReplayDetailFormat.Split(P1.QuickSpell, P1.QuickBoss),
                                     ReplayDetailFormat.Split(P2.QuickSpell, P2.QuickBoss));

    public string WinnerText => WinnerSide switch
    {
        1 => "1P",
        2 => "2P",
        _ => ReplayFormat.Missing,
    };

    public string Text(DetailField field) => field switch
    {
        DetailField.Label => LabelText,
        DetailField.Opponent => OpponentText,
        DetailField.Field => FieldText,
        DetailField.Bgm => BgmText,
        DetailField.EndScore => ReplayDetailFormat.Score(EndScore),
        DetailField.EndScoreExCb => ReplayDetailFormat.Score(EndScoreExCb),
        DetailField.SegmentScore => ReplayDetailFormat.Score(SegmentScore),
        DetailField.ClearBonus => ReplayDetailFormat.Score(ClearBonus),
        DetailField.SegmentExCb => ReplayDetailFormat.Score(SegmentExCb),
        DetailField.Lives => ReplayFormat.Lives(Lives),
        DetailField.Life => IsMatchRow ? LifePair.Text : ReplayDetailFormat.Life(Life1Raw),
        DetailField.Time => ReplayFormat.Frames(Frames),
        DetailField.Cards => CardsPair.Text,
        DetailField.Boss => BossPair.Text,
        DetailField.Reversal => ReversalPair.Text,
        DetailField.Quick => QuickPair.Text,
        DetailField.Winner => WinnerText,
        _ => throw new InvalidOperationException(
                 "DetailField." + field + " に出す字が書かれていない（ReplayDetailRow.Text）"),
    };

    private ReplayDetailCell[]? _cells;

    public IReadOnlyList<ReplayDetailCell> Cells
    {
        get
        {
            if (_cells is not null) return _cells;
            var cols = ReplayDetailColumns.For(Section);
            var cells = new ReplayDetailCell[cols.Length];
            for (var i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                cells[i] = new ReplayDetailCell(Text(c.Field), PairOf(c.Field),
                                                c.Width, c.RightAligned, StyleOf(c));
            }
            return _cells = cells;
        }
    }

    private SidePair? PairOf(DetailField field) => field switch
    {
        DetailField.Cards => CardsPair,
        DetailField.Boss => BossPair,
        DetailField.Reversal => ReversalPair,
        DetailField.Quick => QuickPair,
        DetailField.Life when IsMatchRow => LifePair,
        _ => null,
    };

    private CellStyle StyleOf(ReplayDetailColumn column)
        => column.Field == DetailField.Winner
           ? (IsP1Win ? CellStyle.P1 : IsP2Win ? CellStyle.P2 : CellStyle.Muted)
           : column.Style;
}

internal sealed class ReplayDetailHeader
{
    public required long? ReplayId { get; init; }

    public required long? SessionId { get; init; }

    public required ReplaySection Section { get; init; }

    public required int? Mode { get; init; }

    public required int? Difficulty { get; init; }

    public required DateTime? PlayedAt { get; init; }

    public required bool PlayedAtHasTime { get; init; }

    public required int? P1Character { get; init; }

    public required int? P2Character { get; init; }

    public required string? P1Name { get; init; }

    public required string? P2Name { get; init; }

    public required MatchMode MatchMode { get; init; }

    public required int RoundCount { get; init; }

    public required long? TotalFrames { get; init; }

    public bool IsMatch => Section == ReplaySection.Match;

    public string P1CharacterText => ReplayLabels.Character(P1Character);

    public string P2CharacterText => ReplayLabels.Character(P2Character);

    public string P1NameText => P1Name ?? "";

    public string P2NameText => P2Name ?? "";

    public string DifficultyText => ReplayLabels.Difficulty(Difficulty);

    public string ModeText => ReplayDetailLabels.Mode(Mode);

    public string PlayedAtText
        => PlayedAt is not DateTime t ? ReplayFormat.Missing
           : t.ToString(PlayedAtHasTime ? "yy/MM/dd HH:mm" : "yy/MM/dd", CultureInfo.InvariantCulture);

    public string TotalTimeText => ReplayFormat.Frames(TotalFrames);

    public string RoundCountText => RoundCount.ToString(CultureInfo.InvariantCulture) + " ラウンド";

    public string SessionText
        => SessionId is long s ? "session " + s.ToString(CultureInfo.InvariantCulture) : "";

    public string SubtitleText
    {
        get
        {
            var parts = new List<string>(7) { PlayedAtText };
            if (SessionText.Length > 0) parts.Add(SessionText);
            parts.Add(ModeText);
            if (IsMatch && MatchMode != MatchMode.Unknown) parts.Add(ReplayLabels.MatchMode(MatchMode));
            parts.Add(DifficultyText);
            parts.Add(RoundCountText);
            parts.Add(TotalTimeText);
            return string.Join("　", parts);
        }
    }

    public string TitleText
    {
        get
        {
            var me = Join(P1CharacterText, P1NameText);
            return IsMatch ? me + " vs " + Join(P2CharacterText, P2NameText) : me;
        }
    }

    private static string Join(string character, string name)
        => name.Length == 0 ? character : character + " " + name;

    public string CrumbText
        => PlayedAtText + "　" + (IsMatch
               ? P1CharacterText + " vs " + P2CharacterText
               : P1CharacterText);
}

internal sealed class ReplayDetail
{
    public ReplayDetailHeader? Header { get; init; }

    public IReadOnlyList<ReplayDetailRow> Rows { get; init; } = [];

    public string? Status { get; init; }

    public int LinkCount { get; init; }

    public int CountedRounds { get; init; }
}

internal static class ReplayDetailLabels
{
    public static readonly string[] Modes = ["Story", "Extra", "Match"];

    public static string Mode(int? v)
        => v is int i && i >= 0 && i < Modes.Length ? Modes[i] : ReplayFormat.Missing;

    public static readonly string[] Fields =
    [
        "迷いの竹林", "幻草原", "白玉楼階段", "永遠亭", "霧の湖", "幽明結界", "妖怪獣道", "迷いの竹林",
        "太陽の畑", "大蝦蟇の池", "無名の丘", "再思の道", "無縁塚", "太陽の畑", "無名の丘", "無縁塚",
    ];

    public static readonly string[] Bgms =
    [
        "春色小径 ～ Colorful Path", "オリエンタルダークフライト", "フラワリングナイト",
        "東方妖々夢 ～ Ancient Temple", "狂気の瞳 ～ Invisible Full Moon", "おてんば恋娘の冒険",
        "幽霊楽団 ～ Phantom Ensemble", "もう歌しか聞こえない ～ Flower Mix", "お宇佐さまの素い幡",
        "今昔幻想郷 ～ Flower Land", "風神少女 (Short Version)", "ポイズンボディ ～ Forsaken Doll",
        "彼岸帰航 ～ Riverside View", "六十年目の東方裁判 ～ Fate of Sixty Years",
    ];

    public static string Field(int? v) => Name(Fields, v);

    public static string Bgm(int? v) => Name(Bgms, v);

    private static string Name(string[] table, int? v)
        => v is not int i ? ReplayFormat.Missing
           : i >= 0 && i < table.Length ? table[i]
           : "ID " + i.ToString(CultureInfo.InvariantCulture);
}

internal static class ReplayDetailFormat
{
    public static string Score(long? v)
        => v is long x ? x.ToString("N0", CultureInfo.InvariantCulture) : ReplayFormat.Missing;

    public static string Life(int? raw)
        => raw is int v ? (v / 2.0).ToString("0.0", CultureInfo.InvariantCulture) : ReplayFormat.Missing;

    public static string Count(int? value)
        => value is int v ? v.ToString(CultureInfo.InvariantCulture) : ReplayFormat.Missing;

    private const string SplitMark = "-";

    public static string Split(int? left, int? right)
        => left is int a && right is int b
           ? a.ToString(CultureInfo.InvariantCulture) + SplitMark
             + b.ToString(CultureInfo.InvariantCulture)
           : ReplayFormat.Missing;
}
