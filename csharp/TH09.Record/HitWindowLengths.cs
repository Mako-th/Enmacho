using System.Globalization;
using TH09.Record.Generated;
using TH09.TickBus;

namespace TH09.Record;

public static class HitWindowLengths
{
    public static int HeadRawMax(int before) => before * HitWindowConst.HeadRawFactor;

    public static int TailRawMax(int after) => after + HitWindowConst.TailRawSlack;

    public static int MaxWindowTicks(int before, int after) =>
        Math.Min(HitWindowConst.MaxWindowRawCap,
                 Math.Max(HitWindowConst.MaxWindowTicks, HeadRawMax(before) + TailRawMax(after)));

    public static string Note(int before, int after, bool countValid) =>
        "数え方: " + (countValid ? "VALID" : "RAW")
        + "   前 " + before.ToString(CultureInfo.InvariantCulture) + " f"
        + "  /  後 " + after.ToString(CultureInfo.InvariantCulture) + " f";

    public static List<string> Warnings(int before, int after, int? maxWindow = null)
    {
        int need = HeadRawMax(before) + TailRawMax(after);
        int mw = maxWindow ?? MaxWindowTicks(before, after);
        var outv = new List<string>();
        if (need > HitWindowConst.MaxWindowRawCap)
            outv.Add($"★窓 1 本の要求 {need} 生 tick が絶対の上限 {HitWindowConst.MaxWindowRawCap} を"
                     + $"超えています（{Note(before, after, true)}）。"
                     + $"尾が {need - HitWindowConst.MaxWindowRawCap} tick ぶん切られます");
        if (mw < need)
            outv.Add($"★マージ上限 {mw} が窓 1 本の要求 {need} を下回っています。"
                     + "被弾 1 件だけの窓でも尾が切られます（印は hits の merge_capped）");
        long grace = CoordLayout.Capacity - CoordRingReader.DefaultMargin - mw;
        if (grace <= 0)
            outv.Add($"★窓（{mw} 生 tick）が座標リング（{CoordLayout.Capacity} tick）を食い切っています。"
                     + "書き手に追い越されて窓を読み戻せません");
        return outv;
    }

    public static (int Before, int After) Clamp(int? before, int? after) =>
        (One(before, HitWindowConst.WindowBeforeTicks,
             HitWindowConst.WindowBeforeMin, HitWindowConst.WindowBeforeMax),
         One(after, HitWindowConst.WindowAfterTicks,
             HitWindowConst.WindowAfterMin, HitWindowConst.WindowAfterMax));

    private static int One(int? v, int dflt, int lo, int hi) =>
        v is null ? dflt : Math.Max(lo, Math.Min(hi, v.Value));
}
