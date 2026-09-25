namespace TH09.TickBus;

public static class CoordLayout
{
    public const string DefaultName = @"Local\TH09TickCoord";

    public const uint Magic = 0x43543954u;
    public const uint Version = 10u;
    public const int HeaderSize = 256;
    public const int RecordSize = 44040;
    public const int Capacity = 8192;

    public const int BulletSlots = 537;
    public const int EnemySlots = 128;
    public const int LaserSlots = 48;
    public const int ExSlots = 256;
    public const int ShotSlots = 128;
    public const int BlastSlots = 32;
    public const int HitlistSlots = 128;

    public const int SideSlots = BulletSlots + EnemySlots;

    public const int LaserTotal = 2 * LaserSlots;

    public const int ShotTotal = 2 * ShotSlots;

    public const int HitlistTotal = 2 * HitlistSlots;

    public const int Slots = 2 * SideSlots + LaserTotal + ExSlots + ShotTotal;

    public const uint IndexMask = Capacity - 1u;
    public const long TotalSize = HeaderSize + (long)RecordSize * Capacity;

    public static readonly (string Name, int Offset)[] HeaderFields =
    [
        ("magic", 0x00),
        ("version", 0x04),
        ("record_size", 0x08),
        ("capacity", 0x0C),
        ("write_index", 0x10),
        ("coord_state", 0x14),
        ("last_error", 0x18),
        ("game_pid", 0x1C),
        ("slot_count", 0x20),
        ("enable", 0x24),
        ("coord_ticks", 0x28),
        ("skipped_ticks", 0x2C),
        ("origin_hits", 0x30),
        ("origin_dropped", 0x34),
        ("origin_error", 0x38),
        ("enemy_kind_clash", 0x3C),
        ("blast_hits", 0x40),
        ("blast_dropped", 0x44),
        ("blast_error", 0x48),
        ("hitlist_clamped", 0x4C),
        ("laser_origin_hits", 0x50),
        ("laser_origin_dropped", 0x54),
        ("laser_origin_error", 0x58),
    ];

    public static readonly (string Name, int Offset)[] RecordFields =
    [
        ("seq_begin", 0x0000),
        ("flags", 0x0004),
        ("x", 0x0008),
        ("y", 0x1E50),
        ("kind", 0x3C98),
        ("laser_angle", 0x5AE0),
        ("laser_tail", 0x5C60),
        ("laser_head", 0x5DE0),
        ("laser_width", 0x5F60),
        ("laser_timer", 0x60E0),
        ("laser_gate0", 0x6260),
        ("laser_gate2", 0x63E0),
        ("laser_phase", 0x6560),
        ("ex_timer", 0x66E0),
        ("ex_side", 0x6AE0),
        ("ex_variant", 0x6EE0),
        ("shot_w", 0x72E0),
        ("shot_h", 0x76E0),
        ("blast_x", 0x7AE0),
        ("blast_y", 0x7B60),
        ("blast_kind", 0x7BE0),
        ("blast_word", 0x7C60),
        ("hitlist_x", 0x7CE0),
        ("hitlist_y", 0x80E0),
        ("hitlist_pivot_x", 0x84E0),
        ("hitlist_pivot_y", 0x88E0),
        ("hitlist_half_x", 0x8CE0),
        ("hitlist_half_y", 0x90E0),
        ("hitlist_radius", 0x94E0),
        ("hitlist_angle", 0x98E0),
        ("state", 0x9CE0),
        ("seq_end", 0xAC04),
    ];

    public static readonly (string Name, int Offset, int Slots, bool IsU16)[] ColumnBlocks = MakeBlocks();

