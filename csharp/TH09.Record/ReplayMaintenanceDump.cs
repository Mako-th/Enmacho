using System.Globalization;

namespace TH09.Record;

public static class ReplayMaintenanceDump
{
    public const string Flag = "--replay-maintenance";

    public const string NoLayer0 = "-";

    public const string Nil = "~";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(TextWriter w, string dbPath, string scriptPath)
    {
        ArgumentNullException.ThrowIfNull(w);
        if (!File.Exists(scriptPath))
        {
            Console.Error.WriteLine("台本がありません: " + scriptPath);
            return 1;
        }
        var lines = new List<string[]>();
        foreach (var raw in File.ReadAllLines(scriptPath))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith('#')) continue;
            lines.Add(line.Split('\t'));
        }

        if (ReplayMaintenance.PointsAtRealData(dbPath, null))
        {
            Console.Error.WriteLine("★本物の本体 DB のフォルダは受け付けません（合成の DB を渡してください）: " + dbPath);
            return 3;
        }
        foreach (var cells in lines)
        {
            if (cells[0] != "delete" || cells.Length < 2 || cells[1] == NoLayer0) continue;
            if (!ReplayMaintenance.PointsAtRealData(dbPath, cells[1])) continue;
            Console.Error.WriteLine("★本物の Layer 0 のフォルダは受け付けません（合成の Layer 0 を渡してください）: "
                                    + cells[1]);
            return 3;
        }
        foreach (var cells in lines)
        {
            if (cells.Length < 2) continue;
            var cfg = cells[0] is "ignorecase" or "excludedwatch" ? cells[1] : null;
            if (cfg is null || !RealDbGuard.InConfigDir(cfg)) continue;
            Console.Error.WriteLine("★本物の config.json のフォルダは受け付けません（合成の設定を渡してください）: " + cfg);
            return 3;
        }
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine("本体 DB がありません: " + dbPath);
            return 1;
        }

        Row(w, "fact", "db", Path.GetFullPath(dbPath));
        Row(w, "fact", "real_main_db", Paths.Default.MainDb);
        Row(w, "fact", "real_layer0_db", Paths.Default.Layer0Db);

        var n = 0;
        foreach (var cells in lines)
        {
            n++;
            var verb = cells[0];
            Row(w, "step", Num(n), verb);
            try
            {
                switch (verb)
                {
                    case "own":
                    {
                        var (own, side) = ReplayOwn.Of(Long(cells, 1), Long(cells, 2));
                        Row(w, "own", Num(n), Show(Long(cells, 1)), Show(Long(cells, 2)),
                            "own=" + (own ? "1" : "0"), "side=" + Show(side));
                        break;
                    }
                    case "humanside":
                    {
                        var sides = Long(cells, 2) is long s ? (MatchSides?)(MatchSides)(int)s : null;
                        Row(w, "humanside", Num(n), Show(Long(cells, 1)), Show(Long(cells, 2)),
                            "side=" + Show(ReplayStages.HumanSideOf(Long(cells, 1), sides)));
                        break;
                    }
                    case "isvalid":
                        Row(w, "isvalid", Num(n), Show(Long(cells, 1)),
                            ReplayOwnership.IsValid(Long(cells, 1) is long iv ? (int)iv : null) ? "1" : "0");
                        break;
                    case "setown":
                    {
                        var r = ReplayOwnership.SetOverride(
                            dbPath, Ids(cells[1]), Long(cells, 2) is long v ? (int)v : null);
                        Result(w, n, r);
                        break;
                    }
                    case "markown":
                        Result(w, n, ReplayOwnership.MarkOwn(dbPath, Ids(cells[1])));
                        break;
                    case "describe":
                    {
                        var d = ReplayMaintenance.Describe(dbPath, Ids(cells[1]));
                        foreach (var r in d.Rows)
                            Row(w, "row", Num(n), "replay_id=" + Num(r.ReplayId), "sha=" + r.Sha256,
                                "paths=" + string.Join("|", r.Paths.Select(p => p.Path + (p.IsCurrent ? "+" : "-"))),
                                "scan_sessions=" + Num(r.ScanSessions), "guessed=" + Num(r.GuessedLinks));
                        Row(w, "describe", Num(n), "rows=" + Num(d.Rows.Count), "missing=" + Join(d.Missing),
                            "scan_sessions=" + Num(d.ScanSessions), "guessed=" + Num(d.GuessedLinks),
                            "path_rows=" + Num(d.PathRows));
                        Row(w, "line", Num(n), ReplayMaintenance.Line(d));
                        break;
                    }
                    case "delete":
                    {
                        var layer0 = cells[1] == NoLayer0 ? null : cells[1];
                        var logs = new List<string>();
                        var r = ReplayMaintenance.Delete(dbPath, layer0, Ids(cells[2]), logs.Add);
                        foreach (var line in logs) Row(w, "log", Num(n), line);
                        Row(w, "deleted", Num(n),
                            "requested=" + Join(r.Requested), "missing=" + Join(r.Missing),
                            "sessions=" + Join(r.ScanSessions.Deleted),
                            "layer0_rows=" + Num(r.ScanSessions.Layer0Rows),
                            "shared_kept=" + Join(r.SharedSessionsKept),
                            "links_detached=" + Num(r.GuessedLinksDetached),
                            "scan_items=" + Num(r.ScanItemRows),
                            "paths=" + Num(r.PathRows), "replays=" + Num(r.ReplayRows));
                        break;
                    }
                    case "foreign":
                    {
                        using var db = RecordDb.OpenReadOnly(dbPath)
                            ?? throw new FileNotFoundException("本体 DB を読み取り専用で開けません: " + dbPath);
                        var ids = Repository.ForeignSessionIds(db.Connection).OrderBy(x => x).ToList();
                        Row(w, "foreign", Num(n), Join(ids));
                        break;
                    }
                    case "underdir":
                        Row(w, "underdir", Num(n), Paths.IsUnderDir(cells[1], cells[2]) ? "1" : "0");
                        break;
                    case "ignorecase":
                    {
                        var cfg = cells[1];
                        if (!string.Equals(Path.GetFileName(cfg), "config.json", StringComparison.Ordinal))
                            throw new ArgumentException("config.json という名前のパスを渡してください: " + cfg);
                        var p = Paths.Resolve(Path.GetDirectoryName(Path.GetFullPath(cfg))!);
                        var first = SessionSide.IgnoreCaseFromConfig(p);
                        var second = "-";
                        if (cells.Length > 2)
                        {
                            File.WriteAllText(cfg, cells[2]);
                            second = SessionSide.IgnoreCaseFromConfig(p) ? "1" : "0";
                        }
                        Row(w, "ignorecase", Num(n), "config=" + p.ConfigPath, "first=" + (first ? "1" : "0"),
                            "second=" + second, "own_dirs=" + string.Join("|", p.OwnReplayDirs));
                        break;
                    }
                    case "excludedwatch":
                    {
                        var watch = ExcludedReplays.Watcher(cells[1]);
                        var a = watch().OrderBy(x => x, StringComparer.Ordinal).ToList();
                        var b = "-";
                        if (cells.Length > 2)
                        {
                            File.WriteAllText(cells[1], cells[2]);
                            b = string.Join(",", watch().OrderBy(x => x, StringComparer.Ordinal));
                        }
                        Row(w, "excludedwatch", Num(n), "first=" + string.Join(",", a), "second=" + b);
                        break;
                    }
                    case "sideof":
                    {
                        var names = cells[6] == Nil ? [] : cells[6].Split(';');
                        var (side, own, prov) = SessionSide.Of(
                            Long(cells, 1), Long(cells, 2), Text(cells, 3), Text(cells, 4), names,
                            Long(cells, 8), null, null, null, Text(cells, 7), cells[5] == "1");
                        Row(w, "sideof", Num(n), "side=" + Show(side), "own=" + (own ? "1" : "0"),
                            "prov=" + (prov ? "1" : "0"));
                        break;
                    }
                    default:
                        Console.Error.WriteLine("知らない命令です（" + n + " 行目）: " + verb);
                        return 1;
                }
            }
            catch (Exception exc)
            {
                Row(w, "raised", Num(n), exc.GetType().Name,
                    exc.Message.Replace("\n", "\\n", StringComparison.Ordinal));
            }
        }
        Row(w, "end", Num(n));
        return 0;
    }

    private static void Result(TextWriter w, int n, ReplayOwnershipResult r) =>
        Row(w, "result", Num(n), "requested=" + Join(r.Requested), "missing=" + Join(r.Missing),
            "changed=" + Num(r.Changed), "unchanged=" + Num(r.Unchanged), "without_side=" + Num(r.WithoutSide));

    private static List<long> Ids(string text)
    {
        var ids = new List<long>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
            ids.Add(long.Parse(part.Trim(), NumberStyles.Integer, Inv));
        return ids;
    }

    private static string? Text(string[] cells, int index) =>
        index < cells.Length && cells[index] != Nil ? cells[index] : null;

    private static long? Long(string[] cells, int index)
    {
        var t = Text(cells, index);
        return t is null ? null : long.Parse(t, NumberStyles.Integer, Inv);
    }

    private static string Show(long? v) => v is null ? Nil : Num(v.Value);

    private static string Join(IReadOnlyList<long> values) =>
        values.Count == 0 ? "" : string.Join(",", values.Select(Num));

    private static string Num(long value) => value.ToString(Inv);

    private static void Row(TextWriter w, params string[] cells) =>
        w.Write(string.Join("\t", cells) + "\n");
}
