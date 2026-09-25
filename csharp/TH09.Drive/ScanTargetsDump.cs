using System.Globalization;
using System.Runtime.Versioning;
using TH09.Record;

using IOPath = System.IO.Path;

namespace TH09.Drive;

[SupportedOSPlatform("windows")]
internal static class ScanTargetsDump
{
    public const string Flag = "--dump-scan-targets";

    public const int DumpSlot = 17;

    private static readonly double[] FormatSamples =
        [242.5, 243.5, 0.25, 0.35, 8.45, 8.55, 2.675, 30527.5, 647.5, 0.05, 8.4798611111111111];

    public static int Run(TextWriter w, string[] args)
    {
        var paths = Paths.Default;
        var mainGiven = args.Length >= 1 && args[0].Length > 0;
        var layer0Given = args.Length >= 2 && args[1].Length > 0;
        var dbPath = mainGiven ? args[0] : paths.MainDb;
        var layer0Path = layer0Given ? args[1] : paths.Layer0Db;

        using var db = RecordDb.OpenReadOnly(dbPath);
        if (db is null)
        {
            Console.Error.WriteLine("本体 DB を読み取り専用で開けません: " + dbPath);
            return 3;
        }
        var conn = db.Connection;

        Row(w, "fact", "main_db", dbPath);
        Row(w, "fact", "main_db_given", mainGiven ? "1" : "0");
        Row(w, "fact", "main_db_readonly", db.ReadOnly ? "1" : "0");
        Row(w, "fact", "layer0_db", layer0Path);
        Row(w, "fact", "layer0_given", layer0Given ? "1" : "0");
        Row(w, "fact", "minrec_ran", layer0Given ? "1" : "0");
        Row(w, "fact", "slot", Num(DumpSlot));
        Row(w, "fact", "decoded_status", ScanTargets.DecodedStatus);
        Row(w, "fact", "captured_status", ScanTargets.CapturedStatus);
        Row(w, "fact", "sec_overhead", ScanTargets.SecOverhead.ToString("R", Inv));
        Row(w, "fact", "sec_per_stage", ScanTargets.SecPerStage.ToString("R", Inv));
        Row(w, "fact", "default_show", Num(ScanTargets.DefaultShow));

        foreach (double v in FormatSamples)
        {
            Row(w, "fmt", v.ToString("R", Inv), ScanTargets.F(v, 0), ScanTargets.F(v, 1));
        }

        var all = ScanTargets.SelectTargets(conn, new ScanFilter(SkipDone: false));
        var replayDir = FirstSlotDir(all);
        var dir = replayDir ?? FirstDir(all);
        var parent = dir is null ? null : IOPath.GetDirectoryName(dir);
        Row(w, "fact", "replay_dir", replayDir ?? "");
        Row(w, "fact", "script_dir", dir ?? "");
        Row(w, "fact", "script_dir_parent", parent ?? "");
        Row(w, "fact", "corpus", Num(all.Count));

        var combos = Script(dir, parent, layer0Given, layer0Path);
        var step = 0;
        var selRows = 0;
        var outRows = 0;
        foreach (var c in combos)
        {
            step++;
            Row(w, ["combo", Num(step), c.Name, c.Mode is long m ? Num(m) : "-",
                    c.Own ? "1" : "0", Num(c.MinStages), c.Chars ?? "-", c.Order,
                    c.NoRecurse ? "1" : "0", c.Rescan ? "1" : "0", c.HideDone ? "1" : "0",
                    Num(c.Limit), c.Max is int x ? Num(x) : "-",
                    c.MinRecordVersion is int v ? Num(v) : "-", c.Layer0 ?? "-",
                    Num(c.Dirs.Count)]);
            foreach (var d in c.Dirs) Row(w, "dir", d);

            var shared = new ScanFilter(
                Dirs: c.Dirs, Recurse: !c.NoRecurse, Modes: c.Mode is long cm ? [cm] : null, Own: c.Own,
                MinStages: c.MinStages, Order: c.Order,
                Chars: ScanTargets.CharsArg(c.Chars),
                MinRecordVersion: c.MinRecordVersion, Layer0Path: c.Layer0);

            var batch = ScanTargets.ForBatch(shared, c.Rescan, c.Max);
            var dry = new StringWriter();
            var picked = ScanTargets.SelectTargets(conn, batch, s => dry.Write(s + "\n"));
            selRows += WriteSel(w, "batch", picked);
            ScanTargets.WriteDryRun(dry, picked, batch, c.Rescan, c.Limit, DumpSlot, replayDir);
            outRows += WriteOut(w, "dry", dry);

            var listed = ScanTargets.ForList(shared, c.Rescan, c.HideDone, c.Limit);
            var list = new StringWriter();
            var shown = ScanTargets.SelectTargets(conn, listed, s => list.Write(s + "\n"));
            selRows += WriteSel(w, "list", shown);
            ScanTargets.WriteList(list, shown, replayDir);
            outRows += WriteOut(w, "list", list);
        }
        Row(w, "end", Num(step), Num(selRows), Num(outRows));
        return 0;
    }

