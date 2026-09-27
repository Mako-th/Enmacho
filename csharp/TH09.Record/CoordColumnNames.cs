using System.Security.Cryptography;
using TH09.Record.Generated;
using TH09.TickBus;

namespace TH09.Record;

public static class CoordColumnNames
{
    public static readonly IReadOnlyList<string> SlotBases = MakeSlotBases();

    public static readonly int LaserBase = 2 * CoordLayout.SideSlots;

    public static readonly int ExBase = 2 * CoordLayout.SideSlots + CoordLayout.LaserTotal;

    public static readonly int ShotBase =
        2 * CoordLayout.SideSlots + CoordLayout.LaserTotal + CoordLayout.ExSlots;

    public static readonly IReadOnlyList<string> BlastBases =
        [.. Enumerable.Range(0, CoordLayout.BlastSlots).Select(i => "blast" + i.ToString())];

    public static readonly IReadOnlyList<string> HitlistBases =
        [.. Enumerable.Range(0, 2).SelectMany(side =>
               Enumerable.Range(0, CoordLayout.HitlistSlots)
                         .Select(i => $"p{side + 1}_h{i}"))];

    public static readonly IReadOnlyList<string[]> Groups = MakeGroups();

    public static readonly IReadOnlyList<string> All = [.. Groups.SelectMany(g => g)];

    public static string[] SlotColumns(int slot)
    {
        if (slot < 0 || slot >= CoordLayout.Slots)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "共有スロットの外です");
        var b = SlotBases[slot];
        return [b + "_x", b + "_y", b + "_kind", b + "_state"];
    }

    public static Dictionary<string, uint[]> WindowColumns(ReadOnlySpan<byte> raw, int count)
    {
        var names = All;
        var cols = new uint[names.Count][];
        for (int i = 0; i < cols.Length; i++) cols[i] = new uint[count];

        int stride = CoordLayout.RecordSize;
        for (int t = 0; t < count; t++)
        {
            int at = 0;
            var rowSpan = raw.Slice(t * stride, stride);
            foreach (var block in CoordLayout.ColumnBlocks)
            {
                for (int slot = 0; slot < block.Slots; slot++)
                {
                    int off = block.Offset + slot * (block.IsU16 ? 2 : 4);
                    cols[at++][t] = block.IsU16
                        ? System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(rowSpan[off..])
                        : System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(rowSpan[off..]);
                }
            }
            if (at != names.Count)
                throw new InvalidDataException(
                    $"★区画から出た列が {at} 本（名前は {names.Count} 本）。区画と名前の並びが 1 対 1 ではありません。");
        }

        var outMap = new Dictionary<string, uint[]>(names.Count, StringComparer.Ordinal);
        for (int i = 0; i < names.Count; i++) outMap[names[i]] = cols[i];
        return outMap;
    }

    public static void SelfCheck()
    {
        if (All.Count != HitWindowConst.ColumnNameCount)
            throw new InvalidDataException(
                $"★保存する列が {All.Count} 本（Python 側は {HitWindowConst.ColumnNameCount} 本）。");
        var got = Fingerprint();
        if (!string.Equals(got, HitWindowConst.ColumnNamesSha256, StringComparison.Ordinal))
            throw new InvalidDataException(
                $"★列名の並びが Python 側と違います（こちら {got[..12]}… / あちら {HitWindowConst.ColumnNamesSha256[..12]}…）。");
        var dup = All.GroupBy(x => x, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (dup is not null)
            throw new InvalidDataException($"★列名 {dup.Key} が {dup.Count()} 本あります（名前で引くので片方が黙って消えます）。");
    }

    public static string Fingerprint() =>
        Convert.ToHexStringLower(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(string.Join("\n", All))));


    private static string[] MakeSlotBases()
    {
        var names = new List<string>(CoordLayout.Slots);
        for (int side = 1; side <= 2; side++)
        {
            for (int i = 0; i < CoordLayout.BulletSlots; i++) names.Add($"p{side}_b{i}");
            for (int i = 0; i < CoordLayout.EnemySlots; i++) names.Add($"p{side}_e{i}");
        }
        for (int side = 1; side <= 2; side++)
            for (int i = 0; i < CoordLayout.LaserSlots; i++) names.Add($"p{side}_l{i}");
        for (int i = 0; i < CoordLayout.ExSlots; i++) names.Add($"ex{i}");
        for (int side = 1; side <= 2; side++)
            for (int i = 0; i < CoordLayout.ShotSlots; i++) names.Add($"p{side}_s{i}");
        if (names.Count != CoordLayout.Slots)
            throw new InvalidDataException($"★共有スロットが {names.Count} 本（期待 {CoordLayout.Slots} 本）。");
        return [.. names];
    }

    private static string SuffixOf(string blockName)
    {
        int us = blockName.IndexOf('_');
        return "_" + (us >= 0 ? blockName[(us + 1)..] : blockName);
    }

    private static string[][] MakeGroups()
    {
        var groups = new List<string[]>();
        foreach (var block in CoordLayout.ColumnBlocks)
        {
            if (string.Equals(block.Name, "flags", StringComparison.Ordinal))
            {
                groups.Add([HitWindowConst.CoordFlagsColumn]);
                continue;
            }
            var suffix = SuffixOf(block.Name);
            IReadOnlyList<string> bases;
            if (block.Name.StartsWith("laser_", StringComparison.Ordinal))
                bases = Span(LaserBase, CoordLayout.LaserTotal);
            else if (block.Name.StartsWith("ex_", StringComparison.Ordinal))
                bases = Span(ExBase, CoordLayout.ExSlots);
            else if (block.Name.StartsWith("shot_", StringComparison.Ordinal))
                bases = Span(ShotBase, CoordLayout.ShotTotal);
            else if (block.Name.StartsWith("blast_", StringComparison.Ordinal))
                bases = BlastBases;
            else if (block.Name.StartsWith("hitlist_", StringComparison.Ordinal))
                bases = HitlistBases;
            else
                bases = SlotBases;
            if (bases.Count != block.Slots)
                throw new InvalidDataException(
                    $"★区画 {block.Name} は {block.Slots} 枠なのに基底名が {bases.Count} 本です。");
            groups.Add([.. bases.Select(b => b + suffix)]);
        }
        return [.. groups];
    }

    private static List<string> Span(int from, int count) =>
        [.. SlotBases.Skip(from).Take(count)];
}
