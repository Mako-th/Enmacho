using System.Globalization;
using System.Text;

namespace TH09.Shell.Data;

internal static class ReplayFilterDump
{
    public const string Flag = "--dump-replay-filter";

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

            using var db = TrackerDb.OpenMainDb();
            var all = ReplayListQuery.LoadAll(db);

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(Separator, "kind", "p1", "p2", "rows", "foreign"));

            var match = all.Where(r => r.Section == ReplaySection.Match).ToList();
            var p1s = match.Select(r => r.P1Character).OfType<int>().Distinct().Order().ToList();
            var p2s = match.Select(r => r.P2Character).OfType<int>().Distinct().Order().ToList();

            foreach (var c in p1s)
                Line(sb, "p1", c, null, all);
            foreach (var c in p2s)
                Line(sb, "p2", null, c, all);
            foreach (var a in p1s)
                foreach (var b in p2s)
                    Line(sb, "pair", a, b, all);

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

    private static void Line(StringBuilder sb, string kind, int? p1, int? p2,
                             List<ReplayListRow> all)
    {
        var rows = ReplayListQuery.Filter(all, new ReplayListFilter(
            Section: ReplaySection.Match, P1Character: p1, P2Character: p2));
        if (kind == "pair" && rows.Count == 0) return;
        var foreign = rows.Count(r => !r.IsOwn);
        sb.AppendLine(string.Join(Separator, kind,
            p1 is null ? "" : ReplayLabels.Character(p1),
            p2 is null ? "" : ReplayLabels.Character(p2),
            rows.Count.ToString(CultureInfo.InvariantCulture),
            foreign.ToString(CultureInfo.InvariantCulture)));
    }
}
