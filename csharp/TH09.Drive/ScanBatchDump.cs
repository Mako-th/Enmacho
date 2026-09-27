using System.Globalization;
using System.Runtime.Versioning;
using TH09.Record;

namespace TH09.Drive;

[SupportedOSPlatform("windows")]
internal static class ScanBatchDump
{
    public const string Flag = "--dump-scan-batch";

    private sealed class ScriptedFailure : Exception
    {
        public ScriptedFailure(string message) : base(message) { }
    }

    private sealed record Step(
        string Run, long ReplayId, string Status, long? SessionId, long GapCount, string? SourceKind,
        string? Error, string? EarlyMismatch, string? Throw, double Elapsed,
        IReadOnlyDictionary<string, long?> Health);

    private sealed record RunSpec(string Name, ScanFilter Filter, bool Rescan, int? Max,
                                  double? MaxMinutes, int Slot, double Timeout, string? Speed);

    public static int Run(TextWriter w, string[] args)
    {
        if (args.Length != 6)
        {
            Console.Error.WriteLine(
                Flag + " <本体DB> <replayフォルダ> <台本.tsv> <書き手ロック> <走査中ロック>"
                + " <Layer0DB|-> の形で呼んでください。");
            return 2;
        }
        var dbPath = args[0];
        var replayDir = args[1];
        var scriptPath = args[2];
        var locks = new SpareLockNames(args[3], args[4]);
        var layer0Db = args[5] == "-" ? null : args[5];
        if (ScanOne.RealLockNameIn(locks) is { } real)
        {
            Console.Error.WriteLine(
                "★検査用の名前しか受け付けない（本物のロックを握ると、"
                + "動いている監視・走査・watcher を実際に止めてしまう）: " + real);
            return 2;
        }

        Row(w, "fact", "main_db", dbPath);
        Row(w, "fact", "replay_dir", replayDir);
        Row(w, "fact", "layer0_db", layer0Db ?? "-");
        Row(w, "fact", "consecutive_limit", Num(ScanBatch.ConsecutiveLimit));
        Row(w, "fact", "no_targets", ScanBatch.NoTargets);
        Row(w, "fact", "speed_default", ScanCommand.SpeedDefault);
        Row(w, "fact", "timeout_default", ScanCommand.TimeoutDefault.ToString("R", Inv));
        Row(w, "fact", "timeout_no_skip", ScanCommand.TimeoutNoSkip.ToString("R", Inv));
        Row(w, "fact", "timeout_raised", ScanCommand.TimeoutRaised);
        Row(w, "fact", "slot_default", Num(Paths.Default.ReplayScanSlot));
        Row(w, "fact", "slot_from_config", Paths.Default.ReplayScanSlotFromConfig ? "1" : "0");
        Row(w, "fact", "default_db", Paths.Default.MainDb);
        Row(w, "fact", "slot_min", Num(ReplaySlots.SlotMin));
        Row(w, "fact", "slot_max", Num(ReplaySlots.SlotMax));

        var (steps, lines) = ReadScript(scriptPath);
        Row(w, "fact", "steps", Num(steps.Count));
        Row(w, "fact", "lines", Num(lines.Count));

        var windows = new HitWindows(null, Quiet);
        var ran = 0;
        foreach (var line in lines)
        {
            ran++;
            if (line.Kind == KindArgs) { DumpArgs(w, ran, line.Argv!); continue; }
            var spec = line.Run!;
            Row(w, "run", Num(ran), spec.Name, spec.Max is int mx ? Num(mx) : "-",
                spec.MaxMinutes is double mm ? mm.ToString("R", Inv) : "-",
                spec.Rescan ? "1" : "0", spec.Filter.Order,
                spec.Filter.Modes is { Count: > 0 } md ? string.Join(",", md.Select(Num)) : "-",
                Num(spec.Slot));

            var body = new StringWriter();
            void Log(string text) => body.Write("[log] " + text + "\n");
            try
            {
                var filter = ScanTargets.ForBatch(spec.Filter, spec.Rescan, spec.Max);
                List<ScanTarget> targets;
                using (var db = RecordDb.OpenReadOnly(dbPath))
                {
                    if (db is null)
                    {
                        Console.Error.WriteLine("本体 DB を読み取り専用で開けません: " + dbPath);
                        return 3;
                    }
                    targets = [.. ScanTargets.SelectTargets(db.Connection, filter, Log)];
                }
                foreach (var t in targets) Row(w, "target", Num(ran), Num(t.ReplayId));

                var result = ScanBatch.Run(new ScanBatch.Options(
                    Targets: targets,
                    RunOne: t => One(t, spec, steps, dbPath, layer0Db, replayDir, locks,
                                     windows, body, Log),
                    MaxMinutes: spec.MaxMinutes,
                    ReplayDir: replayDir,
                    Exclusive: () => ScanOne.AcquireExclusive(dbPath, locks, Log),
                    Out: body,
                    Log: Log));
                Row(w, "result", Num(ran), Num(result.ExitCode), Num(result.Ok),
                    Num(result.Mismatch), Num(result.Failed), Num(result.Empty),
                    Num(result.Skipped), Num(result.Total), Num(result.Ran),
                    result.StoppedBy ?? "-");
            }
            catch (Exception exc)
            {
                Row(w, "raised", Num(ran), exc.GetType().Name,
                    exc.Message.Replace("\n", "\\n"));
            }
            foreach (var text in Lines(body)) Row(w, "out", Num(ran), text);
        }
        Row(w, "end", Num(ran));
        return 0;
    }


