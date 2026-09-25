using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TH09.Launch;
using TH09.Record;
using TH09.Shell.Data;

namespace TH09.Shell.ViewModels;

internal sealed record ScanRunOption(ScanRun Value, string Label,
                                     bool CanChoose = true, string Reason = "");

internal sealed record ScanOrderOption(ScanOrder Value, string Label);

internal sealed partial class ScanScopeChip : ObservableObject
{
    public ScanScopeChip(ScanScope value, string label, bool isOn)
    {
        Value = value;
        Label = label;
        IsOn = isOn;
    }

    public ScanScope Value { get; }

    public string Label { get; }

    [ObservableProperty]
    public partial bool IsOn { get; set; }
}

internal sealed partial class ScanPickChip : ObservableObject
{
    public ScanPickChip(int index, string label, bool isSelected)
    {
        Index = index;
        Label = label;
        IsSelected = isSelected;
    }

    public int Index { get; }

    public string Label { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

internal sealed partial class ScanSettingsViewModel : ObservableObject
{
    public static IReadOnlyList<ScanRunOption> RunOptions { get; } =
    [
        new(ScanRun.AddNew, "新規のみ走査して DB 追加"),
        new(ScanRun.Rescan, "上書きして新たに走査"),
        new(ScanRun.ResetScans, "実プレイは残し、走査の結果だけ消して走査"),
        new(ScanRun.ResetAll, "DB ごと空にして走査（実プレイも消えます）"),
    ];

    public const string ResetBlockedReason = "いまは選べません（空にする口がまだありません）";

    public const string ResetScansKeepsNote =
        "実プレイ（走査由来ではないセッション。Layer 0 のファイルも含めて残ります）";

    public const string ResetAllWipesNote = "実プレイも消えます";

    public static string BlockedRunsNote { get; } = MakeBlockedRunsNote();

    public static bool HasBlockedRuns { get; } = BlockedRunsNote.Length > 0;

    public string BlockedRunsText => BlockedRunsNote;

    public bool ShowBlockedRuns => HasBlockedRuns;

    private static string MakeBlockedRunsNote()
    {
        var blocked = RunOptions.Where(o => !o.CanChoose).ToList();
        if (blocked.Count == 0) return "";
        return string.Join("／",
            blocked.GroupBy(o => o.Reason, StringComparer.Ordinal)
                   .Select(g => string.Join(" / ", g.Select(o => o.Label)) + " は" + g.Key));
    }

    public static IReadOnlyList<(int Index, string Label)> ModeChoices { get; } =
    [
        ((int)ScanMode.Story, HistoryLabels.Modes[0]),
        ((int)ScanMode.Extra, HistoryLabels.Modes[1]),
        ((int)ScanMode.Match, HistoryLabels.Modes[2]),
    ];

    public static IReadOnlyList<(int Index, string Label)> MatchKindChoices { get; } =
    [
        ((int)ScanMatchKind.HumanVsHuman, ReplayLabels.HumanVsHumanText),
        ((int)ScanMatchKind.HumanVsCpu, ReplayLabels.HumanVsCpuText),
        ((int)ScanMatchKind.CpuVsHuman, ReplayLabels.CpuVsHumanText),
        ((int)ScanMatchKind.CpuVsCpu, ReplayLabels.CpuVsCpuText),
    ];

    public static IReadOnlyList<ScanOrderOption> OrderOptions { get; } =
    [
        new(ScanOrder.Id, "id（決まった順）"),
        new(ScanOrder.Size, "size（小さいものから）"),
        new(ScanOrder.Mtime, "mtime（更新の古いものから）"),
    ];

    public const string NoneText = "無し";

    public ScanSettingsViewModel()
    {
        for (var i = 0; i < ReplayLabels.Difficulties.Length; i++)
            DifficultyChips.Add(new ScanPickChip(i, ReplayLabels.Difficulties[i], true));
        foreach (var i in ReplayLabels.DisplayOrder)
            CharacterChips.Add(new ScanPickChip(i, ReplayLabels.Characters[i], true));
        foreach (var (index, label) in ModeChoices)
            ModeChips.Add(new ScanPickChip(index, label, true));
        foreach (var (index, label) in MatchKindChoices)
            MatchKindChips.Add(new ScanPickChip(index, label, true));
        SelectedRun = RunOptions[0];
        SelectedOrder = OrderOptions[0];
        LoadHitWindowDefaults();
    }

    public ObservableCollection<ScanPickChip> DifficultyChips { get; } = [];

    public ObservableCollection<ScanPickChip> CharacterChips { get; } = [];

    public ObservableCollection<ScanPickChip> ModeChips { get; } = [];

    public ObservableCollection<ScanPickChip> MatchKindChips { get; } = [];

    [ObservableProperty]
    public partial ScanRunOption SelectedRun { get; set; } = RunOptions[0];

    [ObservableProperty]
    public partial ScanOrderOption SelectedOrder { get; set; } = OrderOptions[0];

    [ObservableProperty]
    public partial bool OwnOnly { get; set; }

    [ObservableProperty]
    public partial string MinStagesText { get; set; } = "";

    public ObservableCollection<string> Directories { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRemoveDirectory))]
    public partial string? SelectedDirectory { get; set; }

