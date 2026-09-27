using System.Globalization;
using Microsoft.Data.Sqlite;

namespace TH09.Record;

public static class SessionDeleteDump
{
    public const string Flag = "--session-delete";

    public const string NoLayer0 = "-";

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

        foreach (var guard in Guards(dbPath, lines))
        {
            Console.Error.WriteLine(guard);
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

        using var db = RecordDb.OpenReadWrite(dbPath);
        var conn = db.Connection;
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
                    case "delete":
                        {
                            var layer0 = cells[1] == NoLayer0 ? null : cells[1];
                            var ids = ParseIds(cells.Length > 2 ? cells[2] : "");
                            var logs = new List<string>();
                            var r = SessionDelete.Run(conn, ids, layer0, logs.Add);
                            foreach (var line in logs) Row(w, "log", Num(n), line);
                            Row(w, "result", Num(n),
                                "deleted=" + Join(r.Deleted),
                                "layer0_rows=" + Num(r.Layer0Rows),
                                "floor=" + Num(r.Floor),
                                "seeded=" + (r.Seeded ? "1" : "0"),
                                "tombstoned=" + Join(r.Tombstoned),
                                "without_tombstone=" + Join(r.WithoutTombstone),
                                "running=" + Join(r.Running));
                            break;
                        }
                    case "nextid":
                        Row(w, "nextid", Num(n), Num(Repository.NextSessionId(conn)));
                        break;
                    case "floor":
                        {
                            var warn = new List<string>();
                            var layer0 = cells[1] == NoLayer0 ? null : cells[1];
                            var value = Repository.Layer0SessionFloor(layer0, warn.Add);
                            foreach (var line in warn) Row(w, "log", Num(n), line);
                            Row(w, "floor", Num(n), Num(value));
                            break;
                        }
                    case "reset":
                        {
                            var layer0 = cells[1] == NoLayer0 ? null : cells[1];
                            var all = cells.Length > 2 && cells[2] == "all";
                            var counts = ScanResultsClear.Count(conn, all);
                            Row(w, "counts", Num(n), "session_ids=" + Join(counts.SessionIds),
                                "all=" + (all ? "1" : "0"), "job_rows=" + Num(counts.JobRows),
                                "item_rows=" + Num(counts.ItemRows),
                                "replay_rows=" + Num(counts.ReplayRows),
                                "replay_path_rows=" + Num(counts.ReplayPathRows));
                            var rr = ScanResultsClear.Run(conn, counts, layer0);
                            Row(w, "result", Num(n), "deleted=" + Join(rr.Deleted),
                                "layer0_rows=" + Num(rr.Layer0Rows));
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

    private static List<string> Guards(string dbPath, List<string[]> lines)
    {
        var bad = new List<string>();
        if (RealDbGuard.InMainDbDir(dbPath))
            bad.Add("★本物の本体 DB のフォルダは受け付けません（合成の DB を渡してください）: " + dbPath);
        foreach (var cells in lines)
        {
            if (cells.Length < 2) continue;
            if (cells[0] is not ("delete" or "floor" or "reset")) continue;
            if (cells[1] == NoLayer0) continue;
            if (RealDbGuard.InLayer0Dir(cells[1]))
                bad.Add("★本物の Layer 0 のフォルダは受け付けません（合成の Layer 0 を渡してください）: "
                        + cells[1]);
        }
        return bad;
    }

    private static List<long> ParseIds(string text)
    {
        var ids = new List<long>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
            ids.Add(long.Parse(part.Trim(), NumberStyles.Integer, Inv));
        return ids;
    }

    private static string Join(IReadOnlyList<long> values) =>
        values.Count == 0 ? "" : string.Join(",", values.Select(Num));

    private static string Num(long value) => value.ToString(Inv);

    private static void Row(TextWriter w, params string[] cells) =>
        w.Write(string.Join("\t", cells) + "\n");
}
