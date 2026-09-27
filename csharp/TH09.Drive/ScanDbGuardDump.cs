using System.Globalization;
using System.Runtime.Versioning;
using TH09.Record;

using IOPath = System.IO.Path;

namespace TH09.Drive;

[SupportedOSPlatform("windows")]
internal static class ScanDbGuardDump
{
    public const string Flag = "--dump-scan-db-guard";

    public static int Run(TextWriter w, string[] args)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length < 1)
        {
            Console.Error.WriteLine(Flag + " <偽リポジトリの根> [走査の旗…] の形で呼んでください。");
            return 2;
        }
        var root = args[0];
        var scanArgs = args[1..];
        var paths = Paths.Resolve(root);
        Row(w, "fact", "main_db", Rel(root, paths.MainDb));
        Row(w, "fact", "layer0_db", Rel(root, paths.Layer0Db));

        ScanArgs a;
        try
        {
            a = ScanCommand.Resolve(ScanCommand.Parse(scanArgs, paths), paths);
        }
        catch (ScanUsageError exc)
        {
            Row(w, "raised", "ScanUsageError", exc.Message);
            return 2;
        }
        catch (ScanSetupFailed exc)
        {
            Row(w, "raised", "ScanSetupFailed", exc.Message);
            return 1;
        }
        Row(w, "fact", "db", Rel(root, a.Db));

        Row(w, "before", "db_exists", File.Exists(a.Db) ? "1" : "0");
        Row(w, "before", "layer0_exists", File.Exists(paths.Layer0Db) ? "1" : "0");
        Row(w, "before", "backup_dirs", Num(BackupDirCount(paths)));

        var body = new StringWriter();
        int exitCode;
        Exception? raised = null;
        try
        {
            exitCode = ScanCommand.Run(body, a, log: _ => { }, paths: paths);
        }
        catch (Exception exc)
        {
            raised = exc;
            exitCode = -1;
        }

        Row(w, "result", "exit_code", Num(exitCode));
        Row(w, "result", "raised", raised is null ? "-" : raised.GetType().Name + ": " + raised.Message);
        foreach (var line in Lines(body)) Row(w, "out", Rel(root, line));
        Row(w, "after", "db_exists", File.Exists(a.Db) ? "1" : "0");
        Row(w, "after", "layer0_exists", File.Exists(paths.Layer0Db) ? "1" : "0");
        Row(w, "after", "backup_dirs", Num(BackupDirCount(paths)));
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
