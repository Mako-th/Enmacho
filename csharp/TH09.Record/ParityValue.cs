using System.Globalization;
using System.Text;

namespace TH09.Record;

public static class ParityValue
{
    public const string TypeNull = "null";

    public const string TypeInteger = "integer";

    public const string TypeReal = "real";

    public const string TypeText = "text";

    public const string TypeBlob = "blob";

    public const string NullToken = "~";

    public static string FloatToken(double v)
    {
        var bits = BitConverter.DoubleToInt64Bits(v);
        var buf = new byte[8];
        for (var i = 0; i < 8; i++) buf[i] = (byte)(bits >> ((7 - i) * 8));
        return "f:" + Convert.ToHexStringLower(buf);
    }

    public static string EscapeText(string s) =>
        s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");

    public static string IntToken(long v) => "i:" + v.ToString(CultureInfo.InvariantCulture);

    public static string StringToken(string v) => "s:" + EscapeText(v);

    public static string BlobToken(byte[] v) => "b:" + Convert.ToHexStringLower(v);

    public static string BasenameToken(string? v)
    {
        if (v is null) return NullToken;
        var s = v.Replace('\\', '/');
        var i = s.LastIndexOf('/');
        return "s:" + EscapeText(i < 0 ? s : s[(i + 1)..]);
    }

    public static string TruncateSecondsToken(string? v)
    {
        if (v is null) return NullToken;
        var i = v.IndexOf('.', StringComparison.Ordinal);
        if (i < 0) return "s:" + EscapeText(v);
        var j = i + 1;
        while (j < v.Length && char.IsAsciiDigit(v[j])) j++;
        return "s:" + EscapeText(v[..i] + v[j..]);
    }

    public static string CanonJsonToken(string? text)
    {
        if (text is null) return NullToken;
        return CanonJson.TryParse(text, out var node)
            ? "j:" + EscapeText(CanonJson.Canon(node))
            : "j!:" + EscapeText(text);
    }

    public static string ValueToken(string sqliteType, object? value)
    {
        if (value is null || sqliteType == TypeNull) return NullToken;
        return sqliteType switch
        {
            TypeInteger => IntToken((long)value),
            TypeReal => FloatToken((double)value),
            TypeBlob => BlobToken((byte[])value),
            _ => StringToken((string)value),
        };
    }

    public static string RankKey(string sqliteType, object? value) => ValueToken(sqliteType, value);

    public static string JoinRow(string head, IEnumerable<string> tokens)
    {
        var sb = new StringBuilder(head);
        foreach (var t in tokens) { sb.Append('\t'); sb.Append(t); }
        return sb.ToString();
    }
}
