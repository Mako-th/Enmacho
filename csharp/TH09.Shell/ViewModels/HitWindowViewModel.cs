using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TH09.Analysis;

namespace TH09.Shell.ViewModels;


internal sealed class HitOption
{
    public required string Key { get; init; }
    public required string Ja { get; init; }
    public required string[] Picks { get; init; }
    public required string Def { get; init; }
    public Dictionary<string, string> Text { get; init; } = new(StringComparer.Ordinal);
    public (double Min, double Max, double Step, string StdJa)? Slider { get; init; }
    public (string Dec, string Inc)? StepJa { get; init; }

    public bool IsSlider => Slider is not null;
}

internal static class HitWindowOptions
{
    public const double ZoomMin = 0.6, ZoomMax = 3.0, ZoomStep = 0.1;

    public static double ZoomDefault =>
        double.Parse(Of("zoom").Def, CultureInfo.InvariantCulture);

    public const string ResetKey = "reset";
    public const string ResetJa = "デフォルトに戻す";

    public static readonly HitOption[] All =
    [
        new HitOption
        {
            Key = "theme", Ja = "テーマ", Picks = ["light", "dark"], Def = "dark",
            Text = new(StringComparer.Ordinal) { ["light"] = "ライト", ["dark"] = "ダーク" },
        },
        new HitOption
        {
            Key = "zoom", Ja = "サイズ", Picks = ["0.75", "1.0", "1.25", "1.5", "2.0"], Def = "1.25",
            Slider = (ZoomMin, ZoomMax, ZoomStep, "標準"),
            StepJa = ("縮小", "拡大"),
        },
        new HitOption
        {
            Key = "speed", Ja = "再生速度", Picks = ["0.25", "0.5", "1.0", "1.25", "1.5"], Def = "1.0",
            Slider = (0.25, 2.00, 0.05, "標準"),
            StepJa = ("−", "＋"),
        },
        new HitOption
        {
            Key = "colorby", Ja = "弾色", Picks = ["kind", "origin"], Def = "origin",
            Text = new(StringComparer.Ordinal) { ["kind"] = "種類", ["origin"] = "由来" },
        },
        new HitOption
        {
            Key = "emph", Ja = "強調", Picks = ["frame", "fill"], Def = "fill",
            Text = new(StringComparer.Ordinal) { ["frame"] = "枠", ["fill"] = "全体" },
        },
        new HitOption
        {
            Key = "extrail", Ja = "Ex の軌跡", Picks = ["hit", "all", "off"], Def = "hit",
            Text = new(StringComparer.Ordinal)
            { ["hit"] = "被弾のみ", ["all"] = "すべて", ["off"] = "なし" },
        },
        new HitOption
        {
            Key = "loop", Ja = "ループ", Picks = ["on", "off"], Def = "on",
            Text = new(StringComparer.Ordinal) { ["on"] = "する", ["off"] = "しない" },
        },
        new HitOption
        {
            Key = "startat", Ja = "再生開始点", Picks = ["head", "hit"], Def = "hit",
            Text = new(StringComparer.Ordinal) { ["head"] = "窓の先頭", ["hit"] = "被弾" },
        },
        new HitOption
        {
            Key = "sides", Ja = "盤面", Picks = ["one", "both"], Def = "one",
            Text = new(StringComparer.Ordinal) { ["one"] = "この陣だけ", ["both"] = "両陣" },
        },
    ];

    public static readonly string[][] Bars =
    [
        ["theme", "sides"],
        ["zoom", "speed"],
        ["colorby", "emph", "extrail", "startat", "loop"],
        [ResetKey],
    ];

    public static HitOption Of(string key) =>
        All.FirstOrDefault(o => o.Key == key)
        ?? throw new KeyNotFoundException("そんな設定の組はありません: " + key);
}

internal sealed class StripSeries
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required double Lo { get; init; }
    public required double Hi { get; init; }
    public required double[] Marks { get; init; }
    public required double?[] Values { get; init; }
    public string? Note { get; init; }
}

internal static class HitWindowStrips
{
    public static readonly (string Key, string Label, double? Lo, double? Hi, double[] Marks)[] Spec =
    [
        ("gauge", "ゲージ", 0.0, 400.0, [200, 300, 400]),
        ("spell_points", "スペルポイント", 0.0, 999990.0, [100000, 300000, 500000]),
        ("bullet_fairy", "弾幕系の弾数", 0.0, 175.0, [50, 100, 150, 175]),
        ("bullet_rival", "カードアタック系の弾数", 0.0, 360.0, [100, 200, 300, 360]),
        ("enemy_total", "自陣の敵数", null, null, []),
    ];

    public const string ExCountLabel = "この陣の Ex の数";
    public const string NoExActive = "この窓は Ex の数を記録していない（主リングに列が無い）";

    public const string InvincibleLabel = "無敵（1 ＝ 被弾しない）";
    public const string NoInvincible = "この窓は無敵の語を記録していない（主リングに列が無い）";

    public static double Tidy(double v) => Math.Round(v, 6);

    public static double[] AutoMarks(double? lo, double? hi, int want = 3)
    {
        if (lo is null || hi is null) return [];
        double a = lo.Value, b = hi.Value;
        if (!(b > a)) return [Tidy(b)];
        double raw = (b - a) / Math.Max(1, want);
        double p = Math.Pow(10.0, Math.Floor(Math.Log10(raw)));
        double step = 10 * p;
        foreach (var s in new[] { 1.0, 2.0, 5.0, 10.0 })
        {
            if (s * p >= raw - 1e-9) { step = s * p; break; }
        }
        if (double.IsInteger(a) && double.IsInteger(b)) step = Math.Max(1.0, Math.Round(step));
        var outList = new List<double> { a };
        double v = Math.Ceiling(a / step) * step;
        while (v < b)
        {
            if (v > a + 1e-9) outList.Add(v);
            v += step;
        }
        outList.Add(b);
        return outList.Select(Tidy).ToArray();
    }
}

internal static class HitWindowGauge
{
    public static double Max { get; } = QuickCards.QuickLevelByGauge.Max(x => x.Gauge);

    public const string LegendJa = "チャージゲージ（上 1P / 下 2P。数字＝カード / ボスカードの Lv）";

    public const string LegendOneJa = "チャージゲージ（数字＝カード / ボスカードの Lv）";

    public const string MarkJoin = " / ";

    public static readonly (double Value, string Text)[] Marks = BuildMarks();

    private static (double Value, string Text)[] BuildMarks()
    {
        var got = new Dictionary<double, List<string>>();
        void Put(double v, CardSource src, CardLevel lv)
        {
            if (!got.TryGetValue(v, out var list)) got[v] = list = [];
            list.Add(CardEvents.CardLabel(src, lv));
        }
        foreach (var (drop, lv) in QuickCards.DropLevel) Put(drop, CardSource.Gauge, lv);
        foreach (var (th, lv) in QuickCards.QuickLevelByGauge) Put(th, CardSource.Quick, lv);
        return got.Keys.OrderBy(x => x)
                  .Select(v => (v, string.Join(MarkJoin, got[v]))).ToArray();
    }
}

internal readonly record struct GaugeTick(
    double? Gauge1, double? Gauge2,
    double? Charge1, double? Charge2,
    double? CardLevel1, double? CardLevel2,
    double? BossCardLevel1, double? BossCardLevel2)
{
    public (double? Gauge, double? Charge, double? CardLevel, double? BossCardLevel) Of(int side) =>
        side == 2 ? (Gauge2, Charge2, CardLevel2, BossCardLevel2)
                  : (Gauge1, Charge1, CardLevel1, BossCardLevel1);
}

internal sealed partial class HitWindowViewModel : ObservableObject
{
    public static double FieldX0 => TH09.Analysis.BoardGeometry.FieldX0;
    public static double FieldX1 => TH09.Analysis.BoardGeometry.FieldX1;
    public static double FieldY0 => TH09.Analysis.BoardGeometry.FieldY0;
    public static double FieldY1 => TH09.Analysis.BoardGeometry.FieldY1;

    public const int Pad = 24;

    public const double DefaultBulletSide = 5.0;

    public static readonly double[] PlayerSides =
    [
        2.0, 2.3, 2.3, 2.3, 2.3, 2.3, 2.3, 2.3,
        2.2, 2.2, 2.2, 2.2, 2.2, 2.2, 2.3, 2.3,
    ];

    public static readonly (int Value, string Text)[] DodgeModes =
    [
        (0, "詰みクイック"),
        (1, "詰み被弾"),
        (3, "棒立ち"),
    ];

    public const string DodgeUnknown = "—";

    private Window? _window;
    private AnalysisDb? _db;
    private Board? _boardCache;
    private int _boardCacheTick = -1;

    private GaugeTick[] _gauge = [];

    private bool?[] _invFlags = [];
    private List<InvincibleSpan> _invSpans = [];

    private LegendScan? _legendScan;

    private int? _foeChar;

    private HitCandidateSet? _hitSet;

    public IReadOnlyDictionary<int, (double W, double H)> BulletSides { get; private set; }
        = new Dictionary<int, (double, double)>();

    public Window? Window => _window;

    [ObservableProperty]
    public partial int Revision { get; set; }

    [ObservableProperty]
    public partial int Side { get; set; } = 1;

    [ObservableProperty]
    public partial int TickIndex { get; set; }

    [ObservableProperty]
    public partial int TickCount { get; set; }

    [ObservableProperty]
    public partial bool Playing { get; set; }


    [RelayCommand]
    private void StopPlay()
    {
        Playing = false;
        TickIndex = StartIndex();
    }

    [ObservableProperty]
    public partial string SubHeading { get; set; } = "";

    [ObservableProperty]
    public partial string Heading { get; set; } = "";


    [ObservableProperty]
    public partial string Theme { get; set; } = HitWindowOptions.Of("theme").Def;

    [ObservableProperty]
    public partial string ColorBy { get; set; } = HitWindowOptions.Of("colorby").Def;

    [ObservableProperty]
    public partial string Emph { get; set; } = HitWindowOptions.Of("emph").Def;

    [ObservableProperty]
    public partial string ExTrail { get; set; } = HitWindowOptions.Of("extrail").Def;
    [ObservableProperty]
    public partial string Loop { get; set; } =
        HitWindowOptions.Of("loop").Def;

    partial void OnLoopChanged(string value) => AfterOptionChanged();

    [ObservableProperty]
    public partial string StartAt { get; set; } = HitWindowOptions.Of("startat").Def;

    [ObservableProperty]
    public partial string Sides { get; set; } = HitWindowOptions.Of("sides").Def;

    public bool BothSides => string.Equals(Sides, "both", StringComparison.Ordinal);

    public Avalonia.Controls.Primitives.ScrollBarVisibility HorizontalScroll =>
        BothSides ? Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
                  : Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;

    [ObservableProperty]
    public partial double Zoom { get; set; } =
        double.Parse(HitWindowOptions.Of("zoom").Def, CultureInfo.InvariantCulture);

    [ObservableProperty]
    public partial double Speed { get; set; } =
        double.Parse(HitWindowOptions.Of("speed").Def, CultureInfo.InvariantCulture);

    public string SpeedText => Speed.ToString("F2", CultureInfo.InvariantCulture) + "x";

    public static double BoardWidthAt(double zoom) =>
        Math.Round((FieldX1 - FieldX0) * zoom) + Pad * 2;

    public static double BoardHeightAt(double zoom) =>
        Math.Round((FieldY1 - FieldY0) * zoom) + Pad * 2;

    public double BoardWidth => BoardWidthAt(Zoom);
    public double BoardHeight => BoardHeightAt(Zoom);

    public List<StripSeries> Strips { get; } = [];

    public List<WindowEvent> Events { get; } = [];

    public string? PrimaryTrigger { get; private set; }

    public bool IsNonHitWindow =>
        PrimaryTrigger is not null
        && Window.NonHitTriggers.Contains(PrimaryTrigger, StringComparer.Ordinal);

    public HitCandidates.HazardCulprit? HazardCulprit { get; private set; }

    public bool HazardCulpritWanted { get; private set; }

    public IReadOnlyList<(int Slot, (int Start, int End)? Span)> HazardCulpritEx { get; private set; } = [];

    public IReadOnlyList<(int Slot, TrailPoint?[] Points)> HazardCulpritExTrail { get; private set; } = [];

    public int HitIndex { get; private set; }

    public Board CurrentBoard
    {
        get
        {
            if (_window is null) return new Board();
            if (_boardCache is not null && _boardCacheTick == TickIndex) return _boardCache;
            _boardCache = _window.BoardAt(TickIndex, Side);
            _boardCacheTick = TickIndex;
            return _boardCache;
        }
    }


    public void Load(string layer0Path, long sessionId, long windowNo, int? side = null)
    {
        if (!_optionsLoaded) { _optionsLoaded = true; LoadOptions(); }
        _db?.Dispose();
        _db = new AnalysisDb(layer0Path);
        _window = HitWindowReader.OpenForDisplay(_db, sessionId, windowNo)
                  ?? throw new InvalidDataException(
                      $"窓が見つかりません: session={sessionId} window={windowNo}");

        Events.Clear();
        Events.AddRange(_window.Events());
        var primary = _window.PrimaryEventIndex(side);
        HitIndex = primary is null ? 0 : Events[primary.Value].Index;
        Side = primary is null ? (side ?? 1) : Events[primary.Value].Side;
        PrimaryTrigger = primary is null ? null : Events[primary.Value].Trigger;

        TickCount = _window.TickCount;
        _boardCache = null;
        _boardCacheTick = -1;
        _legendScan = null;
        BuildGauge();
        BuildStrips();
        TickIndex = StartIndex();
        BuildStatic();
        BuildRows(TickIndex);
        BuildHeadings(sessionId, windowNo);
        _layer0Path = layer0Path;
        _sessionId = sessionId;
        _windowNo = windowNo;
        Revision++;
        SyncOther();
    }


    private string? _layer0Path;
    private long _sessionId;
    private long _windowNo;

    private bool _isMirror;

    [ObservableProperty]
    public partial HitWindowViewModel? Other { get; set; }

    public ObservableCollection<HitWindowViewModel> Panes { get; } = [];

    private void SyncOther()
    {
        if (_isMirror) return;
        if (BothSides && _window is not null && _layer0Path is not null)
        {
            var o = Other;
            if (o is null)
            {
                o = new HitWindowViewModel
                {
                    _isMirror = true,
                    _neverSave = true,
                    _optionsLoaded = true,
                };
                o.BulletSides = BulletSides;
                Other = o;
            }
            MirrorOptionsTo(o);
            int want = 3 - Side;
            if (o._sessionId != _sessionId || o._windowNo != _windowNo || o.Side != want)
            {
                o.Load(_layer0Path, _sessionId, _windowNo, side: want);
                o.Side = want;
            }
            o.TickIndex = TickIndex;
        }
        else
        {
            Other?.Close();
            Other = null;
        }
        RebuildPanes();
    }

    private void RebuildPanes()
    {
        Panes.Clear();
        if (Other is not { } o)
        {
            IsRightPane = false;
            Panes.Add(this);
        }
        else if (Side == 1) { IsRightPane = false; o.IsRightPane = true; Panes.Add(this); Panes.Add(o); }
        else { IsRightPane = true; o.IsRightPane = false; Panes.Add(o); Panes.Add(this); }
        NotifyPaneLook();
        Other?.NotifyPaneLook();
    }

