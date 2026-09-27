using System.Globalization;

namespace TH09.Record;

public static class StoryRecordsDump
{
    public const string Flag = "--dump-story-records";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(TextWriter w, string dbPath)
    {
        ArgumentNullException.ThrowIfNull(w);
        using var db = RecordDb.OpenReadOnly(dbPath);
        if (db is null)
        {
            Console.Error.WriteLine("本体 DB を読み取り専用で開けません: " + dbPath);
            return 1;
        }
        var c = db.Connection;

        var plays = StoryRecords.StoryPlays(c);
        foreach (var p in plays)
            Row(w, "play", N(p.SessionId), N(p.Mode), N(p.Character), N(p.Difficulty),
                N(p.StageMax), N(p.FinalScore), N(p.Misses), N(p.Extends),
                F(p.InitialLives), F(p.FinalLives));

        var recs = StoryRecords.StoryCharacterRecords(plays);
        foreach (var r in recs)
            Row(w, "char", N(r.Mode), N(r.Character), N(r.Plays), N(r.BestScore),
                N(r.BestScoreSession), N(r.MinMisses), N(r.MinMissSession),
                F(r.MaxFinalLives), N(r.MaxFinalLivesSession), N(r.StageMax));

        Row(w, "count", "play", N(plays.Count), "char", N(recs.Count));
        return 0;
    }

    private static string N(long? v) => v is null ? "~" : v.Value.ToString(Inv);

    private static string N(int? v) => v is null ? "~" : v.Value.ToString(Inv);

    private static string N(int v) => v.ToString(Inv);

    private static string F(double? v) => v is null ? "~" : v.Value.ToString("R", Inv);

    private static void Row(TextWriter w, params string[] cells) => w.Write(string.Join("\t", cells) + "\n");
}
