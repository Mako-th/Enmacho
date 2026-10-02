using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TH09.Launch;
using TH09.Record;
using TH09.Shell.Data;
using TH09.Shell.Navigation;

namespace TH09.Shell.ViewModels;

internal sealed partial class DriveControlViewModel : ObservableObject
{
    private const string Category = "起動";


    public const string ImportConfirmTitle = "既存Replay一括登録";

    public const string ImportConfirmMessage =
        "「対象にするフォルダ」の既存.rpyを全てデコードして登録します。\n"
        + "件数が多いと時間がかかります。終わったら上段に件数が出ます。\n\n実行しますか？";

    public const string MonitorStopConfirmTitle = "監視を停止";

    public const string MonitorStopConfirmMessage =
        "プレイ途中ですが停止していいですか？今のセッション記録が途切れます。";

    public const string FirstRunDbTitle = "記録用のデータベース";

    public static readonly string FirstRunDbMessage =
        "記録用のデータベースがまだありません。作ってリプレイを登録しますか？\n\n"
        + ImportConfirmMessage[..ImportConfirmMessage.LastIndexOf("\n\n", StringComparison.Ordinal)];

    public const string StoppedText = "停止しました";

    public const string AutoWord = "自動";

    public const string ScanBlocksMonitorWord = "走査中";

    public const string RestartWord = "起こし直します";

    public const string RestartGaveUpWord = "起こし直しても落ちたので止めました";

    public const string ScanFormTitle = "走査";

    public const string ScanConfirmTitle = "走査を始めます";

    public const string ScanConfirmMessage =
        "進捗は上段に出ます。途中で止めるときは上段の「停止」を押してください。\n\n始めますか？";

    public const string DropFirstConfirmTitle = "バックアップの空きが足りません";

    public const string DropFirstConfirmMessageFormat =
        "バックアップの置き先の空きが足りません（要 {0} ／ 空き {1}）。前の回のバックアップ（{2} 件 / {3}）を先に消せば入ります。\n\n"
        + "先に消してからバックアップを取り、走査を始めますか？\n"
        + "※バックアップを取り終えるまでの間、完成したバックアップが 1 つも無くなります。";

    public const string ScanStopsMonitorNote = "監視を止めてから走査します。";

    public const string ScanStopsMonitorSessionNote = "今のセッション記録が途切れます。";

    public const string NoFoldersSelectedMessage = "フォルダが一つも指定されていません。";

    public const string FoldersOutOfRangeMessageFormat =
        "設定の「リプレイのフォルダ」（ゲームのフォルダ・ユーザー側のデータ・追加の置き場）の外にあるフォルダが"
        + "選ばれています: {0}。走査の窓で外すか、設定の「記録」タブの追加の置き場に足してください。";

    public const string ScanMonitorStopFailedMessage = "監視を止められなかったので走査を始めていません";

    private readonly INavigationService navigation;
    private readonly DriveProcessController control;

    private string failureText = "";

    private bool autoStartAllowed;

    private readonly AutoMonitorRestart restart = new();
    private IDisposable? pendingRestart;
    private bool monitorSeenRunning;
    private bool autoHalted;
    private bool stoppedMonitorForScan;
    private string restartNote = "";

    private bool scanSeenRunning;
    private bool pendingScanPlan;
    private ScanSettings? lastRealScanSettings;

    internal event Action<DbUpdateCause>? DbUpdated;

    private bool importSeenRunning;
    private bool monitorSessionWasOpen;
    private int watchRegisteredSeen;

    private void RaiseDbUpdated(DbUpdateCause cause)
    {
        try
        {
            DbUpdated?.Invoke(cause);
        }
        catch (Exception ex)
        {
            LogSource.Error(Category, DbUpdateCauses.Label(cause) + "を受けた読み直しに失敗しました: "
                                      + LogSource.Describe(ex));
        }
    }

    internal Func<DateTime> Clock { get; set; } = () => DateTime.UtcNow;

    internal Func<TimeSpan, Action, IDisposable> RestartScheduler { get; set; }
        = (delay, action) => DispatcherTimer.RunOnce(action, delay);

