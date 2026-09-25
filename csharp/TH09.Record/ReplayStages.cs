using System.Globalization;

namespace TH09.Record;

public enum MatchSides
{
    HumanVsHuman = 0,

    HumanVsCpu = 1,

    CpuVsHuman = 2,

    CpuVsCpu = 3,
}

public static class ReplayStages
{
    public const long MissingIndex = 99;

    public const long P1IndexLimit = 10;

    public sealed record P1Stage(long Index, CanonJson.Node Node)
    {
        public CanonJson.Node? Score =>
            Node.Members.TryGetValue("score", out var v) ? v : null;
    }

    private static readonly CanonJson.Node EmptyObject =
        CanonJson.Node.FromObject(new Dictionary<string, CanonJson.Node>(StringComparer.Ordinal));

    private static readonly CanonJson.Node EmptyArray = CanonJson.Node.FromArray([]);


    public static CanonJson.Node Parse(string? decodedJson)
    {
        var text = string.IsNullOrEmpty(decodedJson) ? "{}" : decodedJson;
        if (!CanonJson.TryParse(text, out var node))
            throw new InvalidDataException(
                "decoded_json が JSON として読めない（原本も json.loads がここで落ちる）");
        return node;
    }


    public static IReadOnlyList<P1Stage> RawP1Stages(CanonJson.Node decoded)
    {
        var root = Truthy(decoded) ? decoded : EmptyObject;
        if (root.Kind != CanonJson.Kind.Object)
            throw new InvalidDataException(
                "decoded_json の中身がオブジェクトでない（原本は .get() で AttributeError）");

        var stages = root.Members.TryGetValue("stages", out var v) ? v : EmptyArray;
        if (stages.Kind != CanonJson.Kind.Array)
            throw new InvalidDataException(
                "decoded_json の stages が配列でない（原本は for が回らずに落ちる）");

        var picked = new List<P1Stage>();
        foreach (var s in stages.Items)
        {
            var index = IndexOf(s);
            if (index < P1IndexLimit) picked.Add(new P1Stage(index, s));
        }
        return picked.OrderBy(x => x.Index).ToList();
    }

    public static IReadOnlyList<P1Stage> RawP1Stages(string? decodedJson) =>
        RawP1Stages(Parse(decodedJson));

    public static IReadOnlyList<P1Stage> P1Stages(CanonJson.Node decoded) =>
        DropEmptyHead(RawP1Stages(decoded));

    public static IReadOnlyList<P1Stage> P1Stages(string? decodedJson) =>
        P1Stages(Parse(decodedJson));


    public const long MatchP1Index = 9;

    public const long MatchP2Index = 19;

    public static (bool P1, bool P2)? MatchAi(CanonJson.Node decoded)
    {
        var root = Truthy(decoded) ? decoded : EmptyObject;
        if (root.Kind != CanonJson.Kind.Object) return null;
        var stages = root.Members.TryGetValue("stages", out var v) ? v : EmptyArray;
        if (stages.Kind != CanonJson.Kind.Array) return null;

        bool? p1 = null, p2 = null;
        foreach (var s in stages.Items)
        {
            if (s.Kind != CanonJson.Kind.Object) continue;
            if (!s.Members.TryGetValue("index", out var idxNode)
                || idxNode.Kind != CanonJson.Kind.Int
                || !long.TryParse(idxNode.Text, NumberStyles.AllowLeadingSign,
                                  CultureInfo.InvariantCulture, out var index)) continue;
            if (index != MatchP1Index && index != MatchP2Index) continue;
            if (!s.Members.TryGetValue("ai", out var ai) || ai.Kind != CanonJson.Kind.Bool) continue;
            if (index == MatchP1Index) p1 = ai.Bool; else p2 = ai.Bool;
        }
        return p1 is bool a && p2 is bool b ? (a, b) : null;
    }

    public static (bool P1, bool P2)? MatchAi(string? decodedJson)
        => CanonJson.TryParse(string.IsNullOrEmpty(decodedJson) ? "{}" : decodedJson, out var node)
               ? MatchAi(node)
               : null;

    public static MatchSides? MatchSidesOf(CanonJson.Node decoded)
        => MatchAi(decoded) is { } ai
               ? (ai.P1, ai.P2) switch
               {
                   (false, false) => MatchSides.HumanVsHuman,
                   (false, true) => MatchSides.HumanVsCpu,
                   (true, false) => MatchSides.CpuVsHuman,
                   (true, true) => MatchSides.CpuVsCpu,
               }
               : null;

    public static MatchSides? MatchSidesOf(string? decodedJson)
        => CanonJson.TryParse(string.IsNullOrEmpty(decodedJson) ? "{}" : decodedJson, out var node)
               ? MatchSidesOf(node)
               : null;

    public static IReadOnlyList<P1Stage> DropEmptyHead(IReadOnlyList<P1Stage> raw)
    {
        var first = -1;
        for (var i = 0; i < raw.Count; i++)
        {
            if (raw[i].Score is { } score && Truthy(score)) { first = i; break; }
        }
        if (first <= 1) return raw;
        return raw.Skip(first).ToList();
    }

    public static bool HasEmptyStages(CanonJson.Node decoded)
    {
        var raw = RawP1Stages(decoded);
        return raw.Count != DropEmptyHead(raw).Count;
    }

    public static bool HasEmptyStages(string? decodedJson) => HasEmptyStages(Parse(decodedJson));


    public static long IndexOf(CanonJson.Node stage)
    {
        if (stage.Kind != CanonJson.Kind.Object)
            throw new InvalidDataException(
                "stages の要素がオブジェクトでない（原本は .get() で AttributeError）");
        return stage.Members.TryGetValue("index", out var v) ? AsPythonInt(v, "index") : MissingIndex;
    }

    public static long AsPythonInt(CanonJson.Node node, string what)
    {
        var text = IntText(node, what);
        if (long.TryParse(text, NumberStyles.AllowLeadingSign,
                          CultureInfo.InvariantCulture, out var v)) return v;
        throw new OverflowException($"{what} が 64bit に収まらない: {text}");
    }

    private static string IntText(CanonJson.Node node, string what)
    {
        if (node.Kind == CanonJson.Kind.Bool) return node.Bool ? "1" : "0";
        if (node.Kind != CanonJson.Kind.Int)
            throw new InvalidDataException(
                $"{what} が整数でない（原本は比較で TypeError）: {CanonJson.Canon(node)}");
        return node.Text;
    }

    public static bool Truthy(CanonJson.Node node) => node.Kind switch
    {
        CanonJson.Kind.Null => false,
        CanonJson.Kind.Bool => node.Bool,
        CanonJson.Kind.Int => node.Text is not ("0" or "-0"),
        CanonJson.Kind.Float => node.Float != 0.0,
        CanonJson.Kind.String => node.Text.Length > 0,
        CanonJson.Kind.Array => node.Items.Count > 0,
        _ => node.Members.Count > 0,
    };
}
