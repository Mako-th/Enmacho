using System.Buffers.Binary;
using System.Text;
using TH09.Generated;

namespace TH09.Replay;

public static class SelfTest
{
    private static int _failures;
    private static int _skips;

    private static void Check(bool cond, string msg)
    {
        Console.WriteLine((cond ? "  ok   " : "  FAIL ") + msg);
        if (!cond) _failures++;
    }

    private static void Skip(string msg)
    {
        Console.WriteLine("  SKIP " + msg);
        _skips++;
    }

    private static void Head(string title) => Console.WriteLine("\n=== " + title + " ===");

    public static int Run(string? realReplay)
    {
        _failures = 0;
        _skips = 0;
        SuiteConstants();
        SuiteDecrypt06();
        SuiteUnlzssEdges();
        SuiteCp932();
        SuiteCp932ExtraTable();
        SuiteStrip();
        SuiteSynthetic();
        SuiteNegativeLegacyNoLzss(realReplay);
        SuiteNegativeOneByte(realReplay);
        SuiteComparator();

        Console.WriteLine();
        if (_failures > 0)
        {
            Console.WriteLine($"FAILED: {_failures} 件");
            return 1;
        }
        Console.WriteLine(_skips > 0 ? $"PASS（ただし SKIP {_skips} 件）" : "すべて PASS");
        return 0;
    }

    private static void SuiteConstants()
    {
        Head("構造定数の整合（Python 側 test_replay_decode_pure.suite_constants と同じ関係を見る）");
        Check(ReplayDecode.HeaderOff == 0xC0,
            $"リプレイヘッダは 0xC0（生ヘッダ24 + 復号済み前置き168）: 0x{ReplayDecode.HeaderOff:X}");
        Check(ReplayDecode.FileHdrStagePtrs + 4 * ReplayDecode.StagePtrCount * 2 == ReplayDecode.HeaderOff,
            "stage_offsets[20] + unknown_offsets[20] でちょうど 0xC0 に届く");
        Check(ReplayDecode.StageOffPair == ReplayDecode.StageOffRngSeed && ReplayDecode.StageOffRngSeed == 0x04,
            "pair と rng_seed は同じ +0x04 を指す");
        Check(ReplayDecode.StageOffLives + 1 < ReplayDecode.StageBodySize && ReplayDecode.StageBodySize <= ReplayDecode.StageOffFieldId,
            $"Stage の読み取り長 0x{ReplayDecode.StageBodySize:X} は lives を含み、field_id(+0x0C) には掛からない");
        Check((1 << ReplayDecode.LzssIndexSize) == 8192 && ReplayDecode.LzssLengthSize == 4
              && ReplayDecode.LzssMinLength == 3 && ReplayDecode.LzssInitialWriteIndex == 1,
            "ZUN_LZSS_PARAMS が tsadecode.cpp L52 と同じ {13,4,3,1}");
        Check(ReplayDecode.HeaderOffName == 206 && ReplayDecode.HeaderOffDate == 196 && ReplayDecode.HeaderOffDiff == 215,
            $"名前は展開後バッファの +14（絶対 {ReplayDecode.HeaderOffName}）/ 日付 +4 / 難易度 +23");
    }

    private static void SuiteDecrypt06()
    {
        Head("decrypt06（tsadecode.cpp L8-14 の移植）");
        var plain = new byte[256 * 3];
        for (int i = 0; i < plain.Length; i++) plain[i] = (byte)(i & 0xFF);
        byte k0 = 0x9B;
        var enc = (byte[])plain.Clone();
        byte k = k0;
        for (int i = 0; i < enc.Length; i++) { enc[i] = (byte)(enc[i] + k); k = (byte)(k + 7); }
        ReplayDecode.Decrypt06(enc, k0);
        Check(enc.AsSpan().SequenceEqual(plain), "自前で暗号化した列を元に戻せる（鍵は 256 で回る）");

        var part = Encoding.ASCII.GetBytes("ABCDEF");
        ReplayDecode.Decrypt06(part, 1, start: 2);
        Check(part[0] == (byte)'A' && part[1] == (byte)'B' && part[2] != (byte)'C',
            $"start より前は触らない（実際 {Encoding.Latin1.GetString(part)}）");
    }

