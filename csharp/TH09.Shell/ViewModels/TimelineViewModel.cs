using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TH09.Analysis;
using TH09.Shell.Data;

namespace TH09.Shell.ViewModels;


internal sealed record TrendRoundTab(int Index, string Label)
{
    public bool IsActive { get; init; }
}

internal sealed partial class TrendSeries : ObservableObject
{
    public required string Id { get; init; }
    public required string Base { get; init; }
    public required int Bi { get; init; }
    public required int Side { get; init; }
    public required string Group { get; init; }
    public required string Label { get; init; }
    public required double[] X { get; init; }
    public required double?[] Y { get; init; }
    public required double Lo { get; init; }
    public required double Hi { get; init; }
    public required bool Fixed { get; init; }
    public required (double V, string T)[] Marks { get; init; }

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    [ObservableProperty]
    public partial Avalonia.Media.IBrush? Swatch { get; set; }

    public Action? Changed { get; init; }

    public string RangeText => Fixed
        ? Lo.ToString("0.###", CultureInfo.InvariantCulture) + "–"
          + Hi.ToString("0.###", CultureInfo.InvariantCulture)
        : "";

    partial void OnIsOnChanged(bool value) => Changed?.Invoke();
}

internal sealed class TrendGroup
{
    public required string Name { get; init; }
    public required List<TrendSeries> Items { get; init; }

    public string OnArg => Name + "\t1";

    public string OffArg => Name + "\t0";
}

internal sealed class TrendLabelGroup
{
    public required string Name { get; init; }
    public required List<KvRow> Items { get; init; }
}

internal sealed partial class TrendBandRow : ObservableObject
{
    public required TimelineBand Band { get; init; }
    public string Name => Band.Name;
    public int Side => Band.Side;
    public string CountText => Band.Runs.Count.ToString(CultureInfo.InvariantCulture);

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    [ObservableProperty]
    public partial Avalonia.Media.IBrush? Swatch { get; set; }

    public Action? Changed { get; init; }

    partial void OnIsOnChanged(bool value) => Changed?.Invoke();
}

internal sealed partial class TrendEventRow : ObservableObject
{
    public required TimelineEvent Event { get; init; }
    public string Name => Event.Name;
    public int Side => Event.Side;
    public string Glyph => Event.Glyph;
    public string CountText => Event.Times.Length.ToString(CultureInfo.InvariantCulture);

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    [ObservableProperty]
    public partial Avalonia.Media.IBrush? Swatch { get; set; }

    public Action? Changed { get; init; }

    partial void OnIsOnChanged(bool value) => Changed?.Invoke();
}

internal sealed partial class TimelineViewModel : ObservableObject
{
    public const int MaxPoints = 600;

    private List<TimelineRound> _rounds = [];

    public long? SessionId { get; private set; }

    public List<TrendSeries> Series { get; } = [];

    public ObservableCollection<TrendGroup> Groups { get; } = [];

    public ObservableCollection<TrendLabelGroup> LabelGroups { get; } = [];

    public ObservableCollection<TrendBandRow> Bands { get; } = [];

    public ObservableCollection<TrendEventRow> Events { get; } = [];

    public ObservableCollection<TrendRoundTab> RoundTabs { get; } = [];

    [ObservableProperty]
    public partial int RoundIndex { get; set; }

    [ObservableProperty]
    public partial string? StatusText { get; set; }

    [ObservableProperty]
    public partial string Mode { get; set; } = ModeOver;

    [ObservableProperty]
    public partial string ColorMode { get; set; } = ColorPlayer;

    [ObservableProperty]
    public partial int Revision { get; set; }

    public const string ModeOver = "over";
    public const string ModeStack = "stack";
    public const string ModeSolo = "solo";
    public const string ColorPlayer = "player";
    public const string ColorStatus = "status";

    public double T0 { get; private set; }

    public double T1 { get; private set; }

    public bool HasRound => _rounds.Count > 0 && RoundIndex < _rounds.Count;

    public TimelineRound? Round => HasRound ? _rounds[RoundIndex] : null;

    public string CountNote
    {
        get
        {
            if (_rounds.Count == 0) return "";
            int on = Series.Count(s => s.IsOn);
            return string.Create(CultureInfo.InvariantCulture,
                $"{_rounds.Count} ラウンド中 {RoundIndex + 1} 本目 ／ 系列 {Series.Count} 本のうち {on} 本を表示"
                + $" ／ {Round?.TickCount ?? 0} tick");
        }
    }

