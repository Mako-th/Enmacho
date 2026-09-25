namespace TH09.Layer0;

public static class TickEncoder
{
    public sealed class SectionPolicy
    {
        public required HashSet<string> InvariantHint { get; init; }
        public required HashSet<string> ChangeHint { get; init; }

        public bool IsStair(string name) => ChangeHint.Contains(name) || InvariantHint.Contains(name);

        public static SectionPolicy Empty => new()
        {
            InvariantHint = new HashSet<string>(StringComparer.Ordinal),
            ChangeHint = new HashSet<string>(StringComparer.Ordinal),
        };

        public static SectionPolicy Load(string path)
        {
            var inv = new HashSet<string>(StringComparer.Ordinal);
            var chg = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in File.ReadLines(path))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                var tab = line.IndexOf('\t');
                if (tab < 0) throw new InvalidDataException("表の行が読めません: " + line);
                var name = line[(tab + 1)..];
                switch (line[..tab])
                {
                    case "I": inv.Add(name); break;
                    case "C": chg.Add(name); break;
                    default: throw new InvalidDataException("表の種別が読めません: " + line);
                }
            }
            if (inv.Count == 0 && chg.Count == 0)
                throw new InvalidDataException("表が 0 語です: " + path);
            return new SectionPolicy { InvariantHint = inv, ChangeHint = chg };
        }
    }

    public sealed record Encoded(byte[] Blob, JsonNodeLite FieldOrder,
                                 List<TickArchive.Section> Sections, long UncompressedBytes);

    public static (uint[] Frames, uint[] Values) ChangesOf(uint[] col)
    {
        var frames = new List<uint>();
        var values = new List<uint>();
        bool first = true;
        uint prev = 0;
        for (int i = 0; i < col.Length; i++)
        {
            if (first || col[i] != prev)
            {
                frames.Add((uint)i);
                values.Add(col[i]);
                prev = col[i];
                first = false;
            }
        }
        return (frames.ToArray(), values.ToArray());
    }

    public static Encoded EncodeColumns(
        IReadOnlyDictionary<string, uint[]> columns, IReadOnlyList<string>? fields,
        SectionPolicy policy, int recordVersion, int level = TickArchive.DefaultLevel)
    {
        var order = fields is not null ? [.. fields] : columns.Keys.ToList();
        int tickCount = order.Count > 0 ? columns[order[0]].Length : 0;
        foreach (var name in order)
        {
            if (columns[name].Length != tickCount)
                throw new InvalidDataException(
                    $"列 {name} の長さが揃っていません（{columns[name].Length} / 期待 {tickCount}）");
        }

        var invariantNames = new List<string>();
        var invariantValues = new List<uint>();
        var stair = new List<(string Name, uint[] Frames, uint[] Values)>();
        var stream = new List<(string Name, uint[] Col)>();

        foreach (var name in order)
        {
            var col = columns[name];
            var (frames, values) = ChangesOf(col);
            if (values.Length <= 1)
            {
                invariantNames.Add(name);
                invariantValues.Add(values.Length > 0 ? values[0] : 0u);
            }
            else if (policy.IsStair(name))
            {
                stair.Add((name, frames, values));
            }
            else
            {
                stream.Add((name, col));
            }
        }

        var parts = new List<byte[]>();
        var sections = new List<TickArchive.Section>();
        var secNodes = new List<JsonNodeLite>();

        long rawBytes = (long)order.Count * tickCount * 4;

        parts.Add(TickArchive.PackChunk(invariantValues.ToArray(), level));
        sections.Add(new TickArchive.Section
        {
            Kind = TickArchive.SectionInvariant, Fields = invariantNames,
            Transform = TickArchive.TransformRaw, Chunks = 1,
        });
        secNodes.Add(JsonNodeLite.FromObject(
        [
            new("kind", JsonNodeLite.FromString(TickArchive.SectionInvariant)),
            new("fields", JsonNodeLite.FromStrings(invariantNames)),
            new("transform", JsonNodeLite.FromString(TickArchive.TransformRaw)),
            new("chunks", JsonNodeLite.FromInt(1)),
        ]));

        foreach (var (name, frames, values) in stair)
        {
            var (fchunk, ftrans) = TickArchive.PackBest(frames, level);
            var (vchunk, vtrans) = TickArchive.PackBest(values, level);
            parts.Add(fchunk);
            parts.Add(vchunk);
            sections.Add(new TickArchive.Section
            {
                Kind = TickArchive.SectionChanges, Field = name,
                FramesTransform = ftrans, ValuesTransform = vtrans, Chunks = 2,
            });
            secNodes.Add(JsonNodeLite.FromObject(
            [
                new("kind", JsonNodeLite.FromString(TickArchive.SectionChanges)),
                new("field", JsonNodeLite.FromString(name)),
                new("count", JsonNodeLite.FromInt(frames.Length)),
                new("frames_transform", JsonNodeLite.FromString(ftrans)),
                new("values_transform", JsonNodeLite.FromString(vtrans)),
                new("chunks", JsonNodeLite.FromInt(2)),
            ]));
        }

        foreach (var (name, col) in stream)
        {
            var (chunk, trans) = TickArchive.PackBest(col, level);
            parts.Add(chunk);
            sections.Add(new TickArchive.Section
            {
                Kind = TickArchive.SectionStream, Field = name, Transform = trans, Chunks = 1,
            });
            secNodes.Add(JsonNodeLite.FromObject(
            [
                new("kind", JsonNodeLite.FromString(TickArchive.SectionStream)),
                new("field", JsonNodeLite.FromString(name)),
                new("transform", JsonNodeLite.FromString(trans)),
                new("chunks", JsonNodeLite.FromInt(1)),
            ]));
        }

        var fieldOrder = JsonNodeLite.FromObject(
        [
            new("format", JsonNodeLite.FromString(TickArchive.Encoding)),
            new("record_version", JsonNodeLite.FromInt(recordVersion)),
            new("tick_count", JsonNodeLite.FromInt(tickCount)),
            new("fields", JsonNodeLite.FromStrings(order)),
            new("sections", JsonNodeLite.FromArray(secNodes)),
        ]);

        int total = 0;
        foreach (var p in parts) total += p.Length;
        var blob = new byte[total];
        int pos = 0;
        foreach (var p in parts) { Buffer.BlockCopy(p, 0, blob, pos, p.Length); pos += p.Length; }

        return new Encoded(blob, fieldOrder, sections, rawBytes);
    }
}
