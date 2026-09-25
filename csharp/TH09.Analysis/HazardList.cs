using System.Globalization;
using TH09.Generated;

namespace TH09.Analysis;

public enum HazardKind
{
    Aabb,
    Circle,
    Obb,
}

public readonly record struct HazardElement(
    HazardKind Kind, double X, double Y, double HalfX, double HalfY,
    double Radius, double Angle, double PivotX, double PivotY);

public static class HazardList
{
    private static readonly double[] CharHitRadiusTable = BuildCharHitRadius();

    private static double[] BuildCharHitRadius()
    {
        var lines = Packed.Lines(AnalysisTables.CharHitRadiusPacked);
        var arr = new double[lines.Length];
        foreach (var line in lines)
        {
            int t = line.IndexOf('\t');
            if (t < 0) throw new InvalidDataException("キャラ別の被弾サイズの表の行に区切りが無い: " + line);
            int id = int.Parse(line[..t], CultureInfo.InvariantCulture);
            double v = double.Parse(line[(t + 1)..], CultureInfo.InvariantCulture);
            if ((uint)id >= (uint)arr.Length)
                throw new InvalidDataException($"キャラ別の被弾サイズの表にキャラ ID 範囲外の行: {id}");
            arr[id] = v;
        }
        return arr;
    }

    public static int CharHitRadiusCount => CharHitRadiusTable.Length;

    public static double? CharHitRadius(int charId) =>
        (uint)charId < (uint)CharHitRadiusTable.Length ? CharHitRadiusTable[charId] : null;


    public static IReadOnlyList<HazardElement>? RealElementsOf(Window w, int side, int index)
    {
        if (side is not (1 or 2)) throw new ArgumentOutOfRangeException(nameof(side), side, "陣は 1 か 2 だけ");
        var rawCount = w.MainAt(side == 1 ? TickWords.Record.P1HitListCount : TickWords.Record.P2HitListCount, index);
        if (rawCount is null) return null;
        uint word = unchecked((uint)(long)Math.Round(rawCount.Value));
        if ((word & AnalysisTables.HitlistValid) == 0) return null;
        int count = (int)Math.Min(word & AnalysisTables.HitlistCountMask, (uint)AnalysisTables.CoordHitlistSlots);

        var outv = new List<HazardElement>(count);
        for (int i = 0; i < count; i++)
        {
            string basename = "p" + side.ToString(CultureInfo.InvariantCulture)
                               + "_h" + i.ToString(CultureInfo.InvariantCulture);
            double? Get(string suffix)
            {
                var bits = w.Raw(basename + suffix, index);
                return bits is null ? null : CoordRing.UnpackF32(bits.Value);
            }
            var x = Get("_x");
            var y = Get("_y");
            if (x is null || y is null) continue;
            double halfX = Get("_half_x") ?? 0.0;
            double halfY = Get("_half_y") ?? 0.0;
            double radius = Get("_radius") ?? 0.0;
            double angle = Get("_angle") ?? 0.0;
            double pivotX = Get("_pivot_x") ?? 0.0;
            double pivotY = Get("_pivot_y") ?? 0.0;
            var kind = radius != 0.0 ? HazardKind.Circle : angle != 0.0 ? HazardKind.Obb : HazardKind.Aabb;
            outv.Add(new HazardElement(kind, x.Value, y.Value, halfX, halfY, radius, angle, pivotX, pivotY));
        }
        return outv;
    }


    public static bool Hits(HazardElement e, double vx, double vy, double hitRadius)
    {
        switch (e.Kind)
        {
            case HazardKind.Circle:
            {
                double rr = e.Radius + hitRadius;
                double dx = e.X - vx, dy = e.Y - vy;
                return dx * dx + dy * dy < rr * rr;
            }
            case HazardKind.Obb:
            {
                double half = hitRadius * 0.5;
                double c = Math.Cos(-e.Angle), s = Math.Sin(-e.Angle);
                double dx = vx - e.PivotX, dy = vy - e.PivotY;
                double rx = e.PivotX + dx * c - dy * s;
                double ry = e.PivotY + dx * s + dy * c;
                return Math.Abs(e.X - rx) <= e.HalfX + half && Math.Abs(e.Y - ry) <= e.HalfY + half;
            }
            default:
            {
                double half = hitRadius * 0.5;
                return Math.Abs(e.X - vx) <= e.HalfX + half && Math.Abs(e.Y - vy) <= e.HalfY + half;
            }
        }
    }

    public static IReadOnlyList<int> Overlaps(
        IReadOnlyList<HazardElement> elements, double vx, double vy, double hitRadius)
    {
        var outv = new List<int>();
        for (int i = 0; i < elements.Count; i++)
            if (Hits(elements[i], vx, vy, hitRadius)) outv.Add(i);
        return outv;
    }

    public static int? FirstOverlap(IReadOnlyList<HazardElement> elements, double vx, double vy, double hitRadius)
    {
        for (int i = 0; i < elements.Count; i++)
            if (Hits(elements[i], vx, vy, hitRadius)) return i;
        return null;
    }