    public bool ByStatus => Mode == ModeOver || ColorMode == ColorStatus;

    public bool IsOverlay => Mode == ModeOver;

    public bool IsStacked => Mode == ModeStack;

    public bool IsSolo => Mode == ModeSolo;

    public bool IsColorPlayer => ColorMode == ColorPlayer;

    public bool IsColorStatus => ColorMode == ColorStatus;

    public bool HasLabels => LabelGroups.Count > 0;


    public void Load(string layer0Path, long sessionId)
    {
        SessionId = sessionId;
        _rounds = [];
        StatusText = null;
        try
        {
            using var db = new AnalysisDb(layer0Path);
            _rounds = TimelineReader.LoadSession(db, sessionId);
            if (_rounds.Count == 0)
                StatusText = "Layer 0 にこのセッションの tick がありません（session "
                             + sessionId.ToString(CultureInfo.InvariantCulture) + "）。";
        }
        catch (Exception ex)
        {
            StatusText = "Layer 0 を読めませんでした: " + ex.Message;
        }
        RoundIndex = 0;
        BuildRoundTabs();
        BuildRound();
    }

    [RelayCommand]
    private void ShowRound(int index)
    {
        if (index < 0 || index >= _rounds.Count) return;
        RoundIndex = index;
    }

    [RelayCommand]
    private void SetMode(string mode) => Mode = mode;

    [RelayCommand]
    private void SetColorMode(string mode) => ColorMode = mode;

    [RelayCommand]
    private void SetGroup(string arg)
    {
        int at = arg.LastIndexOf('\t');
        if (at < 0) return;
        string name = arg[..at];
        bool on = arg[(at + 1)..] == "1";
        foreach (var g in Groups)
        {
            if (name.Length > 0 && g.Name != name) continue;
            foreach (var s in g.Items) s.IsOn = on;
        }
    }

    partial void OnRoundIndexChanged(int value)
    {
        BuildRoundTabs();
        BuildRound();
    }

    partial void OnModeChanged(string value)
    {
        OnPropertyChanged(nameof(ByStatus));
        OnPropertyChanged(nameof(IsOverlay));
        OnPropertyChanged(nameof(IsStacked));
        OnPropertyChanged(nameof(IsSolo));
        Bump();
    }

    partial void OnColorModeChanged(string value)
    {
        OnPropertyChanged(nameof(ByStatus));
        OnPropertyChanged(nameof(IsColorPlayer));
        OnPropertyChanged(nameof(IsColorStatus));
        Bump();
    }

    private void Bump()
    {
        Revision++;
        var palette = Views.TrendPalette.Of(ThemeName);
        bool byStatus = ByStatus;
        foreach (var s in Series)
            s.Swatch = byStatus ? palette.Status(s.Bi) : palette.SideColor(s.Side);
        foreach (var b in Bands) b.Swatch = palette.SideColor(b.Side);
        foreach (var e in Events) e.Swatch = palette.SideColor(e.Side);
        OnPropertyChanged(nameof(CountNote));
        OnPropertyChanged(nameof(HasRound));
        OnPropertyChanged(nameof(HasLabels));
    }

    public const string ThemeName = "dark";

    private void BuildRoundTabs()
    {
        RoundTabs.Clear();
        foreach (var r in _rounds)
            RoundTabs.Add(new TrendRoundTab(r.Index, RoundTab(r)) { IsActive = r.Index == RoundIndex });
    }

    public static string RoundTab(TimelineRound r)
    {
        long? modeValue = null;
        foreach (var v in r.Col("mode") ?? [])
        {
            if (v.IsMissing) continue;
            modeValue = v.AsLong;
            break;
        }
        var (stage, no) = r.StageRound();
        if (modeValue == 2 || stage is null)
            return "R" + (r.Index + 1).ToString(CultureInfo.InvariantCulture);
        return "S" + stage.Value.ToString(CultureInfo.InvariantCulture)
               + "R" + no.ToString(CultureInfo.InvariantCulture);
    }


