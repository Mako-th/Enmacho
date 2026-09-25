using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace TH09.Layer0;

public static class Program
{
    private const string Usage = """
        TH09 Layer 0（段階 2）。Python 版 th09_tick_archive.py と同じ値を出す。

          th09_layer0 --selftest
              自己検査。★合成データ（実データに無い経路）＋ 否定テスト。DB もゲームも触らない。

          th09_layer0 --dump --corpus <corpus.txt> --out <csharp.tsv>
              corpus.txt に並んだセグメント／窓を全部復号して、列のフィンガープリントを書く。
              ★対象も DB の場所も corpus.txt から取る（C# 側で数え直さない）。

          th09_layer0 --compare --python <python.tsv> --csharp <csharp.tsv> [--report <out.txt>]
              2 つの記録を突き合わせる。★0 件は「一致」ではなく NG。

          th09_layer0 --roundtrip --corpus <corpus.txt> --hints <hints.txt> --out <pack.bin>
              corpus のものを C# で復号 → C# で符号化し直して入れ物へ書く。
              合成（実データに無い経路）も同じ入れ物へ入れる。
              ★中身の照合は Python 側（parity/verify_roundtrip.py）が行う。
              --hints は Python の INVARIANT_HINT / CHANGE_HINT を書き出したもの。
              ★C# 側に表を写していないので、これが無いと区画の割り当てが Python と揃わない。

          th09_layer0 --sizes --corpus <corpus.txt> --out <sizes.tsv>
              Python の zlib と .NET の zlib で圧縮後バイト数がどれだけ違うかを測る（参考値）。

          th09_layer0 --rebuild --db <元> --out <行き先> [--threads N] [--resume] [--limit N] [--deep]
              ★Layer 0 を新形式（solid-brotli-v2。検査和つき）へ★建て直す。
              ★元は読むだけ（Mode=ReadOnly ＋ PRAGMA query_only=1）。★原本は 1 バイトも書かない。
              ★行き先は「まだ無いファイル」でなければならない（--resume のときだけ既存を開く）。
              ★入替（改名）はこの道具の仕事ではない ——切替プロトコルの 1 行。
              --limit は試し（★完了の印を入れないので、そのまま --resume で続けられる）。
              --deep は行き先を読み直して列まで組み上げて比べる（★全行。時間がかかる）。

          th09_layer0 --write --script <台本> --out <db> --hints <hints.txt>
                      --record-version N [--segment-ticks N] [--pending-ticks N]
                      [--level N] [--append] [--no-verify]
                      [--encoding <colzlib-v1|solid-brotli-v1|solid-brotli-v2>]
              ★台本のとおりに Layer 0 へ★セグメントを書き足す（Layer0Writer）。
              ★行き先は「まだ無いファイル」でなければならない（--append のときだけ既存を開く）。
              ★--append で開けるのは、この道具が建てた DB だけ（★本物の Layer 0 は開けない）。
              ★--encoding は blob 列へ実際に入れる容器（省略時は colzlib-v1。段階 5 単位 9e）。
              ★書いた直後に読み戻す（--no-verify で外せるが、外すと「黙って空を書いた」が出ない）。

          th09_layer0 --recheck --db <db>
              ★既にある Layer 0 の全セグメントを、書く側と同じ経路で読み戻す。
              ★読むだけ（Mode=ReadOnly ＋ PRAGMA query_only=1）。
        """;

    private static readonly HashSet<string> KnownFlags = new(StringComparer.Ordinal)
    {
        "help", "h", "selftest", "dump", "compare", "roundtrip", "sizes", "rebuild",
        "corpus", "out", "python", "csharp", "report", "hints",
        "db", "threads", "resume", "limit", "deep",
        "write", "script", "record-version", "segment-ticks", "pending-ticks",
        "level", "append", "no-verify", "recheck", "encoding",
    };

