using System.Text;

namespace TH09.Layer0;

public static class SelfTest
{
    private static int _pass;
    private static readonly List<string> Failures = [];

    public static int Run()
    {
        _pass = 0;
        Failures.Clear();

        SyntheticRoundTripInsideCSharp();
        NegativeOneBitInOneWord();
        NegativeFieldOrderKeyOrderIsNotADifference();
        NegativeFieldOrderValueIsADifference();
        NegativeLegacyIgnoresValuesTransform();
        NegativeLegacyLosesInvariantNames();
        NegativeLegacyDoesNotSkipUnknownSection();
        OnlyEmptyMeansNothing();
        NegativeCompareRejectsEmptyAndTampered();
        NumbersThatAreNotIntegersAreRejected();
        NegativeEmptyChunkMustStillBeZlib();
        SolidBrotliV2Crc();

        Console.WriteLine();
        Console.WriteLine($"自己検査: {_pass} 件が通り、{Failures.Count} 件が落ちた");
        foreach (var f in Failures) Console.WriteLine("  NG: " + f);
        return Failures.Count == 0 ? 0 : 1;
    }

    private static void Check(bool ok, string what)
    {
        if (ok) { _pass++; Console.WriteLine("  ok   " + what); }
        else { Failures.Add(what); Console.WriteLine("  NG   " + what); }
    }


    private static void SyntheticRoundTripInsideCSharp()
    {
        Console.WriteLine("[1] 合成データを C# で符号化 → C# で復号");
        long words = 0;
        int n = 0;
        foreach (var c in Synthetic.All())
        {
            var rec = Synthetic.BuildRecord(c, n++);
            var order = TickArchive.ParseFieldOrder(Encoding.UTF8.GetString(rec.FieldOrderUtf8));
            var cols = TickArchive.DecodeColumns(rec.Blob, order);
            words += cols.WordCount;
            Check(SameColumns(cols, c.Columns), "往復: " + c.Label);

            if (rec.FieldOrderEncoding.Length > 0)
            {
                var reopened = TickArchive.UnpackFieldOrder(rec.FieldOrderPacked, rec.FieldOrderEncoding);
                Check(reopened.Raw.ToCanonicalJson() == order.Raw.ToCanonicalJson(),
                      $"包み直し（{rec.FieldOrderEncoding}）: " + c.Label);
            }
        }
        Check(n > 0 && words > 0, $"合成の母数（{n} 件 / 全部で {words} 語）★0 なら測れていない");
    }


    private static void NegativeOneBitInOneWord()
    {
        Console.WriteLine("[2] 否定: 1 語だけ 1 ビット変えた BLOB");
        var (columns, fields) = SampleColumns();
        var good = TickEncoder.EncodeColumns(columns, fields, TickEncoder.SectionPolicy.Empty, 13);
        var goodCols = TickArchive.DecodeColumns(good.Blob, Order(good));
        var goodDigest = Parity.ColumnDigest(goodCols);

        var tampered = new Dictionary<string, uint[]>(columns, StringComparer.Ordinal);
        var col = (uint[])columns["stream_a"].Clone();
        col[col.Length / 2] ^= 1u;
        tampered["stream_a"] = col;
        var bad = TickEncoder.EncodeColumns(tampered, fields, TickEncoder.SectionPolicy.Empty, 13);
        var badCols = TickArchive.DecodeColumns(bad.Blob, Order(bad));
        var badDigest = Parity.ColumnDigest(badCols);

        Check(goodDigest != badDigest, "1 語 1 ビットの差で列のフィンガープリントが変わる");

        var rep = Parity.Compare(
            [new Parity.Row("T", 1, 0, 300, goodCols.Count, goodCols.WordCount, goodDigest, "x")],
            [new Parity.Row("T", 1, 0, 300, badCols.Count, badCols.WordCount, badDigest, "x")]);
        Check(!rep.Ok, "突き合わせが 1 ビットの差を NG にする");
    }

