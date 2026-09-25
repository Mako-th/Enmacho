using Microsoft.Data.Sqlite;
using TH09.Layer0;
using TH09.Record.Generated;

namespace TH09.Record;

public static class RoundRanges
{
    public static readonly string[] Cols =
    [
        "flags", "mode", "stage_index", "completed_rounds", "round_frames", "result_state",
        "p1_wins", "p2_wins", "rounds_required",
        "p1_spell_points", "p2_spell_points", "p1_gauge", "p2_gauge",
        "p1_score_raw", "p2_score_raw", "p1_boss_type", "p2_boss_type",
        "p1_boss_depth", "p2_boss_depth", "p1_boss_reversals", "p2_boss_reversals",
        "p1_cpu_quick_timer_cur", "p2_cpu_quick_timer_cur",
        "p1_cpu_stand_timer_cur", "p2_cpu_stand_timer_cur",
        "p1_zero_hit_timer", "p2_zero_hit_timer",
    ];

    private static readonly HashSet<string> WantedCols = new(Cols, StringComparer.Ordinal);

    public sealed class Arrays
    {
        public required uint[] Flags { get; init; }
        public required uint[] Mode { get; init; }
        public required uint[] StageIndex { get; init; }
        public required uint[] CompletedRounds { get; init; }
        public required uint[] RoundFrames { get; init; }
        public required uint[] ResultState { get; init; }
        public required uint[] P1Wins { get; init; }
        public required uint[] P2Wins { get; init; }
        public required uint[] RoundsRequired { get; init; }
        public required uint[] P1SpellPoints { get; init; }
        public required uint[] P2SpellPoints { get; init; }
        public required uint[] P1Gauge { get; init; }
        public required uint[] P2Gauge { get; init; }
        public required uint[] P1ScoreRaw { get; init; }
        public required uint[] P2ScoreRaw { get; init; }
        public required uint[] P1BossType { get; init; }
        public required uint[] P2BossType { get; init; }
        public required uint[] P1BossDepth { get; init; }
        public required uint[] P2BossDepth { get; init; }
        public required uint[] P1BossReversals { get; init; }
        public required uint[] P2BossReversals { get; init; }
        public required uint[] P1CpuQuickTimerCur { get; init; }
        public required uint[] P2CpuQuickTimerCur { get; init; }
        public required uint[] P1CpuStandTimerCur { get; init; }
        public required uint[] P2CpuStandTimerCur { get; init; }
        public required uint[] P1ZeroHitTimer { get; init; }
        public required uint[] P2ZeroHitTimer { get; init; }

        public int Count => RoundFrames.Length;
    }

    public sealed record RoundKey(long? StageNumber, long RoundNumber);

    public readonly record struct Span(int Start, int Stop);

    public sealed record Entry(long SessionId, long? StageNumber, long RoundNumber, int Start, int Stop);

    public static long? StageNumberOf(long mode, long stageIndex) => mode is 0 or 1 ? stageIndex + 1 : null;


    public static List<(RoundKey Key, List<Span> Parts)> Compute(Arrays a)
    {
        var order = new List<RoundKey>();
        var outMap = new Dictionary<RoundKey, List<Span>>();
        int n = a.Count;
        if (n == 0) return [];

        var mode = a.Mode; var si = a.StageIndex; var cr = a.CompletedRounds;
        var rf = a.RoundFrames; var rs = a.ResultState;
        var p1w = a.P1Wins; var p2w = a.P2Wins; var rr = a.RoundsRequired;

        var ends = new HashSet<int>();
        var nexts = new HashSet<int>();
        var stages = new HashSet<int>();
        for (var i = 1; i < n; i++)
        {
            if (cr[i] > cr[i - 1]) ends.Add(i);
            if (rs[i - 1] == 1 && rs[i] == 0 && rf[i] < rf[i - 1] && si[i] == si[i - 1]) nexts.Add(i);
            if (si[i] != si[i - 1]) stages.Add(i);
        }

        int? start = 0;
        var resume = 0;
        var key = new RoundKey(StageNumberOf(mode[0], si[0]), 1);

        void Close(int stop)
        {
            var lo = start ?? resume;
            if (stop <= lo) return;
            if (!outMap.TryGetValue(key, out var list))
            {
                list = [];
                outMap[key] = list;
                order.Add(key);
            }
            list.Add(new Span(lo, stop));
        }

        var all = new SortedSet<int>();
        foreach (var i in ends) all.Add(i);
        foreach (var i in nexts) all.Add(i);
        foreach (var i in stages) all.Add(i);

        foreach (var i in all)
        {
            if (ends.Contains(i))
            {
                Close(i + 1);
                start = null;
                resume = i + 1;
            }
            if (nexts.Contains(i) && MonitorRules.MoreRoundsToPlay(mode[i], p1w[i], p2w[i], rr[i]))
            {
                if (start is not null) Close(i);
                start = i;
                key = new RoundKey(StageNumberOf(mode[i], si[i]), (long)cr[i] + 1);
            }
            if (stages.Contains(i) && mode[i] is 0 or 1 && si[i] <= 8)
            {
                if (start is not null) Close(i);
                start = i;
                key = new RoundKey(StageNumberOf(mode[i], si[i]), 1);
            }
        }
        if (start is not null) Close(n);

        return order.Select(k => (k, outMap[k])).ToList();
    }

