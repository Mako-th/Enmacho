using TH09.ProcView;

namespace TH09.Drive;

public sealed class ProcViewMenuMemory : IMenuMemory
{
    private readonly IGameMemory _memory;

    public ProcViewMenuMemory(IGameMemory memory)
    {
        ArgumentNullException.ThrowIfNull(memory);
        _memory = memory;
    }

    public bool TryReadUInt32(uint address, out uint value, out string error)
    {
        if (_memory.TryReadInt32(address, out int raw))
        {
            value = unchecked((uint)raw);
            error = "";
            return true;
        }
        value = 0;
        error = Reason(address, 4);
        return false;
    }

    public bool TryReadBytes(uint address, int size, out byte[] value, out string error)
    {
        if (_memory.TryReadBytes(address, size, out value))
        {
            error = "";
            return true;
        }
        value = [];
        error = Reason(address, size);
        return false;
    }

    private static string Reason(uint address, int size)
        => "読めません: " + MenuMap.Hex8(address) + " から " + size.ToString(
               System.Globalization.CultureInfo.InvariantCulture) + " バイト";
}
