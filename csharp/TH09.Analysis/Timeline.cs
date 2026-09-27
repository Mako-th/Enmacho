using TA = TH09.Layer0.TickArchive;

namespace TH09.Analysis;


public readonly record struct TimelineLabel(TickValue Value, long? RoundDelta, long? Total)
{
    public static TimelineLabel Of(TickValue v) => new(v, null, null);

    public static TimelineLabel Cumulative(long round, long total)
        => new(TickValue.Int(total), round, total);

    public static readonly TimelineLabel Undecided = new(TickValue.Missing, null, null);

    public bool IsCumulative => RoundDelta is not null;
}

public readonly record struct TimelineRun(long Value, double Start, double End);

public sealed class TimelineBand
{
    public required string Name { get; init; }
    public required int Side { get; init; }
    public required string Key { get; init; }
    public bool DefaultOff { get; init; }
    public required (long Value, string Text)[] Legend { get; init; }
    public required List<TimelineRun> Runs { get; init; }
}

public sealed class TimelineEvent
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required int Side { get; init; }
    public required string Glyph { get; init; }
    public required bool On { get; init; }
    public required double[] Times { get; init; }
}

public sealed class TimelineRound
{
    private readonly Dictionary<string, uint[]> _raw;
    private readonly uint[] _flags;
    private readonly int[] _idx;
    private readonly Dictionary<string, TickValue[]> _cache = new(StringComparer.Ordinal);
    private List<KeyValuePair<string, TimelineLabel>>? _labels;

    internal TimelineRound(int index, long[] frames, Dictionary<string, uint[]> raw,
                           uint[] flags, int[] idx, long firstSeq, long lastSeq)
    {
        Index = index;
        Frames = frames;
        _raw = raw;
        _flags = flags;
        _idx = idx;
        FirstSeq = firstSeq;
        LastSeq = lastSeq;
        Seconds = new double[frames.Length];
        for (int i = 0; i < frames.Length; i++) Seconds[i] = frames[i] / 60.0;
    }

    public int Index { get; }

    public long[] Frames { get; }

    public double[] Seconds { get; }

    public long FirstSeq { get; }

    public long LastSeq { get; }

    public int TickCount => Frames.Length;

    public double Duration => Seconds.Length == 0 ? 0.0 : Seconds[^1] - Seconds[0];

    public bool Has(string name)
    {
        if (IsPacked(name)) return false;
        if (_raw.ContainsKey(name)) return true;
        return DerivedOf(name) is not null;
    }

    public TickValue[]? Col(string name)
    {
        if (_cache.TryGetValue(name, out var got)) return got;
        if (IsPacked(name)) return null;
        if (_raw.TryGetValue(name, out var raw))
        {
            var spec = TimelineDecode.SpecOf(name);
            var vals = new TickValue[_idx.Length];
            for (int i = 0; i < _idx.Length; i++)
                vals[i] = TimelineDecode.Decode(spec, raw[_idx[i]], _flags[_idx[i]]);
            _cache[name] = vals;
            return vals;
        }
        if (DerivedOf(name) is not (int side, int part)) return null;
        AddDerived(side);
        return _cache.TryGetValue(name, out var d) ? d : null;
    }

    public IReadOnlyList<KeyValuePair<string, TimelineLabel>> Labels
        => _labels ??= TimelineReader.BuildLabels(this);

    private static bool IsPacked(string name)
        => name is "p1_enemy_class_counts" or "p2_enemy_class_counts";

    private (int Side, int Part)? DerivedOf(string name)
    {
        if (name.Length < 4 || name[0] != 'p' || name[2] != '_') return null;
        int side = name[1] - '0';
        if (side is not (1 or 2)) return null;
        int part = Array.IndexOf(TimelineTables.DerivedFromClassCounts, name[3..]);
        if (part < 0) return null;
        return _raw.ContainsKey("p" + side + "_enemy_class_counts") ? (side, part) : null;
    }

