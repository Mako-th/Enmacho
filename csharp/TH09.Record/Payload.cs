using System.Globalization;
using System.Text;

namespace TH09.Record;

public sealed class Payload
{
    private readonly List<KeyValuePair<string, string>> _items = [];

    public int Count => _items.Count;

    public long? Side
    {
        get
        {
            var at = _items.FindIndex(kv => kv.Key == "side");
            if (at < 0) return null;
            var text = _items[at].Value;
            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
        }
    }

    public Payload Add(string key, long value) => Put(key, value.ToString(CultureInfo.InvariantCulture));

    public Payload Add(string key, long? value) =>
        value is null ? Put(key, "null") : Add(key, value.Value);

    public Payload Add(string key, bool value) => Put(key, value ? "true" : "false");

    public Payload Add(string key, string? value) =>
        Put(key, value is null ? "null" : JsonString(value));

    public Payload AddFloat(string key, double value) => Put(key, FloatText(value));

    public Payload AddStrings(string key, IEnumerable<string> values) =>
        Put(key, "[" + string.Join(",", values.Select(JsonString)) + "]");

    public Payload AddEmptyArray(string key) => Put(key, "[]");

    public Payload Set(string key, long value) => Replace(key, value.ToString(CultureInfo.InvariantCulture));

    public Payload Set(string key, long? value) =>
        value is null ? Replace(key, "null") : Set(key, value.Value);

    public Payload Set(string key, string? value) =>
        Replace(key, value is null ? "null" : JsonString(value));

    public Payload Set(string key, bool value) => Replace(key, value ? "true" : "false");

    public Payload Clone()
    {
        var copy = new Payload();
        copy._items.AddRange(_items);
        return copy;
    }

    public bool Has(string key) => _items.Any(kv => kv.Key == key);

    public string ToJson()
    {
        var sb = new StringBuilder("{");
        for (var i = 0; i < _items.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(JsonString(_items[i].Key)).Append(':').Append(_items[i].Value);
        }
        return sb.Append('}').ToString();
    }

    private Payload Put(string key, string json)
    {
        _items.Add(new KeyValuePair<string, string>(key, json));
        return this;
    }

    private Payload Replace(string key, string json)
    {
        var at = _items.FindIndex(kv => kv.Key == key);
        if (at < 0) return Put(key, json);
        _items[at] = new KeyValuePair<string, string>(key, json);
        return this;
    }

    internal static string FloatText(double v)
    {
        if (double.IsNaN(v)) return "NaN";
        if (double.IsPositiveInfinity(v)) return "Infinity";
        if (double.IsNegativeInfinity(v)) return "-Infinity";
        var s = v.ToString("R", CultureInfo.InvariantCulture);
        return s.Contains('.') || s.Contains('e') || s.Contains('E') ? s : s + ".0";
    }

    internal static string JsonString(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (ch < 0x20) sb.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    else sb.Append(ch);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }
}
