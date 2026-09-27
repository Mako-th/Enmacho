using TH09.Record;

namespace TH09.Shell.Data;

internal static class ModeDisplay
{
    public const string StoryLabel = "Story";

    public const string ExtraLabel = "Extra";

    private static readonly string[] Ranked =
    [
        CaptureScope.Story, CaptureScope.Extra,
        CaptureScope.Cpu, CaptureScope.Local, CaptureScope.Net,
    ];

    public static int Rank(string scope)
    {
        var i = Array.IndexOf(Ranked, scope);
        return i < 0 ? Ranked.Length : i;
    }

    public static IReadOnlyList<string> Scopes { get; } =
        [.. CaptureScope.All.OrderBy(Rank)];

    public static string Label(string scope)
    {
        if (string.Equals(scope, CaptureScope.Story, StringComparison.Ordinal)) return StoryLabel;
        if (string.Equals(scope, CaptureScope.Extra, StringComparison.Ordinal)) return ExtraLabel;
        if (string.Equals(scope, CaptureScope.Cpu, StringComparison.Ordinal))
            return StatsSections.Title(StatsSection.MatchCpu);
        if (string.Equals(scope, CaptureScope.Local, StringComparison.Ordinal))
            return StatsSections.Title(StatsSection.MatchLocal);
        if (string.Equals(scope, CaptureScope.Net, StringComparison.Ordinal))
            return StatsSections.Title(StatsSection.MatchNet);
        return CaptureScope.Label(scope);
    }

    private static string ScopeOf(StatsSection s) => s switch
    {
        StatsSection.StoryExtra => CaptureScope.Story,
        StatsSection.MatchCpu => CaptureScope.Cpu,
        StatsSection.MatchNet => CaptureScope.Net,
        _ => CaptureScope.Local,
    };

    public static IReadOnlyList<StatsSection> Sections { get; } =
        [.. StatsSections.All.OrderBy(s => Rank(ScopeOf(s)))];
}
