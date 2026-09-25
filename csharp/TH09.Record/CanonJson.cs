using System.Globalization;
using System.Text;

namespace TH09.Record;

public static class CanonJson
{
    public enum Kind
    {
        Null,

        Bool,

        Int,

        Float,

        String,

        Array,

        Object,
    }

    public sealed class Node
    {
        public Kind Kind { get; }

        public bool Bool { get; }

        public string Text { get; }

        public double Float { get; }

        public List<Node> Items { get; }

        public Dictionary<string, Node> Members { get; }

        private Node(Kind kind, bool b = false, string text = "", double f = 0,
                     List<Node>? items = null, Dictionary<string, Node>? members = null)
        {
            Kind = kind; Bool = b; Text = text; Float = f;
            Items = items ?? [];
            Members = members ?? new Dictionary<string, Node>(StringComparer.Ordinal);
        }

        internal static readonly Node NullNode = new(Kind.Null);
        internal static Node FromBool(bool v) => new(Kind.Bool, b: v);
        internal static Node FromInt(string decimalText) => new(Kind.Int, text: decimalText);
        internal static Node FromFloat(double v) => new(Kind.Float, f: v);
        internal static Node FromString(string v) => new(Kind.String, text: v);
        internal static Node FromArray(List<Node> items) => new(Kind.Array, items: items);
        internal static Node FromObject(Dictionary<string, Node> m) => new(Kind.Object, members: m);
    }


