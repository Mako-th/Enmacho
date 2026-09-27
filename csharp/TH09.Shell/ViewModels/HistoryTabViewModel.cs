using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls.Selection;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TH09.Record;
using TH09.Shell.Data;
using TH09.Shell.Navigation;

namespace TH09.Shell.ViewModels;

internal sealed record HistoryHeader(HistoryColumn Column, bool IsActive, string Arrow)
{
    public string Label => Column.Label;
    public double Width => Column.Width;
    public bool RightAligned => Column.RightAligned;
    public HistorySortKey? Key => Column.Key;

    public double LeftSeat => Data.SortMark.LeftSeat(RightAligned);

    public double RightSeat => Data.SortMark.RightSeat(RightAligned);
}

internal sealed partial class HistoryTabViewModel : TabViewModelBase
{
    private const string Category = "履歴";


    public const string DeleteConfirmTitle = "Session削除";

    public const string DeleteConfirmTail =
        "\n\n生tick（Layer 0）も一緒に削除します。\nReplayファイル本体とReplay Library登録は削除しません。";

    public const string DeleteFailedTitle = "削除失敗";

    public const string PruneTitle = "履歴を整理";

    public const string PruneNothingText =
        "削除対象はありません。\n（自己ベスト保持Sessionと保持件数内のSessionは残ります。）";

    public const string OpenDetailMenuText = "個別詳細を開く";

    public const string LegendText = "薄く塗った行 ＝ 整理で消さないセッション（自己ベストの保持・自動スキャン由来）";

    public static string DeletePrompt(IReadOnlyList<long> sids)
        => sids.Count > 1
            ? "選択した " + sids.Count.ToString(CultureInfo.InvariantCulture) + " 件のSessionのDB記録を削除しますか？"
            : "Session " + sids[0].ToString(CultureInfo.InvariantCulture) + " のDB記録を削除しますか？";

    public static string PrunePrompt(int keepAbortedReplay, int count)
    {
        var ktxt = keepAbortedReplay <= 0
            ? "無制限" : "最新" + keepAbortedReplay.ToString(CultureInfo.InvariantCulture) + "件";
        return "中断/リプレイは" + ktxt + "を残し、古い " + count.ToString(CultureInfo.InvariantCulture)
               + " 件のDB記録を削除します。\n自己ベスト（区間スコア/時間/最終スコア）保持Sessionと\n"
               + "自動スキャン由来のSessionは保護され削除しません。\n\n実行しますか？";
    }

    public static string DeleteMenuLabel(int n)
        => n > 1 ? "選択した " + n.ToString(CultureInfo.InvariantCulture) + " 件のDB記録を削除"
                 : "このSessionのDB記録を削除";

    private readonly List<HistoryRow> _all = [];

    private bool _applying;

    private IReadOnlyList<long> _prunePlan = [];

    private bool _autoRefreshBusy;

    private string? _lastSignature;

    private string? _lastAutoRefreshError;

    public HistoryTabViewModel(INavigationService navigation) : base(navigation)
    {
        Selection.Source = Rows;
        Selection.SelectionChanged += (_, _) => OnSelectionChanged();
        Reload();
    }

    public override ShellTab Key => ShellTab.History;
    public override string Title => "履歴";

    public override string Placeholder => "";

    public ObservableCollection<HistoryRow> Rows { get; } = [];

    public ObservableCollection<HistoryHeader> Headers { get; } = [];

    public static double RowsWidth
        => TableSidePadding * 2 + HistoryColumns.All.Sum(c => c.Width);

    private const double TableSidePadding = 16;

    public SelectionModel<HistoryRow> Selection { get; } = new() { SingleSelect = false };

    public bool ModifierHeld { get; set; }

    public Func<(int KeepAbortedReplay, int KeepCompleted)> RetentionSource { get; set; } = ReadRetention;

    [ObservableProperty]
    public partial HistoryKind Kind { get; set; } = HistoryKind.LivePlay;

    [ObservableProperty]
    public partial string? StatusText { get; set; }

