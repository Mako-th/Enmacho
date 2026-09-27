using System.Globalization;
using Microsoft.Data.Sqlite;
using TH09.Record.Generated;

namespace TH09.Record;

public sealed record MatchSessionInfo(long Sid, SessionBucket Bucket, int? Side, bool Own, bool Provisional,
                                      long? Me, long? Foe, long? ReplayId, long? Difficulty);

public readonly record struct MatchPlayerKey(SessionBucket Bucket, long? Me)
{
    public string Text() => string.Join("/", SessionSide.Text(Bucket), N(Me));

    private static string N(long? v) => v is null ? "~" : v.Value.ToString(CultureInfo.InvariantCulture);
}

public sealed record MatchBestsBoth(IReadOnlyDictionary<MatchKey, MatchBestCell> WithOpponent,
                                    IReadOnlyDictionary<MatchPlayerKey, MatchBestCell> ByPlayer);

public readonly record struct MatchKey(SessionBucket Bucket, long? Me, long? Foe)
{
    public string Text() => string.Join("/", SessionSide.Text(Bucket), N(Me), N(Foe));

    private static string N(long? v) => v is null ? "~" : v.Value.ToString(CultureInfo.InvariantCulture);
}

public sealed class MatchBestCell
{
    public long? Time { get; set; }

    public long? Round { get; set; }

    public long? Sid { get; set; }

    public BestSource? Src { get; set; }

    public SortedSet<long> Holders { get; } = [];

    public int Sessions { get; set; }

    public int Rounds { get; set; }

    public int Skipped { get; set; }
}

public sealed record MatchProgressBests(IReadOnlyDictionary<MatchKey, MatchBestCell> Bests,
                                        IReadOnlyList<string> Names);

