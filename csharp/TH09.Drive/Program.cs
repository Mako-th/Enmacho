using System.Text;

namespace TH09.Drive;

public static class Program
{
    internal static readonly string ExeName =
        Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "th09_drive";

    public static int Main(string[] args)
    {
        try { Console.OutputEncoding = new UTF8Encoding(false); } catch (IOException) { }
        try
        {
            return Run(args);
        }
        catch (MenuMapException exc)
        {
            Console.Error.WriteLine("★メニューのレイアウトが壊れています: " + exc.Message);
            return 1;
        }
        catch (Exception exc)
        {
            Console.Error.WriteLine(exc.GetType().Name + ": " + exc.Message);
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            Help();
            return args.Length == 0 ? 2 : 0;
        }
        if (args[0] == InputProbe.Flag)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("共有メモリを触れるのは Windows だけです。");
                return 3;
            }
            return InputProbe.Run(args[1..]);
        }
        if (args[0] == MonitorCommand.Flag)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("--monitor は追加引数を受け付けません: " + string.Join(" ", args[1..]));
                return 2;
            }
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("監視を走らせられるのは Windows だけです。");
                return 3;
            }
            return RunMonitor();
        }
        if (args[0] == WatchCommand.Flag)
        {
            bool importExisting = args.Length == 2
                                  && args[1] == WatchCommand.ImportExistingFlag;
            if (args.Length > 2 || (args.Length == 2 && !importExisting))
            {
                Console.Error.WriteLine("--watch が受け付ける追加引数は --import-existing だけです: "
                                        + string.Join(" ", args[1..]));
                return 2;
            }
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("リプレイ保存の見張りを走らせられるのは Windows だけです。");
                return 3;
            }
            return RunWatch(importExisting);
        }
        if (args[0] == ScanTargetsDump.Flag)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("本体 DB と Layer 0 の置き場所を引けるのは Windows だけです。");
                return 3;
            }
            using var writer = new StreamWriter(Console.OpenStandardOutput(),
                                                new UTF8Encoding(false), 1 << 16);
            int rc = ScanTargetsDump.Run(writer, args[1..]);
            writer.Flush();
            return rc;
        }
        if (args[0] == ScanOneDump.Flag)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("本体 DB と replay フォルダを触れるのは Windows だけです。");
                return 3;
            }
            using var writer = new StreamWriter(Console.OpenStandardOutput(),
                                                new UTF8Encoding(false), 1 << 16);
            int rc = ScanOneDump.Run(writer, args[1..]);
            writer.Flush();
            return rc;
        }
        if (args[0] == ScanBatchDump.Flag)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("本体 DB と replay フォルダを触れるのは Windows だけです。");
                return 3;
            }
            using var writer = new StreamWriter(Console.OpenStandardOutput(),
                                                new UTF8Encoding(false), 1 << 16);
            int rc = ScanBatchDump.Run(writer, args[1..]);
            writer.Flush();
            return rc;
        }
        if (args[0] == ScanMissingDbDump.Flag)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("本体 DB と replay フォルダを触れるのは Windows だけです。");
                return 3;
            }
            using var writer = new StreamWriter(Console.OpenStandardOutput(),
                                                new UTF8Encoding(false), 1 << 16);
            int rc = ScanMissingDbDump.Run(writer, args[1..]);
            writer.Flush();
            return rc;
        }
        if (args[0] == ScanRunModesDump.Flag)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("config.json の置き場所を引けるのは Windows だけです。");
                return 3;
            }
            using var writer = new StreamWriter(Console.OpenStandardOutput(),
                                                new UTF8Encoding(false), 1 << 16);
            int rc = ScanRunModesDump.Run(writer, args[1..]);
            writer.Flush();
            return rc;
        }
        if (args[0] == RunBatchZeroDump.Flag)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("控え・Layer 0 の置き場所を引けるのは Windows だけです。");
                return 3;
            }
            using var writer = new StreamWriter(Console.OpenStandardOutput(),
                                                new UTF8Encoding(false), 1 << 16);
            int rc = RunBatchZeroDump.Run(writer, args[1..]);
            writer.Flush();
            return rc;
        }
        if (args[0] == ScanDbGuardDump.Flag)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("本体 DB と Layer 0 の置き場所を引けるのは Windows だけです。");
                return 3;
            }
            using var writer = new StreamWriter(Console.OpenStandardOutput(),
                                                new UTF8Encoding(false), 1 << 16);
            int rc = ScanDbGuardDump.Run(writer, args[1..]);
            writer.Flush();
            return rc;
        }
        if (args[0] == ScanDestinationDump.Flag)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("控え・Layer 0 の置き場所を引けるのは Windows だけです。");
                return 3;
            }
            using var writer = new StreamWriter(Console.OpenStandardOutput(),
                                                new UTF8Encoding(false), 1 << 16);
            int rc = ScanDestinationDump.Run(writer, args[1..]);
            writer.Flush();
            return rc;
        }
        if (args[0] == MonitorLoopDump.Flag)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("捕捉の配線を組めるのは Windows だけです。");
                return 3;
            }
            using var writer = new StreamWriter(Console.OpenStandardOutput(),
                                                new UTF8Encoding(false), 1 << 16);
            int rc = MonitorLoopDump.Run(writer, args[1..]);
            writer.Flush();
            return rc;
        }
        if (args[0] == ScanBackupDump.Flag)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("控えの置き場所を引けるのは Windows だけです。");
                return 3;
            }
            using var writer = new StreamWriter(Console.OpenStandardOutput(),
                                                new UTF8Encoding(false), 1 << 16);
            int rc = ScanBackupDump.Run(writer, args[1..]);
            writer.Flush();
            return rc;
        }
        if (args[0] == ReplayWatchDump.Flag)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("本体 DB と replay フォルダを触れるのは Windows だけです。");
                return 3;
            }
            using var watchWriter = new StreamWriter(Console.OpenStandardOutput(),
                                                     new UTF8Encoding(false), 1 << 16);
            int watchRc = ReplayWatchDump.Run(watchWriter, args[1..]);
            watchWriter.Flush();
            return watchRc;
        }
        if (args[0] == ScanCommand.Flag)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("本体 DB と replay フォルダを触れるのは Windows だけです。");
                return 3;
            }
            return RunScan(args[1..]);
        }
        if (args[0] == Layer1Command.Flag)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("本体 DB と Layer 0 を触れるのは Windows だけです。");
                return 3;
            }
            using var writer = new StreamWriter(Console.OpenStandardOutput(),
                                                new UTF8Encoding(false), 1 << 16)
            {
                AutoFlush = true,
            };
            int rc = Layer1Command.Run(writer, args[1..]);
            writer.Flush();
            return rc;
        }
        if (args[0] == AutoPlayDump.Flag && args.Length == 1)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("この台本を走らせられるのは Windows だけです。");
                return 3;
            }
            using var writer = new StreamWriter(Console.OpenStandardOutput(),
                                                new UTF8Encoding(false), 1 << 16);
            int rc = AutoPlayDump.Run(writer);
            writer.Flush();
            return rc;
        }
        if (args.Length > 1)
        {
            Console.Error.WriteLine("引数が多すぎます（このコマンドはモードを 1 つだけ取ります）: "
                                    + string.Join(" ", args));
            return 2;
        }

        MenuMap.SelfCheck();
        TH09.TickBus.TickBusLayout.SelfCheck();

        switch (args[0])
        {
            case "--selftest":
                Console.WriteLine("メニューのレイアウトは正当です（領域 "
                                  + TH09.Generated.MenuMapLayout.RegionCount + " 個 / 定数 "
                                  + TH09.Generated.MenuMapLayout.ConstantCount + " 個）。");
                return 0;
            case "--dump-menu-driver":
                {
                    using var writer = new StreamWriter(Console.OpenStandardOutput(),
                                                        new UTF8Encoding(false), 1 << 16);
                    int rc = MenuDriverDump.Run(writer);
                    writer.Flush();
                    return rc;
                }
            case SlotDump.Flag:
                {
                    using var writer = new StreamWriter(Console.OpenStandardOutput(),
                                                        new UTF8Encoding(false), 1 << 16);
                    int rc = SlotDump.Run(writer);
                    writer.Flush();
                    return rc;
                }
            case LiveViewDump.Flag:
                {
                    using var writer = new StreamWriter(Console.OpenStandardOutput(),
                                                        new UTF8Encoding(false), 1 << 16);
                    int rc = LiveViewDump.Run(writer);
                    writer.Flush();
                    return rc;
                }
            case "--dump-menu-map":
                {
                    using var writer = new StreamWriter(Console.OpenStandardOutput(),
                                                        new UTF8Encoding(false), 1 << 16);
                    int rc = MenuMapDump.Run(writer);
                    writer.Flush();
                    return rc;
                }
            default:
                Console.Error.WriteLine("未知のモードです: " + args[0]);
                Console.Error.WriteLine("--help を見てください。");
                return 2;
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static int RunScan(string[] argv)
    {
        try
        {
            var args = ScanCommand.Resolve(ScanCommand.Parse(argv), null, ReplaySlots.DefaultLog);
            using var writer = new StreamWriter(Console.OpenStandardOutput(),
                                                new UTF8Encoding(false), 1 << 16)
            {
                AutoFlush = true,
            };
            int rc = ScanCommand.Run(writer, args);
            writer.Flush();
            return rc;
        }
        catch (ScanUsageError exc)
        {
            Console.Error.WriteLine(exc.Message);
            Console.Error.WriteLine("--scan --help のつもりなら、" + ExeName + " --help を見てください。");
            return 2;
        }
        catch (ScanSetupFailed exc)
        {
            Console.Error.WriteLine(exc.Message);
            return 1;
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static int RunMonitor()
        => RunUntilCanceled(token => MonitorCommand.Run(token, Console.WriteLine));

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static int RunWatch(bool importExisting)
        => RunUntilCanceled(token => WatchCommand.Run(Console.Out, importExisting, token));

    private static int RunUntilCanceled(Func<CancellationToken, int> run)
    {
        using var cancel = new CancellationTokenSource();
        ConsoleCancelEventHandler onCancel = (_, e) =>
        {
            e.Cancel = true;
            cancel.Cancel();
        };
        Console.CancelKeyPress += onCancel;
        try
        {
            return run(cancel.Token);
        }
        finally
        {
            Console.CancelKeyPress -= onCancel;
        }
    }

    private static void Help()
    {
        Console.WriteLine($"""
{ExeName} — 通常監視・リプレイ保存監視・走査

  --monitor           通常のプレイ記録を監視する（Ctrl+C で停止）
  --watch [--import-existing]
                      リプレイ保存を監視する（任意で起動時に既存分も登録。Ctrl+C で停止）
  --selftest          定数テーブルの自己検査だけを走らせる（★原本の `_self_check()`）
  --dump-menu-map     メニューの読み解きを丸ごとタブ区切りで吐く（★突き合わせ用）
  --dump-menu-driver  疑似ゲーム相手にメニュー操作の台本を走らせ、軌跡を吐く（★突き合わせ用）
  --dump-live-view    メニューの読み出し（凍結の判定を含む）の台本を吐く（★突き合わせ用）
  --dump-slot         スロットの退避と残骸の回収の台本を吐く（★突き合わせ用。★一時フォルダだけ）
  --dump-scan-targets [本体DB [Layer0]]
                      走査の対象選択の台本を吐く（★突き合わせ用。★読むだけ）
  --dump-auto-play    自動再生の駆動の台本を吐く（★突き合わせ用。★ゲームにも本物の席にも触らない）
  --dump-scan-one <本体DB> <replayフォルダ> <台本> <書き手ロック> <走査中ロック> <Layer0DB|->
                      1 本スキャンの台本を吐く（★突き合わせ用。★本物のロックの名前は拒む）
                      ★Layer 0 は「-」で「持たない構成」。★既定値は無い（本物を黙って開かせない）
  --dump-scan-batch <本体DB> <replayフォルダ> <台本> <書き手ロック> <走査中ロック> <Layer0DB|->
                      一括の台本を吐く（★突き合わせ用。★本物のロックの名前は拒む）
  --dump-scan-missing-db <本体DB> <置き場所の根> [走査の旗…]
                      本体 DB が無いときの走査の前ぶれ（作って登録する）を 1 回流して吐く
                      （★本物の本体 DB のフォルダは拒む。★ゲームにも Layer 0 にも触らない）
  --dump-scan-run-modes --root <フォルダ> [--own] [--run <走り方>]
                      走り方の見出し（--own の警告を含む）を吐く（★突き合わせ用。
                      ★config.json は渡した一時フォルダの下から読む。★本物のフォルダは拒む）
  --dump-run-batch-zero <偽リポジトリの根>
                      一括で対象 0 件のとき控え・Layer 0 に触れないことを吐く（★突き合わせ用。
                      ★対象 0 件しか流さない。本物のロックは取らない）
  --dump-scan-db-guard <偽リポジトリの根> [走査の旗…]
                      --db が既定と違うのに走査する指定が来たら断ることを吐く（★突き合わせ用。
                      ★本物の exe に --batch/--single/--auto は渡さない）
  --dump-scan-destinations <偽リポジトリの根> [走査の旗…]
                      --backup / --batch・--single・--auto が実際に使う控え・Layer 0 の
                      行き先だけを、実行せずに吐く（★突き合わせ用。★1 バイトも動かさない）
  --dump-watch-loop [引数…]
                      リプレイ保存の見張りの台本を吐く（★突き合わせ用。★触るのは台本が
                      指した一時フォルダだけ。★走査中の合図は読むだけで立てない）
  --dump-scan-backup <偽リポジトリの根> <日時> <plan|run>
                      走る前の控えを偽のリポジトリの上で回す（★本物のデータには触らない）
  --dump-monitor-loop <偽リポジトリの根> <台本> <不在で 0 を返す回数>
                      監視の待受を台本どおりに回して吐く（★突き合わせ用。★ゲームも DB も
                      共有メモリも触らない。★ゲームが終了しても待ち続けることを見る）
  --scan [引数…]      走査そのもの（--list / --dry-run / --restore-slots / --verify / --backup ほか）
                      ★引数の一覧は {ExeName} --scan（引数なし）で出ます
  --build-layer1 [--db <本体DB>] [--layer0 <Layer0>] [--check] [--all] [--session N]
                      [--writer-lock <名前> --scan-lock <名前>]
                      Layer 1（round_metrics）を Layer 0 から作って本体 DB へ書く（★段階 5単位9a）。
                      ★中身は TH09.Record.Layer1Build を呼ぶだけ（th09_record --build-layer1 と同じ字）。
                      ★--db / --layer0 を省くと本物の場所。★--check は 1 バイトも書かずに数えるだけ。
                      ★--writer-lock / --scan-lock は検査用（本物の名前は受け付けない）。
  --probe-input <名前> <保持> <保持のfields> <叩くキー>
                      入力コマンドを書く口を 1 回通す（★検査用。★本物の Bus の名前は拒む）

★th09.exe を起動・終了・前面化せず、OS のキーボード・マウス入力も使いません。
★--monitor と捕捉を伴う --scan は既存のゲームを探して読みます。
★走査のメニュー入力は Tick Bus 経由だけです。
★突き合わせは `PYTHONUTF8=1 python csharp/tools/test_menu_map.py` と
  `PYTHONUTF8=1 python csharp/tools/test_menu_driver.py`。
""");
    }
}