    [ObservableProperty]
    public partial bool IsDeleteConfirmOpen { get; set; }

    [ObservableProperty]
    public partial string DeleteConfirmText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsPruneConfirmOpen { get; set; }

    [ObservableProperty]
    public partial string PruneConfirmText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsNoticeOpen { get; set; }

    [ObservableProperty]
    public partial string NoticeTitle { get; set; } = "";

    [ObservableProperty]
    public partial string NoticeText { get; set; } = "";

    public IReadOnlyList<long> LastDeleted { get; private set; } = [];

    public string DeleteConfirmHeader => DeleteConfirmTitle;

    public string PruneConfirmHeader => PruneTitle;

    public string PruneButtonText => PruneTitle;

    public string Legend => LegendText;

    public string OpenDetailText => OpenDetailMenuText;

    public string DeleteMenuText => DeleteMenuLabel(Math.Max(1, Selection.Count));

    public bool HasSelection => Selection.Count > 0;

    public int ProtectedCount => _all.Count(r => r.IsProtected);

    public string ProtectedText
        => "全 " + _all.Count.ToString("N0", CultureInfo.InvariantCulture) + " 件中 "
           + ProtectedCount.ToString("N0", CultureInfo.InvariantCulture) + " 件";

    public bool IsLive => Kind == HistoryKind.LivePlay;

    public bool IsReplay => Kind == HistoryKind.ReplayPlayback;

    public bool IsAll => Kind == HistoryKind.All;

    public string CountText
        => _all.Count.ToString("N0", CultureInfo.InvariantCulture) + " 件中 "
           + Rows.Count.ToString("N0", CultureInfo.InvariantCulture) + " 件";

    private HistorySortKey _sort = HistorySortKey.SessionId;
    private bool _descending = true;

    public HistorySortKey SortKey => _sort;

    public bool SortDescending => _descending;


    [RelayCommand]
    private void ShowLive() => Kind = HistoryKind.LivePlay;

    [RelayCommand]
    private void ShowReplay() => Kind = HistoryKind.ReplayPlayback;

    [RelayCommand]
    private void ShowAll() => Kind = HistoryKind.All;

    [RelayCommand]
    private void SortBy(HistorySortKey? key)
    {
        if (key is not HistorySortKey k) return;
        _descending = _sort == k ? !_descending : DefaultDescending(k);
        _sort = k;
        Apply();
    }

    [RelayCommand]
    private void Reload()
    {
        _all.Clear();
        StatusText = null;
        _lastSignature = null;
        try
        {
            if (!TrackerDb.MainDbExists)
            {
                var why = TrackerDb.MainDbPath is null
                    ? "本体 DB の場所が未設定（TrackerDb.MainDbPath）。"
                    : "本体 DB が見つかりません: " + TrackerDb.MainDbPath;
                StatusText = why;
                if (TrackerDb.MainDbPath is null)
                {
                    LogSource.Error(Category, why);
                }
                else
                {
                    LogSource.Info(Category, why);
                }
            }
            else
            {
                var snap = HistoryQuery.Read(TrackerDb.MainDbPath!);
                _all.AddRange(snap.Rows);
                _lastSignature = snap.Signature;
                _lastAutoRefreshError = null;
                LogSource.Info(Category, "セッション "
                    + _all.Count.ToString("N0", CultureInfo.InvariantCulture) + " 件を読んだ");
            }
        }
        catch (Exception ex)
        {
            StatusText = "本体 DB を読めませんでした: " + ex.Message;
            LogSource.Error(Category, LogSource.Describe(ex));
        }
        Apply();
    }

    public void AutoRefresh()
    {
        if (_autoRefreshBusy) return;
        if (IsDeleteConfirmOpen || IsPruneConfirmOpen || IsNoticeOpen) return;
        if (TrackerDb.MainDbPath is not string main || !TrackerDb.MainDbExists) return;

        _autoRefreshBusy = true;
        Task.Run(() =>
        {
            HistorySnapshot? snapshot = null;
            Exception? error = null;
            try { snapshot = HistoryQuery.Read(main); }
            catch (Exception ex) { error = ex; }
            Dispatcher.UIThread.Post(() => ApplyAutoRefresh(snapshot, error));
        });
    }

