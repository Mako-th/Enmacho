using Microsoft.Data.Sqlite;

namespace TH09.Record;

public sealed record PbRun(BestSource Source, IReadOnlyDictionary<long, long> StageEndRaw,
                           IReadOnlyDictionary<long, long> StageEndNet,
                           IReadOnlyDictionary<(long Stage, long Round), long> RoundEndRaw);

public sealed record PbValue(long Value, bool NetKnown, BestSource Source);

public sealed record StreamCompareItem(string Kind, long? Value, long? Delta, BestSource? Source,
                                       long? UsedRound, bool Fallback);

public sealed record StreamSpotRow(long? Stage, long? Round, long? Opponent, bool Running, bool Won,
                                   long? CurrentSegment, long? CurrentTotal,
                                   IReadOnlyList<StreamCompareItem> Items)
{
    public bool IsTime => Stage is null;
}

public sealed record StreamPanelView(long Session, long? Mode, long? Difficulty, long? Character,
                                     IReadOnlyList<StreamSpotRow> Stages, StreamSpotRow? Match);

public static class StreamBests
{
    public const string KindSb = "SB";

    public const string KindPb = "PB";

    public const string KindTarget = "Target";

    public const string KindWr = "WR";

    public static readonly string[] AllKinds = [KindSb, KindPb, KindTarget, KindWr];

    public static IReadOnlyList<string> Visible(IReadOnlyList<string>? hidden)
    {
        if (hidden is null || hidden.Count == 0) return AllKinds;
        var set = new HashSet<string>(hidden, StringComparer.Ordinal);
        return [.. AllKinds.Where(k => !set.Contains(k))];
    }


    public static PbRun? PlayerBestRun(SqliteConnection c, long? mode, long? character, long? difficulty,
                                       long? excludeSid = null, bool ownOnly = true)
    {
        ArgumentNullException.ThrowIfNull(c);
        SessionProtection.FinalScoreBest? fin = null;
        foreach (var f in SessionProtection.FinalScoreBests(c, ownOnly: ownOnly, excludeSid: excludeSid))
        {
            if (f.Mode != mode || f.Character != character || f.Difficulty != difficulty) continue;
            fin = f;
            break;
        }
        if (fin is null) return null;

        if (fin.Source is BestSource.Live or BestSource.Scan)
        {
            var sid = fin.SessionId!.Value;
            var idx = SelfBests.LoadClearBonusIndex(c);
            var stageOf = new Dictionary<long, long>();
            var stageEndRaw = new Dictionary<long, long>();
            var stageEndNet = new Dictionary<long, long>();
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "SELECT stage_record_id,stage_number,score_at_end,clear_bonus_id"
                                + " FROM stages WHERE session_id=$0 ORDER BY stage_record_id";
                cmd.Parameters.AddWithValue("$0", sid);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    if (r.IsDBNull(1) || r.IsDBNull(2)) continue;
                    var srid = r.GetInt64(0);
                    var stage = r.GetInt64(1);
                    var end = r.GetInt64(2);
                    long? cbid = r.IsDBNull(3) ? null : r.GetInt64(3);
                    stageOf[srid] = stage;
                    stageEndRaw[stage] = end;
                    if (idx.Resolve(sid, cbid, stage) is long bonus) stageEndNet[stage] = end - bonus;
                }
            }

