using TH09.Shell.ViewModels;

namespace TH09.Shell.Navigation;

internal interface INavigationHost
{
    IReadOnlyList<TabViewModelBase> Tabs { get; }

    TabViewModelBase? SelectedTab { get; set; }

    ReplayDetailViewModel Detail { get; }

    bool IsDetailOpen { get; set; }
}