    private void BuildRound()
    {
        Series.Clear();
        Groups.Clear();
        LabelGroups.Clear();
        Bands.Clear();
        Events.Clear();
        T0 = T1 = 0;
        if (Round is not TimelineRound r)
        {
            Bump();
            return;
        }
        T0 = r.Seconds.Length > 0 ? Tidy(r.Seconds[0], 3) : 0;
        T1 = r.Seconds.Length > 0 ? Tidy(r.Seconds[^1], 3) : 0;

        BuildSeries(r);
        BuildLabels(r);
        foreach (var b in r.Bands())
        {
            if (b.Runs.Count == 0) continue;
            Bands.Add(new TrendBandRow { Band = b, IsOn = !b.DefaultOff, Changed = Bump });
        }
        foreach (var e in r.Events())
            Events.Add(new TrendEventRow { Event = e, IsOn = e.On, Changed = Bump });
        Bump();
    }

    private void BuildSeries(TimelineRound r)
    {
        var byBase = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var name in r.VisibleLines())
        {
            var b = FieldDisplay.PairBase(name);
            if (!byBase.TryGetValue(b, out var list)) byBase[b] = list = [];
            list.Add(name);
        }
        var ordered = byBase.Keys
            .OrderBy(b => TrendTables.OrderIndex(b))
            .ThenBy(b => b, StringComparer.Ordinal).ToList();

        for (int bi = 0; bi < ordered.Count; bi++)
        {
            string b = ordered[bi];
            var domain = TrendTables.Domain(b);
            foreach (var name in byBase[b].OrderBy(n => n, StringComparer.Ordinal))
            {
                var values = r.Series(name);
                if (values is null) continue;
                var pts = TimelineReader.Envelope(r.Seconds, values, MaxPoints);
                var xs = new double[pts.Count];
                var ys = new double?[pts.Count];
                bool any = false;
                double min = 0, max = 0;
                for (int i = 0; i < pts.Count; i++)
                {
                    xs[i] = Tidy(pts[i].T, 3);
                    double? v = pts[i].V;
                    if (v is double d && !IsFinite(d)) v = null;
                    ys[i] = v is double dv ? Tidy(dv, 3) : null;
                    if (ys[i] is not double y) continue;
                    if (!any) { min = max = y; any = true; }
                    else { if (y < min) min = y; if (y > max) max = y; }
                }
                if (!any) continue;
                var s = new TrendSeries
                {
                    Id = name, Base = b, Bi = bi, Side = FieldDisplay.SideOf(name),
                    Group = TrendTables.GroupOf(b),
                    Label = TrendTables.SeriesLabel(name, b),
                    X = xs, Y = ys,
                    Lo = domain?.Lo ?? min, Hi = domain?.Hi ?? max,
                    Fixed = domain is not null,
                    Marks = domain?.Marks ?? [],
                    Changed = Bump,
                };
                s.IsOn = TrendTables.DefaultOn.Contains(b, StringComparer.Ordinal);
                Series.Add(s);
            }
        }

        var groups = new List<TrendGroup>();
        var at = new Dictionary<string, TrendGroup>(StringComparer.Ordinal);
        foreach (var s in Series)
        {
            if (!at.TryGetValue(s.Group, out var g))
            {
                at[s.Group] = g = new TrendGroup { Name = s.Group, Items = [] };
                groups.Add(g);
            }
            g.Items.Add(s);
        }
        foreach (var g in groups) Groups.Add(g);
    }

    private void BuildLabels(TimelineRound r)
    {
        var have = new List<KeyValuePair<string, TimelineLabel>>(r.VisibleLabels());
        have.Add(new("__duration__", TimelineLabel.Of(TickValue.Float(r.Duration))));
        var map = new Dictionary<string, TimelineLabel>(StringComparer.Ordinal);
        foreach (var kv in have) map[kv.Key] = kv.Value;
        bool isMatch = map.TryGetValue("mode", out var m) && !m.Value.IsMissing && m.Value.AsLong == 2;

        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, fields, hideInMatch) in TrendTables.LabelGroups)
        {
            if (isMatch && hideInMatch)
            {
                foreach (var f in fields) used.Add(f);
                continue;
            }
            var items = new List<KvRow>();
            foreach (var f in fields)
            {
                if (isMatch && TrendTables.HideInMatch.Contains(f)) { used.Add(f); continue; }
                if (!map.TryGetValue(f, out var v)) continue;
                used.Add(f);
                items.Add(TrendTables.LabelText(f, v));
            }
            if (items.Count > 0) LabelGroups.Add(new TrendLabelGroup { Name = name, Items = items });
        }
        var rest = have.Where(kv => !used.Contains(kv.Key)
                                    && !TrendTables.DroppedClearBonus.Contains(kv.Key)).ToList();
        if (rest.Count > 0)
            LabelGroups.Add(new TrendLabelGroup
            {
                Name = "その他",
                Items = rest.Select(kv => TrendTables.LabelText(kv.Key, kv.Value)).ToList(),
            });
    }

    public static double Tidy(double v, int digits)
    {
        if (!double.IsFinite(v)) return v;
        try
        {
            var d = decimal.Parse(v.ToString("G17", CultureInfo.InvariantCulture),
                                  NumberStyles.Float, CultureInfo.InvariantCulture);
            return (double)Math.Round(d, digits, MidpointRounding.ToEven);
        }
        catch (OverflowException)
        {
            return v;
        }
    }

    private static bool IsFinite(double v) => !double.IsNaN(v) && v > -1e308 && v < 1e308;
}