            var roundEndRaw = new Dictionary<(long, long), long>();
            var byStage = new Dictionary<long, List<(long Round, long? StartScore)>>();
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "SELECT stage_record_id,round_number,score_1_at_start"
                                + " FROM rounds WHERE session_id=$0 ORDER BY stage_record_id,round_record_id";
                cmd.Parameters.AddWithValue("$0", sid);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    if (r.IsDBNull(0) || r.IsDBNull(1)) continue;
                    var srid = r.GetInt64(0);
                    var rn = r.GetInt64(1);
                    long? start = r.IsDBNull(2) ? null : r.GetInt64(2);
                    if (!byStage.TryGetValue(srid, out var list)) byStage[srid] = list = [];
                    list.Add((rn, start));
                }
            }
            foreach (var (srid, list) in byStage)
            {
                if (!stageOf.TryGetValue(srid, out var stage)) continue;
                for (var i = 0; i + 1 < list.Count; i++)
                    if (list[i + 1].StartScore is long ns)
                        roundEndRaw[(stage, list[i].Round)] = ns;
            }

            return new PbRun(new BestSource(fin.Source, sid, fin.ReplayId), stageEndRaw, stageEndNet, roundEndRaw);
        }
        else
        {
            var rid = fin.ReplayId!.Value;
            var stageEndRaw = new Dictionary<long, long>();
            foreach (var s in SelfBests.ReplaySegmentScores(c, ownOnly: false, skipScanned: false))
                if (s.ReplayId == rid) stageEndRaw[s.Stage] = s.ScoreAtEnd;
            return new PbRun(new BestSource(fin.Source, null, rid), stageEndRaw,
                             new Dictionary<long, long>(), new Dictionary<(long, long), long>());
        }
    }


    public static IReadOnlyList<StreamCompareItem> ComposeSpot(
        bool running, bool won, long? currentSegmentNet, long? currentTotalNet, long myClearBonus,
        (long Value, BestSource Source)? sb, PbValue? pb,
        StreamTargetLookupValue? target, StreamTargetLookupValue? wr,
        long? handClearBonus = null)
    {
        var handCb = handClearBonus ?? myClearBonus;
        return
        [
            Item(KindSb, running, won, currentSegmentNet, myClearBonus,
                sb?.Value, sb?.Source, null, false),
            BuildPb(running, won, currentTotalNet, myClearBonus, pb),
            Item(KindTarget, running, won, currentTotalNet, handCb,
                target?.Value, null, target?.UsedRound, target?.Fallback ?? false),
            Item(KindWr, running, won, currentTotalNet, handCb,
                wr?.Value, null, wr?.UsedRound, wr?.Fallback ?? false),
        ];
    }

    private static StreamCompareItem Item(string kind, bool running, bool won, long? currentNet,
                                          long myClearBonus, long? refNet, BestSource? source,
                                          long? usedRound, bool fallback)
    {
        if (refNet is null) return new StreamCompareItem(kind, null, null, source, usedRound, fallback);
        var shown = won ? refNet.Value + myClearBonus : refNet.Value;
        var delta = !running && currentNet is not null ? currentNet.Value - refNet.Value : (long?)null;
        return new StreamCompareItem(kind, shown, delta, source, usedRound, fallback);
    }

    private static StreamCompareItem BuildPb(bool running, bool won, long? currentNet, long myClearBonus,
                                             PbValue? pb)
    {
        if (pb is null) return new StreamCompareItem(KindPb, null, null, null, null, false);
        if (won && !pb.NetKnown) return new StreamCompareItem(KindPb, null, null, pb.Source, null, false);
        return Item(KindPb, running, won, currentNet, myClearBonus, pb.Value, pb.Source, null, false);
    }


    public static StreamPanelView? BuildSessionPanel(SqliteConnection c, long? sid,
                                                     IReadOnlyList<StreamTargetEntry> targets,
                                                     bool ownOnly = true)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(targets);
        var live = LiveProgress.Of(c, sid, ownOnly: ownOnly);
        if (live is null) return null;

        if (live.Mode is 0 or 1)
        {
            var stages = BuildStoryStages(c, live, targets, ownOnly);
            return new StreamPanelView(live.Session, live.Mode, live.Difficulty, live.Character, stages, null);
        }
        if (live.Mode == 2 && live.Match is { } m)
        {
            var match = BuildMatchSpot(c, live, m, targets, ownOnly);
            return new StreamPanelView(live.Session, live.Mode, live.Difficulty, m.Me, [], match);
        }
        return new StreamPanelView(live.Session, live.Mode, live.Difficulty, live.Character, [], null);
    }

    private static IReadOnlyList<StreamSpotRow> BuildStoryStages(
        SqliteConnection c, LiveProgressView live, IReadOnlyList<StreamTargetEntry> targets, bool ownOnly)
    {
        var (raw, _, _) = SelfBests.BestTableRaw(c, ownOnly: ownOnly, excludeSid: live.Session);
        var rawByKey = new Dictionary<string, SegmentBestRawRow>(StringComparer.Ordinal);
        foreach (var row in raw)
            rawByKey[Repository.SegKey.Of(row.Mode, row.Difficulty, row.Character, row.Stage, row.Opponent).Text()]
                = row;
        var pbRun = PlayerBestRun(c, live.Mode, live.Character, live.Difficulty,
                                  excludeSid: live.Session, ownOnly: ownOnly);

        var idx = SelfBests.LoadClearBonusIndex(c);
        var statusByStage = new Dictionary<long, (string? Status, long? ClearBonusId)>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT stage_number,status,clear_bonus_id FROM stages WHERE session_id=$0";
            cmd.Parameters.AddWithValue("$0", live.Session);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                if (r.IsDBNull(0)) continue;
                statusByStage[r.GetInt64(0)] = (r.IsDBNull(1) ? null : r.GetString(1),
                                                r.IsDBNull(2) ? null : r.GetInt64(2));
            }
        }
        var roundsByStage = new Dictionary<long, List<(long Round, long? Start, string? Status)>>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT st.stage_number,r.round_number,r.score_1_at_start,r.status"
                            + " FROM rounds r JOIN stages st ON st.stage_record_id=r.stage_record_id"
                            + " WHERE r.session_id=$0 ORDER BY st.stage_number,r.round_number";
            cmd.Parameters.AddWithValue("$0", live.Session);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                if (r.IsDBNull(0) || r.IsDBNull(1)) continue;
                var stage = r.GetInt64(0);
                if (!roundsByStage.TryGetValue(stage, out var list)) roundsByStage[stage] = list = [];
                list.Add((r.GetInt64(1), r.IsDBNull(2) ? null : r.GetInt64(2),
                         r.IsDBNull(3) ? null : r.GetString(3)));
            }
        }
        long? FinalRound(long stage) =>
            roundsByStage.TryGetValue(stage, out var list) && list.Count > 0 ? list[^1].Round : null;

        var outRows = new List<StreamSpotRow>();
        foreach (var st in live.Stages)
        {
            if (st.Stage is not long stage) continue;
            statusByStage.TryGetValue(stage, out var status);
            var won = status.Status == "completed";
            var myCb = idx.Resolve(live.Session, status.ClearBonusId, stage) ?? 0;

            (long Value, BestSource Source)? sb = null;
            var key = Repository.SegKey.Of(live.Mode, live.Difficulty, live.Character, stage, st.Opponent).Text();
            if (rawByKey.TryGetValue(key, out var rawRow) && rawRow.Score is long rs && rawRow.ScoreSource is { } rsrc)
                sb = (rs, rsrc);

            if (roundsByStage.TryGetValue(stage, out var rounds))
            {
                for (var i = 0; i + 1 < rounds.Count; i++)
                {
                    var (roundNo, _, _) = rounds[i];
                    var roundRunning = rounds[i].Status == LiveProgress.Running;
                    var roundTotal = rounds[i + 1].Start;

                    PbValue? roundPb = pbRun is not null
                        && pbRun.RoundEndRaw.TryGetValue((stage, roundNo), out var rv)
                        ? new PbValue(rv, true, pbRun.Source)
                        : null;
                    var roundLookup = StreamTargets.Lookup(targets, live.Mode ?? 0, live.Difficulty ?? 0,
                                                           live.Character ?? 0, stage, roundNo);
                    var roundItems = ComposeSpot(roundRunning, won: false, currentSegmentNet: null,
                                                 currentTotalNet: roundTotal, myClearBonus: 0,
                                                 sb: null, roundPb, roundLookup.Target, roundLookup.Wr);
                    outRows.Add(new StreamSpotRow(stage, roundNo, st.Opponent, roundRunning, false,
                                                  null, roundTotal, roundItems));
                }
            }

            var currentSegment = st.Segment is long seg ? (won ? seg - myCb : seg) : (long?)null;
            var currentTotal = st.Score is long tot ? (won ? tot - myCb : tot) : (long?)null;
            PbValue? pb = null;
            if (pbRun is not null)
            {
                if (pbRun.StageEndNet.TryGetValue(stage, out var net)) pb = new PbValue(net, true, pbRun.Source);
                else if (pbRun.StageEndRaw.TryGetValue(stage, out var rawv))
                    pb = new PbValue(rawv, false, pbRun.Source);
            }

            var lookup = StreamTargets.Lookup(targets, live.Mode ?? 0, live.Difficulty ?? 0,
                                              live.Character ?? 0, stage, FinalRound(stage));
            var handCb = StreamTargets.ClearBonusOf(targets, live.Mode ?? 0, live.Difficulty ?? 0,
                                                    live.Character ?? 0, stage);

            var items = ComposeSpot(st.Running, won, currentSegment, currentTotal, myCb, sb, pb,
                                    lookup.Target, lookup.Wr, handCb);
            outRows.Add(new StreamSpotRow(stage, null, st.Opponent, st.Running, won,
                                          currentSegment, currentTotal, items));
        }
        return outRows;
    }

    private static StreamSpotRow BuildMatchSpot(SqliteConnection c, LiveProgressView live, LiveMatch m,
                                                IReadOnlyList<StreamTargetEntry> targets, bool ownOnly)
    {
        (long Value, BestSource Source)? sb = m.Best is long bt && m.BestSrc is { } bsrc ? (bt, bsrc) : null;

        PbValue? pb = null;
        if (m.Kind is { } bucket)
        {
            var both = MatchBests.BestsBoth(c, excludeSid: live.Session, ownOnly: ownOnly);
            if (both.ByPlayer.TryGetValue(new MatchPlayerKey(bucket, m.Me), out var cell) && cell.Time is long t)
                pb = new PbValue(t, true, cell.Src ?? new BestSource(BestSource.Live, null, null));
        }

        long? round = m.Current is not null
            ? live.Rounds.Count
            : live.Rounds.Count > 0 ? live.Rounds[^1].Round : null;
        var lookup = StreamTargets.Lookup(targets, 2, live.Difficulty ?? 0, m.Me ?? 0, null, round);

        var running = m.ShownIsCurrent;
        var items = ComposeSpot(running, won: false, currentSegmentNet: m.Shown, currentTotalNet: m.Shown,
                                myClearBonus: 0, sb: sb, pb: pb, target: lookup.Target, wr: lookup.Wr);
        return new StreamSpotRow(null, null, m.Foe, running, false, m.Shown, m.Shown, items);
    }
}
