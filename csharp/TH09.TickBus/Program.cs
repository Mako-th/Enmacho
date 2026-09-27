using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TH09.TickBus;

public static class Program
{
    public static int Main(string[] args)
    {
        try { Console.OutputEncoding = new UTF8Encoding(false); } catch (IOException) { }
        try
        {
            return Run(args);
        }
        catch (TickBusLayoutException exc)
        {
            Console.Error.WriteLine("レイアウト: " + exc.Message);
            return 1;
        }
        catch (Exception exc)
        {
            Console.Error.WriteLine(exc.GetType().Name + ": " + exc.Message);
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            Help();
            return args.Length == 0 ? 2 : 0;
        }

        string mode = args[0];
        var opt = ParseOptions(args.Skip(1));

        return mode switch
        {
            "--selftest" => SelfTest.Run(Console.Out) == 0 ? 0 : 1,
            "--constants" => DumpConstants(),
            "--header" => DumpHeader(opt),
            "--parity" => Parity(opt),
            "--coord-header" => DumpCoordHeader(opt),
            "--coord-parity" => CoordParity(opt),
            _ => Usage($"未知のモードです: {mode}"),
        };
    }

    private static void Help()
    {
        Console.WriteLine("""
th09_tickbus — Tick Bus / 座標リングを読み取り専用で読む（段階 1）

  --selftest                            共有メモリを触らずに走る自己検査
  --constants                           実行時の定数を key=value で全部出す（写しの検査用）
  --header       --name <名前>          主リングのヘッダを出す（検証も行う）
  --parity       --name <名前> --out <json> [--start N | --from-head | --seconds S] [--limit N]
                                        レコードを seq ごとに JSON へ吐く（突き合わせ用）
  --coord-header --name <名前>          座標リングのヘッダを出す
  --coord-parity --name <名前> --out <json> --first N --count M
                                        窓を 1 本読んで内訳と digest を JSON へ吐く

  共通: --margin N（既定 64） --adonis（Execution Type を Network Play 側にする）
        --rewind N（--from-head / --seconds のとき head から何 tick 戻って読み始めるか）

★名前を省くと本物のバス（Local\TH09TickBus / Local\TH09TickCoord）を読む。
  **読み取り専用でしか開かない**（作る口を持たない）ので、居なければ素直に失敗する。
★合成のバスは必ず別名にすること（本物の名前で作ると本番の記録を壊しうる）。
""");
    }

