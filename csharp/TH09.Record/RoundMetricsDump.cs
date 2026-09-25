using System.Globalization;
using System.Text;
using TH09.Record.Generated;

namespace TH09.Record;

public static class RoundMetricsDump
{
    public const string Flag = "--dump-round-metrics";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(TextWriter w, string dbPath, string layer0Path, long? onlySession)
    {
        ArgumentNullException.ThrowIfNull(w);
        using var db = RecordDb.OpenReadOnly(dbPath);
        if (db is null)
        {
            Console.Error.WriteLine("本体 DB を読み取り専用で開けません: " + dbPath);
            return 1;
        }
        using var layer0 = TickReplay.OpenLayer0(layer0Path);
        var sessions = onlySession is { } only ? [only] : TickReplay.Sessions(layer0);

        var rowsOut = 0;
        var sessionsOk = 0;
        var dupTotal = 0;
        var unmatchedTotal = 0;
        var skipped = new List<(long SessionId, string Why)>();

        foreach (var sid in sessions)
        {
            var (data, why) = RoundRanges.LoadColumns(layer0, sid);
            if (data is null)
            {
                skipped.Add((sid, why ?? "?"));
                continue;
            }
            sessionsOk++;
            var (idMap, dup) = RoundMetrics.RoundIdMap(db.Connection, sid);
            dupTotal += dup;
            var mode = RoundMetrics.SessionMode(db.Connection, sid) ?? data.Mode[0];
            var kurai = RoundMetrics.KuraiCounts(db.Connection, sid, mode);
            var ranges = RoundRanges.Compute(data);

            foreach (var (key, parts) in ranges.OrderBy(t => t.Parts[0].Start))
            {
                if (!idMap.TryGetValue(key, out var rid))
                {
                    unmatchedTotal++;
                    continue;
                }
                var k1 = new RoundMetrics.KuraiKey(key.StageNumber, key.RoundNumber, 1);
                var k2 = new RoundMetrics.KuraiKey(key.StageNumber, key.RoundNumber, 2);
                var values = RoundMetrics.BuildRowValues(data, parts,
                    kurai.GetValueOrDefault(k1), kurai.GetValueOrDefault(k2));
                WriteRow(w, sid, rid, values);
                rowsOut++;
            }
        }

        foreach (var (sid, why) in skipped)
            Console.Error.WriteLine("session=" + Num(sid) + " を見送り: " + why);
        Console.Error.WriteLine("★母数: " + Num(sessions.Count) + " セッションのうち "
                                + Num(sessionsOk) + " セッションから " + Num(rowsOut) + " 行"
                                + "（重複 round_id " + Num(dupTotal) + " 件 / rounds に無い区間 "
                                + Num(unmatchedTotal) + " 件）");
        return rowsOut > 0 ? 0 : 1;
    }

    private static void WriteRow(TextWriter w, long sessionId, long roundRecordId,
                                 Dictionary<string, object?> values)
    {
        var sb = new StringBuilder();
        sb.Append(Num(sessionId)).Append('\t').Append(Num(roundRecordId));
        foreach (var name in RecordLabels.RoundMetricsColumns)
        {
            sb.Append('\t').Append(Token(values[name]));
        }
        sb.Append('\n');
        w.Write(sb.ToString());
    }

    private static string Token(object? v) => v switch
    {
        null => "",
        long l => l.ToString(Inv),
        double d => d.ToString("R", Inv),
        _ => throw new InvalidOperationException("round_metrics に想定外の型: " + v.GetType()),
    };

    private static string Num(long value) => value.ToString(Inv);
}
