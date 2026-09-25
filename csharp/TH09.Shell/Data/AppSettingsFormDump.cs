using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using TH09.Record;
using TH09.Shell.ViewModels;

namespace TH09.Shell.Data;

[SupportedOSPlatform("windows")]
internal static class AppSettingsFormDump
{
    public const string Flag = "--dump-settings-form";

    public const string ToggleKeyFlag = "--dump-toggle-key";

    public const string OpsVar = "TH09_SETTINGS_FORM_OPS";

    public static int Run(string path, bool save)
    {
        if (InRealConfigDir(path))
        {
            Console.Error.WriteLine(
                "★本物の config.json のフォルダは受け付けません（合成の設定を渡してください）: " + path);
            return 3;
        }
        using var w = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
        AppSettingsSource.ReadFrom(path);
        var form = new AppSettingsViewModel(path);
        var ops = Environment.GetEnvironmentVariable(OpsVar) ?? "";
        if (!TryApplyOps(form, ops, out var opError))
        {
            Console.Error.WriteLine(OpsVar + " が読めません: " + opError);
            return 2;
        }
        if (save) form.SaveCommand.Execute(null);

        Row(w, "fact", "config", Path.GetFullPath(path));
        Row(w, "fact", "ops", ops);
        Row(w, "fact", "save-result", form.SaveResultText);

        Row(w, "item", Paths.RecordReplayPlaybackKey, Flag01(form.RecordReplayPlayback));
        Row(w, "item", ConfigStore.RecordReplayToggleKeyKey, form.RecordReplayToggleKey);
        Row(w, "item", ConfigStore.HistoryRetentionAbortedKey, form.HistoryRetentionAbortedText);
        Row(w, "item", ConfigStore.HistoryRetentionCompletedKey, form.HistoryRetentionCompletedText);
        Row(w, "fact", "retention-unlimited",
            Flag01(form.HistoryRetentionAbortedUnlimited)
            + "/" + Flag01(form.HistoryRetentionCompletedUnlimited));
        Row(w, "item", ConfigStore.TickHookKey,
            ConfigStore.TickHookWords[Math.Clamp(form.TickHookIndex, 0,
                                                 ConfigStore.TickHookWords.Length - 1)]);
        Row(w, "item", ConfigStore.HitWindowsKey, Flag01(form.HitWindows));
        Row(w, "item", ConfigStore.HitWindowBeforeKey, form.HitWindowBeforeText);
        Row(w, "item", ConfigStore.HitWindowAfterKey, form.HitWindowAfterText);
        foreach (var scope in form.HitWindowScopes)
            Row(w, "item", ConfigStore.HitWindowScopesKey + "." + scope.Name, Flag01(scope.IsOn));
        Row(w, "item", ConfigStore.HitWindowQuickKey, Flag01(form.HitWindowQuick));
        Row(w, "item", ConfigStore.AutoMonitorOnGameKey, Flag01(form.AutoMonitorOnGame));
        Row(w, "item", ConfigStore.WatchReplaysWithMonitorKey,
            Flag01(form.WatchReplaysWithMonitor));
        Row(w, "item", ConfigStore.BackupKeepOneKey, Flag01(form.BackupKeepOne));
        foreach (var item in form.StatsItems)
            Row(w, "item", ConfigStore.StatsHiddenItemsKey + ".shown." + item.Label,
                Flag01(item.IsShown));
        foreach (var item in form.StreamPanelItems)
            Row(w, "item", ConfigStore.StreamPanelHiddenItemsKey + ".shown." + item.Kind,
                Flag01(item.IsShown));
        foreach (var item in form.PlayTabItems)
            Row(w, "item", ConfigStore.PlayTabHiddenItemsKey + ".shown." + item.Kind,
                Flag01(item.IsShown));
        var total = 0;
        var hiddenCount = 0;
        foreach (var page in form.StatsPages)
        {
            total += page.Rows.Count;
            hiddenCount += page.Rows.Count(x => x.IsItem && !x.IsShown);
        }
        Row(w, "item", ConfigStore.StatsColumnOrderKey,
            total.ToString(CultureInfo.InvariantCulture));
        Row(w, "item", ConfigStore.StatsHiddenColumnsKey,
            hiddenCount.ToString(CultureInfo.InvariantCulture));
        Row(w, "fact", "column-page-count",
            form.StatsPages.Count.ToString(CultureInfo.InvariantCulture));
        Row(w, "fact", "column-page",
            form.SelectedStatsPageIndex.ToString(CultureInfo.InvariantCulture));
        foreach (var page in form.StatsPages)
        {
            var key = StatsSections.Key(page.Section);
            Row(w, "column-page", key, page.Label, Flag01(page.IsCurrent),
                page.Rows.Count.ToString(CultureInfo.InvariantCulture));
            for (var i = 0; i < page.Rows.Count; i++)
            {
                var row = page.Rows[i];
                Row(w, "column", key, i.ToString(CultureInfo.InvariantCulture),
                    row.IsHeading ? "head" : row.IsSeparator ? "sep" : "col",
                    row.Name, Flag01(row.IsShown), row.Where, row.Part.Key,
                    Flag01(row.IsPinned));
            }
        }
        Row(w, "fact", "column-selected",
            form.SelectedColumnIndex.ToString(CultureInfo.InvariantCulture));
        Row(w, "fact", "column-can",
            Flag01(form.CanMoveColumnUp) + "/" + Flag01(form.CanMoveColumnDown)
            + "/" + Flag01(form.CanRemoveSeparator) + "/" + Flag01(form.CanRenameSeparator)
            + "/" + Flag01(form.CanAddSeparator));
        Row(w, "item", ConfigStore.StreamModeKey + ".width", form.StreamWidthText);
        Row(w, "item", ConfigStore.StreamModeKey + ".height", form.StreamHeightText);
        Row(w, "item", ConfigStore.StreamModeKey + ".font_scale", form.StreamFontScaleText);
        Row(w, "item", ConfigStore.StreamModeKey + ".bg",
            form.StreamBackgroundIndex >= 0 && form.StreamBackgroundIndex < form.StreamBackgrounds.Count
                ? form.StreamBackgrounds[form.StreamBackgroundIndex].Hex : "");
        Row(w, "item", ConfigStore.StreamModeKey + ".topmost", Flag01(form.StreamTopmost));
        Row(w, "item", ConfigStore.StreamModeKey + ".borderless", Flag01(form.StreamBorderless));
        Row(w, "item", ConfigStore.StreamModeKey + ".content",
            AppSettingsSource.Current.StreamMode.Content);
        Row(w, "fact", "stream-bg-choices",
            form.StreamBackgrounds.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var choice in form.StreamBackgrounds) Row(w, "stream-bg", choice.Label, choice.Hex);

        Row(w, "fact", "target-where",
            form.TargetMode.ToString(CultureInfo.InvariantCulture)
            + "/" + form.TargetDifficulty.ToString(CultureInfo.InvariantCulture)
            + "/" + form.TargetCharacter.ToString(CultureInfo.InvariantCulture)
            + "/" + form.TargetStage.ToString(CultureInfo.InvariantCulture));
        Row(w, "fact", "target-where-text", form.TargetWhereText);
        Row(w, "fact", "target-chips",
            form.TargetModes.Count.ToString(CultureInfo.InvariantCulture)
            + "/" + form.TargetDifficulties.Count.ToString(CultureInfo.InvariantCulture)
            + "/" + form.TargetCharacters.Count.ToString(CultureInfo.InvariantCulture)
            + "/" + form.TargetStages.Count.ToString(CultureInfo.InvariantCulture));
        TargetChips(w, "mode", form.TargetModes);
        TargetChips(w, "difficulty", form.TargetDifficulties);
        TargetChips(w, "character", form.TargetCharacters);
        TargetChips(w, "stage", form.TargetStages);
        Row(w, "fact", "target-shows",
            Flag01(form.HasTargetStages) + "/" + Flag01(form.HasTargetClearBonus));
        Row(w, "item", TH09.Record.StreamTargets.Key + ".clear_bonus", form.TargetClearBonusText);
        Row(w, "fact", "target-row-count",
            form.TargetRows.Count.ToString(CultureInfo.InvariantCulture));
        for (var i = 0; i < form.TargetRows.Count; i++)
        {
            var row = form.TargetRows[i];
            Row(w, "target-row", i.ToString(CultureInfo.InvariantCulture),
                row.Round?.ToString(CultureInfo.InvariantCulture) ?? "", row.Label,
                row.TargetText, row.WrText, row.Hint);
        }

        var visible = StatsSections.Visible(AppSettingsSource.Current.StatsHiddenItems);
        Row(w, "fact", "stats-visible-count", visible.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var s in visible) Row(w, "stats-visible", StatsSections.Title(s));

        Row(w, "fact", "mode-count",
            form.HitWindowScopes.Count.ToString(CultureInfo.InvariantCulture));
        for (var i = 0; i < form.HitWindowScopes.Count; i++)
            Row(w, "mode", i.ToString(CultureInfo.InvariantCulture),
                form.HitWindowScopes[i].Name, form.HitWindowScopes[i].Label);
        Row(w, "fact", "stats-item-count",
            form.StatsItems.Count.ToString(CultureInfo.InvariantCulture));
        for (var i = 0; i < form.StatsItems.Count; i++)
            Row(w, "stats-item", i.ToString(CultureInfo.InvariantCulture), form.StatsItems[i].Label);

        Row(w, "fact", "own-name-count",
            form.OwnPlayerNames.Count.ToString(CultureInfo.InvariantCulture));
        for (var i = 0; i < form.OwnPlayerNames.Count; i++)
            Row(w, "own-name", i.ToString(CultureInfo.InvariantCulture), form.OwnPlayerNames[i]);
        Row(w, "fact", "own-name-draft", form.OwnPlayerNameDraft);
        Row(w, "fact", "own-name-note", form.OwnPlayerNameNote);
        Row(w, "fact", "own-name-selected",
            form.SelectedOwnPlayerNameIndex.ToString(CultureInfo.InvariantCulture));
        Row(w, "fact", "own-name-can", Flag01(form.CanRemoveOwnPlayerName));

        Row(w, "fact", "game_paths_data_root", form.GamePaths.DataRoot);
        Row(w, "fact", "game_paths_config_path", form.GamePaths.ConfigPath);
        Row(w, "fact", "game_dir_file", form.GamePaths.GameDirFile);
        Row(w, "fact", "replay_paths_file", form.GamePaths.ReplayPathsFile);
        Row(w, "item", Paths.GameDirFileName, form.GameDirText);
        Row(w, "fact", "game-dir-display", form.GameDirDisplayText);
        Row(w, "fact", "game-dir-note", form.GameDirNote);
        Row(w, "fact", "user-data-dir-display", form.UserDataDirDisplayText);
        Row(w, "fact", "extra-replay-dir-count",
            form.ExtraReplayDirs.Count.ToString(CultureInfo.InvariantCulture));
        for (var i = 0; i < form.ExtraReplayDirs.Count; i++)
            Row(w, "extra-replay-dir", i.ToString(CultureInfo.InvariantCulture),
                form.ExtraReplayDirs[i]);
        Row(w, "fact", "extra-replay-dir-selected",
            form.SelectedExtraReplayDirIndex.ToString(CultureInfo.InvariantCulture));
        Row(w, "fact", "extra-replay-dir-can", Flag01(form.CanRemoveExtraReplayDir));

        string[] notes = form.NotesText.Length == 0 ? [] : form.NotesText.Split('\n');
        Row(w, "fact", "notes", notes.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var line in notes) Row(w, "note", line);
        w.Flush();
        return 0;
    }

