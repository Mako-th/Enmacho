using System.Globalization;
using System.Text;

namespace TH09.Record;

public sealed class RecordCorpus
{
    public const string Version = "th09-record-corpus-v1";

    public required string Layer0Path { get; init; }

    public required List<long> Sessions { get; init; }

    public required List<string> Why { get; init; }

    public static RecordCorpus Load(string path)
    {
        string? db = null;
        var sessions = new List<long>();
        var why = new List<string>();
        foreach (var raw in File.ReadLines(path, Encoding.UTF8))
        {
            var line = raw.TrimEnd('\r', '\n');
            if (line.Length == 0 || line[0] == '#') continue;
            var f = line.Split('\t');
            if (f[0] == "@layer0") { db = f[1]; continue; }
            if (f[0] != "S") throw new InvalidDataException("corpus の種別が読めません: " + line);
            sessions.Add(long.Parse(f[1], CultureInfo.InvariantCulture));
            why.Add(f.Length > 2 ? f[2] : "");
        }
        if (db is null)
            throw new InvalidDataException("corpus に @layer0 の行がありません（DB の場所は Python 側が決める）");
        if (sessions.Count == 0)
            throw new InvalidDataException("corpus に流すセッションが 1 つもありません（0 件は『測れていない』）");
        return new RecordCorpus { Layer0Path = db, Sessions = sessions, Why = why };
    }
}
