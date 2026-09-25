using System.Runtime.Versioning;
using TH09.Record;
using TH09.Replay;

namespace TH09.Drive;

[SupportedOSPlatform("windows")]
public static class WatchCommand
{
    public const string Flag = "--watch";

    public const string ImportExistingFlag = "--import-existing";

    public static int Run(TextWriter w, bool importExisting, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(w);
        return WithLoop(w, Paths.Default.MainDb, Paths.Default, (loop, roots) =>
        {
            WriteRoots(w, roots);
            if (importExisting) RegisterExisting(w, loop);

            var baseline = loop.Baseline();
            w.WriteLine("既存リプレイ " + baseline + " 件を基準登録しました。既存ファイルは読み込みません。");
            loop.Run(ReplayWatchLoop.DefaultInterval, token);
            return 0;
        });
    }

    public static int ImportExisting(TextWriter w, string dbPath, Paths? paths = null)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentException.ThrowIfNullOrEmpty(dbPath);
        return WithLoop(w, dbPath, paths ?? Paths.Default, (loop, roots) =>
        {
            WriteRoots(w, roots);
            return RegisterExisting(w, loop);
        });
    }

    private static T WithLoop<T>(TextWriter w, string dbPath, Paths paths,
                                 Func<ReplayWatchLoop, IReadOnlyList<string>, T> run)
    {
        using var registrar = ReplayRegistrar.Open(dbPath);
        var roots = paths.ReplayWatchDirs();
        var own = paths.OwnNames;
        var names = ReplayOwner.OwnNames(own.PlayerName, own.Names);
        var (ignoreCase, partial) = ReplayOwner.OwnMatchOptions(own.IgnoreCase, own.Partial);
        var loop = new ReplayWatchLoop(registrar, roots, names, ignoreCase, partial,
                                       log: w.WriteLine);
        return run(loop, roots);
    }

    private static void WriteRoots(TextWriter w, IReadOnlyList<string> roots)
    {
        foreach (var root in roots) w.WriteLine("監視先: " + root);
    }

    private static int RegisterExisting(TextWriter w, ReplayWatchLoop loop)
    {
        var imported = loop.ImportExisting();
        w.WriteLine(ScanProgressLines.ImportResult(imported));
        w.WriteLine(ScanProgressLines.SweptStalePaths(loop.SweepMoved()));
        return imported;
    }
}
