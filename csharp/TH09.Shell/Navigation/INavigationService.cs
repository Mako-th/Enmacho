namespace TH09.Shell.Navigation;

internal interface INavigationService
{
    void OpenReplayDetail(ReplayDetailRequest request);

    void ShowTab(ShellTab tab);

    bool TryShowTab(ShellTab tab);

    bool CanGoBack { get; }

    void GoBack();

    bool CanGoForward { get; }

    void GoForward();
}

internal interface IInnerHistory
{
    bool TryGoBack();

    bool TryGoForward();
}
