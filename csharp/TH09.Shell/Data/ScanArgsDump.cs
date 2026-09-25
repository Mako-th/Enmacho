using System.Text;
using TH09.Launch;

namespace TH09.Shell.Data;

internal static class ScanArgsDump
{
    public const string Flag = "--dump-scan-args";

    public static int Run()
    {
        using var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
        try
        {
            (string Name, LaunchKind Kind, ScanSettings? Scan)[] accepted =
            [
                ("monitor", LaunchKind.Monitor, null),
                ("watch", LaunchKind.Watch, null),
                ("import-only", LaunchKind.ImportOnly, null),
                ("default", LaunchKind.Scan, ScanSettings.Default),
                ("plan", LaunchKind.Scan, ScanSettings.Default with { DryRun = true }),
                ("rescan", LaunchKind.Scan, ScanSettings.Default with { Run = ScanRun.Rescan }),
                ("reset-scans", LaunchKind.Scan, ScanSettings.Default with { Run = ScanRun.ResetScans }),
                ("reset-all", LaunchKind.Scan, ScanSettings.Default with { Run = ScanRun.ResetAll }),
                ("filters", LaunchKind.Scan, new ScanSettings
                {
                    Run = ScanRun.Rescan,
                    Modes = [ScanMode.Match],
                    Difficulties = [3, 2],
                    MatchKinds = [ScanMatchKind.CpuVsCpu, ScanMatchKind.HumanVsHuman],
                    Characters = [1, 13],
                    OwnOnly = true,
                    MinStages = 3,
                    Directories = [@"C:\th09\replay", @"D:\別のフォルダ"],
                    NoRecurse = true,
                    Order = ScanOrder.Size,
                    MaxCount = 5,
                    MaxMinutes = 90.5,
                    MinRecordVersion = 13,
                    Backup = false,
                    DropOldBackups = true,
                }),
                ("hit-window", LaunchKind.Scan, ScanSettings.Default with
                {
                    HitWindows = true,
                    HitWindowBefore = 180,
                    HitWindowAfter = 90,
                    HitWindowQuick = false,
                    HitWindowScopes = new Dictionary<ScanScope, bool>
                    {
                        [ScanScope.Net] = false,
                        [ScanScope.Cpu] = true,
                        [ScanScope.Story] = true,
                    },
                }),
                ("hit-window-default", LaunchKind.Scan, ScanSettings.Default),
                ("mtime", LaunchKind.Scan, ScanSettings.Default with { Order = ScanOrder.Mtime }),
                ("story", LaunchKind.Scan, ScanSettings.Default with { Modes = [ScanMode.Story] }),
                ("extra", LaunchKind.Scan, ScanSettings.Default with { Modes = [ScanMode.Extra] }),
                ("modes", LaunchKind.Scan,
                 ScanSettings.Default with { Modes = [ScanMode.Story, ScanMode.Match] }),
            ];

            (string Name, LaunchKind Kind, ScanSettings? Scan)[] rejected =
            [
                ("dir-flag", LaunchKind.Scan, ScanSettings.Default with { Directories = ["--db"] }),
                ("dir-dash", LaunchKind.Scan, ScanSettings.Default with { Directories = ["-x"] }),
                ("dir-blank", LaunchKind.Scan, ScanSettings.Default with { Directories = ["   "] }),
                ("dir-control", LaunchKind.Scan,
                 ScanSettings.Default with { Directories = ["C:\\a\tb"] }),
                ("scan-on-monitor", LaunchKind.Monitor, ScanSettings.Default),
                ("scan-on-watch", LaunchKind.Watch, ScanSettings.Default),
                ("scan-on-import", LaunchKind.ImportOnly, ScanSettings.Default),
                ("scan-without-settings", LaunchKind.Scan, null),
                ("max-zero", LaunchKind.Scan, ScanSettings.Default with { MaxCount = 0 }),
                ("minutes-zero", LaunchKind.Scan, ScanSettings.Default with { MaxMinutes = 0 }),
                ("min-stages-negative", LaunchKind.Scan, ScanSettings.Default with { MinStages = -1 }),
                ("version-negative", LaunchKind.Scan,
                 ScanSettings.Default with { MinRecordVersion = -1 }),
                ("hit-before-zero", LaunchKind.Scan,
                 ScanSettings.Default with { HitWindowBefore = 0 }),
                ("hit-after-negative", LaunchKind.Scan,
                 ScanSettings.Default with { HitWindowAfter = -1 }),
                ("hit-scope-undefined", LaunchKind.Scan, ScanSettings.Default with
                {
                    HitWindowScopes = new Dictionary<ScanScope, bool> { [(ScanScope)9] = true },
                }),
                ("difficulty-negative", LaunchKind.Scan,
                 ScanSettings.Default with { Difficulties = [-1] }),
                ("character-negative", LaunchKind.Scan,
                 ScanSettings.Default with { Characters = [-1] }),
                ("run-undefined", LaunchKind.Scan, ScanSettings.Default with { Run = (ScanRun)9 }),
                ("order-undefined", LaunchKind.Scan, ScanSettings.Default with { Order = (ScanOrder)9 }),
                ("mode-undefined", LaunchKind.Scan, ScanSettings.Default with { Modes = [(ScanMode)9] }),
                ("match-kind-undefined", LaunchKind.Scan,
                 ScanSettings.Default with { MatchKinds = [(ScanMatchKind)9] }),
            ];

            foreach (var (name, kind, scan) in accepted)
            {
                var argv = DriveLauncher.PreviewArguments(kind, Options(scan));
                stdout.Write("args\t" + name + "\t" + string.Join("\t", argv) + "\n");
            }
            foreach (var (name, kind, scan) in rejected)
            {
                stdout.Write("reject\t" + name + "\t" + Refused(kind, scan) + "\n");
            }
            stdout.Write("count\targs\t" + accepted.Length + "\n");
            stdout.Write("count\treject\t" + rejected.Length + "\n");
            stdout.Flush();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
    }

    private static LaunchOptions? Options(ScanSettings? scan)
        => scan is null ? null : new LaunchOptions { Scan = scan };

    private static string Refused(LaunchKind kind, ScanSettings? scan)
    {
        try
        {
            _ = DriveLauncher.PreviewArguments(kind, Options(scan));
            return "NONE\t-";
        }
        catch (ArgumentException ex)
        {
            return ex.GetType().Name + "\t" + Flat(ex.Message);
        }
    }

    private static string Flat(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text) sb.Append(char.IsControl(c) ? ' ' : c);
        return sb.ToString();
    }
}
