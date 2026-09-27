using Microsoft.Data.Sqlite;

using Replays = TH09.Generated.DbColumns.Replays;

namespace TH09.Record;

public sealed record ReplayOwnershipResult(IReadOnlyList<long> Requested, IReadOnlyList<long> Missing,
                                           int Changed, int Unchanged);

public static class ReplayOwnership
{
    public const int Foreign = 0;

    public const int OwnP1 = 1;

    public const int OwnP2 = 2;

    public static bool IsValid(int? value) => value is null or Foreign or OwnP1 or OwnP2;

    public static ReplayOwnershipResult SetOverride(string mainDb, IReadOnlyList<long> replayIds,
                                                    int? value, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(mainDb);
        ArgumentNullException.ThrowIfNull(replayIds);
        if (!IsValid(value))
            throw new ArgumentOutOfRangeException(
                nameof(value), value,
                "own_override に書けるのは null / " + Foreign + " / " + OwnP1 + " / " + OwnP2 + " だけです。");
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
        foreach (var rid in replayIds)
        {
            using (var read = c.CreateCommand())
            {
                read.CommandText = $"SELECT {Replays.OwnOverride} FROM {Replays.Table}"
                                 + $" WHERE {Replays.ReplayId}=$0";
                read.Parameters.AddWithValue("$0", rid);
                using var r = read.ExecuteReader();
                if (!r.Read()) { missing.Add(rid); continue; }
                var now = r.IsDBNull(0) ? (int?)null : (int)r.GetInt64(0);
                if (now == value) { unchanged++; continue; }
            }
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
            log?.Invoke("所有の覆しで見つからなかった replay_id が " + missing.Count + " 件あります: "
                        + string.Join(",", missing));
        return new ReplayOwnershipResult(replayIds, missing, changed, unchanged);
    }
}