    private static void NegativeFieldOrderKeyOrderIsNotADifference()
    {
        Console.WriteLine("[3] 否定: 鍵の並びと空白の違いは不一致にしない");
        const string a = "{\"format\":\"colzlib-v1\",\"tick_count\":3,\"sections\":[{\"kind\":\"stream\",\"field\":\"x\",\"chunks\":1}]}";
        const string b = "{ \"sections\" : [ { \"chunks\" : 1 , \"field\" : \"x\" , \"kind\" : \"stream\" } ] ,\n"
                         + "  \"tick_count\" : 3 , \"format\" : \"colzlib-v1\" }";
        Check(JsonNodeLite.Parse(a).ToCanonicalJson() == JsonNodeLite.Parse(b).ToCanonicalJson(),
              "鍵の並び順と空白が違っても正規形は同じ");

        const string c = "{\"format\":\"colzlib-v1\",\"tick_count\":3,\"fields\":[\"x\",\"y\"]}";
        const string d = "{\"format\":\"colzlib-v1\",\"tick_count\":3,\"fields\":[\"y\",\"x\"]}";
        Check(JsonNodeLite.Parse(c).ToCanonicalJson() != JsonNodeLite.Parse(d).ToCanonicalJson(),
              "配列（fields / sections）の並びが違えば別物になる");
    }

    private static void NegativeFieldOrderValueIsADifference()
    {
        Console.WriteLine("[4] 否定: field_order の値が 1 つ違えば不一致");
        const string a = "{\"format\":\"colzlib-v1\",\"tick_count\":3,\"sections\":[{\"kind\":\"stream\",\"field\":\"x\",\"transform\":\"raw\",\"chunks\":1}]}";
        const string b = "{\"format\":\"colzlib-v1\",\"tick_count\":3,\"sections\":[{\"kind\":\"stream\",\"field\":\"x\",\"transform\":\"delta\",\"chunks\":1}]}";
        Check(Parity.FieldOrderDigest(JsonNodeLite.Parse(a)) != Parity.FieldOrderDigest(JsonNodeLite.Parse(b)),
              "transform が raw / delta で違えば field_order のフィンガープリントが変わる");
        const string e = "{\"format\":\"colzlib-v1\",\"tick_count\":4,\"sections\":[]}";
        const string f = "{\"format\":\"colzlib-v1\",\"tick_count\":5,\"sections\":[]}";
        Check(Parity.FieldOrderDigest(JsonNodeLite.Parse(e)) != Parity.FieldOrderDigest(JsonNodeLite.Parse(f)),
              "tick_count が違えば field_order のフィンガープリントが変わる");
    }

    private static void NegativeLegacyIgnoresValuesTransform()
    {
        Console.WriteLine("[5] 否定: values_transform を見ない旧実装なら落ちる");
        var columns = new Dictionary<string, uint[]>(StringComparer.Ordinal)
        {
            ["stair"] = Stairs(600, step: 20, delta: 1000),
        };
        var enc = TickEncoder.EncodeColumns(columns, ["stair"], Synthetic.StairPolicy("stair"), 13);
        var order = Order(enc);
        var sec = order.Sections.Single(s => s.Kind == TickArchive.SectionChanges);
        Check(sec.ValuesTransform == TickArchive.TransformDelta,
              "この標本は values_transform=delta になっている（前提の確認）");

        var now = TickArchive.DecodeColumns(enc.Blob, order);
        Check(now["stair"].SequenceEqual(columns["stair"]), "今の実装は元の値に戻す");

        var legacy = LegacyDecode(enc.Blob, order, ignoreValuesTransform: true);
        Check(!legacy["stair"].SequenceEqual(columns["stair"]), "★旧実装（values の差分を戻さない）は落ちる");
    }

