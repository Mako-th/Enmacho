using System.Globalization;

namespace TH09.Analysis;

public sealed class EnemyHitbox
{
    private const string WhyNone =
        "当たり判定を持たない枠のサイズを読もうとした（先に None を見ること）";

    private readonly double _halfW, _halfH, _halfWMax, _halfHMax;
    private readonly bool _ranged;

    public static readonly EnemyHitbox NoHitbox = new();

    private EnemyHitbox() => None = true;

    private EnemyHitbox(double halfW, double halfH, double halfWMax, double halfHMax, bool ranged)
    {
        None = false;
        _halfW = halfW; _halfH = halfH; _halfWMax = halfWMax; _halfHMax = halfHMax;
        _ranged = ranged;
    }

    public bool None { get; }

    public double HalfW => Sized(_halfW);

    public double HalfH => Sized(_halfH);

    public double HalfWMax => Sized(_halfWMax);

    public double HalfHMax => Sized(_halfHMax);

    public bool Ranged => None ? throw new InvalidOperationException(WhyNone) : _ranged;

    internal static EnemyHitbox FromRaw(double wl, double hl, double wh, double hh) =>
        new(wl / EnemySize.HitboxDiv, hl / EnemySize.HitboxDiv,
            wh / EnemySize.HitboxDiv, hh / EnemySize.HitboxDiv,
            wl != wh || hl != hh);

    private double Sized(double v) => None ? throw new InvalidOperationException(WhyNone) : v;
}

public static class EnemySize
{
    internal const double HitboxDiv = AnalysisTables.EnemyHitboxDiv;


    private static readonly Dictionary<string, EnemyHitbox> HitboxByClass = BuildHitboxByClass();

    private static readonly HashSet<string> NoHitboxClasses =
        new(Packed.Lines(AnalysisTables.EnemyHitboxNonePacked), StringComparer.Ordinal);

    private static readonly Dictionary<int, (double Lead, double Alone)> FairyRawByIndex =
        BuildFairyRaw();

    private static readonly HashSet<int> FairyLeadMin = BuildFairyLeadMin();

    private static readonly Dictionary<int, string> FairyNameJa = BuildFairyNameJa();

    private static Dictionary<string, EnemyHitbox> BuildHitboxByClass()
    {
        var map = new Dictionary<string, EnemyHitbox>(StringComparer.Ordinal);
        foreach (var line in Packed.Lines(AnalysisTables.EnemyHitboxRawPacked))
        {
            var f = line.Split('\t');
            if (f.Length != 5)
                throw new InvalidDataException($"ENEMY_HITBOX_RAW の行が 5 列でない: {line}");
            map[f[0]] = EnemyHitbox.FromRaw(D(f[1]), D(f[2]), D(f[3]), D(f[4]));
        }
        return map;
    }

    private static Dictionary<int, (double, double)> BuildFairyRaw()
    {
        var map = new Dictionary<int, (double, double)>();
        foreach (var line in Packed.Lines(AnalysisTables.EnemyKindIdxFairyRawPacked))
        {
            var f = line.Split('\t');
            if (f.Length != 3)
                throw new InvalidDataException($"ENEMY_KIND_IDX_FAIRY_RAW の行が 3 列でない: {line}");
            map[I(f[0])] = (D(f[1]), D(f[2]));
        }
        return map;
    }

    private static HashSet<int> BuildFairyLeadMin()
    {
        var set = new HashSet<int>();
        foreach (var line in Packed.Lines(AnalysisTables.EnemyKindIdxFairyLeadMinPacked))
            set.Add(I(line));
        return set;
    }

    private static Dictionary<int, string> BuildFairyNameJa()
    {
        var map = new Dictionary<int, string>();
        foreach (var line in Packed.Lines(AnalysisTables.EnemyKindIdxJaPacked))
        {
            var f = line.Split('\t');
            if (f.Length != 2)
                throw new InvalidDataException($"ENEMY_KIND_IDX_JA の行が 2 列でない: {line}");
            map[I(f[0])] = f[1];
        }
        return map;
    }

