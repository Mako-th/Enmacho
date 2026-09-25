using System.Globalization;
using System.Text;

namespace TH09.Shell.Data;

internal static class StatsRecordsDump
{
    public const string Flag = "--dump-stats-records";

    public static readonly string[] Kinds = ["state", "cols", "row", "note", "gate", "count"];

    private const char Sep = '\t';

    public static readonly (string Key, bool Foreign, bool Filter)[] States =
        [("0", false, false), ("1", true, false), ("1c", true, true)];

    public static int Run(string dbPath)
    {
        using var stdout = new StreamWriter(Console.OpenStandardOutput(),
                                            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        TrackerDb.MainDbPath = dbPath;
        if (!TrackerDb.MainDbExists)
        {
            Console.Error.WriteLine("本体 DB が見つかりません: " + dbPath);
            return 2;
        }

        using var db = TrackerDb.OpenMainDb();
        var payload = StatsLeafQuery.LoadAll(db);

        int tables = 0, rows = 0;
        foreach (var (key, foreign, filter) in States)
        {
            var chars = filter
                ? new StatsCharFilter([.. StatsLeafDump.ProbeSelf],
                                      [.. Enumerable.Range(0, StatsCharFilter.CharacterCount)])
                : StatsCharFilter.All;

            foreach (var (label, path) in Gates())
            {
                var view = new StatsView(payload, StatsSection.StoryExtra, path, foreign, chars);
                var rec = view.Records();
                Row(stdout, "gate", key, label, rec is null ? "none" : "table");
                if (rec is null || path.Count > 0) continue;

                Row(stdout, "state", key, foreign ? "1" : "0", filter ? "1" : "0", rec.Title,
                    rec.Width.ToString("0.##", CultureInfo.InvariantCulture));
                Row(stdout, "cols", key,
                    string.Join("/", rec.Columns.Select(c => c.Label)),
                    string.Join("/", rec.Columns.Select(
                        c => c.Width.ToString("0.##", CultureInfo.InvariantCulture))),
                    string.Join("/", rec.Columns.Select(c => c.RightAligned ? "R" : "L")));
                foreach (var r in rec.Rows)
                {
                    Row(stdout, ["row", key, r.Mode.ToString(CultureInfo.InvariantCulture),
                                 r.Character.ToString(CultureInfo.InvariantCulture),
                                 .. r.Cells.Select(c => c.Text)]);
                    rows++;
                }
                foreach (var t in rec.Notes) Row(stdout, "note", key, t);
                tables++;
            }
        }

        Row(stdout, "count", "table", tables.ToString(CultureInfo.InvariantCulture),
            "row", rows.ToString(CultureInfo.InvariantCulture),
            "storyplays", payload.StoryPlays.Count.ToString(CultureInfo.InvariantCulture));
        stdout.Flush();
        return 0;
    }

    private static IEnumerable<(string Label, IReadOnlyList<string> Path)> Gates()
    {
        yield return ("root", []);
        yield return ("mode", ["*"]);
        yield return ("mychar", ["*", "*"]);
    }

    private static void Row(TextWriter w, params string[] cells)
        => w.Write(string.Join(Sep, cells) + "\n");
}
