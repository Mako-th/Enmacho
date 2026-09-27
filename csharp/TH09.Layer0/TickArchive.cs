using System.Buffers.Binary;
using System.Collections;
using System.IO.Compression;
using System.Text;

namespace TH09.Layer0;

public static class TickArchive
{
    public const string Encoding = "colzlib-v1";

    public const int DefaultLevel = 6;

    public const string SectionInvariant = "invariant";
    public const string SectionChanges = "changes";
    public const string SectionStream = "stream";

    public const string FieldOrderJson = "json";
    public const string FieldOrderJsonZlib = "json-zlib";

    public const string FieldOrderJsonBrotli = "json-brotli";
    public const string FieldOrderNofieldsSuffix = "-nofields";

    public const string TransformRaw = "raw";
    public const string TransformDelta = "delta";

    public sealed class FieldOrder
    {
        public required string Format { get; init; }
        public required int RecordVersion { get; init; }
        public required int TickCount { get; init; }
        public required List<Section> Sections { get; init; }

        public List<string> Fields { get; init; } = [];

        public required JsonNodeLite Raw { get; init; }
    }

    public sealed class Section
    {
        public required string Kind { get; init; }
        public string? Field { get; init; }
        public List<string>? Fields { get; init; }
        public string? Transform { get; init; }
        public string? FramesTransform { get; init; }
        public string? ValuesTransform { get; init; }
        public int Chunks { get; init; }
    }

    public sealed class TickColumns : IEnumerable<KeyValuePair<string, uint[]>>
    {
        private readonly Dictionary<string, uint[]> _columns;

        internal TickColumns(Dictionary<string, uint[]> columns) => _columns = columns;

        public int Count => _columns.Count;

        public IReadOnlyCollection<string> Names => _columns.Keys;

        public long WordCount
        {
            get { long n = 0; foreach (var kv in _columns) n += kv.Value.LongLength; return n; }
        }

        public uint[] this[string word]
        {
            get
            {
                if (_columns.TryGetValue(word, out var column)) return column;
                throw new KeyNotFoundException(Explain(word));
            }
        }

        public bool Has(string word) => _columns.ContainsKey(word);

        public IEnumerator<KeyValuePair<string, uint[]>> GetEnumerator() => _columns.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private string Explain(string word)
        {
            var near = _columns.Keys
                .Where(k => k.StartsWith(word, StringComparison.Ordinal)
                            || word.StartsWith(k, StringComparison.Ordinal)
                            || k.Contains(word, StringComparison.Ordinal))
                .OrderBy(k => k.Length)
                .Take(5)
                .ToArray();
            return "この窓に `" + word + "` という語は無い（入っているのは " + _columns.Count + " 列）"
                   + (near.Length > 0 ? "。近い綴り: " + string.Join(" / ", near) : "");
        }
    }


    public static FieldOrder UnpackFieldOrder(byte[] blob, string encoding)
    {
        bool rebuild = encoding.EndsWith(FieldOrderNofieldsSuffix, StringComparison.Ordinal);
        string basis = rebuild ? encoding[..^FieldOrderNofieldsSuffix.Length] : encoding;

        byte[] raw;
        if (basis == FieldOrderJsonZlib) raw = ZlibDecompress(blob, 0, blob.Length, 0);
        else if (basis == FieldOrderJsonBrotli) raw = UnpackJsonBrotli(blob);
        else if (basis == FieldOrderJson) raw = blob;
        else throw new InvalidDataException("未知の field_order 形式です: " + encoding);

        return ParseFieldOrder(raw, rebuild);
    }

