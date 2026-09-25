using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.Versioning;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TH09.Record;
using TH09.Shell.Data;

namespace TH09.Shell.ViewModels;

internal sealed partial class SettingScopeChoice : ObservableObject
{
    public SettingScopeChoice(string name, string label, bool isOn)
    {
        Name = name;
        Label = label;
        IsOn = isOn;
    }

    public string Name { get; }

    public string Label { get; }

    [ObservableProperty]
    public partial bool IsOn { get; set; }
}

internal sealed partial class SettingStatsChoice : ObservableObject
{
    public SettingStatsChoice(string label, bool isShown)
    {
        Label = label;
        IsShown = isShown;
    }

    public string Label { get; }

    [ObservableProperty]
    public partial bool IsShown { get; set; }
}

internal sealed partial class SettingCompareChoice : ObservableObject
{
    public SettingCompareChoice(string kind, bool isShown)
    {
        Kind = kind;
        IsShown = isShown;
    }

    public string Kind { get; }

    public string Label => Kind;

    [ObservableProperty]
    public partial bool IsShown { get; set; }
}

internal sealed partial class SettingColumnEntry : ObservableObject
{
    public const string NoNameText = "（名前なし）";

    public const string CardSeparatorText = "区切り（線だけ）";

    public SettingColumnEntry(StatsLayoutPart part, string name, bool isSeparator, string where,
                              bool isShown, bool isHeading = false, bool isPinned = false)
    {
        Part = part;
        Name = name;
        IsSeparator = isSeparator;
        Where = where;
        IsShown = isShown;
        IsHeading = isHeading;
        IsPinned = isPinned;
    }

    public StatsLayoutPart Part { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    public partial string Name { get; set; }

    public bool IsSeparator { get; }

    public bool IsHeading { get; }

    public bool IsPinned { get; }

    public bool IsItem => !IsSeparator && !IsHeading;

    public bool IsMovable => !IsHeading && !IsPinned;

    public bool CanRename => IsSeparator && !Part.Cards;

    public string Where { get; }

    [ObservableProperty]
    public partial bool IsShown { get; set; }

    public string Label
        => IsHeading ? Part.Title
           : !IsSeparator ? Name
           : Part.Cards ? CardSeparatorText
           : Name.Length == 0 ? NoNameText : Name;

    public FontWeight RowWeight
        => IsSeparator || IsHeading ? FontWeight.Bold : FontWeight.Normal;

    public StatsLayoutEntry ToEntry()
        => IsSeparator ? StatsLayoutEntry.Separator(Name) : StatsLayoutEntry.Item(Name);
}

internal sealed partial class SettingColumnPage : ObservableObject
{
    public SettingColumnPage(StatsSection section, string label)
    {
        Section = section;
        Label = label;
    }

    public StatsSection Section { get; }

    public string Label { get; }

    public ObservableCollection<SettingColumnEntry> Rows { get; } = [];

    [ObservableProperty]
    public partial bool IsCurrent { get; set; }
}

internal sealed partial class SettingTargetChip : ObservableObject
{
    public SettingTargetChip(long value, string label)
    {
        Value = value;
        Label = label;
    }

    public long Value { get; }

    public string Label { get; }

    [ObservableProperty]
    public partial bool IsCurrent { get; set; }
}

internal sealed partial class SettingTargetRow : ObservableObject
{
    public SettingTargetRow(long? round, string label)
    {
        Round = round;
        Label = label;
    }

    public long? Round { get; }

    public string Label { get; }

    [ObservableProperty]
    public partial string TargetText { get; set; } = "";

    [ObservableProperty]
    public partial string WrText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHint))]
    public partial string Hint { get; set; } = "";

    public bool HasHint => Hint.Length > 0;
}

[SupportedOSPlatform("windows")]
internal sealed partial class AppSettingsViewModel : ObservableObject
{
    public const string Title = "設定";

    public const string SavedText =
        "保存しました。統計に出す区分と、要約カード・内訳表の列は、次に起動したときから変わります。"
        + "ゲームのフォルダ・追加の置き場は、次のリプレイの登録"
        + "（起動時の自動登録・Replay保存監視・既存Replay一括登録）から効きます。";


    private readonly string configPath;

    private readonly Paths gamePaths;

    public AppSettingsViewModel() : this(AppSettingsSource.ConfigPath) { }

    public AppSettingsViewModel(string configPath)
    {
        this.configPath = configPath;
        gamePaths = Paths.Resolve(
            Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? Directory.GetCurrentDirectory());
        foreach (var name in ModeDisplay.Scopes)
            HitWindowScopes.Add(new SettingScopeChoice(name, ModeDisplay.Label(name), true));
        foreach (var section in ModeDisplay.Sections)
            StatsItems.Add(new SettingStatsChoice(StatsSections.Title(section), true));
        foreach (var kind in StreamBests.AllKinds)
        {
            StreamPanelItems.Add(new SettingCompareChoice(kind, true));
            PlayTabItems.Add(new SettingCompareChoice(kind, true));
        }
        foreach (var choice in StreamPanelViewModel.Backgrounds) StreamBackgrounds.Add(choice);
        Apply(AppSettingsSource.Current);
    }

    internal Paths GamePaths => gamePaths;

    public event Action? Adopted;

    public string ConfigPathText => configPath;

    public string Header => Title;


    [ObservableProperty]
    public partial bool RecordReplayPlayback { get; set; }

    [ObservableProperty]
    public partial string RecordReplayToggleKey { get; set; } = MonitorToggleKeyReading.Known;

    [ObservableProperty]
    public partial string HistoryRetentionAbortedText { get; set; } = "";

    [ObservableProperty]
    public partial string HistoryRetentionCompletedText { get; set; } = "";

    [ObservableProperty]
    public partial bool HistoryRetentionAbortedUnlimited { get; set; }

