using System.Globalization;
using Microsoft.Data.Sqlite;
using TH09.Record.Generated;

namespace TH09.Record;

public readonly record struct FinalKey(long? Mode, long? Character, long? Difficulty);

public sealed record ProgressBests(IReadOnlyDictionary<string, BestTableRow> Segments,
                                   IReadOnlyDictionary<FinalKey, SessionProtection.FinalScoreBest> Finals,
                                   MatchProgressBests Match);

public sealed record LiveStage(long? Stage, long? Opponent, bool Running, long? Score, long? Segment,
                               long? Best, BestSource? BestSrc, long? Delta,
                               long? Time, long? BestTime, long? TimeDelta);

public sealed record LiveRound(int Round, long? Time, long? Winner, bool Running, string? Status,
                               long? Life1, long? Life2, long? Best, long? Delta);

public sealed record LiveMatch(SessionBucket? Kind, int? Side, bool? Provisional, bool? Own,
                               long? Me, long? Foe, long? Current, long? Longest,
                               long? Shown, bool ShownIsCurrent,
                               long? Best, long? BestRound, BestSource? BestSrc,
                               int Sessions, int Rounds, int Skipped, long? Delta);

public sealed record LiveTotal(long? Score, long? Best, BestSource? BestSrc, long? Delta);

public sealed record LiveProgressView(long Session, long? Mode, long? Difficulty, long? Character, long? P2,
                                      string? Status, string Execution, bool IsReplay, bool Comparable,
                                      double? Lives, long? Combo, long? MaxCombo, long? SpellPoints,
                                      IReadOnlyList<LiveStage> Stages, IReadOnlyList<LiveRound> Rounds,
                                      LiveTotal? Total, LiveMatch? Match);

public static class LiveProgress
{
    public const string Running = "running";

    public static ProgressBests Bests(SqliteConnection c, long? excludeSid = null, bool ownOnly = true)
    {
        ArgumentNullException.ThrowIfNull(c);
        var seg = new Dictionary<string, BestTableRow>(StringComparer.Ordinal);
        foreach (var b in SelfBests.BestTable(c, ownOnly: ownOnly, excludeSid: excludeSid))
            seg[Repository.SegKey.Of(b.Mode, b.Difficulty, b.Character, b.Stage, b.Opponent).Text()] = b;

        var fin = new Dictionary<FinalKey, SessionProtection.FinalScoreBest>();
        foreach (var f in SessionProtection.FinalScoreBests(c, ownOnly: ownOnly, excludeSid: excludeSid))
            fin[new FinalKey(f.Mode, f.Character, f.Difficulty)] = f;

        return new ProgressBests(seg, fin, MatchBests.Progress(c, excludeSid: excludeSid, ownOnly: ownOnly));
    }

