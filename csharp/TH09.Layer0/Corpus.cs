using System.Text;

namespace TH09.Layer0;

public sealed class Corpus
{
    public const string Version = "th09-layer0-corpus-v1";

    public enum Kind { Ticks, Window }

    public readonly record struct Item(Kind Kind, long SessionId, long No);

    public required string DbPath { get; init; }
    public required List<Item> Items { get; init; }

    public int TicksCount => Items.Count(x => x.Kind == Kind.Ticks);
    public int WindowCount => Items.Count(x => x.Kind == Kind.Window);

    public static Corpus Load(string path)
    {
        string? db = null;
        var items = new List<Item>();
        foreach (var raw in File.ReadLines(path, Encoding.UTF8))
        {
            var line = raw.TrimEnd('\r', '\n');
            if (line.Length == 0 || line[0] == '#') continue;
            var f = line.Split('\t');
            if (f[0] == "@db") { db = f[1]; continue; }
            var kind = f[0] switch
            {
                "T" => Kind.Ticks,
                "W" => Kind.Window,
                _ => throw new InvalidDataException("corpus の種別が読めません: " + line),
            };
            items.Add(new Item(kind, long.Parse(f[1]), long.Parse(f[2])));
        }
        if (db is null) throw new InvalidDataException("corpus に @db の行がありません（DB の場所は Python 側が決める）");
        return new Corpus { DbPath = db, Items = items };
    }
}
