using System.Runtime.CompilerServices;

namespace TH09.Analysis;

public static class PlayerShots
{

    public static string LegendJa => AnalysisTables.ShotLegendJa;

    public static string C1Ja => AnalysisTables.ShotC1JaTrue;

    public static string NormalJa => AnalysisTables.ShotC1JaFalse;

    public static string UnknownJa => AnalysisTables.ShotC1UnknownJa;

    public static string SizeRangeJa => AnalysisTables.ShotSizeRangeJa;

    public static double C1LightnessDrop => AnalysisTables.ShotC1LDrop;

    public static string LegendHeading(bool? c1) =>
        c1 is null ? UnknownJa : (c1.Value ? C1Ja : LegendJa);


    private static readonly Dictionary<int, int> C1Entry = BuildC1Entry();

    private static Dictionary<int, int> BuildC1Entry()
    {
        var map = new Dictionary<int, int>();
        foreach (var line in Packed.Lines(AnalysisTables.ShotC1EntryPacked))
        {
            var f = line.Split('\t');
            map[int.Parse(f[0])] = int.Parse(f[1]);
        }
        return map;
    }

    public static int C1EntryCount => C1Entry.Count;


    public static ShotKindParts KindParts(uint kind) => new(
        kind,
        kind & AnalysisTables.CoordShotKindSpriteMask,
        (kind >> AnalysisTables.CoordShotKindBehavShift) & AnalysisTables.CoordShotKindBehavMask,
        (kind >> AnalysisTables.CoordShotKindEntryShift) & AnalysisTables.CoordShotKindEntryMask);

    public static ShotBox BoxHalf(uint wBits, uint hBits)
    {
        double w = CoordRing.UnpackF32(wBits), h = CoordRing.UnpackF32(hBits);
        return new ShotBox(w, h, w * 0.5, h * 0.5);
    }

    public static bool? IsC1(int? charId, uint entry)
    {
        if (entry == 0 || charId is null) return null;
        if (!C1Entry.TryGetValue(charId.Value, out var boundary)) return null;
        return (long)entry * 4 >= boundary;
    }

    public static bool IsDrawable(int slot, uint state) =>
        CoordRing.SlotIsAlive(slot, state) && !CoordRing.SlotIsVanishing(slot, state);


    public static bool HasShotSlots(Window w) => SlotsOf(w, 1).Count > 0 || SlotsOf(w, 2).Count > 0;

    public static IReadOnlyList<int> SlotsOf(Window w, int side)
    {
        var cache = Caches.GetValue(w, static _ => new Cache());
        lock (cache)
        {
            if (cache.Sides is null)
            {
                if (w.Meta.SlotCount > CoordRing.SlotCount)
                    throw new InvalidDataException(
                        $"窓の slot_count({w.Meta.SlotCount}) が枠の表({CoordRing.SlotCount})より多い"
                        + "（座標リングの版が違う窓を、今の表で読もうとしている）");
                var made = new List<int>[3] { [], [], [] };
                for (int slot = 0; slot < w.Meta.SlotCount; slot++)
                {
                    var b = CoordRing.BaseName(slot);
                    if (b.Length < 4 || b[0] != 'p' || b[3] != 's') continue;
                    made[b[1] == '1' ? 1 : 2].Add(slot);
                }
                cache.Sides = made;
            }
            return cache.Sides[side];
        }
    }

    public static List<PlayerShot> At(Window w, int i, int side)
    {
        var outList = new List<PlayerShot>();
        foreach (var slot in SlotsOf(w, side))
        {
            var st = w.Raw(CoordRing.ColState(slot), i);
            if (st is null || !IsDrawable(slot, st.Value)) continue;
            double? x = w.Float(CoordRing.ColX(slot), i), y = w.Float(CoordRing.ColY(slot), i);
            if (x is null || y is null) continue;
            var wb = w.Raw(ShotCol(slot, SuffixW), i);
            var hb = w.Raw(ShotCol(slot, SuffixH), i);
            var kind = w.Raw(CoordRing.ColKind(slot), i);
            outList.Add(new PlayerShot
            {
                Slot = slot,
                X = x.Value,
                Y = y.Value,
                Kind = kind,
                State = st.Value,
                Box = wb is null || hb is null ? null : BoxHalf(wb.Value, hb.Value),
                Parts = KindParts(kind ?? 0),
            });
        }
        return outList;
    }


    private const string SuffixW = "_w";
    private const string SuffixH = "_h";

    private static string ShotCol(int slot, string suffix)
    {
        if (!CoordRing.IsShotSlot(slot))
            throw new ArgumentException($"slot {slot} は自機ショット枠ではありません");
        return CoordRing.BaseName(slot) + suffix;
    }


    private sealed class Cache
    {
        public List<int>[]? Sides;
    }