    private void ApplyAutoRefresh(HistorySnapshot? snapshot, Exception? error)
    {
        _autoRefreshBusy = false;
        if (error is not null)
        {
            var msg = LogSource.Describe(error);
            if (msg != _lastAutoRefreshError)
            {
                _lastAutoRefreshError = msg;
                LogSource.Error(Category, "[自動更新] " + msg);
            }
            return;
        }
        if (snapshot is not { } snap) return;
        _lastAutoRefreshError = null;
        if (IsDeleteConfirmOpen || IsPruneConfirmOpen || IsNoticeOpen) return;
        if (snap.Signature == _lastSignature) return;
        _lastSignature = snap.Signature;

        var keepSelected = SelectedIds();
        _all.Clear();
        _all.AddRange(snap.Rows);
        Apply();

        if (keepSelected.Count == 0) return;
        var want = new HashSet<long>(keepSelected);
        _applying = true;
        for (var i = 0; i < Rows.Count; i++)
            if (want.Contains(Rows[i].SessionId)) Selection.Select(i);
        _applying = false;
        NotifySelection();
    }

    [RelayCommand]
    private void SelectAll()
    {
        _applying = true;
        Selection.SelectAll();
        _applying = false;
        NotifySelection();
    }

    [RelayCommand]
    private void OpenSelected()
    {
        if (Selection.SelectedItem is not HistoryRow row) return;
        OpenDetail(row);
    }

    [RelayCommand]
    private void RequestDelete()
    {
        var ids = SelectedIds();
        if (ids.Count == 0) return;
        DeleteConfirmText = DeletePrompt(ids) + DeleteConfirmTail;
        IsDeleteConfirmOpen = true;
    }

    [RelayCommand]
    private void CancelDelete() => IsDeleteConfirmOpen = false;

    [RelayCommand]
    private void ConfirmDelete()
    {
        IsDeleteConfirmOpen = false;
        var ids = SelectedIds();
        if (ids.Count == 0) return;
        try
        {
            Delete(ids);
            LogSource.Info(Category, "[GUI] Session削除: "
                + ids.Count.ToString(CultureInfo.InvariantCulture) + " 件");
        }
        catch (Exception ex)
        {
            LogSource.Error(Category, DeleteFailedTitle + ": " + LogSource.Describe(ex));
            ShowNotice(DeleteFailedTitle, ex.Message);
        }
        Reload();
    }

    [RelayCommand]
    private void RequestPrune()
    {
        if (TrackerDb.MainDbPath is not string main || !TrackerDb.MainDbExists)
        {
            ShowNotice(PruneTitle, StatusText ?? "本体 DB の場所が未設定（TrackerDb.MainDbPath）。");
            return;
        }
        var (keepA, keepC) = RetentionSource();
        PrunePlan plan;
        try
        {
            plan = HistoryMaintenance.Plan(main, keepA, keepC);
        }
        catch (Exception ex)
        {
            LogSource.Error(Category, PruneTitle + ": " + LogSource.Describe(ex));
            ShowNotice(PruneTitle, ex.Message);
            return;
        }
        if (plan.ToDelete.Count == 0)
        {
            ShowNotice(PruneTitle, PruneNothingText);
            return;
        }
        _prunePlan = plan.ToDelete;
        PruneConfirmText = PrunePrompt(keepA, plan.ToDelete.Count);
        IsPruneConfirmOpen = true;
    }

    [RelayCommand]
    private void CancelPrune()
    {
        IsPruneConfirmOpen = false;
        _prunePlan = [];
    }

