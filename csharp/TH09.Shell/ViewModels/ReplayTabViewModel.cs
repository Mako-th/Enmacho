using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TH09.Shell.Data;
using TH09.Shell.Navigation;

namespace TH09.Shell.ViewModels;

internal sealed record FilterOption(string Label, int? Value);

internal sealed record NameOption(string Label, string? Value);

internal sealed record RevealChoice(string Label, string Directory);

internal sealed record ReplayHeader(ReplayColumn Column, bool IsActive, string Arrow)
{
    public string Label => Column.Label;
    public double Width => Column.Width;
    public bool RightAligned => Column.RightAligned;
    public ReplaySortKey? Key => Column.Key;

    public double LeftSeat => Data.SortMark.LeftSeat(RightAligned);

    public double RightSeat => Data.SortMark.RightSeat(RightAligned);
}

internal sealed partial class ReplayTabViewModel : TabViewModelBase
{
    private readonly List<ReplayListRow> _all = [];

    private bool _applying;

    private ReplayListRow? _contextRow;

    private int? _storyDifficulty;
    private int? _matchDifficulty;

    public ReplayTabViewModel(INavigationService navigation) : base(navigation)
    {
        Reload();
    }

    public override ShellTab Key => ShellTab.Replay;
    public override string Title => "リプレイ";

    public override string Placeholder => "";

    public ObservableCollection<ReplayListRow> Rows { get; } = [];

    [ObservableProperty]
    public partial bool IsMatch { get; set; }

    [ObservableProperty]
    public partial bool OwnOnly { get; set; }

    [ObservableProperty]
    public partial string? StatusText { get; set; }

    [ObservableProperty]
    public partial ReplayListRow? SelectedRow { get; set; }

    public bool ContextHeld
    {
        get => _contextHeld;
        set
        {
            _contextHeld = value;
            if (value) ContextRow = null;
        }
    }

    private bool _contextHeld;

    public ReplayListRow? ContextRow
    {
        get => _contextRow;
        private set
        {
            if (ReferenceEquals(_contextRow, value)) return;
            _contextRow = value;
            RebuildRevealOptions(value);
            OnPropertyChanged(nameof(HasContextRow));
            OnPropertyChanged(nameof(OwnIsAuto));
            OnPropertyChanged(nameof(OwnIsForeign));
            OnPropertyChanged(nameof(OwnIsP1));
            OnPropertyChanged(nameof(OwnIsP2));
            OnPropertyChanged(nameof(OwnAutoText));
            OnPropertyChanged(nameof(OwnForeignText));
            OnPropertyChanged(nameof(OwnP1Text));
            OnPropertyChanged(nameof(OwnP2Text));
        }
    }

    public bool HasContextRow => _contextRow is not null;

    public bool OwnIsAuto => _contextRow?.OwnOverride is null;

    public bool OwnIsForeign => _contextRow?.OwnOverride == TH09.Record.ReplayOwnership.Foreign;

    public bool OwnIsP1 => _contextRow?.OwnOverride == TH09.Record.ReplayOwnership.OwnP1;

    public bool OwnIsP2 => _contextRow?.OwnOverride == TH09.Record.ReplayOwnership.OwnP2;

    public string OwnAutoText => ReplayOwnLabels.Item(ReplayOwnLabels.Auto, OwnIsAuto);

    public string OwnForeignText => ReplayOwnLabels.Item(ReplayOwnLabels.Foreign, OwnIsForeign);

    public string OwnP1Text => ReplayOwnLabels.Item(ReplayOwnLabels.OwnP1, OwnIsP1);

    public string OwnP2Text => ReplayOwnLabels.Item(ReplayOwnLabels.OwnP2, OwnIsP2);

    [ObservableProperty]
    public partial string? OwnNote { get; set; }

    [ObservableProperty]
    public partial string? RevealNote { get; set; }

    public ObservableCollection<RevealChoice> RevealOptions { get; } = [];

    public string CountText
    {
        get
        {
            var total = 0;
            foreach (var r in _all) if (SectionOf(r)) total++;
            return total.ToString("N0", CultureInfo.InvariantCulture) + " 件中 "
                   + Rows.Count.ToString("N0", CultureInfo.InvariantCulture) + " 件";
        }
    }


    public ObservableCollection<FilterOption> DifficultyOptions { get; } = [];

    public ObservableCollection<FilterOption> CharacterOptions { get; } = [];

    public ObservableCollection<FilterOption> P1CharacterOptions { get; } = [];

    public ObservableCollection<FilterOption> P2CharacterOptions { get; } = [];

    public ObservableCollection<FilterOption> MatchModeOptions { get; } = [];

    public ObservableCollection<NameOption> PlayerNameOptions { get; } = [];

    [ObservableProperty]
    public partial FilterOption? SelectedDifficulty { get; set; }

