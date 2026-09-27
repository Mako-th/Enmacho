using System.Globalization;
using System.Runtime.Versioning;
using TH09.Layer0;
using TH09.Record;

using IOPath = System.IO.Path;

namespace TH09.Drive;

[SupportedOSPlatform("windows")]
internal static class CapturePartsDump
{
    public const string SubMode = "parts";

    private const string WithBackup = "backup";

    private const string NoBackup = "no-backup";

    private const string KeepOld = "keep";

    private const string DropOld = "drop";

    public const int FakePid = 4242;

    private sealed record Script(ScanOutcome Outcome, string Build, bool Emptying = true,
                                 string Live = LiveAsIs);

    private const string LiveAsIs = "asis";

    private const string LiveRemove = "remove";

    private const string LiveStamped = "stamped";

    private const string BuildWriter = "writer";

    private const string BuildPlain = "plain";

    private const string BuildNone = "none";

    private const string PlainBytes = "SCANNED-LAYER0-BYTES";

    private static readonly Dictionary<string, Script> Scripts = new(StringComparer.Ordinal)
    {
        ["done"] = new(new(Ok: 4, Mismatch: 1, Empty: 0, Failed: 0, Ran: 5, Total: 5,
                           StoppedBy: null), BuildWriter),
        ["swap"] = new(new(Ok: 4, Mismatch: 1, Empty: 0, Failed: 0, Ran: 5, Total: 5,
                           StoppedBy: null), BuildPlain),
        ["swapdrop"] = new(new(Ok: 2, Mismatch: 0, Empty: 0, Failed: 0, Ran: 2, Total: 2,
                               StoppedBy: null), BuildPlain),
        ["stop"] = new(new(Ok: 3, Mismatch: 0, Empty: 0, Failed: 0, Ran: 3, Total: 5,
                           StoppedBy: "Ctrl+C"), BuildPlain),
        ["fail"] = new(new(Ok: 0, Mismatch: 0, Empty: 0, Failed: 5, Ran: 5, Total: 5,
                           StoppedBy: null), BuildPlain),
        ["none"] = new(new(Ok: 0, Mismatch: 0, Empty: 0, Failed: 0, Ran: 0, Total: 0,
                           StoppedBy: null), BuildPlain),
        ["nobuild"] = new(new(Ok: 2, Mismatch: 0, Empty: 0, Failed: 0, Ran: 2, Total: 2,
                              StoppedBy: null), BuildNone),
        ["nocopy"] = new(new(Ok: 2, Mismatch: 0, Empty: 0, Failed: 0, Ran: 2, Total: 2,
                             StoppedBy: null), BuildPlain),
        ["taken"] = new(new(Ok: 2, Mismatch: 0, Empty: 0, Failed: 0, Ran: 2, Total: 2,
                            StoppedBy: null), BuildPlain),
        ["addnew"] = new(new(Ok: 4, Mismatch: 1, Empty: 0, Failed: 0, Ran: 5, Total: 5,
                             StoppedBy: null), BuildWriter, Emptying: false, Live: LiveRemove),
        ["addstamped"] = new(new(Ok: 4, Mismatch: 1, Empty: 0, Failed: 0, Ran: 5, Total: 5,
                                 StoppedBy: null), BuildWriter, Emptying: false,
                             Live: LiveStamped),
        ["nostamp"] = new(new(Ok: 4, Mismatch: 1, Empty: 0, Failed: 0, Ran: 5, Total: 5,
                              StoppedBy: null), BuildWriter, Emptying: false, Live: LiveAsIs),
        ["noswap"] = new(new(Ok: 4, Mismatch: 1, Empty: 0, Failed: 0, Ran: 5, Total: 5,
                             StoppedBy: "Ctrl+C"), BuildWriter, Emptying: true),
    };

