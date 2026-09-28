using CommunityToolkit.Mvvm.ComponentModel;
using TH09.Shell.Navigation;

namespace TH09.Shell.ViewModels;

internal enum DbUpdateCause
{
    ScanEnded,
    ImportEnded,
    PlayEnded,
    ReplayRegistered,
    HistoryEdited,
}

internal static class DbUpdateCauses
{
    public static string Label(DbUpdateCause cause) => cause switch
    {
        DbUpdateCause.ScanEnded => "走査の終了",
        DbUpdateCause.ImportEnded => "既存Replay一括登録の終了",
        DbUpdateCause.PlayEnded => "プレイ 1 回ぶんの記録の終了",
        DbUpdateCause.ReplayRegistered => "Replay保存監視の登録",
        DbUpdateCause.HistoryEdited => "履歴の削除・整理",
        _ => cause.ToString(),
    };
}

internal interface IReloadsOnDbUpdate
{
    void ReloadOnDbUpdate(DbUpdateCause cause);
}

internal abstract class TabViewModelBase : ObservableObject
{
    protected TabViewModelBase(INavigationService navigation) => Navigation = navigation;

    protected INavigationService Navigation { get; }

    public abstract ShellTab Key { get; }

    public abstract string Title { get; }

    public abstract string Placeholder { get; }
}