    public DriveControlViewModel(INavigationService navigation, DriveProcessController control)
    {
        this.navigation = navigation;
        this.control = control;
        control.StateChanged += OnStateChanged;
        if (OperatingSystem.IsWindows())
        {
            Settings.Adopted += ApplyAutoMonitor;
            Settings.Adopted += ApplyWatchSync;
            Settings.Backups.IsScanRunning = () => IsScanRunning;
        }
        ApplyAutoMonitor();
        Apply(control.Snapshot());
    }

    [ObservableProperty]
    public partial string MonitorText { get; set; } = Label("監視", false);

    [ObservableProperty]
    public partial string WatchText { get; set; } = Label("Replay保存監視", false);

    [ObservableProperty]
    public partial bool IsMonitorRunning { get; set; }

    [ObservableProperty]
    public partial bool IsWatchRunning { get; set; }

    [ObservableProperty]
    public partial bool IsMonitorSessionOpen { get; set; }

    [ObservableProperty]
    public partial bool IsMonitorStopConfirmOpen { get; set; }

    [ObservableProperty]
    public partial bool IsMonitorAuto { get; set; }

    [ObservableProperty]
    public partial bool CanToggleMonitor { get; set; } = true;

    [ObservableProperty]
    public partial bool CanImport { get; set; } = true;

    [ObservableProperty]
    public partial bool IsImportRunning { get; set; }

    [ObservableProperty]
    public partial bool IsImportConfirmOpen { get; set; }

    [ObservableProperty]
    public partial bool IsFirstRunDbOpen { get; set; }

    [ObservableProperty]
    public partial string FirstRunDbPathText { get; set; } = "";

    [ObservableProperty]
    public partial string LastResultText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsDropFirstConfirmOpen { get; set; }

    [ObservableProperty]
    public partial string DropFirstConfirmText { get; set; } = "";

    public string DropFirstConfirmHeader => DropFirstConfirmTitle;

    public string ImportConfirmText => ImportConfirmMessage;

    public string ImportConfirmHeader => ImportConfirmTitle;

    public string MonitorStopConfirmHeader => MonitorStopConfirmTitle;

    public string MonitorStopConfirmText => MonitorStopConfirmMessage;

    public string FirstRunDbHeader => FirstRunDbTitle;

    public string FirstRunDbText => FirstRunDbMessage;


    public ScanSettingsViewModel Scan { get; } = new();

    [ObservableProperty]
    public partial bool IsScanFormOpen { get; set; }

    [ObservableProperty]
    public partial bool IsScanConfirmOpen { get; set; }

    [ObservableProperty]
    public partial bool IsScanRunning { get; set; }

    partial void OnIsScanRunningChanged(bool value)
    {
        if (OperatingSystem.IsWindows()) Settings.Backups.NotifyScanChanged();
    }

    [ObservableProperty]
    public partial bool CanStartScan { get; set; } = true;

    [ObservableProperty]
    public partial string ScanStatusText { get; set; } = ScanRunningLabel;

    internal const string ScanRunningLabel = "走査: 実行中";

    [ObservableProperty]
    public partial bool HasScanProgress { get; set; }

    [ObservableProperty]
    public partial int ScanProgressCurrent { get; set; }

    [ObservableProperty]
    public partial int ScanProgressTotal { get; set; } = 1;

    [ObservableProperty]
    public partial string ScanConfirmSummary { get; set; } = "";

    public string ScanFormHeader => ScanFormTitle;

    public string ScanConfirmHeader => ScanConfirmTitle;

    public string ScanConfirmText => ScanConfirmMessage;


    public const string ScanPlanTitle = "走査の予定";

    [ObservableProperty]
    public partial bool IsScanPlanOpen { get; set; }

    [ObservableProperty]
    public partial bool ScanPlanFailed { get; set; }

    [ObservableProperty]
    public partial string ScanPlanTargetLine { get; set; } = "";

    [ObservableProperty]
    public partial string ScanPlanHoursLine { get; set; } = "";

