using System.Runtime.Versioning;
using System.Text;
using Avalonia;
using Avalonia.Threading;
using TH09.Record;
using TH09.Shell.ViewModels;

namespace TH09.Shell.Data;

[SupportedOSPlatform("windows")]
internal static class ReplayOwnUiDump
{
    public const string Flag = "--dump-replay-own-ui";

    public static int Run(string db, string layer0, string config, string badConfig)
    {
        using var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
        try
        {
            if (HistoryMaintenance.PointsAtRealData(db, layer0))
            {
                Console.Error.WriteLine("本物の記録の場所を指しています（本体 DB か Layer 0 のフォルダ）。"
                                        + "この口は合成の対だけを受け付けます: " + db + " / " + layer0);
                return 3;
            }
            foreach (var c in new[] { config, badConfig })
            {
                if (AppSettingsFormDump.InRealConfigDir(c))
                {
                    Console.Error.WriteLine("★本物の config.json のフォルダは受け付けません（合成の設定を渡してください）: " + c);
                    return 3;
                }
            }
            if (File.Exists(badConfig) || Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(badConfig))))
            {
                Console.Error.WriteLine("badConfig は「存在しないフォルダの中の名前」を渡してください: " + badConfig);
                return 1;
            }

            AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();
            AppSettingsSource.ReadFrom(config);
            LogSource.Clear();
            TrackerDb.MainDbPath = db;
            TrackerDb.Layer0DbPath = layer0;

            var nav = new ReplayOwnDump.CountingNavigation();
            var solo = new ReplayTabViewModel(nav);
            var ids0 = string.Join(",", solo.Rows.Select(r => r.ReplayId).Order());
            Write(stdout, "load", ids0 == "11,12,13,15" && string.IsNullOrEmpty(solo.StatusText), "行 " + ids0);
            solo.CheckHeld = true;
            solo.SelectedRow = solo.Rows.First(r => r.ReplayId == 11);
            solo.CheckHeld = false;
            Write(stdout, "check-press-does-not-open", nav.Opened == 0, "開いた回数 " + nav.Opened);
            solo.SelectedRow = solo.Rows.First(r => r.ReplayId == 12);
            Write(stdout, "text-press-opens", nav.Opened == 1, "開いた回数 " + nav.Opened);
            solo.Rows.First(r => r.ReplayId == 11).IsChecked = true;
            solo.Rows.First(r => r.ReplayId == 12).IsChecked = true;
            solo.ContextHeld = true;
            solo.SelectedRow = solo.Rows.First(r => r.ReplayId == 13);
            Write(stdout, "menu-count-equals-checks",
                  solo.CheckedCount == 2 && solo.TargetCount == 2
                  && solo.DeleteMenuText == ReplayOwnLabels.DeleteMenu(2)
                  && solo.TargetLineText == ReplayOwnLabels.TargetLine(2, true),
                  solo.TargetLineText + " ／ " + solo.DeleteMenuText);
            solo.ClearChecksCommand.Execute(null);
            Write(stdout, "menu-count-context-row",
                  solo.TargetCount == 1 && solo.TargetLineText == ReplayOwnLabels.TargetLine(1, false)
                  && solo.TargetRows()[0].ReplayId == 13,
                  solo.TargetLineText);
            solo.Rows.First(r => r.ReplayId == 11).IsChecked = true;
            solo.ReloadCommand.Execute(null);
            Write(stdout, "check-survives-manual-reload",
                  solo.CheckedCount == 1 && solo.Rows.First(r => r.ReplayId == 11).IsChecked
                  && !solo.Rows.First(r => r.ReplayId == 12).IsChecked,
                  "チェック " + solo.CheckedCount + " 件");

            using var shell = ShellViewModel.Create(new DriveControlDump.FakeLauncher());
            var replay = shell.Tabs.OfType<ReplayTabViewModel>().Single();
            var stats = shell.Tabs.OfType<StatsTabViewModel>().Single();
            var edited = 0;
            replay.DbEdited += () => edited++;

