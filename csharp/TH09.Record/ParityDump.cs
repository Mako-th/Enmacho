using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using TH09.Record.Generated;

namespace TH09.Record;

public sealed record ParityStats(int Tables, int ComparedColumns, int ExcludedColumns,
                                 IReadOnlyList<(string Table, long Rows)> RowsPerTable,
                                 IReadOnlyList<long> ExcludedSessions,
                                 IReadOnlyList<string> CorpusMissing,
                                 int? Limit)
{
    public long TotalRows => RowsPerTable.Sum(x => x.Rows);
}

public static class ParityDump
{
    internal static bool IsExcluded(ParityKind kind) => ParitySpec.ExcludedKinds.Contains(kind);

    internal static List<string> EmittedColumns(ParityTable table) =>
        table.Columns.Where(c => !IsExcluded(c.Kind)).Select(c => c.Name).ToList();

    internal static bool IsFullyExcluded(ParityTable table) =>
        table.Columns.All(c => IsExcluded(c.Kind));


    public static (List<string> MissingInSpec, List<string> MissingInDb, int TablesInDb, int ColumnsInDb)
        CheckAgainstDb(RecordDb db)
    {
        var real = db.TableColumns();
        var missingInSpec = new List<string>();
        var missingInDb = new List<string>();
        var spec = ParitySpec.Tables.ToDictionary(t => t.Name, StringComparer.Ordinal);

        foreach (var (table, cols) in real.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (!spec.TryGetValue(table, out var st))
            {
                missingInSpec.AddRange(cols.Count > 0
                    ? cols.Select(c => $"{table}.{c}")
                    : [$"{table}（表ごと）"]);
                continue;
            }
            var known = st.Columns.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
            missingInSpec.AddRange(cols.Where(c => !known.Contains(c)).Select(c => $"{table}.{c}"));
        }
        foreach (var st in ParitySpec.Tables)
        {
            if (!real.TryGetValue(st.Name, out var cols)) { missingInDb.Add($"{st.Name}（表ごと）"); continue; }
            var have = cols.ToHashSet(StringComparer.Ordinal);
            missingInDb.AddRange(st.Columns.Where(c => !have.Contains(c.Name)).Select(c => $"{st.Name}.{c.Name}"));
        }
        return (missingInSpec, missingInDb, real.Count, real.Sum(kv => kv.Value.Count));
    }


    public static List<long> ExcludedSessions(RecordDb db)
    {
        var list = new List<long>();
        try
        {
            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = "SELECT session_id FROM sessions WHERE status IN ("
                            + Bind(cmd, ParitySpec.ExcludedSessionStatus) + ") ORDER BY session_id";
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(r.GetInt64(0));
        }
        catch (SqliteException)
        {
        }
        return list;
    }

    public static List<long>? CorpusReplayIds(RecordDb db)
    {
        if (ParitySpec.CorpusReplaySha256.Length == 0) return null;
        var list = new List<long>();
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = "SELECT replay_id FROM replays WHERE sha256 IN ("
                        + Bind(cmd, ParitySpec.CorpusReplaySha256) + ") ORDER BY replay_id";
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(r.GetInt64(0));
        return list;
    }

    public static List<string> CorpusMissing(RecordDb db)
    {
        if (ParitySpec.CorpusReplaySha256.Length == 0) return [];
        var have = new HashSet<string>(StringComparer.Ordinal);
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = "SELECT sha256 FROM replays WHERE sha256 IN ("
                        + Bind(cmd, ParitySpec.CorpusReplaySha256) + ")";
        using var r = cmd.ExecuteReader();
        while (r.Read()) have.Add(r.GetString(0));
        return ParitySpec.CorpusReplaySha256.Where(s => !have.Contains(s)).ToList();
    }

    private static string Bind<T>(SqliteCommand cmd, IReadOnlyList<T> values)
    {
        var names = new List<string>(values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            var name = "@p" + cmd.Parameters.Count.ToString(CultureInfo.InvariantCulture);
            cmd.Parameters.AddWithValue(name, values[i]!);
            names.Add(name);
        }
        return string.Join(",", names);
    }


