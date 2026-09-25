using System.Globalization;
using System.Runtime.Versioning;
using TH09.Record;
using TH09.Replay;

namespace TH09.Drive;

[SupportedOSPlatform("windows")]
public static class ReplayWatchDump
{
    public const string Flag = "--dump-watch-loop";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(TextWriter w, string[] args)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(args);
        string? db = null, script = null, tree = null, root = null;
        var dirs = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var more = i + 1 < args.Length;
            switch (args[i])
            {
                case "--db" when more: db = args[++i]; break;
                case "--script" when more: script = args[++i]; break;
                case "--tree" when more: tree = args[++i]; break;
                case "--root" when more: root = args[++i]; break;
                case "--dir" when more: dirs.Add(args[++i]); break;
                default:
                    Console.Error.WriteLine("引数が分かりません: " + args[i]);
                    return 1;
            }
        }
        if (db is null || script is null || tree is null || dirs.Count == 0)
        {
            Console.Error.WriteLine(
                "使い方: " + Flag + " --db <DB> --script <台本> --tree <触ってよい根> --dir <監視先> …");
            return 1;
        }
        if (!File.Exists(script))
        {
            Console.Error.WriteLine("台本がありません: " + script);
            return 1;
        }

        var lines = new List<string[]>();
        foreach (var raw in File.ReadAllLines(script))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith('#')) continue;
            lines.Add(line.Split('\t'));
        }

        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(tree));
        if (InDataRoot(full))
        {
            Console.Error.WriteLine(
                "★本物のデータの根の下は受け付けません（一時フォルダを渡してください）: " + full);
            return 3;
        }
        var outside = new List<string>();
        foreach (var path in new[] { db }.Concat(dirs).Concat(ScriptPaths(lines)))
        {
            if (!Under(full, path)) outside.Add(path);
        }
        if (outside.Count > 0)
        {
            Console.Error.WriteLine(
                "★--tree の外は触りません（" + outside.Count + " 件）: " + outside[0]);
            return 3;
        }

        string? clock = null;
        foreach (var cells in lines)
        {
            if (cells.Length >= 2 && cells[0] == "now") { clock = cells[1]; break; }
        }
        var fixedClock = clock is not null;

        var paths = root is null ? Paths.Default : Paths.Resolve(root);
        var own = paths.OwnNames;
        var names = ReplayOwner.OwnNames(own.PlayerName, own.Names);
        var (ignoreCase, partial) = ReplayOwner.OwnMatchOptions(own.IgnoreCase, own.Partial);

        Row(w, "fact", "db", Path.GetFullPath(db));
        Row(w, "fact", "tree", full);
        Row(w, "fact", "data_root", paths.DataRoot);
        Row(w, "fact", "real_data_root", Paths.Default.DataRoot);
        Row(w, "fact", "config_loaded", paths.ConfigLoaded ? "1" : "0");
        Row(w, "fact", "clock", clock ?? "(default)");
        Row(w, "fact", "ignore_case", ignoreCase ? "1" : "0");
        Row(w, "fact", "partial", partial ? "1" : "0");
        Row(w, "fact", "own_names", Num(names.Count));
        for (var i = 0; i < names.Count; i++) Row(w, "ownname", Num(i), names[i]);
        for (var i = 0; i < dirs.Count; i++) Row(w, "dir", Num(i), dirs[i]);

        var step = 0;
        var scanning = false;
        var signalReads = 0;
        Func<string>? nowFn = fixedClock ? () => clock! : null;
        using var registrar = ReplayRegistrar.Open(db, nowFn);
        var loop = new ReplayWatchLoop(
            registrar,
            [.. dirs],
            names, ignoreCase, partial,
            scanIsActive: () => { signalReads++; return scanning; },
            log: line => Row(w, "log", Num(step), line));

        var pos = 0;
        var cycles = 0;
        int? abort = null;

        bool Do(string[] cells)
        {
            switch (cells[0])
            {
                case "now":
                    clock = cells[1];
                    return true;
                case "scan":
                    scanning = cells[1] == "1";
                    return true;
                case "baseline":
                    Row(w, "baseline", Num(step), "known=" + Num(loop.Baseline()));
                    return true;
                case "import":
                    Row(w, "import", Num(step), "registered=" + Num(loop.ImportExisting()));
                    return true;
                case "sweepmoved":
                    Row(w, "sweepmoved", Num(step), "swept=" + Num(loop.SweepMoved()));
                    return true;
                case "copy":
                    File.Copy(cells[1], cells[2], overwrite: true);
                    SetStamp(cells[2], cells[3]);
                    return true;
                case "touch":
                    SetStamp(cells[1], cells[2]);
                    return true;
                case "rm":
                    File.Delete(cells[1]);
                    return true;
                case "mkdir":
                    Directory.CreateDirectory(cells[1]);
                    return true;
                default:
                    Console.Error.WriteLine("知らない手です（" + step + " 行目）: " + cells[0]);
                    abort = 1;
                    return false;
            }
        }

        bool Advance()
        {
            while (pos < lines.Count)
            {
                var cells = lines[pos++];
                step = pos;
                Row(w, "step", Num(step), cells[0]);
                if (cells[0] == "cycle") return true;
                try
                {
                    if (!Do(cells)) return false;
                }
                catch (Exception exc)
                {
                    Row(w, "raised", Num(step), exc.GetType().Name,
                        exc.Message.Replace("\n", "\\n", StringComparison.Ordinal));
                }
            }
            return false;
        }

        void Emit(ReplayWatchCycle c)
        {
            cycles++;
            Row(w, "cycle", Num(cycles),
                "scanning=" + (c.Scanning ? "1" : "0"),
                "seen=" + Num(c.Seen),
                "registered=" + Num(c.Registered),
                "held=" + Num(c.Held),
                "skipped=" + Num(c.SkippedSlots),
                "missing=" + Num(c.Missing),
                "swept=" + Num(c.Swept),
                "known=" + Num(loop.KnownCount));
        }

        if (Advance()) loop.Run(Advance, Emit);
        if (abort is not null) return abort.Value;
        Row(w, "fact", "signal_reads", Num(signalReads));
        Row(w, "fact", "cycles", Num(cycles));
        Row(w, "end", Num(step));
        return 0;
    }

    private static IEnumerable<string> ScriptPaths(IReadOnlyList<string[]> lines)
    {
        foreach (var cells in lines)
        {
            if (cells[0] == "copy" && cells.Length >= 3)
            {
                yield return cells[1];
                yield return cells[2];
            }
            else if (cells.Length >= 2
                     && (cells[0] == "touch" || cells[0] == "rm" || cells[0] == "mkdir"))
            {
                yield return cells[1];
            }
        }
    }

    private static bool Under(string root, string path)
    {
        var target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (string.Equals(target, root, StringComparison.OrdinalIgnoreCase)) return true;
        return target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool InDataRoot(string path)
    {
        var data = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Paths.Default.DataRoot));
        return data.Length > 0 && Under(data, path);
    }

    private static void SetStamp(string path, string iso) =>
        File.SetLastWriteTime(path,
            DateTimeOffset.Parse(iso, Inv, DateTimeStyles.RoundtripKind).LocalDateTime);

    private static string Num(long value) => value.ToString(Inv);

    private static void Row(TextWriter w, params string[] cells) =>
        w.Write(string.Join("\t", cells) + "\n");
}