    private void NotifyPaneLook()
    {
        OnPropertyChanged(nameof(PaneLabel));
        OnPropertyChanged(nameof(PaneName));
        OnPropertyChanged(nameof(InPair));
        OnPropertyChanged(nameof(ShowFoeText));
        OnPropertyChanged(nameof(GaugeOnlySide));
        OnPropertyChanged(nameof(GaugeJa));
        OnPropertyChanged(nameof(PaneMargin));
    }

    public bool IsMirror => _isMirror;

    public bool InPair => _isMirror || Other is not null;

    public string PaneLabel => _isMirror ? "もう一方の陣" : "被弾したのは";

    public bool ShowFoeText => !InPair;

    public string PaneName => Side + "P";

    public int GaugeOnlySide => InPair ? Side : 0;

    [ObservableProperty]
    public partial bool IsRightPane { get; set; }

    partial void OnIsRightPaneChanged(bool value) => OnPropertyChanged(nameof(PaneMargin));

    public const double BoardGapDots = 48;

    public static double BoardGapAt(double zoom) =>
        Math.Max(0, Math.Round(BoardGapDots * zoom) - Pad * 2);

    public Avalonia.Thickness PaneMargin =>
        IsRightPane ? new Avalonia.Thickness(BoardGapAt(Zoom), 0, 0, 0) : default;

    private void MirrorOptionsTo(HitWindowViewModel o)
    {
        o.Theme = Theme;
        o.Zoom = Zoom;
        o.ColorBy = ColorBy;
        o.Emph = Emph;
        o.ExTrail = ExTrail;
    }

    partial void OnSidesChanged(string value)
    {
        OnPropertyChanged(nameof(BothSides));
        OnPropertyChanged(nameof(HorizontalScroll));
        SyncOther();
        AfterOptionChanged();
    }

