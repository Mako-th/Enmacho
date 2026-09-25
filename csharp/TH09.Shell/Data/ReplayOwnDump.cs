using System.Text;
using TH09.Record;
using TH09.Shell.Navigation;
using TH09.Shell.ViewModels;

namespace TH09.Shell.Data;

internal static class ReplayOwnDump
{
    public const string Flag = "--dump-replay-own";

    public static int Run(string db)
    {
        using var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("所有の覆しの吐き出しは Windows でだけ動きます。");
                return 3;
            }
            if (HistoryMaintenance.PointsAtRealData(db, null))
            {
                Console.Error.WriteLine(
                    "本物の記録の場所を指しています（本体 DB のフォルダ）。"
                    + "この口は合成の DB だけを受け付けます: " + db);
                return 3;
            }

            TrackerDb.MainDbPath = db;
            TrackerDb.Layer0DbPath = null;

            var nav = new CountingNavigation();
            var vm = new ReplayTabViewModel(nav);
            Write(stdout, "load", vm.Rows.Count > 0 && string.IsNullOrEmpty(vm.StatusText));
            var pickedChar = vm.CharacterOptions.Count > 1 ? vm.CharacterOptions[1] : null;
            if (pickedChar is not null) vm.SelectedCharacter = pickedChar;
            var sortBefore = vm.SortKey;
            var ownOnlyBefore = vm.Rows.Count(r => r.IsOwn);

            var opened = nav.Opened;
            var target = vm.Rows[0];
            var replayId = target.ReplayId;
            var ownBefore = target.Own;
            vm.ContextHeld = true;
            vm.SelectedRow = target;
            Write(stdout, "context-row",
                  vm.HasContextRow && ReferenceEquals(vm.ContextRow, target) && nav.Opened == opened);

            Write(stdout, "mark-auto",
                  vm.OwnIsAuto && !vm.OwnIsForeign && !vm.OwnIsP1 && !vm.OwnIsP2
                  && vm.OwnAutoText.StartsWith(ReplayOwnLabels.Mark, StringComparison.Ordinal)
                  && vm.OwnP2Text.StartsWith(ReplayOwnLabels.Seat, StringComparison.Ordinal));

            vm.ContextHeld = false;
            vm.ContextHeld = true;
            Write(stdout, "context-empty", !vm.HasContextRow);

            Pick(vm, replayId);

            vm.SetOwnP2Command.Execute(null);
            Write(stdout, "apply-p2-db", ReadOverride(replayId) == ReplayOwnership.OwnP2);
            Write(stdout, "apply-p2-note",
                  vm.OwnNote is string n1 && n1.Contains(ReplayOwnLabels.OwnP2, StringComparison.Ordinal)
                  && n1.Contains(ReplayOwnLabels.EffectNote, StringComparison.Ordinal));
            var after = vm.Rows.FirstOrDefault(r => r.ReplayId == replayId);
            Write(stdout, "apply-p2-row",
                  after is not null && after.Own == OwnSide.P2
                  && after.OwnOverride == ReplayOwnership.OwnP2);
            Write(stdout, "keep-filter",
                  (pickedChar is null || vm.SelectedCharacter?.Value == pickedChar.Value)
                  && vm.SortKey == sortBefore);
            Write(stdout, "context-cleared", !vm.HasContextRow);

            Pick(vm, replayId);
            Write(stdout, "mark-p2",
                  vm.OwnIsP2 && !vm.OwnIsAuto
                  && vm.OwnP2Text.StartsWith(ReplayOwnLabels.Mark, StringComparison.Ordinal)
                  && vm.OwnAutoText.StartsWith(ReplayOwnLabels.Seat, StringComparison.Ordinal));
            vm.SetOwnP2Command.Execute(null);
            Write(stdout, "apply-again",
                  vm.OwnNote is string n2 && n2.Contains("変わっていません", StringComparison.Ordinal)
                  && ReadOverride(replayId) == ReplayOwnership.OwnP2);

            Pick(vm, replayId);
            vm.SetOwnForeignCommand.Execute(null);
            var foreignRow = vm.Rows.First(r => r.ReplayId == replayId);
            Write(stdout, "apply-foreign",
                  ReadOverride(replayId) == ReplayOwnership.Foreign
                  && foreignRow.Own == OwnSide.None && !foreignRow.IsOwn);

            Pick(vm, replayId);
            vm.SetOwnAutoCommand.Execute(null);
            var backRow = vm.Rows.First(r => r.ReplayId == replayId);
            Write(stdout, "back-to-auto",
                  ReadOverride(replayId) is null && backRow.Own == ownBefore
                  && backRow.OwnOverride is null
                  && vm.Rows.Count(r => r.IsOwn) == ownOnlyBefore);

            var miss = ReplayOwnership.SetOverride(db, [-1L], ReplayOwnership.OwnP1);
            Write(stdout, "missing-id",
                  miss.Missing.Count == 1 && miss.Changed == 0 && miss.Unchanged == 0);

            var rejected = false;
            try { ReplayOwnership.SetOverride(db, [replayId], 3); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Write(stdout, "reject-unknown", rejected && ReadOverride(replayId) is null);

            var empty = ReplayOwnership.SetOverride(db, [], ReplayOwnership.OwnP1);
            Write(stdout, "empty-noop",
                  empty.Changed == 0 && empty.Missing.Count == 0 && empty.Requested.Count == 0);

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

    private static void Pick(ReplayTabViewModel vm, long replayId)
    {
        vm.ContextHeld = true;
        vm.SelectedRow = vm.Rows.First(r => r.ReplayId == replayId);
    }

    private static int? ReadOverride(long replayId)
    {
        using var conn = TrackerDb.OpenMainDb();
        using var cmd = conn.Command("SELECT own_override FROM replays WHERE replay_id=$0");
        cmd.Parameters.AddWithValue("$0", replayId);
        using var r = cmd.ExecuteReader();
        if (!r.Read() || r.IsDBNull(0)) return null;
        return (int)r.GetInt64(0);
    }

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
