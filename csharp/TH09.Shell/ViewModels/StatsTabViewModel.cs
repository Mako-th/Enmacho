using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TH09.Record;
using TH09.Shell.Data;
using TH09.Shell.Navigation;

namespace TH09.Shell.ViewModels;

internal sealed record StatsSectionTab(StatsSection Section, string Label, bool IsActive);

internal sealed record StatsNavState(StatsSection Section, string[] Path);

internal sealed record StatsLeafHeader(StatsLeafColumn Column, bool IsActive, bool Descending)
{
    public string Label => Column.Label;
    public double Width => Column.Width;
    public bool RightAligned => Column.RightAligned;
    public StatsLeafField? Key => Column.Sortable ? Column.Field : null;
    public string Arrow => SortMark.Of(IsActive, Descending);
    public double LeftSeat => SortMark.LeftSeat(RightAligned);
    public double RightSeat => SortMark.RightSeat(RightAligned);
    public string? Tip => Column.Tip.Length == 0 ? null : Column.Tip;
}

internal sealed record StatsGroupHeader(StatsLayoutLead Lead, string Label,
                                        bool IsActive, bool Descending)
{
    public StatsGroupColumn Column => Lead.Column;
    public string Top => Lead.IsGroupStart ? Lead.Band : "";
    public bool IsGroupStart => Lead.IsGroupStart;
    public double Width => Column.Width;
    public bool RightAligned => Column.RightAligned;
    public StatsGroupSortKey Key => new(Column.Field);
    public string Arrow => SortMark.Of(IsActive, Descending);
    public double LeftSeat => SortMark.LeftSeat(RightAligned);
    public double RightSeat => SortMark.RightSeat(RightAligned);
}

internal sealed record StatsAggHeader(StatsAggColumn Column, bool IsActive, bool Descending)
{
    public string Top => Column.Top;
    public string Head => Column.Head;
    public double Width => Column.Width;
    public bool IsGroupStart => Column.IsGroupStart;
    public string? TipOrNull => Column.TipOrNull;
    public StatsGroupSortKey Key => new(null, Column.Value, Column.Stat);
    public string Arrow => SortMark.Of(IsActive, Descending);
    public double MarkSeat => SortMark.SeatWidth;
}

internal sealed record StatsCharChip(int Id, string Label, bool IsFoe, bool IsSelected);

internal sealed partial class StatsTabViewModel : TabViewModelBase, IInnerHistory
{
    public bool TryGoBack()
    {
        if (!CanGoBack) return false;
        GoBack();
        return true;
    }

    public bool TryGoForward()
    {
        if (!CanGoForward) return false;
        GoForward();
        return true;
    }

    private StatsPayload? _payload;

    private bool _applying;

    private readonly List<string> _path = [];

    private readonly List<StatsNavState> _history = [];

    private int _historyAt = -1;

    public event EventHandler? ScrollToHomeRequested;

    private StatsLeafField _leafSort = StatsLeafQuery.DefaultLeafSort;
    private bool _leafDescending = true;

    private StatsGroupSortKey? _groupSort;
    private bool _groupDescending = true;

    private readonly HashSet<int> _selfChars = [.. Enumerable.Range(0, StatsCharFilter.CharacterCount)];
    private readonly HashSet<int> _foeChars = [.. Enumerable.Range(0, StatsCharFilter.CharacterCount)];

    private readonly StatsSection[] _visibleSections = OperatingSystem.IsWindows()
        ? StatsSections.Visible(AppSettingsSource.Current.StatsHiddenItems)
        : StatsSections.All;

    private readonly StatsOrderMap _columnOrder = OperatingSystem.IsWindows()
        ? AppSettingsSource.Current.StatsColumnOrder : StatsOrderMap.Empty;

    private readonly StatsOrderMap _hiddenColumns = OperatingSystem.IsWindows()
        ? AppSettingsSource.Current.StatsHiddenColumns : StatsOrderMap.Empty;

    public StatsTabViewModel(INavigationService navigation) : base(navigation)
    {
        if (Array.IndexOf(_visibleSections, Section) < 0) Section = _visibleSections[0];
        Reload();
    }

