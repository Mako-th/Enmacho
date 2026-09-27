using System.Runtime.CompilerServices;

namespace TH09.Analysis;


public enum BossSource
{
    Boss,

    Reversal,
}

public readonly record struct CardAttackEvent(int Index, int Side, CardSource Source,
                                              CardLevel Level, bool Certain);

public readonly record struct BossAttackEvent(int Index, int Side, BossSource Source, int? Level);

public readonly record struct CutinName(string Text, int? Side, string? Source);

public interface IExtSource
{
    int TickCount { get; }

    int PreCount { get; }

    int ExtIndex(int i);

    double?[]? ExtSeries(string name);
}

public sealed class WindowExt : IExtSource
{
    private readonly Window _w;
    public WindowExt(Window w) => _w = w;
    public int TickCount => _w.TickCount;
    public int PreCount => _w.PreCount;
    public int ExtIndex(int i) => _w.ExtIndex(i);
    public double?[]? ExtSeries(string name) => _w.ExtSeries(name);
}

public sealed class CardEvents
{


    private static readonly Dictionary<string, string> LevelJa = Table(AnalysisTables.CardLevelShortJaPacked);

    private static readonly Dictionary<string, string> SourceNoteJa = Table(AnalysisTables.CardSourceNoteJaPacked);

    private static readonly Dictionary<string, string> SourceLongJa = Table(AnalysisTables.CardSourceJaPacked);

    private static readonly Dictionary<string, string> BossShortJa = Table(AnalysisTables.BossShortJaPacked);

    private static readonly Dictionary<int, string> BossLevelJa = IntTable(AnalysisTables.BossLevelJaPacked);

    private static readonly int[] BossLevelBySp =
        Packed.Lines(AnalysisTables.BossLevelBySpPacked).Select(int.Parse).ToArray();