    private static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);

    private static int I(string s) => int.Parse(s, CultureInfo.InvariantCulture);


    public static EnemyHitbox? Half(string? enemyClass)
    {
        if (enemyClass is null) return null;
        if (NoHitboxClasses.Contains(enemyClass)) return EnemyHitbox.NoHitbox;
        return HitboxByClass.TryGetValue(enemyClass, out var box) ? box : null;
    }


    public static int? KindIndex(uint kind, uint? flags)
    {
        if (flags is null) return null;
        if ((flags.Value & AnalysisTables.CoordFlagEnemyKindIdx) == 0) return null;
        if ((flags.Value & AnalysisTables.CoordFlagEnemyKindClash) != 0) return null;
        return (int)((kind >> AnalysisTables.CoordEnemyKindIdxShift) & AnalysisTables.CoordEnemyKindIdxMask);
    }

    public static bool FairyLead(uint kind) =>
        (CatWord(kind) & AnalysisTables.CoordEnemyFairyLeadBit) != 0;

    public static uint CatWord(uint kind) => kind & AnalysisTables.CoordEnemyKindWordMask;


    public static EnemyHitbox? HalfByIndex(int idx, bool? lead = null)
    {
        if (!FairyRawByIndex.TryGetValue(idx, out var raw)) return null;
        var (lo, hi) = raw;
        if (lead is not null && FairyLeadMin.Contains(idx))
        {
            lo = hi = lead.Value ? lo : hi;
        }
        return EnemyHitbox.FromRaw(lo, lo, hi, hi);
    }

    public static string? KindNameJa(int idx) =>
        FairyNameJa.TryGetValue(idx, out var ja) ? ja : null;


    public static int? KindIndex(Window w, int slot, int i)
    {
        var kind = w.Raw(CoordRing.ColKind(slot), i);
        return kind is null ? null : KindIndex(kind.Value, w.CoordFlags(i));
    }

    public static bool? FairyLead(Window w, int slot, int i)
    {
        var kind = w.Raw(CoordRing.ColKind(slot), i);
        return kind is null ? null : FairyLead(kind.Value);
    }

    public static EnemyHitbox? Of(string? enemyClass, int? kindIndex, bool? lead)
    {
        if (kindIndex is not null
            && string.Equals(enemyClass, AnalysisTables.EnemyClassFairy, StringComparison.Ordinal))
        {
            var byIdx = HalfByIndex(kindIndex.Value, lead);
            if (byIdx is not null) return byIdx;
        }
        return Half(enemyClass);
    }

    public static EnemyHitbox? Of(Window w, int slot, int i, string? enemyClass) =>
        Of(enemyClass, KindIndex(w, slot, i), FairyLead(w, slot, i));


    public static IEnumerable<(string Label, Func<string?> Body)> SelfTestCases()
    {
        yield return ($"★母数（分類 {HitboxByClass.Count} / 判定なし {NoHitboxClasses.Count}"
                      + $" / 妖精 {FairyRawByIndex.Count} / bit9 で割れる {FairyLeadMin.Count}"
                      + $" / 呼称 {FairyNameJa.Count}）", () =>
        {
            if (HitboxByClass.Count == 0) return "ENEMY_HITBOX_RAW が 0 行";
            if (NoHitboxClasses.Count == 0) return "ENEMY_HITBOX_NONE が 0 行";
            if (FairyRawByIndex.Count == 0) return "ENEMY_KIND_IDX_FAIRY_RAW が 0 行";
            if (FairyLeadMin.Count == 0) return "ENEMY_KIND_IDX_FAIRY_LEAD_MIN が 0 行";
            if (FairyNameJa.Count == 0) return "ENEMY_KIND_IDX_JA が 0 行";
            return null;
        });

        yield return ("★「判定なし」の分類が分類の表にも載っていない（判定の有無は 1 つの表にしか置かない）", () =>
        {
            var both = NoHitboxClasses.Where(HitboxByClass.ContainsKey).ToList();
            return both.Count == 0 ? null : "両方に居る分類: " + string.Join(",", both);
        });

        yield return ("★bit9 で割れる種別と「幅を持つ種別」が一致する", () =>
        {
            var ranged = FairyRawByIndex.Where(kv => kv.Value.Lead != kv.Value.Alone)
                                        .Select(kv => kv.Key).OrderBy(k => k).ToList();
            var lead = FairyLeadMin.OrderBy(k => k).ToList();
            if (!ranged.SequenceEqual(lead))
                return $"幅を持つ {Show(ranged)} ≠ bit9 で割れる {Show(lead)}"
                       + "（幅があるのに bit9 で割れない種別は、幅のまま残り続ける）";
            var missing = FairyLeadMin.Where(k => !FairyRawByIndex.ContainsKey(k)).ToList();
            return missing.Count == 0 ? null : "表に無い種別が割れる側に居る: " + Show(missing);
        });

        yield return ("★flags を渡さないと種別インデックスは null（＝知らない）", () =>
        {
            foreach (var kind in LegacyKinds)
                if (KindIndex(kind, null) is int got)
                    return $"kind=0x{kind:X} で {got} を返した（v7 以前の窓では読めないはず）";
            return null;
        });

        yield return ("★区画のビットが立っていない tick でも null（0 に倒さない）", () =>
            KindIndex(MakeKind(3, false), 0u) is int g ? $"{g} を返した" : null);

        yield return ("★CLASH が立っている窓では null（相乗りしていない枠が混ざる）", () =>
        {
            var flags = AnalysisTables.CoordFlagEnemyKindIdx | AnalysisTables.CoordFlagEnemyKindClash;
            return KindIndex(MakeKind(3, false), flags) is int g ? $"{g} を返した" : null;
        });

        yield return ("★区画が立っていれば bit16-23 をそのまま返す（符号を付けない）", () =>
        {
            foreach (var idx in new[] { 0, 1, 3, 0x7F, 0xFF })
            {
                var got = KindIndex(MakeKind(idx, false), AnalysisTables.CoordFlagEnemyKindIdx);
                if (got != idx) return $"idx={idx} が {Show(got)} になった";
            }
            return null;
        });

        yield return ("★bit9 は相乗りの語（bit16 以上）に影響されない", () =>
        {
            var decoy = MakeKind((int)AnalysisTables.CoordEnemyFairyLeadBit >> 8, false);
            if (FairyLead(decoy)) return "相乗りの語に釣られて lead になった";
            if (!FairyLead(MakeKind(0, true))) return "bit9 が立っているのに lead にならない";
            return null;
        });

        foreach (var idx in FairyLeadMin.OrderBy(k => k))
        {
            var (lo, hi) = FairyRawByIndex[idx];
            var name = KindNameJa(idx) ?? $"idx {idx}";
            yield return ($"★{name}の {lo:0.#} / {hi:0.#} が bit9 で 1 値に割れる", () =>
            {
                if (lo == hi) return $"表が幅を持っていない（{lo} == {hi}）——この検査は何も見ていない";
                var lead = HalfByIndex(idx, true);
                var alone = HalfByIndex(idx, false);
                var unknown = HalfByIndex(idx, null);
                if (lead is null || alone is null || unknown is null) return "表に無い（null）";
                if (lead.Ranged || alone.Ranged) return "bit9 を渡したのに幅が残った";
                if (!Near(lead.HalfW, lo / HitboxDiv)) return $"bit9 あり: {lead.HalfW} ≠ {lo / HitboxDiv}";
                if (!Near(alone.HalfW, hi / HitboxDiv)) return $"bit9 なし: {alone.HalfW} ≠ {hi / HitboxDiv}";
                if (!unknown.Ranged) return "bit9 が読めないのに 1 値へ倒した";
                if (!Near(unknown.HalfW, lo / HitboxDiv) || !Near(unknown.HalfWMax, hi / HitboxDiv))
                    return $"読めないときの幅が {unknown.HalfW}〜{unknown.HalfWMax}";
                return null;
            });
        }

        yield return ("★bit9 で割れない種別は lead を渡しても答えが変わらない", () =>
        {
            var plain = FairyRawByIndex.Keys.Where(k => !FairyLeadMin.Contains(k)).OrderBy(k => k).ToList();
            if (plain.Count == 0) return "割れない種別が 1 つも無い（この検査は何も見ていない）";
            foreach (var idx in plain)
            {
                var a = HalfByIndex(idx, null); var b = HalfByIndex(idx, true); var c = HalfByIndex(idx, false);
                if (a is null || b is null || c is null) return $"idx={idx} が null";
                if (a.Ranged || b.Ranged || c.Ranged) return $"idx={idx} に幅がある";
                if (!Near(a.HalfW, b.HalfW) || !Near(a.HalfW, c.HalfW))
                    return $"idx={idx} が lead で変わった（{a.HalfW}/{b.HalfW}/{c.HalfW}）";
            }
            return null;
        });

        yield return ("★表に無い種別インデックスは null（知らない）", () =>
        {
            var unknown = Enumerable.Range(0, 256).First(k => !FairyRawByIndex.ContainsKey(k));
            return HalfByIndex(unknown, true) is null ? null : $"idx={unknown} に答えを返した";
        });

        foreach (var cls in NoHitboxClasses.OrderBy(c => c, StringComparer.Ordinal))
        {
            yield return ($"★{cls} は「判定なし」で、サイズを読むと落ちる", () =>
            {
                var box = Half(cls);
                if (box is null) return "null（＝知らない）になった —— 判定なしと混ざっている";
                if (!box.None) return "None が立っていない";
                try { _ = box.HalfW; return "HalfW が読めてしまった（0 を返す形になっている）"; }
                catch (InvalidOperationException) { }
                try { _ = box.Ranged; return "Ranged が読めてしまった"; }
                catch (InvalidOperationException) { }
                return null;
            });
        }

        yield return ("★表に無い分類は null（知らない）——「判定なし」と混ぜない", () =>
        {
            const string cls = "no_such_enemy_class";
            if (Half(cls) is not null) return "答えを返した";
            if (Half(null) is not null) return "分類が null のときに答えを返した";
            return null;
        });

        yield return ("★幅を持つ分類は妖精だけ（分類の表からそのまま）", () =>
        {
            var ranged = HitboxByClass.Where(kv => !kv.Value.None && kv.Value.Ranged)
                                      .Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
            return ranged.SequenceEqual(new[] { AnalysisTables.EnemyClassFairy })
                ? null : "幅を持つ分類: " + string.Join(",", ranged);
        });

        yield return ("★入口の振り分け（妖精だけ種別の表／それ以外と読めない窓は分類の表）", () =>
        {
            var fairy = AnalysisTables.EnemyClassFairy;
            var idx = FairyLeadMin.OrderBy(k => k).First();
            var (lo, _) = FairyRawByIndex[idx];

            var got = Of(fairy, idx, true);
            if (got is null || got.Ranged || !Near(got.HalfW, lo / HitboxDiv))
                return "妖精 ＋ 読めた個体で種別の表を引いていない";

            var wide = Of(fairy, null, null);
            var byClass = Half(fairy);
            if (wide is null || byClass is null) return "妖精の分類が表に無い";
            if (!wide.Ranged || !Near(wide.HalfW, byClass.HalfW) || !Near(wide.HalfWMax, byClass.HalfWMax))
                return "読めない窓で分類の幅へ落ちていない（0 に倒している疑い）";

            var alien = Enumerable.Range(0, 256).First(k => !FairyRawByIndex.ContainsKey(k));
            var fell = Of(fairy, alien, true);
            if (fell is null || !fell.Ranged) return "表に無い番号で分類の表へ落ちていない";

            var ghost = Of(AnalysisTables.EnemyClassGhost, idx, true);
            var ghostByClass = Half(AnalysisTables.EnemyClassGhost);
            if (ghost is null || ghostByClass is null) return "幽霊が表に無い";
            if (!Near(ghost.HalfW, ghostByClass.HalfW)) return "幽霊が種別の表を引いた";

            foreach (var cls in NoHitboxClasses)
                if (Of(cls, idx, true) is not { None: true }) return $"{cls} が「判定なし」でなくなった";

            return Of("no_such_enemy_class", idx, true) is null ? null : "知らない分類に答えを返した";
        });

        yield return ("★否定: 旧のやり方（flags を見ずに kind>>16）だと v7 以前の窓が全部 青妖精 になる", () =>
        {
            static int LegacyKindIndex(uint kind) =>
                (int)((kind >> AnalysisTables.CoordEnemyKindIdxShift) & AnalysisTables.CoordEnemyKindIdxMask);

            var legacyZero = LegacyKinds.Count(k => LegacyKindIndex(k) == 0);
            if (legacyZero != LegacyKinds.Length)
                return $"旧のやり方が {LegacyKinds.Length} 本中 {legacyZero} 本しか 0 を返さない"
                       + "（この標本は v7 以前の窓を模していない ——否定になっていない）";

            var newAnswered = LegacyKinds.Where(k => KindIndex(k, null) is not null).ToList();
            if (newAnswered.Count != 0)
                return $"新が {newAnswered.Count} 本に答えてしまった（旧と同じ嘘をついている）";

            var notFairy = LegacyKinds.Count(k =>
                !string.Equals(CoordRing.EnemyClass(k), AnalysisTables.EnemyClassFairy, StringComparison.Ordinal));
            return notFairy > 0 ? null
                : "標本が全部妖精なので「全部が青妖精になる」の害が見えていない";
        });
    }

    private static readonly uint[] LegacyKinds =
    [
        0u,
        AnalysisTables.CoordEnemyFairyLeadBit,
        AnalysisTables.CoordEnemyLilyBit,
        AnalysisTables.CoordEnemyGhostMask & 0x40u,
        AnalysisTables.CoordEnemyBossMask,
    ];

    private static uint MakeKind(int idx, bool lead) =>
        ((uint)idx & AnalysisTables.CoordEnemyKindIdxMask) << AnalysisTables.CoordEnemyKindIdxShift
        | (lead ? AnalysisTables.CoordEnemyFairyLeadBit : 0u);

    private static bool Near(double a, double b) => Math.Abs(a - b) < 1e-9;

    private static string Show(IEnumerable<int> xs) => "{" + string.Join(",", xs) + "}";

    private static string Show(int? x) => x is null ? "null" : x.Value.ToString(CultureInfo.InvariantCulture);
}