    private static void NegativeLegacyLosesInvariantNames()
    {
        Console.WriteLine("[6] 否定: fields を持つ区画を拾わない作り直しなら落ちる");
        var (columns, fields) = SampleColumns();
        var enc = TickEncoder.EncodeColumns(columns, fields, TickEncoder.SectionPolicy.Empty, 13);
        var rebuilt = TickArchive.FieldsFromSections(enc.Sections).ToList();
        var legacy = enc.Sections.Where(s => s.Field is not null).Select(s => s.Field!).ToList();

        Check(rebuilt.Count == fields.Count, $"今の作り直しは {fields.Count} 列を全部拾う（拾えたのは {rebuilt.Count} 列）");
        Check(legacy.Count < rebuilt.Count, $"★旧実装（field だけ見る）は {rebuilt.Count - legacy.Count} 列を落とす");
    }

    private static void NegativeLegacyDoesNotSkipUnknownSection()
    {
        Console.WriteLine("[7] 否定: 未知の区画を読み飛ばさない旧実装なら落ちる");
        var c = Synthetic.All().Single(x => x.Label.StartsWith("未知の区画を 2 チャンク", StringComparison.Ordinal));
        var rec = Synthetic.BuildRecord(c, 0);
        var order = TickArchive.ParseFieldOrder(Encoding.UTF8.GetString(rec.FieldOrderUtf8));

        var now = TickArchive.DecodeColumns(rec.Blob, order);
        Check(SameColumns(now, c.Columns), "今の実装は未知の区画を chunks ぶん読み飛ばす");

        bool legacyBroke;
        try
        {
            var legacy = LegacyDecode(rec.Blob, order, skipUnknownChunks: false);
            legacyBroke = !SameColumns(legacy, c.Columns);
        }
        catch (Exception)
        {
            legacyBroke = true;
        }
        Check(legacyBroke, "★旧実装（未知の区画を読み飛ばさない）は落ちる");
    }

    private static void OnlyEmptyMeansNothing()
    {
        Console.WriteLine("[8] only=[] と only=null の分かれ目");
        var (columns, fields) = SampleColumns();
        var enc = TickEncoder.EncodeColumns(columns, fields, TickEncoder.SectionPolicy.Empty, 13);
        var order = Order(enc);
        Check(TickArchive.DecodeColumns(enc.Blob, order, only: null).Count == fields.Count, "only=null は全部");
        Check(TickArchive.DecodeColumns(enc.Blob, order, only: []).Count == 0, "only=[] は 1 本も返さない");
        var one = TickArchive.DecodeColumns(enc.Blob, order, only: ["stream_a"]);
        Check(one.Count == 1 && one.Has("stream_a"), "only=[1 本] はその 1 本だけ");
        bool threw = false;
        try { _ = one["stream_b"]; } catch (KeyNotFoundException) { threw = true; }
        Check(threw, "★無い語を引いたら例外（既定値を返さない）");
    }

    private static void NegativeCompareRejectsEmptyAndTampered()
    {
        Console.WriteLine("[9] 否定: 突き合わせ側の 0 件と改竄");
        Check(!Parity.Compare([], []).Ok, "★0 件は「全部一致」ではなく NG");

        var row = new Parity.Row("T", 1, 0, 300, 4, 1200, new string('a', 64), new string('b', 64));
        Check(Parity.Compare([row], [row]).Ok, "同じ記録どうしは OK");

        var zero = row with { Words = 0 };
        Check(!Parity.Compare([zero], [zero]).Ok, "★語数 0 の記録は、フィンガープリントが一致していても NG");

        var flipped = row with { ColumnDigest = new string('a', 63) + "b" };
        Check(!Parity.Compare([row], [flipped]).Ok, "★フィンガープリントが 1 文字違えば NG");

        Check(!Parity.Compare([row], []).Ok, "片方に無い記録は NG");
        Check(!Parity.Compare([], [row]).Ok, "もう片方に無い記録も NG");
    }

    private static void NumbersThatAreNotIntegersAreRejected()
    {
        Console.WriteLine("[10] 否定: field_order に小数が来たら落とす");
        bool threw = false;
        try { JsonNodeLite.Parse("{\"tick_count\":3.5}"); }
        catch (InvalidDataException) { threw = true; }
        Check(threw, "★小数はその場で落とす（黙って丸めない）");
    }