    [ObservableProperty]
    public partial FilterOption? SelectedCharacter { get; set; }

    [ObservableProperty]
    public partial FilterOption? SelectedP1Character { get; set; }

    [ObservableProperty]
    public partial FilterOption? SelectedP2Character { get; set; }

    [ObservableProperty]
    public partial FilterOption? SelectedMatchMode { get; set; }

    [ObservableProperty]
    public partial NameOption? SelectedPlayerName { get; set; }


    public ObservableCollection<ReplayHeader> Headers { get; } = [];

    public bool ShowRoundTimes => IsMatch;

    private ReplaySortKey _storySort = ReplaySortKey.DateTime;
    private bool _storyDescending = true;
    private ReplaySortKey _matchSort = ReplaySortKey.DateTime;
    private bool _matchDescending = true;

    public ReplaySortKey SortKey => IsMatch ? _matchSort : _storySort;

    public bool SortDescending => IsMatch ? _matchDescending : _storyDescending;


    [RelayCommand]
    private void ShowStory() => IsMatch = false;

    [RelayCommand]
    private void ShowMatch() => IsMatch = true;

    [RelayCommand]
    private void SortBy(ReplaySortKey? key)
    {
        if (key is not ReplaySortKey k) return;
        if (IsMatch)
        {
            _matchDescending = _matchSort == k ? !_matchDescending : DefaultDescending(k);
            _matchSort = k;
        }
        else
        {
            _storyDescending = _storySort == k ? !_storyDescending : DefaultDescending(k);
            _storySort = k;
        }
        Apply();
    }

    [RelayCommand]
    private void ClearFilters()
    {
        _applying = true;
        OwnOnly = false;
        if (IsMatch) _matchDifficulty = null; else _storyDifficulty = null;
        SelectedDifficulty = DifficultyOptions.FirstOrDefault();
        SelectedCharacter = CharacterOptions.FirstOrDefault();
        SelectedP1Character = P1CharacterOptions.FirstOrDefault();
        SelectedP2Character = P2CharacterOptions.FirstOrDefault();
        SelectedMatchMode = MatchModeOptions.FirstOrDefault();
        SelectedPlayerName = PlayerNameOptions.FirstOrDefault();
        _applying = false;
        Apply();
    }

    [RelayCommand]
    private void Reload()
    {
        _all.Clear();
        StatusText = null;
        try
        {
            if (!TrackerDb.MainDbExists)
            {
                StatusText = TrackerDb.MainDbPath is null
                    ? "本体 DB の場所が未設定（TrackerDb.MainDbPath）。"
                    : "本体 DB が見つかりません: " + TrackerDb.MainDbPath;
            }
            else
            {
                using var db = TrackerDb.OpenMainDb();
                _all.AddRange(ReplayListQuery.LoadAll(db));
            }
        }
        catch (Exception ex)
        {
            StatusText = "本体 DB を読めませんでした: " + ex.Message;
        }
        BuildOptions();
        Apply();
    }


