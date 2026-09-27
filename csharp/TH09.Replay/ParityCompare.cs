using System.Text;
using System.Text.Json;

namespace TH09.Replay;

public static class ParityCompare
{
    private static readonly string[] IgnoredValueKeys = ["error"];

    public sealed class Report
    {
        public int Total;
        public int Matched;
        public readonly List<string> Problems = [];
        public bool Ok => Total > 0 && Matched == Total && Problems.Count == 0;
    }

    public static Report CompareFiles(string pythonPath, string csharpPath)
        => CompareLines(File.ReadAllLines(pythonPath, Encoding.UTF8),
                        File.ReadAllLines(csharpPath, Encoding.UTF8));

    public static Report CompareLines(IEnumerable<string> pythonLines, IEnumerable<string> csharpLines)
    {
        var rep = new Report();
        var py = Index(pythonLines, "python", rep);
        var cs = Index(csharpLines, "csharp", rep);

        foreach (var key in py.Keys)
            if (!cs.ContainsKey(key)) rep.Problems.Add($"C# 側に無い: {key}");
        foreach (var key in cs.Keys)
            if (!py.ContainsKey(key)) rep.Problems.Add($"Python 側に無い: {key}");

        foreach (var (key, pdoc) in py)
        {
            if (!cs.TryGetValue(key, out var cdoc)) continue;
            rep.Total++;
            var diffs = new List<string>();
            DiffElement("", pdoc.RootElement, cdoc.RootElement, diffs);
            if (diffs.Count == 0) rep.Matched++;
            else rep.Problems.Add($"不一致: {key}\n    " + string.Join("\n    ", diffs.Take(12)));
        }

        foreach (var d in py.Values) d.Dispose();
        foreach (var d in cs.Values) d.Dispose();
        return rep;
    }

    private static Dictionary<string, JsonDocument> Index(IEnumerable<string> lines, string side, Report rep)
    {
        var map = new Dictionary<string, JsonDocument>(StringComparer.Ordinal);
        int n = 0;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            n++;
            JsonDocument doc;
            try { doc = JsonDocument.Parse(line); }
            catch (JsonException e) { rep.Problems.Add($"{side} の {n} 行目が JSON として読めない: {e.Message}"); continue; }
            if (!doc.RootElement.TryGetProperty("path", out var p) || p.ValueKind != JsonValueKind.String)
            {
                rep.Problems.Add($"{side} の {n} 行目に path がない");
                doc.Dispose();
                continue;
            }
            var key = p.GetString()!;
            if (map.ContainsKey(key))
            {
                rep.Problems.Add($"{side} に同じ path が 2 度出る: {key}");
                doc.Dispose();
                continue;
            }
            map[key] = doc;
        }
        return map;
    }

    private static void DiffElement(string path, JsonElement a, JsonElement b, List<string> outDiffs)
    {
        if (outDiffs.Count > 200) return;
        if (a.ValueKind != b.ValueKind)
        {
            outDiffs.Add($"{Label(path)}: 型が違う python={a.ValueKind} csharp={b.ValueKind}");
            return;
        }
        switch (a.ValueKind)
        {
            case JsonValueKind.Object:
                var akeys = a.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
                var bkeys = b.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
                foreach (var k in akeys.Except(bkeys)) outDiffs.Add($"{Label(path)}: C# 側に鍵が無い {k}");
                foreach (var k in bkeys.Except(akeys)) outDiffs.Add($"{Label(path)}: Python 側に鍵が無い {k}");
                foreach (var k in akeys.Intersect(bkeys).OrderBy(s => s, StringComparer.Ordinal))
                {
                    if (IgnoredValueKeys.Contains(k)) continue;
                    DiffElement(path.Length == 0 ? k : path + "." + k, a.GetProperty(k), b.GetProperty(k), outDiffs);
                }
                break;

            case JsonValueKind.Array:
                var av = a.EnumerateArray().ToList();
                var bv = b.EnumerateArray().ToList();
                if (av.Count != bv.Count)
                {
                    outDiffs.Add($"{Label(path)}: 要素数が違う python={av.Count} csharp={bv.Count}");
                    break;
                }
                for (int i = 0; i < av.Count; i++)
                    DiffElement($"{path}[{i}]", av[i], bv[i], outDiffs);
                break;

            case JsonValueKind.String:
                if (!string.Equals(a.GetString(), b.GetString(), StringComparison.Ordinal))
                    outDiffs.Add($"{Label(path)}: python={Quote(a.GetString())} csharp={Quote(b.GetString())}");
                break;

            case JsonValueKind.Number:
                if (!string.Equals(a.GetRawText(), b.GetRawText(), StringComparison.Ordinal))
                    outDiffs.Add($"{Label(path)}: python={a.GetRawText()} csharp={b.GetRawText()}");
                break;

            default:
                break;
        }
    }

    private static string Label(string path) => path.Length == 0 ? "(根)" : path;

    private static string Quote(string? s) => s is null ? "null" : "\"" + s.Replace("\"", "\\\"") + "\"";
}
