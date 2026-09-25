using System.Globalization;
using System.Runtime.Versioning;
using TH09.Record;

using IOPath = System.IO.Path;

namespace TH09.Drive;

[SupportedOSPlatform("windows")]
internal static class ScanBackupDump
{
    public const string Flag = "--dump-scan-backup";

    private const string ModePlan = "plan";

    private const string ModeRun = "run";

    private const string ModeRunYes = "run-yes";

    private const string ModeRunForce = "run-force";

    public static int Run(TextWriter w, string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine(
                Flag + " <偽リポジトリの根> <日時 yyyyMMdd_HHmmss> <"
                + ModePlan + "|" + ModeRun + "|" + ModeRunYes + "|" + ModeRunForce
                + "> の形で呼んでください。");
            return 2;
        }
        var root = args[0];
        if (!DateTime.TryParseExact(args[1], ScanBackup.StampFormat, Inv, DateTimeStyles.None,
                                    out var now))
        {
            Console.Error.WriteLine("日時が読めません（" + ScanBackup.StampFormat + "）: " + args[1]);
            return 2;
        }
        if (args[2] != ModePlan && args[2] != ModeRun && args[2] != ModeRunYes
            && args[2] != ModeRunForce)
        {
            Console.Error.WriteLine("知らないモードです（" + ModePlan + " / " + ModeRun
                                    + " / " + ModeRunYes + " / " + ModeRunForce + "）: "
                                    + args[2]);
            return 2;
        }

        var paths = Paths.Resolve(root);
        Row(w, "fact", "root", root);
        Row(w, "fact", "mode", paths.Mode.ToString());
        Row(w, "fact", "main_db", paths.MainDb);
        Row(w, "fact", "layer0_db", paths.Layer0Db);
        Row(w, "fact", "backup_root", paths.BackupRoot);
        Row(w, "fact", "manifest", ScanBackup.ManifestName);
        Row(w, "fact", "prefix", ScanBackup.Prefix);
        Row(w, "fact", "stamp_format", ScanBackup.StampFormat);
        Row(w, "fact", "drop_old_backups_flag", ScanBackup.DropOldBackupsFlag);
        Row(w, "fact", "keep_old_backups_flag", ScanBackup.KeepOldBackupsFlag);
        Row(w, "fact", "stale_grace_minutes", Num(ScanBackup.StaleGraceMinutes));

        List<string> replays = [];
        using (var db = RecordDb.OpenReadOnly(paths.MainDb))
        {
            if (db is not null) replays = Repository.CurrentReplayFiles(db.Connection);
        }
        Row(w, "fact", "replay_rows", Num(replays.Count));

        var confirmed = args[2] is ModeRunYes or ModeRunForce;
        var plan = ScanBackup.Plan(new ScanBackup.Options(
            paths, replays, now,
            DropOldBackups: confirmed, RemovalForced: args[2] == ModeRunForce));
        Row(w, "plan", "removal_confirmed", plan.RemovalConfirmed ? "1" : "0");
        Row(w, "plan", "removal_forced", plan.RemovalForced ? "1" : "0");
        Row(w, "plan", "root", Rel(root, plan.Root));
        foreach (var s in plan.Sources)
        {
            Row(w, "source", s.Label, Rel(root, s.Base), Rel(root, s.Destination),
                Num(s.Files.Count), Num(s.Bytes));
            foreach (var f in s.Files) Row(w, "file", s.Label, Rel(root, f));
        }
        Row(w, "plan", "bytes", Num(plan.Bytes));
        Row(w, "plan", "destination", Rel(root, plan.Destination),
            plan.DestinationExists ? "1" : "0");
        Row(w, "plan", "blocked",
            ScanBackup.BlockedByExistingLayer0(paths) is null ? "0" : "1");
        foreach (var r in plan.Removing)
            Row(w, "removing", Rel(root, r.Path), Num(r.Bytes), Num(r.Files));
        foreach (var r in plan.Stale)
            Row(w, "stale", Rel(root, r.Path), Num(r.Bytes), Num(r.Files));
        foreach (var line in plan.Kept) Row(w, "kept", Rel(root, line));

        var body = new StringWriter();
        try
        {
            if (args[2] != ModePlan) ScanBackup.Run(body, plan);
            else ScanBackup.Describe(body, plan);
        }
        catch (Exception exc)
        {
            Row(w, "raised", exc.GetType().Name, exc.Message.Replace("\n", "\\n"));
        }
        foreach (var line in Lines(body)) Row(w, "out", Rel(root, line));

        if (Directory.Exists(plan.Root))
        {
            foreach (var f in Directory.EnumerateFiles(plan.Root, "*", SearchOption.AllDirectories)
                                       .OrderBy(x => x, StringComparer.Ordinal))
            {
                Row(w, "made", Rel(plan.Root, f), Num(new FileInfo(f).Length));
            }
        }
        if (Directory.Exists(paths.BackupRoot))
        {
            foreach (var e in Directory.EnumerateFileSystemEntries(paths.BackupRoot)
                                       .OrderBy(x => x, StringComparer.Ordinal))
            {
                Row(w, "left", Rel(root, e), Directory.Exists(e) ? "dir" : "file");
            }
        }
        var dir = IOPath.GetDirectoryName(IOPath.GetFullPath(paths.Layer0Db));
        if (dir is not null && Directory.Exists(dir))
        {
            foreach (var f in Directory.EnumerateFiles(dir).OrderBy(x => x, StringComparer.Ordinal))
                Row(w, "layer0dir", Rel(root, f));
        }
        Row(w, "end", args[2]);
        return 0;
    }


    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

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

    private static string Num(long value) => value.ToString(Inv);

    private static void Row(TextWriter w, params string[] cells) =>
        w.Write(string.Join("\t", cells) + "\n");
}
