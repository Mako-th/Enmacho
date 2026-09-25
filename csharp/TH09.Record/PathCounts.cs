using System.Globalization;
using Microsoft.Data.Sqlite;
using TH09.Layer0;
using TH09.Record.Generated;

namespace TH09.Record;

public static class PathCounts
{
    public sealed record Probe(string Name, string[] Words, Func<uint[][], int, bool> Hit, string Why);

    private static bool OddFloat(uint bits)
    {
        var exp = (bits >> 23) & 0xFF;
        var man = bits & 0x7FFFFF;
        if (bits == 0x8000_0000u) return true;
        if (exp == 0 && man != 0) return true;
        return exp == 0xFF;
    }

    public static readonly Probe[] Probes =
    [
        new("pause_used", ["pause_used"], (c, i) => c[0][i] != 0,
            "生の pause_used が 0 でない tick（★rounds.pause_used が 1 になるかは別。そちらは実 DB 4,178 ラウンド全部 0）"),
        new("知らない id", ["p1_character", "p2_character"],
            (c, i) => c[0][i] >= (uint)RecordLabels.Characters.Length
                   || c[1][i] >= (uint)RecordLabels.Characters.Length,
            "表に無いキャラ id（Unknown(値) になる）"),
        new("知らない id / BGM", ["battle_bgm_id"],
            (c, i) => !MonitorRules.ValidBgmId(c[0][i]),
            "0〜13 の外の BGM id"),
        new("u32_上半分", ["p1_life_raw", "p2_life_raw"],
            (c, i) => (c[0][i] & 0x8000_0000u) != 0 || (c[1][i] & 0x8000_0000u) != 0,
            "2^31 以上の u32（符号を付けると負に化ける値）"),
        new("float_端", ["p1_gauge", "p2_gauge"],
            (c, i) => OddFloat(c[0][i]) || OddFloat(c[1][i]),
            "-0.0 / 非正規化数 / NaN / 無限大のゲージ"),
    ];

    public sealed record Count(string Name, long Hits, int Sessions, long NoWord,
                               IReadOnlyList<long> Examples, string Why);

    public static (List<Count> Counts, long Ticks, long Valid, int Sessions)
        Scan(SqliteConnection layer0, IReadOnlyList<long>? sessions, Action<string>? progress = null)
    {
        var ids = sessions ?? TickReplay.Sessions(layer0);
        var need = new HashSet<string>(StringComparer.Ordinal) { "flags" };
        foreach (var p in Probes) foreach (var w in p.Words) need.Add(w);

        var hits = new long[Probes.Length];
        var noWord = new long[Probes.Length];
        var seen = new HashSet<long>[Probes.Length];
        for (var k = 0; k < Probes.Length; k++) seen[k] = [];
        long ticks = 0, valid = 0;
        var done = 0;

        foreach (var sid in ids)
        {
            using var cmd = layer0.CreateCommand();
            cmd.CommandText = "SELECT record_version,field_order,encoding,blob FROM session_ticks"
                            + " WHERE session_id=$0 ORDER BY segment_no";
            cmd.Parameters.AddWithValue("$0", sid);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var order = TickArchive.ParseFieldOrder(r.GetString(1));
                var cols = TickArchive.Decode((byte[])r.GetValue(3), r.GetString(2), order, need);
                var have = new Dictionary<string, uint[]>(StringComparer.Ordinal);
                foreach (var kv in cols) have[kv.Key] = kv.Value;
                if (!have.TryGetValue("flags", out var flags))
                    throw new InvalidDataException($"session={sid} に flags がありません");

                var n = order.TickCount;
                ticks += n;
                for (var i = 0; i < n; i++)
                    if ((flags[i] & TickReplay.RequiredFlags) == TickReplay.RequiredFlags) valid++;

                for (var k = 0; k < Probes.Length; k++)
                {
                    var p = Probes[k];
                    var got = new uint[p.Words.Length][];
                    var missing = false;
                    for (var w = 0; w < p.Words.Length; w++)
                    {
                        if (have.TryGetValue(p.Words[w], out var col)) got[w] = col;
                        else { missing = true; break; }
                    }
                    if (missing) { noWord[k] += n; continue; }
                    for (var i = 0; i < n; i++)
                    {
                        if ((flags[i] & TickReplay.RequiredFlags) != TickReplay.RequiredFlags) continue;
                        if (!p.Hit(got, i)) continue;
                        hits[k]++;
                        seen[k].Add(sid);
                    }
                }
            }
            done++;
            if (progress is not null && done % 100 == 0)
                progress($"  {done}/{ids.Count} セッション（{ticks} tick）");
        }

        var counts = new List<Count>();
        for (var k = 0; k < Probes.Length; k++)
            counts.Add(new Count(Probes[k].Name, hits[k], seen[k].Count, noWord[k],
                                 [.. seen[k].Order().Take(20)], Probes[k].Why));
        return (counts, ticks, valid, ids.Count);
    }
}
