using System.Globalization;
using System.Text;

namespace TH09.Shell.Data;

internal static class RevealChoicesDump
{
    public const string Flag = "--dump-reveal-choices";

    public static readonly string[] Columns = ["label", "dir"];

    private const char Separator = '\t';

    public static int Run(string dbPath, string replayIdText)
    {
        using var stdout = new StreamWriter(Console.OpenStandardOutput(),
                                            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        try
        {
            if (!long.TryParse(replayIdText, NumberStyles.Integer,
                               CultureInfo.InvariantCulture, out var replayId))
            {
                Console.Error.WriteLine("replay_id は整数で指してください: " + replayIdText);
                return 2;
            }

            TrackerDb.MainDbPath = dbPath;
            if (!TrackerDb.MainDbExists)
            {
                Console.Error.WriteLine("本体 DB が見つかりません: " + dbPath);
                return 2;
            }

            using var db = TrackerDb.OpenMainDb();
            var rows = ReplayDetailQuery.LoadFiles(db, replayId, sessionId: null);
            var candidates = ReplayFileReveal.Candidates(rows);

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(Separator, Columns));
            foreach (var c in candidates)
                sb.AppendLine(Clean(c.Label) + Separator + Clean(c.Directory));

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

    private static string Clean(string s)
        => s.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
}