    [ObservableProperty]
    public partial bool HistoryRetentionCompletedUnlimited { get; set; }

    partial void OnHistoryRetentionAbortedUnlimitedChanged(bool value)
    {
        if (value) HistoryRetentionAbortedText = "";
    }

    partial void OnHistoryRetentionCompletedUnlimitedChanged(bool value)
    {
        if (value) HistoryRetentionCompletedText = "";
    }

    [ObservableProperty]
    public partial int TickHookIndex { get; set; }

    [ObservableProperty]
    public partial bool HitWindows { get; set; }

    [ObservableProperty]
    public partial string HitWindowBeforeText { get; set; } = "";

    [ObservableProperty]
    public partial string HitWindowAfterText { get; set; } = "";

    public ObservableCollection<SettingScopeChoice> HitWindowScopes { get; } = [];

    [ObservableProperty]
    public partial bool HitWindowQuick { get; set; }

    [ObservableProperty]
    public partial bool AutoMonitorOnGame { get; set; }

    [ObservableProperty]
    public partial bool WatchReplaysWithMonitor { get; set; }

    [ObservableProperty]
    public partial bool BackupKeepOne { get; set; }

    public ObservableCollection<SettingStatsChoice> StatsItems { get; } = [];

    public ObservableCollection<SettingCompareChoice> StreamPanelItems { get; } = [];

    public ObservableCollection<SettingCompareChoice> PlayTabItems { get; } = [];


