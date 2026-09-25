namespace TH09.Analysis;

public static class TimelineDecode
{
    public readonly record struct Spec(uint FlagMask, char Kind);

    private static readonly Dictionary<string, Spec> Specs = BuildSpecs();

    public static readonly string[] RecordFields = Packed.Lines(AnalysisTables.RecordFieldsPacked);

    private static Dictionary<string, Spec> BuildSpecs()
    {
        var map = new Dictionary<string, Spec>(StringComparer.Ordinal);
        foreach (var line in Packed.Lines(AnalysisTables.DecodeSpecPacked))
        {
            var f = line.Split('\t');
            if (f.Length != 3) throw new InvalidDataException("段階 6 の表の形が違う: " + line);
            map[f[0]] = new Spec(uint.Parse(f[1]), f[2] switch
            {
                "f32" => 'f',
                "s32" => 's',
                "u32" => 'u',
                _ => throw new InvalidDataException("知らない種別: " + f[2]),
            });
        }
        return map;
    }

    public static Spec SpecOf(string name) =>
        Specs.TryGetValue(name, out var s) ? s
        : throw new KeyNotFoundException($"主リングにそんな語はありません: {name}"
                                         + $"（似た綴り: {Nearest(name)}）");

    private static string Nearest(string name)
    {
        var hits = RecordFields.Where(n => n.Contains(name, StringComparison.Ordinal)
                                        || name.Contains(n, StringComparison.Ordinal)).Take(3).ToList();
        return hits.Count == 0 ? "なし" : string.Join(" / ", hits);
    }

    public static int SpecCount => Specs.Count;

    public static TickValue Decode(string name, uint raw, uint flags) => Decode(SpecOf(name), raw, flags);

    public static TickValue Decode(in Spec spec, uint raw, uint flags)
    {
        if (spec.FlagMask != 0 && (flags & spec.FlagMask) == 0) return TickValue.Missing;
        if (spec.Kind == 'f') return TickValue.Float(BitConverter.UInt32BitsToSingle(raw));
        if (spec.Kind == 's')
        {
            int v = unchecked((int)raw);
            return v == AnalysisTables.Sentinel ? TickValue.Missing : TickValue.Int(v);
        }
        return TickValue.Int(raw);
    }
}