    public static IEnumerable<(string Label, Func<string?> Body)> SelfTestCases()
    {
        yield return ($"母数: キャラ別の被弾サイズの表が 16 行（{CharHitRadiusCount}）", () =>
            CharHitRadiusCount == 16 ? null : $"{CharHitRadiusCount} 行（16 のはず）");

        yield return ("実測との一致: 霊夢(0)=2.0 ／ てゐ等(8-13)=2.2 ／ 他(1,14,15)=2.3", () =>
        {
            static string? Near(double? got, double want, string who) =>
                got is double g && Math.Abs(g - want) < 1e-4 ? null : $"{who}: {got}（{want} のはず）";
            var errs = new List<string?>
            {
                Near(CharHitRadius(0), 2.0, "id0 霊夢"),
                Near(CharHitRadius(8), 2.2, "id8 てゐ"),
                Near(CharHitRadius(13), 2.2, "id13 映姫"),
                Near(CharHitRadius(1), 2.3, "id1"),
                Near(CharHitRadius(14), 2.3, "id14"),
                Near(CharHitRadius(15), 2.3, "id15"),
            }.Where(e => e is not null).ToList();
            return errs.Count == 0 ? null : string.Join(" / ", errs);
        });

        yield return ("範囲外のキャラ ID は null（推測しない）", () =>
            CharHitRadius(-1) is null && CharHitRadius(16) is null
                ? null : "範囲外なのに値が返った");


        yield return ("合成: 軸平行矩形は境界ちょうどで当たる（≤）", () =>
        {
            var e = new HazardElement(HazardKind.Aabb, 100, 100, 10, 10, 0, 0, 0, 0);
            return Hits(e, 112.0, 100.0, 4.0) ? null : "境界ちょうどで当たらない（≤ のはず）";
        });
        yield return ("★否定: 境界の外は当たらない", () =>
        {
            var e = new HazardElement(HazardKind.Aabb, 100, 100, 10, 10, 0, 0, 0, 0);
            return !Hits(e, 112.01, 100.0, 4.0) ? null : "境界の外なのに当たった";
        });

        yield return ("合成: 円は境界ちょうどでは当たらない（厳密な不等号）", () =>
        {
            var e = new HazardElement(HazardKind.Circle, 0, 0, 0, 0, 10, 0, 0, 0);
            return !Hits(e, 12.0, 0.0, 2.0) ? null : "円の境界ちょうどで当たった（< のはず）";
        });
        yield return ("★否定: 円は境界のわずか内側なら当たる", () =>
            Hits(new HazardElement(HazardKind.Circle, 0, 0, 0, 0, 10, 0, 0, 0), 11.99, 0.0, 2.0)
                ? null : "境界のわずか内側なのに当たらない");

        yield return ("合成: 回転矩形は pivot を中心に −角度だけ自機を回してから判定する", () =>
        {
            var e = new HazardElement(HazardKind.Obb, 0, 10, 2, 5, 0, Math.PI / 2, 0, 0);
            return Hits(e, -10.0, 0.0, 0.0) ? null : "回転を掛けずに判定している（当たるはず）";
        });
        yield return ("★否定: 回転を掛けなければ上の点は当たらない（回転を忘れた旧の形）", () =>
        {
            var e = new HazardElement(HazardKind.Obb, 0, 10, 2, 5, 0, Math.PI / 2, 0, 0);
            bool Legacy(double vx, double vy) => Math.Abs(e.X - vx) <= e.HalfX && Math.Abs(e.Y - vy) <= e.HalfY;
            return !Legacy(-10.0, 0.0) ? null : "旧の式でも当たってしまい、否定テストとして効いていない";
        });


        yield return ("合成: 重なりが複数あるとき、最初に一致した添字を返す（距離順ではない）", () =>
        {
            var near = new HazardElement(HazardKind.Aabb, 0, 0, 50, 50, 0, 0, 0, 0);
            var far = new HazardElement(HazardKind.Aabb, 0, 0, 50, 50, 0, 0, 0, 0);
            var elems = new[] { far, near };
            var got = FirstOverlap(elems, 1.0, 1.0, 0.0);
            return got == 0 ? null : $"最初の重なりが添字 {got}（0 のはず。距離順に並べ替えている疑い）";
        });
        yield return ("★否定: 距離順に並べ替えると先頭が変わってしまう", () =>
        {
            var elems = new[]
            {
                new HazardElement(HazardKind.Circle, 100, 0, 0, 0, 150, 0, 0, 0),
                new HazardElement(HazardKind.Circle, 1, 0, 0, 0, 5, 0, 0, 0),
            };
            int legacyIdx = Enumerable.Range(0, elems.Length)
                .Where(i => Hits(elems[i], 0, 0, 0.0))
                .OrderBy(i => Math.Abs(elems[i].X))
                .First();
            if (legacyIdx == 1)
            {
                var got = FirstOverlap(elems, 0, 0, 0.0);
                return got == 0 ? null : "距離順(旧)と同じ答えになった（リスト順で見ていない）";
            }
            return "合成が狂っている（旧の並べ替えが働いていない）";
        });

        yield return ("合成: 重なりが 1 つも無ければ null / 空", () =>
        {
            var elems = new[] { new HazardElement(HazardKind.Aabb, 1000, 1000, 1, 1, 0, 0, 0, 0) };
            if (FirstOverlap(elems, 0, 0, 0.0) is not null) return "重ならないのに答えが出た";
            return Overlaps(elems, 0, 0, 0.0).Count == 0 ? null : "重ならないのに Overlaps が空でない";
        });


        yield return ("合成: 有効印（bit31）が立っていなければ null（件数が 0 でなくても読まない）", () =>
        {
            var w = SynthHazardWindow(2, side: 1, index: 1,
                countWord: 5u , elems: new Dictionary<int, HazardElement>());
            return RealElementsOf(w, 1, 1) is null ? null : "有効印が無いのに要素が返った";
        });
        yield return ("★否定: 有効印を見ずにマスクだけで読むと、上の窓でも要素が返ってしまう", () =>
        {
            uint word = 5u;
            bool legacyReadable = (word & AnalysisTables.HitlistCountMask) > 0;
            return legacyReadable ? null : "合成が狂っている（マスクだけでは読めない値にした）";
        });

        yield return ("合成: 件数・形の振り分け（円/矩形/回転矩形）が本物の列から正しく読める", () =>
        {
            var elems = new Dictionary<int, HazardElement>
            {
                [0] = new HazardElement(HazardKind.Aabb, 10, 20, 3, 4, 0, 0, 0, 0),
                [1] = new HazardElement(HazardKind.Circle, -5, 6, 0, 0, 7, 0, 0, 0),
                [2] = new HazardElement(HazardKind.Obb, 1, 2, 3, 4, 0, 0.5, 9, 9),
            };
            var w = SynthHazardWindow(1, side: 2, index: 0,
                countWord: AnalysisTables.HitlistValid | 3u, elems: elems);
            var got = RealElementsOf(w, 2, 0);
            if (got is null) return "有効印を立てたのに null";
            if (got.Count != 3) return $"要素数 {got.Count}（3 のはず）";
            if (got[0].Kind != HazardKind.Aabb) return $"添字0 の形 {got[0].Kind}（Aabb のはず）";
            if (got[1].Kind != HazardKind.Circle) return $"添字1 の形 {got[1].Kind}（Circle のはず）";
            if (got[2].Kind != HazardKind.Obb) return $"添字2 の形 {got[2].Kind}（Obb のはず）";
            if (Math.Abs(got[1].Radius - 7.0) > 1e-4) return "円の半径が化けた";
            if (Math.Abs(got[2].PivotX - 9.0) > 1e-4 || Math.Abs(got[2].PivotY - 9.0) > 1e-4)
                return "回転矩形の pivot が化けた";
            return null;
        });

        yield return ("合成: 件数は下位 9 ビットだけを見る（HitlistCountMask）", () =>
        {
            var elems = new Dictionary<int, HazardElement>
            {
                [0] = new HazardElement(HazardKind.Aabb, 1, 1, 1, 1, 0, 0, 0, 0),
            };
            var w = SynthHazardWindow(1, side: 1, index: 0,
                countWord: AnalysisTables.HitlistValid | 0x00FE0000u | 1u, elems: elems);
            var got = RealElementsOf(w, 1, 0);
            return got is { Count: 1 } ? null : $"要素数 {got?.Count}（1 のはず。マスクを見ていない疑い）";
        });
    }

