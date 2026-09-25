using System.Globalization;
using System.Runtime.CompilerServices;

namespace TH09.Analysis;

public enum ClearRingSource
{
    Gauge,

    Quick,

    Hit,
}

public readonly record struct ClearRingShape(string Kind, uint Site, double R0, double Dr, int Life)
{
    public int Frames => Life - 1;

    public double? Radius(int frame) => frame < 1 || frame > Frames ? null : R0 + frame * Dr;

    public double Reach => Radius(Frames) ?? R0;
}

public readonly record struct ClearRingOrigin(int Index, int Side, string Kind,
                                              ClearRingSource Source, bool Certain, string? Note);

public sealed record ClearRing
{
    public required string Kind { get; init; }

    public required double X { get; init; }

    public required double Y { get; init; }

    public required double R { get; init; }

    public required int Frame { get; init; }

    public required int Frames { get; init; }

    public required ClearRingSource Source { get; init; }

    public required bool Certain { get; init; }

    public required bool Pre { get; init; }

    public required string? Note { get; init; }

    public required int Origin { get; init; }
}

public static class ClearRings
{

    private static readonly ClearRingShape[] Table = LoadTable();
    private static readonly Dictionary<string, ClearRingShape> ByKind = LoadByKind();
    private static readonly Dictionary<string, string> Ja = LoadJa();

    public static IReadOnlyList<string> Kinds { get; } = Table.Select(s => s.Kind).ToArray();

    public static int KindCount => Table.Length;

    public const string KindHit = "hit";

    public static string LegendJa => AnalysisTables.ClearRingLegendJa;

    public static string SourcesJa => AnalysisTables.ClearRingSourcesJa;

    public static string PreNote => AnalysisTables.RingPreNote;

    private static readonly string[] NoteCatalog =
    [
        AnalysisTables.RingPreNote,
        AnalysisTables.QuickLevelNote,
        AnalysisTables.QuickDowngradeNote,
        AnalysisTables.CardNoBossWordNote,
    ];

    private const string NoteJoin = "／";

    private static string JoinPreNote(string? note) =>
        string.IsNullOrEmpty(note) ? PreNote : PreNote + NoteJoin + note;

    private static ClearRingShape[] LoadTable()
    {
        var outv = new List<ClearRingShape>();
        foreach (var line in Packed.Lines(AnalysisTables.ClearRingKindsPacked))
        {
            var f = line.Split('\t');
            if (f.Length != 5)
                throw new InvalidDataException($"弾消しリングの表の行が 5 列でない: {line}");
            outv.Add(new ClearRingShape(f[0], Site(f[1]), D(f[2]), D(f[3]), I(f[4])));
        }
        return outv.ToArray();
    }

    private static Dictionary<string, ClearRingShape> LoadByKind()
    {
        var map = new Dictionary<string, ClearRingShape>(StringComparer.Ordinal);
        foreach (var s in Table)
        {
            if (!map.TryAdd(s.Kind, s))
                throw new InvalidDataException($"弾消しリングの表に同じ種別が 2 行ある: {s.Kind}");
        }
        return map;
    }

