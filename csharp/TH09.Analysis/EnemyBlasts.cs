using System.Globalization;
using System.Runtime.CompilerServices;

namespace TH09.Analysis;

public readonly record struct EnemyBlastShape(
    int Idx, double R0, double Dr, int Life, int Wait, int Frames, double Reach);

public readonly record struct BlastWord(int Side, int? EnemySlot, int Idx, bool PosOk);

public readonly record struct EnemyBlastOrigin(
    int Index, int? Slot, double X, double Y, int Idx, uint? Kind, bool? PosOk);

public readonly record struct EnemyBlast(
    int? Slot, double X, double Y, double R, int Idx, int Frame, int Frames, double Reach);

public static class EnemyBlasts
{

    private static readonly double FieldX0 = BoardGeometry.FieldX0, FieldX1 = BoardGeometry.FieldX1;
    private static readonly double FieldY0 = BoardGeometry.FieldY0, FieldY1 = BoardGeometry.FieldY1;

    private const long HitlistValid = AnalysisTables.HitlistValid;

    private static readonly Dictionary<int, double> ScaleByIndex = BuildScale();

    private static readonly Dictionary<int, int> LifeByIndex = BuildLife();

    private static Dictionary<int, double> BuildScale()
    {
        var map = new Dictionary<int, double>();
        foreach (var line in Packed.Lines(AnalysisTables.EnemyBlastScalePacked))
        {
            var f = line.Split('\t');
            if (f.Length != 2)
                throw new InvalidDataException($"ENEMY_BLAST_SCALE の行が 2 列でない: {line}");
            map[I(f[0])] = D(f[1]);
        }
        return map;
    }

    private static Dictionary<int, int> BuildLife()
    {
        var map = new Dictionary<int, int>();
        foreach (var line in Packed.Lines(AnalysisTables.EnemyBlastLifePacked))
        {
            var f = line.Split('\t');
            if (f.Length != 2)
                throw new InvalidDataException($"ENEMY_BLAST_LIFE の行が 2 列でない: {line}");
            map[I(f[0])] = I(f[1]);
        }
        return map;
    }