internal static class TrendTables
{
    public static readonly string[] Order =
    [
        "life_raw", "gauge", "spell_points", "score_raw", "current_combo",
        "charge", "enemy_total", "bullet_fairy", "bullet_rival",
        "internal_rank", "lily_counter",
    ];

    public static int OrderIndex(string b)
    {
        int i = Array.IndexOf(Order, b);
        return i < 0 ? 99 : i;
    }

    public static readonly string[] DefaultOn = ["life_raw", "gauge", "spell_points"];

    public static (double Lo, double Hi, (double V, string T)[] Marks)? Domain(string b) => b switch
    {
        "gauge" => (0.0, 400.0, [(100, "1本"), (200, "★クイック可"), (300, "3本")]),
        "spell_points" => (0.0, 999990.0,
            [(100000, "10万"), (300000, "30万"), (500000, "50万"), (999990, "★カンスト＝開花")]),
        "life_raw" => (0.0, 5.0, [(1, ""), (2, ""), (3, ""), (4, "")]),
        "charge" => (0.0, 400.0, []),
        "internal_rank" => (1.0, 22.0, []),
        "lily_counter" => (0.0, 10000.0, [(10000, "★リリー出現")]),
        _ => null,
    };

    public static readonly (string Name, string[] Bases)[] SeriesGroups =
    [
        ("基本", ["life_raw", "gauge", "spell_points", "score_raw", "current_combo", "charge"]),
        ("自陣の敵", ["enemy_total", "enemy_fairy", "enemy_charge", "enemy_boss",
                      "c2_count", "c3_count", "boss_count", "boss_hp"]),
        ("弾", ["bullet_fairy", "bullet_rival"]),
        ("Ex / カード", ["ex_active", "ex_triggered",
                         "card_attack_level", "boss_card_attack_level"]),
        ("タイマー", ["zero_hit_timer", "invincible_timer", "combo_gauge_cur"]),
        ("CPU", ["cpu_dodge_mode", "cpu_quick_timer", "cpu_stand_timer",
                 "cpu_quick_timer_cur", "cpu_stand_timer_cur", "cpu_charge_instruction"]),
        ("共通", ["internal_rank", "lily_counter", "hit_damage_base"]),
    ];

    private static readonly Dictionary<string, string> GroupIndex = BuildGroupIndex();