    public static List<Entry> Flatten(long sessionId, List<(RoundKey Key, List<Span> Parts)> ranges)
    {
        var list = new List<Entry>();
        foreach (var (k, parts) in ranges)
            foreach (var span in parts)
                list.Add(new Entry(sessionId, k.StageNumber, k.RoundNumber, span.Start, span.Stop));
        return list;
    }


    public static (Arrays? Data, string? Why) LoadColumns(SqliteConnection layer0, long sessionId)
    {
        var parts = new Dictionary<string, List<uint[]>>(StringComparer.Ordinal);
        foreach (var name in Cols) parts[name] = [];
        long total = 0;

        foreach (var seg in SegmentReader.SegmentsOf(layer0, sessionId))
        {
            var order = TickArchive.ParseFieldOrder(seg.FieldOrderText);
            var cols = TickArchive.Decode(seg.Blob, seg.Encoding, order, WantedCols);
            var missing = Cols.Where(name => !cols.Has(name)).ToList();
            if (missing.Count > 0)
                return (null, "古いレイアウトで " + string.Join(",", missing.Take(3)) + " が無い");
            foreach (var name in Cols) parts[name].Add(cols[name]);
            total += order.TickCount;
        }
        if (total == 0) return (null, "Layer 0 に生tickが無い");

        var concatenated = new Dictionary<string, uint[]>(StringComparer.Ordinal);
        foreach (var name in Cols) concatenated[name] = Concat(parts[name]);

        var flags = concatenated["flags"];
        var mask = new bool[flags.Length];
        var liveCount = 0;
        for (var i = 0; i < flags.Length; i++)
        {
            if ((flags[i] & RecordLabels.RequiredFlags) != RecordLabels.RequiredFlags) continue;
            mask[i] = true;
            liveCount++;
        }
        if (liveCount == 0) return (null, "players/stats が有効な tick が 1 つも無い");

        var result = new Arrays
        {
            Flags = Filter(concatenated["flags"], mask, liveCount),
            Mode = Filter(concatenated["mode"], mask, liveCount),
            StageIndex = Filter(concatenated["stage_index"], mask, liveCount),
            CompletedRounds = Filter(concatenated["completed_rounds"], mask, liveCount),
            RoundFrames = Filter(concatenated["round_frames"], mask, liveCount),
            ResultState = Filter(concatenated["result_state"], mask, liveCount),
            P1Wins = Filter(concatenated["p1_wins"], mask, liveCount),
            P2Wins = Filter(concatenated["p2_wins"], mask, liveCount),
            RoundsRequired = Filter(concatenated["rounds_required"], mask, liveCount),
            P1SpellPoints = Filter(concatenated["p1_spell_points"], mask, liveCount),
            P2SpellPoints = Filter(concatenated["p2_spell_points"], mask, liveCount),
            P1Gauge = Filter(concatenated["p1_gauge"], mask, liveCount),
            P2Gauge = Filter(concatenated["p2_gauge"], mask, liveCount),
            P1ScoreRaw = Filter(concatenated["p1_score_raw"], mask, liveCount),
            P2ScoreRaw = Filter(concatenated["p2_score_raw"], mask, liveCount),
            P1BossType = Filter(concatenated["p1_boss_type"], mask, liveCount),
            P2BossType = Filter(concatenated["p2_boss_type"], mask, liveCount),
            P1BossDepth = Filter(concatenated["p1_boss_depth"], mask, liveCount),
            P2BossDepth = Filter(concatenated["p2_boss_depth"], mask, liveCount),
            P1BossReversals = Filter(concatenated["p1_boss_reversals"], mask, liveCount),
            P2BossReversals = Filter(concatenated["p2_boss_reversals"], mask, liveCount),
            P1CpuQuickTimerCur = Filter(concatenated["p1_cpu_quick_timer_cur"], mask, liveCount),
            P2CpuQuickTimerCur = Filter(concatenated["p2_cpu_quick_timer_cur"], mask, liveCount),
            P1CpuStandTimerCur = Filter(concatenated["p1_cpu_stand_timer_cur"], mask, liveCount),
            P2CpuStandTimerCur = Filter(concatenated["p2_cpu_stand_timer_cur"], mask, liveCount),
            P1ZeroHitTimer = Filter(concatenated["p1_zero_hit_timer"], mask, liveCount),
            P2ZeroHitTimer = Filter(concatenated["p2_zero_hit_timer"], mask, liveCount),
        };
        return (result, null);
    }

    private static uint[] Concat(List<uint[]> chunks)
    {
        var total = 0;
        foreach (var c in chunks) total += c.Length;
        var outArr = new uint[total];
        var at = 0;
        foreach (var c in chunks) { c.CopyTo(outArr, at); at += c.Length; }
        return outArr;
    }

    private static uint[] Filter(uint[] src, bool[] mask, int liveCount)
    {
        var outArr = new uint[liveCount];
        var j = 0;
        for (var i = 0; i < src.Length; i++)
            if (mask[i]) outArr[j++] = src[i];
        return outArr;
    }
}
