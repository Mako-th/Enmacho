using System.Globalization;
using System.Text;

namespace TH09.Shell.Data;

internal static class CharOrderDump
{
    public const string Flag = "--dump-char-order";

    private const char Separator = '\t';

    public static int Run(string dbPath)
    {
        using var stdout = new StreamWriter(Console.OpenStandardOutput(),
                                            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        try
        {
            TrackerDb.MainDbPath = dbPath;
            if (!TrackerDb.MainDbExists)
            {
                Console.Error.WriteLine("本体 DB が見つかりません: " + dbPath);
                return 2;
            }

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(Separator, "kind", "where", "dir", "rows", "known", "names"));

            sb.AppendLine(string.Join(Separator, "order", "DisplayOrder", "-",
                ReplayLabels.DisplayOrder.Length.ToString(CultureInfo.InvariantCulture),
                ReplayLabels.DisplayOrder.Length.ToString(CultureInfo.InvariantCulture),
                string.Join("/", ReplayLabels.DisplayOrder.Select(i => ReplayLabels.Characters[i]))));

            using var db = TrackerDb.OpenMainDb();

            var replays = ReplayListQuery.LoadAll(db);
            foreach (var (key, name) in new[]
                     {
                         (ReplaySortKey.P1Character, "1P"), (ReplaySortKey.P2Character, "2P"),
                     })
            {
                foreach (var desc in new[] { false, true })
                {
                    var rows = new List<ReplayListRow>(replays);
                    ReplayListQuery.Sort(rows, key, desc);
                    var ids = rows.Select(r => key == ReplaySortKey.P1Character
                                               ? r.P1Character : r.P2Character);
                    Line(sb, "replay", name, desc, rows.Count, ids);
                }
            }

            var history = HistoryQuery.LoadAll(db);
            foreach (var (key, name) in new[]
                     {
                         (HistorySortKey.P1Character, "1P"), (HistorySortKey.P2Character, "2P"),
                     })
            {
                foreach (var desc in new[] { false, true })
                {
                    var rows = new List<HistoryRow>(history);
                    HistoryQuery.Sort(rows, key, desc);
                    var ids = rows.Select(r => key == HistorySortKey.P1Character
                                               ? r.P1Character
                                               : r.IsStory ? null : r.P2Character);
                    Line(sb, "history", name, desc, rows.Count, ids);
                }
            }

            stdout.Write(sb.ToString());
            stdout.Flush();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
    }

    private static void Line(StringBuilder sb, string kind, string where, bool desc,
                             int rows, IEnumerable<int?> ids)
    {
        var names = new List<string>();
        var known = 0;
        foreach (var id in ids)
        {
            if (id is not null && ReplayLabels.DisplayRank(id) is not null) known++;
            var name = ReplayLabels.Character(id);
            if (names.Count == 0 || !string.Equals(names[^1], name, StringComparison.Ordinal))
                names.Add(name);
        }
        sb.AppendLine(string.Join(Separator, kind, where, desc ? "desc" : "asc",
            rows.ToString(CultureInfo.InvariantCulture),
            known.ToString(CultureInfo.InvariantCulture),
            string.Join("/", names)));
    }
}