    private void AddDerived(int side)
    {
        var packed = _raw["p" + side + "_enemy_class_counts"];
        var spec = TimelineDecode.SpecOf("p" + side + "_enemy_class_counts");
        var parts = new TickValue[3][];
        for (int p = 0; p < 3; p++) parts[p] = new TickValue[_idx.Length];
        for (int i = 0; i < _idx.Length; i++)
        {
            var v = TimelineDecode.Decode(spec, packed[_idx[i]], _flags[_idx[i]]);
            if (v.IsMissing)
            {
                for (int p = 0; p < 3; p++) parts[p][i] = TickValue.Missing;
                continue;
            }
            uint word = unchecked((uint)v.AsLong);
            parts[0][i] = TickValue.Int(word & 0xFF);
            parts[1][i] = TickValue.Int((word >> 8) & 0xFF);
            parts[2][i] = TickValue.Int((word >> 16) & 0xFF);
        }
        for (int p = 0; p < 3; p++)
            _cache["p" + side + "_" + TimelineTables.DerivedFromClassCounts[p]] = parts[p];
    }

    internal IEnumerable<string> RawNames => _raw.Keys;

    public (int? Stage, int Round) StageRound()
    {
        var st = First(Col("stage_index"));
        var cr = First(Col("completed_rounds"));
        return (st is long s ? (int)(s + 1) : null, cr is long c ? (int)(c + 1) : Index + 1);
    }

    private static long? First(TickValue[]? col)
    {
        if (col is null) return null;
        foreach (var v in col) if (!v.IsMissing) return v.AsLong;
        return null;
    }

    public HashSet<int> CpuSides()
    {
        var outSet = new HashSet<int>();
        for (int side = 1; side <= 2; side++)
        {
            var col = Col("p" + side + "_control");
            if (col is null) continue;
            foreach (var v in col)
            {
                if (v.IsMissing || v.AsLong != 1) continue;
                outSet.Add(side);
                break;
            }
        }
        return outSet;
    }

    public List<string> VisibleLines()
    {
        var cpu = CpuSides();
        var names = new List<string>(FieldDisplay.NamesOf("LINE"));
        for (int side = 1; side <= 2; side++)
            foreach (var suf in TimelineTables.DerivedFromClassCounts)
                names.Add("p" + side + "_" + suf);

        var outList = new List<string>();
        foreach (var name in names)
        {
            var col = Col(name);
            if (col is null) continue;
            if (name.Contains("_cpu_", StringComparison.Ordinal))
            {
                int side = FieldDisplay.SideOf(name);
                if (cpu.Count == 0 || (side != 0 && !cpu.Contains(side))) continue;
            }
            bool any = false;
            foreach (var v in col) if (!v.IsMissing) { any = true; break; }
            if (!any) continue;
            outList.Add(name);
        }
        return outList;
    }

    public List<KeyValuePair<string, TimelineLabel>> VisibleLabels()
    {
        var cpu = CpuSides();
        var outList = new List<KeyValuePair<string, TimelineLabel>>();
        foreach (var kv in Labels)
        {
            if (kv.Key.Contains("_cpu_", StringComparison.Ordinal))
            {
                int side = FieldDisplay.SideOf(kv.Key);
                if (cpu.Count == 0 || (side != 0 && !cpu.Contains(side))) continue;
            }
            outList.Add(kv);
        }
        return outList;
    }

    public double?[]? Series(string name)
    {
        var raw = Col(name);
        if (raw is null) return null;
        double k = FieldDisplay.Scale(name) ?? 1.0;
        var outArr = new double?[raw.Length];
        for (int i = 0; i < raw.Length; i++)
            outArr[i] = raw[i].IsMissing ? null : raw[i].AsDouble * k;
        return outArr;
    }

    public List<TimelineEvent> Events()
    {
        var outList = new List<TimelineEvent>();
        for (int side = 1; side <= 2; side++)
        {
            var hit = Col("p" + side + "_hit_kind");
            if (hit is not null)
            {
                var ts = new List<double>();
                for (int i = 0; i < hit.Length; i++)
                {
                    if (hit[i].IsMissing || (hit[i].AsLong & AnalysisTables.HitValid) == 0) continue;
                    int j = Math.Max(0, i - AnalysisTables.HitDelayTicks);
                    ts.Add(Seconds[j]);
                }
                if (ts.Count > 0)
                    outList.Add(new TimelineEvent
                    {
                        Key = "hit_" + side, Name = side + "P 被弾", Side = side,
                        Glyph = "hit", On = true, Times = ts.ToArray(),
                    });
            }
            foreach (var (field, ja, glyph) in TimelineTables.CounterEvents)
            {
                var col = Col("p" + side + "_" + field);
                if (col is null) continue;
                var ts = IncreaseTimes(col, Seconds, absolute: field != "ex_triggered");
                if (ts.Count == 0) continue;
                outList.Add(new TimelineEvent
                {
                    Key = field + "_" + side, Name = side + "P " + ja, Side = side,
                    Glyph = glyph, On = false, Times = ts.ToArray(),
                });
            }
        }
        return outList;
    }