    private static (string, int, int, bool)[] MakeBlocks()
    {
        int Off(string name)
        {
            foreach (var (n, o) in RecordFields)
                if (string.Equals(n, name, StringComparison.Ordinal)) return o;
            throw new TickBusLayoutException($"座標リングに {name} という区画がありません");
        }
        var list = new List<(string, int, int, bool)>
        {
            ("x", Off("x"), Slots, false),
            ("y", Off("y"), Slots, false),
            ("kind", Off("kind"), Slots, false),
            ("state", Off("state"), Slots, true),
        };
        foreach (var n in (string[])["laser_angle", "laser_tail", "laser_head", "laser_width",
                                     "laser_timer", "laser_gate0", "laser_gate2", "laser_phase"])
            list.Add((n, Off(n), LaserTotal, false));
        foreach (var n in (string[])["ex_timer", "ex_side", "ex_variant"])
            list.Add((n, Off(n), ExSlots, false));
        foreach (var n in (string[])["shot_w", "shot_h"])
            list.Add((n, Off(n), ShotTotal, false));
        list.Add(("flags", Off("flags"), 1, false));
        foreach (var n in (string[])["blast_x", "blast_y", "blast_kind", "blast_word"])
            list.Add((n, Off(n), BlastSlots, false));
        foreach (var n in (string[])["hitlist_x", "hitlist_y", "hitlist_pivot_x", "hitlist_pivot_y",
                                     "hitlist_half_x", "hitlist_half_y", "hitlist_radius",
                                     "hitlist_angle"])
            list.Add((n, Off(n), HitlistTotal, false));
        return [.. list];
    }

    public static long RecordOffset(uint index) => HeaderSize + (long)(index & IndexMask) * RecordSize;

    public static (string Field, string Message)? RejectReason(CoordBusHeader h)
    {
        if (h.Magic != Magic) return ("magic", $"magic が違います: {h.Magic}（期待 {Magic}）");
        if (h.Version != Version) return ("version", $"version が違います: {h.Version}（期待 {Version}）");
        if (h.RecordSizeWord != RecordSize) return ("record_size", $"record_size が違います: {h.RecordSizeWord}（期待 {RecordSize}）");
        if (h.CapacityWord != Capacity) return ("capacity", $"capacity が違います: {h.CapacityWord}（期待 {Capacity}）");
        if (h.SlotCount != Slots) return ("slot_count", $"slot_count が違います: {h.SlotCount}（期待 {Slots}）");
        return null;
    }

    public static void VerifyHeader(CoordBusHeader h)
    {
        var bad = RejectReason(h);
        if (bad is not null)
            throw new TickBusLayoutException($"座標リングの {bad.Value.Message}。DLL と Python の世代が食い違っています。");
    }

    public static void SelfCheck()
    {
        int prev = -1;
        foreach (var (name, off) in RecordFields)
        {
            if (off % 4 != 0) throw new TickBusLayoutException($"座標 {name}: オフセット 0x{off:X} が 4 の倍数ではありません");
            if (off <= prev) throw new TickBusLayoutException($"座標 {name}: オフセットが昇順ではありません");
            prev = off;
        }
        int seqEnd = RecordFields[^1].Offset;
        if (seqEnd + 4 != RecordSize)
            throw new TickBusLayoutException($"座標レコードの seq_end がレコード末尾に接していません（0x{seqEnd:X} + 4 != {RecordSize}）");
        long sum = 4 + 4 + 4;
        foreach (var (name, _off, slots, isU16) in ColumnBlocks)
        {
            if (string.Equals(name, "flags", StringComparison.Ordinal)) continue;
            sum += (long)slots * (isU16 ? 2 : 4);
        }
        if (sum != RecordSize)
            throw new TickBusLayoutException($"座標レコードの区画の合計が {sum} B で、RecordSize {RecordSize} B と合いません");
        if ((Capacity & (int)IndexMask) != 0)
            throw new TickBusLayoutException($"COORDBUS_CAPACITY は 2 の冪でなければなりません: {Capacity}");
    }
}

public readonly struct CoordBusHeader(uint[] words)
{
    public uint[] Words { get; } = words;

    public uint At(int byteOffset) => Words[byteOffset >> 2];

    public uint Magic => At(0x00);
    public uint Version => At(0x04);
    public uint RecordSizeWord => At(0x08);
    public uint CapacityWord => At(0x0C);
    public uint WriteIndex => At(0x10);
    public uint CoordState => At(0x14);
    public uint LastError => At(0x18);
    public uint GamePid => At(0x1C);
    public uint SlotCount => At(0x20);

    public uint Enable => At(0x24);

    public uint CoordTicks => At(0x28);
    public uint SkippedTicks => At(0x2C);
}