    public override ShellTab Key => ShellTab.Stats;
    public override string Title => "統計";

    public override string Placeholder => "";

    [ObservableProperty]
    public partial StatsSection Section { get; set; }

    [ObservableProperty]
    public partial bool IncludeForeign { get; set; }

    [ObservableProperty]
    public partial string? StatusText { get; set; }

    [ObservableProperty]
    public partial string? EmptyText { get; set; }

    [ObservableProperty]
    public partial IStatsLeafRow? SelectedRow { get; set; }

    [ObservableProperty]
    public partial StatsGroup? SelectedGroup { get; set; }

    public ObservableCollection<StatsSectionTab> Sections { get; } = [];

    public ObservableCollection<StatsCrumb> Crumbs { get; } = [];

    public ObservableCollection<StatsCardGroup> Cards { get; } = [];

    public ObservableCollection<StatsGroup> Groups { get; } = [];

    public ObservableCollection<StatsAggHeader> GroupColumns { get; } = [];

    public ObservableCollection<StatsGroupHeader> GroupHeaders { get; } = [];


    public ObservableCollection<StatsGroup> GroupsGain { get; } = [];

    public ObservableCollection<StatsAggHeader> GroupColumnsGain { get; } = [];

    public ObservableCollection<StatsGroupHeader> GroupHeadersGain { get; } = [];

    [ObservableProperty]
    public partial bool ShowGain { get; set; }

    [ObservableProperty]
    public partial string GroupCaption { get; set; } = "";

    [ObservableProperty]
    public partial string GainCaption { get; set; } = "";


    [ObservableProperty]
    public partial double GroupsWidth { get; set; }

    [ObservableProperty]
    public partial double LeafWidth { get; set; }

    private const double TableSidePadding = 16;

    public ObservableCollection<IStatsLeafRow> Rows { get; } = [];

    public ObservableCollection<StatsLeafHeader> Headers { get; } = [];

    [ObservableProperty]
    public partial bool IsLeaf { get; set; }

    [ObservableProperty]
    public partial bool MatrixOn { get; set; }

    [ObservableProperty]
    public partial bool CanShowMatrix { get; set; }

    [ObservableProperty]
    public partial bool ShowMatrix { get; set; }

    [ObservableProperty]
    public partial bool ShowGroups { get; set; }

    [ObservableProperty]
    public partial string MatrixTitle { get; set; } = "";

    public ObservableCollection<StatsMatrixRow> MatrixRows { get; } = [];

    public ObservableCollection<string> MatrixLegend { get; } = [];


    [ObservableProperty]
    public partial bool RecordsOn { get; set; }

    [ObservableProperty]
    public partial bool CanShowRecords { get; set; }

    [ObservableProperty]
    public partial bool ShowRecords { get; set; }

    [ObservableProperty]
    public partial string RecordsTitle { get; set; } = "";

    [ObservableProperty]
    public partial double RecordsWidth { get; set; }

    public ObservableCollection<StatsRecordColumn> RecordColumns { get; } = [];

    public ObservableCollection<StatsRecordRow> RecordRows { get; } = [];

    public ObservableCollection<string> RecordNotes { get; } = [];

    public string RecordsToggleText => StatsRecords.ToggleLabel;

    [ObservableProperty]
    public partial string GroupTitle { get; set; } = "";

    [ObservableProperty]
    public partial string CountText { get; set; } = "";

    public bool CanGoUp => _path.Count > 0;

    public IReadOnlyList<string> CurrentPath => [.. _path];

    public int HistoryCount => _history.Count;

    public ObservableCollection<StatsFixed> Fixed { get; } = [];

    [ObservableProperty]
    public partial bool HasFixed { get; set; }

    [ObservableProperty]
    public partial bool HasCards { get; set; }


    [ObservableProperty]
    public partial bool IsFilterOpen { get; set; }

    public ObservableCollection<StatsCharChip> SelfChips { get; } = [];

    public ObservableCollection<StatsCharChip> FoeChips { get; } = [];

    [ObservableProperty]
    public partial string FilterSummary { get; set; } = "";

    public const string FoeHintStory = "（Story では「面の相手」）";

