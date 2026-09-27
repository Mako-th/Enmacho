using CommunityToolkit.Mvvm.ComponentModel;
using TH09.Shell.Navigation;

namespace TH09.Shell.ViewModels;

internal abstract class TabViewModelBase : ObservableObject
{
    protected TabViewModelBase(INavigationService navigation) => Navigation = navigation;

    protected INavigationService Navigation { get; }

    public abstract ShellTab Key { get; }

    public abstract string Title { get; }

    public abstract string Placeholder { get; }
}