    public static ParityStats Dump(RecordDb db, TextWriter writer, int? limit = null, bool useCorpus = true)
    {
        var rows = new List<(string, long)>();
        var compared = 0;
        var excluded = 0;

        var drop = ExcludedSessions(db);
        var keepReplays = useCorpus ? CorpusReplayIds(db) : null;
        var corpusMissing = useCorpus ? CorpusMissing(db) : [];

        List<long>? keepSessions = null;
        if (drop.Count > 0 || keepReplays is not null)
        {
            try
            {
                using var cmd = db.Connection.CreateCommand();
                var cond = new List<string>();
                if (drop.Count > 0)
                    cond.Add("status NOT IN (" + Bind(cmd, ParitySpec.ExcludedSessionStatus) + ")");
                if (keepReplays is not null)
                    cond.Add("session_id IN (SELECT session_id FROM session_replays WHERE replay_id IN ("
                             + Bind(cmd, keepReplays) + "))");
                cmd.CommandText = "SELECT session_id FROM sessions WHERE " + string.Join(" AND ", cond);
                var got = new List<long>();
                using (var r = cmd.ExecuteReader()) { while (r.Read()) got.Add(r.GetInt64(0)); }
                keepSessions = got;
            }
            catch (SqliteException)
            {
                keepSessions = null;
            }
        }

        List<long>? keepRounds = null;
        if (keepSessions is not null)
        {
            try
            {
                if (keepSessions.Count == 0)
                {
                    keepRounds = [];
                }
                else
                {
                    using var cmd = db.Connection.CreateCommand();
                    cmd.CommandText = "SELECT round_record_id FROM rounds WHERE session_id IN ("
                                    + Bind(cmd, keepSessions) + ")";
                    var got = new List<long>();
                    using var r = cmd.ExecuteReader();
                    while (r.Read()) got.Add(r.GetInt64(0));
                    keepRounds = got;
                }
            }
            catch (SqliteException)
            {
                keepRounds = null;
            }
        }

        foreach (var table in ParitySpec.Tables)
        {
            var cols = EmittedColumns(table);
            compared += cols.Count;
            excluded += table.Columns.Length - cols.Count;
            if (cols.Count == 0)
            {
                var n = db.CountRows(table.Name);
                rows.Add((table.Name, 0));
                writer.Write($"# table {table.Name} rows-in-db={n.ToString(CultureInfo.InvariantCulture)}"
                             + $" emitted=0 （表ごと除外: {KindName(table.Columns[0].Kind)}）\n");
                continue;
            }

            var kinds = table.Columns.ToDictionary(c => c.Name, c => c.Kind, StringComparer.Ordinal);
            var ranks = new Dictionary<string, Dictionary<string, long>>(StringComparer.Ordinal);
            foreach (var c in cols)
            {
                if (kinds[c] == ParityKind.IdOrderOnly) ranks[c] = RankMap(db, table.Name, c, keepSessions, keepReplays, keepRounds);
            }

            using var cmd = db.Connection.CreateCommand();
            var where = WhereFor(cmd, table.Name, keepSessions, keepReplays, keepRounds);
            var sql = new StringBuilder("SELECT ");
            sql.Append(string.Join(",", cols.Select(c => $"\"{c}\",typeof(\"{c}\")")));
            sql.Append($" FROM \"{table.Name}\"");
            if (where.Length > 0) sql.Append(" WHERE ").Append(where);
            if (table.Order.Length > 0)
                sql.Append(" ORDER BY ").Append(string.Join(",", table.Order.Select(c => $"\"{c}\"")));
            if (limit is not null) sql.Append(" LIMIT ").Append(limit.Value.ToString(CultureInfo.InvariantCulture));
            cmd.CommandText = sql.ToString();

            writer.Write("@" + table.Name + "\t" + string.Join("\t", cols) + "\n");
            long emitted = 0;
            using (var r = cmd.ExecuteReader())
            {
                var tokens = new string[cols.Count];
                while (r.Read())
                {
                    for (var i = 0; i < cols.Count; i++)
                    {
                        var type = r.GetString(i * 2 + 1);
                        var value = ReadValue(r, i * 2, type);
                        tokens[i] = kinds[cols[i]] switch
                        {
                            ParityKind.IdOrderOnly => value is null
                                ? ParityValue.NullToken
                                : "n:" + ranks[cols[i]][ParityValue.RankKey(type, value)]
                                        .ToString(CultureInfo.InvariantCulture),
                            ParityKind.PathBasename => ParityValue.BasenameToken(
                                RequireText(cols[i], "パス", type, value)),
                            ParityKind.JsonSemantic => JsonToken(type, value),
                            ParityKind.TimeTruncSec => ParityValue.TruncateSecondsToken(
                                RequireText(cols[i], "時刻", type, value)),
                            _ => ParityValue.ValueToken(type, value),
                        };
                    }
                    writer.Write(ParityValue.JoinRow(table.Name, tokens));
                    writer.Write('\n');
                    emitted++;
                }
            }
            rows.Add((table.Name, emitted));
            writer.Write($"# table {table.Name} emitted-rows={emitted.ToString(CultureInfo.InvariantCulture)}"
                         + $" compared-cols={cols.Count} excluded-cols={table.Columns.Length - cols.Count}\n");
        }

        return new ParityStats(ParitySpec.Tables.Length, compared, excluded, rows, drop, corpusMissing, limit);
    }

    private static string JsonToken(string type, object? value)
    {
        if (value is null) return ParityValue.NullToken;
        if (value is string s) return ParityValue.CanonJsonToken(s);
        if (value is byte[] b) return ParityValue.CanonJsonToken(Encoding.UTF8.GetString(b));
        throw new InvalidDataException(
            $"JSON の列に文字列でない値が入っている（typeof={type}）。"
            + "Python 側の str() の書式に合わせる規則を決めていないので、黙って通さない。");
    }

