using System.Runtime.Versioning;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TH09.Record;
using TH09.Shell.Data;
using TH09.Shell.Navigation;

namespace TH09.Shell.ViewModels;

internal sealed partial class ShellViewModel : ObservableObject, INavigationHost, IDisposable
{
    private readonly DriveProcessController _driveControl;

    private ShellViewModel(NavigationService navigation, IReadOnlyList<TabViewModelBase> tabs,
                           ReplayDetailViewModel detail, DriveProcessController driveControl,
                           DriveControlViewModel drive, StreamPanelViewModel streamPanel)
    {
        Navigation = navigation;
        Tabs = tabs;
        Detail = detail;
        _driveControl = driveControl;
        Drive = drive;
        StreamPanel = streamPanel;
        SelectedTab = tabs[0];
    }

    public static ShellViewModel Create(IDriveProcessLauncher? launcher = null)
    {
        var navigation = new NavigationService();

        TabViewModelBase[] tabs =
        [
            new PlayTabViewModel(navigation),
            new HistoryTabViewModel(navigation),
            new ReplayTabViewModel(navigation),
            new StatsTabViewModel(navigation),
        ];

        var detail = new ReplayDetailViewModel(navigation);
        detail.AttachPanes(new HitWindowViewModel(), new TimelineViewModel());

        var driveControl = launcher is null
            ? new DriveProcessController()
            : new DriveProcessController(launcher);
        var drive = new DriveControlViewModel(navigation, driveControl);

        if (!TestLaunchGuard.Active)
        {
            try
            {
                driveControl.Start(TH09.Launch.LaunchKind.RestoreSlots);
            }
            catch (Exception exc)
            {
                LogSource.Warn("起動", "前回の退避の回収を始められません: " + LogSource.Describe(exc));
            }
        }

        var initialStreamOptions = StreamPanelOptions.Default;
        if (OperatingSystem.IsWindows())
            initialStreamOptions = StreamOptionsOf(AppSettingsSource.Current.StreamMode);
        var streamPanel = new StreamPanelViewModel(
            initialStreamOptions, SaveStreamSettings,
            static () => OperatingSystem.IsWindows()
                ? AppSettingsSource.Current.StreamPanelHiddenItems
                : []);

        var shell = new ShellViewModel(navigation, tabs, detail, driveControl, drive, streamPanel);
        navigation.Attach(shell);
        drive.Settings.Adopted += () =>
        {
            if (!OperatingSystem.IsWindows()) return;
            streamPanel.ApplyOptions(StreamOptionsOf(AppSettingsSource.Current.StreamMode));
        };
        return shell;
    }

    [SupportedOSPlatform("windows")]
    private static StreamPanelOptions StreamOptionsOf(StreamModeSettings sm)
        => new(sm.Width, sm.Height, sm.FontScale, sm.Background, sm.Topmost, sm.Borderless);

    public string WindowTitle => "幻想閻魔帳";

    public DriveControlViewModel Drive { get; }

    public StreamPanelViewModel StreamPanel { get; }

    [ObservableProperty]
    public partial bool IsStreamPanelOpen { get; set; }

    public string StreamPanelText => "配信パネル";

    [RelayCommand]
    private void ToggleStreamPanel() => IsStreamPanelOpen = !IsStreamPanelOpen;

    internal static void SaveStreamSettings(StreamPanelOptions options,
                                            IReadOnlyList<string> hiddenItems)
    {
        if (!OperatingSystem.IsWindows()) return;
        var current = AppSettingsSource.Current;
        var next = current with
        {
            StreamMode = current.StreamMode with
            {
                Width = (int)Math.Round(options.Width),
                Height = (int)Math.Round(options.Height),
                FontScale = options.FontScale,
                Background = options.Background,
                Topmost = options.Topmost,
                Borderless = options.Borderless,
            },
            StreamPanelHiddenItems = hiddenItems,
        };
        var outcome = ConfigStore.Save(AppSettingsSource.ConfigPath, next);
        if (outcome.Written) AppSettingsSource.Adopt(next);
        else LogSource.Error("配信パネル", "設定を保存できませんでした: " + (outcome.Reason ?? "理由不明"));
    }

    public void Dispose() => _driveControl.Dispose();

    public NavigationService Navigation { get; }

    public IReadOnlyList<TabViewModelBase> Tabs { get; }

    [ObservableProperty]
    public partial TabViewModelBase? SelectedTab { get; set; }

    public ReplayDetailViewModel Detail { get; }

    [ObservableProperty]
    public partial bool IsDetailOpen { get; set; }


    public void NavigateBack()
    {
        if (IsDetailOpen) { if (Detail is IInnerHistory di && di.TryGoBack()) return; }
        else if (SelectedTab is IInnerHistory inner && inner.TryGoBack()) return;
        Navigation.GoBack();
    }

    public void NavigateForward()
    {
        if (IsDetailOpen) { if (Detail is IInnerHistory di && di.TryGoForward()) return; }
        else if (SelectedTab is IInnerHistory inner && inner.TryGoForward()) return;
        Navigation.GoForward();
    }
}
