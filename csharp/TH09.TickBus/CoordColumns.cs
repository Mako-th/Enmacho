using System.Buffers.Binary;
using System.Security.Cryptography;

namespace TH09.TickBus;

public static class CoordColumns
{
    public static uint[] Column(ReadOnlySpan<byte> raw, int count, in (string Name, int Offset, int Slots, bool IsU16) block, int slot)
    {
        if (slot < 0 || slot >= block.Slots)
            throw new TickBusLayoutException($"{block.Name}: 枠 {slot} は 0..{block.Slots - 1} の外です");
        var outv = new uint[count];
        int stride = CoordLayout.RecordSize;
        for (int t = 0; t < count; t++)
        {
            int at = t * stride + block.Offset + slot * (block.IsU16 ? 2 : 4);
            outv[t] = block.IsU16
                ? BinaryPrimitives.ReadUInt16LittleEndian(raw[at..])
                : BinaryPrimitives.ReadUInt32LittleEndian(raw[at..]);
        }
        return outv;
    }

    public static Dictionary<string, string> BlockDigests(ReadOnlySpan<byte> raw, int count)
    {
        var outMap = new Dictionary<string, string>(StringComparer.Ordinal);
        int stride = CoordLayout.RecordSize;
        foreach (var block in CoordLayout.ColumnBlocks)
        {
            var buf = new byte[(long)block.Slots * count * 4];
            int w = 0;
            for (int slot = 0; slot < block.Slots; slot++)
            {
                for (int t = 0; t < count; t++)
                {
                    int at = t * stride + block.Offset + slot * (block.IsU16 ? 2 : 4);
                    uint v = block.IsU16
                        ? BinaryPrimitives.ReadUInt16LittleEndian(raw[at..])
                        : BinaryPrimitives.ReadUInt32LittleEndian(raw[at..]);
                    BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(w), v);
                    w += 4;
                }
            }
            outMap[block.Name] = Convert.ToHexStringLower(SHA256.HashData(buf));
        }
        return outMap;
    }

    public static string Sha256Hex(ReadOnlySpan<byte> data) => Convert.ToHexStringLower(SHA256.HashData(data));
}
