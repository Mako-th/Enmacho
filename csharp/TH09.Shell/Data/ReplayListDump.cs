using System.Globalization;
using System.Text;

namespace TH09.Shell.Data;

internal static class ReplayListDump
{
    public static readonly string[] Columns =
    [
        "id",
        "own", "mark", "dt", "hasTime", "diff", "p1", "p2",
        "score", "lives", "reach", "time", "frames", "result", "rounds",
        "kind",
        "mode", "sid",
    ];

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
            var rows = ReplayListQuery.LoadAll(db);

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(Separator, Columns));
            foreach (var row in rows)
            {
                for (var i = 0; i < Columns.Length; i++)
                {
                    if (i > 0) sb.Append(Separator);
                    sb.Append(Cell(row, Columns[i]));
                }
                sb.AppendLine();
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

    private static string Cell(ReplayListRow row, string column) => column switch
    {
        "id" => row.ReplayId.ToString(CultureInfo.InvariantCulture),
        "own" => ((int)row.Own).ToString(CultureInfo.InvariantCulture),
        "mark" => row.OwnMark,
        "dt" => row.PlayedAtText,
        "hasTime" => row.PlayedAtHasTime.ToString(),
        "diff" => row.DifficultyText,
        "p1" => row.P1CharacterText,
        "p2" => row.P2CharacterText,
        "score" => row.FinalScore?.ToString(CultureInfo.InvariantCulture) ?? "",
        "lives" => row.LivesText,
        "reach" => row.ReachText,
        "time" => row.TotalTimeText,
        "frames" => row.TotalFrames?.ToString(CultureInfo.InvariantCulture) ?? "",
        "result" => row.ResultText,
        "rounds" => RoundsText(row),
        "kind" => row.KindText,
        "mode" => row.Section == ReplaySection.Match ? row.MatchModeText : "",
        "sid" => row.SessionId?.ToString(CultureInfo.InvariantCulture) ?? "",
        _ => throw new InvalidOperationException(
                 "ReplayListDump.Columns に " + column + " があるのに、吐き方が書かれていない"),
    };

    private static string RoundsText(ReplayListRow row)
        => string.Join("|", row.Rounds.Select(
               x => x.Text + "," + (x.WinnerSide?.ToString(CultureInfo.InvariantCulture) ?? "")
                    + "," + x.HasNext));
}
