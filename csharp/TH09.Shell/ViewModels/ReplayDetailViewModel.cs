using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TH09.Analysis;
using TH09.Shell.Data;
using TH09.Shell.Navigation;

namespace TH09.Shell.ViewModels;

internal sealed record HitWindowChoice(long SessionId, HitWindowRef Ref, int No)
{
    public long WindowNo => Ref.WindowNo;

    public int? Side => Ref.Side;

    public string Label => No.ToString(CultureInfo.InvariantCulture) + "本目"
                           + (Ref.Side is int s ? "（" + s + "P" + (Ref.IsQuickOnly ? "・詰みクイック" : "") + "）"
                              : "");

    public string TickText => Ref.TickCount.ToString(CultureInfo.InvariantCulture) + " tick";
}

internal enum DetailPage
{
    Overview,
    Trend,
    HitWindow,
    Files,
}

internal sealed record DetailPageButton(DetailPage Page, string Label)
{
    public bool IsActive { get; init; }
}

internal sealed record ReplayDetailHeaderCell(ReplayDetailColumn Column)
{
    public string Label => Column.Label;
    public double Width => Column.Width;
    public bool RightAligned => Column.RightAligned;
    public bool IsMuted => Column.Style == CellStyle.Muted;
}

internal sealed partial class ReplayDetailViewModel : ObservableObject, IInnerHistory
{
    private readonly INavigationService _navigation;

    public ReplayDetailViewModel(INavigationService navigation)
    {
        _navigation = navigation;
        BuildPages();
    }


    private readonly List<DetailPage> _pageHistory = [];
    private int _pageHistoryAt = -1;
    private bool _restoringPage;

    public bool CanGoBackInDetail => _pageHistoryAt > 0;

    public bool CanGoForwardInDetail => _pageHistoryAt >= 0 && _pageHistoryAt < _pageHistory.Count - 1;

    bool IInnerHistory.TryGoBack()
    {
        if (!CanGoBackInDetail) return false;
        _pageHistoryAt--;
        RestorePage();
        OnPropertyChanged(nameof(CanGoBackInDetail));
        OnPropertyChanged(nameof(CanGoForwardInDetail));
        return true;
    }

    bool IInnerHistory.TryGoForward()
    {
        if (!CanGoForwardInDetail) return false;
        _pageHistoryAt++;
        RestorePage();
        OnPropertyChanged(nameof(CanGoBackInDetail));
        OnPropertyChanged(nameof(CanGoForwardInDetail));
        return true;
    }

    private void RestorePage()
    {
        _restoringPage = true;
        try { Page = _pageHistory[_pageHistoryAt]; }
        finally { _restoringPage = false; }
    }

    private void PushPageHistory(DetailPage page)
    {
        if (_pageHistoryAt >= 0 && _pageHistoryAt < _pageHistory.Count - 1)
            _pageHistory.RemoveRange(_pageHistoryAt + 1, _pageHistory.Count - _pageHistoryAt - 1);
        _pageHistory.Add(page);
        _pageHistoryAt = _pageHistory.Count - 1;
    }

    public ReplayDetailRequest Request { get; private set; }

    public ObservableCollection<ReplayDetailRow> Rows { get; } = [];

    public ObservableCollection<ReplayDetailHeaderCell> Headers { get; } = [];

    [ObservableProperty]
    public partial double RoundsWidth { get; set; }

    private const double TableSidePadding = 16;

    public ObservableCollection<DetailPageButton> Pages { get; } = [];

    public ObservableCollection<ReplayFileRow> Files { get; } = [];

    [ObservableProperty]
    public partial ReplayFileRow? SelectedFile { get; set; }

    [ObservableProperty]
    public partial string? FilesNote { get; set; }

    public bool HasFiles => Files.Count > 0;

    [ObservableProperty]
    public partial ReplayDetailHeader? Header { get; set; }

    [ObservableProperty]
    public partial string? StatusText { get; set; }

    [ObservableProperty]
    public partial DetailPage Page { get; set; } = DetailPage.Overview;

    [ObservableProperty]
    public partial object? HitWindowContent { get; set; }

    [ObservableProperty]
    public partial object? TrendContent { get; set; }