            void Settle()
            {
                stats.ReloadReading?.Wait();
                Dispatcher.UIThread.RunJobs();
            }
            ReplayListRow RowOf(long id) => replay.Rows.First(r => r.ReplayId == id);
            void Pick(long id)
            {
                replay.ContextHeld = true;
                replay.SelectedRow = RowOf(id);
            }
            void Check(params long[] ids)
            {
                replay.ClearChecksCommand.Execute(null);
                foreach (var id in ids) RowOf(id).IsChecked = true;
            }
            long? Ov(long id) => Scalar("SELECT own_override FROM replays WHERE replay_id=$0", id);
            long Cnt(string sql, long id) => Scalar(sql, id) ?? -1;
            bool LogHas(LogLevel level, string text)
                => LogSource.Snapshot().Any(e => e.Level == level && e.Message.Contains(text, StringComparison.Ordinal));
            string Note() => replay.OwnNote ?? "";
            var statsTask = stats.ReloadReading;
            bool StatsReloaded()
            {
                Settle();
                var moved = !ReferenceEquals(stats.ReloadReading, statsTask);
                statsTask = stats.ReloadReading;
                return moved;
            }

            Check(11);
            shell.ReloadForDbUpdate(DbUpdateCause.PlayEnded);
            replay.ReloadReading?.Wait();
            Settle();
            Write(stdout, "check-survives-auto-reload",
                  replay.CheckedCount == 1 && RowOf(11).IsChecked && !RowOf(12).IsChecked,
                  "チェック " + replay.CheckedCount + " 件");
            statsTask = stats.ReloadReading;

            Check(11, 12, 15);
            replay.MarkOwnCommand.Execute(null);
            var reloaded = StatsReloaded();
            Write(stdout, "mark-own-db", Ov(11) == 1 && Ov(12) == 1 && Ov(15) == 2,
                  $"11={Ov(11)} 12={Ov(12)} 15={Ov(15)}（15 は覆しの 2 のまま）");
            Write(stdout, "mark-own-note",
                  Note().Contains(ReplayOwnLabels.OwnResultLine(3, 2, 1, 0, 0), StringComparison.Ordinal),
                  Note());
            Write(stdout, "mark-own-log",
                  LogHas(LogLevel.Info, "[GUI] " + ReplayOwnLabels.MarkOwn + ": " + ReplayOwnLabels.OwnResultLine(3, 2, 1, 0, 0)),
                  "ログの 1 行");
            Write(stdout, "mark-own-signals-others", edited == 1 && reloaded, $"DbEdited {edited} 回 ／ 統計 {(reloaded ? "読み直した" : "読み直さない")}");
            Write(stdout, "checks-survive-write",
                  replay.CheckedCount == 3 && new long[] { 11, 12, 15 }.All(i => RowOf(i).IsChecked),
                  "チェック " + replay.CheckedCount + " 件");
            Write(stdout, "row-mark-follows", RowOf(11).Own == OwnSide.P1 && RowOf(15).Own == OwnSide.P2, "一覧の印");

            replay.SetOwnForeignCommand.Execute(null);
            Write(stdout, "set-foreign", Ov(11) == 0 && Ov(12) == 0 && Ov(15) == 0 && edited == 2 && StatsReloaded(),
                  $"11={Ov(11)} 12={Ov(12)} 15={Ov(15)} ／ DbEdited {edited}");
            replay.SetOwnP2Command.Execute(null);
            replay.SetOwnP1Command.Execute(null);
            Write(stdout, "set-p1-p2", Ov(11) == 1 && Ov(12) == 1 && Ov(15) == 1 && edited == 4,
                  $"11={Ov(11)} 12={Ov(12)} 15={Ov(15)} ／ DbEdited {edited}");
            StatsReloaded();
            replay.SetOwnP1Command.Execute(null);
            Write(stdout, "no-change-no-signal",
                  edited == 4 && !StatsReloaded()
                  && Note().Contains(ReplayOwnLabels.OwnResultLine(3, 0, 3, 0, 0), StringComparison.Ordinal),
                  $"DbEdited {edited} ／ " + Note());
            replay.SetOwnAutoCommand.Execute(null);
            Write(stdout, "set-auto", Ov(11) is null && Ov(12) is null && Ov(15) is null && edited == 5,
                  $"11={Ov(11)?.ToString() ?? "NULL"} 12={Ov(12)?.ToString() ?? "NULL"} 15={Ov(15)?.ToString() ?? "NULL"}");
            StatsReloaded();

