using System.Globalization;
using System.Runtime.Versioning;
using Avalonia;

[assembly: SupportedOSPlatform("windows")]

namespace TH09.Shell;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; }
        catch (IOException) { }

        string exe = Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "th09_shell";

        if (args.Length >= 1 && args[0].StartsWith("--dump-", StringComparison.Ordinal))
            Data.LogSource.SuppressFileWrites = true;
        if (args.Length >= 1 && (args[0] == "--selftest" || args[0] == "--hitwindow-bench"))
        {
            Data.TestLaunchGuard.Active = true;
            Data.LogSource.SuppressFileWrites = true;
        }

        if (args.Length == 1 && args[0] == Data.DriveControlDump.Flag)
            return Data.DriveControlDump.Run();
        if (args.Length == 1 && args[0] == Data.DriveControlDump.BarFlag)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("上段の診断は Windows でだけ動きます（設定を読むため）。");
                return 3;
            }
            return Data.DriveControlDump.RunBar();
        }
        if (args.Length == 1 && args[0] == Data.DriveControlDump.BarDistFlag)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("上段の診断は Windows でだけ動きます（設定を読むため）。");
                return 3;
            }
            return Data.DriveControlDump.RunBarDist();
        }
        if (args.Length == 1 && args[0] == Data.ScanArgsDump.Flag)
            return Data.ScanArgsDump.Run();
        if (args.Length is 2 or 3 && args[0] == Data.AppSettingsFormDump.Flag)
        {
            if (args.Length == 3 && args[2] != "save")
            {
                Console.Error.WriteLine(Data.AppSettingsFormDump.Flag
                    + " の 3 つ目は save だけです: " + args[2]);
                return 2;
            }
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("設定の読み書きは Windows でだけ動きます。");
                return 3;
            }
            return Data.AppSettingsFormDump.Run(args[1], args.Length == 3);
        }
        if (args.Length >= 2 && args[0] == Data.AppSettingsFormDump.ToggleKeyFlag)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("設定の読み書きは Windows でだけ動きます。");
                return 3;
            }
            return Data.AppSettingsFormDump.RunToggleKey(args[1..]);
        }
        if (args.Length == 2 && args[0] == "--dump-replay-list")
            return Data.ReplayListDump.Run(args[1]);
        if (args.Length >= 1 && args[0] == Data.ReplayFilterDump.Flag)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("使い方: " + exe + " " + Data.ReplayFilterDump.Flag + " <db>");
                return 2;
            }
            return Data.ReplayFilterDump.Run(args[1]);
        }
        if (args.Length == 2 && args[0] == "--dump-stats-leaf")
            return Data.StatsLeafDump.Run(args[1]);
        if (args.Length >= 1 && args[0] == Data.StatsRecordsDump.Flag)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("使い方: " + exe + " " + Data.StatsRecordsDump.Flag + " <db>");
                return 2;
            }
            return Data.StatsRecordsDump.Run(args[1]);
        }
        if (args.Length == 2 && args[0] == "--dump-history")
            return Data.HistoryDump.Run(args[1]);
        if (args.Length == 2 && args[0] == Data.CharOrderDump.Flag)
            return Data.CharOrderDump.Run(args[1]);
        if (args.Length >= 1 && args[0] == Data.StatsLayoutDump.Flag)
        {
            if (args.Length > 2)
            {
                Console.Error.WriteLine("使い方: " + exe + " " + Data.StatsLayoutDump.Flag
                                        + " [config.json]");
                return 2;
            }
            return Data.StatsLayoutDump.Run(args.Length == 2 ? args[1] : null);
        }
        if (args.Length == 5 && args[0] == Data.HistoryEditDump.Flag)
            return Data.HistoryEditDump.Run(args[1], args[2], args[3], args[4]);
        if (args.Length == 2 && args[0] == Data.ReplayOwnDump.Flag)
            return Data.ReplayOwnDump.Run(args[1]);
        if (args.Length >= 1 && args[0] == Data.StreamPanelViewDump.Flag)
        {
            if (args.Length < 2
                || !Data.DumpFlags.TryRead(args[2..], out var viewNarrow, out var viewBlocks,
                                           out var viewHidden)
                || viewNarrow || viewBlocks)
            {
                Console.Error.WriteLine(Data.StreamPanelViewDump.Flag + " <db> ["
                                        + Data.DumpFlags.HideFlag + " SB,WR]");
                return 2;
            }
            return Data.StreamPanelViewDump.Run(args[1], viewHidden);
        }
        if (args.Length is 8 or 9 && args[0] == Data.StreamModeSaveDump.Flag)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("配信パネルの設定の保存は Windows でだけ動きます。");
                return 3;
            }
            if (!double.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var w)
                || !double.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var h)
                || !double.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var fs)
                || (args[6] != "0" && args[6] != "1") || (args[7] != "0" && args[7] != "1"))
            {
                Console.Error.WriteLine(Data.StreamModeSaveDump.Flag
                    + " <config> <width> <height> <fontScale> <bg> <topmost 0|1> <borderless 0|1>"
                    + " [<隠す項目 SB,WR>]");
                return 2;
            }
            return Data.StreamModeSaveDump.Run(args[1], w, h, fs, args[5], args[6] == "1",
                                               args[7] == "1",
                                               Data.DumpFlags.SplitKinds(args.Length == 9 ? args[8] : ""));
        }
        if (args.Length is 3 or 4 && args[0] == "--dump-replay-detail")
            return Data.ReplayDetailDump.Run(args[1], args[2], args.Length == 4 ? args[3] : null);
        if (args.Length == 3 && args[0] == Data.ReplayFilesDump.Flag)
            return Data.ReplayFilesDump.Run(args[1], args[2]);
        if (args.Length == 3 && args[0] == Data.RevealChoicesDump.Flag)
            return Data.RevealChoicesDump.Run(args[1], args[2]);
        if (args.Length == 2 && args[0] == Data.HistorySignatureDump.Flag)
            return Data.HistorySignatureDump.Run(args[1]);

        var unknown = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--config" && i + 1 < args.Length)
            {
                if (!OperatingSystem.IsWindows())
                {
                    Console.Error.WriteLine("--config は Windows でだけ効きます。");
                    return 2;
                }
                Data.AppSettingsSource.ReadFrom(args[i + 1]);
                i++;
                continue;
            }
            if (args[i] is "--help" or "-h" or "/?")
            {
                Console.WriteLine(exe + " — 幻想閻魔帳の画面（東方花映塚のプレイとリプレイの記録・分析）");
                Console.WriteLine();
                Console.WriteLine("  " + exe + "                      窓を開く（配布版はタブ 4 枚・開発者版は 6 枚）");
                Console.WriteLine("  " + exe + " --dump-drive-control  子の起こし方を fake だけで通す（★窓を開かない）");
                Console.WriteLine("  " + exe + " --dump-drive-bar     共通上段の配線を fake だけで通す（★窓を開かない）");
                Console.WriteLine("  " + exe + " --dump-drive-bar-dist");
                Console.WriteLine("                                  配布版と同じ4枚のタブだけを持つ器で同じ配線を通す");
                Console.WriteLine("                                  （★窓を開かない）");
                Console.WriteLine("  " + exe + " --dump-scan-args     走査の設定がどんな引数になるかを吐く（★窓を開かない・");
                Console.WriteLine("                                  ★子を起こさない）");
                Console.WriteLine("  " + exe + " --dump-settings-form <config> [save]");
                Console.WriteLine("                                  設定の器を合成の config.json だけで動かして吐く");
                Console.WriteLine("                                  （★窓を開かない・★本物の設定のフォルダは断る）");
                Console.WriteLine("  " + exe + " --dump-toggle-key <字>…");
                Console.WriteLine("                                  監視を切り替えるキーの読み替えを吐く（★窓を開かない）");
                Console.WriteLine("  " + exe + " --dump-replay-list <db>  リプレイ一覧を TSV で吐く（★窓を開かない）");
                Console.WriteLine("  " + exe + " --dump-replay-filter <db>  Match を 1P / 2P キャラで絞った件数を TSV で吐く（★窓を開かない）");
                Console.WriteLine("  " + exe + " --dump-stats-leaf <db>  統計の葉・要約カード・内訳を TSV で吐く（★窓を開かない）");
                Console.WriteLine("  " + exe + " " + Data.StatsRecordsDump.Flag
                                  + " <db>  統計のキャラ別記録を TSV で吐く（★窓を開かない）");
                Console.WriteLine("  " + exe + " --dump-history <db>  履歴一覧を TSV で吐く（★窓を開かない）");
                Console.WriteLine("  " + exe + " --dump-char-order <db>");
                Console.WriteLine("                                  キャラの列で並べ替えたときの名前の順を吐く（★窓を開かない）");
                Console.WriteLine("  " + exe + " --dump-stats-layout [config]");
                Console.WriteLine("                                  内訳表の列の並び・区切り・隠す列を吐く（★窓も DB も開かない）");
                Console.WriteLine("  " + exe + " --dump-history-edit <db> <layer0> <keepAborted> <keepCompleted>");
                Console.WriteLine("                                  履歴の削除・整理を合成の対だけで通す（★窓を開かない・★本物は断る）");
                Console.WriteLine("  " + exe + " --dump-stream-view <db>");
                Console.WriteLine("                                  配信パネルの本文・献立・既定の設定・SB/PB/Target/WR の");
                Console.WriteLine("                                  塊を吐く（★窓を開かない）");
                Console.WriteLine("  " + exe + " --dump-stream-view-save <config> <w> <h> <fontScale> <bg>");
                Console.WriteLine("                                   <topmost 0|1> <borderless 0|1>");
                Console.WriteLine("                                  配信パネルの設定の保存を 1 回動かす（★窓を開かない・");
                Console.WriteLine("                                  ★合成の config.json だけを渡すこと）");
                Console.WriteLine("  " + exe + " --dump-replay-detail <db> <replay_id|all> [<layer0>]");
                Console.WriteLine("                                  リプレイのラウンド表を TSV で吐く（★窓を開かない。");
                Console.WriteLine("                                  ★<layer0> を渡すとクイックの内訳をそこから数える）");
                Console.WriteLine("  " + exe + " --dump-replay-files <db> <session_id|replay:<id>|all>");
                Console.WriteLine("                                  個別詳細の「ファイルの場所」を TSV で吐く（★窓を開かない）");
                Console.WriteLine("  " + exe + " --dump-reveal-choices <db> <replay_id>");
                Console.WriteLine("                                  右クリックの献立の候補（置き場所が複数のとき）を TSV で吐く（★窓を開かない）");
                Console.WriteLine("  " + exe + " --dump-history-signature <db>");
                Console.WriteLine("                                  履歴の自動更新が見るフィンガープリントを吐く（★窓を開かない）");
                Console.WriteLine("  " + exe + " --config <config.json>");
                Console.WriteLine("                                  設定の読み先だけを差し替える（★1 バイトも書かない。");
                Console.WriteLine("                                  ★--selftest と合わせて、合成の設定で絵を撮るための口）");
                Console.WriteLine("  " + exe + " --help               これ");
                return 0;
            }
            unknown.Add(args[i]);
        }
        if (unknown.Count > 0)
        {
            Console.Error.WriteLine("★知らない引数です（窓は開きません）: " + string.Join(" ", unknown));
            Console.Error.WriteLine("★使える口は " + exe + " --help に出ます。");
            Console.Error.WriteLine("★--align-check / --shot-stats / --shot-pages / --live-probe は"
                                    + " --hitwindow-bench の下の旗です"
                                    + "（例: " + exe + " --hitwindow-bench --align-check）。");
            return 2;
        }

        Data.CrashLog.Install();
        Data.LogSource.EnsureStarted();
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var b = AppBuilder.Configure<App>()
                          .UsePlatformDetect()
                          .LogToTrace();
        var want = Environment.GetEnvironmentVariable(RenderModeVar);
        RenderModeAsked = string.IsNullOrWhiteSpace(want) ? "auto" : want.Trim().ToLowerInvariant();
        var modes = RenderModesOf(RenderModeAsked);
        var comp = CompositionModesOf(
            Environment.GetEnvironmentVariable(CompositionModeVar)?.Trim().ToLowerInvariant());
        if (modes is not null || comp is not null)
        {
            var opts = new Avalonia.Win32PlatformOptions();
            if (modes is not null) opts.RenderingMode = modes;
            if (comp is not null) opts.CompositionMode = comp;
            b = b.With(opts);
        }
        return b;
    }

    public const string RenderModeVar = "TH09_RENDER";

    public const string CompositionModeVar = "TH09_COMPOSITION";

    private static IReadOnlyList<Avalonia.Win32CompositionMode>? CompositionModesOf(string? want)
        => want switch
        {
            "winui" => [Avalonia.Win32CompositionMode.WinUIComposition],
            "dcomp" or "directcomposition" =>
                [Avalonia.Win32CompositionMode.DirectComposition],
            "redirection" => [Avalonia.Win32CompositionMode.RedirectionSurface],
            _ => null,
        };

    public static string RenderModeAsked { get; private set; } = "auto";

    private static IReadOnlyList<Avalonia.Win32RenderingMode>? RenderModesOf(string s) => s switch
    {
        "angle" => [Avalonia.Win32RenderingMode.AngleEgl],
        "wgl" => [Avalonia.Win32RenderingMode.Wgl],
        "software" => [Avalonia.Win32RenderingMode.Software],
        _ => null,
    };
}
