using System.Globalization;
using TH09.Record;

namespace TH09.Shell.Data;

internal static class StreamTargetForm
{
    public const long StoryMode = 0;

    public const long ExtraMode = 1;

    public const long MatchMode = 2;

    public static readonly long[] Modes = [StoryMode, ExtraMode, MatchMode];

    public const long ExtraDifficulty = 4;

    public static readonly long[] Difficulties = [0, 1, 2, 3];

    public const long StageCount = 9;

    public const long StoryMaxRound = 6;

    public const long MatchMaxRound = 3;

    public const long FramesPerSecond = StreamTargets.MatchFramesPerSecond;

    public const string Empty = "";

    public static string ModeLabel(long mode) => LiveFormat.ModeOrUnknown(mode);

    public static string DifficultyLabel(long difficulty) =>
        LiveFormat.DifficultyOrUnknown(difficulty);

    public static IReadOnlyList<long> DifficultiesOf(long mode) =>
        mode == ExtraMode ? [ExtraDifficulty] : Difficulties;

    public static IReadOnlyList<long> StagesOf(long mode)
    {
        if (mode == MatchMode) return [];
        var list = new List<long>();
        for (var i = 1L; i <= StageCount; i++) list.Add(i);
        return list;
    }

    public static long MaxRoundOf(long mode) => mode == MatchMode ? MatchMaxRound : StoryMaxRound;

    public static string WholeLabel(long mode) => mode == MatchMode ? "マッチ全体" : "面全体";

    public static string RoundLabel(long mode, long? round) =>
        round is long r ? "R" + r.ToString(CultureInfo.InvariantCulture) : WholeLabel(mode);

    public static bool HasClearBonus(long mode) => mode != MatchMode;

    public static string UnitLabel(long mode) => mode == MatchMode ? "秒" : "スコア";

    public static string Format(long mode, long? value)
    {
        if (value is not long v) return Empty;
        if (mode != MatchMode) return LiveFormat.Grouped(v);
        if (v % FramesPerSecond == 0)
            return LiveFormat.Grouped(v / FramesPerSecond);
        return (v / (double)FramesPerSecond).ToString("0.###", CultureInfo.InvariantCulture);
    }

    public static string FormatClearBonus(long? value) =>
        value is long v ? LiveFormat.Grouped(v) : Empty;

    public static bool TryParse(long mode, string text, string where, out long? value, out string error)
    {
        value = null;
        error = "";
        var clean = Clean(text);
        if (clean.Length == 0) return true;
        if (mode != MatchMode)
        {
            if (!long.TryParse(clean, NumberStyles.Integer, CultureInfo.InvariantCulture, out var score)
                || score < 0)
            {
                error = "★" + where + " は 0 以上のスコアを入れてください: " + text;
                return false;
            }
            value = score;
            return true;
        }
        if (!double.TryParse(clean, NumberStyles.Float, CultureInfo.InvariantCulture, out var sec)
            || double.IsNaN(sec) || double.IsInfinity(sec) || sec < 0)
        {
            error = "★" + where + " は 0 以上の秒数を入れてください: " + text;
            return false;
        }
        value = (long)Math.Round(sec * FramesPerSecond, MidpointRounding.AwayFromZero);
        return true;
    }

    public static bool TryParseClearBonus(string text, string where, out long? value, out string error)
        => TryParse(StoryMode, text, where, out value, out error);

    public static string? WithClearBonus(long mode, long? raw, long? clearBonus)
    {
        if (mode == MatchMode || raw is not long v || clearBonus is not long cb) return null;
        return LiveFormat.Grouped(v + cb);
    }

    public static string? PanelText(long mode, long? raw)
        => mode == MatchMode && raw is long v ? ReplayFormat.FineFrames(v) : null;

    public static string HintLabel(long mode) => mode == MatchMode ? "配信パネル " : "見たまま ";

    private static string Clean(string text)
    {
        var chars = new List<char>(text.Length);
        foreach (var ch in text)
        {
            if (ch is ',' or ' ' or '\t' or '　') continue;
            chars.Add(ch);
        }
        return new string([.. chars]);
    }
}
