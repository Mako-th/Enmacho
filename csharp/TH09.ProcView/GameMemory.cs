using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using TH09.ProcView.Interop;

namespace TH09.ProcView;

public interface IGameMemory
{
    bool TryReadInt32(uint address, out int value);

    bool TryReadBytes(uint address, int size, out byte[] value);
}

[SupportedOSPlatform("windows")]
public sealed class ProcessMemory : IGameMemory, IDisposable
{
    private nint _handle;

    private ProcessMemory(nint handle, int pid)
    {
        _handle = handle;
        Pid = pid;
    }

    public int Pid { get; }

    public bool IsOpen => _handle != 0;

    public static ProcessMemory? TryOpen(int pid, out int error)
    {
        error = 0;
        if (pid <= 0) return null;
        var handle = NativeMethods.OpenProcess(
            NativeMethods.ProcessVmRead | NativeMethods.ProcessQueryLimitedInformation,
            0, (uint)pid);
        if (handle == 0)
        {
            error = Marshal.GetLastWin32Error();
            return null;
        }
        return new ProcessMemory(handle, pid);
    }

    public bool TryReadInt32(uint address, out int value)
    {
        value = 0;
        Span<byte> word = stackalloc byte[sizeof(int)];
        if (!TryRead(address, word)) return false;
        value = BinaryPrimitives.ReadInt32LittleEndian(word);
        return true;
    }

    public bool TryReadBytes(uint address, int size, out byte[] value)
    {
        value = [];
        if (size <= 0) return false;
        var buffer = new byte[size];
        if (!TryRead(address, buffer)) return false;
        value = buffer;
        return true;
    }

    private bool TryRead(uint address, Span<byte> buffer)
    {
        if (_handle == 0 || buffer.Length == 0) return false;
        var ok = NativeMethods.ReadProcessMemory(_handle, (nint)address, ref buffer[0],
                                                 (nuint)buffer.Length, out var read);
        if (ok != 0 && read == (nuint)buffer.Length) return true;
        buffer.Clear();
        return false;
    }

    public void Dispose()
    {
        var handle = _handle;
        _handle = 0;
        if (handle != 0) NativeMethods.CloseHandle(handle);
        GC.SuppressFinalize(this);
    }

    ~ProcessMemory() => Dispose();
}