    [RelayCommand]
    private void ConfirmPrune()
    {
        IsPruneConfirmOpen = false;
        var ids = _prunePlan;
        _prunePlan = [];
        if (ids.Count == 0) return;
        try
        {
            Delete(ids);
            LogSource.Info(Category, "[GUI] 履歴整理: "
                + ids.Count.ToString(CultureInfo.InvariantCulture) + " 件削除");
        }
        catch (Exception ex)
        {
            LogSource.Error(Category, PruneTitle + ": " + LogSource.Describe(ex));
            ShowNotice(PruneTitle, ex.Message);
        }
        Reload();
    }

    [RelayCommand]
    private void CloseNotice() => IsNoticeOpen = false;

    public void EnsureSelected(HistoryRow row)
    {
        var index = Rows.IndexOf(row);
        if (index < 0 || Selection.IsSelected(index)) return;
        _applying = true;
        Selection.Clear();
        Selection.Select(index);
        _applying = false;
        NotifySelection();
    }


    partial void OnKindChanged(HistoryKind value)
    {
        OnPropertyChanged(nameof(IsLive));
        OnPropertyChanged(nameof(IsReplay));
        OnPropertyChanged(nameof(IsAll));
        Apply();
    }

    private void OnSelectionChanged()
    {
        if (_applying) return;
        NotifySelection();
        if (ModifierHeld || Selection.Count != 1) return;
        if (Selection.SelectedItem is not HistoryRow row) return;
        OpenDetail(row);
    }

    private void OpenDetail(HistoryRow row)
    {
        _applying = true;
        Selection.Clear();
        _applying = false;
        NotifySelection();
        Navigation.OpenReplayDetail(ReplayDetailRequest.FromSession(row.SessionId, row.ReplayId));
    }

    private void NotifySelection()
    {
        OnPropertyChanged(nameof(DeleteMenuText));
        OnPropertyChanged(nameof(HasSelection));
    }

    private List<long> SelectedIds() => Selection.SelectedItems.OfType<HistoryRow>().Select(r => r.SessionId).ToList();

    private void Delete(IReadOnlyList<long> ids)
    {
        if (TrackerDb.MainDbPath is not string main)
            throw new InvalidOperationException("本体 DB の場所が未設定（TrackerDb.MainDbPath）。");
        if (TrackerDb.Layer0DbPath is not string layer0)
            throw new InvalidOperationException(
                "Layer 0 の場所が未設定（TrackerDb.Layer0DbPath）。生tickを消せないので、本体の記録も消しません。");
        var r = HistoryMaintenance.Delete(main, layer0, ids, line => LogSource.Warn(Category, line));
        LastDeleted = r.Deleted;
    }

    private void ShowNotice(string title, string text)
    {
        NoticeTitle = title;
        NoticeText = text;
        IsNoticeOpen = true;
    }

    private static (int, int) ReadRetention()
        => OperatingSystem.IsWindows()
            ? (AppSettingsSource.Current.HistoryRetentionAborted, AppSettingsSource.Current.HistoryRetentionCompleted)
            : (ConfigStore.HistoryRetentionAbortedDefault, ConfigStore.HistoryRetentionCompletedDefault);

    private void Apply()
    {
        if (_applying) return;
        var rows = HistoryQuery.Filter(_all, new HistoryFilter(Kind));
        HistoryQuery.Sort(rows, _sort, _descending);

        _applying = true;
        Selection.Clear();
        Rows.Clear();
        foreach (var r in rows) Rows.Add(r);
        _applying = false;

        BuildHeaders();
        NotifySelection();
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(ProtectedCount));
        OnPropertyChanged(nameof(ProtectedText));
        OnPropertyChanged(nameof(SortKey));
        OnPropertyChanged(nameof(SortDescending));
    }

    private void BuildHeaders()
    {
        Headers.Clear();
        foreach (var col in HistoryColumns.All)
        {
            var active = col.Key == _sort;
            Headers.Add(new HistoryHeader(col, active,
                                          Data.SortMark.Of(active, _descending)));
        }
    }

    private static bool DefaultDescending(HistorySortKey key) => key switch
    {
        HistorySortKey.SessionId or HistorySortKey.StartedAt or HistorySortKey.Lives
            or HistorySortKey.Score or HistorySortKey.HasReplay => true,
        _ => false,
    };
}