    public static int Run(TextWriter w, string[] args)
    {
        if (args.Length >= 1 && string.Equals(args[0], Layer0StampDump.SubMode,
                                              StringComparison.Ordinal))
        {
            return Layer0StampDump.Run(w, args[1..]);
        }
        if (args.Length >= 1 && string.Equals(args[0], Layer0RestampDump.SubMode,
                                              StringComparison.Ordinal))
        {
            return Layer0RestampDump.Run(w, args[1..]);
        }
        if (args.Length != 6)
        {
            Console.Error.WriteLine(
                ScanOneDump.Flag + " " + SubMode
                + " <偽リポジトリの根> <日時 yyyyMMdd_HHmmss> <本数> <backup|no-backup>"
                + " <" + string.Join("|", Scripts.Keys) + "> <" + KeepOld + "|" + DropOld + ">"
                + " の形で呼んでください。");
            return 2;
        }
        var root = args[0];
        if (!DateTime.TryParseExact(args[1], ScanBackup.StampFormat, Inv, DateTimeStyles.None,
                                    out var now))
        {
            Console.Error.WriteLine("日時が読めません（" + ScanBackup.StampFormat + "）: " + args[1]);
            return 2;
        }
        if (!int.TryParse(args[2], NumberStyles.Integer, Inv, out var count) || count < 1)
        {
            Console.Error.WriteLine("本数は 1 以上の整数です: " + args[2]);
            return 2;
        }
        if (args[3] != WithBackup && args[3] != NoBackup)
        {
            Console.Error.WriteLine("知らないモードです（" + WithBackup + " / " + NoBackup + "）: "
                                    + args[3]);
            return 2;
        }
        if (!Scripts.TryGetValue(args[4], out var script))
        {
            Console.Error.WriteLine("知らない走査の結果です（" + string.Join(" / ", Scripts.Keys)
                                    + "）: " + args[4]);
            return 2;
        }
        if (args[5] != KeepOld && args[5] != DropOld)
        {
            Console.Error.WriteLine("知らない指定です（" + KeepOld + " / " + DropOld + "）: "
                                    + args[5]);
            return 2;
        }
        var backup = args[3] == WithBackup;
        var drop = args[5] == DropOld;

        var paths = Paths.Resolve(root);
        Row(w, "fact", "root", Rel(root, IOPath.GetFullPath(root)));
        Row(w, "fact", "mode", paths.Mode.ToString());
        Row(w, "fact", "layer0_db", Rel(root, paths.Layer0Db));
        Row(w, "fact", "backup_root", Rel(root, paths.BackupRoot));
        Row(w, "fact", "backup", backup ? "1" : "0");
        Row(w, "fact", "drop_retired", drop ? "1" : "0");
        Row(w, "fact", "script", args[4], script.Build, script.Emptying ? "1" : "0", script.Live);
        PrepareLive(w, paths.Layer0Db, script.Live);
        Row(w, "fact", "replays", Num(count));
        Row(w, "fact", "retired_mark", Layer0Swap.RetiredMark);
        Row(w, "fact", "scanning_mark", Layer0Swap.ScanningMark);
        Row(w, "fact", "drop_old_flag", Layer0Swap.DropOldFlag);
        Row(w, "fact", "segment_ticks", Num(CaptureParts.SegmentTicks));
        Row(w, "fact", "rewind_slack_sec", LivePorts.RewindSlackSeconds.ToString("R", Inv));
        Row(w, "fact", "rewind_max_ticks", Num(LivePorts.RewindMaxTicks));
        Row(w, "fact", "policy_invariant", Num(CaptureParts.Policy.InvariantHint.Count));
        Row(w, "fact", "policy_change", Num(CaptureParts.Policy.ChangeHint.Count));
        Row(w, "before", "layer0_exists", File.Exists(paths.Layer0Db) ? "1" : "0",
            File.Exists(paths.Layer0Db) ? Num(new FileInfo(paths.Layer0Db).Length) : "-");
        foreach (var suffix in Layer0Swap.SidecarSuffixes)
        {
            Row(w, "before", "sidecar" + suffix,
                File.Exists(paths.Layer0Db + suffix) ? "1" : "0");
        }

        var trace = new List<string>();
        var options = new CaptureParts.Options(
            Paths: paths,
            Log: line => trace.Add("log:" + line),
            Now: now,
            CoordSeat: _ => { trace.Add("coord_seat"); return new CountedSeat(trace); },
            MakeWindows: log => { trace.Add("windows"); return new HitWindows(null, log); },
            FindPid: () => { trace.Add("find_pid"); return FakePid; },
            DropRetired: drop,
            Emptying: script.Emptying);

        var body = new StringWriter();
        Layer0Ready? ready = null;
        try
        {
            ready = CaptureParts.PrepareLayer0(body, options, backup, []);
        }
        catch (Exception exc)
        {
            Row(w, "raised", exc.GetType().Name, Rel(root, exc.Message));
        }
        foreach (var line in Lines(body)) Row(w, "out", Rel(root, line));
        if (ready is not null)
        {
            Row(w, "ready", "live", Rel(root, ready.Live));
            Row(w, "ready", "destination", Rel(root, ready.Destination));
            Row(w, "ready", "stamp", ready.Stamp);
            Row(w, "ready", "backup_requested", ready.BackupRequested ? "1" : "0");
            Row(w, "ready", "backup_root", ready.BackupRoot is null ? "-" : Rel(root, ready.BackupRoot));
            Row(w, "ready", "drop_retired", ready.DropRetired ? "1" : "0");
            Row(w, "ready", "emptying", ready.Emptying ? "1" : "0");
            Row(w, "ready", "appending", ready.Appending ? "1" : "0");
        }
        if (ready?.BackupRoot is { } backupRoot)
        {
            var copy = IOPath.Combine(backupRoot, ScanBackup.Layer0Dir,
                                      IOPath.GetFileName(paths.Layer0Db));
            Row(w, "backup", "layer0", File.Exists(copy) ? "1" : "0",
                File.Exists(copy) ? Num(new FileInfo(copy).Length) : "-");
            Row(w, "backup", "manifest",
                File.Exists(IOPath.Combine(backupRoot, ScanBackup.ManifestName)) ? "1" : "0");
        }
        Row(w, "during", "live_exists", File.Exists(paths.Layer0Db) ? "1" : "0",
            File.Exists(paths.Layer0Db) ? Num(new FileInfo(paths.Layer0Db).Length) : "-");
        if (ready is null)
        {
            Row(w, "count", "trace", Num(trace.Count));
            return 1;
        }

        using (var parts = CaptureParts.Open(options with
        {
            Layer0Db = ready.Destination,
            Layer0Append = ready.Appending,
        }))
        {
            Row(w, "open", "pid", Num(parts.Pid));
            Row(w, "open", "windows_enabled", parts.Windows.Enabled ? "1" : "0");
            Row(w, "open", "opened_ports", Num(parts.OpenedPorts));
            for (var i = 1; i <= (script.Build == BuildWriter ? count : 0); i++)
            {
                try
                {
                    using var writer = parts.OpenArchive();
                    Row(w, "archive", Num(i), writer is null ? "none" : "ok");
                }
                catch (Exception exc)
                {
                    Row(w, "archive", Num(i), "raised", exc.GetType().Name,
                        Rel(root, exc.Message));
                }
            }
            if (script.Build == BuildPlain) File.WriteAllText(ready.Destination, PlainBytes);
            Row(w, "after", "layer0_built", File.Exists(ready.Destination) ? "1" : "0");
            Row(w, "after", "live_exists", File.Exists(ready.Live) ? "1" : "0",
                File.Exists(ready.Live) ? Num(new FileInfo(ready.Live).Length) : "-");

            IDisposable? firstRing = null;
            HitWindows? firstWindows = null;
            for (var i = 1; i <= count; i++)
            {
                var auto = parts.AutoParts(parts.Pid, new FakeGame(), () => new FakeGame(),
                                           null, false);
                var ring = auto.CoordRing?.Invoke();
                firstRing ??= ring;
                firstWindows ??= parts.Windows;
                Row(w, "coord", Num(i), ReferenceEquals(ring, firstRing) ? "same" : "different",
                    ReferenceEquals(parts.Windows, firstWindows) ? "same" : "different");
            }
            Row(w, "count", "coord_seat", Num(trace.Count(x => x == "coord_seat")));
            Row(w, "count", "windows", Num(trace.Count(x => x == "windows")));
            Row(w, "count", "find_pid", Num(trace.Count(x => x == "find_pid")));
        }

        if (args[4] == "nocopy" && ready.BackupRoot is { } taken0)
        {
            var copy = IOPath.Combine(taken0, ScanBackup.Layer0Dir,
                                      IOPath.GetFileName(ready.Live));
            if (File.Exists(copy)) File.Delete(copy);
            Row(w, "poison", "backup_layer0_removed", Rel(root, copy));
        }
        if (args[4] == "taken")
        {
            var name = Layer0Swap.Retired(ready.Live, ready.Stamp);
            File.WriteAllText(name, "占有");
            Row(w, "poison", "retired_name_taken", Rel(root, name));
        }

        var swapBody = new StringWriter();
        var result = Layer0Swap.Finish(swapBody, ready, script.Outcome);
        foreach (var line in Lines(swapBody)) Row(w, "swapout", Rel(root, line));
        Row(w, "swap", "swapped", result.Swapped ? "1" : "0");
        Row(w, "swap", "blocked", result.Blocked is null ? "-" : Rel(root, result.Blocked));
        Row(w, "swap", "retired", result.Retired is null ? "-" : Rel(root, result.Retired),
            result.Retired is not null && File.Exists(result.Retired)
                ? Num(new FileInfo(result.Retired).Length) : "-");
        Row(w, "swap", "sidecars", Num(result.Sidecars));
        Row(w, "swap", "dropped", result.Dropped is null ? "-" : Rel(root, result.Dropped));

        Row(w, "end", "live", File.Exists(ready.Live) ? "1" : "0",
            File.Exists(ready.Live) ? Num(new FileInfo(ready.Live).Length) : "-");
        Row(w, "end", "scanning", File.Exists(ready.Destination) ? "1" : "0",
            File.Exists(ready.Destination) ? Num(new FileInfo(ready.Destination).Length) : "-");
        var dir = IOPath.GetDirectoryName(IOPath.GetFullPath(ready.Live))!;
        foreach (var file in Directory.EnumerateFiles(dir).OrderBy(x => x, StringComparer.Ordinal))
        {
            Row(w, "layer0dir", Rel(root, file), Num(new FileInfo(file).Length));
        }
        Row(w, "count", "seat_closed", Num(CountedSeat.Closed));
        Row(w, "count", "trace", Num(trace.Count));
        return 0;
    }