    public List<TimelineBand> Bands()
    {
        var outList = new List<TimelineBand>();
        for (int side = 1; side <= 2; side++)
        {
            var bc = Col("p" + side + "_boss_count");
            if (bc is not null)
            {
                var on = OnOff(bc);
                on = DropCutinStale(on, side);
                outList.Add(new TimelineBand
                {
                    Name = side + "P陣のボス", Side = side, Key = "boss_" + side,
                    Legend = [(1, "ボス在場")],
                    Runs = Runs(on, Seconds, dropZero: true),
                });
            }
            foreach (var (suffix, ja) in TimelineTables.CountBands)
            {
                var col = Col("p" + side + "_" + suffix);
                if (col is null) continue;
                var runs = Runs(OnOff(col), Seconds, dropZero: true);
                if (runs.Count == 0) continue;
                outList.Add(new TimelineBand
                {
                    Name = side + "P陣の" + ja, Side = side, Key = suffix + "_" + side,
                    Legend = [(1, ja)], Runs = runs,
                });
            }
            var dm = Col("p" + side + "_cpu_dodge_mode");
            if (dm is not null && CpuSides().Contains(side))
            {
                var vals = new long?[dm.Length];
                for (int i = 0; i < dm.Length; i++) vals[i] = dm[i].IsMissing ? null : dm[i].AsLong;
                outList.Add(new TimelineBand
                {
                    Name = side + "P CPU の状態", Side = side, Key = "cpu_" + side,
                    Legend = TimelineTables.DodgeModeLegend,
                    Runs = Runs(vals, Seconds, dropZero: false),
                });
            }
            var iv = Col("p" + side + "_invincible_timer");
            if (iv is not null)
            {
                var on = new long?[iv.Length];
                for (int i = 0; i < iv.Length; i++)
                    on[i] = (!iv[i].IsMissing && iv[i].AsLong > 0) ? 1L : 0L;
                outList.Add(new TimelineBand
                {
                    Name = side + "P 無敵", Side = side, Key = "inv_" + side, DefaultOff = true,
                    Legend = [(1, "無敵")],
                    Runs = Runs(on, Seconds, dropZero: true),
                });
            }
        }
        return outList;
    }

    private static long?[] OnOff(TickValue[] col)
    {
        var outArr = new long?[col.Length];
        for (int i = 0; i < col.Length; i++)
            outArr[i] = col[i].IsMissing ? null : (col[i].AsLong > 0 ? 1L : 0L);
        return outArr;
    }

    private long?[] DropCutinStale(long?[] on, int side)
    {
        var outArr = (long?[])on.Clone();
        foreach (var field in TimelineTables.CutinFields)
        {
            var col = Col("p" + side + "_" + field);
            if (col is null || col.Length == 0) continue;
            long? prev = null;
            for (int i = 0; i < col.Length; i++)
            {
                if (col[i].IsMissing) { prev = null; continue; }
                long v = col[i].AsLong;
                if (prev is long p && v > p)
                {
                    int stop = Math.Min(outArr.Length, i + AnalysisTables.CutinFreezeFrames);
                    for (int j = i; j < stop; j++)
                        if (outArr[j] is long w && w != 0) outArr[j] = 0;
                }
                prev = v;
            }
        }
        return outArr;
    }

    internal static List<double> IncreaseTimes(TickValue[] col, double[] seconds, bool absolute)
    {
        var outList = new List<double>();
        long? prev = null;
        for (int i = 0; i < col.Length; i++)
        {
            if (col[i].IsMissing) { prev = null; continue; }
            long v = col[i].AsLong;
            if (absolute)
            {
                if (prev is long p && v > p) outList.Add(seconds[i]);
            }
            else if (v > 0)
            {
                outList.Add(seconds[i]);
            }
            prev = v;
        }
        return outList;
    }