    private static string? RequireText(string column, string what, string type, object? value)
    {
        if (value is null) return null;
        if (value is string s) return s;
        throw new InvalidDataException(
            $"{what}の列 `{column}` に文字列でない値が入っている（typeof={type}）。"
            + "Python 側の str() の書式に合わせる規則を決めていないので、黙って通さない。");
    }

    private static Dictionary<string, long> RankMap(RecordDb db, string table, string column,
                                                    List<long>? keepSessions, List<long>? keepReplays,
                                                    List<long>? keepRounds)
    {
        using var cmd = db.Connection.CreateCommand();
        var where = WhereFor(cmd, table, keepSessions, keepReplays, keepRounds);
        cmd.CommandText = $"SELECT DISTINCT \"{column}\",typeof(\"{column}\") FROM \"{table}\""
                        + (where.Length > 0 ? " WHERE " + where : "")
                        + $" ORDER BY \"{column}\"";
        var map = new Dictionary<string, long>(StringComparer.Ordinal);
        long n = 0;
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var type = r.GetString(1);
            var value = ReadValue(r, 0, type);
            if (value is null) continue;
            map[ParityValue.RankKey(type, value)] = n++;
        }
        return map;
    }

    private static string WhereFor(SqliteCommand cmd, string table, List<long>? keepSessions,
                                   List<long>? keepReplays, List<long>? keepRounds)
    {
        var parts = new List<string>();
        var sessionCol = ParitySpec.SessionScoped.FirstOrDefault(x => x.Table == table).Column;
        if (sessionCol is not null && keepSessions is not null)
        {
            if (keepSessions.Count == 0) return "1=0";
            parts.Add($"\"{sessionCol}\" IN ({Bind(cmd, keepSessions)})");
        }
        var replayCol = ParitySpec.ReplayScoped.FirstOrDefault(x => x.Table == table).Column;
        if (replayCol is not null && keepReplays is not null)
        {
            if (keepReplays.Count == 0) return "1=0";
            parts.Add($"\"{replayCol}\" IN ({Bind(cmd, keepReplays)})");
        }
        if (table == "round_metrics" && keepRounds is not null)
        {
            if (keepRounds.Count == 0) return "1=0";
            parts.Add($"round_record_id IN ({Bind(cmd, keepRounds)})");
        }
        return parts.Count > 0 ? string.Join(" AND ", parts) : "";
    }

    private static object? ReadValue(SqliteDataReader r, int ordinal, string type) => type switch
    {
        ParityValue.TypeNull => null,
        ParityValue.TypeInteger => r.GetInt64(ordinal),
        ParityValue.TypeReal => r.GetDouble(ordinal),
        ParityValue.TypeBlob => r.GetFieldValue<byte[]>(ordinal),
        _ => r.GetString(ordinal),
    };

    internal static string KindName(ParityKind kind)
    {
        var s = kind.ToString();
        var sb = new StringBuilder();
        for (var i = 0; i < s.Length; i++)
        {
            if (i > 0 && char.IsUpper(s[i])) sb.Append('_');
            sb.Append(char.ToUpperInvariant(s[i]));
        }
        return sb.ToString();
    }


    public static List<string> HeaderLines(ParityStats stats, RecordDb? db, bool useCorpus = true)
    {
        var lines = new List<string> { "# " + ParitySpec.FormatVersion };
        if (db is not null) lines.Add("# db-file " + db.FileName());
        lines.Add($"# tables {stats.Tables} / compared-columns {stats.ComparedColumns}"
                  + $" / excluded-columns {stats.ExcludedColumns}");
        var head = stats.ExcludedSessions.Take(20).Select(x => x.ToString(CultureInfo.InvariantCulture));
        lines.Add($"# excluded-sessions {stats.ExcludedSessions.Count} ["
                  + string.Join(",", head) + (stats.ExcludedSessions.Count > 20 ? ",…" : "")
                  + $"]（status={string.Join("/", ParitySpec.ExcludedSessionStatus)} のまま閉じていないもの）");
        var corpusCount = useCorpus ? ParitySpec.CorpusReplaySha256.Length : 0;
        lines.Add($"# corpus {corpusCount} 本" + (corpusCount == 0 ? "（空なので全件）" : ""));
        if (stats.CorpusMissing.Count > 0)
            lines.Add($"# ★corpus-missing {stats.CorpusMissing.Count} 本（DB に無い sha256）: "
                      + string.Join(" ", stats.CorpusMissing));
        if (stats.Limit is not null)
            lines.Add($"# ★limit {stats.Limit.Value} ——表ごとに切っている。合否の判定には使えない");
        return lines;
    }

    public static ParityStats WriteDump(RecordDb db, string path, int? limit = null, bool useCorpus = true)
    {
        var body = new StringWriter { NewLine = "\n" };
        var stats = Dump(db, body, limit, useCorpus);
        var text = string.Join("\n", HeaderLines(stats, db, useCorpus)) + "\n" + body.ToString();
        File.WriteAllText(path, text, new UTF8Encoding(false));
        return stats;
    }
}
