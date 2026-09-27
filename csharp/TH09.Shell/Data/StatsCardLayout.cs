namespace TH09.Shell.Data;

internal static class StatsCardLayout
{
    public static IReadOnlyList<StatsCardGroup> NaturalOf(StatsLayoutPart part)
    {
        ArgumentNullException.ThrowIfNull(part);
        if (part.Page != StatsSection.StoryExtra)
        {
            var groups = StatsLeafQuery.MatchCards([]);
            if (groups.Count > 0)
            {
                var last = groups[^1];
                groups[^1] = new StatsCardGroup([.. last.Cards,
                    new StatsCard(StatsLeafQuery.ProvisionalCardKey, "", "")]);
            }
            return groups;
        }
        return part.Level == StatsLayout.LevelStage
            ? StatsLeafQuery.StoryStageCards([])
            : StatsLeafQuery.StoryPlayCards([]);
    }

    public static List<StatsCardGroup> Apply(IReadOnlyList<StatsCardGroup> groups,
                                             IReadOnlyList<string>? order,
                                             IEnumerable<string>? hidden)
    {
        ArgumentNullException.ThrowIfNull(groups);
        var hide = new HashSet<string>(hidden ?? [], StringComparer.Ordinal);
        if ((order is null || order.Count == 0) && hide.Count == 0) return [.. groups];

        var byName = new Dictionary<string, StatsCard>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            foreach (var card in group.Cards) byName.TryAdd(card.Key, card);
        }

        var outGroups = new List<StatsCardGroup>();
        var current = new List<StatsCard>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        void Flush()
        {
            if (current.Count == 0) return;
            outGroups.Add(new StatsCardGroup(current));
            current = [];
        }
        var entries = order is { Count: > 0 } ? StatsLayout.ParseOrder(order) : NaturalEntries(groups);
        foreach (var entry in entries)
        {
            if (entry.IsSeparator) { Flush(); continue; }
            if (!used.Add(entry.Name)) continue;
            if (hide.Contains(entry.Name)) continue;
            if (byName.TryGetValue(entry.Name, out var card)) current.Add(card);
        }
        Flush();

        var tail = -1;
        for (var g = 0; g < groups.Count; g++)
        {
            foreach (var card in groups[g].Cards)
            {
                if (used.Contains(card.Key) || hide.Contains(card.Key)) continue;
                if (tail != g) { Flush(); tail = g; }
                current.Add(card);
            }
        }
        Flush();
        return outGroups;
    }

    private static List<StatsLayoutEntry> NaturalEntries(IReadOnlyList<StatsCardGroup> groups)
    {
        var list = new List<StatsLayoutEntry>();
        for (var i = 0; i < groups.Count; i++)
        {
            if (i > 0) list.Add(StatsLayoutEntry.Separator(""));
            foreach (var card in groups[i].Cards) list.Add(StatsLayoutEntry.Item(card.Key));
        }
        return list;
    }
}
