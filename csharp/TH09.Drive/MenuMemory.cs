namespace TH09.Drive;

public interface IMenuMemory
{
    bool TryReadUInt32(uint address, out uint value, out string error);

    bool TryReadBytes(uint address, int size, out byte[] value, out string error);
}
