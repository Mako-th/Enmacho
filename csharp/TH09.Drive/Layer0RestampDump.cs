using System.Globalization;
using System.Runtime.Versioning;
using TH09.Layer0;
using TH09.Record;

using IOPath = System.IO.Path;

namespace TH09.Drive;

[SupportedOSPlatform("windows")]
internal static class Layer0RestampDump
{
    public const string SubMode = "restamp";

    private static readonly string[] Cases =
    [
        "nobackup",
        "notlayer0",
        "missing",
        "dryrun",
        "busy",
        "restamp",
        "again",
    ];

    public static int Run(TextWriter w, string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine(
                ScanOneDump.Flag + " " + CapturePartsDump.SubMode + " " + SubMode
                + " <偽リポジトリの根> <日時 yyyyMMdd_HHmmss> の形で呼んでください。");
            return 2;
        }
        var root = args[0];
        if (!DateTime.TryParseExact(args[1], ScanBackup.StampFormat, Inv, DateTimeStyles.None,
                                    out var now))
        {
            Console.Error.WriteLine("日時が読めません（" + ScanBackup.StampFormat + "）: " + args[1]);
            return 2;
        }

        Row(w, "fact", "flag", Layer0RestampCommand.Flag);
        Row(w, "fact", "v1", SolidBrotli.Encoding);
        Row(w, "fact", "v2", SolidBrotli.EncodingV2);
        Row(w, "fact", "cases", Num(Cases.Length));

        var rc = 0;
        for (var i = 0; i < Cases.Length; i++)
        {
            var here = IOPath.Combine(root, Cases[i]);
            if (!Case(w, here, Cases[i], now.AddMinutes(i))) rc = 1;
        }
        Row(w, "end", Num(Cases.Length));
        return rc;
    }


    private static bool Case(TextWriter w, string root, string name, DateTime now)
    {
        var paths = Build(root, name);
        var live = paths.Layer0Db;

        if (name != "nobackup" && name != "missing" && name != "notlayer0")
        {
            var body = new StringWriter();
            try
            {
                ScanBackup.Run(body, ScanBackup.Plan(new ScanBackup.Options(paths, [], now)));
            }
            catch (Exception exc)
            {
                Row(w, name, "backup_raised", exc.GetType().Name, One(exc.Message));
            }
            File.Delete(paths.MainDb);
        }

        var spare = "th09-test-layer0-restamp-" + IOPath.GetFileName(root);

        if (name == "again")
        {
            var primed = Layer0RestampCommand.Plan(paths);
            var primeOut = new StringWriter();
            var primeCode = Layer0RestampCommand.Run(primeOut, primed, dryRun: false, paths, spare);
            Row(w, name, "primed_code", Num(primeCode));
        }

        var beforeStatus = File.Exists(live) ? SafeInspect(live) : null;
        Row(w, name, "before_v1", beforeStatus is null ? "-" : Num(beforeStatus.TotalV1),
            beforeStatus is null ? "-" : Num(beforeStatus.TotalV2));
        var beforeDigest = Fingerprint(Layer0StampSample.SegmentDigest(live));
        Row(w, name, "before_digest", Num(beforeDigest));

        var plan = Layer0RestampCommand.Plan(paths);
        Row(w, name, "plan", plan.Blocked is null ? "-" : One(Rel(root, plan.Blocked)),
            plan.AlreadyDone ? "1" : "0",
            plan.Backup is null ? "-" : "backup",
            plan.Shape.IsLayer0 ? "1" : "0", Num(plan.Status.TotalV1));

        var dry = name == "dryrun";
        IDisposable? busyHolder = name == "busy"
            ? SessionWriter.OpenWithSpareLock(paths.MainDb, spare, _ => { })
            : null;

        int code;
        var out1 = new StringWriter();
        try
        {
            code = Layer0RestampCommand.Run(out1, plan, dry, paths, spare);
        }
        finally
        {
            busyHolder?.Dispose();
        }
        foreach (var line in Lines(out1)) Row(w, name, "out", Rel(root, line));
        Row(w, name, "code", Num(code));
        Row(w, name, "warned",
            Lines(out1).Any(l => l.Contains(Layer0RestampCommand.Warning, StringComparison.Ordinal))
                ? "1" : "0");

        var afterStatus = File.Exists(live) ? SafeInspect(live) : null;
        Row(w, name, "after_v1", afterStatus is null ? "-" : Num(afterStatus.TotalV1),
            afterStatus is null ? "-" : Num(afterStatus.TotalV2));
        Row(w, name, "after_digest", Num(Fingerprint(Layer0StampSample.SegmentDigest(live))));

        if (afterStatus is not null && afterStatus.TotalV2 > 0)
        {
            var (v1Remaining, v2Checked, v2Failed) =
                Layer0Restamp.VerifyAll(live, TextWriter.Null);
            Row(w, name, "verify_all", Num(v1Remaining), Num(v2Checked), Num(v2Failed));
        }
        return true;
    }


    private static Paths Build(string root, string name)
    {
        Directory.CreateDirectory(IOPath.Combine(root, "python"));
        Directory.CreateDirectory(IOPath.Combine(root, "project_material_documents"));
        File.WriteAllText(IOPath.Combine(root, "python", "config.json"), "{}");
        File.WriteAllText(IOPath.Combine(root, "th09_tracker.sqlite3"), "MAIN-DB");
        var paths = Paths.Resolve(root);
        Directory.CreateDirectory(IOPath.GetDirectoryName(paths.Layer0Db)!);
        if (name == "missing") return paths;
        if (name == "notlayer0")
        {
            File.WriteAllText(paths.Layer0Db, "NOT-A-LAYER0");
            return paths;
        }
        Fill(paths.Layer0Db);
        return paths;
    }

    private static void Fill(string dbPath) =>
        Layer0StampSample.BuildUnstamped(
            CaptureParts.ArchiveOptions(dbPath, Layer0OpenMode.Create, SolidBrotli.Encoding),
            [(11L, 8), (23L, 5)]);


    private static Layer0Restamp.Status SafeInspect(string dbPath)
    {
        try
        {
            return Layer0Restamp.Inspect(dbPath);
        }
        catch (Exception)
        {
            return new Layer0Restamp.Status([]);
        }
    }


    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static string Num(long value) => value.ToString(Inv);

    private static string One(string text) =>
        text.Replace("\r", "").Replace("\n", " ").Replace("\t", " ");

    private static string Rel(string root, string text)
    {
        var full = IOPath.GetFullPath(root).TrimEnd(IOPath.DirectorySeparatorChar);
        return text.Replace(full, "<root>", StringComparison.OrdinalIgnoreCase);
    }

    private static long Fingerprint(List<string> rows)
    {
        long hash = 1469598103934665603L;
        foreach (var text in rows)
        {
            foreach (var ch in text)
            {
                hash ^= ch;
                hash *= 1099511628211L;
            }
        }
        return hash & 0x7FFFFFFFFFFFL;
    }

    private static List<string> Lines(StringWriter body)
    {
        var text = body.ToString().Replace("\r\n", "\n");
        var lines = text.Split('\n');
        var last = lines.Length;
        while (last > 0 && lines[last - 1].Length == 0) last--;
        return [.. lines[..last]];
    }

    private static void Row(TextWriter w, params string[] cells) =>
        w.Write(string.Join("\t", cells) + "\n");
}
