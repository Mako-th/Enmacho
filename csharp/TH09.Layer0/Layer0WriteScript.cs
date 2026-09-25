using System.Globalization;

namespace TH09.Layer0;

public static class Layer0WriteScript
{
    public sealed record Script(List<Layer0Writer.FieldSlot> Fields, int WordCount,
                                List<string[]> Commands);

    public sealed record Result(long Ticks, long Adds, long Begins, long Breaks, long Ends);

    public static Script Load(string path)
    {
        var fields = new List<Layer0Writer.FieldSlot>();
        var commands = new List<string[]>();
        int wordCount = 0;
        int lineNo = 0;

        foreach (var raw in File.ReadLines(path))
        {
            lineNo++;
            int hash = raw.IndexOf('#');
            var line = (hash >= 0 ? raw[..hash] : raw).Trim();
            if (line.Length == 0) continue;
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            switch (parts[0])
            {
                case "words":
                    Need(parts, 2, lineNo);
                    wordCount = int.Parse(parts[1], CultureInfo.InvariantCulture);
                    break;
                case "field":
                    Need(parts, 3, lineNo);
                    fields.Add(new Layer0Writer.FieldSlot(
                        parts[1], int.Parse(parts[2], CultureInfo.InvariantCulture)));
                    break;
                case "begin":
                case "lost":
                case "torn":
                    Need(parts, 2, lineNo);
                    commands.Add(parts);
                    break;
                case "tick":
                case "add":
                case "break":
                case "end":
                    commands.Add(parts);
                    break;
                default:
                    throw new InvalidDataException(
                        $"知らない命令です（{lineNo} 行目）: {parts[0]}");
            }
        }

        if (fields.Count == 0)
            throw new InvalidDataException("台本に列が 1 本もありません: " + path);
        if (wordCount <= 0)
            throw new InvalidDataException("台本に `words` がありません: " + path);
        return new Script(fields, wordCount, commands);
    }

    public static Result Run(Script script, Layer0Writer writer)
    {
        var batch = new List<uint[]>();
        long lost = 0, torn = 0;
        long ticks = 0, adds = 0, begins = 0, breaks = 0, ends = 0;

        void Submit()
        {
            if (batch.Count == 0) return;
            writer.Add(batch, lost, torn);
            batch = [];
            adds++;
        }

        foreach (var parts in script.Commands)
        {
            switch (parts[0])
            {
                case "tick":
                    if (parts.Length - 1 != script.WordCount)
                        throw new InvalidDataException(
                            $"tick の語数が {parts.Length - 1}（`words` は {script.WordCount}）");
                    var words = new uint[script.WordCount];
                    for (int i = 0; i < words.Length; i++)
                        words[i] = uint.Parse(parts[i + 1], CultureInfo.InvariantCulture);
                    batch.Add(words);
                    ticks++;
                    break;
                case "add":
                    Submit();
                    break;
                case "lost":
                    Submit();
                    lost = long.Parse(parts[1], CultureInfo.InvariantCulture);
                    break;
                case "torn":
                    Submit();
                    torn = long.Parse(parts[1], CultureInfo.InvariantCulture);
                    break;
                case "begin":
                    Submit();
                    writer.BeginSession(long.Parse(parts[1], CultureInfo.InvariantCulture));
                    begins++;
                    break;
                case "break":
                    Submit();
                    writer.BreakSegment(parts.Length > 1 ? string.Join(' ', parts[1..]) : "");
                    breaks++;
                    break;
                case "end":
                    Submit();
                    writer.EndSession();
                    ends++;
                    break;
            }
        }
        Submit();
        return new Result(ticks, adds, begins, breaks, ends);
    }

    private static void Need(string[] parts, int n, int lineNo)
    {
        if (parts.Length < n)
            throw new InvalidDataException($"引数が足りません（{lineNo} 行目）: {string.Join(' ', parts)}");
    }
}
