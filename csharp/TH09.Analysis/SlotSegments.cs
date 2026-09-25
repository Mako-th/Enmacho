using System.Runtime.CompilerServices;

namespace TH09.Analysis;

public static class SlotSegments
{
    public const double ReuseJump = AnalysisTables.ReuseJump;

    public static IReadOnlyList<(int Start, int End)> Of(Window w, int slot)
    {
        var cache = CacheOf(w);
        lock (cache)
        {
            if (cache.Segments.TryGetValue(slot, out var got)) return got;
            var made = Compute(slot, w.TickCount,
                               Column(w, CoordRing.ColState(slot)),
                               Column(w, CoordRing.ColKind(slot)),
                               Column(w, CoordRing.ColX(slot)),
                               Column(w, CoordRing.ColY(slot)),
                               IsBullet(w, slot), SideOf(w, slot));
            cache.Segments[slot] = made;
            return made;
        }
    }

    public static (int Start, int End)? At(Window w, int slot, int i)
    {
        foreach (var seg in Of(w, slot))
            if (seg.Start <= i && i <= seg.End) return seg;
        return null;
    }

    public static IReadOnlyList<(int Start, int End)> Compute(
        int slot, int tickCount, uint[]? state, uint[]? kind, uint[]? xs, uint[]? ys,
        bool isBullet, int? side)
    {
        var outList = new List<(int Start, int End)>();
        if (state is null || state.Length == 0) return outList;
        int end = Math.Min(tickCount, state.Length);
        int cur = -1;
        bool prev = false;
        for (int i = 0; i < end; i++)
        {
            if (!CoordRing.SlotIsAlive(slot, state[i]))
            {
                if (cur >= 0) outList.Add((cur, i - 1));
                cur = -1;
                prev = false;
                continue;
            }
            if (prev && Reused(i, state, kind, xs, ys, isBullet, side))
            {
                outList.Add((cur, i - 1));
                cur = i;
            }
            else if (cur < 0) cur = i;
            prev = true;
        }
        if (cur >= 0) outList.Add((cur, end - 1));
        return outList;
    }

    private static bool Reused(int i, uint[] st, uint[]? kd, uint[]? xs, uint[]? ys,
                               bool isBullet, int? side)
    {
        uint a = st[i - 1], b = st[i];
        if (isBullet)
        {
            const uint m = AnalysisTables.CoordStateAliveMask;
            if ((a & m) == AnalysisTables.CoordBulletStateVanish
                && (b & m) != AnalysisTables.CoordBulletStateVanish) return true;
            uint p = BulletOrigin.SiteId(a), q = BulletOrigin.SiteId(b);
            if (p != 0 && q != 0 && p != q) return true;
        }
        if (kd is not null && i < kd.Length && kd[i - 1] != kd[i]) return true;
        if (side is not null && xs is not null && ys is not null
            && i < xs.Length && i < ys.Length)
        {
            double dx = CoordRing.UnpackF32(xs[i]) - CoordRing.UnpackF32(xs[i - 1]);
            double dy = CoordRing.UnpackF32(ys[i]) - CoordRing.UnpackF32(ys[i - 1]);
            if (Math.Sqrt(dx * dx + dy * dy) >= ReuseJump) return true;
        }
        return false;
    }

    public static int? SideOf(Window w, int slot)
    {
        var cache = CacheOf(w);
        lock (cache) { return Buckets(w, cache).Side.TryGetValue(slot, out var sd) ? sd : null; }
    }

    public static bool IsBullet(Window w, int slot)
    {
        var cache = CacheOf(w);
        lock (cache) { return Buckets(w, cache).Bullets.Contains(slot); }
    }

    private static uint[]? Column(Window w, string name) => w.Cols.Has(name) ? w.Cols[name] : null;


    private sealed class Cache
    {
        public readonly Dictionary<int, IReadOnlyList<(int Start, int End)>> Segments = new();
        public Dictionary<int, int>? Side;
        public HashSet<int>? Bullets;
    }

    private static readonly ConditionalWeakTable<Window, Cache> Caches = new();

    private static Cache CacheOf(Window w) => Caches.GetValue(w, static _ => new Cache());

    private static (Dictionary<int, int> Side, HashSet<int> Bullets) Buckets(Window w, Cache cache)
    {
        if (cache.Side is null || cache.Bullets is null)
        {
            var side = new Dictionary<int, int>();
            var bullets = new HashSet<int>();
            for (int sd = 1; sd <= 2; sd++)
                foreach (var (kind, slots) in w.SlotsOf(sd))
                    foreach (var slot in slots)
                    {
                        side[slot] = sd;
                        if (kind == "bullet") bullets.Add(slot);
                    }
            cache.Side = side;
            cache.Bullets = bullets;
        }
        return (cache.Side, cache.Bullets);
    }


