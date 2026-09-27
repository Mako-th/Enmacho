using System.Globalization;

namespace TH09.Record;

public static class LiveProgressDump
{
    public const string Flag = "--dump-live-progress";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(TextWriter w, string dbPath, bool ownOnly, IReadOnlyList<long> sids)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(sids);
        using var db = RecordDb.OpenReadOnly(dbPath);
        if (db is null)
        {
            Console.Error.WriteLine("本体 DB を読み取り専用で開けません: " + dbPath);
            return 1;
        }
        var c = db.Connection;

        Row(w, "opt", "own_only", ownOnly ? "1" : "0", "sids", sids.Count == 0 ? "~" : Join(sids));

        var own = SessionSide.OwnReplayNames(c);
        foreach (var n in own.Keep) Row(w, "name", "keep", n.Name, N(n.Count));
        foreach (var n in own.Drop) Row(w, "name", "drop", n.Name, N(n.Count));
        Row(w, "name", "my", own.MyName ?? "~", "~");

        var ses = MatchBests.Sessions(c, own.Names);
        foreach (var s in ses.Values.OrderBy(x => x.Sid))
            Row(w, "matchses", N(s.Sid), SessionSide.Text(s.Bucket), N(s.Side), s.Own ? "1" : "0",
                s.Provisional ? "1" : "0", N(s.Me), N(s.Foe), N(s.ReplayId), N(s.Difficulty));

        var cells = MatchBests.Bests(c, ownOnly: ownOnly, names: own.Names);
        foreach (var kv in cells.OrderBy(x => x.Key.Text(), StringComparer.Ordinal))
        {
            var b = kv.Value;
            Row(w, "matchbest", kv.Key.Text(), N(b.Time), N(b.Round), N(b.Sid),
                b.Src is null ? "~" : b.Src.Text(), Join(b.Holders),
                N(b.Sessions), N(b.Rounds), N(b.Skipped));
        }

        long?[] live = sids.Count == 0 ? [null] : sids.Select(x => (long?)x).ToArray();
        var shown = 0;
        foreach (var sid in live)
        {
            var v = LiveProgress.Of(c, sid, ownOnly);
            if (v is null)
            {
                Row(w, "live", sid is null ? "~" : N(sid), "none");
                continue;
            }
            shown++;
            Row(w, "live", N(v.Session), "head", N(v.Mode), N(v.Difficulty), N(v.Character), N(v.P2),
                v.Status ?? "~", v.Execution, v.IsReplay ? "1" : "0", v.Comparable ? "1" : "0",
                F(v.Lives), N(v.Combo), N(v.MaxCombo), N(v.SpellPoints));
            foreach (var st in v.Stages)
                Row(w, "livestage", N(v.Session), N(st.Stage), N(st.Opponent), st.Running ? "1" : "0",
                    N(st.Score), N(st.Segment), N(st.Best), Src(st.BestSrc), N(st.Delta),
                    N(st.Time), N(st.BestTime), N(st.TimeDelta));
            foreach (var r in v.Rounds)
                Row(w, "liveround", N(v.Session), N(r.Round), N(r.Time), N(r.Winner),
                    r.Running ? "1" : "0", r.Status ?? "~", N(r.Life1), N(r.Life2),
                    N(r.Best), N(r.Delta));
            if (v.Total is { } t)
                Row(w, "livetotal", N(v.Session), N(t.Score), N(t.Best), Src(t.BestSrc), N(t.Delta));
            if (v.Match is { } m)
                Row(w, "livematch", N(v.Session),
                    m.Kind is null ? "~" : SessionSide.Text(m.Kind.Value), N(m.Side),
                    B(m.Provisional), B(m.Own), N(m.Me), N(m.Foe), N(m.Current), N(m.Longest),
                    N(m.Shown), m.ShownIsCurrent ? "1" : "0", N(m.Best), N(m.BestRound),
                    Src(m.BestSrc), N(m.Sessions), N(m.Rounds), N(m.Skipped), N(m.Delta));
        }

        Row(w, "count", "matchses", N(ses.Count), "sided", N(ses.Values.Count(x => x.Side is not null)),
            "matchbest", N(cells.Count), "live", N(shown), "names", N(own.Keep.Count));
        return 0;
    }

    private static string Src(BestSource? s) => s is null ? "~" : s.Text();

    private static string N(long? v) => v is null ? "~" : v.Value.ToString(Inv);

    private static string N(int? v) => v is null ? "~" : v.Value.ToString(Inv);

    private static string N(int v) => v.ToString(Inv);

    private static string B(bool? v) => v is null ? "~" : v.Value ? "1" : "0";

    private static string F(double? v) => v is null ? "~" : v.Value.ToString("R", Inv);

    private static string Join(IEnumerable<long> ids) => string.Join(",", ids.Select(x => x.ToString(Inv)));

    private static void Row(TextWriter w, params string[] cells) => w.Write(string.Join("\t", cells) + "\n");
}
