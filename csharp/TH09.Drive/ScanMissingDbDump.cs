using System.Runtime.Versioning;
using System.Text;
using TH09.Record;

namespace TH09.Drive;

[SupportedOSPlatform("windows")]
internal static class ScanMissingDbDump
{
    public const string Flag = "--dump-scan-missing-db";

    public static int Run(TextWriter w, string[] args)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length < 2)
        {
            Console.Error.WriteLine("使い方: " + Flag + " <db> <root> [走査の旗…]");
            return 2;
        }
        var db = args[0];
        var root = args[1];
        if (RealDbGuard.InMainDbDir(db))
        {
            Console.Error.WriteLine("★本物の本体 DB のフォルダは使えません: " + db);
            return 2;
        }
        var a = ScanCommand.Parse(["--db", db, .. args[2..]]);
        var plan = ScanRunModes.Plan(a.Run, null);
        var paths = Paths.Resolve(root);
        var missing = ScanCommand.ReplaysEmpty(db);

        var said = new StringWriter { NewLine = "\n" };
        int? registered = null;
        var stop = ScanCommand.PrepareMissingDb(
            said, a, plan, missing,
            () => (registered = WatchCommand.ImportExisting(said, db, paths)).Value);

        Row(w, "fact", "missing_before", missing ? "1" : "0");
        Row(w, "fact", "run", plan.Name);
        Row(w, "result", "stop", stop?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-");
        Row(w, "result", "registered",
            registered?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-");
        Row(w, "result", "exists_after", File.Exists(db) ? "1" : "0");
        var hasReplays = "0";
        var targets = "-";
        if (File.Exists(db))
        {
            using var ro = RecordDb.OpenReadOnly(db);
            if (ro is not null && ro.TableNames().Contains("replays", StringComparer.Ordinal))
            {
                hasReplays = "1";
            }
            if (ro is not null && hasReplays == "1" && a.MinRecordVersion is null)
            {
                targets = ScanTargets.SelectTargets(
                    ro.Connection, ScanTargets.ForBatch(ScanCommand.SharedFilter(a), plan.IncludesDone, a.Max))
                    .Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        Row(w, "result", "replays_table", hasReplays);
        Row(w, "result", "targets", targets);
        foreach (var line in said.ToString().Split('\n'))
        {
            if (line.Length > 0) Row(w, "out", line);
        }
        return 0;
    }

    private static void Row(TextWriter w, params string[] cells)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < cells.Length; i++)
        {
            if (i > 0) sb.Append('\t');
            sb.Append(cells[i].Replace('\t', ' ').Replace('\n', ' '));
        }
        w.Write(sb.Append('\n').ToString());
    }
}
