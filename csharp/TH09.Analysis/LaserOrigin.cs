using TH09.Generated;

namespace TH09.Analysis;

public static class LaserOrigin
{
    public const int SubExChild = 8;
    public const int SubBossChild = 9;
    public const int SubBossDirect = 3;

    public const int CharMarisa = 1;
    public const int CharIku = 13;

    private static readonly Dictionary<(int Char, int Sub), LaserOriginKind> KindTable = new()
    {
        [(CharMarisa, SubBossChild)] = LaserOriginKind.BossChild,
        [(CharMarisa, SubExChild)] = LaserOriginKind.ExChild,
        [(CharIku, SubBossDirect)] = LaserOriginKind.BossDirect,
    };

    public static LaserOriginKind Classify(int foeCharId, int? sub) =>
        sub is int s && KindTable.TryGetValue((foeCharId, s), out var k) ? k : LaserOriginKind.Unknown;

    public static uint SubPlusOne(uint state) =>
        (state >> AnalysisTables.CoordLaserOriginSubShift) & AnalysisTables.CoordLaserOriginSubMask;

    public static uint EnemySlotPlusOne(uint state) =>
        (state >> AnalysisTables.CoordLaserOriginEnemyShift) & AnalysisTables.CoordLaserOriginEnemyMask;

    public static LaserOriginParts Parts(uint state)
    {
        uint subP1 = SubPlusOne(state);
        uint enemyP1 = EnemySlotPlusOne(state);
        return new LaserOriginParts(
            subP1 == 0 ? null : (int)subP1 - 1,
            subP1 == AnalysisTables.CoordLaserOriginSubMask,
            enemyP1 == 0 ? null : (int)enemyP1 - 1);
    }

    public static int? AttackSubAtChildBirth(Window w, int side, int laserSlot, LaserOriginParts origin, int i)
    {
        if (origin.EnemySlot is not int localSlot) return null;
        var enemySlot = BulletOrigin.GlobalEnemySlot(side, localSlot, w.Meta.SlotCount);
        if (enemySlot is null) return null;
        var laserSeg = SlotSegments.At(w, laserSlot, i);
        int laserBirth = laserSeg?.Start ?? i;
        var childSeg = SlotSegments.At(w, enemySlot.Value, laserBirth);
        if (childSeg is not { } seg || seg.Start == 0) return null;
        if (w.MainAt(side == 1 ? TickWords.Record.P1BossSub : TickWords.Record.P2BossSub, seg.Start)
            is not double av) return null;
        return (int)av;
    }

    public static int? AttackSub(Window w, int side, int laserSlot, int foeCharId, LaserOriginParts origin, int i) =>
        Classify(foeCharId, origin.Sub) switch
        {
            LaserOriginKind.BossChild => AttackSubAtChildBirth(w, side, laserSlot, origin, i),
            LaserOriginKind.BossDirect => origin.Sub,
            _ => null,
        };


    private static uint Compose(uint alive, uint subPlus1, uint enemyPlus1) =>
        alive | (subPlus1 << AnalysisTables.CoordLaserOriginSubShift)
              | (enemyPlus1 << AnalysisTables.CoordLaserOriginEnemyShift);

