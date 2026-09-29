using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TH09.Shell.Data;
using TH09.Shell.Navigation;
using ExcludedReplayEntry = TH09.Record.ExcludedReplayEntry;
using ReplayDeleteResult = TH09.Record.ReplayDeleteResult;
using ReplayDescribeRow = TH09.Record.ReplayDescribeRow;
using ReplayDescription = TH09.Record.ReplayDescription;
using ReplayMaintenance = TH09.Record.ReplayMaintenance;
using ReplayOwnershipResult = TH09.Record.ReplayOwnershipResult;

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

internal sealed partial class ReplayTabViewModel : TabViewModelBase, IReloadsOnDbUpdate
{
    private readonly List<ReplayListRow> _all = [];

    private bool _applying;

    private ReplayListRow? _contextRow;

    private int? _storyDifficulty;
    private int? _matchDifficulty;

    private readonly Func<IReadOnlyList<ExcludedReplayEntry>, ExcludedSaveResult> _saveExcluded;

    public ReplayTabViewModel(INavigationService navigation,
                              Func<IReadOnlyList<ExcludedReplayEntry>, ExcludedSaveResult>? saveExcluded = null)
        : base(navigation)
    {
        _saveExcluded = saveExcluded ?? SaveExcludedForReal;
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
            NotifyTargetChanged();
        }
    }

    public bool HasContextRow => _contextRow is not null;


    private readonly HashSet<long> _checked = [];

    public bool CheckHeld { get; set; }

    public int CheckedCount => _checked.Count;

    public string CheckedCountText => ReplayOwnLabels.CheckedCount(_checked.Count);

    public bool HasChecked => _checked.Count > 0;

    public bool AllShownChecked => Rows.Count > 0 && Rows.All(r => _checked.Contains(r.ReplayId));

    [RelayCommand]
    private void ToggleAllShown()
    {
        var all = AllShownChecked;
        foreach (var r in Rows)
        {
            if (all) _checked.Remove(r.ReplayId); else _checked.Add(r.ReplayId);
            r.SetCheckedFromSet(!all);
        }
        NotifyChecksChanged();
    }

    [RelayCommand]
    private void ClearChecks()
    {
        _checked.Clear();
        foreach (var r in Rows) r.SetCheckedFromSet(false);
        NotifyChecksChanged();
    }

    private void OnRowToggled(ReplayListRow row, bool value)
    {
        if (value) _checked.Add(row.ReplayId); else _checked.Remove(row.ReplayId);
        NotifyChecksChanged();
    }

    private void NotifyChecksChanged()
    {
        OnPropertyChanged(nameof(CheckedCount));
        OnPropertyChanged(nameof(CheckedCountText));
        OnPropertyChanged(nameof(HasChecked));
        OnPropertyChanged(nameof(AllShownChecked));
        NotifyTargetChanged();
    }

    public IReadOnlyList<ReplayListRow> TargetRows()
    {
        if (_checked.Count > 0) return [.. _all.Where(r => _checked.Contains(r.ReplayId))];
        return _contextRow is null ? [] : [_contextRow];
    }

    public int TargetCount => _checked.Count > 0 ? _checked.Count : (_contextRow is null ? 0 : 1);

    public bool HasTarget => TargetCount > 0;

    public string TargetLineText
        => TargetCount == 0 ? ReplayOwnLabels.NoTarget
                            : ReplayOwnLabels.TargetLine(TargetCount, _checked.Count > 0);

    public string DeleteMenuText => ReplayOwnLabels.DeleteMenu(TargetCount);

    private ReplayListRow? MarkRow => TargetCount == 1 ? TargetRows()[0] : null;

    private void NotifyTargetChanged()
    {
        OnPropertyChanged(nameof(TargetCount));
        OnPropertyChanged(nameof(HasTarget));
        OnPropertyChanged(nameof(TargetLineText));
        OnPropertyChanged(nameof(DeleteMenuText));
        OnPropertyChanged(nameof(OwnIsAuto));
        OnPropertyChanged(nameof(OwnIsForeign));
        OnPropertyChanged(nameof(OwnIsP1));
        OnPropertyChanged(nameof(OwnIsP2));
        OnPropertyChanged(nameof(OwnAutoText));
        OnPropertyChanged(nameof(OwnForeignText));
        OnPropertyChanged(nameof(OwnP1Text));
        OnPropertyChanged(nameof(OwnP2Text));
    }

    public bool OwnIsAuto => MarkRow is { OwnOverride: null };

    public bool OwnIsForeign => MarkRow?.OwnOverride == TH09.Record.ReplayOwnership.Foreign;

    public bool OwnIsP1 => MarkRow?.OwnOverride == TH09.Record.ReplayOwnership.OwnP1;

    public bool OwnIsP2 => MarkRow?.OwnOverride == TH09.Record.ReplayOwnership.OwnP2;

    public string OwnAutoText => ReplayOwnLabels.Item(ReplayOwnLabels.Auto, OwnIsAuto);

    public string MarkOwnText => ReplayOwnLabels.Item(ReplayOwnLabels.MarkOwn, false);

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
        var alive = new HashSet<long>(_all.Select(r => r.ReplayId));
        _checked.RemoveWhere(id => !alive.Contains(id));
        BuildOptions();
        Apply();
        NotifyChecksChanged();
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
        if (CheckHeld) return;
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

    [RelayCommand]
    private void MarkOwn()
        => RunOwnVerb(ReplayOwnLabels.MarkOwn,
                      (main, ids, log) => TH09.Record.ReplayOwnership.MarkOwn(main, ids, log));


    private PendingDelete? _pendingDelete;

    private sealed record PendingDelete(IReadOnlyList<long> Ids, ReplayDescription Description);

    [ObservableProperty]
    public partial bool IsDeleteConfirmOpen { get; set; }

    [ObservableProperty]
    public partial string DeleteConfirmText { get; set; } = "";

    [RelayCommand]
    private void Delete()
    {
        var ids = TargetRows().Select(t => t.ReplayId).ToList();
        if (ids.Count == 0) return;
        if (TrackerDb.MainDbPath is not string main || !TrackerDb.MainDbExists)
        {
            OwnNote = ReplayOwnLabels.NoDbDelete;
            return;
        }
        if (TrackerDb.Layer0DbPath is null)
        {
            OwnNote = ReplayOwnLabels.NoLayer0Delete;
            LogSource.Warn(ReplayOwnLabels.Category, ReplayOwnLabels.Delete + ": " + ReplayOwnLabels.NoLayer0Delete);
            return;
        }
        ReplayDescription d;
        try
        {
            d = ReplayMaintenance.Describe(main, ids);
        }
        catch (Exception ex)
        {
            LogSource.Error(ReplayOwnLabels.Category, ReplayOwnLabels.Delete + ": " + LogSource.Describe(ex));
            OwnNote = ReplayOwnLabels.Delete + "の見通しを作れませんでした: " + ex.Message;
            return;
        }
        if (d.Rows.Count == 0)
        {
            OwnNote = ReplayOwnLabels.AllMissing(ids.Count);
            LogSource.Warn(ReplayOwnLabels.Category, "[GUI] " + ReplayOwnLabels.Delete + ": " + OwnNote);
            ReloadNowInPlace(DbUpdateCause.ReplayEdited);
            return;
        }
        _pendingDelete = new PendingDelete(ids, d);
        DeleteConfirmText = ReplayOwnLabels.DeleteConfirmText(d.Rows.Count, ReplayMaintenance.SessionsLine(d));
        IsDeleteConfirmOpen = true;
    }

    [RelayCommand]
    private void ConfirmDeleteAndExclude() => RunDelete(exclude: true);

    [RelayCommand]
    private void ConfirmDeleteOnly() => RunDelete(exclude: false);

    [RelayCommand]
    private void CancelDelete()
    {
        IsDeleteConfirmOpen = false;
        _pendingDelete = null;
    }

    private void RunDelete(bool exclude)
    {
        IsDeleteConfirmOpen = false;
        var pending = _pendingDelete;
        _pendingDelete = null;
        if (pending is null) return;
        if (TrackerDb.MainDbPath is not string main || !TrackerDb.MainDbExists)
        {
            OwnNote = ReplayOwnLabels.NoDbDelete;
            return;
        }
        var d = pending.Description;
        var excludedAdded = 0;
        var excludedAlready = 0;
        if (exclude)
        {
            var stamp = DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
            var entries = d.Rows.Select(r => new ExcludedReplayEntry(r.Sha256, CurrentPathOf(r), stamp)).ToList();
            var save = _saveExcluded(entries);
            if (!save.Saved)
            {
                OwnNote = ReplayOwnLabels.ExcludeSaveFailed(save.Reason);
                LogSource.Error(ReplayOwnLabels.Category, "[GUI] " + ReplayOwnLabels.Delete + ": " + OwnNote);
                return;
            }
            excludedAdded = save.Added;
            excludedAlready = save.AlreadyThere;
        }
        ReplayDeleteResult r;
        try
        {
            r = ReplayMaintenance.Delete(main, TrackerDb.Layer0DbPath, pending.Ids,
                                         line => LogSource.Warn(ReplayOwnLabels.Category, line));
        }
        catch (Exception ex)
        {
            LogSource.Error(ReplayOwnLabels.Category, ReplayOwnLabels.Delete + ": " + LogSource.Describe(ex));
            OwnNote = ReplayOwnLabels.DeleteFailed(ex.Message, exclude && (excludedAdded + excludedAlready) > 0);
            ReloadNowInPlace(DbUpdateCause.ReplayEdited);
            return;
        }
        _checked.ExceptWith(pending.Ids);
        OwnNote = ReplayOwnLabels.Deleted(pending.Ids.Count, r.ReplayRows, r.Missing.Count,
                                          r.ScanSessions.Deleted.Count, r.GuessedLinksDetached,
                                          exclude ? excludedAdded : null, excludedAlready);
        LogSource.Info(ReplayOwnLabels.Category, "[GUI] " + ReplayOwnLabels.Delete + ": " + OwnNote);
        ReloadNowInPlace(DbUpdateCause.ReplayEdited);
        DbEdited?.Invoke();
    }

    private static string CurrentPathOf(ReplayDescribeRow row)
    {
        foreach (var (path, isCurrent) in row.Paths) if (isCurrent) return path;
        return row.Paths.Count > 0 ? row.Paths[0].Path : "";
    }

    private static ExcludedSaveResult SaveExcludedForReal(IReadOnlyList<ExcludedReplayEntry> entries)
        => OperatingSystem.IsWindows()
            ? ExcludedReplaysSave.Add(entries)
            : new ExcludedSaveResult(false, 0, 0, "設定の保存は Windows でだけ動きます。");

    internal void PreviewRowsForShot(IReadOnlyList<ReplayListRow> rows, int checkedCount)
    {
        _all.Clear();
        _all.AddRange(rows);
        StatusText = null;
        _checked.Clear();
        foreach (var r in rows.Take(checkedCount)) _checked.Add(r.ReplayId);
        BuildOptions();
        Apply();
        NotifyChecksChanged();
    }

    internal void PreviewContextForShot(ReplayListRow? row)
    {
        ContextRow = row;
    }

    internal void PreviewDeleteConfirmForShot(bool open)
    {
        if (!open)
        {
            IsDeleteConfirmOpen = false;
            return;
        }
        var n = Math.Max(1, TargetCount);
        var rows = Enumerable.Range(0, n).Select(i => new ReplayDescribeRow(
            i + 1, new string((char)('a' + i % 6), 64),
            [(@"D:\TH09\replay\th9_" + (i + 1).ToString("00", CultureInfo.InvariantCulture) + ".rpy", true)],
            ScanSessions: 1, GuessedLinks: i == 0 ? 1 : 0)).ToList();
        DeleteConfirmText = ReplayOwnLabels.DeleteConfirmText(
            n, ReplayMaintenance.SessionsLine(new ReplayDescription(rows, [])));
        IsDeleteConfirmOpen = true;
    }

    private void ApplyOwnOverride(int? value, string label)
        => RunOwnVerb(label, (main, ids, log) =>
            TH09.Record.ReplayOwnership.SetOverride(main, ids, value, log));

    private void RunOwnVerb(string label,
                            Func<string, IReadOnlyList<long>, Action<string>, ReplayOwnershipResult> verb)
    {
        var targets = TargetRows();
        if (targets.Count == 0) return;
        var ids = targets.Select(t => t.ReplayId).ToList();
        if (TrackerDb.MainDbPath is not string main || !TrackerDb.MainDbExists)
        {
            OwnNote = ReplayOwnLabels.NoDb;
            return;
        }
        ReplayOwnershipResult r;
        try
        {
            r = verb(main, ids, line => LogSource.Warn(ReplayOwnLabels.Category, line));
        }
        catch (Exception ex)
        {
            LogSource.Error(ReplayOwnLabels.Category,
                            ReplayOwnLabels.Menu + ": " + LogSource.Describe(ex));
            OwnNote = ReplayOwnLabels.Menu + "に失敗しました: " + ex.Message;
            return;
        }
        OwnNote = ids.Count == 1 && r.Missing.Count == 0 && r.WithoutSide == 0
            ? ReplayOwnLabels.Applied(ids[0], label, r.Changed > 0)
            : ReplayOwnLabels.AppliedMany(ids.Count, label, r.Changed, r.Unchanged, r.Missing.Count, r.WithoutSide);
        LogSource.Info(ReplayOwnLabels.Category,
                       "[GUI] " + label + ": " + ReplayOwnLabels.OwnResultLine(
                           ids.Count, r.Changed, r.Unchanged, r.Missing.Count, r.WithoutSide)
                       + "（replay_id " + string.Join(", ", ids.Take(5).Select(
                           i => i.ToString(CultureInfo.InvariantCulture)))
                       + (ids.Count > 5 ? " ほか" : "") + "）");
        ReloadNowInPlace(DbUpdateCause.ReplayEdited);
        if (r.Changed > 0) DbEdited?.Invoke();
    }

    public void ReloadOnDbUpdate(DbUpdateCause cause)
    {
        if (cause == DbUpdateCause.ReplayEdited) return;
        if (_reloadBusy || !TrackerDb.MainDbExists) return;
        _reloadBusy = true;
        ReloadReading = Task.Run(() =>
        {
            List<ReplayListRow>? rows = null;
            Exception? error = null;
            try
            {
                using var db = TrackerDb.OpenMainDb();
                rows = ReplayListQuery.LoadAll(db);
            }
            catch (Exception ex) { error = ex; }
            Dispatcher.UIThread.Post(() => ApplyReload(cause, rows, error));
        });
    }

    private bool _reloadBusy;

    internal event Action? DbEdited;

    private void ReloadNowInPlace(DbUpdateCause cause)
    {
        if (!TrackerDb.MainDbExists) return;
        List<ReplayListRow>? rows = null;
        Exception? error = null;
        try
        {
            using var db = TrackerDb.OpenMainDb();
            rows = ReplayListQuery.LoadAll(db);
        }
        catch (Exception ex) { error = ex; }
        ApplyReload(cause, rows, error);
    }

    internal Task? ReloadReading { get; private set; }

    private void ApplyReload(DbUpdateCause cause, List<ReplayListRow>? rows, Exception? error)
    {
        _reloadBusy = false;
        if (error is not null || rows is null)
        {
            var why = error is null ? "行が返りませんでした" : LogSource.Describe(error);
            LogSource.Error(ReplayOwnLabels.Category, "[DB 更新後の読み直し] " + why);
            StatusText = "読み直しに失敗しました（表示は前のままです）: " + why;
            return;
        }
        RebuildKeepingFilters(() =>
        {
            _all.Clear();
            _all.AddRange(rows);
            StatusText = null;
            var alive = new HashSet<long>(rows.Select(r => r.ReplayId));
            _checked.RemoveWhere(id => !alive.Contains(id));
            BuildOptions();
        }, inPlace: true);
        NotifyChecksChanged();
        LogSource.Info(ReplayOwnLabels.Category, DbUpdateCauses.Label(cause) + "を受けて "
                       + rows.Count.ToString("N0", CultureInfo.InvariantCulture) + " 件を読み直した");
    }

    private bool _syncRowsInPlace;

    private void RebuildKeepingFilters(Action rebuild, bool inPlace)
    {
        var ch = SelectedCharacter?.Value;
        var p1 = SelectedP1Character?.Value;
        var p2 = SelectedP2Character?.Value;
        var mode = SelectedMatchMode?.Value;
        var name = SelectedPlayerName?.Value;

        rebuild();

        _applying = true;
        SelectedCharacter = CharacterOptions.FirstOrDefault(o => o.Value == ch) ?? CharacterOptions[0];
        SelectedP1Character = P1CharacterOptions.FirstOrDefault(o => o.Value == p1) ?? P1CharacterOptions[0];
        SelectedP2Character = P2CharacterOptions.FirstOrDefault(o => o.Value == p2) ?? P2CharacterOptions[0];
        SelectedMatchMode = MatchModeOptions.FirstOrDefault(o => o.Value == mode) ?? MatchModeOptions[0];
        SelectedPlayerName = PlayerNameOptions.FirstOrDefault(o => o.Value == name) ?? PlayerNameOptions[0];
        _applying = false;

        ContextRow = null;
        _syncRowsInPlace = inPlace;
        try { Apply(); }
        finally { _syncRowsInPlace = false; }
    }

    private void SyncRows(List<ReplayListRow> desired)
    {
        var keep = new HashSet<long>(desired.Select(r => r.ReplayId));
        for (var i = Rows.Count - 1; i >= 0; i--)
            if (!keep.Contains(Rows[i].ReplayId)) Rows.RemoveAt(i);

        for (var i = 0; i < desired.Count; i++)
        {
            var want = desired[i];
            if (i < Rows.Count && Rows[i].ReplayId == want.ReplayId)
            {
                Rows[i] = want;
                continue;
            }
            for (var j = i + 1; j < Rows.Count; j++)
            {
                if (Rows[j].ReplayId != want.ReplayId) continue;
                Rows.RemoveAt(j);
                break;
            }
            Rows.Insert(i, want);
        }
        while (Rows.Count > desired.Count) Rows.RemoveAt(Rows.Count - 1);
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
        if (_syncRowsInPlace)
        {
            SyncRows(rows);
        }
        else
        {
            Rows.Clear();
            foreach (var r in rows) Rows.Add(r);
        }
        _applying = false;

        foreach (var r in Rows)
        {
            r.Toggled = OnRowToggled;
            r.SetCheckedFromSet(_checked.Contains(r.ReplayId));
        }
        OnPropertyChanged(nameof(AllShownChecked));

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