    public static int Main(string[] args)
    {
        try { Console.OutputEncoding = new UTF8Encoding(false); } catch (IOException) { }

        var opt = ParseArgs(args);
        var unknown = opt.Keys.Where(k => !KnownFlags.Contains(k)).ToList();
        if (unknown.Count > 0)
        {
            Console.Error.WriteLine("知らない引数です: " + string.Join(" / ", unknown.Select(u => "--" + u)));
            Console.Error.WriteLine("--help を見てください。");
            return 2;
        }
        if (args.Length == 0 || opt.ContainsKey("help") || opt.ContainsKey("h"))
        {
            Console.WriteLine(Usage);
            return args.Length == 0 ? 1 : 0;
        }

        try
        {
            if (opt.ContainsKey("selftest")) return SelfTest.Run();
            if (opt.ContainsKey("dump")) return RunDump(opt);
            if (opt.ContainsKey("compare")) return RunCompare(opt);
            if (opt.ContainsKey("roundtrip")) return RunRoundTrip(opt);
            if (opt.ContainsKey("sizes")) return RunSizes(opt);
            if (opt.ContainsKey("rebuild")) return RunRebuild(opt);
            if (opt.ContainsKey("write")) return RunWrite(opt);
            if (opt.ContainsKey("recheck")) return RunRecheck(opt);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"落ちた: {e.GetType().Name}: {e.Message}");
            return 2;
        }

