using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TH09.Replay;

public static class ParityJson
{
    public static List<string> ReadCorpus(string path)
    {
        var list = new List<string>();
        foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
        {
            var line = raw.TrimEnd('\r', '\n');
            if (line.Length == 0 || line[0] == '#') continue;
            list.Add(line);
        }
        return list;
    }

    public static string? Sha256Hex(string path)
    {
        try
        {
            return Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static int Dump(IReadOnlyList<string> corpus, string outPath)
    {
        using var stream = File.Create(outPath);
        var opts = new JsonWriterOptions { Indented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        using var w = new Utf8JsonWriter(stream, opts);
        foreach (var path in corpus)
        {
            var result = ReplayDecode.DecodeReplay(path);
            WriteRecord(w, path, Sha256Hex(path), result);
            stream.WriteByte((byte)'\n');
            w.Reset();
        }
        return corpus.Count;
    }

    public static void WriteRecord(Utf8JsonWriter w, string path, string? sha256, ReplayResult r)
    {
        w.WriteStartObject();
        w.WriteString("path", path);
        if (sha256 is null) w.WriteNull("sha256"); else w.WriteString("sha256", sha256);
        w.WritePropertyName("result");
        WriteResult(w, r);
        w.WriteEndObject();
        w.Flush();
    }

    public static void WriteResult(Utf8JsonWriter w, ReplayResult r)
    {
        w.WriteStartObject();
        w.WriteString("status", r.Status);
        w.WritePropertyName("stages");
        w.WriteStartArray();
        foreach (var s in r.Stages)
        {
            w.WriteStartObject();
            w.WriteNumber("index", s.Index);
            WriteNullableNumber(w, "score", s.Score);
            WriteNullableNumber(w, "shot", s.Shot);
            w.WriteBoolean("ai", s.Ai);
            w.WriteNumber("lives", s.Lives);
            w.WriteNumber("pair", s.Pair);
            WriteNullableNumber(w, "rng_seed", s.RngSeed);
            WriteNullableNumber(w, "field_id", s.FieldId);
            WriteNullableNumber(w, "opponent", s.Opponent);
            w.WriteEndObject();
        }
        w.WriteEndArray();
        if (r.Decoded)
        {
            w.WriteString("name", r.Name);
            w.WriteString("date", r.Date);
            WriteNullableNumber(w, "difficulty", r.Difficulty);
            WriteNullableNumber(w, "mode", r.Mode);
            WriteNullableNumber(w, "p1_char", r.P1Char);
            WriteNullableNumber(w, "p2_char", r.P2Char);
            WriteNullableString(w, "p1_name", r.P1Name);
            WriteNullableString(w, "p2_name", r.P2Name);
        }
        else
        {
            w.WriteString("error", r.Error ?? "");
        }
        w.WriteEndObject();
    }

    private static void WriteNullableNumber(Utf8JsonWriter w, string name, long? v)
    {
        if (v is null) w.WriteNull(name); else w.WriteNumber(name, v.Value);
    }

    private static void WriteNullableString(Utf8JsonWriter w, string name, string? v)
    {
        if (v is null) w.WriteNull(name); else w.WriteString(name, v);
    }

    public static string ResultToJson(ReplayResult r)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            WriteResult(w, r);
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
