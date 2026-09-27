using System.Globalization;
using System.Text;

namespace TH09.Drive;

internal static class SlotDump
{
    public const string Flag = "--dump-slot";

    public const int DeadPid = 999999;

    public static int Run(TextWriter writer)
    {
        string root = Path.Combine(Path.GetTempPath(),
            "th09_slotdump_" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture)
            + "_" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
        string replayDir = Path.Combine(root, "replay");
        Directory.CreateDirectory(root);

        bool closed = false;
        int step = 0;
        var script = new Script(writer, root, replayDir);
        void Log(string message)
        {
            if (!closed) script.Row("log", script.Mask(message));
        }
        script.Log = Log;

        void Step(string verb, params string[] args)
        {
            step++;
            script.Row(["step", Num(step), verb, .. args]);
        }

        writer.Write("root\t" + Esc(root) + "\n");
        script.Row("fact", "dead_pid", Num(DeadPid));
        script.Row("fact", "dead_pid_alive", ReplaySlots.PidAlive(DeadPid) ? "1" : "0");
        script.Row("fact", "self_pid_alive", ReplaySlots.PidAlive(Environment.ProcessId) ? "1" : "0");
        script.Row("fact", "now_iso", ReplaySlots.NowIso());

        foreach (string name in new[] { "th9_07.rpy", "TH9_22.RPY", "th9_00.rpy", "th9_26.rpy",
                                        "th9_5.rpy", "th9_007.rpy", "th9_25.rpy.tickscan.bak",
                                        "th9_12.rpy.tickscan.owned", "foo.rpy", "th9_xx.rpy" })
        {
            Step("slotof", name);
            int? got = ReplaySlots.SlotOf(name);
            script.Row("out", got is null ? "-" : Num(got.Value));
        }
        foreach (string rel in new[] { "replay/th9_07.rpy", "replay/th9_5.rpy", "replay/th9_00.rpy",
                                       "replay/sub/th9_07.rpy", "store/th9_07.rpy",
                                       "replay/th9_07.rpy.tickscan.bak" })
        {
            Step("isslot", rel);
            script.Row("out", ReplaySlots.IsSlotFile(script.At(rel), [replayDir]) ? "1" : "0");
        }
        foreach (var (a, b) in new[] { ("replay", "replay"), ("replay", "store"),
                                       ("replay", "store/../replay"), ("replay", "replay/sub") })
        {
            Step("samedir", a, b);
            script.Row("out", ReplaySlots.SameDir(script.At(a), script.At(b)) ? "1" : "0");
        }
        foreach (int slot in new[] { 3, 25, 1 })
        {
            Step("slotpath", Num(slot));
            script.Row("out", script.Rel(ReplaySlots.SlotPath(replayDir, slot)));
        }
        foreach (string token in new[] { "@self", "@dead", "0", "-4", "@none" })
        {
            Step("pidalive", token);
            script.Row("out", ReplaySlots.PidAlive(script.Pid(token)) ? "1" : "0");
        }

        script.Restore(Step);

        script.Write(Step, "replay/th9_07.rpy", "SCAN-PUT-22");
        script.Write(Step, "replay/th9_07.rpy" + ReplaySlots.BackupSuffix, "ORIGINAL-07");
        script.Marker(Step, "replay/th9_07.rpy" + ReplaySlots.OwnedSuffix,
                      "{\"slot\": 7, \"had_backup\": true, \"pid\": @dead}");

        script.Write(Step, "replay/th9_22.rpy", "SCAN-PUT-07");
        script.Marker(Step, "replay/th9_22.rpy" + ReplaySlots.OwnedSuffix,
                      "{\"slot\": 22, \"had_backup\": false, \"pid\": @dead}");

        script.Write(Step, "replay/th9_03.rpy", "SCAN-PUT-19");
        script.Marker(Step, "replay/th9_03.rpy" + ReplaySlots.OwnedSuffix,
                      "{\"slot\": 3, \"had_backup\": false, \"pid\": @selfstr}");

        script.Write(Step, "replay/th9_14.rpy", "SCAN-PUT-03");
        script.Marker(Step, "replay/th9_14.rpy" + ReplaySlots.OwnedSuffix,
                      "{\"slot\": 14, \"had_backup\":");

        script.Write(Step, "replay/th9_19.rpy", "SCAN-PUT-14");
        script.Marker(Step, "replay/th9_19.rpy" + ReplaySlots.OwnedSuffix,
                      "{\"slot\": 19, \"had_backup\": true, \"pid\": @dead}");

        script.Write(Step, "replay/th9_11.rpy", "SCAN-PUT-25");
        script.Marker(Step, "replay/th9_11.rpy" + ReplaySlots.OwnedSuffix, "[7, 22, 3]");

        script.Write(Step, "replay/th9_13.rpy", "SCAN-PUT-11");
        script.Marker(Step, "replay/th9_13.rpy" + ReplaySlots.OwnedSuffix,
                      "{\"slot\": 13, \"had_backup\": 0, \"pid\": @dead}");

        script.Write(Step, "replay/th9_21.rpy", "SCAN-PUT-13");
        script.Marker(Step, "replay/th9_21.rpy" + ReplaySlots.OwnedSuffix,
                      "{\"slot\": 21, \"pid\": @dead}");

        script.Write(Step, "replay/th9_08.rpy", "SCAN-PUT-21");
        script.Write(Step, "replay/th9_08.rpy" + ReplaySlots.BackupSuffix, "ORIGINAL-08");
        script.Marker(Step, "replay/th9_08.rpy" + ReplaySlots.OwnedSuffix,
                      "{\"slot\": 8, \"had_backup\": true, \"pid\": @selfstr}");

        script.Write(Step, "replay/th9_25.rpy" + ReplaySlots.BackupSuffix, "ORIGINAL-25");

        script.Write(Step, "replay/th9_18.rpy.tickscan.bak2", "NOT-A-BACKUP");
        script.Write(Step, "replay/notes.txt", "PLAIN");

        script.Restore(Step);
        script.Evidence(Step);

        foreach (var (playing, path, demo) in new[]
                 {
                     ("1", "@none", "-"), ("0", "@none", "-"), ("1", "@none", "1"),
                     ("1", "@none", "0"), ("1", "./replay/th9_07.rpy", "-"),
                     ("1", "replay" + "\\" + "th9_22.rpy", "-"),
                     ("1", "./replay/th9_03.rpy", "-"), ("1", "demo/demorpy0.rpy", "-"),
                     ("1", "", "-"),
                 })
        {
            Step("reason", playing, path.Length == 0 ? "@empty" : path, demo);
            string? why = ReplaySlots.LeftoverPlaybackReason(
                script.Saved, playing: playing == "1",
                replayPath: path == "@none" ? null : path,
                demo: demo == "-" ? null : demo == "1");
            script.Row("out", why ?? "-");
        }
        script.Evidence(Step);
        Step("reason", "1", "@none", "-");
        script.Row("out", ReplaySlots.LeftoverPlaybackReason(script.Saved, playing: true) ?? "-");

        Step("recover");
        var game = new FakeGame();
        var drv = new MenuDriver(game, game, log: MenuDriver.Quiet);
        script.Row("out", ReplaySlots.RecoverLeftoverPlayback(drv, game, null, Log) ?? "-");

        script.Write(Step, "store/pick-A.rpy", "SRC-A");
        script.Occupy(Step, "store/pick-A.rpy", "12", "ok", "4242");

        script.Write(Step, "replay/th9_09.rpy", "ORIGINAL-09");
        script.Write(Step, "store/pick-B.rpy", "SRC-B");
        script.Occupy(Step, "store/pick-B.rpy", "9", "ok", "-");

        script.Occupy(Step, "replay/th9_09.rpy", "21", "ok", "-");

        script.Write(Step, "store/pick-C.rpy", "SRC-C");
        script.Occupy(Step, "store/pick-C.rpy", "18", "abandon", "7");
        script.Write(Step, "replay/th9_16.rpy", "ORIGINAL-16");
        script.Write(Step, "store/pick-F.rpy", "SRC-F");
        script.Occupy(Step, "store/pick-F.rpy", "16", "abandon", "-");
        script.Restore(Step);
        script.Evidence(Step);

        script.Write(Step, "store/pick-D.rpy", "SRC-D");
        script.Occupy(Step, "store/pick-D.rpy", "20", "throw", "-", "台本: 途中で落ちる");

        script.Write(Step, "replay/th9_04.rpy", "ORIGINAL-04");
        script.Write(Step, "replay/th9_04.rpy" + ReplaySlots.BackupSuffix, "STALE-04");
        script.Write(Step, "store/pick-E.rpy", "SRC-E");
        script.Occupy(Step, "store/pick-E.rpy", "4", "ok", "-");

        writer.Write("end\t" + Num(step) + "\t" + Num(script.Listed) + "\n");
        closed = true;
        try { Directory.Delete(root, recursive: true); }
        catch (Exception exc) when (exc is IOException or UnauthorizedAccessException) { }
        return 0;
    }

