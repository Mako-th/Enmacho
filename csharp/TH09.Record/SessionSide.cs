using Microsoft.Data.Sqlite;
using TH09.Record.Generated;

namespace TH09.Record;

public enum SessionBucket
{
    Story,

    Net,

    Cpu,

    Local,
}

public sealed record OwnNameCount(string Name, int Count);

public sealed record OwnNames(IReadOnlyList<OwnNameCount> Keep, IReadOnlyList<OwnNameCount> Drop,
                              string? MyName)
{
    public IReadOnlyList<string> Names => _names ??= Keep.Select(x => x.Name).ToArray();

    private string[]? _names;
}

public static class SessionSide
{
    public static string Text(SessionBucket bucket) => bucket switch
    {
        SessionBucket.Story => RecordLabels.BucketStory,
        SessionBucket.Net => RecordLabels.BucketNet,
        SessionBucket.Cpu => RecordLabels.BucketCpu,
        SessionBucket.Local => RecordLabels.BucketLocal,
        _ => throw new ArgumentOutOfRangeException(nameof(bucket)),
    };

    public static SessionBucket Bucket(long? gameMode, string? source, string? execType,
                                       long? p1Control, long? p2Control)
    {
        if (gameMode is 0 or 1) return SessionBucket.Story;
        if (source == RecordLabels.NetSource || execType == RecordLabels.NetExec) return SessionBucket.Net;
        if (p1Control == RecordLabels.ControlCpu || p2Control == RecordLabels.ControlCpu)
            return SessionBucket.Cpu;
        return SessionBucket.Local;
    }

    public static (int? Side, bool Own, bool Provisional) Of(
        long? ownOverride, long? ownerSide, string? p1Name, string? p2Name, IReadOnlyList<string> names,
        long? replayId, long? p1Control, long? p2Control, long? seat, string? execType)
    {
        var byName = SideByName(p1Name, p2Name, names);
        long? side = ownOverride ?? byName ?? ownerSide;
        var hasReplay = replayId is not null;
        var own = execType != RecordLabels.ExecReplay || (hasReplay && side is 1 or 2);
        if (side is 1 or 2) return ((int)side.Value, own, false);

        int? human = null;
        var humans = 0;
        if (p1Control != RecordLabels.ControlCpu) { human = 1; humans++; }
        if (p2Control != RecordLabels.ControlCpu) { human = 2; humans++; }
        if (humans == 1) return (human, own, false);

        if (seat == RecordLabels.NetSeatLocalIsP1) return (1, own, false);
        if (seat == RecordLabels.NetSeatLocalIsP2) return (2, own, false);

        if (!hasReplay && execType == RecordLabels.NetExec) return (1, own, true);
        return (null, own, false);
    }

    public static int? SideByName(string? p1Name, string? p2Name, IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        if (names.Count == 0 || !names.Any(n => !string.IsNullOrWhiteSpace(n))) return null;
        if (NameHit(p1Name, names)) return 1;
        if (NameHit(p2Name, names)) return 2;
        return null;
    }

    public static OwnNames OwnReplayNames(SqliteConnection c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var mine = new Dictionary<string, int>(StringComparer.Ordinal);
        var theirs = new Dictionary<string, int>(StringComparer.Ordinal);
        try
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT p1_name,p2_name,is_own,owner_side,own_override FROM replays";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var p1 = r.IsDBNull(0) ? null : r.GetString(0);
                var p2 = r.IsDBNull(1) ? null : r.GetString(1);
                var isOwn = r.IsDBNull(2) ? 0 : r.GetInt64(2);
                long? ownerSide = r.IsDBNull(3) ? null : r.GetInt64(3);
                long? ov = r.IsDBNull(4) ? null : r.GetInt64(4);
                var side = ov ?? ownerSide;
                if (isOwn == 0 || side is not (1 or 2)) continue;
                Bump(mine, side == 1 ? p1 : p2);
                Bump(theirs, side == 1 ? p2 : p1);
            }
        }
        catch (SqliteException)
        {
            return new OwnNames([], [], null);
        }

        var keep = mine.Where(kv => !theirs.ContainsKey(kv.Key))
                       .Select(kv => new OwnNameCount(kv.Key, kv.Value)).ToList();
        var drop = mine.Where(kv => theirs.ContainsKey(kv.Key))
                       .Select(kv => new OwnNameCount(kv.Key, kv.Value)).ToList();
        keep.Sort(ByCount);
        drop.Sort(ByCount);
        return new OwnNames(keep, drop, keep.Count > 0 ? keep[0].Name : null);

        static void Bump(Dictionary<string, int> bag, string? raw)
        {
            if (raw is null) return;
            var v = raw.Trim();
            if (v.Length == 0) return;
            bag[v] = bag.TryGetValue(v, out var n) ? n + 1 : 1;
        }

        static int ByCount(OwnNameCount a, OwnNameCount b)
            => b.Count != a.Count ? b.Count.CompareTo(a.Count) : string.CompareOrdinal(a.Name, b.Name);
    }

    public static OwnNames OwnReplayNames(string mainDb)
    {
        using var db = RecordDb.OpenReadOnly(mainDb)
            ?? throw new FileNotFoundException("本体 DB を読み取り専用で開けません: " + mainDb, mainDb);
        return OwnReplayNames(db.Connection);
    }

    private static bool NameHit(string? candidate, IReadOnlyList<string> names)
    {
        if (candidate is null) return false;
        var v = candidate.Trim();
        if (v.Length == 0) return false;
        v = v.ToLowerInvariant();
        foreach (var raw in names)
        {
            var n = raw.Trim().ToLowerInvariant();
            if (n.Length == 0) continue;
            if (v.Contains(n, StringComparison.Ordinal)) return true;
        }
        return false;
    }
}
