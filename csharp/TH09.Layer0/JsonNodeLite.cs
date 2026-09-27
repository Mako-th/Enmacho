using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TH09.Layer0;

public sealed class JsonNodeLite
{
    public enum NodeKind { Null, Bool, Int, String, Array, Object }

    public NodeKind Kind { get; }
    private readonly bool _bool;
    private readonly long _int;
    private readonly string? _string;
    private readonly List<JsonNodeLite>? _array;
    private readonly List<KeyValuePair<string, JsonNodeLite>>? _object;

    private JsonNodeLite(NodeKind kind, bool b = false, long i = 0, string? s = null,
                         List<JsonNodeLite>? arr = null,
                         List<KeyValuePair<string, JsonNodeLite>>? obj = null)
    {
        Kind = kind; _bool = b; _int = i; _string = s; _array = arr; _object = obj;
    }

    public static readonly JsonNodeLite Null = new(NodeKind.Null);
    public static JsonNodeLite FromBool(bool v) => new(NodeKind.Bool, b: v);
    public static JsonNodeLite FromInt(long v) => new(NodeKind.Int, i: v);
    public static JsonNodeLite FromString(string v) => new(NodeKind.String, s: v);
    public static JsonNodeLite FromArray(List<JsonNodeLite> items) => new(NodeKind.Array, arr: items);
    public static JsonNodeLite FromObject(List<KeyValuePair<string, JsonNodeLite>> members) =>
        new(NodeKind.Object, obj: members);

    public static JsonNodeLite FromStrings(IEnumerable<string> items) =>
        FromArray(items.Select(FromString).ToList());


    public static JsonNodeLite Parse(byte[] utf8)
    {
        using var doc = JsonDocument.Parse(utf8);
        return FromElement(doc.RootElement);
    }

    public static JsonNodeLite Parse(string json) => Parse(Encoding.UTF8.GetBytes(json));

    private static JsonNodeLite FromElement(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Null or JsonValueKind.Undefined: return Null;
            case JsonValueKind.True: return FromBool(true);
            case JsonValueKind.False: return FromBool(false);
            case JsonValueKind.String: return FromString(e.GetString()!);
            case JsonValueKind.Number:
                if (!e.TryGetInt64(out var n))
                    throw new InvalidDataException(
                        "field_order に整数でない数がありました: " + e.GetRawText()
                        + "（小数は Python と .NET で書式が揃わないので、通さずに落とす）");
                return FromInt(n);
            case JsonValueKind.Array:
            {
                var items = new List<JsonNodeLite>();
                foreach (var it in e.EnumerateArray()) items.Add(FromElement(it));
                return FromArray(items);
            }
            case JsonValueKind.Object:
            {
                var members = new List<KeyValuePair<string, JsonNodeLite>>();
                foreach (var p in e.EnumerateObject())
                    members.Add(new KeyValuePair<string, JsonNodeLite>(p.Name, FromElement(p.Value)));
                return FromObject(members);
            }
            default:
                throw new InvalidDataException("知らない JSON の種類です: " + e.ValueKind);
        }
    }


    public bool Has(string name) => Find(name) is not null;

    private JsonNodeLite? Find(string name)
    {
        if (_object is null) return null;
        foreach (var kv in _object) if (string.Equals(kv.Key, name, StringComparison.Ordinal)) return kv.Value;
        return null;
    }

    public string? String(string name)
    {
        var v = Find(name);
        return v is not null && v.Kind == NodeKind.String ? v._string : null;
    }

    public long Int(string name, long fallback)
    {
        var v = Find(name);
        return v is not null && v.Kind == NodeKind.Int ? v._int : fallback;
    }

    public IReadOnlyList<JsonNodeLite> Array(string name)
    {
        var v = Find(name);
        return v is not null && v.Kind == NodeKind.Array ? v._array! : [];
    }

    public string AsString() => Kind == NodeKind.String
        ? _string!
        : throw new InvalidDataException("文字列ではありません: " + Kind);


    public JsonNodeLite WithMember(string name, JsonNodeLite value)
    {
        var members = new List<KeyValuePair<string, JsonNodeLite>>(_object ?? []);
        for (int i = 0; i < members.Count; i++)
        {
            if (!string.Equals(members[i].Key, name, StringComparison.Ordinal)) continue;
            members[i] = new KeyValuePair<string, JsonNodeLite>(name, value);
            return FromObject(members);
        }
        members.Add(new KeyValuePair<string, JsonNodeLite>(name, value));
        return FromObject(members);
    }

    public JsonNodeLite WithoutMember(string name) =>
        FromObject((_object ?? []).Where(kv => !string.Equals(kv.Key, name, StringComparison.Ordinal)).ToList());


    public string ToCompactJson(bool escapeNonAscii) => Write(new StringBuilder(), sortKeys: false, escapeNonAscii).ToString();

    public string ToCanonicalJson() => Write(new StringBuilder(), sortKeys: true, escapeNonAscii: true).ToString();

    private StringBuilder Write(StringBuilder sb, bool sortKeys, bool escapeNonAscii)
    {
        switch (Kind)
        {
            case NodeKind.Null: sb.Append("null"); break;
            case NodeKind.Bool: sb.Append(_bool ? "true" : "false"); break;
            case NodeKind.Int: sb.Append(_int.ToString(CultureInfo.InvariantCulture)); break;
            case NodeKind.String: WriteString(sb, _string!, escapeNonAscii); break;
            case NodeKind.Array:
                sb.Append('[');
                for (int i = 0; i < _array!.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    _array[i].Write(sb, sortKeys, escapeNonAscii);
                }
                sb.Append(']');
                break;
            case NodeKind.Object:
            {
                var members = _object!;
                if (sortKeys) members = [.. members.OrderBy(kv => kv.Key, StringComparer.Ordinal)];
                sb.Append('{');
                for (int i = 0; i < members.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    WriteString(sb, members[i].Key, escapeNonAscii);
                    sb.Append(':');
                    members[i].Value.Write(sb, sortKeys, escapeNonAscii);
                }
                sb.Append('}');
                break;
            }
        }
        return sb;
    }

    private static void WriteString(StringBuilder sb, string s, bool escapeNonAscii)
    {
        sb.Append('"');
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < 0x20 || (escapeNonAscii && ch > 0x7E))
                        sb.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    else sb.Append(ch);
                    break;
            }
        }
        sb.Append('"');
    }
}