    private static string Num(long value) => value.ToString(CultureInfo.InvariantCulture);

    internal static string Esc(string text) =>
        text.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");

    private sealed class Script(TextWriter writer, string root, string replayDir)
    {
        public Action<string> Log { get; set; } = _ => { };

        public IReadOnlyList<LeftoverEvidence> Saved { get; private set; } = [];

        public int Listed { get; private set; }

        public void Row(params string[] cells) =>
            writer.Write(string.Join("\t", cells.Select(Esc)) + "\n");

        public string At(string rel) =>
            Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));

        public string Rel(string full) =>
            Path.GetRelativePath(root, full).Replace(Path.DirectorySeparatorChar, '/');

        public long? Pid(string token) => token switch
        {
            "@self" => Environment.ProcessId,
            "@dead" => DeadPid,
            "@none" => null,
            _ => long.Parse(token, CultureInfo.InvariantCulture),
        };

        public void Write(Action<string, string[]> step, string rel, string body)
        {
            step("write", [rel, Hex(Encoding.UTF8.GetBytes(body))]);
            Parent(rel);
            File.WriteAllBytes(At(rel), Encoding.UTF8.GetBytes(body));
            List();
        }

        public void Marker(Action<string, string[]> step, string rel, string template)
        {
            step("marker", [rel, template]);
            Parent(rel);
            File.WriteAllText(At(rel), Fill(template), new UTF8Encoding(false));
            List();
        }

        private void Parent(string rel)
        {
            string? dir = Path.GetDirectoryName(At(rel));
            if (dir is not null) Directory.CreateDirectory(dir);
        }

        public void Restore(Action<string, string[]> step)
        {
            step("restore", []);
            var got = ReplaySlots.RestoreLeftoverBackups(replayDir, Log);
            Row("counts", got.FromBackup.ToString(CultureInfo.InvariantCulture),
                got.FromMarker.ToString(CultureInfo.InvariantCulture),
                got.SkippedAlive.ToString(CultureInfo.InvariantCulture),
                got.SkippedUnknown.ToString(CultureInfo.InvariantCulture),
                got.Restored.ToString(CultureInfo.InvariantCulture));
            List();
        }

        public void Evidence(Action<string, string[]> step)
        {
            step("evidence", []);
            Saved = ReplaySlots.TakeLeftoverEvidence();
            foreach (var e in Saved) Row("evidence", e.SlotFile, e.Why);
            Row("out", ReplaySlots.TakeLeftoverEvidence().Count
                       .ToString(CultureInfo.InvariantCulture));
        }

        public void Occupy(Action<string, string[]> step, string src, string slot, string mode,
                           string jobId, string extra = "-")
        {
            step("occupy", [src, slot, mode, jobId, extra]);
            string answer = "-";
            var occ = new SlotOccupation(replayDir, At(src),
                                         slot == "-" ? null : int.Parse(slot, CultureInfo.InvariantCulture),
                                         jobId == "-" ? null : long.Parse(jobId, CultureInfo.InvariantCulture),
                                         Log);
            try
            {
                occ.Enter();
                if (mode == "throw") throw new InvalidOperationException(extra);
            }
            catch (SlotOccupationError exc)
            {
                answer = "!" + exc.Message;
            }
            catch (InvalidOperationException exc)
            {
                answer = "!" + exc.Message;
            }
            finally
            {
                if (mode != "abandon") occ.Restore();
            }
            Row("out", Mask(answer));
            List();
        }

        public string Mask(string text) =>
            text.Replace(root, "@root", StringComparison.OrdinalIgnoreCase)
                .Replace("pid=" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
                         "pid=@self", StringComparison.Ordinal);

        private void List()
        {
            foreach (string full in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                                             .Select(Rel).Order(StringComparer.Ordinal))
            {
                byte[] raw = File.ReadAllBytes(At(full));
                Listed++;
                if (full.EndsWith(ReplaySlots.OwnedSuffix, StringComparison.Ordinal))
                {
                    Row("list", full, "txt", Blank(Encoding.UTF8.GetString(raw)));
                }
                else
                {
                    Row("list", full, "hex", Hex(raw));
                }
            }
            Row("list", "", "end", "");
        }

        private string Fill(string template)
        {
            string self = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
            return template.Replace("@selfstr", "\"" + self + "\"")
                           .Replace("@self", self)
                           .Replace("@dead", DeadPid.ToString(CultureInfo.InvariantCulture));
        }

        private string Blank(string text)
        {
            string slashed = text.Replace("\\\\", "/");
            string rootSlash = root.Replace(Path.DirectorySeparatorChar, '/');
            slashed = slashed.Replace(rootSlash, "@root", StringComparison.OrdinalIgnoreCase);
            string self = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
            slashed = slashed.Replace("\"pid\": \"" + self + "\"", "\"pid\": @selfstr",
                                      StringComparison.Ordinal);
            slashed = slashed.Replace("\"pid\": " + self, "\"pid\": @self", StringComparison.Ordinal);
            const string key = "\"started_at\": \"";
            const char quote = (char)0x22;
            int at = slashed.IndexOf(key, StringComparison.Ordinal);
            if (at >= 0)
            {
                int close = slashed.IndexOf(quote, at + key.Length);
                if (close > 0) slashed = slashed[..(at + key.Length)] + "@time" + slashed[close..];
            }
            return slashed;
        }

        private static string Hex(byte[] raw) => Convert.ToHexString(raw);
    }
}
