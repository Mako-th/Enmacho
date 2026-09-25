using CommunityToolkit.Mvvm.ComponentModel;
using TH09.Shell.ViewModels;

namespace TH09.Shell.Navigation;

internal sealed class NavigationService : ObservableObject, INavigationService
{
    private readonly record struct Place(ShellTab? Tab, ReplayDetailRequest? DetailRequest);

    private readonly Stack<Place> _back = new();

    private readonly Stack<Place> _forward = new();

    private INavigationHost? _host;
    private bool _canGoBack;
    private bool _canGoForward;

    public void Attach(INavigationHost host) => _host = host;

    public bool CanGoBack
    {
        get => _canGoBack;
        private set => SetProperty(ref _canGoBack, value);
    }

    public bool CanGoForward
    {
        get => _canGoForward;
        private set => SetProperty(ref _canGoForward, value);
    }

    private void Sync()
    {
        CanGoBack = _back.Count > 0;
        CanGoForward = _forward.Count > 0;
    }

    public void OpenReplayDetail(ReplayDetailRequest request)
    {
        var host = Host();
        _back.Push(Snapshot(host));
        _forward.Clear();
        host.Detail.Show(request);
        host.IsDetailOpen = true;
        Sync();
    }

    public void ShowTab(ShellTab tab)
    {
        var host = Host();
        var target = host.Tabs.FirstOrDefault(t => t.Key == tab)
                     ?? throw new InvalidOperationException("タブ " + tab + " が Tabs に登録されていない");
        Move(host, target);
    }

    public bool TryShowTab(ShellTab tab)
    {
        var host = Host();
        var target = host.Tabs.FirstOrDefault(t => t.Key == tab);
        if (target is null) return false;
        Move(host, target);
        return true;
    }

    private void Move(INavigationHost host, TabViewModelBase target)
    {
        if (!host.IsDetailOpen && ReferenceEquals(host.SelectedTab, target)) return;
        _back.Push(Snapshot(host));
        _forward.Clear();
        host.IsDetailOpen = false;
        host.SelectedTab = target;
        Sync();
    }

    public void GoBack()
    {
        if (_back.Count == 0) return;
        var host = Host();
        _forward.Push(Snapshot(host));
        Restore(host, _back.Pop());
        Sync();
    }

    public void GoForward()
    {
        if (_forward.Count == 0) return;
        var host = Host();
        _back.Push(Snapshot(host));
        Restore(host, _forward.Pop());
        Sync();
    }

    private static void Restore(INavigationHost host, Place place)
    {

        if (place.Tab is ShellTab tab)
        {
            var target = host.Tabs.FirstOrDefault(t => t.Key == tab);
            if (target is not null) host.SelectedTab = target;
        }

        if (place.DetailRequest is ReplayDetailRequest request)
        {
            host.Detail.Show(request);
            host.IsDetailOpen = true;
        }
        else
        {
            host.IsDetailOpen = false;
        }

    }

    private static Place Snapshot(INavigationHost host)
        => new(host.SelectedTab?.Key,
               host.IsDetailOpen ? host.Detail.Request : null);

    private INavigationHost Host()
        => _host ?? throw new InvalidOperationException(
               "NavigationService.Attach がまだ呼ばれていない（シェルの組み立て順が崩れている）");
}
