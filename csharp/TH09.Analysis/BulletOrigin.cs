using System.Globalization;
using System.Runtime.CompilerServices;
using TH09.Generated;

namespace TH09.Analysis;

public static class BulletOrigin
{

    public const string OriginEnemy = "enemy";
    public const string OriginWhiteBullet = "white_bullet";
    public const string OriginGhostPenalty = "ghost_penalty";
    public const string OriginBulletItem = "bullet_item";
    public const string OriginEx = "ex";


    public const string TierBoss = "boss";
    public const string TierEx = "ex";
    public const string TierCard = "card";
    public const string TierLily = "lily";
    public const string TierOther = "other";
    public const string TierWhiteOrigin = "white_origin";
    public const string TierGhostPenalty = "ghost_penalty";


    private static readonly Dictionary<uint, string> Sites = LoadSites();
    private static readonly Dictionary<string, string> Labels = LoadLabels();
    private static readonly Dictionary<int, (string Name, int Owner)> Sprites = LoadSprites();
    private static readonly HashSet<int> DegenerateChars = LoadDegenerateChars();
    private static readonly Dictionary<uint, (string EnemyClass, int? CardLevel)> ForcedClass = LoadForcedClass();

    public static int SiteCount => Sites.Count;
    public static int LabelCount => Labels.Count;
    public static int SpriteCount => Sprites.Count;

    public static int DegenerateCharCount => DegenerateChars.Count;

    public static int ForcedClassCount => ForcedClass.Count;

    private static Dictionary<uint, string> LoadSites()
    {
        var map = new Dictionary<uint, string>();
        foreach (var line in Packed.Lines(AnalysisTables.BulletOriginSitesPacked))
        {
            var f = line.Split('\t');
            map[uint.Parse(f[0], CultureInfo.InvariantCulture)] = f[1];
        }
        return map;
    }

