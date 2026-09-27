using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using TH09.Record;

namespace TH09.Shell.Data;

[SupportedOSPlatform("windows")]
internal static class StatsLayoutDump
{
    public const string Flag = "--dump-stats-layout";

    private const char Separator = '\t';

    public static int Run(string? configPath)
    {
        using var w = new StreamWriter(Console.OpenStandardOutput(),
                                       new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var order = StatsOrderMap.Empty;
        var hidden = StatsOrderMap.Empty;
        if (configPath is not null)
        {
            AppSettingsSource.ReadFrom(configPath);
            order = AppSettingsSource.Current.StatsColumnOrder;
            hidden = AppSettingsSource.Current.StatsHiddenColumns;
        }

        Row(w, "fact", "config", configPath is null ? "(なし)" : "あり");
        Row(w, "fact", "order", Num(Sections(order)));
        Row(w, "fact", "hidden", Num(Sections(hidden)));
        Row(w, "fact", "lead_band", StatsGroupColumns.LeadBandName);
        Row(w, "fact", "parts", Num(StatsLayout.AllParts.Count));

        foreach (var part in StatsLayout.AllParts)
        {
            Row(w, "part", part.PageKey, part.Key, part.Title, Flag01(part.Cards));
            foreach (var item in StatsLayout.CatalogOf(part))
                Row(w, "item", part.PageKey, part.Key, item.Name, item.Where);
            var def = StatsLayout.DefaultOf(part);
            for (var i = 0; i < def.Count; i++)
            {
                Row(w, "def", part.PageKey, part.Key, Num(i),
                    def[i].IsSeparator ? "sep" : "col", def[i].Name, def[i].Text);
            }

            var use = order.Of(part.PageKey, part.Key);
            var hide = hidden.Of(part.PageKey, part.Key);
            var useOrder = use.Count > 0 || hide.Count > 0 ? use : [.. Texts(def)];

            if (part.Cards)
            {
                var natural = StatsCardLayout.NaturalOf(part);
                Emit(w, "cnat", part, natural);
                Emit(w, "clay", part, StatsCardLayout.Apply(natural, useOrder, hide));
                continue;
            }
            foreach (var (where, lead, agg) in StatsLayout.TablesOf(part))
            {
                Emit(w, "nat", part, where, StatsLayout.Apply(lead, agg, null, null));
                Emit(w, "lay", part, where, StatsLayout.Apply(lead, agg, useOrder, hide));
            }
        }
        w.Flush();
        return 0;
    }

    private static int Sections(StatsOrderMap map)
    {
        var n = 0;
        foreach (var _ in map.All()) n++;
        return n;
    }

    private static IEnumerable<string> Texts(IReadOnlyList<StatsLayoutEntry> entries)
    {
        foreach (var e in entries) yield return e.Text;
    }

    private static void Emit(TextWriter w, string kind, StatsLayoutPart part, string where,
                             StatsLayoutResult r)
    {
        for (var i = 0; i < r.Lead.Count; i++)
        {
            var x = r.Lead[i];
            Row(w, kind, part.PageKey, part.Key, where, "lead", Num(i), x.Name, x.Band,
                Flag01(x.IsGroupStart), Flag01(StatsLayout.IsPinned(x.Column)),
                x.Column.Label, Num((int)x.Column.Width));
        }
        for (var i = 0; i < r.Agg.Count; i++)
        {
            var x = r.Agg[i];
            Row(w, kind, part.PageKey, part.Key, where, "agg", Num(i), x.Name, x.Column.Band,
                Flag01(x.Column.IsGroupStart), "0", x.Column.Head, Num((int)x.Column.Width));
        }
    }

    private static void Emit(TextWriter w, string kind, StatsLayoutPart part,
                             IReadOnlyList<StatsCardGroup> groups)
    {
        for (var g = 0; g < groups.Count; g++)
        {
            for (var i = 0; i < groups[g].Cards.Count; i++)
                Row(w, kind, part.PageKey, part.Key, Num(g), Num(i), groups[g].Cards[i].Key);
        }
    }

    private static string Num(int n) => n.ToString(CultureInfo.InvariantCulture);

    private static string Flag01(bool b) => b ? "1" : "0";

    private static void Row(TextWriter w, params string[] cells)
        => w.WriteLine(string.Join(Separator, cells));
}
