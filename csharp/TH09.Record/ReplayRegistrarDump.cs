using System.Globalization;

namespace TH09.Record;

public static class ReplayRegistrarDump
{
    public const string Flag = "--replay-register";

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

        if (RealDbGuard.InMainDbDir(dbPath))
        {
            Console.Error.WriteLine(
                "★本物の本体 DB のフォルダは受け付けません（合成の DB を渡してください）: " + dbPath);
            return 3;
        }
        foreach (var cells in lines)
        {
            if (cells.Length < 2 || cells[0] != "signal") continue;
            if (!ScanLedger.IsRealLockName(cells[1])) continue;
            Console.Error.WriteLine(
                "★検査用の名前しか受け付けない（本物のロックの名前を読むだけでも、"
                + "取り違えて握ったときに動いている監視・走査・watcher を止めてしまう）: " + cells[1]);
            return 3;
        }

        string? fixedNow = null;
        foreach (var cells in lines)
        {
            if (cells.Length >= 2 && cells[0] == "now") fixedNow = cells[1];
        }

        Row(w, "fact", "db", Path.GetFullPath(dbPath));
        Row(w, "fact", "real_main_db", Paths.Default.MainDb);
        Row(w, "fact", "clock", fixedNow ?? "(default)");
        Row(w, "fact", "mtime_link_method", ReplayRegistrar.MtimeLinkMethod);
        Row(w, "fact", "scan_link_method", ScanLink.Method);

        using var registrar = ReplayRegistrar.Open(
            dbPath, fixedNow is null ? null : () => fixedNow);
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
                    case "now":
                        break;
                    case "register":
                    {
                        var r = registrar.Register(cells[1], Facts(cells));
                        Row(w, "register", Num(n),
                            "replay_id=" + Num(r.ReplayId),
                            "replay_inserted=" + Flag01(r.ReplayInserted),
                            "path_inserted=" + Flag01(r.PathInserted),
                            "source=" + r.Source,
                            "owner_side=" + (r.OwnerSide is null ? Nil : Num(r.OwnerSide.Value)),
                            "is_own=" + (r.IsOwn is null ? Nil : Num(r.IsOwn.Value)),
                            "link=" + LinkText(r.Link));
                        break;
                    }
                    case "sweep":
                    {
                        var rows = registrar.SweepMissing(cells.Skip(1));
                        Row(w, "sweep", Num(n), "rows=" + Num(rows));
                        break;
                    }
                    case "signal":
                        Row(w, "signal", Num(n), cells[1],
                            Flag01(ScanLedger.IsSignalled(cells[1])));
                        break;
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

    private static ReplayFacts Facts(string[] c) => new(
        Status: c[2],
        Decoded: c[3] == "1",
        Source: c[4],
        OwnerSideAuto: Int(c, 5),
        Mode: Long(c, 6),
        Difficulty: Long(c, 7),
        PlayerName: Text(c, 8),
        ReplayDate: Text(c, 9),
        P1Char: Long(c, 10),
        P2Char: Long(c, 11),
        P1Name: Text(c, 12),
        P2Name: Text(c, 13),
        DecodedJson: Text(c, 14) ?? "");

    private static string LinkText(ReplayLinkResult? link) =>
        link is null
            ? "none"
            : string.Create(Inv, $"{link.SessionId}/{link.Confidence}/{link.Seconds}/{Flag01(link.Written)}");

    private static string? Text(string[] cells, int index) =>
        index < cells.Length && cells[index] != Nil ? cells[index] : null;

    private static long? Long(string[] cells, int index)
    {
        var t = Text(cells, index);
        return t is null ? null : long.Parse(t, Inv);
    }

    private static int? Int(string[] cells, int index)
    {
        var t = Text(cells, index);
        return t is null ? null : int.Parse(t, Inv);
    }

    private static string Flag01(bool value) => value ? "1" : "0";

    private static string Num(long value) => value.ToString(Inv);

    private static void Row(TextWriter w, params string[] cells) =>
        w.Write(string.Join("\t", cells) + "\n");
}
