using System.Text.Json;
using System.Text.Json.Nodes;

namespace TH09.Shell.Data;

internal static class UiSettings
{
    public const string FileName = "ui_settings.json";

    public const string HitWindowSection = "hitwindow";

    private static string? _path;

    public static string? Path_
    {
        get
        {
            if (_path is not null) return _path;
            try
            {
                var root = TH09.Record.Paths.Default.DataRoot;
                if (string.IsNullOrEmpty(root)) return null;
                _path = System.IO.Path.Combine(root, FileName);
                return _path;
            }
            catch { return null; }
        }
    }

    public static Dictionary<string, string> Load(string section)
    {
        var outMap = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var path = Path_;
            if (path is null || !File.Exists(path)) return outMap;
            var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
            if (root?[section] is not JsonObject sec) return outMap;
            foreach (var kv in sec)
            {
                if (kv.Value is JsonValue v) outMap[kv.Key] = v.ToString();
            }
        }
        catch
        {
            outMap.Clear();
        }
        return outMap;
    }

    public static bool Save(string section, IReadOnlyDictionary<string, string> values)
    {
        try
        {
            var path = Path_;
            if (path is null) return false;
            JsonObject root;
            try
            {
                root = (File.Exists(path)
                        ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : null) ?? [];
            }
            catch { root = []; }
            var sec = new JsonObject();
            foreach (var kv in values) sec[kv.Key] = kv.Value;
            root[section] = sec;
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return true;
        }
        catch { return false; }
    }

    public static IEnumerable<(string Label, Func<string?> Body)> SelfTestCases()
    {
        yield return ("節の名前が空でない（母数）", () =>
            !string.IsNullOrEmpty(FileName) && !string.IsNullOrEmpty(HitWindowSection)
                ? null : "名前が空");

        yield return ("★読めないパスでも落ちない（空が返る）", () =>
        {
            var got = Load("no_such_section");
            return got.Count == 0 ? null : "空でない";
        });

        yield return ("★★壊れた JSON でも落ちない（既定で動く）", () =>
        {
            var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                                             "th09_ui_" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(tmp, "{ これは JSON ではない ");
                Dictionary<string, string> Parse(string text)
                {
                    var m = new Dictionary<string, string>(StringComparer.Ordinal);
                    try
                    {
                        if (JsonNode.Parse(text) is not JsonObject r) return m;
                        if (r[HitWindowSection] is not JsonObject s) return m;
                        foreach (var kv in s) if (kv.Value is JsonValue v) m[kv.Key] = v.ToString();
                    }
                    catch { m.Clear(); }
                    return m;
                }
                return Parse(File.ReadAllText(tmp)).Count == 0 ? null : "空にならない";
            }
            finally { try { File.Delete(tmp); } catch { } }
        });

        yield return ("★オブジェクトでない中身は空扱い（配列・数・文字列）", () =>
        {
            foreach (var bad in new[] { "[1,2,3]", "42", "\"x\"" })
            {
                if (JsonNode.Parse(bad) is JsonObject) return "オブジェクトと読めてしまう: " + bad;
            }
            return null;
        });

        yield return ("★否定: 旧（例外を外へ出す）なら壊れた JSON で画面が開かない", () =>
        {
            static Dictionary<string, string> Legacy(string text)
            {
                var r = (JsonObject)JsonNode.Parse(text)!;
                return r.ToDictionary(k => k.Key, k => k.Value!.ToString(), StringComparer.Ordinal);
            }
            try
            {
                Legacy("{ 壊れている ");
                return "旧が落ちない（写しが間違っている。否定テストの意味が無い）";
            }
            catch { }
            var got = Load("hitwindow");
            return got is not null ? null : "新が null を返した";
        });
    }
}
