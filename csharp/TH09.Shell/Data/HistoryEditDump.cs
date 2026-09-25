using System.Globalization;
using System.Text;
using TH09.Record;
using TH09.Shell.Navigation;
using TH09.Shell.ViewModels;

namespace TH09.Shell.Data;

internal static class HistoryEditDump
{
    public const string Flag = "--dump-history-edit";

    public static int Run(string db, string layer0, string keepA, string keepC)
    {
        using var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("履歴の削除・整理の吐き出しは Windows でだけ動きます。");
                return 3;
            }
            if (HistoryMaintenance.PointsAtRealData(db, layer0))
            {
                Console.Error.WriteLine(
                    "本物の記録の場所を指しています（本体 DB か Layer 0 のフォルダ）。"
                    + "この口は合成の対だけを受け付けます: " + db + " / " + layer0);
                return 3;
            }
            if (layer0 == "-")
            {
                Console.Error.WriteLine(
                    Flag + " に - は渡せません（生tickごと消えるかを見る口なので、実在する Layer 0 が要ります）。");
                return 1;
            }
            if (!int.TryParse(keepA, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ka)
                || !int.TryParse(keepC, NumberStyles.Integer, CultureInfo.InvariantCulture, out var kc))
            {
                Console.Error.WriteLine("保持数は整数で渡してください: " + keepA + " / " + keepC);
                return 1;
            }

            TrackerDb.MainDbPath = db;
            TrackerDb.Layer0DbPath = layer0;

            var nav = new CountingNavigation();
            var vm = new HistoryTabViewModel(nav);
            vm.Kind = HistoryKind.All;

            Write(stdout, "load", vm.Rows.Count > 0 && string.IsNullOrEmpty(vm.StatusText));

            var protectedIds = HistoryMaintenance.ProtectedSessionIds(db);
            Write(stdout, "protected-mark", vm.ProtectedCount == protectedIds.Count);

            vm.ModifierHeld = false;
            var opened = nav.Opened;
            vm.Selection.Select(0);
            Write(stdout, "open-plain", nav.Opened == opened + 1 && vm.Selection.Count == 0);

            vm.ModifierHeld = true;
            opened = nav.Opened;
            vm.Selection.Select(0);
            Write(stdout, "open-modifier", nav.Opened == opened && vm.Selection.Count == 1);

            opened = nav.Opened;
            vm.SelectAllCommand.Execute(null);
            Write(stdout, "select-all",
                  vm.Selection.Count == vm.Rows.Count && vm.Rows.Count > 0 && nav.Opened == opened);

            vm.Selection.Clear();
            vm.RequestDeleteCommand.Execute(null);
            Write(stdout, "delete-none", !vm.IsDeleteConfirmOpen);

            vm.RetentionSource = () => (0, 0);
            vm.RequestPruneCommand.Execute(null);
            Write(stdout, "prune-nothing",
                  vm.IsNoticeOpen && !vm.IsPruneConfirmOpen
                  && vm.NoticeText == HistoryTabViewModel.PruneNothingText);
            vm.CloseNoticeCommand.Execute(null);

            vm.RetentionSource = () => (ka, kc);
            var planned = HistoryMaintenance.Plan(db, ka, kc).ToDelete.Count;
            var rowsBefore = vm.Rows.Count;
            vm.RequestPruneCommand.Execute(null);
            Write(stdout, "prune-confirm-first",
                  planned > 0 && vm.IsPruneConfirmOpen
                  && vm.PruneConfirmText == HistoryTabViewModel.PrunePrompt(ka, planned)
                  && vm.Rows.Count == rowsBefore);

            vm.CancelPruneCommand.Execute(null);
            Write(stdout, "prune-cancel", !vm.IsPruneConfirmOpen && vm.Rows.Count == rowsBefore);

            vm.RequestPruneCommand.Execute(null);
            vm.ConfirmPruneCommand.Execute(null);
            Write(stdout, "prune-apply",
                  planned > 0 && vm.LastDeleted.Count > 0 && vm.Rows.Count < rowsBefore);
            stdout.Write("pruned\t"
                         + string.Join(",", vm.LastDeleted.Select(
                               id => id.ToString(CultureInfo.InvariantCulture))) + "\n");

            var savedLayer0 = TrackerDb.Layer0DbPath;
            TrackerDb.Layer0DbPath = null;
            vm.ModifierHeld = true;
            vm.Selection.Clear();
            vm.Selection.Select(0);
            var rowsKept = vm.Rows.Count;
            vm.RequestDeleteCommand.Execute(null);
            vm.ConfirmDeleteCommand.Execute(null);
            Write(stdout, "layer0-missing",
                  vm.IsNoticeOpen && vm.NoticeTitle == HistoryTabViewModel.DeleteFailedTitle
                  && vm.Rows.Count == rowsKept);
            vm.CloseNoticeCommand.Execute(null);
            TrackerDb.Layer0DbPath = savedLayer0;

            vm.ModifierHeld = true;
            vm.Selection.Clear();
            vm.Selection.Select(0);
            vm.Selection.Select(1);
            var two = Selected(vm);
            var rowsTwo = vm.Rows.Count;
            vm.RequestDeleteCommand.Execute(null);
            Write(stdout, "delete-confirm-first",
                  two.Count == 2 && vm.IsDeleteConfirmOpen
                  && vm.DeleteConfirmText == HistoryTabViewModel.DeletePrompt(two)
                                             + HistoryTabViewModel.DeleteConfirmTail
                  && vm.Rows.Count == rowsTwo);

            vm.CancelDeleteCommand.Execute(null);
            Write(stdout, "delete-cancel", !vm.IsDeleteConfirmOpen && vm.Rows.Count == rowsTwo);

            vm.Selection.Clear();
            vm.Selection.Select(0);
            var one = Selected(vm);
            vm.RequestDeleteCommand.Execute(null);
            var head = one.Count == 1
                ? "Session " + one[0].ToString(CultureInfo.InvariantCulture) + " のDB記録を削除しますか？"
                : "";
            Write(stdout, "delete-single-text",
                  one.Count == 1 && vm.IsDeleteConfirmOpen
                  && vm.DeleteConfirmText.StartsWith(head, StringComparison.Ordinal));

            vm.ConfirmDeleteCommand.Execute(null);
            Write(stdout, "delete-apply",
                  one.Count == 1 && vm.LastDeleted.Contains(one[0])
                  && vm.Rows.All(r => r.SessionId != one[0]));
            stdout.Write("deleted\t"
                         + string.Join(",", vm.LastDeleted.Select(
                               id => id.ToString(CultureInfo.InvariantCulture))) + "\n");

            Write(stdout, "end", true);
            stdout.Flush();
            return 0;
        }
        catch (Exception ex)
        {
            stdout.Flush();
            Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
    }

    private static List<long> Selected(HistoryTabViewModel vm)
        => vm.Selection.SelectedItems.OfType<HistoryRow>().Select(r => r.SessionId).ToList();

    private static void Write(TextWriter stdout, string name, bool ok)
        => stdout.Write(name + "\t" + (ok ? "ok" : "NG") + "\n");

    private sealed class CountingNavigation : INavigationService
    {
        public int Opened { get; private set; }

        public void OpenReplayDetail(ReplayDetailRequest request) => Opened++;

        public void ShowTab(ShellTab tab) { }

        public bool TryShowTab(ShellTab tab) => true;

        public bool CanGoBack => false;

        public void GoBack() { }

        public bool CanGoForward => false;

        public void GoForward() { }
    }
}