    private static ScanBatchItem One(ScanTarget t, RunSpec spec,
                                     IReadOnlyDictionary<string, Step> steps, string dbPath,
                                     string? layer0Db, string replayDir, SpareLockNames locks,
                                     HitWindows windows, TextWriter body, Action<string> log)
    {
        if (!steps.TryGetValue(Key(spec.Name, t.ReplayId), out var s))
            throw new InvalidDataException("台本に " + Key(spec.Name, t.ReplayId) + " がありません。");
        var r = ScanOne.Run(new ScanOne.Options(
            DbPath: dbPath,
            ReplayId: t.ReplayId,
            Layer0Db: layer0Db,
            Slot: spec.Slot,
            Auto: false,
            Timeout: spec.Timeout,
            Speed: spec.Speed,
            Capture: _ => Scripted(s),
            Windows: windows,
            ReplayDir: replayDir,
            SpareLocks: locks,
            Out: body,
            Log: log,
            Path: t.Path));
        return new ScanBatchItem(r.ExitCode, r.Problem);
    }

    private static CaptureResult Scripted(Step s)
    {
        if (s.Throw is { } spec)
        {
            var (kind, message) = SplitOnce(spec);
            throw kind switch
            {
                "setup" => new ScanSetupFailed(message),
                "cancel" => new OperationCanceledException(message),
                _ => new ScriptedFailure(message),
            };
        }
        return new CaptureResult
        {
            Status = s.Status,
            SessionId = s.SessionId,
            GapCount = s.GapCount,
            SourceKind = s.SourceKind,
            Error = s.Error,
            EarlyMismatch = s.EarlyMismatch,
            Elapsed = s.Elapsed,
            Health = s.Health,
        };
    }


    private static void DumpArgs(TextWriter w, int no, string[] argv)
    {
        var said = new List<string>();
        try
        {
            var a = ScanCommand.Resolve(ScanCommand.Parse(argv), null, said.Add);
            Row(w, "args", Num(no), "ok", string.Join(" ", argv));
            foreach (var text in ScanCommand.Describe(a)) Row(w, "arg", Num(no), text);
            foreach (var text in said) Row(w, "argsay", Num(no), text);
        }
        catch (ScanUsageError)
        {
            Row(w, "args", Num(no), "usage", string.Join(" ", argv));
        }
        catch (ScanSetupFailed exc)
        {
            Row(w, "args", Num(no), "exit", string.Join(" ", argv));
            Row(w, "argerr", Num(no), exc.Message);
        }
    }


    private const string KindItem = "item";

    private const string KindRun = "run";

    private const string KindArgs = "args";

    private sealed record Line(string Kind, RunSpec? Run, string[]? Argv);

