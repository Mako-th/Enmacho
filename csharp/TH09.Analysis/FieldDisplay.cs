namespace TH09.Analysis;

public static class FieldDisplay
{
    private static readonly Dictionary<string, string> Kinds = BuildKinds();
    private static readonly Dictionary<string, double> Scales = BuildScales();

    public static readonly HashSet<string> SharedFields =
        new(Packed.Lines(AnalysisTables.SharedFieldsPacked), StringComparer.Ordinal);

    private static Dictionary<string, string> BuildKinds()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in Packed.Lines(AnalysisTables.DisplayKindsPacked))
        {
            var f = line.Split('\t');
            map[f[0]] = f[1];
        }
        return map;
    }

    private static Dictionary<string, double> BuildScales()
    {
        var map = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var line in Packed.Lines(AnalysisTables.DisplayScalePacked))
        {
            var f = line.Split('\t');
            map[f[0]] = double.Parse(f[1], System.Globalization.CultureInfo.InvariantCulture);
        }
        return map;
    }

    public static int Count => Kinds.Count;

    public static string Kind(string name) => Kinds.TryGetValue(name, out var k) ? k : "NONE";

    public static List<string> NamesOf(params string[] kinds)
    {
        var want = new HashSet<string>(kinds, StringComparer.Ordinal);
        return Packed.Lines(AnalysisTables.DisplayKindsPacked)
                     .Select(l => l.Split('\t'))
                     .Where(f => want.Contains(f[1]))
                     .Select(f => f[0]).ToList();
    }

    public static double? Scale(string name) => Scales.TryGetValue(name, out var s) ? s : null;

    public static string PairBase(string name) =>
        name.Length >= 3 && (name.StartsWith("p1_", StringComparison.Ordinal)
                             || name.StartsWith("p2_", StringComparison.Ordinal))
            ? name[3..] : name;

    public static int SideOf(string name) =>
        name.StartsWith("p1_", StringComparison.Ordinal) ? 1
        : name.StartsWith("p2_", StringComparison.Ordinal) ? 2 : 0;
}