    public bool CanRemoveDirectory => SelectedDirectory is not null;

    [ObservableProperty]
    public partial bool NoRecurse { get; set; }

    [ObservableProperty]
    public partial string MaxCountText { get; set; } = "";

    [ObservableProperty]
    public partial string MaxMinutesText { get; set; } = "";

    [ObservableProperty]
    public partial string MinRecordVersionText { get; set; } = "";

    [ObservableProperty]
    public partial bool Backup { get; set; } = true;

    [ObservableProperty]
    public partial bool DropOldBackups { get; set; }


    [ObservableProperty]
    public partial bool HitWindowsFromConfig { get; set; } = true;

    [ObservableProperty]
    public partial bool HitWindows { get; set; } = true;

    [ObservableProperty]
    public partial string HitWindowBeforeText { get; set; } = "";

    [ObservableProperty]
    public partial string HitWindowAfterText { get; set; } = "";

    [ObservableProperty]
    public partial bool HitWindowQuick { get; set; }

    public ObservableCollection<ScanScopeChip> HitWindowScopes { get; } = [];

    private void LoadHitWindowDefaults()
    {
        if (OperatingSystem.IsWindows()) LoadHitWindowFromSettings();
        else
        {
            foreach (var scope in Enum.GetValues<ScanScope>())
                HitWindowScopes.Add(new ScanScopeChip(scope, scope.ToString(), true));
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void LoadHitWindowFromSettings()
    {
        var settings = AppSettingsSource.Current;
        HitWindows = settings.HitWindows;
        HitWindowBeforeText = settings.HitWindowBefore.ToString(CultureInfo.InvariantCulture);
        HitWindowAfterText = settings.HitWindowAfter.ToString(CultureInfo.InvariantCulture);
        HitWindowQuick = settings.HitWindowQuick;
        foreach (var scope in Enum.GetValues<ScanScope>()
                                  .OrderBy(s => ModeDisplay.Rank(ScopeName(s))))
        {
            var name = ScopeName(scope);
            bool on = !settings.HitWindowScopes.TryGetValue(name, out bool value) || value;
            HitWindowScopes.Add(new ScanScopeChip(scope, ScopeLabel(scope, name), on));
        }
    }

    private static string ScopeName(ScanScope scope)
    {
        if (!OperatingSystem.IsWindows()) return "";
        var all = CaptureScope.All;
        int i = (int)scope;
        return all.Count == Enum.GetValues<ScanScope>().Length && i >= 0 && i < all.Count
            ? all[i] : "";
    }

    private static string ScopeLabel(ScanScope scope, string name)
        => name.Length > 0 && OperatingSystem.IsWindows()
            ? ModeDisplay.Label(name) : scope.ToString();

    [RelayCommand]
    private static void ToggleChip(ScanPickChip? chip)
    {
        if (chip is not null) chip.IsSelected = !chip.IsSelected;
    }

    [RelayCommand]
    private void SelectAllModes() => SetAll(ModeChips, true);

    [RelayCommand]
    private void ClearModes() => SetAll(ModeChips, false);

    public void AddDirectory(string? path)
    {
        var trimmed = (path ?? "").Trim();
        if (trimmed.Length == 0) return;
        if (Directories.Any(d => string.Equals(d, trimmed, StringComparison.OrdinalIgnoreCase)))
            return;
        Directories.Add(trimmed);
        SelectedDirectory = trimmed;
    }

    [RelayCommand]
    private void RemoveDirectory()
    {
        if (SelectedDirectory is not { } picked) return;
        Directories.Remove(picked);
        SelectedDirectory = null;
    }

    [RelayCommand]
    private void SelectAllDifficulties() => SetAll(DifficultyChips, true);

    [RelayCommand]
    private void ClearDifficulties() => SetAll(DifficultyChips, false);

    [RelayCommand]
    private void SelectAllMatchKinds() => SetAll(MatchKindChips, true);

    [RelayCommand]
    private void ClearMatchKinds() => SetAll(MatchKindChips, false);

    [RelayCommand]
    private void SelectAllCharacters() => SetAll(CharacterChips, true);

    [RelayCommand]
    private void ClearCharacters() => SetAll(CharacterChips, false);

    public bool TryBuild(bool dryRun, out ScanSettings? settings, out string error)
    {
        settings = null;
        error = "";
        if (!SelectedRun.CanChoose)
        {
            error = SelectedRun.Label + "は" + SelectedRun.Reason;
            return false;
        }
        if (!TryNumber(MinStagesText, "最小ステージ数", out int? minStages, out error)) return false;
        if (!TryNumber(MaxCountText, "対象の上限本数", out int? maxCount, out error)) return false;
        if (!TryNumber(MinRecordVersionText, "Layer 0 の版", out int? minVersion, out error)) return false;
        if (!TryMinutes(MaxMinutesText, out double? maxMinutes, out error)) return false;
        int? before = null, after = null;
        if (!HitWindowsFromConfig)
        {
            if (!TryNumber(HitWindowBeforeText, "被弾窓の前", out before, out error)) return false;
            if (!TryNumber(HitWindowAfterText, "被弾窓の後", out after, out error)) return false;
            if (before is null || after is null)
            {
                error = "被弾窓の前と後は数で書いてください（空欄）。";
                return false;
            }
        }

        var built = new ScanSettings
        {
            Run = SelectedRun.Value,
            Modes = Picked(ModeChips).Select(i => (ScanMode)i).ToList(),
            Difficulties = Picked(DifficultyChips),
            MatchKinds = Picked(MatchKindChips).Select(i => (ScanMatchKind)i).ToList(),
            Characters = Picked(CharacterChips),
            OwnOnly = OwnOnly,
            MinStages = minStages ?? 0,
            Directories = [.. Directories],
            NoRecurse = NoRecurse,
            Order = SelectedOrder.Value,
            MaxCount = maxCount,
            MaxMinutes = maxMinutes,
            MinRecordVersion = minVersion,
            Backup = Backup,
            DropOldBackups = DropOldBackups,
            DryRun = dryRun,
            HitWindows = HitWindowsFromConfig ? null : HitWindows,
            HitWindowBefore = before,
            HitWindowAfter = after,
            HitWindowQuick = HitWindowsFromConfig ? null : HitWindowQuick,
            HitWindowScopes = HitWindowsFromConfig
                ? null
                : HitWindowScopes.ToDictionary(s => s.Value, s => s.IsOn),
        };
        try
        {
            _ = DriveLauncher.PreviewArguments(LaunchKind.Scan, new LaunchOptions { Scan = built });
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return false;
        }
        settings = built;
        return true;
    }

    public string Summary()
    {
        var filters = new List<string>();
        AddPicked(filters, "モード", ModeChips);
        AddPicked(filters, "難易度", DifficultyChips);
        AddPicked(filters, "対戦区分（Match のみ）", MatchKindChips);
        AddPicked(filters, "キャラ", CharacterChips);
        if (OwnOnly) filters.Add("自分のリプレイだけ");
        if (MinStagesText.Trim().Length > 0) filters.Add("最小ステージ数 " + MinStagesText.Trim());
        if (Directories.Count > 0)
            filters.Add("フォルダ " + Directories.Count + " 個" + (NoRecurse ? "（直下だけ）" : ""));

        var limits = new List<string>();
        if (MaxCountText.Trim().Length > 0) limits.Add("本数 " + MaxCountText.Trim());
        if (MaxMinutesText.Trim().Length > 0) limits.Add("総時間 " + MaxMinutesText.Trim() + " 分");
        if (MinRecordVersionText.Trim().Length > 0)
            limits.Add("Layer 0 が版 " + MinRecordVersionText.Trim() + " 以上なら飛ばす");

        var lines = new List<string>
        {
            "走り方: " + SelectedRun.Label,
            "絞り込み: " + (filters.Count > 0 ? string.Join(" ／ ", filters) : NoneText),
            "処理順: " + SelectedOrder.Label,
            "上限: " + (limits.Count > 0 ? string.Join(" ／ ", limits) : NoneText),
            "走る前の控え: " + (Backup ? "取る" : "取らない")
                + "／前の回の控え: " + (DropOldBackups ? "消す" : "残す"),
        };
        if (SelectedRun.Value == ScanRun.ResetScans)
            lines.Add("消えるもの: 走査由来のセッションと走査の作業履歴 ／ 残るもの: " + ResetScansKeepsNote);
        else if (SelectedRun.Value == ScanRun.ResetAll)
            lines.Add("消えるもの: セッション全部・リプレイの登録（" + ResetAllWipesNote
                      + "）／ Layer 0 も空から建て直します");
        if (!HitWindowsFromConfig)
        {
            var scopes = HitWindowScopes.Where(s => s.IsOn).Select(s => s.Label).ToList();
            lines.Add("被弾窓（この走査だけ）: " + (HitWindows ? "取る" : "取らない")
                      + "／前 " + HitWindowBeforeText.Trim() + "・後 " + HitWindowAfterText.Trim()
                      + "／クイック " + (HitWindowQuick ? "起点にする" : "起点にしない")
                      + "／遊び方 "
                      + (scopes.Count == 0 ? NoneText : string.Join(",", scopes)));
        }
        return string.Join("\n", lines);
    }

    private static void SetAll(ObservableCollection<ScanPickChip> chips, bool on)
    {
        foreach (var chip in chips) chip.IsSelected = on;
    }

    private static IReadOnlyList<int> Picked(ObservableCollection<ScanPickChip> chips)
    {
        var picked = chips.Where(c => c.IsSelected).Select(c => c.Index).ToList();
        return picked.Count == chips.Count ? [] : picked;
    }

    private static void AddPicked(List<string> into, string name,
                                  ObservableCollection<ScanPickChip> chips)
    {
        var picked = Picked(chips);
        if (picked.Count == 0) return;
        into.Add(name + " " + string.Join(",", chips.Where(c => c.IsSelected).Select(c => c.Label)));
    }

    private static bool TryNumber(string text, string name, out int? value, out string error)
    {
        value = null;
        error = "";
        var trimmed = text.Trim();
        if (trimmed.Length == 0) return true;
        if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            error = name + "は数で書いてください: " + trimmed;
            return false;
        }
        value = parsed;
        return true;
    }

    private static bool TryMinutes(string text, out double? value, out string error)
    {
        value = null;
        error = "";
        var trimmed = text.Trim();
        if (trimmed.Length == 0) return true;
        if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
        {
            error = "総時間の上限は数で書いてください: " + trimmed;
            return false;
        }
        value = parsed;
        return true;
    }
}