            Check();
            Pick(13);
            replay.SetOwnP2Command.Execute(null);
            Write(stdout, "context-row-target", Ov(13) == 2 && Ov(11) is null && edited == 6 && replay.CheckedCount == 0,
                  $"13={Ov(13)} 11={Ov(11)?.ToString() ?? "NULL"}");
            Pick(13);
            replay.SetOwnAutoCommand.Execute(null);

            replay.IsMatch = true;
            Check();
            Pick(14);
            replay.MarkOwnCommand.Execute(null);
            Write(stdout, "mark-own-without-side",
                  Ov(14) == 3 && Note().Contains(ReplayOwnLabels.OwnResultLine(1, 1, 0, 0, 1), StringComparison.Ordinal),
                  $"14={Ov(14)} ／ " + Note());
            Pick(14);
            replay.SetOwnAutoCommand.Execute(null);
            replay.IsMatch = false;
            StatsReloaded();

            var editedBeforeMissing = edited;
            Check(13, 15);
            var gone = ReplayMaintenance.Delete(db, layer0, [15L]);
            replay.MarkOwnCommand.Execute(null);
            Write(stdout, "missing-row-counted",
                  gone.ReplayRows == 1 && Ov(13) == 1
                  && Note().Contains(ReplayOwnLabels.OwnResultLine(2, 1, 0, 1, 0), StringComparison.Ordinal)
                  && replay.Rows.All(r => r.ReplayId != 15) && replay.CheckedCount == 1
                  && edited == editedBeforeMissing + 1,
                  $"13={Ov(13)} ／ チェック {replay.CheckedCount} 件 ／ " + Note());
            Pick(13);
            replay.SetOwnAutoCommand.Execute(null);
            StatsReloaded();

            Check(11);
            TrackerDb.Layer0DbPath = null;
            replay.DeleteCommand.Execute(null);
            var noLayer0 = !replay.IsDeleteConfirmOpen && Note() == ReplayOwnLabels.NoLayer0Delete;
            TrackerDb.Layer0DbPath = layer0;
            Write(stdout, "delete-needs-layer0",
                  noLayer0 && Cnt("SELECT COUNT(*) FROM replays WHERE replay_id=$0", 11) == 1, Note());
            Check(11);
            var described = ReplayMaintenance.Describe(db, [11L]);
            replay.DeleteCommand.Execute(null);
            Write(stdout, "delete-confirm-first",
                  replay.IsDeleteConfirmOpen
                  && replay.DeleteConfirmText.Contains(ReplayMaintenance.SessionsLine(described), StringComparison.Ordinal)
                  && !replay.DeleteConfirmText.Contains("リプレイ 1 本", StringComparison.Ordinal)
                  && replay.DeleteConfirmText.StartsWith(ReplayOwnLabels.DeleteConfirmText(1), StringComparison.Ordinal)
                  && Cnt("SELECT COUNT(*) FROM replays WHERE replay_id=$0", 11) == 1,
                  replay.DeleteConfirmText.Replace("\n", " ／ "));
            var configBytes = File.ReadAllBytes(config);
            var editedBeforeCancel = edited;
            replay.CancelDeleteCommand.Execute(null);
            replay.ConfirmDeleteOnlyCommand.Execute(null);
            replay.ConfirmDeleteAndExcludeCommand.Execute(null);
            Write(stdout, "cancel-writes-nothing",
                  !replay.IsDeleteConfirmOpen && Cnt("SELECT COUNT(*) FROM replays WHERE replay_id=$0", 11) == 1
                  && File.ReadAllBytes(config).AsSpan().SequenceEqual(configBytes)
                  && edited == editedBeforeCancel && replay.CheckedCount == 1 && !StatsReloaded(),
                  "行 11 は残り、設定のバイトも同じ");

