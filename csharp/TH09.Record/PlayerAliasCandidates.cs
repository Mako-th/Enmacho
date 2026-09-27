using System.Text;
using Microsoft.Data.Sqlite;

using ReplayCols = TH09.Generated.DbColumns.Replays;
using NetCols = TH09.Generated.DbColumns.SessionNetPlay;

namespace TH09.Record;

public sealed class NameStat
{
    public int N;

    public string First = "";

    public string Last = "";

    public Dictionary<string, int> Opponents { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Sources { get; } = new(StringComparer.Ordinal);
}

public static class PlayerAliasCandidates
{
    public static Dictionary<string, NameStat> Collect(SqliteConnection c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var outp = new Dictionary<string, NameStat>(StringComparer.Ordinal);

        void Touch(string? name, string? other, string when, string source)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (!outp.TryGetValue(name, out var rec))
            {
                rec = new NameStat();
                outp[name] = rec;
            }
            rec.N++;
            rec.Sources.Add(source);
            if (when.Length > 0)
            {
                if (rec.First.Length == 0 || string.CompareOrdinal(when, rec.First) < 0) rec.First = when;
                if (string.CompareOrdinal(when, rec.Last) > 0) rec.Last = when;
            }
            if (!string.IsNullOrWhiteSpace(other))
                rec.Opponents[other] = rec.Opponents.GetValueOrDefault(other) + 1;
        }

        try
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {ReplayCols.P1Name},{ReplayCols.P2Name},{ReplayCols.ReplayDate}"
                             + $" FROM {ReplayCols.Table}";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var p1 = r.IsDBNull(0) ? null : r.GetString(0);
                var p2 = r.IsDBNull(1) ? null : r.GetString(1);
                var when = AsYmd(r.IsDBNull(2) ? null : r.GetString(2));
                Touch(p1, p2, when, "replay");
                Touch(p2, p1, when, "replay");
            }
        }
        catch (SqliteException)
        {
        }
        try
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {NetCols.LocalName},{NetCols.RemoteName},{NetCols.RecordedAt}"
                             + $" FROM {NetCols.Table}";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var local = r.IsDBNull(0) ? null : r.GetString(0);
                var remote = r.IsDBNull(1) ? null : r.GetString(1);
                var when = AsYmd(r.IsDBNull(2) ? null : r.GetString(2));
                Touch(local, remote, when, "net");
                Touch(remote, local, when, "net");
            }
        }
        catch (SqliteException)
        {
        }
        return outp;
    }

    public static string Norm(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var nfkc = name.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        var sb = new StringBuilder(nfkc.Length);
        foreach (var ch in nfkc)
        {
            if (!char.IsWhiteSpace(ch)) sb.Append(ch);
        }
        return sb.ToString();
    }

    public static string AsYmd(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Length >= 10 && value[4] == '-' && value[7] == '-')
            return value[2..10].Replace("-", "/", StringComparison.Ordinal);
        return value.Length >= 8 && value[2] == '/' ? value[..8] : "";
    }

    public static List<(string A, string B, string Why)> Hints(
        IEnumerable<string> names, IReadOnlyDictionary<string, long> aliasMap)
    {
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(aliasMap);
        var ordered = names.ToList();
        ordered.Sort(StringComparer.Ordinal);
        var outp = new List<(string, string, string)>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var a = ordered[i];
            for (var j = i + 1; j < ordered.Count; j++)
            {
                var b = ordered[j];
                if (aliasMap.TryGetValue(a, out var pa) && aliasMap.TryGetValue(b, out var pb) && pa == pb)
                    continue;
                var na = Norm(a);
                var nb = Norm(b);
                if (na == nb)
                {
                    outp.Add((a, b, "大小・空白を落とすと同じ"));
                }
                else if (na.Length >= 2 && nb.Length >= 2
                         && (na.StartsWith(nb, StringComparison.Ordinal)
                             || nb.StartsWith(na, StringComparison.Ordinal)))
                {
                    outp.Add((a, b, "片方がもう片方で始まる"));
                }
            }
        }
        return outp;
    }

    public static string OverlapText(NameStat x, NameStat y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        if (x.First.Length == 0 || x.Last.Length == 0 || y.First.Length == 0 || y.Last.Length == 0)
            return "時期不明";
        return string.CompareOrdinal(x.First, y.Last) <= 0 && string.CompareOrdinal(y.First, x.Last) <= 0
            ? "時期が重なる" : "時期が重ならない";
    }
}