    private static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);

    private static int I(string s) => int.Parse(s, CultureInfo.InvariantCulture);


    private static readonly string[] BlastSuffixes = Packed.Lines(AnalysisTables.BlastSuffixesPacked);

    private static readonly string[] BlastBases = Packed.Lines(AnalysisTables.BlastBasesPacked);

    private static readonly int ColXAt = SuffixAt("_x");
    private static readonly int ColYAt = SuffixAt("_y");
    private static readonly int ColKindAt = SuffixAt("_kind");
    private static readonly int ColWordAt = SuffixAt("_word");

    private static int SuffixAt(string suffix) => Array.IndexOf(BlastSuffixes, suffix);

    public static string BlastCol(int index, int which)
    {
        if ((uint)index >= (uint)BlastBases.Length)
            throw new ArgumentException($"{index} は爆風の通番ではありません");
        if ((uint)which >= (uint)BlastSuffixes.Length)
            throw new ArgumentException($"{which} は爆風の列ではありません");
        return BlastBases[index] + BlastSuffixes[which];
    }

    public static int SlotCount => BlastBases.Length;


    public static string Ja => AnalysisTables.EnemyBlastJa;

    public static string SizeJa => AnalysisTables.EnemyBlastSizeJa;

    public static string EraseNote => AnalysisTables.EnemyBlastEraseNote;

    public static string NoKindNote => AnalysisTables.EnemyBlastNoKindNote;

    public enum Source
    {
        Record,
        Infer,
    }

    public static Source SourceOf(Window w) => HasRecord(w) ? Source.Record : Source.Infer;

    public static string SourceNote(Window w) =>
        SourceOf(w) == Source.Record ? AnalysisTables.EnemyBlastRecordNote
                                     : AnalysisTables.EnemyBlastInferNote;


    public static double? Scale(int? idx)
    {
        if (idx is null) return null;
        return ScaleByIndex.TryGetValue(idx.Value, out var dr) ? dr : AnalysisTables.EnemyBlastScaleOther;
    }

    public static EnemyBlastShape? Shape(int? idx)
    {
        if (idx is null) return null;
        if (!LifeByIndex.TryGetValue(idx.Value, out var life)) return null;
        double dr = Scale(idx)!.Value;
        int frames = life - 1;
        return new EnemyBlastShape(idx.Value, AnalysisTables.EnemyBlastR0, dr, life,
                                   AnalysisTables.EnemyBlastWait, frames,
                                   AnalysisTables.EnemyBlastR0 + frames * dr);
    }

    public static double? Radius(int? idx, int? sinceKill)
    {
        var sh = Shape(idx);
        if (sh is null || sinceKill is null) return null;
        int f = sinceKill.Value - sh.Value.Wait;
        if (f < 1 || f > sh.Value.Frames) return null;
        return sh.Value.R0 + f * sh.Value.Dr;
    }


    public static bool HasKindIndex(Window w) => w.HasCoordFlags();

    public static bool HasRecord(Window w) => ComputeHasRecord(n => Column(w, n));

    public static bool ComputeHasRecord(Func<string, uint[]?> column)
    {
        if (column(BlastCol(0, ColWordAt)) is null) return false;
        var flags = column(AnalysisTables.CoordFlagsColumn);
        if (flags is null || flags.Length == 0) return false;
        foreach (var v in flags)
            if ((v & AnalysisTables.CoordFlagEnemyBlast) != 0) return true;
        return false;
    }

    public static BlastWord? WordParts(uint? word)
    {
        if (word is null) return null;
        uint v = word.Value;
        if ((v & AnalysisTables.CoordBlastPresent) == 0) return null;
        uint enemy = (v >> AnalysisTables.CoordBlastEnemyShift) & AnalysisTables.CoordBlastEnemyMask;
        return new BlastWord(
            (int)((v >> AnalysisTables.CoordBlastSideShift) & AnalysisTables.CoordBlastSideMask),
            enemy == 0 ? null : (int)enemy - AnalysisTables.CoordBlastEnemyBias,
            (int)((v >> AnalysisTables.CoordBlastIdxShift) & AnalysisTables.CoordBlastIdxMask),
            (v & AnalysisTables.CoordBlastPosOk) != 0);
    }

    public static List<EnemyBlastOrigin> ComputeOriginsRecorded(
        int tickCount, int side, Func<string, uint[]?> column)
    {
        var outList = new List<EnemyBlastOrigin>();
        for (int k = 0; k < SlotCount; k++)
        {
            var words = column(BlastCol(k, ColWordAt));
            if (words is null || words.Length == 0) continue;
            var xs = column(BlastCol(k, ColXAt));
            var ys = column(BlastCol(k, ColYAt));
            var kinds = column(BlastCol(k, ColKindAt));
            int end = Math.Min(tickCount, words.Length);
            for (int i = 0; i < end; i++)
            {
                var parts = WordParts(words[i]);
                if (parts is null || parts.Value.Side + 1 != side) continue;
                var x = Bits(xs, i);
                var y = Bits(ys, i);
                if (x is null || y is null) continue;
                int? slot = parts.Value.EnemySlot is int e
                    ? e + (side == 1 ? AnalysisTables.CoordBaseP1Enemy : AnalysisTables.CoordBaseP2Enemy)
                    : null;
                outList.Add(new EnemyBlastOrigin(i, slot, x.Value, y.Value, parts.Value.Idx,
                                                 Cell(kinds, i), parts.Value.PosOk));
            }
        }
        return outList;
    }

    public static IReadOnlyList<EnemyBlastOrigin> Origins(Window w, int side)
    {
        var cache = CacheOf(w);
        lock (cache)
        {
            if (cache.Origins.TryGetValue(side, out var got)) return got;
            var made = ComputeOriginsFor(w.TickCount, side, w.SlotsOf(side)["enemy"],
                                         n => Column(w, n));
            cache.Origins[side] = made;
            return made;
        }
    }

    public static List<EnemyBlastOrigin> ComputeOriginsFor(
        int tickCount, int side, IReadOnlyList<int> enemySlots, Func<string, uint[]?> column)
    {
        var made = new List<EnemyBlastOrigin>();
        if (ComputeHasRecord(column))
            made.AddRange(ComputeOriginsRecorded(tickCount, side, column));
        else
        {
            var flags = column(AnalysisTables.CoordFlagsColumn);
            foreach (var slot in enemySlots)
                made.AddRange(ComputeOrigins(slot, tickCount,
                                             column(CoordRing.ColState(slot)),
                                             column(CoordRing.ColKind(slot)),
                                             column(CoordRing.ColX(slot)),
                                             column(CoordRing.ColY(slot)),
                                             flags));
        }
        return made.OrderBy(e => e.Index).ThenBy(e => e.Slot ?? -1).ToList();
    }

    public static List<EnemyBlastOrigin> ComputeOrigins(
        int slot, int tickCount, uint[]? state, uint[]? kind, uint[]? xs, uint[]? ys, uint[]? coordFlags)
    {
        var outList = new List<EnemyBlastOrigin>();
        if (state is null || state.Length == 0) return outList;
        int end = Math.Min(tickCount, state.Length);
        for (int i = 1; i < end; i++)
        {
            if (!CoordRing.SlotIsAlive(slot, state[i - 1])) continue;
            if (CoordRing.SlotIsAlive(slot, state[i])) continue;

            var kd = Cell(kind, i - 1);
            if (kd is null) continue;
            if (!string.Equals(CoordRing.EnemyClass(EnemySize.CatWord(kd.Value)),
                               AnalysisTables.EnemyClassFairy, StringComparison.Ordinal)) continue;

            var x = Bits(xs, i - 1);
            var y = Bits(ys, i - 1);
            if (x is null || y is null) continue;
            if (!(FieldX0 <= x.Value && x.Value <= FieldX1)) continue;
            if (!(FieldY0 <= y.Value && y.Value <= FieldY1)) continue;

            var idx = EnemySize.KindIndex(kd.Value, Cell(coordFlags, i - 1));
            if (idx is null) continue;
            outList.Add(new EnemyBlastOrigin(i, slot, x.Value, y.Value, idx.Value, null, null));
        }
        return outList;
    }

    public static List<EnemyBlast> At(Window w, int i, int side)
    {
        if (i < 0 || i >= w.TickCount) return [];
        return ComputeAt(Origins(w, side), AdvanceCounts(w, side), i);
    }

    public static List<EnemyBlast> ComputeAt(
        IReadOnlyList<EnemyBlastOrigin> origins, int[] counts, int i)
    {
        var outList = new List<EnemyBlast>();
        foreach (var e in origins)
        {
            if (e.Index > i) continue;
            var sh = Shape(e.Idx);
            if (sh is null) continue;
            int frame = counts[i + 1] - counts[e.Index];
            var r = Radius(e.Idx, frame);
            if (r is null) continue;
            outList.Add(new EnemyBlast(e.Slot, e.X, e.Y, r.Value, e.Idx, frame,
                                       sh.Value.Frames, sh.Value.Reach));
        }
        return outList;
    }

    public static bool[] AdvanceFlags(Window w, int side)
    {
        var name = "p" + side.ToString(CultureInfo.InvariantCulture) + "_hit_list_count";
        var col = w.Main.Has(name) ? w.Main[name] : null;
        return ComputeAdvance(w.TickCount, col);
    }

    public static bool[] ComputeAdvance(int tickCount, TickValue[]? col)
    {
        var outv = new bool[tickCount];
        if (col is null) { Array.Fill(outv, true); return outv; }
        for (int i = 0; i < outv.Length; i++)
            outv[i] = i >= col.Length || col[i].IsMissing || (col[i].AsLong & HitlistValid) != 0;
        return outv;
    }

    public static int[] AdvanceCounts(Window w, int side)
    {
        var cache = CacheOf(w);
        lock (cache)
        {
            if (cache.Counts.TryGetValue(side, out var got)) return got;
            var made = Accumulate(AdvanceFlags(w, side));
            cache.Counts[side] = made;
            return made;
        }
    }

    public static int[] Accumulate(bool[] adv)
    {
        var outv = new int[adv.Length + 1];
        for (int i = 0; i < adv.Length; i++) outv[i + 1] = outv[i] + (adv[i] ? 1 : 0);
        return outv;
    }


    private sealed class Cache
    {
        public readonly Dictionary<int, IReadOnlyList<EnemyBlastOrigin>> Origins = new();
        public readonly Dictionary<int, int[]> Counts = new();
    }

    private static readonly ConditionalWeakTable<Window, Cache> Caches = new();

    private static Cache CacheOf(Window w) => Caches.GetValue(w, static _ => new Cache());

    private static uint[]? Column(Window w, string name) => w.Cols.Has(name) ? w.Cols[name] : null;

    private static uint? Cell(uint[]? col, int i) => col is not null && (uint)i < (uint)col.Length ? col[i] : null;

    private static double? Bits(uint[]? col, int i)
    {
        var v = Cell(col, i);
        return v is null ? null : CoordRing.UnpackF32(v.Value);
    }


    public static IEnumerable<(string Label, Func<string?> Body)> SelfTestCases()
    {
        yield return ($"★母数（増分 {ScaleByIndex.Count} / 寿命 {LifeByIndex.Count}）", () =>
        {
            if (ScaleByIndex.Count == 0) return "ENEMY_BLAST_SCALE が 0 行";
            if (LifeByIndex.Count == 0) return "ENEMY_BLAST_LIFE が 0 行";
            return string.IsNullOrEmpty(Ja) ? "画面に出す名前が空" : null;
        });

        yield return ("★増分の表は寿命の表に含まれる（増分だけある種別は形にならない）", () =>
        {
            var orphan = ScaleByIndex.Keys.Where(k => !LifeByIndex.ContainsKey(k))
                                          .OrderBy(k => k).ToList();
            return orphan.Count == 0 ? null
                : "寿命の表に無い種別が増分の表に居る: " + Show(orphan)
                  + "（その種別は Shape() が null になり、増分が誰にも使われない）";
        });

        yield return ("★寿命の表の種別はすべて形になる（★到達半径も出る）", () =>
        {
            foreach (var idx in LifeByIndex.Keys.OrderBy(k => k))
            {
                var sh = Shape(idx);
                if (sh is null) return $"idx={idx} が null";
                if (sh.Value.Frames != sh.Value.Life - 1) return $"idx={idx} の Frames が Life-1 でない";
                if (!Near(sh.Value.Reach, sh.Value.R0 + sh.Value.Frames * sh.Value.Dr))
                    return $"idx={idx} の Reach が R0 + Frames*Dr でない";
                if (sh.Value.Wait != AnalysisTables.EnemyBlastWait)
                    return $"idx={idx} の Wait が表と違う";
            }
            return null;
        });

        yield return ("★★表の外は 増分が『あり』・寿命が『なし』（この非対称は意図的）", () =>
        {
            int alien = FirstUnknownIndex();
            var dr = Scale(alien);
            if (dr is null) return $"idx={alien} の増分が null（ゲームは無条件に置く）";
            if (!Near(dr.Value, AnalysisTables.EnemyBlastScaleOther))
                return $"idx={alien} の増分が {dr} ——表の外の値になっていない";
            if (Shape(alien) is not null) return $"idx={alien} に形を返した（出ていないものに答えを用意している）";
            return Radius(alien, AnalysisTables.EnemyBlastWait + 1) is null ? null
                : $"idx={alien} に半径を返した";
        });

        yield return ("★種別が読めない窓（null）は増分も形も半径も null（0 に倒さない）", () =>
        {
            if (Scale(null) is not null) return "増分に答えた";
            if (Shape(null) is not null) return "形に答えた";
            return Radius(null, 5) is null && Radius(LifeByIndex.Keys.First(), null) is null
                ? null : "半径に答えた";
        });

        foreach (var idx in LifeByIndex.Keys.OrderBy(k => k))
        {
            var sh = Shape(idx)!.Value;
            yield return ($"★idx {idx}: 待機 {sh.Wait}F の間は null、{sh.Frames} 回広がって消える", () =>
            {
                for (int f = 0; f <= sh.Wait; f++)
                    if (Radius(idx, f) is double got)
                        return $"撃破から {f}F で半径 {got} を返した（まだ何も出ていないはず）";
                var first = Radius(idx, sh.Wait + 1);
                if (first is null) return "待機の次のフレームで何も出ない";
                if (!Near(first.Value, sh.R0 + sh.Dr)) return $"1 枚目が {first} ≠ {sh.R0 + sh.Dr}";
                var last = Radius(idx, sh.Wait + sh.Frames);
                if (last is null) return "最後のフレームで何も出ない";
                if (!Near(last.Value, sh.Reach)) return $"最後が {last} ≠ 到達半径 {sh.Reach}";
                return Radius(idx, sh.Wait + sh.Frames + 1) is null ? null : "寿命を過ぎても消えない";
            });
        }

        yield return ("★負の進み数でも null（0 を返さない）", () =>
            Radius(LifeByIndex.Keys.First(), -3) is null ? null : "半径を返した");

        int enemySlot = AnalysisTables.CoordBaseP1Enemy;
        uint kindFairy = 0u;
        uint flagsOk = AnalysisTables.CoordFlagEnemyKindIdx;
        int sample = LifeByIndex.Keys.OrderBy(k => k).First();

        uint KindOf(int idx, uint extra) =>
            (((uint)idx & AnalysisTables.CoordEnemyKindIdxMask) << AnalysisTables.CoordEnemyKindIdxShift) | extra;

        uint[] Bits2(double a, double b) =>
            [BitConverter.SingleToUInt32Bits((float)a), BitConverter.SingleToUInt32Bits((float)b)];

        yield return ("合成: 盤の中で枠が 生 → 空 になったら起点が 1 件", () =>
        {
            var got = ComputeOrigins(enemySlot, 2, [1, 0], [KindOf(sample, kindFairy), 0],
                                     Bits2(0, 0), Bits2(200, 200), [flagsOk, flagsOk]);
            if (got.Count != 1) return $"{got.Count} 件（1 件のはず）";
            if (got[0].Index != 1) return $"index {got[0].Index}（1 のはず）";
            if (got[0].Slot != enemySlot) return $"slot {got[0].Slot}";
            return got[0].Idx == sample ? null : $"idx {got[0].Idx} ≠ {sample}";
        });

        yield return ("★合成: 盤の外で消えた枠は起点にしない（去っただけかもしれない）", () =>
        {
            var outside = FieldY1 + 1.0;
            var got = ComputeOrigins(enemySlot, 2, [1, 0], [KindOf(sample, kindFairy), 0],
                                     Bits2(0, 0), Bits2(outside, outside), [flagsOk, flagsOk]);
            return got.Count == 0 ? null : $"{got.Count} 件（盤外を撃破と読んでいる）";
        });

        yield return ("★合成: 座標が NaN なら起点にしない（死んだ枠のゴミ float）", () =>
        {
            uint nan = BitConverter.SingleToUInt32Bits(float.NaN);
            var got = ComputeOrigins(enemySlot, 2, [1, 0], [KindOf(sample, kindFairy), 0],
                                     [nan, nan], Bits2(200, 200), [flagsOk, flagsOk]);
            return got.Count == 0 ? null : $"{got.Count} 件（NaN を盤の中と読んでいる）";
        });

        yield return ("★★合成: リリーは起点にしない（ゲームが爆風を作らない）", () =>
        {
            var kd = KindOf(sample, AnalysisTables.CoordEnemyLilyBit);
            var got = ComputeOrigins(enemySlot, 2, [1, 0], [kd, 0],
                                     Bits2(0, 0), Bits2(200, 200), [flagsOk, flagsOk]);
            if (got.Count != 0) return $"{got.Count} 件（リリーの爆風を描いている）";
            return string.Equals(CoordRing.EnemyClass(EnemySize.CatWord(kd)),
                                 AnalysisTables.EnemyClassLily, StringComparison.Ordinal)
                ? null : "リリーが分類でリリーになっていない（この検査は何も見ていない）";
        });

        yield return ("★合成: coord_flags の列が無い窓は 1 件も出ない（青に倒さない）", () =>
        {
            var got = ComputeOrigins(enemySlot, 2, [1, 0], [KindOf(sample, kindFairy), 0],
                                     Bits2(0, 0), Bits2(200, 200), null);
            return got.Count == 0 ? null : $"{got.Count} 件（種別を読めない窓で描いている）";
        });

        yield return ("★合成: 区画のビットが落ちている tick も出ない", () =>
        {
            var got = ComputeOrigins(enemySlot, 2, [1, 0], [KindOf(sample, kindFairy), 0],
                                     Bits2(0, 0), Bits2(200, 200), [0u, 0u]);
            return got.Count == 0 ? null : $"{got.Count} 件";
        });

        yield return ("合成: 生き通し／空き通しの枠には起点が無い", () =>
        {
            var kd = KindOf(sample, kindFairy);
            uint y200 = BitConverter.SingleToUInt32Bits(200f);
            var alive = ComputeOrigins(enemySlot, 3, [1, 1, 1], [kd, kd, kd],
                                       [0u, 0u, 0u], [y200, y200, y200],
                                       [flagsOk, flagsOk, flagsOk]);
            if (alive.Count != 0) return $"生き通しで {alive.Count} 件";
            var dead = ComputeOrigins(enemySlot, 3, [0, 0, 0], [0, 0, 0],
                                      [0u, 0u, 0u], [0u, 0u, 0u], [flagsOk, flagsOk, flagsOk]);
            return dead.Count == 0 ? null : $"空き通しで {dead.Count} 件";
        });

        yield return ("★合成: 窓の頭（index 0）は起点にならない（前の tick が無い）", () =>
        {
            var got = ComputeOrigins(enemySlot, 1, [0], [0], [0u], [0u], [flagsOk]);
            return got.Count == 0 ? null : $"{got.Count} 件";
        });

        yield return ("★合成: 列が無ければ毎 tick 進む（黙って止めない）", () =>
        {
            var adv = ComputeAdvance(4, null);
            return adv.All(v => v) ? null : "止まった tick がある";
        });

        yield return ("★合成: VALID が落ちている tick は進まない／欠測は進む", () =>
        {
            TickValue[] col = [TickValue.Int(HitlistValid), TickValue.Int(0), TickValue.Missing];
            var adv = ComputeAdvance(3, col);
            if (!adv[0]) return "VALID が立っている tick で止まっている";
            if (adv[1]) return "VALID が落ちている tick で進んでいる";
            return adv[2] ? null : "★欠測を『止まった』に倒している（読めていないだけ）";
        });

        yield return ("★合成: 止まっている間は円が太らない（帯の間の 1 枚を止める）", () =>
        {
            var sh = Shape(sample)!.Value;
            var origins = new List<EnemyBlastOrigin> { new(0, enemySlot, 0, 0, sample, null, null) };
            int n = sh.Wait + sh.Frames + 4;
            var adv = Enumerable.Repeat(true, n).ToArray();
            adv[1] = false;
            var counts = Accumulate(adv);
            var plain = Accumulate(Enumerable.Repeat(true, n).ToArray());

            int LastTick(int[] c)
            {
                int last = -1;
                for (int i = 0; i < n; i++) if (ComputeAt(origins, c, i).Count > 0) last = i;
                return last;
            }
            int a = LastTick(plain), b = LastTick(counts);
            if (a < 0 || b < 0) return "円が 1 枚も出ていない（この検査は何も見ていない）";
            return b == a + 1 ? null : $"止めた側の最後が {b}（止めない側は {a}。1 tick ずれるはず）";
        });

        yield return ("★合成: 起点の tick には何も出ない（待機の間）／到達半径まで太る", () =>
        {
            var sh = Shape(sample)!.Value;
            var origins = new List<EnemyBlastOrigin> { new(0, enemySlot, 0, 0, sample, null, null) };
            int n = sh.Wait + sh.Frames + 2;
            var counts = Accumulate(Enumerable.Repeat(true, n).ToArray());
            for (int i = 0; i < sh.Wait; i++)
                if (ComputeAt(origins, counts, i).Count != 0)
                    return $"tick {i}（待機中）に円が出た";
            var first = ComputeAt(origins, counts, sh.Wait);
            if (first.Count != 1) return "待機の次の tick に円が出ない";
            if (first[0].Frame != sh.Wait + 1) return $"1 枚目の Frame が {first[0].Frame}";
            var last = ComputeAt(origins, counts, sh.Wait + sh.Frames - 1);
            if (last.Count != 1) return "最後の tick に円が出ない";
            if (!Near(last[0].R, sh.Reach)) return $"最後の半径 {last[0].R} ≠ 到達半径 {sh.Reach}";
            return ComputeAt(origins, counts, sh.Wait + sh.Frames).Count == 0 ? null : "消えない";
        });


        yield return ($"★母数（記録の枠 {SlotCount} / 接尾辞 {BlastSuffixes.Length}）", () =>
        {
            if (SlotCount == 0) return "BLAST_BASES が 0 行";
            if (BlastSuffixes.Length == 0) return "BLAST_SUFFIXES が 0 行";
            if (ColXAt < 0 || ColYAt < 0 || ColKindAt < 0 || ColWordAt < 0)
                return "接尾辞を引けない（表の並びが変わった）: " + string.Join(",", BlastSuffixes);
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int k = 0; k < SlotCount; k++)
                for (int c = 0; c < BlastSuffixes.Length; c++)
                    if (!names.Add(BlastCol(k, c))) return "列名が重複している: " + BlastCol(k, c);
            return names.Count == SlotCount * BlastSuffixes.Length ? null
                : $"列名が {names.Count} 本（{SlotCount} × {BlastSuffixes.Length} のはず）";
        });

        yield return ("★母数（凡例の文言 6 本が空でなく、2 本の断りが別の文）", () =>
        {
            var all = new (string Name, string Text)[]
            {
                ("Ja", Ja), ("SizeJa", SizeJa), ("EraseNote", EraseNote), ("NoKindNote", NoKindNote),
                ("Record", AnalysisTables.EnemyBlastRecordNote),
                ("Infer", AnalysisTables.EnemyBlastInferNote),
            };
            var empty = all.Where(t => string.IsNullOrEmpty(t.Text)).Select(t => t.Name).ToList();
            if (empty.Count > 0) return "空の文言: " + string.Join(",", empty);
            return string.Equals(AnalysisTables.EnemyBlastRecordNote,
                                 AnalysisTables.EnemyBlastInferNote, StringComparison.Ordinal)
                ? "記録の道と推論の道の断りが同じ文" : null;
        });

        yield return ("★爆風の通番は範囲外で落ちる（エラーを出さずに別の枠の列名を返さない）", () =>
        {
            static string? Throws(Func<string> f)
            {
                try { var got = f(); return "落ちずに " + got + " を返した"; }
                catch (ArgumentException) { return null; }
            }
            return Throws(() => BlastCol(-1, ColXAt))
                ?? Throws(() => BlastCol(SlotCount, ColXAt))
                ?? Throws(() => BlastCol(0, BlastSuffixes.Length))
                ?? Throws(() => BlastCol(0, -1));
        });

        yield return ("★空きの語（0）と読めない語は爆風ではない", () =>
        {
            if (WordParts(0u) is not null) return "空きの語 0 を爆風と読んだ";
            if (WordParts(null) is not null) return "null を爆風と読んだ";
            var bare = WordParts(AnalysisTables.CoordBlastPresent);
            if (bare is null) return "PRESENT だけの語を空きと読んだ";
            if (bare.Value.EnemySlot is not null) return "枠が 0 の語で枠番号を答えた";
            return bare.Value.PosOk ? "POS_OK が立っていないのに true" : null;
        });

        uint RawWord(uint side0, uint enemyField, uint idxField, bool posOk)
        {
            uint v = AnalysisTables.CoordBlastPresent;
            v |= (side0 & AnalysisTables.CoordBlastSideMask) << AnalysisTables.CoordBlastSideShift;
            v |= (enemyField & AnalysisTables.CoordBlastEnemyMask) << AnalysisTables.CoordBlastEnemyShift;
            v |= (idxField & AnalysisTables.CoordBlastIdxMask) << AnalysisTables.CoordBlastIdxShift;
            if (posOk) v |= AnalysisTables.CoordBlastPosOk;
            return v;
        }

        yield return ("★語の区画が混ざらない（陣 / 枠 / 種別 / POS_OK を総当たり）", () =>
        {
            foreach (uint side0 in new uint[] { 0, 1 })
                foreach (uint ef in new uint[] { 1, 2, (uint)AnalysisTables.CoordEnemySlots })
                    foreach (uint idxf in new uint[] { 0, 1, 3, 0xFF })
                        foreach (bool posOk in new[] { true, false })
                        {
                            var got = WordParts(RawWord(side0, ef, idxf, posOk));
                            if (got is null) return "PRESENT が立っているのに null";
                            var v = got.Value;
                            if (v.Side != (int)side0) return $"陣が {v.Side}（{side0} のはず）";
                            if (v.Idx != (int)idxf) return $"種別が {v.Idx}（{idxf} のはず）";
                            if (v.PosOk != posOk) return "POS_OK が違う";
                            if (v.EnemySlot is null) return "枠が null になった";
                        }
            return null;
        });

        yield return ("★★敵の枠 0 と『不明』を混ぜない（記録の +1 を戻すのは 1 か所）", () =>
        {
            if (WordParts(RawWord(0, 0, 0, true))!.Value.EnemySlot is int z)
                return $"記録 0 を枠 {z} と答えた（0 を『不明』に使っている）";
            int? zero = null;
            for (uint ef = 1; ef <= (uint)AnalysisTables.CoordEnemySlots; ef++)
                if (WordParts(RawWord(0, ef, 0, true))!.Value.EnemySlot == 0) { zero = (int)ef; break; }
            if (zero is null) return "枠 0 になる記録が 1 つも無い（+1 を戻していない）";
            int last = AnalysisTables.CoordEnemySlots - 1;
            var top = WordParts(RawWord(0, (uint)(zero.Value + last), 0, true))!.Value.EnemySlot;
            if (top != last) return $"最後の枠が {top}（{last} のはず）";
            for (int k = 0; k < 4; k++)
            {
                var a = WordParts(RawWord(0, (uint)(zero.Value + k), 0, true))!.Value.EnemySlot;
                if (a != k) return $"記録 {zero.Value + k} が枠 {a}（{k} のはず）";
            }
            return null;
        });

        const int SynN = 40, SynAt = 10, SynGhostSlot = 7, SynFairySlot = 3;
        const uint GhostCat = AnalysisTables.CoordEnemyGhostMask
                              & (~AnalysisTables.CoordEnemyGhostMask + 1u);
        int recIdx = LifeByIndex.Keys.OrderBy(k => k).Last();

        static uint F32(double v) => BitConverter.SingleToUInt32Bits((float)v);

        Dictionary<string, uint[]> MakeSyn(bool withRecord, uint cat, int idx, double cx, double cy)
        {
            var d = new Dictionary<string, uint[]>(StringComparer.Ordinal);
            uint fl = AnalysisTables.CoordFlagEnemyKindIdx;
            if (withRecord) fl |= AnalysisTables.CoordFlagEnemyBlast;
            d[AnalysisTables.CoordFlagsColumn] = Enumerable.Repeat(fl, SynN).ToArray();

            int es = AnalysisTables.CoordBaseP1Enemy + SynGhostSlot;
            uint kw = cat | (((uint)idx & AnalysisTables.CoordEnemyKindIdxMask)
                             << AnalysisTables.CoordEnemyKindIdxShift);
            d[CoordRing.ColState(es)] = Enumerable.Range(0, SynN).Select(i => i < SynAt ? 1u : 0u).ToArray();
            d[CoordRing.ColKind(es)] = Enumerable.Range(0, SynN).Select(i => i < SynAt ? kw : 0u).ToArray();
            d[CoordRing.ColX(es)] = Enumerable.Range(0, SynN).Select(i => i < SynAt ? F32(20.0) : 0u).ToArray();
            d[CoordRing.ColY(es)] = Enumerable.Range(0, SynN).Select(i => i < SynAt ? F32(200.0) : 0u).ToArray();

            for (int k = 0; k < SlotCount; k++)
                for (int c = 0; c < BlastSuffixes.Length; c++)
                    d[BlastCol(k, c)] = new uint[SynN];
            if (withRecord)
            {
                d[BlastCol(1, ColXAt)][SynAt] = F32(cx);
                d[BlastCol(1, ColYAt)][SynAt] = F32(cy);
                d[BlastCol(1, ColKindAt)][SynAt] = cat;
                d[BlastCol(1, ColWordAt)][SynAt] =
                    RawWord(0, (uint)(SynGhostSlot + AnalysisTables.CoordBlastEnemyBias), (uint)idx, true);
                d[BlastCol(0, ColXAt)][SynAt] = F32(-30.0);
                d[BlastCol(0, ColYAt)][SynAt] = F32(100.0);
                d[BlastCol(0, ColWordAt)][SynAt] =
                    RawWord(1, (uint)(SynFairySlot + AnalysisTables.CoordBlastEnemyBias), 0, true);
            }
            return d;
        }

        static Func<string, uint[]?> Col(Dictionary<string, uint[]> d) =>
            n => d.TryGetValue(n, out var v) ? v : null;

        static List<int> Enemies(int side) =>
            Enumerable.Range(side == 1 ? AnalysisTables.CoordBaseP1Enemy
                                       : AnalysisTables.CoordBaseP2Enemy,
                             AnalysisTables.CoordEnemySlots).ToList();

        yield return ("★★合成 v8: 記録された幽霊の爆風が、そのまま起点になる", () =>
        {
            var d = MakeSyn(true, GhostCat, recIdx, 21.5, 201.5);
            var col = Col(d);
            if (!ComputeHasRecord(col)) return "v8 の窓を『記録していない』と答えた";
            var got = ComputeOriginsFor(SynN, 1, Enemies(1), col);
            if (got.Count != 1) return $"1P の爆風が {got.Count} 件（1 件のはず）";
            var e = got[0];
            if (e.Index != SynAt) return $"tick が {e.Index}（{SynAt} のはず）";
            int want = AnalysisTables.CoordBaseP1Enemy + SynGhostSlot;
            if (e.Slot != want) return $"敵の枠が {e.Slot}（{want} のはず。+1 の戻し忘れを疑う）";
            if (e.Idx != recIdx) return $"種別が {e.Idx}（{recIdx} のはず）";
            if (e.PosOk != true) return $"POS_OK が {e.PosOk}";
            if (!Near(e.X, 21.5) || !Near(e.Y, 201.5))
                return $"中心が {e.X}/{e.Y}（記録した 21.5/201.5 のはず。敵枠の位置を読んでいる）";
            if (e.Kind != GhostCat) return $"kind が {e.Kind}（幽霊の {GhostCat} のはず）";
            return null;
        });

        yield return ("★★合成 v8: 陣は語の bit1 で分かれる（通番の位置では割れない）", () =>
        {
            var col = Col(MakeSyn(true, GhostCat, recIdx, 21.5, 201.5));
            var a = ComputeOriginsFor(SynN, 1, Enemies(1), col);
            var b = ComputeOriginsFor(SynN, 2, Enemies(2), col);
            if (a.Count != 1 || b.Count != 1) return $"陣ごとの件数が {a.Count} / {b.Count}（1 / 1 のはず）";
            int want = AnalysisTables.CoordBaseP2Enemy + SynFairySlot;
            if (b[0].Slot != want) return $"2P の枠が {b[0].Slot}（{want} のはず）";
            return Near(b[0].X, -30.0) ? null : $"2P の中心が {b[0].X}（-30.0 のはず）";
        });

        yield return ("★★合成 v8: 32 枠すべて空きでも『記録している窓』（0 個と未記録は別）", () =>
        {
            var d = MakeSyn(true, GhostCat, recIdx, 21.5, 201.5);
            for (int k = 0; k < SlotCount; k++) d[BlastCol(k, ColWordAt)] = new uint[SynN];
            var col = Col(d);
            if (!ComputeHasRecord(col)) return "全部空きにしたら『記録していない窓』になった";
            var a = ComputeOriginsFor(SynN, 1, Enemies(1), col);
            var b = ComputeOriginsFor(SynN, 2, Enemies(2), col);
            return a.Count == 0 && b.Count == 0 ? null : $"{a.Count} / {b.Count} 件出た";
        });

        yield return ("★★合成: 列はあるが区画のビットが無い窓は『未記録』＝推論へ落ちる", () =>
        {
            var d = MakeSyn(true, GhostCat, recIdx, 21.5, 201.5);
            d[AnalysisTables.CoordFlagsColumn] =
                Enumerable.Repeat(AnalysisTables.CoordFlagEnemyKindIdx, SynN).ToArray();
            var col = Col(d);
            if (ComputeHasRecord(col))
                return "フラグが無い窓を『記録している』と答えた"
                       + "（フックが刺さらなかった走行を『爆風 0 個』と読んでしまう）";
            var got = ComputeOriginsFor(SynN, 1, Enemies(1), col);
            return got.Count == 0 ? null : $"推論へ落ちたのに {got.Count} 件出た（幽霊のはず）";
        });

        yield return ("★合成 v8: 記録の道は盤の外の爆風も返す（推論のふるいを掛けない）", () =>
        {
            var col = Col(MakeSyn(true, GhostCat, recIdx, 0.0, FieldY1 + 50.0));
            var got = ComputeOriginsFor(SynN, 1, Enemies(1), col);
            if (got.Count != 1) return $"{got.Count} 件（盤外でも 1 件のはず）";
            return Near(got[0].Y, FieldY1 + 50.0) ? null : $"中心が {got[0].Y}";
        });

        yield return ("★合成 v8: 記録の道は種別を語から取る（coord_flags のガードを通さない）", () =>
        {
            var d = MakeSyn(true, GhostCat, recIdx, 21.5, 201.5);
            d[AnalysisTables.CoordFlagsColumn] =
                Enumerable.Repeat(AnalysisTables.CoordFlagEnemyBlast, SynN).ToArray();
            var got = ComputeOriginsFor(SynN, 1, Enemies(1), Col(d));
            if (got.Count != 1) return $"{got.Count} 件（1 件のはず）";
            return got[0].Idx == recIdx ? null : $"種別が {got[0].Idx}（{recIdx} のはず）";
        });

        yield return ("★否定: 旧（待機を引かない）なら撃破の直後に円が出る", () =>
        {
            var sh = Shape(sample)!.Value;
            double? LegacyRadius(int f) =>
                f < 1 || f > sh.Frames ? null : sh.R0 + f * sh.Dr;

            if (LegacyRadius(1) is null)
                return "旧が落ちない（写しが間違っている。否定テストの意味が無い）";
            if (Radius(sample, 1) is double got)
                return $"新も撃破の直後に半径 {got} を返した（待機を引いていない）";
            return Radius(sample, sh.Wait + sh.Frames) is not null
                   && LegacyRadius(sh.Wait + sh.Frames) is null
                ? null : "待機ぶんの後ろずれが起きていない";
        });

        yield return ("★否定: 旧（f <= 0 に 0 を返す）なら『半径 0 の円』が生まれる", () =>
        {
            var sh = Shape(sample)!.Value;
            double LegacyRadius(int since)
            {
                int f = since - sh.Wait;
                return f < 1 ? 0.0 : sh.R0 + f * sh.Dr;
            }
            if (LegacyRadius(0) != 0.0) return "旧が落ちない（写しが間違っている）";
            return Radius(sample, 0) is null ? null : "新も待機中に値を返している";
        });

        yield return ("★否定: 旧（表の外に life = idx + 8 を当てる）なら出ていない種別に答える", () =>
        {
            int alien = FirstUnknownIndex();
            int LegacyLife(int idx) => idx + 8;
            if (LegacyLife(alien) <= 0) return "旧が落ちない（写しが間違っている）";
            if (Shape(alien) is not null) return $"新も idx={alien} に形を返した";
            var mismatch = LifeByIndex.Where(kv => LegacyLife(kv.Key) != kv.Value)
                                      .Select(kv => kv.Key).OrderBy(k => k).ToList();
            return mismatch.Count == 0 ? null
                : "表の中で旧と新の寿命が食い違う: " + Show(mismatch)
                  + "（表か、読み方の前提のどちらかが動いている）";
        });

        yield return ("★否定: 旧（枠が空いたら全部撃破）なら盤外の離脱まで爆風になる", () =>
        {
            var outside = FieldY1 + 1.0;
            bool LegacyIsKill(uint[] st) =>
                CoordRing.SlotIsAlive(enemySlot, st[0]) && !CoordRing.SlotIsAlive(enemySlot, st[1]);
            uint[] state = [1, 0];
            if (!LegacyIsKill(state)) return "旧が落ちない（写しが間違っている）";
            var got = ComputeOrigins(enemySlot, 2, state, [KindOf(sample, kindFairy), 0],
                                     Bits2(0, 0), Bits2(outside, outside), [flagsOk, flagsOk]);
            return got.Count == 0 ? null : "新も盤外を撃破と読んでいる";
        });

        yield return ("★★否定: 旧（記録があっても推論へ倒す）なら v8 の窓で幽霊の爆風が丸ごと消える", () =>
        {
            List<EnemyBlastOrigin> Legacy(Func<string, uint[]?> c, int side)
            {
                var made = new List<EnemyBlastOrigin>();
                var fl = c(AnalysisTables.CoordFlagsColumn);
                foreach (var slot in Enemies(side))
                    made.AddRange(ComputeOrigins(slot, SynN, c(CoordRing.ColState(slot)),
                                                 c(CoordRing.ColKind(slot)), c(CoordRing.ColX(slot)),
                                                 c(CoordRing.ColY(slot)), fl));
                return made;
            }

            var ghost = Col(MakeSyn(true, GhostCat, recIdx, 21.5, 201.5));
            var legacyGhost = Legacy(ghost, 1);
            if (legacyGhost.Count != 0)
                return $"旧が幽霊を {legacyGhost.Count} 件出した（写しが間違っている。否定の意味が無い）";

            var fairy = Col(MakeSyn(true, 0u, recIdx, 21.5, 201.5));
            var legacyFairy = Legacy(fairy, 1);
            if (legacyFairy.Count != 1)
                return $"旧が妖精を {legacyFairy.Count} 件（1 件のはず）"
                       + "——推論の道そのものが死んでいる。この 0 は『幽霊を落とした』の証拠にならない";

            var got = ComputeOriginsFor(SynN, 1, Enemies(1), ghost);
            if (got.Count != 1) return $"新が {got.Count} 件（1 件のはず。記録の道へ分岐していない）";
            if (got[0].Kind != GhostCat) return "新が返したのは幽霊ではない";
            return Near(got[0].X, 21.5) ? null
                : $"中心が {got[0].X}（記録の 21.5 のはず ——推論の位置を読んでいる）";
        });

        yield return ("★★否定: 旧（推論の道を消す）なら v7 以前の窓が 1 個も描けなくなる", () =>
        {
            var d = MakeSyn(false, 0u, recIdx, 0, 0);
            for (int k = 0; k < SlotCount; k++)
                for (int c = 0; c < BlastSuffixes.Length; c++)
                    d.Remove(BlastCol(k, c));
            var col = Col(d);
            if (ComputeHasRecord(col)) return "列が無い窓を『記録している』と答えた";
            var legacy = ComputeOriginsRecorded(SynN, 1, col);
            if (legacy.Count != 0)
                return $"旧が {legacy.Count} 件出した（写しが間違っている）";
            var got = ComputeOriginsFor(SynN, 1, Enemies(1), col);
            return got.Count == 1 ? null
                : $"新が {got.Count} 件（1 件のはず。推論の道が効いていない）";
        });
    }

    private static int FirstUnknownIndex() =>
        Enumerable.Range(0, 256).First(k => !LifeByIndex.ContainsKey(k));

    private static bool Near(double a, double b) => Math.Abs(a - b) < 1e-9;

    private static string Show(IEnumerable<int> xs) => "{" + string.Join(",", xs) + "}";
}
