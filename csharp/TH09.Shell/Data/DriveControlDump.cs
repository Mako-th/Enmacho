using System.Text;
using Avalonia;
using Avalonia.Threading;
using TH09.Launch;
using TH09.Record;
using TH09.Shell.Navigation;
using TH09.Shell.ViewModels;

namespace TH09.Shell.Data;

internal static class DriveControlDump
{
    public const string Flag = "--dump-drive-control";

    public const string BarFlag = "--dump-drive-bar";

    public const string BarDistFlag = "--dump-drive-bar-dist";

    public const string DbUpdateReloadFlag = "--dump-db-update-reload";

    public static int Run()
    {
        using var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
        try
        {
            LogSource.Clear();
            var fake = new FakeLauncher();
            using (var control = new DriveProcessController(fake))
            {
                var notices = new List<DriveControlSnapshot>();
                control.StateChanged += notices.Add;
                control.Start(LaunchKind.Monitor);
                bool duplicate = ThrowsDuplicate(() => control.Start(LaunchKind.Monitor));
                fake.Last(LaunchKind.Monitor).Emit("子の1行");
                fake.Last(LaunchKind.Monitor).Exit(0);
                bool zeroState = control.Snapshot()[LaunchKind.Monitor] is
                { IsRunning: false, CanStart: true, LastExitCode: 0 };

                control.Start(LaunchKind.Watch);
                fake.Last(LaunchKind.Watch).Exit(9);
                control.Start(LaunchKind.Watch);
                var natural = fake.Last(LaunchKind.Watch);
                natural.ExitSilently(4);
                control.Start(LaunchKind.Watch);
                bool naturalDispose = natural.WasDisposed
                                      && control.Snapshot()[LaunchKind.Watch].LastExitCode == 4;
                fake.Last(LaunchKind.Watch).Exit(0);

                fake.FinishNextWithoutNotice(LaunchKind.ImportOnly, 5);
                control.Start(LaunchKind.ImportOnly);
                bool earlyExit = fake.Last(LaunchKind.ImportOnly).WasDisposed
                                 && control.Snapshot()[LaunchKind.ImportOnly] is
                                 { IsRunning: false, CanStart: true, LastExitCode: 5 };

                control.Start(LaunchKind.ImportOnly);
                bool importResultStartsNull =
                    control.Snapshot()[LaunchKind.ImportOnly].LastImportResultLine is null;
                fake.Last(LaunchKind.ImportOnly).Emit("関係ない行");
                bool importResultIgnoresNoise =
                    control.Snapshot()[LaunchKind.ImportOnly].LastImportResultLine is null;
                var importLine = ScanProgressLines.ImportResult(7);
                fake.Last(LaunchKind.ImportOnly).Emit(importLine);
                bool importResultReads =
                    control.Snapshot()[LaunchKind.ImportOnly].LastImportResultLine == importLine;
                fake.Last(LaunchKind.ImportOnly).Exit(0);
                bool importResultSurvivesExit =
                    control.Snapshot()[LaunchKind.ImportOnly].LastImportResultLine == importLine;
                control.Start(LaunchKind.ImportOnly);
                bool importResultResetsOnRestart =
                    control.Snapshot()[LaunchKind.ImportOnly].LastImportResultLine is null;
                fake.Last(LaunchKind.ImportOnly).Exit(0);
                bool importResult = importResultStartsNull && importResultIgnoresNoise
                                    && importResultReads && importResultSurvivesExit
                                    && importResultResetsOnRestart;

                control.Start(LaunchKind.Monitor);
                bool stopped = control.Stop(LaunchKind.Monitor);
                control.Start(LaunchKind.Monitor);
                bool restarted = fake.StartCount(LaunchKind.Monitor) == 3;
                control.Start(LaunchKind.Scan);
                fake.Last(LaunchKind.Scan).Emit("理由の行");
                fake.Last(LaunchKind.Scan).Emit("   ");
                fake.Last(LaunchKind.Scan).Exit(1);
                bool failureLine = control.Snapshot()[LaunchKind.Scan].LastFailureLine == "理由の行";

                control.Start(LaunchKind.Scan);
                fake.Last(LaunchKind.Scan).Emit("止める前の行");
                control.Stop(LaunchKind.Scan);
                failureLine &= control.Snapshot()[LaunchKind.Scan].LastFailureLine is null;

                control.Start(LaunchKind.Scan);
                fake.Last(LaunchKind.Scan).Emit("終わりの行");
                fake.Last(LaunchKind.Scan).Exit(0);
                failureLine &= control.Snapshot()[LaunchKind.Scan].LastFailureLine is null;

                control.Start(LaunchKind.Scan);
                fake.Last(LaunchKind.Scan).Exit(1);
                failureLine &= control.Snapshot()[LaunchKind.Scan].LastFailureLine is null;

                control.Start(LaunchKind.Scan);
                bool progressStartsNull = control.Snapshot()[LaunchKind.Scan].ScanProgress is null;
                fake.Last(LaunchKind.Scan).Emit("");
                fake.Last(LaunchKind.Scan).Emit("何か関係ない行");
                bool progressIgnoresNoise = control.Snapshot()[LaunchKind.Scan].ScanProgress is null;
                var header = ScanProgressLines.ScanHeader(3, 138, 555, "foo.rpy", 12.0);
                fake.Last(LaunchKind.Scan).Emit(header);
                bool progressReadsScan = control.Snapshot()[LaunchKind.Scan].ScanProgress is
                    { Phase: ScanProgressLines.Phase.Scanning, Current: 3, Total: 138 };
                var cut = header[..header.IndexOf(ScanProgressLines.ScanHeaderReplayMarker,
                                                  StringComparison.Ordinal)];
                fake.Last(LaunchKind.Scan).Emit(cut);
                bool progressIgnoresTruncated = control.Snapshot()[LaunchKind.Scan].ScanProgress is
                    { Phase: ScanProgressLines.Phase.Scanning, Current: 3, Total: 138 };
                fake.Last(LaunchKind.Scan).Emit(
                    "[12:34:56] " + ScanProgressLines.Layer1Progress(5, 50, 900, 12.3));
                bool progressReadsLayer1 = control.Snapshot()[LaunchKind.Scan].ScanProgress is
                    { Phase: ScanProgressLines.Phase.RebuildingLayer1, Current: 5, Total: 50 };
                fake.Last(LaunchKind.Scan).Exit(0);
                bool progressClearsOnExit = control.Snapshot()[LaunchKind.Scan].ScanProgress is null;
                control.Start(LaunchKind.Scan);
                bool progressClearsOnRestart = control.Snapshot()[LaunchKind.Scan].ScanProgress is null;
                fake.Last(LaunchKind.Scan).Exit(0);
                bool scanProgress = progressStartsNull && progressIgnoresNoise && progressReadsScan
                                    && progressIgnoresTruncated && progressReadsLayer1
                                    && progressClearsOnExit && progressClearsOnRestart;

                control.Start(LaunchKind.ImportOnly);
                control.Dispose();
                bool stateNotice = notices.Any(s => s[LaunchKind.Monitor] is
                                                { IsRunning: true, CanStart: false })
                                   && notices.Any(s => s[LaunchKind.Watch].LastExitCode == 4)
                                   && notices[^1] is { IsDisposed: true }
                                   && notices[^1].Processes.All(p => !p.IsRunning && !p.CanStart);

                Write(stdout, "start", fake.StartCount(LaunchKind.Monitor) == 3);
                Write(stdout, "duplicate", duplicate);
                Write(stdout, "output-info", Has(LogLevel.Info, "子の1行"));
                Write(stdout, "zero-exit-info", Has(LogLevel.Info, "終了コード 0"));
                Write(stdout, "zero-state", zeroState);
                Write(stdout, "nonzero-exit-error", Has(LogLevel.Error, "終了コード 9"));
                Write(stdout, "natural-dispose", naturalDispose);
                Write(stdout, "early-exit", earlyExit);
                Write(stdout, "stop", stopped);
                Write(stdout, "restart", restarted);
                Write(stdout, "dispose-stop", fake.Last(LaunchKind.ImportOnly).WasStopped
                                             && fake.Last(LaunchKind.Monitor).WasStopped);
                Write(stdout, "intentional-nonzero-info", Has(LogLevel.Info, "終了コード 1"));
                Write(stdout, "failure-line", failureLine);
                Write(stdout, "scan-progress", scanProgress);
                Write(stdout, "import-result", importResult);
                Write(stdout, "state-notice", stateNotice);
            }
            stdout.Flush();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static int RunBar()
    {
        using var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
        var barDir = Path.Combine(Path.GetTempPath(), "th09_shell_bar_dump_" + Environment.ProcessId);
        try
        {
            AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();
            Directory.CreateDirectory(barDir);
            var barConfig = Path.Combine(barDir, "config.json");
            File.WriteAllText(barConfig,
                "{\"" + ConfigStore.WatchReplaysWithMonitorKey + "\": true}");
            AppSettingsSource.ReadFrom(barConfig);
            LogSource.Clear();
            var fake = new FakeLauncher();
            var shell = ShellViewModel.Create(fake);
            var bar = shell.Drive;

            AddDirectoryConfirmed(bar, @"C:\synthetic_gate\replay");

            bool restoreAtStartup = fake.StartCount(TH09.Launch.LaunchKind.RestoreSlots) == 1
                                    && fake.LastOptions(TH09.Launch.LaunchKind.RestoreSlots)?.Scan is null;
            var restoreArgs = string.Join(
                " ", TH09.Launch.DriveLauncher.PreviewArguments(TH09.Launch.LaunchKind.RestoreSlots));

            bool initialOff = bar.MonitorText == "監視" && !bar.IsMonitorRunning
                              && !bar.IsWatchRunning
                              && bar.CanImport && !bar.IsImportConfirmOpen;

            bar.ToggleMonitorCommand.Execute(null);
            bool monitorOn = fake.StartCount(LaunchKind.Monitor) == 1
                             && bar.MonitorText == "監視" && bar.IsMonitorRunning;
            bool watchOn = fake.StartCount(LaunchKind.Watch) == 1 && bar.IsWatchRunning;
            bar.ToggleMonitorCommand.Execute(null);
            bool monitorOff = fake.Last(LaunchKind.Monitor).WasStopped
                              && bar.MonitorText == "監視" && !bar.IsMonitorRunning;
            bool watchOff = fake.Last(LaunchKind.Watch).WasStopped && !bar.IsWatchRunning;

            bar.RequestImportCommand.Execute(null);
            bool confirmFirst = bar.IsImportConfirmOpen
                                && fake.StartCount(LaunchKind.ImportOnly) == 0;
            bar.CancelImportCommand.Execute(null);
            bool cancelled = !bar.IsImportConfirmOpen
                             && fake.StartCount(LaunchKind.ImportOnly) == 0;

            bool notLogBefore = shell.SelectedTab?.Key != ShellTab.Log;
            bar.RequestImportCommand.Execute(null);
            bar.ConfirmImportCommand.Execute(null);
            bool importStarted = fake.StartCount(LaunchKind.ImportOnly) == 1
                                 && !bar.IsImportConfirmOpen;
            bool movedToLog = notLogBefore && shell.SelectedTab?.Key == ShellTab.Log;
            bool busy = !bar.CanImport;
            bar.RequestImportCommand.Execute(null);
            bool busyBlocks = !bar.IsImportConfirmOpen
                              && fake.StartCount(LaunchKind.ImportOnly) == 1;

            int scanStartsWhileImporting = fake.StartCount(LaunchKind.Scan);
            bar.OpenScanCommand.Execute(null);
            bool scanBlockedDuringImport = !bar.CanStartScan;
            bar.PlanScanCommand.Execute(null);
            bar.RequestScanRunCommand.Execute(null);
            bar.ConfirmScanCommand.Execute(null);
            bool scanStaysBlockedDuringImport = !bar.IsScanConfirmOpen
                && fake.StartCount(LaunchKind.Scan) == scanStartsWhileImporting;
            bar.CloseScanCommand.Execute(null);

            fake.Last(LaunchKind.ImportOnly).Exit(0);
            bool importAgain = bar.CanImport
                               && bar.LastResultText.Contains("既存Replay一括登録: 終了コード 0",
                                                              StringComparison.Ordinal);
            bool scanUnblockedAfterImport = bar.CanStartScan;

            bar.RequestImportCommand.Execute(null);
            bar.ConfirmImportCommand.Execute(null);
            var importResultLine = TH09.Record.ScanProgressLines.ImportResult(42);
            fake.Last(LaunchKind.ImportOnly).Emit(importResultLine);
            fake.Last(LaunchKind.ImportOnly).Exit(0);
            bool importResultShown = bar.LastResultText.Contains(importResultLine,
                                                                  StringComparison.Ordinal)
                                     && !bar.LastResultText.Contains("既存Replay一括登録: 終了コード",
                                                                     StringComparison.Ordinal);

            var missing = new FileNotFoundException("th09_drive.exe がありません", "th09_drive.exe");
            fake.FailNextStart(LaunchKind.Monitor, missing);
            bar.ToggleMonitorCommand.Execute(null);
            bool startFailure = !bar.IsMonitorRunning
                                && bar.MonitorText == "監視"
                                && bar.LastResultText == "監視: 開始できません（" + missing.Message + "）"
                                && Has(LogLevel.Error, missing.Message);

            bool scanClosed = !bar.IsScanFormOpen && bar.CanStartScan && !bar.IsScanRunning;
            bar.OpenScanCommand.Execute(null);
            bool scanOpened = bar.IsScanFormOpen && fake.StartCount(LaunchKind.Scan) == 0;

            shell.Navigation.ShowTab(ShellTab.Log);
            bar.Scan.SelectedRun = ScanSettingsViewModel.RunOptions[1];
            bar.PlanScanCommand.Execute(null);
            var planned = fake.LastOptions(LaunchKind.Scan)?.Scan;
            bool scanPlanStaysPut = fake.StartCount(LaunchKind.Scan) == 1
                                    && !bar.IsScanConfirmOpen
                                    && planned is { DryRun: true, Run: ScanRun.Rescan }
                                    && shell.SelectedTab?.Key == ShellTab.Log
                                    && bar.IsScanFormOpen;
            var planLine = TH09.Record.ScanProgressLines.ScanPlanLine(
                7, "新規のみ走査してDB追加", 1.5, 5, 2);
            fake.Last(LaunchKind.Scan).Emit(planLine);
            fake.Last(LaunchKind.Scan).Exit(0);
            bool scanPlanned = scanPlanStaysPut
                               && bar.IsScanPlanOpen && !bar.ScanPlanFailed
                               && bar.ScanPlanTargetLine.Contains("対象 7 件", StringComparison.Ordinal)
                               && bar.ScanPlanTargetLine.Contains("新規のみ走査してDB追加",
                                                                  StringComparison.Ordinal)
                               && bar.ScanPlanHoursLine.Contains("1.5", StringComparison.Ordinal)
                               && bar.ScanPlanFilesLine.Contains("コピー 5", StringComparison.Ordinal)
                               && bar.ScanPlanFilesLine.Contains("在中 2", StringComparison.Ordinal);
            bar.CloseScanPlanCommand.Execute(null);
            bool scanPlanCloses = !bar.IsScanPlanOpen && bar.IsScanFormOpen;
            scanPlanned &= scanPlanCloses;

            bar.OpenScanCommand.Execute(null);
            bar.Scan.CharacterChips[0].IsSelected = false;
            bar.Scan.MaxCountText = "7";
            bar.RequestScanRunCommand.Execute(null);
            bool scanConfirmFirst = bar.IsScanConfirmOpen
                                    && fake.StartCount(LaunchKind.Scan) == 1
                                    && bar.ScanConfirmSummary.Contains("上書きして新たに走査",
                                                                       StringComparison.Ordinal);
            bar.CancelScanCommand.Execute(null);
            bool scanCancelled = !bar.IsScanConfirmOpen && bar.IsScanFormOpen
                                 && fake.StartCount(LaunchKind.Scan) == 1;

            bar.RequestScanRunCommand.Execute(null);
            bar.ConfirmScanCommand.Execute(null);
            var started = fake.LastOptions(LaunchKind.Scan)?.Scan;
            bool scanStarted = fake.StartCount(LaunchKind.Scan) == 2
                               && started is { DryRun: false, Run: ScanRun.Rescan, MaxCount: 7 }
                               && !started.Backup
                               && bar.Scan.Summary().Contains("走る前のバックアップ: 取らない",
                                                              StringComparison.Ordinal)
                               && started.Characters.Count == bar.Scan.CharacterChips.Count - 1
                               && !started.Characters.Contains(0)
                               && !bar.IsScanFormOpen && !bar.IsScanConfirmOpen;
            bool scanRunning = bar.IsScanRunning && !bar.CanStartScan;

            int importStartsWhileScanning = fake.StartCount(LaunchKind.ImportOnly);
            bool importBlockedDuringScan = !bar.CanImport;
            bar.RequestImportCommand.Execute(null);
            bool importRequestBlockedDuringScan = !bar.IsImportConfirmOpen;
            bar.ConfirmImportCommand.Execute(null);
            bool importStaysBlockedDuringScan =
                fake.StartCount(LaunchKind.ImportOnly) == importStartsWhileScanning;

            bool scanStatusInitial = bar.ScanStatusText == DriveControlViewModel.ScanRunningLabel
                                     && !bar.HasScanProgress;
            fake.Last(LaunchKind.Scan).Emit("読めない行");
            bool scanStatusIgnoresNoise = bar.ScanStatusText == DriveControlViewModel.ScanRunningLabel
                                          && !bar.HasScanProgress;
            fake.Last(LaunchKind.Scan).Emit(
                TH09.Record.ScanProgressLines.ScanHeader(12, 138, 999, "x.rpy", 3.0));
            bool scanStatusShowsScan = bar.HasScanProgress && bar.ScanProgressCurrent == 12
                                       && bar.ScanProgressTotal == 138
                                       && bar.ScanStatusText == "走査 12 / 138 本";
            fake.Last(LaunchKind.Scan).Emit("また読めない行");
            bool scanStatusKeepsAfterNoise = bar.HasScanProgress && bar.ScanProgressCurrent == 12
                                             && bar.ScanProgressTotal == 138
                                             && bar.ScanStatusText == "走査 12 / 138 本";
            fake.Last(LaunchKind.Scan).Emit(
                "[00:00:01] " + TH09.Record.ScanProgressLines.Layer1Progress(4, 40, 88, 1.2));
            bool scanStatusShowsLayer1 = bar.HasScanProgress && bar.ScanProgressCurrent == 4
                                         && bar.ScanProgressTotal == 40
                                         && bar.ScanStatusText == "Layer 1 作り直し 4 / 40";
            bool scanProgressBar = scanStatusInitial && scanStatusIgnoresNoise
                                   && scanStatusShowsScan && scanStatusKeepsAfterNoise
                                   && scanStatusShowsLayer1;

            bar.RequestScanRunCommand.Execute(null);
            bool scanBusyBlocks = !bar.IsScanConfirmOpen && fake.StartCount(LaunchKind.Scan) == 2;

            bar.StopScanCommand.Execute(null);
            bool scanStopped = fake.Last(LaunchKind.Scan).WasStopped
                               && !bar.IsScanRunning && bar.CanStartScan;
            bool importUnblockedAfterScan = bar.CanImport;
            bool scanProgressResetAfterStop = !bar.HasScanProgress
                                              && bar.ScanStatusText == DriveControlViewModel.ScanRunningLabel;
            scanProgressBar &= scanProgressResetAfterStop;

            bar.Scan.MaxCountText = "たくさん";
            bar.PlanScanCommand.Execute(null);
            bool scanBadNumber = fake.StartCount(LaunchKind.Scan) == 2
                                 && bar.LastResultText.Contains("数で書いてください",
                                                                StringComparison.Ordinal);
            bar.Scan.MaxCountText = "7";

            int beforeReset = fake.StartCount(LaunchKind.Scan);
            bar.Scan.SelectedRun = ScanSettingsViewModel.RunOptions[2];
            bar.PlanScanCommand.Execute(null);
            var resetPlanned = fake.LastOptions(LaunchKind.Scan)?.Scan;
            bool scanResetAllowed =
                fake.StartCount(LaunchKind.Scan) == beforeReset + 1
                && resetPlanned is { DryRun: true, Run: ScanRun.ResetScans }
                && !bar.LastResultText.Contains(ScanSettingsViewModel.ResetBlockedReason,
                                                StringComparison.Ordinal);
            fake.Last(LaunchKind.Scan).Exit(0);
            bar.Scan.SelectedRun = ScanSettingsViewModel.RunOptions[0];

            bool modeAllOn = bar.Scan.ModeChips.Count == 3 && bar.Scan.ModeChips.All(c => c.IsSelected);
            bar.PlanScanCommand.Execute(null);
            bool modeAll = fake.LastOptions(LaunchKind.Scan)?.Scan?.Modes is { Count: 0 };
            fake.Last(LaunchKind.Scan).Exit(0);
            bar.Scan.ToggleChipCommand.Execute(bar.Scan.ModeChips[1]);
            bar.PlanScanCommand.Execute(null);
            var pickedModes = fake.LastOptions(LaunchKind.Scan)?.Scan?.Modes;
            bool modePassed = pickedModes is { Count: 2 }
                              && pickedModes.Contains(ScanMode.Story)
                              && pickedModes.Contains(ScanMode.Match)
                              && !pickedModes.Contains(ScanMode.Extra);
            fake.Last(LaunchKind.Scan).Exit(0);
            bar.Scan.ClearModesCommand.Execute(null);
            bool modeCleared = bar.Scan.ModeChips.All(c => !c.IsSelected);
            bar.Scan.SelectAllModesCommand.Execute(null);
            bool modeBulk = modeCleared && bar.Scan.ModeChips.All(c => c.IsSelected);
            bool oneModeOnly = modeAllOn && modeBulk;

            bar.Scan.SelectedDirectory = bar.Scan.Directories[0];
            bar.Scan.RemoveDirectoryCommand.Execute(null);

            AddDirectoryConfirmed(bar, @"C:\th09\replay");
            AddDirectoryConfirmed(bar, @"C:\TH09\REPLAY");
            AddDirectoryConfirmed(bar, @"D:\別のフォルダ");
            AddDirectoryConfirmed(bar, "   ");
            bool dirsAdded = bar.Scan.Directories.Count == 2;
            bar.Scan.SelectedDirectory = bar.Scan.Directories[0];
            bool canRemove = bar.Scan.CanRemoveDirectory;
            bar.Scan.RemoveDirectoryCommand.Execute(null);
            bool dirsRemoved = bar.Scan.Directories.Count == 1
                               && bar.Scan.Directories[0] == @"D:\別のフォルダ"
                               && !bar.Scan.CanRemoveDirectory;
            bar.PlanScanCommand.Execute(null);
            var withDirs = fake.LastOptions(LaunchKind.Scan)?.Scan;
            bool dirsPassed = withDirs is not null && withDirs.Directories.Count == 1
                              && withDirs.Directories[0] == @"D:\別のフォルダ";
            fake.Last(LaunchKind.Scan).Exit(0);

            bool hitDefaultOff = bar.Scan.HitWindowsFromConfig;
            bar.PlanScanCommand.Execute(null);
            var noOverride = fake.LastOptions(LaunchKind.Scan)?.Scan;
            bool hitNotSent = noOverride is
                { HitWindows: null, HitWindowBefore: null, HitWindowAfter: null,
                  HitWindowQuick: null, HitWindowScopes: null };
            fake.Last(LaunchKind.Scan).Exit(0);
            bar.Scan.HitWindowsFromConfig = false;
            bar.Scan.HitWindows = false;
            bar.Scan.HitWindowBeforeText = "180";
            bar.Scan.HitWindowAfterText = "90";
            bar.Scan.HitWindowQuick = true;
            foreach (var chip in bar.Scan.HitWindowScopes)
                if (chip.Value == ScanScope.Net) chip.IsOn = false;
            bar.PlanScanCommand.Execute(null);
            var overridden = fake.LastOptions(LaunchKind.Scan)?.Scan;
            bool hitSent = overridden is
                              { HitWindows: false, HitWindowBefore: 180, HitWindowAfter: 90,
                                HitWindowQuick: true }
                          && overridden.HitWindowScopes is { Count: 5 } scopes
                          && !scopes[ScanScope.Net];
            fake.Last(LaunchKind.Scan).Exit(0);
            bar.Scan.HitWindowBeforeText = "たくさん";
            int beforeBadHit = fake.StartCount(LaunchKind.Scan);
            bar.PlanScanCommand.Execute(null);
            bool hitBadNumber = fake.StartCount(LaunchKind.Scan) == beforeBadHit
                                && bar.LastResultText.Contains("被弾窓の前",
                                                               StringComparison.Ordinal);
            bar.Scan.HitWindowsFromConfig = true;

            bool settingsClosed = !bar.IsSettingsFormOpen;
            bar.OpenSettingsCommand.Execute(null);
            bar.Settings.HitWindowBeforeText = "12345";
            bool settingsOpened = bar.IsSettingsFormOpen;
            bar.CloseSettingsCommand.Execute(null);
            bool settingsDiscarded = !bar.IsSettingsFormOpen
                                     && bar.Settings.HitWindowBeforeText != "12345";

            bar.ToggleMonitorCommand.Execute(null);
            bool failureCleared = bar.IsMonitorRunning
                                  && !bar.LastResultText.Contains("開始できません", StringComparison.Ordinal);

            fake.Last(LaunchKind.Monitor).StopFails = true;
            bar.ToggleMonitorCommand.Execute(null);
            bool stopFailure = bar.IsMonitorRunning
                               && bar.MonitorText == "監視"
                               && bar.LastResultText == "監視: 停止できませんでした";
            fake.Last(LaunchKind.Monitor).StopFails = false;

            bar.ToggleMonitorCommand.Execute(null);
            bar.ToggleMonitorCommand.Execute(null);
            const string reason = "監視を続けられません: 行き先に印（writer_stamp）がありません";
            fake.Last(LaunchKind.Monitor).Emit(reason);
            fake.Last(LaunchKind.Monitor).Exit(1);
            bool exitReason = !bar.IsMonitorRunning
                              && bar.LastResultText.Contains("監視: 終了コード 1（" + reason + "）",
                                                             StringComparison.Ordinal);
            bar.ToggleMonitorCommand.Execute(null);
            fake.Last(LaunchKind.Monitor).ExitCodeOnStop = -1;
            bar.ToggleMonitorCommand.Execute(null);
            bool intendedExit = !bar.IsMonitorRunning
                                && bar.LastResultText.Contains(
                                       "監視: " + DriveControlViewModel.StoppedText,
                                       StringComparison.Ordinal)
                                && !bar.LastResultText.Contains("監視: 終了コード",
                                                                StringComparison.Ordinal);

            bar.ToggleMonitorCommand.Execute(null);
            bool idleStopNoConfirm = !bar.IsMonitorSessionOpen;
            fake.Last(LaunchKind.Monitor).Emit(MonitorLines.SessionOpenedPrefix + "12 (…)");
            bool sessionOpened = bar.IsMonitorSessionOpen;
            bar.ToggleMonitorCommand.Execute(null);
            bool stopConfirmFirst = bar.IsMonitorStopConfirmOpen && bar.IsMonitorRunning;
            bar.CancelMonitorStopCommand.Execute(null);
            bool stopCancelled = !bar.IsMonitorStopConfirmOpen && bar.IsMonitorRunning;
            fake.Last(LaunchKind.Monitor).Emit(MonitorLines.SessionClosed(12));
            bool sessionClosed = !bar.IsMonitorSessionOpen;
            bool noticeShown = bar.LastResultText.Contains(MonitorLines.SessionClosed(12),
                                                           StringComparison.Ordinal);
            fake.Last(LaunchKind.Monitor).Emit(MonitorLines.SessionOpenedPrefix + "13 (…)");
            bar.ToggleMonitorCommand.Execute(null);
            bar.ConfirmMonitorStopCommand.Execute(null);
            bool stopConfirmed = !bar.IsMonitorStopConfirmOpen && !bar.IsMonitorRunning
                                 && fake.Last(LaunchKind.Monitor).WasStopped;

            bar.ToggleMonitorCommand.Execute(null);

            shell.Dispose();
            bool disposeStops = fake.Last(LaunchKind.Monitor).WasStopped
                                && bar.MonitorText == "監視" && !bar.IsMonitorRunning;

            bool autoMonitor = AutoMonitorProbe();
            bool watchSync = WatchSyncProbe();
            bool autoImportSetting = AutoImportProbe();
            bool scanStopsMonitor = ScanStopsMonitorProbe() && ScanStopsAutoMonitorProbe();
            bool foldersRequired = FoldersRequiredProbe();
            bool scanDirsPersist = ScanDirsPersistProbe();
            bool scanDirsOutOfRange = OutOfRangeConfirmProbe();
            bool dropFirstDialog = DropFirstProbe();
            bool dropFirstLine = DropFirstLineProbe();

            Write(stdout, "bar-restore-slots-at-startup", restoreAtStartup);
            WriteText(stdout, "bar-restore-slots-args", restoreArgs);
            Write(stdout, "bar-initial-off", initialOff);
            Write(stdout, "bar-monitor-on", monitorOn);
            Write(stdout, "bar-monitor-off", monitorOff);
            Write(stdout, "bar-watch-on", watchOn);
            Write(stdout, "bar-watch-off", watchOff);
            Write(stdout, "bar-import-confirm-first", confirmFirst);
            Write(stdout, "bar-import-cancel", cancelled);
            Write(stdout, "bar-import-start", importStarted);
            Write(stdout, "bar-import-log-tab", movedToLog);
            Write(stdout, "bar-import-busy", busy && busyBlocks);
            Write(stdout, "bar-import-again", importAgain);
            Write(stdout, "bar-import-result", importResultShown);
            Write(stdout, "bar-import-blocks-scan", scanBlockedDuringImport
                                                    && scanStaysBlockedDuringImport
                                                    && scanUnblockedAfterImport);
            Write(stdout, "bar-start-failure", startFailure && failureCleared);
            Write(stdout, "bar-stop-failure", stopFailure);
            Write(stdout, "bar-exit-reason", exitReason);
            Write(stdout, "bar-dispose-stop", disposeStops);
            Write(stdout, "bar-scan-open", scanClosed && scanOpened);
            Write(stdout, "bar-scan-plan", scanPlanned);
            Write(stdout, "bar-scan-confirm-first", scanConfirmFirst && scanCancelled);
            Write(stdout, "bar-scan-start", scanStarted);
            Write(stdout, "bar-scan-busy", scanRunning && scanBusyBlocks);
            Write(stdout, "bar-scan-bad-number", scanBadNumber);
            Write(stdout, "bar-scan-stop", scanStopped);
            Write(stdout, "bar-scan-blocks-import", importBlockedDuringScan
                                                    && importRequestBlockedDuringScan
                                                    && importStaysBlockedDuringScan
                                                    && importUnblockedAfterScan);
            Write(stdout, "bar-scan-progress", scanProgressBar);
            Write(stdout, "bar-scan-reset-allowed", scanResetAllowed);
            Write(stdout, "bar-settings-open", settingsClosed && settingsOpened);
            Write(stdout, "bar-settings-discard", settingsDiscarded);
            Write(stdout, "bar-intended-exit", intendedExit);
            Write(stdout, "bar-monitor-session", idleStopNoConfirm && sessionOpened
                                                 && sessionClosed && noticeShown);
            Write(stdout, "bar-monitor-stop-confirm", stopConfirmFirst && stopCancelled
                                                      && stopConfirmed);
            Write(stdout, "bar-scan-mode-chip", oneModeOnly && modePassed && modeAll);
            Write(stdout, "bar-scan-dirs", dirsAdded && canRemove && dirsRemoved && dirsPassed);
            Write(stdout, "bar-scan-hit-window", hitDefaultOff && hitNotSent && hitSent
                                                 && hitBadNumber);
            WriteText(stdout, "bar-scan-scope-order",
                      string.Join(",", bar.Scan.HitWindowScopes.Select(c => c.Label)));
            Write(stdout, "bar-auto-monitor", autoMonitor);
            Write(stdout, "bar-watch-sync", watchSync);
            Write(stdout, "bar-auto-import-setting", autoImportSetting);
            Write(stdout, "bar-scan-stops-monitor", scanStopsMonitor);
            Write(stdout, "bar-folders-required", foldersRequired);
            Write(stdout, "bar-scan-dirs-persist", scanDirsPersist);
            Write(stdout, "bar-scan-dirs-out-of-range", scanDirsOutOfRange);
            Write(stdout, "bar-scan-drop-first", dropFirstDialog);
            Write(stdout, "bar-scan-drop-first-line", dropFirstLine);
            stdout.Flush();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
        finally
        {
            try { if (Directory.Exists(barDir)) Directory.Delete(barDir, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static int RunBarDist()
    {
        using var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
        var barDistDir = Path.Combine(Path.GetTempPath(), "th09_shell_bar_dist_dump_" + Environment.ProcessId);
        try
        {
            Directory.CreateDirectory(barDistDir);
            var barDistConfig = Path.Combine(barDistDir, "config.json");
            File.WriteAllText(barDistConfig,
                "{\"" + ConfigStore.WatchReplaysWithMonitorKey + "\": true}");
            AppSettingsSource.ReadFrom(barDistConfig);
            LogSource.Clear();
            var navigation = new NavigationService();
            var host = new DistHost(navigation);
            navigation.Attach(host);
            var fake = new FakeLauncher();
            using var control = new DriveProcessController(fake);
            var bar = new DriveControlViewModel(navigation, control);
            AddDirectoryConfirmed(bar, @"C:\synthetic_gate\replay");

            bar.RequestImportCommand.Execute(null);
            bar.ConfirmImportCommand.Execute(null);
            bool importNoCrash = fake.StartCount(LaunchKind.ImportOnly) == 1
                                 && host.SelectedTab?.Key == ShellTab.Play;
            var importLine = ScanProgressLines.ImportResult(3);
            fake.Last(LaunchKind.ImportOnly).Emit(importLine);
            fake.Last(LaunchKind.ImportOnly).Exit(0);
            bool importResultShown = bar.LastResultText.Contains(importLine, StringComparison.Ordinal);

            bar.OpenScanCommand.Execute(null);
            bar.RequestScanRunCommand.Execute(null);
            bar.ConfirmScanCommand.Execute(null);
            bool scanStartNoCrash = fake.StartCount(LaunchKind.Scan) == 1
                                    && !bar.IsScanFormOpen && host.SelectedTab?.Key == ShellTab.Play;
            var batchLine = ScanProgressLines.BatchOutcomeLine(2, 0, 1, 1, 0, 4);
            fake.Last(LaunchKind.Scan).Emit(batchLine);
            fake.Last(LaunchKind.Scan).Exit(1);
            bool batchShown = bar.LastResultText.Contains("済 2", StringComparison.Ordinal)
                              && bar.LastResultText.Contains("不一致 0", StringComparison.Ordinal)
                              && bar.LastResultText.Contains("失敗 1", StringComparison.Ordinal)
                              && bar.LastResultText.Contains("中身が空 1", StringComparison.Ordinal)
                              && bar.LastResultText.Contains("未処理 0", StringComparison.Ordinal)
                              && bar.LastResultText.Contains("全 4 件", StringComparison.Ordinal)
                              && bar.LastResultText.Contains("終了コード 1", StringComparison.Ordinal);

            bar.OpenScanCommand.Execute(null);
            bar.PlanScanCommand.Execute(null);
            bool planStartsClosed = !bar.IsScanPlanOpen && bar.IsScanFormOpen;
            var planLine = ScanProgressLines.ScanPlanLine(12, "新規のみ走査してDB追加", 3.2, 10, 2);
            fake.Last(LaunchKind.Scan).Emit(planLine);
            fake.Last(LaunchKind.Scan).Exit(0);
            bool planShown = bar.IsScanPlanOpen && !bar.ScanPlanFailed && bar.IsScanFormOpen
                             && bar.ScanPlanTargetLine.Contains("対象 12 件", StringComparison.Ordinal)
                             && bar.ScanPlanTargetLine.Contains("新規のみ走査してDB追加",
                                                                StringComparison.Ordinal)
                             && bar.ScanPlanHoursLine.Contains("3.2", StringComparison.Ordinal)
                             && bar.ScanPlanFilesLine.Contains("コピー 10", StringComparison.Ordinal)
                             && bar.ScanPlanFilesLine.Contains("在中 2", StringComparison.Ordinal);
            bar.CloseScanPlanCommand.Execute(null);
            bool planCloses = !bar.IsScanPlanOpen;

            bar.PlanScanCommand.Execute(null);
            fake.Last(LaunchKind.Scan).Emit("エラー: 何か");
            fake.Last(LaunchKind.Scan).Exit(1);
            bool planFailShown = bar.IsScanPlanOpen && bar.ScanPlanFailed
                                 && bar.ScanPlanFailureText == "エラー: 何か";

            Write(stdout, "dist-import-no-crash", importNoCrash);
            Write(stdout, "dist-import-result-shown", importResultShown);
            Write(stdout, "dist-scan-start-no-crash", scanStartNoCrash);
            Write(stdout, "dist-scan-batch-shown", batchShown);
            Write(stdout, "dist-scan-plan-starts-closed", planStartsClosed);
            Write(stdout, "dist-scan-plan-shown", planShown);
            Write(stdout, "dist-scan-plan-closes", planCloses);
            Write(stdout, "dist-scan-plan-fail-shown", planFailShown);
            stdout.Flush();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
        finally
        {
            try { if (Directory.Exists(barDistDir)) Directory.Delete(barDistDir, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private sealed class DistHost : INavigationHost
    {
        public DistHost(INavigationService navigation)
        {
            Tabs =
            [
                new StubTab(ShellTab.Play, navigation),
                new StubTab(ShellTab.History, navigation),
                new StubTab(ShellTab.Replay, navigation),
                new StubTab(ShellTab.Stats, navigation),
            ];
            SelectedTab = Tabs[0];
            Detail = new ReplayDetailViewModel(navigation);
        }

        public IReadOnlyList<TabViewModelBase> Tabs { get; }
        public TabViewModelBase? SelectedTab { get; set; }
        public ReplayDetailViewModel Detail { get; }
        public bool IsDetailOpen { get; set; }
    }

    private sealed class StubTab : TabViewModelBase
    {
        public StubTab(ShellTab key, INavigationService navigation) : base(navigation) => Key = key;
        public override ShellTab Key { get; }
        public override string Title => Key.ToString();
        public override string Placeholder => "";
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool AutoMonitorProbe()
    {
        var dir = Path.Combine(Path.GetTempPath(),
                               "th09_shell_auto_monitor_" + Environment.ProcessId);
        var path = Path.Combine(dir, "config.json");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(path,
                "{\"" + ConfigStore.AutoMonitorOnGameKey + "\": true}",
                new UTF8Encoding(false));
            AppSettingsSource.ReadFrom(path);
            var fake = new FakeLauncher();
            using var shell = ShellViewModel.Create(fake);
            var bar = shell.Drive;
            var now = new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc);
            var fired = new List<(TimeSpan Delay, Action Fire)>();
            bar.Clock = () => now;
            bar.RestartScheduler = (delay, action) =>
            {
                fired.Add((delay, action));
                return new NoopDisposable();
            };
            bool quietBeforeAllow = fake.StartCount(LaunchKind.Monitor) == 0
                                    && bar.IsMonitorAuto && !bar.CanToggleMonitor
                                    && bar.MonitorText == "監視: " + DriveControlViewModel.AutoWord;
            bar.AllowAutoMonitorStart();
            bool started = fake.StartCount(LaunchKind.Monitor) == 1 && bar.IsMonitorRunning;
            bar.ToggleMonitorCommand.Execute(null);
            bool stillRunning = bar.IsMonitorRunning
                                && !fake.Last(LaunchKind.Monitor).WasStopped
                                && !bar.IsMonitorStopConfirmOpen;
            bar.AllowAutoMonitorStart();
            bool notDoubled = fake.StartCount(LaunchKind.Monitor) == 1;

            fake.Last(LaunchKind.Monitor).Exit(1);
            bool scheduled1 = fired.Count == 1 && fired[0].Delay == AutoMonitorRestart.Delay
                              && fake.StartCount(LaunchKind.Monitor) == 1
                              && !bar.CanToggleMonitor
                              && bar.LastResultText.Contains("監視: 終了コード 1", StringComparison.Ordinal)
                              && bar.LastResultText.Contains(DriveControlViewModel.RestartWord,
                                                             StringComparison.Ordinal)
                              && bar.LastResultText.Contains("1 回目 / " + AutoMonitorRestart.MaxAttempts,
                                                             StringComparison.Ordinal);
            now += AutoMonitorRestart.Delay;
            fired[^1].Fire();
            bool restarted1 = fake.StartCount(LaunchKind.Monitor) == 2 && bar.IsMonitorRunning
                              && !bar.LastResultText.Contains(DriveControlViewModel.RestartWord,
                                                              StringComparison.Ordinal);
            now += AutoMonitorRestart.StableAfter;
            fake.Last(LaunchKind.Monitor).Exit(1);
            bool countReset = fired.Count == 2
                              && bar.LastResultText.Contains("1 回目 / " + AutoMonitorRestart.MaxAttempts,
                                                             StringComparison.Ordinal);
            now += AutoMonitorRestart.Delay;
            fired[^1].Fire();
            now += TimeSpan.FromSeconds(5);
            fake.Last(LaunchKind.Monitor).Exit(1);
            bool second = bar.LastResultText.Contains("2 回目 / " + AutoMonitorRestart.MaxAttempts,
                                                      StringComparison.Ordinal);
            now += AutoMonitorRestart.Delay;
            fired[^1].Fire();
            now += TimeSpan.FromSeconds(5);
            fake.Last(LaunchKind.Monitor).Exit(1);
            now += AutoMonitorRestart.Delay;
            fired[^1].Fire();
            bool restarted3 = second && fired.Count == 4
                              && fake.StartCount(LaunchKind.Monitor) == 5
                              && bar.IsMonitorRunning;
            now += TimeSpan.FromSeconds(5);
            fake.Last(LaunchKind.Monitor).Exit(1);
            bool halted = fired.Count == 4
                          && !bar.IsMonitorRunning && bar.CanToggleMonitor && bar.IsMonitorAuto
                          && bar.LastResultText.Contains("監視: 終了コード 1", StringComparison.Ordinal)
                          && bar.LastResultText.Contains(DriveControlViewModel.RestartGaveUpWord,
                                                         StringComparison.Ordinal);
            bar.ToggleMonitorCommand.Execute(null);
            bool manual = fake.StartCount(LaunchKind.Monitor) == 6
                          && bar.IsMonitorRunning && !bar.CanToggleMonitor
                          && !bar.LastResultText.Contains(DriveControlViewModel.RestartGaveUpWord,
                                                          StringComparison.Ordinal);
            shell.Dispose();
            bool noRestartOnDispose = fired.Count == 4 && !bar.IsMonitorRunning;
            return quietBeforeAllow && started && stillRunning && notDoubled
                   && scheduled1 && restarted1 && countReset && restarted3 && halted && manual
                   && noRestartOnDispose;
        }
        finally
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool WatchSyncProbe()
    {
        var dir = Path.Combine(Path.GetTempPath(),
                               "th09_shell_watch_sync_" + Environment.ProcessId);
        var path = Path.Combine(dir, "config.json");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(path,
                "{\"" + ConfigStore.WatchReplaysWithMonitorKey + "\": false}",
                new UTF8Encoding(false));
            AppSettingsSource.ReadFrom(path);
            var fake = new FakeLauncher();
            using var shell = ShellViewModel.Create(fake);
            var bar = shell.Drive;

            bar.ToggleMonitorCommand.Execute(null);
            bool blocked = bar.IsMonitorRunning
                          && fake.StartCount(LaunchKind.Watch) == 0 && !bar.IsWatchRunning;

            File.WriteAllText(path,
                "{\"" + ConfigStore.WatchReplaysWithMonitorKey + "\": true}",
                new UTF8Encoding(false));
            bar.Settings.ReloadCommand.Execute(null);
            bool adoptedStarts = bar.IsMonitorRunning && bar.IsWatchRunning
                                 && fake.StartCount(LaunchKind.Watch) == 1;

            File.WriteAllText(path,
                "{\"" + ConfigStore.WatchReplaysWithMonitorKey + "\": false}",
                new UTF8Encoding(false));
            bar.Settings.ReloadCommand.Execute(null);
            bool adoptedStops = !bar.IsWatchRunning && fake.Last(LaunchKind.Watch).WasStopped;

            bar.ToggleMonitorCommand.Execute(null);
            bool stillBlocked = !bar.IsMonitorRunning && !bar.IsWatchRunning
                               && fake.StartCount(LaunchKind.Watch) == 1;

            return blocked && adoptedStarts && adoptedStops && stillBlocked;
        }
        finally
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool AutoImportProbe()
    {
        var dir = Path.Combine(Path.GetTempPath(),
                               "th09_shell_auto_import_" + Environment.ProcessId);
        try
        {
            Directory.CreateDirectory(dir);
            var dbPath = Path.Combine(dir, "th09_tracker.sqlite3");
            File.WriteAllBytes(dbPath, []);
            var missingDbPath = Path.Combine(dir, "no_such.sqlite3");

            var offConfig = Path.Combine(dir, "config_off.json");
            File.WriteAllText(offConfig, "{}", new UTF8Encoding(false));
            AppSettingsSource.ReadFrom(offConfig);
            var fake = new FakeLauncher();
            using var shell = ShellViewModel.Create(fake);
            var bar = shell.Drive;
            AddDirectoryConfirmed(bar, @"C:\synthetic_gate\replay");
            bar.AutoImportExisting(dbPath);
            bool defaultOff = fake.StartCount(LaunchKind.ImportOnly) == 0;

            var onConfig = Path.Combine(dir, "config_on.json");
            File.WriteAllText(onConfig,
                "{\"" + ConfigStore.AutoImportOnStartKey + "\": true}", new UTF8Encoding(false));
            AppSettingsSource.ReadFrom(onConfig);
            bar.AutoImportExisting(missingDbPath);
            bool noDbNoStart = fake.StartCount(LaunchKind.ImportOnly) == 0;

            bar.AutoImportExisting(dbPath);
            bool onStarts = fake.StartCount(LaunchKind.ImportOnly) == 1
                           && !bar.IsImportConfirmOpen && !bar.IsScanFormOpen;
            fake.Last(LaunchKind.ImportOnly).Exit(0);

            return defaultOff && noDbNoStart && onStarts;
        }
        finally
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool ScanStopsMonitorProbe()
    {
        var dir = Path.Combine(Path.GetTempPath(),
                               "th09_shell_scan_stops_monitor_" + Environment.ProcessId);
        try
        {
            Directory.CreateDirectory(dir);
            AppSettingsSource.ReadFrom(Path.Combine(dir, "config.json"));
            var fake = new FakeLauncher();
            using var shell = ShellViewModel.Create(fake);
            var bar = shell.Drive;
            AddDirectoryConfirmed(bar, @"C:\synthetic_gate\replay");

            bar.ToggleMonitorCommand.Execute(null);
            bool monitorOn = bar.IsMonitorRunning && fake.StartCount(LaunchKind.Monitor) == 1;

            bar.OpenScanCommand.Execute(null);
            bar.RequestScanRunCommand.Execute(null);
            bool noteShown = bar.ScanConfirmSummary.Contains(DriveControlViewModel.ScanStopsMonitorNote,
                                                             StringComparison.Ordinal);
            bar.ConfirmScanCommand.Execute(null);
            bool stoppedForScan = fake.Last(LaunchKind.Monitor).WasStopped && !bar.IsMonitorRunning
                                 && fake.StartCount(LaunchKind.Scan) == 1;
            bool blockedWhileScanning = !bar.CanToggleMonitor
                                       && bar.MonitorText
                                          == "監視: " + DriveControlViewModel.ScanBlocksMonitorWord;
            bar.ToggleMonitorCommand.Execute(null);
            bool toggleIgnoredDuringScan = !bar.IsMonitorRunning
                                           && fake.StartCount(LaunchKind.Monitor) == 1;

            fake.Last(LaunchKind.Scan).Exit(0);
            bool manualStaysOffAfterScan = !bar.IsMonitorRunning && bar.CanToggleMonitor
                                           && fake.StartCount(LaunchKind.Monitor) == 1;

            bar.ToggleMonitorCommand.Execute(null);
            bool monitorOnAgain = bar.IsMonitorRunning && fake.StartCount(LaunchKind.Monitor) == 2;
            bar.OpenScanCommand.Execute(null);
            bar.PlanScanCommand.Execute(null);
            bool planKeepsMonitor = bar.IsMonitorRunning
                                    && !fake.Last(LaunchKind.Monitor).WasStopped
                                    && fake.StartCount(LaunchKind.Scan) == 2;
            fake.Last(LaunchKind.Scan).Exit(0);

            return monitorOn && noteShown && stoppedForScan && blockedWhileScanning
                   && toggleIgnoredDuringScan && manualStaysOffAfterScan
                   && monitorOnAgain && planKeepsMonitor;
        }
        finally
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool DropFirstProbe()
    {
        var dir = Path.Combine(Path.GetTempPath(),
                               "th09_shell_drop_first_" + Environment.ProcessId);
        try
        {
            Directory.CreateDirectory(dir);
            AppSettingsSource.ReadFrom(Path.Combine(dir, "config.json"));
            var fake = new FakeLauncher();
            using var shell = ShellViewModel.Create(fake);
            var bar = shell.Drive;
            AddDirectoryConfirmed(bar, @"C:\synthetic_gate\replay");

            var offerLine = ScanProgressLines.BackupDropFirstOfferLine("4.7 GiB", "4.0 GiB", 1, "4.3 GiB");
            const string errorLine = "エラー: 控えの置き先に空きが足りません（要 4.7 GiB / 空き 4.0 GiB）。";

            bar.OpenScanCommand.Execute(null);
            bar.RequestScanRunCommand.Execute(null);
            bar.ConfirmScanCommand.Execute(null);
            var first = fake.LastOptions(LaunchKind.Scan)?.Scan;
            bool firstHasNoFlag = first is { DropOldBackupsFirst: false, DryRun: false };
            fake.Last(LaunchKind.Scan).Emit(offerLine);
            fake.Last(LaunchKind.Scan).Emit(errorLine);
            bool openedBeforeExit = bar.IsDropFirstConfirmOpen;
            fake.Last(LaunchKind.Scan).Exit(1);
            bool opened = bar.IsDropFirstConfirmOpen && !openedBeforeExit
                          && DriveControlViewModel.DropFirstConfirmTitle == "バックアップの空きが足りません"
                          && bar.DropFirstConfirmText.Contains("4.7 GiB", StringComparison.Ordinal)
                          && bar.DropFirstConfirmText.Contains("4.0 GiB", StringComparison.Ordinal)
                          && bar.DropFirstConfirmText.Contains("1 件 / 4.3 GiB", StringComparison.Ordinal);

            bar.CancelDropFirstCommand.Execute(null);
            bool noKeepsQuiet = !bar.IsDropFirstConfirmOpen && fake.StartCount(LaunchKind.Scan) == 1;

            bar.OpenScanCommand.Execute(null);
            bar.RequestScanRunCommand.Execute(null);
            bar.ConfirmScanCommand.Execute(null);
            fake.Last(LaunchKind.Scan).Emit(offerLine);
            fake.Last(LaunchKind.Scan).Emit(errorLine);
            fake.Last(LaunchKind.Scan).Exit(1);
            bool openedAgain = bar.IsDropFirstConfirmOpen && fake.StartCount(LaunchKind.Scan) == 2;
            var beforeYes = fake.LastOptions(LaunchKind.Scan)?.Scan;
            bar.ConfirmDropFirstCommand.Execute(null);
            var afterYes = fake.LastOptions(LaunchKind.Scan)?.Scan;
            bool yesStarts = !bar.IsDropFirstConfirmOpen && fake.StartCount(LaunchKind.Scan) == 3
                             && bar.IsScanRunning
                             && afterYes is { DropOldBackupsFirst: true, DryRun: false }
                             && beforeYes is not null
                             && afterYes == (beforeYes with { DropOldBackupsFirst = true });

            fake.Last(LaunchKind.Scan).Emit(errorLine);
            fake.Last(LaunchKind.Scan).Exit(1);
            bool noLoop = !bar.IsDropFirstConfirmOpen && fake.StartCount(LaunchKind.Scan) == 3;

            bar.OpenScanCommand.Execute(null);
            bar.RequestScanRunCommand.Execute(null);
            bar.ConfirmScanCommand.Execute(null);
            fake.Last(LaunchKind.Scan).Emit(errorLine);
            fake.Last(LaunchKind.Scan).Exit(1);
            bool plainFailure = !bar.IsDropFirstConfirmOpen && fake.StartCount(LaunchKind.Scan) == 4;

            bar.OpenScanCommand.Execute(null);
            bar.RequestScanRunCommand.Execute(null);
            bar.ConfirmScanCommand.Execute(null);
            fake.Last(LaunchKind.Scan).Emit(offerLine);
            fake.Last(LaunchKind.Scan).Exit(0);
            bool zeroExit = !bar.IsDropFirstConfirmOpen && fake.StartCount(LaunchKind.Scan) == 5;

            bar.OpenScanCommand.Execute(null);
            bar.PlanScanCommand.Execute(null);
            fake.Last(LaunchKind.Scan).Emit(offerLine);
            fake.Last(LaunchKind.Scan).Exit(1);
            bool dryRunQuiet = !bar.IsDropFirstConfirmOpen && fake.StartCount(LaunchKind.Scan) == 6
                               && fake.LastOptions(LaunchKind.Scan)?.Scan is { DryRun: true };
            bool dryRunKeepsLast = true;
            bar.OpenScanCommand.Execute(null);
            bar.RequestScanRunCommand.Execute(null);
            bar.ConfirmScanCommand.Execute(null);
            fake.Last(LaunchKind.Scan).Emit(offerLine);
            fake.Last(LaunchKind.Scan).Exit(1);
            bar.ConfirmDropFirstCommand.Execute(null);
            dryRunKeepsLast = fake.LastOptions(LaunchKind.Scan)?.Scan is
                { DropOldBackupsFirst: true, DryRun: false };
            fake.Last(LaunchKind.Scan).Exit(0);

            return firstHasNoFlag && opened && noKeepsQuiet && openedAgain && yesStarts && noLoop
                   && plainFailure && zeroExit && dryRunQuiet && dryRunKeepsLast;
        }
        finally
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool DropFirstLineProbe()
    {
        var line = ScanProgressLines.BackupDropFirstOfferLine("4.7 GiB", "4.0 GiB", 3, "12.5 KiB");
        bool roundTrip = ScanProgressLines.TryParseBackupDropFirstOffer(line, out var o)
                         && o.Need == "4.7 GiB" && o.Free == "4.0 GiB" && o.OldCount == 3
                         && o.OldSize == "12.5 KiB";
        bool stamped = ScanProgressLines.TryParseBackupDropFirstOffer("[22:25:03] " + line, out var s)
                       && s.OldCount == 3 && s.Need == "4.7 GiB";
        string[] bad =
        [
            "",
            "エラー: 控えの置き先に空きが足りません（要 4.7 GiB / 空き 4.0 GiB）。",
            "控え空き不足: 要 4.7 GiB / 空き 4.0 GiB / 旧控え 1 件 4.3 GiB",
            "控え空き不足: 要  / 空き 4.0 GiB / 旧控え 1 件 4.3 GiB ／ 先に消せば入ります",
            "控え空き不足: 要 4.7 GiB / 空き  / 旧控え 1 件 4.3 GiB ／ 先に消せば入ります",
            "控え空き不足: 要 4.7 GiB / 空き 4.0 GiB / 旧控え x 件 4.3 GiB ／ 先に消せば入ります",
            "控え空き不足: 要 4.7 GiB / 空き 4.0 GiB / 旧控え 1 件  ／ 先に消せば入ります",
        ];
        bool negatives = bad.All(b => !ScanProgressLines.TryParseBackupDropFirstOffer(b, out _));
        bool nullSafe = !ScanProgressLines.TryParseBackupDropFirstOffer(null, out _);
        return roundTrip && stamped && negatives && nullSafe;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool ScanStopsAutoMonitorProbe()
    {
        var dir = Path.Combine(Path.GetTempPath(),
                               "th09_shell_scan_stops_auto_" + Environment.ProcessId);
        var path = Path.Combine(dir, "config.json");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(path, "{\"" + ConfigStore.AutoMonitorOnGameKey + "\": true}",
                              new UTF8Encoding(false));
            AppSettingsSource.ReadFrom(path);
            var fake = new FakeLauncher();
            using var shell = ShellViewModel.Create(fake);
            var bar = shell.Drive;
            AddDirectoryConfirmed(bar, @"C:\synthetic_gate\replay");
            bar.AllowAutoMonitorStart();
            bool autoStarted = fake.StartCount(LaunchKind.Monitor) == 1 && bar.IsMonitorRunning;

            bar.OpenScanCommand.Execute(null);
            bar.RequestScanRunCommand.Execute(null);
            bar.ConfirmScanCommand.Execute(null);
            bool stoppedForScan = fake.Last(LaunchKind.Monitor).WasStopped && !bar.IsMonitorRunning
                                 && fake.StartCount(LaunchKind.Scan) == 1;

            fake.Last(LaunchKind.Scan).Exit(0);
            bool notYetRestarted = fake.StartCount(LaunchKind.Monitor) == 1 && !bar.IsMonitorRunning;
            Dispatcher.UIThread.RunJobs();
            bool restarted = fake.StartCount(LaunchKind.Monitor) == 2 && bar.IsMonitorRunning;

            fake.Last(LaunchKind.Monitor).NotifyExitOffThread = true;
            bar.OpenScanCommand.Execute(null);
            bar.RequestScanRunCommand.Execute(null);
            bar.ConfirmScanCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            bool lateNoticeKeepsOff = fake.StartCount(LaunchKind.Monitor) == 2 && !bar.IsMonitorRunning
                                      && fake.StartCount(LaunchKind.Scan) == 2 && bar.IsScanRunning;
            fake.Last(LaunchKind.Scan).Exit(0);
            Dispatcher.UIThread.RunJobs();
            bool lateNoticeRestarted = fake.StartCount(LaunchKind.Monitor) == 3 && bar.IsMonitorRunning;

            var fired = new List<Action>();
            bar.RestartScheduler = (_, action) =>
            {
                fired.Add(action);
                return new NoopDisposable();
            };
            fake.Last(LaunchKind.Monitor).Exit(1);
            bool restartScheduled = fired.Count == 1 && !bar.IsMonitorRunning;
            bar.OpenScanCommand.Execute(null);
            bar.RequestScanRunCommand.Execute(null);
            bar.ConfirmScanCommand.Execute(null);
            if (fired.Count > 0) fired[0]();
            Dispatcher.UIThread.RunJobs();
            bool pendingWaitsForScan = fake.StartCount(LaunchKind.Monitor) == 3 && !bar.IsMonitorRunning
                                       && fake.StartCount(LaunchKind.Scan) == 3 && bar.IsScanRunning;
            fake.Last(LaunchKind.Scan).Exit(0);
            Dispatcher.UIThread.RunJobs();
            bool pendingRestartedAfterScan = fake.StartCount(LaunchKind.Monitor) == 4 && bar.IsMonitorRunning;

            return autoStarted && stoppedForScan && notYetRestarted && restarted
                   && lateNoticeKeepsOff && lateNoticeRestarted
                   && restartScheduled && pendingWaitsForScan && pendingRestartedAfterScan;
        }
        finally
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool FoldersRequiredProbe()
    {
        var dir = Path.Combine(Path.GetTempPath(),
                               "th09_shell_folders_required_" + Environment.ProcessId);
        try
        {
            Directory.CreateDirectory(dir);
            var dbPath = Path.Combine(dir, "th09_tracker.sqlite3");
            File.WriteAllBytes(dbPath, []);
            var onConfig = Path.Combine(dir, "config.json");
            File.WriteAllText(onConfig,
                "{\"" + ConfigStore.AutoImportOnStartKey + "\": true}", new UTF8Encoding(false));
            AppSettingsSource.ReadFrom(onConfig);

            var fake = new FakeLauncher();
            using var shell = ShellViewModel.Create(fake);
            var bar = shell.Drive;

            bar.RequestImportCommand.Execute(null);
            bar.ConfirmImportCommand.Execute(null);
            bool importRefusedEmpty = fake.StartCount(LaunchKind.ImportOnly) == 0
                && bar.LastResultText.Contains(DriveControlViewModel.NoFoldersSelectedMessage,
                                               StringComparison.Ordinal);

            bar.AutoImportExisting(dbPath);
            bool autoImportRefusedEmpty = fake.StartCount(LaunchKind.ImportOnly) == 0;

            bar.OpenScanCommand.Execute(null);
            bar.RequestScanRunCommand.Execute(null);
            bar.ConfirmScanCommand.Execute(null);
            bool scanRefusedEmpty = fake.StartCount(LaunchKind.Scan) == 0
                && bar.LastResultText.Contains(DriveControlViewModel.NoFoldersSelectedMessage,
                                               StringComparison.Ordinal);

            bar.PlanScanCommand.Execute(null);
            bool planRefusedEmpty = fake.StartCount(LaunchKind.Scan) == 0
                && bar.IsScanPlanOpen && bar.ScanPlanFailed
                && bar.ScanPlanFailureText == DriveControlViewModel.NoFoldersSelectedMessage;
            bar.CloseScanPlanCommand.Execute(null);

            var gameDirForRequired = Path.Combine(dir, "game");
            Directory.CreateDirectory(gameDirForRequired);
            Paths.Resolve(dir).TryWriteGameDir(gameDirForRequired);
            var inRangeDir = Path.Combine(gameDirForRequired, "replay");
            bar.Scan.AddDirectory(inRangeDir);
            bool addedDirectoryWithoutConfirm = !bar.Scan.IsOutOfRangeConfirmOpen
                && bar.Scan.Directories.Contains(inRangeDir);

            bar.RequestImportCommand.Execute(null);
            bar.ConfirmImportCommand.Execute(null);
            bool importStartsWithDir = fake.StartCount(LaunchKind.ImportOnly) == 1;
            fake.Last(LaunchKind.ImportOnly).Exit(0);

            bar.AutoImportExisting(dbPath);
            bool autoImportStartsWithDir = fake.StartCount(LaunchKind.ImportOnly) == 2;
            fake.Last(LaunchKind.ImportOnly).Exit(0);

            bar.OpenScanCommand.Execute(null);
            bar.RequestScanRunCommand.Execute(null);
            bar.ConfirmScanCommand.Execute(null);
            bool scanStartsWithDir = fake.StartCount(LaunchKind.Scan) == 1;
            fake.Last(LaunchKind.Scan).Exit(0);

            bar.PlanScanCommand.Execute(null);
            bool planStartsWithDir = fake.StartCount(LaunchKind.Scan) == 2;
            fake.Last(LaunchKind.Scan).Exit(0);

            return importRefusedEmpty && autoImportRefusedEmpty && scanRefusedEmpty
                   && planRefusedEmpty && addedDirectoryWithoutConfirm
                   && importStartsWithDir && autoImportStartsWithDir
                   && scanStartsWithDir && planStartsWithDir;
        }
        finally
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool ScanDirsPersistProbe()
    {
        var dir = Path.Combine(Path.GetTempPath(),
                               "th09_shell_scan_dirs_persist_" + Environment.ProcessId);
        var path = Path.Combine(dir, "config.json");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(path, "{}", new UTF8Encoding(false));
            AppSettingsSource.ReadFrom(path);
            var fake = new FakeLauncher();
            bool wroteOnAdd, wroteOnRemove, settingsSaveKeepsDirs;
            using (var shell = ShellViewModel.Create(fake))
            {
                var bar = shell.Drive;
                AddDirectoryConfirmed(bar, @"C:\th09\replay_a");
                AddDirectoryConfirmed(bar, @"D:\別のフォルダ");
                bar.Scan.NoRecurse = true;
                var afterAdd = ConfigStore.Load(path);
                wroteOnAdd = afterAdd.ScanDirs.Count == 2
                            && afterAdd.ScanDirs[0] == @"C:\th09\replay_a"
                            && afterAdd.ScanDirs[1] == @"D:\別のフォルダ"
                            && afterAdd.ScanNoRecurse;

                bar.Scan.SelectedDirectory = bar.Scan.Directories[0];
                bar.Scan.RemoveDirectoryCommand.Execute(null);
                var afterRemove = ConfigStore.Load(path);
                wroteOnRemove = afterRemove.ScanDirs.Count == 1
                               && afterRemove.ScanDirs[0] == @"D:\別のフォルダ";

                bar.Settings.HistoryRetentionAbortedText = "5";
                bar.Settings.SaveCommand.Execute(null);
                var afterSettingsSave = ConfigStore.Load(path);
                settingsSaveKeepsDirs = afterSettingsSave.ScanDirs.Count == 1
                                       && afterSettingsSave.ScanDirs[0] == @"D:\別のフォルダ"
                                       && afterSettingsSave.ScanNoRecurse
                                       && afterSettingsSave.HistoryRetentionAborted == 5;
            }
            using var shell2 = ShellViewModel.Create(fake);
            bool reloadsOnRestart = shell2.Drive.Scan.Directories.Count == 1
                                    && shell2.Drive.Scan.Directories[0] == @"D:\別のフォルダ"
                                    && shell2.Drive.Scan.NoRecurse;

            return wroteOnAdd && wroteOnRemove && settingsSaveKeepsDirs && reloadsOnRestart;
        }
        finally
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool OutOfRangeConfirmProbe()
    {
        var dir = Path.Combine(Path.GetTempPath(),
                               "th09_shell_out_of_range_" + Environment.ProcessId);
        try
        {
            Directory.CreateDirectory(dir);
            var configPath = Path.Combine(dir, "config.json");
            File.WriteAllText(configPath,
                "{\"" + ConfigStore.AutoImportOnStartKey + "\": true}", new UTF8Encoding(false));
            AppSettingsSource.ReadFrom(configPath);

            var paths = Paths.Resolve(dir);
            var gameDir = Path.Combine(dir, "game");
            Directory.CreateDirectory(gameDir);
            paths.TryWriteGameDir(gameDir);
            var insideDir = Path.Combine(gameDir, "replay_inside");
            var outsideDir = Path.Combine(Path.GetTempPath(),
                "th09_shell_out_of_range_outside_" + Environment.ProcessId);
            var outsideDirForGuard = outsideDir + "_guard";

            var dbPath = Path.Combine(dir, "th09_tracker.sqlite3");
            File.WriteAllBytes(dbPath, []);

            var fake = new FakeLauncher();
            bool noConfirmOnInside, confirmOpensOnOutside, cancelKeepsNothing, confirmAddsAndWrites;
            bool settingsSeesAddedExtraDir, settingsSaveKeepsAddedExtraDir;
            using (var shell = ShellViewModel.Create(fake))
            {
                var bar = shell.Drive;

                bar.Scan.AddDirectory(insideDir);
                noConfirmOnInside = !bar.Scan.IsOutOfRangeConfirmOpen
                    && bar.Scan.Directories.Contains(insideDir);
                bar.Scan.SelectedDirectory = insideDir;
                bar.Scan.RemoveDirectoryCommand.Execute(null);

                bar.Scan.AddDirectory(outsideDir);
                confirmOpensOnOutside = bar.Scan.IsOutOfRangeConfirmOpen
                    && string.Equals(bar.Scan.OutOfRangeConfirmPathText, outsideDir,
                                     StringComparison.OrdinalIgnoreCase)
                    && !bar.Scan.Directories.Contains(outsideDir);

                bar.Scan.CancelAddOutOfRangeCommand.Execute(null);
                cancelKeepsNothing = !bar.Scan.IsOutOfRangeConfirmOpen
                    && !bar.Scan.Directories.Contains(outsideDir)
                    && paths.ReplayExtraDirsRaw().Count == 0;

                bar.Scan.AddDirectory(outsideDir);
                bar.Scan.ConfirmAddOutOfRangeCommand.Execute(null);
                var extra = paths.ReplayExtraDirsRaw();
                confirmAddsAndWrites = !bar.Scan.IsOutOfRangeConfirmOpen
                    && bar.Scan.Directories.Contains(outsideDir)
                    && extra.Count == 1
                    && string.Equals(extra[0], outsideDir, StringComparison.OrdinalIgnoreCase);

                bar.OpenSettingsCommand.Execute(null);
                settingsSeesAddedExtraDir = bar.Settings.ExtraReplayDirs.Contains(outsideDir);
                bar.Settings.SaveCommand.Execute(null);
                var extraAfterSettingsSave = paths.ReplayExtraDirsRaw();
                settingsSaveKeepsAddedExtraDir = extraAfterSettingsSave.Count == 1
                    && string.Equals(extraAfterSettingsSave[0], outsideDir,
                                     StringComparison.OrdinalIgnoreCase);
                bar.CloseSettingsCommand.Execute(null);

                bar.Scan.SelectedDirectory = outsideDir;
                bar.Scan.RemoveDirectoryCommand.Execute(null);
            }

            bool importRefused, autoImportRefused, scanRefused, planRefused;
            using (var shell2 = ShellViewModel.Create(fake))
            {
                var bar = shell2.Drive;
                bar.Scan.Directories.Add(outsideDirForGuard);

                bar.RequestImportCommand.Execute(null);
                bar.ConfirmImportCommand.Execute(null);
                importRefused = fake.StartCount(LaunchKind.ImportOnly) == 0
                    && bar.LastResultText.Contains(outsideDirForGuard, StringComparison.Ordinal);

                bar.AutoImportExisting(dbPath);
                autoImportRefused = fake.StartCount(LaunchKind.ImportOnly) == 0;

                bar.OpenScanCommand.Execute(null);
                bar.RequestScanRunCommand.Execute(null);
                bar.ConfirmScanCommand.Execute(null);
                scanRefused = fake.StartCount(LaunchKind.Scan) == 0
                    && bar.LastResultText.Contains(outsideDirForGuard, StringComparison.Ordinal);

                bar.PlanScanCommand.Execute(null);
                planRefused = fake.StartCount(LaunchKind.Scan) == 0
                    && bar.IsScanPlanOpen && bar.ScanPlanFailed
                    && bar.ScanPlanFailureText.Contains(outsideDirForGuard, StringComparison.Ordinal);
                bar.CloseScanPlanCommand.Execute(null);
            }

            return noConfirmOnInside && confirmOpensOnOutside && cancelKeepsNothing
                   && confirmAddsAndWrites && settingsSeesAddedExtraDir
                   && settingsSaveKeepsAddedExtraDir && importRefused && autoImportRefused
                   && scanRefused && planRefused;
        }
        finally
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static void AddDirectoryConfirmed(DriveControlViewModel bar, string dir)
    {
        bar.Scan.AddDirectory(dir);
        if (bar.Scan.IsOutOfRangeConfirmOpen) bar.Scan.ConfirmAddOutOfRangeCommand.Execute(null);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static int RunDbUpdateReload(string dbBefore, string dbAfter, string dbGone)
    {
        using var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
        var dir = Path.Combine(Path.GetTempPath(), "th09_shell_db_update_reload_" + Environment.ProcessId);
        try
        {
            foreach (var p in new[] { dbBefore, dbAfter, dbGone })
            {
                if (HistoryMaintenance.PointsAtRealData(p, null))
                {
                    Console.Error.WriteLine(
                        "本物の記録の場所を指しています（本体 DB のフォルダ）。"
                        + "この口は合成の DB だけを受け付けます: " + p);
                    return 3;
                }
                if (!File.Exists(p))
                {
                    Console.Error.WriteLine("DB が在りません: " + p);
                    return 2;
                }
            }

            AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();
            Directory.CreateDirectory(dir);
            var config = Path.Combine(dir, "config.json");
            File.WriteAllText(config, "{\"" + ConfigStore.WatchReplaysWithMonitorKey + "\": true, \""
                                      + ConfigStore.AutoImportOnStartKey + "\": true}");
            AppSettingsSource.ReadFrom(config);
            LogSource.Clear();
            TrackerDb.MainDbPath = dbBefore;
            var noLayer0 = Path.Combine(dir, "no_layer0.sqlite3");
            TrackerDb.Layer0DbPath = noLayer0;

            var fake = new FakeLauncher();
            using var shell = ShellViewModel.Create(fake);
            var bar = shell.Drive;
            AddDirectoryConfirmed(bar, @"C:\synthetic_gate\replay");
            var replay = shell.Tabs.OfType<ReplayTabViewModel>().Single();
            var stats = shell.Tabs.OfType<StatsTabViewModel>().Single();
            var history = shell.Tabs.OfType<HistoryTabViewModel>().Single();
            var detail = shell.Detail;

            string Ids() => string.Join(",", replay.Rows.Select(r => r.ReplayId));
            string Crumbs() => string.Join(">", stats.Crumbs.Select(c => c.Label));
            void Settle()
            {
                replay.ReloadReading?.Wait();
                stats.ReloadReading?.Wait();
                detail.ReloadReading?.Wait();
                Dispatcher.UIThread.RunJobs();
            }
            void StartScanRun()
            {
                bar.OpenScanCommand.Execute(null);
                bar.RequestScanRunCommand.Execute(null);
                bar.ConfirmScanCommand.Execute(null);
            }
            void FinishScanRun(int exitCode)
            {
                fake.Last(LaunchKind.Scan).Exit(exitCode);
                Settle();
            }

            replay.OwnOnly = true;
            replay.SelectedCharacter = replay.CharacterOptions.First(o => o.Value == 3);
            replay.SortByCommand.Execute(ReplaySortKey.DateTime);
            stats.ShowSectionCommand.Execute(StatsSection.MatchCpu);
            stats.IncludeForeign = true;
            foreach (var pick in new[] { "1", "3" })
            {
                stats.SelectedGroup = stats.Groups.FirstOrDefault(g => g.Key == pick);
                Dispatcher.UIThread.RunJobs();
            }
            var crumbsBefore = Crumbs();
            var historyBefore = stats.HistoryCount;
            var idsBefore = Ids();
            var countBefore = stats.CountText;
            bool positioned = replay.OwnOnly && replay.SelectedCharacter?.Value == 3
                              && replay.SortKey == ReplaySortKey.DateTime && !replay.SortDescending
                              && stats.Crumbs.Count >= 3 && stats.HistoryCount >= 3
                              && stats.Section == StatsSection.MatchCpu && stats.IncludeForeign;
            WriteText(stdout, "rows-before", replay.Rows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            WriteText(stdout, "replay-ids-before", idsBefore);
            WriteText(stdout, "stats-count-before", countBefore);
            WriteText(stdout, "stats-crumbs-before", crumbsBefore);
            Write(stdout, "positioned", positioned);

            TrackerDb.MainDbPath = dbAfter;
            StartScanRun();
            Settle();
            Write(stdout, "not-reloaded-while-running",
                  bar.IsScanRunning && Ids() == idsBefore && stats.CountText == countBefore);

            FinishScanRun(0);
            var idsAfter = Ids();
            var countAfter = stats.CountText;
            WriteText(stdout, "replay-ids-after-scan", idsAfter);
            WriteText(stdout, "stats-count-after-scan", countAfter);
            Write(stdout, "replay-reloaded", idsAfter != idsBefore && string.IsNullOrEmpty(replay.StatusText));
            Write(stdout, "stats-reloaded", countAfter != countBefore && string.IsNullOrEmpty(stats.StatusText));
            Write(stdout, "replay-position-kept",
                  replay.OwnOnly && replay.SelectedCharacter?.Value == 3 && !replay.IsMatch
                  && replay.SortKey == ReplaySortKey.DateTime && !replay.SortDescending);
            Write(stdout, "stats-position-kept",
                  stats.Section == StatsSection.MatchCpu && stats.IncludeForeign
                  && Crumbs() == crumbsBefore && stats.HistoryCount == historyBefore
                  && stats.Crumbs.Count >= 3);
            WriteText(stdout, "stats-crumbs-after-scan", Crumbs());

            TrackerDb.MainDbPath = dbGone;
            bar.OpenScanCommand.Execute(null);
            bar.PlanScanCommand.Execute(null);
            FinishScanRun(0);
            bool planKeeps = Ids() == idsAfter && stats.CountText == countAfter;
            bar.CloseScanPlanCommand.Execute(null);
            Write(stdout, "plan-does-not-reload", planKeeps);

            StartScanRun();
            FinishScanRun(1);
            var idsGone = Ids();
            WriteText(stdout, "replay-ids-after-failure", idsGone);
            WriteText(stdout, "stats-count-after-failure", stats.CountText);
            Write(stdout, "failed-scan-reloads", idsGone != idsAfter && stats.CountText != countAfter);
            Write(stdout, "replay-filter-kept-on-failure",
                  replay.OwnOnly && replay.SelectedCharacter?.Value == 3 && !replay.SortDescending);
            Write(stdout, "stats-vanished-branch-resets",
                  stats.Crumbs.Count == 1 && stats.HistoryCount == 1
                  && stats.Section == StatsSection.MatchCpu && stats.IncludeForeign
                  && string.IsNullOrEmpty(stats.StatusText));

            var missing = Path.Combine(dir, "no_such.sqlite3");
            TrackerDb.MainDbPath = missing;
            StartScanRun();
            FinishScanRun(0);
            Write(stdout, "missing-db-noop",
                  Ids() == idsGone && string.IsNullOrEmpty(replay.StatusText)
                  && string.IsNullOrEmpty(stats.StatusText));

            var bad = Path.Combine(dir, "bad.sqlite3");
            File.WriteAllText(bad, "これは SQLite ではありません");
            TrackerDb.MainDbPath = bad;
            StartScanRun();
            FinishScanRun(0);
            Write(stdout, "unreadable-keeps-rows", Ids() == idsGone && replay.Rows.Count > 0);
            Write(stdout, "unreadable-says-why",
                  replay.StatusText is { } rs && rs.StartsWith("読み直しに失敗", StringComparison.Ordinal)
                  && stats.StatusText is { } ss && ss.StartsWith("読み直しに失敗", StringComparison.Ordinal));

            void Swap(string db) => TrackerDb.MainDbPath = db;
            void ReloadViaScan(string db, bool detailOpen = false)
            {
                Swap(db);
                StartScanRun();
                shell.IsDetailOpen = detailOpen;
                FinishScanRun(0);
            }
            string Count() => stats.CountText;

            ReloadViaScan(dbBefore);
            stats.ShowSectionCommand.Execute(StatsSection.MatchCpu);
            foreach (var pick in new[] { "1", "3" })
            {
                stats.SelectedGroup = stats.Groups.FirstOrDefault(g => g.Key == pick);
                Dispatcher.UIThread.RunJobs();
            }
            stats.GoToDepthCommand.Execute(1);
            var histBefore = stats.HistoryCount;
            bool histBuilt = histBefore >= 4 && stats.CurrentPath.Count == 1 && stats.CurrentPath[0] == "1";
            WriteText(stdout, "stats-history-before", histBefore.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write(stdout, "stats-history-built", histBuilt);
            ReloadViaScan(dbGone);
            WriteText(stdout, "stats-history-after-gone", stats.HistoryCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write(stdout, "stats-history-drops-dead-step",
                  histBuilt && stats.HistoryCount == 2 && stats.CurrentPath.Count == 1
                  && stats.CurrentPath[0] == "1" && string.IsNullOrEmpty(stats.StatusText));
            Write(stdout, "stats-history-back-goes-to-head",
                  stats.CanGoBack && !stats.CanGoForward && GoBackAndPathIsEmpty(stats));
            stats.GoForwardCommand.Execute(null);
            var histAlive = stats.HistoryCount;
            ReloadViaScan(dbAfter);
            Write(stdout, "stats-history-intact-when-all-alive",
                  histAlive == 2 && stats.HistoryCount == histAlive && stats.CurrentPath.Count == 1);
            stats.ShowSectionCommand.Execute(StatsSection.MatchCpu);
            stats.GoToDepthCommand.Execute(0);

            ReloadViaScan(dbAfter);
            var idsAfterBase = Ids();
            var countAfterBase = Count();
            Swap(dbBefore);
            bar.RequestImportCommand.Execute(null);
            bar.ConfirmImportCommand.Execute(null);
            Settle();
            Write(stdout, "import-not-reloaded-while-running",
                  bar.IsImportRunning && Ids() == idsAfterBase && Count() == countAfterBase);
            fake.Last(LaunchKind.ImportOnly).Exit(0);
            Settle();
            var idsImported = Ids();
            WriteText(stdout, "replay-ids-after-import", idsImported);
            Write(stdout, "import-end-reloads", idsImported != idsAfterBase && Count() != countAfterBase
                                                && string.IsNullOrEmpty(replay.StatusText));
            Swap(dbAfter);
            bar.AutoImportExisting(TrackerDb.MainDbPath);
            bool autoStarted = bar.IsImportRunning;
            fake.Last(LaunchKind.ImportOnly).Exit(0);
            Settle();
            var idsAutoImported = Ids();
            WriteText(stdout, "replay-ids-after-auto-import", idsAutoImported);
            Write(stdout, "auto-import-end-reloads",
                  autoStarted && idsAutoImported != idsImported && Count() == countAfterBase);

            Swap(dbBefore);
            bar.ToggleMonitorCommand.Execute(null);
            var monitor = fake.Last(LaunchKind.Monitor);
            monitor.Emit(MonitorLines.GameFound(4321));
            monitor.Emit(MonitorLines.SessionOpenedPrefix + "30 (…)");
            monitor.Emit("記録中: tick 1,200 を書きました");
            monitor.Emit("記録中: tick 2,400 を書きました");
            Settle();
            Write(stdout, "play-not-reloaded-while-open",
                  bar.IsMonitorSessionOpen && Ids() == idsAutoImported && Count() == countAfterBase);
            monitor.Emit(MonitorLines.SessionClosed(30));
            Settle();
            var idsPlayed = Ids();
            WriteText(stdout, "replay-ids-after-play", idsPlayed);
            Write(stdout, "play-end-reloads", idsPlayed != idsAutoImported && Count() != countAfterBase);

            Swap(dbAfter);
            monitor.Emit(MonitorLines.GameGone);
            Settle();
            Write(stdout, "game-gone-without-session-noop", Ids() == idsPlayed);
            monitor.Emit(MonitorLines.SessionOpenedPrefix + "31 (…)");
            monitor.Emit(MonitorLines.GameGone);
            Settle();
            Write(stdout, "game-gone-closes-and-reloads", Ids() == idsAutoImported);

            bool watchRunning = fake.StartCount(LaunchKind.Watch) == 1 && bar.IsWatchRunning;
            var watch = fake.Last(LaunchKind.Watch);
            Swap(dbBefore);
            watch.Emit(@"保留: C:\synthetic_gate\replay\th9_01.rpy InvalidDataException: broken");
            Settle();
            Write(stdout, "watch-held-noop", watchRunning && Ids() == idsAutoImported);
            monitor.Emit(@"登録: C:\synthetic_gate\replay\th9_01.rpy [decoded]");
            Settle();
            Write(stdout, "registered-line-from-monitor-noop", Ids() == idsAutoImported);
            watch.Emit(@"登録: C:\synthetic_gate\replay\th9_01.rpy [decoded]");
            Settle();
            Write(stdout, "watch-registered-reloads", Ids() == idsPlayed);
            bar.ToggleMonitorCommand.Execute(null);
            Settle();

            ReloadViaScan(dbBefore);
            detail.Show(ReplayDetailRequest.FromSession(2));
            shell.IsDetailOpen = true;
            detail.ShowPageCommand.Execute(DetailPage.Files);
            var headingBefore = detail.Heading;
            var pageHistoryBefore = (detail.CanGoBackInDetail, detail.CanGoForwardInDetail);
            WriteText(stdout, "detail-heading-before", headingBefore);
            WriteText(stdout, "detail-state-before",
                      "header=" + detail.HasHeader + " page=" + detail.Page + " back=" + detail.CanGoBackInDetail
                      + " status=" + (detail.StatusText ?? "(null)"));
            Write(stdout, "detail-shown",
                  detail.HasHeader && detail.Page == DetailPage.Files && detail.CanGoBackInDetail
                  && detail.StatusText is { Length: > 0 } && !detail.StatusText.StartsWith("読み直しに失敗", StringComparison.Ordinal));
            ReloadViaScan(dbGone, detailOpen: true);
            var headingAfter = detail.Heading;
            WriteText(stdout, "detail-heading-after", headingAfter);
            Write(stdout, "detail-reloaded", detail.HasHeader && headingAfter != headingBefore);
            Write(stdout, "detail-position-kept",
                  detail.Page == DetailPage.Files && (detail.CanGoBackInDetail, detail.CanGoForwardInDetail) == pageHistoryBefore
                  && detail.Request.SessionId == 2);
            ReloadViaScan(dbBefore, detailOpen: false);
            Write(stdout, "closed-detail-not-reloaded", detail.Heading == headingAfter);
            Swap(bad);
            StartScanRun();
            shell.IsDetailOpen = true;
            FinishScanRun(0);
            Write(stdout, "detail-unreadable-keeps-and-says-why",
                  detail.Heading == headingAfter && detail.HasHeader
                  && detail.StatusText is { } ds && ds.StartsWith("読み直しに失敗", StringComparison.Ordinal));
            shell.IsDetailOpen = false;

            var work = Path.Combine(dir, "work.sqlite3");
            if (HistoryMaintenance.PointsAtRealData(work, noLayer0))
            {
                Console.Error.WriteLine("作業用の写しが本物の記録の場所を指しています: " + work);
                return 3;
            }
            File.Copy(dbBefore, work, true);
            ReloadViaScan(work);
            history.Kind = HistoryKind.All;
            history.ModifierHeld = true;
            var sessionsBefore = history.Rows.Count;
            var countAtWork = Count();
            WriteText(stdout, "history-sessions-before", sessionsBefore.ToString(System.Globalization.CultureInfo.InvariantCulture));
            WriteText(stdout, "stats-count-at-work", countAtWork);

            TrackerDb.Layer0DbPath = null;
            history.Selection.Clear();
            history.Selection.Select(0);
            var replayReadBefore = replay.ReloadReading;
            var statsReadBefore = stats.ReloadReading;
            history.RequestDeleteCommand.Execute(null);
            history.ConfirmDeleteCommand.Execute(null);
            Settle();
            Write(stdout, "history-failed-delete-noop",
                  history.IsNoticeOpen && ReferenceEquals(replay.ReloadReading, replayReadBefore)
                  && ReferenceEquals(stats.ReloadReading, statsReadBefore) && Count() == countAtWork);
            history.CloseNoticeCommand.Execute(null);
            TrackerDb.Layer0DbPath = noLayer0;

            history.RetentionSource = () => (2, 2);
            replayReadBefore = replay.ReloadReading;
            history.RequestPruneCommand.Execute(null);
            bool pruneAsked = history.IsPruneConfirmOpen;
            history.ConfirmPruneCommand.Execute(null);
            var prunedCount = history.LastDeleted.Count;
            Settle();
            WriteText(stdout, "history-pruned-count", prunedCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            WriteText(stdout, "stats-count-after-prune", Count());
            Write(stdout, "history-prune-reloads",
                  pruneAsked && prunedCount > 0 && !ReferenceEquals(replay.ReloadReading, replayReadBefore)
                  && Count() != countAtWork);

            var countAfterPrune = Count();
            replayReadBefore = replay.ReloadReading;
            history.Selection.Clear();
            history.Selection.Select(0);
            history.RequestDeleteCommand.Execute(null);
            history.ConfirmDeleteCommand.Execute(null);
            var deletedCount = history.LastDeleted.Count;
            Settle();
            WriteText(stdout, "history-deleted-count", deletedCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            WriteText(stdout, "stats-count-after-delete", Count());
            Write(stdout, "history-delete-reloads",
                  deletedCount > 0 && !ReferenceEquals(replay.ReloadReading, replayReadBefore)
                  && Count() != countAfterPrune);
            Write(stdout, "end", true);
            stdout.Flush();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
        finally
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static bool GoBackAndPathIsEmpty(StatsTabViewModel stats)
    {
        stats.GoBackCommand.Execute(null);
        return stats.CurrentPath.Count == 0;
    }

    private static bool ThrowsDuplicate(Action action)
    {
        try { action(); return false; }
        catch (InvalidOperationException) { return true; }
    }

    private static bool Has(LogLevel level, string text)
        => LogSource.Snapshot().Any(e => e.Level == level && e.Message.Contains(text, StringComparison.Ordinal));

    private static void Write(TextWriter stdout, string name, bool ok)
        => stdout.Write(name + "\t" + (ok ? "ok" : "NG") + "\n");

    private static void WriteText(TextWriter stdout, string name, string value)
        => stdout.Write(name + "\t" + value + "\n");

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }

    internal sealed class FakeLauncher : IDriveProcessLauncher
    {
        private readonly Dictionary<LaunchKind, List<FakeHandle>> handles = [];
        private readonly Dictionary<LaunchKind, int> endBeforeSubscribe = [];
        private readonly Dictionary<LaunchKind, Exception> failNextStart = [];

        private readonly Dictionary<LaunchKind, LaunchOptions?> lastOptions = [];

        public IDriveProcessHandle Start(LaunchKind kind, LaunchOptions? options)
        {
            lastOptions[kind] = options;
            if (failNextStart.Remove(kind, out var error)) throw error;
            var handle = new FakeHandle();
            if (!handles.TryGetValue(kind, out var list)) handles.Add(kind, list = []);
            list.Add(handle);
            if (endBeforeSubscribe.Remove(kind, out int code)) handle.ExitSilently(code);
            return handle;
        }

        public LaunchOptions? LastOptions(LaunchKind kind)
            => lastOptions.TryGetValue(kind, out var options) ? options : null;

        public FakeHandle Last(LaunchKind kind) => handles[kind][^1];
        public int StartCount(LaunchKind kind) => handles.TryGetValue(kind, out var list) ? list.Count : 0;
        public void FinishNextWithoutNotice(LaunchKind kind, int code) => endBeforeSubscribe[kind] = code;
        public void FailNextStart(LaunchKind kind, Exception error) => failNextStart[kind] = error;
    }

    internal sealed class FakeHandle : IDriveProcessHandle
    {
        public event Action<string>? OutputLine;
        public event Action? Exited;
        public bool IsRunning { get; private set; } = true;
        public int? ExitCode { get; private set; }
        public bool WasStopped { get; private set; }
        public bool WasDisposed { get; private set; }

        public bool StopFails { get; set; }

        public int ExitCodeOnStop { get; set; } = 1;

        public bool NotifyExitOffThread { get; set; }

        public bool Stop()
        {
            WasStopped = true;
            if (StopFails) return false;
            if (IsRunning) Exit(ExitCodeOnStop);
            return true;
        }

        public void Dispose()
        {
            WasDisposed = true;
            Stop();
        }
        public void Emit(string line) => OutputLine?.Invoke(line);

        public void Exit(int code)
        {
            if (!IsRunning) return;
            ExitCode = code;
            IsRunning = false;
            if (NotifyExitOffThread) Task.Run(() => Exited?.Invoke()).Wait();
            else Exited?.Invoke();
        }

        public void ExitSilently(int code)
        {
            if (!IsRunning) return;
            ExitCode = code;
            IsRunning = false;
        }
    }
}