    [ObservableProperty]
    public partial string ScanPlanFilesLine { get; set; } = "";

    [ObservableProperty]
    public partial string ScanPlanFailureText { get; set; } = "";

    public string ScanPlanHeader => ScanPlanTitle;

    [RelayCommand]
    private void CloseScanPlan() => IsScanPlanOpen = false;


    public AppSettingsViewModel Settings { get; } = new();

    [ObservableProperty]
    public partial bool IsSettingsFormOpen { get; set; }

    internal Avalonia.Input.Key? MonitorToggleKey
        => OperatingSystem.IsWindows()
            ? MonitorToggleKeyReading.Read(AppSettingsSource.Current.RecordReplayToggleKey, out _)
            : null;


    [RelayCommand]
    private void ToggleMonitor()
    {
        if (!CanToggleMonitor) return;
        if (IsMonitorAuto)
        {
            if (!autoHalted) return;
            restart.Reset();
            autoHalted = false;
            restartNote = "";
            CanToggleMonitor = false;
            StartAutoMonitor();
            return;
        }
        if (IsMonitorRunning && IsMonitorSessionOpen)
        {
            IsMonitorStopConfirmOpen = true;
            return;
        }
        Toggle(LaunchKind.Monitor);
    }

    [RelayCommand]
    private void CancelMonitorStop() => IsMonitorStopConfirmOpen = false;

    [RelayCommand]
    private void ConfirmMonitorStop()
    {
        IsMonitorStopConfirmOpen = false;
        Toggle(LaunchKind.Monitor);
    }

    [RelayCommand]
    private void RequestImport()
    {
        if (!CanImport) return;
        IsImportConfirmOpen = true;
    }

    [RelayCommand]
    private void CancelImport() => IsImportConfirmOpen = false;

    [RelayCommand]
    private void ConfirmImport()
    {
        IsImportConfirmOpen = false;
        if (!CanImport) return;
        if (Scan.Directories.Count == 0)
        {
            failureText = ImportConfirmTitle + ": " + NoFoldersSelectedMessage;
            LastResultText = failureText;
            return;
        }
        if (FoldersOutOfRangeReason() is { } outOfRangeReason)
        {
            failureText = ImportConfirmTitle + ": " + outOfRangeReason;
            LastResultText = failureText;
            return;
        }
        try
        {
            control.Start(LaunchKind.ImportOnly, ImportLaunchOptions());
            ClearFailure();
        }
        catch (Exception ex)
        {
            LogSource.Error(Category, ImportConfirmTitle + " を開始できませんでした: " + ex.Message);
            ShowFailure(LaunchKind.ImportOnly, "開始できません", ex);
            return;
        }
        IsScanFormOpen = false;
        navigation.TryShowTab(ShellTab.Log);
    }

    private string? FoldersOutOfRangeReason()
    {
        var outside = Scan.OutOfRangeDirectories();
        if (outside.Count == 0) return null;
        return string.Format(FoldersOutOfRangeMessageFormat, string.Join(", ", outside));
    }

    private LaunchOptions ImportLaunchOptions() => new()
    {
        Scan = new ScanSettings { Directories = [.. Scan.Directories], NoRecurse = Scan.NoRecurse },
    };

    internal void OfferFirstRunDb(string? mainDbPath)
    {
        bool offer = !string.IsNullOrEmpty(mainDbPath) && !File.Exists(mainDbPath) && CanImport;
        FirstRunDbPathText = offer ? "作る場所: " + mainDbPath : "";
        IsFirstRunDbOpen = offer;
    }

    internal void AutoImportExisting(string? mainDbPath)
    {
        if (string.IsNullOrEmpty(mainDbPath) || !File.Exists(mainDbPath)) return;
        if (!OperatingSystem.IsWindows() || !AppSettingsSource.Current.AutoImportOnStart) return;
        if (!CanImport || IsScanRunning) return;
        if (Scan.Directories.Count == 0) return;
        if (Scan.OutOfRangeDirectories().Count > 0) return;
        try
        {
            control.Start(LaunchKind.ImportOnly, ImportLaunchOptions());
            ClearFailure();
        }
        catch (Exception ex)
        {
            LogSource.Error(Category,
                ImportConfirmTitle + " を自動で開始できませんでした: " + ex.Message);
            ShowFailure(LaunchKind.ImportOnly, "開始できません", ex);
        }
    }

