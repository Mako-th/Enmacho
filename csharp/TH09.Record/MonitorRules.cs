using System.Globalization;
using TH09.Record.Generated;

namespace TH09.Record;

public static class MonitorRules
{
    public static string Name(System.Collections.Frozen.FrozenDictionary<int, string> map, long key)
    {
        var k = unchecked((int)key);
        if (k == key && map.TryGetValue(k, out var v)) return v;
        return "Unknown(" + key.ToString(CultureInfo.InvariantCulture) + ")";
    }

    public static string Char(long i) =>
        i >= 0 && i < RecordLabels.Characters.Length
            ? RecordLabels.Characters[(int)i]
            : "Unknown(" + i.ToString(CultureInfo.InvariantCulture) + ")";

    public static string FmtFrames(long f)
    {
        var m = f / 3600;
        var s = f / 60 % 60;
        var cs = (long)((f % 60) * 100 / 60);
        return m.ToString("00", CultureInfo.InvariantCulture) + ":"
             + s.ToString("00", CultureInfo.InvariantCulture) + "."
             + cs.ToString("00", CultureInfo.InvariantCulture);
    }

    public static bool ValidBgmId(long value) => value is >= 0 and <= 13;

    public static int NormalizeComboGauge(int value) =>
        value <= RecordLabels.ComboGaugeSentinelMax ? 0 : value;

    public static long GaugeBits(double value) => BitConverter.SingleToUInt32Bits((float)value);

    public static bool MoreRoundsToPlay(long mode, long p1Wins, long p2Wins, long roundsRequired) =>
        mode == 2 ? Math.Max(p1Wins, p2Wins) < roundsRequired : p1Wins < roundsRequired;
}