    public ObservableCollection<string> OwnPlayerNames { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRemoveOwnPlayerName))]
    public partial int SelectedOwnPlayerNameIndex { get; set; } = -1;

    public bool CanRemoveOwnPlayerName
        => SelectedOwnPlayerNameIndex >= 0 && SelectedOwnPlayerNameIndex < OwnPlayerNames.Count;

    [ObservableProperty]
    public partial string OwnPlayerNameDraft { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOwnPlayerNameNote))]
    public partial string OwnPlayerNameNote { get; set; } = "";

    public bool HasOwnPlayerNameNote => OwnPlayerNameNote.Length > 0;

    partial void OnOwnPlayerNameDraftChanged(string value) => OwnPlayerNameNote = "";


    public const string GameDirUnknownText =
        "まだ分かりません（監視で花映塚を見つけると自動で覚えます）。";

    public const string UserDataDirNoneText =
        "ありません（ゲームのフォルダが Program Files の外なら、通常は作られません）。";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GameDirDisplayText))]
    [NotifyPropertyChangedFor(nameof(UserDataDirDisplayText))]
    [NotifyPropertyChangedFor(nameof(GameDirNote))]
    [NotifyPropertyChangedFor(nameof(HasGameDirNote))]
    public partial string GameDirText { get; set; } = "";

    public string GameDirDisplayText
        => GameDirText.Length > 0 ? GameDirText : GameDirUnknownText;

    private string gameDirLoaded = "";

    public string GameDirNote
        => GameDirText.Length > 0 && !SafeFileExists(Path.Combine(GameDirText, "th09.exe"))
            ? "このフォルダに th09.exe が見当たりません（それでも保存できます）。" : "";

    public bool HasGameDirNote => GameDirNote.Length > 0;

    public void SetGameDir(string? path)
    {
        var trimmed = (path ?? "").Trim();
        if (trimmed.Length == 0) return;
        GameDirText = trimmed;
    }

    internal string? VirtualStoreRootOverride { get; set; }

    public string UserDataDirDisplayText
    {
        get
        {
            if (GameDirText.Length == 0) return "-";
            return Paths.UserDataDirFor(GameDirText, VirtualStoreRootOverride) ?? UserDataDirNoneText;
        }
    }

    public ObservableCollection<string> ExtraReplayDirs { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRemoveExtraReplayDir))]
    public partial int SelectedExtraReplayDirIndex { get; set; } = -1;

    public bool CanRemoveExtraReplayDir
        => SelectedExtraReplayDirIndex >= 0 && SelectedExtraReplayDirIndex < ExtraReplayDirs.Count;

    public void AddExtraReplayDir(string? path)
    {
        var trimmed = (path ?? "").Trim();
        if (trimmed.Length == 0) return;
        if (ExtraReplayDirs.Any(d => string.Equals(d, trimmed, StringComparison.OrdinalIgnoreCase)))
            return;
        ExtraReplayDirs.Add(trimmed);
    }

    [RelayCommand]
    private void RemoveExtraReplayDir()
    {
        if (!CanRemoveExtraReplayDir) return;
        ExtraReplayDirs.RemoveAt(SelectedExtraReplayDirIndex);
        SelectedExtraReplayDirIndex = -1;
    }

    private static bool SafeFileExists(string path)
    {
        try { return File.Exists(path); }
        catch (Exception) { return false; }
    }


    public ObservableCollection<SettingColumnPage> StatsPages { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatsColumns))]
    public partial int SelectedStatsPageIndex { get; set; }

    partial void OnSelectedStatsPageIndexChanged(int value)
    {
        for (var i = 0; i < StatsPages.Count; i++) StatsPages[i].IsCurrent = i == value;
        SelectedColumnIndex = -1;
        NotifyColumnState();
    }

    [RelayCommand]
    private void SelectStatsPage(SettingColumnPage? page)
    {
        var at = page is null ? -1 : StatsPages.IndexOf(page);
        if (at >= 0) SelectedStatsPageIndex = at;
    }

    public ObservableCollection<SettingColumnEntry> StatsColumns
        => SelectedStatsPageIndex >= 0 && SelectedStatsPageIndex < StatsPages.Count
            ? StatsPages[SelectedStatsPageIndex].Rows : [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedColumn))]
    [NotifyPropertyChangedFor(nameof(CanMoveColumnUp))]
    [NotifyPropertyChangedFor(nameof(CanMoveColumnDown))]
    [NotifyPropertyChangedFor(nameof(CanRemoveSeparator))]
    [NotifyPropertyChangedFor(nameof(CanRenameSeparator))]
    [NotifyPropertyChangedFor(nameof(CanAddSeparator))]
    [NotifyPropertyChangedFor(nameof(SelectedSeparatorName))]
    public partial int SelectedColumnIndex { get; set; } = -1;

    public SettingColumnEntry? SelectedColumn
        => SelectedColumnIndex >= 0 && SelectedColumnIndex < StatsColumns.Count
            ? StatsColumns[SelectedColumnIndex] : null;

    public bool CanMoveColumnUp => CanSwap(SelectedColumnIndex, -1);

    public bool CanMoveColumnDown => CanSwap(SelectedColumnIndex, 1);

    public bool CanRemoveSeparator => SelectedColumn is { IsSeparator: true, IsPinned: false };

    public bool CanRenameSeparator => SelectedColumn is { CanRename: true };

    public bool CanAddSeparator => SelectedColumn is { IsHeading: false };

    private bool CanSwap(int at, int delta)
    {
        if (SelectedColumn is not { IsMovable: true } row) return false;
        var to = at + delta;
        if (to < 0 || to >= StatsColumns.Count) return false;
        var other = StatsColumns[to];
        return other.IsMovable && other.Part == row.Part;
    }

    public string SelectedSeparatorName
    {
        get => SelectedColumn is { IsSeparator: true } e ? e.Name : "";
        set
        {
            if (SelectedColumn is not { IsSeparator: true } e || e.Name == value) return;
            e.Name = value;
            OnPropertyChanged();
        }
    }

    public const string StatsColumnsNote =
        "区分ごとに分かれていて、その中は段（プレイ・面・対戦）と、要約カード／内訳表の列で分かれます。"
        + "小見出しをまたいで動かすことはできません。"
        + "各段の内訳表のいちばん上の区切りは動かせません・消せません（名前だけ変えられます）。"
        + "要約カードの区切りは線だけで、名前は付きません。"
        + "行の名前の列（自キャラ・面など）はこの一覧に出ません ——いつも左端に出ます。";

    public string StatsColumnsNoteText => StatsColumnsNote;

    public const string NewSeparatorName = "区切り";


    [ObservableProperty]
    public partial string StreamWidthText { get; set; } = "";

    [ObservableProperty]
    public partial string StreamHeightText { get; set; } = "";

    [ObservableProperty]
    public partial string StreamFontScaleText { get; set; } = "";

    public ObservableCollection<StreamPanelBackgroundChoice> StreamBackgrounds { get; } = [];

    [ObservableProperty]
    public partial int StreamBackgroundIndex { get; set; }

    [ObservableProperty]
    public partial bool StreamTopmost { get; set; }

    [ObservableProperty]
    public partial bool StreamBorderless { get; set; }


    private readonly Dictionary<(long Mode, long Difficulty, long Character, long Stage, long Round),
                                (string Target, string Wr)> targetCells = [];

    private readonly Dictionary<(long Mode, long Difficulty, long Character, long Stage), string>
        targetBonuses = [];

    private const long WholeKey = 0;

    public ObservableCollection<SettingTargetChip> TargetModes { get; } = [];

    public ObservableCollection<SettingTargetChip> TargetDifficulties { get; } = [];

    public ObservableCollection<SettingTargetChip> TargetCharacters { get; } = [];

    public ObservableCollection<SettingTargetChip> TargetStages { get; } = [];

    public ObservableCollection<SettingTargetRow> TargetRows { get; } = [];

    [ObservableProperty]
    public partial long TargetMode { get; set; } = StreamTargetForm.StoryMode;

    [ObservableProperty]
    public partial long TargetDifficulty { get; set; }

    [ObservableProperty]
    public partial long TargetCharacter { get; set; }

    [ObservableProperty]
    public partial long TargetStage { get; set; } = 1;

    [ObservableProperty]
    public partial string TargetClearBonusText { get; set; } = "";

    public bool HasTargetStages => TargetStages.Count > 0;

    public bool HasTargetClearBonus => StreamTargetForm.HasClearBonus(TargetMode);

    public string TargetWhereText
    {
        get
        {
            var where = StreamTargetForm.ModeLabel(TargetMode)
                        + " / " + StreamTargetForm.DifficultyLabel(TargetDifficulty)
                        + " / " + ReplayLabels.Character((int)TargetCharacter);
            if (StreamTargetForm.HasClearBonus(TargetMode))
                where += " / S" + TargetStage.ToString(CultureInfo.InvariantCulture);
            return where + "（単位: " + StreamTargetForm.UnitLabel(TargetMode) + "）";
        }
    }

    public const string TargetNote =
        "Target・WR は「クリアボーナスを抜いた素の値」を入れます。"
        + "面のクリアボーナスを入れると、その行の下に「見たまま（素の値 ＋ CB）」が出ます。"
        + "空欄は未入力（配信パネル・プレイタブでは「—」）。";

    public const string TargetNoteMatch =
        "Target・WR は秒で入れます（配信パネル・プレイタブには 分:秒 で出ます）。"
        + "空欄は未入力（配信パネル・プレイタブでは「—」）。";

    public string TargetNoteText =>
        StreamTargetForm.HasClearBonus(TargetMode) ? TargetNote : TargetNoteMatch;


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotes))]
    public partial string NotesText { get; set; } = "";

    public bool HasNotes => NotesText.Length > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSaveResult))]
    public partial string SaveResultText { get; set; } = "";

    public bool HasSaveResult => SaveResultText.Length > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasToggleKeyNote))]
    public partial string ToggleKeyNote { get; set; } = "";

    public bool HasToggleKeyNote => ToggleKeyNote.Length > 0;

    partial void OnRecordReplayToggleKeyChanged(string value)
    {
        _ = MonitorToggleKeyReading.Read(value, out var reason);
        ToggleKeyNote = reason;
    }


    [RelayCommand]
    private void Reload()
    {
        var settings = ConfigStore.Load(configPath);
        AppSettingsSource.Adopt(settings);
        Apply(settings);
        Adopted?.Invoke();
        SaveResultText = "";
    }

    [RelayCommand]
    private void Save()
    {
        if (!TryBuild(out var settings, out var error))
        {
            SaveResultText = "★保存しませんでした: " + error;
            return;
        }
        var outcome = ConfigStore.Save(configPath, settings);
        if (!outcome.Written)
        {
            SaveResultText = "★保存しませんでした: " + (outcome.Reason ?? "");
            return;
        }
        if (GameDirText.Length > 0
            && !string.Equals(GameDirText, gameDirLoaded, StringComparison.OrdinalIgnoreCase))
            gamePaths.TryWriteGameDir(GameDirText);
        gamePaths.TryWriteReplayExtraDirs(ExtraReplayDirs);
        var saved = ConfigStore.Load(configPath);
        AppSettingsSource.Adopt(saved);
        Apply(saved);
        Adopted?.Invoke();
        SaveResultText = SavedText;
    }


    [RelayCommand]
    private void AddOwnPlayerName()
    {
        var trimmed = OwnPlayerNameDraft.Trim();
        if (trimmed.Length == 0)
        {
            OwnPlayerNameNote = "名前を入れてください（空欄・空白だけは足せません）。";
            return;
        }
        if (OwnPlayerNames.Contains(trimmed, StringComparer.Ordinal))
        {
            OwnPlayerNameNote = "同じ名前が既にあります: " + trimmed;
            return;
        }
        OwnPlayerNames.Add(trimmed);
        OwnPlayerNameDraft = "";
        OwnPlayerNameNote = "";
    }

    [RelayCommand]
    private void RemoveOwnPlayerName()
    {
        if (!CanRemoveOwnPlayerName) return;
        OwnPlayerNames.RemoveAt(SelectedOwnPlayerNameIndex);
        SelectedOwnPlayerNameIndex = -1;
    }


    [RelayCommand]
    private void SelectTargetMode(SettingTargetChip? chip)
    {
        if (chip is null || chip.Value == TargetMode) return;
        StashTargetRows();
        TargetMode = chip.Value;
        var diffs = StreamTargetForm.DifficultiesOf(TargetMode);
        if (!diffs.Contains(TargetDifficulty)) TargetDifficulty = diffs[0];
        FillTargetChips();
        FillTargetRows();
    }

    [RelayCommand]
    private void SelectTargetDifficulty(SettingTargetChip? chip)
    {
        if (chip is null || chip.Value == TargetDifficulty) return;
        StashTargetRows();
        TargetDifficulty = chip.Value;
        FillTargetChips();
        FillTargetRows();
    }

    [RelayCommand]
    private void SelectTargetCharacter(SettingTargetChip? chip)
    {
        if (chip is null || chip.Value == TargetCharacter) return;
        StashTargetRows();
        TargetCharacter = chip.Value;
        FillTargetChips();
        FillTargetRows();
    }

    [RelayCommand]
    private void SelectTargetStage(SettingTargetChip? chip)
    {
        if (chip is null || chip.Value == TargetStage) return;
        StashTargetRows();
        TargetStage = chip.Value;
        FillTargetChips();
        FillTargetRows();
    }

    [RelayCommand]
    private void ClearTargetPage()
    {
        foreach (var row in TargetRows) { row.TargetText = ""; row.WrText = ""; }
        TargetClearBonusText = "";
        StashTargetRows();
        RefreshTargetHints();
    }


    [RelayCommand]
    private void MoveColumnUp() => MoveColumn(-1);

    [RelayCommand]
    private void MoveColumnDown() => MoveColumn(1);

    [RelayCommand]
    private void AddColumnSeparator()
    {
        if (SelectedColumn is not { IsHeading: false } row) return;
        var at = SelectedColumnIndex + 1;
        var name = row.Part.Cards ? "" : UnusedSeparatorName(row.Part);
        StatsColumns.Insert(at, new SettingColumnEntry(row.Part, name, isSeparator: true,
                                                       where: "", isShown: true));
        SelectedColumnIndex = at;
        NotifyColumnState();
    }

    private string UnusedSeparatorName(StatsLayoutPart part)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in StatsColumns)
        {
            if (row.IsSeparator && row.Part == part) used.Add(row.Name);
        }
        if (!used.Contains(NewSeparatorName)) return NewSeparatorName;
        for (var n = 2; ; n++)
        {
            var name = NewSeparatorName + " " + n.ToString(CultureInfo.InvariantCulture);
            if (!used.Contains(name)) return name;
        }
    }

    [RelayCommand]
    private void RemoveColumnSeparator()
    {
        if (!CanRemoveSeparator) return;
        var at = SelectedColumnIndex;
        StatsColumns.RemoveAt(at);
        SelectedColumnIndex = Math.Min(at, StatsColumns.Count - 1);
        NotifyColumnState();
    }

    [RelayCommand]
    private void ResetColumns()
    {
        if (SelectedStatsPageIndex < 0 || SelectedStatsPageIndex >= StatsPages.Count) return;
        var page = StatsPages[SelectedStatsPageIndex];
        FillPage(page, StatsOrderMap.Empty, StatsOrderMap.Empty);
        SelectedColumnIndex = -1;
        NotifyColumnState();
    }

    private void MoveColumn(int delta)
    {
        if (!CanSwap(SelectedColumnIndex, delta)) return;
        var at = SelectedColumnIndex;
        var to = at + delta;
        StatsColumns.Move(at, to);
        SelectedColumnIndex = to;
        NotifyColumnState();
    }

    private void NotifyColumnState()
    {
        OnPropertyChanged(nameof(SelectedColumn));
        OnPropertyChanged(nameof(CanMoveColumnUp));
        OnPropertyChanged(nameof(CanMoveColumnDown));
        OnPropertyChanged(nameof(CanRemoveSeparator));
        OnPropertyChanged(nameof(CanRenameSeparator));
        OnPropertyChanged(nameof(CanAddSeparator));
        OnPropertyChanged(nameof(SelectedSeparatorName));
    }


    private void Apply(AppSettings s)
    {
        RecordReplayPlayback = s.RecordReplayPlayback;
        RecordReplayToggleKey = s.RecordReplayToggleKey;
        HistoryRetentionAbortedUnlimited = s.HistoryRetentionAborted == 0;
        HistoryRetentionAbortedText = Retention(s.HistoryRetentionAborted);
        HistoryRetentionCompletedUnlimited = s.HistoryRetentionCompleted == 0;
        HistoryRetentionCompletedText = Retention(s.HistoryRetentionCompleted);
        TickHookIndex = (int)s.TickHook;
        HitWindows = s.HitWindows;
        HitWindowBeforeText = Num(s.HitWindowBefore);
        HitWindowAfterText = Num(s.HitWindowAfter);
        foreach (var scope in HitWindowScopes)
            scope.IsOn = s.HitWindowScopes.TryGetValue(scope.Name, out var on) && on;
        HitWindowQuick = s.HitWindowQuick;
        AutoMonitorOnGame = s.AutoMonitorOnGame;
        WatchReplaysWithMonitor = s.WatchReplaysWithMonitor;
        BackupKeepOne = s.BackupKeepOne;
        var hidden = new HashSet<string>(s.StatsHiddenItems, StringComparer.Ordinal);
        foreach (var item in StatsItems) item.IsShown = !hidden.Contains(item.Label);
        ApplyCompare(StreamPanelItems, s.StreamPanelHiddenItems);
        ApplyCompare(PlayTabItems, s.PlayTabHiddenItems);
        ApplyColumns(s.StatsColumnOrder, s.StatsHiddenColumns);
        StreamWidthText = Num(s.StreamMode.Width);
        StreamHeightText = Num(s.StreamMode.Height);
        StreamFontScaleText = s.StreamMode.FontScale.ToString(CultureInfo.InvariantCulture);
        ApplyBackground(s.StreamMode.Background);
        StreamTopmost = s.StreamMode.Topmost;
        StreamBorderless = s.StreamMode.Borderless;
        ApplyTargets(s.StreamTargets);
        OwnPlayerNames.Clear();
        foreach (var name in s.OwnPlayerNames) OwnPlayerNames.Add(name);
        SelectedOwnPlayerNameIndex = -1;
        OwnPlayerNameDraft = "";
        OwnPlayerNameNote = "";
        GameDirText = gamePaths.ReadGameDir() ?? "";
        gameDirLoaded = GameDirText;
        ExtraReplayDirs.Clear();
        foreach (var dir in gamePaths.ReplayExtraDirsRaw()) ExtraReplayDirs.Add(dir);
        SelectedExtraReplayDirIndex = -1;
        var missing = s.Notes.Count(n => n.Kind == ConfigStore.NoteMissing);
        var shown = s.Notes.Where(n => n.Kind != ConfigStore.NoteMissing).Select(n => n.Text).ToList();
        if (missing > 0 && !s.Notes.Any(n => n.Kind == ConfigStore.NoteUnreadable))
            shown.Add($"設定ファイルに書かれていない {missing} 項目は、既定の値で動いています。");
        NotesText = string.Join("\n", shown);
        _ = MonitorToggleKeyReading.Read(RecordReplayToggleKey, out var reason);
        ToggleKeyNote = reason;
    }


    private void ApplyTargets(IReadOnlyList<StreamTargetEntry> entries)
    {
        targetCells.Clear();
        targetBonuses.Clear();
        foreach (var e in entries)
        {
            var stage = e.Stage ?? WholeKey;
            var round = e.Round ?? WholeKey;
            targetCells[(e.Mode, e.Difficulty, e.Character, stage, round)] =
                (StreamTargetForm.Format(e.Mode, e.Target), StreamTargetForm.Format(e.Mode, e.Wr));
            if (e.ClearBonus is long cb)
                targetBonuses[(e.Mode, e.Difficulty, e.Character, stage)] =
                    StreamTargetForm.FormatClearBonus(cb);
        }
        var diffs = StreamTargetForm.DifficultiesOf(TargetMode);
        if (!diffs.Contains(TargetDifficulty)) TargetDifficulty = diffs[0];
        FillTargetChips();
        FillTargetRows();
    }

    private void FillTargetChips()
    {
        Fill(TargetModes, StreamTargetForm.Modes, StreamTargetForm.ModeLabel, TargetMode);
        Fill(TargetDifficulties, StreamTargetForm.DifficultiesOf(TargetMode),
             StreamTargetForm.DifficultyLabel, TargetDifficulty);
        Fill(TargetCharacters, [.. ReplayLabels.DisplayOrder.Select(x => (long)x)],
             v => ReplayLabels.Character((int)v), TargetCharacter);
        Fill(TargetStages, StreamTargetForm.StagesOf(TargetMode),
             v => "S" + v.ToString(CultureInfo.InvariantCulture), TargetStage);
        OnPropertyChanged(nameof(HasTargetStages));
        OnPropertyChanged(nameof(HasTargetClearBonus));
        OnPropertyChanged(nameof(TargetWhereText));
        OnPropertyChanged(nameof(TargetNoteText));

        static void Fill(ObservableCollection<SettingTargetChip> into, IReadOnlyList<long> values,
                         Func<long, string> label, long current)
        {
            var same = into.Count == values.Count;
            if (same)
            {
                for (var i = 0; i < values.Count; i++)
                    if (into[i].Value != values[i]) { same = false; break; }
            }
            if (!same)
            {
                into.Clear();
                foreach (var v in values) into.Add(new SettingTargetChip(v, label(v)));
            }
            foreach (var chip in into) chip.IsCurrent = chip.Value == current;
        }
    }

    private void FillTargetRows()
    {
        var max = StreamTargetForm.MaxRoundOf(TargetMode);
        if (TargetRows.Count != max + 1)
        {
            foreach (var row in TargetRows) row.PropertyChanged -= OnTargetRowChanged;
            TargetRows.Clear();
            TargetRows.Add(NewRow(null));
            for (var r = 1L; r <= max; r++) TargetRows.Add(NewRow(r));
        }
        foreach (var row in TargetRows)
        {
            var cell = CellOf(row.Round);
            row.TargetText = cell.Target;
            row.WrText = cell.Wr;
        }
        TargetClearBonusText =
            targetBonuses.TryGetValue(StageKey(), out var cb) ? cb : "";
        RefreshTargetHints();

        SettingTargetRow NewRow(long? round)
        {
            var row = new SettingTargetRow(round, StreamTargetForm.RoundLabel(TargetMode, round));
            row.PropertyChanged += OnTargetRowChanged;
            return row;
        }
    }

    private void StashTargetRows()
    {
        foreach (var row in TargetRows)
        {
            var key = CellKey(row.Round);
            if (row.TargetText.Length == 0 && row.WrText.Length == 0) targetCells.Remove(key);
            else targetCells[key] = (row.TargetText, row.WrText);
        }
        var stage = StageKey();
        if (TargetClearBonusText.Length == 0 || !StreamTargetForm.HasClearBonus(TargetMode))
            targetBonuses.Remove(stage);
        else targetBonuses[stage] = TargetClearBonusText;
    }

    private void RefreshTargetHints()
    {
        _ = StreamTargetForm.TryParseClearBonus(TargetClearBonusText, "", out var cb, out _);
        foreach (var row in TargetRows)
        {
            _ = StreamTargetForm.TryParse(TargetMode, row.TargetText, "", out var t, out _);
            _ = StreamTargetForm.TryParse(TargetMode, row.WrText, "", out var w, out _);
            var parts = new List<string>(2);
            if (HintOf(t) is string ts) parts.Add(StreamBests.KindTarget + " " + ts);
            if (HintOf(w) is string ws) parts.Add(StreamBests.KindWr + " " + ws);
            row.Hint = parts.Count == 0
                ? "" : StreamTargetForm.HintLabel(TargetMode) + string.Join("  ", parts);
        }

        string? HintOf(long? raw) => StreamTargetForm.HasClearBonus(TargetMode)
            ? StreamTargetForm.WithClearBonus(TargetMode, raw, cb)
            : StreamTargetForm.PanelText(TargetMode, raw);
    }

    private void OnTargetRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingTargetRow.TargetText) or nameof(SettingTargetRow.WrText))
            RefreshTargetHints();
    }

    partial void OnTargetClearBonusTextChanged(string value) => RefreshTargetHints();

    private (long, long, long, long, long) CellKey(long? round) =>
        (TargetMode, TargetDifficulty, TargetCharacter,
         StreamTargetForm.HasClearBonus(TargetMode) ? TargetStage : WholeKey, round ?? WholeKey);

    private (long, long, long, long) StageKey() =>
        (TargetMode, TargetDifficulty, TargetCharacter,
         StreamTargetForm.HasClearBonus(TargetMode) ? TargetStage : WholeKey);

    private (string Target, string Wr) CellOf(long? round) =>
        targetCells.TryGetValue(CellKey(round), out var cell) ? cell : ("", "");

    private static void ApplyCompare(IEnumerable<SettingCompareChoice> items,
                                     IReadOnlyList<string> hidden)
    {
        var visible = new HashSet<string>(StreamBests.Visible(hidden), StringComparer.Ordinal);
        foreach (var item in items) item.IsShown = visible.Contains(item.Kind);
    }

    private void ApplyColumns(StatsOrderMap order, StatsOrderMap hidden)
    {
        if (StatsPages.Count == 0)
        {
            foreach (var section in ModeDisplay.Sections)
                StatsPages.Add(new SettingColumnPage(section, StatsSections.Title(section)));
        }
        var keep = SelectedColumnIndex;
        foreach (var page in StatsPages) FillPage(page, order, hidden);
        var count = StatsColumns.Count;
        SelectedColumnIndex = count == 0 ? -1 : Math.Clamp(keep, -1, count - 1);
        for (var i = 0; i < StatsPages.Count; i++) StatsPages[i].IsCurrent = i == SelectedStatsPageIndex;
        NotifyColumnState();
    }

    private void FillPage(SettingColumnPage page, StatsOrderMap order, StatsOrderMap hidden)
    {
        var rows = new List<SettingColumnEntry>();
        foreach (var part in StatsLayout.AllParts)
        {
            if (part.Page != page.Section) continue;
            var where = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in StatsLayout.CatalogOf(part)) where[item.Name] = item.Where;
            var hide = new HashSet<string>(hidden.Of(part.PageKey, part.Key), StringComparer.Ordinal);
            var saved = order.Of(part.PageKey, part.Key);
            var entries = saved.Count > 0 ? StatsLayout.ParseOrder(saved) : StatsLayout.DefaultOf(part);

            rows.Add(new SettingColumnEntry(part, part.Title, isSeparator: false, where: "",
                                            isShown: true, isHeading: true));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var first = true;
            foreach (var e in entries)
            {
                if (!e.IsSeparator && !seen.Add(e.Name)) continue;
                if (first && !part.Cards && !e.IsSeparator)
                {
                    rows.Add(Row(part, StatsLayoutEntry.Separator(StatsGroupColumns.LeadBandName),
                                 where, hide, pinned: true));
                }
                rows.Add(Row(part, e, where, hide, pinned: first && !part.Cards && e.IsSeparator));
                first = false;
            }
            if (first && !part.Cards)
            {
                rows.Add(Row(part, StatsLayoutEntry.Separator(StatsGroupColumns.LeadBandName),
                             where, hide, pinned: true));
            }
            foreach (var item in StatsLayout.CatalogOf(part))
            {
                if (seen.Add(item.Name))
                    rows.Add(Row(part, StatsLayoutEntry.Item(item.Name), where, hide));
            }
        }
        page.Rows.Clear();
        foreach (var row in rows) page.Rows.Add(row);
    }

    private static SettingColumnEntry Row(StatsLayoutPart part, StatsLayoutEntry e,
                                          IReadOnlyDictionary<string, string> where,
                                          ICollection<string> hide, bool pinned = false)
        => new(part, e.Name, e.IsSeparator,
               e.IsSeparator ? "" : (where.TryGetValue(e.Name, out var w) ? w : ""),
               e.IsSeparator || !hide.Contains(e.Name), isHeading: false, isPinned: pinned);

    private void ApplyBackground(string hex)
    {
        var known = StreamPanelViewModel.Backgrounds;
        while (StreamBackgrounds.Count > known.Count) StreamBackgrounds.RemoveAt(StreamBackgrounds.Count - 1);
        var at = -1;
        for (var i = 0; i < known.Count; i++)
        {
            if (string.Equals(known[i].Hex, hex, StringComparison.OrdinalIgnoreCase)) { at = i; break; }
        }
        if (at < 0)
        {
            StreamBackgrounds.Add(new StreamPanelBackgroundChoice(hex, hex));
            at = StreamBackgrounds.Count - 1;
        }
        StreamBackgroundIndex = at;
    }

    public bool TryBuild([NotNullWhen(true)] out AppSettings? settings, out string error)
    {
        settings = null;
        if (!TryRetention(HistoryRetentionAbortedUnlimited, HistoryRetentionAbortedText,
                          "中断・リプレイの履歴の件数", out int aborted, out error)) return false;
        if (!TryRetention(HistoryRetentionCompletedUnlimited, HistoryRetentionCompletedText,
                          "完走の履歴の件数", out int completed, out error)) return false;
        if (!TryNumber(HitWindowBeforeText, "被弾窓の前", out int before, out error)) return false;
        if (!TryNumber(HitWindowAfterText, "被弾窓の後", out int after, out error)) return false;
        if (!StatsItems.Any(x => x.IsShown))
        {
            error = "統計に出す区分を 1 つ以上選んでください（全部隠すと区分のボタンが無くなります）。";
            return false;
        }
        var columns = ColumnsForSave();
        if (!TryPositive(StreamWidthText, "配信パネルの幅", out int streamWidth, out error)) return false;
        if (!TryPositive(StreamHeightText, "配信パネルの高さ", out int streamHeight, out error)) return false;
        if (!TryScale(StreamFontScaleText, "配信パネルの字の大きさ", out double fontScale, out error))
            return false;
        if (StreamBackgroundIndex < 0 || StreamBackgroundIndex >= StreamBackgrounds.Count)
        {
            error = "配信パネルの背景色を選んでください。";
            return false;
        }
        if (!TryTargets(out var targets, out error)) return false;
        settings = new AppSettings(
            RecordReplayPlayback: RecordReplayPlayback,
            RecordReplayToggleKey: RecordReplayToggleKey,
            HistoryRetentionAborted: aborted,
            HistoryRetentionCompleted: completed,
            TickHook: Hook(TickHookIndex),
            HitWindows: HitWindows,
            HitWindowBefore: before,
            HitWindowAfter: after,
            HitWindowScopes: HitWindowScopes.ToDictionary(x => x.Name, x => x.IsOn,
                                                          StringComparer.Ordinal),
            HitWindowQuick: HitWindowQuick,
            AutoMonitorOnGame: AutoMonitorOnGame,
            WatchReplaysWithMonitor: WatchReplaysWithMonitor,
            BackupKeepOne: BackupKeepOne,
            StatsHiddenItems: [.. StatsItems.Where(x => !x.IsShown).Select(x => x.Label)],
            StatsColumnOrder: columns.Order,
            StatsHiddenColumns: columns.Hidden,
            StreamMode: AppSettingsSource.Current.StreamMode with
            {
                Width = streamWidth,
                Height = streamHeight,
                FontScale = fontScale,
                Background = StreamBackgrounds[StreamBackgroundIndex].Hex,
                Topmost = StreamTopmost,
                Borderless = StreamBorderless,
            },
            StreamPanelHiddenItems: [.. StreamPanelItems.Where(x => !x.IsShown).Select(x => x.Kind)],
            PlayTabHiddenItems: [.. PlayTabItems.Where(x => !x.IsShown).Select(x => x.Kind)],
            StreamTargets: targets,
            OwnPlayerNames: NormalizedOwnPlayerNames(),
            Notes: []);
        error = "";
        return true;
    }

    private IReadOnlyList<string> NormalizedOwnPlayerNames()
    {
        var result = new List<string>(OwnPlayerNames.Count);
        foreach (var raw in OwnPlayerNames)
        {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0) continue;
            if (!result.Contains(trimmed, StringComparer.Ordinal)) result.Add(trimmed);
        }
        return result;
    }

    private bool TryTargets(out IReadOnlyList<StreamTargetEntry> targets, out string error)
    {
        StashTargetRows();
        targets = [];
        error = "";
        var rows = new List<StreamTargetEntry>();
        foreach (var key in targetCells.Keys.OrderBy(k => k.Mode).ThenBy(k => k.Difficulty)
                                            .ThenBy(k => k.Character).ThenBy(k => k.Stage)
                                            .ThenBy(k => k.Round))
        {
            var (mode, difficulty, character, stage, round) = key;
            var cell = targetCells[key];
            var where = WhereText(mode, difficulty, character, stage, round);
            if (!StreamTargetForm.TryParse(mode, cell.Target, where + " の "
                                           + StreamBests.KindTarget, out var target, out error))
                return false;
            if (!StreamTargetForm.TryParse(mode, cell.Wr, where + " の "
                                           + StreamBests.KindWr, out var wr, out error))
                return false;
            var bonusKey = (mode, difficulty, character, stage);
            long? bonus = null;
            if (round == WholeKey && targetBonuses.TryGetValue(bonusKey, out var cbText)
                && !StreamTargetForm.TryParseClearBonus(cbText, where + " のクリアボーナス",
                                                        out bonus, out error))
                return false;
            rows.Add(new StreamTargetEntry(mode, difficulty, character,
                                           stage == WholeKey ? null : stage,
                                           round == WholeKey ? null : round,
                                           target, wr, bonus));
        }
        var seen = new HashSet<(long, long, long, long)>(
            rows.Where(r => r.Round is null)
                .Select(r => (r.Mode, r.Difficulty, r.Character, r.Stage ?? WholeKey)));
        foreach (var key in targetBonuses.Keys.OrderBy(k => k.Item1).ThenBy(k => k.Item2)
                                              .ThenBy(k => k.Item3).ThenBy(k => k.Item4))
        {
            if (seen.Contains(key)) continue;
            var (mode, difficulty, character, stage) = key;
            var where = WhereText(mode, difficulty, character, stage, WholeKey);
            if (!StreamTargetForm.TryParseClearBonus(targetBonuses[key],
                                                     where + " のクリアボーナス",
                                                     out var bonus, out error))
                return false;
            rows.Add(new StreamTargetEntry(mode, difficulty, character,
                                           stage == WholeKey ? null : stage, null,
                                           null, null, bonus));
        }
        targets = rows;
        return true;

        static string WhereText(long mode, long difficulty, long character, long stage, long round)
        {
            var text = StreamTargetForm.ModeLabel(mode)
                       + " / " + StreamTargetForm.DifficultyLabel(difficulty)
                       + " / " + ReplayLabels.Character((int)character);
            if (stage != WholeKey) text += " / S" + stage.ToString(CultureInfo.InvariantCulture);
            return text + " / " + StreamTargetForm.RoundLabel(mode, round == WholeKey ? null : round);
        }
    }

    private static TickHookMode Hook(int index) => index switch
    {
        1 => TickHookMode.On,
        2 => TickHookMode.Off,
        _ => TickHookMode.Auto,
    };

    private static string Num(int v) => v.ToString(CultureInfo.InvariantCulture);

    private static string Retention(int v) => v == 0 ? "" : Num(v);

    private static bool TryRetention(bool unlimited, string text, string name,
                                     out int value, out string error)
    {
        if (unlimited)
        {
            value = 0;
            error = "";
            return true;
        }
        return TryNumber(text, name, out value, out error);
    }

    private (StatsOrderMap Order, StatsOrderMap Hidden) ColumnsForSave()
    {
        var orderPages = new List<StatsOrderPage>();
        var hiddenPages = new List<StatsOrderPage>();
        foreach (var page in StatsPages)
        {
            var orderSections = new List<StatsOrderSection>();
            var hiddenSections = new List<StatsOrderSection>();
            foreach (var part in StatsLayout.AllParts)
            {
                if (part.Page != page.Section) continue;
                var order = new List<string>();
                var hidden = new List<string>();
                foreach (var row in page.Rows)
                {
                    if (row.Part != part || row.IsHeading) continue;
                    order.Add(row.ToEntry().Text);
                    if (row.IsItem && !row.IsShown) hidden.Add(row.Name);
                }
                if (!SameAsDefault(part, order))
                    orderSections.Add(new StatsOrderSection(part.Key, order));
                if (hidden.Count > 0)
                    hiddenSections.Add(new StatsOrderSection(part.Key, hidden));
            }
            if (orderSections.Count > 0)
                orderPages.Add(new StatsOrderPage(StatsSections.Key(page.Section), orderSections));
            if (hiddenSections.Count > 0)
                hiddenPages.Add(new StatsOrderPage(StatsSections.Key(page.Section), hiddenSections));
        }
        return (new StatsOrderMap(orderPages), new StatsOrderMap(hiddenPages));
    }

    private static bool SameAsDefault(StatsLayoutPart part, IReadOnlyList<string> order)
    {
        var natural = StatsLayout.DefaultOf(part);
        if (order.Count != natural.Count) return false;
        for (var i = 0; i < order.Count; i++)
        {
            if (!string.Equals(order[i], natural[i].Text, StringComparison.Ordinal)) return false;
        }
        return true;
    }

    private static bool TryPositive(string text, string name, out int value, out string error)
    {
        if (!TryNumber(text, name, out value, out error)) return false;
        if (value >= 1) return true;
        error = name + "は 1 以上の数で書いてください: " + value.ToString(CultureInfo.InvariantCulture);
        return false;
    }

    private static bool TryScale(string text, string name, out double value, out string error)
    {
        error = "";
        var trimmed = text.Trim();
        if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            error = name + "は数で書いてください: " + (trimmed.Length == 0 ? "（空欄）" : trimmed);
            return false;
        }
        if (value >= StreamPanelViewModel.MinFontScale && value <= StreamPanelViewModel.MaxFontScale)
            return true;
        error = name + "は "
                + StreamPanelViewModel.MinFontScale.ToString(CultureInfo.InvariantCulture) + "〜"
                + StreamPanelViewModel.MaxFontScale.ToString(CultureInfo.InvariantCulture)
                + " の間で書いてください: " + trimmed;
        return false;
    }

    private static bool TryNumber(string text, string name, out int value, out string error)
    {
        error = "";
        var trimmed = text.Trim();
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            return true;
        error = name + "は数で書いてください: "
                + (trimmed.Length == 0 ? "（空欄）" : trimmed);
        return false;
    }
}
