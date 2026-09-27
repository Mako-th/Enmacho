using System.Globalization;
using System.Runtime.Versioning;
using TH09.Record;

namespace TH09.Drive;

[SupportedOSPlatform("windows")]
internal static class ScanOneDump
{
    public const string Flag = "--dump-scan-one";

    private sealed class ScriptedFailure : Exception
    {
        public ScriptedFailure(string message) : base(message) { }
    }

    private sealed record Step(
        string Name, long ReplayId, int? Slot, double Timeout, string? Speed,
        string Status, long? SessionId, long GapCount, string? SourceKind,
        string? Error, string? EarlyMismatch, string? Throw, double Elapsed,
        IReadOnlyDictionary<string, long?> Health);

    private sealed record Line(string Kind, Step? Step, long ReplayId, long SessionId);

    private const string KindItem = "item";

    private const string KindRestore = "restore";

    private const string KindVerify = "verify";

    public static int Run(TextWriter w, string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length >= 1 && string.Equals(args[0], CapturePartsDump.SubMode,
                                              StringComparison.Ordinal))
        {
            return CapturePartsDump.Run(w, args[1..]);
        }
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
        var layer0Db = Opt(args[5]);
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
        Row(w, "fact", "job_note", ScanOne.JobNote);
        Row(w, "fact", "link_method", ScanLink.Method);
        Row(w, "fact", "link_confidence", ScanLink.ConfidenceText);
        Row(w, "fact", "paths_game_replay_dir", Paths.Default.GameReplayDir() ?? "");
        Row(w, "fact", "paths_replay_extra_dirs",
            string.Join("|", Paths.Default.ReplayExtraDirs()));
        Row(w, "fact", "attach_delay_sec",
            Paths.Default.TickHookAttachDelaySec.ToString("R", Inv));
        Row(w, "fact", "attach_delay_from_config",
            Paths.Default.TickHookAttachDelayFromConfig ? "1" : "0");

        var lines = ReadScript(scriptPath);
        Row(w, "fact", "steps", Num(lines.Count));

        var windows = new HitWindows(null, Quiet);

