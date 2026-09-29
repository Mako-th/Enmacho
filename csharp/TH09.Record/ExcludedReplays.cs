using System.Text.Json;

namespace TH09.Record;

public sealed record ExcludedReplayEntry(string Sha256, string Path, string ExcludedAt)
{
    public string FileName
    {
        get
        {
            var trimmed = Path.TrimEnd('\\', '/');
            var i = trimmed.LastIndexOfAny(['\\', '/']);
            return i >= 0 ? trimmed[(i + 1)..] : trimmed;
        }
    }

    public string ExcludedDay => ExcludedAt.Length >= 10 ? ExcludedAt[..10] : ExcludedAt;
}

public static class ExcludedReplays
{
    public const string Key = "excluded_replays";

    public const string Sha256Field = "sha256";

    public const string PathField = "path";

    public const string ExcludedAtField = "excluded_at";

    public static (IReadOnlyList<ExcludedReplayEntry> Entries, IReadOnlyList<SettingNote> Notes) Load(
        string path)
    {
        var notes = new List<SettingNote>();
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(File.ReadAllBytes(path));
        }
        catch (Exception)
        {
            return ([], notes);
        }
        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty(Key, out var arr))
            {
                notes.Add(new SettingNote(Key, ConfigStore.NoteMissing,
                                          Key + " は書かれていないので、除外は 0 件です"));
                return ([], notes);
            }
            if (arr.ValueKind != JsonValueKind.Array)
            {
                notes.Add(new SettingNote(Key, ConfigStore.NoteType,
                                          Key + " が配列ではないので読み飛ばしました"));
                return ([], notes);
            }
            var entries = new List<ExcludedReplayEntry>();
            var i = 0;
            foreach (var item in arr.EnumerateArray())
            {
                var label = Key + "[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
                i++;
                if (item.ValueKind != JsonValueKind.Object
                    || Str(item, Sha256Field) is not { Length: > 0 } sha)
                {
                    notes.Add(new SettingNote(label, ConfigStore.NoteType,
                                              label + " は " + Sha256Field + " を持つ {…} ではないので読み飛ばしました"));
                    continue;
                }
                entries.Add(new ExcludedReplayEntry(sha, Str(item, PathField) ?? "",
                                                    Str(item, ExcludedAtField) ?? ""));
            }
            return (entries, notes);
        }
    }

    public static IReadOnlySet<string> ShaSet(string path)
        => new HashSet<string>(Load(path).Entries.Select(e => e.Sha256.ToLowerInvariant()),
                               StringComparer.Ordinal);

    public static Func<IReadOnlySet<string>> Watcher(string path)
    {
        (DateTime Stamp, long Size)? seen = null;
        IReadOnlySet<string> cached = new HashSet<string>(StringComparer.Ordinal);
        return () =>
        {
            (DateTime, long)? now = null;
            try
            {
                var info = new FileInfo(path);
                if (info.Exists) now = (info.LastWriteTimeUtc, info.Length);
            }
            catch (Exception)
            {
                now = null;
            }
            if (now is null) { seen = null; cached = new HashSet<string>(StringComparer.Ordinal); return cached; }
            if (seen != now) { cached = ShaSet(path); seen = now; }
            return cached;
        };
    }

    private static string? Str(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    internal static void Write(Utf8JsonWriter w, string key, IReadOnlyList<ExcludedReplayEntry> entries)
    {
        w.WriteStartArray(key);
        foreach (var e in entries)
        {
            w.WriteStartObject();
            w.WriteString(Sha256Field, e.Sha256);
            w.WriteString(PathField, e.Path);
            w.WriteString(ExcludedAtField, e.ExcludedAt);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }
}
