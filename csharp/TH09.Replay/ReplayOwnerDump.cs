using System.Globalization;
using System.Text;

namespace TH09.Replay;

public static class ReplayOwnerDump
{
    public const string Flag = "--dump-replay-owner";

    private const string Absent = "-";
    private const char Present = '=';

    public static int Run(string scriptPath, TextWriter writer)
    {
        if (!File.Exists(scriptPath))
        {
            Console.Error.WriteLine("台本が見つかりません: " + scriptPath);
            return 1;
        }
        var rows = new List<string[]>();
        foreach (var raw in File.ReadAllLines(scriptPath, Encoding.UTF8))
        {
            var line = raw.TrimEnd('\r', '\n');
            if (line.Length == 0 || line[0] == '#') continue;
            var cells = line.Split('\t');
            for (int i = 0; i < cells.Length; i++) cells[i] = Unesc(cells[i]);
            rows.Add(cells);
        }
        if (rows.Count == 0)
        {
            Console.Error.WriteLine("台本が 0 行です。測れていないので中止します。");
            return 1;
        }

        var order = new List<string>();
        var cfgs = new Dictionary<string, Combo>(StringComparer.Ordinal);
        int cases = 0, results = 0;

        for (int i = 0; i < ReplayOwner.DefaultOwnNames.Length; i++)
            Row(writer, "defname", Num(i), Present + ReplayOwner.DefaultOwnNames[i]);

        foreach (var cells in rows)
        {
            switch (cells[0])
            {
                case "cfg":
                    {
                        var combo = new Combo(
                            PlayerName: Opt(cells[2]),
                            OwnNames: cells[3] == "1" ? new List<string>() : null,
                            IgnoreCase: Flag3(cells[4]),
                            Partial: Flag3(cells[5]));
                        cfgs[cells[1]] = combo;
                        order.Add(cells[1]);
                        break;
                    }
                case "ownname":
                    {
                        var combo = cfgs[cells[1]];
                        if (combo.OwnNames is null)
                            throw new InvalidOperationException("own の鍵が無い組に ownname が来た: " + cells[1]);
                        combo.OwnNames.Add(Opt(cells[2]) ?? "");
                        break;
                    }
                case "case":
                    cases++;
                    break;
                default:
                    throw new InvalidOperationException("知らない行の種類: " + cells[0]);
            }
        }

        var resolved = new Dictionary<string, (IReadOnlyList<string> Names, bool IgnoreCase, bool Partial)>(
            StringComparer.Ordinal);
        foreach (var key in order)
        {
            var combo = cfgs[key];
            var names = ReplayOwner.OwnNames(combo.PlayerName, combo.OwnNames);
            var (ignoreCase, partial) = ReplayOwner.OwnMatchOptions(combo.IgnoreCase, combo.Partial);
            resolved[key] = (names, ignoreCase, partial);
            Row(writer, "namecount", key, Num(names.Count));
            for (int i = 0; i < names.Count; i++) Row(writer, "names", key, Num(i), Present + names[i]);
            Row(writer, "opts", key, ignoreCase ? "1" : "0", partial ? "1" : "0");
        }

        foreach (var cells in rows)
        {
            if (cells[0] != "case") continue;
            var (names, ignoreCase, partial) = resolved[cells[1]];
            var decoded = new ReplayResult
            {
                Status = "decoded",
                P1Name = Opt(cells[3]),
                P2Name = Opt(cells[4]),
                Name = Opt(cells[5]),
            };
            var path = Opt(cells[6]);
            int? side = ReplayOwner.OwnerSide(decoded, path, names, ignoreCase, partial);
            string source = ReplayOwner.ReplaySource(path, decoded);
            Row(writer, "res", cells[1], cells[2], side is null ? Absent : Num(side.Value), source);
            results++;
        }
        Row(writer, "end", Num(order.Count), Num(cases), Num(results));
        writer.Flush();
        return 0;
    }

    private sealed record Combo(string? PlayerName, List<string>? OwnNames, bool? IgnoreCase, bool? Partial);

    private static string? Opt(string cell) =>
        cell == Absent ? null : cell.Length > 0 && cell[0] == Present ? cell[1..] : cell;

    private static bool? Flag3(string cell) => cell == Absent ? null : cell == "1";

    private static string Num(int v) => v.ToString(CultureInfo.InvariantCulture);

    private static void Row(TextWriter writer, params string[] cells) =>
        writer.Write(string.Join("\t", cells.Select(Esc)) + "\n");

    internal static string Esc(string text) =>
        text.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");

    internal static string Unesc(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                char c = text[i + 1];
                if (c == '\\') { sb.Append('\\'); i++; continue; }
                if (c == 'n') { sb.Append('\n'); i++; continue; }
                if (c == 'r') { sb.Append('\r'); i++; continue; }
                if (c == 't') { sb.Append('\t'); i++; continue; }
            }
            sb.Append(text[i]);
        }
        return sb.ToString();
    }
}
