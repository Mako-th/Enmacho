using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using TH09.TickBus;

[assembly: SupportedOSPlatform("windows")]

namespace TH09.Record;

public static class LiveCapture
{
    public const string FormatVersion = "th09-record-capture-v1";

    public const string CaseName = "live";

    public sealed record CaptureResult(
        uint FromSeq, uint UntilSeq, int Requested, int Accepted, int Gaps, int SeenRecords,
        uint HeadAtOpen, bool Late, bool Complete,
        long Torn, long Lost, long GapEvents, double WaitedSeconds, double ReadSeconds);

    public static readonly (string Name, int Offset)[] SortedFields =
        [.. TickBusLayout.RecordFields.OrderBy(f => f.Name, StringComparer.Ordinal)];

    private static readonly Dictionary<string, int> _offsetOf =
        TickBusLayout.RecordFields.ToDictionary(f => f.Name, f => f.Offset, StringComparer.Ordinal);

    public static CaptureResult Read(TickBusReader bus, uint from, uint until, double seconds,
                                     int margin, List<uint[]> raws, Action<string>? log = null)
    {
        static bool AtOrAfter(uint a, uint b) => unchecked((int)(a - b)) >= 0;

        int requested = unchecked((int)(until - from)) + 1;
        if (requested <= 0)
            throw new ArgumentException($"seq の範囲が空です（from={from} until={until}）。"
                                        + "★0 件は「一致」ではなく「測れていない」ので、先に弾く。");

        uint headAtOpen = bus.ReadWriteIndex();
        int keep = Math.Max(0, TickBusLayout.Capacity - margin);
        int behindAtOpen = unchecked((int)(headAtOpen - from));
        bool late = behindAtOpen > keep;

        var reader = new RingReader(bus, margin) { Log = log ?? (static _ => { }) };
        var started = DateTime.UtcNow;
        var deadline = started.AddSeconds(seconds);

        while (!AtOrAfter(bus.ReadWriteIndex(), unchecked(from + 1)))
        {
            if (DateTime.UtcNow >= deadline)
            {
                return new CaptureResult(from, until, requested, 0, requested, 0,
                                         headAtOpen, late, false, 0, 0, 0,
                                         (DateTime.UtcNow - started).TotalSeconds, 0);
            }
            Thread.Sleep(1);
        }
        var waited = (DateTime.UtcNow - started).TotalSeconds;

        reader.NextIndex = from;
        int seen = 0;
        var seqs = new List<uint>();
        var readStarted = DateTime.UtcNow;
        while (DateTime.UtcNow < deadline)
        {
            var got = reader.Poll(requested);
            if (got.Count == 0)
            {
                Thread.Sleep(1);
                continue;
            }
            bool done = false;
            foreach (var rec in got)
            {
                seen++;
                if (!AtOrAfter(rec.Seq, from)) continue;
                if (AtOrAfter(rec.Seq, unchecked(until + 1))) { done = true; break; }
                raws.Add(rec.Words);
                seqs.Add(rec.Seq);
            }
            if (done) break;
            if (seqs.Count > 0 && AtOrAfter(seqs[^1], until)) break;
        }
        var readSeconds = (DateTime.UtcNow - readStarted).TotalSeconds;

        int accepted = seqs.Count;
        bool complete = accepted == requested
                        && seqs.Count > 0 && seqs[0] == from && seqs[^1] == until
                        && !late;
        for (var i = 1; complete && i < seqs.Count; i++)
            if (seqs[i] != unchecked(seqs[i - 1] + 1)) complete = false;

        return new CaptureResult(from, until, requested, accepted, requested - accepted, seen,
                                 headAtOpen, late, complete,
                                 reader.TornRecords, reader.LostRecords, reader.GapEvents,
                                 waited, readSeconds);
    }

    public static void WriteTicks(string path, IReadOnlyList<uint[]> raws, CaptureResult r)
    {
        var sb = new StringBuilder();
        sb.Append("# ").Append(FormatVersion).Append('\n');
        sb.Append("# ★実機の Tick Bus から拾った生のレコード。原本は csharp/TH09.Record/LiveCapture.cs\n");
        sb.Append("# ★語の並びは名前の昇順（Ordinal）。両側が自分の一覧を並べ替えるので写しが要らない\n");
        sb.Append("@range\t").Append(r.FromSeq.ToString(CultureInfo.InvariantCulture)).Append('\t')
          .Append(r.UntilSeq.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("@words\t").Append(string.Join("\t", SortedFields.Select(f => f.Name))).Append('\n');
        foreach (var w in raws)
        {
            for (var i = 0; i < SortedFields.Length; i++)
            {
                if (i > 0) sb.Append('\t');
                sb.Append(w[SortedFields[i].Offset >> 2].ToString(CultureInfo.InvariantCulture));
            }
            sb.Append('\n');
        }
        sb.Append("# ★母数: 要求 ").Append(r.Requested.ToString(CultureInfo.InvariantCulture))
          .Append(" tick のうち ").Append(r.Accepted.ToString(CultureInfo.InvariantCulture))
          .Append(" tick を拾った（欠け ").Append(r.Gaps.ToString(CultureInfo.InvariantCulture))
          .Append(" 件 / torn ").Append(r.Torn.ToString(CultureInfo.InvariantCulture))
          .Append(" / 再同期 ").Append(r.GapEvents.ToString(CultureInfo.InvariantCulture)).Append("）\n");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    public static SyntheticTicks.Case ToCase(IReadOnlyList<uint[]> raws)
    {
        var rows = new List<uint[]>(raws.Count);
        foreach (var w in raws)
        {
            var row = new uint[TickReplay.NeededWords.Length];
            for (var i = 0; i < row.Length; i++)
            {
                var off = _offsetOf[TickReplay.NeededWords[i]];
                row[i] = w[off >> 2];
            }
            rows.Add(row);
        }
        return new SyntheticTicks.Case(CaseName, "実機の Tick Bus から拾った生 tick", rows);
    }
}
