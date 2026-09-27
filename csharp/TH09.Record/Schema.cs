using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using TH09.Record.Generated;

namespace TH09.Record;

public sealed record ColumnShape(string Name, string Type, int NotNull, string? Default, int Pk);

public sealed record ShapeDiff(IReadOnlyList<string> Fatal, IReadOnlyList<string> Pending);

public static class Schema
{
    public static string Fingerprint()
    {
        var joined = string.Join("\n", RecordSchema.Statements);
        return Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(joined)));
    }

    public static RecordDb CreateFrom(string path)
    {
        var db = RecordDb.OpenReadWrite(path);
        using var tx = db.Connection.BeginTransaction();
        foreach (var sql in RecordSchema.Statements)
        {
            using var cmd = db.Connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
        return db;
    }

    public static List<(string Table, string Column)> EnsureShape(RecordDb db)
    {
        foreach (var sql in RecordSchema.Statements) RecordDb.Exec(db.Connection, IfNotExists(sql));

        var added = new List<(string, string)>();
        var have = db.TableColumns();
        foreach (var (table, column, decl) in RecordSchema.PendingColumns)
        {
            if (!have.TryGetValue(table, out var cols) || cols.Contains(column, StringComparer.Ordinal)) continue;
            RecordDb.Exec(db.Connection, $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {decl}");
            cols.Add(column);
            added.Add((table, column));
        }
        return added;
    }

    internal static string IfNotExists(string sql)
    {
        foreach (var head in new[] { "CREATE TABLE ", "CREATE INDEX " })
        {
            if (sql.StartsWith(head, StringComparison.Ordinal))
                return head + "IF NOT EXISTS " + sql[head.Length..];
        }
        throw new InvalidDataException("知らない形の DDL: " + sql[..Math.Min(40, sql.Length)]);
    }


    public static Dictionary<string, List<ColumnShape>> Shapes(RecordDb db)
    {
        var map = new Dictionary<string, List<ColumnShape>>(StringComparer.Ordinal);
        foreach (var table in db.TableNames())
        {
            var cols = new List<ColumnShape>();
            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                cols.Add(new ColumnShape(
                    r.GetString(1),
                    (r.IsDBNull(2) ? "" : r.GetString(2)).ToUpperInvariant(),
                    r.GetInt32(3),
                    r.IsDBNull(4) ? null : r.GetValue(4).ToString(),
                    r.GetInt32(5)));
            }
            map[table] = cols;
        }
        return map;
    }

    public static HashSet<(string, string)> PendingSet() =>
        RecordSchema.PendingColumns.Select(x => (x.Table, x.Column)).ToHashSet();

    public static ShapeDiff Compare(Dictionary<string, List<ColumnShape>> want,
                                    Dictionary<string, List<ColumnShape>> got,
                                    HashSet<(string, string)>? pending = null)
    {
        pending ??= [];
        var fatal = new List<string>();
        var later = new List<string>();

        foreach (var name in got.Keys.Where(k => !want.ContainsKey(k)).OrderBy(x => x, StringComparer.Ordinal))
            fatal.Add($"★表 `{name}` が実 DB にあるのに、定義元のどこにも `CREATE TABLE` が無い（空から作り直すと欠ける）");
        foreach (var name in want.Keys.Where(k => !got.ContainsKey(k)).OrderBy(x => x, StringComparer.Ordinal))
            later.Add($"表 `{name}` が実 DB にまだ無い（次に DB を開けば作られる）");

        foreach (var name in want.Keys.Where(got.ContainsKey).OrderBy(x => x, StringComparer.Ordinal))
        {
            var a = want[name].ToDictionary(c => c.Name, StringComparer.Ordinal);
            var b = got[name].ToDictionary(c => c.Name, StringComparer.Ordinal);
            foreach (var col in b.Keys.Where(k => !a.ContainsKey(k)).OrderBy(x => x, StringComparer.Ordinal))
                fatal.Add($"★`{name}.{col}` が実 DB にあるのに、定義元のどこにも無い");
            foreach (var col in a.Keys.Where(k => !b.ContainsKey(k)).OrderBy(x => x, StringComparer.Ordinal))
            {
                if (pending.Contains((name, col)))
                    later.Add($"`{name}.{col}` は実 DB にまだ無い（次に DB を開けば `ALTER` で足される）");
                else
                    fatal.Add($"★`{name}.{col}` が定義元にあるのに実 DB に無く、後から足す仕掛けも無い（新規 DB と既存 DB で形が変わる）");
            }
            foreach (var col in a.Keys.Where(b.ContainsKey).OrderBy(x => x, StringComparer.Ordinal))
            {
                if (a[col] != b[col])
                    fatal.Add($"★`{name}.{col}` の形が違う（定義元 {Describe(a[col])} / 実 DB {Describe(b[col])}。"
                              + "並びは 型 / NOT NULL / 既定値 / 主キー）");
            }
            var orderA = want[name].Where(c => b.ContainsKey(c.Name)).Select(c => c.Name).ToList();
            var orderB = got[name].Where(c => a.ContainsKey(c.Name)).Select(c => c.Name).ToList();
            if (!orderA.SequenceEqual(orderB, StringComparer.Ordinal))
                fatal.Add($"★`{name}` の列の並びが違う（定義元 [{string.Join(", ", orderA)}] / 実 DB [{string.Join(", ", orderB)}]）");
        }
        return new ShapeDiff(fatal, later);
    }

    private static string Describe(ColumnShape c) =>
        $"('{c.Type}', {c.NotNull.ToString(CultureInfo.InvariantCulture)}, "
        + $"{(c.Default is null ? "None" : "'" + c.Default + "'")}, {c.Pk.ToString(CultureInfo.InvariantCulture)})";


    public static int WriteShapes(Dictionary<string, List<ColumnShape>> shapes, string path)
    {
        var sb = new StringBuilder();
        sb.Append("# th09-schema-shape-v1\n");
        sb.Append(CultureInfo.InvariantCulture,
                  $"# tables {shapes.Count} / columns {shapes.Sum(kv => kv.Value.Count)}\n");
        sb.Append("@table\tordinal\tname\ttype\tnotnull\tdefault\tpk\n");
        var lines = 0;
        foreach (var table in shapes.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            var cols = shapes[table];
            for (var i = 0; i < cols.Count; i++)
            {
                var c = cols[i];
                sb.Append(CultureInfo.InvariantCulture,
                          $"{table}\t{i}\t{c.Name}\t{c.Type}\t{c.NotNull}\t{c.Default ?? "~"}\t{c.Pk}\n");
                lines++;
            }
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        return lines;
    }
}
