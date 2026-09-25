using TH09.Record;

namespace TH09.Shell.Data;

internal sealed record StatsLayoutEntry(string Name, bool IsSeparator)
{
    public string Text => IsSeparator ? ConfigStore.StatsSeparatorPrefix + Name : Name;

    public static StatsLayoutEntry Item(string name) => new(name, false);

    public static StatsLayoutEntry Separator(string name) => new(name, true);

    public static StatsLayoutEntry Parse(string text)
        => text.StartsWith(ConfigStore.StatsSeparatorPrefix, StringComparison.Ordinal)
            ? Separator(text[ConfigStore.StatsSeparatorPrefix.Length..])
            : Item(text);
}

internal sealed record StatsLayoutItem(string Name, string Where);

internal sealed record StatsLayoutPart(StatsSection Page, string Level, bool Cards)
{
    public string PageKey => StatsSections.Key(Page);

    public string Key => Level + "." + (Cards ? StatsLayout.KindCards : StatsLayout.KindColumns);

    public string Title
        => StatsLayout.LevelTitle(Level) + "の"
           + (Cards ? StatsLayout.CardsTitle : StatsLayout.ColumnsTitle);
}

internal sealed record StatsLayoutLead(StatsGroupColumn Column, string Name,
                                       string Band, bool IsGroupStart);

internal sealed record StatsLayoutAgg(StatsAggColumn Column, string Name);

internal sealed record StatsLayoutBand(string Band, bool IsStart, bool Continued)
{
    public List<StatsAggColumn> Columns { get; } = [];
}

internal sealed record StatsLayoutResult(IReadOnlyList<StatsLayoutLead> Lead,
                                         IReadOnlyList<StatsLayoutAgg> Agg);

internal static class StatsLayout
{
    public const string WherePlay = "プレイ";

    public const string WhereStage = "面";

    public const string WhereGain = "区間";

    public const string WhereMatch = "対戦";

    public const string WhereSeparator = " / ";

    public const string KindCards = "cards";

    public const string KindColumns = "cols";

    public const string LevelPlay = "play";

    public const string LevelStage = "stage";

    public const string LevelMatch = "match";

    public const string CardsTitle = "要約カード";

    public const string ColumnsTitle = "内訳表の列";

    public static string LevelTitle(string level) => level switch
    {
        LevelPlay => WherePlay,
        LevelStage => WhereStage,
        _ => WhereMatch,
    };

    public static string LevelOf(StatsSection page, int depth)
        => page != StatsSection.StoryExtra ? LevelMatch
           : depth >= 2 ? LevelStage : LevelPlay;

    public static StatsLayoutPart PartOf(StatsSection page, int depth, bool cards)
        => new(page, LevelOf(page, depth), cards);

    private static IReadOnlyList<StatsLayoutPart>? parts;

    public static IReadOnlyList<StatsLayoutPart> AllParts => parts ??= BuildParts();

    private static List<StatsLayoutPart> BuildParts()
    {
        var list = new List<StatsLayoutPart>();
        foreach (var page in ModeDisplay.Sections)
        {
            foreach (var level in LevelsOf(page))
            {
                list.Add(new StatsLayoutPart(page, level, Cards: true));
                list.Add(new StatsLayoutPart(page, level, Cards: false));
            }
        }
        return list;
    }

    public static IReadOnlyList<string> LevelsOf(StatsSection page)
        => page == StatsSection.StoryExtra ? [LevelPlay, LevelStage] : [LevelMatch];

    public static IEnumerable<(string Where, StatsGroupColumn[] Lead, StatsAggColumn[] Agg)>
        TablesOf(StatsLayoutPart part)
    {
        ArgumentNullException.ThrowIfNull(part);
        if (part.Cards) yield break;
        var lead = StatsGroupColumns.For(part.Page);
        if (part.Level == LevelPlay)
        {
            yield return (WherePlay, lead, AggOf(part.Page, 0, gain: false));
        }
        else if (part.Level == LevelStage)
        {
            yield return (WhereStage, lead, AggOf(part.Page, 2, gain: false));
            yield return (WhereGain, lead, AggOf(part.Page, 2, gain: true));
        }
        else
        {
            yield return (WhereMatch, lead, AggOf(part.Page, 0, gain: false));
        }
    }