    public static IEnumerable<(string Label, Func<string?> Body)> SelfTestCases()
    {
        int bulletSlot = AnalysisTables.CoordBaseP1Bullet;

        yield return ("合成: 生き通しの弾枠 1 つは 1 区間（合成 6 tick）", () =>
        {
            var seg = Compute(bulletSlot, 6, [1, 1, 1, 1, 1, 1], null, null, null, true, 1);
            if (seg.Count != 1) return $"区間 {seg.Count} 本（1 本のはず）";
            return seg[0] == (0, 5) ? null : $"区間 {seg[0]} ≠ (0, 5)";
        });

        yield return ("合成: 死んだ tick で区間が切れる（枠が空く）", () =>
        {
            var seg = Compute(bulletSlot, 5, [1, 1, 0, 1, 1], null, null, null, true, 1);
            if (seg.Count != 2) return $"区間 {seg.Count} 本（2 本のはず）";
            return seg[0] == (0, 1) && seg[1] == (3, 4) ? null : $"区間 {seg[0]} / {seg[1]}";
        });

        yield return ("合成: 弾の state 5 → 1 は解放して確保し直した印", () =>
        {
            var st = new uint[] { 1, AnalysisTables.CoordBulletStateVanish, 1, 1 };
            var seg = Compute(bulletSlot, st.Length, st, null, null, null, true, 1);
            if (seg.Count != 2) return $"区間 {seg.Count} 本（2 本のはず）";
            return seg[0] == (0, 1) && seg[1] == (2, 3) ? null : $"区間 {seg[0]} / {seg[1]}";
        });

        yield return ("合成: 弾でない枠には state 5 の印を掛けない", () =>
        {
            var st = new uint[] { 1, AnalysisTables.CoordBulletStateVanish, 1, 1 };
            var seg = Compute(AnalysisTables.CoordBaseP1Enemy, st.Length, st, null, null, null, false, 1);
            return seg.Count == 1 ? null : $"区間 {seg.Count} 本（1 本のはず。弾の印が漏れている）";
        });

        yield return ("合成: 由来の口 ID が変われば別の弾（★両方が非 0 のときだけ）", () =>
        {
            uint s1 = 1u | (1u << AnalysisTables.CoordOriginSiteShift);
            uint s2 = 1u | (2u << AnalysisTables.CoordOriginSiteShift);
            var two = Compute(bulletSlot, 4, [s1, s1, s2, s2], null, null, null, true, 1);
            if (two.Count != 2) return $"口が変わったのに区間 {two.Count} 本";
            var one = Compute(bulletSlot, 4, [1u, 1u, s2, s2], null, null, null, true, 1);
            return one.Count == 1 ? null : $"site_id 0 → 非 0 で切れた（区間 {one.Count} 本）";
        });

        yield return ($"合成: 座標が {ReuseJump} 以上跳んだら別のもの（★側を持たない枠には掛けない）", () =>
        {
            uint[] xs = [F(0f), F(0f), F((float)ReuseJump), F((float)ReuseJump)];
            uint[] ys = [F(0f), F(0f), F(0f), F(0f)];
            var cut = Compute(bulletSlot, 4, [1, 1, 1, 1], null, xs, ys, true, 1);
            if (cut.Count != 2) return $"跳んだのに区間 {cut.Count} 本";
            var keep = Compute(bulletSlot, 4, [1, 1, 1, 1], null, xs, ys, true, null);
            return keep.Count == 1 ? null : $"側の無い枠でも跳びで切れた（区間 {keep.Count} 本）";
        });

        yield return ("合成: 列が無い／空なら区間ゼロ（★『1 本』を作らない）", () =>
        {
            if (Compute(bulletSlot, 4, null, null, null, null, true, 1).Count != 0) return "列が無いのに区間が出た";
            return Compute(bulletSlot, 4, [], null, null, null, true, 1).Count == 0 ? null : "空の列で区間が出た";
        });

        yield return ("合成: tickCount より列が長くても窓の外へはみ出さない", () =>
        {
            var seg = Compute(bulletSlot, 3, [1, 1, 1, 1, 1], null, null, null, true, 1);
            if (seg.Count != 1) return $"区間 {seg.Count} 本";
            return seg[0] == (0, 2) ? null : $"区間 {seg[0]} ≠ (0, 2)（窓の外まで繋いだ）";
        });

        yield return ("★否定: 旧（＝枠の再利用で切らない）なら、誕生 tick が窓の頭になってしまう", () =>
        {
            uint[] st = [1, 1, 1, 1, 1, 1];
            uint[] kd = [7, 7, 7, 9, 9, 9];
            var now = Compute(bulletSlot, st.Length, st, kd, null, null, true, 1);
            var legacy = LegacyAliveRunsOnly(bulletSlot, st.Length, st);
            if (legacy.Count != 1 || legacy[0] != (0, 5))
                return "旧実装の写しが『1 本に繋がる』になっていない（否定テストが効いていない）";
            if (BirthOf(legacy, 4) != 0)
                return "旧実装でも誕生 tick が窓の頭にならなかった（否定テストが効いていない）";
            if (now.Count != 2) return $"新は 2 区間のはず（{now.Count} 本）";
            return BirthOf(now, 4) == 3 ? null : $"新の誕生 tick {BirthOf(now, 4)} ≠ 3";
        });
    }

    private static uint F(float v) => BitConverter.SingleToUInt32Bits(v);

    private static int BirthOf(IReadOnlyList<(int Start, int End)> segs, int i)
    {
        foreach (var s in segs) if (s.Start <= i && i <= s.End) return s.Start;
        return -1;
    }

    private static List<(int Start, int End)> LegacyAliveRunsOnly(int slot, int n, uint[] st)
    {
        var outList = new List<(int Start, int End)>();
        int cur = -1;
        for (int i = 0; i < n; i++)
        {
            if (CoordRing.SlotIsAlive(slot, st[i])) { if (cur < 0) cur = i; }
            else if (cur >= 0) { outList.Add((cur, i - 1)); cur = -1; }
        }
        if (cur >= 0) outList.Add((cur, n - 1));
        return outList;
    }
}
