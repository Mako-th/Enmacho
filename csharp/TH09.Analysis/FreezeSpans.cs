namespace TH09.Analysis;

public static class FreezeSpans
{



    public const string KindCutin = "cutin";
    public const string KindRoundStart = "round_start";
    public const string KindRoundEnd = "round_end";
    public const string KindRoundOver = "round_over";
    public const string KindBoth = "both";
    public const string KindTimeStop = "time_stop";
    public const string KindTimeStopMirror = "time_stop_mirror";

    private static readonly Dictionary<string, string> KindJa = BuildKindJa();

    private static Dictionary<string, string> BuildKindJa()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in Packed.Lines(AnalysisTables.FreezeKindsPacked))
        {
            var f = line.Split('\t');
            map[f[0]] = f[1];
        }
        return map;
    }

    private const string SideMark = "%s";

    public static string UncertainSuffix => AnalysisTables.FreezeUncertainSuffix;

    public static string WarnStopSides => AnalysisTables.FreezeWarnStopSides;


    public const string RoleAll = "all";

    public const string RoleField = "field";

    public const string RoleFieldBoth = "field_both";

    private static readonly Dictionary<string, string> RoleNoteJa = new(StringComparer.Ordinal)
    {
        [RoleAll] = "",
        [RoleField] = "（自機だけ動ける）",
        [RoleFieldBoth] = "（自機は両方とも動ける）",
    };


    private static readonly string[] WordFields = Packed.Lines(AnalysisTables.FreezeWordFieldsPacked);

    private static readonly string[] ValidFields = ["p1_hit_list_count", "p2_hit_list_count"];

    private static readonly string[] NeededFields =
    [
        "p1_hit_list_count", "p2_hit_list_count",
        "p1_game_flags", "p2_game_flags", "global_state",
        "p1_boss_attacks", "p2_boss_attacks",
        "p1_boss_reversals", "p2_boss_reversals",
        "p1_boss_type", "p2_boss_type",
        "p1_gauge", "p2_gauge",
        "p1_life_raw", "p2_life_raw",
        "flags", "round_frames", "result_state",
        "p1_character", "p2_character",
    ];

    private static readonly string[] MarkPriority = Packed.Lines(AnalysisTables.FreezeMarkPriorityPacked);

    private static readonly uint[] MirrorSkipEnemyCats =
        Packed.Lines(AnalysisTables.MirrorSkipEnemyCatsPacked).Select(uint.Parse).ToArray();


    public static bool HasWords(Window w) => WordFields.All(w.Main.Has);

    public static bool HasValidWords(Window w) => ValidFields.All(w.Main.Has);

    public static bool CanRead(Window w) => HasWords(w) || HasValidWords(w);

    public static FreezeResult Of(Window w) => Of(InputOf(w));

    public static FreezeInput InputOf(Window w)
    {
        var cols = new Dictionary<string, double?[]>(StringComparer.Ordinal);
        foreach (var name in NeededFields)
        {
            var col = w.Series(name);
            if (col is not null) cols[name] = col;
        }
        return new FreezeInput(w.TickCount, cols, SakuyaMirrorOf(w), () => BoardStill(w),
                               CardEvents.For(w));
    }

    public static FreezeResult Of(FreezeInput f)
    {
        bool words = HasWords(f);
        bool valid = HasValidWords(f);
        var spans = FinishSpans(f, words ? WordSpans(f) : LegacySpans(f));
        return new FreezeResult(words || valid, words, spans, f.Cards is not null);
    }

    public static bool?[] FrozenFlags(Window w) => FrozenFlags(Of(w), w.TickCount);

    public static bool?[] FrozenFlags(FreezeResult r, int tickCount)
    {
        var outv = new bool?[tickCount];
        for (int i = 0; i < outv.Length; i++) outv[i] = r.Readable ? false : null;
        foreach (var sp in r.Spans)
            for (int i = Math.Max(0, sp.Index); i < Math.Min(tickCount, sp.Index + sp.Frames); i++)
                outv[i] = true;
        return outv;
    }

    public static FreezeSpan? At(FreezeResult r, int tick)
    {
        FreezeSpan? best = null;
        foreach (var sp in r.Spans)
        {
            if (tick < sp.Index || tick >= sp.Index + sp.Frames) continue;
            if (best is null || sp.Depth > best.Value.Depth) best = sp;
        }
        return best;
    }

    public static string? Role(FreezeSpan? sp, int boardSide)
    {
        if (sp is null) return null;
        var v = sp.Value;
        if (string.Equals(v.Kind, KindTimeStopMirror, StringComparison.Ordinal)) return RoleFieldBoth;
        if (v.Side is null) return RoleAll;
        return v.Side.Value == boardSide ? RoleAll : RoleField;
    }

    public static string RoleNote(string? role) =>
        role is not null && RoleNoteJa.TryGetValue(role, out var s) ? s : "";

    public static string Label(string kind, int? side = null, bool certain = true, int? lead = null)
    {
        var text = KindJa.TryGetValue(kind, out var ja) ? ja : KindJa[KindBoth];
        int? who = side;
        if (string.Equals(kind, KindTimeStop, StringComparison.Ordinal) && side is not null)
            who = 3 - side.Value;
        if (text.Contains(SideMark, StringComparison.Ordinal))
            text = text.Replace(SideMark, who is null ? "?" : who.Value.ToString(),
                                StringComparison.Ordinal);
        if (lead is not null) text = CardEvents.SideLabel(lead, text)!;
        return certain ? text : text + UncertainSuffix;
    }


    private static bool HasWords(FreezeInput f) => WordFields.All(n => f.Series(n) is not null);

    private static bool HasValidWords(FreezeInput f) => ValidFields.All(n => f.Series(n) is not null);

    private static (bool? P1, bool? P2)[]? ValidFlags(FreezeInput f)
    {
        var c1 = f.Series("p1_hit_list_count");
        var c2 = f.Series("p2_hit_list_count");
        if (c1 is null || c2 is null) return null;
        var outv = new (bool?, bool?)[f.TickCount];
        for (int i = 0; i < outv.Length; i++)
            outv[i] = (Bit(At(c1, i), AnalysisTables.HitlistValid), Bit(At(c2, i), AnalysisTables.HitlistValid));
        return outv;
    }

    private const int DownP1 = 1;
    private const int DownP2 = 2;

    private static int DownOf((bool? P1, bool? P2) row) =>
        (row.P1 == false ? DownP1 : 0) | (row.P2 == false ? DownP2 : 0);

    private static int SidesDown(int mask) => mask == (DownP1 | DownP2) ? 2 : (mask == 0 ? 0 : 1);

    private static int OneSide(int mask) => mask == DownP1 ? 1 : 2;

    private static List<(int Start, int Frames, T Value)> WordRuns<T>(int n, Func<int, T?> valueOf)
        where T : struct
    {
        var outv = new List<(int, int, T)>();
        int start = -1;
        T cur = default;
        bool has = false;

        void Flush(int end)
        {
            if (has) outv.Add((start, end - start, cur));
            has = false;
        }

        for (int i = 0; i < n; i++)
        {
            var v = valueOf(i);
            if (v is null) { Flush(i); continue; }
            if (has && EqualityComparer<T>.Default.Equals(cur, v.Value)) continue;
            Flush(i);
            start = i; cur = v.Value; has = true;
        }
        Flush(n);
        return outv;
    }

    private static List<(int Start, int Frames, bool Mismatch)> TimeStopRuns(FreezeInput f)
    {
        var gf1 = f.Series("p1_game_flags");
        var gf2 = f.Series("p2_game_flags");

        bool? Up(double?[]? col, int i) => Bit(At(col, i), AnalysisTables.GfTimeStop);

        bool? Value(int i)
        {
            var a = Up(gf1, i);
            var b = Up(gf2, i);
            if (a is null && b is null) return null;
            if (a != true && b != true) return null;
            if (a is null || b is null) return false;
            return a.Value != b.Value;
        }

        return WordRuns(f.TickCount, Value);
    }

    private static List<(int Start, int Frames, int CutSide)> CutinRuns(FreezeInput f)
    {
        var col = f.Series("global_state");

        int? Value(int i)
        {
            var v = At(col, i);
            if (v is null) return null;
            uint w = U32(v.Value);
            bool p1 = (w & AnalysisTables.GsFreezeP1) != 0;
            bool p2 = (w & AnalysisTables.GsFreezeP2) != 0;
            if (!p1 && !p2) return null;
            if (p1 && p2) return 0;
            return p1 ? 1 : 2;
        }

        return WordRuns(f.TickCount, Value);
    }

    private static List<(int Start, int Frames, T Value)> Grow<T>(
        int n, (bool? P1, bool? P2)[]? flags, List<(int Start, int Frames, T Value)> runs)
        where T : struct
    {
        if (flags is null) return runs;
        bool Down(int i) => i >= 0 && i < n && DownOf(flags[i]) != 0;

        var taken = new bool[n];
        foreach (var r in runs)
            for (int i = Math.Max(0, r.Start); i < Math.Min(n, r.Start + r.Frames); i++) taken[i] = true;

        var outv = new List<(int, int, T)>(runs.Count);
        foreach (var r in runs)
        {
            int a = r.Start, b = r.Frames;
            for (int k = 0; k < AnalysisTables.FreezeWordEdgeTicks; k++)
            {
                if (a - 1 < 0 || !Down(a - 1) || taken[a - 1]) break;
                a--; b++; taken[a] = true;
            }
            for (int k = 0; k < AnalysisTables.FreezeWordEdgeTicks; k++)
            {
                int e = a + b;
                if (!Down(e) || taken[e]) break;
                b++; taken[e] = true;
            }
            outv.Add((a, b, r.Value));
        }
        return outv;
    }

    private static (int? Side, bool Known) StoppedSide(
        int n, (bool? P1, bool? P2)[]? flags, int start, int frames,
        List<(int Start, int Frames, int CutSide)> cutins)
    {
        if (flags is null) return (null, false);
        var inside = new bool[n];
        foreach (var c in cutins)
            for (int i = Math.Max(0, c.Start); i < Math.Min(n, c.Start + c.Frames); i++) inside[i] = true;

        int down = 0;
        for (int i = Math.Max(0, start); i < Math.Min(n, start + frames); i++)
        {
            if (inside[i]) continue;
            down |= DownOf(flags[i]);
        }
        return (SidesDown(down) == 1 ? OneSide(down) : null, true);
    }

    private static List<FreezeSpan> WordSpans(FreezeInput f)
    {
        int n = f.TickCount;
        var flags = ValidFlags(f);
        var stops = Grow(n, flags, TimeStopRuns(f));
        var cutins = Grow(n, flags, CutinRuns(f));
        var outv = new List<FreezeSpan>();
        var covered = new bool[n];

        void Cover(int start, int frames)
        {
            for (int i = Math.Max(0, start); i < Math.Min(n, start + frames); i++) covered[i] = true;
        }

        foreach (var (start, frames, mismatch) in stops)
        {
            var (side, known) = StoppedSide(n, flags, start, frames, cutins);
            var kind = side is not null || !known ? KindTimeStop : KindTimeStopMirror;
            int? lead = kind == KindTimeStopMirror ? TimeStopLead(f, start) : null;
            outv.Add(new FreezeSpan(start, frames, kind, Label(kind, side, true, lead), side,
                                    true, 0, null, mismatch ? WarnStopSides : null)
            {
                Lead = lead,
            });
            Cover(start, frames);
        }
        foreach (var (start, frames, cutSide) in cutins)
        {
            int depth = stops.Any(s => s.Start <= start && start + frames <= s.Start + s.Frames) ? 1 : 0;
            outv.Add(new FreezeSpan(start, frames, KindCutin, CutinLabel(f, start), null,
                                    true, depth, cutSide == 0 ? null : cutSide, null));
            Cover(start, frames);
        }
        outv.AddRange(LeftoverValidSpans(f, flags, covered));
        outv.AddRange(RoundOverSpans(f, outv));
        return outv.OrderBy(m => m.Index).ThenBy(m => m.Depth).ToList();
    }

    private static List<FreezeSpan> LeftoverValidSpans(
        FreezeInput f, (bool? P1, bool? P2)[]? flags, bool[] covered)
    {
        var outv = new List<FreezeSpan>();
        if (flags is null) return outv;
        int n = f.TickCount;
        var marks = Marks(f);

        var runs = new List<int[]>();
        int[]? cur = null;
        for (int i = 0; i < n; i++)
        {
            int down = DownOf(flags[i]);
            if (down == 0 || covered[i]) { cur = null; continue; }
            if (cur is not null && cur[2] == down) { cur[1]++; continue; }
            cur = [i, 1, down];
            runs.Add(cur);
        }
        foreach (var r in runs)
        {
            if (r[1] <= AnalysisTables.FreezeWordEdgeTicks) continue;
            if (SidesDown(r[2]) < 2) continue;
            var kind = BothKind(marks, r[0]);
            var label = kind == KindCutin ? CutinLabel(f, r[0]) : Label(kind);
            outv.Add(new FreezeSpan(r[0], r[1], kind, label, null, true, 0, null, null));
        }
        return outv;
    }

    private static List<FreezeSpan> RoundOverSpans(FreezeInput f, List<FreezeSpan> spans)
    {
        var outv = new List<FreezeSpan>();
        var rf = f.Series("round_frames");
        var rs = f.Series("result_state");
        if (rf is null || rs is null) return outv;
        int n = f.TickCount;

        var covered = new bool[n];
        foreach (var m in spans)
            for (int i = Math.Max(0, m.Index); i < Math.Min(n, m.Index + m.Frames); i++) covered[i] = true;

        var runs = new List<int[]>();
        int[]? cur = null;
        bool decided = false;
        for (int i = 1; i < Math.Min(n, rf.Length); i++)
        {
            var v = At(rs, i);
            if (v is not null) decided = (long)v.Value != 0;
            var a = rf[i - 1];
            var b = rf[i];
            if (a is null || b is null || (long)a.Value != (long)b.Value || !decided || covered[i])
            {
                cur = null;
                continue;
            }
            if (cur is not null && cur[0] + cur[1] == i) { cur[1]++; continue; }
            cur = [i, 1];
            runs.Add(cur);
        }
        foreach (var r in runs)
        {
            if (r[1] < AnalysisTables.RoundOverMinTicks) continue;
            outv.Add(new FreezeSpan(r[0], r[1], KindRoundOver, Label(KindRoundOver), null,
                                    true, 0, null, null));
        }
        return outv;
    }


    private static string CutinLabel(FreezeInput f, int start) =>
        f.Cards is null ? Label(KindCutin) : f.Cards.CutinLabel(start);

    private static List<FreezeSpan> FinishSpans(FreezeInput f, List<FreezeSpan> spans) =>
        MarkLabelSource(f, MarkNamedElsewhere(f, spans));

    private static List<FreezeSpan> MarkNamedElsewhere(FreezeInput f, List<FreezeSpan> spans)
    {
        var marks = Marks(f);
        for (int k = 0; k < spans.Count; k++)
        {
            var m = spans[k];
            if (!string.Equals(m.Kind, KindCutin, StringComparison.Ordinal))
            {
                spans[k] = m with { PointNamed = false };
                continue;
            }
            bool over = CutinIsRoundEnd(f, marks, m.Index);
            bool byPoint = f.Cards is not null && f.Cards.CutinNamedByPoint(m.Index);
            spans[k] = m with
            {
                PointNamed = byPoint || over,
                Label = over ? Label(KindRoundOver) : m.Label,
            };
        }
        return spans;
    }

    private static bool CutinIsRoundEnd(FreezeInput f, SortedDictionary<int, string> marks, int start)
    {
        if (f.Cards is null) return false;
        if (f.Cards.CutinCard(start) is not null || f.Cards.CutinBoss(start) is not null) return false;
        return string.Equals(BothKind(marks, start), KindRoundEnd, StringComparison.Ordinal);
    }

    private static List<FreezeSpan> MarkLabelSource(FreezeInput f, List<FreezeSpan> spans)
    {
        for (int k = 0; k < spans.Count; k++)
        {
            var m = spans[k];
            int? side = null;
            string? src = null;
            if (string.Equals(m.Kind, KindTimeStop, StringComparison.Ordinal) && m.Side is not null)
            {
                side = 3 - m.Side.Value;
                src = KindTimeStop;
            }
            else if (string.Equals(m.Kind, KindTimeStopMirror, StringComparison.Ordinal))
            {
                side = m.Lead;
                src = KindTimeStop;
            }
            else if (string.Equals(m.Kind, KindCutin, StringComparison.Ordinal) && f.Cards is not null)
            {
                var parts = f.Cards.CutinParts(m.Index);
                if (string.Equals(parts.Text, m.Label, StringComparison.Ordinal))
                {
                    side = parts.Side;
                    src = parts.Source;
                }
            }
            spans[k] = m with { LabelSide = side, LabelSrc = side is null ? null : src };
        }
        return spans;
    }


    private static int? TimeStopLead(FreezeInput f, int start)
    {
        if (f.Cards is null) return null;
        int head = TimeStopHead(f, start);
        int lo = AnalysisTables.TimeStopLeadTicks + AnalysisTables.TimeStopLeadTolLo;
        int hi = AnalysisTables.TimeStopLeadTicks + AnalysisTables.TimeStopLeadTolHi;
        var got = new List<(int Dist, int Side)>();
        foreach (var e in f.Cards.Cards())
        {
            int d = head - e.Index;
            if (d <= 0) continue;
            d -= CutinTicksBetween(f, e.Index, head);
            if (d >= lo && d <= hi)
                got.Add((Math.Abs(d - AnalysisTables.TimeStopLeadTicks), e.Side));
        }
        if (got.Count == 0) return null;
        if (got.Count == 1) return got[0].Side;
        var near = got.OrderBy(t => t.Dist).ToList();
        return near[0].Dist == near[1].Dist ? null : near[0].Side;
    }

    private static int TimeStopHead(FreezeInput f, int start)
    {
        int i = start;
        if (!TimeStopGateUp(f, i)) return i;
        while (TimeStopGateUp(f, i - 1)) i--;
        return i;
    }

    private static bool TimeStopGateUp(FreezeInput f, int i)
    {
        for (int n = 1; n <= 2; n++)
        {
            var v = f.Cards?.ExtAt($"p{n}_game_flags", i);
            if (v is not null && (U32(v.Value) & AnalysisTables.GfTimeStop) != 0) return true;
        }
        return false;
    }

    private static int CutinTicksBetween(FreezeInput f, int a, int b)
    {
        if (f.Cards is null) return 0;
        uint mask = AnalysisTables.GsFreezeP1 | AnalysisTables.GsFreezeP2;
        int got = 0;
        for (int i = a; i < b; i++)
        {
            var v = f.Cards.ExtAt("global_state", i);
            if (v is not null && (U32(v.Value) & mask) != 0) got++;
        }
        return got;
    }


    private static List<int[]> FreezeRuns(FreezeInput f)
    {
        var outv = new List<int[]>();
        var flags = ValidFlags(f);
        if (flags is null) return outv;

        var runs = new List<int[]>();
        int[]? cur = null;
        for (int i = 0; i < f.TickCount; i++)
        {
            int down = DownOf(flags[i]);
            if (down == 0) { cur = null; continue; }
            if (cur is not null && cur[2] == down) { cur[1]++; continue; }
            cur = [i, 1, down];
            runs.Add(cur);
        }
        foreach (var r0 in runs)
        {
            var r = new[] { r0[0], r0[1], r0[2] };
            var last = outv.Count > 0 ? outv[^1] : null;
            bool touch = last is not null && last[0] + last[1] == r[0];
            if (touch && SidesDown(r[2]) >= 2 && SidesDown(last![2]) == 1 && last[1] == 1)
            {
                outv.RemoveAt(outv.Count - 1);
                r = [last[0], r[1] + last[1], r[2]];
            }
            else if (touch && SidesDown(r[2]) == 1 && r[1] == 1 && SidesDown(last![2]) >= 2)
            {
                last[1] += r[1];
                continue;
            }
            outv.Add(r);
        }
        return outv;
    }

    private static List<FreezeSpan> LegacySpans(FreezeInput f)
    {
        var marks = Marks(f);
        var outv = new List<FreezeSpan>();
        foreach (var r in FreezeRuns(f))
        {
            string kind;
            int? side;
            if (SidesDown(r[2]) >= 2) { kind = BothKind(marks, r[0]); side = null; }
            else { kind = KindTimeStop; side = OneSide(r[2]); }
            outv.Add(new FreezeSpan(r[0], r[1], kind, Label(kind, side), side, true, 0, null, null));
        }
        foreach (var (start, frames) in MirrorFreezeRuns(f))
            outv.Add(new FreezeSpan(start, frames, KindTimeStopMirror,
                                    Label(KindTimeStopMirror, null, certain: false), null,
                                    false, 0, null, null));
        outv.AddRange(RoundOverSpans(f, outv));
        return outv.OrderBy(m => m.Index).ToList();
    }

    private static List<(int Start, int Frames)> MirrorFreezeRuns(FreezeInput f)
    {
        var outv = new List<(int, int)>();
        if (!f.SakuyaMirror) return outv;
        var flags = ValidFlags(f);
        if (flags is null) return outv;
        var still = f.BoardStill();
        if (still is null) return outv;

        int[]? cur = null;
        for (int i = 0; i < f.TickCount; i++)
        {
            if (still[i] == true && flags[i].P1 == true && flags[i].P2 == true)
            {
                cur ??= [i, 0];
                cur[1]++;
                continue;
            }
            if (cur is not null && cur[1] >= AnalysisTables.MirrorMinFrames) outv.Add((cur[0], cur[1]));
            cur = null;
        }
        if (cur is not null && cur[1] >= AnalysisTables.MirrorMinFrames) outv.Add((cur[0], cur[1]));
        return outv;
    }


    private static SortedDictionary<int, string> Marks(FreezeInput f)
    {
        var marks = new SortedDictionary<int, string>();

        void Mark(int i, string kind)
        {
            int rank = Array.IndexOf(MarkPriority, kind);
            if (rank < 0) throw new ArgumentException("契機の優先順に無い種別: " + kind);
            if (!marks.TryGetValue(i, out var old) || rank > Array.IndexOf(MarkPriority, old))
                marks[i] = kind;
        }

        void Scan(string name, Func<double, double, bool> pred, string kind)
        {
            var col = f.Series(name);
            if (col is null) return;
            double? prev = null;
            for (int i = 0; i < Math.Min(f.TickCount, col.Length); i++)
            {
                var v = col[i];
                if (v is null) { prev = null; continue; }
                if (prev is not null && pred(prev.Value, v.Value)) Mark(i, kind);
                prev = v;
            }
        }

        for (int n = 1; n <= 2; n++)
        {
            Scan($"p{n}_boss_attacks", (a, b) => b > a, KindCutin);
            Scan($"p{n}_boss_reversals", (a, b) => b > a, KindCutin);
            Scan($"p{n}_boss_type", (a, b) => a == 0 && b != 0, KindCutin);
            Scan($"p{n}_gauge", (a, b) => b < a - AnalysisTables.GaugeDropEps, KindCutin);
            Scan($"p{n}_life_raw", (a, b) => a != 0 && b == 0, KindRoundEnd);
        }

        var fl = f.Series("flags");
        if (fl is not null)
        {
            bool? prev = null;
            for (int i = 0; i < Math.Min(f.TickCount, fl.Length); i++)
            {
                var v = fl[i];
                if (v is null) { prev = null; continue; }
                bool on = (U32(v.Value) & AnalysisTables.FlagPlayersValid) != 0;
                if (prev == false && on) Mark(i, KindRoundStart);
                prev = on;
            }
        }
        return marks;
    }

    private static string BothKind(SortedDictionary<int, string> marks, int start)
    {
        for (int d = 0; d <= AnalysisTables.FreezeMarkTol; d++)
        {
            if (d == 0)
            {
                if (marks.TryGetValue(start, out var here)) return here;
                continue;
            }
            if (marks.TryGetValue(start - d, out var lo)) return lo;
            if (marks.TryGetValue(start + d, out var hi)) return hi;
        }
        return KindBoth;
    }


    private static bool SakuyaMirrorOf(Window w)
    {
        for (int n = 1; n <= 2; n++)
        {
            var col = w.Series($"p{n}_character");
            if (col is null) return false;
            long? got = null;
            for (int i = 0; i < col.Length; i++)
            {
                if (col[i] is null) continue;
                long v = (long)col[i]!.Value;
                if (got is null) got = v;
                else if (got.Value != v) return false;
            }
            if (got is null || got.Value != AnalysisTables.SakuyaCharacter) return false;
        }
        return true;
    }

    private static bool?[]? BoardStill(Window w)
    {
        int n = w.TickCount;
        var moved = new int[n];
        var live = new int[n];
        bool usable = false;
        var cats = new Dictionary<uint, uint>();

        for (int side = 1; side <= 2; side++)
        {
            foreach (var (group, slots) in w.SlotsOf(side))
            {
                foreach (var slot in slots)
                {
                    var xs = Column(w, CoordRing.ColX(slot));
                    var ys = Column(w, CoordRing.ColY(slot));
                    var st = Column(w, CoordRing.ColState(slot));
                    if (xs is null || ys is null || st is null) continue;
                    usable = true;
                    var kd = string.Equals(group, "enemy", StringComparison.Ordinal)
                        ? Column(w, CoordRing.ColKind(slot)) : null;
                    int stop = Math.Min(n, Math.Min(st.Length, Math.Min(xs.Length, ys.Length)));
                    for (int i = 1; i < stop; i++)
                    {
                        if (!CoordRing.SlotIsAlive(slot, st[i - 1]) || !CoordRing.SlotIsAlive(slot, st[i]))
                            continue;
                        if (kd is not null && i < kd.Length)
                        {
                            uint word = kd[i];
                            if (!cats.TryGetValue(word, out var cat))
                            {
                                cat = CoordRing.EnemyCat(word);
                                cats[word] = cat;
                            }
                            if (MirrorSkipEnemyCats.Contains(cat)) continue;
                        }
                        live[i]++;
                        if (xs[i] != xs[i - 1] || ys[i] != ys[i - 1]) moved[i]++;
                    }
                }
            }
        }
        if (!usable) return null;
        var outv = new bool?[n];
        for (int i = 0; i < n; i++)
            outv[i] = i == 0 || live[i] < AnalysisTables.MirrorMinLive ? null : moved[i] == 0;
        return outv;
    }

    private static uint[]? Column(Window w, string name) => w.Cols.Has(name) ? w.Cols[name] : null;


    private static double? At(double?[]? col, int i) =>
        col is not null && (uint)i < (uint)col.Length ? col[i] : null;

    private static bool? Bit(double? v, uint mask) => v is null ? null : (U32(v.Value) & mask) != 0;

    private static uint U32(double v) => unchecked((uint)(long)v);


    public static IEnumerable<(string Label, Func<string?> Body)> SelfTestCases()
    {
        yield return ("凍結の種別の表が 7 件ある（母数）", () =>
            KindJa.Count == 7 ? null : $"{KindJa.Count} 件（表を引けていない）");
        yield return ("★種別の鍵 7 つが全部その表にある", () =>
        {
            var keys = new[] { KindCutin, KindRoundStart, KindRoundEnd, KindRoundOver,
                               KindBoth, KindTimeStop, KindTimeStopMirror };
            var gone = keys.Where(k => !KindJa.ContainsKey(k)).ToList();
            return gone.Count == 0 ? null : "表に無い鍵: " + string.Join(",", gone);
        });
        yield return ("契機の優先順が 3 件ある（母数・★並び順が意味を持つ）", () =>
            MarkPriority.Length == 3
            && MarkPriority[0] == KindCutin && MarkPriority[^1] == KindRoundStart
                ? null : "優先順: " + string.Join(",", MarkPriority));
        yield return ("停止の語が 3 件ある（母数）", () =>
            WordFields.Length == 3 ? null : "停止の語: " + string.Join(",", WordFields));
        yield return ("ミラーで飛ばす敵の分類が 2 件ある（母数）", () =>
            MirrorSkipEnemyCats.Length == 2
            && MirrorSkipEnemyCats.Contains(AnalysisTables.EnemyCatC2)
            && MirrorSkipEnemyCats.Contains(AnalysisTables.EnemyCatC3)
                ? null : "分類: " + string.Join(",", MirrorSkipEnemyCats));
        yield return ("役の但し書きが 3 件ある（母数）", () =>
            RoleNoteJa.Count == 3 ? null : $"{RoleNoteJa.Count} 件");
        yield return ("触る主リングの語が 20 件ある（母数）", () =>
            NeededFields.Length == 20 ? null : $"{NeededFields.Length} 件");
        yield return ("推定の接尾と警告の文言が空でない（母数）", () =>
            !string.IsNullOrEmpty(UncertainSuffix) && !string.IsNullOrEmpty(WarnStopSides)
                ? null : "空（生成物を引けていない）");

        yield return ("カットインの名前", () =>
            Label(KindCutin) == "カットイン" ? null : Label(KindCutin));
        yield return ("★時止めの名前は「止めた側」を出す（引数の側とは逆）", () =>
            Label(KindTimeStop, 2) == "1P 時止め" ? null : Label(KindTimeStop, 2));
        yield return ("★側が無ければ「?」を出す（黙って側を消さない）", () =>
            Label(KindTimeStop) == "?P 時止め" ? null : Label(KindTimeStop));
        yield return ("★推定の接尾は certain が付ける", () =>
            Label(KindTimeStopMirror, null, certain: false) == "時止め" + UncertainSuffix
                ? null : Label(KindTimeStopMirror, null, certain: false));
        yield return ("★★否定: 表の %s をそのまま出す旧なら、画面に「%sP 時止め」と出る", () =>
        {
            string LegacyLabel(string kind, int? side) =>
                KindJa.TryGetValue(kind, out var ja) ? ja : KindJa[KindBoth];
            var old = LegacyLabel(KindTimeStop, 2);
            if (old != "%sP 時止め")
                return "旧が落ちない（写しが間違っている。実際に出たのは「" + old + "」）";
            var now = Label(KindTimeStop, 2);
            if (now.Contains(SideMark, StringComparison.Ordinal))
                return "新も差し込み口を素通ししている: 「" + now + "」";
            return now == "1P 時止め" ? null : "新の名前が違う: 「" + now + "」";
        });
        yield return ("★否定: 側の無い種別に差し込み口は無い（余計な置換をしていない）", () =>
            KindJa[KindTimeStopMirror].Contains(SideMark, StringComparison.Ordinal)
                ? "ミラーの文言に差し込み口がある（表が変わった）" : null);
        yield return ("知らない種別は「両陣が停止」に倒す", () =>
            Label("なにこれ") == "両陣が停止" ? null : Label("なにこれ"));

        yield return ("★欠測は連を切る（`_word_runs()` と同じ）", () =>
        {
            var got = WordRuns(6, i => i == 2 ? (int?)null : (int?)1);
            return got.Count == 2 && got[0] == (0, 2, 1) && got[1] == (3, 3, 1)
                ? null : "連: " + string.Join(" / ", got);
        });
        yield return ("値が変われば連を切る", () =>
        {
            var got = WordRuns(4, i => (int?)(i < 2 ? 1 : 2));
            return got.Count == 2 && got[0] == (0, 2, 1) && got[1] == (2, 2, 2)
                ? null : "連: " + string.Join(" / ", got);
        });

        yield return ("★両側落ちの縁の 1 tick は併合する（入口も出口も）", () =>
        {
            var f = EdgeCaseInput();
            var got = FreezeRuns(f);
            if (got.Count != 1) return $"{got.Count} 本（1 本にまとまっていない）";
            return got[0][0] == 10 && got[0][1] == 32 && SidesDown(got[0][2]) == 2
                ? null : $"({got[0][0]}, {got[0][1]}, {got[0][2]})";
        });
        yield return ("★否定: 縁を併合しない素朴な実装なら 3 本に割れる", () =>
        {
            var f = EdgeCaseInput();
            var flags = ValidFlags(f)!;
            var legacy = WordRuns(f.TickCount, i => DownOf(flags[i]) is var d && d == 0 ? (int?)null : d);
            if (legacy.Count != 3) return $"旧が落ちない（{legacy.Count} 本。写しが間違っている）";
            return FreezeRuns(f).Count == 1 ? null : "新も 1 本になっていない";
        });
        yield return ("★長さ 2 以上の片側落ちは接していても併合しない（時止め＋カットイン）", () =>
        {
            var f = Fake(30, ("p1_hit_list_count", HitList(30, (12, 20))),
                             ("p2_hit_list_count", HitList(30, (10, 20))));
            var got = FreezeRuns(f);
            return got.Count == 2 && got[0][0] == 10 && got[0][1] == 2 && got[1][0] == 12
                ? null : "連: " + string.Join(" / ", got.Select(r => $"({r[0]},{r[1]},{r[2]})"));
        });

        yield return ("契機: ゲージ減 / ライフ 0 / players 0→1", () =>
        {
            var f = MarkInput();
            var marks = Marks(f);
            if (!marks.TryGetValue(30, out var a) || a != KindCutin) return "ゲージ減が拾えない";
            if (!marks.TryGetValue(50, out var b) || b != KindRoundEnd) return "ライフ 0 が拾えない";
            if (!marks.TryGetValue(5, out var c) || c != KindRoundStart) return "ラウンド開始が拾えない";
            return null;
        });
        yield return ("★同じ tick なら、より特定的な契機を採る（決着 > カットイン）", () =>
        {
            var gauge = Const(20, 400.0);
            gauge[10] = 300.0;
            var life = Const(20, 5.0);
            life[10] = 0.0;
            var f = Fake(20, ("p1_gauge", gauge), ("p1_life_raw", life));
            var marks = Marks(f);
            return marks.TryGetValue(10, out var k) && k == KindRoundEnd ? null : "採った種別: " + (marks.TryGetValue(10, out var v) ? v : "なし");
        });
        yield return ("★契機は帯の頭から ±2 tick まで見る（外れたら「両陣が停止」）", () =>
        {
            var marks = new SortedDictionary<int, string> { [32] = KindCutin };
            if (BothKind(marks, 30) != KindCutin) return "±2 が効いていない";
            return BothKind(marks, 29) == KindBoth ? null : "±3 を拾ってしまっている";
        });

        yield return ("★語の道: 時止めの中のカットインが入れ子になる（母数 2 本）", () =>
        {
            var r = Of(NestInput());
            if (!r.Readable) return "読めない扱いになっている";
            if (r.Spans.Count != 2) return $"{r.Spans.Count} 本（2 本のはず）";
            var stop = r.Spans[0];
            var cut = r.Spans[1];
            if (stop.Kind != KindTimeStop || stop.Index != 10 || stop.Frames != 40)
                return $"時止め: {stop.Kind} ({stop.Index},{stop.Frames})";
            if (stop.Side != 2 || stop.Label != "1P 時止め") return $"側: {stop.Side} / {stop.Label}";
            if (cut.Kind != KindCutin || cut.Depth != 1) return $"カットイン: {cut.Kind} depth={cut.Depth}";
            return null;
        });
        yield return ("★入れ子はいちばん内側を返す（`At()`）", () =>
        {
            var r = Of(NestInput());
            var inner = At(r, 25);
            var outer = At(r, 12);
            if (inner is null || inner.Value.Kind != KindCutin) return "内側が取れない";
            return outer is not null && outer.Value.Kind == KindTimeStop ? null : "外側が取れない";
        });
        yield return ("★否定: 広げない素朴な実装なら、盤面が止まっているのに帯の無い tick が残る", () =>
        {
            var f = NestInput();
            var flags = ValidFlags(f);
            var raw = CutinRuns(f);
            if (raw.Count != 1) return $"カットインの連が {raw.Count} 本（合成が間違っている）";
            if (raw[0].Start != 20) return $"旧の頭が {raw[0].Start}";
            var grown = Grow(f.TickCount, flags, raw);
            if (grown[0].Start != 19) return $"新の頭が {grown[0].Start}（VALID の縁へ広がっていない）";
            return DownOf(flags![19]) != 0 ? null : "tick 19 がそもそも VALID の落ちではない（合成が間違っている）";
        });

        yield return ("★片側だけ落ちた連は帯にしない（バトル開始時の強制移動）", () =>
        {
            var f = Fake(60, ("p1_game_flags", Const(60, 0)), ("p2_game_flags", Const(60, 0)),
                             ("global_state", Const(60, 0)),
                             ("p1_hit_list_count", HitList(60, (10, 30))),
                             ("p2_hit_list_count", HitList(60)));
            var r = Of(f);
            return r.Spans.Count == 0 ? null : "帯: " + string.Join(" / ", r.Spans.Select(s => s.Kind));
        });
        yield return ("★長さ 1 の縁は帯にしないが、長さ 5 の両側落ちは帯になる", () =>
        {
            var f = Fake(60, ("p1_game_flags", Const(60, 0)), ("p2_game_flags", Const(60, 0)),
                             ("global_state", Const(60, 0)),
                             ("p1_hit_list_count", HitList(60, (10, 11), (20, 25))),
                             ("p2_hit_list_count", HitList(60, (10, 11), (20, 25))));
            var r = Of(f);
            if (r.Spans.Count != 1) return $"{r.Spans.Count} 本（1 本のはず）";
            return r.Spans[0].Index == 20 && r.Spans[0].Frames == 5 && r.Spans[0].Kind == KindBoth
                ? null : $"({r.Spans[0].Index},{r.Spans[0].Frames},{r.Spans[0].Kind})";
        });

        yield return ("★決着していれば `round_frames` の停滞を帯にする", () =>
        {
            var r = Of(RoundOverInput(decided: true));
            if (r.Spans.Count != 1) return $"{r.Spans.Count} 本";
            return r.Spans[0].Kind == KindRoundOver && r.Spans[0].Index == 11 && r.Spans[0].Frames == 9
                ? null : $"({r.Spans[0].Index},{r.Spans[0].Frames},{r.Spans[0].Kind})";
        });
        yield return ("★否定: `result_state` を見ない素朴な実装なら、次のラウンドの頭まで帯にする", () =>
        {
            var f = RoundOverInput(decided: false);
            var rf = f.Series("round_frames")!;
            int flat = 0;
            for (int i = 1; i < f.TickCount; i++)
                if (rf[i - 1] is not null && rf[i] is not null && (long)rf[i - 1]!.Value == (long)rf[i]!.Value) flat++;
            if (flat == 0) return "旧が落ちない（停滞が 1 つも無い。合成が間違っている）";
            return Of(f).Spans.Count == 0 ? null : "新も決着していない停滞を帯にしている";
        });

        yield return ("★ミラーは盤面の静止から当てる（推定・6 tick 未満は出さない）", () =>
        {
            var still = new bool?[60];
            for (int i = 0; i < 60; i++) still[i] = i >= 10 && i < 30 || i >= 40 && i < 44;
            var f = Fake(60, ("p1_hit_list_count", HitList(60)), ("p2_hit_list_count", HitList(60)));
            f = new FreezeInput(60, f.Snapshot(), sakuyaMirror: true, boardStill: () => still);
            var r = Of(f);
            if (r.Spans.Count != 1) return $"{r.Spans.Count} 本（4 tick の連まで拾っている）";
            var sp = r.Spans[0];
            return sp.Kind == KindTimeStopMirror && !sp.Certain && sp.Index == 10 && sp.Frames == 20
                ? null : $"({sp.Index},{sp.Frames},{sp.Kind},certain={sp.Certain})";
        });
        yield return ("★キャラが咲夜ミラーだと読めない窓では出さない", () =>
        {
            var still = new bool?[60];
            for (int i = 0; i < 60; i++) still[i] = i >= 10 && i < 30;
            var f = Fake(60, ("p1_hit_list_count", HitList(60)), ("p2_hit_list_count", HitList(60)));
            f = new FreezeInput(60, f.Snapshot(), sakuyaMirror: false, boardStill: () => still);
            return Of(f).Spans.Count == 0 ? null : "ミラーと読めていない窓で推定の帯を出している";
        });

        yield return ("★★語も VALID も無ければ「読めない」（0 本と混ぜない）", () =>
        {
            var f = Fake(10);
            var r = Of(f);
            if (r.Readable) return "読める扱いになっている";
            var flags = FrozenFlags(r, 10);
            return flags.All(v => v is null) ? null : "false で埋めている";
        });
        yield return ("★VALID があれば「読めて、止まっていない」（null ではなく false）", () =>
        {
            var f = Fake(10, ("p1_hit_list_count", HitList(10)), ("p2_hit_list_count", HitList(10)));
            var r = Of(f);
            if (!r.Readable) return "読めない扱いになっている";
            return FrozenFlags(r, 10).All(v => v == false) ? null : "false になっていない";
        });
        yield return ("★否定: 「0 本＝凍結なし」と読む素朴な実装なら、測っていない窓が false で埋まる", () =>
        {
            var r = Of(Fake(10));
            var legacy = new bool[10];
            foreach (var sp in r.Spans)
                for (int i = sp.Index; i < sp.Index + sp.Frames; i++) legacy[i] = true;
            if (legacy.Any(v => v)) return "旧が落ちない（写しが間違っている）";
            return FrozenFlags(r, 10).All(v => v is null) ? null : "新も false で埋めている";
        });

        yield return ("役: 止められた側の盤面は自機も止まる / 止めた側は自機だけ動ける", () =>
        {
            var sp = new FreezeSpan(0, 10, KindTimeStop, "1P 時止め", 2, true, 0, null, null);
            if (Role(sp, 2) != RoleAll) return "止められた側が all になっていない";
            return Role(sp, 1) == RoleField ? null : "止めた側が field になっていない";
        });
        yield return ("★否定: 「側が無ければ all」と読む素朴な実装なら、ミラーで自機が止まる", () =>
        {
            var sp = new FreezeSpan(0, 10, KindTimeStopMirror, "時止め", null, true, 0, null, null);
            var legacy = sp.Side is null ? RoleAll : RoleField;
            if (legacy != RoleAll) return "旧が落ちない（写しが間違っている）";
            return Role(sp, 1) == RoleFieldBoth ? null : "新もミラーを all と言っている";
        });
        yield return ("役の但し書きは表から引く（知らない役は空）", () =>
            RoleNote(RoleFieldBoth) == "（自機は両方とも動ける）" && RoleNote(null) == ""
                ? null : "但し書き: " + RoleNote(RoleFieldBoth));

        yield return ("★フラグが片側にしか立たなければ帯に警告が載る", () =>
        {
            var gf1 = Const(30, 0.0);
            for (int i = 10; i < 20; i++) gf1[i] = 1.0;
            var f = Fake(30, ("p1_game_flags", gf1), ("p2_game_flags", Const(30, 0.0)),
                             ("global_state", Const(30, 0.0)),
                             ("p1_hit_list_count", HitList(30)), ("p2_hit_list_count", HitList(30)));
            var r = Of(f);
            if (r.Spans.Count != 1) return $"{r.Spans.Count} 本";
            return r.Warnings.Count == 1 && r.Warnings[0] == WarnStopSides
                ? null : "警告: " + string.Join(" / ", r.Warnings);
        });
        yield return ("★★片側が欠測なだけなら警告を出さない（欠測と食い違いは別）", () =>
        {
            var gf1 = Const(30, 0.0);
            var gf2 = Const(30, 0.0);
            for (int i = 10; i < 20; i++) { gf1[i] = 1.0; gf2[i] = null; }
            var f = Fake(30, ("p1_game_flags", gf1), ("p2_game_flags", gf2),
                             ("global_state", Const(30, 0.0)),
                             ("p1_hit_list_count", HitList(30)), ("p2_hit_list_count", HitList(30)));
            var r = Of(f);
            if (r.Spans.Count != 1) return $"{r.Spans.Count} 本（帯そのものは出るはず）";
            return r.Warnings.Count == 0 ? null : "欠測を食い違いに数えている: " + r.Warnings[0];
        });

        yield return ("★★カットインの帯に発動の名前が付く（側も源も同じ判断から出る）", () =>
        {
            var r = Of(NamedNestInput());
            if (!r.Named) return "名前を付けにいっていない扱いになっている";
            if (r.Spans.Count != 2) return $"{r.Spans.Count} 本（2 本のはず）";
            var cut = r.Spans[1];
            if (cut.Kind != KindCutin) return "2 本目が " + cut.Kind;
            if (cut.Label != "1P C2") return "名前: " + cut.Label;
            if (cut.LabelSide != 1 || cut.LabelSrc != "gauge")
                return $"側/源: {cut.LabelSide}/{cut.LabelSrc}";
            if (!cut.PointNamed) return "点が出しているのに PointNamed が立っていない";
            var stop = r.Spans[0];
            return stop.Label == "1P 時止め" && stop.LabelSide == 1 && stop.LabelSrc == KindTimeStop
                ? null : $"時止め: {stop.Label} / {stop.LabelSide} / {stop.LabelSrc}";
        });
        yield return ("★★否定: 起点を数えていない窓は「カットイン」のまま（0 と混ぜない）", () =>
        {
            var r = Of(NestInput());
            if (r.Named) return "名前を付けにいった扱いになっている";
            var cut = r.Spans[1];
            if (cut.Label != "カットイン") return "名前: " + cut.Label;
            if (cut.LabelSide is not null || cut.PointNamed)
                return "測っていないのに側や印が付いている";
            return Of(NamedNestInput()).Spans[1].Label == "1P C2"
                ? null : "起点を渡しても名前が付かない";
        });
        yield return ("★★ラウンド終了の演出は名前を寄せる（凡例から「カットイン」を消す）", () =>
        {
            var r = Of(RoundEndCutinInput(withCard: false));
            var cuts = r.Spans.Where(s => s.Kind == KindCutin).ToList();
            if (cuts.Count != 1) return $"カットインの帯が {cuts.Count} 本（1 本のはず）";
            var cut = cuts[0];
            if (cut.Label != "ラウンド終了") return "名前: " + cut.Label;
            if (!cut.PointNamed) return "PointNamed が立っていない";
            return cut.LabelSide is null && cut.LabelSrc is null
                ? null : $"側/源が付いている: {cut.LabelSide}/{cut.LabelSrc}";
        });
        yield return ("★★否定: 発動が特定できた帯まで「ラウンド終了」へ寄せる旧", () =>
        {
            var f = RoundEndCutinInput(withCard: true);
            var marks = Marks(f);
            var cuts = Of(f).Spans.Where(s => s.Kind == KindCutin).ToList();
            if (cuts.Count != 1) return $"カットインの帯が {cuts.Count} 本（合成が間違っている）";
            if (BothKind(marks, cuts[0].Index) != KindRoundEnd)
                return "旧が落ちない（決着の契機が帯の頭に無い。合成が間違っている）";
            var now = cuts[0].Label;
            return now == "2P C3" ? null : "新も名前を潰している: " + now;
        });
        yield return ("★★語の無い窓でも印は付く（VALID の道も出口を通る）", () =>
        {
            var r = Of(LegacyNamedInput());
            if (r.FromWords) return "語の道になっている（合成が間違っている）";
            var cuts = r.Spans.Where(s => s.Kind == KindCutin).ToList();
            if (cuts.Count != 1) return $"カットインの帯が {cuts.Count} 本（1 本のはず）";
            var cut = cuts[0];
            if (cut.Label != "カットイン") return "名前: " + cut.Label;
            if (!cut.PointNamed) return "★点が出しているのに PointNamed が立っていない（出口を通っていない）";
            return cut.LabelSide is null ? null : "側を名乗っている: " + cut.LabelSide;
        });
        yield return ("★★ミラーの主導を当てる（★演出の tick を引く）", () =>
        {
            var f = MirrorLeadInput();
            var r = Of(f);
            var mirs = r.Spans.Where(s => s.Kind == KindTimeStopMirror).ToList();
            if (mirs.Count != 1)
                return "ミラーの帯が " + mirs.Count + " 本: "
                       + string.Join(" / ", r.Spans.Select(s => s.Kind));
            var mir = mirs[0];
            if (mir.Lead != 2) return "主導: " + (mir.Lead?.ToString() ?? "なし");
            if (mir.Label != "2P 時止め") return "名前: " + mir.Label;
            return mir.LabelSide == 2 && mir.LabelSrc == KindTimeStop
                ? null : $"側/源: {mir.LabelSide}/{mir.LabelSrc}";
        });
        yield return ("★★否定: 演出の tick を引かない旧なら、主導が決まらない", () =>
        {
            var f = MirrorLeadInput();
            int head = TimeStopHead(f, MirrorStopAt);
            int raw = head - MirrorCardAt;
            int lo = AnalysisTables.TimeStopLeadTicks + AnalysisTables.TimeStopLeadTolLo;
            int hi = AnalysisTables.TimeStopLeadTicks + AnalysisTables.TimeStopLeadTolHi;
            if (raw >= lo && raw <= hi)
                return $"旧が落ちない（引かなくても {raw} で入る。合成が間違っている）";
            int cut = CutinTicksBetween(f, MirrorCardAt, head);
            if (cut == 0) return "演出の tick が 0（合成が間違っている）";
            return TimeStopLead(f, MirrorStopAt) == 2 ? null : "新も主導を落としている";
        });
        yield return ("★★候補が 2 つで同点なら主導を決めない（★近いほうがあれば採る）", () =>
        {
            var tie = Of(MirrorTwoCardInput(41, 39)).Spans
                        .Where(s => s.Kind == KindTimeStopMirror).ToList();
            if (tie.Count != 1) return $"ミラーの帯が {tie.Count} 本（合成が間違っている）";
            if (tie[0].Lead is not null) return "同点なのに主導を決めている: " + tie[0].Lead;
            if (tie[0].Label != "時止め") return "名前: " + tie[0].Label;
            var near = Of(MirrorTwoCardInput(41, 40)).Spans
                         .Where(s => s.Kind == KindTimeStopMirror).ToList();
            if (near.Count != 1) return $"ミラーの帯が {near.Count} 本";
            return near[0].Lead == 2 ? null
                : "近いほうを採っていない: " + (near[0].Lead?.ToString() ?? "なし");
        });
        yield return ("★主導が決まらなければ名前に側を出さない（黙って倒さない）", () =>
        {
            var f = MirrorLeadInput(cardAt: null);
            var mirs = Of(f).Spans.Where(s => s.Kind == KindTimeStopMirror).ToList();
            if (mirs.Count != 1) return $"ミラーの帯が {mirs.Count} 本";
            var mir = mirs[0];
            if (mir.Lead is not null) return "主導: " + mir.Lead;
            return mir.Label == "時止め" && mir.LabelSide is null ? null
                : $"{mir.Label} / {mir.LabelSide}";
        });

    }


    private static FreezeInput Fake(int n, params (string Name, double?[] Col)[] cols)
    {
        var map = new Dictionary<string, double?[]>(StringComparer.Ordinal);
        foreach (var (name, col) in cols) map[name] = col;
        return new FreezeInput(n, map);
    }

    private static double?[] Const(int n, double v) =>
        Enumerable.Repeat((double?)v, n).ToArray();

    private static double?[] HitList(int n, params (int From, int To)[] down)
    {
        var col = new double?[n];
        for (int i = 0; i < n; i++)
            col[i] = down.Any(d => i >= d.From && i < d.To) ? 0.0 : AnalysisTables.HitlistValid;
        return col;
    }

    private static FreezeInput EdgeCaseInput() =>
        Fake(60, ("p1_hit_list_count", HitList(60, (11, 42))),
                 ("p2_hit_list_count", HitList(60, (10, 41))));

    private static FreezeInput MarkInput()
    {
        var gauge = Const(60, 400.0);
        for (int i = 30; i < 60; i++) gauge[i] = 300.0;
        var life = Const(60, 5.0);
        for (int i = 50; i < 60; i++) life[i] = 0.0;
        var flags = Const(60, 1.0);
        for (int i = 0; i < 5; i++) flags[i] = 0.0;
        return Fake(60, ("p1_gauge", gauge), ("p1_life_raw", life), ("flags", flags));
    }

    private static FreezeInput NestInput()
    {
        var gf = Const(60, 0.0);
        for (int i = 10; i < 50; i++) gf[i] = 1.0;
        var gs = Const(60, 0.0);
        for (int i = 20; i < 30; i++) gs[i] = AnalysisTables.GsFreezeP2;
        return Fake(60, ("p1_game_flags", gf), ("p2_game_flags", gf), ("global_state", gs),
                        ("p1_hit_list_count", HitList(60, (20, 30))),
                        ("p2_hit_list_count", HitList(60, (10, 50))));
    }


    private static FreezeInput NamedNestInput()
    {
        var ext = new CardEvents.FakeExt(60, 0)
            .With("p1_gauge", i => i < 20 ? 400.0 : 300.0)
            .With("p1_spell_attacks", i => i < 20 ? 0.0 : 1.0)
            .With("p1_boss_attacks", _ => 0.0);
        var f = NestInput();
        return new FreezeInput(f.TickCount, f.Snapshot(), cards: CardEvents.Of(ext));
    }

    private static FreezeInput RoundEndCutinInput(bool withCard)
    {
        const int at = 30;
        var gs = Const(60, 0.0);
        for (int i = at; i < at + 10; i++) gs[i] = AnalysisTables.GsFreezeP1;
        var life = Const(60, 3.0);
        for (int i = at; i < 60; i++) life[i] = 0.0;
        var cols = new Dictionary<string, double?[]>(StringComparer.Ordinal)
        {
            ["p1_game_flags"] = Const(60, 0.0),
            ["p2_game_flags"] = Const(60, 0.0),
            ["global_state"] = gs,
            ["p1_hit_list_count"] = HitList(60),
            ["p2_hit_list_count"] = HitList(60),
            ["p2_life_raw"] = life,
        };
        var ext = new CardEvents.FakeExt(60, 0)
            .With("p2_gauge", i => withCard && i >= at ? 200.0 : 400.0)
            .With("p2_spell_attacks", i => withCard && i >= at ? 1.0 : 0.0)
            .With("p2_boss_attacks", _ => 0.0);
        if (withCard) cols["p2_gauge"] = ext.ExtSeries("p2_gauge")!;
        return new FreezeInput(60, cols, cards: CardEvents.Of(ext));
    }

    private static FreezeInput LegacyNamedInput()
    {
        const int at = 20;
        var gauge = Const(60, 400.0);
        for (int i = at; i < 60; i++) gauge[i] = 200.0;
        var cols = new Dictionary<string, double?[]>(StringComparer.Ordinal)
        {
            ["p1_hit_list_count"] = HitList(60, (at, at + 6)),
            ["p2_hit_list_count"] = HitList(60, (at, at + 6)),
            ["p2_gauge"] = gauge,
        };
        var ext = new CardEvents.FakeExt(60, 0)
            .With("p2_gauge", i => i < at ? 400.0 : 200.0)
            .With("p2_spell_attacks", i => i < at ? 0.0 : 1.0)
            .With("p2_boss_attacks", _ => 0.0);
        return new FreezeInput(60, cols, cards: CardEvents.Of(ext));
    }

    private const int MirrorStopAt = 60;
    private const int MirrorCardAt = 10;
    private const int MirrorCutinAt = 20;
    private const int MirrorCutinLen = 10;

    private static FreezeInput MirrorLeadInput(int? cardAt = MirrorCardAt)
    {
        const int n = 120;
        var gf = Const(n, 0.0);
        for (int i = MirrorStopAt; i < MirrorStopAt + 40; i++) gf[i] = AnalysisTables.GfTimeStop;
        var gs = Const(n, 0.0);
        for (int i = MirrorCutinAt; i < MirrorCutinAt + MirrorCutinLen; i++)
            gs[i] = AnalysisTables.GsFreezeP2;
        var cols = new Dictionary<string, double?[]>(StringComparer.Ordinal)
        {
            ["p1_game_flags"] = gf,
            ["p2_game_flags"] = gf,
            ["global_state"] = gs,
            ["p1_hit_list_count"] = HitList(n),
            ["p2_hit_list_count"] = HitList(n),
        };
        var ext = new CardEvents.FakeExt(n, 0)
            .With("p2_gauge", i => cardAt is int a && i >= a ? 300.0 : 400.0)
            .With("p2_spell_attacks", i => cardAt is int a && i >= a ? 1.0 : 0.0)
            .With("p2_boss_attacks", _ => 0.0)
            .With("p1_game_flags", i => i >= MirrorStopAt && i < MirrorStopAt + 40
                                        ? AnalysisTables.GfTimeStop : 0.0)
            .With("p2_game_flags", i => i >= MirrorStopAt && i < MirrorStopAt + 40
                                        ? AnalysisTables.GfTimeStop : 0.0)
            .With("global_state", i => i >= MirrorCutinAt && i < MirrorCutinAt + MirrorCutinLen
                                       ? AnalysisTables.GsFreezeP2 : 0.0);
        return new FreezeInput(n, cols, cards: CardEvents.Of(ext));
    }

    private static FreezeInput MirrorTwoCardInput(int lead1, int lead2)
    {
        const int n = 120;
        var gf = Const(n, 0.0);
        for (int i = MirrorStopAt; i < MirrorStopAt + 40; i++) gf[i] = AnalysisTables.GfTimeStop;
        var cols = new Dictionary<string, double?[]>(StringComparer.Ordinal)
        {
            ["p1_game_flags"] = gf,
            ["p2_game_flags"] = gf,
            ["global_state"] = Const(n, 0.0),
            ["p1_hit_list_count"] = HitList(n),
            ["p2_hit_list_count"] = HitList(n),
        };
        var ext = new CardEvents.FakeExt(n, 0)
            .With("global_state", _ => 0.0)
            .With("p1_game_flags", i => i >= MirrorStopAt && i < MirrorStopAt + 40
                                        ? AnalysisTables.GfTimeStop : 0.0)
            .With("p2_game_flags", i => i >= MirrorStopAt && i < MirrorStopAt + 40
                                        ? AnalysisTables.GfTimeStop : 0.0);
        foreach (var (side, lead) in new[] { (1, lead1), (2, lead2) })
        {
            int at = MirrorStopAt - lead;
            ext = ext.With($"p{side}_gauge", i => i < at ? 400.0 : 300.0)
                     .With($"p{side}_spell_attacks", i => i < at ? 0.0 : 1.0)
                     .With($"p{side}_boss_attacks", _ => 0.0);
        }
        return new FreezeInput(n, cols, cards: CardEvents.Of(ext));
    }

    private static FreezeInput RoundOverInput(bool decided)
    {
        var rf = new double?[60];
        for (int i = 0; i < 60; i++) rf[i] = i < 10 ? i : (i < 20 ? 10 : i - 9);
        return Fake(60, ("p1_hit_list_count", HitList(60)), ("p2_hit_list_count", HitList(60)),
                        ("round_frames", rf), ("result_state", Const(60, decided ? 1.0 : 0.0)));
    }
}