    private static Dictionary<string, string> LoadLabels()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in Packed.Lines(AnalysisTables.BulletOriginLabelsPacked))
        {
            var f = line.Split('\t');
            map[f[0]] = f[1];
        }
        return map;
    }

    private static Dictionary<int, (string Name, int Owner)> LoadSprites()
    {
        var map = new Dictionary<int, (string, int)>();
        foreach (var line in Packed.Lines(AnalysisTables.BulletSpritesPacked))
        {
            var f = line.Split('\t');
            map[int.Parse(f[0], CultureInfo.InvariantCulture)] =
                (f[1], int.Parse(f[2], CultureInfo.InvariantCulture));
        }
        return map;
    }

    private static HashSet<int> LoadDegenerateChars()
    {
        var set = new HashSet<int>();
        foreach (var line in Packed.Lines(AnalysisTables.CardLevelDegenerateCharsPacked))
            set.Add(int.Parse(line, CultureInfo.InvariantCulture));
        return set;
    }

    private static Dictionary<uint, (string, int?)> LoadForcedClass()
    {
        var map = new Dictionary<uint, (string, int?)>();
        foreach (var line in Packed.Lines(AnalysisTables.BulletOriginForcedClassPacked))
        {
            var f = line.Split('\t');
            map[uint.Parse(f[0], CultureInfo.InvariantCulture)] =
                (f[1], f[2].Length == 0 ? null : int.Parse(f[2], CultureInfo.InvariantCulture));
        }
        return map;
    }


    public static uint SiteId(uint state) =>
        (state >> AnalysisTables.CoordOriginSiteShift) & AnalysisTables.CoordOriginSiteMask;

    public static BulletOriginParts Parts(uint state)
    {
        uint siteId = SiteId(state);
        uint rawEnemy = (state >> AnalysisTables.CoordOriginEnemyShift) & AnalysisTables.CoordOriginEnemyMask;
        Sites.TryGetValue(siteId, out var origin);
        return new BulletOriginParts(
            siteId, origin, LabelOf(origin),
            origin == OriginEnemy && rawEnemy != 0 ? (int)rawEnemy - 1 : null);
    }

    public static string? LabelOf(string? origin) =>
        origin is not null && Labels.TryGetValue(origin, out var ja) ? ja : null;

    public static BulletSpriteParts SpriteParts(int sprite)
    {
        if (!Sprites.TryGetValue(sprite, out var row)) return new BulletSpriteParts(sprite, null, null, false);
        bool shared = row.Owner < 0;
        return new BulletSpriteParts(sprite, row.Name, shared ? null : row.Owner, shared);
    }


    public static string? Tier(string? origin, string? enemyClass)
    {
        if (origin is null) return null;
        if (origin == OriginEx) return TierEx;
        if (origin == OriginWhiteBullet) return TierWhiteOrigin;
        if (origin == OriginEnemy)
        {
            if (enemyClass == AnalysisTables.EnemyClassBoss) return TierBoss;
            if (enemyClass == AnalysisTables.EnemyClassC2C3) return TierCard;
            if (enemyClass == AnalysisTables.EnemyClassLily) return TierLily;
            return TierOther;
        }
        if (origin == OriginGhostPenalty) return TierGhostPenalty;
        return TierOther;
    }


    public static int? GlobalEnemySlot(int side, int n, int slotCount)
    {
        if (n < 0 || n >= AnalysisTables.CoordEnemySlots) return null;
        int b = side switch
        {
            1 => AnalysisTables.CoordBaseP1Enemy,
            2 => AnalysisTables.CoordBaseP2Enemy,
            _ => -1,
        };
        if (b < 0) return null;
        int gs = b + n;
        return gs < slotCount && gs < CoordRing.SlotCount ? gs : null;
    }


    private const int CardLevel2 = 2;
    private const int CardLevel3 = 3;

    public static int? CardLevelOf(uint kind, int? foeChar, IReadOnlyList<int>? classCounts)
    {
        uint cat = ((kind & AnalysisTables.CoordEnemyKindWordMask) >> AnalysisTables.CoordEnemyCatShift)
                   & AnalysisTables.CoordEnemyCatMask;
        if (foeChar is null || !DegenerateChars.Contains(foeChar.Value))
            return cat == AnalysisTables.EnemyCatC2 ? CardLevel2
                 : cat == AnalysisTables.EnemyCatC3 ? CardLevel3 : null;
        if (classCounts is null) return null;
        int c2 = classCounts[0], c3 = classCounts[1];
        if (c2 != 0 && c3 == 0) return CardLevel2;
        if (c3 != 0 && c2 == 0) return CardLevel3;
        return null;
    }

    public static int[] UnpackClassCounts(uint word)
    {
        int bits = AnalysisTables.EnemyClassCountsBits;
        uint mask = (1u << bits) - 1u;
        var outv = new int[AnalysisTables.EnemyClassCountsFields];
        for (int k = 0; k < outv.Length; k++) outv[k] = (int)((word >> (k * bits)) & mask);
        return outv;
    }

    private static int? MainInt(double? v) => v is null ? null : (int)Math.Round(v.Value);

    private static uint? MainWord(double? v) => v is null ? null : (uint)Math.Round(v.Value);


    public static BulletOriginInfo? At(IBulletOriginSource src, int slot, int i)
    {
        if (!src.IsBulletSlot(slot)) return null;
        var st = src.Raw(CoordRing.ColState(slot), i);
        if (st is null) return null;
        var parts = Parts(st.Value);
        var got = new BulletOriginInfo
        {
            Origin = parts.Origin,
            Label = parts.Label,
            SiteId = parts.SiteId,
            Recorded = parts.SiteId != 0 || src.HasBulletOrigin(),
        };
        if (ForcedClass.TryGetValue(parts.SiteId, out var forced))
        {
            got.EnemyClass = forced.EnemyClass;
            got.CardLevel = forced.CardLevel;
            return got;
        }
        if (parts.EnemySlot is not int n) return got;
        int? side = src.SideOf(slot);
        int? gs = side is null ? null : GlobalEnemySlot(side.Value, n, src.SlotCount);
        got.EnemySlot = gs;
        if (gs is null) return got;
        var est = src.Raw(CoordRing.ColState(gs.Value), i);
        if (est is null || !CoordRing.SlotIsAlive(gs.Value, est.Value)) return got;
        var kd = src.Raw(CoordRing.ColKind(gs.Value), i);
        if (kd is null) return got;
        got.EnemyKind = kd;
        var cls = CoordRing.EnemyClass(kd.Value);
        if (cls != AnalysisTables.EnemyClassBoss
            && (kd.Value & AnalysisTables.CoordEnemyLauncherBit) != 0 && side is int launcherSide)
        {
            var launcherFoe = MainInt(src.MainAt(launcherSide == 1 ? TickWords.Record.P2Character
                                                                   : TickWords.Record.P1Character, i));
            if (launcherFoe == AnalysisTables.CharSakuya) cls = AnalysisTables.EnemyClassBoss;
        }
        got.EnemyClass = cls;
        if (got.EnemyClass == AnalysisTables.EnemyClassBoss)
        {
            got.BossSub = src.MainAt(side!.Value == 1 ? TickWords.Record.P1BossSub
                                                      : TickWords.Record.P2BossSub, i);
        }
        else if (got.EnemyClass == AnalysisTables.EnemyClassC2C3)
        {
            int sd = side!.Value;
            var foe = MainInt(src.MainAt(sd == 1 ? TickWords.Record.P2Character
                                                 : TickWords.Record.P1Character, i));
            var word = MainWord(src.MainAt(sd == 1 ? TickWords.Record.P1EnemyClassCounts
                                                   : TickWords.Record.P2EnemyClassCounts, i));
            got.CardLevel = CardLevelOf(kd.Value, foe,
                                        word is uint w ? UnpackClassCounts(w) : null);
        }
        return got;
    }

    public static BulletOriginInfo? At(Window w, int slot, int i) => At(WindowOrigins.For(w), slot, i);

    public static BulletOriginInfo? AtBirth(Window w, int slot, int i)
    {
        var seg = SlotSegments.At(w, slot, i);
        var got = At(w, slot, seg?.Start ?? i);
        return AtBirthCore(got, seg?.Start);
    }

    public static BulletOriginInfo? AtBirthCore(BulletOriginInfo? got, int? segStart)
    {
        if (got is null || segStart != 0) return got;
        if (ForcedClass.ContainsKey(got.SiteId)) return got;
        got.EnemyClass = null;
        got.EnemyKind = null;
        got.CardLevel = null;
        got.BossSub = null;
        return got;
    }

    public static bool HasRecorded(Window w) => WindowOrigins.For(w).HasBulletOrigin();


    public static IEnumerable<(string Label, Func<string?> Body)> SelfTestCases()
    {
        yield return ($"母数: 由来の口の表が 0 行でない（{SiteCount} 口）", () =>
            SiteCount > 0 ? null : "0 口。生成表を引けていない");

        yield return ($"母数: 由来の日本語の表が 0 行でない（{LabelCount} 語）", () =>
            LabelCount > 0 ? null : "0 語。生成表を引けていない");

        yield return ($"母数: sprite の呼称の表が 0 行でない（{SpriteCount} 種）", () =>
            SpriteCount > 0 ? null : "0 種。生成表を引けていない");

        yield return ("母数: 口の表に出る鍵が、C# 側の名前とちょうど一致する（★片方が古くならないように）", () =>
        {
            var fromTable = new HashSet<string>(Sites.Values, StringComparer.Ordinal);
            var here = new HashSet<string>(
                new[] { OriginEnemy, OriginWhiteBullet, OriginGhostPenalty, OriginBulletItem, OriginEx },
                StringComparer.Ordinal);
            var missing = fromTable.Except(here).ToList();
            var extra = here.Except(fromTable).ToList();
            if (missing.Count > 0) return $"表にあって C# に無い鍵: {string.Join(",", missing)}";
            return extra.Count == 0 ? null : $"C# にあって表に無い鍵: {string.Join(",", extra)}";
        });

        yield return ("母数: 口の表のどの鍵にも日本語がある", () =>
        {
            foreach (var key in Sites.Values)
                if (LabelOf(key) is null) return $"鍵 {key} に日本語が無い";
            return null;
        });

        yield return ("母数: 口 ID はどれも由来のビット幅（5 ビット）に収まる", () =>
        {
            foreach (var id in Sites.Keys)
                if (id == 0 || (id & AnalysisTables.CoordOriginSiteMask) != id)
                    return $"口 ID {id} がビット幅に収まらない（0 も口として載っていてはいけない）";
            return null;
        });

        yield return ("合成: state のビットから口 ID と敵の枠番号（★+1 の戻し）が解ける", () =>
        {
            uint state = Compose(1, siteId: 2, enemyPlus1: 6);
            var p = Parts(state);
            if (p.SiteId != 2) return $"口 ID {p.SiteId} ≠ 2";
            if (p.Origin != OriginEnemy) return $"由来 {p.Origin} ≠ {OriginEnemy}";
            return p.EnemySlot == 5 ? null : $"敵の枠 {p.EnemySlot} ≠ 5（+1 を戻していない）";
        });

        yield return ("合成: 記録上の +1 があるので、枠 0 は 1 として入る（★0 は「記録が無い」）", () =>
        {
            if (Parts(Compose(1, 2, enemyPlus1: 1)).EnemySlot != 0) return "枠 0 を解けていない";
            return Parts(Compose(1, 2, enemyPlus1: 0)).EnemySlot is null
                ? null : "raw 0 を枠 -1 として返した";
        });

        yield return ("合成: enemy 以外の口には敵の枠を入れない（ビットが立っていても無視）", () =>
        {
            uint exSite = Sites.First(kv => kv.Value == OriginEx).Key;
            var p = Parts(Compose(1, exSite, enemyPlus1: 9));
            if (p.Origin != OriginEx) return $"由来 {p.Origin} ≠ {OriginEx}";
            return p.EnemySlot is null ? null : $"enemy でないのに敵の枠 {p.EnemySlot} が入った";
        });

        yield return ("合成: 口 ID 0 は由来なし（★『由来が無い』と言い切らない）", () =>
        {
            var p = Parts(1u);
            if (p.SiteId != 0 || p.Origin is not null || p.Label is not null) return "0 に由来が付いた";
            return Tier(p.Origin, null) is null ? null : "記録の無い弾が段に落ちた";
        });

        yield return ($"合成: 表に無い sprite は名前が null（未観測。母数 {SpriteCount} 種）", () =>
        {
            int unknown = 0;
            while (Sprites.ContainsKey(unknown)) unknown++;
            var s = SpriteParts(unknown);
            if (s.Name is not null) return $"sprite {unknown} に名前 {s.Name} が付いた";
            return !s.Shared ? null : "知らない sprite を「共通弾」と答えた";
        });

        yield return ("合成: 共通弾は Shared が真で持ち主が null、持ち主つきはその逆", () =>
        {
            var sharedKey = Sprites.Where(kv => kv.Value.Owner < 0).Select(kv => (int?)kv.Key).FirstOrDefault();
            var ownedKey = Sprites.Where(kv => kv.Value.Owner >= 0).Select(kv => (int?)kv.Key).FirstOrDefault();
            if (sharedKey is null || ownedKey is null)
                return "共通弾か持ち主つきが表に 1 つも無い（母数が足りない）";
            var a = SpriteParts(sharedKey.Value);
            if (!a.Shared || a.Owner is not null) return $"共通弾 {sharedKey} が Shared={a.Shared} Owner={a.Owner}";
            var b = SpriteParts(ownedKey.Value);
            return !b.Shared && b.Owner == Sprites[ownedKey.Value].Owner
                ? null : $"持ち主つき {ownedKey} が Shared={b.Shared} Owner={b.Owner}";
        });

        yield return ("合成: 段の鍵が 7 通りすべて出る（★『その他』へ潰れていない）", () =>
        {
            var want = new HashSet<string>(
                new[] { TierBoss, TierEx, TierCard, TierLily, TierOther, TierWhiteOrigin, TierGhostPenalty },
                StringComparer.Ordinal);
            var got = new HashSet<string>(StringComparer.Ordinal);
            void Add(string? t) { if (t is not null) got.Add(t); }
            Add(Tier(OriginEnemy, AnalysisTables.EnemyClassBoss));
            Add(Tier(OriginEnemy, AnalysisTables.EnemyClassC2C3));
            Add(Tier(OriginEnemy, AnalysisTables.EnemyClassLily));
            Add(Tier(OriginEnemy, AnalysisTables.EnemyClassFairy));
            Add(Tier(OriginEx, null));
            Add(Tier(OriginWhiteBullet, null));
            Add(Tier(OriginGhostPenalty, null));
            Add(Tier(OriginBulletItem, null));
            if (!want.SetEquals(got))
                return $"段 {got.Count} 種（{string.Join(",", got.OrderBy(x => x, StringComparer.Ordinal))}）";
            return Tier(OriginEnemy, null) == TierOther ? null : "分類不明の enemy が「その他」に落ちない";
        });

        yield return ("合成: 由来を記録していない弾（origin = null）は段に落ちない", () =>
        {
            foreach (var cls in new string?[] { null, AnalysisTables.EnemyClassBoss, AnalysisTables.EnemyClassLily })
                if (Tier(null, cls) is not null) return $"記録なしが段 {Tier(null, cls)} に落ちた";
            return null;
        });

        yield return ("合成: 敵の枠の通し番号が、列名の索引と同じ答えになる（★別の道から）", () =>
        {
            int n = 0;
            foreach (var side in new[] { 1, 2 })
            {
                var gs = GlobalEnemySlot(side, 3, CoordRing.SlotCount);
                if (gs is null) return $"側 {side} の敵 3 が引けない";
                var want = $"p{side}_e3";
                if (CoordRing.BaseName(gs.Value) != want)
                    return $"通し番号 {gs} の名前 {CoordRing.BaseName(gs.Value)} ≠ {want}";
                n++;
            }
            if (GlobalEnemySlot(1, AnalysisTables.CoordEnemySlots, CoordRing.SlotCount) is not null)
                return "敵の枠数を越えた番号が引けてしまう（由来のビットは 8 ビット＝最大 254）";
            return n == 2 ? null : $"母数 {n} ≠ 2";
        });

        yield return ("合成: 撃った枠が生きていれば分類とボスの sub まで出る", () =>
        {
            var src = FakeShooter(alive: true, enemyKind: AnalysisTables.CoordEnemyBossMask, bossSub: 7);
            var got = At(src, FakeBulletSlot, 0);
            if (got is null) return "弾枠なのに null が返った";
            if (got.EnemyClass != AnalysisTables.EnemyClassBoss) return $"分類 {got.EnemyClass}";
            if (got.EnemySlot != FakeEnemySlot) return $"敵の通し番号 {got.EnemySlot} ≠ {FakeEnemySlot}";
            return got.BossSub == 7 ? null : $"boss_sub {got.BossSub} ≠ 7";
        });

        yield return ("★否定: 旧（＝撃った枠の生死を見ずに kind を読む）なら、死んだ枠が「妖精」になる", () =>
        {
            var legacy = CoordRing.EnemyClass(0);
            if (legacy != AnalysisTables.EnemyClassFairy)
                return $"旧のやり方でも妖精にならなかった（{legacy}）——否定テストが効いていない";
            var src = FakeShooter(alive: false, enemyKind: 0, bossSub: 7);
            var got = At(src, FakeBulletSlot, 0);
            if (got is null) return "弾枠なのに null が返った";
            if (got.Origin != OriginEnemy) return $"由来 {got.Origin} ≠ {OriginEnemy}（合成が効いていない）";
            if (got.EnemySlot != FakeEnemySlot) return $"敵の通し番号 {got.EnemySlot}（枠は指せているはず）";
            if (got.EnemyKind is not null) return $"死んだ枠の kind {got.EnemyKind} を読んだ";
            return got.EnemyClass is null ? null : $"新でも分類 {got.EnemyClass} が付いた（妖精に倒している）";
        });

        yield return ("合成: 記録していない窓でも Recorded が真を名乗らない", () =>
        {
            var src = new FakeSource
            {
                SlotCount = CoordRing.SlotCount,
                Bullets = { FakeBulletSlot },
                Sides = { [FakeBulletSlot] = 1 },
                Origin = false,
                Cols = { [CoordRing.ColState(FakeBulletSlot)] = 1u },
            };
            var got = At(src, FakeBulletSlot, 0);
            if (got is null) return "弾枠なのに null が返った";
            if (got.Recorded) return "site_id 0 かつ窓に由来が無いのに Recorded が真";
            src.Origin = true;
            var got2 = At(src, FakeBulletSlot, 0);
            return got2 is not null && got2.Recorded ? null : "窓が由来を運んでいるのに Recorded が偽";
        });

        yield return ($"母数: C2 と C3 が縮退するキャラの表が 0 行でない（{DegenerateCharCount} 人）", () =>
            DegenerateCharCount > 0 ? null : "0 人。生成表を引けていない");

        yield return ("母数: 敵の分類の数のほどき方（★幅も本数も生成表から。区画が隣へ漏れない）", () =>
        {
            int bits = AnalysisTables.EnemyClassCountsBits;
            uint mask = (1u << bits) - 1u;
            var one = UnpackClassCounts(mask);
            if (one.Length != AnalysisTables.EnemyClassCountsFields) return $"区画 {one.Length} 本";
            if (one[0] != (int)mask) return $"C2 の数 {one[0]} ≠ {mask}";
            for (int k = 1; k < one.Length; k++) if (one[k] != 0) return $"区画 {k} へ漏れた（{one[k]}）";
            var two = UnpackClassCounts(mask << bits);
            return two[1] == (int)mask && two[0] == 0 ? null : $"区画 1 を取り違えた（{two[0]}, {two[1]}）";
        });

        yield return ("合成: 縮退しないキャラは enemy_cat だけで 2 / 3 に決まる（数を渡さなくても）", () =>
        {
            int normal = 0;
            while (DegenerateChars.Contains(normal)) normal++;
            if (CardLevelOf(KindOfCat(AnalysisTables.EnemyCatC2), normal, null) != 2) return "C2 が 2 にならない";
            if (CardLevelOf(KindOfCat(AnalysisTables.EnemyCatC3), normal, null) != 3) return "C3 が 3 にならない";
            return CardLevelOf(KindOfCat(AnalysisTables.EnemyCatC2), null, null) == 2
                ? null : "キャラ不明のとき列挙の道に落ちていない";
        });

        yield return ($"合成: 縮退キャラは同じ tick の数で決める（★両方 > 0 なら決めない。母数 {DegenerateCharCount} 人）", () =>
        {
            uint kind = KindOfCat(AnalysisTables.EnemyCatC2);
            foreach (var ch in DegenerateChars)
            {
                if (CardLevelOf(kind, ch, UnpackClassCounts(ComposeCounts(2, 0, 0))) != 2)
                    return $"キャラ {ch}: C2 だけ居るのに 2 にならない";
                if (CardLevelOf(kind, ch, UnpackClassCounts(ComposeCounts(0, 2, 0))) != 3)
                    return $"キャラ {ch}: C3 だけ居るのに 3 にならない";
                if (CardLevelOf(kind, ch, UnpackClassCounts(ComposeCounts(9, 1, 0))) is int both)
                    return $"キャラ {ch}: 両方 > 0 なのに {both} に倒した";
                if (CardLevelOf(kind, ch, null) is int noCounts)
                    return $"キャラ {ch}: 数を渡していないのに {noCounts} を返した";
            }
            return null;
        });

        yield return ("合成: enemy_cat が C2 でも C3 でもなければ null（★0 や 3 を 2 に倒さない）", () =>
        {
            foreach (uint cat in new uint[] { 0u, AnalysisTables.CoordEnemyCatMask })
            {
                if (cat == AnalysisTables.EnemyCatC2 || cat == AnalysisTables.EnemyCatC3) continue;
                if (CardLevelOf(KindOfCat(cat), null, null) is int got)
                    return $"cat {cat} に レベル {got} が付いた";
            }
            return null;
        });

        yield return ("合成: At() の C2/C3 の枝が CardLevel まで埋める（★段は card）", () =>
        {
            var src = FakeShooter(alive: true, enemyKind: KindOfCat(AnalysisTables.EnemyCatC2), bossSub: 0);
            src.Mains[TickWords.Record.P2Character] = DegenerateChars.First();
            src.Mains[TickWords.Record.P1EnemyClassCounts] = ComposeCounts(0, 4, 0);
            var got = At(src, FakeBulletSlot, 0);
            if (got is null) return "弾枠なのに null が返った";
            if (got.EnemyClass != AnalysisTables.EnemyClassC2C3) return $"分類 {got.EnemyClass}";
            if (got.CardLevel != 3) return $"CardLevel {got.CardLevel} ≠ 3（相手が縮退キャラで C3 だけ 4 体）";
            if (got.BossSub is not null) return $"C2/C3 なのに boss_sub {got.BossSub} が入った";
            return Tier(got.Origin, got.EnemyClass) == TierCard ? null : "段が card にならない";
        });

        yield return ("★否定: 旧（＝縮退を見ずに enemy_cat だけで決める）なら、縮退キャラで答えが食い違う", () =>
        {
            uint kind = KindOfCat(AnalysisTables.EnemyCatC2);
            int? legacy = LegacyCatOnly(kind);
            if (legacy != 2) return $"旧の写しが列挙だけで 2 を返していない（{legacy}）——否定テストが効いていない";
            int degen = DegenerateChars.First();
            int? now = CardLevelOf(kind, degen, UnpackClassCounts(ComposeCounts(0, 4, 0)));
            if (legacy == now) return $"縮退キャラでも旧と同じ答え（{now}）——否定テストが効いていない";
            if (now != 3) return $"新 {now} ≠ 3";
            int normal = 0;
            while (DegenerateChars.Contains(normal)) normal++;
            return CardLevelOf(kind, normal, null) == legacy
                ? null : "縮退しないキャラで新旧がずれた（列挙の道を通っていない）";
        });

        yield return ("合成: AtBirthCore は窓の頭（segStart==0）だけ分類を null に倒す", () =>
        {
            BulletOriginInfo Fresh() => new()
            {
                Origin = OriginEnemy, Label = "敵の弾", SiteId = 2, Recorded = true,
                EnemySlot = FakeEnemySlot, EnemyClass = AnalysisTables.EnemyClassFairy,
                EnemyKind = 123u, CardLevel = 2, BossSub = 4.0,
            };
            var head = AtBirthCore(Fresh(), segStart: 0);
            if (head is null) return "窓の頭で null が返った（弾自体の由来まで消した）";
            if (head.EnemyClass is not null) return $"窓の頭なのに分類 {head.EnemyClass} が残った";
            if (head.EnemyKind is not null) return $"窓の頭なのに EnemyKind {head.EnemyKind} が残った";
            if (head.CardLevel is not null) return $"窓の頭なのに CardLevel {head.CardLevel} が残った";
            if (head.BossSub is not null) return $"窓の頭なのに BossSub {head.BossSub} が残った";
            if (head.Origin != OriginEnemy || head.EnemySlot != FakeEnemySlot || !head.Recorded)
                return "窓の頭で由来（Origin/EnemySlot/Recorded）まで消えた";

            var mid = AtBirthCore(Fresh(), segStart: 5);
            if (mid!.EnemyClass != AnalysisTables.EnemyClassFairy) return "窓の頭でない区間の分類まで消えた";

            var noSeg = AtBirthCore(Fresh(), segStart: null);
            return noSeg!.EnemyClass == AnalysisTables.EnemyClassFairy
                ? null : "区間が引けない（segStart=null）のに分類が消えた（別の話に手を出した）";
        });

        yield return ("★否定: 旧（＝窓の頭でも分類をそのまま出す）なら、存在しない分類を騙ったままになる", () =>
        {
            static BulletOriginInfo? LegacyAtBirthCore(BulletOriginInfo? got, int? _) => got;
            var legacy = LegacyAtBirthCore(new BulletOriginInfo
            {
                Origin = OriginEnemy, Label = "敵の弾", SiteId = 2, Recorded = true,
                EnemyClass = AnalysisTables.EnemyClassFairy,
            }, 0);
            if (legacy!.EnemyClass != AnalysisTables.EnemyClassFairy)
                return "旧の写しが分類を残していない——否定テストが効いていない";
            var now = AtBirthCore(new BulletOriginInfo
            {
                Origin = OriginEnemy, Label = "敵の弾", SiteId = 2, Recorded = true,
                EnemyClass = AnalysisTables.EnemyClassFairy,
            }, 0);
            return now!.EnemyClass is null ? null : "新でも窓の頭の分類が残った（妖精を騙ったまま）";
        });

        yield return ($"母数: 撃った敵を持たない口の表が 0 行でない（{ForcedClassCount} 口）", () =>
            ForcedClassCount > 0 ? null : "0 口。生成表を引けていない");

        yield return ("合成: 撃った敵を持たない口（ミスティアの設置物）は敵の枠のビットが立っていてもそれより先に分岐する（優先度）", () =>
        {
            uint c2Site = ForcedClass.First(kv => kv.Value.EnemyClass == AnalysisTables.EnemyClassC2C3
                                                && kv.Value.CardLevel == 2).Key;
            var src = new FakeSource
            {
                SlotCount = CoordRing.SlotCount,
                Bullets = { FakeBulletSlot },
                Sides = { [FakeBulletSlot] = 1 },
                Origin = true,
                Cols =
                {
                    [CoordRing.ColState(FakeBulletSlot)] = Compose(1, c2Site, 3 + 1),
                    [CoordRing.ColState(FakeEnemySlot)] = 1u,
                    [CoordRing.ColKind(FakeEnemySlot)] = 0u,
                },
            };
            var got = At(src, FakeBulletSlot, 0);
            if (got is null) return "弾枠なのに null が返った";
            if (got.EnemyClass != AnalysisTables.EnemyClassC2C3) return $"分類 {got.EnemyClass} ≠ c2c3";
            if (got.CardLevel != 2) return $"レベル {got.CardLevel} ≠ 2";
            if (got.EnemySlot is not null) return $"撃った敵の枠 {got.EnemySlot} を名乗った（本来は無い）";
            return got.EnemyKind is null ? null : $"EnemyKind {got.EnemyKind} を名乗った（本来は無い）";
        });

        yield return ("★否定: 旧（優先度が無く、敵の枠をそのまま読む）なら、ミスティアの設置物の弾も撃った敵（妖精）を読んでしまう", () =>
        {
            uint c2Site = ForcedClass.First(kv => kv.Value.EnemyClass == AnalysisTables.EnemyClassC2C3
                                                && kv.Value.CardLevel == 2).Key;
            var parts = Parts(Compose(1, c2Site, 3 + 1));
            if (parts.EnemySlot != 3) return $"合成の前提が崩れている（EnemySlot {parts.EnemySlot} ≠ 3）——否定テストが効いていない";
            var legacy = CoordRing.EnemyClass(0u);
            if (legacy != AnalysisTables.EnemyClassFairy)
                return $"旧の写しが妖精を返していない（{legacy}）——否定テストが効いていない";
            return null;
        });

        yield return ("合成: AtBirthCore は撃った敵を持たない口（ForcedClass）を窓の頭でも消さない", () =>
        {
            uint bossSite = ForcedClass.First(kv => kv.Value.EnemyClass == AnalysisTables.EnemyClassBoss).Key;
            var got = new BulletOriginInfo
            {
                Origin = OriginEnemy, Label = "敵の弾", SiteId = bossSite, Recorded = true,
                EnemyClass = AnalysisTables.EnemyClassBoss,
            };
            var head = AtBirthCore(got, segStart: 0);
            return head!.EnemyClass == AnalysisTables.EnemyClassBoss ? null
                 : $"窓の頭で撃った敵を持たない口の分類が消えた（{head.EnemyClass}）";
        });

        yield return ("★否定: 旧（ForcedClass を見ずに窓の頭を一律で消す）なら、ミスティアの設置物の分類も消えてしまう", () =>
        {
            static BulletOriginInfo? LegacyAtBirthCore(BulletOriginInfo? g, int? segStart)
            {
                if (g is null || segStart != 0) return g;
                g.EnemyClass = null; g.EnemyKind = null; g.CardLevel = null; g.BossSub = null;
                return g;
            }
            uint bossSite = ForcedClass.First(kv => kv.Value.EnemyClass == AnalysisTables.EnemyClassBoss).Key;
            var legacy = LegacyAtBirthCore(new BulletOriginInfo
            {
                Origin = OriginEnemy, Label = "敵の弾", SiteId = bossSite, Recorded = true,
                EnemyClass = AnalysisTables.EnemyClassBoss,
            }, 0);
            return legacy!.EnemyClass is null ? null : "旧の写しが分類を残している——否定テストが効いていない";
        });

        yield return ("合成: 咲夜のボス s6 の子（発射台）が撃つ白弾は、送り手が咲夜のときだけボスへ寄せる", () =>
        {
            uint enemySite = Sites.First(kv => kv.Value == OriginEnemy && !ForcedClass.ContainsKey(kv.Key)).Key;
            var src = new FakeSource
            {
                SlotCount = CoordRing.SlotCount,
                Bullets = { FakeBulletSlot },
                Sides = { [FakeBulletSlot] = 1 },
                Origin = true,
                Cols =
                {
                    [CoordRing.ColState(FakeBulletSlot)] = Compose(1, enemySite, 3 + 1),
                    [CoordRing.ColState(FakeEnemySlot)] = 1u,
                    [CoordRing.ColKind(FakeEnemySlot)] = AnalysisTables.CoordEnemyLauncherBit,
                },
                Mains = { [TickWords.Record.P2Character] = AnalysisTables.CharSakuya },
            };
            var got = At(src, FakeBulletSlot, 0);
            if (got is null) return "弾枠なのに null が返った";
            if (got.EnemyKind != AnalysisTables.CoordEnemyLauncherBit) return $"EnemyKind {got.EnemyKind} が発射台ビットでない";
            return got.EnemyClass == AnalysisTables.EnemyClassBoss ? null : $"分類 {got.EnemyClass} ≠ boss";
        });

        yield return ("★否定: 送り手が咲夜でなければ同じ規則を当てない（発射台のまま「その他」）", () =>
        {
            var legacy = CoordRing.EnemyClass(AnalysisTables.CoordEnemyLauncherBit);
            if (legacy != AnalysisTables.EnemyClassOther)
                return $"素材の前提が崩れている（enemy_class(発射台ビット) が {legacy} ≠ other）";
            uint enemySite = Sites.First(kv => kv.Value == OriginEnemy && !ForcedClass.ContainsKey(kv.Key)).Key;
            int notSakuya = (AnalysisTables.CharSakuya + 1) % 16;
            var src = new FakeSource
            {
                SlotCount = CoordRing.SlotCount,
                Bullets = { FakeBulletSlot },
                Sides = { [FakeBulletSlot] = 1 },
                Origin = true,
                Cols =
                {
                    [CoordRing.ColState(FakeBulletSlot)] = Compose(1, enemySite, 3 + 1),
                    [CoordRing.ColState(FakeEnemySlot)] = 1u,
                    [CoordRing.ColKind(FakeEnemySlot)] = AnalysisTables.CoordEnemyLauncherBit,
                },
                Mains = { [TickWords.Record.P2Character] = notSakuya },
            };
            var got = At(src, FakeBulletSlot, 0);
            return got?.EnemyClass == AnalysisTables.EnemyClassOther ? null
                 : $"咲夜以外なのに分類が {got?.EnemyClass} に変わった（規則を当ててはいけない）";
        });

        yield return ("合成: 発射台ビットが無ければ咲夜でも上書きしない／もとからボスなら送り手を問わずボスのまま", () =>
        {
            uint enemySite = Sites.First(kv => kv.Value == OriginEnemy && !ForcedClass.ContainsKey(kv.Key)).Key;
            FakeSource Make(uint kind, int foeChar) => new()
            {
                SlotCount = CoordRing.SlotCount,
                Bullets = { FakeBulletSlot },
                Sides = { [FakeBulletSlot] = 1 },
                Origin = true,
                Cols =
                {
                    [CoordRing.ColState(FakeBulletSlot)] = Compose(1, enemySite, 3 + 1),
                    [CoordRing.ColState(FakeEnemySlot)] = 1u,
                    [CoordRing.ColKind(FakeEnemySlot)] = kind,
                },
                Mains = { [TickWords.Record.P2Character] = foeChar },
            };
            var noBit = At(Make(0u, AnalysisTables.CharSakuya), FakeBulletSlot, 0);
            if (noBit?.EnemyClass != AnalysisTables.EnemyClassFairy)
                return $"発射台ビットが無いのに分類が {noBit?.EnemyClass}（咲夜でも上書きしてはいけない）";
            uint alreadyBoss = AnalysisTables.CoordEnemyBossMask | AnalysisTables.CoordEnemyLauncherBit;
            int notSakuya = (AnalysisTables.CharSakuya + 1) % 16;
            var already = At(Make(alreadyBoss, notSakuya), FakeBulletSlot, 0);
            return already?.EnemyClass == AnalysisTables.EnemyClassBoss ? null
                 : $"もとからボスなのに分類が {already?.EnemyClass}";
        });
    }


    private static int FakeBulletSlot => AnalysisTables.CoordBaseP1Bullet;
    private static int FakeEnemySlot => AnalysisTables.CoordBaseP1Enemy + 3;

    private static uint Compose(uint alive, uint siteId, uint enemyPlus1) =>
        alive | (siteId << AnalysisTables.CoordOriginSiteShift)
              | (enemyPlus1 << AnalysisTables.CoordOriginEnemyShift);

    private static uint KindOfCat(uint cat) => cat << AnalysisTables.CoordEnemyCatShift;

    private static uint ComposeCounts(int c2, int c3, int boss)
    {
        int bits = AnalysisTables.EnemyClassCountsBits;
        return (uint)c2 | ((uint)c3 << bits) | ((uint)boss << (2 * bits));
    }

    private static int? LegacyCatOnly(uint kind)
    {
        uint cat = ((kind & AnalysisTables.CoordEnemyKindWordMask) >> AnalysisTables.CoordEnemyCatShift)
                   & AnalysisTables.CoordEnemyCatMask;
        return cat == AnalysisTables.EnemyCatC2 ? CardLevel2
             : cat == AnalysisTables.EnemyCatC3 ? CardLevel3 : null;
    }

    private static FakeSource FakeShooter(bool alive, uint enemyKind, double bossSub)
    {
        uint enemySite = Sites.First(kv => kv.Value == OriginEnemy && !ForcedClass.ContainsKey(kv.Key)).Key;
        return new FakeSource
        {
            SlotCount = CoordRing.SlotCount,
            Bullets = { FakeBulletSlot },
            Sides = { [FakeBulletSlot] = 1 },
            Origin = true,
            Cols =
            {
                [CoordRing.ColState(FakeBulletSlot)] = Compose(1, enemySite, 3 + 1),
                [CoordRing.ColState(FakeEnemySlot)] = alive ? 1u : 0u,
                [CoordRing.ColKind(FakeEnemySlot)] = enemyKind,
            },
            Mains = { [TickWords.Record.P1BossSub] = bossSub },
        };
    }

    private sealed class FakeSource : IBulletOriginSource
    {
        public int SlotCount { get; init; }
        public HashSet<int> Bullets { get; } = [];
        public Dictionary<int, int> Sides { get; } = new();
        public Dictionary<string, uint> Cols { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, double> Mains { get; } = new(StringComparer.Ordinal);
        public bool Origin { get; set; }

        public uint? Raw(string name, int i) => Cols.TryGetValue(name, out var v) ? v : null;
        public double? MainAt(string name, int i) => Mains.TryGetValue(name, out var v) ? v : null;
        public bool HasBulletOrigin() => Origin;
        public int? SideOf(int slot) => Sides.TryGetValue(slot, out var sd) ? sd : null;
        public bool IsBulletSlot(int slot) => Bullets.Contains(slot);
    }
}