    private static Dictionary<string, string> BuildGroupIndex()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, bases) in SeriesGroups)
            foreach (var b in bases) map[b] = name;
        return map;
    }

    public static string GroupOf(string b) => GroupIndex.TryGetValue(b, out var g) ? g : "その他";

    public static readonly (string Name, string[] Fields, bool HideInMatch)[] LabelGroups =
    [
        ("試合", ["mode", "difficulty", "stage_index", "__duration__"], false),
        ("ランク", ["initial_rank", "rank_interval", "rank_max"], false),
        ("ステージ", ["field_id", "battle_bgm_id"], false),
        ("1P / 2P", ["p1_character", "p2_character", "p1_control", "p2_control",
                     "p1_cpu_level", "p2_cpu_level"], false),
        ("残機", ["p1_lives", "p2_lives"], true),
        ("最大", ["p1_max_combo", "p2_max_combo"], false),
        ("カード / ボス / リバサ", ["p1_spell_attacks", "p2_spell_attacks",
                                    "p1_boss_attacks", "p2_boss_attacks",
                                    "p1_boss_reversals", "p2_boss_reversals"], false),
        ("クリアボーナス", ["clear_lives_bonus", "clear_total"], true),
        ("結果", ["p1_wins", "p2_wins", "result_winner", "pause_used"], false),
    ];

    public static readonly string[] DroppedClearBonus =
    [
        "clear_life_bonus", "clear_max_combo_bonus", "clear_spell_bonus",
        "clear_boss_bonus", "clear_reversal_bonus",
    ];

    public static readonly HashSet<string> HideInMatch = BuildHideInMatch();

    private static HashSet<string> BuildHideInMatch()
    {
        var s = new HashSet<string>(StringComparer.Ordinal)
        {
            "stage_index", "p1_lives", "p2_lives",
        };
        foreach (var n in DroppedClearBonus) s.Add(n);
        foreach (var (_n, fields, hide) in LabelGroups)
            if (hide) foreach (var f in fields) s.Add(f);
        return s;
    }

    public static string SeriesLabel(string name, string b)
    {
        string ja = SeriesJa.TryGetValue(b, out var v) ? v : b;
        int side = FieldDisplay.SideOf(name);
        return side == 0 ? ja : side.ToString(CultureInfo.InvariantCulture) + "P " + ja;
    }

    public static readonly Dictionary<string, string> SeriesJa = new(StringComparer.Ordinal)
    {
        ["life_raw"] = "ライフ", ["gauge"] = "ゲージ", ["spell_points"] = "スペルポイント",
        ["score_raw"] = "スコア", ["current_combo"] = "コンボ", ["charge"] = "溜め量",
        ["enemy_total"] = "自陣の敵数", ["bullet_fairy"] = "弾幕系の弾数",
        ["bullet_rival"] = "その他攻撃の弾数", ["internal_rank"] = "内部ランク",
        ["lily_counter"] = "リリーカウンタ", ["enemy_fairy"] = "自陣の妖精",
        ["enemy_boss"] = "自陣のボス扱い数", ["enemy_charge"] = "自陣の幽霊",
        ["boss_hp"] = "ボスHP", ["ex_active"] = "Ex 発生中", ["ex_triggered"] = "Ex 発動",
        ["combo_gauge_cur"] = "コンボゲージ", ["zero_hit_timer"] = "無被弾フレーム",
        ["card_attack_level"] = "カードLv", ["boss_card_attack_level"] = "ボスカードLv",
        ["invincible_timer"] = "無敵の残り", ["enemy_class_counts"] = "自陣の内訳(パック)",
        ["cpu_dodge_mode"] = "CPU 回避モード", ["cpu_quick_timer"] = "CPU クイックTimer",
        ["cpu_stand_timer"] = "CPU 棒立ちTimer", ["cpu_quick_timer_cur"] = "CPU クイックTimer(現)",
        ["cpu_stand_timer_cur"] = "CPU 棒立ちTimer(現)",
        ["cpu_charge_instruction"] = "CPU 溜め指示", ["hit_damage_base"] = "被弾ダメージ段位",
        ["player_state"] = "自機の状態",
        ["c2_count"] = "自陣の C2", ["c3_count"] = "自陣の C3",
        ["boss_count"] = "自陣のボス",
    };

    public static readonly string[] Fields =
    [
        "迷いの竹林", "幻草原", "白玉楼階段", "永遠亭", "霧の湖", "幽明結界",
        "妖怪獣道", "迷いの竹林", "太陽の畑", "大蝦蟇の池", "無名の丘", "再思の道",
        "無縁塚", "太陽の畑", "無名の丘", "無縁塚",
    ];

    public static readonly string[] Bgms =
    [
        "春色小径 ～ Colorful Path", "オリエンタルダークフライト", "フラワリングナイト",
        "東方妖々夢 ～ Ancient Temple", "狂気の瞳 ～ Invisible Full Moon",
        "おてんば恋娘の冒険", "幽霊楽団 ～ Phantom Ensemble",
        "もう歌しか聞こえない ～ Flower Mix", "お宇佐さまの素い幡",
        "今昔幻想郷 ～ Flower Land", "風神少女 (Short Version)",
        "ポイズンボディ ～ Forsaken Doll", "彼岸帰航 ～ Riverside View",
        "六十年目の東方裁判 ～ Fate of Sixty Years",
    ];

    public static string Mmss(double sec)
    {
        double s = TimelineViewModel.Tidy(Math.Max(0.0, sec), 1);
        int m = (int)(s / 60);
        return m.ToString(CultureInfo.InvariantCulture) + ":"
               + (s - m * 60).ToString("00.0", CultureInfo.InvariantCulture);
    }

    public static KvRow LabelText(string name, TimelineLabel label)
    {
        var v = label.Value;
        string who = name.StartsWith("p1_", StringComparison.Ordinal) ? "1P" : "2P";
        if (name == "__duration__") return new("対戦時間", Mmss(v.AsDouble));
        if (name == "initial_rank") return new("初期ランク", Num(v));
        if (name is "p1_character" or "p2_character")
            return new(who, Pick(ReplayLabels.Characters, v));
        if (name == "mode") return new("モード", Pick(ReplayDetailLabels.Modes, v));
        if (name == "difficulty") return new("難易度", Pick(ReplayLabels.Difficulties, v));
        if (name is "p1_control" or "p2_control")
            return new(who + " 操作", !v.IsMissing && v.AsLong == 1 ? "CPU" : "人間");
        if (name == "pause_used")
            return new("ポーズ", !v.IsMissing && v.AsLong != 0 ? "使った" : "なし");
        if (name == "field_id") return new("ステージ", Pick(Fields, v));
        if (name == "battle_bgm_id") return new("BGM", Pick(Bgms, v));
        if (name == "stage_index")
            return new("面", v.IsMissing ? ReplayFormat.Missing
                            : (v.AsLong + 1).ToString(CultureInfo.InvariantCulture) + " / 9");
        if (name == "result_winner")
            return new("勝者", !v.IsMissing && v.AsLong is 1 or 2
                               ? v.AsLong.ToString(CultureInfo.InvariantCulture) + "P" : "—");
        if (name is "p1_wins" or "p2_wins") return new(who + " 取得R", Num(v));
        if (name == "rank_interval") return new("ランク上昇間隔", Num(v));
        if (name == "rank_max") return new("ランク上限", Num(v));
        if (name is "p1_cpu_level" or "p2_cpu_level") return new(who + " CPU Lv", Num(v));
        if (name.StartsWith("clear_", StringComparison.Ordinal))
            return new("ボーナス " + name["clear_".Length..], Comma(v));
        foreach (var (suf, ja) in CounterSuffixes)
        {
            if (!name.EndsWith(suf, StringComparison.Ordinal)) continue;
            if (label.IsCumulative)
                return new(who + " " + ja, string.Create(CultureInfo.InvariantCulture,
                    $"{label.RoundDelta!.Value} 回（通算 {label.Total!.Value}）"));
            return new(who + " " + ja, Comma(v));
        }
        if (name is "p1_lives" or "p2_lives") return new(who + " 残機", G(v));
        return new(name, v.Kind == TickKind.Float ? G(v) : Num(v));
    }

    public static readonly (string Suffix, string Ja)[] CounterSuffixes =
    [
        ("_max_combo", "最大コンボ"),
        ("_spell_attacks", "カードアタック"),
        ("_boss_attacks", "ボスアタック"),
        ("_boss_reversals", "リバーサル"),
    ];

    private static string Pick(string[] table, TickValue v)
    {
        if (v.IsMissing) return ReplayFormat.Missing;
        long i = v.AsLong;
        return i >= 0 && i < table.Length ? table[i] : i.ToString(CultureInfo.InvariantCulture);
    }

    private static string Num(TickValue v) => v.IsMissing ? ReplayFormat.Missing
        : v.Kind == TickKind.Float ? G(v) : v.AsLong.ToString(CultureInfo.InvariantCulture);

    private static string Comma(TickValue v) => v.IsMissing ? ReplayFormat.Missing
        : ((long)v.AsDouble).ToString("N0", CultureInfo.InvariantCulture);

    private static string G(TickValue v) => v.IsMissing ? ReplayFormat.Missing
        : v.AsDouble.ToString("G6", CultureInfo.InvariantCulture);
}
