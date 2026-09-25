using System.Globalization;
using System.Text;
using TH09.Layer0;
using TH09.Record.Generated;
using TH09.TickBus;

namespace TH09.Record;

public static class HitWindowScript
{
    public const string FormatVersion = "th09-record-hitwin-v1";

    private sealed class Setup
    {
        public int Before = 6;
        public int After = 4;
        public bool CountValid = true;
        public int? BeforeRawMax;
        public int? AfterRawMax;
        public int? MaxWindow;
        public int Margin = CoordRingReader.DefaultMargin;
        public int Level = TickArchive.DefaultLevel;
        public int RecordVersion = (int)TH09.Generated.TickWords.Version;
        public List<string>? Triggers;
        public Dictionary<string, bool>? Scopes;
        public string? ReplaySource;
        public bool Adonis;
        public List<(string Verb, string[] Args)> Steps = [];
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
                case "before": s.Before = Int(f[1]); break;
                case "after": s.After = Int(f[1]); break;
                case "count_valid": s.CountValid = f[1] == "1"; break;
                case "before_raw_max": s.BeforeRawMax = Int(f[1]); break;
                case "after_raw_max": s.AfterRawMax = Int(f[1]); break;
                case "max_window": s.MaxWindow = Int(f[1]); break;
                case "margin": s.Margin = Int(f[1]); break;
                case "level": s.Level = Int(f[1]); break;
                case "record_version": s.RecordVersion = Int(f[1]); break;
                case "triggers": s.Triggers = [.. f[1].Split(',', StringSplitOptions.RemoveEmptyEntries)]; break;
                case "adonis": s.Adonis = f[1] == "1"; break;
                case "replay_source": s.ReplaySource = f[1].Length == 0 ? null : f[1]; break;
                case "scopes":
                    s.Scopes = f[1] == "all" ? null : ParseScopes(f[1]);
                    break;
                case "@tick":
                case "@feed":
                case "@begin":
                case "@pump":
                case "@pump-force":
                case "@end":
                case "@replay-source":
                    s.Steps.Add((f[0], f[1..]));
                    break;
                default:
                    throw new InvalidDataException("台本に知らない行があります: " + line);
            }
        }
        if (s.Steps.Count == 0)
            throw new InvalidDataException("台本に手順が 1 つもありません（0 手は『測れていない』）");
        return s;
    }

    private static int Int(string s) => int.Parse(s, CultureInfo.InvariantCulture);

    private static Dictionary<string, bool> ParseScopes(string text)
    {
        var raw = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=');
            if (kv.Length != 2) throw new InvalidDataException("scopes の書き方が読めません: " + part);
            raw[kv[0]] = kv[1] == "1";
        }
        return CaptureScope.Normalize(raw);
    }

    private static readonly Dictionary<string, int> WordOffsets =
        TickBusLayout.RecordFields.ToDictionary(x => x.Name, x => x.Offset, StringComparer.Ordinal);

    private static TickRecord MakeRecord(string[] args)
    {
        var words = new uint[TH09.Generated.TickWords.RecordSize / 4];
        uint seq = uint.Parse(args[0], CultureInfo.InvariantCulture);
        words[TH09.Generated.TickWords.Record.SeqBeginOffset >> 2] = seq;
        words[TH09.Generated.TickWords.Record.SeqEndOffset >> 2] = seq;
        if (args.Length > 1 && args[1].Length > 0)
        {
            foreach (var part in args[1].Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=');
                if (kv.Length != 2) throw new InvalidDataException("@tick の語が読めません: " + part);
                if (!WordOffsets.TryGetValue(kv[0], out var off))
                    throw new InvalidDataException("@tick に知らない語名があります: " + kv[0]);
                words[off >> 2] = uint.Parse(kv[1], CultureInfo.InvariantCulture);
            }
        }
        return new TickRecord(words);
    }

    public static int Run(string coordName, string scriptPath, string dbPath, string outPath)
    {
        var setup = Parse(scriptPath);
        CoordColumnNames.SelfCheck();

        var logs = new List<string>();
        using var bus = CoordBusReader.OpenVerified(coordName);
        var reader = new CoordRingReader(bus, setup.Margin);

        using var writer = Layer0Writer.Open(new Layer0Writer.Options(
            DbPath: dbPath,
            Mode: Layer0OpenMode.Create,
            RecordVersion: setup.RecordVersion,
            Fields: [.. TickBusLayout.RecordFields.Select(
                (x, i) => new Layer0Writer.FieldSlot(x.Name, x.Offset >> 2))],
            Policy: TickEncoder.SectionPolicy.Empty,
            MaxSegmentTicks: 1,
            Level: setup.Level));

        var collector = new HitWindowCollector(reader, writer, new HitWindowCollector.Options(
            Before: setup.Before,
            After: setup.After,
            CountValid: setup.CountValid,
            BeforeRawMax: setup.BeforeRawMax,
            AfterRawMax: setup.AfterRawMax,
            MaxWindow: setup.MaxWindow,
            Triggers: setup.Triggers,
            Margin: setup.Margin), logs.Add);

        string? replaySource = setup.ReplaySource;
        if (setup.Scopes is not null)
        {
            collector.ScopeGate = rec =>
            {
                var scope = CaptureScope.OfRecord(rec, setup.Adonis, replaySource);
                return CaptureScope.IsEnabled(scope, setup.Scopes) ? null : scope;
            };
        }

        var sb = new StringBuilder();
        sb.Append("# ").Append(FormatVersion).Append(" out\n");
        sb.Append("# ★原本は csharp/TH09.Record/HitWindowScript.cs（台本は Python 側が書く）\n");
        sb.Append("start\tbefore=").Append(setup.Before)
          .Append("\tafter=").Append(setup.After)
          .Append("\tcount_valid=").Append(setup.CountValid ? 1 : 0)
          .Append("\tscopes=").Append(setup.Scopes is null ? "all" : "set")
          .Append('\n');
        int logAt = 0;
        void FlushLogs(string tag)
        {
            for (; logAt < logs.Count; logAt++)
                sb.Append("log\t").Append(tag).Append('\t').Append(logs[logAt]).Append('\n');
        }
        FlushLogs("init");

        var batch = new List<TickRecord>();
        long ticks = 0, feeds = 0, pumps = 0;
        for (int i = 0; i < setup.Steps.Count; i++)
        {
            var (verb, args) = setup.Steps[i];
            var tag = i.ToString(CultureInfo.InvariantCulture);
            switch (verb)
            {
                case "@tick":
                    batch.Add(MakeRecord(args));
                    ticks++;
                    break;
                case "@feed":
                    collector.Feed(batch);
                    sb.Append("step\t").Append(tag).Append("\tfeed\tticks=")
                      .Append(batch.Count.ToString(CultureInfo.InvariantCulture))
                      .Append("\tpending=").Append(collector.PendingCount.ToString(CultureInfo.InvariantCulture))
                      .Append('\n');
                    batch = [];
                    feeds++;
                    break;
                case "@begin":
                    collector.BeginSession(long.Parse(args[0], CultureInfo.InvariantCulture));
                    sb.Append("step\t").Append(tag).Append("\tbegin\t").Append(args[0]).Append('\n');
                    break;
                case "@pump":
                case "@pump-force":
                {
                    int n = collector.Pump(force: verb == "@pump-force");
                    pumps++;
                    sb.Append("step\t").Append(tag).Append('\t').Append(verb[1..])
                      .Append("\twrote=").Append(n.ToString(CultureInfo.InvariantCulture))
                      .Append("\tpending=").Append(collector.PendingCount.ToString(CultureInfo.InvariantCulture))
                      .Append('\n');
                    break;
                }
                case "@end":
                    collector.EndSession();
                    sb.Append("step\t").Append(tag).Append("\tend\tpending=")
                      .Append(collector.PendingCount.ToString(CultureInfo.InvariantCulture)).Append('\n');
                    break;
                case "@replay-source":
                    replaySource = args.Length > 0 && args[0].Length > 0 ? args[0] : null;
                    sb.Append("step\t").Append(tag).Append("\treplay-source\t")
                      .Append(replaySource ?? "None").Append('\n');
                    break;
                default:
                    throw new InvalidDataException("知らない手順です: " + verb);
            }
            FlushLogs(tag);
        }

        sb.Append("counters")
          .Append("\twindows=").Append(collector.WindowsWritten)
          .Append("\tticks=").Append(collector.TicksWritten)
          .Append("\thits_seen=").Append(collector.HitsSeen)
          .Append("\tlost=").Append(collector.LostTicks)
          .Append("\tverify_failures=").Append(collector.VerifyFailures)
          .Append("\tverify_checked=").Append(collector.VerifyChecked)
          .Append("\tmin_slack=").Append(collector.MinSlack?.ToString(CultureInfo.InvariantCulture) ?? "None")
          .Append("\thead_capped=").Append(collector.WindowsHeadCapped)
          .Append("\ttail_capped=").Append(collector.WindowsTailCapped)
          .Append("\tmerge_capped=").Append(collector.WindowsMergeCapped)
          .Append("\ttail_unfinished=").Append(collector.WindowsTailUnfinished)
          .Append("\tpending=").Append(collector.PendingCount)
          .Append("\tskipped=").Append(collector.HitsSkipped)
          .Append("\traced=").Append(reader.RacedReads)
          .Append('\n');
        sb.Append("# bytes=").Append(collector.BytesWritten).Append('\n');
        foreach (var t in setup.Triggers ?? [.. HitWindowConst.DefaultTriggers])
            sb.Append("trigger\t").Append(t).Append('\t')
              .Append(collector.HitsByTrigger.GetValueOrDefault(t)).Append('\n');
        foreach (var kv in collector.HitsSkippedByScope.OrderBy(x => x.Key, StringComparer.Ordinal))
            sb.Append("skipped\t").Append(kv.Key).Append('\t').Append(kv.Value).Append('\n');
        sb.Append("note\t").Append(collector.SkippedNote()).Append('\n');
        sb.Append("status\t").Append(collector.Status()).Append('\n');
        sb.Append("end\tticks=").Append(ticks).Append("\tfeeds=").Append(feeds)
          .Append("\tpumps=").Append(pumps).Append('\n');

        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("書き出し: " + outPath);
        Console.WriteLine("  ★母数: 生 tick " + ticks.ToString(CultureInfo.InvariantCulture)
                          + " 枚 / feed " + feeds.ToString(CultureInfo.InvariantCulture)
                          + " 回 / pump " + pumps.ToString(CultureInfo.InvariantCulture) + " 回");
        Console.WriteLine("  " + collector.Status());
        return ticks > 0 ? 0 : 1;
    }
}