    public static bool TryParse(string text, out Node node)
    {
        node = Node.NullNode;
        try
        {
            var p = new Parser(text);
            p.SkipWhitespace();
            var v = p.ParseValue();
            p.SkipWhitespace();
            if (!p.AtEnd) return false;
            node = v;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private sealed class Parser(string s)
    {
        private readonly string _s = s;
        private int _i;

        public bool AtEnd => _i >= _s.Length;

        public void SkipWhitespace()
        {
            while (_i < _s.Length && (_s[_i] == ' ' || _s[_i] == '\t' || _s[_i] == '\n' || _s[_i] == '\r'))
                _i++;
        }

        private static FormatException Bad(string why) => new(why);

        private char Peek() => _i < _s.Length ? _s[_i] : throw Bad("末尾で切れている");

        private bool Literal(string word)
        {
            if (_i + word.Length > _s.Length) return false;
            if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) return false;
            _i += word.Length;
            return true;
        }

        public Node ParseValue()
        {
            var c = Peek();
            switch (c)
            {
                case '{': return ParseObject();
                case '[': return ParseArray();
                case '"': return Node.FromString(ParseString());
                case 't': if (Literal("true")) return Node.FromBool(true); break;
                case 'f': if (Literal("false")) return Node.FromBool(false); break;
                case 'n': if (Literal("null")) return Node.NullNode; break;
                case 'N': if (Literal("NaN"))
                              return Node.FromFloat(BitConverter.Int64BitsToDouble(0x7FF8000000000000L));
                          break;
                case 'I': if (Literal("Infinity")) return Node.FromFloat(double.PositiveInfinity); break;
                case '-': if (Literal("-Infinity")) return Node.FromFloat(double.NegativeInfinity); break;
            }
            if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
            throw Bad($"値として読めない文字: {c}");
        }

        private Node ParseObject()
        {
            _i++;
            var members = new Dictionary<string, Node>(StringComparer.Ordinal);
            SkipWhitespace();
            if (!AtEnd && Peek() == '}') { _i++; return Node.FromObject(members); }
            while (true)
            {
                SkipWhitespace();
                if (Peek() != '"') throw Bad("鍵は文字列でなければならない");
                var key = ParseString();
                SkipWhitespace();
                if (Peek() != ':') throw Bad("鍵の後は ':'");
                _i++;
                SkipWhitespace();
                members[key] = ParseValue();
                SkipWhitespace();
                var c = Peek();
                if (c == ',') { _i++; continue; }
                if (c == '}') { _i++; return Node.FromObject(members); }
                throw Bad("',' か '}' が要る");
            }
        }

        private Node ParseArray()
        {
            _i++;
            var items = new List<Node>();
            SkipWhitespace();
            if (!AtEnd && Peek() == ']') { _i++; return Node.FromArray(items); }
            while (true)
            {
                SkipWhitespace();
                items.Add(ParseValue());
                SkipWhitespace();
                var c = Peek();
                if (c == ',') { _i++; continue; }
                if (c == ']') { _i++; return Node.FromArray(items); }
                throw Bad("',' か ']' が要る");
            }
        }

        private string ParseString()
        {
            _i++;
            var sb = new StringBuilder();
            while (true)
            {
                if (AtEnd) throw Bad("文字列が閉じていない");
                var c = _s[_i++];
                if (c == '"') return sb.ToString();
                if (c != '\\')
                {
                    if (c <= 0x1F) throw Bad("文字列の中に生の制御文字がある");
                    sb.Append(c);
                    continue;
                }
                if (AtEnd) throw Bad("エスケープが途中で切れている");
                var e = _s[_i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u': sb.Append(ParseHex4()); break;
                    default: throw Bad($"知らないエスケープ: \\{e}");
                }
            }
        }

        private char ParseHex4()
        {
            if (_i + 4 > _s.Length) throw Bad(@"\u の後に 16 進 4 桁が要る");
            var span = _s.AsSpan(_i, 4);
            foreach (var ch in span)
            {
                var ok = (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F');
                if (!ok) throw Bad(@"\u の後に 16 進 4 桁が要る");
            }
            _i += 4;
            return (char)ushort.Parse(span, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private Node ParseNumber()
        {
            var start = _i;
            if (!AtEnd && _s[_i] == '-') _i++;
            if (AtEnd) throw Bad("数が途中で切れている");
            if (_s[_i] == '0') { _i++; }
            else if (_s[_i] >= '1' && _s[_i] <= '9') { while (!AtEnd && char.IsAsciiDigit(_s[_i])) _i++; }
            else throw Bad("数の整数部が無い");

            var isFloat = false;
            if (!AtEnd && _s[_i] == '.' && _i + 1 < _s.Length && char.IsAsciiDigit(_s[_i + 1]))
            {
                isFloat = true;
                _i++;
                while (!AtEnd && char.IsAsciiDigit(_s[_i])) _i++;
            }
            if (!AtEnd && (_s[_i] == 'e' || _s[_i] == 'E'))
            {
                var save = _i;
                _i++;
                if (!AtEnd && (_s[_i] == '+' || _s[_i] == '-')) _i++;
                if (!AtEnd && char.IsAsciiDigit(_s[_i]))
                {
                    isFloat = true;
                    while (!AtEnd && char.IsAsciiDigit(_s[_i])) _i++;
                }
                else
                {
                    _i = save;
                }
            }

            var text = _s[start.._i];
            if (!isFloat)
            {
                return Node.FromInt(text == "-0" ? "0" : text);
            }
            return Node.FromFloat(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture));
        }
    }


    public static string Canon(Node node)
    {
        var sb = new StringBuilder();
        Write(sb, node);
        return sb.ToString();
    }

    private static void Write(StringBuilder sb, Node node)
    {
        switch (node.Kind)
        {
            case Kind.Null: sb.Append("null"); break;
            case Kind.Bool: sb.Append(node.Bool ? "true" : "false"); break;
            case Kind.Int: sb.Append(node.Text); break;
            case Kind.Float: sb.Append(ParityValue.FloatToken(node.Float)); break;
            case Kind.String: WriteJsonString(sb, node.Text); break;
            case Kind.Array:
                sb.Append('[');
                for (var i = 0; i < node.Items.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    Write(sb, node.Items[i]);
                }
                sb.Append(']');
                break;
            case Kind.Object:
                sb.Append('{');
                var keys = node.Members.Keys.ToList();
                keys.Sort(CompareByUtf8Bytes);
                for (var i = 0; i < keys.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    WriteJsonString(sb, keys[i]);
                    sb.Append(':');
                    Write(sb, node.Members[keys[i]]);
                }
                sb.Append('}');
                break;
        }
    }

    internal static int CompareByUtf8Bytes(string a, string b)
    {
        var x = Encoding.UTF8.GetBytes(a);
        var y = Encoding.UTF8.GetBytes(b);
        var n = Math.Min(x.Length, y.Length);
        for (var i = 0; i < n; i++)
        {
            if (x[i] != y[i]) return x[i] < y[i] ? -1 : 1;
        }
        return x.Length.CompareTo(y.Length);
    }

    internal static void WriteJsonString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c >= ' ' && c <= '~') sb.Append(c);
                    else sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    break;
            }
        }
        sb.Append('"');
    }
}