    private static (Dictionary<string, Step> Steps, List<Line> Lines) ReadScript(string path)
    {
        var steps = new Dictionary<string, Step>(StringComparer.Ordinal);
        var lines = new List<Line>();
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var f = line.Split('\t');
            if (string.Equals(f[0], KindItem, StringComparison.Ordinal))
            {
                if (f.Length != 12)
                    throw new InvalidDataException("item は 11 列です（実際は "
                                                   + Num(f.Length - 1) + "）: " + line);
                var rid = long.Parse(f[2], Inv);
                steps[Key(f[1], rid)] = new Step(
                    Run: f[1],
                    ReplayId: rid,
                    Status: f[3],
                    SessionId: f[4] == "-" ? null : long.Parse(f[4], Inv),
                    GapCount: long.Parse(f[5], Inv),
                    SourceKind: Opt(f[6]),
                    Error: Opt(f[7]),
                    EarlyMismatch: Opt(f[8]),
                    Throw: Opt(f[9]),
                    Elapsed: double.Parse(f[10], NumberStyles.Float, Inv),
                    Health: ParseHealth(f[11]));
                continue;
            }
            if (string.Equals(f[0], KindArgs, StringComparison.Ordinal))
            {
                lines.Add(new Line(KindArgs, null, f[1..]));
                continue;
            }
            if (string.Equals(f[0], KindRun, StringComparison.Ordinal))
            {
                if (f.Length != 3)
                    throw new InvalidDataException("run は 2 列です（名前 ＋ 設定）: " + line);
                lines.Add(new Line(KindRun, ParseRun(f[1], f[2]), null));
                continue;
            }
            throw new InvalidDataException("台本の行が読めません（item / run / args のはず）: " + line);
        }
        return (steps, lines);
    }

    private static RunSpec ParseRun(string name, string text)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var i = pair.IndexOf('=', StringComparison.Ordinal);
            if (i < 0) throw new InvalidDataException("run の設定が読めません: " + pair);
            map[pair[..i]] = pair[(i + 1)..];
        }
        string? Get(string key) => map.TryGetValue(key, out var v) ? v : null;
        foreach (var key in map.Keys)
        {
            if (!KnownRunKeys.Contains(key, StringComparer.Ordinal))
                throw new InvalidDataException("run の知らない設定です: " + key);
        }
        var filter = new ScanFilter(
            Dirs: Get("dir") is { } d ? d.Split('|', StringSplitOptions.RemoveEmptyEntries) : null,
            Modes: ScanTargets.ModesArg(Get("mode")),
            Own: Get("own") == "1",
            MinStages: Get("min_stages") is { } ms ? int.Parse(ms, Inv) : 0,
            Order: Get("order") ?? ScanTargets.OrderId,
            Chars: ScanTargets.CharsArg(Get("chars")));
        return new RunSpec(
            Name: name,
            Filter: filter,
            Rescan: Get("rescan") == "1",
            Max: Get("max") is { } mx ? int.Parse(mx, Inv) : null,
            MaxMinutes: Get("max_minutes") is { } mm
                        ? double.Parse(mm, NumberStyles.Float, Inv) : null,
            Slot: Get("slot") is { } sl ? int.Parse(sl, Inv) : Paths.Default.ReplayScanSlot,
            Timeout: Get("timeout") is { } to
                     ? double.Parse(to, NumberStyles.Float, Inv) : ScanCommand.TimeoutDefault,
            Speed: Get("speed"));
    }

    private static readonly string[] KnownRunKeys =
        ["dir", "mode", "own", "min_stages", "order", "chars", "rescan", "max", "max_minutes",
         "slot", "timeout", "speed"];

    private static Dictionary<string, long?> ParseHealth(string text)
    {
        var map = new Dictionary<string, long?>(StringComparer.Ordinal);
        if (text == "-") return map;
        foreach (var pair in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var i = pair.IndexOf('=', StringComparison.Ordinal);
            if (i < 0) throw new InvalidDataException("健康値の書き方が読めません: " + pair);
            var value = pair[(i + 1)..];
            map[pair[..i]] = value == "null" ? null : long.Parse(value, Inv);
        }
        return map;
    }


    private static readonly Action<string> Quiet = _ => { };

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static string? Opt(string field) => field == "-" ? null : field;

    private static (string Kind, string Message) SplitOnce(string text)
    {
        var i = text.IndexOf(':', StringComparison.Ordinal);
        return i < 0 ? (text, "") : (text[..i], text[(i + 1)..]);
    }

    private static List<string> Lines(StringWriter body)
    {
        var text = body.ToString().Replace("\r\n", "\n");
        var lines = text.Split('\n');
        var last = lines.Length;
        while (last > 0 && lines[last - 1].Length == 0) last--;
        return [.. lines[..last]];
    }

    private static string Key(string run, long replayId) => run + "/" + Num(replayId);

    private static string Num(long value) => value.ToString(Inv);

    private static void Row(TextWriter w, params string[] cells) =>
        w.Write(string.Join("\t", cells) + "\n");
}
