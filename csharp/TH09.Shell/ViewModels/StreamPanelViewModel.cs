using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TH09.Record;
using TH09.Shell.Data;

namespace TH09.Shell.ViewModels;

internal sealed record StreamPanelOptions(
    double Width, double Height, double FontScale, string Background, bool Topmost, bool Borderless)
{
    public static readonly StreamPanelOptions Default =
        new(Width: 480, Height: 1080, FontScale: 1.5, Background: "#0b0b12",
            Topmost: false, Borderless: false);
}

internal sealed record StreamPanelBackgroundChoice(string Label, string Hex);

internal sealed partial class StreamPanelViewModel : ObservableObject
{
    private readonly Action<StreamPanelOptions, IReadOnlyList<string>> _save;
    private readonly Func<IReadOnlyList<string>> _readHidden;

    private int _frame;

    private bool _applyingHidden;

    public StreamPanelViewModel(StreamPanelOptions initial,
                                Action<StreamPanelOptions, IReadOnlyList<string>> save,
                                Func<IReadOnlyList<string>>? readHidden = null)
    {
        _save = save;
        _readHidden = readHidden ?? (static () => []);
        Options = initial;
        ApplyHidden(_readHidden());
    }

    [ObservableProperty]
    public partial StreamPanelOptions Options { get; set; }

    partial void OnOptionsChanged(StreamPanelOptions value)
    {
        OnPropertyChanged(nameof(BackgroundBrush));
        OnPropertyChanged(nameof(WindowDecorations));
        OnPropertyChanged(nameof(FontSize));
    }

    public static readonly IReadOnlyList<StreamPanelBackgroundChoice> Backgrounds =
    [
        new("黒", "#0b0b12"),
        new("濃紺", "#0d1b2a"),
        new("緑（クロマキー）", "#00b140"),
        new("マゼンタ（クロマキー）", "#b400ff"),
    ];


    public IReadOnlyList<string> HiddenItems { get; private set; } = [];

    [ObservableProperty]
    public partial bool ShowSb { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowPb { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowTarget { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowWr { get; set; } = true;

    partial void OnShowSbChanged(bool value) => ItemToggled();

    partial void OnShowPbChanged(bool value) => ItemToggled();

    partial void OnShowTargetChanged(bool value) => ItemToggled();

    partial void OnShowWrChanged(bool value) => ItemToggled();

    private void ItemToggled()
    {
        if (_applyingHidden) return;
        HiddenItems = [.. StreamBests.AllKinds.Where(k => !IsShown(k))];
        _save(Options, HiddenItems);
    }

    private bool IsShown(string kind) =>
        kind == StreamBests.KindSb ? ShowSb
        : kind == StreamBests.KindPb ? ShowPb
        : kind == StreamBests.KindTarget ? ShowTarget
        : ShowWr;

    private void ApplyHidden(IReadOnlyList<string> hidden)
    {
        var visible = new HashSet<string>(StreamBests.Visible(hidden), StringComparer.Ordinal);
        _applyingHidden = true;
        ShowSb = visible.Contains(StreamBests.KindSb);
        ShowPb = visible.Contains(StreamBests.KindPb);
        ShowTarget = visible.Contains(StreamBests.KindTarget);
        ShowWr = visible.Contains(StreamBests.KindWr);
        _applyingHidden = false;
        HiddenItems = [.. StreamBests.AllKinds.Where(k => !visible.Contains(k))];
    }

    public IBrush BackgroundBrush => new SolidColorBrush(Color.Parse(Options.Background));

    public WindowDecorations WindowDecorations
        => Options.Borderless ? WindowDecorations.None : WindowDecorations.Full;

    public double FontSize => 12.0 * Options.FontScale;

    [ObservableProperty]
    public partial string Text { get; set; } = PlayLiveQuery.WaitingText;

    [ObservableProperty]
    public partial string? StatusText { get; set; }

    public event Action? CloseRequested;

    public event Action<double, double>? ResizeRequested;

    public event Action? HideOffscreenRequested;

    public event Action? RestoreOnscreenRequested;

    [RelayCommand]
    private void HideOffscreen() => HideOffscreenRequested?.Invoke();

    [RelayCommand]
    private void ShowOnscreen() => RestoreOnscreenRequested?.Invoke();

    public string ResetSizeLabel =>
        "サイズを " + (int)StreamPanelOptions.Default.Width + "x"
        + (int)StreamPanelOptions.Default.Height + " に戻す";

    public void Refresh()
    {
        _frame++;
        ApplyHidden(_readHidden());
        try
        {
            if (!TrackerDb.MainDbExists)
            {
                StatusText = TrackerDb.MainDbPath is null
                    ? "本体 DB の場所が未設定（TrackerDb.MainDbPath）。"
                    : "本体 DB が見つかりません: " + TrackerDb.MainDbPath;
                Text = "";
                return;
            }
            using var db = TrackerDb.OpenMainDb();
            var progress = PlayLiveQuery.Load(db, _frame % 4, narrow: true, blockLookup: sid =>
                TrackerDb.MainDbPath is string mainDbPath
                    ? StreamBlockSource.Load(mainDbPath, sid,
                        StreamTargets.Load(AppSettingsSource.ConfigPath).Entries, out _,
                        HiddenItems, narrow: true)
                    : []);
            StatusText = progress.StatusText;
            Text = progress.Text;
        }
        catch (Exception ex)
        {
            StatusText = "本体 DB を読めませんでした: " + ex.Message;
        }
    }

    [RelayCommand]
    private void SetBackground(string hex) => Update(Options with { Background = hex });

    public const double MinFontScale = 0.6;

    public const double MaxFontScale = 3.0;

    public const double FontScaleStep = 0.1;

    private static double ClampScale(double v)
        => Math.Max(MinFontScale, Math.Min(MaxFontScale, v));

    private static double Shift(double v, double step)
    {
        var cur = ClampScale(double.IsFinite(v) ? v : StreamPanelOptions.Default.FontScale);
        return ClampScale((double)((decimal)cur + (decimal)step));
    }

    [RelayCommand]
    private void EnlargeFont()
        => Update(Options with { FontScale = Shift(Options.FontScale, FontScaleStep) });

    [RelayCommand]
    private void ShrinkFont()
        => Update(Options with { FontScale = Shift(Options.FontScale, -FontScaleStep) });

    [RelayCommand]
    private void ToggleTopmost() => Update(Options with { Topmost = !Options.Topmost });

    [RelayCommand]
    private void ToggleBorderless() => Update(Options with { Borderless = !Options.Borderless });

    [RelayCommand]
    private void ResetSize()
    {
        var next = Options with
        {
            Width = StreamPanelOptions.Default.Width,
            Height = StreamPanelOptions.Default.Height,
        };
        Update(next);
        ResizeRequested?.Invoke(next.Width, next.Height);
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    internal void ApplyOptions(StreamPanelOptions next)
    {
        var resize = next.Width != Options.Width || next.Height != Options.Height;
        Options = next;
        if (resize) ResizeRequested?.Invoke(next.Width, next.Height);
    }

    private void Update(StreamPanelOptions next)
    {
        Options = next;
        _save(next, HiddenItems);
    }
}
