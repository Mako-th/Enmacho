using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TH09.Record.Generated;

namespace TH09.Record;

public static class RoundMetrics
{

    public sealed record SideResult(
        long SpellCapTicks, long? SpellPointsMax, long? SpellPointsMaxFrame, long SpellPointsTotal,
        double? GaugeAvg, double? GaugeMax, double? ScoreGainRate, long BossPresentTicks,
        long? CpuQuickTimerMax, long? CpuStandTimerMax, long CpuTimerFrozenTicks,
        long? NoHitGapMaxTicks, long BossReversalCleanCount);

    private readonly record struct SideColumns(
        uint[] SpellPoints, uint[] Gauge, uint[] ScoreRaw, uint[] BossType, uint[] BossDepth,
        uint[] BossReversals, uint[] CpuQuickTimerCur, uint[] CpuStandTimerCur,
        uint[] ZeroHitTimer, uint[] FoeZeroHitTimer);

    private static SideColumns Pick(RoundRanges.Arrays a, int side) => side switch
    {
        1 => new SideColumns(a.P1SpellPoints, a.P1Gauge, a.P1ScoreRaw, a.P1BossType, a.P1BossDepth,
                             a.P1BossReversals, a.P1CpuQuickTimerCur, a.P1CpuStandTimerCur,
                             a.P1ZeroHitTimer, a.P2ZeroHitTimer),
        2 => new SideColumns(a.P2SpellPoints, a.P2Gauge, a.P2ScoreRaw, a.P2BossType, a.P2BossDepth,
                             a.P2BossReversals, a.P2CpuQuickTimerCur, a.P2CpuStandTimerCur,
                             a.P2ZeroHitTimer, a.P1ZeroHitTimer),
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "side は 1 か 2 だけ"),
    };

    private static int S(uint v) => unchecked((int)v);

    private static float F(uint v) => BitConverter.UInt32BitsToSingle(v);

    public static SideResult SideMetricsOf(RoundRanges.Arrays a, IReadOnlyList<RoundRanges.Span> parts, int side)
    {
        var c = Pick(a, side);

        long spellCapTicks = 0, spellPointsTotal = 0, bossPresentTicks = 0;
        long cpuTimerFrozenTicks = 0, bossReversalCleanCount = 0;
        long? spellPointsMax = null, spellPointsMaxFrame = null;
        long? cpuQuickTimerMax = null, cpuStandTimerMax = null, noHitGapMaxTicks = null;
        double? gaugeMax = null;
        var gaugeSum = 0.0;
        long gaugeN = 0, gain = 0, frames = 0;

        foreach (var part in parts)
        {
            var lo = part.Start;
            var hi = part.Stop;
            var n = hi - lo;

            long localMaxSp = -1;
            var localMaxIdx = -1;
            for (var i = 0; i < n; i++)
            {
                long v = c.SpellPoints[lo + i];
                if (v >= RecordLabels.SpellPointsCap) spellCapTicks++;
                if (v > localMaxSp) { localMaxSp = v; localMaxIdx = i; }
            }
            if (localMaxIdx >= 0 && (spellPointsMax is null || localMaxSp > spellPointsMax))
            {
                spellPointsMax = localMaxSp;
                spellPointsMaxFrame = a.RoundFrames[lo + localMaxIdx];
            }

            for (var i = 1; i < n; i++)
            {
                long cur = c.SpellPoints[lo + i];
                long prev = c.SpellPoints[lo + i - 1];
                if (cur < prev) spellPointsTotal += prev;
            }

            var finite = new List<float>(n);
            for (var i = 0; i < n; i++)
            {
                var g = F(c.Gauge[lo + i]);
                if (float.IsFinite(g)) finite.Add(g);
            }
            if (finite.Count > 0)
            {
                gaugeSum += PairwiseSumF32(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(finite));
                gaugeN += finite.Count;
                var mx = finite[0];
                for (var i = 1; i < finite.Count; i++) if (finite[i] > mx) mx = finite[i];
                gaugeMax = gaugeMax is null ? mx : Math.Max(gaugeMax.Value, mx);
            }

            var span = (long)a.RoundFrames[hi - 1] - a.RoundFrames[lo];
            if (span > 0)
            {
                frames += span;
                gain += ((long)c.ScoreRaw[hi - 1] - c.ScoreRaw[lo]) * 10;
            }

            for (var i = 0; i < n; i++) if (c.BossType[lo + i] == 3) bossPresentTicks++;

            UpdateTimerMax(c.CpuQuickTimerCur, lo, hi, ref cpuQuickTimerMax);
            UpdateTimerMax(c.CpuStandTimerCur, lo, hi, ref cpuStandTimerMax);

            for (var i = 0; i < n; i++)
            {
                if (S(c.FoeZeroHitTimer[lo + i]) > RecordLabels.ZeroHitFreezeFrames) cpuTimerFrozenTicks++;
            }

            UpdateTimerMax(c.ZeroHitTimer, lo, hi, ref noHitGapMaxTicks);

            bossReversalCleanCount += CleanReversals(
                c.BossType.AsSpan(lo, n), c.BossDepth.AsSpan(lo, n), c.BossReversals.AsSpan(lo, n));
        }

        var gaugeAvg = gaugeN > 0 ? gaugeSum / gaugeN : (double?)null;
        var scoreGainRate = frames > 0 ? gain / (frames / 60.0) : (double?)null;

        return new SideResult(spellCapTicks, spellPointsMax, spellPointsMaxFrame, spellPointsTotal,
            gaugeAvg, gaugeMax, scoreGainRate, bossPresentTicks,
            cpuQuickTimerMax, cpuStandTimerMax, cpuTimerFrozenTicks, noHitGapMaxTicks,
            bossReversalCleanCount);
    }

    private static void UpdateTimerMax(uint[] arr, int lo, int hi, ref long? current)
    {
        long? localMax = null;
        for (var i = lo; i < hi; i++)
        {
            var v = S(arr[i]);
            if (v == RecordLabels.TimerSentinel) continue;
            if (localMax is null || v > localMax) localMax = v;
        }
        if (localMax is null) return;
        current = current is null ? localMax : Math.Max(current.Value, localMax.Value);
    }


    internal static float PairwiseSumF32(ReadOnlySpan<float> a)
    {
        var n = a.Length;
        if (n == 0) return 0f;
        if (n < 8)
        {
            var res = 0f;
            for (var i = 0; i < n; i++) res += a[i];
            return res;
        }
        if (n <= 128)
        {
            Span<float> r = stackalloc float[8];
            for (var j = 0; j < 8; j++) r[j] = a[j];
            var i2 = 8;
            var limit = n - (n % 8);
            for (; i2 < limit; i2 += 8)
                for (var j = 0; j < 8; j++) r[j] += a[i2 + j];
            var res = ((r[0] + r[1]) + (r[2] + r[3])) + ((r[4] + r[5]) + (r[6] + r[7]));
            for (; i2 < n; i2++) res += a[i2];
            return res;
        }
        var n2 = n / 2;
        n2 -= n2 % 8;
        return PairwiseSumF32(a[..n2]) + PairwiseSumF32(a[n2..]);
    }


    internal static long CleanReversals(ReadOnlySpan<uint> bossType, ReadOnlySpan<uint> bossDepth,
                                        ReadOnlySpan<uint> reversals)
    {
        var n = bossType.Length;
        if (n == 0) return 0;
        var present = new bool[n];
        var any = false;
        for (var i = 0; i < n; i++) { present[i] = bossType[i] == 3; any |= present[i]; }
        if (!any) return 0;

        var starts = new List<int>();
        var stops = new List<int>();
        for (var i = 1; i < n; i++)
        {
            if (present[i] && !present[i - 1]) starts.Add(i);
            if (!present[i] && present[i - 1]) stops.Add(i);
        }
        if (present[0]) starts.Insert(0, 0);
        if (present[n - 1]) stops.Add(n);

        long clean = 0;
        var pairCount = Math.Min(starts.Count, stops.Count);
        for (var idx = 0; idx < pairCount; idx++)
        {
            var s = starts[idx];
            var e = stops[idx];
            var k = -1;
            for (var j = 1; j < e - s; j++)
            {
                if (reversals[s + j] > reversals[s + j - 1]) { k = j; break; }
            }
            if (k < 0) continue;
            var ok = true;
            for (var j = 0; j < k; j++)
            {
                if (bossDepth[s + j] > 0) { ok = false; break; }
            }
            if (ok) clean++;
        }
        return clean;
    }


    public readonly record struct KuraiKey(long? StageNumber, long RoundNumber, int Side);

    public static Dictionary<KuraiKey, long> KuraiCounts(SqliteConnection mainDb, long sessionId, long mode)
    {
        var outMap = new Dictionary<KuraiKey, long>();
        using var cmd = mainDb.CreateCommand();
        cmd.CommandText = "SELECT payload_json FROM events WHERE session_id=$0 AND event_type='HIT'";
        cmd.Parameters.AddWithValue("$0", sessionId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            if (r.IsDBNull(0)) continue;
            var payload = r.GetString(0);
            JsonElement root;
            try
            {
                using var doc = JsonDocument.Parse(payload);
                root = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                continue;
            }
            if (!root.TryGetProperty("kurai_c2", out var kuraiEl) || !Truthy(kuraiEl)) continue;
            if (!root.TryGetProperty("side", out var sideEl) || sideEl.ValueKind != JsonValueKind.Number) continue;
            var side = sideEl.GetInt32();
            if (side is not (1 or 2)) continue;
            var stageIndex = NumberOr0(root, "stage_index");
            var completedRounds = NumberOr0(root, "completed_rounds");
            var key = new KuraiKey(RoundRanges.StageNumberOf(mode, stageIndex), completedRounds + 1, side);
            outMap[key] = outMap.GetValueOrDefault(key) + 1;
        }
        return outMap;
    }

    private static long NumberOr0(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;

    private static bool Truthy(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => false,
        JsonValueKind.Number => e.GetDouble() != 0,
        JsonValueKind.String => e.GetString() is { Length: > 0 },
        JsonValueKind.Array => e.GetArrayLength() > 0,
        JsonValueKind.Object => e.EnumerateObject().Any(),
        _ => false,
    };


    public static (Dictionary<RoundRanges.RoundKey, long> Map, int Duplicates) RoundIdMap(
        SqliteConnection mainDb, long sessionId)
    {
        var map = new Dictionary<RoundRanges.RoundKey, long>();
        var dup = 0;
        using var cmd = mainDb.CreateCommand();
        cmd.CommandText = "SELECT r.round_record_id,s.stage_number,r.round_number FROM rounds r"
                         + " LEFT JOIN stages s ON s.stage_record_id=r.stage_record_id"
                         + " WHERE r.session_id=$0 ORDER BY r.round_record_id";
        cmd.Parameters.AddWithValue("$0", sessionId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var rid = r.GetInt64(0);
            long? sn = r.IsDBNull(1) ? null : r.GetInt64(1);
            var rn = r.GetInt64(2);
            var key = new RoundRanges.RoundKey(sn, rn);
            if (!map.TryAdd(key, rid)) dup++;
        }
        return (map, dup);
    }

    public static long? SessionMode(SqliteConnection mainDb, long sessionId)
    {
        using var cmd = mainDb.CreateCommand();
        cmd.CommandText = "SELECT game_mode FROM session_metadata WHERE session_id=$0";
        cmd.Parameters.AddWithValue("$0", sessionId);
        var v = cmd.ExecuteScalar();
        return v is null or DBNull ? null : Convert.ToInt64(v, CultureInfo.InvariantCulture);
    }


    public static Dictionary<string, object?> BuildRowValues(RoundRanges.Arrays a,
        IReadOnlyList<RoundRanges.Span> parts, long kuraiC2P1, long kuraiC2P2)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["total_ticks"] = parts.Sum(p => (long)(p.Stop - p.Start)),
            ["analysis_version"] = (long)RecordLabels.RoundMetricsAnalysisVersion,
        };
        foreach (var side in new[] { 1, 2 })
        {
            var m = SideMetricsOf(a, parts, side);
            var p = "p" + side.ToString(CultureInfo.InvariantCulture) + "_";
            values[p + "spell_cap_ticks"] = m.SpellCapTicks;
            values[p + "spell_points_max"] = m.SpellPointsMax;
            values[p + "spell_points_max_frame"] = m.SpellPointsMaxFrame;
            values[p + "spell_points_total"] = m.SpellPointsTotal;
            values[p + "gauge_avg"] = m.GaugeAvg;
            values[p + "gauge_max"] = m.GaugeMax;
            values[p + "score_gain_rate"] = m.ScoreGainRate;
            values[p + "boss_present_ticks"] = m.BossPresentTicks;
            values[p + "cpu_quick_timer_max"] = m.CpuQuickTimerMax;
            values[p + "cpu_stand_timer_max"] = m.CpuStandTimerMax;
            values[p + "cpu_timer_frozen_ticks"] = m.CpuTimerFrozenTicks;
            values[p + "no_hit_gap_max_ticks"] = m.NoHitGapMaxTicks;
            values[p + "boss_reversal_clean_count"] = m.BossReversalCleanCount;
            values[p + "kurai_c2_count"] = side == 1 ? kuraiC2P1 : kuraiC2P2;
        }
        foreach (var name in RecordLabels.RoundMetricsUncomputedColumns) values[name] = null;
        return values;
    }
}