    [RelayCommand]
    private void ConfirmFirstRunDb()
    {
        IsFirstRunDbOpen = false;
        ConfirmImport();
    }

    [RelayCommand]
    private void DeferFirstRunDb()
    {
        IsFirstRunDbOpen = false;
        LogSource.Info(Category, FirstRunDbTitle + ": 後で（まだ作っていません）");
    }

    [RelayCommand]
    private void OpenScan() => IsScanFormOpen = true;

    [RelayCommand]
    private void CloseScan()
    {
        IsScanFormOpen = false;
        IsScanConfirmOpen = false;
    }

    [RelayCommand]
    private void PlanScan()
    {
        if (!CanStartScan) return;
        StartScan(dryRun: true);
    }

    [RelayCommand]
    private void RequestScanRun()
    {
        if (!CanStartScan) return;
        ScanConfirmSummary = Scan.Summary() + ScanConfirmMonitorNote();
        IsScanConfirmOpen = true;
    }

    private string ScanConfirmMonitorNote() =>
        IsMonitorRunning
            ? "\n\n" + ScanStopsMonitorNote + (IsMonitorSessionOpen ? ScanStopsMonitorSessionNote : "")
            : "";

    [RelayCommand]
    private void CancelScan() => IsScanConfirmOpen = false;

    [RelayCommand]
    private void ConfirmScan()
    {
        IsScanConfirmOpen = false;
        if (!CanStartScan) return;
        StartScan(dryRun: false);
    }

    private void OfferDropFirst(ScanProgressLines.BackupDropFirstOffer offer)
    {
        DropFirstConfirmText = string.Format(DropFirstConfirmMessageFormat,
                                             offer.Need, offer.Free, Num(offer.OldCount), offer.OldSize);
        IsDropFirstConfirmOpen = true;
    }

    [RelayCommand]
    private void ConfirmDropFirst()
    {
        IsDropFirstConfirmOpen = false;
        if (!CanStartScan) return;
        if (lastRealScanSettings is not { } last) return;
        LogSource.Info(Category, DropFirstConfirmTitle + ": 先に消してからバックアップを取り、走査を始め直します");
        StartScan(dryRun: false, rerun: last with { DropOldBackupsFirst = true });
    }

    [RelayCommand]
    private void CancelDropFirst()
    {
        IsDropFirstConfirmOpen = false;
        LogSource.Info(Category, DropFirstConfirmTitle + ": いいえ（古いバックアップは消していません）");
    }

    [RelayCommand]
    private void OpenSettings()
    {
        Settings.RefreshReplayFolders();
        Settings.RefreshExcludedReplays();
        Settings.Backups.Refresh();
        IsSettingsFormOpen = true;
    }

    [RelayCommand]
    private void CloseSettings()
    {
        IsSettingsFormOpen = false;
        Settings.ReloadCommand.Execute(null);
    }

    [RelayCommand]
    private void StopScan()
    {
        try
        {
            if (!control.Snapshot()[LaunchKind.Scan].IsRunning) return;
            if (!control.Stop(LaunchKind.Scan)) ShowStopFailure(LaunchKind.Scan);
            else ClearFailure();
        }
        catch (Exception ex)
        {
            LogSource.Error(Category, NameOf(LaunchKind.Scan) + " を停止できませんでした: " + ex.Message);
            ShowFailure(LaunchKind.Scan, "停止できません", ex);
        }
    }


    internal void AllowAutoMonitorStart()
    {
        autoStartAllowed = true;
        ApplyAutoMonitor();
    }