    private static Dictionary<string, string> LoadJa()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in Packed.Lines(AnalysisTables.ClearRingJaPacked))
        {
            var f = line.Split('\t');
            if (f.Length != 2)
                throw new InvalidDataException($"弾消しリングの語の表の行が 2 列でない: {line}");
            map[f[0]] = f[1];
        }
        return map;
    }

    private static uint Site(string s) =>
        uint.Parse(s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? s[2..] : s,
                   NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);
    private static int I(string s) => int.Parse(s, CultureInfo.InvariantCulture);

    public static ClearRingShape? Shape(string? kind) =>
        kind is not null && ByKind.TryGetValue(kind, out var s) ? s : null;

    public static double? Radius(string? kind, int frame) => Shape(kind)?.Radius(frame);

    public static string? KindLabel(string? kind) =>
        kind is not null && Ja.TryGetValue(kind, out var ja) ? ja : null;


    public static List<ClearRing> At(Window w, int i, int side) => Scan.For(w).At(i, side);

    public static IReadOnlyList<ClearRingOrigin> Origins(Window w, int side) => Scan.For(w).Origins(side);


    private const long HitListValid = AnalysisTables.HitlistValid;

    private const int PlayerStateDown = AnalysisTables.PlayerStateDown;

    private const double GaugeDropEps = AnalysisTables.GaugeDropEps;

    private const string SpellWord = "spell_attacks";
    private const string BossWord = "boss_attacks";

    private static IEnumerable<string> WordsRead(int side)
    {
        yield return $"p{side}_gauge";
        yield return $"p{side}_{SpellWord}";
        yield return $"p{side}_{BossWord}";
        yield return $"p{side}_player_state";
        yield return $"p{side}_hit_list_count";
        yield return $"p{side}_pos_x";
        yield return $"p{side}_pos_y";
    }

    private static ClearRingSource RingSourceOf(CardSource s) => s switch
    {
        CardSource.Gauge => ClearRingSource.Gauge,
        CardSource.Quick => ClearRingSource.Quick,
        _ => throw new InvalidOperationException(
                 "スペルポイント由来のカードアタックはリングを作らない（起点の段で落とすこと）"),
    };

    public static string? CardNote(bool zero, CardDrop d)
    {
        if (!d.Certain)
            return zero ? AnalysisTables.QuickLevelNote : AnalysisTables.CardNoBossWordNote;
        return !zero && d.Source == CardSource.Quick ? AnalysisTables.QuickDowngradeNote : null;
    }

    private static string RingKindOf(CardLevel level) => level switch
    {
        CardLevel.C2 => "c2",
        CardLevel.C3 => "c3",
        CardLevel.C4 => "c4",
        _ => throw new InvalidOperationException("知らないカードのレベル: " + level),
    };


    private sealed class Scan
    {
        private static readonly ConditionalWeakTable<Window, Scan> Cached = new();

        public static Scan For(Window w) =>
            Cached.GetValue(w, static x => new Scan(new WindowExt(x)));

        private readonly IExtSource _src;
        private readonly int _n, _preN;
        private readonly Dictionary<int, bool[]> _adv = [];
        private readonly Dictionary<int, int[]> _advCum = [];
        private readonly Dictionary<int, List<ClearRingOrigin>> _bySide = [];
        private List<ClearRingOrigin>? _cards;
        private List<ClearRingOrigin>? _hits;

        private static readonly int[] CenterProbe = [0, -1, 1];

        public Scan(IExtSource src)
        {
            _src = src; _n = src.TickCount; _preN = src.PreCount;
        }

        private int X(int i) => _src.ExtIndex(i);

        private double?[]? Ext(string name) => _src.ExtSeries(name);

        private double? ValueAt(double?[]? col, int i)
        {
            if (col is null) return null;
            int k = X(i);
            return (uint)k < (uint)col.Length ? col[k] : null;
        }

        private bool? Rose(double?[]? col, int i)
        {
            var a = ValueAt(col, i - 1);
            var b = ValueAt(col, i);
            if (a is null || b is null) return null;
            return (long)b.Value > (long)a.Value;
        }

        public bool[] Adv(int side)
        {
            if (_adv.TryGetValue(side, out var got)) return got;
            var col = Ext($"p{side}_hit_list_count");
            var outv = new bool[_preN + _n];
            for (int k = 0; k < outv.Length; k++)
                outv[k] = col is null || col[k] is null || ((long)col[k]!.Value & HitListValid) != 0;
            _adv[side] = outv;
            return outv;
        }

        private int[] AdvCum(int side)
        {
            if (_advCum.TryGetValue(side, out var got)) return got;
            var flags = Adv(side);
            var cum = new int[flags.Length + 1];
            for (int k = 0; k < flags.Length; k++) cum[k + 1] = cum[k] + (flags[k] ? 1 : 0);
            _advCum[side] = cum;
            return cum;
        }

        public int FrameOf(int side, int start, int i) =>
            AdvCum(side)[X(i) + 1] - AdvCum(side)[X(start)];

        private List<ClearRingOrigin> Cards()
        {
            if (_cards is not null) return _cards;
            var outv = new List<ClearRingOrigin>();
            for (int side = 1; side <= 2; side++)
            {
                var g = Ext($"p{side}_gauge");
                if (g is null) continue;
                var sa = Ext($"p{side}_{SpellWord}");
                var ba = Ext($"p{side}_{BossWord}");
                for (int i = -_preN + 1; i < _n; i++)
                {
                    var a = ValueAt(g, i - 1);
                    var b = ValueAt(g, i);
                    if (a is null || b is null || b.Value >= a.Value - GaugeDropEps) continue;
                    var got = QuickCards.OfDrop(a.Value, b.Value, Rose(ba, i), Rose(sa, i) ?? false);
                    if (got is not CardDrop d) continue;
                    bool zero = Math.Abs(b.Value) <= QuickCards.CardZeroTol;
                    outv.Add(new ClearRingOrigin(i, side, RingKindOf(d.Level),
                                                 RingSourceOf(d.Source), d.Certain, CardNote(zero, d)));
                }
            }
            _cards = outv.OrderBy(e => e.Index).ThenBy(e => e.Side).ToList();
            return _cards;
        }

        private List<ClearRingOrigin> Hits()
        {
            if (_hits is not null) return _hits;
            var outv = new List<ClearRingOrigin>();
            for (int side = 1; side <= 2; side++)
            {
                var st = Ext($"p{side}_player_state");
                if (st is null) continue;
                for (int i = -_preN + 1; i < _n; i++)
                {
                    var a = ValueAt(st, i - 1);
                    var b = ValueAt(st, i);
                    if (a is null || b is null) continue;
                    if ((int)a.Value == PlayerStateDown && (int)b.Value != PlayerStateDown)
                        outv.Add(new ClearRingOrigin(i, side, KindHit, ClearRingSource.Hit, true, null));
                }
            }
            _hits = outv.OrderBy(e => e.Index).ThenBy(e => e.Side).ToList();
            return _hits;
        }

        public List<ClearRingOrigin> Origins(int side)
        {
            if (_bySide.TryGetValue(side, out var got)) return got;
            var outv = Cards().Where(e => e.Side == side)
                              .Concat(Hits().Where(e => e.Side == side))
                              .OrderBy(e => e.Index).ToList();
            _bySide[side] = outv;
            return outv;
        }

        private (double X, double Y)? Center(int side, int index)
        {
            var xs = Ext($"p{side}_pos_x");
            var ys = Ext($"p{side}_pos_y");
            foreach (int d in CenterProbe)
            {
                var x = ValueAt(xs, index + d);
                var y = ValueAt(ys, index + d);
                if (x is not null && y is not null) return (x.Value, y.Value);
            }
            return null;
        }

        public List<ClearRing> At(int i, int side)
        {
            var outv = new List<ClearRing>();
            if (i < 0 || i >= _n) return outv;
            foreach (var e in Origins(side))
            {
                if (e.Index > i) continue;
                if (Shape(e.Kind) is not ClearRingShape sh) continue;
                int frame = FrameOf(side, e.Index, i);
                if (sh.Radius(frame) is not double r) continue;
                if (Center(e.Side, e.Index) is not (double cx, double cy)) continue;
                outv.Add(new ClearRing
                {
                    Kind = e.Kind, X = cx, Y = cy, R = r,
                    Frame = frame, Frames = sh.Frames,
                    Source = e.Source, Certain = e.Certain,
                    Note = e.Index < 0 ? JoinPreNote(e.Note) : e.Note,
                    Pre = e.Index < 0, Origin = e.Index,
                });
            }
            return outv;
        }
    }


    private interface IExtSource
    {
        int TickCount { get; }
        int PreCount { get; }
        int ExtIndex(int i);
        double?[]? ExtSeries(string name);
    }

    private sealed class WindowExt : IExtSource
    {
        private readonly Window _w;
        public WindowExt(Window w) => _w = w;
        public int TickCount => _w.TickCount;
        public int PreCount => _w.PreCount;
        public int ExtIndex(int i) => _w.ExtIndex(i);
        public double?[]? ExtSeries(string name) => _w.ExtSeries(name);
    }


    private sealed class FakeExt : IExtSource
    {
        private readonly Dictionary<string, double?[]> _cols = new(StringComparer.Ordinal);
        public int TickCount { get; }
        public int PreCount { get; }
        public FakeExt(int tickCount, int preCount) { TickCount = tickCount; PreCount = preCount; }
        public int ExtIndex(int i) => PreCount + i;
        public double?[]? ExtSeries(string name) => _cols.TryGetValue(name, out var v) ? v : null;

        public FakeExt With(string name, Func<int, double?> f)
        {
            var col = new double?[PreCount + TickCount];
            for (int k = 0; k < col.Length; k++) col[k] = f(k - PreCount);
            _cols[name] = col;
            return this;
        }
    }

    private static Scan Synthetic(int n, int dropAt, double before, double after,
                                  bool valid = true, bool withValidWord = true)
    {
        var src = new FakeExt(n, 0)
            .With("p1_gauge", i => i < dropAt ? before : after)
            .With("p1_spell_attacks", i => i < dropAt ? 0.0 : 1.0)
            .With("p1_boss_attacks", _ => 0.0)
            .With("p1_pos_x", _ => 10.0)
            .With("p1_pos_y", _ => 20.0);
        if (withValidWord)
            src = src.With("p1_hit_list_count", _ => valid ? (double)(HitListValid | 3) : null);
        return new Scan(src);
    }

    private static readonly (double Before, double After, bool? Boss, bool Spell)[] NoteGrid = BuildNoteGrid();

    private static (double Before, double After, bool? Boss, bool Spell)[] BuildNoteGrid()
    {
        double[] befores =
        [
            100.0, 108.47, 150.0, 197.11, 198.26, 199.0, 200.0, 250.0,
            295.0, 299.583, 300.0, 300.327, 350.0, 395.08, 398.41, 400.0, 401.0, 500.0, 600.0,
        ];
        double[] drops = [50.0, 100.0, 200.0, 297.0, 300.0, 303.0, 400.0];
        var outv = new List<(double, double, bool?, bool)>();
        foreach (var before in befores)
        {
            var afters = new List<double> { 0.0 };
            foreach (var d in drops) afters.Add(before - d);
            foreach (var after in afters)
            {
                if (after >= before - GaugeDropEps) continue;
                foreach (bool? boss in new bool?[] { true, false, null })
                    foreach (bool spell in new[] { true, false })
                        outv.Add((before, after, boss, spell));
            }
        }
        return outv.ToArray();
    }

    public static IEnumerable<(string Label, Func<string?> Body)> SelfTestCases()
    {
        yield return ($"★母数（弾消しリングの種別 {KindCount} / 名前 {Ja.Count}）", () =>
        {
            if (KindCount == 0) return "CLEAR_RING_KINDS が 0 行。生成物を引けていない";
            if (Ja.Count == 0) return "CLEAR_RING_JA が 0 行。生成物を引けていない";
            return null;
        });

        yield return ("★形の表と名前の表が同じ種別を持っている（片方だけ増えたら落ちる）", () =>
        {
            var noJa = Kinds.Where(k => KindLabel(k) is null).ToList();
            var noShape = Ja.Keys.Where(k => Shape(k) is null).ToList();
            if (noJa.Count > 0) return "名前が無い種別: " + string.Join(",", noJa);
            if (noShape.Count > 0) return "形が無い種別: " + string.Join(",", noShape);
            return null;
        });

        yield return ("★爆風が混ざっていない（消せるものが違う ＝ 別の出来事）", () =>
        {
            var bad = Ja.Where(kv => kv.Value.Contains("爆風", StringComparison.Ordinal))
                        .Select(kv => kv.Key).ToList();
            return bad.Count == 0 ? null
                 : "爆風がこの表に入っている: " + string.Join(",", bad)
                   + "（カードアタックは種類を問わず消す / 爆風は白弾だけ。ENEMY_BLAST_* 側へ）";
        });

        yield return ("★どの種別も 2 フレーム以上・広がる（ラウンド終了のリングは表に入れない）", () =>
        {
            foreach (var s in Table)
            {
                if (s.Frames < 1)
                    return $"{s.Kind} は Frames={s.Frames}（1 度も描かれない。表の意味が変わった可能性）";
                if (s.Dr <= 0)
                    return $"{s.Kind} は Dr={s.Dr}（広がらないリング。ラウンド終了の演出が混ざっていないか）";
            }
            return null;
        });

        yield return ("★カードのレベルも被弾も、表の鍵に着地する", () =>
        {
            foreach (var lv in new[] { CardLevel.C2, CardLevel.C3, CardLevel.C4 })
                if (Shape(RingKindOf(lv)) is null) return $"{lv} の鍵が表に無い";
            return Shape(KindHit) is null ? "hit の鍵が表に無い" : null;
        });

        yield return ("★凡例の文言が空でない（1 行に畳んだ見出しと「何で出るか」）", () =>
            !string.IsNullOrEmpty(LegendJa) && !string.IsNullOrEmpty(SourcesJa)
                ? null : "空。凡例に出せない");

        yield return ("★読む語が RECORD_FIELDS に在る（綴りを人の目で守らない）", () =>
        {
            var known = new HashSet<string>(TimelineDecode.RecordFields, StringComparer.Ordinal);
            var miss = new[] { 1, 2 }.SelectMany(WordsRead).Where(x => !known.Contains(x)).ToList();
            return miss.Count == 0 ? null : "主リングに無い語を読もうとしている: " + string.Join(",", miss);
        });

        yield return ("★表に無い種別は null（半径 0 を返さない）", () =>
        {
            if (Shape("no_such_ring") is not null) return "形が返った";
            if (Radius("no_such_ring", 1) is not null) return "半径が返った";
            if (KindLabel("no_such_ring") is not null) return "名前が返った";
            return Shape(null) is null && Radius(null, 1) is null ? null : "null で落ちるか値が返る";
        });

        yield return ("★frame は 1 起点（生成した frame の r0 は誰にも使われない）", () =>
        {
            foreach (var s in Table)
            {
                if (s.Radius(0) is not null) return $"{s.Kind}: frame 0 に半径が出た";
                if (s.Radius(-1) is not null) return $"{s.Kind}: 負の frame に半径が出た";
                if (s.Radius(1) != s.R0 + s.Dr) return $"{s.Kind}: 1 枚目が r0 + dr でない";
            }
            return null;
        });

        yield return ("★最後の 1 枚を越えたら null（life ではなく life − 1 で切る）", () =>
        {
            foreach (var s in Table)
            {
                int last = s.Life - 1;
                if (s.Radius(last) is null) return $"{s.Kind}: 最後の 1 枚（life − 1）が描けない";
                if (s.Radius(s.Life) is not null) return $"{s.Kind}: life ぶん描いている（1 枚長い）";
                if (s.Radius(last) != s.Reach) return $"{s.Kind}: 到達半径が最後の 1 枚と食い違う";
                if (s.Frames != last) return $"{s.Kind}: Frames={s.Frames}（life − 1 ＝ {last} のはず）";
            }
            return null;
        });

        yield return ("★否定: 旧（life ぶん描く）なら最後の 1 枚が余分に出る", () =>
        {
            static double? Legacy(ClearRingShape s, int f) =>
                f < 1 || f > s.Life ? null : s.R0 + f * s.Dr;
            foreach (var s in Table)
            {
                if (Legacy(s, s.Life) is null)
                    return $"{s.Kind}: 旧の写しが間違っている（否定テストの意味が無い）";
                if (s.Radius(s.Life) is not null)
                    return $"{s.Kind}: 新も life ぶん描いている"
                           + "（0x41C95E で枠が返るフレームの半径は当たり判定に一度も使われない）";
            }
            return null;
        });

        yield return ("★否定: 旧（frame を 0 起点で数える）なら半径が 1 段小さい", () =>
        {
            if (Shape(KindHit) is not ClearRingShape s) return "hit が表に無い";
            double Legacy(int f) => s.R0 + (f - 1) * s.Dr;
            if (Legacy(1) != s.R0) return "旧の写しが間違っている（否定テストの意味が無い）";
            if (s.Radius(1) != s.R0 + s.Dr) return "新が r0 + 1*dr になっていない";
            return Legacy(1) != s.Radius(1) ? null : "旧と新が同じ値（dr が 0 の種別を見ている）";
        });

        yield return ("★否定: 旧（範囲外に 0 を返す）なら『描かない』が『半径 0 で描く』になる", () =>
        {
            double Legacy(string kind, int f) => Radius(kind, f) ?? 0.0;
            if (Legacy(KindHit, 9999) != 0.0 || Legacy("no_such_ring", 1) != 0.0)
                return "旧の写しが間違っている（否定テストの意味が無い）";
            int legacyDrawn = new[] { (KindHit, 9999), ("no_such_ring", 1) }
                              .Count(t => Legacy(t.Item1, t.Item2) >= 0.0);
            int drawn = new[] { (KindHit, 9999), ("no_such_ring", 1) }
                        .Count(t => Radius(t.Item1, t.Item2) is not null);
            if (legacyDrawn != 2) return "旧が 2 個描かない（写しが違う）";
            return drawn == 0 ? null : $"新が {drawn} 個描いている";
        });

        yield return ("★合成: クイックの C2 が 1 枚目から到達半径まで伸びて、その次で消える", () =>
        {
            if (Shape("c2") is not ClearRingShape c2) return "c2 が表に無い";
            int frames = c2.Life - 1;
            double reach = c2.R0 + frames * c2.Dr;
            int start = 3;
            var sc = Synthetic(n: start + frames + 3, dropAt: start, before: 250.0, after: 0.0);

            var org = sc.Origins(1);
            if (org.Count != 1) return $"起点が {org.Count} 件（1 件のはず）";
            if (org[0].Index != start) return $"起点が {org[0].Index}（{start} のはず）";
            if (org[0].Kind != "c2") return "種別が c2 でない: " + org[0].Kind;
            if (org[0].Source != ClearRingSource.Quick) return "クイックになっていない";

            var at = sc.At(start, 1);
            if (at.Count != 1) return $"発動 tick に {at.Count} 個（1 個のはず）";
            if (at[0].Frame != 1) return $"発動 tick の frame が {at[0].Frame}（1 のはず）";
            if (at[0].R != c2.R0 + c2.Dr) return "発動 tick の半径が r0 + dr でない";
            if (at[0].X != 10.0 || at[0].Y != 20.0) return "中心が自機位置でない";
            if (at[0].Pre) return "窓の中の発動が pre になっている";

            var last = sc.At(start + frames - 1, 1);
            if (last.Count != 1) return "最後の 1 枚が出ていない";
            if (last[0].Frame != frames) return $"最後の frame が {last[0].Frame}（{frames} のはず）";
            if (last[0].R != reach) return $"最後の半径が {last[0].R}（{reach} のはず）";
            if (last[0].Frames != frames) return $"Frames が {last[0].Frames}（{frames} のはず）";
            return sc.At(start + frames, 1).Count == 0 ? null : "life ぶん描いている（1 枚長い）";
        });

        yield return ("★合成: リングは撃った側の陣にしか出ない", () =>
        {
            var sc = Synthetic(n: 20, dropAt: 3, before: 250.0, after: 0.0);
            if (sc.At(5, 1).Count != 1) return "1P に出ていない";
            return sc.At(5, 2).Count == 0 ? null : "2P の陣に漏れている";
        });

        yield return ("★合成: 窓の頭より前の発動も出る（Pre / Origin が負）", () =>
        {
            var sc = new Scan(new FakeExt(6, 5)
                .With("p1_gauge", i => i < -2 ? 250.0 : 0.0)
                .With("p1_pos_x", i => i < 0 ? -7.0 : 0.0)
                .With("p1_pos_y", i => i < 0 ? 99.0 : 0.0)
                .With("p1_hit_list_count", _ => HitListValid));

            var org = sc.Origins(1);
            if (org.Count != 1) return $"起点が {org.Count} 件（1 件のはず）";
            if (org[0].Index != -2) return $"起点が {org[0].Index}（−2 のはず）";

            var at = sc.At(0, 1);
            if (at.Count != 1) return $"窓の頭に {at.Count} 個（1 個のはず）";
            if (!at[0].Pre) return "Pre が立っていない（シークバーに点が無いことを画面が言えない）";
            if (at[0].Origin != -2) return $"Origin が {at[0].Origin}";
            if (at[0].Frame != 3) return $"frame が {at[0].Frame}（−2..0 の 3 のはず）";
            return at[0].X == -7.0 && at[0].Y == 99.0 ? null : "中心が生成 tick の位置でない";
        });

        yield return ("★合成: スペルポイント由来（ゲージが減らない発動）はリングを 1 個も作らない", () =>
        {
            var sc = new Scan(new FakeExt(20, 0)
                .With("p1_gauge", _ => 500.0)
                .With("p1_spell_attacks", i => i < 5 ? 0.0 : 1.0)
                .With("p1_pos_x", _ => 0.0)
                .With("p1_pos_y", _ => 0.0)
                .With("p1_hit_list_count", _ => HitListValid));
            if (sc.Origins(1).Count != 0) return "起点が出ている";
            return sc.At(10, 1).Count == 0 ? null : "盤面に円が出ている";
        });

        yield return ("★合成: 被弾リングは『やられを抜けた tick』（被弾 tick + 1 ではない）", () =>
        {
            const int enter = 2, leave = 7;
            var sc = new Scan(new FakeExt(20, 0)
                .With("p1_player_state", i => i >= enter && i < leave ? PlayerStateDown : 0.0)
                .With("p1_pos_x", _ => 0.0)
                .With("p1_pos_y", _ => 0.0)
                .With("p1_hit_list_count", _ => HitListValid));

            var org = sc.Origins(1);
            if (org.Count != 1) return $"起点が {org.Count} 件（1 件のはず）";
            if (org[0].Source != ClearRingSource.Hit) return "被弾になっていない";
            if (org[0].Note is not null) return "被弾の起点に断りが付いている: " + org[0].Note;
            var drawn = sc.At(leave, 1);
            if (drawn.Count != 1) return $"被弾の tick に {drawn.Count} 個";
            if (drawn[0].Note is not null) return "被弾の円に断りが付いている: " + drawn[0].Note;
            int legacy = enter + 1;
            if (org[0].Index == legacy) return "旧（被弾 tick + 1）と同じ位置に出ている";
            return org[0].Index == leave ? null : $"起点が {org[0].Index}（{leave} のはず）";
        });

        yield return ("★否定: 旧（VALID の欠測を『進んでいない』に倒す）ならリングが伸びない", () =>
        {
            var sc = Synthetic(n: 20, dropAt: 3, before: 250.0, after: 0.0, valid: false);
            bool Legacy(double? v) => v is not null && ((long)v.Value & HitListValid) != 0;
            if (Legacy(null)) return "旧の写しが間違っている（否定テストの意味が無い）";
            var at = sc.At(6, 1);
            if (at.Count != 1) return "欠測の tick でリングが消えている（欠測を止まったと読んでいる）";
            return at[0].Frame == 4 ? null : $"frame が {at[0].Frame}（3..6 の 4 のはず）";
        });

        yield return ("★合成: VALID の列が無い窓（v10 以前）は毎 tick 進める", () =>
        {
            var sc = Synthetic(n: 20, dropAt: 3, before: 250.0, after: 0.0, withValidWord: false);
            var flags = sc.Adv(1);
            if (flags.Any(x => !x)) return "止まっている tick がある（止まった証拠が無いのに止めている）";
            var at = sc.At(6, 1);
            return at.Count == 1 && at[0].Frame == 4 ? null : "リングが伸びない";
        });

        yield return ("★累積で数えた frame が、素朴に数えたものと 1 件も違わない", () =>
        {
            var sc = new Scan(new FakeExt(40, 0)
                .With("p1_gauge", i => i < 4 ? 250.0 : 0.0)
                .With("p1_pos_x", _ => 0.0)
                .With("p1_pos_y", _ => 0.0)
                .With("p1_hit_list_count", i => i % 3 == 0 ? HitListValid : 0.0));
            var flags = sc.Adv(1);
            int n = 0;
            for (int start = 0; start < 40; start++)
                for (int i = start; i < 40; i++)
                {
                    int naive = 0;
                    for (int j = start; j <= i; j++) if (flags[j]) naive++;
                    if (sc.FrameOf(1, start, i) != naive)
                        return $"start={start} i={i} で {sc.FrameOf(1, start, i)} ≠ {naive}";
                    n++;
                }
            return n > 0 ? null : "1 組も突き合わせていない（母数 0）";
        });

        yield return ("★窓の外の tick を渡しても落ちない（空を返す）", () =>
        {
            var sc = Synthetic(n: 20, dropAt: 3, before: 250.0, after: 0.0);
            return sc.At(-1, 1).Count == 0 && sc.At(20, 1).Count == 0 && sc.At(999, 1).Count == 0
                ? null : "窓の外に円が出た";
        });

        yield return ("★語が 1 つも無い窓でも落ちない（0 件を返す）", () =>
        {
            var sc = new Scan(new FakeExt(10, 0));
            return sc.Origins(1).Count == 0 && sc.At(5, 1).Count == 0 && sc.At(5, 2).Count == 0
                ? null : "起点か円が出た";
        });

        yield return ("★中心が読めない発動は描かない（0,0 に倒さない）", () =>
        {
            var sc = new Scan(new FakeExt(20, 0)
                .With("p1_gauge", i => i < 3 ? 250.0 : 0.0)
                .With("p1_hit_list_count", _ => HitListValid));
            if (sc.Origins(1).Count != 1) return "起点が出ていない（中心と起点を混ぜている）";
            return sc.At(5, 1).Count == 0 ? null : "中心が読めないのに円を描いた";
        });

        yield return ($"★母数（断り書き {NoteCatalog.Length} 本）", () =>
        {
            if (NoteCatalog.Length == 0) return "0 本。生成物を引けていない";
            var empty = NoteCatalog.Where(string.IsNullOrEmpty).Count();
            if (empty > 0) return $"{NoteCatalog.Length} 本のうち {empty} 本が空。生成物を引けていない";
            var dup = NoteCatalog.Length - NoteCatalog.Distinct(StringComparer.Ordinal).Count();
            return dup == 0 ? null : $"同じ字面が {dup} 本ある";
        });

        yield return ($"★格子 {NoteGrid.Length} 通り: 確度が落ちた回に断りが無い、を作らせない", () =>
        {
            int rings = 0;
            foreach (var (before, after, boss, spell) in NoteGrid)
            {
                if (QuickCards.OfDrop(before, after, boss, spell) is not CardDrop d) continue;
                if (Shape(RingKindOf(d.Level)) is null) continue;
                rings++;
                bool zero = Math.Abs(after) <= QuickCards.CardZeroTol;
                var note = CardNote(zero, d);
                if (!d.Certain && string.IsNullOrEmpty(note))
                    return $"before={before} after={after} boss={boss} spell={spell}"
                           + " が Certain=false なのに断りが空（推定の理由が画面から消える）";
            }
            return rings > 0 ? null : $"格子 {NoteGrid.Length} 通りで 1 件もリングにならなかった（母数 0）";
        });

        yield return ($"★格子 {NoteGrid.Length} 通り: カードの断り 3 本が全部 1 回以上出る（死んだ枝が無い）", () =>
        {
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (before, after, boss, spell) in NoteGrid)
            {
                if (QuickCards.OfDrop(before, after, boss, spell) is not CardDrop d) continue;
                bool zero = Math.Abs(after) <= QuickCards.CardZeroTol;
                if (CardNote(zero, d) is string note)
                    seen[note] = seen.TryGetValue(note, out var c) ? c + 1 : 1;
            }
            var want = NoteCatalog.Where(x => x != PreNote).ToList();
            var missing = want.Where(x => !seen.ContainsKey(x)).ToList();
            if (missing.Count > 0)
                return $"{want.Count} 本のうち {missing.Count} 本が 1 度も出ない"
                       + "（CardNote の枝が QuickCards.OfDrop と対になっていない）";
            return null;
        });

        yield return ("★合成: 窓の頭より前 ＋ レベルが推定 のとき、断りが 2 本とも残る", () =>
        {
            var sc = new Scan(new FakeExt(6, 5)
                .With("p1_gauge", i => i < -2 ? 300.0 : 0.0)
                .With("p1_pos_x", _ => 0.0)
                .With("p1_pos_y", _ => 0.0)
                .With("p1_hit_list_count", _ => HitListValid));
            var at = sc.At(0, 1);
            if (at.Count != 1) return $"{at.Count} 個（1 個のはず）";
            var r = at[0];
            if (!r.Pre) return "Pre が立っていない（合成が効いていない）";
            if (r.Certain) return "レベルが推定になっていない（合成が効いていない）";
            if (r.Note is null) return "断りが空（Certain=false なのに理由が無い）";
            if (!r.Note.Contains(PreNote, StringComparison.Ordinal))
                return "窓の頭より前の断りが無い";
            if (!r.Note.Contains(AnalysisTables.QuickLevelNote, StringComparison.Ordinal))
                return "推定の理由が消えている";
            return null;
        });

        yield return ("★否定: 旧（窓の頭より前の断りで差し替える）なら推定の理由が消える", () =>
        {
            static string? Legacy(string? note) => PreNote;
            var legacy = Legacy(AnalysisTables.QuickLevelNote);
            if (legacy is not null && legacy.Contains(AnalysisTables.QuickLevelNote, StringComparison.Ordinal))
                return "旧の写しが間違っている（差し替えになっていない。否定テストの意味が無い）";

            var got = JoinPreNote(AnalysisTables.QuickLevelNote);
            if (!got.Contains(PreNote, StringComparison.Ordinal)) return "新が窓の頭の断りを落とした";
            if (!got.Contains(AnalysisTables.QuickLevelNote, StringComparison.Ordinal))
                return "新も推定の理由を消している（差し替えになっている）";
            return JoinPreNote(null) == PreNote ? null : "理由が無いのに区切りが付いている";
        });

        yield return ("★合成: 全部の円で「Certain=false なのに断りが空」が 0 件（組み上がった側）", () =>
        {
            var cases = new[]
            {
                Synthetic(n: 60, dropAt: 3, before: 250.0, after: 0.0),
                Synthetic(n: 60, dropAt: 3, before: 300.0, after: 0.0),
                Synthetic(n: 60, dropAt: 3, before: 400.0, after: 100.0),
                new Scan(new FakeExt(6, 5)
                    .With("p1_gauge", i => i < -2 ? 300.0 : 0.0)
                    .With("p1_pos_x", _ => 0.0).With("p1_pos_y", _ => 0.0)
                    .With("p1_hit_list_count", _ => HitListValid)),
            };
            int n = 0;
            foreach (var sc in cases)
                for (int i = 0; i < 60; i++)
                    for (int side = 1; side <= 2; side++)
                        foreach (var r in sc.At(i, side))
                        {
                            n++;
                            if (!r.Certain && string.IsNullOrEmpty(r.Note))
                                return $"Certain=false なのに断りが空: {r.Kind} origin={r.Origin}";
                            if (r.Pre && (r.Note is null || !r.Note.Contains(PreNote, StringComparison.Ordinal)))
                                return $"Pre なのに窓の頭の断りが無い: origin={r.Origin}";
                        }
            return n > 0 ? null : "1 個も円が出なかった（母数 0）";
        });
    }
}