public static class MatchBests
{
    public static Dictionary<long, MatchSessionInfo> Sessions(
        SqliteConnection c, IReadOnlyList<string>? names = null, long? onlySid = null)
    {
        ArgumentNullException.ThrowIfNull(c);
        names ??= SessionSide.OwnReplayNames(c).Names;
        var seats = Seats(c);
        var rc = HasReplayCols(c);
        var cols = rc
            ? "r.source,r.p1_name,r.p2_name,r.owner_side,r.own_override"
            : "NULL source,NULL p1_name,NULL p2_name,NULL owner_side,NULL own_override";

        using var cmd = c.CreateCommand();
        var q = "SELECT s.session_id sid,m.game_mode,m.difficulty,m.p1_character,m.p2_character,"
              + "m.p1_control,m.p2_control,m.execution_type,sr.replay_id," + cols
              + " FROM sessions s JOIN session_metadata m USING(session_id)"
              + " LEFT JOIN session_replays sr USING(session_id)"
              + " LEFT JOIN replays r ON r.replay_id=sr.replay_id"
              + " WHERE m.game_mode=2";
        if (onlySid is long only)
        {
            q += " AND s.session_id=$0";
            cmd.Parameters.AddWithValue("$0", only);
        }
        cmd.CommandText = q;

        var outMap = new Dictionary<long, MatchSessionInfo>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var sid = r.GetInt64(0);
            var gameMode = Long(r, 1);
            var diff = Long(r, 2);
            var p1Char = Long(r, 3);
            var p2Char = Long(r, 4);
            var p1Control = Long(r, 5);
            var p2Control = Long(r, 6);
            var execType = Text(r, 7);
            var replayId = Long(r, 8);
            var source = Text(r, 9);
            var p1Name = Text(r, 10);
            var p2Name = Text(r, 11);
            var ownerSide = Long(r, 12);
            var ownOverride = Long(r, 13);

            var bucket = SessionSide.Bucket(gameMode, source, execType, p1Control, p2Control);
            var (side, own, prov) = SessionSide.Of(ownOverride, ownerSide, p1Name, p2Name, names,
                                                   replayId, p1Control, p2Control,
                                                   seats.TryGetValue(sid, out var seat) ? seat : null,
                                                   execType);
            long? me = side is null ? null : (side == 1 ? p1Char : p2Char);
            long? foe = side is null ? null : (side == 1 ? p2Char : p1Char);
            outMap[sid] = new MatchSessionInfo(sid, bucket, side, own, prov, me, foe, replayId, diff);
        }
        return outMap;
    }

    public static MatchSessionInfo? Context(SqliteConnection c, long sid, IReadOnlyList<string>? names = null)
        => Sessions(c, names, onlySid: sid).TryGetValue(sid, out var v) ? v : null;

    public static IReadOnlyDictionary<MatchKey, MatchBestCell> Bests(
        SqliteConnection c, long? excludeSid = null, bool ownOnly = true, IReadOnlyList<string>? names = null)
        => BestsBoth(c, excludeSid, ownOnly, names).WithOpponent;

    public static IReadOnlyDictionary<MatchPlayerKey, MatchBestCell> PlayerBests(
        SqliteConnection c, long? excludeSid = null, bool ownOnly = true, IReadOnlyList<string>? names = null)
        => BestsBoth(c, excludeSid, ownOnly, names).ByPlayer;

    public static MatchBestsBoth BestsBoth(
        SqliteConnection c, long? excludeSid = null, bool ownOnly = true, IReadOnlyList<string>? names = null)
    {
        ArgumentNullException.ThrowIfNull(c);
        var ses = Sessions(c, names);
        var scanBySid = SessionProtection.Scanned(c).ReplayBySession;

        var withFoe = new Dictionary<MatchKey, MatchBestCell>();
        var withFoeSeen = new Dictionary<MatchKey, HashSet<long>>();
        var byPlayer = new Dictionary<MatchPlayerKey, MatchBestCell>();
        var byPlayerSeen = new Dictionary<MatchPlayerKey, HashSet<long>>();

        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT r.session_id sid,r.round_number rn,r.duration_frames f,r.status"
                        + " FROM rounds r JOIN session_metadata m USING(session_id)"
                        + " WHERE m.game_mode=2 ORDER BY r.round_record_id";
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
        {
            var sid = rd.GetInt64(0);
            if (!ses.TryGetValue(sid, out var s) || s.Side is null) continue;
            if (excludeSid is long ex && sid == ex) continue;
            if (ownOnly && !s.Own) continue;

            var round = Long(rd, 1);
            var status = Text(rd, 3);
            var frames = Long(rd, 2);

            Bump(withFoe, withFoeSeen, new MatchKey(s.Bucket, s.Me, s.Foe), sid, status, frames, round, scanBySid);
            Bump(byPlayer, byPlayerSeen, new MatchPlayerKey(s.Bucket, s.Me), sid, status, frames, round, scanBySid);
        }
        return new MatchBestsBoth(withFoe, byPlayer);
    }

    private static void Bump<TKey>(Dictionary<TKey, MatchBestCell> data, Dictionary<TKey, HashSet<long>> seen,
                                   TKey key, long sid, string? status, long? frames, long? round,
                                   Dictionary<long, long> scanBySid) where TKey : notnull
    {
        if (!data.TryGetValue(key, out var d)) data[key] = d = new MatchBestCell();
        if (status != RecordLabels.MatchRoundStatus || frames is null) { d.Skipped++; return; }
        d.Rounds++;
        if (!seen.TryGetValue(key, out var bag)) seen[key] = bag = [];
        if (bag.Add(sid)) d.Sessions++;

        if (d.Time is null || frames > d.Time)
        {
            d.Time = frames;
            d.Round = round;
            d.Sid = sid;
            d.Src = BestSource.OfSession(sid, scanBySid);
            d.Holders.Clear();
            d.Holders.Add(sid);
        }
        else if (frames == d.Time)
        {
            d.Holders.Add(sid);
        }
    }

    public static MatchProgressBests Progress(SqliteConnection c, long? excludeSid = null, bool ownOnly = true)
    {
        ArgumentNullException.ThrowIfNull(c);
        var names = SessionSide.OwnReplayNames(c).Names;
        return new MatchProgressBests(Bests(c, excludeSid, ownOnly, names), names);
    }

    private static Dictionary<long, long?> Seats(SqliteConnection c)
    {
        var outMap = new Dictionary<long, long?>();
        try
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT session_id,seat FROM session_net_play";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                long? seat = null;
                if (!r.IsDBNull(1))
                {
                    var v = r.GetValue(1);
                    seat = v switch
                    {
                        long l => l,
                        double dd => (long)dd,
                        string t when long.TryParse(t, NumberStyles.AllowLeadingSign,
                                                    CultureInfo.InvariantCulture, out var p) => p,
                        _ => null,
                    };
                }
                outMap[r.GetInt64(0)] = seat;
            }
        }
        catch (SqliteException)
        {
            return [];
        }
        return outMap;
    }

    private static bool HasReplayCols(SqliteConnection c)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(replays)";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            if (string.Equals(r.GetString(1), "decode_status", StringComparison.Ordinal)) return true;
        return false;
    }

    internal static long? Long(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetInt64(i);

    internal static string? Text(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
}
