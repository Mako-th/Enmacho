using System.Globalization;

namespace TH09.Record;

public static class HistoryPruneDump
{
    public const string PlanFlag = "--history-plan";

    public const string PruneFlag = "--history-prune";

    public const string NoLayer0 = SessionDeleteDump.NoLayer0;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int RunPlan(TextWriter w, string dbPath, int keepAbortedReplay, int keepCompleted)
    {
        ArgumentNullException.ThrowIfNull(w);
        using var db = RecordDb.OpenReadOnly(dbPath);
        if (db is null)
        {
            Console.Error.WriteLine("本体 DB を読み取り専用で開けません: " + dbPath);
            return 1;
        }
        WritePlan(w, db, keepAbortedReplay, keepCompleted);
        return 0;
    }

    public static int RunPrune(TextWriter w, string dbPath, string? layer0Path,
                               int keepAbortedReplay, int keepCompleted)
    {
        ArgumentNullException.ThrowIfNull(w);
        if (HistoryMaintenance.PointsAtRealData(dbPath, layer0Path))
        {
            Console.Error.WriteLine("★本物の記録のフォルダは受け付けません（合成の対を渡してください）: "
                                    + dbPath + " / " + (layer0Path ?? NoLayer0));
            return 3;
        }
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine("本体 DB がありません: " + dbPath);
            return 1;
        }
        PrunePlan plan;
        using (var ro = RecordDb.OpenReadOnly(dbPath))
        {
            if (ro is null)
            {
                Console.Error.WriteLine("本体 DB を読み取り専用で開けません: " + dbPath);
                return 1;
            }
            plan = WritePlan(w, ro, keepAbortedReplay, keepCompleted);
        }
        Row(w, "fact", "real_main_db", Paths.Default.MainDb);
        Row(w, "fact", "real_layer0_db", Paths.Default.Layer0Db);
        var logs = new List<string>();
        try
        {
            var r = HistoryMaintenance.Delete(dbPath, layer0Path, plan.ToDelete, logs.Add);
            foreach (var line in logs) Row(w, "log", line);
            Row(w, "result",
                "deleted=" + Join(r.Deleted),
                "layer0_rows=" + r.Layer0Rows.ToString(Inv),
                "tombstoned=" + Join(r.Tombstoned),
                "without_tombstone=" + Join(r.WithoutTombstone),
                "running=" + Join(r.Running));
        }
        catch (Exception exc)
        {
            foreach (var line in logs) Row(w, "log", line);
            Row(w, "raised", exc.GetType().Name, exc.Message.Replace("\n", "\\n", StringComparison.Ordinal));
        }
        Row(w, "end");
        return 0;
    }

    private static PrunePlan WritePlan(TextWriter w, RecordDb db, int keepAbortedReplay, int keepCompleted)
    {
        var c = db.Connection;
        var plan = HistoryMaintenance.Plan(c, keepAbortedReplay, keepCompleted);
        Row(w, "keep", keepAbortedReplay.ToString(Inv), keepCompleted.ToString(Inv));
        Row(w, "protected", Join(plan.Protected.Order().ToList()));
        foreach (var sid in plan.AbortedOrReplay) Row(w, "category", sid.ToString(Inv), "abrep");
        foreach (var sid in plan.Completed) Row(w, "category", sid.ToString(Inv), "completed");
        Row(w, "plan", Join(plan.ToDelete));
        foreach (var f in SessionProtection.FinalScoreBests(c))
            Row(w, "final", Num(f.Mode), Num(f.Character), Num(f.Difficulty), f.Score.ToString(Inv),
                f.Source, Num(f.SessionId), Num(f.ReplayId));
        var links = SessionProtection.Scanned(c);
        Row(w, "scanned", Join(links.ReplacedReplayIds.Order().ToList()),
            string.Join(",", links.ReplayBySession.OrderBy(x => x.Key)
                                 .Select(x => x.Key.ToString(Inv) + ":" + x.Value.ToString(Inv))));
        return plan;
    }

    private static string Num(long? v) => v is long x ? x.ToString(Inv) : "~";

    private static string Join(IReadOnlyList<long> values) =>
        values.Count == 0 ? "" : string.Join(",", values.Select(v => v.ToString(Inv)));

    private static void Row(TextWriter w, params string[] cells) =>
        w.Write(string.Join("\t", cells) + "\n");
}
