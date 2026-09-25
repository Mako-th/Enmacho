using System.Globalization;
using System.Text;

namespace TH09.Shell.Data;

internal static class StatsLeafDump
{
    public static readonly string[] Kinds =
        ["cols", "tips", "sec", "row", "drill", "card", "cardsrc", "leaf", "empty", "mx", "agg",
         "aggcell", "fixed", "leadcols", "agg2", "aggcell2", "sub", "subcell"];

    public static readonly string[] Extras = ["#own", "#prov", "#frames", "#rounds"];

    public static IEnumerable<int> ProbeSelf
        => Enumerable.Range(0, StatsCharFilter.CharacterCount).Where(i => i % 2 == 0);

    public static IEnumerable<int> ProbeFoe
        => Enumerable.Range(0, Math.Min(7, StatsCharFilter.CharacterCount));

    public const string ProbeKey = "1c";

    private const char Sep = '\t';

    public static int Run(string dbPath)
    {
        using var stdout = new StreamWriter(Console.OpenStandardOutput(),
                                            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        try
        {
            TrackerDb.MainDbPath = dbPath;
            if (!TrackerDb.MainDbExists)
            {
                Console.Error.WriteLine("本体 DB が見つかりません: " + dbPath);
                return 2;
            }

            using var db = TrackerDb.OpenMainDb();
            var payload = StatsLeafQuery.LoadAll(db);

            var sb = new StringBuilder();
            Line(sb, "sec", "*", "sessions", payload.SessionCount.ToString(CultureInfo.InvariantCulture),
                 "myname", payload.MyName ?? "");
            foreach (var (name, count) in payload.OwnNames)
                Line(sb, "sec", "*", "ownname", name, count.ToString(CultureInfo.InvariantCulture));
            foreach (var (name, count) in payload.DroppedNames)
                Line(sb, "sec", "*", "dropname", name, count.ToString(CultureInfo.InvariantCulture));
            Line(sb, "sec", "*", "untagged",
                 payload.Untagged.ToString(CultureInfo.InvariantCulture));
            Line(sb, "sec", "*", "nameused",
                 payload.NameUsed.ToString(CultureInfo.InvariantCulture),
                 payload.NameConflict.ToString(CultureInfo.InvariantCulture));
            Line(sb, "sec", "*", "notesessions", StatsNotes.Sessions(payload));
            Line(sb, "sec", "*", "notecounts", StatsNotes.Counts(payload));
            var defs = StatsDefinitions.All(payload, ViewModels.StatsTabViewModel.HistoryNote);
            for (var i = 0; i < defs.Count; i++)
                Line(sb, "sec", "*", "define", i.ToString(CultureInfo.InvariantCulture), defs[i]);
            Line(sb, "sec", "*", "charfilter", ProbeKey,
                 string.Join(",", ProbeSelf.Select(i => i.ToString(CultureInfo.InvariantCulture))),
                 string.Join(",", ProbeFoe.Select(i => i.ToString(CultureInfo.InvariantCulture))));

            foreach (var section in StatsSections.All)
            {
                var key = StatsSections.Key(section);
                var cols = StatsLeafColumns.For(section);
                Line(sb, "cols", key, [.. cols.Select(c => c.Label), .. Extras]);
                Line(sb, "tips", key, [.. cols.Select(c => c.Tip)]);
                Line(sb, "leadcols", key,
                     [.. StatsGroupColumns.For(section).Select(c => c.Label)]);

                var whole = new StatsView(payload, section, [], foreign: true, StatsCharFilter.All);
                Line(sb, "sec", key, StatsSections.Title(section),
                     whole.SectionCount.ToString(CultureInfo.InvariantCulture),
                     "skip", payload.Skips[section].ToString(CultureInfo.InvariantCulture),
                     "plays", (section == StatsSection.StoryExtra ? payload.Plays.Count : 0)
                         .ToString(CultureInfo.InvariantCulture));

                foreach (var row in Sorted(whole))
                    Line(sb, "row", key, Cells(row, cols));

                foreach (var foreign in new[] { false, true })
                    Walk(sb, payload, section, foreign ? "1" : "0", foreign, StatsCharFilter.All);
                Walk(sb, payload, section, ProbeKey, true, new StatsCharFilter(ProbeSelf, ProbeFoe));
            }

            stdout.Write(sb.ToString());
            stdout.Flush();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
    }

    private static void Walk(StringBuilder sb, StatsPayload payload, StatsSection section,
                             string f, bool foreign, StatsCharFilter chars)
    {
        var key = StatsSections.Key(section);
        var path = new List<string>();
        while (true)
        {
            var view = new StatsView(payload, section, path, foreign, chars);
            var where = string.Join("/", path);
            var groups = view.Groups();

            var cards = view.Cards();
            for (var gi = 0; gi < cards.Count; gi++)
                foreach (var c in cards[gi].Cards)
                {
                    Line(sb, "card", key, f, where, gi.ToString(CultureInfo.InvariantCulture),
                         c.Key, c.Value, c.Note);
                    if (c.HasSource)
                        Line(sb, "cardsrc", key, f, where, gi.ToString(CultureInfo.InvariantCulture),
                             c.Key, c.Source);
                }

            var fixedItems = view.Fixed();
            for (var i = 0; i < fixedItems.Count; i++)
                Line(sb, "fixed", key, f, where, i.ToString(CultureInfo.InvariantCulture),
                     fixedItems[i].Key, fixedItems[i].Value);

            var aggCols = view.GroupColumns;
            for (var i = 0; i < aggCols.Length; i++)
                Line(sb, "agg", key, f, where, i.ToString(CultureInfo.InvariantCulture),
                     aggCols[i].Label, aggCols[i].Tip, aggCols[i].IsGroupStart ? "1" : "0");

            var gainCols = view.GroupColumnsGain;
            for (var i = 0; i < gainCols.Length; i++)
                Line(sb, "agg2", key, f, where, i.ToString(CultureInfo.InvariantCulture),
                     gainCols[i].Label, gainCols[i].Tip, gainCols[i].IsGroupStart ? "1" : "0");
            foreach (var g in view.GroupsGain())
            {
                if (g.Cells.Count != gainCols.Length)
                    throw new InvalidOperationException(
                        "2 枚目の升目の数が列の数と合わない: " + g.Cells.Count + " / " + gainCols.Length);
                for (var i = 0; i < g.Cells.Count; i++)
                    Line(sb, "aggcell2", key, f, where, g.Key,
                         i.ToString(CultureInfo.InvariantCulture),
                         g.Cells[i].Text, g.Cells[i].Tip, g.Cells[i].IsGroupStart ? "1" : "0");
            }

            DumpSubs(sb, key, f, where, "reach", groups, aggCols);
            DumpSubs(sb, key, f, where, "gain", view.GroupsGain(), gainCols);

            foreach (var g in groups)
            {
                Line(sb, "drill", key, f, where, g.Key, g.Label,
                     g.Count.ToString(CultureInfo.InvariantCulture),
                     g.PlayCount.ToString(CultureInfo.InvariantCulture),
                     g.Wins.ToString(CultureInfo.InvariantCulture),
                     g.Losses.ToString(CultureInfo.InvariantCulture),
                     g.WinRate, g.RoundWinRate,
                     g.CushionSpec,
                     string.Join(",", g.Lead.Select((c, i) => (c, i))
                                            .Where(x => x.c.Cushion > 0)
                                            .Select(x => x.i.ToString(CultureInfo.InvariantCulture))));
                if (g.Cells.Count != aggCols.Length)
                    throw new InvalidOperationException(
                        "集計の升目の数が列の数と合わない: " + g.Cells.Count + " / " + aggCols.Length);
                for (var i = 0; i < g.Cells.Count; i++)
                    Line(sb, "aggcell", key, f, where, g.Key,
                         i.ToString(CultureInfo.InvariantCulture),
                         g.Cells[i].Text, g.Cells[i].Tip, g.Cells[i].IsGroupStart ? "1" : "0");
            }

            if (view.Matrix() is StatsMatrix mx)
            {
                Line(sb, "mx", key, f, where, "head", mx.Title,
                     mx.SourceCount.ToString(CultureInfo.InvariantCulture),
                     mx.OutsideCount.ToString(CultureInfo.InvariantCulture),
                     mx.Rows.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var text in mx.Legend) Line(sb, "mx", key, f, where, "legend", text);
                for (var r = 0; r < mx.Rows.Count; r++)
                {
                    var cells = mx.Rows[r].Cells;
                    for (var c = 0; c < cells.Count; c++)
                        Line(sb, "mx", key, f, where, "cell",
                             r.ToString(CultureInfo.InvariantCulture),
                             c.ToString(CultureInfo.InvariantCulture),
                             cells[c].Text, cells[c].Tip, cells[c].ColorSpec,
                             cells[c].Pick is IReadOnlyList<string> p ? string.Join("/", p) : "",
                             cells[c].IsHeader ? "1" : "0", cells[c].IsSelf ? "1" : "0");
                }
            }

            if (view.IsLeaf)
            {
                var cols = StatsLeafColumns.For(section);
                foreach (var row in view.LeafRows())
                    Line(sb, "leaf", key, [f, where, .. Cells(row, cols)]);
                return;
            }
            if (groups.Count == 0)
            {
                Line(sb, "empty", key, f, where,
                     view.RowCount.ToString(CultureInfo.InvariantCulture));
                return;
            }
            path.Add(groups[0].Key);
        }
    }

    private static void DumpSubs(StringBuilder sb, string key, string f, string where, string table,
                                 IReadOnlyList<StatsGroup> groups, StatsAggColumn[] cols)
    {
        foreach (var g in groups)
        {
            foreach (var sub in g.Subs)
            {
                Line(sb, "sub", key, f, where, table, g.Key, sub.Key, sub.Label,
                     sub.Count.ToString(CultureInfo.InvariantCulture),
                     string.Join("/", sub.Drill));
                if (sub.Cells.Count != cols.Length)
                    throw new InvalidOperationException(
                        "小区分の升目の数が列の数と合わない: " + sub.Cells.Count + " / " + cols.Length);
                for (var i = 0; i < sub.Cells.Count; i++)
                    Line(sb, "subcell", key, f, where, table, g.Key, sub.Key,
                         i.ToString(CultureInfo.InvariantCulture),
                         sub.Cells[i].Text, sub.Cells[i].Tip);
            }
        }
    }

    private static List<IStatsLeafRow> Sorted(StatsView whole)
        => StatsLeafQuery.SortLeaf(
            whole.Section == StatsSection.StoryExtra ? [.. whole.StageRows] : [.. whole.MatchRows],
            whole.Columns);

    private static string[] Cells(IStatsLeafRow row, StatsLeafColumn[] cols)
    {
        var cells = row.Cells.Select(c => c.Text).ToList();
        var (own, prov, frames) = row switch
        {
            StatsMatchRow m => (m.IsOwn, m.IsProvisional, m.Frames),
            StatsStageRow s => (s.IsOwn, false, (long?)s.Frames),
            _ => throw new InvalidOperationException("知らない葉の行: " + row.GetType().Name),
        };
        cells.Add(own ? "1" : "0");
        cells.Add(prov ? "1" : "0");
        cells.Add(frames?.ToString(CultureInfo.InvariantCulture) ?? "");
        cells.Add(string.Join("|", row.Rounds.Select(x => string.Join(",",
            x.Number?.ToString(CultureInfo.InvariantCulture) ?? "",
            x.Frames?.ToString(CultureInfo.InvariantCulture) ?? "",
            x.Status ?? "",
            x.WinnerSide?.ToString(CultureInfo.InvariantCulture) ?? "",
            x.IsSelfWin ? "1" : "0",
            x.IsCurrent ? "1" : "0",
            x.HasNext ? "1" : "0"))));
        if (cells.Count != cols.Length + Extras.Length)
            throw new InvalidOperationException("升目の数が列の数と合わない（列を足して吐き方を書き忘れた）");
        return [.. cells];
    }

    private static void Line(StringBuilder sb, string kind, string section, params string[] fields)
    {
        if (Array.IndexOf(Kinds, kind) < 0)
            throw new InvalidOperationException("StatsLeafDump.Kinds に " + kind + " が無い");
        sb.Append(kind).Append(Sep).Append(section);
        foreach (var f in fields) sb.Append(Sep).Append(Clean(f));
        sb.Append('\n');
    }

    private static string Clean(string v)
        => v.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
}
