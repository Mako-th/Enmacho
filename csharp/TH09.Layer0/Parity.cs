using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TH09.Layer0;

public static class Parity
{
    public const string Version = "th09-layer0-parity-v1";

    public readonly record struct Row(string Kind, long SessionId, long No, int TickCount,
                                      int ColumnCount, long Words, string ColumnDigest, string FieldOrderDigest)
    {
        public string ToLine() => string.Join('\t',
        [
            Kind,
            SessionId.ToString(CultureInfo.InvariantCulture),
            No.ToString(CultureInfo.InvariantCulture),
            TickCount.ToString(CultureInfo.InvariantCulture),
            ColumnCount.ToString(CultureInfo.InvariantCulture),
            Words.ToString(CultureInfo.InvariantCulture),
            ColumnDigest,
            FieldOrderDigest,
        ]);

        public static Row Parse(string line)
        {
            var f = line.Split('\t');
            if (f.Length != 8) throw new InvalidDataException("記録の列数が 8 ではありません: " + line);
            return new Row(f[0], long.Parse(f[1]), long.Parse(f[2]), int.Parse(f[3]),
                           int.Parse(f[4]), long.Parse(f[5]), f[6], f[7]);
        }

        public string Key => Kind + ":" + SessionId + ":" + No;
    }

    public static string ColumnDigest(TickArchive.TickColumns columns)
    {
        using var sha = SHA256.Create();
        var names = columns.Names.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var head = new byte[8];
        foreach (var name in names)
        {
            var col = columns[name];
            var nameBytes = Encoding.UTF8.GetBytes(name);
            sha.TransformBlock(nameBytes, 0, nameBytes.Length, null, 0);
            sha.TransformBlock([0], 0, 1, null, 0);
            BinaryPrimitives.WriteUInt64LittleEndian(head, (ulong)col.LongLength);
            sha.TransformBlock(head, 0, 8, null, 0);
            var raw = new byte[col.Length * 4];
            if (BitConverter.IsLittleEndian) Buffer.BlockCopy(col, 0, raw, 0, raw.Length);
            else for (int i = 0; i < col.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(i * 4), col[i]);
            sha.TransformBlock(raw, 0, raw.Length, null, 0);
        }
        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }

    public static string FieldOrderDigest(JsonNodeLite fieldOrder) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fieldOrder.ToCanonicalJson()))).ToLowerInvariant();


    public static void WriteRows(string path, IEnumerable<Row> rows, string side, string dbPath,
                                 IEnumerable<string>? notes = null)
    {
        using var w = new StreamWriter(path, false, new UTF8Encoding(false));
        w.WriteLine("# " + Version);
        w.WriteLine("# side=" + side);
        w.WriteLine("# db=" + dbPath);
        foreach (var n in notes ?? []) w.WriteLine("# " + n);
        w.WriteLine("# 列: kind\tsession_id\tno\ttick_count\tcolumns\twords\tcolumn_sha256\tfield_order_sha256");
        long records = 0, words = 0;
        foreach (var r in rows) { w.WriteLine(r.ToLine()); records++; words += r.Words; }
        w.WriteLine($"# totals records={records} words={words}");
    }

    public static List<Row> ReadRows(string path)
    {
        var rows = new List<Row>();
        foreach (var line in File.ReadLines(path, Encoding.UTF8))
        {
            if (line.Length == 0 || line[0] == '#') continue;
            rows.Add(Row.Parse(line));
        }
        return rows;
    }


    public sealed class Report
    {
        public int Total;
        public int MatchedColumns;
        public int MatchedFieldOrder;
        public long WordsCompared;
        public readonly List<string> Problems = [];
        public bool Ok => Total > 0 && Problems.Count == 0
                          && MatchedColumns == Total && MatchedFieldOrder == Total;
    }

    public static Report Compare(List<Row> python, List<Row> csharp)
    {
        var rep = new Report();
        var byKey = new Dictionary<string, Row>(StringComparer.Ordinal);
        foreach (var r in csharp)
        {
            if (byKey.ContainsKey(r.Key)) { rep.Problems.Add($"C# 側に同じ鍵が 2 度出ています: {r.Key}"); continue; }
            byKey[r.Key] = r;
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var p in python)
        {
            rep.Total++;
            seen.Add(p.Key);
            if (!byKey.TryGetValue(p.Key, out var c))
            {
                rep.Problems.Add($"{p.Key}: C# 側に無い");
                continue;
            }
            if (p.Words == 0 || c.Words == 0)
            {
                rep.Problems.Add($"{p.Key}: 語数が 0（python={p.Words} / csharp={c.Words}）★これは一致ではなく測れていない");
                continue;
            }
            bool bad = false;
            if (p.TickCount != c.TickCount)
            { rep.Problems.Add($"{p.Key}: tick_count が違う python={p.TickCount} csharp={c.TickCount}"); bad = true; }
            if (p.ColumnCount != c.ColumnCount)
            { rep.Problems.Add($"{p.Key}: 列数が違う python={p.ColumnCount} csharp={c.ColumnCount}"); bad = true; }
            if (p.Words != c.Words)
            { rep.Problems.Add($"{p.Key}: 語数が違う python={p.Words} csharp={c.Words}"); bad = true; }

            if (string.Equals(p.ColumnDigest, c.ColumnDigest, StringComparison.Ordinal)) rep.MatchedColumns++;
            else { rep.Problems.Add($"{p.Key}: 列のフィンガープリントが違う python={p.ColumnDigest[..16]}… csharp={c.ColumnDigest[..16]}…"); bad = true; }

            if (string.Equals(p.FieldOrderDigest, c.FieldOrderDigest, StringComparison.Ordinal)) rep.MatchedFieldOrder++;
            else { rep.Problems.Add($"{p.Key}: field_order の正規形が違う python={p.FieldOrderDigest[..16]}… csharp={c.FieldOrderDigest[..16]}…"); bad = true; }

            if (!bad) rep.WordsCompared += p.Words;
        }
        foreach (var c in csharp)
        {
            if (!seen.Contains(c.Key)) rep.Problems.Add($"{c.Key}: Python 側に無い");
        }
        return rep;
    }
}
