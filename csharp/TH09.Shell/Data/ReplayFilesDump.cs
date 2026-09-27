using System.Globalization;
using System.Text;

namespace TH09.Shell.Data;

internal static class ReplayFilesDump
{
    public const string Flag = "--dump-replay-files";

    public const string AllKeyword = "all";

    public const string ReplayPrefix = "replay:";

    public static readonly string[] Columns =
        ["sid", "id", "path", "full", "src", "cur", "conf", "decode", "dir"];

    public const string SqlSessions = """
        SELECT s.session_id
          FROM sessions s
         ORDER BY s.session_id
        """;

    private const char Separator = '\t';

    public static int Run(string dbPath, string target)
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

            var all = string.Equals(target, AllKeyword, StringComparison.Ordinal);
            long? oneReplay = null, oneSession = null;
            if (!all)
            {
                if (target.StartsWith(ReplayPrefix, StringComparison.Ordinal))
                {
                    if (!long.TryParse(target.AsSpan(ReplayPrefix.Length), NumberStyles.Integer,
                                       CultureInfo.InvariantCulture, out var rid))
                    {
                        Console.Error.WriteLine(ReplayPrefix + " の後ろが数ではありません: " + target);
                        return 2;
                    }
                    oneReplay = rid;
                }
                else if (long.TryParse(target, NumberStyles.Integer,
                                       CultureInfo.InvariantCulture, out var sid))
                {
                    oneSession = sid;
                }
                else
                {
                    Console.Error.WriteLine("session_id か " + ReplayPrefix + "<id> か "
                                            + AllKeyword + " を渡してください: " + target);
                    return 2;
                }
            }

            using var db = TrackerDb.OpenMainDb();

            var sessions = new List<long?>();
            if (all) TrackerDb.ForEachRow(db, SqlSessions, r => sessions.Add(r.GetInt64(0)));
            else sessions.Add(oneSession);

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(Separator, Columns));
            foreach (var sid in sessions)
                foreach (var row in ReplayDetailQuery.LoadFiles(db, oneReplay, sid))
                    Append(sb, sid, row);

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

    private static void Append(StringBuilder sb, long? sessionId, ReplayFileRow row)
    {
        var reveal = ReplayFileReveal.For(row);
        for (var i = 0; i < Columns.Length; i++)
        {
            if (i > 0) sb.Append(Separator);
            sb.Append(Clean(Cell(sessionId, row, reveal, Columns[i])));
        }
        sb.AppendLine();
    }

    private static string Cell(long? sessionId, ReplayFileRow row, RevealTarget reveal, string column)
        => column switch
        {
            "sid" => sessionId?.ToString(CultureInfo.InvariantCulture) ?? "",
            "id" => row.Text(ReplayFileField.Id),
            "path" => row.Text(ReplayFileField.Path),
            "full" => row.FullPath ?? "",
            "src" => row.Text(ReplayFileField.Source),
            "cur" => row.Text(ReplayFileField.Current),
            "conf" => row.Text(ReplayFileField.Confidence),
            "decode" => row.Text(ReplayFileField.Decode),
            "dir" => reveal.Directory ?? reveal.Note ?? "",
            _ => throw new InvalidOperationException(
                     "ReplayFilesDump.Columns に " + column + " があるのに、吐き方が書かれていない"),
        };

    private static string Clean(string s)
        => s.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
}
