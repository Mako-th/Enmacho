using System.Buffers.Binary;
using System.Text;
using TH09.Generated;

namespace TH09.Replay;

public static class ReplayDecode
{
    private static ReadOnlySpan<byte> Magic => "T9RP"u8;

    public const int HeaderSize = 24;
    public const int DecryptedPrefixSize = 168;

    public const int LzssIndexSize = 13;
    public const int LzssLengthSize = 4;
    public const int LzssMinLength = 3;
    public const int LzssInitialWriteIndex = 1;
    private const int LzssHistorySize = 1 << LzssIndexSize;
    private const int LzssHistoryMask = LzssHistorySize - 1;

    public const int StageOffScore = 0x00;
    public const int StageOffRngSeed = 0x04;
    public const int StageOffPair = StageOffRngSeed;
    public const int StageOffShot = 0x06;
    public const int StageOffAi = 0x07;
    public const int StageOffLives = 0x08;
    public const int StageOffFieldId = 0x0C;
    public const int StageBodySize = 0x0A;

    public const int FileHdrStagePtrs = 0x20;
    public const int StagePtrCount = 20;

    public const int HeaderOff = HeaderSize + DecryptedPrefixSize;
    public const int HeaderOffDate = HeaderOff + 0x04;
    public const int HeaderLenDate = 10;
    public const int HeaderOffName = HeaderOffDate + HeaderLenDate;
    public const int HeaderLenName = 9;
    public const int HeaderOffDiff = HeaderOffName + HeaderLenName;

    private static readonly Encoding Cp932 = MakeCp932();

