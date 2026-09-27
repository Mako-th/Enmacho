using TH09.Analysis;
using TH09.Record;

namespace TH09.Shell.Data;

internal enum RecordKind
{
    Scan,

    Replay,

    SessionOnly,
}

internal static class RecordKinds
{
    public const string Header = "種別";

    public const double Width = 140;

    public const string ScanText = "走査";

    public const string ReplayText = "リプレイ";

    public const string SessionOnlyText = "セッション記録のみ";

    public static RecordKind Of(bool scanLinked, bool hasReplay)
        => scanLinked ? RecordKind.Scan
         : hasReplay ? RecordKind.Replay
                     : RecordKind.SessionOnly;

    public static bool IsScanLink(string? linkMethod)
        => string.Equals(linkMethod, ScanLink.Method, StringComparison.Ordinal);

    public static string Text(RecordKind kind) => kind switch
    {
        RecordKind.Scan => ScanText,
        RecordKind.Replay => ReplayText,
        _ => SessionOnlyText,
    };

    public const string SqlLinks = """
        SELECT sr.session_id,
               sr.replay_id,
               sr.link_method
          FROM session_replays sr
         ORDER BY sr.session_id, sr.replay_id
        """;

    public static ScanLinkIds ReadScanLinks(AnalysisDb db)
    {
        ArgumentNullException.ThrowIfNull(db);
        var sessions = new HashSet<long>();
        var replays = new HashSet<long>();
        var pairs = new HashSet<(long, long)>();
        TrackerDb.ForEachRow(db, SqlLinks, r =>
        {
            if (!IsScanLink(TrackerDb.StringOrNull(r, 2))) return;
            sessions.Add(r.GetInt64(0));
            replays.Add(r.GetInt64(1));
            pairs.Add((r.GetInt64(0), r.GetInt64(1)));
        });
        return new ScanLinkIds(sessions, replays, pairs);
    }
}

internal sealed record ScanLinkIds(IReadOnlySet<long> Sessions, IReadOnlySet<long> Replays,
                                   IReadOnlySet<(long Session, long Replay)> Pairs)
{
    public static ScanLinkIds Empty { get; } =
        new(new HashSet<long>(), new HashSet<long>(), new HashSet<(long, long)>());
}
