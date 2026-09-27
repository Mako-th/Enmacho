using System.Globalization;

namespace TH09.Record;

public static class RoundRangesDump
{
    public const string Flag = "--dump-round-ranges";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(TextWriter w, string layer0Path, long? onlySession)
    {
        ArgumentNullException.ThrowIfNull(w);
        using var layer0 = TickReplay.OpenLayer0(layer0Path);
        var sessions = onlySession is { } only ? [only] : TickReplay.Sessions(layer0);

        var entries = new List<RoundRanges.Entry>();
        var skipped = new List<(long SessionId, string Why)>();
        foreach (var s in sessions)
        {
            var (data, why) = RoundRanges.LoadColumns(layer0, s);
            if (data is null)
            {
                skipped.Add((s, why ?? "?"));
                continue;
            }
            entries.AddRange(RoundRanges.Flatten(s, RoundRanges.Compute(data)));
        }

        foreach (var e in entries
                 .OrderBy(e => e.SessionId)
                 .ThenBy(e => e.Start)
                 .ThenBy(e => e.StageNumber ?? -1)
                 .ThenBy(e => e.RoundNumber))
        {
            w.Write(Num(e.SessionId) + "\t" + (e.StageNumber is { } sn ? Num(sn) : "")
                    + "\t" + Num(e.RoundNumber) + "\t" + Num(e.Start) + "\t" + Num(e.Stop) + "\n");
        }

        foreach (var (sid, why) in skipped)
            Console.Error.WriteLine("session=" + Num(sid) + " を見送り: " + why);
        Console.Error.WriteLine("★母数: " + Num(sessions.Count) + " セッションのうち "
                                + Num(sessions.Count - skipped.Count) + " セッションから "
                                + Num(entries.Count) + " 区間");
        return entries.Count > 0 ? 0 : 1;
    }

    private static string Num(long value) => value.ToString(Inv);
}
