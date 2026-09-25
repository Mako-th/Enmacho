using System.Globalization;
using System.Text;

namespace TH09.Shell.Data;

internal static class HistoryDump
{
    public static readonly string[] Columns =
    [
        "id",
        "dt", "mode", "diff", "p1", "p2", "lives", "score", "state", "rep",
        "kind", "exec", "repId", "repCount",
        "recKind",
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
            var rows = HistoryQuery.LoadAll(db);

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

    private static string Cell(HistoryRow row, string column) => column switch
    {
        "id" => row.SessionId.ToString(CultureInfo.InvariantCulture),
        "dt" => row.StartedAtText,
        "mode" => row.ModeText,
        "diff" => row.DifficultyText,
        "p1" => row.P1CharacterText,
        "p2" => row.P2CharacterText,
        "lives" => row.FinalLivesText,
        "score" => row.FinalScoreText,
        "state" => row.StatusText,
        "rep" => row.HasReplayText,
        "kind" => row.IsReplayPlayback.ToString(),
        "exec" => row.ExecutionType ?? "",
        "repId" => row.ReplayId?.ToString(CultureInfo.InvariantCulture) ?? "",
        "repCount" => row.ReplayCount.ToString(CultureInfo.InvariantCulture),
        "recKind" => row.KindText,
        _ => throw new InvalidOperationException(
                 "HistoryDump.Columns に " + column + " があるのに、吐き方が書かれていない"),
    };
}