    private static void PrepareLive(TextWriter w, string live, string how)
    {
        if (how == LiveRemove)
        {
            foreach (var suffix in new[] { "" }.Concat(Layer0Swap.SidecarSuffixes))
            {
                if (File.Exists(live + suffix)) File.Delete(live + suffix);
            }
            Row(w, "live", how, File.Exists(live) ? "1" : "0");
            return;
        }
        if (how == LiveStamped)
        {
            foreach (var suffix in new[] { "" }.Concat(Layer0Swap.SidecarSuffixes))
            {
                if (File.Exists(live + suffix)) File.Delete(live + suffix);
            }
            using (var writer = Layer0Writer.Open(
                       CaptureParts.ArchiveOptions(live, Layer0OpenMode.Create)))
            {
                writer.Flush();
            }
            var state = Layer0Stamp.Inspect(live);
            Row(w, "live", how, state.Stamped ? "1" : "0", Num(state.StampRows),
                Num(state.Bytes));
            return;
        }
        Row(w, "live", how, File.Exists(live) ? "1" : "0",
            File.Exists(live) ? Num(new FileInfo(live).Length) : "-");
    }

    private sealed class CountedSeat : IDisposable
    {
        private readonly List<string> _trace;

        public static int Closed { get; private set; }

        public CountedSeat(List<string> trace) => _trace = trace;

        public void Dispose()
        {
            Closed++;
            _trace.Add("coord_seat_closed");
        }
    }


    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static void Row(TextWriter w, params string[] cells) =>
        w.Write(string.Join("\t", cells) + "\n");

    private static string One(string text) =>
        text.Replace("\r", "").Replace("\n", "\\n").Replace("\t", " ");

    private static string Rel(string root, string text)
    {
        var full = IOPath.GetFullPath(root).TrimEnd(IOPath.DirectorySeparatorChar);
        return One(text.Replace(full, "<root>").Replace(full.Replace('\\', '/'), "<root>"));
    }

    private static IEnumerable<string> Lines(StringWriter body) =>
        body.ToString().Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static string Num(long value) => value.ToString(Inv);
}