    public static IEnumerable<(string Label, Func<string?> Body)> SelfTestCases()
    {
        yield return ("母数: ビット位置は弾の由来（COORD_ORIGIN_*）と同じ値", () =>
            AnalysisTables.CoordLaserOriginSubShift == AnalysisTables.CoordOriginSiteShift
            && AnalysisTables.CoordLaserOriginSubMask == AnalysisTables.CoordOriginSiteMask
            && AnalysisTables.CoordLaserOriginEnemyShift == AnalysisTables.CoordOriginEnemyShift
            && AnalysisTables.CoordLaserOriginEnemyMask == AnalysisTables.CoordOriginEnemyMask
                ? null : "レーザーの由来のビット位置が弾の由来と食い違いました（生成表がずれています）");

        yield return ("合成: state のビットから sub+1 と敵の枠+1（★+1 の戻し）が解ける", () =>
        {
            uint state = Compose(1, subPlus1: 10, enemyPlus1: 6);
            var p = Parts(state);
            if (p.Sub != 9) return $"sub {p.Sub} ≠ 9（sub+1=10 の戻し）";
            if (p.SubFolded) return "sub 9 なのに寄せ扱い（SubFolded）になった";
            return p.EnemySlot == 5 ? null : $"敵の枠 {p.EnemySlot} ≠ 5（+1 を戻していない）";
        });

        yield return ("合成: 記録上の +1 があるので、枠 0 は 1 として入る（★0 は『不明』）", () =>
        {
            if (Parts(Compose(1, subPlus1: 2, enemyPlus1: 1)).EnemySlot != 0) return "枠 0 を解けていない";
            return Parts(Compose(1, subPlus1: 2, enemyPlus1: 0)).EnemySlot is null
                ? null : "raw 0 を枠 -1 として返した";
        });

        yield return ("合成: sub は寄せ先（マスク一杯）で SubFolded が立ち、それ未満では立たない", () =>
        {
            var below = Parts(Compose(1, subPlus1: AnalysisTables.CoordLaserOriginSubMask - 1, enemyPlus1: 0));
            if (below.SubFolded) return "寄せ先の 1 つ手前で SubFolded が立った";
            var folded = Parts(Compose(1, subPlus1: AnalysisTables.CoordLaserOriginSubMask, enemyPlus1: 0));
            if (!folded.SubFolded) return "寄せ先（マスク一杯）で SubFolded が立たない";
            return folded.Sub == (int)AnalysisTables.CoordLaserOriginSubMask - 1
                ? null : $"寄せ先の Sub が {folded.Sub}（マスク-1 のはず）";
        });

        yield return ("合成: 由来のビットが 0 なら sub・敵の枠とも不明（null）", () =>
        {
            var p = Parts(1u);
            return p.Sub is null && p.EnemySlot is null && !p.SubFolded
                ? null : $"由来なしなのに sub={p.Sub} enemy={p.EnemySlot} folded={p.SubFolded}";
        });

        yield return ("★否定: 旧（＝+1 を戻さない）なら、枠番号が 1 つずれる", () =>
        {
            static int? LegacyEnemySlot(uint state) =>
                EnemySlotPlusOne(state) is 0 ? null : (int)EnemySlotPlusOne(state);
            var legacy = LegacyEnemySlot(Compose(1, 2, enemyPlus1: 1));
            var now = Parts(Compose(1, 2, enemyPlus1: 1)).EnemySlot;
            if (legacy != 1) return $"旧の写しが 1 を返していません（{legacy}）——否定テストが効いていない";
            return legacy != now ? null : "旧のやり方でも新と同じ枠番号でした（この試験は空振りです）";
        });


        yield return ("合成: 攻撃 sub は「子の敵」が生まれた tick の値を使う（レーザー自身の誕生ではない）", () =>
        {
            var w = LaserChildWindow(tickCount: 12, side: 1,
                                     laserAliveFrom: 8, laserAliveTo: 11,
                                     enemyLocalSlot: 5, enemyAliveFrom: 2, enemyAliveTo: 11,
                                     bossSub: new Dictionary<int, double> { [2] = 6.0, [6] = 2.0 });
            int laserSlot = AnalysisTables.CoordBaseP1Laser;
            var origin = new LaserOriginParts(SubBossChild, false, 5);
            var got = AttackSubAtChildBirth(w, 1, laserSlot, origin, 8);
            return got == 6 ? null : $"{got}（6 のはず。レーザー自身の誕生 tick=8 の boss_sub=2 を読んでいないか確かめること）";
        });

        yield return ("★否定: レーザー自身の誕生 tick で読む旧のやり方（AttackSubAtBirth）なら、2 を返してしまう", () =>
        {
            var w = LaserChildWindow(tickCount: 12, side: 1,
                                     laserAliveFrom: 8, laserAliveTo: 11,
                                     enemyLocalSlot: 5, enemyAliveFrom: 2, enemyAliveTo: 11,
                                     bossSub: new Dictionary<int, double> { [2] = 6.0, [6] = 2.0 });
            int laserSlot = AnalysisTables.CoordBaseP1Laser;
            static int? LegacyAtLaserBirth(Window w2, int side2, int slot2, int i2)
            {
                var seg = SlotSegments.At(w2, slot2, i2);
                int at = seg?.Start ?? i2;
                return w2.MainAt(side2 == 1 ? TickWords.Record.P1BossSub : TickWords.Record.P2BossSub, at)
                    is double av ? (int)av : null;
            }
            var legacy = LegacyAtLaserBirth(w, 1, laserSlot, 8);
            var origin = new LaserOriginParts(SubBossChild, false, 5);
            var now = AttackSubAtChildBirth(w, 1, laserSlot, origin, 8);
            if (legacy != 2) return $"旧の写しが 2 を返していません（{legacy}）——否定テストが効いていない";
            return legacy != now ? null : "旧のやり方でも新と同じ値でした（この試験は空振りです）";
        });

        yield return ("合成: 由来に子の枠（EnemySlot）が無ければ null（子を辿れない）", () =>
        {
            var w = LaserChildWindow(tickCount: 12, side: 1,
                                     laserAliveFrom: 8, laserAliveTo: 11,
                                     enemyLocalSlot: 5, enemyAliveFrom: 2, enemyAliveTo: 11,
                                     bossSub: new Dictionary<int, double> { [2] = 6.0 });
            int laserSlot = AnalysisTables.CoordBaseP1Laser;
            var origin = new LaserOriginParts(SubBossChild, false, null);
            var got = AttackSubAtChildBirth(w, 1, laserSlot, origin, 8);
            return got is null ? null : $"子の枠が無いのに {got} を返した（null のはず）";
        });

        yield return ("合成: 子の枠の占有区間の頭が窓の外（tick 0）なら null（嘘の値を運ばない）", () =>
        {
            var w = LaserChildWindow(tickCount: 12, side: 1,
                                     laserAliveFrom: 8, laserAliveTo: 11,
                                     enemyLocalSlot: 5, enemyAliveFrom: 0, enemyAliveTo: 11,
                                     bossSub: new Dictionary<int, double> { [0] = 2.0, [6] = 6.0 });
            int laserSlot = AnalysisTables.CoordBaseP1Laser;
            var origin = new LaserOriginParts(SubBossChild, false, 5);
            var got = AttackSubAtChildBirth(w, 1, laserSlot, origin, 8);
            return got is null ? null : $"子が窓の頭からの枠なのに {got} を返した（null のはず）";
        });

        yield return ("★否定: 5・6 以外に丸めない（子の誕生 tick の値をそのまま返す。映姫の sub 3 が消えないこと）", () =>
        {
            var w = LaserChildWindow(tickCount: 12, side: 1,
                                     laserAliveFrom: 8, laserAliveTo: 11,
                                     enemyLocalSlot: 5, enemyAliveFrom: 2, enemyAliveTo: 11,
                                     bossSub: new Dictionary<int, double> { [2] = 3.0 });
            int laserSlot = AnalysisTables.CoordBaseP1Laser;
            var origin = new LaserOriginParts(SubBossChild, false, 5);
            var got = AttackSubAtChildBirth(w, 1, laserSlot, origin, 8);
            return got == 3 ? null : $"{got}（3 のはず。5・6 以外を null へ丸める古いフィルタが戻っていないか確かめること）";
        });


        yield return ("Classify: 魔理沙 sub 9 は BossChild・sub 8 は ExChild", () =>
        {
            if (Classify(CharMarisa, SubBossChild) != LaserOriginKind.BossChild)
                return "魔理沙 sub 9 が BossChild にならない";
            return Classify(CharMarisa, SubExChild) == LaserOriginKind.ExChild
                ? null : "魔理沙 sub 8 が ExChild にならない";
        });

        yield return ("Classify: 映姫 sub 3 は BossDirect（子を介さない審判）", () =>
            Classify(CharIku, SubBossDirect) == LaserOriginKind.BossDirect
                ? null : "映姫 sub 3 が BossDirect にならない");

        yield return ("★否定: 表に無い組み合わせは Unknown へ落ちる（推測で塗らない）", () =>
        {
            if (Classify(CharIku, SubExChild) != LaserOriginKind.Unknown)
                return "映姫 sub 8（魔理沙の Ex 由来の値）が Unknown にならない（表に無いのに拾っている）";
            if (Classify(CharMarisa, SubBossDirect) != LaserOriginKind.Unknown)
                return "魔理沙 sub 3（映姫の直接由来の値）が Unknown にならない（表に無いのに拾っている）";
            const int reimu = 0;
            if (Classify(reimu, SubBossChild) != LaserOriginKind.Unknown)
                return "霊夢 sub 9 が Unknown にならない（16 人全称のフィルタになっていないか確かめること）";
            return Classify(CharIku, null) == LaserOriginKind.Unknown
                ? null : "sub が null なのに Unknown 以外を返した";
        });

        yield return ("AttackSub: 映姫（BossDirect）は子を辿らず、由来 sub をそのまま返す", () =>
        {
            var w = LaserChildWindow(tickCount: 12, side: 1,
                                     laserAliveFrom: 2, laserAliveTo: 11,
                                     enemyLocalSlot: 5, enemyAliveFrom: 0, enemyAliveTo: 0,
                                     bossSub: new Dictionary<int, double> { [0] = (double)SubBossDirect });
            int laserSlot = AnalysisTables.CoordBaseP1Laser;
            var origin = new LaserOriginParts(SubBossDirect, false, null);
            var got = AttackSub(w, 1, laserSlot, CharIku, origin, 2);
            return got == SubBossDirect ? null : $"{got}（{SubBossDirect} のはず。由来 sub をそのまま返すこと）";
        });

        yield return ("★否定: 映姫に AttackSubAtChildBirth を直に流用すると null になる（子が無いので辿れない）", () =>
        {
            var w = LaserChildWindow(tickCount: 12, side: 1,
                                     laserAliveFrom: 2, laserAliveTo: 11,
                                     enemyLocalSlot: 5, enemyAliveFrom: 0, enemyAliveTo: 0,
                                     bossSub: new Dictionary<int, double> { [0] = (double)SubBossDirect });
            int laserSlot = AnalysisTables.CoordBaseP1Laser;
            var origin = new LaserOriginParts(SubBossDirect, false, null);
            var legacy = AttackSubAtChildBirth(w, 1, laserSlot, origin, 2);
            if (legacy is not null) return $"旧の写しが null を返していません（{legacy}）——否定テストが効いていない";
            var now = AttackSub(w, 1, laserSlot, CharIku, origin, 2);
            return legacy != now ? null : "AttackSubAtChildBirth の直の流用でも AttackSub と同じ結果でした（この試験は空振りです）";
        });

        yield return ("AttackSub: 魔理沙（BossChild）はいままでどおり子の誕生 tick を辿る", () =>
        {
            var w = LaserChildWindow(tickCount: 12, side: 1,
                                     laserAliveFrom: 8, laserAliveTo: 11,
                                     enemyLocalSlot: 5, enemyAliveFrom: 2, enemyAliveTo: 11,
                                     bossSub: new Dictionary<int, double> { [2] = 6.0, [6] = 2.0 });
            int laserSlot = AnalysisTables.CoordBaseP1Laser;
            var origin = new LaserOriginParts(SubBossChild, false, 5);
            var got = AttackSub(w, 1, laserSlot, CharMarisa, origin, 8);
            return got == 6 ? null : $"{got}（6 のはず。AttackSubAtChildBirth と同じ経路を通ること）";
        });

        yield return ("AttackSub: Ex の子・分からないは攻撃名を出さない種別なので null", () =>
        {
            var w = LaserChildWindow(tickCount: 4, side: 1,
                                     laserAliveFrom: 0, laserAliveTo: 3,
                                     enemyLocalSlot: 5, enemyAliveFrom: 0, enemyAliveTo: 3,
                                     bossSub: new Dictionary<int, double> { [0] = 6.0 });
            int laserSlot = AnalysisTables.CoordBaseP1Laser;
            var ex = new LaserOriginParts(SubExChild, false, 5);
            if (AttackSub(w, 1, laserSlot, CharMarisa, ex, 0) is not null) return "Ex の子で null になっていない";
            var unknown = new LaserOriginParts(null, false, null);
            return AttackSub(w, 1, laserSlot, CharMarisa, unknown, 0) is null
                ? null : "分からないで null になっていない";
        });
    }