    internal static List<TimelineRun> Runs(long?[] col, double[] seconds, bool dropZero)
    {
        var outList = new List<TimelineRun>();
        int? start = null;
        long? cur = null;
        for (int i = 0; i < col.Length; i++)
        {
            if (col[i] == cur) continue;
            if (cur is long c && start is int s && !(dropZero && c == 0))
                outList.Add(new TimelineRun(c, seconds[s], seconds[i]));
            cur = col[i];
            start = i;
        }
        if (cur is long c2 && start is int s2 && !(dropZero && c2 == 0))
            outList.Add(new TimelineRun(c2, seconds[s2], seconds[^1]));
        return outList;
    }
}

public static class TimelineReader
{
    public static List<TimelineRound> LoadSession(AnalysisDb l0, long sessionId,
                                                  IEnumerable<string>? only = null)
    {
        var want = new List<string>();
        if (only is not null) want.AddRange(only);
        else
            foreach (var n in TimelineDecode.RecordFields)
                if (FieldDisplay.Kind(n) != "NONE") want.Add(n);
        foreach (var must in TimelineTables.MustHave)
            if (!want.Contains(must, StringComparer.Ordinal)) want.Add(must);

        var wantSet = new HashSet<string>(want, StringComparer.Ordinal);
        var chunks = new List<TA.TickColumns>();
        using (var cmd = l0.Command(
                   "SELECT field_order,blob,encoding FROM session_ticks WHERE session_id=$0 ORDER BY segment_no",
                   sessionId))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                var order = TA.ParseFieldOrder(r.GetString(0));
                chunks.Add(TA.Decode((byte[])r.GetValue(1), r.GetString(2), order, wantSet));
            }
        }
        if (chunks.Count == 0) return [];

        var names = chunks[0].Names.ToList();
        int total = 0;
        foreach (var c in chunks) total += c[names[0]].Length;
        var cols = new Dictionary<string, uint[]>(names.Count, StringComparer.Ordinal);
        foreach (var name in names)
        {
            var buf = new uint[total];
            int at = 0;
            foreach (var c in chunks) { var col = c[name]; col.CopyTo(buf, at); at += col.Length; }
            cols[name] = buf;
        }
        return SplitRounds(cols);
    }

    public static List<TimelineRound> SplitRounds(Dictionary<string, uint[]> cols)
    {
        var rf = cols["round_frames"];
        var flg = cols["flags"];
        var live = new List<int>();
        for (int i = 0; i < rf.Length; i++) if (rf[i] > 0) live.Add(i);
        if (live.Count == 0) return [];

        var groups = new List<List<int>>();
        var cur = new List<int> { live[0] };
        for (int k = 1; k < live.Count; k++)
        {
            int a = live[k - 1], b = live[k];
            if (rf[b] < rf[a]) { groups.Add(cur); cur = []; }
            cur.Add(b);
        }
        groups.Add(cur);
        groups = groups.Where(g => g.Count >= 2
                                   && rf[g[^1]] - rf[g[0]] >= AnalysisTables.MinRoundFrames).ToList();

        var seq = cols.TryGetValue("seq_begin", out var sq) ? sq : null;
        var raw = new Dictionary<string, uint[]>(cols.Count, StringComparer.Ordinal);
        foreach (var (name, col) in cols)
            if (name is not ("round_frames" or "flags")) raw[name] = col;

        var rounds = new List<TimelineRound>(groups.Count);
        for (int n = 0; n < groups.Count; n++)
        {
            var idx = groups[n];
            var frames = new long[idx.Count];
            for (int i = 0; i < idx.Count; i++) frames[i] = rf[idx[i]];
            long first = seq is null ? -1 : seq[idx[0]];
            long last = seq is null ? -1 : seq[idx[^1]];
            rounds.Add(new TimelineRound(n, frames, raw, flg, idx.ToArray(), first, last));
        }
        return rounds;
    }

    internal static List<KeyValuePair<string, TimelineLabel>> BuildLabels(TimelineRound round)
    {
        var outList = new List<KeyValuePair<string, TimelineLabel>>();
        var at = new Dictionary<string, int>(StringComparer.Ordinal);
        void Put(string name, TimelineLabel v)
        {
            if (at.TryGetValue(name, out int i)) outList[i] = new(name, v);
            else { at[name] = outList.Count; outList.Add(new(name, v)); }
        }

        foreach (var name in FieldDisplay.NamesOf("LABEL"))
        {
            var col = round.Col(name);
            if (col is null || col.Length == 0) continue;
            var vals = col.Where(v => !v.IsMissing).ToList();
            if (vals.Count == 0) continue;
            if (TimelineTables.CumulativeSuffix.Any(s => name.EndsWith(s, StringComparison.Ordinal)))
            {
                Put(name, TimelineLabel.Cumulative(vals[^1].AsLong - vals[0].AsLong, vals[^1].AsLong));
            }
            else
            {
                Put(name, TimelineLabel.Of(IsFinalValue(name) ? vals[^1] : vals[0]));
            }
        }

        if (round.Col("internal_rank") is TickValue[] rank)
        {
            foreach (var v in rank)
            {
                if (v.IsMissing) continue;
                Put("initial_rank", TimelineLabel.Of(v));
                break;
            }
        }
        Winner(round, Put);
        return outList;
    }

    private static void Winner(TimelineRound round, Action<string, TimelineLabel> put)
    {
        var got = new Dictionary<int, long>();
        for (int side = 1; side <= 2; side++)
        {
            if (round.Col("p" + side + "_wins") is not TickValue[] raw) continue;
            var col = raw.Where(v => !v.IsMissing).ToList();
            if (col.Count == 0) continue;
            put("p" + side + "_wins", TimelineLabel.Of(col[^1]));
            got[side] = col[^1].AsLong - col[0].AsLong;
        }
        var winners = got.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
        if (winners.Count == 1) put("result_winner", TimelineLabel.Of(TickValue.Int(winners[0])));
        else if (got.Count > 0) put("result_winner", TimelineLabel.Undecided);
    }

    private static bool IsFinalValue(string name)
        => name.StartsWith("clear_", StringComparison.Ordinal)
           || TimelineTables.FinalSuffix.Any(s => name.EndsWith(s, StringComparison.Ordinal));

    public static List<(double T, double? V)> Envelope(double[] seconds, double?[] values, int width)
    {
        int n = values.Length;
        var outList = new List<(double, double?)>();
        if (width <= 0 || n <= width * 2)
        {
            for (int i = 0; i < n; i++) outList.Add((seconds[i], values[i]));
            return outList;
        }
        double step = n / (double)width;
        for (int k = 0; k < width; k++)
        {
            int a = (int)(k * step);
            int b = Math.Max((int)(k * step) + 1, (int)((k + 1) * step));
            int end = Math.Min(b, n);
            int lo = -1, hi = -1;
            for (int i = a; i < end; i++)
            {
                if (values[i] is not double v) continue;
                if (lo < 0 || v < values[lo]!.Value) lo = i;
                if (hi < 0 || v > values[hi]!.Value) hi = i;
            }
            if (lo < 0)
            {
                outList.Add((seconds[Math.Min(a, n - 1)], null));
                continue;
            }
            if (seconds[lo] <= seconds[hi])
            {
                outList.Add((seconds[lo], values[lo]));
                outList.Add((seconds[hi], values[hi]));
            }
            else
            {
                outList.Add((seconds[hi], values[hi]));
                outList.Add((seconds[lo], values[lo]));
            }
        }
        return outList;
    }
}

internal static class TimelineTables
{
    public static readonly string[] MustHave = ["round_frames", "flags"];

    public static readonly string[] DerivedFromClassCounts =
        Packed.Lines(AnalysisTables.DerivedFromClassCountsPacked);

    public static readonly string[] FinalSuffix = Packed.Lines(AnalysisTables.FinalSuffixPacked);

    public static readonly string[] CumulativeSuffix = Packed.Lines(AnalysisTables.CumulativeSuffixPacked);

    public static readonly (string Field, string Ja, string Glyph)[] CounterEvents =
    [
        ("spell_attacks", "カードアタック", "card"),
        ("boss_attacks", "ボスアタック", "boss"),
        ("boss_reversals", "リバーサル", "rev"),
        ("ex_triggered", "Ex 発動", "ex"),
    ];

    public static readonly string[] CutinFields = ["boss_attacks", "boss_reversals"];

    public static readonly (string Suffix, string Ja)[] CountBands =
    [
        ("c2_count", "C2"),
        ("c3_count", "C3"),
    ];

    public static readonly (long Value, string Text)[] DodgeModeLegend =
    [
        (0, "詰みクイック"),
        (1, "詰み被弾"),
        (3, "棒立ち"),
    ];
}
