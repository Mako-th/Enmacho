namespace TH09.Shell.Data;

internal readonly record struct SessionWhenValue(DateTime? At, bool HasTime, bool FromReplay);

internal sealed class SessionWhen
{
    private readonly Dictionary<long, (bool Scan, string? Date, string? Mtime)> _by = [];

    public static SessionWhenValue Resolve(string? startedAt, bool linked, bool scanLinked,
                                           string? replayDate, string? mtime)
    {
        if (linked)
        {
            var (at, hasTime) = ReplayListQuery.ResolvePlayedAt(replayDate, mtime);
            if (at is not null || scanLinked) return new SessionWhenValue(at, hasTime, true);
        }
        return new SessionWhenValue(ReplayListQuery.ParseMtime(startedAt), true, false);
    }

    public SessionWhenValue For(long sessionId, string? startedAt)
        => _by.TryGetValue(sessionId, out var x)
            ? Resolve(startedAt, true, x.Scan, x.Date, x.Mtime)
            : Resolve(startedAt, false, false, null, null);

    public static SessionWhen Load(TH09.Analysis.AnalysisDb db)
    {
        var pick = new Dictionary<long, (long Replay, bool Scan)>();
        TrackerDb.ForEachRow(db, RecordKinds.SqlLinks, r =>
        {
            var sid = r.GetInt64(0);
            var rid = r.GetInt64(1);
            var scan = RecordKinds.IsScanLink(TrackerDb.StringOrNull(r, 2));
            if (!pick.TryGetValue(sid, out var have)
                || (scan && !have.Scan)
                || (scan == have.Scan && rid < have.Replay))
                pick[sid] = (rid, scan);
        });

        var wanted = new HashSet<long>(pick.Values.Select(v => v.Replay));
        var dates = new Dictionary<long, (string? Date, string? Mtime)>();
        if (wanted.Count > 0)
            TrackerDb.ForEachRow(db, SqlReplayDates, r =>
            {
                var id = r.GetInt64(0);
                if (wanted.Contains(id))
                    dates[id] = (TrackerDb.StringOrNull(r, 1), TrackerDb.StringOrNull(r, 2));
            });

        var me = new SessionWhen();
        foreach (var (sid, (rid, scan)) in pick)
        {
            dates.TryGetValue(rid, out var d);
            me._by[sid] = (scan, d.Date, d.Mtime);
        }
        return me;
    }

    public const string SqlReplayDates = """
        SELECT r.replay_id,
               r.replay_date,
               r.mtime
          FROM replays r
        """;
}