            AppSettingsSource.ReadFrom(badConfig);
            replay.DeleteCommand.Execute(null);
            replay.ConfirmDeleteAndExcludeCommand.Execute(null);
            Write(stdout, "save-failure-keeps-rows",
                  Cnt("SELECT COUNT(*) FROM replays WHERE replay_id=$0", 11) == 1
                  && Cnt("SELECT COUNT(*) FROM sessions WHERE session_id=$0", 900) == 1
                  && Note().Contains("削除しませんでした", StringComparison.Ordinal)
                  && LogHas(LogLevel.Error, "削除しませんでした") && edited == editedBeforeCancel
                  && replay.CheckedCount == 1 && !File.Exists(badConfig) && !StatsReloaded(),
                  Note());
            AppSettingsSource.ReadFrom(config);

            replay.DeleteCommand.Execute(null);
            replay.ConfirmDeleteAndExcludeCommand.Execute(null);
            var (entries, _) = ExcludedReplays.Load(config);
            Write(stdout, "delete-and-exclude-db",
                  Cnt("SELECT COUNT(*) FROM replays WHERE replay_id=$0", 11) == 0
                  && Cnt("SELECT COUNT(*) FROM replay_paths WHERE replay_id=$0", 11) == 0
                  && Cnt("SELECT COUNT(*) FROM session_replays WHERE replay_id=$0", 11) == 0
                  && Cnt("SELECT COUNT(*) FROM sessions WHERE session_id=$0", 900) == 0
                  && Cnt("SELECT COUNT(*) FROM sessions WHERE session_id=$0", 901) == 1
                  && Cnt("SELECT COUNT(*) FROM sessions WHERE session_id=$0", 950) == 1,
                  "replays/paths/links 0・走査セッション 900 は消え、推測の 901・950 は残った");
            Write(stdout, "delete-and-exclude-config",
                  entries.Count == 1 && entries[0].Sha256 == "sha-11",
                  string.Join("|", entries.Select(e => e.Sha256 + "," + e.Path + "," + e.ExcludedAt)));
            Write(stdout, "delete-clears-checks-and-signals",
                  replay.CheckedCount == 0 && replay.Rows.All(r => r.ReplayId != 11)
                  && edited == editedBeforeCancel + 1 && StatsReloaded(),
                  $"チェック {replay.CheckedCount} 件 ／ DbEdited {edited}");
            Write(stdout, "delete-note",
                  Note() == ReplayOwnLabels.Deleted(1, 1, 0, 1, 2, 1, 0) && LogHas(LogLevel.Info, Note()),
                  Note());

            Check();
            Pick(12);
            replay.DeleteCommand.Execute(null);
            replay.ConfirmDeleteOnlyCommand.Execute(null);
            Write(stdout, "delete-only",
                  Cnt("SELECT COUNT(*) FROM replays WHERE replay_id=$0", 12) == 0
                  && Cnt("SELECT COUNT(*) FROM sessions WHERE session_id=$0", 902) == 0
                  && ExcludedReplays.Load(config).Entries.Count == 1
                  && Note() == ReplayOwnLabels.Deleted(1, 1, 0, 1, 1, null, 0)
                  && edited == editedBeforeCancel + 2,
                  Note());

            Check(13);
            ReplayMaintenance.Delete(db, layer0, [13L]);
            replay.DeleteCommand.Execute(null);
            Write(stdout, "delete-all-missing-no-confirm",
                  !replay.IsDeleteConfirmOpen && Note() == ReplayOwnLabels.AllMissing(1)
                  && replay.Rows.All(r => r.ReplayId != 13),
                  Note());

            Write(stdout, "end", true, "");
            stdout.Flush();
            return 0;
        }
        catch (Exception ex)
        {
            stdout.Flush();
            Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace);
            return 1;
        }
    }

    private static long? Scalar(string sql, long id)
    {
        using var conn = TrackerDb.OpenMainDb();
        using var cmd = conn.Command(sql);
        cmd.Parameters.AddWithValue("$0", id);
        var v = cmd.ExecuteScalar();
        return v is null or DBNull ? null : Convert.ToInt64(v, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void Write(TextWriter stdout, string name, bool ok, string detail)
        => stdout.Write(name + "\t" + (ok ? "ok" : "NG") + "\t" + detail.Replace("\t", " ").Replace("\n", " ") + "\n");
}
