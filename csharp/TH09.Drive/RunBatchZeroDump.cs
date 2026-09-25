using System.Globalization;
using System.Runtime.Versioning;
using TH09.Record;

using IOPath = System.IO.Path;

namespace TH09.Drive;

[SupportedOSPlatform("windows")]
internal static class RunBatchZeroDump
{
    public const string Flag = "--dump-run-batch-zero";

    public static int Run(TextWriter w, string[] args)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length != 1)
        {
            Console.Error.WriteLine(Flag + " <偽リポジトリの根> の形で呼んでください。");
            return 2;
        }
        var root = args[0];
        var paths = Paths.Resolve(root);
        Row(w, "fact", "layer0_db", Rel(root, paths.Layer0Db));
        Row(w, "fact", "backup_root", Rel(root, paths.BackupRoot));
        Row(w, "fact", "no_targets", ScanBatch.NoTargets);

        Row(w, "before", "layer0_exists", File.Exists(paths.Layer0Db) ? "1" : "0");
        Row(w, "before", "backup_dirs", Num(BackupDirCount(paths)));

        var a = new ScanArgs(Db: IOPath.Combine(root, "th09_tracker.sqlite3"), Batch: true);
        var body = new StringWriter();
        int exitCode;
        Exception? raised = null;
        try
        {
            exitCode = CaptureParts.RunBatch(
                body, a, targets: [], replayFiles: [], log: _ => { },
                o: new CaptureParts.Options(Paths: paths));
        }
        catch (Exception exc)
        {
            raised = exc;
            exitCode = -1;
        }

        Row(w, "result", "exit_code", Num(exitCode));
        Row(w, "result", "raised", raised is null ? "-" : raised.GetType().Name + ": " + raised.Message);
        foreach (var line in Lines(body)) Row(w, "out", Rel(root, line));
        Row(w, "after", "layer0_exists", File.Exists(paths.Layer0Db) ? "1" : "0");
        Row(w, "after", "backup_dirs", Num(BackupDirCount(paths)));

        var otherRoot = IOPath.Combine(root, "would_have_printed");
        Directory.CreateDirectory(otherRoot);
        var otherPaths = Paths.Resolve(otherRoot);
        var control = new StringWriter();
        try
        {
            CaptureParts.PrepareLayer0(control, new CaptureParts.Options(Paths: otherPaths),
                                       backup: true, replayFiles: []);
        }
        catch (Exception exc)
        {
            Row(w, "control", "raised", exc.GetType().Name + ": " + exc.Message);
        }
        var controlLines = Lines(control).ToList();
        Row(w, "control", "lines", Num(controlLines.Count));
        var recognized = 0;
        foreach (var line in controlLines)
        {
            var hit = ScanProgressLines.TryParse(line) is not null
                    || ScanProgressLines.TryParseBatchOutcome(line, out _)
                    || ScanProgressLines.TryParseStopReason(line, out _)
                    || ScanProgressLines.TryParseImportResult(line, out _)
                    || ScanProgressLines.TryParseScanPlan(line, out _)
                    || MonitorLines.SessionMark(line) is not null
                    || MonitorLines.IsNotice(line);
            if (hit)
            {
                recognized++;
                Row(w, "control", "recognized", Rel(root, line));
            }
        }
        Row(w, "control", "recognized_count", Num(recognized));
        return 0;
    }

    private static int BackupDirCount(Paths paths) =>
        Directory.Exists(paths.BackupRoot) ? Directory.GetDirectories(paths.BackupRoot).Length : 0;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static string Num(long v) => v.ToString(Inv);

    private static string Rel(string root, string text)
    {
        var full = IOPath.GetFullPath(root).TrimEnd(IOPath.DirectorySeparatorChar);
        return text.Replace(full, "<root>").Replace(full.Replace('\\', '/'), "<root>")
                   .Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
    }

    private static IEnumerable<string> Lines(StringWriter body) =>
        body.ToString().Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static void Row(TextWriter w, params string[] cells) =>
        w.Write(string.Join("\t", cells) + "\n");
}
