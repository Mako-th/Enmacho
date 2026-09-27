using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace TH09.Analysis;

public static class FeatureJson
{
    public enum Kind { Null, Bool, Int, Float, String }

    public readonly struct Value
    {
        public Kind Kind { get; }
        private readonly bool _bool;
        private readonly string _text;

        private Value(Kind kind, bool b, string text) { Kind = kind; _bool = b; _text = text; }

        public static readonly Value Null = new(Kind.Null, false, "");
        public static Value Bool(bool v) => new(Kind.Bool, v, "");
        public static Value Int(string literal) => new(Kind.Int, false, literal);
        public static Value Float(string literal) => new(Kind.Float, false, literal);
        public static Value Str(string v) => new(Kind.String, false, v);

        public (string Type, string Text) Cells() => Kind switch
        {
            Kind.Null => ("null", ""),
            Kind.Bool => ("bool", _bool ? "true" : "false"),
            Kind.Int => ("i", NormalizeInt(_text)),
            Kind.Float => ("f64", BitConverter.DoubleToInt64Bits(ParseDouble(_text))
                                              .ToString("x16", CultureInfo.InvariantCulture)),
            Kind.String => ("s", Escape(_text)),
            _ => throw new InvalidOperationException("知らない値の種類: " + Kind),
        };

        private static string NormalizeInt(string literal) =>
            long.TryParse(literal, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n)
                ? n.ToString(CultureInfo.InvariantCulture)
                : BigInteger.Parse(literal, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)
                            .ToString(CultureInfo.InvariantCulture);

        private static double ParseDouble(string literal) =>
            double.Parse(literal, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    public static string Escape(string text)
    {
        if (text.AsSpan().IndexOfAny('\\', '\t', '\r') < 0 && !text.Contains('\n')) return text;
        var sb = new StringBuilder(text.Length + 8);
        foreach (var ch in text)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '\t': sb.Append("\\t"); break;
                case '\r': sb.Append("\\r"); break;
                case '\n': sb.Append("\\n"); break;
                default: sb.Append(ch); break;
            }
        }
        return sb.ToString();
    }

    public static Dictionary<string, Value> Parse(string? json, string where)
    {
        var text = string.IsNullOrEmpty(json) ? "{}" : json;
        var bytes = Encoding.UTF8.GetBytes(text);
        var reader = new Utf8JsonReader(bytes, isFinalBlock: true, state: default);
        var outMap = new Dictionary<string, Value>(StringComparer.Ordinal);

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            throw new InvalidDataException($"特徴量の JSON がオブジェクトではありません: {where}");

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new InvalidDataException($"鍵が来るはずの所に {reader.TokenType}: {where}");
            var key = reader.GetString()!;
            if (!reader.Read()) throw new InvalidDataException($"値が途中で切れています: {where} key={key}");
            outMap[key] = ReadValue(ref reader, where, key);
        }
        return outMap;
    }

    private static Value ReadValue(ref Utf8JsonReader reader, string where, string key)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null: return Value.Null;
            case JsonTokenType.True: return Value.Bool(true);
            case JsonTokenType.False: return Value.Bool(false);
            case JsonTokenType.String: return Value.Str(reader.GetString()!);
            case JsonTokenType.Number:
            {
                ReadOnlySpan<byte> raw = reader.HasValueSequence
                    ? System.Buffers.BuffersExtensions.ToArray(reader.ValueSequence)
                    : reader.ValueSpan;
                var literal = Encoding.UTF8.GetString(raw);
                bool isFloat = raw.IndexOfAny((byte)'.', (byte)'e', (byte)'E') >= 0;
                return isFloat ? Value.Float(literal) : Value.Int(literal);
            }
            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
                throw new InvalidDataException(
                    $"この形式は入れ子の値を吐けない: {where} key={key} ({reader.TokenType})");
            default:
                throw new InvalidDataException($"知らない JSON の種類 {reader.TokenType}: {where} key={key}");
        }
    }

    public static List<string> SortedKeys(Dictionary<string, Value> map)
    {
        var keys = new List<string>(map.Keys);
        keys.Sort(StringComparer.Ordinal);
        return keys;
    }
}