    private void ApplyAutoMonitor()
    {
        bool auto = OperatingSystem.IsWindows() && AppSettingsSource.Current.AutoMonitorOnGame;
        if (!auto)
        {
            CancelPendingRestart();
            restart.Reset();
            autoHalted = false;
            restartNote = "";
        }
        IsMonitorAuto = auto;
        RecomputeCanToggleMonitor();
        MonitorText = MonitorLabel(IsMonitorRunning, auto, IsScanRunning);
        if (auto) IsMonitorStopConfirmOpen = false;
        if (!auto || !autoStartAllowed || autoHalted) return;
        StartAutoMonitor();
    }

    private void StartAutoMonitor()
    {
        try
        {
            var now = control.Snapshot();
            if (now[LaunchKind.Monitor].IsRunning) return;
            if (now[LaunchKind.Scan].IsRunning)
            {
                stoppedMonitorForScan = true;
                return;
            }
            control.Start(LaunchKind.Monitor);
            restart.Started(Clock());
            restartNote = "";
            ClearFailure();
            Apply(control.Snapshot());
        }
        catch (Exception ex)
        {
            LogSource.Error(Category,
                NameOf(LaunchKind.Monitor) + " を自動で開始できませんでした: " + ex.Message);
            ShowFailure(LaunchKind.Monitor, "開始できません", ex);
        }
    }

    private void OnMonitorExited(DriveControlSnapshot snapshot, DriveProcessState monitor)
    {
        if (snapshot.IsDisposed)
        {
            CancelPendingRestart();
            return;
        }
        if (!IsMonitorAuto || !autoStartAllowed || monitor.StoppedIntentionally) return;
        if (restart.OnUnintendedExit(Clock()) is TimeSpan delay)
        {
            restartNote = "／ " + delay.TotalSeconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
                          + " 秒後に" + RestartWord + "（" + restart.Attempts + " 回目 / "
                          + AutoMonitorRestart.MaxAttempts + " 回）";
            CancelPendingRestart();
            pendingRestart = RestartScheduler(delay, () =>
            {
                pendingRestart = null;
                StartAutoMonitor();
            });
            return;
        }
        autoHalted = true;
        RecomputeCanToggleMonitor();
        restartNote = "／ " + AutoMonitorRestart.MaxAttempts + " 回" + RestartGaveUpWord + "。押すと起こし直します";
    }

    private void CancelPendingRestart()
    {
        pendingRestart?.Dispose();
        pendingRestart = null;
    }

    private void RecomputeCanToggleMonitor() =>
        CanToggleMonitor = (!IsMonitorAuto || autoHalted) && !IsScanRunning;


    private void SyncWatchToMonitor(bool wasRunning, bool isRunning, DriveControlSnapshot snapshot)
    {
        if (!OperatingSystem.IsWindows()) return;
        if (!wasRunning && isRunning)
        {
            if (AppSettingsSource.Current.WatchReplaysWithMonitor) StartWatchIfWanted(snapshot);
        }
        else if (wasRunning && !isRunning)
        {
            StopWatchIfRunning(snapshot);
        }
    }

    private void ApplyWatchSync()
    {
        if (!OperatingSystem.IsWindows()) return;
        var snapshot = control.Snapshot();
        bool wanted = AppSettingsSource.Current.WatchReplaysWithMonitor;
        if (wanted && snapshot[LaunchKind.Monitor].IsRunning) StartWatchIfWanted(snapshot);
        else if (!wanted && snapshot[LaunchKind.Watch].IsRunning) StopWatchIfRunning(snapshot);
    }

    private void StartWatchIfWanted(DriveControlSnapshot snapshot)
    {
        if (!snapshot[LaunchKind.Watch].CanStart) return;
        try
        {
            control.Start(LaunchKind.Watch);
        }
        catch (Exception ex)
        {
            LogSource.Error(Category,
                NameOf(LaunchKind.Watch) + " を自動で開始できませんでした: " + ex.Message);
            ShowFailure(LaunchKind.Watch, "開始できません", ex);
        }
    }

    private void StopWatchIfRunning(DriveControlSnapshot snapshot)
    {
        if (!snapshot[LaunchKind.Watch].IsRunning) return;
        if (!control.Stop(LaunchKind.Watch)) ShowStopFailure(LaunchKind.Watch);
    }


