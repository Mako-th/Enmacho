using System.Text.Json;
using TA = TH09.Layer0.TickArchive;

namespace TH09.Analysis;

public static class HitWindowReader
{
    public static Window? Open(AnalysisDb l0, long sessionId, long windowNo,
                               HashSet<string>? only = null, int? pre = null)
    {
        if (!l0.HasTable("session_hit_windows")) return null;
        WindowMeta meta;
        TA.TickColumns cols;
        using (var cmd = l0.Command(
                   "SELECT first_seq,first_frame,tick_count,slot_count,quant,hits,lost_ticks,"
                   + "field_order,field_order_encoding,encoding,blob"
                   + " FROM session_hit_windows WHERE session_id=$0 AND window_no=$1", sessionId, windowNo))
        using (var r = cmd.ExecuteReader())
        {
            if (!r.Read()) return null;
            meta = new WindowMeta
            {
                SessionId = sessionId,
                WindowNo = windowNo,
                FirstSeq = r.GetInt64(0),
                FirstFrame = r.IsDBNull(1) ? null : r.GetInt64(1),
                TickCount = r.GetInt32(2),
                SlotCount = r.GetInt32(3),
                Quant = r.GetString(4),
                Hits = ParseHits(r.GetString(5)),
                LostTicks = r.GetInt32(6),
            };
            var order = TA.UnpackFieldOrder((byte[])r.GetValue(7), r.GetString(8));
            cols = TA.Decode((byte[])r.GetValue(10), r.GetString(9), order, only);
        }

        var (main, preCols) = MainSlice(l0, sessionId, meta.FirstSeq, meta.TickCount,
                                        pre ?? AnalysisTables.PreMaxTicks);
        return new Window(meta, cols, main, preCols);
    }

    public static Window? OpenForDisplay(AnalysisDb l0, long sessionId, long windowNo,
                                         HashSet<string>? only = null, int? pre = null)
    {
        var w = Open(l0, sessionId, windowNo, only, pre);
        return w is null ? null : WindowCut.ForDisplay(w);
    }

    private static List<HitEvent> ParseHits(string json)
    {
        var outList = new List<HitEvent>();
        if (string.IsNullOrEmpty(json)) return outList;
        using var doc = JsonDocument.Parse(json);
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            outList.Add(new HitEvent(e.GetProperty("seq").GetInt64(),
                                     e.GetProperty("side").GetInt32(),
                                     e.GetProperty("trigger").GetString()!));
        }
        return outList;
    }

    public static (MainColumns Window, MainColumns Pre) MainSlice(
        AnalysisDb l0, long sessionId, long firstSeq, int count, int pre)
    {
        var want = new HashSet<string>(TimelineDecode.RecordFields, StringComparer.Ordinal);
        var chunks = new List<TA.TickColumns>();
        using (var cmd = l0.Command(
                   "SELECT field_order,blob,encoding FROM session_ticks WHERE session_id=$0 ORDER BY segment_no", sessionId))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                var order = TA.ParseFieldOrder(r.GetString(0));
                chunks.Add(TA.Decode((byte[])r.GetValue(1), r.GetString(2), order, want));
            }
        }
        if (chunks.Count == 0) return (MainColumns.Empty, MainColumns.Empty);

        var names = chunks[0].Names.ToList();
        var raw = new Dictionary<string, uint[]>(StringComparer.Ordinal);
        int total = chunks.Sum(c => c[names[0]].Length);
        foreach (var name in names)
        {
            var buf = new uint[total];
            int at = 0;
            foreach (var c in chunks) { var col = c[name]; col.CopyTo(buf, at); at += col.Length; }
            raw[name] = buf;
        }

        var seqs = raw["seq_begin"];
        int start = Array.IndexOf(seqs, unchecked((uint)firstSeq));
        if (start < 0) return (MainColumns.Empty, MainColumns.Empty);
        int end = Math.Min(start + count, seqs.Length);
        var flags = raw["flags"];
        int cut = PreStart(raw, flags, start, pre);

        return (Slice(raw, flags, start, end), cut < start ? Slice(raw, flags, cut, start) : MainColumns.Empty);
    }

    private static MainColumns Slice(Dictionary<string, uint[]> raw, uint[] flags, int from, int to)
    {
        var outMap = new Dictionary<string, TickValue[]>(raw.Count, StringComparer.Ordinal);
        foreach (var (name, col) in raw)
        {
            var spec = TimelineDecode.SpecOf(name);
            var vals = new TickValue[to - from];
            for (int i = from; i < to; i++) vals[i - from] = TimelineDecode.Decode(spec, col[i], flags[i]);
            outMap[name] = vals;
        }
        return new MainColumns(outMap);
    }

    public static int PreStart(Dictionary<string, uint[]> raw, uint[] flags, int start, int pre)
    {
        if (pre <= 0) return start;
        var seqs = raw["seq_begin"];
        if (!raw.TryGetValue("round_frames", out var rf)) return start;
        var spec = TimelineDecode.SpecOf("round_frames");
        int k = start;
        while (k > 0 && start - k < pre)
        {
            if (seqs[k - 1] != seqs[k] - 1) break;
            var a = TimelineDecode.Decode(spec, rf[k - 1], flags[k - 1]);
            var b = TimelineDecode.Decode(spec, rf[k], flags[k]);
            if (a.IsMissing || b.IsMissing) break;
            if (a.AsLong > b.AsLong || a.AsLong == 0) break;
            k--;
        }
        return k;
    }
}