    [ObservableProperty]
    public partial string FoeHint { get; set; } = "";

    public const string TagNote = "スコアタ / サバイバルのタグはまだ無い（自動判定はしない）";

    public string TagNoteText => TagNote;

    [ObservableProperty]
    public partial string UntaggedText { get; set; } = "";


    [ObservableProperty]
    public partial bool IsNotesOpen { get; set; }

    public ObservableCollection<string> Notes { get; } = [];

    public ObservableCollection<string> Definitions { get; } = [];

    public const string DefinitionsTitle = "定義と根拠";

    public string DefinitionsTitleText => DefinitionsTitle;

    [ObservableProperty]
    public partial string NotesSummary { get; set; } = "";

    public const string ReadAtLabel = "読み込み";

    public const string DbLabel = "DB";

    private DateTime? _readAt;


    public const string HistoryNote =
        "戻る / 進むで 1 段ずつ移動できる（Backspace とマウスの戻る / 進むボタンでも動く）";

    public string HistoryNoteText => HistoryNote;

    public bool CanGoBack => _historyAt > 0;

    public bool CanGoForward => _historyAt >= 0 && _historyAt < _history.Count - 1;


    [RelayCommand]
    private void ShowSection(StatsSection section) => Go(section, []);

    [RelayCommand]
    private void GoToDepth(int depth)
    {
        if (depth < 0 || depth >= _path.Count) return;
        Go(Section, _path.Take(depth));
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack()
    {
        if (!CanGoBack) return;
        _historyAt--;
        Restore();
    }

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private void GoForward()
    {
        if (!CanGoForward) return;
        _historyAt++;
        Restore();
    }

    private void Go(StatsSection section, IEnumerable<string> path)
    {
        var next = path.ToArray();
        if (section == Section && _path.SequenceEqual(next, StringComparer.Ordinal)) return;
        if (_historyAt >= 0 && _historyAt < _history.Count - 1)
            _history.RemoveRange(_historyAt + 1, _history.Count - _historyAt - 1);
        _history.Add(new StatsNavState(section, next));
        _historyAt = _history.Count - 1;
        Move(section, next);
    }

    private void Restore() => Move(_history[_historyAt].Section, _history[_historyAt].Path);

    private void Move(StatsSection section, IReadOnlyList<string> path)
    {
        _path.Clear();
        _path.AddRange(path);
        if (Section != section) Section = section;
        else Apply();
        ScrollToHomeRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void PickMatrix(StatsMatrixCell? cell)
    {
        if (_applying || cell?.Pick is not IReadOnlyList<string> next) return;
        var pick = next.ToList();
        Dispatcher.UIThread.Post(() => Go(Section, pick), DispatcherPriority.Background);
    }

    [RelayCommand]
    private void SortLeaf(StatsLeafField? key)
    {
        if (key is not StatsLeafField k) return;
        _leafDescending = _leafSort == k ? !_leafDescending : DefaultDescending(k);
        _leafSort = k;
        Apply();
    }

    [RelayCommand]
    private void SortGroups(StatsGroupSortKey? key)
    {
        if (key is null) return;
        _groupDescending = _groupSort == key ? !_groupDescending : key.Lead != StatsGroupField.Name;
        _groupSort = key;
        Apply();
    }

    private static bool DefaultDescending(StatsLeafField field) => field switch
    {
        StatsLeafField.When or StatsLeafField.Session or StatsLeafField.Time
            or StatsLeafField.SpellScore or StatsLeafField.Reach
            or StatsLeafField.ReachExBonus or StatsLeafField.Segment
            or StatsLeafField.ClearBonus or StatsLeafField.SegmentExBonus
            or StatsLeafField.Result or StatsLeafField.RoundScore => true,
        _ => false,
    };


    [RelayCommand]
    private void ToggleChar(StatsCharChip? chip)
    {
        if (_applying || chip is null) return;
        var set = chip.IsFoe ? _foeChars : _selfChars;
        if (!set.Remove(chip.Id)) set.Add(chip.Id);
        Apply();
    }

    [RelayCommand]
    private void SelectAllSelf() => SetAll(foe: false, on: true);

    [RelayCommand]
    private void ClearSelf() => SetAll(foe: false, on: false);

    [RelayCommand]
    private void SelectAllFoe() => SetAll(foe: true, on: true);

    [RelayCommand]
    private void ClearFoe() => SetAll(foe: true, on: false);

    private void SetAll(bool foe, bool on)
    {
        if (_applying) return;
        var set = foe ? _foeChars : _selfChars;
        set.Clear();
        if (on) for (var i = 0; i < StatsCharFilter.CharacterCount; i++) set.Add(i);
        Apply();
    }

    [RelayCommand]
    private void GoUp()
    {
        if (_path.Count == 0) return;
        Go(Section, _path.Take(_path.Count - 1));
    }

    [RelayCommand]
    private void Reload()
    {
        _payload = null;
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
                _payload = StatsLeafQuery.LoadAll(db);
                _readAt = DateTime.Now;
            }
        }
        catch (Exception ex)
        {
            StatusText = "本体 DB を読めませんでした: " + ex.Message;
        }
        _history.Clear();
        _history.Add(new StatsNavState(Section, []));
        _historyAt = 0;
        _path.Clear();
        Apply();
    }


