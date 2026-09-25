using System.Globalization;

namespace TH09.Record;

public static class PlayerAliasesDump
{
    public const string ListFlag = "--player-aliases-list";

    public const string WriteFlag = "--player-aliases-write";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;


    public static int List(TextWriter w, string dbPath)
    {
        ArgumentNullException.ThrowIfNull(w);
        using var db = RecordDb.OpenReadOnly(dbPath);
        if (db is null)
        {
            Console.Error.WriteLine("本体 DB を読み取り専用で開けません: " + dbPath);
            return 1;
        }
        var conn = db.Connection;
        var data = PlayerAliasCandidates.Collect(conn);
        var aliasMap = PlayerIdentity.AliasMap(conn);
        var people = PlayerIdentity.Players(conn).ToDictionary(p => p.PlayerId);

        Row(w, "fact", "db", Path.GetFullPath(dbPath));
        Row(w, "fact", "names", Num(data.Count));
        foreach (var name in data.Keys.OrderByDescending(k => data[k].N)
                                       .ThenBy(k => k, StringComparer.Ordinal))
        {
            var rec = data[name];
            var hasPid = aliasMap.TryGetValue(name, out var pid);
            var isSelf = hasPid && people.TryGetValue(pid, out var person) && person.IsSelf;
            Row(w, "name", name, Num(rec.N), rec.First, rec.Last,
                hasPid ? Num(pid) : "", isSelf ? "1" : "0");
        }

        var bound = aliasMap.Values.ToHashSet();
        var empty = people.Values.Where(p => !bound.Contains(p.PlayerId))
                          .OrderBy(p => p.PlayerId).ToList();
        Row(w, "fact", "empty-players", Num(empty.Count));
        foreach (var p in empty) Row(w, "empty", Num(p.PlayerId), p.DisplayName);

        var selves = people.Values.Where(p => p.IsSelf).OrderBy(p => p.PlayerId).ToList();
        Row(w, "fact", "self-players", Num(selves.Count));
        foreach (var p in selves) Row(w, "self", Num(p.PlayerId), p.DisplayName);

        var cand = PlayerAliasCandidates.Hints(data.Keys, aliasMap);
        Row(w, "fact", "candidates", Num(cand.Count));
        foreach (var (a, b, why) in cand)
            Row(w, "cand", a, b, why, PlayerAliasCandidates.OverlapText(data[a], data[b]));
        return 0;
    }


    public static int Write(TextWriter w, string dbPath, string scriptPath)
    {
        ArgumentNullException.ThrowIfNull(w);
        if (RealDbGuard.InMainDbDir(dbPath))
        {
            Console.Error.WriteLine(
                "★本物の本体 DB のフォルダは受け付けません（合成の DB を渡してください）: " + dbPath);
            return 3;
        }
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

        Row(w, "fact", "db", Path.GetFullPath(dbPath));
        Row(w, "fact", "real_main_db", Paths.Default.MainDb);

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
                    case "seed":
                    {
                        var check = cells.Length > 1 && cells[1] == "1";
                        var data = PlayerAliasCandidates.Collect(conn);
                        var alias = PlayerIdentity.AliasMap(conn);
                        var todo = data.Keys.Where(k => !alias.ContainsKey(k))
                                       .OrderByDescending(k => data[k].N)
                                       .ToList();
                        Row(w, "seed-plan", Num(n), "names=" + Num(data.Count),
                            "registered=" + Num(data.Count - todo.Count), "todo=" + Num(todo.Count));
                        if (!check)
                        {
                            foreach (var name in todo)
                            {
                                var rec = data[name];
                                var pid = PlayerIdentity.AddPlayer(conn, name);
                                PlayerIdentity.SetPlayerAlias(conn, name, pid,
                                    source: string.Join('+', rec.Sources.OrderBy(x => x, StringComparer.Ordinal)),
                                    firstSeenAt: rec.First.Length == 0 ? null : rec.First);
                            }
                            Row(w, "seed-result", Num(n), "added=" + Num(todo.Count));
                        }
                        break;
                    }
                    case "merge":
                    {
                        var name = cells[1];
                        var into = long.Parse(cells[2], NumberStyles.Integer, Inv);
                        var check = cells.Length > 3 && cells[3] == "1";
                        var before = PlayerIdentity.PlayerIdForName(conn, name);
                        if (before is null)
                        {
                            Row(w, "merge-error", Num(n), "not_registered");
                            break;
                        }
                        var known = PlayerIdentity.Players(conn).Select(p => p.PlayerId).ToHashSet();
                        if (!known.Contains(into))
                        {
                            Row(w, "merge-error", Num(n), "unknown_player");
                            break;
                        }
                        if (before.Value == into)
                        {
                            Row(w, "merge-noop", Num(n), Num(before.Value));
                            break;
                        }
                        if (check)
                        {
                            Row(w, "merge-check", Num(n), Num(before.Value), Num(into));
                            break;
                        }
                        var (b, a) = PlayerIdentity.MergeAlias(conn, name, into);
                        Row(w, "merge-result", Num(n), "before=" + Num(b), "after=" + Num(a));
                        break;
                    }
                    case "self":
                    {
                        var pid = long.Parse(cells[1], NumberStyles.Integer, Inv);
                        var on = cells.Length > 2 && cells[2] == "1";
                        var check = cells.Length > 3 && cells[3] == "1";
                        var known = PlayerIdentity.Players(conn).Select(p => p.PlayerId).ToHashSet();
                        if (!known.Contains(pid))
                        {
                            Row(w, "self-error", Num(n), "unknown_player");
                            break;
                        }
                        if (check)
                        {
                            Row(w, "self-check", Num(n), Num(pid), on ? "1" : "0");
                            break;
                        }
                        var ok = PlayerIdentity.SetSelf(conn, pid, on);
                        Row(w, "self-result", Num(n), Num(pid), on ? "1" : "0", ok ? "1" : "0");
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

    private static string Num(long value) => value.ToString(Inv);

    private static void Row(TextWriter w, params string[] cells) =>
        w.Write(string.Join('\t', cells) + "\n");
}
