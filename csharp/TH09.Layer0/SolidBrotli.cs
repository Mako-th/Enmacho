using System.Buffers.Binary;
using System.IO.Compression;

namespace TH09.Layer0;

public static class SolidBrotli
{
    public const int Quality = 9;

    public const int Window = 22;

    public const string Encoding = "solid-brotli-v1";

    public const string EncodingV2 = "solid-brotli-v2";

    public static (byte[] Payload, int[] RawLengths) Flatten(byte[] blob)
    {
        var lengths = new List<int>();
        var parts = new List<byte[]>();
        long total = 0;
        int pos = 0;
        while (pos < blob.Length)
        {
            if (pos + 8 > blob.Length)
                throw new InvalidDataException("BLOB が途中で終わっています（チャンク見出しが読めません）");
            uint rawLen = BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(pos));
            uint compLen = BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(pos + 4));
            int start = pos + 8;
            long end = (long)start + compLen;
            if (end > blob.Length)
                throw new InvalidDataException("BLOB が途中で終わっています（チャンク本体が足りません）");
            var raw = TickArchive.ZlibDecompress(blob, start, (int)compLen, (int)rawLen);
            if (raw.Length != rawLen)
                throw new InvalidDataException($"圧縮前バイト数が一致しません: {raw.Length}（記録は {rawLen}）");
            parts.Add(raw);
            lengths.Add(raw.Length);
            total += raw.Length;
            pos = (int)end;
        }
        if (total > int.MaxValue) throw new InvalidDataException("連結後が 2 GiB を超えます: " + total);

        var buf = new byte[total];
        int at = 0;
        foreach (var p in parts) { Buffer.BlockCopy(p, 0, buf, at, p.Length); at += p.Length; }
        return (buf, lengths.ToArray());
    }

    public static byte[][] Split(byte[] payload, int[] rawLengths)
    {
        var chunks = new byte[rawLengths.Length][];
        int at = 0;
        for (int i = 0; i < rawLengths.Length; i++)
        {
            if (at + rawLengths[i] > payload.Length)
                throw new InvalidDataException($"連結した中身が足りません（チャンク {i} / 位置 {at}）");
            chunks[i] = payload[at..(at + rawLengths[i])];
            at += rawLengths[i];
        }
        if (at != payload.Length)
            throw new InvalidDataException($"連結した中身が余っています（使ったのは {at} / 全体は {payload.Length}）");
        return chunks;
    }

    public static byte[][] ChunksOf(byte[] blob)
    {
        var (payload, lengths) = Flatten(blob);
        return Split(payload, lengths);
    }

    public static byte[] Envelope(byte[] payload, int[] rawLengths)
    {
        var buf = new byte[4 + 4 * rawLengths.Length + payload.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(buf, (uint)rawLengths.Length);
        for (int i = 0; i < rawLengths.Length; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(4 + 4 * i), (uint)rawLengths[i]);
        Buffer.BlockCopy(payload, 0, buf, 4 + 4 * rawLengths.Length, payload.Length);
        return buf;
    }

    public static byte[][] OpenEnvelope(byte[] enveloped)
    {
        if (enveloped.Length < 4) throw new InvalidDataException("中身が短すぎます（チャンク数が読めません）");
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(enveloped);
        long head = 4L + 4L * count;
        if (head > enveloped.Length) throw new InvalidDataException($"長さの表が入りきりません（チャンク {count} 本）");
        var lengths = new int[count];
        for (int i = 0; i < count; i++)
        {
            uint v = BinaryPrimitives.ReadUInt32LittleEndian(enveloped.AsSpan(4 + 4 * i));
            if (v > int.MaxValue) throw new InvalidDataException($"チャンクの長さが大きすぎます: {v}");
            lengths[i] = (int)v;
        }
        return Split(enveloped[(int)head..], lengths);
    }

    public static byte[] Compress(ReadOnlySpan<byte> raw, int quality = Quality, int window = Window)
    {
        var dst = new byte[BrotliEncoder.GetMaxCompressedLength(raw.Length)];
        if (!BrotliEncoder.TryCompress(raw, dst, out int written, quality, window))
            throw new InvalidOperationException(
                $"brotli の圧縮に失敗しました（入力 {raw.Length} B / 行き先 {dst.Length} B / q{quality} w{window}）");
        return dst[..written];
    }

    public static byte[] Decompress(ReadOnlySpan<byte> packed, int expected)
    {
        var dst = new byte[expected];
        if (!BrotliDecoder.TryDecompress(packed, dst, out int written))
            throw new InvalidDataException($"brotli の展開に失敗しました（圧縮 {packed.Length} B → 期待 {expected} B）");
        if (written != expected)
            throw new InvalidDataException($"圧縮前バイト数が一致しません: {written}（記録は {expected}）");
        return dst;
    }


    public const int HeaderBytes = 4;

    public const int HeaderBytesV2 = 8;

    public static byte[] Pack(byte[] currentBlob)
    {
        var (payload, lengths) = Flatten(currentBlob);
        var env = Envelope(payload, lengths);
        var packed = Compress(env);
        var buf = new byte[HeaderBytes + packed.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(buf, (uint)env.Length);
        packed.CopyTo(buf.AsSpan(HeaderBytes));
        return buf;
    }

    public static byte[][] Unpack(byte[] blob)
    {
        if (blob.Length < HeaderBytes)
            throw new InvalidDataException($"新形式の BLOB が短すぎます: {blob.Length} B（見出しに {HeaderBytes} B 要る）");
        uint expected = BinaryPrimitives.ReadUInt32LittleEndian(blob);
        if (expected > int.MaxValue)
            throw new InvalidDataException($"展開後のバイト数が大きすぎます: {expected}");
        var env = Decompress(blob.AsSpan(HeaderBytes), (int)expected);
        return OpenEnvelope(env);
    }

    public static byte[] PackV2(byte[] currentBlob)
    {
        var (payload, lengths) = Flatten(currentBlob);
        var env = Envelope(payload, lengths);
        var packed = Compress(env);
        var buf = new byte[HeaderBytesV2 + packed.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(buf, (uint)env.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(4), Crc32.Compute(env));
        packed.CopyTo(buf.AsSpan(HeaderBytesV2));
        return buf;
    }

    public static byte[][] UnpackV2(byte[] blob)
    {
        if (blob.Length < HeaderBytesV2)
            throw new InvalidDataException(
                $"新形式（v2）の BLOB が短すぎます: {blob.Length} B（見出しに {HeaderBytesV2} B 要る）");
        uint expected = BinaryPrimitives.ReadUInt32LittleEndian(blob);
        uint expectedCrc = BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(4));
        if (expected > int.MaxValue)
            throw new InvalidDataException($"展開後のバイト数が大きすぎます: {expected}");
        var env = Decompress(blob.AsSpan(HeaderBytesV2), (int)expected);
        uint actualCrc = Crc32.Compute(env);
        if (actualCrc != expectedCrc)
            throw new InvalidDataException(
                $"CRC-32 が一致しません（記録は 0x{expectedCrc:X8} / 実測は 0x{actualCrc:X8}）"
                + "——本体が壊れています");
        return OpenEnvelope(env);
    }

    public static byte[] UpgradeToV2(byte[] v1Blob)
    {
        if (v1Blob.Length < HeaderBytes)
            throw new InvalidDataException(
                $"v1 の BLOB が短すぎます: {v1Blob.Length} B（見出しに {HeaderBytes} B 要る）");
        uint expected = BinaryPrimitives.ReadUInt32LittleEndian(v1Blob);
        if (expected > int.MaxValue)
            throw new InvalidDataException($"展開後のバイト数が大きすぎます: {expected}");
        var packedBrotli = v1Blob.AsSpan(HeaderBytes);
        var env = Decompress(packedBrotli, (int)expected);
        uint crc = Crc32.Compute(env);
        var buf = new byte[HeaderBytesV2 + packedBrotli.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(buf, expected);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(4), crc);
        packedBrotli.CopyTo(buf.AsSpan(HeaderBytesV2));
        return buf;
    }

    public static (byte[] NewBlob, int Chunks) PackVerified(byte[] currentBlob)
    {
        var before = ChunksOf(currentBlob);
        var packed = Pack(currentBlob);
        RequireSameChunks(before, Unpack(packed));
        return (packed, before.Length);
    }

    public static (byte[] NewBlob, int Chunks) PackVerifiedV2(byte[] currentBlob)
    {
        var before = ChunksOf(currentBlob);
        var packed = PackV2(currentBlob);
        RequireSameChunks(before, UnpackV2(packed));
        return (packed, before.Length);
    }

    public static void RequireSameChunks(IReadOnlyList<byte[]> before, IReadOnlyList<byte[]> after)
    {
        if (before.Count != after.Count)
            throw new InvalidDataException($"チャンク数が変わりました: {before.Count} → {after.Count}");
        for (int i = 0; i < before.Count; i++)
        {
            if (!before[i].AsSpan().SequenceEqual(after[i]))
                throw new InvalidDataException(
                    $"チャンク {i} の中身が変わりました（{before[i].Length} B → {after[i].Length} B）");
        }
    }
}