    public static LiveProgressView? Of(SqliteConnection c, long? sid = null, bool ownOnly = true,
                                       ProgressBests? bests = null)
    {
        ArgumentNullException.ThrowIfNull(c);
        var head = Head(c, sid);
        if (head is null) return null;
        var (session, status, mode, diff, ch, p2, execType) = head.Value;
        var running = status == Running;
        var snap = Snapshot(c, session);

        var rounds = Rounds(c, session);
        var cur = running ? snap.Int("p1_score") : null;

        var common = new
        {
            Lives = snap.Real("p1_lives"),
            Combo = snap.Int("p1_current_combo"),
            MaxCombo = snap.Int("p1_max_combo"),
            Spell = snap.Int("p1_spell_points"),
            Execution = string.IsNullOrEmpty(execType) ? RecordLabels.ExecLive : execType,
            IsReplay = execType == RecordLabels.ExecReplay,
            Comparable = mode is 0 or 1,
        };

        if (mode == 2)
        {
            var mp = bests?.Match ?? MatchBests.Progress(c, excludeSid: session, ownOnly: ownOnly);
            var ctx = MatchBests.Context(c, session, mp.Names);
            MatchBestCell? cell = null;
            if (ctx is not null && ctx.Side is not null
                && mp.Bests.TryGetValue(new MatchKey(ctx.Bucket, ctx.Me, ctx.Foe), out var found))
                cell = found;
            var bt = cell?.Time;

            long? longest = null;
            long? current = null;
            var list = new List<LiveRound>();
            for (var i = 0; i < rounds.Count; i++)
            {
                var r = rounds[i];
                var run = r.Status == Running;
                var t = r.Frames;
                if (t is null && run && running) t = snap.Int("round_frames");
                var ok = run || r.Status == RecordLabels.MatchRoundStatus;
                if (run) current = t;
                else if (ok && t is not null && (longest is null || t > longest)) longest = t;
                list.Add(new LiveRound(i + 1, t, r.Winner, run, r.Status, r.Life1, r.Life2,
                                       ok ? bt : null,
                                       ok && t is not null && bt is not null ? t - bt : null));
            }
            var shown = current ?? longest;
            var match = new LiveMatch(ctx?.Bucket, ctx?.Side, ctx?.Provisional, ctx?.Own,
                                      ctx?.Me, ctx?.Foe, current, longest,
                                      shown, current is not null,
                                      bt, cell?.Round, cell?.Src,
                                      cell?.Sessions ?? 0, cell?.Rounds ?? 0, cell?.Skipped ?? 0,
                                      shown is not null && bt is not null ? shown - bt : null);
            return new LiveProgressView(session, mode, diff, ch, p2, status, common.Execution,
                                        common.IsReplay, common.Comparable,
                                        common.Lives, common.Combo, common.MaxCombo, common.Spell,
                                        [], list, null, match);
        }

        var idx = bests ?? Bests(c, excludeSid: session, ownOnly: ownOnly);
        idx.Finals.TryGetValue(new FinalKey(mode, ch, diff), out var fin);
        var times = Repository.StageTimeFrames(c);
        var stages = new List<LiveStage>();
        long? last = null;
        foreach (var st in Stages(c, session))
        {
            var run = st.Status == Running;
            var end = st.ScoreAtEnd;
            if (end is null && run && cur is not null) end = cur;
            var seg = end is not null && st.ScoreAtStart is not null ? end - st.ScoreAtStart : null;

            idx.Segments.TryGetValue(
                Repository.SegKey.Of(mode, diff, ch, st.Stage, st.Opponent).Text(), out var b);

            long? t = times.TryGetValue(st.StageRecordId, out var sum) ? sum : null;
            if (t is null && run)
            {
                var rf = snap.Int("round_frames");
                if (rf is not null)
                {
                    long closed = 0;
                    foreach (var r in rounds)
                        if (r.StageRecordId == st.StageRecordId && r.Frames is long f) closed += f;
                    t = closed + rf.Value;
                }
            }

            var bt = b?.Time;
            var bs = b?.Score;
            stages.Add(new LiveStage(st.Stage, st.Opponent, run, end, seg, bs, b?.ScoreSource,
                                     seg is not null && bs is not null ? seg - bs : null,
                                     t, bt, t is not null && bt is not null ? t - bt : null));
            if (end is not null) last = end;
        }

        var total = running && cur is not null ? cur : last;
        var fb = fin?.Score;
        var totalCell = new LiveTotal(total, fb,
                                      fin is null ? null : new BestSource(fin.Source, fin.SessionId, fin.ReplayId),
                                      total is not null && fb is not null ? total - fb : null);
        return new LiveProgressView(session, mode, diff, ch, p2, status, common.Execution,
                                    common.IsReplay, common.Comparable,
                                    common.Lives, common.Combo, common.MaxCombo, common.Spell,
                                    stages, [], totalCell, null);
    }

    public static LiveProgressView? Read(string mainDb, long? sid = null, bool ownOnly = true)
    {
        using var db = RecordDb.OpenReadOnly(mainDb)
            ?? throw new FileNotFoundException("本体 DB を読み取り専用で開けません: " + mainDb, mainDb);
        return Of(db.Connection, sid, ownOnly);
    }