    private static readonly (string Label, byte[] Data)[] LzssCases =
    [
        ("空", []),
        ("終端のみ", [0x00, 0x00]),
        ("0埋め8バイト", [0, 0, 0, 0, 0, 0, 0, 0]),
        ("literal だけ", [.. Enumerable.Repeat((byte)0xFF, 16)]),
        ("乱雑", [.. Enumerable.Range(0, 200).Select(i => (byte)i)]),
        ("1バイト", [0x80]),
    ];

    private static void SuiteUnlzssEdges()
    {
        Head("unlzss の端（壊れた入力・境界）");
        var mustFail = new HashSet<string> { "0埋め8バイト" };
        foreach (var (label, data) in LzssCases)
        {
            string shown;
            bool threw = false;
            try { shown = ReplayDecode.Unlzss(data).Length + " バイト展開"; }
            catch (InvalidDataException) { shown = "例外:InvalidDataException"; threw = true; }
            if (mustFail.Contains(label))
                Check(threw, $"{label} は例外（末尾のビットを使い切れない。実際 {shown}）");
            else
                Console.WriteLine($"      {label}: {shown}");
        }
    }

    private static void SuiteCp932()
    {
        Head("cp932（InvariantGlobalization と両立するか）");
        var (ok, decoded) = ReplayDecode.SelfCheckCp932();
        Check(ok, $"0x93 0x8C 0x95 0xFB → 東方（実際 {decoded}）");
        Check(ReplayDecode.Text([0xB1, 0xB2, 0xB3]) == "ｱｲｳ",
            "半角カナ 0xB1 0xB2 0xB3 → ｱｲｳ（単バイト域も cp932 として読めている）");

        Check(ReplayDecode.DecodeCp932([0x81, 0x20, 0x93, 0x8C]) == "� 東",
            "★先導バイト + 不正な後続は「先導 1 バイトだけ捨てて読み直す」"
            + $"（81 20 93 8C → 実際 {Quote(ReplayDecode.DecodeCp932([0x81, 0x20, 0x93, 0x8C]))}）");

        var leads = ReplayDecode.Cp932Table().Take(256)
            .Where(l => l.EndsWith("	FFFD", StringComparison.Ordinal))
            .Select(l => Convert.ToInt32(l[..2], 16)).ToList();
        var expected = Enumerable.Range(0x81, 0x9F - 0x81 + 1).Concat(Enumerable.Range(0xE0, 0xFC - 0xE0 + 1)).ToList();
        Check(leads.SequenceEqual(expected),
            $"先導バイトは 0x81-0x9F と 0xE0-0xFC の 60 個（実際 {leads.Count} 個）");
    }

    private static readonly (byte Lead, byte Trail, string Want, string Where)[] Cp932ExtraProbes =
    [
        (0xEE, 0xE0, "髙", "0xED40-0xEEFC（NEC 選定 IBM 拡張。人名用字）"),
        (0x87, 0x90, "≒", "0x8790-0x879C（NEC 特殊文字）"),
        (0xFA, 0x4A, "Ⅰ", "0xFA4A-0xFA5B（IBM 拡張）"),
    ];

    public static string LegacyDecodeCp932WithoutTable(ReadOnlySpan<byte> raw)
    {
        var sb = new StringBuilder(raw.Length);
        for (int i = 0; i < raw.Length;)
        {
            if (LegacyIsLead(raw[i]) && i + 1 < raw.Length)
            {
                var pair = LegacyCp932.GetString(raw.Slice(i, 2));
                if (pair.Length == 1 && pair[0] != '�')
                {
                    sb.Append(pair);
                    i += 2;
                    continue;
                }
                sb.Append('�');
                i += 1;
                continue;
            }
            sb.Append(LegacyCp932.GetString(raw.Slice(i, 1)));
            i += 1;
        }
        return sb.ToString();
    }

    private static readonly Encoding LegacyCp932 = MakeLegacyCp932();

