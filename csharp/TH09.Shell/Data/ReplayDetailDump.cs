using System.Globalization;
using System.Text;

namespace TH09.Shell.Data;

internal static class ReplayDetailDump
{
    public const string AllKeyword = "all";

    public static readonly string[] HeaderColumns =
    [
        "id", "sid", "section", "mode", "diff", "dt", "p1", "p1name", "p2", "p2name",
        "matchmode", "nrounds", "time", "frames", "sub", "crumb", "links", "counted", "status",
    ];

    public static readonly string[] RoundColumns =
    [
        "id", "sid", "rr", "label", "oppo", "field", "bgm", "end", "endx", "seg", "cb", "segx",
        "lives", "life", "time", "cards", "boss", "rev", "quick", "win", "qany",
    ];

    private const char Separator = '\t';

    public static int Run(string dbPath, string target, string? layer0Path = null)
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
            long one = 0;
            if (!all && !long.TryParse(target, NumberStyles.Integer, CultureInfo.InvariantCulture, out one))
            {
                Console.Error.WriteLine("replay_id か " + AllKeyword + " を渡してください: " + target);
                return 2;
            }

            if (layer0Path is not null && !File.Exists(layer0Path))
            {
                Console.Error.WriteLine("Layer 0 が見つかりません: " + layer0Path);
                return 2;
            }

            using var db = TrackerDb.OpenMainDb();

            var ids = new List<long>();
            if (all)
                foreach (var row in ReplayListQuery.LoadAll(db)) ids.Add(row.ReplayId);
            else
                ids.Add(one);

            var heads = new StringBuilder();
            var rounds = new StringBuilder();
            heads.AppendLine(string.Join(Separator, HeaderColumns));
            rounds.AppendLine(string.Join(Separator, RoundColumns));

            foreach (var id in ids)
            {
                var detail = ReplayDetailQuery.Load(db, id, null, layer0Path);
                AppendHeader(heads, id, detail);
                foreach (var row in detail.Rows) AppendRound(rounds, id, detail, row);
            }

            stdout.Write(heads.ToString());
            stdout.WriteLine();
            stdout.Write(rounds.ToString());
            stdout.Flush();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
    }

    private static void AppendHeader(StringBuilder sb, long id, ReplayDetail d)
    {
        for (var i = 0; i < HeaderColumns.Length; i++)
        {
            if (i > 0) sb.Append(Separator);
            sb.Append(Clean(HeaderCell(id, d, HeaderColumns[i])));
        }
        sb.AppendLine();
    }

    private static void AppendRound(StringBuilder sb, long id, ReplayDetail d, ReplayDetailRow row)
    {
        for (var i = 0; i < RoundColumns.Length; i++)
        {
            if (i > 0) sb.Append(Separator);
            sb.Append(Clean(RoundCell(id, d, row, RoundColumns[i])));
        }
        sb.AppendLine();
    }

    private static string HeaderCell(long id, ReplayDetail d, string column)
    {
        var h = d.Header;
        return column switch
        {
            "id" => id.ToString(CultureInfo.InvariantCulture),
            "sid" => h?.SessionId?.ToString(CultureInfo.InvariantCulture) ?? "",
            "section" => h is null ? "" : (h.IsMatch ? "match" : "story"),
            "mode" => h?.ModeText ?? "",
            "diff" => h?.DifficultyText ?? "",
            "dt" => h?.PlayedAtText ?? "",
            "p1" => h?.P1CharacterText ?? "",
            "p1name" => h?.P1NameText ?? "",
            "p2" => h?.P2CharacterText ?? "",
            "p2name" => h?.P2NameText ?? "",
            "matchmode" => h is null ? "" : ReplayLabels.MatchMode(h.MatchMode),
            "nrounds" => (h?.RoundCount ?? 0).ToString(CultureInfo.InvariantCulture),
            "time" => h?.TotalTimeText ?? "",
            "frames" => h?.TotalFrames?.ToString(CultureInfo.InvariantCulture) ?? "",
            "sub" => h?.SubtitleText ?? "",
            "crumb" => h?.CrumbText ?? "",
            "links" => d.LinkCount.ToString(CultureInfo.InvariantCulture),
            "counted" => d.CountedRounds.ToString(CultureInfo.InvariantCulture),
            "status" => d.Status ?? "",
            _ => throw new InvalidOperationException(
                     "ReplayDetailDump.HeaderColumns に " + column + " があるのに、吐き方が書かれていない"),
        };
    }

    private static string RoundCell(long id, ReplayDetail d, ReplayDetailRow row, string column) => column switch
    {
        "id" => id.ToString(CultureInfo.InvariantCulture),
        "sid" => d.Header?.SessionId?.ToString(CultureInfo.InvariantCulture) ?? "",
        "rr" => row.RoundRecordId.ToString(CultureInfo.InvariantCulture),
        "label" => row.Text(DetailField.Label),
        "oppo" => row.Text(DetailField.Opponent),
        "field" => row.Text(DetailField.Field),
        "bgm" => row.Text(DetailField.Bgm),
        "end" => row.Text(DetailField.EndScore),
        "endx" => row.Text(DetailField.EndScoreExCb),
        "seg" => row.Text(DetailField.SegmentScore),
        "cb" => row.Text(DetailField.ClearBonus),
        "segx" => row.Text(DetailField.SegmentExCb),
        "lives" => row.Text(DetailField.Lives),
        "life" => row.Text(DetailField.Life),
        "time" => row.Text(DetailField.Time),
        "cards" => row.Text(DetailField.Cards),
        "boss" => row.Text(DetailField.Boss),
        "rev" => row.Text(DetailField.Reversal),
        "quick" => row.Text(DetailField.Quick),
        "win" => row.Text(DetailField.Winner),
        "qany" => (row.P1.QuickSpell is null && row.P2.QuickSpell is null
                   && row.P1.QuickBoss is null && row.P2.QuickBoss is null) ? "" : "1",
        _ => throw new InvalidOperationException(
                 "ReplayDetailDump.RoundColumns に " + column + " があるのに、吐き方が書かれていない"),
    };

    private static string Clean(string s)
        => s.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
}
