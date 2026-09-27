namespace TH09.Analysis;

internal static class Packed
{
    public static string[] Lines(string text)
    {
        var raw = text.Split('\n');
        var outList = new List<string>(raw.Length);
        foreach (var line in raw)
        {
            var t = line.TrimEnd('\r');
            if (t.Length > 0) outList.Add(t);
        }
        return outList.ToArray();
    }
}
