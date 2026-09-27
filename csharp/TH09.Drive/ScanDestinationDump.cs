using System.Runtime.Versioning;
using TH09.Record;

using IOPath = System.IO.Path;

namespace TH09.Drive;

[SupportedOSPlatform("windows")]
internal static class ScanDestinationDump
{
    public const string Flag = "--dump-scan-destinations";

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
        var paths = Paths.Resolve(root);
        ScanArgs a;
        try
        {
            a = ScanCommand.Resolve(ScanCommand.Parse(args[1..], paths), paths);
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

        Row(w, "fact", "root", Rel(root, IOPath.GetFullPath(root)));
        Row(w, "fact", "main_db", Rel(root, paths.MainDb));
        Row(w, "fact", "layer0_db", Rel(root, paths.Layer0Db));
        Row(w, "fact", "backup_root", Rel(root, paths.BackupRoot));

        var backupOptions = ScanCommand.BackupOptions(a, replayFiles: [], paths);
        var backupPlan = ScanBackup.Plan(backupOptions);
        Row(w, "backup", "options_paths_backup_root",
            backupOptions.Paths is { } bp ? Rel(root, bp.BackupRoot) : "(null)");
        Row(w, "backup", "plan_root", Rel(root, backupPlan.Root));

        var captureOptions = ScanCommand.CaptureOptions(a, w, _ => { }, paths);
        Row(w, "capture", "options_paths_main_db",
            captureOptions.Paths is { } cp ? Rel(root, cp.MainDb) : "(null)");
        Row(w, "capture", "options_paths_layer0_db",
            captureOptions.Paths is { } cp2 ? Rel(root, cp2.Layer0Db) : "(null)");
        Row(w, "capture", "options_paths_backup_root",
            captureOptions.Paths is { } cp3 ? Rel(root, cp3.BackupRoot) : "(null)");
        return 0;
    }

    private static string Rel(string root, string text)
    {
        var full = IOPath.GetFullPath(root).TrimEnd(IOPath.DirectorySeparatorChar);
        return text.Replace(full, "<root>").Replace(full.Replace('\\', '/'), "<root>")
                   .Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
    }

    private static void Row(TextWriter w, params string[] cells) =>
        w.Write(string.Join("\t", cells) + "\n");
}