    public static byte[] PackJsonBrotli(byte[] rawJson)
    {
        var packed = SolidBrotli.Compress(rawJson);
        var buf = new byte[4 + packed.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(buf, (uint)rawJson.Length);
        packed.CopyTo(buf.AsSpan(4));
        return buf;
    }

    public static byte[] UnpackJsonBrotli(byte[] blob)
    {
        if (blob.Length < 4)
            throw new InvalidDataException($"field_order（brotli）が短すぎます: {blob.Length} B");
        uint expected = BinaryPrimitives.ReadUInt32LittleEndian(blob);
        if (expected > int.MaxValue)
            throw new InvalidDataException($"field_order の展開後バイト数が大きすぎます: {expected}");
        return SolidBrotli.Decompress(blob.AsSpan(4), (int)expected);
    }

    public static FieldOrder ParseFieldOrder(string json) =>
        ParseFieldOrder(System.Text.Encoding.UTF8.GetBytes(json), rebuildFields: false);

    private static FieldOrder ParseFieldOrder(byte[] utf8, bool rebuildFields)
    {
        var root = JsonNodeLite.Parse(utf8);
        var sections = new List<Section>();
        foreach (var sec in root.Array("sections"))
        {
            List<string>? fields = null;
            if (sec.Has("fields"))
            {
                fields = [];
                foreach (var f in sec.Array("fields")) fields.Add(f.AsString());
            }
            sections.Add(new Section
            {
                Kind = sec.String("kind") ?? "",
                Field = sec.String("field"),
                Fields = fields,
                Transform = sec.String("transform"),
                FramesTransform = sec.String("frames_transform"),
                ValuesTransform = sec.String("values_transform"),
                Chunks = (int)sec.Int("chunks", 0),
            });
        }

        var fieldsTop = new List<string>();
        if (rebuildFields || !root.Has("fields"))
        {
            fieldsTop.AddRange(FieldsFromSections(sections));
            root = root.WithMember("fields", JsonNodeLite.FromStrings(fieldsTop));
        }
        else
        {
            foreach (var f in root.Array("fields")) fieldsTop.Add(f.AsString());
        }

        return new FieldOrder
        {
            Format = root.String("format") ?? "",
            RecordVersion = (int)root.Int("record_version", 0),
            TickCount = (int)root.Int("tick_count", 0),
            Sections = sections,
            Fields = fieldsTop,
            Raw = root,
        };
    }

    public static (byte[] Blob, string Encoding) PackFieldOrder(
        JsonNodeLite fieldOrder, List<Section> sections, bool compress = true, bool omitFields = false)
    {
        var node = fieldOrder;
        if (omitFields)
        {
            var rebuilt = FieldsFromSections(sections).ToList();
            var declared = node.Has("fields")
                ? node.Array("fields").Select(x => x.AsString()).ToList()
                : rebuilt;
            var a = rebuilt.OrderBy(x => x, StringComparer.Ordinal).ToList();
            var b = declared.OrderBy(x => x, StringComparer.Ordinal).ToList();
            if (!a.SequenceEqual(b, StringComparer.Ordinal))
                throw new InvalidDataException(
                    $"sections から列名を作り直せないので fields を落とせません（{declared.Count} 列中 {rebuilt.Count} 列しか拾えません）");
            node = node.WithoutMember("fields");
        }
        var raw = System.Text.Encoding.UTF8.GetBytes(node.ToCompactJson(escapeNonAscii: false));
        var suffix = omitFields ? FieldOrderNofieldsSuffix : "";
        if (!compress) return (raw, FieldOrderJson + suffix);
        return (ZlibCompress(raw, DefaultLevel), FieldOrderJsonZlib + suffix);
    }

    public static IEnumerable<string> FieldsFromSections(List<Section> sections)
    {
        foreach (var sec in sections)
        {
            if (sec.Fields is not null) { foreach (var f in sec.Fields) yield return f; }
            else if (sec.Field is not null) yield return sec.Field;
        }
    }


    private sealed class ChunkReader(byte[] blob)
    {
        private int _pos;

        public uint[] Next()
        {
            if (_pos + 8 > blob.Length)
                throw new InvalidDataException("BLOB が途中で終わっています（チャンク見出しが読めません）");
            uint rawLen = BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(_pos));
            uint compLen = BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(_pos + 4));
            int start = _pos + 8;
            long end = (long)start + compLen;
            if (end > blob.Length)
                throw new InvalidDataException("BLOB が途中で終わっています（チャンク本体が足りません）");
            var raw = ZlibDecompress(blob, start, (int)compLen, (int)rawLen);
            if (raw.Length != rawLen)
                throw new InvalidDataException("圧縮前バイト数が一致しません: " + raw.Length + "（記録は " + rawLen + "）");
            _pos = (int)end;
            return WordsOf(raw);
        }
    }

    public static uint[] WordsOf(byte[] raw)
    {
        if ((raw.Length & 3) != 0)
            throw new InvalidDataException("チャンクの長さが 4 の倍数ではありません: " + raw.Length);
        var col = new uint[raw.Length / 4];
        if (BitConverter.IsLittleEndian) Buffer.BlockCopy(raw, 0, col, 0, raw.Length);
        else for (int i = 0; i < col.Length; i++) col[i] = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(i * 4));
        return col;
    }

    private sealed class ChunkList(IReadOnlyList<byte[]> chunks)
    {
        private int _at;

        public uint[] Next()
        {
            if (_at >= chunks.Count)
                throw new InvalidDataException(
                    $"チャンクが足りません（{chunks.Count} 本しかないのに {_at + 1} 本目を求められた）");
            return WordsOf(chunks[_at++]);
        }

        public int Remaining => chunks.Count - _at;
    }

    public static TickColumns Decode(byte[] blob, string encoding, FieldOrder order, HashSet<string>? only = null)
    {
        if (encoding == Encoding) return DecodeColumns(blob, order, only);
        if (encoding == SolidBrotli.Encoding) return DecodeColumnsFromChunks(SolidBrotli.Unpack(blob), order, only);
        if (encoding == SolidBrotli.EncodingV2)
            return DecodeColumnsFromChunks(SolidBrotli.UnpackV2(blob), order, only);
        throw new InvalidDataException(
            $"未知の encoding です: {encoding}"
            + $"（対応は {Encoding} / {SolidBrotli.Encoding} / {SolidBrotli.EncodingV2}）");
    }

    internal static TickColumns DecodeColumns(byte[] blob, FieldOrder order, HashSet<string>? only = null)
        => DecodeCore(new ChunkReader(blob).Next, order, only);

    internal static TickColumns DecodeColumnsFromChunks(
        IReadOnlyList<byte[]> chunks, FieldOrder order, HashSet<string>? only = null)
    {
        var list = new ChunkList(chunks);
        var cols = DecodeCore(list.Next, order, only);
        if (list.Remaining != 0)
            throw new InvalidDataException(
                $"チャンクが余っています: {list.Remaining} 本（全部で {chunks.Count} 本）");
        return cols;
    }

    private static TickColumns DecodeCore(Func<uint[]> next, FieldOrder order, HashSet<string>? only)
    {
        if (order.Format != Encoding)
            throw new InvalidDataException("未知の形式です: " + order.Format + "（対応は " + Encoding + "）");

        int tickCount = order.TickCount;
        var outMap = new Dictionary<string, uint[]>(StringComparer.Ordinal);

        foreach (var sec in order.Sections)
        {
            switch (sec.Kind)
            {
                case SectionInvariant:
                {
                    var values = next();
                    var names = sec.Fields ?? [];
                    for (int i = 0; i < names.Count && i < values.Length; i++)
                    {
                        if (only is not null && !only.Contains(names[i])) continue;
                        var col = new uint[tickCount];
                        col.AsSpan().Fill(values[i]);
                        outMap[names[i]] = col;
                    }
                    break;
                }
                case SectionChanges:
                {
                    var frames = next();
                    var values = next();
                    var name = sec.Field ?? throw new InvalidDataException("changes 区画に field がありません");
                    if (only is not null && !only.Contains(name)) break;
                    if (sec.FramesTransform == TransformDelta) Undelta(frames);
                    if (sec.ValuesTransform == TransformDelta) Undelta(values);
                    var col = new uint[tickCount];
                    for (int k = 0; k < frames.Length; k++)
                    {
                        long start = frames[k];
                        long stop = k + 1 < frames.Length ? frames[k + 1] : tickCount;
                        if (start < 0 || stop > tickCount || stop < start || start > tickCount)
                            throw new InvalidDataException(
                                $"changes 区画の frames が範囲外です: {name} [{start},{stop}) / tick_count={tickCount}");
                        if (values.Length <= k)
                            throw new InvalidDataException($"changes 区画の values が足りません: {name}");
                        col.AsSpan((int)start, (int)(stop - start)).Fill(values[k]);
                    }
                    outMap[name] = col;
                    break;
                }
                case SectionStream:
                {
                    var col = next();
                    var name = sec.Field ?? throw new InvalidDataException("stream 区画に field がありません");
                    if (only is not null && !only.Contains(name)) break;
                    if (sec.Transform == TransformDelta) Undelta(col);
                    outMap[name] = col;
                    break;
                }
                default:
                    for (int i = 0; i < sec.Chunks; i++) next();
                    break;
            }
        }
        return new TickColumns(outMap);
    }

    public static void Undelta(uint[] col)
    {
        uint acc = 0;
        for (int i = 0; i < col.Length; i++)
        {
            acc = unchecked(acc + col[i]);
            col[i] = acc;
        }
    }

    public static uint[] Delta(uint[] col)
    {
        var outCol = new uint[col.Length];
        uint prev = 0;
        for (int i = 0; i < col.Length; i++)
        {
            outCol[i] = unchecked(col[i] - prev);
            prev = col[i];
        }
        return outCol;
    }

    public static float AsFloat(uint bits) => BitConverter.Int32BitsToSingle(unchecked((int)bits));


    public static byte[] ZlibCompress(byte[] data, int level)
    {
        if (data.Length == 0) return EmptyZlibStream(level);
        using var ms = new MemoryStream(Math.Max(64, data.Length / 3));
        using (var z = new ZLibStream(ms, new ZLibCompressionOptions { CompressionLevel = level }, leaveOpen: true))
            z.Write(data, 0, data.Length);
        return ms.ToArray();
    }

    private static byte[] EmptyZlibStream(int level)
    {
        using var probe = new MemoryStream();
        using (var z = new ZLibStream(probe, new ZLibCompressionOptions { CompressionLevel = level }, leaveOpen: true))
            z.Write([0], 0, 1);
        var head = probe.ToArray();
        return [head[0], head[1], 0x03, 0x00, 0x00, 0x00, 0x00, 0x01];
    }

    public static byte[] ZlibDecompress(byte[] data, int offset, int count, int expected)
    {
        using var input = new MemoryStream(data, offset, count, writable: false);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = expected > 0 ? new MemoryStream(expected) : new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    public static byte[] PackChunk(uint[] col, int level)
    {
        var raw = new byte[col.Length * 4];
        if (BitConverter.IsLittleEndian) Buffer.BlockCopy(col, 0, raw, 0, raw.Length);
        else for (int i = 0; i < col.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(i * 4), col[i]);
        var comp = ZlibCompress(raw, level);
        var outBuf = new byte[8 + comp.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(0), (uint)raw.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(4), (uint)comp.Length);
        Buffer.BlockCopy(comp, 0, outBuf, 8, comp.Length);
        return outBuf;
    }

    public static (byte[] Chunk, string Transform) PackBest(uint[] col, int level)
    {
        var plain = PackChunk(col, level);
        if (col.Length < 2) return (plain, TransformRaw);
        var diff = PackChunk(Delta(col), level);
        return diff.Length < plain.Length ? (diff, TransformDelta) : (plain, TransformRaw);
    }
}