    partial void OnIsMatchChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowRoundTimes));
        BuildDifficultyOptions();
        Apply();
    }

    partial void OnOwnOnlyChanged(bool value) => Apply();

    partial void OnSelectedDifficultyChanged(FilterOption? value)
    {
        if (!_applying)
        {
            if (IsMatch) _matchDifficulty = value?.Value;
            else _storyDifficulty = value?.Value;
        }
        Apply();
    }
    partial void OnSelectedCharacterChanged(FilterOption? value) => Apply();
    partial void OnSelectedP1CharacterChanged(FilterOption? value) => Apply();
    partial void OnSelectedP2CharacterChanged(FilterOption? value) => Apply();
    partial void OnSelectedMatchModeChanged(FilterOption? value) => Apply();
    partial void OnSelectedPlayerNameChanged(NameOption? value) => Apply();

    partial void OnSelectedRowChanged(ReplayListRow? value)
    {
        if (value is null || _applying) return;
        var row = value;
        _applying = true;
        SelectedRow = null;
        _applying = false;
        if (ContextHeld) { ContextRow = row; return; }
        ContextRow = null;
        Navigation.OpenReplayDetail(ReplayDetailRequest.FromReplay(row.ReplayId, row.SessionId));
    }

    public string? RevealDirectory(ReplayListRow? row, string? chosenDirectory = null)
    {
        if (chosenDirectory is not null)
        {
            RevealNote = null;
            return chosenDirectory;
        }
        if (row is null) return null;
        var candidates = LoadRevealCandidates(row.ReplayId);
        if (candidates.Count >= 2)
        {
            RevealNote = null;
            return null;
        }
        var target = candidates.Count == 1
            ? new RevealTarget(candidates[0].Directory, null)
            : ReplayFileReveal.For(row.FullPath);
        RevealNote = target.Note;
        return target.Directory;
    }

    private void RebuildRevealOptions(ReplayListRow? row)
    {
        RevealOptions.Clear();
        if (row is null) return;
        var candidates = LoadRevealCandidates(row.ReplayId);
        if (candidates.Count < 2) return;
        foreach (var c in candidates) RevealOptions.Add(new RevealChoice(c.Label, c.Directory));
    }

    private static IReadOnlyList<ReplayFileReveal.RevealCandidate> LoadRevealCandidates(long replayId)
    {
        if (!TrackerDb.MainDbExists) return [];
        try
        {
            using var db = TrackerDb.OpenMainDb();
            var rows = ReplayDetailQuery.LoadFiles(db, replayId, sessionId: null);
            return ReplayFileReveal.Candidates(rows);
        }
        catch
        {
            return [];
        }
    }


    [RelayCommand]
    private void SetOwnAuto() => ApplyOwnOverride(null, ReplayOwnLabels.Auto);

    [RelayCommand]
    private void SetOwnForeign()
        => ApplyOwnOverride(TH09.Record.ReplayOwnership.Foreign, ReplayOwnLabels.Foreign);

    [RelayCommand]
    private void SetOwnP1()
        => ApplyOwnOverride(TH09.Record.ReplayOwnership.OwnP1, ReplayOwnLabels.OwnP1);

    [RelayCommand]
    private void SetOwnP2()
        => ApplyOwnOverride(TH09.Record.ReplayOwnership.OwnP2, ReplayOwnLabels.OwnP2);

    private void ApplyOwnOverride(int? value, string label)
    {
        if (ContextRow is not ReplayListRow row) return;
        if (TrackerDb.MainDbPath is not string main || !TrackerDb.MainDbExists)
        {
            OwnNote = ReplayOwnLabels.NoDb;
            return;
        }
        try
        {
            var r = TH09.Record.ReplayOwnership.SetOverride(
                main, [row.ReplayId], value,
                line => LogSource.Warn(ReplayOwnLabels.Category, line));
            if (r.Missing.Count > 0)
            {
                OwnNote = "replay_id " + row.ReplayId.ToString(CultureInfo.InvariantCulture)
                          + " の行が本体 DB にありません（覆していません）。";
                return;
            }
            OwnNote = ReplayOwnLabels.Applied(row.ReplayId, label, r.Changed > 0);
            LogSource.Info(ReplayOwnLabels.Category,
                           "[GUI] " + ReplayOwnLabels.Menu + ": replay_id "
                           + row.ReplayId.ToString(CultureInfo.InvariantCulture)
                           + " ★ " + label
                           + "（変わった " + r.Changed.ToString(CultureInfo.InvariantCulture) + " 件）");
        }
        catch (Exception ex)
        {
            LogSource.Error(ReplayOwnLabels.Category,
                            ReplayOwnLabels.Menu + ": " + LogSource.Describe(ex));
            OwnNote = ReplayOwnLabels.Menu + "に失敗しました: " + ex.Message;
            return;
        }
        ReloadKeepingFilters();
    }

    private void ReloadKeepingFilters()
    {
        var ch = SelectedCharacter?.Value;
        var p1 = SelectedP1Character?.Value;
        var p2 = SelectedP2Character?.Value;
        var mode = SelectedMatchMode?.Value;
        var name = SelectedPlayerName?.Value;

        Reload();

        _applying = true;
        SelectedCharacter = CharacterOptions.FirstOrDefault(o => o.Value == ch) ?? CharacterOptions[0];
        SelectedP1Character = P1CharacterOptions.FirstOrDefault(o => o.Value == p1) ?? P1CharacterOptions[0];
        SelectedP2Character = P2CharacterOptions.FirstOrDefault(o => o.Value == p2) ?? P2CharacterOptions[0];
        SelectedMatchMode = MatchModeOptions.FirstOrDefault(o => o.Value == mode) ?? MatchModeOptions[0];
        SelectedPlayerName = PlayerNameOptions.FirstOrDefault(o => o.Value == name) ?? PlayerNameOptions[0];
        _applying = false;

        ContextRow = null;
        Apply();
    }

    private bool SectionOf(ReplayListRow row)
        => row.Section == (IsMatch ? ReplaySection.Match : ReplaySection.StoryExtra);

    private void Apply()
    {
        if (_applying) return;
        var section = IsMatch ? ReplaySection.Match : ReplaySection.StoryExtra;
        var filter = new ReplayListFilter(
            Section: section,
            OwnOnly: OwnOnly,
            Difficulty: SelectedDifficulty?.Value,
            AnyCharacter: IsMatch ? null : SelectedCharacter?.Value,
            P1Character: IsMatch ? SelectedP1Character?.Value : null,
            P2Character: IsMatch ? SelectedP2Character?.Value : null,
            Mode: IsMatch && SelectedMatchMode?.Value is int m ? (MatchMode)m : null,
            PlayerName: IsMatch ? SelectedPlayerName?.Value : null);

        var rows = ReplayListQuery.Filter(_all, filter);
        ReplayListQuery.Sort(rows, SortKey, SortDescending);

        _applying = true;
        Rows.Clear();
        foreach (var r in rows) Rows.Add(r);
        _applying = false;

        BuildHeaders(section);
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(SortKey));
        OnPropertyChanged(nameof(SortDescending));
    }

    private void BuildHeaders(ReplaySection section)
    {
        Headers.Clear();
        foreach (var col in ReplayColumns.For(section))
        {
            var active = col.Key == SortKey;
            Headers.Add(new ReplayHeader(col, active,
                                         Data.SortMark.Of(active, SortDescending)));
        }
    }

    private void BuildDifficultyOptions()
    {
        var was = _applying;
        _applying = true;
        var section = IsMatch ? ReplaySection.Match : ReplaySection.StoryExtra;
        Fill(DifficultyOptions, "難易度 すべて",
             Distinct(_all.Where(r => r.Section == section).Select(r => r.Difficulty)).OrderBy(v => v)
                 .Select(v => new FilterOption(ReplayLabels.Difficulty(v), v)));
        var want = IsMatch ? _matchDifficulty : _storyDifficulty;
        SelectedDifficulty = DifficultyOptions.FirstOrDefault(o => o.Value == want)
                             ?? DifficultyOptions[0];
        _applying = was;
    }

    private void BuildOptions()
    {
        _applying = true;

        BuildDifficultyOptions();

        Fill(CharacterOptions, "キャラ すべて",
             Distinct(_all.Where(r => r.Section == ReplaySection.StoryExtra)
                          .SelectMany(r => new[] { r.P1Character, r.P2Character }))
                 .OrderBy(v => ReplayLabels.DisplayRank(v) ?? int.MaxValue)
                 .Select(v => new FilterOption(ReplayLabels.Character(v), v)));

        Fill(P1CharacterOptions, "1Pキャラ すべて",
             Distinct(_all.Where(r => r.Section == ReplaySection.Match).Select(r => r.P1Character))
                 .OrderBy(v => ReplayLabels.DisplayRank(v) ?? int.MaxValue)
                 .Select(v => new FilterOption(ReplayLabels.Character(v), v)));

        Fill(P2CharacterOptions, "2Pキャラ すべて",
             Distinct(_all.Where(r => r.Section == ReplaySection.Match).Select(r => r.P2Character))
                 .OrderBy(v => ReplayLabels.DisplayRank(v) ?? int.MaxValue)
                 .Select(v => new FilterOption(ReplayLabels.Character(v), v)));

        Fill(MatchModeOptions, "Match Mode すべて",
        [
            new FilterOption(ReplayLabels.HumanVsHumanText, (int)MatchMode.HumanVsHuman),
            new FilterOption(ReplayLabels.HumanVsCpuText, (int)MatchMode.HumanVsCpu),
            new FilterOption(ReplayLabels.CpuVsHumanText, (int)MatchMode.CpuVsHuman),
            new FilterOption(ReplayLabels.CpuVsCpuText, (int)MatchMode.CpuVsCpu),
        ]);

        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var r in _all)
        {
            if (r.Section != ReplaySection.Match) continue;
            if (r.P1Name is string a) names.Add(a);
            if (r.P2Name is string b) names.Add(b);
        }
        PlayerNameOptions.Clear();
        PlayerNameOptions.Add(new NameOption("プレイヤー名 すべて", null));
        foreach (var n in names) PlayerNameOptions.Add(new NameOption(n, n));

        SelectedCharacter = CharacterOptions[0];
        SelectedP1Character = P1CharacterOptions[0];
        SelectedP2Character = P2CharacterOptions[0];
        SelectedMatchMode = MatchModeOptions[0];
        SelectedPlayerName = PlayerNameOptions[0];

        _applying = false;
    }

    private static IEnumerable<int> Distinct(IEnumerable<int?> values)
    {
        var seen = new HashSet<int>();
        foreach (var v in values) if (v is int i && seen.Add(i)) yield return i;
    }

    private static void Fill(ObservableCollection<FilterOption> target, string allLabel,
                             IEnumerable<FilterOption> options)
    {
        target.Clear();
        target.Add(new FilterOption(allLabel, null));
        foreach (var o in options) target.Add(o);
    }

    private static bool DefaultDescending(ReplaySortKey key) => key switch
    {
        ReplaySortKey.DateTime or ReplaySortKey.Score or ReplaySortKey.Lives
            or ReplaySortKey.Reach or ReplaySortKey.Time => true,
        _ => false,
    };
}