public readonly record struct FreezeSpan(int Index, int Frames, string Kind, string Label,
                                         int? Side, bool Certain, int Depth, int? CutSide,
                                         string? Warn)
{
    public bool PointNamed { get; init; }

    public int? LabelSide { get; init; }

    public string? LabelSrc { get; init; }

    public int? Lead { get; init; }
}

public sealed class FreezeResult
{
    public FreezeResult(bool readable, bool fromWords, IReadOnlyList<FreezeSpan> spans, bool named)
    {
        Readable = readable; FromWords = fromWords; Spans = spans; Named = named;
    }

    public bool Readable { get; }

    public bool Named { get; }

    public bool FromWords { get; }

    public IReadOnlyList<FreezeSpan> Spans { get; }

    public IReadOnlyList<string> Warnings
    {
        get
        {
            var seen = new List<string>();
            foreach (var m in Spans)
                if (m.Warn is not null && !seen.Contains(m.Warn, StringComparer.Ordinal)) seen.Add(m.Warn);
            return seen;
        }
    }
}

public sealed class FreezeInput
{
    private readonly Dictionary<string, double?[]> _cols;
    private readonly Func<bool?[]?>? _boardStill;
    private bool?[]? _still;
    private bool _stillDone;

    public FreezeInput(int tickCount, Dictionary<string, double?[]> cols,
                       bool sakuyaMirror = false, Func<bool?[]?>? boardStill = null,
                       CardEvents? cards = null)
    {
        TickCount = tickCount;
        _cols = cols;
        SakuyaMirror = sakuyaMirror;
        _boardStill = boardStill;
        Cards = cards;
    }

    public int TickCount { get; }

    public CardEvents? Cards { get; }

    public bool SakuyaMirror { get; }

    public double?[]? Series(string name) => _cols.TryGetValue(name, out var col) ? col : null;

    public bool?[]? BoardStill()
    {
        if (!_stillDone)
        {
            _still = _boardStill?.Invoke();
            _stillDone = true;
        }
        return _still;
    }

    public Dictionary<string, double?[]> Snapshot() => new(_cols, StringComparer.Ordinal);
}