public sealed class WindowMeta
{
    public required long SessionId { get; init; }
    public required long WindowNo { get; init; }
    public required long FirstSeq { get; init; }
    public required long? FirstFrame { get; init; }
    public required int TickCount { get; init; }
    public required int SlotCount { get; init; }
    public required string Quant { get; init; }
    public required List<HitEvent> Hits { get; init; }
    public required int LostTicks { get; init; }

    public WindowMeta Shifted(long firstSeq, int tickCount, long? firstFrame) => new()
    {
        SessionId = SessionId,
        WindowNo = WindowNo,
        FirstSeq = firstSeq,
        FirstFrame = firstFrame,
        TickCount = tickCount,
        SlotCount = SlotCount,
        Quant = Quant,
        Hits = Hits,
        LostTicks = LostTicks,
    };
}

public readonly record struct HitEvent(long Seq, int Side, string Trigger);

public sealed class MainColumns
{
    private readonly Dictionary<string, TickValue[]> _cols;
    public MainColumns(Dictionary<string, TickValue[]> cols) => _cols = cols;
    public static readonly MainColumns Empty = new(new Dictionary<string, TickValue[]>(StringComparer.Ordinal));

    public int Count => _cols.Count;
    public int TickCount => _cols.Count == 0 ? 0 : _cols.Values.First().Length;
    public bool Has(string name) => _cols.ContainsKey(name);

    public MainColumns Slice(int from)
    {
        if (from <= 0) return this;
        var map = new Dictionary<string, TickValue[]>(_cols.Count, StringComparer.Ordinal);
        foreach (var (name, col) in _cols)
        {
            int keep = Math.Max(0, col.Length - from);
            var buf = new TickValue[keep];
            if (keep > 0) Array.Copy(col, from, buf, 0, keep);
            map[name] = buf;
        }
        return new MainColumns(map);
    }

    public TickValue[] this[string name] =>
        _cols.TryGetValue(name, out var v) ? v
        : throw new KeyNotFoundException($"主リングにその語の列がありません: {name}");
}

public static class WindowCut
{
    public const int RoundCutValidTicks = AnalysisTables.RoundCutValidTicks;

    public const int RoundOverShowTicks = AnalysisTables.RoundOverShowTicks;

    public static int RoundCutWantTicks => RoundCutValidTicks + RoundOverShowTicks;

    public static int? RoundDecided(Window w)
    {
        if (!w.Main.Has("result_state")) return null;
        var col = w.Main["result_state"];
        int end = Math.Min(w.TickCount, col.Length);
        for (int i = 1; i < end; i++)
        {
            if (col[i - 1].IsMissing || col[i].IsMissing) continue;
            if ((long)col[i - 1].AsDouble == 0 && (long)col[i].AsDouble != 0) return i;
        }
        return null;
    }

    public static int? RoundCutIndex(Window w, int side)
    {
        var d = RoundDecided(w);
        if (d is null) return null;
        var adv = EnemyBlasts.AdvanceFlags(w, side);
        int want = RoundCutWantTicks;
        int n = 0;
        for (int i = d.Value; i < w.TickCount; i++)
        {
            if (n >= want) return i;
            if (i < adv.Length && adv[i]) n++;
        }
        return null;
    }

