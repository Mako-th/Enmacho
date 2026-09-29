using Microsoft.Data.Sqlite;

using Replays = TH09.Generated.DbColumns.Replays;

namespace TH09.Record;

public sealed record ReplayOwnershipResult(IReadOnlyList<long> Requested, IReadOnlyList<long> Missing,
                                           int Changed, int Unchanged, int WithoutSide = 0);

public static class ReplayOwnership
{
    public const int Foreign = 0;

    public const int OwnP1 = 1;

    public const int OwnP2 = 2;

    public const int OwnUnknownSide = 3;

    public static bool IsValid(int? value) => value is null or Foreign or OwnP1 or OwnP2 or OwnUnknownSide;

    public static ReplayOwnershipResult SetOverride(string mainDb, IReadOnlyList<long> replayIds,
                                                    int? value, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(mainDb);
        ArgumentNullException.ThrowIfNull(replayIds);
        if (!IsValid(value))
            throw new ArgumentOutOfRangeException(
                nameof(value), value,
                "own_override に書けるのは null / " + Foreign + " / " + OwnP1 + " / " + OwnP2 + " / "
                + OwnUnknownSide + " だけです。");
        return Apply(mainDb, replayIds, _ => value, "所有の覆し", log);
    }

    public static ReplayOwnershipResult MarkOwn(string mainDb, IReadOnlyList<long> replayIds,
                                                Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(mainDb);
        ArgumentNullException.ThrowIfNull(replayIds);
        return Apply(mainDb, replayIds, row =>
        {
            if (row.OwnOverride is OwnP1 or OwnP2) return (int)row.OwnOverride;
            return ReplayStages.HumanSideOf(row.Mode, row.DecodedJson)
                   ?? (row.OwnerSide is OwnP1 or OwnP2 ? (int)row.OwnerSide : OwnUnknownSide);
        }, "自分のものにする", log);
    }

    private sealed record Row(long? OwnOverride, long? OwnerSide, long? Mode, string? DecodedJson);

    private static ReplayOwnershipResult Apply(string mainDb, IReadOnlyList<long> replayIds,
                                               Func<Row, int?> pick, string what, Action<string>? log)
    {
        if (replayIds.Count == 0)
            return new ReplayOwnershipResult([], [], 0, 0);
        if (!File.Exists(mainDb))
            throw new FileNotFoundException("本体 DB がありません: " + mainDb, mainDb);

        using var db = RecordDb.OpenReadWrite(mainDb);
        var c = db.Connection;
        using var tx = c.BeginTransaction();

        var missing = new List<long>();
        var changed = 0;
        var unchanged = 0;
        var withoutSide = 0;
        foreach (var rid in replayIds)
        {
            int? now;
            int? value;
            using (var read = c.CreateCommand())
            {
                read.CommandText = $"SELECT {Replays.OwnOverride},{Replays.OwnerSide},{Replays.Mode},"
                                 + $"{Replays.DecodedJson} FROM {Replays.Table}"
                                 + $" WHERE {Replays.ReplayId}=$0";
                read.Parameters.AddWithValue("$0", rid);
                using var r = read.ExecuteReader();
                if (!r.Read()) { missing.Add(rid); continue; }
                now = r.IsDBNull(0) ? null : (int)r.GetInt64(0);
                value = pick(new Row(r.IsDBNull(0) ? null : r.GetInt64(0),
                                     r.IsDBNull(1) ? null : r.GetInt64(1),
                                     r.IsDBNull(2) ? null : r.GetInt64(2),
                                     r.IsDBNull(3) ? null : r.GetString(3)));
            }
            if (value is OwnUnknownSide) withoutSide++;
            if (now == value) { unchanged++; continue; }
            using (var write = c.CreateCommand())
            {
                write.CommandText = $"UPDATE {Replays.Table} SET {Replays.OwnOverride}=$1"
                                  + $" WHERE {Replays.ReplayId}=$0";
                write.Parameters.AddWithValue("$0", rid);
                write.Parameters.AddWithValue("$1", value is int v ? v : DBNull.Value);
                changed += write.ExecuteNonQuery();
            }
        }
        tx.Commit();

        if (missing.Count > 0)
            log?.Invoke(what + "で見つからなかった replay_id が " + missing.Count + " 件あります: "
                        + string.Join(",", missing));
        return new ReplayOwnershipResult(replayIds, missing, changed, unchanged, withoutSide);
    }
}