        var ran = 0;
        foreach (var line in lines)
        {
            ran++;
            var s = line.Step;
            if (s is null)
            {
                Row(w, "step", Num(ran), line.Kind + "-" + Num(line.ReplayId));
            }
            else
            {
                Row(w, ["step", Num(ran), s.Name, Num(s.ReplayId),
                        s.Slot is int n ? Num(n) : "-", s.Timeout.ToString("R", Inv),
                        s.Speed ?? "-", s.Status, s.SessionId is long v ? Num(v) : "-",
                        Num(s.GapCount), s.SourceKind ?? "-", s.Error ?? "-",
                        s.EarlyMismatch ?? "-", s.Throw ?? "-"]);
            }

            var seen = new List<string>();
            var body = new StringWriter();
            void Log(string text) => body.Write("[log] " + text + "\n");
            try
            {
                if (string.Equals(line.Kind, KindRestore, StringComparison.Ordinal))
                {
                    Row(w, "rc", Num(ScanOne.RestoreSlots(body, replayDir, Log)));
                }
                else if (string.Equals(line.Kind, KindVerify, StringComparison.Ordinal))
                {
                    using var db = RecordDb.OpenReadOnly(dbPath);
                    if (db is null)
                    {
                        Console.Error.WriteLine("本体 DB を読み取り専用で開けません: " + dbPath);
                        return 3;
                    }
                    Row(w, "rc", Num(ScanOne.Verify(body, db.Connection,
                                                    line.ReplayId, line.SessionId)));
                }
                else
                {
                    var r = ScanOne.Run(new ScanOne.Options(
                        DbPath: dbPath,
                        ReplayId: s!.ReplayId,
                        Layer0Db: layer0Db,
                        Slot: s.Slot,
                        Auto: false,
                        Timeout: s.Timeout,
                        Speed: s.Speed,
                        Capture: req => Scripted(s, req, seen),
                        Windows: windows,
                        ReplayDir: replayDir,
                        SpareLocks: locks,
                        Out: body,
                        Log: Log));
                    Row(w, "rc", Num(r.ExitCode), Num(r.JobId), Num(r.ItemId),
                        r.VerifyStatus ?? "-", r.Linked ? "1" : "0", r.Problem ?? "-");
                    Row(w, "superseded",
                        r.Superseded is null ? "-" : ScanLink.JoinSorted(r.Superseded.Delete),
                        r.Superseded is null ? "-" : ScanLink.JoinSorted(r.Superseded.Keep));
                }
            }
            catch (Exception exc)
            {
                Row(w, "raised", exc.GetType().Name, exc.Message.Replace("\n", "\\n"));
            }
            foreach (var text in seen) Row(w, "capreq", text);
            foreach (var text in Lines(body)) Row(w, "out", text);
        }
        Row(w, "end", Num(ran));
        return 0;
    }

    private static CaptureResult Scripted(Step s, CaptureRequest req, List<string> seen)
    {
        seen.Add("slot=" + Num(s.Slot ?? req.Slot) + " req_slot=" + Num(req.Slot)
                 + " timeout=" + req.Timeout.ToString("R", Inv)
                 + " attach=" + req.AttachDelaySec.ToString("R", Inv)
                 + " announce=" + (req.Announce ? "1" : "0")
                 + " should_stop=" + (req.ShouldStop is null ? "-" : "yes")
                 + " source=" + (req.Windows.ReplaySource ?? "-"));
        if (s.Throw is { } spec)
        {
            var (kind, message) = SplitOnce(spec);
            throw kind switch
            {
                "setup" => new ScanSetupFailed(message),
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


    private static List<Line> ReadScript(string path)
    {
        var steps = new List<Line>();
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var f = line.Split('\t');
            if (f.Length == 1 && string.Equals(f[0], KindRestore, StringComparison.Ordinal))
            {
                steps.Add(new Line(KindRestore, null, 0, 0));
                continue;
            }
            if (f.Length == 3 && string.Equals(f[0], KindVerify, StringComparison.Ordinal))
            {
                steps.Add(new Line(KindVerify, null, long.Parse(f[1], Inv),
                                   long.Parse(f[2], Inv)));
                continue;
            }
            if (f.Length != 15 || !string.Equals(f[0], KindItem, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "台本の行が読めません（item ＋ 14 列 / restore / verify ＋ 2 列のはず。実際は "
                    + f[0] + " ＋ " + Num(f.Length - 1) + " 列）: " + line);
            }
            steps.Add(new Line(KindItem, new Step(
                Name: f[1],
                ReplayId: long.Parse(f[2], Inv),
                Slot: f[3] == "-" ? null : int.Parse(f[3], Inv),
                Timeout: double.Parse(f[4], NumberStyles.Float, Inv),
                Speed: Opt(f[5]),
                Status: f[6],
                SessionId: f[7] == "-" ? null : long.Parse(f[7], Inv),
                GapCount: long.Parse(f[8], Inv),
                SourceKind: Opt(f[9]),
                Error: Opt(f[10]),
                EarlyMismatch: Opt(f[11]),
                Throw: Opt(f[12]),
                Elapsed: double.Parse(f[13], NumberStyles.Float, Inv),
                Health: ParseHealth(f[14])), 0, 0));
        }
        return steps;
    }

    private static Dictionary<string, long?> ParseHealth(string text)
    {
        var map = new Dictionary<string, long?>(StringComparer.Ordinal);
        if (text == "-") return map;
        foreach (var pair in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var i = pair.IndexOf('=', StringComparison.Ordinal);
            if (i < 0) throw new InvalidDataException("健康値の書き方が読めません: " + pair);
            var key = pair[..i];
            var value = pair[(i + 1)..];
            map[key] = value == "null" ? null : long.Parse(value, Inv);
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

    private static IEnumerable<string> Lines(StringWriter body)
    {
        var text = body.ToString().Replace("\r\n", "\n");
        var lines = text.Split('\n');
        var last = lines.Length;
        while (last > 0 && lines[last - 1].Length == 0) last--;
        for (var i = 0; i < last; i++) yield return lines[i];
    }

    private static string Num(long value) => value.ToString(Inv);

    private static void Row(TextWriter w, params string[] cells) =>
        w.Write(string.Join("\t", cells) + "\n");
}