        Console.Error.WriteLine("引数が分かりません。--help を見てください。");
        return 1;
    }


    private static int RunRebuild(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "db", out var db) || !Require(opt, "out", out var outPath)) return 1;

        int threads = Math.Max(1, Environment.ProcessorCount - 2);
        if (opt.TryGetValue("threads", out var t) && t is not null)
        {
            if (!int.TryParse(t, out threads) || threads < 1)
            {
                Console.Error.WriteLine("--threads は 1 以上の整数で指定してください");
                return 2;
            }
        }
        int limit = 0;
        if (opt.TryGetValue("limit", out var l) && l is not null)
        {
            if (!int.TryParse(l, out limit) || limit < 1)
            {
                Console.Error.WriteLine("--limit は 1 以上の整数で指定してください");
                return 2;
            }
        }

        var options = new Layer0Rebuild.Options(
            SourcePath: db, DestPath: outPath, Threads: threads,
            Resume: opt.ContainsKey("resume"), Limit: limit, Deep: opt.ContainsKey("deep"));
        return Layer0Rebuild.Run(options, Console.Out);
    }


    private static int RunWrite(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "script", out var scriptPath) || !Require(opt, "out", out var outPath)) return 1;
        if (!Require(opt, "hints", out var hintsPath)) return 1;
        if (!RequireInt(opt, "record-version", out int recordVersion, min: 0)) return 2;
        if (!OptionalInt(opt, "segment-ticks", Generated.Layer0Schema.CaptureSegmentTicks,
                         out int segmentTicks, min: 1)) return 2;
        if (!OptionalInt(opt, "pending-ticks", Generated.Layer0Schema.PendingTickLimit,
                         out int pendingTicks, min: 0)) return 2;
        if (!OptionalInt(opt, "level", Generated.Layer0Schema.DefaultLevel, out int level, min: 0)) return 2;
        opt.TryGetValue("encoding", out var encoding);
        encoding ??= TickArchive.Encoding;
        if (encoding != TickArchive.Encoding && encoding != SolidBrotli.Encoding
            && encoding != SolidBrotli.EncodingV2)
        {
            Console.Error.WriteLine(
                $"--encoding は {TickArchive.Encoding} か {SolidBrotli.Encoding} か "
                + $"{SolidBrotli.EncodingV2} で指定してください（{encoding} は知りません）");
            return 2;
        }

        var script = Layer0WriteScript.Load(scriptPath);
        var options = new Layer0Writer.Options(
            DbPath: outPath,
            Mode: opt.ContainsKey("append") ? Layer0OpenMode.Append : Layer0OpenMode.Create,
            RecordVersion: recordVersion,
            Fields: script.Fields,
            Policy: TickEncoder.SectionPolicy.Load(hintsPath),
            MaxSegmentTicks: segmentTicks,
            PendingTickLimit: pendingTicks,
            Level: level,
            Verify: !opt.ContainsKey("no-verify"),
            WriteEncoding: encoding);

        Layer0WriteScript.Result ran;
        Layer0Writer.Stats stats;
        using (var writer = Layer0Writer.Open(options, Console.Out))
        {
            ran = Layer0WriteScript.Run(script, writer);
            writer.Flush();
            stats = writer.GetStats();
        }

        Console.WriteLine($"行き先: {outPath}");
        Console.WriteLine($"台本: 列 {script.Fields.Count} 本 / 語 {script.WordCount} / "
                          + $"tick {ran.Ticks} 本 / add {ran.Adds} 回 / "
                          + $"begin {ran.Begins} 回 / break {ran.Breaks} 回 / end {ran.Ends} 回");
        Console.WriteLine($"書いた: セグメント {stats.Segments} 本 / tick {stats.Ticks} 本 / "
                          + $"blob {stats.Bytes:N0} B / lost {stats.Lost} / torn {stats.Torn}");
        Console.WriteLine($"読み戻し: {stats.VerifyChecked} 本を読み直して "
                          + $"{stats.VerifyFailures} 本が失敗"
                          + (options.Verify ? "" : "（★--no-verify なので 1 本も見ていない）"));
        if (stats.Segments == 0)
        {
            Console.Error.WriteLine("★セグメントを 1 本も書いていません。");
            return 1;
        }
        return stats.VerifyFailures == 0 ? 0 : 1;
    }


    private static int RunRecheck(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "db", out var db)) return 1;
        var (checkedCount, failed, declared) = Layer0Writer.Recheck(db, Console.Out);
        Console.WriteLine($"読み戻し: {checkedCount} 本のうち {failed} 本が読めない"
                          + $"（中身の無い申告セグメント {declared} 本は飛ばした）");
        if (checkedCount == 0)
        {
            Console.WriteLine("★0 本です。★これは「全部読めた」ではなく「測れていない」。");
            return 1;
        }
        return failed == 0 ? 0 : 1;
    }


    private static int RunDump(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "corpus", out var corpusPath) || !Require(opt, "out", out var outPath)) return 1;
        var corpus = Corpus.Load(corpusPath);
        if (corpus.Items.Count == 0)
        {
            Console.Error.WriteLine("corpus が 0 件です。測れていないので中止します。");
            return 1;
        }

        var sw = Stopwatch.StartNew();
        using var db = new Layer0Reader(corpus.DbPath);
        var rows = new List<Parity.Row>(corpus.Items.Count);
        long words = 0;
        foreach (var item in corpus.Items)
        {
            rows.Add(item.Kind == Corpus.Kind.Ticks ? DumpSegment(db, item) : DumpWindow(db, item));
            words += rows[^1].Words;
        }
        sw.Stop();

        Parity.WriteRows(outPath, rows, "csharp", corpus.DbPath,
        [
            $"セグメント {corpus.TicksCount} 件 / 窓 {corpus.WindowCount} 件",
            $"かかった時間 {sw.Elapsed.TotalSeconds:F1} 秒",
        ]);
        Console.WriteLine($"C# 側を書き出した: {rows.Count} 件 / {words:N0} 語 / {sw.Elapsed.TotalSeconds:F1} 秒 → {outPath}");
        Console.WriteLine($"  内訳: session_ticks {corpus.TicksCount} セグメント / session_hit_windows {corpus.WindowCount} 窓");
        return 0;
    }

    private static Parity.Row DumpSegment(Layer0Reader db, Corpus.Item item)
    {
        var seg = db.ReadSegment(item.SessionId, item.No)
                  ?? throw new InvalidDataException($"セグメントが見つかりません: {item.SessionId}/{item.No}");
        var order = TickArchive.ParseFieldOrder(seg.FieldOrderText);
        var cols = TickArchive.Decode(seg.Blob, seg.Encoding, order);
        return new Parity.Row("T", item.SessionId, item.No, order.TickCount, cols.Count, cols.WordCount,
                              Parity.ColumnDigest(cols), Parity.FieldOrderDigest(order.Raw));
    }

    private static Parity.Row DumpWindow(Layer0Reader db, Corpus.Item item)
    {
        var win = db.ReadWindow(item.SessionId, item.No)
                  ?? throw new InvalidDataException($"窓が見つかりません: {item.SessionId}/{item.No}");
        var order = TickArchive.UnpackFieldOrder(win.FieldOrderBlob, win.FieldOrderEncoding);
        var cols = TickArchive.Decode(win.Blob, win.Encoding, order);
        return new Parity.Row("W", item.SessionId, item.No, order.TickCount, cols.Count, cols.WordCount,
                              Parity.ColumnDigest(cols), Parity.FieldOrderDigest(order.Raw));
    }


    private static int RunCompare(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "python", out var pyPath) || !Require(opt, "csharp", out var csPath)) return 1;
        var py = Parity.ReadRows(pyPath);
        var cs = Parity.ReadRows(csPath);
        var rep = Parity.Compare(py, cs);

        long pyWords = py.Sum(r => r.Words);
        if (rep.Total == 0)
        {
            Console.WriteLine("★記録が 0 件です。★これは「全部一致」ではなく「測れていない」。");
        }
        else
        {
            Console.WriteLine($"突き合わせ: {rep.Total} 件のうち {rep.MatchedColumns} 件で列の値が全語一致");
            Console.WriteLine($"            {rep.Total} 件のうち {rep.MatchedFieldOrder} 件で field_order の正規形が一致");
            Console.WriteLine($"            比べた語は {rep.WordsCompared:N0} 語（Python 側の記録に載っている語は {pyWords:N0} 語）");
        }

        if (opt.TryGetValue("report", out var reportPath) && reportPath is not null)
        {
            File.WriteAllLines(reportPath,
                [$"# 突き合わせ {rep.MatchedColumns}/{rep.Total} 件で列が一致 / {rep.MatchedFieldOrder}/{rep.Total} 件で field_order が一致",
                 $"# 比べた語 {rep.WordsCompared}",
                 .. rep.Problems], new UTF8Encoding(false));
            Console.WriteLine($"詳細を書いた: {reportPath}");
        }
        foreach (var line in rep.Problems.Take(20)) Console.WriteLine("  " + line);
        if (rep.Problems.Count > 20) Console.WriteLine($"  …ほか {rep.Problems.Count - 20} 件（--report で全部出る）");

        Console.WriteLine(rep.Ok ? "OK: 全数一致" : "NG");
        return rep.Ok ? 0 : 1;
    }


    private static int RunRoundTrip(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "corpus", out var corpusPath) || !Require(opt, "out", out var outPath)) return 1;
        if (!Require(opt, "hints", out var hintsPath)) return 1;
        var policy = TickEncoder.SectionPolicy.Load(hintsPath);
        var corpus = Corpus.Load(corpusPath);

        var records = new List<RoundTripPack.Record>();
        var sw = Stopwatch.StartNew();

        long no = 0;
        foreach (var c in Synthetic.All()) records.Add(Synthetic.BuildRecord(c, no++));
        int syntheticCount = records.Count;

        using var db = new Layer0Reader(corpus.DbPath);
        foreach (var item in corpus.Items)
        {
            if (item.Kind == Corpus.Kind.Ticks)
            {
                var seg = db.ReadSegment(item.SessionId, item.No)
                          ?? throw new InvalidDataException($"セグメントが見つかりません: {item.SessionId}/{item.No}");
                var order = TickArchive.ParseFieldOrder(seg.FieldOrderText);
                var cols = TickArchive.Decode(seg.Blob, seg.Encoding, order);
                records.Add(ReEncode("T", item, order.Fields, cols, policy, seg.RecordVersion,
                                     compress: true, omitFields: false));
            }
            else
            {
                var win = db.ReadWindow(item.SessionId, item.No)
                          ?? throw new InvalidDataException($"窓が見つかりません: {item.SessionId}/{item.No}");
                var order = TickArchive.UnpackFieldOrder(win.FieldOrderBlob, win.FieldOrderEncoding);
                var cols = TickArchive.Decode(win.Blob, win.Encoding, order);
                records.Add(ReEncode("W", item, order.Fields, cols, policy, order.RecordVersion,
                                     compress: true, omitFields: true));
            }
        }
        sw.Stop();

        RoundTripPack.Write(outPath, records);
        Console.WriteLine($"往復の入れ物を書いた: {records.Count} 件（合成 {syntheticCount} 件 ＋ 実データ {records.Count - syntheticCount} 件）"
                          + $" / {sw.Elapsed.TotalSeconds:F1} 秒 → {outPath}");
        Console.WriteLine($"  実データの内訳: session_ticks {corpus.TicksCount} セグメント / session_hit_windows {corpus.WindowCount} 窓");
        Console.WriteLine("  ★中身の照合は Python 側（parity/verify_roundtrip.py）が行う。");
        return 0;
    }

    private static RoundTripPack.Record ReEncode(
        string kind, Corpus.Item item, List<string> fields, TickArchive.TickColumns cols,
        TickEncoder.SectionPolicy policy, int recordVersion, bool compress, bool omitFields)
    {
        var map = new Dictionary<string, uint[]>(StringComparer.Ordinal);
        foreach (var kv in cols) map[kv.Key] = kv.Value;
        var enc = TickEncoder.EncodeColumns(map, fields, policy, recordVersion);
        var sections = TickArchive.ParseFieldOrder(enc.FieldOrder.ToCompactJson(escapeNonAscii: false)).Sections;
        var plain = omitFields
            ? enc.FieldOrder.WithMember("fields", JsonNodeLite.FromStrings(TickArchive.FieldsFromSections(sections)))
            : enc.FieldOrder;
        var (packed, packedEncoding) = TickArchive.PackFieldOrder(enc.FieldOrder, sections, compress, omitFields);
        return new RoundTripPack.Record
        {
            Kind = kind,
            SessionId = item.SessionId,
            No = item.No,
            Label = "",
            FieldOrderUtf8 = Encoding.UTF8.GetBytes(plain.ToCompactJson(escapeNonAscii: false)),
            FieldOrderEncoding = packedEncoding,
            FieldOrderPacked = packed,
            Blob = enc.Blob,
            Expected = null,
        };
    }


    private static int RunSizes(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "corpus", out var corpusPath) || !Require(opt, "out", out var outPath)) return 1;
        var corpus = Corpus.Load(corpusPath);
        using var db = new Layer0Reader(corpus.DbPath);
        using var w = new StreamWriter(outPath, false, new UTF8Encoding(false));
        w.WriteLine("# kind\tsession_id\tno\tpython_blob_bytes\tcsharp_blob_bytes");
        w.WriteLine("# ★これは参考値。★圧縮バイト列の一致は合格条件ではない（復号した中身が一致すればよい）。");
        long py = 0, cs = 0;
        int n = 0;
        var policy = opt.TryGetValue("hints", out var h) && h is not null
            ? TickEncoder.SectionPolicy.Load(h) : TickEncoder.SectionPolicy.Empty;
        foreach (var item in corpus.Items)
        {
            int before, after;
            if (item.Kind == Corpus.Kind.Ticks)
            {
                var seg = db.ReadSegment(item.SessionId, item.No)!;
                var order = TickArchive.ParseFieldOrder(seg.FieldOrderText);
                var cols = TickArchive.Decode(seg.Blob, seg.Encoding, order);
                var map = new Dictionary<string, uint[]>(StringComparer.Ordinal);
                foreach (var kv in cols) map[kv.Key] = kv.Value;
                before = seg.Blob.Length;
                after = TickEncoder.EncodeColumns(map, order.Fields, policy, seg.RecordVersion).Blob.Length;
                w.WriteLine($"T\t{item.SessionId}\t{item.No}\t{before}\t{after}");
            }
            else
            {
                var win = db.ReadWindow(item.SessionId, item.No)!;
                var order = TickArchive.UnpackFieldOrder(win.FieldOrderBlob, win.FieldOrderEncoding);
                var cols = TickArchive.Decode(win.Blob, win.Encoding, order);
                var map = new Dictionary<string, uint[]>(StringComparer.Ordinal);
                foreach (var kv in cols) map[kv.Key] = kv.Value;
                before = win.Blob.Length;
                after = TickEncoder.EncodeColumns(map, order.Fields, policy, order.RecordVersion).Blob.Length;
                w.WriteLine($"W\t{item.SessionId}\t{item.No}\t{before}\t{after}");
            }
            py += before; cs += after; n++;
        }
        w.WriteLine($"# totals records={n} python={py} csharp={cs}");
        Console.WriteLine($"圧縮後の大きさ（参考）: {n} 件で Python {py:N0} B → C# {cs:N0} B"
                          + (py > 0 ? $"（{(cs - py) * 100.0 / py:+0.00;-0.00}%）" : ""));
        return 0;
    }


    private static bool Require(Dictionary<string, string?> opt, string key, [NotNullWhen(true)] out string? value)
    {
        opt.TryGetValue(key, out value);
        if (!string.IsNullOrEmpty(value)) return true;
        Console.Error.WriteLine($"--{key} が要ります。");
        return false;
    }

    private static bool RequireInt(Dictionary<string, string?> opt, string key, out int value, int min)
    {
        value = 0;
        if (!opt.TryGetValue(key, out var raw) || raw is null)
        {
            Console.Error.WriteLine($"--{key} が要ります。");
            return false;
        }
        if (!int.TryParse(raw, out value) || value < min)
        {
            Console.Error.WriteLine($"--{key} は {min} 以上の整数で指定してください");
            return false;
        }
        return true;
    }

    private static bool OptionalInt(Dictionary<string, string?> opt, string key, int fallback,
                                    out int value, int min)
    {
        value = fallback;
        if (!opt.TryGetValue(key, out var raw) || raw is null) return true;
        if (!int.TryParse(raw, out value) || value < min)
        {
            Console.Error.WriteLine($"--{key} は {min} 以上の整数で指定してください");
            return false;
        }
        return true;
    }

    private static Dictionary<string, string?> ParseArgs(string[] args)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;
            var key = args[i][2..];
            string? value = (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                ? args[++i] : null;
            map[key] = value;
        }
        return map;
    }
}
