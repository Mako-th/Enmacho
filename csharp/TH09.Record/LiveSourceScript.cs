using System.Globalization;
using System.Text;
using TH09.TickBus;

namespace TH09.Record;

public static class LiveSourceScript
{
    public const string FormatVersion = "th09-record-livesource-v1";

    public const string RaiseUnavailable = "snapshot-unavailable";

    public const string RaiseHookLost = "tick-hook-lost";

    private sealed class Setup
    {
        public int Margin = RingReader.DefaultMargin;
        public int Rewind;
        public int InvalidTicks = 30;
        public double InvalidSeconds = 0.5;
        public double StallSeconds = 3.0;
        public bool Adonis;
        public bool GameDead;
        public List<string> Fields = [];
        public List<string> Sinks = [];
        public List<(string Verb, string Arg)> Steps = [];
    }

    private static Setup Parse(string path)
    {
        var s = new Setup();
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0 || line[0] == '#') continue;
            var f = line.Split('\t');
            switch (f[0])
            {
                case "margin": s.Margin = int.Parse(f[1], CultureInfo.InvariantCulture); break;
                case "rewind": s.Rewind = int.Parse(f[1], CultureInfo.InvariantCulture); break;
                case "invalid_ticks": s.InvalidTicks = int.Parse(f[1], CultureInfo.InvariantCulture); break;
                case "invalid_seconds": s.InvalidSeconds = double.Parse(f[1], CultureInfo.InvariantCulture); break;
                case "stall_seconds": s.StallSeconds = double.Parse(f[1], CultureInfo.InvariantCulture); break;
                case "adonis": s.Adonis = f[1] == "1"; break;
                case "gamedead": s.GameDead = f[1] == "1"; break;
                case "fields": s.Fields = [.. f[1..]]; break;
                case "@sink": s.Sinks.Add(f[1]); break;
                case "@seek": s.Steps.Add(("seek", f[1])); break;
                case "@poll": s.Steps.Add(("poll", "")); break;
                case "@sleep": s.Steps.Add(("sleep", f[1])); break;
                default:
                    throw new InvalidDataException("台本に知らない行があります: " + line);
            }
        }
        if (s.Fields.Count == 0)
            throw new InvalidDataException("台本に fields 行がありません（0 語は『測れていない』）");
        if (s.Steps.Count == 0)
            throw new InvalidDataException("台本に手順が 1 つもありません（0 手は『測れていない』）");
        return s;
    }

    public static int Run(string busName, string scriptPath, string outPath, Action<string> log)
    {
        var setup = Parse(scriptPath);
        var sb = new StringBuilder();
        sb.Append("# ").Append(FormatVersion).Append(" out\n");
        sb.Append("# ★原本は csharp/TH09.Record/LiveSourceScript.cs（台本は Python 側が書く）\n");
        sb.Append("@sinks\t").Append(string.Join("\t", setup.Sinks)).Append('\n');
        sb.Append("@fields\t").Append(string.Join("\t", setup.Fields)).Append('\n');

        var calls = new List<(string Name, List<uint> Seqs)>();
        TickRecord? first = null;

        using var bus = TickBusReader.OpenVerified(busName);
        using var src = new LiveTickSource(
            bus,
            new LiveTickSource.Options(setup.Rewind, setup.Margin, setup.InvalidTicks,
                                       setup.InvalidSeconds, setup.StallSeconds),
            log)
        {
            AdonisLoaded = setup.Adonis,
            GameAlive = setup.GameDead ? () => false : null,
        };

        src.Start();
        sb.Append("start\trewound=").Append(src.Ring.Rewound.ToString(CultureInfo.InvariantCulture))
          .Append("\tnext=").Append((src.Ring.NextIndex ?? 0).ToString(CultureInfo.InvariantCulture))
          .Append('\n');

        foreach (var name in setup.Sinks)
        {
            var captured = name;
            src.AddSink(captured, recs =>
            {
                if (first is null && recs.Count > 0) first = recs[0];
                calls.Add((captured, [.. recs.Select(r => r.Seq)]));
            });
        }

        long polls = 0, snapshots = 0, sinkCalls = 0;
        for (var i = 0; i < setup.Steps.Count; i++)
        {
            var (verb, arg) = setup.Steps[i];
            var tag = i.ToString(CultureInfo.InvariantCulture);
            switch (verb)
            {
                case "seek":
                    src.Ring.NextIndex = uint.Parse(arg, CultureInfo.InvariantCulture);
                    sb.Append("step\t").Append(tag).Append("\tseek\t").Append(arg).Append('\n');
                    continue;
                case "sleep":
                    Thread.Sleep((int)Math.Round(double.Parse(arg, CultureInfo.InvariantCulture) * 1000.0));
                    sb.Append("step\t").Append(tag).Append("\tsleep\t").Append(arg).Append('\n');
                    continue;
                default:
                    break;
            }

            calls.Clear();
            IReadOnlyList<Snapshot> snaps = [];
            string? raised = null;
            try
            {
                snaps = src.Read();
            }
            catch (SnapshotUnavailableException) { raised = RaiseUnavailable; }
            catch (TickHookLostException) { raised = RaiseHookLost; }
            polls++;
            snapshots += snaps.Count;
            sinkCalls += calls.Count;

            sb.Append("step\t").Append(tag).Append("\tpoll\tsnapshots=")
              .Append(snaps.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
            foreach (var (name, seqs) in calls)
            {
                sb.Append("sink\t").Append(tag).Append('\t').Append(name).Append('\t')
                  .Append(string.Join(",", seqs.Select(x => x.ToString(CultureInfo.InvariantCulture))))
                  .Append('\n');
            }
            sb.Append("counters\t").Append(tag)
              .Append("\tread=").Append(src.Ring.ReadRecords.ToString(CultureInfo.InvariantCulture))
              .Append("\tlost=").Append(src.Ring.LostRecords.ToString(CultureInfo.InvariantCulture))
              .Append("\ttorn=").Append(src.Ring.TornRecords.ToString(CultureInfo.InvariantCulture))
              .Append("\tgap=").Append(src.Ring.GapEvents.ToString(CultureInfo.InvariantCulture))
              .Append('\n');
            if (raised is not null)
                sb.Append("raise\t").Append(tag).Append('\t').Append(raised).Append('\n');

            for (var j = 0; j < snaps.Count; j++)
            {
                sb.Append("snap\t").Append(tag).Append('\t').Append(j.ToString(CultureInfo.InvariantCulture));
                foreach (var name in setup.Fields)
                    sb.Append('\t').Append(LiveTickSource.Token(snaps[j], name));
                sb.Append('\n');
                sb.Append("newgame\t").Append(tag).Append('\t')
                  .Append(j.ToString(CultureInfo.InvariantCulture)).Append('\t')
                  .Append(NewGameRule.IsNewGame(snaps[j]) ? "1" : "0").Append('\n');
            }
            if (snaps.Count > 0)
            {
                var sameWall = snaps.All(x => string.Equals(x.WallTime, snaps[0].WallTime, StringComparison.Ordinal));
                var sameMono = snaps.All(x => x.Monotonic.Equals(snaps[0].Monotonic));
                sb.Append("batch\t").Append(tag)
                  .Append("\twallsame=").Append(sameWall ? "1" : "0")
                  .Append("\tmonosame=").Append(sameMono ? "1" : "0").Append('\n');
            }
        }

        if (first is TickRecord rec)
        {
            var bad = LiveTickSource.CheckAgainstMapper(rec, setup.Adonis);
            sb.Append("mapcheck\tfields=")
              .Append(LiveTickSource.MapperBackedFieldCount.ToString(CultureInfo.InvariantCulture))
              .Append("\tbad=").Append(bad.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
            foreach (var (name, mine, mapper) in bad)
                sb.Append("mapbad\t").Append(name).Append('\t').Append(mine).Append('\t').Append(mapper).Append('\n');
        }
        else
        {
            sb.Append("mapcheck\tfields=")
              .Append(LiveTickSource.MapperBackedFieldCount.ToString(CultureInfo.InvariantCulture))
              .Append("\tbad=-1\n");
        }

        sb.Append("end\tpolls=").Append(polls.ToString(CultureInfo.InvariantCulture))
          .Append("\tsnapshots=").Append(snapshots.ToString(CultureInfo.InvariantCulture))
          .Append("\tsinkcalls=").Append(sinkCalls.ToString(CultureInfo.InvariantCulture)).Append('\n');

        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("書き出し: " + outPath);
        Console.WriteLine("  ★母数: poll " + polls.ToString(CultureInfo.InvariantCulture)
                          + " 回のうち Snapshot " + snapshots.ToString(CultureInfo.InvariantCulture)
                          + " 枚 / 行き先の呼び出し " + sinkCalls.ToString(CultureInfo.InvariantCulture)
                          + " 回（挿してある行き先 " + setup.Sinks.Count.ToString(CultureInfo.InvariantCulture)
                          + " 本）");
        Console.WriteLine("  " + src.Status());
        return polls > 0 ? 0 : 1;
    }
}