    private static Encoding MakeCp932()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(932,
            EncoderFallback.ReplacementFallback,
            new DecoderReplacementFallback("�"));
    }

    public static (bool Ok, string Decoded) SelfCheckCp932()
    {
        var s = DecodeCp932([0x93, 0x8C, 0x95, 0xFB]);
        return (s == "東方", s);
    }

    private static readonly bool[] IsLeadByte = BuildLeadTable();

    private static bool[] BuildLeadTable()
    {
        var table = new bool[256];
        Span<byte> one = stackalloc byte[1];
        for (int b = 0; b < 256; b++)
        {
            one[0] = (byte)b;
            var s = Cp932.GetString(one);
            table[b] = s.Length == 1 && s[0] == '�';
        }
        return table;
    }

    public static string DecodeCp932(ReadOnlySpan<byte> raw)
    {
        var sb = new StringBuilder(raw.Length);
        for (int i = 0; i < raw.Length;)
        {
            if (IsLeadByte[raw[i]] && i + 1 < raw.Length)
            {
                if (Cp932Extra.TryGet((raw[i] << 8) | raw[i + 1], out char mapped))
                {
                    sb.Append(mapped);
                    i += 2;
                    continue;
                }
                var pair = Cp932.GetString(raw.Slice(i, 2));
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
            sb.Append(Cp932.GetString(raw.Slice(i, 1)));
            i += 1;
        }
        return sb.ToString();
    }

    public static IEnumerable<string> Cp932Table()
    {
        for (int b = 0; b < 256; b++)
            yield return $"{b:X2}\t{Escape(DecodeCp932([(byte)b]))}";
        for (int lead = 0; lead < 256; lead++)
        {
            if (!IsLeadByte[lead]) continue;
            for (int trail = 0; trail < 256; trail++)
                yield return $"{lead:X2}{trail:X2}\t{Escape(DecodeCp932([(byte)lead, (byte)trail]))}";
        }
    }

    private static string Escape(string s) => string.Join(",", s.Select(c => ((int)c).ToString("X4")));


    public static void Decrypt06(Span<byte> buf, byte key, int start = 0)
    {
        byte k = key;
        for (int i = start; i < buf.Length; i++)
        {
            buf[i] = (byte)(buf[i] - k);
            k = (byte)(k + 7);
        }
    }

    private struct BitReader
    {
        private int _pos;
        private uint _acc;
        private int _nacc;
        public long Consumed;
        public readonly long Total;

        public BitReader(ReadOnlySpan<byte> data)
        {
            _pos = 0; _acc = 0; _nacc = 0; Consumed = 0;
            Total = (long)data.Length * 8;
        }

        public int Take(ReadOnlySpan<byte> data, int n)
        {
            while (_nacc < n && _pos < data.Length)
            {
                _acc = (_acc << 8) | data[_pos];
                _pos++;
                _nacc += 8;
            }
            if (_nacc >= n)
            {
                _nacc -= n;
                int v = (int)(_acc >> _nacc);
                _acc &= (uint)((1L << _nacc) - 1);
                Consumed += n;
                return v;
            }
            int rest = (int)(_acc << (n - _nacc));
            Consumed += _nacc;
            _acc = 0;
            _nacc = 0;
            return rest;
        }

        public readonly bool FullyConsumed => Consumed == Total;
    }

    public static byte[] Unlzss(ReadOnlySpan<byte> data)
    {
        var bits = new BitReader(data);
        var history = new byte[LzssHistorySize];
        int writeIndex = LzssInitialWriteIndex;
        var output = new GrowBuffer((int)Math.Clamp(data.Length * 4L, 64L, 1L << 24));
        while (true)
        {
            if (bits.Take(data, 1) != 0)
            {
                byte b = (byte)bits.Take(data, 8);
                output.Add(b);
                history[writeIndex] = b;
                writeIndex = (writeIndex + 1) & LzssHistoryMask;
            }
            else
            {
                int readFrom = bits.Take(data, LzssIndexSize);
                if (readFrom == 0) break;
                int count = bits.Take(data, LzssLengthSize) + LzssMinLength;
                for (int i = 0; i < count; i++)
                {
                    byte b = history[readFrom];
                    output.Add(b);
                    history[writeIndex] = b;
                    writeIndex = (writeIndex + 1) & LzssHistoryMask;
                    readFrom = (readFrom + 1) & LzssHistoryMask;
                }
            }
        }
        if (!bits.FullyConsumed)
            throw new InvalidDataException("LZSSデータが壊れています（末尾まで消費できませんでした）");
        return output.ToArray();
    }

    private struct GrowBuffer
    {
        private byte[] _buf;
        private int _len;

        public GrowBuffer(int capacity)
        {
            _buf = new byte[capacity];
            _len = 0;
        }

        public void Add(byte b)
        {
            if (_len == _buf.Length) Array.Resize(ref _buf, _buf.Length * 2);
            _buf[_len++] = b;
        }

        public readonly byte[] ToArray() => _buf.AsSpan(0, _len).ToArray();
    }

    public static byte[] DecodeBytes(ReadOnlySpan<byte> raw)
    {
        if (raw.Length < HeaderSize || !raw[..4].SequenceEqual(Magic))
            throw new InvalidDataException("TH09識別子 T9RP がありません");
        long stored = BinaryPrimitives.ReadUInt32LittleEndian(raw[12..16]);
        long end = stored - HeaderSize;
        byte key = raw[21];
        if (end <= HeaderSize || end > raw.Length)
            throw new InvalidDataException($"ヘッダのサイズ値が不正: stored={stored}, end={end}, file={raw.Length}");
        var enc = raw[HeaderSize..(int)end].ToArray();
        Decrypt06(enc, key);
        if (enc.Length < DecryptedPrefixSize)
            throw new InvalidDataException("復号後データが短すぎます");
        var body = Unlzss(enc.AsSpan(DecryptedPrefixSize));
        var outBuf = new byte[HeaderSize + DecryptedPrefixSize + body.Length];
        raw[..HeaderSize].CopyTo(outBuf);
        enc.AsSpan(0, DecryptedPrefixSize).CopyTo(outBuf.AsSpan(HeaderSize));
        body.CopyTo(outBuf.AsSpan(HeaderSize + DecryptedPrefixSize));
        return outBuf;
    }


    private static ReadOnlySpan<byte> Slice(ReadOnlySpan<byte> buf, long off, int size)
    {
        if (off < 0 || off + size > buf.Length)
            throw new InvalidDataException($"復号済みデータの範囲外を読もうとしました: off={off}, size={size}, len={buf.Length}");
        return buf.Slice((int)off, size);
    }

    public static uint[] StageOffsets(ReadOnlySpan<byte> decoded)
    {
        var raw = Slice(decoded, FileHdrStagePtrs, 4 * StagePtrCount);
        var values = new uint[StagePtrCount];
        for (int i = 0; i < StagePtrCount; i++)
            values[i] = BinaryPrimitives.ReadUInt32LittleEndian(raw.Slice(i * 4, 4));
        return values;
    }

    public static string Text(ReadOnlySpan<byte> raw)
    {
        int nul = raw.IndexOf((byte)0);
        if (nul >= 0) raw = raw[..nul];
        return PyStrip(DecodeCp932(raw));
    }

    public static string PyStrip(string s)
    {
        int i = 0, j = s.Length;
        while (i < j && IsPythonSpace(s[i])) i++;
        while (j > i && IsPythonSpace(s[j - 1])) j--;
        return s[i..j];
    }

    private static bool IsPythonSpace(char c) => char.IsWhiteSpace(c) || (c >= '\u001c' && c <= '\u001f');

    private static (uint Score, ushort Pair, byte Shot, byte AiByte, byte Lives) StageBody(ReadOnlySpan<byte> decoded, long off)
    {
        var b = Slice(decoded, off, StageBodySize);
        return (BinaryPrimitives.ReadUInt32LittleEndian(b[..4]),
                BinaryPrimitives.ReadUInt16LittleEndian(b.Slice(4, 2)),
                b[6], b[7], b[8]);
    }

    private static int? ShotId(int v) => v is >= 0 and < 16 ? v : null;

    private static int? PartnerShot(int index, IReadOnlyDictionary<int, int> shotByIndex)
    {
        int partner = index < 10 ? index + 10 : index - 10;
        return shotByIndex.TryGetValue(partner, out int shot) ? shot : null;
    }

    private static long Score10(uint v) => (long)v * 10;

    public static ReplayResult DecodeReplay(string path) => DecodeReplayWith(path, raw => DecodeBytes(raw));

    public static ReplayResult DecodeReplayWith(string path, Func<byte[], byte[]> decodeBytes)
    {
        try
        {
            var raw = File.ReadAllBytes(path);
            var decoded = decodeBytes(raw);

            string name = Text(Slice(decoded, HeaderOffName, HeaderLenName));
            string date = Text(Slice(decoded, HeaderOffDate, HeaderLenDate));
            int difficulty = Slice(decoded, HeaderOffDiff, 1)[0];

            var stages = new List<StageInfo>();
            var offsets = StageOffsets(decoded);
            for (int i = 0; i < offsets.Length; i++)
            {
                long off = offsets[i];
                if (off == 0) continue;
                var b = StageBody(decoded, off);

                int? fieldId = null;
                if (off + StageOffFieldId + 1 <= decoded.Length)
                    fieldId = decoded[(int)(off + StageOffFieldId)];
                int? rngSeed = null;
                if (off + StageOffRngSeed + 2 <= decoded.Length)
                    rngSeed = BinaryPrimitives.ReadUInt16LittleEndian(decoded.AsSpan((int)(off + StageOffRngSeed), 2));

                stages.Add(new StageInfo(
                    Index: i,
                    Score: Score10(b.Score),
                    Shot: ShotId(b.Shot),
                    Ai: (b.AiByte & 0x01) != 0,
                    Lives: b.Lives,
                    Pair: b.Pair,
                    RngSeed: rngSeed,
                    FieldId: fieldId,
                    Opponent: null));
            }

            var shotByIndex = new Dictionary<int, int>();
            foreach (var s in stages)
                if (s.Shot is int shot) shotByIndex[s.Index] = shot;
            for (int k = 0; k < stages.Count; k++)
                stages[k] = stages[k] with { Opponent = PartnerShot(stages[k].Index, shotByIndex) };

            bool isMatch = stages.Exists(s => s.Index == 9) || stages.Exists(s => s.Index == 19);
            int mode = isMatch ? 2 : (difficulty == 4 ? 1 : 0);
            int? p1, p2;
            if (isMatch)
            {
                p1 = stages.Find(s => s.Index == 9)?.Shot;
                p2 = stages.Find(s => s.Index == 19)?.Shot;
            }
            else
            {
                p1 = stages.Count > 0 ? stages[0].Shot : null;
                p2 = null;
            }
            var names = isMatch ? MatchPlayerNames(path) : (P1: null, P2: null);

            return new ReplayResult
            {
                Status = "decoded",
                Stages = stages,
                Name = name,
                Date = date,
                Difficulty = difficulty,
                Mode = mode,
                P1Char = p1,
                P2Char = p2,
                P1Name = names.P1,
                P2Name = names.P2,
            };
        }
        catch (Exception e)
        {
            return new ReplayResult { Status = "decode_failed", Error = e.GetType().Name + ": " + e.Message };
        }
    }

    public static (string? P1, string? P2) MatchPlayerNames(string? fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return (null, null);
        string? dir = Path.GetDirectoryName(fullPath);
        string parent = string.IsNullOrEmpty(dir) ? "" : new DirectoryInfo(dir).Name;
        string low = parent.ToLowerInvariant();
        int i = low.IndexOf(" vs ", StringComparison.Ordinal);
        if (i < 0) return (null, null);
        string a = PyStrip(parent[..Math.Min(i, parent.Length)]);
        string b = PyStrip(i + 4 <= parent.Length ? parent[(i + 4)..] : "");
        return (a.Length == 0 ? null : a, b.Length == 0 ? null : b);
    }
}

public sealed record StageInfo(int Index, long? Score, int? Shot, bool Ai, int Lives, int Pair, int? RngSeed, int? FieldId, int? Opponent);

public sealed class ReplayResult
{
    public string Status { get; set; } = "decode_failed";
    public List<StageInfo> Stages { get; set; } = [];
    public string? Error { get; set; }
    public string? Name { get; set; }
    public string? Date { get; set; }
    public int? Difficulty { get; set; }
    public int? Mode { get; set; }
    public int? P1Char { get; set; }
    public int? P2Char { get; set; }
    public string? P1Name { get; set; }
    public string? P2Name { get; set; }

    public bool Decoded => Status == "decoded";
}