    private static int Usage(string message)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine("--help を見てください。");
        return 2;
    }

    private sealed class Options
    {
        public string? Name;
        public string? Out;
        public uint? Start;
        public double? Seconds;
        public int Limit = 100000;
        public int Margin = RingReader.DefaultMargin;
        public int Rewind;
        public bool FromHead;
        public uint First;
        public int Count;
        public bool Adonis;
    }

    private static Options ParseOptions(IEnumerable<string> args)
    {
        var o = new Options();
        var list = args.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            string a = list[i];
            string Next(string what)
            {
                if (i + 1 >= list.Count) throw new TickBusLayoutException($"{what} に値がありません");
                return list[++i];
            }
            switch (a)
            {
                case "--name": o.Name = Next(a); break;
                case "--out": o.Out = Next(a); break;
                case "--start": o.Start = uint.Parse(Next(a), CultureInfo.InvariantCulture); break;
                case "--seconds": o.Seconds = double.Parse(Next(a), CultureInfo.InvariantCulture); break;
                case "--limit": o.Limit = int.Parse(Next(a), CultureInfo.InvariantCulture); break;
                case "--margin": o.Margin = int.Parse(Next(a), CultureInfo.InvariantCulture); break;
                case "--rewind": o.Rewind = int.Parse(Next(a), CultureInfo.InvariantCulture); break;
                case "--first": o.First = uint.Parse(Next(a), CultureInfo.InvariantCulture); break;
                case "--count": o.Count = int.Parse(Next(a), CultureInfo.InvariantCulture); break;
                case "--from-head": o.FromHead = true; break;
                case "--adonis": o.Adonis = true; break;
                default: throw new TickBusLayoutException($"未知の引数です: {a}");
            }
        }
        return o;
    }

    private static int DumpConstants()
    {
        Console.WriteLine($"tick.name={TickBusReader.DefaultName}");
        Console.WriteLine($"tick.magic={TickBusLayout.Magic}");
        Console.WriteLine($"tick.version={TickBusLayout.Version}");
        Console.WriteLine($"tick.header_size={TickBusLayout.HeaderSize}");
        Console.WriteLine($"tick.record_size={TickBusLayout.RecordSize}");
        Console.WriteLine($"tick.capacity={TickBusLayout.Capacity}");
        Console.WriteLine($"tick.index_mask={TickBusLayout.IndexMask}");
        Console.WriteLine($"tick.total_size={TickBusLayout.TotalSize}");
        Console.WriteLine($"tick.seq_building={TickBusLayout.SeqBuilding}");
        Console.WriteLine($"tick.flag_replaymgr_valid={TickBusBits.FlagReplaymgrValid}");
        foreach (var (name, off) in TickBusLayout.HeaderFields) Console.WriteLine($"tick.header.{name}={off}");
        foreach (var (name, off) in TickBusLayout.RecordFields) Console.WriteLine($"tick.field.{name}={off}");
        Console.WriteLine($"coord.name={CoordLayout.DefaultName}");
        Console.WriteLine($"coord.magic={CoordLayout.Magic}");
        Console.WriteLine($"coord.version={CoordLayout.Version}");
        Console.WriteLine($"coord.header_size={CoordLayout.HeaderSize}");
        Console.WriteLine($"coord.record_size={CoordLayout.RecordSize}");
        Console.WriteLine($"coord.capacity={CoordLayout.Capacity}");
        Console.WriteLine($"coord.bullet_slots={CoordLayout.BulletSlots}");
        Console.WriteLine($"coord.enemy_slots={CoordLayout.EnemySlots}");
        Console.WriteLine($"coord.laser_slots={CoordLayout.LaserSlots}");
        Console.WriteLine($"coord.laser_total={CoordLayout.LaserTotal}");
        Console.WriteLine($"coord.ex_slots={CoordLayout.ExSlots}");
        Console.WriteLine($"coord.shot_slots={CoordLayout.ShotSlots}");
        Console.WriteLine($"coord.shot_total={CoordLayout.ShotTotal}");
        Console.WriteLine($"coord.blast_slots={CoordLayout.BlastSlots}");
        Console.WriteLine($"coord.side_slots={CoordLayout.SideSlots}");
        Console.WriteLine($"coord.slots={CoordLayout.Slots}");
        Console.WriteLine($"coord.index_mask={CoordLayout.IndexMask}");
        Console.WriteLine($"coord.total_size={CoordLayout.TotalSize}");
        foreach (var (name, off) in CoordLayout.HeaderFields) Console.WriteLine($"coord.header.{name}={off}");
        foreach (var (name, off) in CoordLayout.RecordFields) Console.WriteLine($"coord.field.{name}={off}");
        for (int i = 0; i < CoordLayout.ColumnBlocks.Length; i++)
        {
            var b = CoordLayout.ColumnBlocks[i];
            Console.WriteLine($"coord.block.{i}={b.Name},{b.Offset},{b.Slots},{(b.IsU16 ? 2 : 4)}");
        }
        return 0;
    }


    private static int DumpHeader(Options o)
    {
        string name = o.Name ?? TickBusReader.DefaultName;
        using var bus = TickBusReader.Open(name);
        var h = bus.ReadHeader();
        foreach (var (word, off) in TickBusLayout.HeaderFields)
            Console.WriteLine($"  {word,-18} 0x{off:X03}  {h.At(off)}");
        var bad = TickBusLayout.RejectReason(h);
        if (bad is null)
        {
            Console.WriteLine("ヘッダは Tick Bus v" + TickBusLayout.Version + " として正当です。");
            return 0;
        }
        Console.Error.WriteLine($"★弾きました（reject={RejectField(bad.Value)}）。");
        return 1;
    }

    private static int Parity(Options o)
    {
        if (o.Out is null) return Usage("--out が要ります。");
        string name = o.Name ?? TickBusReader.DefaultName;
        using var bus = TickBusReader.OpenVerified(name);
        var reader = new RingReader(bus, o.Margin);
        var records = new List<TickRecord>();

        if (o.Start is not null)
        {
            reader.NextIndex = o.Start;
            records.AddRange(reader.Poll(o.Limit));
        }
        else if (o.FromHead)
        {
            reader.StartAtHead(o.Rewind);
            records.AddRange(reader.Poll(o.Limit));
        }
        else
        {
            reader.StartAtHead(o.Rewind);
            var deadline = DateTime.UtcNow.AddSeconds(o.Seconds ?? 3.0);
            while (DateTime.UtcNow < deadline && records.Count < o.Limit)
            {
                var got = reader.Poll(o.Limit - records.Count);
                if (got.Count == 0) Thread.Sleep(1);
                else records.AddRange(got);
            }
        }

        var header = bus.ReadHeader();
        using var stream = File.Create(o.Out);
        using var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });
        w.WriteStartObject();
        w.WriteString("impl", "csharp");
        w.WriteString("name", name);
        w.WriteStartObject("header");
        foreach (var (word, off) in TickBusLayout.HeaderFields) w.WriteNumber(word, header.At(off));
        w.WriteEndObject();
        w.WriteStartObject("counters");
        w.WriteNumber("gap_events", reader.GapEvents);
        w.WriteNumber("lost_records", reader.LostRecords);
        w.WriteNumber("torn_records", reader.TornRecords);
        w.WriteNumber("read_records", reader.ReadRecords);
        w.WriteNumber("rewound", reader.Rewound);
        if (reader.NextIndex is null) w.WriteNull("next_index"); else w.WriteNumber("next_index", reader.NextIndex.Value);
        w.WriteEndObject();
        w.WriteStartArray("records");
        foreach (var rec in records)
        {
            w.WriteStartObject();
            w.WriteNumber("seq", rec.Seq);
            w.WriteStartObject("words");
            foreach (var (word, off) in TickBusLayout.RecordFields) w.WriteNumber(word, rec.At(off));
            w.WriteEndObject();
            w.WriteStartObject("snapshot");
            foreach (var (key, value) in SnapshotMapper.FromRecord(rec, o.Adonis)) WriteSnap(w, key, value);
            w.WriteEndObject();
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
        w.Flush();
        Console.WriteLine($"{name}: {records.Count} レコードを {o.Out} へ書きました"
            + $"（torn {reader.TornRecords} / 欠落 {reader.LostRecords} / 再同期 {reader.GapEvents}）");
        return 0;
    }

    private static void WriteSnap(Utf8JsonWriter w, string key, SnapValue v)
    {
        switch (v.Kind)
        {
            case SnapValue.K.Int: w.WriteNumber(key, v.IntValue); break;
            case SnapValue.K.Float: w.WriteString(key, "f:" + BitConverter.DoubleToUInt64Bits(v.FloatValue).ToString("x16", CultureInfo.InvariantCulture)); break;
            default: w.WriteString(key, "s:" + v.StrValue); break;
        }
    }


    private static int DumpCoordHeader(Options o)
    {
        string name = o.Name ?? CoordLayout.DefaultName;
        using var bus = CoordBusReader.Open(name);
        var h = bus.ReadHeader();
        foreach (var (word, off) in CoordLayout.HeaderFields)
            Console.WriteLine($"  {word,-18} 0x{off:X02}  {h.At(off)}");
        var bad = CoordLayout.RejectReason(h);
        if (bad is null)
        {
            Console.WriteLine("ヘッダは Coord Bus v" + CoordLayout.Version + " として正当です。"
                + (h.Enable == 0 ? "★enable=0 なので DLL は開くが書きません。" : ""));
            return 0;
        }
        Console.Error.WriteLine($"★弾きました（reject={bad.Value.Field} 理由: {bad.Value.Message}）。");
        return 1;
    }

    private static int CoordParity(Options o)
    {
        if (o.Out is null) return Usage("--out が要ります。");
        string name = o.Name ?? CoordLayout.DefaultName;
        using var bus = CoordBusReader.OpenVerified(name);
        var reader = new CoordRingReader(bus, o.Margin);
        var win = reader.ReadWindow(o.First, o.Count);
        var header = bus.ReadHeader();

        using var stream = File.Create(o.Out);
        using var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });
        w.WriteStartObject();
        w.WriteString("impl", "csharp");
        w.WriteString("name", name);
        w.WriteStartObject("header");
        foreach (var (word, off) in CoordLayout.HeaderFields) w.WriteNumber(word, header.At(off));
        w.WriteEndObject();
        if (win is null)
        {
            w.WriteNull("window");
        }
        else
        {
            w.WriteStartObject("window");
            w.WriteNumber("first", win.First);
            w.WriteNumber("count", win.Count);
            w.WriteNumber("requested_first", win.RequestedFirst);
            w.WriteNumber("requested_count", win.RequestedCount);
            w.WriteNumber("skipped_head", win.SkippedHead);
            w.WriteNumber("stopped_ticks", win.StoppedTicks);
            w.WriteNumber("unwritten_tail", win.UnwrittenTail);
            if (win.Slack is null) w.WriteNull("slack"); else w.WriteNumber("slack", win.Slack.Value);
            if (win.StoppedAt is null) w.WriteNull("stopped_at"); else w.WriteNumber("stopped_at", win.StoppedAt.Value);
            w.WriteString("raw_sha256", CoordColumns.Sha256Hex(win.Raw));
            w.WriteStartObject("blocks");
            foreach (var (block, digest) in CoordColumns.BlockDigests(win.Raw, win.Count)) w.WriteString(block, digest);
            w.WriteEndObject();
            w.WriteEndObject();
        }
        w.WriteStartObject("counters");
        w.WriteNumber("torn_records", reader.TornRecords);
        w.WriteNumber("read_ticks", reader.ReadTicks);
        w.WriteNumber("raced_reads", reader.RacedReads);
        if (reader.MinSlack is null) w.WriteNull("min_slack"); else w.WriteNumber("min_slack", reader.MinSlack.Value);
        if (reader.LastSlack is null) w.WriteNull("last_slack"); else w.WriteNumber("last_slack", reader.LastSlack.Value);
        if (reader.LastMiss is null) w.WriteNull("last_miss"); else w.WriteString("last_miss", MissName(reader.LastMiss.Value));
        w.WriteEndObject();
        w.WriteEndObject();
        w.Flush();
        Console.WriteLine($"{name}: 窓 {(win is null ? "取れず" : win.Count + " tick")} を {o.Out} へ書きました"
            + (reader.LastMiss is null ? "" : $"（miss={MissName(reader.LastMiss.Value)}）"));
        return 0;
    }

    private static string RejectField(TickBusLayout.HeaderReject r) => r switch
    {
        TickBusLayout.HeaderReject.Magic => "magic",
        TickBusLayout.HeaderReject.Version => "version",
        TickBusLayout.HeaderReject.RecordSize => "record_size",
        _ => "capacity",
    };

    private static string MissName(CoordMiss miss) => miss switch
    {
        CoordMiss.Overrun => "overrun",
        CoordMiss.Torn => "torn",
        _ => "unwritten",
    };
}