    public void LoadBulletSides(string? sizesJsonPath)
    {
        if (string.IsNullOrEmpty(sizesJsonPath) || !File.Exists(sizesJsonPath)) return;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(sizesJsonPath));
            var map = new Dictionary<int, (double, double)>();
            foreach (var p in doc.RootElement.GetProperty("sprites").EnumerateObject())
            {
                map[int.Parse(p.Name, CultureInfo.InvariantCulture)] =
                    (p.Value.GetProperty("w").GetDouble(), p.Value.GetProperty("h").GetDouble());
            }
            BulletSides = map;
        }
        catch (Exception)
        {
        }
    }

    public (double HalfW, double HalfH) BulletHalf(uint kind)
    {
        int sprite = (int)(kind & 0xFFFF);
        if (BulletSides.TryGetValue(sprite, out var wh)) return (wh.W * 0.5, wh.H * 0.5);
        return (DefaultBulletSide * 0.5, DefaultBulletSide * 0.5);
    }


    public double GaugeMax => HitWindowGauge.Max;

    public IReadOnlyList<(double Value, string Text)> GaugeMarks => HitWindowGauge.Marks;

    public string GaugeJa =>
        GaugeOnlySide == 0 ? HitWindowGauge.LegendJa : HitWindowGauge.LegendOneJa;

    public GaugeTick? GaugeAt(int i) =>
        (uint)i < (uint)_gauge.Length ? _gauge[i] : null;

    private void BuildGauge()
    {
        if (_window is null) { _gauge = []; return; }
        int n = _window.TickCount;
        var g = new GaugeTick[n];
        var s = _window.Series("p1_gauge");
        var t = _window.Series("p2_gauge");
        var c1 = _window.Series("p1_charge");
        var c2 = _window.Series("p2_charge");
        var l1 = _window.Series("p1_card_attack_level");
        var l2 = _window.Series("p2_card_attack_level");
        var b1 = _window.Series("p1_boss_card_attack_level");
        var b2 = _window.Series("p2_boss_card_attack_level");
        double? At(double?[]? col, int i) => col is not null && i < col.Length ? col[i] : null;
        for (int i = 0; i < n; i++)
        {
            g[i] = new GaugeTick(At(s, i), At(t, i), At(c1, i), At(c2, i),
                                 At(l1, i), At(l2, i), At(b1, i), At(b2, i));
        }
        _gauge = g;
    }


    private void BuildStrips()
    {
        Strips.Clear();
        if (_window is null) return;
        foreach (var (key, label, lo, hi, marks) in HitWindowStrips.Spec)
        {
            var name = "p" + Side + "_" + key;
            if (!_window.Main.Has(name))
            {
                Strips.Add(new StripSeries
                {
                    Key = key, Label = label, Lo = 0, Hi = 1, Marks = [],
                    Values = new double?[_window.TickCount],
                    Note = "この窓は " + label + " を記録していない（主リングに列が無い）",
                });
                continue;
            }
            var v = _window.Series(name)!;
            double lo2 = lo ?? 0.0;
            double hi2 = hi ?? Math.Max(1.0, v.Where(x => x is not null).Select(x => x!.Value)
                                             .DefaultIfEmpty(0.0).Max());
            Strips.Add(new StripSeries
            {
                Key = key, Label = label, Lo = lo2, Hi = hi2,
                Marks = lo is null ? HitWindowStrips.AutoMarks(lo2, hi2) : marks,
                Values = v,
            });
        }
        Strips.Add(ExCountStrip());
        Strips.Add(InvincibleStrip());
    }

    private StripSeries InvincibleStrip()
    {
        int n = _window?.TickCount ?? 0;
        _invFlags = [];
        _invSpans = [];
        if (_window is null || !PlayerInvincible.HasWords(_window))
        {
            return new StripSeries
            {
                Key = "invincible", Label = HitWindowStrips.InvincibleLabel, Lo = 0, Hi = 1,
                Marks = [], Values = new double?[n], Note = HitWindowStrips.NoInvincible,
            };
        }
        _invFlags = PlayerInvincible.Flags(_window, Side);
        _invSpans = PlayerInvincible.Spans(_window, Side);
        var flags = _invFlags;
        var v = new double?[n];
        for (int i = 0; i < n && i < flags.Length; i++)
            v[i] = flags[i] is null ? null : (flags[i]!.Value ? 1.0 : 0.0);
        return new StripSeries
        {
            Key = "invincible", Label = HitWindowStrips.InvincibleLabel, Lo = 0, Hi = 1,
            Marks = [], Values = v,
        };
    }

    private StripSeries ExCountStrip()
    {
        var name = "p" + (3 - Side) + "_ex_active";
        if (_window is null || !_window.Main.Has(name))
        {
            return new StripSeries
            {
                Key = "ex_count", Label = HitWindowStrips.ExCountLabel, Lo = 0, Hi = 1,
                Marks = [], Values = new double?[_window?.TickCount ?? 0],
                Note = HitWindowStrips.NoExActive,
            };
        }
        var v = _window.Series(name)!;
        double hi = Math.Max(2.0, v.Where(x => x is not null).Select(x => x!.Value)
                                   .DefaultIfEmpty(0.0).Max());
        return new StripSeries
        {
            Key = "ex_count", Label = HitWindowStrips.ExCountLabel, Lo = 0, Hi = hi,
            Marks = HitWindowStrips.AutoMarks(0, hi), Values = v,
        };
    }

    private void BuildHeadings(long sessionId, long windowNo)
    {
        Heading = "被弾したのは " + Side + "P";
        SubHeading = string.Create(CultureInfo.InvariantCulture,
            $"session {sessionId} ／ 窓 {windowNo} ／ 窓の長さ {TickCount} tick ／ 枠 {_window?.Meta.SlotCount ?? 0}");
    }

    public double? MainAt(string name, int i)
    {
        return _window?.MainAt(name, i);
    }

    public (double X, double Y)? SelfAt(int i)
    {
        var x = MainAt("p" + Side + "_pos_x", i);
        var y = MainAt("p" + Side + "_pos_y", i);
        return x is null || y is null ? null : (x.Value, y.Value);
    }

    public double SelfSide(int i)
    {
        var c = MainAt("p" + Side + "_character", i);
        int id = c is null ? 0 : (int)c.Value;
        return PlayerSides[(uint)id < (uint)PlayerSides.Length ? id : 0];
    }


    public int StartIndex() => StartAt == "head" ? 0 : HitIndex;

    partial void OnTickIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentBoard));
        BuildRows(value);
        OnPropertyChanged(nameof(TimeText));
        if (Other is { } o) o.TickIndex = value;
    }


    public IReadOnlyList<KvRow> SituationRows { get; private set; } = [];

    public ObservableCollection<LiveKvRow> SituationRowsLive { get; } = [];

    public ObservableCollection<LiveKvRow> CursorRowsLive { get; } = [];

    public ObservableCollection<LiveKvRow> HitRowsLive { get; } = [];

    public ObservableCollection<LiveKvRow> ExRowsLive { get; } = [];

    public ObservableCollection<LiveKvRow> FreezeLegendRowsLive { get; } = [];

    private static void Merge(ObservableCollection<LiveKvRow> live, IReadOnlyList<KvRow> want)
    {
        bool same = live.Count == want.Count;
        if (same)
            for (var i = 0; i < want.Count; i++)
                if (!string.Equals(live[i].K, want[i].K, StringComparison.Ordinal)) { same = false; break; }
        if (!same)
        {
            live.Clear();
            foreach (var r in want) live.Add(new LiveKvRow { K = r.K, V = r.V });
            return;
        }
        for (var i = 0; i < want.Count; i++)
            if (!string.Equals(live[i].V, want[i].V, StringComparison.Ordinal))
                live[i].V = want[i].V;
    }

    public IReadOnlyList<KvRow> CursorRows { get; private set; } = [];

    public IReadOnlyList<KvRow> HitRows { get; private set; } = [];

    public string? CandidateWhyText { get; private set; }

    public IReadOnlyList<KvRow> ExRows { get; private set; } = [];

    public string HitSideText { get; private set; } = "";

    public string HitFoeText { get; private set; } = "";

    public IBrush HitSideBrush => Views.BoardPalette.Of(Theme).Side(Side);

    public IReadOnlyList<KvRow> FreezeLegendRows { get; private set; } = [];

    public IReadOnlyList<LegendRow> CardLegendRows { get; private set; } = [];

    internal IReadOnlyList<string> CardLegendMaterials { get; private set; } = [];

    public const string NoFreezeWords = "この窓は一時停止の語を記録していない（主リングに列が無い）";

    public const string ExPathNotChecked = "★この答えは Ex の枠を見ていない（Ex の特定が未移植）";

    public const string NoOriginNotRecorded = "★記録なし（この窓は由来を記録していない）";

    public const string MedicineMistKey = "medicine_mist";

    public const double SpeedMultBase = 0.4;

    public string TimeText
    {
        get
        {
            var f = MainAt("round_frames", TickIndex);
            return f is null ? NotRecordedMark : Mmss(f.Value / 60.0);
        }
    }

    public static string Mmss(double s)
    {
        decimal exact = decimal.Parse(Math.Abs(s).ToString("G17", CultureInfo.InvariantCulture),
                                       NumberStyles.Float, CultureInfo.InvariantCulture);
        decimal a = decimal.Round(exact, 1, MidpointRounding.ToEven);
        int m = (int)(a / 60);
        decimal r = a - m * 60;
        return string.Create(CultureInfo.InvariantCulture, $"{m}:{r:00.0}");
    }

    public const string NotRecordedMark = "—";

    public const string MissingMark = "欠測";

    public const double StripGroupFrom = 10000;

    public static string StripValueText(StripSeries s, double? v)
    {
        if (s.Note is not null) return NotRecordedMark;
        if (v is null) return MissingMark;
        double r = Math.Round(v.Value, 2);
        return r >= StripGroupFrom
            ? r.ToString("#,0.###", CultureInfo.InvariantCulture)
            : r.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private void BuildStatic()
    {
        if (_window is null) return;
        int i = Math.Max(0, Math.Min(Math.Max(0, TickCount - 1), HitIndex - 1));
        var sit = new List<KvRow>();
        void Add(List<KvRow> to, string k, string word, string fmt = "0.###")
        {
            var v = MainAt(word, i);
            if (v is not null) to.Add(new KvRow(k, v.Value.ToString(fmt, CultureInfo.InvariantCulture)));
        }
        int foe = 3 - Side;
        _foeChar = MainAt("p" + foe + "_character", i) is double fc ? (int)fc : null;
        string Who(int sd) => sd + "P" + (MainAt("p" + sd + "_control", i) == 1 ? "（CPU）" : "");

        var st = MainAt("stage_index", i);
        if (st is not null)
            sit.Add(new KvRow("面", ((int)st.Value + 1).ToString(CultureInfo.InvariantCulture)));
        sit.Add(new KvRow(Who(Side) + " キャラ", CharacterAt(Side, i)));
        sit.Add(new KvRow(Who(foe) + " キャラ", CharacterAt(foe, i)));
        var rf = MainAt("round_frames", i);
        if (rf is not null) sit.Add(new KvRow("ラウンド経過", Mmss(rf.Value / 60.0)));
        var lifeBefore = MainAt("p" + Side + "_life_raw", i);
        if (lifeBefore is not null)
        {
            var lifeAfter = MainAt("p" + Side + "_life_raw", HitIndex + 1);
            double sc = FieldDisplay.Scale("p" + Side + "_life_raw") ?? 1.0;
            string L(double v) => (v * sc).ToString("0.##", CultureInfo.InvariantCulture);
            sit.Add(new KvRow("ライフ", lifeAfter is null ? L(lifeBefore.Value)
                : L(lifeBefore.Value) + " → " + L(lifeAfter.Value)));
        }
        Add(sit, "ゲージ", "p" + Side + "_gauge", "0");
        var sp = MainAt("p" + Side + "_spell_points", i);
        if (sp is not null)
            sit.Add(new KvRow("スペルポイント",
                ((long)sp.Value).ToString("#,0", CultureInfo.InvariantCulture)));
        Add(sit, "無敵の残り", "p" + Side + "_invincible_timer", "0.###");
        var bt = MainAt("p" + Side + "_boss_type", i);
        if (bt is not null)
            sit.Add(new KvRow("ボス", (int)bt.Value == BoardGeometry.BossTypeBoss ? "いる" : "いない"));
        foreach (var sd in new[] { Side, foe })
        {
            if (MainAt("p" + sd + "_control", i) != 1) continue;
            var dm = MainAt("p" + sd + "_cpu_dodge_mode", i);
            sit.Add(new KvRow(Who(sd) + " 回避モード", DodgeJa(dm)));
            Add(sit, Who(sd) + " クイックTimer", "p" + sd + "_cpu_quick_timer_cur", "0.###");
        }
        SituationRows = sit;

        BuildHitRows();
        BuildFreezeLegend();
        BuildCardLegend();
        ResolveHitEnemy();
        ResolveHitLaser();
        ResolveHitEx();
        ResolveHitTrails();
        ResolveExTrails();
        _legendScan ??= ScanForLegend();
        BuildLegend();
        HitSideText = Side + "P　" + SideName(Side, i);
        HitFoeText = foe + "P　" + SideName(foe, i);

        Merge(SituationRowsLive, SituationRows);
        Merge(HitRowsLive, HitRows);
        Merge(FreezeLegendRowsLive, FreezeLegendRows);
        OnPropertyChanged(nameof(SituationRows));
        OnPropertyChanged(nameof(HitRows));
        OnPropertyChanged(nameof(FreezeLegendRows));
        OnPropertyChanged(nameof(CardLegendRows));
        OnPropertyChanged(nameof(HitSideText));
        OnPropertyChanged(nameof(HitFoeText));
        OnPropertyChanged(nameof(HitSideBrush));
    }

    private void BuildRows(int i)
    {
        if (_window is null) return;
        BuildExRows(i);

        var cur = new List<KvRow>();
        foreach (var s in Strips)
        {
            cur.Add(new KvRow(s.Label,
                StripValueText(s, (uint)i < (uint)s.Values.Length ? s.Values[i] : null)));
        }
        var b = CurrentBoard;
        cur.Add(new KvRow("盤面の弾", b.Bullets.Count.ToString(CultureInfo.InvariantCulture)));
        cur.Add(new KvRow("盤面の敵", b.Enemies.Count.ToString(CultureInfo.InvariantCulture)));
        int lethal = b.Lasers.Count(l => l.Laser is not null && l.Laser.Value.Lethal);
        cur.Add(new KvRow("レーザー", b.Lasers.Count == 0 ? "0"
            : string.Create(CultureInfo.InvariantCulture, $"{lethal} 致死 / {b.Lasers.Count}")));
        var rings = ClearRings.At(_window, i, Side);
        int pre = rings.Count(r => r.Pre);
        cur.Add(new KvRow(ClearRings.LegendJa, rings.Count == 0 ? "0"
            : rings.Count + " 個" + (pre > 0 ? "（うち " + pre + " 個は" + ClearRings.PreNote + "）" : "")));
        cur.Add(new KvRow("無敵", InvincibleText(i)));
        CursorRows = cur;


        Merge(CursorRowsLive, CursorRows);
        Merge(ExRowsLive, ExRows);
        OnPropertyChanged(nameof(CursorRows));
        OnPropertyChanged(nameof(ExRows));
    }


    public string CharacterAt(int side, int i)
    {
        var c = MainAt("p" + side + "_character", i);
        return Data.ReplayLabels.Character(c is null ? null : (int)c.Value);
    }

    public static string DodgeJa(double? raw)
    {
        if (raw is null) return DodgeUnknown;
        int v = (int)raw.Value;
        foreach (var (value, text) in DodgeModes) if (value == v) return text;
        return DodgeUnknown;
    }

    private string? _nameP1, _nameP2;

    public void SetReplayNames(string? p1, string? p2)
    {
        _nameP1 = string.IsNullOrWhiteSpace(p1) ? null : p1;
        _nameP2 = string.IsNullOrWhiteSpace(p2) ? null : p2;
        BuildStatic();
    }

    public string SideName(int sd, int i)
    {
        var nm = sd == 1 ? _nameP1 : _nameP2;
        bool cpu = MainAt("p" + sd + "_control", i) == 1;
        bool foeCpu = MainAt("p" + (3 - sd) + "_control", i) == 1;
        var who = nm ?? (cpu ? Data.ReplayLabels.CpuName
                             : foeCpu ? "Player" : "Player" + sd);
        return CharacterAt(sd, i) + "（" + who + "）";
    }

    private string InvincibleText(int i)
    {
        if (_window is null || (uint)i >= (uint)_invFlags.Length || _invFlags[i] is null)
            return NotRecordedMark;
        if (!_invFlags[i]!.Value) return "いいえ";
        foreach (var s in _invSpans)
            if (i >= s.Index && i < s.Index + s.Frames) return s.Label;
        return "はい";
    }

    private void BuildHitRows()
    {
        var w = _window;
        CandidateWhyText = null;
        if (w is null) { HitRows = []; return; }
        var rows = new List<KvRow>();
        var set = HitCandidates.Resolve(w, Side, playerSide: SelfSide(HitIndex));
        _hitSet = set;
        ResolveHazardCulprit();
        bool hazardResolved = HazardAnyMatched(HazardCulprit);
        rows.Add(new KvRow("種別", HitCandidates.TypeJa(set?.Type) ?? "—"));
        if (set is not null)
        {
            switch (set.Type)
            {
                case HitTypeKind.Bullet when set.ResolvedSlot is int slot:
                    rows.Add(new KvRow("当たった弾", "スロット " + slot));
                    rows.Add(new KvRow("確度", "ポインタで確定"));
                    rows.Add(new KvRow("由来", !BulletOrigin.HasRecorded(w)
                        ? NoOriginNotRecorded
                        : OriginLegendText(BulletOrigin.AtBirth(w, slot, HitIndex))));
                    break;
                case HitTypeKind.Laser:
                    AddLaserRows(rows, w, hazardResolved);
                    break;
                case HitTypeKind.ExCircle when set.ResolvedSlot is int exSlot:
                    AddExResolvedRows(rows, w, exSlot);
                    break;
                case HitTypeKind.Contact when set.ResolvedSlot is int foe:
                    if (set.HowFromEx)
                    {
                        AddExResolvedRows(rows, w, foe);
                        break;
                    }
                    var cls = HitCandidates.EnemyIdentify(w, Side)?.EnemyClass;
                    rows.Add(new KvRow("当たった敵", "スロット " + foe
                        + (cls is null ? "" : "（" + BoardLabels.EnemyJa(cls) + "）")));
                    rows.Add(new KvRow("確度",
                        "★確定 — " + HitCandidates.HowJa(HitCandidates.HowEnemyHitXy)));
                    break;
                default:
                    if (!hazardResolved)
                    {
                        rows.Add(new KvRow("当たったもの", "ポインタが無い種別"));
                        rows.Add(new KvRow("確度", "候補どまり"));
                    }
                    break;
            }
            if (set.Slots.Count > 0)
            {
                rows.Add(new KvRow("候補の軌跡", "候補 " + set.Slots.Count + " 件を全部描いた"));
                var how = set.HowFromEx ? HitCandidates.ExHowJa(set.HowKey) : HitCandidates.HowJa(set.HowKey);
                if (how.Length > 0) rows.Add(new KvRow("候補の出し方", how));
            }
            else if (!hazardResolved)
            {
                var why = HitCandidates.WhyJa(set.WhyKey);
                if (why.Length > 0)
                {
                    rows.Add(new KvRow("候補の軌跡", "なし"));
                    rows.Add(new KvRow("候補が無い理由", why));
                    CandidateWhyText = why;
                }
            }
            if (!set.ExPathChecked) rows.Add(new KvRow("断り", ExPathNotChecked));
        }
        rows.Add(new KvRow("起点", string.Join(" / ", Events.Select(e => e.Trigger))));

        if (HazardCulpritWanted)
        {
            rows.Add(new KvRow(HazardCulpritJa, HazardCulpritText()));
            if (HazardCulprit is not null) rows.Add(new KvRow("選び方", HazardCulpritHowShort));
        }
        if (set?.Type == HitTypeKind.Laser) AddLaserOriginRows(rows, w);
        HitRows = rows;
    }

    private string HazardCulpritText()
    {
        if (HazardCulprit is not { } hc)
            return "なし";
        int matched = 0;
        foreach (var o in hc.All) if (o.Match is not null) matched++;
        return "重なり " + hc.All.Count.ToString(CultureInfo.InvariantCulture)
             + " 件 ／ 対応づいた " + matched.ToString(CultureInfo.InvariantCulture) + " 件";
    }

    private static bool HazardAnyMatched(HitCandidates.HazardCulprit? hc)
    {
        if (hc is null) return false;
        foreach (var o in hc.All) if (o.Match is not null) return true;
        return false;
    }

    private void AddLaserRows(List<KvRow> rows, Window w, bool hazardResolved)
    {
        var L = HitCandidates.LaserIdentify(w, Side);
        if (L is null)
        {
            if (!hazardResolved)
            {
                rows.Add(new KvRow("当たったレーザー", "絞れず"));
                rows.Add(new KvRow("確度", "候補どまり"));
            }
            return;
        }
        rows.Add(new KvRow("当たったレーザー", L.Slot is int s ? "スロット " + s : "絞れず"));
        rows.Add(new KvRow("確度", L.How switch
        {
            LaserHow.Pivot => "★確定（始点が一致。ゲームの値）",
            LaserHow.PivotAngle => "★確定（始点と角度が一致。扇 " + L.Fan + " 本から。ゲームの値）",
            LaserHow.PivotGeometry => L.Slot is not null
                ? "扇 " + L.Fan + " 本のうち幾何で1本（始点は確定）"
                : "扇 " + L.Fan + " 本まで（1本に絞れず）",
            _ => "幾何のみ（v8 以前。始点が記録されていない）",
        }));
        if (L.Distance is double d)
            rows.Add(new KvRow("自機との距離", d.ToString("0.##", CultureInfo.InvariantCulture)));
    }

    private void AddLaserOriginRows(List<KvRow> rows, Window w)
    {
        var L = HitCandidates.LaserIdentify(w, Side);
        if (L?.Slot is not int slot) return;
        var state = w.Raw(CoordRing.ColState(slot), HitIndex);
        if (state is null) return;
        var origin = LaserOrigin.Parts(state.Value);
        rows.Add(new KvRow("レーザー由来", LaserOriginJa(origin.Sub)));
        var kind = LaserOrigin.Classify(_foeChar ?? 0, origin.Sub);
        if (kind is LaserOriginKind.BossChild or LaserOriginKind.BossDirect)
        {
            int? attackSub = LaserOrigin.AttackSub(w, Side, slot, _foeChar ?? 0, origin, HitIndex);
            var name = BossAttacks.NameOf(_foeChar, attackSub);
            if (name is not null) rows.Add(new KvRow("攻撃名", name));
        }
    }

    private string LaserOriginJa(int? sub)
    {
        if (sub is null) return NoOriginRecorded;
        return LaserOrigin.Classify(_foeChar ?? 0, sub) switch
        {
            LaserOriginKind.ExChild => "Ex の子",
            LaserOriginKind.BossChild => "ボスアタックの子",
            LaserOriginKind.BossDirect => "ボスアタック（直接）",
            _ => "その他（sub " + sub + "）",
        };
    }

    private void AddExResolvedRows(List<KvRow> rows, Window w, int slot)
    {
        var ex = HitCandidates.ExIdentify(w, Side);
        var name = ex?.Name is null ? null : (ExItems.NameJa(ex.Name) ?? ex.Name);
        rows.Add(new KvRow("当たった Ex", "スロット " + slot + (name is null ? "" : "（" + name + "）")));
        rows.Add(new KvRow("確度",
            "★確定 — " + (ex is null ? "" : HitCandidates.ExHowJa(ex.How))));
    }

    private void BuildExRows(int i)
    {
        var w = _window;
        if (w is null) { ExRows = []; return; }
        var rows = new List<KvRow>();
        int mist = ExItems.At(w, i, Side).Count(x => x.Name == MedicineMistKey);
        if (mist > 0)
            rows.Add(new KvRow(ExItems.NameJa(MedicineMistKey) ?? MedicineMistKey, mist + " 枚"));
        var sp = MainAt("p" + Side + "_speed_mult", i);
        if (sp is double v && v > 0 && v < 0.999)
        {
            int n = (int)Math.Round(Math.Log(v) / Math.Log(SpeedMultBase));
            rows.Add(new KvRow("移動速度", string.Create(CultureInfo.InvariantCulture,
                $"×{v:0.000}（{SpeedMultBase:0.#}^{n}）")));
        }
        ExRows = rows;
    }

    private void BuildFreezeLegend()
    {
        var w = _window;
        if (w is null) { FreezeLegendRows = []; return; }
        if (!FreezeSpans.CanRead(w))
        {
            FreezeLegendRows = [new KvRow(NoFreezeWords, "")];
            return;
        }
        var got = FreezeSpans.Of(w);
        if (!got.Readable) { FreezeLegendRows = [new KvRow(NoFreezeWords, "")]; return; }
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var sp in got.Spans)
        {
            if (!seen.ContainsKey(sp.Label)) { seen[sp.Label] = 0; order.Add(sp.Label); }
            seen[sp.Label]++;
        }
        var rows = new List<KvRow>
        {
            new("この窓の一時停止", order.Count == 0 ? "なし"
                : string.Join(" / ", order.Select(k => seen[k] > 1 ? k + " ×" + seen[k] : k))),
        };
        int nest = got.Spans.Count(s => s.Depth > 0);
        if (nest > 0) rows.Add(new KvRow("入れ子", nest + " 本が別の帯の中"));
        rows.AddRange(got.Warnings.Select(x => new KvRow("★注意", x)));
        FreezeLegendRows = rows;
    }

    private static class CardLegendKeys
    {
        public static string Source(string srcKey, int side) => "cardsrc:" + srcKey + ":" + side;
        public const string SpellHollow = "cardspell";
        public static string Estimate(string note) => "cardest:" + note;
    }

    private void BuildCardLegend()
    {
        var w = _window;
        if (w is null) { CardLegendRows = []; CardLegendMaterials = []; return; }
        var cards = CardEvents.For(w).Cards().Where(e => e.Index >= 0).ToList();
        if (cards.Count == 0) { CardLegendRows = []; CardLegendMaterials = []; return; }

        var pal = Views.BoardPalette.Of(Theme);
        var rows = new List<LegendRow>();
        var mats = new List<string>();

        foreach (CardSource src in Enum.GetValues<CardSource>())
        {
            var ja = CardEvents.SourceLongName(src);
            if (ja is null) continue;
            var order = new List<int>();
            var count = new Dictionary<int, int>();
            foreach (var e in cards)
            {
                if (e.Source != src) continue;
                if (!count.ContainsKey(e.Side)) order.Add(e.Side);
                count[e.Side] = count.GetValueOrDefault(e.Side) + 1;
            }
            var srcKey = CardEvents.SourceKey(src);
            foreach (var side in order)
            {
                var key = CardLegendKeys.Source(srcKey, side);
                mats.Add(key);
                var color = Views.StripCanvas.TickLabelBrush(pal, side, srcKey);
                rows.Add(new LegendRow
                {
                    Swatch = color, Round = true,
                    Text = ja, Note = count[side] + " 件",
                    Key = key,
                });
            }
        }

        var spellSide = cards.Where(e => e.Source == CardSource.Spell)
                             .Select(e => (int?)e.Side).FirstOrDefault();
        if (spellSide is int ss)
        {
            mats.Add(CardLegendKeys.SpellHollow);
            var color = Views.StripCanvas.TickLabelBrush(pal, ss, CardEvents.SourceKey(CardSource.Spell));
            rows.Add(new LegendRow
            {
                Stroke = color, StrokeW = Views.BoardPalette.TrailW, Round = true,
                Text = CardLegendSpellHollow, Note = CardEvents.SpellNote,
                Key = CardLegendKeys.SpellHollow,
            });
        }

        var whyOrder = new List<string>();
        var whyCount = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var e in cards)
        {
            if (e.Certain) continue;
            var note = ClearRings.CardNote(e.Source == CardSource.Quick,
                                           new CardDrop(e.Source, e.Level, e.Certain));
            if (note is null) continue;
            if (!whyCount.ContainsKey(note)) whyOrder.Add(note);
            whyCount[note] = whyCount.GetValueOrDefault(note) + 1;
        }
        foreach (var t in whyOrder)
        {
            var key = CardLegendKeys.Estimate(t);
            mats.Add(key);
            rows.Add(new LegendRow
            {
                Text = CardLegendEstimateLabel, Note = t + "（" + whyCount[t] + " 件）",
                Key = key,
            });
        }

        CardLegendRows = rows;
        CardLegendMaterials = mats;
    }

    public const string NoOriginRecorded = "由来の記録なし";

    private string OriginLegendText(BulletOriginInfo? o) =>
        OriginLegendText(o?.Origin, o?.EnemyClass, o?.CardLevel,
                         o?.BossSub is null ? null : (int)o.BossSub.Value);

    private string OriginLegendText(string? origin, string? cls, int? level, int? sub)
    {
        var label = BulletOrigin.LabelOf(origin);
        if (label is null) return NoOriginRecorded;
        if (cls is null) return label;
        var ja = BoardLabels.CardLevelJa(level) ?? BoardLabels.EnemyJa(cls);
        var at = BossAttacks.NameOf(_foeChar, sub);
        if (at is not null) ja += OriginAttackJoin + at;
        return label + "（" + ja + "）";
    }

    private (string Name, string? Note) OriginUniqueParts(string? origin, string? cls, int? level, int? sub)
    {
        var label = BulletOrigin.LabelOf(origin);
        if (label is null) return (NoOriginRecorded, null);
        if (cls is null) return (label, null);
        var ja = BoardLabels.CardLevelJa(level) ?? BoardLabels.EnemyJa(cls);
        return (ja, BossAttacks.NameOf(_foeChar, sub));
    }


    public const string LegendCategoryEnemy = "敵";
    public const string LegendCategoryBulletUnique = "弾幕 - 敵固有";
    public const string LegendCategoryBulletCommon = "弾幕 - 共通";
    public const string LegendCategoryPlayer = "自機関連";
    public const string LegendCategoryOther = "その他";

    public const string LegendNoHit = "判定無し";
    public const string LegendEnemyHitOne = "★当たった 1 体（側の色）";
    public const string LegendNoteJoin = "　";
    public const string LegendEnemyRanged = "1 つに決まらない敵は外側が点線";
    public const string LegendGhostActivated = "幽霊（活性化）";
    public const string LegendFaintBullet = "淡い弾";
    public const string LegendBulletOther = "その他";
    public const string LegendBulletOtherNote = "分類不可";
    public const string LegendNoOriginWindow = "★この窓は由来を記録していない（座標リング v5 以前）";
    public const string LegendNoShotSlots = "記録が無い（座標リング v6 以前）";
    public const string LegendNotRecorded = "記録が無い";
    public const string LegendSelf = "自機";
    public const string LegendSelfInv = "自機（無敵）";
    public const string LegendRingEst = "推定のリング";
    public const string LegendRingEstNote = "点線（レベルが 1 つに決まらない）";
    public const string LegendLaserOriginBossJa = "致死のレーザー（ボスアタック由来）";

    private string LaserOriginBossNoteJa(int sub)
    {
        var name = BossAttacks.NameOf(_foeChar, sub);
        return name ?? ("sub " + sub.ToString(CultureInfo.InvariantCulture));
    }

    public const string LegendLaserOriginExJa = "致死のレーザー（Ex 由来）";
    public const string LegendLaserOriginExNote = "Ex の規定色（紫）と同じ";

    public const string LegendLaserOriginUnknownJa = "致死のレーザー（由来が分からない）";
    public const string LegendLaserOriginUnknownNote = "由来の記録が無い、またはそのキャラでは知らない sub（推測しない）";

    public const string LegendTrail = "被弾した弾の軌跡";
    public const string HazardCulpritJa = "危険物の重なり";

    public const string HazardCulpritHow =
        "その記録の本物の危険物リストで、自機の判定と重なった要素のうち、盤面の物に対応づいた分を"
        + "全部、側の色で強調する";

    public const string HazardCulpritHowUnmatched =
        "その記録の本物の危険物リストで、自機の判定と重なった要素はあるが"
        + "（★盤面の物には 1 つも対応づけられなかった）";

    public const string HazardCulpritHowNone =
        "求めたが答えが出ない（その記録に本物の危険物リストが無い、または重なりが無い）";

    public const string HazardCulpritBodyJa = "危険物の重なり（本体の色）";

    private static string HazardCulpritBodyNoteJa(string kind) => kind switch
    {
        "bullet" => "対応づいた弾の本体を側の色で塗りつぶす",
        "enemy" => "対応づいた敵の輪郭を側の色にする",
        "laser" => "対応づいたレーザーを側の色にする",
        _ => "",
    };

    public const string HazardCulpritHowShort = "対応づいた分は全部強調";
    public const string LegendExTrail = "Ex の軌跡";
    public const string LegendHitPoint = "被弾の位置（×）";
    public const string LegendHitPointNote = "ゲームが持っていた 1 点";
    public const string LegendExArmed = "Ex（判定あり）";
    public const string LegendExArmedNoteBoss = "実線・ボスの攻撃";
    public const string LegendExArmedNoteUnknown = "実線・★ボスの攻撃か分からない";
    public const string LegendExArmedNoteFoe = "実線・相手の色";
    public const string LegendExCross = "Ex（十字）";
    public const string LegendExCrossNote = "判定無し";
    public const string LegendHitBox = "判定 ";

    public const string CardLegendHeading = "カードアタック（シークバーの点）";
    public const string CardLegendSpellHollow = "中抜きの点";
    public const string CardLegendEstimateLabel = "★推定";

    public static readonly string[] LegendSilent = [LegendMaterialKeys.ExArmed("hit")];

    public const string OriginAttackJoin = "・";

    private static readonly string[] EnemyOrderPriority =
        ["boss", "lily", "ghost", "fairy", "c2c3", "other"];

    private static uint[]? ColumnOf(Window w, string name) => w.Cols.Has(name) ? w.Cols[name] : null;

    private static bool[] AliveTicks(Window w, int n, IReadOnlyList<int> slots)
    {
        var got = new bool[n];
        foreach (var slot in slots)
        {
            var st = ColumnOf(w, CoordRing.ColState(slot));
            if (st is null) continue;
            int lim = Math.Min(n, st.Length);
            for (int i = 0; i < lim; i++)
                if (!got[i] && CoordRing.SlotIsAlive(slot, st[i])) got[i] = true;
        }
        return got;
    }

    private static int ShotKeyOf(bool? c1) => c1 is null ? 2 : (c1.Value ? 1 : 0);

    private static bool? ShotC1Of(int key) => key == 2 ? null : key == 1;

    public bool? InvAt(int i) =>
        (uint)i < (uint)_invFlags.Length ? _invFlags[i] : null;

    public int InvSpanCount => _invSpans.Count;

    public bool InvRecorded => _window is not null && PlayerInvincible.HasWords(_window);

    public (int Slot, (int Start, int End)? Span)? HitEnemy { get; private set; }

    private void ResolveHitEnemy()
    {
        HitEnemy = null;
        var w = _window;
        var set = _hitSet;
        if (w is null || set is null) return;
        if (set.Type != HitTypeKind.Contact || set.ResolvedSlot is not int slot) return;
        HitEnemy = (slot, SlotSegments.At(w, slot, HitIndex));
    }

    public (int Slot, (int Start, int End)? Span)? HitLaser { get; private set; }

    private void ResolveHitLaser()
    {
        HitLaser = null;
        var w = _window;
        var set = _hitSet;
        if (w is null || set is null) return;
        if (set.Type != HitTypeKind.Laser || set.ResolvedSlot is not int slot) return;
        HitLaser = (slot, SlotSegments.At(w, slot, HitIndex));
    }

    public (int Slot, (int Start, int End)? Span)? HitEx { get; private set; }

    private void ResolveHitEx()
    {
        HitEx = null;
        var w = _window;
        var set = _hitSet;
        if (w is null || set is null) return;
        if (set.Type is not (HitTypeKind.ExCircle or HitTypeKind.Contact)) return;
        if (!set.HowFromEx || set.ResolvedSlot is not int slot) return;
        HitEx = (slot, SlotSegments.At(w, slot, HitIndex));
    }

    private void ResolveHazardCulprit()
    {
        HazardCulprit = null;
        HazardCulpritWanted = false;
        HazardCulpritEx = [];
        var w = _window;
        if (w is null) return;
        var set = _hitSet;
        HazardCulpritWanted = IsNonHitWindow || set is null || set.Confidence != HitConfidence.Resolved;
        if (!HazardCulpritWanted) return;
        HazardCulprit = HitCandidates.HazardListCulprit(w, Side);
        if (HazardCulprit is { } hc)
        {
            var exList = new List<(int, (int, int)?)>();
            foreach (var o in hc.All)
                if (o.Match is { Kind: HitCandidates.HazardMatchKind.Ex } m)
                    exList.Add((m.Slot, SlotSegments.At(w, m.Slot, HitIndex)));
            HazardCulpritEx = exList;
        }
    }

    public IReadOnlyList<(int Slot, TrailPoint?[] Points, bool Fixed, bool Est)> HitTrails
    { get; private set; } = [];

    public bool HitHasNoCandidate { get; private set; }

    public HitPointAt? HitPoint { get; private set; }

    public int? CulpritBulletSlot { get; private set; }

    public (double HalfW, double HalfH)? CulpritHalf { get; private set; }

    public IReadOnlyList<(int Slot, HitCandidates.HazardMatchKind Kind)> HazardCulpritTrailSlots
    { get; private set; } = [];

    private void ResolveHitTrails()
    {
        HitTrails = [];
        HitPoint = null;
        CulpritBulletSlot = null;
        CulpritHalf = null;
        HazardCulpritTrailSlots = [];
        var w = _window;
        var set = _hitSet;
        if (w is null || set is null) return;
        HitPoint = HitCandidates.HitPointOf(w, Side);
        bool fixedOne = set.ResolvedSlot is not null;
        var slots = fixedOne ? new[] { set.ResolvedSlot!.Value } : [.. set.Slots];
        int? estSlot = null;
        if (!fixedOne && set.Type == HitTypeKind.Laser)
        {
            var laserId = HitCandidates.LaserIdentify(w, Side);
            if (laserId is { Slot: int es }
                && laserId.How is LaserHow.PivotGeometry or LaserHow.GeometryOnly)
                estSlot = es;
        }
        var outList = new List<(int, TrailPoint?[], bool, bool)>(slots.Length);
        foreach (var slot in slots)
        {
            if (CoordRing.IsExSlot(slot)) continue;
            outList.Add((slot, HitCandidates.Trail(w, slot, w.TickCount - 1, at: HitIndex),
                         fixedOne, slot == estSlot));
        }
        if (HazardCulprit is { } hc)
        {
            var trailSlots = new List<(int, HitCandidates.HazardMatchKind)>();
            foreach (var o in hc.All)
            {
                if (o.Match is not { } m) continue;
                if (m.Kind is not (HitCandidates.HazardMatchKind.Bullet or HitCandidates.HazardMatchKind.Enemy
                                   or HitCandidates.HazardMatchKind.Laser)) continue;
                trailSlots.Add((m.Slot, m.Kind));
                if (!outList.Exists(x => x.Item1 == m.Slot))
                    outList.Add((m.Slot, HitCandidates.Trail(w, m.Slot, w.TickCount - 1, at: HitIndex),
                                 true, false));
            }
            HazardCulpritTrailSlots = trailSlots;
        }
        HitTrails = outList;
        HitHasNoCandidate = !fixedOne && set.Slots.Count == 0;
        if (set.Type == HitTypeKind.Bullet && set.ResolvedSlot is int culprit)
        {
            CulpritBulletSlot = culprit;
            CulpritHalf = ResolveCulpritHalf(w, culprit);
        }
    }

    public IReadOnlyDictionary<int, TrailPoint?[]> ExTrails { get; private set; } =
        new Dictionary<int, TrailPoint?[]>();

    private void ResolveExTrails()
    {
        ExTrails = EmptyExTrails;
        HazardCulpritExTrail = [];
        var w = _window;
        if (w is null || !ExItems.HasExSlots(w)) return;
        var all = ExItems.TrailsFor(w, Side, w.TickCount - 1);
        if (HazardCulprit is { } hcFull && HazardCulpritEx.Count > 0)
        {
            var exTrailList = new List<(int, TrailPoint?[])>(HazardCulpritEx.Count);
            foreach (var (slot, _) in HazardCulpritEx)
                exTrailList.Add((slot, ExItems.Trail(w, slot, w.TickCount - 1, at: hcFull.TickIndex)));
            HazardCulpritExTrail = exTrailList;
        }
        ExTrails = all.Count == 0 || HitEx is not { } hitEx
            ? all
            : all.Where(kv => kv.Key != hitEx.Slot).ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    private static readonly Dictionary<int, TrailPoint?[]> EmptyExTrails = new();

    private (double HalfW, double HalfH)? ResolveCulpritHalf(Window w, int culpritSlot)
    {
        int Clamp(int k) => Math.Max(0, Math.Min(w.TickCount - 1, k));
        foreach (var k in new[] { Clamp(HitIndex), Clamp(HitIndex - 1), Clamp(HitIndex + 1) })
        {
            var got = w.BoardAt(k, Side).Bullets.FirstOrDefault(x => x.Slot == culpritSlot);
            if (got is not null && got.Kind is uint kind) return BulletHalf(kind);
        }
        return null;
    }

    private LegendScan ScanForLegend()
    {
        var scan = new LegendScan();
        var w = _window;
        if (w is null) return scan;
        int n = w.TickCount;
        var slots = w.SlotsOf(Side);

        var firstAt = new Dictionary<(string?, string?, int?, int?), (int Tick, int Slot)>();
        foreach (var slot in slots["bullet"])
        {
            var st = ColumnOf(w, CoordRing.ColState(slot));
            var xs = ColumnOf(w, CoordRing.ColX(slot));
            if (st is null || xs is null) continue;
            var kd = ColumnOf(w, CoordRing.ColKind(slot));
            IReadOnlyList<(int Start, int End)>? segs = null;
            int segIdx = 0, lim = Math.Min(Math.Min(n, st.Length), xs.Length);
            int headAt = -1;
            bool headDone = false;
            (string?, string?, int?, int?) headKey = default;
            for (int i = 0; i < lim; i++)
            {
                uint s = st[i];
                if (!CoordRing.SlotIsAlive(slot, s) || CoordRing.SlotIsVanishing(slot, s)) continue;
                uint kind = kd is not null && i < kd.Length ? kd[i] : 0u;
                int sprite = (int)(kind & 0xFFFF);
                if (!scan.SpriteHalf.ContainsKey(sprite)) scan.SpriteHalf[sprite] = BulletHalf(kind);
                segs ??= SlotSegments.Of(w, slot);
                while (segIdx < segs.Count && segs[segIdx].End < i) segIdx++;
                bool inSeg = segIdx < segs.Count && segs[segIdx].Start <= i;
                int head = inSeg ? segs[segIdx].Start : i;
                if (head != headAt)
                {
                    headAt = head;
                    headDone = false;
                    var o = BulletOrigin.AtBirthCore(BulletOrigin.At(w, slot, head),
                                                     inSeg ? segs[segIdx].Start : null);
                    headKey = (o?.Origin, o?.EnemyClass, o?.CardLevel,
                               o?.BossSub is null ? null : (int?)(int)o.BossSub.Value);
                }
                if (headDone) continue;
                headDone = true;
                if (!firstAt.TryGetValue(headKey, out var got)
                    || i < got.Tick || (i == got.Tick && slot < got.Slot))
                    firstAt[headKey] = (i, slot);
            }
        }
        foreach (var sp in scan.SpriteHalf.Keys.OrderBy(x => x)) scan.Sprites.Add(sp);
        var ordered = firstAt.OrderBy(kv => kv.Value.Tick).ThenBy(kv => kv.Value.Slot)
                             .Select(kv => kv.Key).ToList();
        (string?, string?, int?, int?) noOrigin = (null, null, null, null);
        if (ordered.Remove(noOrigin)) scan.Origins.Add(noOrigin);
        scan.Origins.AddRange(ordered);

        var cf = new uint?[n];
        for (int i = 0; i < n; i++) cf[i] = w.CoordFlags(i);
        var boxCache = new Dictionary<(string, uint, uint?), EnemyHitbox?>();
        var seenCls = new HashSet<string>(StringComparer.Ordinal);
        var foeHit = HitEnemy;
        foreach (var slot in slots["enemy"])
        {
            var st = ColumnOf(w, CoordRing.ColState(slot));
            var xs = ColumnOf(w, CoordRing.ColX(slot));
            if (st is null || xs is null) continue;
            var kd = ColumnOf(w, CoordRing.ColKind(slot));
            int lim = Math.Min(Math.Min(n, st.Length), xs.Length);
            for (int i = 0; i < lim; i++)
            {
                uint s = st[i];
                if (!CoordRing.SlotIsAlive(slot, s) || CoordRing.SlotIsVanishing(slot, s)) continue;
                uint kind = kd is not null && i < kd.Length ? kd[i] : 0u;
                var cls = CoordRing.EnemyClass(kind);
                seenCls.Add(cls);
                if (foeHit is { } hf && slot == hf.Slot
                    && (hf.Span is not { } sp2 || (i >= sp2.Start && i <= sp2.End)))
                    scan.EnemyHit.Add(cls);
                else scan.EnemyPlain.Add(cls);
                if (cls == "ghost" && CoordRing.EnemyGhostActivated(kind)
                    && !(foeHit is { } gf && slot == gf.Slot
                         && (gf.Span is not { } sp3 || (i >= sp3.Start && i <= sp3.End))))
                    scan.GhostActivated = true;
                var key = (cls, kind, cf[i]);
                if (!boxCache.TryGetValue(key, out var box))
                {
                    box = EnemySize.Of(cls, EnemySize.KindIndex(kind, cf[i]), EnemySize.FairyLead(kind));
                    boxCache[key] = box;
                }
                if (box is not null && !box.None && box.Ranged) scan.EnemyRanged = true;
            }
        }
        foreach (var cls in EnemyOrderPriority.Where(seenCls.Contains)) scan.EnemyClasses.Add(cls);

        {
            var laserOriginBossSubs = new SortedSet<int>();
            bool laserOriginEx = false, laserOriginUnknown = false;
            foreach (var slot in slots["laser"])
            {
                var st = ColumnOf(w, CoordRing.ColState(slot));
                if (st is null) continue;
                int lim = Math.Min(n, st.Length);
                for (int i = 0; i < lim; i++)
                {
                    uint s = st[i];
                    if (!CoordRing.SlotIsAlive(slot, s)) continue;
                    if (!w.LaserOf(slot, i).Lethal) continue;
                    var origin = LaserOrigin.Parts(s);
                    var kind = LaserOrigin.Classify(_foeChar ?? 0, origin.Sub);
                    if (kind is LaserOriginKind.BossChild or LaserOriginKind.BossDirect)
                    {
                        if (LaserOrigin.AttackSub(w, Side, slot, _foeChar ?? 0, origin, i) is int asub)
                            laserOriginBossSubs.Add(asub);
                        else laserOriginUnknown = true;
                    }
                    else if (kind == LaserOriginKind.ExChild) laserOriginEx = true;
                    else laserOriginUnknown = true;
                }
            }
            scan.LaserOriginBossSubs = [.. laserOriginBossSubs];
            scan.LaserOriginEx = laserOriginEx;
            scan.LaserOriginUnknown = laserOriginUnknown;
        }

        int j = Math.Max(0, Math.Min(Math.Max(0, n - 1), HitIndex - 1));
        int? shotChar = MainAt("p" + Side + "_character", j) is double sc ? (int)sc : null;
        var shotAlive = AliveTicks(w, n, PlayerShots.SlotsOf(w, Side));
        var exAlive = AliveTicks(w, n, ExItems.Slots(w));
        bool anyRing = ClearRings.Origins(w, Side).Count > 0;
        bool anyBlast = EnemyBlasts.Origins(w, Side).Count > 0;
        for (int i = 0; i < n; i++)
        {
            foreach (var sh in shotAlive[i] ? PlayerShots.At(w, i, Side) : [])
            {
                if (sh.Box is not { } box) continue;
                int key = ShotKeyOf(PlayerShots.IsC1(shotChar, sh.Parts.Entry));
                double hw = Math.Round(box.HalfW, 2), hh = Math.Round(box.HalfH, 2);
                if (!scan.Shots.TryGetValue(key, out var v)) scan.Shots[key] = (hw, hw, hh, hh);
                else
                    scan.Shots[key] = (Math.Min(v.LoW, hw), Math.Max(v.HiW, hw),
                                       Math.Min(v.LoH, hh), Math.Max(v.HiH, hh));
            }
            foreach (var r in anyRing ? ClearRings.At(w, i, Side) : [])
            {
                scan.RingKinds.Add(r.Kind);
                if (!r.Certain) scan.RingEst = true;
            }
            if (anyBlast && !scan.Blast && EnemyBlasts.At(w, i, Side).Count > 0) scan.Blast = true;
            foreach (var ex in exAlive[i] ? ExItems.At(w, i, Side) : [])
            {
                var effect = ExItems.ColorOf(ex.Name);
                bool shaped = ex.Shape?.Circles is { Count: > 0 } || ex.Shape?.Rect is not null;
                if (!shaped)
                {
                    scan.ExCross = true;
                }
                else if (effect is null)
                {
                    int? cardLevel = ExItems.CardLevelOf(ex.ExType);
                    if (cardLevel == 2) scan.ExArmedCard2 = true;
                    else if (cardLevel == 3) scan.ExArmedCard3 = true;
                    else if (ex.FromBoss == true) scan.ExArmedBoss = true;
                    else if (ex.FromBoss is null) scan.ExArmedUnknown = true;
                    else scan.ExArmedFoe = true;
                }
                else
                {
                    scan.ExCross = true;
                }
            }
        }

        var track = SpiritField.Track(w, Side);
        if (track is not null)
        {
            var byName = new Dictionary<string, (int Ticks, int Angles, bool Certain, string ShapeKey)>(StringComparer.Ordinal);
            var order = new List<string>();
            for (int i = 0; i < n; i++)
            {
                int k = w.ExtIndex(i);
                var t = (uint)k < (uint)track.Length ? track[k] : null;
                if (t is null) continue;
                int ang = Math.Max(1, t.Angles.Count);
                if (!byName.TryGetValue(t.Label, out var got))
                {
                    order.Add(t.Label);
                    byName[t.Label] = (1, ang, t.AngleKnown, t.Shape.Name);
                }
                else byName[t.Label] = (got.Ticks + 1, Math.Max(got.Angles, ang), got.Certain, got.ShapeKey);
            }
            foreach (var k in order.OrderByDescending(k => byName[k].Certain ? 1 : 0))
                scan.Spirit.Add((k, byName[k].Ticks, byName[k].Angles, byName[k].Certain, byName[k].ShapeKey));
        }

        var hitSet = _hitSet;
        scan.TrailKind = hitSet?.ResolvedSlot is not null ? "fixed"
                        : (hitSet is not null && hitSet.Slots.Count > 0) ? "cand" : null;
        scan.HitPointPresent = HitPoint is not null;
        scan.ExTrailCount = ExTrails.Count;
        scan.HazardCulpritWanted = HazardCulpritWanted;
        scan.HazardCulpritPresent = HazardCulprit is not null;
        scan.HazardCulpritMatched = HazardAnyMatched(HazardCulprit);
        var bodyKinds = new List<string>();
        if (HazardCulprit is { } hcBody)
        {
            bool hasBullet = false, hasEnemy = false, hasLaser = false;
            foreach (var o in hcBody.All)
            {
                switch (o.Match?.Kind)
                {
                    case HitCandidates.HazardMatchKind.Bullet: hasBullet = true; break;
                    case HitCandidates.HazardMatchKind.Enemy: hasEnemy = true; break;
                    case HitCandidates.HazardMatchKind.Laser: hasLaser = true; break;
                }
            }
            if (hasBullet) bodyKinds.Add("bullet");
            if (hasEnemy) bodyKinds.Add("enemy");
            if (hasLaser) bodyKinds.Add("laser");
        }
        scan.HazardCulpritBodyKinds = bodyKinds;
        scan.FoeChar = _foeChar;
        scan.ExArmedHit = hitSet is not null && hitSet.ResolvedSlot is not null
            && (hitSet.Type == HitTypeKind.ExCircle || (hitSet.Type == HitTypeKind.Contact && hitSet.HowFromEx));

        var mats = scan.Materials;
        foreach (var cls in scan.EnemyClasses)
        {
            if (scan.EnemyPlain.Contains(cls)) mats.Add(LegendMaterialKeys.Enemy(cls, false));
            if (scan.EnemyHit.Contains(cls)) mats.Add(LegendMaterialKeys.Enemy(cls, true));
        }
        if (scan.EnemyRanged) mats.Add(LegendMaterialKeys.EnemyRanged);
        if (scan.GhostActivated) mats.Add(LegendMaterialKeys.EnemyGhostActivated);
        foreach (var sp in scan.Sprites)
        {
            var half = scan.SpriteHalf[sp];
            mats.Add(LegendMaterialKeys.Sprite(sp, half.W, half.H));
        }
        foreach (var o in scan.Origins)
        {
            mats.Add(o.Origin is null && o.Cls is null && o.Level is null && o.Sub is null
                ? LegendMaterialKeys.OriginNoRecorded
                : LegendMaterialKeys.Origin(o.Origin, o.Cls, o.Level, o.Sub,
                                            BulletOrigin.Tier(o.Origin, o.Cls) ?? "-"));
        }
        if (scan.ExArmedCard2) mats.Add(LegendMaterialKeys.ExArmed("card2"));
        if (scan.ExArmedCard3) mats.Add(LegendMaterialKeys.ExArmed("card3"));
        if (scan.ExArmedBoss) mats.Add(LegendMaterialKeys.ExArmed("boss"));
        if (scan.ExArmedUnknown) mats.Add(LegendMaterialKeys.ExArmed("unknown"));
        if (scan.ExArmedFoe) mats.Add(LegendMaterialKeys.ExArmed("foe"));
        if (scan.ExArmedHit) mats.Add(LegendMaterialKeys.ExArmed("hit"));
        if (scan.ExCross) mats.Add(LegendMaterialKeys.ExCross);
        foreach (var sub in scan.LaserOriginBossSubs) mats.Add(LegendMaterialKeys.LaserOrigin("boss", sub));
        if (scan.LaserOriginEx) mats.Add(LegendMaterialKeys.LaserOrigin("ex", null));
        if (scan.LaserOriginUnknown) mats.Add(LegendMaterialKeys.LaserOrigin("unknown", null));
        foreach (var (key, v) in scan.Shots)
            mats.Add(LegendMaterialKeys.Shot(key, v.LoW, v.HiW, v.LoH, v.HiH));
        mats.Add(LegendMaterialKeys.Self(SelfSide(Math.Max(0, Math.Min(n - 1, HitIndex - 1)))));
        if (InvSpanCount > 0) mats.Add(LegendMaterialKeys.InvSpans);
        foreach (var (_, _, _, certain, shapeKey) in scan.Spirit)
            mats.Add(LegendMaterialKeys.Spirit(shapeKey, certain));
        foreach (var kind in scan.RingKinds) mats.Add(LegendMaterialKeys.Ring(kind));
        if (scan.RingEst) mats.Add(LegendMaterialKeys.RingEst);
        if (scan.Blast) mats.Add(LegendMaterialKeys.Blast);
        if (scan.TrailKind is not null) mats.Add(LegendMaterialKeys.Trail(scan.TrailKind));
        if (scan.HitPointPresent && scan.TrailKind is null) mats.Add(LegendMaterialKeys.HitPoint);
        if (scan.ExTrailCount > 0) mats.Add(LegendMaterialKeys.ExTrail(scan.ExTrailCount));
        if (scan.HazardCulpritWanted)
            mats.Add(LegendMaterialKeys.HazardCulprit(scan.HazardCulpritPresent, scan.HazardCulpritMatched));
        foreach (var hcbk in scan.HazardCulpritBodyKinds) mats.Add(LegendMaterialKeys.HazardCulpritBody(hcbk));
        if (!BulletOrigin.HasRecorded(w)) mats.Add(LegendMaterialKeys.NotRecorded("origin"));
        if (!PlayerShots.HasShotSlots(w)) mats.Add(LegendMaterialKeys.NotRecorded("shot"));
        if (!InvRecorded) mats.Add(LegendMaterialKeys.NotRecorded("inv"));
        if (!SpiritField.HasWords(w) && scan.Spirit.Count == 0) mats.Add(LegendMaterialKeys.NotRecorded("spirit"));
        if (!EnemyBlasts.HasKindIndex(w) && !scan.Blast) mats.Add(LegendMaterialKeys.NotRecorded("blast"));

        return scan;
    }

    internal const int CharMarisa = LaserOrigin.CharMarisa, CharIku = LaserOrigin.CharIku;

    private void BuildLegend()
    {
        var rows = new List<LegendRow>();
        var scan = _legendScan;
        var w = _window;
        if (scan is not null && w is not null)
        {
            var pal = Views.BoardPalette.Of(Theme);
            var side = pal.Side(Side);
            int i = Math.Max(0, Math.Min(Math.Max(0, TickCount - 1), HitIndex - 1));
            int foe = 3 - Side;
            string charMe = CharacterAt(Side, i);
            string charFoe = CharacterAt(foe, i);

            void Category(string name, Action body)
            {
                int before = rows.Count;
                body();
                if (rows.Count > before) rows.Insert(before, new LegendRow { Header = true, Text = name });
            }

            string playerCategory = LegendCategoryPlayer + " - " + charMe;
            string bulletUniqueCategory = "弾幕 - " + charFoe;

            LegendRow ExArmedRow(bool? fromBoss, string note, string materialKind, int? cardLevel = null)
            {
                var armed = pal.ArmedExColor(Side, isHit: false, fromBoss, cardLevel, scan.FoeChar ?? 0);
                return new LegendRow
                {
                    Swatch = pal.Fade(armed, Views.BoardPalette.ExArmFillA),
                    Stroke = pal.Fade(armed, Views.BoardPalette.ExArmLineA),
                    StrokeW = Views.BoardPalette.TrailW,
                    Text = LegendExArmed,
                    Note = note,
                    Key = LegendMaterialKeys.ExArmed(materialKind),
                };
            }
            string ExArmedNoteCard(int level) =>
                "実線・" + (BoardLabels.CardLevelJa(level) ?? ("レベル" + level)) + "の設置物";
            bool bossExToBossBucket = scan.ExArmedBoss && (scan.FoeChar == CharMarisa || scan.FoeChar == CharIku);

            Category(LegendCategoryEnemy, () =>
            {
                foreach (var cls in scan.EnemyClasses)
                {
                    var brush = pal.EnemyBrush(cls);
                    bool none = EnemySize.Half(cls)?.None ?? false;
                    foreach (bool hitOne in new[] { false, true })
                    {
                        if (!(hitOne ? scan.EnemyHit : scan.EnemyPlain).Contains(cls)) continue;
                        var col = hitOne ? side : brush;
                        var note = new List<string>();
                        if (none) note.Add(LegendNoHit);
                        if (cls == "fairy" && scan.EnemyRanged) note.Add(LegendEnemyRanged);
                        if (hitOne) note.Add(LegendEnemyHitOne);
                        rows.Add(new LegendRow
                        {
                            Swatch = none ? pal.Fade(col, Views.BoardPalette.EnemyNoHitFillA) : null,
                            Stroke = col,
                            Dashed = none,
                            Text = BoardLabels.EnemyJa(cls),
                            Note = string.Join(LegendNoteJoin, note),
                            Key = LegendMaterialKeys.Enemy(cls, hitOne)
                                + (cls == "fairy" && scan.EnemyRanged
                                   ? "|" + LegendMaterialKeys.EnemyRanged : ""),
                        });
                    }
                    if (cls == "ghost" && scan.GhostActivated)
                        rows.Add(new LegendRow
                        {
                            Stroke = pal.GhostActivatedBrush,
                            Text = LegendGhostActivated,
                            Key = LegendMaterialKeys.EnemyGhostActivated,
                        });
                }
            });

            string OriginKey((string? Origin, string? Cls, int? Level, int? Sub) o) =>
                o.Origin is null && o.Cls is null && o.Level is null && o.Sub is null
                    ? LegendMaterialKeys.OriginNoRecorded
                    : LegendMaterialKeys.Origin(o.Origin, o.Cls, o.Level, o.Sub,
                                                BulletOrigin.Tier(o.Origin, o.Cls) ?? "-");

            LegendRow OriginRow((string? Origin, string? Cls, int? Level, int? Sub) o) => new()
            {
                Swatch = pal.OriginBrush(o.Origin, o.Cls, o.Level, o.Sub),
                Text = OriginLegendText(o.Origin, o.Cls, o.Level, o.Sub),
                Key = OriginKey(o),
            };

            LegendRow OriginUniqueRow((string? Origin, string? Cls, int? Level, int? Sub) o)
            {
                var (name, note) = OriginUniqueParts(o.Origin, o.Cls, o.Level, o.Sub);
                return new LegendRow
                {
                    Key = OriginKey(o),
                    Swatch = pal.OriginBrush(o.Origin, o.Cls, o.Level, o.Sub),
                    Text = name,
                    Note = note ?? "",
                };
            }

            Category(bulletUniqueCategory, () =>
            {
                if (ColorBy != "origin") return;
                foreach (var o in scan.Origins.Where(o => BulletOrigin.Tier(o.Origin, o.Cls) == BulletOrigin.TierBoss))
                    rows.Add(OriginUniqueRow(o));
                if (bossExToBossBucket) rows.Add(ExArmedRow(true, LegendExArmedNoteBoss, "boss"));
                foreach (var o in scan.Origins.Where(o => BulletOrigin.Tier(o.Origin, o.Cls) == BulletOrigin.TierCard)
                                              .OrderByDescending(o => o.Level ?? int.MinValue))
                    rows.Add(OriginUniqueRow(o));
                if (scan.ExArmedCard3) rows.Add(ExArmedRow(false, ExArmedNoteCard(3), "card3", cardLevel: 3));
                if (scan.ExArmedCard2) rows.Add(ExArmedRow(false, ExArmedNoteCard(2), "card2", cardLevel: 2));
                foreach (var o in scan.Origins.Where(o => BulletOrigin.Tier(o.Origin, o.Cls) == BulletOrigin.TierEx))
                    rows.Add(OriginUniqueRow(o));
                if (scan.ExArmedBoss && !bossExToBossBucket) rows.Add(ExArmedRow(true, LegendExArmedNoteBoss, "boss"));
                if (scan.ExArmedUnknown) rows.Add(ExArmedRow(null, LegendExArmedNoteUnknown, "unknown"));
                if (scan.ExArmedFoe) rows.Add(ExArmedRow(false, LegendExArmedNoteFoe, "foe"));
                if (scan.ExCross)
                    rows.Add(new LegendRow
                    {
                        Stroke = pal.Dead,
                        Dashed = true,
                        Text = LegendExCross,
                        Note = LegendExCrossNote,
                        Key = LegendMaterialKeys.ExCross,
                    });
                foreach (var sub in scan.LaserOriginBossSubs)
                {
                    var brush = pal.LaserOriginColor(Side, isHit: false, LaserOriginKind.BossChild, sub, scan.FoeChar ?? 0);
                    rows.Add(new LegendRow
                    {
                        Swatch = pal.Fade(brush, Views.BoardPalette.LaserFoeA),
                        Bar = true,
                        Text = LegendLaserOriginBossJa,
                        Note = LaserOriginBossNoteJa(sub),
                        Key = LegendMaterialKeys.LaserOrigin("boss", sub),
                    });
                }
                if (scan.LaserOriginEx)
                {
                    var brush = pal.LaserOriginColor(Side, isHit: false, LaserOriginKind.ExChild, null, scan.FoeChar ?? 0);
                    rows.Add(new LegendRow
                    {
                        Swatch = pal.Fade(brush, Views.BoardPalette.LaserFoeA),
                        Bar = true,
                        Text = LegendLaserOriginExJa,
                        Note = LegendLaserOriginExNote,
                        Key = LegendMaterialKeys.LaserOrigin("ex", null),
                    });
                }
                if (scan.LaserOriginUnknown)
                {
                    var brush = pal.LaserOriginColor(Side, isHit: false, LaserOriginKind.Unknown, null, scan.FoeChar ?? 0);
                    rows.Add(new LegendRow
                    {
                        Swatch = pal.Fade(brush, Views.BoardPalette.LaserFoeA),
                        Bar = true,
                        Text = LegendLaserOriginUnknownJa,
                        Note = LegendLaserOriginUnknownNote,
                        Key = LegendMaterialKeys.LaserOrigin("unknown", null),
                    });
                }
            });

            Category(LegendCategoryBulletCommon, () =>
            {
                if (ColorBy == "origin")
                {
                    foreach (var o in scan.Origins.Where(o => BulletOrigin.Tier(o.Origin, o.Cls) == BulletOrigin.TierLily))
                        rows.Add(OriginUniqueRow(o));
                    foreach (var o in scan.Origins.Where(o => BulletOrigin.Tier(o.Origin, o.Cls) == BulletOrigin.TierGhostPenalty))
                        rows.Add(OriginRow(o));
                    foreach (var o in scan.Origins.Where(o => BulletOrigin.Tier(o.Origin, o.Cls) == BulletOrigin.TierWhiteOrigin))
                        rows.Add(OriginRow(o));
                    if (scan.SpriteHalf.TryGetValue(0, out var faintHalf))
                        rows.Add(new LegendRow
                        {
                            Swatch = pal.Erasable(pal.BulletSpriteBrush(0)),
                            Text = LegendFaintBullet,
                            Key = LegendMaterialKeys.Sprite(0, faintHalf.W, faintHalf.H),
                        });
                    var otherOrigins = scan.Origins.Where(o => BulletOrigin.Tier(o.Origin, o.Cls) == BulletOrigin.TierOther).ToList();
                    if (otherOrigins.Count > 0)
                    {
                        var rep = otherOrigins[0];
                        rows.Add(new LegendRow
                        {
                            Swatch = pal.OriginBrush(rep.Origin, rep.Cls, rep.Level, rep.Sub),
                            Text = LegendBulletOther,
                            Note = LegendBulletOtherNote,
                            Key = string.Join("|", otherOrigins.Select(OriginKey)),
                        });
                    }
                    if (scan.Origins.Any(o => o.Origin is null))
                        rows.Add(OriginRow((null, null, null, null)));
                    if (!BulletOrigin.HasRecorded(w))
                        rows.Add(new LegendRow
                        {
                            Wide = true, Text = LegendNoOriginWindow,
                            Key = LegendMaterialKeys.NotRecorded("origin"),
                        });
                }
                else
                {
                    var order = new List<string>();
                    var group = new Dictionary<string, (string Name, int? Owner, List<int> Sp, (double W, double H) Half)>(StringComparer.Ordinal);
                    foreach (var sp in scan.Sprites)
                    {
                        var parts = BulletOrigin.SpriteParts(sp);
                        string name = parts.Name ?? ("sprite " + sp.ToString(CultureInfo.InvariantCulture));
                        string key = name + "|" + (parts.Name is null ? "unknown"
                            : sp == 0 ? "white"
                            : parts.Shared ? "shared"
                            : "char" + parts.Owner!.Value.ToString(CultureInfo.InvariantCulture));
                        if (!group.TryGetValue(key, out var got))
                        {
                            order.Add(key);
                            group[key] = (name, parts.Owner, [sp], scan.SpriteHalf[sp]);
                        }
                        else got.Sp.Add(sp);
                    }
                    foreach (var key in order)
                    {
                        var g = group[key];
                        int lead = g.Sp[0];
                        rows.Add(new LegendRow
                        {
                            Swatch = lead == 0 ? pal.Erasable(pal.BulletSpriteBrush(lead))
                                               : pal.BulletSpriteBrush(lead),
                            Text = g.Name + (g.Owner is null ? ""
                                : "（" + Data.ReplayLabels.Character(g.Owner.Value) + "）"),
                            Note = LegendHitBox + Num(Math.Round(g.Half.W, 2) * 2)
                                 + "x" + Num(Math.Round(g.Half.H, 2) * 2)
                                 + " / sprite " + string.Join("・", g.Sp.Select(
                                     x => x.ToString(CultureInfo.InvariantCulture))),
                            Key = string.Join("|", g.Sp.Select(
                                x => LegendMaterialKeys.Sprite(x, scan.SpriteHalf[x].W, scan.SpriteHalf[x].H))),
                        });
                    }
                }
            });

            Category(playerCategory, () =>
            {
                rows.Add(new LegendRow
                {
                    Swatch = side,
                    Text = LegendSelf,
                    Note = Num(SelfSide(i)),
                    Key = LegendMaterialKeys.Self(SelfSide(i)),
                });

                if (InvSpanCount > 0)
                    rows.Add(new LegendRow
                    {
                        Swatch = pal.Fade(side, Views.BoardPalette.SelfInvA),
                        Stroke = side,
                        Text = LegendSelfInv,
                        Key = LegendMaterialKeys.InvSpans,
                    });
                else if (!InvRecorded)
                    rows.Add(new LegendRow
                    {
                        Text = LegendSelfInv, Note = LegendNotRecorded,
                        Key = LegendMaterialKeys.NotRecorded("inv"),
                    });

                foreach (var key in new[] { 1, 0, 2 })
                {
                    if (!scan.Shots.TryGetValue(key, out var v)) continue;
                    rows.Add(new LegendRow
                    {
                        Swatch = pal.Fade(pal.ShotBrush(Side, ShotC1Of(key)),
                                          key == 1 ? Views.BoardPalette.ShotAC1 : Views.BoardPalette.ShotA),
                        Text = PlayerShots.LegendHeading(ShotC1Of(key)),
                        Note = ShotSizeNote(v),
                        Key = LegendMaterialKeys.Shot(key, v.LoW, v.HiW, v.LoH, v.HiH),
                    });
                }
                if (!PlayerShots.HasShotSlots(w))
                    rows.Add(new LegendRow
                    {
                        Text = PlayerShots.LegendJa, Note = LegendNoShotSlots,
                        Key = LegendMaterialKeys.NotRecorded("shot"),
                    });

                string shapeJa = MainAt("p" + Side + "_character", i) is double scv
                    ? SpiritShapeNames.Ja(SpiritField.Of((int)scv)?.Name) : "";
                foreach (var (label, ticks, angles, certain, shapeKey) in scan.Spirit)
                    rows.Add(new LegendRow
                    {
                        Swatch = pal.Fade(side, Views.BoardPalette.SpiritFillA),
                        Stroke = pal.Fade(side, Views.BoardPalette.SpiritLineA),
                        StrokeW = Views.BoardPalette.SpiritW,
                        Note = shapeJa,
                        Round = true,
                        Dashed = !certain,
                        Text = label,
                        Key = LegendMaterialKeys.Spirit(shapeKey, certain),
                    });
                if (!SpiritField.HasWords(w) && scan.Spirit.Count == 0)
                    rows.Add(new LegendRow
                    {
                        Text = SpiritField.FieldJa, Note = LegendNotRecorded,
                        Key = LegendMaterialKeys.NotRecorded("spirit"),
                    });
            });

            Category(LegendCategoryOther, () =>
            {
                if (scan.RingKinds.Count > 0)
                    rows.Add(new LegendRow
                    {
                        Swatch = pal.Fade(side, Views.BoardPalette.ClearRingFillA),
                        Stroke = pal.Fade(side, Views.BoardPalette.ClearRingLineA),
                        StrokeW = Views.BoardPalette.ClearRingW,
                        Round = true,
                        Text = ClearRings.LegendJa,
                        Key = string.Join("|", scan.RingKinds.OrderBy(k => k, StringComparer.Ordinal)
                                                             .Select(LegendMaterialKeys.Ring)),
                    });

                if (scan.RingEst)
                    rows.Add(new LegendRow
                    {
                        Stroke = side,
                        StrokeW = Views.BoardPalette.ClearRingW,
                        Round = true,
                        Dashed = true,
                        Text = LegendRingEst,
                        Note = LegendRingEstNote,
                        Key = LegendMaterialKeys.RingEst,
                    });

                if (scan.Blast)
                    rows.Add(new LegendRow
                    {
                        Swatch = pal.Fade(pal.WhiteBullet, Views.BoardPalette.BlastFillA),
                        Stroke = pal.Fade(pal.WhiteBullet, Views.BoardPalette.BlastLineA),
                        StrokeW = Views.BoardPalette.BlastW,
                        Round = true,
                        Text = EnemyBlasts.Ja,
                        Key = LegendMaterialKeys.Blast,
                    });
                else if (!EnemyBlasts.HasKindIndex(w))
                    rows.Add(new LegendRow
                    {
                        Text = EnemyBlasts.Ja, Note = EnemyBlasts.NoKindNote,
                        Key = LegendMaterialKeys.NotRecorded("blast"),
                    });

                var set = _hitSet;
                if (scan.TrailKind == "fixed")
                    rows.Add(new LegendRow
                    {
                        Swatch = pal.Fade(side, Views.BoardPalette.TrailAFixed),
                        Bar = true,
                        Text = LegendTrail,
                        Key = LegendMaterialKeys.Trail("fixed"),
                    });
                else if (scan.TrailKind == "cand")
                    rows.Add(new LegendRow
                    {
                        Swatch = pal.Fade(side, (set?.SizeRanged ?? false) ? Views.BoardPalette.TrailAEst
                                                                           : Views.BoardPalette.TrailACand),
                        Bar = true,
                        Dashed = true,
                        Text = LegendTrail,
                        Key = LegendMaterialKeys.Trail("cand"),
                    });
                if (scan.TrailKind is null && scan.HitPointPresent)
                    rows.Add(new LegendRow
                    {
                        Stroke = pal.Fade(side, Views.BoardPalette.TrailAEst),
                        Dashed = true,
                        Text = LegendHitPoint,
                        Note = LegendHitPointNote,
                        Key = LegendMaterialKeys.HitPoint,
                    });


                if (scan.ExTrailCount > 0)
                    rows.Add(new LegendRow
                    {
                        Swatch = pal.Fade(side, Views.BoardPalette.TrailACand),
                        Bar = true,
                        Dashed = true,
                        Text = LegendExTrail,
                        Note = "ほかに " + scan.ExTrailCount.ToString(CultureInfo.InvariantCulture) + " 本",
                        Key = LegendMaterialKeys.ExTrail(scan.ExTrailCount),
                    });

                if (scan.HazardCulpritWanted)
                {
                    bool strong = scan.HazardCulpritMatched;
                    rows.Add(new LegendRow
                    {
                        Swatch = pal.Fade(side, strong ? Views.BoardPalette.TrailAFixed : Views.BoardPalette.TrailACand),
                        Bar = true,
                        Dashed = !strong,
                        Text = HazardCulpritJa,
                        Note = !scan.HazardCulpritPresent ? HazardCulpritHowNone
                             : strong ? HazardCulpritHow : HazardCulpritHowUnmatched,
                        Key = LegendMaterialKeys.HazardCulprit(scan.HazardCulpritPresent, scan.HazardCulpritMatched),
                    });
                }

                foreach (var bodyKind in scan.HazardCulpritBodyKinds)
                    rows.Add(new LegendRow
                    {
                        Swatch = side,
                        Round = false,
                        Text = HazardCulpritBodyJa,
                        Note = HazardCulpritBodyNoteJa(bodyKind),
                        Key = LegendMaterialKeys.HazardCulpritBody(bodyKind),
                    });
            });
        }
        LegendRows = rows;
        OnPropertyChanged(nameof(LegendRows));
    }

    private static string Num(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

    private static string Range(double lo, double hi) =>
        lo == hi ? Num(lo * 2) : Num(lo * 2) + "〜" + Num(hi * 2);

    private static string ShotSizeNote((double LoW, double HiW, double LoH, double HiH) v)
    {
        bool varies = v.LoW != v.HiW || v.LoH != v.HiH;
        return LegendHitBox + Range(v.LoW, v.HiW) + "x" + Range(v.LoH, v.HiH)
             + (varies ? "（" + PlayerShots.SizeRangeJa + "）" : "");
    }

    partial void OnSideChanged(int value)
    {
        _boardCache = null;
        _boardCacheTick = -1;
        _legendScan = null;
        BuildStrips();
        BuildStatic();
        Revision++;
        NotifyPaneLook();
    }

    partial void OnOtherChanged(HitWindowViewModel? value) => NotifyPaneLook();

    partial void OnColorByChanged(string value)
    {
        BuildLegend();
        AfterOptionChanged();
    }

    partial void OnThemeChanged(string value)
    {
        BuildLegend();
        BuildCardLegend();
        OnPropertyChanged(nameof(CardLegendRows));
        AfterOptionChanged();
        OnPropertyChanged(nameof(HitSideBrush));
    }

    partial void OnEmphChanged(string value) => AfterOptionChanged();

    partial void OnExTrailChanged(string value) => AfterOptionChanged();

    partial void OnStartAtChanged(string value)
    {
        TickIndex = StartIndex();
        AfterOptionChanged();
    }

    partial void OnSpeedChanged(double value)
    {
        OnPropertyChanged(nameof(SpeedText));
        AfterOptionChanged();
    }

    partial void OnTickCountChanged(int value) => OnPropertyChanged(nameof(SeekMax));

    partial void OnZoomChanged(double value)
    {
        OnPropertyChanged(nameof(BoardWidth));
        OnPropertyChanged(nameof(BoardHeight));
        OnPropertyChanged(nameof(PaneMargin));
        AfterOptionChanged();
    }

    public void StepBy(int d)
    {
        if (TickCount <= 0) return;
        TickIndex = Math.Clamp(TickIndex + d, 0, TickCount - 1);
    }

    [RelayCommand]
    private void TogglePlay() => Playing = !Playing;

    [RelayCommand]
    private void GoHome() => TickIndex = 0;

    [RelayCommand]
    private void GoHit() => TickIndex = Math.Clamp(HitIndex, 0, Math.Max(0, TickCount - 1));

    public void SpeedBump(int d)
    {
        var s = HitWindowOptions.Of("speed").Slider!.Value;
        SetSpeed(Speed + d * s.Step);
    }

    public void SetSpeed(double v)
    {
        var s = HitWindowOptions.Of("speed").Slider!.Value;
        double q = Math.Min(s.Max, Math.Max(s.Min, v));
        Speed = Math.Round(Math.Round(q / s.Step) * s.Step, 2);
    }

    public void SetZoom(double v) =>
        Zoom = Math.Round(Math.Min(HitWindowOptions.ZoomMax,
                                   Math.Max(HitWindowOptions.ZoomMin, v)), 2);

    public void ZoomBump(int d) => SetZoom(Zoom + d * HitWindowOptions.ZoomStep);

    public double TickMs => 1000.0 / 60.0 / (Speed <= 0 ? 1 : Speed);

    public void ResetOptions()
    {
        Theme = HitWindowOptions.Of("theme").Def;
        ColorBy = HitWindowOptions.Of("colorby").Def;
        Emph = HitWindowOptions.Of("emph").Def;
        ExTrail = HitWindowOptions.Of("extrail").Def;
        StartAt = HitWindowOptions.Of("startat").Def;
        SetZoom(double.Parse(HitWindowOptions.Of("zoom").Def, CultureInfo.InvariantCulture));
        SetSpeed(double.Parse(HitWindowOptions.Of("speed").Def, CultureInfo.InvariantCulture));
    }



    public int SeekMax => Math.Max(0, TickCount - 1);

    public string EventText => string.Create(CultureInfo.InvariantCulture,
        $"起点 {Events.Count} 件");

    public IReadOnlyList<LegendRow> LegendRows { get; private set; } = [];

    internal IReadOnlyList<string> LegendMaterials => _legendScan?.Materials ?? [];

    public IReadOnlyList<string> DefsRows { get; } =
    [
        "チャージゲージ … ★記録した値をそのまま出している（推測も復元もしていない）。"
        + "★塗り＝ゲージ（0〜" + HitWindowGauge.Max.ToString("0", CultureInfo.InvariantCulture) + "）／"
        + "★バーの下の細い線＝溜め ——★長さの意味が違うので塗りにしていない。"
        + "★右の数字＝カードアタック / ボスカードアタックのレベル（読めなかったほうは出さないので、1 つしか出ていないこともある）。"
        + "★バーの中の縦線は境目（"
        + string.Join(" / ", HitWindowGauge.Marks.Select(
            m => m.Value.ToString("0", CultureInfo.InvariantCulture) + "＝" + m.Text))
        + "）。★読めなかった tick は棒を出さない（0 と区別するため）。",
        "弾 … 判定サイズ（一辺の半分）の軸平行矩形で描いた。★大きさに下限は掛けていないので、見えているのは全部実寸。"
        + "★当たった 1 発だけは軌跡の終端を側の色で塗り直す（他の弾は種類 / 由来の色のまま）。"
        + "★判定サイズの表に無い sprite は、既定の一辺（"
        + DefaultBulletSide.ToString("0.#", CultureInfo.InvariantCulture) + "）で描く（未採取のまま）。",
        "記号の輪 … 破線の輪が付くのは当たった弾・当たった Ex・当たった敵・候補のもの・被弾の位置の 5 か所で、"
        + "どれも実寸ではない（自機の位置を示す輪は別の固定記号）。",
        "レーザー … 線の太さが判定幅そのもの。★致死でないものは当たり判定が無いので、太さではなく点線で言い分けた（点線の太さは実寸ではない）。",
        "敵 … ★判定サイズが分かるものは実寸の矩形・実線で描いた"
        + "（幅が確定しない枠だけ、外側にもう 1 本を点線で足している）。"
        + "★当たり判定を持たないものは点線と淡い塗り、★大きさを知らないものは実線で、どちらも記号の大きさ"
        + " ——★「当たらない」と「測れていない」を同じ絵にしていない。色は分類。",
        "自機 … 中の正方形が実寸。まわりの一回り大きい抜きは記号で、判定ではない。",
        "帯 … ★弾数の 2 本は上限を固定してある（軸の右端がその上限）。"
        + "★上限を固定していない 2 本（敵数・Ex の数）だけが窓ごとに伸縮するので、両端に数字を出している。",
        "記録が無い列 … ★線を引かずに、そう書く。平らな 0 の線を引くと「1 個も出なかった」と読めてしまうため。",
        "当たった敵の確度 … 「★確定」に続く語で判断の中身を言っている。判断に使った内部の鍵（プログラムの中の名前）はそのままでは出さない。",


        "確度 … 弾はゲーム自身が持つ枠のポインタ、レーザーは記録した始点（角度まで合えばそれも使う）、"
        + "敵と Ex は当たった判定要素の座標で、それぞれ 1 つに決めている。1 つに決まらなかったものは"
        + "全部を候補として残す（1 つに倒さない）。線は「実線＝確定・点線＝確定でない」の 2 段だけで、"
        + "確からしさを太さでは表さない（太さは判定サイズを表すため）。",
        "軌跡が途中で切れる … 弾やレーザーは枠の番号で追っているが、枠の番号はものの区別ではない。"
        + "同じ枠に別のものが入ったと分かった所で線を切っている（切りすぎる側に倒してある）。"
        + "見分けが付かない入れ替わりだけは、繋がったまま残る。",
        "候補 … 軌跡だけだと盤面のどれが候補か読めないので、候補のものにも破線の輪を付ける。"
        + "確定も候補も 1 つも出せない窓は、ゲームが持っていた被弾の 1 点だけを × で打ち、"
        + "盤面の下に理由を 1 行出す（レーザーの被弾では座標を信用できないので × は打たない）。",
        "Ex 本体（円・矩形） … 軌跡と同じ考え方で描く：判定が生きている間は実線、"
        + "効果だけで判定が無いものは点線、判定を持たないもの（親など）は記号の大きさの十字。"
        + "当たった 1 個には輪も付ける。",

        "帯（確度） … 帯の塗りは種別で変えない。名前に「（推定）」と付く帯だけが推定で、それ以外は確定。",
        "決着で打ち切る … この窓はラウンドの決着の少し先で終わる。記録を捨てたのではなく、表示の長さだけを切っている。",
        "窓の頭 … 窓がラウンド開始前まで伸びていた回は、その帯の手前で切ってある（表示の長さだけ）。",
        "咲夜ミラー … ゲームがどちらの自機も止めないので、自機が止まった側からは帯を作れない。"
        + "ゲームが持つ語がある窓では確定、無い窓は盤面が両陣とも動かないことから推定"
        + "（名前の末尾に付く）。両方が撃っている回は、先に撃った方を主導としている（間違っている可能性がある）。",
        "帯の入れ子 … カットインの帯は時止めの帯の中に入れ子で描く（内側の本数だけ外側が伸びる）。"
        + "盤面の文字はいちばん内側の帯の名前を出す。",
        "凍結の見せ方 … 帯の間は盤面全体を薄く沈める。強調を「枠」にすると、この沈みを外す。",

        "この陣の Ex の数 … 相手が撃った Ex のうち、この陣に出ているものの数。"
        + "ゲーム自身が持つカウンタから取るので、座標リングが古くて盤面に Ex を描けない窓でも、"
        + "このグラフだけは出る。",

        "「Ex の軌跡」の設定 … 既定は「被弾のみ」＝確定した 1 本だけを実線で出す。"
        + "「すべて」にすると、残りの Ex の軌跡も点線・淡く出す。"
        + "「なし」でも盤面の Ex 本体（円・矩形・十字）は消えない（消えるのは軌跡だけ）。",
        .. ExItems.EffectNotes().Select(n => n.Ja + " … " + n.Note),

        "設定は覚えている … 選んだ設定を保存し、次に窓を開いたときも同じ設定で始まる。"
        + "「" + HitWindowOptions.ResetJa + "」を押すと、全部その場で既定に戻る。",
        "被弾での停止は速度で変わらない … 再生は被弾とクイックカードアタックの起点で "
        + (Views.HitWindowView.HoldMs / 1000.0).ToString("0.##", CultureInfo.InvariantCulture)
        + " 秒止まる。この停止は見るための停止であって再生の一部ではないので、速度では割らない。"
        + "同じ tick では二度と止まらない。",
        "キーの操作 … ← → は 1 tick、Shift ＋ ← → は 10 tick。Space が再生・停止、"
        + "Home が窓の先頭、H が被弾の瞬間、Esc が再生をやめて再生開始点へ戻る。",

        "弾消しリングは記録していない … 広がり方はキャラによらず同じなので、"
        + "いつ出たか（発動の起点）さえ分かれば、静的な表から形が全部出る。"
        + "だから座標リングを取り直さなくても、古い窓でもそのまま出る。",
        "止まっている間は太らない … リングが 1 段進むのは、その側の自機の更新が走ったフレームだけ。"
        + "演出（カットイン）の帯の間は止まり、時止めでは止めた側のリングだけが進む。",
        "シークバーの点 … 色が発動の出どころ。塗りつぶし＝盤面にリングが出る回、"
        + "中抜き＝出ない回（スペルポイント由来はリングが出ないので中抜き）。",
        "演出の帯の名前 … 帯の頭に発動が見つかった帯には「どちらが何を出したか」の名前が付く。"
        + "見つからなかった帯（ボスの攻撃宣言など）は種別の名前のまま。",
        "点と帯の名前 … 側と発動の名前（例: 側＋レベル）。括弧の中はその発動の出どころ。",
        "クイックで少し止まる … クイックカードアタックの起点でも、被弾と同じだけ止まる。"
        + "通常のカードアタックとスペルポイント由来では止めない（1 試合に何十回も出るため）。",
        "窓の頭より前から続くリング … 窓が始まった瞬間にもう出ていたリングも描く。"
        + "推定ではなく、窓の外の記録をそのまま読んでいる。"
        + "そのリングはシークバーに点が出ない（点を打つ場所が窓の外にあるため）——"
        + "「カーソル位置」の弾消しリングの行に、その断りが出る。",

        "何を無敵と呼んでいるか … ゲーム自身が被弾を判定する条件"
        + "（通常の状態で、かつ無敵タイマーが残っていない）を、そのまま裏返したもの。"
        + "長さは当てていない（毎 tick の記録をそのまま読む）。",
        "2 通りある … 状態によるもの（やられている最中）とタイマーによるものがあり、"
        + "重なる回もある。前者は動けないので、同じ「無敵」でも意味が逆になる。",
        "見せ方 … 無敵の間は自機を淡くし、位置の輪をもう 1 本外側に足す（二重）。"
        + "点滅にも色替えにもしていない。大きさは 1px も変えない（実寸なので強調に使えない）。",
        "ここに出ない無敵がある … " + PlayerInvincible.LimitNote,
    ];

    public List<OptionBarRow> OptionBars { get; } = [];

    private OptionGroupRow? _zoomGroup, _speedGroup;

    public HitWindowViewModel()
    {
        BuildOptionBars();
        Panes.Add(this);
    }

    private void BuildOptionBars()
    {
        foreach (var row in HitWindowOptions.Bars)
        {
            var groups = new List<OptionGroupRow>();
            foreach (var key in row)
            {
                if (key == HitWindowOptions.ResetKey)
                {
                    groups.Add(new OptionGroupRow
                    {
                        Key = key, Label = "", IsSlider = false,
                        Picks =
                        [
                            new OptionPick
                            {
                                Value = key, Text = HitWindowOptions.ResetJa, StdJa = "",
                                ChooseCommand = new RelayCommand(ResetOptions),
                            },
                        ],
                    });
                    continue;
                }
                var o = HitWindowOptions.Of(key);
                if (o.IsSlider)
                {
                    var s = o.Slider!.Value;
                    bool isZoom = key == "zoom";
                    var g = new OptionGroupRow
                    {
                        Key = key, Label = o.Ja, IsSlider = true,
                        DecJa = o.StepJa!.Value.Dec, IncJa = o.StepJa.Value.Inc,
                        Min = s.Min, Max = s.Max, Step = s.Step,
                        Picks = o.Picks.Select(v => new OptionPick
                        {
                            Value = v, Text = v, StdJa = v == o.Def ? s.StdJa : "",
                            ChooseCommand = new RelayCommand(() =>
                            {
                                double q = double.Parse(v, CultureInfo.InvariantCulture);
                                if (isZoom) SetZoom(q); else SetSpeed(q);
                            }),
                        }).ToList(),
                        DecCommand = new RelayCommand(() => { if (isZoom) ZoomBump(-1); else SpeedBump(-1); }),
                        IncCommand = new RelayCommand(() => { if (isZoom) ZoomBump(1); else SpeedBump(1); }),
                        OnValue = q => { if (isZoom) SetZoom(q); else SetSpeed(q); },
                    };
                    if (isZoom) _zoomGroup = g; else _speedGroup = g;
                    groups.Add(g);
                    continue;
                }
                groups.Add(new OptionGroupRow
                {
                    Key = key, Label = o.Ja, IsSlider = false,
                    Picks = o.Picks.Select(v => new OptionPick
                    {
                        Value = v, Text = o.Text[v], StdJa = "",
                        ChooseCommand = new RelayCommand(() => SetOption(key, v)),
                    }).ToList(),
                });
            }
            OptionBars.Add(new OptionBarRow { Groups = groups });
        }
        SyncOptionGroups();
    }

    public void SetOption(string key, string value)
    {
        switch (key)
        {
            case "theme": Theme = value; break;
            case "colorby": ColorBy = value; break;
            case "emph": Emph = value; break;
            case "extrail": ExTrail = value; break;
            case "loop": Loop = value; break;
            case "startat": StartAt = value; break;
            case "sides": Sides = value; break;
            default: throw new KeyNotFoundException("そんな設定の組はありません: " + key);
        }
    }

    private void AfterOptionChanged()
    {
        SyncOptionGroups();
        SaveOptions();
        Revision++;
        if (Other is { } o) MirrorOptionsTo(o);
    }

    private void SyncOptionGroups()
    {
        if (_zoomGroup is not null)
        {
            _zoomGroup.Value = Zoom;
            _zoomGroup.NowText = Zoom.ToString("F2", CultureInfo.InvariantCulture) + "x";
        }
        if (_speedGroup is not null)
        {
            _speedGroup.Value = Speed;
            _speedGroup.NowText = SpeedText;
        }
        foreach (var row in OptionBars)
        {
            foreach (var g in row.Groups)
            {
                var now = OptionValue(g.Key);
                foreach (var p in g.Picks)
                    p.IsOn = g.IsSlider ? SameNumber(now, p.Value, g.Step)
                                        : string.Equals(now, p.Value, StringComparison.Ordinal);
            }
        }
    }

    private static bool SameNumber(string now, string pick, double step)
    {
        if (!double.TryParse(now, NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
            || !double.TryParse(pick, NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
            return false;
        return Math.Abs(a - b) < (step > 0 ? step / 2.0 : 1e-9);
    }

    public void Close()
    {
        if (!_isMirror && Other is { } o)
        {
            o.Close();
            Other = null;
            RebuildPanes();
        }
        _db?.Dispose();
        _db = null;
        _window = null;
        _boardCache = null;
        _boardCacheTick = -1;
    }


    private bool _neverSave;

    public void DisableOptionSaving() => _neverSave = true;

    public void SaveOptions()
    {
        if (_neverSave) return;
        if (_suppressSave) return;
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var o in HitWindowOptions.All) map[o.Key] = OptionValue(o.Key);
        Data.UiSettings.Save(Data.UiSettings.HitWindowSection, map);
    }

    private bool _optionsLoaded;

    private bool _suppressSave;

    public void LoadOptions()
    {
        var saved = Data.UiSettings.Load(Data.UiSettings.HitWindowSection);
        if (saved.Count == 0) return;
        _suppressSave = true;
        try
        {
        foreach (var o in HitWindowOptions.All)
        {
            if (!saved.TryGetValue(o.Key, out var v) || string.IsNullOrEmpty(v)) continue;
            if (o.IsSlider)
            {
                if (!double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    continue;
                SetOptionValue(o.Key, d);
            }
            else
            {
                if (!o.Picks.Contains(v, StringComparer.Ordinal)) continue;
                SetOption(o.Key, v);
            }
        }
        }
        finally { _suppressSave = false; }
    }

    private string OptionValue(string key) => key switch
    {
        "theme" => Theme,
        "colorby" => ColorBy,
        "emph" => Emph,
        "extrail" => ExTrail,
        "loop" => Loop,
        "startat" => StartAt,
        "sides" => Sides,
        "zoom" => Zoom.ToString(CultureInfo.InvariantCulture),
        "speed" => Speed.ToString(CultureInfo.InvariantCulture),
        _ => "",
    };

    private void SetOptionValue(string key, double v)
    {
        if (key == "zoom") SetZoom(v);
        else if (key == "speed") SetSpeed(v);
    }


    public static IEnumerable<(string Label, Func<string?> Body)> OptionSelfTestCases()
    {
        yield return ($"組の母数（{HitWindowOptions.All.Length} 組）", () =>
            HitWindowOptions.All.Length > 0 ? null : "0 組。表を引けていない");

        yield return ("★全部の組が読み書きの口を持っている（組を足したら気づく）", () =>
        {
            var vm = new HitWindowViewModel { _suppressSave = true };
            var miss = HitWindowOptions.All.Where(o => vm.OptionValue(o.Key).Length == 0)
                                           .Select(o => o.Key).ToList();
            return miss.Count == 0 ? null : "口が無い組: " + string.Join(",", miss);
        });

        yield return ("★飛び飛びの組は、既定値が Picks に在る", () =>
        {
            var bad = HitWindowOptions.All.Where(o => !o.IsSlider)
                        .Where(o => !o.Picks.Contains(o.Def, StringComparer.Ordinal))
                        .Select(o => o.Key).ToList();
            return bad.Count == 0 ? null : "既定が選べない組: " + string.Join(",", bad);
        });

        yield return ("★★選べない値は捨てて既定のまま（黙って入れない）", () =>
        {
            var o = HitWindowOptions.Of("theme");
            return !o.Picks.Contains("no_such_theme", StringComparer.Ordinal)
                ? null : "選べない値が Picks に在る";
        });

        yield return ("★★★飛び飛びの組は、全部の値がボタンの道（SetOption）を通る", () =>
        {
            var vm = new HitWindowViewModel { _suppressSave = true };
            var bad = new List<string>();
            int pressed = 0;
            foreach (var o in HitWindowOptions.All.Where(x => !x.IsSlider))
            {
                foreach (var v in o.Picks)
                {
                    try { vm.SetOption(o.Key, v); pressed++; }
                    catch (Exception ex) { bad.Add(o.Key + "=" + v + " (" + ex.GetType().Name + ")"); }
                    if (!string.Equals(vm.OptionValue(o.Key), v, StringComparison.Ordinal))
                        bad.Add(o.Key + "=" + v + " は入ったが読み戻せない");
                }
            }
            if (pressed == 0) return "1 つも押していない（表を引けていない）";
            return bad.Count == 0 ? null : "落ちた / 戻らない: " + string.Join(" / ", bad);
        });

        yield return ("★★★全部の組が、フッターのどこかの行に居る（画面に出ない組を作らない）", () =>
        {
            var placed = HitWindowOptions.Bars.SelectMany(r => r)
                                              .Where(k => k != HitWindowOptions.ResetKey).ToList();
            var miss = HitWindowOptions.All.Select(o => o.Key)
                                           .Where(k => !placed.Contains(k, StringComparer.Ordinal))
                                           .ToList();
            var dup = placed.GroupBy(k => k, StringComparer.Ordinal)
                            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            var ghost = placed.Where(k => HitWindowOptions.All.All(o => o.Key != k)).ToList();
            if (placed.Count == 0) return "行の表が空（引けていない）";
            return miss.Count == 0 && dup.Count == 0 && ghost.Count == 0 ? null
                : $"画面に出ない組: [{string.Join(",", miss)}] / 2 回置いた組: [{string.Join(",", dup)}]"
                  + $" / 組が無い鍵: [{string.Join(",", ghost)}]";
        });

        yield return ("★否定: 知らない鍵は黙って捨てずに落ちる", () =>
        {
            var vm = new HitWindowViewModel { _suppressSave = true };
            try { vm.SetOption("no_such_option", "x"); }
            catch (KeyNotFoundException) { return null; }
            return "知らない鍵が黙って通った（＝上のガードは組を足しても落ちない）";
        });

        yield return ("★★上端を超える値は捨てず、Set が上端へ収める（丸めを 2 か所に書かない）", () =>
        {
            var vm = new HitWindowViewModel { _suppressSave = true };
            var s = HitWindowOptions.Of("zoom").Slider!.Value;
            vm.SetOptionValue("zoom", s.Max + 10.0);
            return Math.Abs(vm.Zoom - s.Max) < 1e-9 ? null
                : $"上端へ収まらない: {vm.Zoom}（上端 {s.Max}）";
        });

        yield return ("★否定: 旧（数かを見ずに入れる）なら壊れた値で既定が消える", () =>
        {
            static bool LegacyTake(string v) => true;
            if (!LegacyTake("これは数ではない"))
                return "旧が落ちない（写しが間違っている）";
            return !double.TryParse("これは数ではない", NumberStyles.Float,
                                    CultureInfo.InvariantCulture, out _)
                ? null : "新でも数でない値が通る";
        });
    }
}

internal readonly record struct KvRow(string K, string V);

internal sealed partial class LiveKvRow : ObservableObject
{
    public required string K { get; init; }

    [ObservableProperty]
    public partial string V { get; set; }
}

internal sealed class LegendRow
{
    public IBrush? Swatch { get; init; }

    public IBrush? Stroke { get; init; }

    public double StrokeW { get; init; } = 1.4;

    public bool Dashed { get; init; }

    public bool Round { get; init; }

    public bool Bar { get; init; }

    public bool Wide { get; init; }

    public bool Header { get; init; }

    public bool HasSwatch => !Wide && !Header && (Swatch is not null || Stroke is not null);

    public required string Text { get; init; }

    public string Note { get; init; } = "";

    public string Key { get; init; } = "";

    public bool IsBar => HasSwatch && Bar;
    public bool IsRound => HasSwatch && !Bar && Round;
    public bool IsBox => HasSwatch && !Bar && !Round;
    public bool HasNote => Note.Length > 0;

    public Avalonia.Collections.AvaloniaList<double>? Dash =>
        Dashed ? new Avalonia.Collections.AvaloniaList<double>(Views.BoardPalette.ClearRingDash) : null;
}

internal static class LegendMaterialKeys
{
    private static string F(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);
    private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);

    public static string Enemy(string cls, bool hit) => "enemy:" + cls + ":" + (hit ? "hit" : "plain");
    public const string EnemyRanged = "enemy:ranged";
    public const string EnemyGhostActivated = "enemy:ghost:activated";
    public static string Sprite(int n, double halfW, double halfH) =>
        "sprite:" + I(n) + " " + F(halfW) + " " + F(halfH);
    public static string Origin(string? origin, string? cls, int? level, int? sub, string tier) =>
        "origin:" + (origin ?? "-") + "/" + (cls ?? "-") + "/"
        + (level is null ? "-" : I(level.Value)) + "/" + (sub is null ? "-" : I(sub.Value))
        + " " + tier;
    public const string OriginNoRecorded = "origin:norecorded";
    public static string ExArmed(string kind) => "ex:armed:" + kind;
    public const string ExCross = "ex:cross";
    public static string LaserOrigin(string kind, int? sub) =>
        "laserorigin:" + kind + (sub is null ? "" : " " + I(sub.Value));
    public static string Shot(int key, double loW, double hiW, double loH, double hiH) =>
        "shot:" + I(key) + " " + F(loW) + " " + F(hiW) + " " + F(loH) + " " + F(hiH);
    public static string Self(double side) => "self " + F(side);
    public const string InvSpans = "inv:spans";
    public static string Spirit(string shape, bool certain) => "spirit:" + shape + ":" + (certain ? "certain" : "est");
    public static string Ring(string kind) => "ring:" + kind;
    public const string RingEst = "ring:est";
    public const string Blast = "blast";
    public static string Trail(string kind) => "trail:" + kind;
    public const string HitPoint = "hitpoint";
    public static string ExTrail(int n) => "extrail " + I(n);
    public static string HazardCulprit(bool present, bool matched) =>
        "hazardculprit " + (!present ? "none" : matched ? "matched" : "unmatched");
    public static string HazardCulpritBody(string kind) => "hazardculpritbody " + kind;
    public static string NotRecorded(string what) => "recorded:" + what + " 0";
}

internal sealed class LegendScan
{
    public List<int> Sprites { get; } = [];

    public Dictionary<int, (double W, double H)> SpriteHalf { get; } = [];

    public List<(string? Origin, string? Cls, int? Level, int? Sub)> Origins { get; } = [];

    public List<string> EnemyClasses { get; } = [];

    public HashSet<string> EnemyPlain { get; } = new(StringComparer.Ordinal);

    public HashSet<string> EnemyHit { get; } = new(StringComparer.Ordinal);

    public bool EnemyRanged;

    public bool GhostActivated;

    public Dictionary<int, (double LoW, double HiW, double LoH, double HiH)> Shots { get; } = [];

    public HashSet<string> RingKinds { get; } = new(StringComparer.Ordinal);

    public bool RingEst;

    public bool Blast;

    public List<(string Label, int Ticks, int Angles, bool Certain, string ShapeKey)> Spirit { get; } = [];

    public bool ExArmedBoss;
    public bool ExArmedUnknown;
    public bool ExArmedFoe;

    public bool ExArmedCard2;
    public bool ExArmedCard3;

    public bool ExCross;


    public bool ExArmedHit;

    public string? TrailKind;

    public bool HitPointPresent;

    public int ExTrailCount;

    public bool HazardCulpritWanted;

    public bool HazardCulpritPresent;

    public bool HazardCulpritMatched;

    public IReadOnlyList<string> HazardCulpritBodyKinds = [];

    public IReadOnlyList<int> LaserOriginBossSubs = [];

    public bool LaserOriginEx;

    public bool LaserOriginUnknown;

    public int? FoeChar;

    public List<string> Materials { get; } = [];
}

internal sealed partial class OptionPick : ObservableObject
{
    public required string Value { get; init; }
    public required string Text { get; init; }
    public required string StdJa { get; init; }
    public required IRelayCommand ChooseCommand { get; init; }

    [ObservableProperty]
    public partial bool IsOn { get; set; }
}

internal sealed partial class OptionGroupRow : ObservableObject
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public bool HasLabel => Label.Length > 0;
    public required bool IsSlider { get; init; }
    public bool IsPickList => !IsSlider;
    public required List<OptionPick> Picks { get; init; }
    public string DecJa { get; init; } = "";
    public string IncJa { get; init; } = "";
    public double Min { get; init; }
    public double Max { get; init; }
    public double Step { get; init; }

    [ObservableProperty]
    public partial double Value { get; set; }

    [ObservableProperty]
    public partial string NowText { get; set; } = "";

    public IRelayCommand? DecCommand { get; init; }
    public IRelayCommand? IncCommand { get; init; }
    public Action<double>? OnValue { get; init; }

    partial void OnValueChanged(double value) => OnValue?.Invoke(value);
}

internal sealed class OptionBarRow
{
    public required List<OptionGroupRow> Groups { get; init; }
}