    private static Window SynthHazardWindow(
        int tickCount, int side, int index, uint countWord,
        IReadOnlyDictionary<int, HazardElement> elems)
    {
        var meta = new WindowMeta
        {
            SessionId = 1, WindowNo = 1, FirstSeq = 1000, FirstFrame = null,
            TickCount = tickCount, SlotCount = 0, Quant = "synth",
            Hits = [], LostTicks = 0,
        };
        var main = new Dictionary<string, TickValue[]>(StringComparer.Ordinal);
        var col = new TickValue[tickCount];
        for (int i = 0; i < tickCount; i++) col[i] = TickValue.Missing;
        col[index] = TickValue.Int(countWord);
        main["p" + side + "_hit_list_count"] = col;

        var raw = new Dictionary<string, uint[]>(StringComparer.Ordinal);
        void Put(int i, string suffix, double v)
        {
            var arr = new uint[tickCount];
            arr[index] = BitConverter.SingleToUInt32Bits((float)v);
            raw["p" + side + "_h" + i + suffix] = arr;
        }
        foreach (var (i, e) in elems)
        {
            Put(i, "_x", e.X); Put(i, "_y", e.Y);
            Put(i, "_half_x", e.HalfX); Put(i, "_half_y", e.HalfY);
            Put(i, "_radius", e.Radius); Put(i, "_angle", e.Angle);
            Put(i, "_pivot_x", e.PivotX); Put(i, "_pivot_y", e.PivotY);
        }
        return new Window(meta, new WindowColumns(raw), new MainColumns(main), MainColumns.Empty);
    }
}