    partial void OnSectionChanged(StatsSection value) => Apply();

    partial void OnIncludeForeignChanged(bool value) => Apply();

    partial void OnMatrixOnChanged(bool value) => Apply();

    partial void OnRecordsOnChanged(bool value) => Apply();

    partial void OnSelectedGroupChanged(StatsGroup? value)
    {
        if (value is null || _applying) return;
        var steps = value.Drill;
        Dispatcher.UIThread.Post(() => Go(Section, [.. _path, .. steps]), DispatcherPriority.Background);
    }

    partial void OnSelectedRowChanged(IStatsLeafRow? value)
    {
        if (value is null || _applying) return;
        var row = value;
        _applying = true;
        SelectedRow = null;
        _applying = false;
        Navigation.OpenReplayDetail(ReplayDetailRequest.FromSession(row.SessionId));
    }

    private static IEnumerable<StatsGroup> Flatten(IReadOnlyList<StatsGroup> rows)
    {
        var hasSubs = false;
        foreach (var g in rows)
        {
            if (g.Subs.Count > 0) { hasSubs = true; break; }
        }
        var band = 0;
        foreach (var g in rows)
        {
            var odd = hasSubs && band % 2 == 1;
            yield return g with { IsBandOdd = odd };
            foreach (var sub in g.Subs) yield return sub with { IsBandOdd = odd };
            if (hasSubs) band++;
        }
    }

