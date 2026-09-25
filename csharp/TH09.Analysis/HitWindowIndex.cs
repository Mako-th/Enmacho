namespace TH09.Analysis;


public readonly record struct HitWindowRef(long WindowNo, long FirstSeq, int TickCount,
                                           int? Side, string? Trigger)
{
    public bool IsQuickOnly => Trigger is not null
                               && Window.NonHitTriggers.Contains(Trigger, StringComparer.Ordinal);
}

public sealed class RoundWindows
{
    public required int Index { get; init; }
    public required int? Stage { get; init; }
    public required int Round { get; init; }
    public required long FirstSeq { get; init; }
    public required long LastSeq { get; init; }
    public required List<HitWindowRef> Windows { get; init; }
}

public static class HitWindowIndex
{
    public static readonly string[] RoundOnlyFields =
        ["round_frames", "flags", "seq_begin", "stage_index", "completed_rounds"];

    public static List<RoundWindows> ForSession(AnalysisDb l0, long sessionId)
    {
        var rounds = TimelineReader.LoadSession(l0, sessionId, RoundOnlyFields);
        if (rounds.Count == 0) return [];

        var all = AllWindows(l0, sessionId);
        var outList = new List<RoundWindows>(rounds.Count);
        foreach (var r in rounds)
        {
            var (stage, no) = r.StageRound();
            var mine = r.FirstSeq < 0
                ? []
                : all.Where(w => w.FirstSeq >= r.FirstSeq && w.FirstSeq <= r.LastSeq).ToList();
            outList.Add(new RoundWindows
            {
                Index = r.Index, Stage = stage, Round = no,
                FirstSeq = r.FirstSeq, LastSeq = r.LastSeq, Windows = mine,
            });
        }
        return outList;
    }

    public static List<HitWindowRef> AllWindows(AnalysisDb l0, long sessionId)
    {
        var outList = new List<HitWindowRef>();
        if (!l0.HasTable("session_hit_windows")) return outList;
        using var cmd = l0.Command(
            "SELECT window_no,first_seq,tick_count,hits FROM session_hit_windows"
            + " WHERE session_id=$0 ORDER BY window_no", sessionId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            long wno = r.GetInt64(0), firstSeq = r.GetInt64(1);
            int ticks = r.GetInt32(2);
            var sides = PerSide(r.GetString(3));
            if (sides.Count == 0)
            {
                outList.Add(new HitWindowRef(wno, firstSeq, ticks, null, null));
                continue;
            }
            foreach (var s in sides)
                outList.Add(new HitWindowRef(wno, firstSeq, ticks, s.Side, s.Trigger));
        }
        return outList;
    }

    internal static List<(int Side, string Trigger, long Seq)> PerSide(string hitsJson)
    {
        var outList = new List<(int Side, string Trigger, long Seq)>();
        try
        {
            if (string.IsNullOrEmpty(hitsJson)) return outList;
            using var doc = System.Text.Json.JsonDocument.Parse(hitsJson);
            var first = new Dictionary<int, (string Trigger, long Seq)>();
            var real = new Dictionary<int, (string Trigger, long Seq)>();
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                int side = e.GetProperty("side").GetInt32();
                string trig = e.GetProperty("trigger").GetString()!;
                long seq = e.GetProperty("seq").GetInt64();
                if (!first.ContainsKey(side)) first[side] = (trig, seq);
                if (!real.ContainsKey(side)
                    && !Window.NonHitTriggers.Contains(trig, StringComparer.Ordinal))
                    real[side] = (trig, seq);
            }
            foreach (var side in first.Keys)
            {
                var pick = real.TryGetValue(side, out var got) ? got : first[side];
                outList.Add((side, pick.Trigger, pick.Seq));
            }
            outList.Sort((a, b) => a.Seq != b.Seq ? a.Seq.CompareTo(b.Seq)
                                                  : a.Side.CompareTo(b.Side));
            return outList;
        }
        catch (Exception)
        {
            outList.Clear();
            return outList;
        }
    }
}
