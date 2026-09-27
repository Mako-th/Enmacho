using System.Globalization;
using Microsoft.Data.Sqlite;
using TH09.Record.Generated;

using Links = TH09.Generated.DbColumns.SessionReplays;

namespace TH09.Record;

public sealed record SupersededPlan(IReadOnlyList<long> Delete, IReadOnlyList<long> Keep)
{
    public bool KeptOnly => Delete.Count == 0 && Keep.Count > 0;

    public bool Empty => Delete.Count == 0 && Keep.Count == 0;
}

public static class ScanLink
{
    public const double Confidence = 1.0;

    public static string Method => RecordLabels.ScanLinkMethod;

    public static string ConfidenceText => PyFloat(Confidence);

    private static string PyFloat(double value)
    {
        var s = value.ToString("R", CultureInfo.InvariantCulture);
        return s.Contains('.', StringComparison.Ordinal)
               || s.Contains('E', StringComparison.Ordinal)
               || s.Contains("Infinity", StringComparison.Ordinal)
               || s.Contains("NaN", StringComparison.Ordinal)
            ? s : s + ".0";
    }

    public static void Link(SqliteConnection conn, long sessionId, long replayId)
    {
        ArgumentNullException.ThrowIfNull(conn);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"INSERT OR REPLACE INTO {Links.Table}"
            + $"({Links.SessionId},{Links.ReplayId},{Links.LinkConfidence},{Links.LinkMethod})"
            + " VALUES($0,$1,$2,$3)";
        cmd.Parameters.AddWithValue("$0", sessionId);
        cmd.Parameters.AddWithValue("$1", replayId);
        cmd.Parameters.AddWithValue("$2", Confidence);
        cmd.Parameters.AddWithValue("$3", Method);
        cmd.ExecuteNonQuery();
    }

    public static SupersededPlan Superseded(SqliteConnection conn, long replayId, long keepSessionId)
    {
        ArgumentNullException.ThrowIfNull(conn);
        var old = new List<long>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SELECT {Links.SessionId} FROM {Links.Table}"
                            + $" WHERE {Links.ReplayId}=$0 AND {Links.SessionId}<>$1";
            cmd.Parameters.AddWithValue("$0", replayId);
            cmd.Parameters.AddWithValue("$1", keepSessionId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) old.Add(r.GetInt64(0));
        }
        if (old.Count == 0) return new SupersededPlan([], []);

        var shared = new HashSet<long>();
        using (var cmd = conn.CreateCommand())
        {
            var slots = string.Join(",", old.Select((_, i) => "$" + i.ToString(CultureInfo.InvariantCulture)));
            cmd.CommandText = $"SELECT {Links.SessionId} FROM {Links.Table}"
                            + $" WHERE {Links.SessionId} IN ({slots})"
                            + $" AND {Links.ReplayId}<>${old.Count.ToString(CultureInfo.InvariantCulture)}";
            for (var i = 0; i < old.Count; i++)
                cmd.Parameters.AddWithValue("$" + i.ToString(CultureInfo.InvariantCulture), old[i]);
            cmd.Parameters.AddWithValue("$" + old.Count.ToString(CultureInfo.InvariantCulture), replayId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) shared.Add(r.GetInt64(0));
        }
        var keep = old.Where(shared.Contains).Distinct().Order().ToList();
        var delete = old.Where(x => !shared.Contains(x)).ToList();
        return new SupersededPlan(delete, keep);
    }

    public static string KeptLine(IReadOnlyList<long> kept) =>
        "古いセッション " + PyList(kept) + " は別のリプレイにも紐づくので残しました";

    public static string PyList(IReadOnlyList<long> values) =>
        "[" + string.Join(", ", values.Select(v => v.ToString(CultureInfo.InvariantCulture))) + "]";

    public static string JoinSorted(IReadOnlyList<long> values) =>
        string.Join(", ", values.Order().Select(v => v.ToString(CultureInfo.InvariantCulture)));
}