    private void StartScan(bool dryRun, ScanSettings? rerun = null)
    {
        ScanSettings settings;
        if (rerun is { } same) settings = same;
        else if (!TryBuildScanSettings(dryRun, out settings)) return;
        if (!dryRun && IsMonitorRunning)
        {
            if (!control.Stop(LaunchKind.Monitor))
            {
                failureText = NameOf(LaunchKind.Scan) + ": " + ScanMonitorStopFailedMessage;
                LastResultText = failureText;
                return;
            }
            stoppedMonitorForScan = true;
        }
        try
        {
            control.Start(LaunchKind.Scan, new LaunchOptions { Scan = settings });
            ClearFailure();
        }
        catch (Exception ex)
        {
            LogSource.Error(Category, NameOf(LaunchKind.Scan) + " を開始できませんでした: " + ex.Message);
            ShowFailure(LaunchKind.Scan, "開始できません", ex);
            if (stoppedMonitorForScan)
            {
                stoppedMonitorForScan = false;
                if (IsMonitorAuto && autoStartAllowed) StartAutoMonitor();
            }
            if (dryRun) ShowScanPlan(null, ex.Message);
            return;
        }
        if (dryRun)
        {
            pendingScanPlan = true;
        }
        else
        {
            lastRealScanSettings = settings with { DropOldBackupsFirst = false };
            IsScanFormOpen = false;
            navigation.TryShowTab(ShellTab.Log);
        }
    }

    private bool TryBuildScanSettings(bool dryRun, out ScanSettings settings)
    {
        settings = ScanSettings.Default;
        if (Scan.Directories.Count == 0)
        {
            failureText = NameOf(LaunchKind.Scan) + ": " + NoFoldersSelectedMessage;
            LastResultText = failureText;
            if (dryRun) ShowScanPlan(null, NoFoldersSelectedMessage);
            return false;
        }
        if (FoldersOutOfRangeReason() is { } outOfRangeReason)
        {
            failureText = NameOf(LaunchKind.Scan) + ": " + outOfRangeReason;
            LastResultText = failureText;
            if (dryRun) ShowScanPlan(null, outOfRangeReason);
            return false;
        }
        if (!Scan.TryBuild(dryRun, out var built, out string error))
        {
            LogSource.Error(Category, NameOf(LaunchKind.Scan) + ": " + error);
            failureText = NameOf(LaunchKind.Scan) + ": " + error;
            LastResultText = failureText;
            if (dryRun) ShowScanPlan(null, error);
            return false;
        }
        settings = built!;
        return true;
    }

    private void ShowScanPlan(ScanProgressLines.ScanPlanSummary? plan, string? failureText)
    {
        if (plan is { } p)
        {
            ScanPlanFailed = false;
            ScanPlanTargetLine = "対象 " + Num(p.TargetCount) + " 件（" + p.DoneNote + "）";
            ScanPlanHoursLine = "所要見積 "
                + p.Hours.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " 時間";
            ScanPlanFilesLine = "ファイル操作: コピー " + Num(p.CopyCount) + " 件 / 在中 "
                + Num(p.InSlotCount) + " 件";
        }
        else
        {
            ScanPlanFailed = true;
            ScanPlanFailureText = string.IsNullOrEmpty(failureText)
                ? "走査の予定を読み取れませんでした。"
                : failureText;
        }
        IsScanPlanOpen = true;
    }

    private void Toggle(LaunchKind kind)
    {
        bool wasRunning = false;
        try
        {
            var state = control.Snapshot()[kind];
            wasRunning = state.IsRunning;
            if (wasRunning)
            {
                if (!control.Stop(kind))
                {
                    ShowStopFailure(kind);
                    return;
                }
            }
            else if (state.CanStart) control.Start(kind);
            ClearFailure();
        }
        catch (Exception ex)
        {
            LogSource.Error(Category, NameOf(kind) + " を切り替えられませんでした: " + ex.Message);
            ShowFailure(kind, wasRunning ? "停止できません" : "開始できません", ex);
        }
    }