    public static int? RoundStartCutIndex(Window w)
    {
        var ev = w.Events();
        int? first = ev.Count == 0 ? null : ev.Min(e => e.Index);
        foreach (var m in FreezeSpans.Of(w).Spans)
        {
            if (!string.Equals(m.Kind, FreezeSpans.KindRoundStart, StringComparison.Ordinal)) continue;
            if (m.Index <= 0) continue;
            if (first is not null && m.Index >= first.Value) continue;
            return m.Index - 1;
        }
        return null;
    }

    public static Window CutHead(Window w, int cut)
    {
        if (cut <= 0) return w;
        var cols = w.Cols.Slice(cut);
        var main = w.Main.Slice(cut);
        int n = Math.Max(0, w.TickCount - cut);
        var head = main.Has("round_frames") && main["round_frames"].Length > 0
                   && !main["round_frames"][0].IsMissing
                   ? (long?)(long)main["round_frames"][0].AsDouble
                   : w.Meta.FirstFrame;
        return new Window(w.Meta.Shifted(w.Meta.FirstSeq + cut, n, head), cols, main, MainColumns.Empty);
    }

    public static Window CutTail(Window w, int tickCount)
    {
        if (tickCount >= w.TickCount || tickCount < 0) return w;
        return new Window(w.Meta.Shifted(w.Meta.FirstSeq, tickCount, w.Meta.FirstFrame),
                          w.Cols, w.Main, w.Pre);
    }

    public static Window ForDisplay(Window w)
    {
        var head = RoundStartCutIndex(w);
        var cut = head is > 0 ? CutHead(w, head.Value) : w;
        var ev = cut.Events();
        if (ev.Count == 0) return cut;
        int side = ev[cut.PrimaryEventIndex() ?? 0].Side;
        var tail = RoundCutIndex(cut, side);
        return tail is null ? cut : CutTail(cut, tail.Value + 1);
    }


    public static Window Synth(int tickCount, Dictionary<string, TickValue[]> main,
                               int[]? hits = null, Dictionary<string, TickValue[]>? pre = null,
                               int side = 1, long firstSeq = 1000)
    {
        var meta = new WindowMeta
        {
            SessionId = 1,
            WindowNo = 1,
            FirstSeq = firstSeq,
            FirstFrame = null,
            TickCount = tickCount,
            SlotCount = 0,
            Quant = "synth",
            Hits = (hits ?? []).Select(i => new HitEvent(firstSeq + i, side, "life_raw")).ToList(),
            LostTicks = 0,
        };
        return new Window(meta,
                          new WindowColumns(new Dictionary<string, uint[]>(StringComparer.Ordinal)),
                          new MainColumns(main),
                          pre is null ? MainColumns.Empty : new MainColumns(pre));
    }

    private static TickValue[] IntCol(int n, Func<int, long> f)
    {
        var v = new TickValue[n];
        for (int i = 0; i < n; i++) v[i] = TickValue.Int(f(i));
        return v;
    }

    private static TickValue[] FloatCol(int n, Func<int, double> f)
    {
        var v = new TickValue[n];
        for (int i = 0; i < n; i++) v[i] = TickValue.Float(f(i));
        return v;
    }

    private static Dictionary<string, TickValue[]> NewMain() => new(StringComparer.Ordinal);

    private static Window DecidedWindow(int n, int decidedAt, Func<int, bool> valid, int[]? hits = null)
    {
        long v = AnalysisTables.HitlistValid;
        var main = NewMain();
        main["result_state"] = IntCol(n, i => i < decidedAt ? 0 : 1);
        main["p1_hit_list_count"] = IntCol(n, i => valid(i) ? v : 0);
        main["p2_hit_list_count"] = IntCol(n, i => valid(i) ? v : 0);
        return Synth(n, main, hits);
    }