public interface IBulletOriginSource
{
    int SlotCount { get; }
    uint? Raw(string name, int i);
    double? MainAt(string name, int i);
    bool HasBulletOrigin();
    int? SideOf(int slot);
    bool IsBulletSlot(int slot);
}

public sealed class WindowOrigins : IBulletOriginSource
{
    private readonly Window _w;
    private readonly object _gate = new();
    private bool? _seen;

    private WindowOrigins(Window w) => _w = w;

    private static readonly ConditionalWeakTable<Window, WindowOrigins> Cached = new();

    public static WindowOrigins For(Window w) => Cached.GetValue(w, static x => new WindowOrigins(x));

    public int SlotCount => _w.Meta.SlotCount;
    public uint? Raw(string name, int i) => _w.Raw(name, i);
    public double? MainAt(string name, int i) => _w.MainAt(name, i);
    public int? SideOf(int slot) => SlotSegments.SideOf(_w, slot);
    public bool IsBulletSlot(int slot) => SlotSegments.IsBullet(_w, slot);

    public bool HasBulletOrigin()
    {
        lock (_gate)
        {
            _seen ??= Scan();
            return _seen.Value;
        }
    }

    private bool Scan()
    {
        for (int side = 1; side <= 2; side++)
            foreach (var slot in _w.SlotsOf(side)["bullet"])
            {
                var name = CoordRing.ColState(slot);
                if (!_w.Cols.Has(name)) continue;
                foreach (var v in _w.Cols[name])
                    if (BulletOrigin.SiteId(v) != 0) return true;
            }
        return false;
    }
}

public readonly record struct BulletOriginParts(uint SiteId, string? Origin, string? Label, int? EnemySlot);

public readonly record struct BulletSpriteParts(int Sprite, string? Name, int? Owner, bool Shared);

public sealed class BulletOriginInfo
{
    public required string? Origin { get; init; }
    public required string? Label { get; init; }
    public required uint SiteId { get; init; }
    public required bool Recorded { get; init; }

    public int? EnemySlot { get; set; }
    public string? EnemyClass { get; set; }
    public uint? EnemyKind { get; set; }

    public int? CardLevel { get; set; }
    public double? BossSub { get; set; }
}