    private static Window LaserChildWindow(
        int tickCount, int side, int laserAliveFrom, int laserAliveTo,
        int enemyLocalSlot, int enemyAliveFrom, int enemyAliveTo,
        IReadOnlyDictionary<int, double> bossSub)
    {
        var meta = new WindowMeta
        {
            SessionId = 1, WindowNo = 1, FirstSeq = 1000, FirstFrame = null,
            TickCount = tickCount, SlotCount = CoordRing.SlotCount, Quant = "synth", Hits = [], LostTicks = 0,
        };
        var main = new Dictionary<string, TickValue[]>(StringComparer.Ordinal);
        var subCol = new TickValue[tickCount];
        Array.Fill(subCol, TickValue.Missing);
        double? carry = null;
        for (int i = 0; i < tickCount; i++)
        {
            if (bossSub.TryGetValue(i, out var v)) carry = v;
            if (carry is double c) subCol[i] = TickValue.Float(c);
        }
        main[side == 1 ? "p1_boss_sub" : "p2_boss_sub"] = subCol;

        int laserSlot = side == 1 ? AnalysisTables.CoordBaseP1Laser : AnalysisTables.CoordBaseP2Laser;
        int enemySlot = (side == 1 ? AnalysisTables.CoordBaseP1Enemy : AnalysisTables.CoordBaseP2Enemy) + enemyLocalSlot;
        var raw = new Dictionary<string, uint[]>(StringComparer.Ordinal);

        var laserSt = new uint[tickCount];
        for (int i = laserAliveFrom; i <= laserAliveTo && i < tickCount; i++) laserSt[i] = 1u;
        raw[CoordRing.ColState(laserSlot)] = laserSt;

        var enemySt = new uint[tickCount];
        for (int i = enemyAliveFrom; i <= enemyAliveTo && i < tickCount; i++) enemySt[i] = 1u;
        raw[CoordRing.ColState(enemySlot)] = enemySt;

        return new Window(meta, new WindowColumns(raw), new MainColumns(main), MainColumns.Empty);
    }
}

public readonly record struct LaserOriginParts(int? Sub, bool SubFolded, int? EnemySlot);

public enum LaserOriginKind
{
    Unknown,
    BossChild,
    ExChild,
    BossDirect,
}