    private sealed record Combo(string Name, IReadOnlyList<string> Dirs, bool NoRecurse = false,
                                long? Mode = null, bool Own = false, int MinStages = 0,
                                string? Chars = null, string Order = ScanTargets.OrderId,
                                bool Rescan = false, bool HideDone = false,
                                int Limit = ScanTargets.DefaultShow, int? Max = null,
                                int? MinRecordVersion = null, string? Layer0 = null);

    private static List<Combo> Script(string? dir, string? parent, bool layer0Given,
                                      string layer0Path)
    {
        string[] dirs = dir is null ? [] : [dir];
        string[] parents = parent is null ? [] : [parent];
        var list = new List<Combo>
        {
            new("plain-rescan", [], Rescan: true),
            new("plain", []),
            new("hide-done", [], HideDone: true),
            new("mode0", [], Mode: 0, Rescan: true),
            new("mode2", [], Mode: 2, Rescan: true, Limit: 4),
            new("own", [], Own: true, Rescan: true, Limit: 6),
            new("stages9", [], MinStages: 9, Rescan: true, Limit: 2),
            new("chars", [], Chars: "5,11", Rescan: true, Limit: 8),
            new("chars-one", [], Chars: "13", Rescan: true, Limit: 3),
            new("order-size", [], Order: ScanTargets.OrderSize, Rescan: true, Limit: 7),
            new("order-mtime", [], Order: ScanTargets.OrderMtime, Rescan: true, Limit: 7),
            new("limit-3", [], Rescan: true, Limit: 3),
            new("limit-500", [], Rescan: true, Limit: 500),
            new("limit-0", [], Rescan: true, Limit: 0),
            new("max-13", [], Rescan: true, Limit: 5, Max: 13),
            new("max-13-limit-500", [], Rescan: true, Limit: 500, Max: 13),
            new("max-with-order", [], Order: ScanTargets.OrderSize, Rescan: true,
                Limit: 4, Max: 9),
        };
        if (dirs.Length > 0)
        {
            list.Add(new Combo("dir", dirs, Rescan: true, Limit: 6));
            list.Add(new Combo("dir-norecurse", dirs, NoRecurse: true, Rescan: true, Limit: 6));
        }
        if (parents.Length > 0)
        {
            list.Add(new Combo("parent", parents, Rescan: true, Limit: 6));
            list.Add(new Combo("parent-norecurse", parents, NoRecurse: true, Rescan: true,
                               Limit: 6));
        }
        list.Add(new Combo("minrec-missing-layer0", [], Rescan: true, Limit: 4,
                           MinRecordVersion: 12, Layer0: layer0Path + ".no-such-file"));
        if (layer0Given)
        {
            foreach (var v in new[] { 1, 12, 99 })
            {
                list.Add(new Combo("minrec-" + Num(v), [], Rescan: true, Limit: 500,
                                   MinRecordVersion: v, Layer0: layer0Path));
            }
        }
        return list;
    }


    private static int WriteSel(TextWriter w, string which, IReadOnlyList<ScanTarget> targets)
    {
        foreach (var t in targets)
        {
            Row(w, ["sel", which, Num(t.ReplayId), Num(t.Stages.Count),
                    t.Done ? "1" : "0", t.Path]);
        }
        Row(w, "selend", which, Num(targets.Count));
        return targets.Count;
    }

    private static int WriteOut(TextWriter w, string kind, StringWriter body)
    {
        var text = body.ToString().Replace("\r\n", "\n");
        string[] lines = text.Length == 0 ? [] : text.TrimEnd('\n').Split('\n');
        foreach (var line in lines) Row(w, "out", kind, line);
        Row(w, "outend", kind, Num(lines.Length));
        return lines.Length;
    }

    private static string? FirstSlotDir(IReadOnlyList<ScanTarget> targets)
    {
        foreach (var t in targets)
        {
            if (ReplaySlots.SlotOf(t.Path) is null) continue;
            var d = IOPath.GetDirectoryName(t.Path);
            if (d is not null) return d;
        }
        return null;
    }

    private static string? FirstDir(IReadOnlyList<ScanTarget> targets) =>
        targets.Count == 0 ? null : IOPath.GetDirectoryName(targets[0].Path);

    private static CultureInfo Inv => CultureInfo.InvariantCulture;

    private static string Num(long v) => v.ToString(Inv);

    private static void Row(TextWriter w, params string[] cells) =>
        w.Write(string.Join("\t", cells.Select(SlotDump.Esc)) + "\n");
}
