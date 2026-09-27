using System.Globalization;
using TH09.Record;

namespace TH09.Shell.Data;

internal static class StatsBestSource
{
    public const string Head = "出典 ";

    public const string Estimate = "未走査";

    public const string Sep = " ／ ";

    public static string Label(BestSource s, bool final = false, bool includeReplayId = true)
    {
        ArgumentNullException.ThrowIfNull(s);
        return s.Kind switch
        {
            BestSource.Live => "実プレイ #" + N(s.SessionId),
            BestSource.Scan => "スキャン #" + N(s.SessionId)
                              + (includeReplayId ? "（Replay #" + N(s.ReplayId) + "）" : ""),
            _ => final ? "リプレイ #" + N(s.ReplayId) + "（最終面到達・以上）"
                       : "リプレイ #" + N(s.ReplayId),
        };
    }

    public static string Line(BestSource? live, BestSource? replay, long? replayValue, bool final)
    {
        var parts = new List<string>(2);
        if (live is not null) parts.Add(Label(live));
        if (replay is not null && replayValue is long v)
            parts.Add(Estimate + Label(replay, final) + " に "
                      + (final ? "≥ " : "") + StatsFormat.Number(v));
        return parts.Count == 0 ? "" : Head + string.Join(Sep, parts);
    }


    public static (BestSource? Source, long? Value) BestReplayFinal(
        IReadOnlyList<ReplayFinalScore> rows, StatsLevel[] levels, IReadOnlyList<string> path,
        bool foreign, StatsCharFilter chars)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(chars);
        var kept = StatsLeafQuery.ApplyPath(
            rows.Where(r => (foreign || r.IsOwn) && chars.KeepsSelf(Int(r.Character))).ToList(),
            levels, path, AxisKey);
        ReplayFinalScore? best = null;
        foreach (var r in kept)
            if (best is null || r.Score > best.Score) best = r;
        return best is null ? (null, null) : (BestSource.OfReplay(best.ReplayId), best.Score);
    }

    public static (BestSource? Source, long? Value) BestReplayReach(
        IReadOnlyList<ReplaySegmentScore> rows, StatsLevel[] levels, IReadOnlyList<string> path,
        bool foreign, StatsCharFilter chars)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(chars);
        var kept = StatsLeafQuery.ApplyPath(
            rows.Where(r => (foreign || r.IsOwn)
                            && chars.Keeps(Int(r.Character), Int(r.Opponent))).ToList(),
            levels, path, AxisKey);
        ReplaySegmentScore? best = null;
        foreach (var r in kept)
            if (best is null || r.ScoreAtEnd > best.ScoreAtEnd) best = r;
        return best is null ? (null, null) : (BestSource.OfReplay(best.ReplayId), best.ScoreAtEnd);
    }

    private static string AxisKey(StatsAxis axis, ReplayFinalScore r) => axis switch
    {
        StatsAxis.ModeDifficulty => Key(r.Mode) + "," + Key(r.Difficulty),
        StatsAxis.MyCharacter => Key(r.Character),
        _ => throw new InvalidOperationException("リプレイの最終スコアに " + axis + " の軸は無い"),
    };

    private static string AxisKey(StatsAxis axis, ReplaySegmentScore r) => axis switch
    {
        StatsAxis.ModeDifficulty => Key(r.Mode) + "," + Key(r.Difficulty),
        StatsAxis.MyCharacter => Key(r.Character),
        StatsAxis.Stage => Key(r.Stage),
        StatsAxis.FoeCharacter => Key(r.Opponent),
        _ => throw new InvalidOperationException("リプレイの区間スコアに " + axis + " の軸は無い"),
    };

    private static string Key(long? v) => v is long i ? i.ToString(CultureInfo.InvariantCulture) : "null";

    private static int? Int(long? v) => v is long i ? (int)i : null;

    private static string N(long? v) => v is long i ? i.ToString(CultureInfo.InvariantCulture) : "?";
}
