using TH09.Generated;

namespace TH09.ProcView;

public sealed class GameExtraReader(IGameMemory memory)
{
    private readonly Dictionary<int, int[]> _cpuCache = [];

    public static int FieldCount
        => GameAddresses.Carried.Count + GameAddresses.CpuSettings.Fields.Length;

    public void Reset() => _cpuCache.Clear();

    public IReadOnlyList<(string Name, long Value)> Read(long? p1CpuLevel, long? p2CpuLevel)
    {
        var got = new List<(string, long)>(FieldCount);
        foreach (var (name, address) in GameAddresses.Carried.Fields)
        {
            if (memory.TryReadInt32(address, out var raw)) got.Add((name, (uint)raw));
        }

        var columns = GameAddresses.CpuSettings.ColumnCount;
        for (var side = 0; side < 2; side++)
        {
            var row = CpuSettings(side == 0 ? p1CpuLevel : p2CpuLevel);
            if (row is null) continue;
            for (var col = 0; col < columns; col++)
                got.Add((GameAddresses.CpuSettings.Fields[side * columns + col], row[col]));
        }
        return got;
    }

    private int[]? CpuSettings(long? level)
    {
        var columns = GameAddresses.CpuSettings.ColumnCount;
        if (level is not long lv
            || lv < GameAddresses.CpuSettings.MinLevel
            || lv > GameAddresses.CpuSettings.MaxLevel)
            return new int[columns];

        var key = (int)lv;
        if (_cpuCache.TryGetValue(key, out var cached)) return cached;

        var rowBase = GameAddresses.CpuSettings.TableBase
                      + (uint)(key * GameAddresses.CpuSettings.Stride);
        var row = new int[columns];
        for (var col = 0; col < columns; col++)
        {
            var at = rowBase + (uint)GameAddresses.CpuSettings.ColumnOffsets[col];
            if (!memory.TryReadInt32(at, out row[col])) return null;
        }
        _cpuCache[key] = row;
        return row;
    }
}
