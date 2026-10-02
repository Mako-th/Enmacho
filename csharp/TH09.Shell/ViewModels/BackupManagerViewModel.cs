using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TH09.Record;
using TH09.Shell.Data;

namespace TH09.Shell.ViewModels;

internal sealed record BackupRow(BackupFolder Folder, bool Complete)
{
    public string Name => System.IO.Path.GetFileName(Folder.Path);

    public string KindText => Complete ? "完成" : "未完成";

    public string SizeText => BackupLayout.Size(Folder.Bytes) + " / ファイル " + Folder.Files + " 個";

    public override string ToString() => Name + "　" + KindText + "　" + SizeText;
}

[SupportedOSPlatform("windows")]
internal sealed partial class BackupManagerViewModel : ObservableObject
{
    private const string Category = "バックアップ";

    public const string ConfirmTitle = "バックアップを消します";

    public const string LastCompleteNote = "消すと戻せるバックアップが無くなります";

    public const string EmptyText = "バックアップはありません";

    public const string BlockedByRunningScan = "走査が動いているので消せません";

    public const string BlockedByOtherScan = "別のプロセスが走査中なので消せません";

    public const string BlockedByInProgress = "取っている最中のバックアップがあるので消せません";

    public const string BlockedByRemoving = "バックアップを消している最中なので消せません";

    private readonly Func<string> rootOf;
    private readonly Func<DateTime> nowOf;
    private readonly Func<bool> scanActiveOutside;
    private readonly Func<string, long> freeOf;
    private readonly Func<string, string, DateTime, BackupRemoveResult> removeOp;

    private bool isRemoving;

    private BackupRow? pending;

    private int inProgress;

    public BackupManagerViewModel(Func<string> rootOf, Func<DateTime>? nowOf = null,
                                  Func<bool>? scanActiveOutside = null,
                                  Func<string, long>? freeOf = null,
                                  Func<string, string, DateTime, BackupRemoveResult>? removeOp = null)
    {
        this.removeOp = removeOp ?? BackupLayout.Remove;
        this.rootOf = rootOf ?? throw new ArgumentNullException(nameof(rootOf));
        this.nowOf = nowOf ?? (() => DateTime.Now);
        this.scanActiveOutside = scanActiveOutside ?? BackupLayout.ScanIsActiveElsewhere;
        this.freeOf = freeOf ?? FreeBytes;
    }

    internal string Root => rootOf();

    public Func<bool> IsScanRunning { get; set; } = () => false;

    public ObservableCollection<BackupRow> Rows { get; } = [];

    [ObservableProperty]
    public partial int SelectedIndex { get; set; } = -1;

    [ObservableProperty]
    public partial string SummaryText { get; set; } = "";

    [ObservableProperty]
    public partial string BlockedText { get; set; } = "";

    [ObservableProperty]
    public partial string ResultText { get; set; } = "";

    [ObservableProperty]
    public partial bool CanRemove { get; set; }

    [ObservableProperty]
    public partial bool IsConfirmOpen { get; set; }

    [ObservableProperty]
    public partial string ConfirmText { get; set; } = "";

    public string ConfirmHeader => ConfirmTitle;

    public bool HasBlocked => BlockedText.Length > 0;

    public bool IsEmpty => Rows.Count == 0;

    public string EmptyLabel => EmptyText;

    partial void OnBlockedTextChanged(string value) => OnPropertyChanged(nameof(HasBlocked));

    partial void OnSelectedIndexChanged(int value) => Recompute();

    internal string? BlockReason()
    {
        if (isRemoving) return BlockedByRemoving;
        if (IsScanRunning()) return BlockedByRunningScan;
        if (scanActiveOutside()) return BlockedByOtherScan;
        if (inProgress > 0) return BlockedByInProgress;
        return null;
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    public void Refresh()
    {
        if (isRemoving) return;
        var root = rootOf();
        BackupInventory inv;
        try
        {
            inv = BackupLayout.List(root, nowOf());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Rows.Clear();
            inProgress = 0;
            SelectedIndex = -1;
            SummaryText = "バックアップの置き先を読めませんでした: " + ex.Message;
            OnPropertyChanged(nameof(IsEmpty));
            Recompute();
            return;
        }
        Rows.Clear();
        foreach (var e in inv.Entries) Rows.Add(new BackupRow(e.Folder, e.Complete));
        inProgress = inv.InProgress;
        SelectedIndex = -1;
        var free = freeOf(root);
        SummaryText = "合計 " + BackupLayout.Size(inv.Entries.Sum(e => e.Folder.Bytes))
                      + "（" + Rows.Count + " 件）／ 置き先の空き "
                      + (free < 0 ? "不明" : BackupLayout.Size(free));
        OnPropertyChanged(nameof(IsEmpty));
        Recompute();
    }

    public bool CanRefresh => !isRemoving;

    public void NotifyScanChanged() => Recompute();

    private void Recompute()
    {
        BlockedText = BlockReason() ?? "";
        CanRemove = SelectedIndex >= 0 && SelectedIndex < Rows.Count && BlockedText.Length == 0;
    }

    [RelayCommand]
    private void Remove()
    {
        Recompute();
        if (!CanRemove) return;
        var row = Rows[SelectedIndex];
        pending = row;
        var text = row.Name + "（" + row.SizeText + "）を消します";
        if (row.Complete && Rows.Count(r => r.Complete) == 1) text += "\n" + LastCompleteNote;
        ConfirmText = text;
        IsConfirmOpen = true;
    }

    [RelayCommand]
    private async Task ConfirmRemove()
    {
        IsConfirmOpen = false;
        var row = pending;
        pending = null;
        if (row is null) return;
        if (BlockReason() is { } why)
        {
            ResultText = "消していません: " + why;
            LogSource.Info(Category, ConfirmTitle + ": 消していません（" + why + "）: " + row.Name);
            Recompute();
            return;
        }
        var root = rootOf();
        var now = nowOf();
        var op = removeOp;
        var path = row.Folder.Path;
        isRemoving = true;
        RefreshCommand.NotifyCanExecuteChanged();
        ResultText = "消しています: " + row.Name + "（" + row.SizeText + "）";
        Recompute();
        BackupRemoveResult result;
        try
        {
            result = await Task.Run(() => op(root, path, now));
        }
        catch (Exception ex)
        {
            result = new BackupRemoveResult(null, "消せませんでした: " + ex.GetType().Name + ": " + ex.Message);
        }
        isRemoving = false;
        RefreshCommand.NotifyCanExecuteChanged();
        if (result.Removed is { } gone)
        {
            ResultText = "消しました: " + row.Name + "（" + BackupLayout.Size(gone.Bytes) + "）";
            LogSource.Info(Category, ConfirmTitle + ": はい（" + row.Name + "・"
                                     + BackupLayout.Size(gone.Bytes) + " / " + "ファイル " + gone.Files + " 個を消しました）");
        }
        else
        {
            ResultText = "消していません: " + result.Reason;
            LogSource.Warn(Category, ConfirmTitle + ": 消していません（" + result.Reason + "）");
        }
        Refresh();
    }

    [RelayCommand]
    private void CancelRemove()
    {
        IsConfirmOpen = false;
        var name = pending?.Name ?? "";
        pending = null;
        LogSource.Info(Category, ConfirmTitle + ": いいえ（消していません）: " + name);
    }

    private static long FreeBytes(string root)
    {
        try
        {
            var top = Path.GetPathRoot(Path.GetFullPath(root));
            return string.IsNullOrEmpty(top) ? -1 : new DriveInfo(top).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                      or ArgumentException)
        {
            return -1;
        }
    }
}
