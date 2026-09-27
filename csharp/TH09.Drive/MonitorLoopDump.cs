using System.Globalization;
using System.Runtime.Versioning;
using TH09.Record;

namespace TH09.Drive;

[SupportedOSPlatform("windows")]
public static class MonitorLoopDump
{
    public const string Flag = "--dump-monitor-loop";

    public const string ScriptEnd = CaptureStatus.Interrupted;

    public const int FakePid = 4321;

    public const string MissingStep = "missing";

    public const string FailStep = "fail";

    public const string BoomStep = "boom";

    public const string ArchiveStep = "archive";

    public const string FailReason = "エラー: 台本の前提崩れ";

    public const string BoomReason = "台本の知らない例外";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(TextWriter w, string[] args)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length != 3)
        {
            Console.Error.WriteLine(
                Flag + " <偽リポジトリの根> <台本> <不在で 0 を返す回数>");
            return 2;
        }
        var root = args[0];
        var steps = args[1].Split(';', StringSplitOptions.RemoveEmptyEntries);
        if (!int.TryParse(args[2], NumberStyles.Integer, Inv, out var absentPolls)
            || absentPolls < 0)
        {
            Console.Error.WriteLine("不在で 0 を返す回数は 0 以上の整数です: " + args[2]);
            return 2;
        }
        var paths = Paths.Resolve(root);
        if (steps.Contains(ArchiveStep, StringComparer.Ordinal) && RealDbGuard.InLayer0Dir(paths.Layer0Db))
        {
            Console.Error.WriteLine("★本物の Layer 0 のフォルダは使えません: " + paths.Layer0Db);
            return 2;
        }
        var lines = new List<string>();
        var polls = 0;
        var captures = 0;
        var befores = 0;
        var layer0Dir = Path.GetDirectoryName(paths.Layer0Db) ?? "";

        Row(w, "fact", "script", string.Join(";", steps));
        Row(w, "fact", "absent_polls", Num(absentPolls));
        Row(w, "fact", "mode", paths.Mode.ToString());
        Row(w, "fact", "layer0_dir_before", Directory.Exists(layer0Dir) ? "1" : "0");

        var options = new CaptureParts.Options(
            Paths: paths,
            Log: lines.Add,
            CoordSeat: _ => new NoSeat(),
            MakeWindows: log => new HitWindows(null, log),
            FindPid: () =>
            {
                polls++;
                return polls <= absentPolls ? 0 : FakePid;
            },
            Layer0Db: paths.Layer0Db);

        CaptureParts? opened = null;

        CaptureResult Capture()
        {
            var step = captures < steps.Length ? steps[captures] : ScriptEnd;
            captures++;
            if (string.Equals(step, ArchiveStep, StringComparison.Ordinal))
            {
                using var writer = opened!.OpenArchive();
                return new CaptureResult { Status = CaptureStatus.Captured };
            }
            if (string.Equals(step, MissingStep, StringComparison.Ordinal))
                throw new ScanSetupFailed(LivePorts.GameMissing);
            if (string.Equals(step, FailStep, StringComparison.Ordinal))
                throw new ScanSetupFailed(FailReason);
            if (string.Equals(step, BoomStep, StringComparison.Ordinal))
                throw new InvalidOperationException(BoomReason);
            return new CaptureResult { Status = step };
        }

        int rc;
        using (var parts = CaptureParts.Open(options))
        {
            opened = parts;
            using var cancel = new CancellationTokenSource();
            rc = MonitorCommand.Loop(parts, Capture, lines.Add, cancel.Token, () => befores++);
        }
        Row(w, "result", "layer0_exists_after", File.Exists(paths.Layer0Db) ? "1" : "0");
        foreach (var line in lines) Row(w, "log", line);
        Row(w, "count", "capture", Num(captures));
        Row(w, "count", "before_capture", Num(befores));
        Row(w, "count", "poll", Num(polls));
        Row(w, "count", "log", Num(lines.Count));
        Row(w, "exit", Num(rc));
        return 0;
    }

    private static string Num(int value) => value.ToString(Inv);

    private static void Row(TextWriter w, params string[] cells) =>
        w.Write(string.Join("\t", cells) + ((char)10));

    private sealed class NoSeat : IDisposable
    {
        public void Dispose() { }
    }
}