    [ObservableProperty]
    public partial ReplayDetailRow? SelectedRow { get; set; }

    public ReplayDetailRow? HitWindowRequestedFor { get; private set; }

    public ObservableCollection<HitWindowChoice> HitWindows { get; } = [];

    [ObservableProperty]
    public partial HitWindowChoice? SelectedHitWindow { get; set; }

    [ObservableProperty]
    public partial string? HitWindowNote { get; set; }

    public bool HasHitWindows => HitWindows.Count > 0;


    private int HitWindowAt => SelectedHitWindow is null
        ? -1 : HitWindows.IndexOf(SelectedHitWindow);

    public bool CanPrevHitWindow => HitWindowAt > 0;

    public bool CanNextHitWindow => HitWindowAt >= 0 && HitWindowAt < HitWindows.Count - 1;

    public string HitWindowPositionText => HitWindowAt < 0 || HitWindows.Count == 0
        ? "" : string.Create(CultureInfo.InvariantCulture,
                             $"{HitWindowAt + 1} / {HitWindows.Count}");

    [RelayCommand(CanExecute = nameof(CanPrevHitWindow))]
    private void PrevHitWindow()
    {
        if (CanPrevHitWindow) SelectedHitWindow = HitWindows[HitWindowAt - 1];
    }

    [RelayCommand(CanExecute = nameof(CanNextHitWindow))]
    private void NextHitWindow()
    {
        if (CanNextHitWindow) SelectedHitWindow = HitWindows[HitWindowAt + 1];
    }

    private void NotifyHitWindowNav()
    {
        OnPropertyChanged(nameof(CanPrevHitWindow));
        OnPropertyChanged(nameof(CanNextHitWindow));
        OnPropertyChanged(nameof(HitWindowPositionText));
        PrevHitWindowCommand.NotifyCanExecuteChanged();
        NextHitWindowCommand.NotifyCanExecuteChanged();
    }

    public bool IsOverview => Page == DetailPage.Overview;

    public bool IsHitWindow => Page == DetailPage.HitWindow;

    public bool IsTrend => Page == DetailPage.Trend;

    public bool IsHitWindowEmpty => IsHitWindow && HitWindowContent is null;

    public bool IsTrendEmpty => IsTrend && TrendContent is null;

    public bool IsFiles => Page is DetailPage.Files;

    public bool IsMatch => Header?.IsMatch ?? false;

    public string Heading => Header?.TitleText ?? ("リプレイ詳細 — " + Request);

    public bool HasHeader => Header is not null;

    public string CountNote
    {
        get
        {
            if (Rows.Count == 0 || _countedRounds == Rows.Count) return "";
            return "カード / ボス / リバサの回数は "
                   + Rows.Count.ToString("N0", CultureInfo.InvariantCulture) + " ラウンド中 "
                   + _countedRounds.ToString("N0", CultureInfo.InvariantCulture) + " ラウンドぶん";
        }
    }

    private int _countedRounds;

    public void Show(ReplayDetailRequest request)
    {
        Request = request;
        _pageHistory.Clear();
        _pageHistory.Add(DetailPage.Overview);
        _pageHistoryAt = 0;
        _restoringPage = true;
        try { Page = DetailPage.Overview; } finally { _restoringPage = false; }
        HitWindowRequestedFor = null;
        _trendLoadedSession = null;
        _windowIndexSession = null;
        _roundWindows = [];
        HitWindows.Clear();
        SelectedHitWindow = null;
        HitWindowNote = null;
        SelectedFile = null;
        Load();
    }

    [RelayCommand]
    private void Reload() => Load();

    [RelayCommand]
    private void ShowPage(DetailPage page) => Page = page;

    [RelayCommand]
    private void Back()
    {
        if (((IInnerHistory)this).TryGoBack()) return;
        _navigation.GoBack();
    }