    private static void NegativeEmptyChunkMustStillBeZlib()
    {
        Console.WriteLine("[11] 否定: 空のチャンクでも zlib として正しいか");
        var legacy = LegacyEmptyZlib(TickArchive.DefaultLevel);
        Check(legacy.Length == 0, "★旧実装（ZLibStream に空入力を任せる）は 1 バイトも出さない");

        var now = TickArchive.ZlibCompress([], TickArchive.DefaultLevel);
        Check(now.Length == 8, $"今の実装は 8 バイトの zlib ストリームを出す（出たのは {now.Length} バイト）");
        Check((now[0] * 256 + now[1]) % 31 == 0, "見出しの検査（(CMF<<8|FLG) % 31 == 0）が通る");
        Check(now[^4] == 0 && now[^3] == 0 && now[^2] == 0 && now[^1] == 1,
              "末尾が空の Adler-32（00 00 00 01）になっている");
        Check(TickArchive.ZlibDecompress(now, 0, now.Length, 0).Length == 0, "解くと 0 バイトに戻る");

        var chunk = TickArchive.PackChunk([], TickArchive.DefaultLevel);
        Check(chunk.Length == 8 + 8, $"空チャンクは見出し 8 ＋ 本体 8 バイト（出たのは {chunk.Length} バイト）");
    }

    private static void SolidBrotliV2Crc()
    {
        Console.WriteLine("[12] solid-brotli-v2（検査和つき）: CRC の検算値・往復・否定");
        var check = Crc32.Compute(System.Text.Encoding.ASCII.GetBytes("123456789"));
        Check(check == 0xCBF43926u, $"CRC-32 の検算値が標準どおり（実測 0x{check:X8}）");

        var (columns, fields) = SampleColumns();
        var enc = TickEncoder.EncodeColumns(columns, fields, TickEncoder.SectionPolicy.Empty, 13);
        var v1Chunks = SolidBrotli.ChunksOf(enc.Blob);
        var (v1Blob, _) = SolidBrotli.PackVerified(enc.Blob);
        var (v2Blob, _) = SolidBrotli.PackVerifiedV2(enc.Blob);
        Check(!Throws(() => SolidBrotli.RequireSameChunks(v1Chunks, SolidBrotli.Unpack(v1Blob))),
              "v1 を展開すると、元のチャンクと 1 バイトも違わない");
        Check(!Throws(() => SolidBrotli.RequireSameChunks(v1Chunks, SolidBrotli.UnpackV2(v2Blob))),
              "v2 を展開すると、元のチャンクと 1 バイトも違わない");
        Check(v2Blob.Length == v1Blob.Length + 4,
              $"v2 は v1 より見出し 4 B ぶんだけ大きい（v1={v1Blob.Length} / v2={v2Blob.Length}）");

        var upgraded = SolidBrotli.UpgradeToV2(v1Blob);
        Check(upgraded.AsSpan().SequenceEqual(v2Blob),
              "v1 → UpgradeToV2 は、最初から v2 で書いたものと 1 バイトも違わない");

        var tamperedCrc = (byte[])v2Blob.Clone();
        tamperedCrc[4] ^= 1;
        var crcThrew = Throws(() => SolidBrotli.UnpackV2(tamperedCrc));
        Check(crcThrew, "★否定: CRC を 1 ビット変えると UnpackV2 が例外を投げる（黙って読めてしまわない）");

    }

    private static bool Throws(Action action)
    {
        try { action(); return false; }
        catch (Exception) { return true; }
    }

    private static byte[] LegacyEmptyZlib(int level)
    {
        using var ms = new MemoryStream();
        using (var z = new System.IO.Compression.ZLibStream(
                   ms, new System.IO.Compression.ZLibCompressionOptions { CompressionLevel = level },
                   leaveOpen: true))
            z.Write([], 0, 0);
        return ms.ToArray();
    }


    private static TickArchive.FieldOrder Order(TickEncoder.Encoded enc) =>
        TickArchive.ParseFieldOrder(enc.FieldOrder.ToCompactJson(escapeNonAscii: false));