    private static readonly ConditionalWeakTable<Window, Cache> Caches = new();


    public static IEnumerable<(string Label, Func<string?> Body)> SelfTestCases()
    {
        yield return ($"★母数（C1 の境界の表 {C1Entry.Count} 件）", () =>
            C1Entry.Count > 0 ? null : "ShotC1EntryPacked が 0 行（生成物を引けていない）");

        yield return ("ショットの語が 1 つも空でない", () =>
        {
            if (string.IsNullOrEmpty(LegendJa)) return "見出しが空";
            if (string.IsNullOrEmpty(C1Ja)) return "C1 の語が空";
            if (string.IsNullOrEmpty(NormalJa)) return "通常の語が空";
            if (string.IsNullOrEmpty(UnknownJa)) return "不明の断りが空";
            if (string.IsNullOrEmpty(SizeRangeJa)) return "サイズが範囲のときの断りが空";
            return null;
        });

        yield return ("★見出しの 3 値（通常は『見出しの語』であって『通常の語』ではない）", () =>
        {
            if (!SameText(LegendHeading(false), LegendJa)) return "通常の見出しが LegendJa でない";
            if (!SameText(LegendHeading(true), C1Ja)) return "C1 の見出しが C1Ja でない";
            if (!SameText(LegendHeading(null), UnknownJa)) return "不明の見出しが UnknownJa でない";
            if (SameText(LegendHeading(false), NormalJa))
                return "通常の見出しに NormalJa を使っている（見出しが 2 通りになる）";
            return null;
        });

        yield return ($"★境界そのものは C1 / その 1 つ手前は通常（表の {C1Entry.Count} 件を全部）", () =>
        {
            foreach (var (id, boundary) in C1Entry.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value)))
            {
                if (boundary % 4 != 0) return $"char {id} の境界 {boundary} が 4 の倍数でない（entry は変位 >> 2）";
                uint at = (uint)(boundary / 4);
                if (IsC1(id, at) != true) return $"char {id}: 境界ちょうどが C1 にならない";
                if (IsC1(id, at - 1) != false) return $"char {id}: 境界の 1 つ手前が通常にならない";
            }
            return null;
        });

        yield return ("★entry が 0 なら必ず null（『運べなかった』を『通常』に倒さない）", () =>
        {
            foreach (var id in C1Entry.Keys)
                if (IsC1(id, 0) is bool got) return $"char {id} で {got} を返した";
            return null;
        });

        yield return ("★キャラが分からなければ null", () =>
            IsC1(null, 9999) is bool g ? $"{g} を返した" : null);

        var absent = AbsentCharIds();
        yield return ($"★★表に無いキャラは必ず null（推測で倒さない。試した ID {absent.Count} 件）", () =>
        {
            if (absent.Count == 0) return "表に穴が無い（この検査は何も見ていない）";
            foreach (var id in absent)
                foreach (var entry in new uint[] { 1, 271, 356, 1266, 0xFFF })
                    if (IsC1(id, entry) is bool got)
                        return $"表に無い char {id} / entry {entry} で {got} を返した";
            return null;
        });

        yield return ("★kind の 3 区画が互いに漏れない（合成）", () =>
        {
            foreach (var (sp, bv, en) in new[] { (0u, 0u, 0u), (2u, 4u, 271u), (4u, 2u, 1266u),
                                                 (AnalysisTables.CoordShotKindSpriteMask,
                                                  AnalysisTables.CoordShotKindBehavMask,
                                                  AnalysisTables.CoordShotKindEntryMask) })
            {
                var p = KindParts(MakeKind(sp, bv, en));
                if (p.Sprite != sp || p.Behav != bv || p.Entry != en)
                    return $"({sp},{bv},{en}) が ({p.Sprite},{p.Behav},{p.Entry}) になった";
            }
            return null;
        });

        yield return ("★entry の上に何が乗っていても 3 区画は釣られない（マスクの検査）", () =>
        {
            uint decoy = MakeKind(2, 4, 271) | 0xFF000000u;
            var p = KindParts(decoy);
            if (p.Sprite != 2 || p.Behav != 4 || p.Entry != 271)
                return $"上位ビットに釣られた（{p.Sprite},{p.Behav},{p.Entry}）";
            return p.Raw == decoy ? null : "Raw が元の語と違う";
        });

        yield return ("★判定は全幅・全高で来るので半分にする（合成 18×48）", () =>
        {
            var box = BoxHalf(F(18f), F(48f));
            if (box.W != 18.0 || box.H != 48.0) return $"全幅が {box.W}×{box.H} になった";
            return box.HalfW == 9.0 && box.HalfH == 24.0 ? null : $"半幅が {box.HalfW}×{box.HalfH}";
        });

        yield return ("★state は 3 値。描くのは 1 だけ（0 ＝ 空き / 2 ＝ 命中後）", () =>
        {
            int slot = AnalysisTables.CoordBaseP1Shot;
            if (IsDrawable(slot, AnalysisTables.CoordShotStateFree)) return "空き枠を描いている";
            if (!IsDrawable(slot, 1)) return "生きている枠を描いていない";
            if (IsDrawable(slot, AnalysisTables.CoordShotStateVanish)) return "命中後の枠を描いている";
            return null;
        });

        yield return ("★否定: 旧（entry == 0 を『通常』に倒す）なら、運べなかった枠が通常に化ける", () =>
        {
            if (C1Entry.Count == 0) return "表が空（この検査は何も見ていない）";
            var (id, boundary) = FirstEntry();
            bool LegacyIsC1(uint entry) => entry * 4 >= boundary;
            if (LegacyIsC1(0)) return "旧の写しが間違っている（0 を C1 と答えた。この検査は何も見ていない）";
            return IsC1(id, 0) is null ? null : "新も entry 0 を通常／C1 に倒している";
        });

        yield return ("★★否定: 旧（表に無いキャラを共通の境界で引く）なら、リリカ・ルナサが C1 に化ける", () =>
        {
            if (absent.Count == 0 || C1Entry.Count == 0) return "表に穴が無い（この検査は何も見ていない）";
            int common = CommonBoundary();
            int id = absent[0];
            uint entry = (uint)(common / 4);
            bool LegacyIsC1(int charId, uint e) =>
                e * 4 >= (C1Entry.TryGetValue(charId, out var b) ? b : common);
            if (!LegacyIsC1(id, entry))
                return "旧の写しが間違っている（表に無いキャラを C1 と答えなかった）";
            return IsC1(id, entry) is null ? null : "新も表に無いキャラを通常／C1 に倒している";
        });

        yield return ("★否定: 旧（半幅にせず全幅で描く）なら矩形が 2 倍になる", () =>
        {
            var box = BoxHalf(F(18f), F(48f));
            double legacyHalfW = CoordRing.UnpackF32(F(18f));
            if (legacyHalfW == box.HalfW) return "旧の写しが間違っている（この検査は何も見ていない）";
            return legacyHalfW == box.HalfW * 2 ? null : "新の半幅が全幅の半分になっていない";
        });

        yield return ("★否定: 旧（生死だけ見て消滅演出を見ない）なら命中後のショットが残る", () =>
        {
            int slot = AnalysisTables.CoordBaseP1Shot;
            bool LegacyDrawable(uint state) => CoordRing.SlotIsAlive(slot, state);
            if (!LegacyDrawable(AnalysisTables.CoordShotStateVanish))
                return "旧の写しが間違っている（命中後を落としている。この検査は何も見ていない）";
            return IsDrawable(slot, AnalysisTables.CoordShotStateVanish) ? "新も命中後を描いている" : null;
        });

        yield return ("★ショット以外の枠の列名を求めたら落ちる（エラーを出さずに別の枠を読ませない）", () =>
        {
            try
            {
                ShotCol(AnalysisTables.CoordBaseP1Bullet, SuffixW);
                return "弾枠で列名が返ってきた";
            }
            catch (ArgumentException) { return null; }
        });
    }

    private static uint MakeKind(uint sprite, uint behav, uint entry) =>
        (sprite & AnalysisTables.CoordShotKindSpriteMask)
        | ((behav & AnalysisTables.CoordShotKindBehavMask) << AnalysisTables.CoordShotKindBehavShift)
        | ((entry & AnalysisTables.CoordShotKindEntryMask) << AnalysisTables.CoordShotKindEntryShift);

    private static uint F(float v) => BitConverter.SingleToUInt32Bits(v);

    private static List<int> AbsentCharIds()
    {
        var outList = new List<int>();
        if (C1Entry.Count == 0) return outList;
        int max = C1Entry.Keys.Max();
        for (int id = 0; id <= max + 1; id++)
            if (!C1Entry.ContainsKey(id)) outList.Add(id);
        return outList;
    }

    private static int CommonBoundary() =>
        C1Entry.Values.GroupBy(v => v).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key;

    private static (int Id, int Boundary) FirstEntry()
    {
        var kv = C1Entry.OrderBy(x => x.Key).First();
        return (kv.Key, kv.Value);
    }

    private static bool SameText(string a, string b) => string.Equals(a, b, StringComparison.Ordinal);
}

public readonly record struct ShotKindParts(uint Raw, uint Sprite, uint Behav, uint Entry);

public readonly record struct ShotBox(double W, double H, double HalfW, double HalfH);

public sealed class PlayerShot
{
    public required int Slot { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required uint? Kind { get; init; }
    public required uint State { get; init; }
    public required ShotBox? Box { get; init; }
    public required ShotKindParts Parts { get; init; }
}
