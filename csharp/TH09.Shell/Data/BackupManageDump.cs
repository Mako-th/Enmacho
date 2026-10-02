using System.Runtime.Versioning;
using System.Text;
using TH09.Record;
using TH09.Shell.ViewModels;

namespace TH09.Shell.Data;

[SupportedOSPlatform("windows")]
internal static class BackupManageDump
{
    public const string Flag = "--dump-backup-manage";

    public const string OpsVar = "TH09_BACKUP_MANAGE_OPS";

    public static int Run(string root)
    {
        var full = Path.GetFullPath(root);
        if (Paths.Default.IsUnderAppDataRoot(full))
        {
            Console.Error.WriteLine("★本物の data フォルダの下は受け付けません（偽の根を渡してください）: " + full);
            return 3;
        }
        using var w = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
        var scanRunning = false;
        var outside = false;
        using var gate = new ManualResetEventSlim(true);
        Task? background = null;
        var vm = new BackupManagerViewModel(
            () => full, scanActiveOutside: () => outside,
            removeOp: (r, p, n) =>
            {
                var removed = BackupLayout.Remove(r, p, n);
                if (!gate.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("ガードが開かなかった");
                return removed;
            })
        {
            IsScanRunning = () => scanRunning,
        };
        Row(w, "fact", "root", full);
        foreach (var bytes in new long[] { 0, 1, 1023, 1024, 1536, 1048576, 4617089843, 5497558138880 })
            Row(w, "size", bytes.ToString(), BackupLayout.Size(bytes));
        vm.Refresh();
        foreach (var op in (Environment.GetEnvironmentVariable(OpsVar) ?? "")
                     .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var i = op.IndexOf(':');
            var name = i < 0 ? op : op[..i];
            var arg = i < 0 ? "" : op[(i + 1)..];
            switch (name)
            {
                case "select":
                    var at = -1;
                    for (var k = 0; k < vm.Rows.Count; k++)
                        if (vm.Rows[k].Name == arg) at = k;
                    if (at < 0) { Console.Error.WriteLine("一覧に無い名前: " + arg); return 2; }
                    vm.SelectedIndex = at;
                    break;
                case "refresh": vm.RefreshCommand.Execute(null); break;
                case "open": vm.Refresh(); break;
                case "remove": vm.RemoveCommand.Execute(null); break;
                case "yes": vm.ConfirmRemoveCommand.ExecuteAsync(null).GetAwaiter().GetResult(); break;
                case "yesbg":
                    gate.Reset();
                    background = vm.ConfirmRemoveCommand.ExecuteAsync(null);
                    break;
                case "release":
                    gate.Set();
                    background?.GetAwaiter().GetResult();
                    background = null;
                    break;
                case "no": vm.CancelRemoveCommand.Execute(null); break;
                case "scan": scanRunning = arg == "1"; vm.NotifyScanChanged(); break;
                case "outside": outside = arg == "1"; vm.NotifyScanChanged(); break;
                case "show": Show(w, arg, vm); break;
                case "write":
                    File.WriteAllText(Path.Combine(full, arg, "late_write.tmp"), "x");
                    break;
                case "force":
                    var forced = BackupLayout.Remove(full, Path.GetFullPath(Path.Combine(full, arg)), DateTime.Now);
                    Row(w, "force", arg, forced.Removed is null ? "0" : "1", forced.Reason ?? "");
                    break;
                default:
                    Console.Error.WriteLine("知らない操作: " + op);
                    return 2;
            }
        }
        gate.Set();
        background?.GetAwaiter().GetResult();
        Row(w, "end", "");
        return 0;
    }

    private static void Show(TextWriter w, string label, BackupManagerViewModel vm)
    {
        foreach (var r in vm.Rows)
            Row(w, "row", label, r.Name, r.KindText, r.SizeText, r.Folder.Bytes.ToString(), r.Folder.Files.ToString());
        Row(w, "state", label,
            "rows=" + vm.Rows.Count,
            "selected=" + vm.SelectedIndex,
            "can=" + (vm.CanRemove ? "1" : "0"),
            "canrefresh=" + (vm.RefreshCommand.CanExecute(null) ? "1" : "0"),
            "blocked=" + vm.BlockedText,
            "confirm=" + (vm.IsConfirmOpen ? "1" : "0"),
            "confirmtext=" + vm.ConfirmText.Replace("\n", "\\n"),
            "result=" + vm.ResultText,
            "summary=" + vm.SummaryText,
            "empty=" + (vm.IsEmpty ? "1" : "0"));
    }

    private static void Row(TextWriter w, params string[] cells) =>
        w.WriteLine(string.Join('\t', cells));
}
