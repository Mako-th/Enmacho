using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TH09.Shell.ViewModels;
using TH09.Shell.Views;
using System.Linq;

namespace TH09.Shell;

internal partial class App : Application
{
    public static (long SessionId, int Seconds, int W, int H, int X, int Y)? AutoPlay { get; set; }

    public static string? SelfTestDir { get; set; }

    public static string? SelfTestSizeSpec { get; set; }

    public static string? SelfTestSizeNote { get; set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void TidyStaleBackups()
    {
        try
        {
            var paths = TH09.Record.Paths.Default;
            if (!TH09.Record.ConfigStore.Load(paths.ConfigPath).BackupKeepOne) return;
            var removed = TH09.Record.BackupLayout.TidyStale(
                paths.BackupRoot, DateTime.Now,
                f => Data.LogSource.Info("起動", "未完成の控えを消しました: " + f.Path
                                                 + "（" + f.Files + " 本 / " + f.Bytes
                                                 + " B ／ 目印なし）"),
                why => Data.LogSource.Warn("起動", why));
            if (removed.Count > 0)
            {
                Data.LogSource.Info("起動",
                    "控えと見做さないもの（未完成）を " + removed.Count + " 件片付けました（合計 "
                    + removed.Sum(r => r.Bytes) + " B）");
            }
        }
        catch (Exception ex)
        {
            Data.LogSource.Warn("起動", "控えの片付けに失敗しました: " + Data.LogSource.Describe(ex));
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (OperatingSystem.IsWindows())
        {
            Data.TrackerDb.MainDbPath = TH09.Record.Paths.Default.MainDb;
            Data.TrackerDb.Layer0DbPath = TH09.Record.Paths.Default.Layer0Db;
            TidyStaleBackups();
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var shell = ShellViewModel.Create();
            var window = new MainWindow { DataContext = shell };
            desktop.MainWindow = window;
            window.Closed += (_, _) => shell.Dispose();

            if (SelfTestDir is null && AutoPlay is null)
            {
                shell.Drive.OfferFirstRunDb(Data.TrackerDb.MainDbPath);
                shell.Drive.AutoImportExisting(Data.TrackerDb.MainDbPath);
                shell.Drive.AllowAutoMonitorStart();
            }

            if (AutoPlay is { } ap)
            {
                window.ShowActivated = false;
                window.ShowInTaskbar = false;
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Position = new PixelPoint(ap.X, ap.Y);
                window.Width = ap.W;
                window.Height = ap.H;
                window.Opened += (_, _) =>
                {
                    foreach (var tab in shell.Tabs)
                    {
                        shell.SelectedTab = tab;
                        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    }
                    shell.SelectedTab = shell.Tabs.OfType<ReplayTabViewModel>().FirstOrDefault()
                                        ?? shell.Tabs[0];
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                    var detail = shell.Detail;
                    detail.Show(Navigation.ReplayDetailRequest.FromSession(ap.SessionId));
                    if (detail.Rows.Count > 0) detail.SelectedRow = detail.Rows[0];
                    detail.Page = DetailPage.HitWindow;
                    shell.IsDetailOpen = true;
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    if (detail.HitWindowContent is not HitWindowViewModel hv)
                    {
                        Console.Error.WriteLine("★被弾窓の中身が結べていない。");
                        Environment.Exit(1);
                        return;
                    }
                    hv.Playing = true;

                    var flip = new Avalonia.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(1.5),
                    };
                    flip.Tick += (_, _) =>
                    {
                        if (detail.CanNextHitWindow) detail.NextHitWindowCommand.Execute(null);
                        else if (detail.CanPrevHitWindow) detail.PrevHitWindowCommand.Execute(null);
                        hv.Playing = true;
                    };
                    flip.Start();
                    Avalonia.Threading.DispatcherTimer.RunOnce(() =>
                    {
                        flip.Stop();
                        hv.Playing = false;
                        int wrote = Data.PlaybackTrace.Dump();
                        window.Close();
                        Environment.Exit(wrote > 0 ? 0 : 1);
                    }, TimeSpan.FromSeconds(ap.Seconds));
                };
            }

            if (SelfTestDir is not null)
            {
                Console.Error.WriteLine("★--selftest は開発者版だけです（配布版には自己検査の道具が入っていません）。");
                Environment.Exit(2);
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
