using System.Globalization;

namespace TH09.Record;

public static class SelfBestsDump
{
    public const string Flag = "--dump-self-bests";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(TextWriter w, string dbPath, bool ownOnly, bool skipScanned)
    {
        ArgumentNullException.ThrowIfNull(w);
        using var db = RecordDb.OpenReadOnly(dbPath);
        if (db is null)
        {
            Console.Error.WriteLine("本体 DB を読み取り専用で開けません: " + dbPath);
            return 1;
        }
        var c = db.Connection;

        Row(w, "opt", "own_only", ownOnly ? "1" : "0", "skip_scanned", skipScanned ? "1" : "0");

        var seg = SelfBests.ReplaySegmentScores(c, ownOnly: ownOnly, skipScanned: skipScanned);
        foreach (var s in seg)
            Row(w, "repseg", N(s.ReplayId), N(s.Mode), N(s.Character), N(s.Difficulty),
                N(s.Stage), N(s.Delta), N(s.ScoreAtEnd), N(s.Opponent), s.IsOwn ? "1" : "0");

        foreach (var f in SelfBests.ReplayFinalScores(c, ownOnly: ownOnly))
            Row(w, "repfinal", N(f.ReplayId), N(f.Mode), N(f.Character), N(f.Difficulty),
                N(f.Score), f.IsOwn ? "1" : "0");

        foreach (var b in SelfBests.BestTable(c, ownOnly: ownOnly))
            Row(w, "best", N(b.Mode), N(b.Character), N(b.Difficulty), N(b.Stage), N(b.Opponent),
                N(b.Score), Src(b.ScoreSource), N(b.Time), Src(b.TimeSource));

        Row(w, "count", "repseg", N(seg.Count), "replays", N(seg.Select(x => x.ReplayId).Distinct().Count()));
        return 0;
    }

    private static string Src(BestSource? s) => s is null ? "~" : s.Text();

    private static string N(long? v) => v is null ? "~" : v.Value.ToString(Inv);

    private static string N(int v) => v.ToString(Inv);

    private static void Row(TextWriter w, params string[] cells) => w.Write(string.Join("\t", cells) + "\n");
}
