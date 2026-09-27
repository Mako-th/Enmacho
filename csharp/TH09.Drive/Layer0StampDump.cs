using System.Globalization;
using System.Runtime.Versioning;
using TH09.Layer0;
using TH09.Record;

using IOPath = System.IO.Path;

namespace TH09.Drive;

[SupportedOSPlatform("windows")]
internal static class Layer0StampDump
{
    public const string SubMode = "stamp";

    private static readonly string[] Cases =
    [
        "nobackup",
        "notlayer0",
        "missing",
        "dryrun",
        "press",
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

        Row(w, "fact", "flag", Layer0StampCommand.Flag);
        Row(w, "fact", "table", Layer0Stamp.Table);
        Row(w, "fact", "state_pressed", Layer0Stamp.StatePressed);
        Row(w, "fact", "state_created", Layer0Stamp.StateCreated);
        Row(w, "fact", "tool", Layer0StampCommand.ToolName);
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
        var before = Rows(live);
        Row(w, name, "before_rows", Num(before.Count), Num(Fingerprint(before)));

        if (name != "nobackup")
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
        }

        var plan = Layer0StampCommand.Plan(paths);
        Row(w, name, "plan", plan.Blocked is null ? "-" : One(Rel(root, plan.Blocked)),
            plan.AlreadyStamped ? "1" : "0",
            plan.Backup is null ? "-" : "backup",
            plan.State.Stamped ? "1" : "0", plan.State.IsLayer0 ? "1" : "0");

        var dry = name == "dryrun";
        var out1 = new StringWriter();
        var code = Layer0StampCommand.Run(out1, plan, dry);
        foreach (var line in Lines(out1)) Row(w, name, "out", Rel(root, line));
        Row(w, name, "code", Num(code));
        Row(w, name, "warned",
            Lines(out1).Any(l => l.Contains(Layer0StampCommand.Warning, StringComparison.Ordinal))
                ? "1" : "0");

        var after = Rows(live);
        var state = Layer0Stamp.Inspect(live);
        Row(w, name, "after_rows", Num(after.Count), Num(Fingerprint(after)));
        Row(w, name, "stamped", state.Stamped ? "1" : "0", Num(state.StampRows));
        Row(w, name, "untouched",
            before.Count == after.Count && Fingerprint(before) == Fingerprint(after) ? "1" : "0");

        var scanOut = new StringWriter();
        Layer0Ready? ready = null;
        try
        {
            ready = CaptureParts.PrepareLayer0(
                scanOut, new CaptureParts.Options(Paths: paths, Now: now, Emptying: false),
                backup: false, replayFiles: []);
        }
        catch (Exception exc)
        {
            Row(w, name, "scan_raised", exc.GetType().Name, One(Rel(root, exc.Message)));
        }
        if (ready is not null)
        {
            Row(w, name, "scan_ready", Rel(root, ready.Destination),
                ready.Appending ? "1" : "0", ready.Emptying ? "1" : "0");
            try
            {
                using var writer = Layer0Writer.Open(CaptureParts.ArchiveOptions(
                    ready.Destination,
                    ready.Appending ? Layer0OpenMode.Append : Layer0OpenMode.Create));
                Row(w, name, "writer", "ok");
            }
            catch (Exception exc)
            {
                Row(w, name, "writer", "raised", exc.GetType().Name, One(exc.Message));
            }
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
        if (name == "again") Layer0Stamp.Press(paths.Layer0Db, Layer0StampCommand.ToolName);
        return paths;
    }

    private static void Fill(string dbPath) =>
        Layer0StampSample.BuildUnstamped(
            CaptureParts.ArchiveOptions(dbPath, Layer0OpenMode.Create),
            [(11L, 8), (23L, 5)]);


    private static List<string> Rows(string dbPath) => Layer0StampSample.SegmentDigest(dbPath);

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


    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static string Num(long value) => value.ToString(Inv);

    private static string One(string text) =>
        text.Replace("\r", "").Replace("\n", " ").Replace("\t", " ");

    private static string Rel(string root, string text)
    {
        var full = IOPath.GetFullPath(root).TrimEnd(IOPath.DirectorySeparatorChar);
        return text.Replace(full, "<root>", StringComparison.OrdinalIgnoreCase);
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
