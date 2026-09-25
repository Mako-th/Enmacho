using TH09.Record;

namespace TH09.Shell.Data;

internal static class StreamCompareFormat
{
    public const string Dash = "—";

    public const int LabelWidth = 7;

    public const string Gap = " ";


    public static IReadOnlyList<StreamSpotRow> PickSpots(StreamPanelView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.Match is { } m) return [m];
        if (view.Stages.Count == 0) return [];
        long? target = null;
        foreach (var s in view.Stages)
            if (s.Round is null && s.Running) { target = s.Stage; break; }
        target ??= view.Stages[^1].Stage;
        return [.. view.Stages.Where(s => s.Stage == target)];
    }

    public static IReadOnlyList<PlayInsertBlock> ComposeBlocks(StreamPanelView view,
                                                               IReadOnlyList<string>? hidden = null,
                                                               bool narrow = false)
        => [.. PickSpots(view).Select(
               s => new PlayInsertBlock(s.Stage, s.Round, ComposeItemLines(s, hidden, narrow)))];

    public static IReadOnlyList<PlayTextLine> ComposeItemLines(StreamSpotRow spot,
                                                               IReadOnlyList<string>? hidden = null,
                                                               bool narrow = false)
    {
        ArgumentNullException.ThrowIfNull(spot);
        var asTime = spot.IsTime;
        IReadOnlyList<StreamCompareItem> items;
        if (hidden is null || hidden.Count == 0) items = spot.Items;
        else
        {
            var visible = new HashSet<string>(StreamBests.Visible(hidden), StringComparer.Ordinal);
            items = [.. spot.Items.Where(i => visible.Contains(i.Kind))];
        }
        var valueWidth = 0;
        var deltaWidth = 0;
        foreach (var i in items)
        {
            valueWidth = Math.Max(valueWidth, ValueText(i, asTime).Length);
            deltaWidth = Math.Max(deltaWidth, DeltaText(i, asTime).Length);
        }
        return [.. items.Select(i => ComposeItem(i, asTime, valueWidth, deltaWidth, narrow))];
    }

    private static string ValueText(StreamCompareItem item, bool asTime)
        => item.Value is long v ? (asTime ? ReplayFormat.FineFrames(v) : LiveFormat.Grouped(v)) : Dash;

    private static string DeltaText(StreamCompareItem item, bool asTime)
        => item.Delta is long d ? (asTime ? ReplayFormat.SignedFineFrames(d) : LiveFormat.SignedGrouped(d))
                                : Dash;

    private static PlayTextLine ComposeItem(StreamCompareItem item, bool asTime,
                                            int valueWidth, int deltaWidth, bool narrow)
    {
        var label = (item.Kind + ":").PadRight(LabelWidth);
        var value = ValueText(item, asTime);
        var delta = DeltaText(item, asTime);
        var spans = new List<PlaySpan>
        {
            PlaySpan.Label(label + " "),
            PlaySpan.Body(new string(' ', Math.Max(valueWidth - value.Length, 0))),
            PlaySpan.Value(value),
            PlaySpan.Body(Gap + new string(' ', Math.Max(deltaWidth - delta.Length, 0))),
            PlaySpan.Signed(delta),
        };
        if (!narrow && item.Source is BestSource src)
            spans.Add(PlaySpan.Label("  " + StatsBestSource.Label(src, includeReplayId: false)));
        if (item.Fallback)
            spans.Add(PlaySpan.Label(item.UsedRound is long ur ? "（R" + ur + "）" : "（面全体）"));
        return new PlayTextLine(spans);
    }
}