    private static Dictionary<string, string> Table(string packed)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in Packed.Lines(packed))
        {
            int at = line.IndexOf('\t');
            if (at < 0) throw new InvalidDataException("表の行に区切りが無い: " + line);
            map[line[..at]] = line[(at + 1)..];
        }
        return map;
    }

    private static Dictionary<int, string> IntTable(string packed)
    {
        var map = new Dictionary<int, string>();
        foreach (var (k, v) in Table(packed)) map[int.Parse(k)] = v;
        return map;
    }


    private static readonly Dictionary<CardSource, string> CardSourceKey = new()
    {
        [CardSource.Gauge] = "gauge",
        [CardSource.Quick] = "quick",
        [CardSource.Spell] = "spell",
    };

    private static readonly Dictionary<CardLevel, string> CardLevelKey = new()
    {
        [CardLevel.C2] = "c2",
        [CardLevel.C3] = "c3",
        [CardLevel.C4] = "c4",
    };

    private static readonly Dictionary<BossSource, string> BossSourceKey = new()
    {
        [BossSource.Boss] = "boss",
        [BossSource.Reversal] = "reversal",
    };

    public static string SourceKey(CardSource s) => CardSourceKey[s];

    public static string SourceKey(BossSource s) => BossSourceKey[s];

    public static string? SourceLongName(CardSource s) =>
        CardSourceKey.TryGetValue(s, out var key) && SourceLongJa.TryGetValue(key, out var ja) ? ja : null;

    public static string SpellNote => AnalysisTables.CardSpellNote;


    public static string? SideLabel(int? side, string? text)
    {
        if (side is null || text is null) return text;
        return Fill(AnalysisTables.SideLabelFmt, side.Value.ToString(), text);
    }

    public static string NoteLabel(string name, string? note) =>
        string.IsNullOrEmpty(note) ? name : Fill(AnalysisTables.NoteLabelFmt, name, note);

    private static string Fill(string fmt, params string[] args)
    {
        var outv = new System.Text.StringBuilder(fmt.Length + 16);
        int at = 0, used = 0;
        while (at < fmt.Length)
        {
            char c = fmt[at];
            if (c == '%' && at + 1 < fmt.Length && (fmt[at + 1] == 'd' || fmt[at + 1] == 's'))
            {
                if (used >= args.Length)
                    throw new InvalidOperationException("書式の差し込み口が引数より多い: " + fmt);
                outv.Append(args[used++]);
                at += 2;
                continue;
            }
            outv.Append(c);
            at++;
        }
        if (used != args.Length)
            throw new InvalidOperationException("書式の差し込み口が引数より少ない: " + fmt);
        return outv.ToString();
    }

    public static string CardLevelName(CardLevel level) =>
        CardLevelKey.TryGetValue(level, out var key) && LevelJa.TryGetValue(key, out var ja) ? ja : "?";

    private static string CardNoteLabel(string name, CardSource source, IReadOnlyList<string>? extra)
    {
        var notes = new List<string>();
        var head = SourceNoteJa[CardSourceKey[source]];
        if (head.Length > 0) notes.Add(head);
        if (extra is not null)
            foreach (var s in extra)
                if (!string.IsNullOrEmpty(s)) notes.Add(s);
        return NoteLabel(name, string.Join(AnalysisTables.CardNoteJoin, notes));
    }

    public static string CardLabel(CardSource source, CardLevel level) =>
        CardNoteLabel(CardLevelName(level), source, null);

    public static string BossLabel(BossSource source, int? level)
    {
        var name = BossShortJa[BossSourceKey[source]];
        return NoteLabel(name, level is null ? null : BossLevelJa[level.Value]);
    }


    private static readonly ConditionalWeakTable<Window, CardEvents> Cached = new();

    public static CardEvents For(Window w) =>
        Cached.GetValue(w, static x => new CardEvents(new WindowExt(x)));

    public static CardEvents Of(IExtSource src) => new(src);

    private readonly IExtSource _src;
    private readonly int _n, _preN;
    private List<CardAttackEvent>? _cards;
    private List<BossAttackEvent>? _bosses;

    private CardEvents(IExtSource src)
    {
        _src = src; _n = src.TickCount; _preN = src.PreCount;
    }

    private double?[]? Ext(string name) => _src.ExtSeries(name);

    public double? ExtAt(string name, int i) => ValueAt(Ext(name), i);

    private double? ValueAt(double?[]? col, int i)
    {
        if (col is null) return null;
        int k = _src.ExtIndex(i);
        return (uint)k < (uint)col.Length ? col[k] : null;
    }

    private bool? Rose(double?[]? col, int i)
    {
        var a = ValueAt(col, i - 1);
        var b = ValueAt(col, i);
        if (a is null || b is null) return null;
        return (long)b.Value > (long)a.Value;
    }

    public IReadOnlyList<CardAttackEvent> Cards()
    {
        if (_cards is not null) return _cards;
        var outv = new List<CardAttackEvent>();
        for (int side = 1; side <= 2; side++)
        {
            var g = Ext($"p{side}_gauge");
            if (g is null) continue;
            var sa = Ext($"p{side}_spell_attacks");
            var ba = Ext($"p{side}_boss_attacks");
            for (int i = -_preN + 1; i < _n; i++)
            {
                var a = ValueAt(g, i - 1);
                var b = ValueAt(g, i);
                bool dropped = a is not null && b is not null
                               && b.Value < a.Value - AnalysisTables.GaugeDropEps;
                if (dropped)
                {
                    if (QuickCards.OfDrop(a!.Value, b!.Value, Rose(ba, i), Rose(sa, i) ?? false)
                        is CardDrop d)
                        outv.Add(new CardAttackEvent(i, side, d.Source, d.Level, d.Certain));
                    continue;
                }
                var p = ValueAt(sa, i - 1);
                var q = ValueAt(sa, i);
                if (p is null || q is null) continue;
                if ((long)q.Value > (long)p.Value)
                    outv.Add(new CardAttackEvent(i, side, CardSource.Spell, CardLevel.C3, true));
            }
        }
        _cards = outv.OrderBy(e => e.Index).ThenBy(e => e.Side).ToList();
        return _cards;
    }

    public IReadOnlyList<BossAttackEvent> Bosses()
    {
        if (_bosses is not null) return _bosses;
        var outv = new List<BossAttackEvent>();
        for (int side = 1; side <= 2; side++)
        {
            var ba = Ext($"p{side}_boss_attacks");
            if (ba is null) continue;
            var br = Ext($"p{side}_boss_reversals");
            var sp = Ext($"p{side}_spell_points");
            for (int i = -_preN + 1; i < _n; i++)
            {
                var a = ValueAt(ba, i - 1);
                var b = ValueAt(ba, i);
                if (a is null || b is null || (long)b.Value <= (long)a.Value) continue;
                var p = ValueAt(br, i - 1);
                var q = ValueAt(br, i);
                bool rev = p is not null && q is not null && (long)q.Value > (long)p.Value;
                var s0 = ValueAt(sp, i - 1);
                var s1 = ValueAt(sp, i);
                int? level = null;
                if (s0 is not null && s1 is not null)
                {
                    long lo = (long)s0.Value, hi = (long)s1.Value;
                    foreach (var t in BossLevelBySp)
                        if (lo < t && t <= hi) { level = t; break; }
                }
                outv.Add(new BossAttackEvent(i, side, rev ? BossSource.Reversal : BossSource.Boss,
                                             level));
            }
        }
        _bosses = outv.OrderBy(e => e.Index).ThenBy(e => e.Side).ToList();
        return _bosses;
    }


    public CardAttackEvent? CutinCard(int start)
    {
        CardAttackEvent? got = null;
        foreach (var e in Cards())
        {
            if (Math.Abs(e.Index - start) > AnalysisTables.CutinCardTol) continue;
            if (got is not null) return null;
            got = e;
        }
        return got;
    }

    public BossAttackEvent? CutinBoss(int start)
    {
        BossAttackEvent? got = null;
        foreach (var e in Bosses())
        {
            if (Math.Abs(e.Index - start) > AnalysisTables.CutinCardTol) continue;
            if (got is not null) return null;
            got = e;
        }
        return got;
    }

    public string CardFullName(CardAttackEvent e, IReadOnlyList<string>? extra = null)
    {
        var name = CardLevelName(e.Level);
        var boss = e.Source == CardSource.Spell ? CutinBoss(e.Index) : null;
        if (boss is BossAttackEvent b && b.Side == e.Side)
            name = name + AnalysisTables.LabelJoin + BossShortJa[BossSourceKey[b.Source]];
        return CardNoteLabel(name, e.Source, extra);
    }

    public CutinName CutinParts(int start)
    {
        if (CutinCard(start) is CardAttackEvent c)
            return new CutinName(SideLabel(c.Side, CardFullName(c))!, c.Side, SourceKey(c.Source));
        if (CutinBoss(start) is BossAttackEvent b)
            return new CutinName(SideLabel(b.Side, BossLabel(b.Source, b.Level))!,
                                 b.Side, SourceKey(b.Source));
        return new CutinName(FreezeSpans.Label(FreezeSpans.KindCutin), null, null);
    }

    public string CutinLabel(int start) => CutinParts(start).Text;

    public bool CutinNamedByPoint(int start) =>
        CutinCard(start) is CardAttackEvent e && e.Index >= 0;


    public static IEnumerable<(string Label, Func<string?> Body)> SelfTestCases()
    {
        yield return ("カードのレベルの表が 3 件ある（母数）", () =>
            LevelJa.Count == 3 ? null : $"{LevelJa.Count} 件（表を引けていない）");
        yield return ("カードの源の注釈が 3 件ある（母数）", () =>
            SourceNoteJa.Count == 3 ? null : $"{SourceNoteJa.Count} 件");
        yield return ("カードの源の長い名前（凡例向け）が 3 件ある（母数）", () =>
            SourceLongJa.Count == 3 ? null : $"{SourceLongJa.Count} 件（表を引けていない）");
        yield return ("ボスの短い名前が 2 件ある（母数）", () =>
            BossShortJa.Count == 2 ? null : $"{BossShortJa.Count} 件");
        yield return ("ボスのしきい値が 3 件ある（母数・★並び順が意味を持つ）", () =>
            BossLevelBySp.Length == 3 && BossLevelBySp[0] > BossLevelBySp[^1]
                ? null : "しきい値: " + string.Join(",", BossLevelBySp));
        yield return ("★列挙の鍵が全部その表にある（列挙と表が割れたら落ちる）", () =>
        {
            var gone = new List<string>();
            foreach (var (k, v) in CardLevelKey) if (!LevelJa.ContainsKey(v)) gone.Add($"level:{k}");
            foreach (var (k, v) in CardSourceKey) if (!SourceNoteJa.ContainsKey(v)) gone.Add($"source:{k}");
            foreach (var (k, v) in CardSourceKey) if (!SourceLongJa.ContainsKey(v)) gone.Add($"sourcelong:{k}");
            foreach (var (k, v) in BossSourceKey) if (!BossShortJa.ContainsKey(v)) gone.Add($"boss:{k}");
            foreach (var t in BossLevelBySp) if (!BossLevelJa.ContainsKey(t)) gone.Add($"sp:{t}");
            return gone.Count == 0 ? null : "表に無い鍵: " + string.Join(",", gone);
        });

        yield return ("カードの名前: 1P C2 / 2P C3(Quick) / 1P C3(50万)", () =>
        {
            var a = SideLabel(1, CardLabel(CardSource.Gauge, CardLevel.C2));
            var b = SideLabel(2, CardLabel(CardSource.Quick, CardLevel.C3));
            var c = SideLabel(1, CardLabel(CardSource.Spell, CardLevel.C3));
            if (a != "1P C2") return "gauge: " + a;
            if (b != "2P C3(Quick)") return "quick: " + b;
            return c == "1P C3(50万)" ? null : "spell: " + c;
        });
        yield return ("ボスの名前: 1P Boss(10万) / 2P Rev(30万) / しきい値なしは Rev だけ", () =>
        {
            var a = SideLabel(1, BossLabel(BossSource.Boss, 100000));
            var b = SideLabel(2, BossLabel(BossSource.Reversal, 300000));
            var c = BossLabel(BossSource.Reversal, null);
            if (a != "1P Boss(10万)") return "boss: " + a;
            if (b != "2P Rev(30万)") return "rev: " + b;
            return c == "Rev" ? null : "しきい値なし: " + c;
        });
        yield return ("★側が読めなければ側を付けない（黙って 1P にしない）", () =>
            SideLabel(null, "C2") == "C2" ? null : SideLabel(null, "C2"));
        yield return ("カードの源の長い名前: カードアタック / クイックカードアタック / 50万C3", () =>
        {
            var g = SourceLongName(CardSource.Gauge);
            var q = SourceLongName(CardSource.Quick);
            var s = SourceLongName(CardSource.Spell);
            if (g != "カードアタック") return "gauge: " + g;
            if (q != "クイックカードアタック") return "quick: " + q;
            return s == "50万C3" ? null : "spell: " + s;
        });
        yield return ("スペルポイント由来の断り文言が空でない", () =>
            string.IsNullOrEmpty(SpellNote) ? "空（表を引けていない）" : null);
        yield return ("★★否定: C4 が無い表（`CardLevelJaPacked`）で代用した旧なら、C4 が名前を失う", () =>
        {
            var legacy = Table(AnalysisTables.CardLevelJaPacked);
            string LegacyName(CardLevel lv) =>
                legacy.TryGetValue(((int)lv + 2).ToString(), out var s) ? s : "?";
            if (LegacyName(CardLevel.C2) != "C2" || LegacyName(CardLevel.C3) != "C3")
                return "旧の写しが間違っている（C2 / C3 すら引けていない）";
            if (LegacyName(CardLevel.C4) != "?")
                return "旧が落ちない（★あちらの表に C4 が入った ＝ 表が変わった）";
            var now = CardLevelName(CardLevel.C4);
            return now == "C4" ? null : "新も C4 の名前を失っている: " + now;
        });
        yield return ("★否定: 書式を素通しする旧なら、画面に「%dP」と出る", () =>
        {
            var raw = AnalysisTables.SideLabelFmt;
            if (!raw.Contains("%d", StringComparison.Ordinal))
                return "旧が落ちない（書式に差し込み口が無い ＝ 表が変わった）";
            var now = SideLabel(1, "C2")!;
            return now.Contains('%') ? "新も素通ししている: " + now : null;
        });
        yield return ("★否定: 入れた文字列の中の差し込み口を読み直さない", () =>
        {
            var got = NoteLabel("%s", "50万");
            return got == "%s(50万)" ? null : "差し込み口を読み直している: " + got;
        });
        yield return ("★差し込み口の数が合わなければ落ちる（黙って余らせない）", () =>
        {
            try { Fill("%s", "a", "b"); }
            catch (InvalidOperationException) { return null; }
            return "落ちなかった";
        });

        yield return ("★起点: ゲージ減は撃った側に出る（★1P と 2P で別の tick・別のレベル）", () =>
        {
            var ev = Sample().Cards();
            var want = new (int Index, int Side, CardSource Source, CardLevel Level)[]
            {
                (-3, 2, CardSource.Gauge, CardLevel.C3),
                (5, 2, CardSource.Spell, CardLevel.C3),
                (12, 1, CardSource.Quick, CardLevel.C4),
            };
            if (ev.Count != want.Length)
                return $"{ev.Count} 件（{want.Length} 件のはず）: "
                       + string.Join(" / ", ev.Select(e => $"{e.Index}:{e.Side}:{e.Source}:{e.Level}"));
            for (int k = 0; k < want.Length; k++)
                if (ev[k].Index != want[k].Index || ev[k].Side != want[k].Side
                    || ev[k].Source != want[k].Source || ev[k].Level != want[k].Level)
                    return $"{k} 件目: {ev[k].Index}:{ev[k].Side}:{ev[k].Source}:{ev[k].Level}";
            return null;
        });
        yield return ("★起点: ボスは reversals の同時増加で分かれる（★2 本。片方は跨いでいない）", () =>
        {
            var ev = Sample().Bosses();
            if (ev.Count != 2)
                return $"{ev.Count} 件（2 件のはず）: "
                       + string.Join(" / ", ev.Select(e => $"{e.Index}:{e.Side}:{e.Source}"));
            var a = ev[0];
            if (a.Index != 12 || a.Side != 1) return $"1 件目 ({a.Index},{a.Side})";
            if (a.Source != BossSource.Boss) return "1 件目の種別: " + a.Source;
            if (a.Level is not null) return "跨いでいないのにしきい値が付いた: " + a.Level;
            var b = ev[1];
            if (b.Index != 30 || b.Side != 2) return $"2 件目 ({b.Index},{b.Side})";
            if (b.Source != BossSource.Reversal) return "2 件目の種別: " + b.Source;
            return b.Level == 300000 ? null : "しきい値: " + (b.Level?.ToString() ?? "なし");
        });
        yield return ("★否定: しきい値を跨がなければ付けない（「10万」と嘘を書かない）", () =>
        {
            var src = new FakeExt(20, 0)
                .With("p1_boss_attacks", i => i < 10 ? 0.0 : 1.0)
                .With("p1_boss_reversals", _ => 0.0)
                .With("p1_spell_points", i => i < 10 ? 99000.0 : 99900.0);
            var ev = CardEvents.Of(src).Bosses();
            if (ev.Count != 1) return $"{ev.Count} 件";
            return ev[0].Level is null ? null : "しきい値を付けている: " + ev[0].Level;
        });

        yield return ("★候補が 2 つ以上なら名乗らせない（黙ってどちらかに倒さない）", () =>
        {
            var src = new FakeExt(30, 0)
                .With("p1_gauge", i => i < 10 ? 400.0 : 300.0)
                .With("p2_gauge", i => i < 11 ? 400.0 : 300.0)
                .With("p1_spell_attacks", i => i < 10 ? 0.0 : 1.0)
                .With("p2_spell_attacks", i => i < 11 ? 0.0 : 1.0)
                .With("p1_boss_attacks", _ => 0.0)
                .With("p2_boss_attacks", _ => 0.0);
            var ce = CardEvents.Of(src);
            if (ce.Cards().Count != 2) return $"起点が {ce.Cards().Count} 件（合成が間違っている）";
            if (ce.CutinCard(10) is not null) return "2 つあるのに 1 つを採っている";
            return ce.CutinCard(13) is CardAttackEvent e && e.Index == 11 && e.Side == 2
                ? null : "1 つに決まるはずの帯で決まっていない";
        });
        yield return ("★名前が付く帯と付かない帯（★母数 2 本）", () =>
        {
            var ce = Sample();
            var far = ce.CutinParts(60);
            if (far.Text != "カットイン") return "遠い帯に名前が付いた: " + far.Text;
            if (far.Side is not null || far.Source is not null) return "遠い帯が側を名乗っている";
            var near = ce.CutinParts(12);
            if (near.Text != "1P C4(Quick)") return "近い帯の名前: " + near.Text;
            return near.Side == 1 && near.Source == "quick" ? null
                : $"側/源: {near.Side}/{near.Source}";
        });
        yield return ("★★50万点はカードが先（★同じ tick にボスが立っていてもカードを採る）", () =>
        {
            var ce = MergeSample(bossSide: 1);
            var got = ce.CutinParts(5);
            return got.Text == "1P C3&Rev(50万)" && got.Source == "spell"
                ? null : $"{got.Text} / {got.Source}";
        });
        yield return ("★★否定: 側を見ずにまとめる旧なら、相手のボスと合体する", () =>
        {
            var ce = MergeSample(bossSide: 2);
            var boss = ce.CutinBoss(5);
            if (boss is null || boss.Value.Side != 2) return "合成が間違っている（2P のボスが無い）";
            var got = ce.CutinParts(5);
            if (got.Text.Contains('&')) return "新も相手のボスと合体している: " + got.Text;
            return got.Text == "1P C3(50万)" ? null : "名前: " + got.Text;
        });
        yield return ("★★C4 はボスを送るが、`C4&Boss` と 2 つ名乗らせない", () =>
        {
            var ce = Sample();
            if (ce.CutinBoss(12) is not BossAttackEvent b || b.Side != 1)
                return "合成が間違っている（C4 の tick にボスが立っていない）";
            var got = ce.CutinParts(12);
            return got.Text == "1P C4(Quick)" ? null : "名前: " + got.Text;
        });
        yield return ("★★否定: 窓の頭より前の発動を「点が出している」と読む旧", () =>
        {
            var ce = Sample();
            var e = ce.CutinCard(-3);
            if (e is null || e.Value.Index >= 0) return "合成が間違っている（負の起点が無い）";
            bool legacy = e is not null;
            if (!legacy) return "旧が落ちない";
            if (ce.CutinNamedByPoint(-3)) return "新も点が出していると言っている";
            return ce.CutinNamedByPoint(12) ? null : "窓の中の起点で立っていない";
        });
    }


    internal sealed class FakeExt : IExtSource
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

    private static CardEvents Sample()
    {
        var src = new FakeExt(80, 8)
            .With("p1_gauge", i => i < 12 ? 400.0 : 0.0)
            .With("p1_spell_attacks", _ => 0.0)
            .With("p1_boss_attacks", i => i < 12 ? 0.0 : 1.0)
            .With("p1_boss_reversals", _ => 0.0)
            .With("p1_spell_points", _ => 10.0)
            .With("p2_gauge", i => i < -3 ? 400.0 : 200.0)
            .With("p2_spell_attacks", i => i < -3 ? 0.0 : (i < 5 ? 1.0 : 2.0))
            .With("p2_boss_attacks", i => i < 30 ? 3.0 : 4.0)
            .With("p2_boss_reversals", i => i < 30 ? 1.0 : 2.0)
            .With("p2_spell_points", i => i < 30 ? 299000.0 : 301000.0);
        return Of(src);
    }

    private static CardEvents MergeSample(int bossSide)
    {
        int other = 3 - bossSide;
        var src = new FakeExt(40, 0)
            .With("p1_gauge", _ => 250.0)
            .With("p1_spell_attacks", i => i < 5 ? 0.0 : 1.0)
            .With($"p{bossSide}_boss_attacks", i => i < 5 ? 0.0 : 1.0)
            .With($"p{bossSide}_boss_reversals", i => i < 5 ? 0.0 : 1.0)
            .With($"p{bossSide}_spell_points", i => i < 5 ? 499000.0 : 501000.0)
            .With($"p{other}_boss_attacks", _ => 0.0);
        return Of(src);
    }
}