    private static (long Sid, string? Status, long? Mode, long? Diff, long? Ch, long? P2, string? Exec)?
        Head(SqliteConnection c, long? sid)
    {
        using var cmd = c.CreateCommand();
        var q = "SELECT s.session_id,s.status,m.game_mode,m.difficulty,m.p1_character,m.p2_character,"
              + "m.execution_type FROM sessions s LEFT JOIN session_metadata m USING(session_id)";
        if (sid is long only)
        {
            q += " WHERE s.session_id=$0";
            cmd.Parameters.AddWithValue("$0", only);
        }
        cmd.CommandText = q + " ORDER BY s.session_id DESC LIMIT 1";
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return (r.GetInt64(0), MatchBests.Text(r, 1), MatchBests.Long(r, 2), MatchBests.Long(r, 3),
                MatchBests.Long(r, 4), MatchBests.Long(r, 5), MatchBests.Text(r, 6));
    }

    private sealed record RoundRow(long? Frames, long? Winner, string? Status, long? Life1, long? Life2,
                                   long? StageRecordId);

    private static List<RoundRow> Rounds(SqliteConnection c, long sid)
    {
        var list = new List<RoundRow>();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT duration_frames,winner_side,status,life_1_raw_at_end,life_2_raw_at_end,"
                        + "stage_record_id FROM rounds WHERE session_id=$0 ORDER BY round_record_id";
        cmd.Parameters.AddWithValue("$0", sid);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new RoundRow(MatchBests.Long(r, 0), MatchBests.Long(r, 1), MatchBests.Text(r, 2),
                                  MatchBests.Long(r, 3), MatchBests.Long(r, 4), MatchBests.Long(r, 5)));
        return list;
    }

    private sealed record StageRow(long StageRecordId, long? Stage, long? Opponent, string? Status,
                                   long? ScoreAtStart, long? ScoreAtEnd);

    private static List<StageRow> Stages(SqliteConnection c, long sid)
    {
        var list = new List<StageRow>();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT stage_record_id,stage_number,opponent_character,status,"
                        + "score_at_start,score_at_end FROM stages WHERE session_id=$0"
                        + " ORDER BY stage_record_id";
        cmd.Parameters.AddWithValue("$0", sid);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new StageRow(r.GetInt64(0), MatchBests.Long(r, 1), MatchBests.Long(r, 2),
                                  MatchBests.Text(r, 3), MatchBests.Long(r, 4), MatchBests.Long(r, 5)));
        return list;
    }

    private static SnapshotView Snapshot(SqliteConnection c, long sid)
    {
        try
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT payload_json FROM snapshots WHERE session_id=$0"
                            + " ORDER BY snapshot_id DESC LIMIT 1";
            cmd.Parameters.AddWithValue("$0", sid);
            using var r = cmd.ExecuteReader();
            if (!r.Read() || r.IsDBNull(0)) return SnapshotView.Empty;
            return CanonJson.TryParse(r.GetString(0), out var node) && node.Kind == CanonJson.Kind.Object
                ? new SnapshotView(node)
                : SnapshotView.Empty;
        }
        catch (SqliteException)
        {
            return SnapshotView.Empty;
        }
    }

    private sealed class SnapshotView(CanonJson.Node? node)
    {
        public static readonly SnapshotView Empty = new(null);

        public long? Int(string name)
        {
            var v = Member(name);
            if (v is null) return null;
            if (v.Kind == CanonJson.Kind.Int)
                return long.Parse(v.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            if (v.Kind == CanonJson.Kind.Float && Math.Abs(v.Float % 1) < double.Epsilon)
                return (long)v.Float;
            return null;
        }

        public double? Real(string name)
        {
            var v = Member(name);
            if (v is null) return null;
            if (v.Kind == CanonJson.Kind.Int)
                return double.Parse(v.Text, NumberStyles.Float, CultureInfo.InvariantCulture);
            return v.Kind == CanonJson.Kind.Float ? v.Float : null;
        }

        private CanonJson.Node? Member(string name)
            => node is not null && node.Members.TryGetValue(name, out var v)
               && v.Kind != CanonJson.Kind.Null ? v : null;
    }
}