    private static bool TryApplyOps(AppSettingsViewModel form, string ops, out string error)
    {
        error = "";
        foreach (var raw in ops.Split(',', StringSplitOptions.RemoveEmptyEntries
                                           | StringSplitOptions.TrimEntries))
        {
            var at = raw.IndexOf(':', StringComparison.Ordinal);
            var name = at < 0 ? raw : raw[..at];
            var arg = at < 0 ? "" : raw[(at + 1)..];
            switch (name)
            {
                case "sel" when int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture,
                                             out var sel):
                    form.SelectedColumnIndex = sel;
                    break;
                case "page" when int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture,
                                              out var page)
                                 && page >= 0 && page < form.StatsPages.Count:
                    form.SelectStatsPageCommand.Execute(form.StatsPages[page]);
                    break;
                case "up": form.MoveColumnUpCommand.Execute(null); break;
                case "down": form.MoveColumnDownCommand.Execute(null); break;
                case "addsep": form.AddColumnSeparatorCommand.Execute(null); break;
                case "delsep": form.RemoveColumnSeparatorCommand.Execute(null); break;
                case "reset": form.ResetColumnsCommand.Execute(null); break;
                case "name": form.SelectedSeparatorName = arg; break;
                case "hide" when int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture,
                                              out var hideAt)
                                 && hideAt >= 0 && hideAt < form.StatsColumns.Count:
                    form.StatsColumns[hideAt].IsShown = false;
                    break;
                case "show" when int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture,
                                              out var showAt)
                                 && showAt >= 0 && showAt < form.StatsColumns.Count:
                    form.StatsColumns[showAt].IsShown = true;
                    break;
                case "w": form.StreamWidthText = arg; break;
                case "h": form.StreamHeightText = arg; break;
                case "fs": form.StreamFontScaleText = arg; break;
                case "bg" when int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture,
                                            out var bg):
                    form.StreamBackgroundIndex = bg;
                    break;
                case "top": form.StreamTopmost = arg == "1"; break;
                case "border": form.StreamBorderless = arg == "1"; break;
                case "tmode" when Pick(form.TargetModes, arg) is { } m:
                    form.SelectTargetModeCommand.Execute(m); break;
                case "tdiff" when Pick(form.TargetDifficulties, arg) is { } d:
                    form.SelectTargetDifficultyCommand.Execute(d); break;
                case "tchar" when Pick(form.TargetCharacters, arg) is { } ch:
                    form.SelectTargetCharacterCommand.Execute(ch); break;
                case "tstage" when Pick(form.TargetStages, arg) is { } st:
                    form.SelectTargetStageCommand.Execute(st); break;
                case "tval" when TargetCell(form, arg, out var cell, out var text):
                    cell.TargetText = text; break;
                case "twr" when TargetCell(form, arg, out var cell2, out var text2):
                    cell2.WrText = text2; break;
                case "tcb": form.TargetClearBonusText = arg; break;
                case "tclear": form.ClearTargetPageCommand.Execute(null); break;
                case "ownname": form.OwnPlayerNameDraft = arg; form.AddOwnPlayerNameCommand.Execute(null);
                    break;
                case "ownsel" when int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture,
                                                out var ownAt):
                    form.SelectedOwnPlayerNameIndex = ownAt;
                    break;
                case "owndel": form.RemoveOwnPlayerNameCommand.Execute(null); break;
                case "gamedir": form.SetGameDir(arg); break;
                case "extra": form.AddExtraReplayDir(arg); break;
                case "extrasel" when int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture,
                                                  out var extraAt):
                    form.SelectedExtraReplayDirIndex = extraAt;
                    break;
                case "extradel": form.RemoveExtraReplayDirCommand.Execute(null); break;
                case "vsroot": form.VirtualStoreRootOverride = arg; break;
                case "childwrite": form.GamePaths.TryWriteGameDir(arg); break;
                default:
                    error = raw;
                    return false;
            }
        }
        return true;
    }

    private static void TargetChips(TextWriter w, string kind,
                                    IEnumerable<ViewModels.SettingTargetChip> chips)
    {
        var i = 0;
        foreach (var chip in chips)
        {
            Row(w, "target-chip", kind, i.ToString(CultureInfo.InvariantCulture),
                chip.Value.ToString(CultureInfo.InvariantCulture), chip.Label, Flag01(chip.IsCurrent));
            i++;
        }
    }

    private static ViewModels.SettingTargetChip? Pick(
        IEnumerable<ViewModels.SettingTargetChip> chips, string arg)
    {
        if (!long.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            return null;
        foreach (var chip in chips) if (chip.Value == v) return chip;
        return null;
    }

    private static bool TargetCell(AppSettingsViewModel form, string arg,
                                   [NotNullWhen(true)] out ViewModels.SettingTargetRow? row,
                                   out string text)
    {
        row = null;
        text = "";
        var at = arg.IndexOf(':', StringComparison.Ordinal);
        if (at < 0) return false;
        if (!long.TryParse(arg[..at], NumberStyles.Integer, CultureInfo.InvariantCulture, out var round))
            return false;
        foreach (var candidate in form.TargetRows)
        {
            if ((candidate.Round ?? 0) != round) continue;
            row = candidate;
            text = arg[(at + 1)..];
            return true;
        }
        return false;
    }

    public static int RunToggleKey(IReadOnlyList<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        using var w = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
        Row(w, "fact", "known", MonitorToggleKeyReading.Known);
        foreach (var word in words)
        {
            var key = MonitorToggleKeyReading.Read(word, out var reason);
            Row(w, "key", word, key?.ToString() ?? "", reason);
        }
        w.Flush();
        return 0;
    }

    private static bool InRealConfigDir(string path)
    {
        if (!OperatingSystem.IsWindows()) return false;
        var real = Path.GetDirectoryName(Full(Paths.Default.ConfigPath)) ?? "";
        return real.Length > 0
               && string.Equals(Path.GetDirectoryName(Full(path)) ?? "", real,
                                StringComparison.OrdinalIgnoreCase);
    }

    private static string Full(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static string Flag01(bool v) => v ? "1" : "0";

    private static void Row(TextWriter w, params string[] cells) =>
        w.WriteLine(string.Join('\t', cells));
}