    private void Load()
    {
        Rows.Clear();
        Headers.Clear();
        RoundsWidth = 0;
        Files.Clear();
        Header = null;
        StatusText = null;
        FilesNote = null;
        _countedRounds = 0;

        try
        {
            if (!TrackerDb.MainDbExists)
            {
                StatusText = TrackerDb.MainDbPath is null
                    ? "本体 DB の場所が未設定（TrackerDb.MainDbPath）。"
                    : "本体 DB が見つかりません: " + TrackerDb.MainDbPath;
                FilesNote = StatusText;
            }
            else
            {
                using var db = TrackerDb.OpenMainDb();
                var detail = ReplayDetailQuery.Load(db, Request.ReplayId, Request.SessionId);
                Header = detail.Header;
                StatusText = detail.Status;
                _countedRounds = detail.CountedRounds;
                foreach (var row in detail.Rows) Rows.Add(row);
                if (detail.Header is ReplayDetailHeader h)
                {
                    var cols = ReplayDetailColumns.For(h.Section);
                    foreach (var col in cols) Headers.Add(new ReplayDetailHeaderCell(col));
                    RoundsWidth = TableSidePadding * 2 + cols.Sum(c => c.Width);
                }
                foreach (var f in ReplayDetailQuery.LoadFiles(
                             db, Request.ReplayId, detail.Header?.SessionId ?? Request.SessionId))
                    Files.Add(f);
                FilesNote = Files.Count == 0 ? ReplayFileLabels.NoFiles : null;
            }
        }
        catch (Exception ex)
        {
            StatusText = "本体 DB を読めませんでした: " + ex.Message;
            FilesNote = StatusText;
        }

        OnPropertyChanged(nameof(IsMatch));
        OnPropertyChanged(nameof(HasHeader));
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(Heading));
        OnPropertyChanged(nameof(CountNote));
    }

    public string? RevealDirectory(ReplayFileRow? row)
    {
        if (row is null) return null;
        var target = ReplayFileReveal.For(row);
        FilesNote = target.Note;
        return target.Directory;
    }

    partial void OnPageChanged(DetailPage value)
    {
        if (!_restoringPage)
        {
            PushPageHistory(value);
            OnPropertyChanged(nameof(CanGoBackInDetail));
            OnPropertyChanged(nameof(CanGoForwardInDetail));
        }
        BuildPages();
        OnPropertyChanged(nameof(IsOverview));
        OnPropertyChanged(nameof(IsHitWindow));
        OnPropertyChanged(nameof(IsTrend));
        OnPropertyChanged(nameof(IsHitWindowEmpty));
        OnPropertyChanged(nameof(IsTrendEmpty));
        OnPropertyChanged(nameof(IsFiles));
        if (value == DetailPage.Trend) LoadTrend();
        if (value == DetailPage.HitWindow) LoadHitWindows();
    }

    partial void OnHitWindowContentChanged(object? value)
        => OnPropertyChanged(nameof(IsHitWindowEmpty));

    partial void OnTrendContentChanged(object? value)
        => OnPropertyChanged(nameof(IsTrendEmpty));

    partial void OnSelectedRowChanged(ReplayDetailRow? value)
    {
        if (value is null) return;
        HitWindowRequestedFor = value;
        SelectedRow = null;
        Page = DetailPage.HitWindow;
    }

    partial void OnSelectedHitWindowChanged(HitWindowChoice? value)
    {
        NotifyHitWindowNav();
        if (value is null || _hitWindow is null || Layer0Path is null) return;
        Data.CrashLog.Doing = string.Create(CultureInfo.InvariantCulture,
            $"被弾窓を開く session={value.SessionId} window={value.WindowNo} side={value.Side}");
        try
        {
            _hitWindow.Load(Layer0Path, value.SessionId, value.WindowNo, value.Side);
            HitWindowNote = null;
        }
        catch (Exception ex)
        {
            HitWindowNote = "この窓を開けませんでした: " + ex.Message;
        }
    }


    private HitWindowViewModel? _hitWindow;
    private TimelineViewModel? _trend;
    private long? _trendLoadedSession;
    private long? _windowIndexSession;
    private List<RoundWindows> _roundWindows = [];

    public void AttachPanes(HitWindowViewModel hitWindow, TimelineViewModel trend)
    {
        _hitWindow = hitWindow;
        _trend = trend;
        HitWindowContent = hitWindow;
        TrendContent = trend;
    }

    public static string? Layer0Path
        => OperatingSystem.IsWindows() && File.Exists(TH09.Record.Paths.Default.Layer0Db)
           ? TH09.Record.Paths.Default.Layer0Db : null;

    private void LoadTrend()
    {
        if (_trend is null) return;
        if (Header?.SessionId is not long sid)
        {
            _trend.StatusText = "セッションが分からないので推移を出せません（リプレイだけの記録）。";
            return;
        }
        if (_trendLoadedSession == sid) return;
        if (Layer0Path is not string path)
        {
            _trend.StatusText = "Layer 0 が見つかりません（TH09.Record.Paths.Default.Layer0Db）。";
            return;
        }
        _trend.Load(path, sid);
        _trendLoadedSession = sid;
    }

    private void LoadHitWindows()
    {
        HitWindows.Clear();
        SelectedHitWindow = null;
        HitWindowNote = null;
        OnPropertyChanged(nameof(HasHitWindows));
        if (_hitWindow is null) return;
        if (HitWindowRequestedFor is not ReplayDetailRow row)
        {
            HitWindowNote = "概要のラウンドを 1 行押すと、そのラウンドの被弾窓が出ます。";
            return;
        }
        if (Header?.SessionId is not long sid)
        {
            HitWindowNote = "セッションが分からないので被弾窓を出せません（リプレイだけの記録）。";
            return;
        }
        if (Layer0Path is not string path)
        {
            HitWindowNote = "Layer 0 が見つかりません（TH09.Record.Paths.Default.Layer0Db）。";
            return;
        }
        try
        {
            if (_windowIndexSession != sid)
            {
                using var l0 = new AnalysisDb(path);
                _roundWindows = HitWindowIndex.ForSession(l0, sid);
                _windowIndexSession = sid;
            }
        }
        catch (Exception ex)
        {
            _roundWindows = [];
            _windowIndexSession = null;
            HitWindowNote = "Layer 0 を読めませんでした: " + ex.Message;
            return;
        }
        if (_roundWindows.Count == 0)
        {
            HitWindowNote = "Layer 0 にこのセッションの tick がありません（session "
                            + sid.ToString(CultureInfo.InvariantCulture) + "）。";
            return;
        }
        var (match, byIndex) = MatchRound(row);
        if (match is null)
        {
            HitWindowNote = string.Create(CultureInfo.InvariantCulture,
                $"このラウンド（{row.LabelText}）に当たるものが Layer 0 側にありません"
                + $"（Layer 0 のラウンドは {_roundWindows.Count} 本）。");
            return;
        }
        long prevWindow = long.MinValue;
        int no = 0;
        foreach (var w in match.Windows)
        {
            if (w.WindowNo != prevWindow) { no++; prevWindow = w.WindowNo; }
            HitWindows.Add(new HitWindowChoice(sid, w, no));
        }
        OnPropertyChanged(nameof(HasHitWindows));
        HitWindowNote = HitWindows.Count == 0
            ? $"このラウンド（{row.LabelText}）に被弾窓はありません。"
            : (byIndex ? "★面番号で引けなかったので、ラウンドの並び順で当てました。" : null);
        if (HitWindows.Count > 0) SelectedHitWindow = HitWindows[0];
        NotifyHitWindowNav();
    }

    private (RoundWindows? Match, bool ByIndex) MatchRound(ReplayDetailRow row)
    {
        if (row.StageNumber is int s && row.RoundNumber is int n)
        {
            var got = _roundWindows.FirstOrDefault(w => w.Stage == s && w.Round == n);
            if (got is not null) return (got, false);
        }
        else if (row.RoundNumber is int n2)
        {
            var got = _roundWindows.FirstOrDefault(w => w.Round == n2);
            if (got is not null) return (got, false);
        }
        int at = Rows.IndexOf(row);
        return at >= 0 && at < _roundWindows.Count ? (_roundWindows[at], true) : (null, false);
    }

    private void BuildPages()
    {
        (DetailPage Page, string Label)[] defs =
        [
            (DetailPage.Overview, "概要"),
            (DetailPage.Trend, "数値の推移"),
            (DetailPage.HitWindow, "被弾窓"),
            (DetailPage.Files, "ファイルの場所"),
        ];
        Pages.Clear();
        foreach (var d in defs)
            Pages.Add(new DetailPageButton(d.Page, d.Label) { IsActive = d.Page == Page });
    }
}