    private static Window RoundStartWindow(int n, int bandAt, int bandLen, int[]? hits,
                                           int? decidedAt = null)
    {
        long v = AnalysisTables.HitlistValid;
        long on = AnalysisTables.FlagPlayersValid;
        var main = NewMain();
        main["flags"] = IntCol(n, i => i < bandAt ? 0 : on);
        main["p1_hit_list_count"] = IntCol(n, i => i >= bandAt && i < bandAt + bandLen ? 0 : v);
        main["p2_hit_list_count"] = IntCol(n, i => i >= bandAt && i < bandAt + bandLen ? 0 : v);
        if (decidedAt is not null) main["result_state"] = IntCol(n, i => i < decidedAt.Value ? 0 : 1);
        return Synth(n, main, hits);
    }

    private static int? KindIndex(Window w, string kind)
    {
        foreach (var m in FreezeSpans.Of(w).Spans)
            if (string.Equals(m.Kind, kind, StringComparison.Ordinal)) return m.Index;
        return null;
    }

    public static IEnumerable<(string Label, Func<string?> Body)> SelfTestCases()
    {

        yield return ("合成: 決着の 14+57 VALID 後で切る（決着 20 → index 91 まで見せる）", () =>
        {
            var w = DecidedWindow(200, 20, _ => true, [5]);
            var cut = RoundCutIndex(w, 1);
            if (cut != 91) return $"打ち切り位置 {cut}（91 のはず）";
            return CutTail(w, cut.Value + 1).TickCount == 92 ? null : "長さが 92 にならない";
        });

        yield return ("★否定: 生 tick で数える旧なら、決着直後の 32 tick 凍結のぶんだけ早く切る", () =>
        {
            var w = DecidedWindow(200, 20, i => i < 21 || i > 52, [5]);
            var cut = RoundCutIndex(w, 1);
            int legacy = 20 + RoundCutWantTicks;
            if (legacy != 91) return $"旧の位置 {legacy}（91 のはず。合成が狂っている）";
            if (cut != 123) return $"打ち切り位置 {cut}（123 のはず）";
            return cut != legacy ? null : "旧と同じ位置になった（VALID を数えていない）";
        });

        yield return ("★「無い」と「0」: 猶予に届かない窓は切らない（null であって 0 ではない）", () =>
        {
            var w = DecidedWindow(70, 20, _ => true, [5]);
            if (RoundCutIndex(w, 1) is int got) return $"打ち切り位置 {got}（届かないので null のはず）";
            return ForDisplay(w).TickCount == 70 ? null : "届いていないのに窓が縮んだ";
        });

        yield return ("★「無い」と「0」: 窓の頭から既に決着している窓は null（0 に寄せない）", () =>
        {
            var main = NewMain();
            main["result_state"] = IntCol(60, _ => 1);
            main["p1_hit_list_count"] = IntCol(60, _ => AnalysisTables.HitlistValid);
            var w = Synth(60, main, [5]);
            return RoundDecided(w) is null ? null : $"決着 {RoundDecided(w)}（言えないはず）";
        });

        yield return ("合成: 事象が 1 つも無い窓では尻を切らない（側が決まらない）", () =>
        {
            var w = DecidedWindow(200, 20, _ => true);
            return ForDisplay(w).TickCount == 200 ? null : "側が無いのに切った";
        });


        yield return ("合成: ラウンド開始の帯（index 10）の 1 tick 手前で切る", () =>
        {
            var w = RoundStartWindow(60, 10, 5, [40]);
            if (KindIndex(w, FreezeSpans.KindRoundStart) != 10) return "合成の帯が round_start にならない";
            var cut = RoundStartCutIndex(w);
            return cut == 9 ? null : $"切る位置 {cut}（9 のはず）";
        });

        yield return ("★否定: 帯の頭ちょうど（10）で切る旧なら、切った窓で帯の名前が変わる", () =>
        {
            var w = RoundStartWindow(60, 10, 5, [40]);
            var legacy = KindIndex(CutHead(w, 10), FreezeSpans.KindRoundStart);
            if (legacy is not null) return $"旧でも round_start が残った（{legacy}）。合成が狂っている";
            return KindIndex(CutHead(w, 9), FreezeSpans.KindRoundStart) == 1
                   ? null : "1 tick 手前で切っても round_start が残らない";
        });

        yield return ("合成: 窓の起点（被弾）より後ろの帯では切らない", () =>
        {
            var w = RoundStartWindow(60, 10, 5, [5]);
            if (KindIndex(w, FreezeSpans.KindRoundStart) != 10) return "合成の帯が round_start にならない";
            return RoundStartCutIndex(w) is null ? null : "被弾より後ろで切った（被弾が消える）";
        });

        yield return ("合成: index 0 の帯では切らない（切る所が無い）", () =>
        {
            long v = AnalysisTables.HitlistValid;
            var main = NewMain();
            main["flags"] = IntCol(60, i => i < 1 ? 0 : AnalysisTables.FlagPlayersValid);
            main["p1_hit_list_count"] = IntCol(60, i => i < 5 ? 0 : v);
            main["p2_hit_list_count"] = IntCol(60, i => i < 5 ? 0 : v);
            var w = Synth(60, main, [40]);
            if (KindIndex(w, FreezeSpans.KindRoundStart) != 0) return "合成の帯が index 0 に出ない";
            return RoundStartCutIndex(w) is null ? null : "index 0 の帯で切った";
        });

        yield return ("合成: 頭を切ると事象の位置が first_seq 経由で追随する（40 → 31）", () =>
        {
            var w = RoundStartWindow(60, 10, 5, [40]);
            var cut = CutHead(w, 9);
            if (cut.Meta.FirstSeq != w.Meta.FirstSeq + 9) return "first_seq が動いていない";
            if (cut.TickCount != 51) return $"長さ {cut.TickCount}（51 のはず）";
            return cut.Events()[0].Index == 31 ? null : $"事象 {cut.Events()[0].Index}（31 のはず）";
        });

        yield return ("★否定: 列だけずらして first_seq を直さない旧なら、事象が 9 tick ずれる", () =>
        {
            var w = RoundStartWindow(60, 10, 5, [40]);
            var legacy = new Window(w.Meta.Shifted(w.Meta.FirstSeq, 51, null),
                                    w.Cols.Slice(9), w.Main.Slice(9), MainColumns.Empty);
            return legacy.Events()[0].Index == 40 ? null : "旧が 40 を返さない（合成が狂っている）";
        });


        yield return ("★否定: 頭を切って Pre を残す旧なら、前のラウンドの発動が今の窓の起点に出る", () =>
        {
            var main = NewMain();
            main["p1_gauge"] = FloatCol(20, _ => 400.0);
            var pre = NewMain();
            pre["p1_gauge"] = FloatCol(4, i => i == 3 ? 200.0 : 400.0);
            var w = Synth(20, main, [10], pre);
            if (w.PreCount != 4) return "合成の Pre が 4 tick 無い";

            var kept = new Window(w.Meta.Shifted(w.Meta.FirstSeq + 1, 19, null),
                                  w.Cols.Slice(1), w.Main.Slice(1), w.Pre);
            var keptCards = CardEvents.For(kept).Cards();
            if (keptCards.Count != 1) return $"旧の起点 {keptCards.Count} 件（1 件のはず。合成が狂っている）";
            if (keptCards[0].Index >= 0) return "旧の起点が窓の中に出た（合成が狂っている）";

            var cut = CutHead(w, 1);
            if (cut.PreCount != 0) return "Pre を捨てていない";
            int n = CardEvents.For(cut).Cards().Count;
            return n == 0 ? null : $"頭を切った窓に前のラウンドの起点が {n} 件残った";
        });


        yield return ("★否定: 尻を先に切る旧なら、頭のラウンド開始が窓の外へ落ちて切れなくなる", () =>
        {
            var w = RoundStartWindow(300, 100, 5, [150], decidedAt: 20);

            var tail = RoundCutIndex(w, 1);
            if (tail != 91) return $"尻の位置 {tail}（91 のはず。合成が狂っている）";
            var tailFirst = CutTail(w, tail.Value + 1);
            var h = RoundStartCutIndex(tailFirst);
            var legacy = h is > 0 ? CutHead(tailFirst, h.Value) : tailFirst;
            if (legacy.TickCount != 92) return $"旧の長さ {legacy.TickCount}（92 のはず）";

            var got = ForDisplay(w);
            if (got.TickCount != 201) return $"長さ {got.TickCount}（201 のはず）";
            if (got.Meta.FirstSeq != w.Meta.FirstSeq + 99) return "頭が 99 tick 落ちていない";
            return got.TickCount != legacy.TickCount ? null : "旧と同じ答えになった（順が効いていない）";
        });
    }
}