    private static void Align(List<StatsGroup> target, IReadOnlyList<StatsGroup> model)
    {
        var order = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < model.Count; i++) order[model[i].Key] = i;
        target.Sort((a, b) =>
        {
            var ia = order.TryGetValue(a.Key, out var x) ? x : int.MaxValue;
            var ib = order.TryGetValue(b.Key, out var y) ? y : int.MaxValue;
            return ia != ib ? ia.CompareTo(ib) : string.CompareOrdinal(a.Key, b.Key);
        });
    }

    private string ReadAtText()
        => _readAt is DateTime t
           ? t.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture)
           : ReplayFormat.Missing;

    private string MakeFilterSummary()
    {
        var n = StatsCharFilter.CharacterCount;
        string Part(string name, HashSet<int> set)
            => name + " " + (set.Count == n
                ? "全部"
                : set.Count.ToString(CultureInfo.InvariantCulture) + "/"
                  + n.ToString(CultureInfo.InvariantCulture));
        var parts = new List<string> { Part("自キャラ", _selfChars), Part("相手キャラ", _foeChars) };
        if (_payload is StatsPayload p)
            parts.Add("未分類 " + p.Untagged.ToString("N0", CultureInfo.InvariantCulture));
        return string.Join(" / ", parts);
    }

    private StatsGroupHeader LeadHeader(StatsLayoutLead lead)
        => new(lead,
               lead.Column.Field == StatsGroupField.Name ? GroupTitle : lead.Column.Label,
               _groupSort?.Lead == lead.Column.Field, _groupDescending);

    private void Apply()
    {
        _applying = true;
        try
        {
            Sections.Clear();
            foreach (var s in _visibleSections)
                Sections.Add(new StatsSectionTab(s, StatsSections.Title(s), s == Section));

            SelfChips.Clear();
            FoeChips.Clear();
            foreach (var i in ReplayLabels.DisplayOrder)
            {
                var label = ReplayLabels.Characters[i];
                SelfChips.Add(new StatsCharChip(i, label, false, _selfChars.Contains(i)));
                FoeChips.Add(new StatsCharChip(i, label, true, _foeChars.Contains(i)));
            }
            FoeHint = Section == StatsSection.StoryExtra ? FoeHintStory : "";

            Crumbs.Clear();
            Fixed.Clear();
            Cards.Clear();
            Groups.Clear();
            GroupColumns.Clear();
            GroupHeaders.Clear();
            GroupsGain.Clear();
            GroupColumnsGain.Clear();
            GroupHeadersGain.Clear();
            Rows.Clear();
            Headers.Clear();
            Notes.Clear();
            Definitions.Clear();
            MatrixRows.Clear();
            MatrixLegend.Clear();
            RecordColumns.Clear();
            RecordRows.Clear();
            RecordNotes.Clear();
            SelectedGroup = null;
            SelectedRow = null;

            FilterSummary = MakeFilterSummary();

            if (_payload is not StatsPayload payload)
            {
                CountText = "";
                EmptyText = null;
                UntaggedText = "";
                NotesSummary = "";
                HasFixed = false;
                HasCards = false;
                IsLeaf = false;
                CanShowMatrix = false;
                ShowMatrix = false;
                CanShowRecords = false;
                ShowRecords = false;
                ShowGroups = false;
                ShowGain = false;
                GroupCaption = "";
                GainCaption = "";
                MatrixTitle = "";
                RecordsTitle = "";
                return;
            }

            UntaggedText = "未分類 " + payload.Untagged.ToString("N0", CultureInfo.InvariantCulture);

            Notes.Add(StatsNotes.Sessions(payload));
            Notes.Add(StatsNotes.Counts(payload));
            Notes.Add(DbLabel + ": " + (TrackerDb.MainDbPath ?? "（未設定）")
                      + "　" + ReadAtLabel + ": " + ReadAtText());
            NotesSummary = ReadAtLabel + " " + ReadAtText();
            foreach (var d in StatsDefinitions.All(payload, HistoryNote)) Definitions.Add(d);

            var view = new StatsView(payload, Section, _path, IncludeForeign,
                                     new StatsCharFilter(_selfChars, _foeChars));
            view.UseLayout(_columnOrder, _hiddenColumns);
            foreach (var c in view.Crumbs()) Crumbs.Add(c);
            foreach (var x in view.Fixed()) Fixed.Add(x);
            HasFixed = Fixed.Count > 0;
            var groups = view.Cards();
            for (var i = 0; i < groups.Count; i++) Cards.Add(groups[i] with { IsFirst = i == 0 });
            HasCards = Cards.Count > 0;

            IsLeaf = view.IsLeaf;
            GroupTitle = view.NextLevel?.Title ?? "";
            var aggCols = view.GroupColumns;
            var leadCols = view.LeadColumns;
            var gainColsForSort = view.GroupColumnsGain;
            if (_groupSort is StatsGroupSortKey gk
                && !StatsSort.CanSort(gk, aggCols, leadCols)
                && !StatsSort.CanSort(gk, gainColsForSort, leadCols))
                _groupSort = null;
            foreach (var x in view.LeadLayout)
                GroupHeaders.Add(LeadHeader(x));
            foreach (var c in aggCols)
                GroupColumns.Add(new StatsAggHeader(
                    c, _groupSort is { Lead: null, Value: { } v, Stat: { } st }
                       && v == c.Value && st == c.Stat, _groupDescending));
            var groupRows = view.Groups();
            var gainCols = gainColsForSort;
            GroupsWidth = TableSidePadding * 2 + leadCols.Sum(c => c.Width)
                          + Math.Max(aggCols.Sum(c => c.Width), gainCols.Sum(c => c.Width));
            ShowGain = ShowGroups && view.HasGain;
            GroupCaption = view.GroupCaption;
            GainCaption = view.GainCaption;
            var gainRows = ShowGain ? view.GroupsGain() : [];

            var inLead = _groupSort is StatsGroupSortKey k1
                         && StatsSort.CanSort(k1, aggCols, leadCols);
            var inGain = _groupSort is StatsGroupSortKey k2 && ShowGain
                         && StatsSort.CanSort(k2, gainCols, leadCols);
            var groupAxis = view.NextLevel?.Axis;
            if (_groupSort is StatsGroupSortKey key && inLead)
                StatsSort.Groups(groupRows, key, _groupDescending, aggCols, groupAxis);

            if (ShowGain)
            {
                if (!inLead && inGain && _groupSort is StatsGroupSortKey gk2)
                {
                    StatsSort.Groups(gainRows, gk2, _groupDescending, gainCols, groupAxis);
                    Align(groupRows, gainRows);
                }
                else
                {
                    Align(gainRows, groupRows);
                }
                foreach (var x in view.LeadLayout)
                    GroupHeadersGain.Add(LeadHeader(x));
                foreach (var c in gainCols)
                    GroupColumnsGain.Add(new StatsAggHeader(
                        c, _groupSort is { Lead: null, Value: { } gv, Stat: { } gst }
                           && gv == c.Value && gst == c.Stat, _groupDescending));
                foreach (var g in Flatten(gainRows)) GroupsGain.Add(g);
            }

            foreach (var g in Flatten(groupRows)) Groups.Add(g);

            var matrix = view.Matrix();
            CanShowMatrix = matrix is not null;
            ShowMatrix = MatrixOn && matrix is not null;
            var records = view.Records();
            CanShowRecords = records is not null;
            ShowRecords = RecordsOn && records is not null && !ShowMatrix;
            ShowGroups = !view.IsLeaf && !ShowMatrix && !ShowRecords;
            MatrixTitle = matrix?.Title ?? "";
            if (ShowMatrix && matrix is StatsMatrix mx)
            {
                foreach (var r in mx.Rows) MatrixRows.Add(r);
                foreach (var t in mx.Legend) MatrixLegend.Add(t);
            }
            RecordsTitle = records?.Title ?? "";
            if (ShowRecords && records is StatsRecordsTable rec)
            {
                RecordsWidth = TableSidePadding * 2 + rec.Width;
                foreach (var c in rec.Columns) RecordColumns.Add(c);
                foreach (var r in rec.Rows) RecordRows.Add(r);
                foreach (var t in rec.Notes) RecordNotes.Add(t);
            }

            if (view.IsLeaf)
            {
                var cols = view.Columns;
                if (!Array.Exists(cols, c => c.Field == _leafSort))
                {
                    _leafSort = StatsLeafQuery.DefaultLeafSort;
                    _leafDescending = true;
                }
                LeafWidth = TableSidePadding * 2 + cols.Sum(c => c.Width);
                foreach (var col in cols)
                    Headers.Add(new StatsLeafHeader(col, col.Field == _leafSort, _leafDescending));
                var leafRows = view.LeafRows();
                StatsSort.Leaf(leafRows, _leafSort, _leafDescending, cols);
                foreach (var r in leafRows) Rows.Add(r);
            }

            CountText = view.BaseCount.ToString("N0", CultureInfo.InvariantCulture) + " 件中 "
                        + view.RowCount.ToString("N0", CultureInfo.InvariantCulture) + " 件"
                        + (payload.Skips[Section] > 0
                           ? "（自分側が決まらず外した " + payload.Skips[Section] + " 件を含まない）" : "");

            EmptyText = view.RowCount > 0 ? null
                : view.SectionCount == 0
                    ? "この区分にはまだ記録がありません。"
                    : "絞り込みの結果、該当する記録がありません（この区分には "
                      + view.SectionCount.ToString("N0", CultureInfo.InvariantCulture) + " 件ある）。";
        }
        finally
        {
            _applying = false;
            OnPropertyChanged(nameof(CanGoUp));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
            GoBackCommand.NotifyCanExecuteChanged();
            GoForwardCommand.NotifyCanExecuteChanged();
        }
    }
}