    public static IEnumerable<(StatsLayoutPart Part, string Where,
                               StatsGroupColumn[] Lead, StatsAggColumn[] Agg)> AllTables()
    {
        foreach (var part in AllParts)
        {
            foreach (var (where, lead, agg) in TablesOf(part))
                yield return (part, where, lead, agg);
        }
    }

    private static readonly Dictionary<string, IReadOnlyList<StatsLayoutEntry>> defaults = [];
    private static readonly Dictionary<string, IReadOnlyList<StatsLayoutItem>> catalogs = [];

    public static IReadOnlyList<StatsLayoutEntry> DefaultOf(StatsLayoutPart part)
    {
        ArgumentNullException.ThrowIfNull(part);
        var key = CacheKey(part);
        if (!defaults.TryGetValue(key, out var got))
            defaults[key] = got = part.Cards ? BuildCardDefault(part) : BuildDefault(part);
        return got;
    }

    public static IReadOnlyList<StatsLayoutItem> CatalogOf(StatsLayoutPart part)
    {
        ArgumentNullException.ThrowIfNull(part);
        var key = CacheKey(part);
        if (!catalogs.TryGetValue(key, out var got))
            catalogs[key] = got = part.Cards ? BuildCardCatalog(part) : BuildCatalog(part);
        return got;
    }

    private static string CacheKey(StatsLayoutPart part) => part.PageKey + "/" + part.Key;

    public static string NameOf(StatsGroupColumn c) => c.Label;

    public static string NameOf(StatsAggColumn c) => c.Label;

    public static bool IsPinned(StatsGroupColumn c) => c.Field == StatsGroupField.Name;

    public static IReadOnlyList<StatsLayoutEntry> ParseOrder(IReadOnlyList<string>? order)
    {
        if (order is null || order.Count == 0) return [];
        var list = new List<StatsLayoutEntry>(order.Count);
        foreach (var text in order) list.Add(StatsLayoutEntry.Parse(text));
        return list;
    }

    private static List<StatsLayoutEntry> NaturalEntries(StatsGroupColumn[] lead, StatsAggColumn[] agg)
    {
        var list = new List<StatsLayoutEntry> { StatsLayoutEntry.Separator(StatsGroupColumns.LeadBandName) };
        foreach (var c in lead)
        {
            if (!IsPinned(c)) list.Add(StatsLayoutEntry.Item(NameOf(c)));
        }
        foreach (var c in agg)
        {
            if (c.IsGroupStart) list.Add(StatsLayoutEntry.Separator(c.Band));
            list.Add(StatsLayoutEntry.Item(NameOf(c)));
        }
        return list;
    }