    private void ShowStopFailure(LaunchKind kind)
    {
        failureText = NameOf(kind) + ": 停止できませんでした";
        LastResultText = failureText;
    }

    private void ShowFailure(LaunchKind kind, string what, Exception ex)
    {
        failureText = NameOf(kind) + ": " + what + "（" + ex.Message + "）";
        LastResultText = failureText;
    }

    private void ClearFailure()
    {
        if (failureText.Length == 0) return;
        failureText = "";
        Apply(control.Snapshot());
    }

    private void OnStateChanged(DriveControlSnapshot snapshot)
    {
        if (Dispatcher.UIThread.CheckAccess()) Apply(snapshot);
        else Dispatcher.UIThread.Post(() => Apply(control.Snapshot()));
    }

    private void Apply(DriveControlSnapshot snapshot)
    {
        var monitor = snapshot[LaunchKind.Monitor];
        bool wasMonitorRunning = IsMonitorRunning;
        IsMonitorRunning = monitor.IsRunning;
        IsMonitorSessionOpen = monitor.SessionOpen;
        IsWatchRunning = snapshot[LaunchKind.Watch].IsRunning;
        IsImportRunning = snapshot[LaunchKind.ImportOnly].IsRunning;
        IsScanRunning = snapshot[LaunchKind.Scan].IsRunning;
        MonitorText = MonitorLabel(IsMonitorRunning, IsMonitorAuto, IsScanRunning);
        WatchText = Label("Replay保存監視", IsWatchRunning);
        CanImport = snapshot[LaunchKind.ImportOnly].CanStart && !IsScanRunning;
        CanStartScan = snapshot[LaunchKind.Scan].CanStart && !IsImportRunning;
        RecomputeCanToggleMonitor();
        ApplyScanProgress(snapshot[LaunchKind.Scan].ScanProgress);
        if (!IsMonitorRunning) IsMonitorStopConfirmOpen = false;
        SyncWatchToMonitor(wasMonitorRunning, monitor.IsRunning, snapshot);
        if (monitor.IsRunning) monitorSeenRunning = true;
        else if (monitorSeenRunning)
        {
            monitorSeenRunning = false;
            OnMonitorExited(snapshot, monitor);
        }
        var scanState = snapshot[LaunchKind.Scan];
        if (scanState.IsRunning) scanSeenRunning = true;
        else if (scanSeenRunning)
        {
            scanSeenRunning = false;
            if (stoppedMonitorForScan)
            {
                stoppedMonitorForScan = false;
                if (IsMonitorAuto && autoStartAllowed && !snapshot[LaunchKind.Monitor].IsRunning)
                    Dispatcher.UIThread.Post(StartAutoMonitor);
            }
            if (!pendingScanPlan && scanState.LastDropFirstOffer is { } offer
                && !scanState.StoppedIntentionally
                && scanState.LastExitCode is int offerExit && offerExit != 0)
                OfferDropFirst(offer);
            if (pendingScanPlan)
            {
                pendingScanPlan = false;
                ShowScanPlan(scanState.LastScanPlan,
                    scanState.LastFailureLine
                        ?? "終了コード " + (scanState.LastExitCode?.ToString(
                            System.Globalization.CultureInfo.InvariantCulture) ?? "不明"));
            }
            else
            {
                RaiseDbUpdated(DbUpdateCause.ScanEnded);
            }
        }
        if (snapshot[LaunchKind.ImportOnly].IsRunning) importSeenRunning = true;
        else if (importSeenRunning)
        {
            importSeenRunning = false;
            RaiseDbUpdated(DbUpdateCause.ImportEnded);
        }
        if (monitor.SessionOpen) monitorSessionWasOpen = true;
        else if (monitorSessionWasOpen)
        {
            monitorSessionWasOpen = false;
            RaiseDbUpdated(DbUpdateCause.PlayEnded);
        }
        var watchRegistered = snapshot[LaunchKind.Watch].WatchRegisteredTotal;
        if (watchRegistered != watchRegisteredSeen)
        {
            watchRegisteredSeen = watchRegistered;
            RaiseDbUpdated(DbUpdateCause.ReplayRegistered);
        }
        LastResultText = failureText.Length > 0 ? failureText
            : monitor is { IsRunning: true, LastNoticeLine: { } notice }
                ? NameOf(LaunchKind.Monitor) + ": " + notice
                : string.Join(" ／ ", snapshot.Processes.Where(p => p.LastExitCode is not null)
                                                       .Select(ExitText))
                  + restartNote;
    }

