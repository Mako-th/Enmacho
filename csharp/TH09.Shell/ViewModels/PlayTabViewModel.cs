using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using TH09.Shell.Data;
using TH09.Shell.Navigation;

namespace TH09.Shell.ViewModels;

internal sealed partial class PlayTabViewModel : TabViewModelBase
{
    private int _frame;

    public PlayTabViewModel(INavigationService navigation) : base(navigation) { }

    public override ShellTab Key => ShellTab.Play;
    public override string Title => "プレイ";

    public override string Placeholder => "";

    [ObservableProperty]
    public partial string Text { get; set; } = PlayLiveQuery.WaitingText;

    [ObservableProperty]
    public partial IReadOnlyList<PlayTextLine> Rows { get; set; }
        = [new PlayTextLine(PlaySpan.Body(PlayLiveQuery.WaitingText))];

    [ObservableProperty]
    public partial string? StatusText { get; set; }

    [ObservableProperty]
    public partial string SourceText { get; set; } = "";

    public bool HasBand => !string.IsNullOrEmpty(StatusText) || !string.IsNullOrEmpty(SourceText);

    partial void OnStatusTextChanged(string? value) => OnPropertyChanged(nameof(HasBand));

    partial void OnSourceTextChanged(string value) => OnPropertyChanged(nameof(HasBand));

    public void Refresh()
    {
        _frame++;
        try
        {
            if (!TrackerDb.MainDbExists)
            {
                StatusText = TrackerDb.MainDbPath is null
                    ? "本体 DB の場所が未設定（TrackerDb.MainDbPath）。"
                    : "本体 DB が見つかりません: " + TrackerDb.MainDbPath;
                Text = "";
                Rows = [];
                SourceText = "";
                return;
            }
            using var db = TrackerDb.OpenMainDb();
            var progress = PlayLiveQuery.Load(db, _frame % 4, blockLookup: PlayBlocks);
            StatusText = progress.StatusText ?? LiveState();
            Text = progress.Text;
            Rows = progress.Rows;
            SourceText = progress.SessionId is long sid
                ? "Session #" + sid.ToString(CultureInfo.InvariantCulture)
                : "";
        }
        catch (Exception ex)
        {
            StatusText = "本体 DB を読めませんでした: " + ex.Message;
        }
    }

    private static IReadOnlyList<PlayInsertBlock> PlayBlocks(long sid)
    {
        if (TrackerDb.MainDbPath is not string mainDbPath) return [];
        if (!OperatingSystem.IsWindows()) return [];
        return StreamBlockSource.Load(
            mainDbPath, sid,
            TH09.Record.StreamTargets.Load(AppSettingsSource.ConfigPath).Entries, out _,
            AppSettingsSource.Current.PlayTabHiddenItems);
    }

    private readonly GameProcess? _game = OperatingSystem.IsWindows() ? new GameProcess() : null;

    private string LiveState()
    {
        if (_game is null || !OperatingSystem.IsWindows())
            return "★このシェルは記録していません（記録は別プロセス）。"
                 + "出ているのは最後に記録されたセッションです。";
        var game = _game.Poll();
        if (!game.Running)
            return "★いま記録していません（花映塚が動いていません）。"
                 + "出ているのは最後に記録されたセッションです。";
        return "★花映塚は動いていますが、このシェルは記録していません"
             + "（記録は別プロセス）。出ているのは最後に記録されたセッションです。";
    }
}
