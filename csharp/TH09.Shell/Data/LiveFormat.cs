using System.Globalization;

namespace TH09.Shell.Data;

internal static class LiveFormat
{

    public static string ModeOrUnknown(long? v) => v switch
    {
        0 => "Story", 1 => "Extra", 2 => "Match", _ => "Unknown",
    };

    public static string DifficultyOrUnknown(long? v)
        => v is long i && i >= 0 && i < ReplayLabels.Difficulties.Length
            ? ReplayLabels.Difficulties[i]
            : "Unknown";

    public static string Life(double? v)
        => v is double d ? (d / 2).ToString("0.0", CultureInfo.InvariantCulture) : ReplayFormat.Missing;

    public static string Grouped(long v) => v.ToString("N0", CultureInfo.InvariantCulture);

    public static string SignedGrouped(long v)
        => (v < 0 ? "-" : "+") + Math.Abs(v).ToString("N0", CultureInfo.InvariantCulture);
}