    public static StatsLayoutResult Apply(StatsGroupColumn[] lead, StatsAggColumn[] agg,
                                          IReadOnlyList<string>? order,
                                          IEnumerable<string>? hidden)
    {
        var hide = new HashSet<string>(hidden ?? [], StringComparer.Ordinal);
        if ((order is null || order.Count == 0) && hide.Count == 0) return Natural(lead, agg);

        var entries = order is { Count: > 0 } ? ParseOrder(order) : NaturalEntries(lead, agg);
        var leadBy = new Dictionary<string, StatsGroupColumn>(StringComparer.Ordinal);
        foreach (var c in lead)
        {
            if (!IsPinned(c)) leadBy[NameOf(c)] = c;
        }
        var aggBy = new Dictionary<string, StatsAggColumn>(StringComparer.Ordinal);
        foreach (var c in agg) aggBy[NameOf(c)] = c;

        var leadBand = StatsGroupColumns.LeadBandName;
        foreach (var e in entries)
        {
            if (!e.IsSeparator) continue;
            leadBand = e.Name;
            break;
        }

        var leadCols = new List<StatsGroupColumn>();
        var aggCols = new List<(StatsAggColumn Column, string Band)>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        var band = leadBand;
        foreach (var e in entries)
        {
            if (e.IsSeparator) { band = e.Name; continue; }
            if (!used.Add(e.Name)) continue;
            if (hide.Contains(e.Name)) continue;
            if (leadBy.TryGetValue(e.Name, out var lc)) { leadCols.Add(lc); continue; }
            if (aggBy.TryGetValue(e.Name, out var ac)) aggCols.Add((ac, band));
        }

        foreach (var c in lead)
        {
            if (IsPinned(c) || used.Contains(NameOf(c)) || hide.Contains(NameOf(c))) continue;
            leadCols.Add(c);
        }
        var tail = "";
        foreach (var c in agg)
        {
            if (c.IsGroupStart) tail = c.Band;
            if (used.Contains(NameOf(c)) || hide.Contains(NameOf(c))) continue;
            aggCols.Add((c, tail));
        }

        var outLead = new List<StatsLayoutLead>(leadCols.Count + 1);
        foreach (var c in lead)
        {
            if (IsPinned(c))
                outLead.Add(new StatsLayoutLead(c, NameOf(c), leadBand, IsGroupStart: true));
        }
        foreach (var c in leadCols)
            outLead.Add(new StatsLayoutLead(c, NameOf(c), leadBand, IsGroupStart: false));

        var bands = new List<StatsLayoutBand> { new(leadBand, IsStart: false, Continued: true) };
        foreach (var (c, b) in aggCols)
        {
            if (!string.Equals(b, bands[^1].Band, StringComparison.Ordinal))
                bands.Add(new StatsLayoutBand(b, IsStart: true, Continued: false));
            bands[^1].Columns.Add(c);
        }
        var outAgg = new List<StatsLayoutAgg>(aggCols.Count);
        foreach (var g in bands)
        {
            var kinds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in g.Columns) kinds.Add(c.Band);
            var mixed = g.Continued || kinds.Count > 1;
            for (var i = 0; i < g.Columns.Count; i++)
                outAgg.Add(Reband(g.Columns[i], g.Band, g.IsStart && i == 0, mixed));
        }
        return new StatsLayoutResult(outLead, outAgg);
    }

    private static StatsLayoutResult Natural(StatsGroupColumn[] lead, StatsAggColumn[] agg)
    {
        var outLead = new List<StatsLayoutLead>(lead.Length);
        for (var i = 0; i < lead.Length; i++)
        {
            outLead.Add(new StatsLayoutLead(lead[i], NameOf(lead[i]),
                                            StatsGroupColumns.LeadBandName, i == 0));
        }
        var outAgg = new List<StatsLayoutAgg>(agg.Length);
        foreach (var c in agg) outAgg.Add(new StatsLayoutAgg(c, NameOf(c)));
        return new StatsLayoutResult(outLead, outAgg);
    }

    private static StatsLayoutAgg Reband(StatsAggColumn c, string band, bool groupStart, bool mixed)
    {
        var name = NameOf(c);
        var col = c with { Band = band, IsGroupStart = groupStart };
        if (mixed && c.Form != StatsAggNameForm.HeadOnly)
        {
            col = col with
            {
                Head = name,
                Form = StatsAggNameForm.HeadOnly,
                Width = c.Width + Math.Max(0, name.Length - c.Head.Length) * HeaderCharWidth,
            };
        }
        return new StatsLayoutAgg(col, name);
    }

    private const double HeaderCharWidth = 12;

    private static List<StatsLayoutEntry> BuildDefault(StatsLayoutPart part)
    {
        var list = new List<StatsLayoutEntry> { StatsLayoutEntry.Separator(StatsGroupColumns.LeadBandName) };
        var tables = TablesOf(part).ToList();
        var lead = new List<List<string>>();
        foreach (var (_, cols, _) in tables)
        {
            var xs = new List<string>();
            foreach (var c in cols)
            {
                if (!IsPinned(c)) xs.Add(NameOf(c));
            }
            lead.Add(xs);
        }
        foreach (var name in Merge(lead)) list.Add(StatsLayoutEntry.Item(name));
        foreach (var (stat, name, _) in StatsAggColumns.Bands)
        {
            list.Add(StatsLayoutEntry.Separator(name));
            var agg = new List<List<string>>();
            foreach (var (_, _, cols) in tables)
            {
                var xs = new List<string>();
                foreach (var c in cols)
                {
                    if (c.Stat == stat) xs.Add(NameOf(c));
                }
                agg.Add(xs);
            }
            foreach (var n in Merge(agg)) list.Add(StatsLayoutEntry.Item(n));
        }
        return list;
    }

    private static List<StatsLayoutEntry> BuildCardDefault(StatsLayoutPart part)
    {
        var list = new List<StatsLayoutEntry>();
        var groups = StatsCardLayout.NaturalOf(part);
        for (var i = 0; i < groups.Count; i++)
        {
            if (i > 0) list.Add(StatsLayoutEntry.Separator(""));
            foreach (var card in groups[i].Cards) list.Add(StatsLayoutEntry.Item(card.Key));
        }
        return list;
    }

    private static List<string> Merge(List<List<string>> sequences)
    {
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var xs in sequences)
        {
            foreach (var x in xs)
            {
                if (!index.ContainsKey(x)) index[x] = index.Count;
            }
        }
        var after = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var waits = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var name in index.Keys)
        {
            after[name] = new HashSet<string>(StringComparer.Ordinal);
            waits[name] = 0;
        }
        foreach (var xs in sequences)
        {
            for (var i = 1; i < xs.Count; i++)
            {
                if (after[xs[i - 1]].Add(xs[i])) waits[xs[i]]++;
            }
        }
        var result = new List<string>(index.Count);
        var left = new List<string>(index.Keys);
        left.Sort((a, b) => index[a].CompareTo(index[b]));
        while (left.Count > 0)
        {
            var pick = left.FindIndex(x => waits[x] == 0);
            if (pick < 0) { result.AddRange(left); break; }
            var name = left[pick];
            left.RemoveAt(pick);
            result.Add(name);
            foreach (var next in after[name]) waits[next]--;
        }
        return result;
    }

    private static List<StatsLayoutItem> BuildCatalog(StatsLayoutPart part)
    {
        var where = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        void Note(string name, string table)
        {
            if (!where.TryGetValue(name, out var xs)) where[name] = xs = [];
            if (!xs.Contains(table)) xs.Add(table);
        }
        foreach (var (table, lead, agg) in TablesOf(part))
        {
            if (table != WhereGain)
            {
                foreach (var c in lead)
                {
                    if (!IsPinned(c)) Note(NameOf(c), table);
                }
            }
            foreach (var c in agg) Note(NameOf(c), table);
        }
        var items = new List<StatsLayoutItem>(where.Count);
        foreach (var e in DefaultOf(part))
        {
            if (e.IsSeparator || !where.TryGetValue(e.Name, out var xs)) continue;
            items.Add(new StatsLayoutItem(e.Name, string.Join(WhereSeparator, xs)));
        }
        return items;
    }

    private static List<StatsLayoutItem> BuildCardCatalog(StatsLayoutPart part)
    {
        var items = new List<StatsLayoutItem>();
        foreach (var e in DefaultOf(part))
        {
            if (!e.IsSeparator) items.Add(new StatsLayoutItem(e.Name, ""));
        }
        return items;
    }

    private static StatsAggColumn[] AggOf(StatsSection section, int depth, bool gain)
        => gain ? StatsAggColumns.Gain(section, depth) : StatsAggColumns.For(section, depth);
}