    private static bool SameColumns(Dictionary<string, uint[]> got, Dictionary<string, uint[]> want)
    {
        if (got.Count != want.Count) return false;
        foreach (var kv in want)
        {
            if (!got.TryGetValue(kv.Key, out var col)) return false;
            if (!col.AsSpan().SequenceEqual(kv.Value)) return false;
        }
        return true;
    }

    private static bool SameColumns(TickArchive.TickColumns got, Dictionary<string, uint[]> want)
    {
        if (got.Count != want.Count) return false;
        foreach (var kv in want)
        {
            if (!got.Has(kv.Key)) return false;
            if (!got[kv.Key].AsSpan().SequenceEqual(kv.Value)) return false;
        }
        return true;
    }

    private static (Dictionary<string, uint[]> Columns, List<string> Fields) SampleColumns()
    {
        var cols = new Dictionary<string, uint[]>(StringComparer.Ordinal)
        {
            ["stream_a"] = Series(300, i => (uint)(i * 3 + (i % 7))),
            ["stream_b"] = Series(300, i => unchecked((uint)BitConverter.SingleToInt32Bits(i * 0.5f))),
            ["invariant_a"] = Series(300, _ => 0xCAFEBABEu),
            ["stair"] = Stairs(300, step: 20, delta: 1),
        };
        return (cols, ["stream_a", "stream_b", "invariant_a", "stair"]);
    }

    private static uint[] Series(int n, Func<int, uint> f)
    {
        var a = new uint[n];
        for (int i = 0; i < n; i++) a[i] = f(i);
        return a;
    }

    private static uint[] Stairs(int n, int step, uint delta) =>
        Series(n, i => (uint)(i / step) * delta);

    private static Dictionary<string, uint[]> LegacyDecode(
        byte[] blob, TickArchive.FieldOrder order,
        bool ignoreValuesTransform = false, bool skipUnknownChunks = true)
    {
        var outMap = new Dictionary<string, uint[]>(StringComparer.Ordinal);
        int pos = 0;
        int tickCount = order.TickCount;

        uint[] Next()
        {
            uint rawLen = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(pos));
            uint compLen = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(pos + 4));
            var raw = TickArchive.ZlibDecompress(blob, pos + 8, (int)compLen, (int)rawLen);
            pos += 8 + (int)compLen;
            var col = new uint[raw.Length / 4];
            Buffer.BlockCopy(raw, 0, col, 0, raw.Length);
            return col;
        }

        foreach (var sec in order.Sections)
        {
            switch (sec.Kind)
            {
                case TickArchive.SectionInvariant:
                {
                    var values = Next();
                    var names = sec.Fields ?? [];
                    for (int i = 0; i < names.Count && i < values.Length; i++)
                    {
                        var col = new uint[tickCount];
                        col.AsSpan().Fill(values[i]);
                        outMap[names[i]] = col;
                    }
                    break;
                }
                case TickArchive.SectionChanges:
                {
                    var frames = Next();
                    var values = Next();
                    if (sec.FramesTransform == TickArchive.TransformDelta) TickArchive.Undelta(frames);
                    if (!ignoreValuesTransform && sec.ValuesTransform == TickArchive.TransformDelta)
                        TickArchive.Undelta(values);
                    var col = new uint[tickCount];
                    for (int k = 0; k < frames.Length; k++)
                    {
                        int start = (int)frames[k];
                        int stop = k + 1 < frames.Length ? (int)frames[k + 1] : tickCount;
                        if (start < 0 || stop > tickCount || stop < start) continue;
                        col.AsSpan(start, stop - start).Fill(values[k]);
                    }
                    outMap[sec.Field!] = col;
                    break;
                }
                case TickArchive.SectionStream:
                {
                    var col = Next();
                    if (sec.Transform == TickArchive.TransformDelta) TickArchive.Undelta(col);
                    outMap[sec.Field!] = col;
                    break;
                }
                default:
                    if (skipUnknownChunks) for (int i = 0; i < sec.Chunks; i++) Next();
                    break;
            }
        }
        return outMap;
    }
}