    private static Encoding MakeLegacyCp932()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(932, EncoderFallback.ReplacementFallback,
                                    new DecoderReplacementFallback("�"));
    }

    private static bool LegacyIsLead(byte b) =>
        LegacyCp932.GetString([b]) is { Length: 1 } s && s[0] == '�';

    private static int CountOneCharPairs(Func<byte[], string> decode)
    {
        int n = 0;
        for (int lead = 0; lead < 256; lead++)
        {
            if (!LegacyIsLead((byte)lead)) continue;
            for (int trail = 0; trail < 256; trail++)
            {
                var s = decode([(byte)lead, (byte)trail]);
                if (s.Length == 1 && s[0] != '�') n++;
            }
        }
        return n;
    }

    private static void SuiteCp932ExtraTable()
    {
        Head("★cp932 の写像表（.NET の CP932 表に無いセルを Python から埋める）");

        foreach (var (lead, trail, want, where) in Cp932ExtraProbes)
        {
            var got = ReplayDecode.DecodeCp932([lead, trail]);
            Check(got == want, $"{lead:X2} {trail:X2} → {want}（実際 {Quote(got)}）／ {where}");
        }

        foreach (var (lead, trail, want, _) in Cp932ExtraProbes)
        {
            var old = LegacyDecodeCp932WithoutTable([lead, trail]);
            Check(old != want && old.Length >= 1 && old[0] == '�',
                $"★表を引かない写しでは {lead:X2} {trail:X2} が {want} にならない（実際 {Quote(old)}）");
        }

        int pairs = 60 * 256;
        int now = CountOneCharPairs(b => ReplayDecode.DecodeCp932(b));
        int old2 = CountOneCharPairs(b => LegacyDecodeCp932WithoutTable(b));
        Check(now == 9604,
            $"いまの実装は {pairs} 組のうち {now} 組が 1 文字になる（Python の cp932 と同じ 9,604 を期待）");
        Check(old2 == 9206,
            $"★表を引かない写しでは {pairs} 組のうち {old2} 組しか 1 文字にならない（9,206 を期待）");
        Check(now - old2 == 398,
            $"★差はちょうど 398 組（実際 {now - old2}）——cp932 の写像表を足す前後で数えた差と同じ");
        Check(Cp932Extra.Count == 405 && Cp932Extra.RangeCellCount == 476,
            $"写像表は 3 範囲 {Cp932Extra.RangeCellCount} セルのうち {Cp932Extra.Count} 件"
            + "（★405 のうち 398 件が .NET と食い違っていたもの / 残り 7 件は元から一致していて同じ字を上書きする）");

        Check(!Cp932Extra.TryGet((0x93 << 8) | 0x8C, out _),
            "★写像表は 3 範囲の外を持たない（0x93 0x8C =「東」は .NET の表から出る）");
    }

    private static void SuiteStrip()
    {
        Head("Python の str.strip() との一致（★string.Trim() では足りない）");
        const string s = "\u001cMako\u001c";
        Check(ReplayDecode.PyStrip(s) == "Mako", $"U+001C を削る（実際 {Quote(ReplayDecode.PyStrip(s))}）");
        Check(s.Trim() != "Mako",
            $"string.Trim() は U+001C を削らない ＝ PyStrip が要る（実際 {Quote(s.Trim())}）");
        Check(ReplayDecode.PyStrip(" \t\r\n　A　\n ") == "A", "全角スペース・タブ・改行はどちらでも削る");
    }


    public static byte[] LzssEncodeLiterals(ReadOnlySpan<byte> body)
    {
        if (body.Length % 8 != 2)
            throw new ArgumentException($"literal 専用の符号化はバイト数 ≡ 2 (mod 8) でないと閉じない: {body.Length}");
        var bits = new List<byte>();
        int acc = 0, nacc = 0;
        void Put(int value, int width)
        {
            for (int i = width - 1; i >= 0; i--)
            {
                acc = (acc << 1) | ((value >> i) & 1);
                if (++nacc == 8) { bits.Add((byte)acc); acc = 0; nacc = 0; }
            }
        }
        foreach (var b in body) { Put(1, 1); Put(b, 8); }
        Put(0, 1);
        Put(0, ReplayDecode.LzssIndexSize);
        if (nacc != 0) throw new InvalidOperationException("ビット境界で閉じなかった（長さの条件が誤っている）");
        return [.. bits];
    }

    public static byte[] BuildSyntheticRpy(byte[] date, byte[] name, byte difficulty,
                                           IReadOnlyList<(int Index, byte[] Bytes)> stages, byte key = 0x5A)
    {
        var body = new List<byte>();
        body.AddRange(new byte[4]);
        body.AddRange(Fixed(date, ReplayDecode.HeaderLenDate));
        body.AddRange(Fixed(name, ReplayDecode.HeaderLenName));
        body.Add(difficulty);
        var offsets = new uint[ReplayDecode.StagePtrCount];
        foreach (var (index, bytes) in stages)
        {
            offsets[index] = (uint)(ReplayDecode.HeaderOff + body.Count);
            body.AddRange(bytes);
        }
        while (body.Count % 8 != 2) body.Add(0x00);

        var prefix = new byte[ReplayDecode.DecryptedPrefixSize];
        for (int i = 0; i < offsets.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(
                prefix.AsSpan(ReplayDecode.FileHdrStagePtrs - ReplayDecode.HeaderSize + i * 4, 4), offsets[i]);
        }

        var lz = LzssEncodeLiterals([.. body]);
        var enc = new byte[prefix.Length + lz.Length];
        prefix.CopyTo(enc, 0);
        lz.CopyTo(enc, prefix.Length);

        byte k = key;
        for (int i = 0; i < enc.Length; i++) { enc[i] = (byte)(enc[i] + k); k = (byte)(k + 7); }

        var raw = new byte[ReplayDecode.HeaderSize + enc.Length];
        "T9RP"u8.CopyTo(raw);
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(12, 4), (uint)(ReplayDecode.HeaderSize * 2 + enc.Length));
        raw[21] = key;
        enc.CopyTo(raw, ReplayDecode.HeaderSize);
        return raw;
    }

    private static byte[] Fixed(byte[] src, int size)
    {
        var b = new byte[size];
        src.AsSpan(0, Math.Min(src.Length, size)).CopyTo(b);
        return b;
    }

    public static byte[] StageBytes(uint score, ushort pair, byte shot, bool ai, byte lives, uint fieldId)
    {
        var b = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(ReplayDecode.StageOffScore, 4), score);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(ReplayDecode.StageOffRngSeed, 2), pair);
        b[ReplayDecode.StageOffShot] = shot;
        b[ReplayDecode.StageOffAi] = (byte)(ai ? 0x01 : 0xFE);
        b[ReplayDecode.StageOffLives] = lives;
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(ReplayDecode.StageOffFieldId, 4), fieldId);
        return b;
    }

    private static void SuiteSynthetic()
    {
        Head("合成リプレイを最後まで通す（復号 → LZSS 展開 → 名前 → stage）");
        using var tmp = new TempDir();
        var rpy = Path.Combine(tmp.Path, "synth_story.rpy");
        File.WriteAllBytes(rpy, BuildSyntheticRpy(
            Encoding.ASCII.GetBytes("26/08/25"),
            Encoding.ASCII.GetBytes("Mako"),
            difficulty: 3,
            stages: [(0, StageBytes(0x11223344, 0xBEEF, 13, ai: true, lives: 2, fieldId: 5))]));

        var r = ReplayDecode.DecodeReplay(rpy);
        Check(r.Status == "decoded", $"status = decoded（実際 {r.Status} / {r.Error}）");
        Check(r.Name == "Mako" && r.Date == "26/08/25" && r.Difficulty == 3,
            $"名前・日付・難易度（実際 {Quote(r.Name)} / {Quote(r.Date)} / {r.Difficulty}）");
        Check(r.Mode == 0 && r.P1Char == 13 && r.P2Char is null, $"mode/p1_char/p2_char（実際 {r.Mode}/{r.P1Char}/{r.P2Char}）");
        Check(r.Stages.Count == 1, $"stage 1 面（実際 {r.Stages.Count}）");
        if (r.Stages.Count == 1)
        {
            var s = r.Stages[0];
            Check(s.Score == 0x11223344L * 10, $"score は内部値 ×10（実際 {s.Score}）");
            Check(s.Pair == 0xBEEF && s.RngSeed == 0xBEEF, $"pair と rng_seed は同じ 2 バイト（実際 {s.Pair}/{s.RngSeed}）");
            Check(s.Shot == 13, $"shot は +1 しない（実際 {s.Shot}）");
            Check(s.Ai, "ai は +0x07 の bit0");
            Check(s.Lives == 2, $"lives（実際 {s.Lives}）");
            Check(s.Opponent is null, $"相方の面（index 10）が無いので opponent は null（実際 {s.Opponent}）");
            Check(s.FieldId == 5, $"field_id は生値のまま残る（実際 {s.FieldId}）");
        }
        var rpy2 = Path.Combine(tmp.Path, "synth_ai0.rpy");
        File.WriteAllBytes(rpy2, BuildSyntheticRpy(
            Encoding.ASCII.GetBytes("26/08/25"), Encoding.ASCII.GetBytes("Mako"), 4,
            [(0, StageBytes(1, 2, 3, ai: false, lives: 1, fieldId: 15))]));
        var r2 = ReplayDecode.DecodeReplay(rpy2);
        Check(r2.Stages.Count == 1 && !r2.Stages[0].Ai, "0xFE でも ai は false（bit0 以外は見ない）");
        Check(r2.Stages.Count == 1 && r2.Stages[0].Opponent is null,
            $"ここも相方が無いので opponent は null（実際 {(r2.Stages.Count > 0 ? r2.Stages[0].Opponent : null)}）");
        Check(r2.Stages.Count == 1 && r2.Stages[0].FieldId == 15, "field_id は 15 のまま（16 キャラ環で巡回しない）");
        Check(r2.Mode == 1, $"difficulty=4（Extra）は mode=1（実際 {r2.Mode}）");

        var rpy2b = Path.Combine(tmp.Path, "synth_partner.rpy");
        File.WriteAllBytes(rpy2b, BuildSyntheticRpy(
            Encoding.ASCII.GetBytes("26/08/25"), Encoding.ASCII.GetBytes("Mako"), 0,
            [(0, StageBytes(1, 2, shot: 2, ai: false, lives: 2, fieldId: 99)),
             (10, StageBytes(1, 2, shot: 9, ai: true, lives: 2, fieldId: 1))]));
        var r2b = ReplayDecode.DecodeReplay(rpy2b);
        Check(r2b.Stages.Count == 2, $"stage 2 面（実際 {r2b.Stages.Count}）");
        var s0 = r2b.Stages.Find(x => x.Index == 0);
        var s10 = r2b.Stages.Find(x => x.Index == 10);
        Check(s0 is not null && s0.Opponent == 9,
            $"index 0 の opponent は相方（index 10）の shot=9（実際 {s0?.Opponent}）");
        Check(s10 is not null && s10.Opponent == 2,
            $"index 10 の opponent は相方（index 0）の shot=2（実際 {s10?.Opponent}）");
        Check(s0 is not null && s0.FieldId == 99 && s10 is not null && s10.FieldId == 1,
            $"field_id は入れ違いのまま残り opponent に影響しない（実際 {s0?.FieldId}/{s10?.FieldId}）");

        var vsDir = Path.Combine(tmp.Path, "Dagoth2hu vs mako");
        Directory.CreateDirectory(vsDir);
        var rpy3 = Path.Combine(vsDir, "synth_match.rpy");
        File.WriteAllBytes(rpy3, BuildSyntheticRpy(
            Encoding.ASCII.GetBytes("26/08/25"), Encoding.ASCII.GetBytes("12:34:56"), 3,
            [(9, StageBytes(100, 7, 4, false, 2, 1)), (19, StageBytes(200, 7, 11, true, 2, 1))]));
        var r3 = ReplayDecode.DecodeReplay(rpy3);
        Check(r3.Mode == 2 && r3.P1Char == 4 && r3.P2Char == 11,
            $"index 9/19 があれば mode=2、p1/p2 は各 shot（実際 {r3.Mode}/{r3.P1Char}/{r3.P2Char}）");
        Check(r3.P1Name == "Dagoth2hu" && r3.P2Name == "mako",
            $"親フォルダ A vs B を割る（実際 {Quote(r3.P1Name)} / {Quote(r3.P2Name)}）");
        var m9 = r3.Stages.Find(x => x.Index == 9);
        var m19 = r3.Stages.Find(x => x.Index == 19);
        Check(m9 is not null && m9.Opponent == 11, $"index 9 の opponent は index 19 の shot=11（実際 {m9?.Opponent}）");
        Check(m19 is not null && m19.Opponent == 4, $"index 19 の opponent は index 9 の shot=4（実際 {m19?.Opponent}）");
    }

    public static byte[] LegacyDecodeBytesWithoutLzss(byte[] raw)
    {
        if (raw.Length < ReplayDecode.HeaderSize) throw new InvalidDataException("短すぎます");
        long stored = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(12, 4));
        long end = stored - ReplayDecode.HeaderSize;
        byte key = raw[21];
        if (end <= ReplayDecode.HeaderSize || end > raw.Length) throw new InvalidDataException("ヘッダのサイズ値が不正");
        var enc = raw.AsSpan(ReplayDecode.HeaderSize, (int)(end - ReplayDecode.HeaderSize)).ToArray();
        ReplayDecode.Decrypt06(enc, key);
        var outBuf = new byte[ReplayDecode.HeaderSize + enc.Length];
        raw.AsSpan(0, ReplayDecode.HeaderSize).CopyTo(outBuf);
        enc.CopyTo(outBuf.AsSpan(ReplayDecode.HeaderSize));
        return outBuf;
    }

    private static void SuiteNegativeLegacyNoLzss(string? realReplay)
    {
        Head("★否定テスト: LZSS 展開を飛ばした写しでは、突き合わせが落ちる");
        using var tmp = new TempDir();
        var rpy = Path.Combine(tmp.Path, "synth.rpy");
        File.WriteAllBytes(rpy, BuildSyntheticRpy(
            Encoding.ASCII.GetBytes("26/08/25"), Encoding.ASCII.GetBytes("Mako"), 3,
            [(0, StageBytes(1234, 5, 6, false, 3, 2))]));

        var good = ReplayDecode.DecodeReplay(rpy);
        var bad = ReplayDecode.DecodeReplayWith(rpy, LegacyDecodeBytesWithoutLzss);
        Check(good.Status == "decoded", "正しい実装では decoded");
        var rep = ParityCompare.CompareLines([RecordLine(rpy, good)], [RecordLine(rpy, bad)]);
        Check(!rep.Ok, $"旧実装（unlzss なし）は不一致として検出される（一致 {rep.Matched}/{rep.Total}）");
        Console.WriteLine($"      旧実装の結果: status={bad.Status} name={Quote(bad.Name)}");

        if (realReplay is null || !File.Exists(realReplay))
        {
            Skip("実物の .rpy が渡されていないので、実ファイルでの旧実装比較は行わない（--file で渡せる）");
            return;
        }
        var g2 = ReplayDecode.DecodeReplay(realReplay);
        var b2 = ReplayDecode.DecodeReplayWith(realReplay, LegacyDecodeBytesWithoutLzss);
        var rep2 = ParityCompare.CompareLines([RecordLine(realReplay, g2)], [RecordLine(realReplay, b2)]);
        Check(g2.Status == "decoded", $"実物が decoded（{Path.GetFileName(realReplay)} / 名前 {Quote(g2.Name)}）");
        Check(!rep2.Ok, "実物でも旧実装（unlzss なし）は不一致として検出される");
    }

    private static void SuiteNegativeOneByte(string? realReplay)
    {
        Head("★否定テスト: 1 バイト変えた .rpy を、突き合わせが検出する");
        using var tmp = new TempDir();
        var orig = Path.Combine(tmp.Path, "orig.rpy");
        var raw = BuildSyntheticRpy(Encoding.ASCII.GetBytes("26/08/25"), Encoding.ASCII.GetBytes("Mako"), 3,
            [(0, StageBytes(1234, 5, 6, false, 3, 2))]);
        File.WriteAllBytes(orig, raw);

        var mutated = (byte[])raw.Clone();
        int nameByte = FindNameByteInRaw(raw);
        mutated[nameByte] = (byte)(mutated[nameByte] ^ 0x01);
        var mut = Path.Combine(tmp.Path, "mutated.rpy");
        File.WriteAllBytes(mut, mutated);

        var a = ReplayDecode.DecodeReplay(orig);
        var b = ReplayDecode.DecodeReplay(mut);
        var rep = ParityCompare.CompareLines([RecordLine(orig, a)], [RecordLine(orig, b)]);
        Check(!rep.Ok, $"1 バイト違いを不一致として検出（一致 {rep.Matched}/{rep.Total}）");
        Console.WriteLine($"      元 name={Quote(a.Name)} / 1バイト変更後 status={b.Status} name={Quote(b.Name)}");

        if (realReplay is null || !File.Exists(realReplay))
        {
            Skip("実物の .rpy が渡されていないので、実ファイルの 1 バイト改変は行わない（--file で渡せる）");
            return;
        }
        var realRaw = File.ReadAllBytes(realReplay);
        var realMut = (byte[])realRaw.Clone();
        realMut[^1] = (byte)(realMut[^1] ^ 0xFF);
        var mut2 = Path.Combine(tmp.Path, "real_mutated.rpy");
        File.WriteAllBytes(mut2, realMut);
        var ra = ReplayDecode.DecodeReplay(realReplay);
        var rb = ReplayDecode.DecodeReplay(mut2);
        var rep2 = ParityCompare.CompareLines([RecordLine(realReplay, ra)], [RecordLine(realReplay, rb)]);
        Check(!rep2.Ok, $"実物でも 1 バイト違いを検出（変更後 status={rb.Status}）");
    }

    private static int FindNameByteInRaw(byte[] raw)
    {
        int bodyIndex = ReplayDecode.HeaderOffName - ReplayDecode.HeaderOff;
        int bitPos = bodyIndex * 9 + 1;
        return ReplayDecode.HeaderSize + ReplayDecode.DecryptedPrefixSize + bitPos / 8;
    }

    private static void SuiteComparator()
    {
        Head("★突き合わせそのものの検査（甘くなっていないか）");
        string a = "{\"path\":\"X\",\"sha256\":\"aa\",\"result\":{\"status\":\"decoded\",\"stages\":[],\"name\":\"Mako\",\"mode\":0}}";
        string same = "{\"path\":\"X\",\"result\":{\"mode\":0,\"name\":\"Mako\",\"stages\":[],\"status\":\"decoded\"},\"sha256\":\"aa\"}";
        Check(ParityCompare.CompareLines([a], [same]).Ok, "鍵の並び順が違うだけなら一致とみなす");

        string valDiff = a.Replace("\"mode\":0", "\"mode\":1");
        Check(!ParityCompare.CompareLines([a], [valDiff]).Ok, "値が 1 つ違えば落ちる（mode 0 → 1）");
        string strDiff = a.Replace("Mako", "mako");
        Check(!ParityCompare.CompareLines([a], [strDiff]).Ok, "文字列の大小が違えば落ちる（Mako → mako）");
        string keyMissing = a.Replace(",\"mode\":0", "");
        Check(!ParityCompare.CompareLines([a], [keyMissing]).Ok, "鍵が 1 つ足りなければ落ちる");
        string shaDiff = a.Replace("\"sha256\":\"aa\"", "\"sha256\":\"bb\"");
        Check(!ParityCompare.CompareLines([a], [shaDiff]).Ok, "同じ path で中身のハッシュが違えば落ちる");
        string floatish = a.Replace("\"mode\":0", "\"mode\":0.0");
        Check(!ParityCompare.CompareLines([a], [floatish]).Ok, "0 と 0.0 を別物として落とす（数値は生テキストで比べる）");
        string extra = "{\"path\":\"Y\",\"sha256\":null,\"result\":{\"status\":\"decode_failed\",\"stages\":[],\"error\":\"x\"}}";
        Check(!ParityCompare.CompareLines([a], [a, extra]).Ok, "片方にしか無い path があれば落ちる");

        string errPy = "{\"path\":\"Z\",\"sha256\":null,\"result\":{\"status\":\"decode_failed\",\"stages\":[],\"error\":\"ValueError: ...\"}}";
        string errCs = "{\"path\":\"Z\",\"sha256\":null,\"result\":{\"status\":\"decode_failed\",\"stages\":[],\"error\":\"InvalidDataException: ...\"}}";
        Check(ParityCompare.CompareLines([errPy], [errCs]).Ok, "error の文言だけは実装依存なので値を見ない");
        string errMissing = errCs.Replace(",\"error\":\"InvalidDataException: ...\"", "");
        Check(!ParityCompare.CompareLines([errPy], [errMissing]).Ok, "★ただし error の鍵が無ければ落ちる（値を無視＝鍵も無視、にしない）");
        string statusDiff = errCs.Replace("decode_failed", "decoded");
        Check(!ParityCompare.CompareLines([errPy], [statusDiff]).Ok, "status は必ず見る（失敗の集合がずれたら落ちる）");

        var empty = ParityCompare.CompareLines([], []);
        Check(!empty.Ok, $"リプレイ 0 件では合格にしない（Total={empty.Total}）");
    }

    private static string RecordLine(string path, ReplayResult r)
    {
        using var ms = new MemoryStream();
        using (var w = new System.Text.Json.Utf8JsonWriter(ms))
        {
            ParityJson.WriteRecord(w, path, ParityJson.Sha256Hex(path), r);
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string Quote(string? s) => s is null ? "null" : "\"" + s + "\"";

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("th09_replay_selftest_").FullName;

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
