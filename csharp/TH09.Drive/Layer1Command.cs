using System.Globalization;
using System.Runtime.Versioning;
using TH09.Record;

namespace TH09.Drive;

[SupportedOSPlatform("windows")]
public static class Layer1Command
{
    public const string Flag = "--build-layer1";

    public static int Run(TextWriter w, string[] args)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(args);

        string? dbArg = null, layer0Arg = null, writerLock = null, scanLock = null, sessionText = null;
        var checkOnly = false;
        var rebuildAll = false;
        for (var i = 0; i < args.Length; i++)
        {
            var more = i + 1 < args.Length;
            switch (args[i])
            {
                case "--db" when more: dbArg = args[++i]; break;
                case "--layer0" when more: layer0Arg = args[++i]; break;
                case "--writer-lock" when more: writerLock = args[++i]; break;
                case "--scan-lock" when more: scanLock = args[++i]; break;
                case "--session" when more: sessionText = args[++i]; break;
                case "--check": checkOnly = true; break;
                case "--all": rebuildAll = true; break;
                default:
                    Console.Error.WriteLine("引数が分かりません: " + args[i]);
                    return 1;
            }
        }
        long? onlySession = null;
        if (sessionText is not null)
        {
            if (!long.TryParse(sessionText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
                               out var v))
            {
                Console.Error.WriteLine("--session は session_id の整数で指してください: " + sessionText);
                return 1;
            }
            onlySession = v;
        }

        var dbPath = dbArg ?? Paths.Default.MainDb;
        var layer0Path = layer0Arg ?? Paths.Default.Layer0Db;

        w.WriteLine("本体 DB : " + Path.GetFullPath(dbPath));
        w.WriteLine("Layer 0 : " + Path.GetFullPath(layer0Path));
        w.Flush();

        var hasWriterLock = writerLock is not null;
        var hasScanLock = scanLock is not null;
        if (hasWriterLock != hasScanLock)
        {
            Console.Error.WriteLine("--writer-lock と --scan-lock は両方そろえて指してください"
                                    + "（片方だけだと、もう片方が本物のロックになる）。");
            return 1;
        }
        if (hasWriterLock && RealDbGuard.InMainDbDir(dbPath))
        {
            Console.Error.WriteLine(
                "★別名のロック（＝検査の走り方）で、本物の本体 DB のフォルダは指せません: " + dbPath);
            return 1;
        }
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine("本体 DB がありません: " + dbPath);
            return 2;
        }
        if (checkOnly)
        {
            var rc = Layer1Build.RunCheck(w, dbPath, onlySession, rebuildAll);
            w.Flush();
            return rc;
        }

        void Log(string m) { w.WriteLine(m); w.Flush(); }

        var ledger = hasWriterLock
            ? ScanLedger.OpenWithSpareLocks(dbPath, writerLock!, scanLock!, Log)
            : ScanLedger.Open(dbPath, Log);
        if (ledger is null)
        {
            Console.Error.WriteLine("排他のロックを取れませんでした（監視・走査と同時には走れません）。");
            return 1;
        }
        using (ledger)
        {
            Layer1Build.Run(ledger, layer0Path, onlySession, rebuildAll, Log);
        }
        w.Flush();
        return 0;
    }

}
