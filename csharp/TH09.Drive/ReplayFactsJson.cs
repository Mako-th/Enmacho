using System.Globalization;
using System.Text;
using TH09.Replay;

namespace TH09.Drive;

public static class ReplayFactsJson
{
    public const string KeySeparator = ": ";

    public const string ItemSeparator = ", ";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string DecodedJson(ReplayResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var sb = new StringBuilder(512);
        sb.Append('{');
        Key(sb, "status", first: true);
        Text(sb, result.Status);
        Key(sb, "stages");
        sb.Append('[');
        for (var i = 0; i < result.Stages.Count; i++)
        {
            if (i > 0) sb.Append(ItemSeparator);
            Stage(sb, result.Stages[i]);
        }
        sb.Append(']');
        if (result.Decoded)
        {
            Key(sb, "name");
            Text(sb, result.Name);
            Key(sb, "date");
            Text(sb, result.Date);
            Key(sb, "difficulty");
            Number(sb, result.Difficulty);
            Key(sb, "mode");
            Number(sb, result.Mode);
            Key(sb, "p1_char");
            Number(sb, result.P1Char);
            Key(sb, "p2_char");
            Number(sb, result.P2Char);
            Key(sb, "p1_name");
            Text(sb, result.P1Name);
            Key(sb, "p2_name");
            Text(sb, result.P2Name);
        }
        else
        {
            Key(sb, "error");
            Text(sb, result.Error ?? "");
        }
        sb.Append('}');
        return sb.ToString();
    }

    private static void Key(StringBuilder sb, string name, bool first = false)
    {
        if (!first) sb.Append(ItemSeparator);
        Text(sb, name);
        sb.Append(KeySeparator);
    }

    private static void Stage(StringBuilder sb, StageInfo s)
    {
        sb.Append('{');
        Key(sb, "index", first: true);
        Number(sb, s.Index);
        Key(sb, "score");
        Number(sb, s.Score);
        Key(sb, "shot");
        Number(sb, s.Shot);
        Key(sb, "ai");
        sb.Append(s.Ai ? "true" : "false");
        Key(sb, "lives");
        Number(sb, s.Lives);
        Key(sb, "pair");
        Number(sb, s.Pair);
        Key(sb, "rng_seed");
        Number(sb, s.RngSeed);
        Key(sb, "field_id");
        Number(sb, s.FieldId);
        Key(sb, "opponent");
        Number(sb, s.Opponent);
        sb.Append('}');
    }

    private static void Number(StringBuilder sb, long? value)
    {
        if (value is null) sb.Append("null");
        else sb.Append(value.Value.ToString(Inv));
    }

    private static void Text(StringBuilder sb, string? value)
    {
        if (value is null)
        {
            sb.Append("null");
            return;
        }
        sb.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append(Escape).Append('"'); break;
                case Escape: sb.Append(Escape).Append(Escape); break;
                case '\b': sb.Append(Escape).Append('b'); break;
                case '\f': sb.Append(Escape).Append('f'); break;
                case '\n': sb.Append(Escape).Append('n'); break;
                case '\r': sb.Append(Escape).Append('r'); break;
                case '\t': sb.Append(Escape).Append('t'); break;
                default:
                    if (c < ' ') sb.Append(Escape).Append('u').Append(((int)c).ToString("x4", Inv));
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }

    private const char Escape = (char)92;
}
