using System.Runtime.Versioning;
using TH09.Record;

namespace TH09.Drive;

[SupportedOSPlatform("windows")]
internal static class ScanRunModesDump
{
    public const string Flag = "--dump-scan-run-modes";

    public static int Run(TextWriter w, string[] args)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(args);
        string? root = null;
        var own = false;
        var mode = ScanRunMode.AddNew;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--root" && i + 1 < args.Length) { root = args[++i]; }
            else if (args[i] == "--own") { own = true; }
            else if (args[i] == "--run" && i + 1 < args.Length)
            {
                try
                {
                    mode = ScanRunModes.Parse(args[++i]);
                }
                catch (ScanUsageError exc)
                {
                    Console.Error.WriteLine(exc.Message);
                    return 2;
                }
            }
            else
            {
                Console.Error.WriteLine("知らない引数です: " + args[i]);
                return 2;
            }
        }
        if (root is null)
        {
            Console.Error.WriteLine(Flag + " --root <フォルダ> の形で呼んでください。");
            return 2;
        }
        if (RealDbGuard.InConfigDir(Path.Combine(root, "config.json")))
        {
            Console.Error.WriteLine("★本物の config.json のフォルダは使えません: " + root);
            return 2;
        }
        var paths = Paths.Resolve(root);
        var plan = ScanRunModes.Plan(mode, paths);
        ScanRunModes.Describe(w, plan, new ScanFilter(Own: own), paths);
        return 0;
    }
}
