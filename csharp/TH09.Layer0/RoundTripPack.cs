using System.Buffers.Binary;
using System.Text;

namespace TH09.Layer0;

public static class RoundTripPack
{
    public static readonly byte[] Magic = Encoding.ASCII.GetBytes("TH09L0RT");
    public const int FormatVersion = 1;

    public sealed class Record
    {
        public required string Kind { get; init; }
        public required long SessionId { get; init; }
        public required long No { get; init; }
        public required string Label { get; init; }
        public required byte[] FieldOrderUtf8 { get; init; }
        public required byte[] Blob { get; init; }
        public string FieldOrderEncoding { get; init; } = "";
        public byte[] FieldOrderPacked { get; init; } = [];
        public Dictionary<string, uint[]>? Expected { get; init; }
    }

    public static void Write(string path, IReadOnlyList<Record> records)
    {
        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs, new UTF8Encoding(false), leaveOpen: true);
        w.Write(Magic);
        WriteU32(w, FormatVersion);
        WriteU32(w, (uint)records.Count);
        foreach (var r in records)
        {
            WriteBytes(w, Encoding.UTF8.GetBytes(r.Kind));
            WriteI64(w, r.SessionId);
            WriteI64(w, r.No);
            WriteBytes(w, Encoding.UTF8.GetBytes(r.Label));
            WriteBytes(w, r.FieldOrderUtf8);
            WriteBytes(w, Encoding.UTF8.GetBytes(r.FieldOrderEncoding));
            WriteBytes(w, r.FieldOrderPacked);
            WriteBytes(w, r.Blob);
            if (r.Expected is null) { w.Write((byte)0); continue; }
            w.Write((byte)1);
            WriteU32(w, (uint)r.Expected.Count);
            foreach (var name in r.Expected.Keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                var col = r.Expected[name];
                WriteBytes(w, Encoding.UTF8.GetBytes(name));
                WriteU32(w, (uint)col.Length);
                var raw = new byte[col.Length * 4];
                if (BitConverter.IsLittleEndian) Buffer.BlockCopy(col, 0, raw, 0, raw.Length);
                else for (int i = 0; i < col.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(i * 4), col[i]);
                w.Write(raw);
            }
        }
    }

    private static void WriteU32(BinaryWriter w, uint v)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, v);
        w.Write(b);
    }

    private static void WriteI64(BinaryWriter w, long v)
    {
        Span<byte> b = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(b, v);
        w.Write(b);
    }

    private static void WriteBytes(BinaryWriter w, byte[] data)
    {
        WriteU32(w, (uint)data.Length);
        w.Write(data);
    }
}