    private void ApplyScanProgress(ScanProgressLines.Mark? progress)
    {
        if (progress is not { } mark)
        {
            HasScanProgress = false;
            ScanStatusText = ScanRunningLabel;
            return;
        }
        HasScanProgress = true;
        ScanProgressCurrent = mark.Current;
        ScanProgressTotal = mark.Total;
        ScanStatusText = mark.Phase == ScanProgressLines.Phase.Scanning
            ? "走査 " + Num(mark.Current) + " / " + Num(mark.Total) + " 本"
            : "Layer 1 作り直し " + Num(mark.Current) + " / " + Num(mark.Total);
    }

    private static string Num(int value)
        => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal void PreviewScanProgressForShot(ScanProgressLines.Mark? progress)
    {
        IsScanRunning = progress is not null;
        ApplyScanProgress(progress);
    }

    private static string ExitText(DriveProcessState p)
        => p.Kind == LaunchKind.ImportOnly && !p.StoppedIntentionally
                && p.LastImportResultLine is { } result
            ? result
        : p.Kind == LaunchKind.Scan && !p.StoppedIntentionally
                && p.LastScanBatchResult is { } batch
            ? ScanBatchExitText(p, batch)
        : p.StoppedIntentionally ? NameOf(p.Kind) + ": " + StoppedText
            : p.LastFailureLine is { } line
                ? NameOf(p.Kind) + ": 終了コード " + p.LastExitCode + "（" + line + "）"
                : NameOf(p.Kind) + ": 終了コード " + p.LastExitCode;

    private static string ScanBatchExitText(DriveProcessState p, ScanProgressLines.BatchOutcome batch)
    {
        var text = NameOf(p.Kind) + ": 済 " + Num(batch.Ok) + " / 不一致 " + Num(batch.Mismatch)
            + " / 失敗 " + Num(batch.Failed) + " / 中身が空 " + Num(batch.Empty)
            + " / 未処理 " + Num(batch.Skipped) + "（全 " + Num(batch.Total) + " 件）";
        if (p.LastScanStopReason is { } reason) text += " ／ 停止理由: " + reason;
        if (p.LastExitCode is int code && code != 0) text += "（終了コード " + code + "）";
        return text;
    }

    internal void PreviewRunningForShot(bool running)
    {
        IsMonitorRunning = running;
        MonitorText = MonitorLabel(running, IsMonitorAuto, IsScanRunning);
    }

    internal void PreviewMonitorStopConfirmForShot(bool open) => IsMonitorStopConfirmOpen = open;

    internal void PreviewScanPlanForShot(ScanProgressLines.ScanPlanSummary? plan, string? failureText)
        => ShowScanPlan(plan, failureText);

    internal void PreviewLastResultTextForShot(string text) => LastResultText = text;

    private static string Label(string name, bool running)
        => name + ": " + (running ? "ON" : "OFF");

    private static string MonitorLabel(bool running, bool auto, bool scanRunning)
    {
        _ = running;
        if (scanRunning) return "監視: " + ScanBlocksMonitorWord;
        return auto ? "監視: " + AutoWord : "監視";
    }

    private static string NameOf(LaunchKind kind) => kind switch
    {
        LaunchKind.Monitor => "監視",
        LaunchKind.Watch => "Replay保存監視",
        LaunchKind.ImportOnly => ImportConfirmTitle,
        LaunchKind.Scan => "走査",
        _ => kind.ToString(),
    };
}
